using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SankalpAPI.Helper;
using SankalpAPI.Interface;
using SankalpAPI.Model;
using SankalpAPI.Security;

namespace SankalpAPI.Services
{
    /// <summary>
    /// Service to communicate with external portal APIs to fetch applicant details,
    /// or execute OTP-based applicant registration and portal mapping when portal URL is unavailable.
    /// Returns standardized { status, message, data } format.
    /// </summary>
    public class ApplicantService : IApplicantService
    {
        private const string SendOtpEndpoint = "/api/Sankalp/send-otp";
        private const string VerifyOtpEndpoint = "/api/Sankalp/verify-otp";
        private const string GetApplicantListEndpoint = "/api/Sankalp/GetApplicantList";
        private const string GetApplicantFullDetailsEndpoint = "/api/Sankalp/GetApplicantDetails";
        private const string GetBeneficiaryMessagesEndpoint = "/api/Beneficiary/Get_Beneficiary_Messages";
        private const string Get_Beneficiary_Status = "/api/Beneficiary/Get_Beneficiary_Status";
        private const string DefaultPortalUrl = "http://10.133.20.151/eLabharthi";
        private const int DefaultTimeoutSeconds = 15;
        private const int OtpResendCooldownSeconds = 30;
        private const int MaxOtpRequestsPerWindow = 5;
        private const int OtpRateLimitWindowMinutes = 10;
        private const int MaxOtpVerifyRetryAttempts = 5;

        private readonly IPortalRepository _portalRepository;
        private readonly IApplicantRepository _applicantRepository;
        private readonly ISmsNotificationService _smsService;
        private readonly ClientTokenService _clientTokenService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ApplicantService> _logger;
        private readonly ExceptionLogging _exceptionLogging;
        private readonly IConfiguration _configuration;

        public ApplicantService(
            IPortalRepository portalRepository, 
            IApplicantRepository applicantRepository, 
            ISmsNotificationService smsService,
            ClientTokenService clientTokenService,
            IHttpClientFactory httpClientFactory, 
            ILogger<ApplicantService> logger, 
            ExceptionLogging exceptionLogging,
            IConfiguration configuration)
        {
            _portalRepository = portalRepository;
            _applicantRepository = applicantRepository;
            _smsService = smsService;
            _clientTokenService = clientTokenService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _exceptionLogging = exceptionLogging;
            _configuration = configuration;
        }

        public async Task<ApplicantResponse> GetApplicantDetailsAsync(ApplicantRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "Request payload is required.",
                    Data = null
                };
            }

            string aesKey;
            try
            {
                aesKey = RSA_Enc_Dec.Decryption(request.ClientKey);
                if (Encoding.UTF8.GetByteCount(aesKey) is not (16 or 24 or 32))
                    throw new CryptographicException("Invalid AES key length.");
            }
            catch (Exception)
            {
                return new ApplicantResponse { Status = false, StatusCode = 400,
                    Message = "A valid RSA-encrypted client_key is required.", Data = null };
            }

            string mobileNo = NormalizeMobileNumber(request.MobileNo);
            string deviceId = request.DeviceId?.Trim() ?? string.Empty;
            string session = request.Session?.Trim() ?? string.Empty;
            string userType = request.UserType?.Trim() ?? string.Empty;

            // Validate mandatory parameters
            if (request.PortalId < 0)
            {
                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "A valid portal_id is required.",
                    Data = null
                };
            }

            if (string.IsNullOrWhiteSpace(mobileNo) || !IsValidMobileNumber(mobileNo))
            {
                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "A valid 10-digit mobile number is required.",
                    Data = null
                };
            }

            try
            {
                // 4. Resolve portal information from database (mst_portal & tbl_map_user_portal)
                var mapping = await ResolvePortalMappingAsync(request.PortalId, mobileNo);
                if (mapping == null)
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        StatusCode = (int)HttpStatusCode.NotFound,
                        Message = $"Portal ID {request.PortalId} was not found in mst_portal.",
                        Data = null
                    };
                }

                // 5. Validate portal active state
                if (!string.IsNullOrWhiteSpace(mapping.IsActive) &&
                    (mapping.IsActive.Equals("N", StringComparison.OrdinalIgnoreCase) || mapping.IsActive == "0"))
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Message = $"Portal '{mapping.PortalName}' (ID {mapping.PortalId}) is currently marked inactive.",
                        Data = null
                    };
                }

                // 6. Step 1: Check Portal URL Availability
                // If portal URL is not configured/available in mst_portal, invoke OTP-based applicant registration flow
                if (!IsPortalUrlAvailable(mapping.PortalUrl))
                {
                    return await InitiateOtpApplicantRegistrationAsync(mapping, mobileNo, deviceId, session, userType, cancellationToken);
                }

                // 7. Construct target external API URL (existing portal-available flow unchanged)
                string targetUrl = BuildTargetUrl(mapping.PortalUrl, SendOtpEndpoint);
                if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out var targetUri))
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Message = $"Invalid portal URL configured for portal '{mapping.PortalName}': '{targetUrl}'",
                        Data = null
                    };
                }

                // 7. Build HTTP Request
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, targetUri);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Inject Bearer token from user-portal mapping if available
                if (!string.IsNullOrWhiteSpace(mapping.Token))
                {
                    httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {mapping.Token.Trim()}");
                }

                // Inject Secret Key from user-portal mapping if available
                if (!string.IsNullOrWhiteSpace(mapping.SecretKey))
                {
                    httpRequest.Headers.TryAddWithoutValidation("X-Secret-Key", mapping.SecretKey.Trim());
                }

                // Correlation ID for distributed tracing
                httpRequest.Headers.TryAddWithoutValidation("X-Correlation-ID", Guid.NewGuid().ToString("N"));

                var forwardPayload = new Dictionary<string, object?>
                {
                    ["clientKey"] = request.ClientKey,
                    ["portalId"] = Aes256CbcEncrypterApp.Encrypt(request.PortalId.ToString(System.Globalization.CultureInfo.InvariantCulture), aesKey),
                    ["mobile"] = Aes256CbcEncrypterApp.Encrypt(mobileNo, aesKey),
                    ["deviceId"] = Aes256CbcEncrypterApp.Encrypt(deviceId, aesKey),
                    ["userType"] = Aes256CbcEncrypterApp.Encrypt(userType, aesKey)
                };

                string jsonContent = JsonConvert.SerializeObject(forwardPayload);
                httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // 8. Execute external HTTP call
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

                _logger.LogInformation("Accessing external API for portal '{PortalName}' (ID {PortalId}) at {TargetUrl} for mobile {Mobile}",
                    mapping.PortalName, mapping.PortalId, targetUri, mobileNo);

                using var response = await client.SendAsync(httpRequest, cancellationToken);
                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                int statusCode = (int)response.StatusCode;
                _logger.LogInformation("External portal response received with status code {StatusCode}", statusCode);

                // 8. Handle HTTP Response
                if (response.IsSuccessStatusCode)
                {
                    bool isStatus = true;
                    string message = "Applicant details retrieved successfully.";
                    object? dataPayload = null;

                    if (!string.IsNullOrWhiteSpace(responseBody))
                    {
                        try
                        {
                            var token = JToken.Parse(responseBody);
                            if (token is JObject obj)
                            {
                                if (obj["status"] != null)
                                {
                                    isStatus = obj["status"]!.Value<bool>();
                                }
                                if (obj["message"] != null)
                                {
                                    message = obj["message"]!.ToString();
                                }
                            }
                        }
                        catch
                        {
                        }
                    }

                    // Extract strongly-typed beneficiary records
                    var beneficiaries = ExtractBeneficiaries(responseBody);
                    if (beneficiaries != null && beneficiaries.Count > 0)
                    {
                        dataPayload = beneficiaries;

                        if (isStatus)
                        {
                            try
                            {
                                await _applicantRepository.SaveBeneficiaryDetailsAsync(mapping.PortalId, mobileNo, deviceId, session, userType, beneficiaries, responseBody);

                                _logger.LogInformation("Saved {Count} beneficiary record(s) to database for mobile {Mobile}",
                                    beneficiaries.Count, mobileNo);
                            }
                            catch (Exception dbEx)
                            {
                                _logger.LogError(dbEx, "Failed to save beneficiary records to database for mobile {Mobile}", mobileNo);
                                _exceptionLogging.SendErrorToText(dbEx);
                            }
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(responseBody))
                    {
                        // Fallback: parse using System.Text.Json so it serializes cleanly without JToken nesting
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
                            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && doc.RootElement.TryGetProperty("data", out var dataProp))
                            {
                                dataPayload = dataProp.Clone();
                            }
                            else
                            {
                                dataPayload = doc.RootElement.Clone();
                            }
                        }
                        catch
                        {
                            dataPayload = new List<object>();
                        }
                    }
                    else
                    {
                        dataPayload = new List<object>();
                    }

                    return new ApplicantResponse
                    {
                        Status = isStatus,
                        StatusCode = statusCode,
                        Message = message,
                        Data = dataPayload
                    };
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    string notFoundMessage = "Applicant details not found on external portal.";
                    try
                    {
                        var token = JToken.Parse(responseBody);
                        if (token is JObject obj && obj["message"] != null)
                        {
                            notFoundMessage = obj["message"]!.ToString();
                        }
                    }
                    catch { }

                    return new ApplicantResponse
                    {
                        Status = false,
                        StatusCode = (int)HttpStatusCode.NotFound,
                        Message = notFoundMessage,
                        Data = null
                    };
                }

                string errMessage = $"External portal returned HTTP {statusCode}.";
                try
                {
                    var token = JToken.Parse(responseBody);
                    if (token is JObject obj && obj["message"] != null)
                    {
                        errMessage = obj["message"]!.ToString();
                    }
                }
                catch { }

                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = statusCode,
                    Message = errMessage,
                    Data = null
                };
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Timeout connecting to external portal ID {PortalId}", request.PortalId);
                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = (int)HttpStatusCode.GatewayTimeout,
                    Message = "Gateway Timeout: The external portal took too long to respond.",
                    Data = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to external portal ID {PortalId}", request.PortalId);
                _exceptionLogging.SendErrorToText(ex);

                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = (int)HttpStatusCode.BadGateway,
                    Message = $"Bad Gateway: Unable to connect to external portal API ({ex.Message})",
                    Data = null
                };
            }
        }

        public async Task<ApplicantResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "Request payload is required.",
                    Data = null
                };
            }

            // 1. Decrypt parameters if encrypted
            string clientKey;
            try
            {
                clientKey = RSA_Enc_Dec.Decryption(request.ClientKey ?? string.Empty);
                if (Encoding.UTF8.GetByteCount(clientKey) is not (16 or 24 or 32))
                    throw new CryptographicException("Invalid AES key length.");
            }
            catch (Exception)
            {
                return new ApplicantResponse { Status = false, StatusCode = 400,
                    Message = "A valid RSA-encrypted client_key is required.", Data = null };
            }
            string rawMobile = request.ResolveMobileNo();
            string? rawDevice = request.ResolveDeviceId();
            string? rawUserType = request.ResolveUserType();

            string mobileNo = NormalizeMobileNumber(DecryptField(rawMobile, clientKey));
            string otp = DecryptField(request.Otp, clientKey);
            string? deviceId = DecryptField(rawDevice, clientKey);
            string? userId = DecryptField(request.UserId, clientKey);
            string? session = DecryptField(request.Session, clientKey);
            string? userType = DecryptField(rawUserType, clientKey);

            // 2. Validate mandatory parameters
            if (request.PortalId < 0)
            {
                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "A valid portal_id is required.",
                    Data = null
                };
            }

            if (string.IsNullOrWhiteSpace(mobileNo) || !IsValidMobileNumber(mobileNo))
            {
                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "A valid 10-digit mobile number is required.",
                    Data = null
                };
            }

            if (string.IsNullOrWhiteSpace(otp))
            {
                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "A valid OTP is required.",
                    Data = null
                };
            }

            try
            {
                // Resolve effective portal ID: if portal_id was not explicitly passed in request,
                // check if there is a pending OTP registration attempt for this mobile number
                long? explicitPortalId = request.ResolveExplicitPortalId();
                long effectivePortalId = explicitPortalId ?? request.PortalId;

                if (!explicitPortalId.HasValue && (request.A_Id == null || request.A_Id.Count == 0))
                {
                    int expiryMinutes = _smsService.GetOtpExpiryMinutes();
                    long? pendingPortalId = await _applicantRepository.GetPendingOtpPortalIdAsync(mobileNo, expiryMinutes);
                    if (pendingPortalId.HasValue && pendingPortalId.Value > 0)
                    {
                        effectivePortalId = pendingPortalId.Value;
                    }
                }

                // 3. Resolve portal to verify portal exists and get name/url
                var mapping = await ResolvePortalMappingAsync(effectivePortalId, mobileNo);
                if (mapping == null)
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        StatusCode = (int)HttpStatusCode.NotFound,
                        Message = $"Portal ID {effectivePortalId} was not found in mst_portal.",
                        Data = null
                    };
                }

                if (!string.IsNullOrWhiteSpace(mapping.IsActive) && (mapping.IsActive.Equals("N", StringComparison.OrdinalIgnoreCase) || mapping.IsActive == "0"))
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Message = $"Portal '{mapping.PortalName}' (ID {mapping.PortalId}) is currently marked inactive.",
                        Data = null
                    };
                }

                // If portal URL is unavailable, execute server-side OTP verification and tbl_map_user_portal mapping insertion
                if (!IsPortalUrlAvailable(mapping.PortalUrl))
                {
                    return await VerifyOtpAndMapApplicantInternalAsync(
                        mapping.PortalId,
                        mobileNo,
                        otp,
                        deviceId,
                        session,
                        userType,
                        cancellationToken);
                }

                // 4. Extract list of a_Id(s) (Existing external portal flow unchanged)
                var aIdList = new List<long>();
                if (request.A_Id != null)
                {
                    foreach (var id in request.A_Id)
                    {
                        if (id > 0 && !aIdList.Contains(id))
                        {
                            aIdList.Add(id);
                        }
                    }
                }

                // If no a_Id passed, attempt fallback to tbl_beneficiary_details
                if (aIdList.Count == 0)
                {
                    var (storedAId, storedSession, storedDeviceId, storedBenNo) = 
                        await _applicantRepository.GetLatestBeneficiaryInfoAsync(request.PortalId, mobileNo, null);

                    if (storedAId > 0)
                    {
                        aIdList.Add(storedAId);
                    }
                    if (string.IsNullOrWhiteSpace(session) && !string.IsNullOrWhiteSpace(storedSession))
                    {
                        session = storedSession;
                    }
                    if (string.IsNullOrWhiteSpace(deviceId) && !string.IsNullOrWhiteSpace(storedDeviceId))
                    {
                        deviceId = storedDeviceId;
                    }
                    if (string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(storedBenNo))
                    {
                        userId = storedBenNo;
                    }
                }

                if (!request.OtpOnlyForward && aIdList.Count == 0)
                    return new ApplicantResponse { Status = false, StatusCode = 400,
                        Message = "A_Id is required. Please provide a_Id or ensure applicant details were fetched first." };

                if (string.IsNullOrWhiteSpace(session))
                {
                    session = DateTime.Now.Year.ToString();
                }

                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    deviceId = "DEFAULT_DEVICE";
                }

                // 5. Resolve target external VerifyOtp URL
                string targetUrl = BuildTargetUrl(mapping.PortalUrl, VerifyOtpEndpoint);
                if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out var targetUri))
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Message = $"Invalid verify-otp URL configured for portal '{mapping.PortalName}': '{targetUrl}'",
                        Data = null
                    };
                }

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

                var verifiedResults = new List<VerifyOtpData>();
                string lastErrorMessage = "Invalid or expired OTP.";
                int lastStatusCode = (int)HttpStatusCode.BadRequest;
                bool anySuccess = false;

                // 6. Call external VerifyOtp API with array of a_Id
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, targetUri);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                if (!string.IsNullOrWhiteSpace(mapping.Token))
                {
                    httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {mapping.Token.Trim()}");
                }

                if (!string.IsNullOrWhiteSpace(mapping.SecretKey))
                {
                    httpRequest.Headers.TryAddWithoutValidation("X-Secret-Key", mapping.SecretKey.Trim());
                }

                httpRequest.Headers.TryAddWithoutValidation("X-Correlation-ID", Guid.NewGuid().ToString("N"));

                var forwardPayload = new Dictionary<string, object?>
                {
                    ["clientKey"] = request.ClientKey,
                    ["otpId"] = Aes256CbcEncrypterApp.Encrypt(request.OtpId, clientKey),
                    ["otp"] = Aes256CbcEncrypterApp.Encrypt(otp, clientKey),
                    ["mobileNo"] = Aes256CbcEncrypterApp.Encrypt(mobileNo, clientKey)
                };
                if (!request.OtpOnlyForward)
                {
                    forwardPayload["a_Id"] = aIdList;
                    forwardPayload["deviceId"] = Aes256CbcEncrypterApp.Encrypt(deviceId, clientKey);
                    forwardPayload["session"] = Aes256CbcEncrypterApp.Encrypt(session, clientKey);
                }

                string jsonContent = JsonConvert.SerializeObject(forwardPayload);
                httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation("Calling verify-otp external API for portal '{PortalName}' (ID {PortalId}) at {TargetUrl} for mobile {Mobile}, a_Id [{AIds}]",
                    mapping.PortalName, mapping.PortalId, targetUri, mobileNo, string.Join(", ", aIdList));

                using var response = await client.SendAsync(httpRequest, cancellationToken);
                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                int statusCode = (int)response.StatusCode;
                var portalResponseHeaders = GetSankalpAuthHeaders(response);

                _logger.LogInformation("External verify-otp response: HTTP {StatusCode}", statusCode);
                string? originalDataString = null;

                if (response.IsSuccessStatusCode)
                {
                    bool rootStatus = false;
                    string message = "OTP verified successfully.";
                    var dataItems = new List<JObject>();

                    if (!string.IsNullOrWhiteSpace(responseBody))
                    {
                        try
                        {
                            var originalResponse = JToken.Parse(responseBody);
                            if (originalResponse is JObject originalObject && originalObject["data"]?.Type == JTokenType.String)
                                originalDataString = originalObject["data"]!.Value<string>();
                            var token = JToken.Parse(PortalResponseDecoder.Decode(responseBody, clientKey));
                            if (token is JObject obj)
                            {
                                if (obj["status"] != null)
                                {
                                    rootStatus = obj["status"]!.Value<bool>();
                                }
                                if (obj["message"] != null)
                                {
                                    message = obj["message"]!.ToString();
                                }
                                if (obj["data"] is JArray dataArr)
                                {
                                    foreach (var item in dataArr)
                                    {
                                        if (item is JObject itemObj)
                                        {
                                            dataItems.Add(itemObj);
                                        }
                                    }
                                }
                                else if (obj["data"] is JObject dataObj)
                                {
                                    dataItems.Add(dataObj);
                                }
                            }
                            else if (token is JArray rootArr)
                            {
                                foreach (var item in rootArr)
                                {
                                    if (item is JObject itemObj)
                                    {
                                        dataItems.Add(itemObj);
                                    }
                                }
                            }
                        }
                        catch (Exception parseEx)
                        {
                            _logger.LogWarning("External verify-otp response could not be decoded ({ErrorType}).", parseEx.GetType().Name);
                            return new ApplicantResponse { Status = false, StatusCode = 502,
                                Message = "The portal responded, but its verification data could not be decrypted or parsed." };
                        }
                    }

                    if (!rootStatus)
                        return new ApplicantResponse { Status = false, StatusCode = 400, Message = message };

                    if (dataItems.Count == 0 && aIdList.Count == 0)
                        return new ApplicantResponse { Status = true, StatusCode = 200,
                            Message = message, Data = originalDataString, ResponseHeaders = portalResponseHeaders };

                    if (dataItems.Count > 0)
                    {
                        foreach (var dObj in dataItems)
                        {
                            bool isItemVerified = false;
                            if (dObj["isVerify"] != null)
                            {
                                isItemVerified = dObj["isVerify"]!.Value<bool>();
                            }
                            else if (dObj["is_verified"] != null)
                            {
                                isItemVerified = dObj["is_verified"]!.Value<bool>();
                            }
                            else if (dObj["is_verify"] != null)
                            {
                                isItemVerified = dObj["is_verify"]!.Value<bool>();
                            }
                            else if (dObj["status"] != null)
                            {
                                isItemVerified = dObj["status"]!.Value<bool>();
                            }
                            else
                            {
                                isItemVerified = rootStatus;
                            }

                            long extAId = 0;
                            if (dObj["a_Id"] != null || dObj["a_id"] != null || dObj["A_Id"] != null)
                            {
                                extAId = (dObj["a_Id"] ?? dObj["a_id"] ?? dObj["A_Id"])!.Value<long>();
                            }

                            string? itemMessage = dObj["message"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(itemMessage))
                            {
                                lastErrorMessage = itemMessage;
                            }

                            if (isItemVerified)
                            {
                                anySuccess = true;

                                string? extBeneficiaryId = (dObj["beneficiaryID"] ?? dObj["beneficiaryId"] ?? dObj["BeneficiaryID"])?.ToString();
                                string? extSecretKey = (dObj["secretKey"] ?? dObj["SecretKey"])?.ToString();
                                string? extToken = (dObj["token"] ?? dObj["Token"])?.ToString();
                                DateTime? extVerifyDate = null;
                                if (dObj["verifyDate"] != null || dObj["VerifyDate"] != null)
                                {
                                    if (DateTime.TryParse((dObj["verifyDate"] ?? dObj["VerifyDate"])!.ToString(), out var parsedDate))
                                    {
                                        extVerifyDate = parsedDate;
                                    }
                                }

                                long finalAId = extAId > 0 ? extAId : (aIdList.Count == 1 ? aIdList[0] : 0);

                                string finalUserId = !string.IsNullOrWhiteSpace(userId)? userId.Trim(): (!string.IsNullOrWhiteSpace(extBeneficiaryId) ? extBeneficiaryId.Trim() : mobileNo.Trim());

                                //string secretKey = !string.IsNullOrWhiteSpace(extSecretKey)? extSecretKey.Trim(): TotpHelper.GenerateBase32Secret(20);
                                string secretKey = string.Empty;
                                if(!string.IsNullOrWhiteSpace(extSecretKey))
                                {
                                    secretKey = extSecretKey.Trim();
                                }
                                else
                                {
                                    secretKey = TotpHelper.GenerateBase32Secret(20);
                                }

                                //string clientToken = !string.IsNullOrWhiteSpace(extToken)? extToken.Trim(): _clientTokenService.GenerateToken();                               

                                string clientToken = string.Empty;
                                if(!string.IsNullOrWhiteSpace(extToken))
                                {
                                    clientToken = extToken.Trim();
                                }
                                else
                                {
                                    clientToken = _clientTokenService.GenerateToken();
                                }

                                string tokenHash = ClientTokenHasher.Hash(clientToken);

                                bool saved = await _applicantRepository.SaveSecretAndTokenAsync(request.PortalId, mobileNo, deviceId, finalUserId, secretKey, clientToken, tokenHash);
                                if (!saved)
                                {
                                    _logger.LogWarning("Saving secret/token to database failed for mobile {Mobile}, portal {PortalId}, a_Id {AId}",
                                        mobileNo, request.PortalId, finalAId);
                                }

                                if (finalAId > 0)
                                {
                                    bool updated = await _applicantRepository.UpdateBeneficiaryVerificationStatusAsync(finalAId, "Y", deviceId);
                                    if (!updated)
                                    {
                                        _logger.LogWarning("OTP verified but updating tbl_beneficiary_details is_verified='Y' failed for a_id {AId}", finalAId);
                                    }
                                    else
                                    {
                                        _logger.LogInformation("Updated tbl_beneficiary_details is_verified='Y' for a_id {AId}", finalAId);
                                    }
                                }

                                string portalName = !string.IsNullOrWhiteSpace(mapping.PortalName) ? mapping.PortalName : "eLabharthi";
                                string? otpauthUrl = null;
                                try
                                {
                                    otpauthUrl = TotpHelper.GenerateOtpauthUri(portalName, finalUserId, secretKey);
                                }
                                catch
                                {
                                    try
                                    {
                                        string base32Key = TotpHelper.GenerateBase32Secret(20);
                                        otpauthUrl = TotpHelper.GenerateOtpauthUri(portalName, finalUserId, base32Key);
                                    }
                                    catch { }
                                }

                                verifiedResults.Add(new VerifyOtpData
                                {
                                    A_Id = finalAId,
                                    BeneficiaryID = extBeneficiaryId,
                                    MobileNo = mobileNo,
                                    SecretKey = secretKey,
                                    Token = clientToken,
                                    VerifyDate = extVerifyDate ?? DateTime.Now,
                                    OtpauthUrl = otpauthUrl,
                                    PortalId = request.PortalId,
                                    UserId = finalUserId,
                                    IsVerified = "Y"
                                });
                            }
                        }
                    }
                    else if (rootStatus)
                    {
                        anySuccess = true;
                        foreach (long aid in aIdList)
                        {
                            string finalUserId = !string.IsNullOrWhiteSpace(userId) ? userId.Trim() : mobileNo.Trim();
                            string secretKey = TotpHelper.GenerateBase32Secret(20);
                            string clientToken = _clientTokenService.GenerateToken();
                            string tokenHash = ClientTokenHasher.Hash(clientToken);

                            await _applicantRepository.SaveSecretAndTokenAsync(request.PortalId, mobileNo, deviceId, finalUserId, secretKey, clientToken, tokenHash);
                            await _applicantRepository.UpdateBeneficiaryVerificationStatusAsync(aid, "Y", deviceId);

                            string portalName = !string.IsNullOrWhiteSpace(mapping.PortalName) ? mapping.PortalName : "eLabharthi";
                            string? otpauthUrl = null;
                            try { otpauthUrl = TotpHelper.GenerateOtpauthUri(portalName, finalUserId, secretKey); } catch { }

                            verifiedResults.Add(new VerifyOtpData
                            {
                                A_Id = aid,
                                MobileNo = mobileNo,
                                SecretKey = secretKey,
                                Token = clientToken,
                                VerifyDate = DateTime.Now,
                                OtpauthUrl = otpauthUrl,
                                PortalId = request.PortalId,
                                UserId = finalUserId,
                                IsVerified = "Y"
                            });
                        }
                    }
                    else
                    {
                        lastErrorMessage = message;
                        lastStatusCode = (int)HttpStatusCode.BadRequest;
                    }
                }
                else
                {
                    lastStatusCode = statusCode;
                    string errMessage = "Invalid or expired OTP.";
                    if (!string.IsNullOrWhiteSpace(responseBody))
                    {
                        try
                        {
                            var token = JToken.Parse(responseBody);
                            if (token is JObject obj)
                            {
                                if (obj["message"] != null)
                                {
                                    errMessage = obj["message"]!.ToString();
                                }
                                else if (obj["errors"] is JObject errorsObj)
                                {
                                    var firstError = errorsObj.Properties().FirstOrDefault();
                                    if (firstError != null && firstError.Value is JArray errArr && errArr.Count > 0)
                                    {
                                        errMessage = errArr[0].ToString();
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                    lastErrorMessage = errMessage;
                }

                // 7. Check if any verification succeeded
                if (!anySuccess || verifiedResults.Count == 0)
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        StatusCode = lastStatusCode >= 400 && lastStatusCode < 500 ? lastStatusCode : (int)HttpStatusCode.BadRequest,
                        Message = lastErrorMessage,
                        Data = null
                    };
                }

                // Mark OTP validated in local tbl_otp
                await _applicantRepository.MarkOtpValidatedAsync(mobileNo, otp);

                _logger.LogInformation("Successfully verified OTP for mobile {Mobile}, portal {PortalId}, count {Count}",
                    mobileNo, request.PortalId, verifiedResults.Count);

                // Return single object if 1 record verified, or array if multiple
                object dataPayload = originalDataString is not null
                    ? originalDataString
                    : verifiedResults.Count == 1 ? (object)verifiedResults[0] : verifiedResults;

                return new ApplicantResponse
                {
                    Status = true,
                    StatusCode = (int)HttpStatusCode.OK,
                    Message = "OTP verified successfully.",
                    Data = dataPayload,
                    ResponseHeaders = portalResponseHeaders
                };
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Timeout connecting to external verify-otp for portal ID {PortalId}", request.PortalId);
                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = (int)HttpStatusCode.GatewayTimeout,
                    Message = "Gateway Timeout: The external portal took too long to respond.",
                    Data = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to external verify-otp for portal ID {PortalId}", request.PortalId);
                _exceptionLogging.SendErrorToText(ex);

                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = (int)HttpStatusCode.BadGateway,
                    Message = $"Bad Gateway: Unable to connect to external verify-otp API ({ex.Message})",
                    Data = null
                };
            }
        }

        public Task<ApplicantResponse> GetApplicantListAsync(
            SankalpApplicantLookupRequest request,
            string? authToken,
            string? authToken1,
            CancellationToken cancellationToken = default) =>
            ForwardSankalpApplicantLookupAsync(GetApplicantListEndpoint, request, authToken, authToken1, cancellationToken);

        public Task<ApplicantResponse> GetApplicantFullDetailsAsync(
            SankalpApplicantLookupRequest request,
            string? authToken,
            string? authToken1,
            CancellationToken cancellationToken = default) =>
            ForwardSankalpApplicantLookupAsync(GetApplicantFullDetailsEndpoint, request, authToken, authToken1, cancellationToken);

        private async Task<ApplicantResponse> ForwardSankalpApplicantLookupAsync(
            string endpoint,
            SankalpApplicantLookupRequest request,
            string? authToken,
            string? authToken1,
            CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Data))
            {
                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "Encrypted data is required."
                };
            }

            if (string.IsNullOrWhiteSpace(authToken) || string.IsNullOrWhiteSpace(authToken1))
            {
                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = (int)HttpStatusCode.Unauthorized,
                    Message = "authtoken and authtoken1 headers from VerifyApplicantOtp are required."
                };
            }

            string baseUrl = _configuration["SankalpExternal:BaseUrl"]?.Trim() ?? string.Empty;
            string targetUrl = BuildTargetUrl(baseUrl, endpoint);
            if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var targetUri))
            {
                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Message = "Sankalp external API base URL is not configured."
                };
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, targetUri);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpRequest.Headers.TryAddWithoutValidation("authtoken", authToken.Trim());
                httpRequest.Headers.TryAddWithoutValidation("authtoken1", authToken1.Trim());
                httpRequest.Content = new StringContent(
                    JsonConvert.SerializeObject(new { data = request.Data }),
                    Encoding.UTF8,
                    "application/json");

                using var response = await client.SendAsync(httpRequest, cancellationToken);
                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                return new ApplicantResponse
                {
                    Status = response.IsSuccessStatusCode,
                    StatusCode = (int)response.StatusCode,
                    Message = response.IsSuccessStatusCode ? "Success" : $"Sankalp API returned HTTP {(int)response.StatusCode}.",
                    // Keep the external response exactly as received: it contains
                    // encrypted data and must not be decrypted or reshaped here.
                    Data = responseBody,
                    ResponseHeaders = GetSankalpAuthHeaders(response)
                };
            }
            catch (TaskCanceledException)
            {
                return new ApplicantResponse { Status = false, StatusCode = (int)HttpStatusCode.GatewayTimeout, Message = "Sankalp API timed out." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to call Sankalp applicant endpoint {Endpoint}", endpoint);
                return new ApplicantResponse { Status = false, StatusCode = (int)HttpStatusCode.BadGateway, Message = "Unable to connect to Sankalp API." };
            }
        }

        private static Dictionary<string, string> GetSankalpAuthHeaders(HttpResponseMessage response)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in new[] { "authtoken", "authtoken1" })
            {
                if (response.Headers.TryGetValues(name, out var values))
                {
                    string? value = values.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(value))
                        headers[name] = value;
                }
            }
            return headers;
        }

        /// <summary>
        /// Step 2: Initiates OTP-based applicant registration when the portal URL is unavailable.
        /// Validates request fields, checks duplicate mapping and rate limits, generates a 6-digit OTP,
        /// sends it via SMS gateway, and stores the hashed OTP bound to the registration attempt.
        /// Does not insert into tbl_map_user_portal at this stage.
        /// </summary>
        private async Task<ApplicantResponse> InitiateOtpApplicantRegistrationAsync(
            PortalUserMappingDto mapping,
            string mobileNo,
            string deviceId,
            string session,
            string userType,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return new ApplicantResponse
                {
                    Status = false,
                    Code = (int)HttpStatusCode.BadRequest,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "A valid deviceid is required.",
                    Data = null
                };
            }

            long? parsedUserTypeId = null;
            if (!string.IsNullOrWhiteSpace(userType))
            {
                if (!long.TryParse(userType.Trim(), out long utId) || utId <= 0)
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        Code = (int)HttpStatusCode.BadRequest,
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Message = "A valid numeric UserType is required.",
                        Data = null
                    };
                }
                parsedUserTypeId = utId;
            }

            try
            {
                // Validate UserType against configured user types for this portal in dbo.UserType (if configured)
                var configuredUserTypes = await _portalRepository.GetUserTypeByIdAsync(mapping.PortalId);
                if (configuredUserTypes != null && configuredUserTypes.Count > 0)
                {
                    if (!parsedUserTypeId.HasValue)
                    {
                        return new ApplicantResponse
                        {
                            Status = false,
                            Code = (int)HttpStatusCode.BadRequest,
                            StatusCode = (int)HttpStatusCode.BadRequest,
                            Message = $"UserType is required for portal '{mapping.PortalName}'.",
                            Data = null
                        };
                    }

                    bool isValidUserType = configuredUserTypes.Any(u =>
                        !string.IsNullOrWhiteSpace(u.UserType_id) &&
                        long.TryParse(u.UserType_id.Trim(), out long configuredId) &&
                        configuredId == parsedUserTypeId.Value);

                    if (!isValidUserType)
                    {
                        return new ApplicantResponse
                        {
                            Status = false,
                            Code = (int)HttpStatusCode.BadRequest,
                            StatusCode = (int)HttpStatusCode.BadRequest,
                            Message = $"Invalid UserType '{userType}' for portal '{mapping.PortalName}' (ID {mapping.PortalId}).",
                            Data = null
                        };
                    }
                }

                // Prevent duplicate mapping for the same applicant, portal, and user type
                bool alreadyMapped = await _applicantRepository.CheckApplicantPortalMappingExistsAsync(
                    mapping.PortalId,
                    mobileNo,
                    parsedUserTypeId);

                if (alreadyMapped)
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        Code = (int)HttpStatusCode.Conflict,
                        StatusCode = (int)HttpStatusCode.Conflict,
                        Message = "Applicant is already registered and mapped with this portal for the specified UserType.",
                        Data = null
                    };
                }

                // Apply OTP resend cooldown and rate limiting
                var (isAllowed, rateLimitError) = await _applicantRepository.CheckOtpSendRateLimitAsync(
                    mobileNo,
                    OtpResendCooldownSeconds,
                    MaxOtpRequestsPerWindow,
                    OtpRateLimitWindowMinutes);

                if (!isAllowed)
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        Code = (int)HttpStatusCode.TooManyRequests,
                        StatusCode = (int)HttpStatusCode.TooManyRequests,
                        Message = rateLimitError ?? "Too many OTP requests. Please try again later.",
                        Data = null
                    };
                }

                // Generate 6-digit cryptographically secure OTP
                int otpNumber = RandomNumberGenerator.GetInt32(100000, 1000000);
                string otp = otpNumber.ToString("D6");
                string otpHash = ClientTokenHasher.Hash(otp);

                // Send OTP via existing SMS service
                var (smsSuccess, smsMessage) = await _smsService.SendOtpAsync(mobileNo, otp);
                if (!smsSuccess)
                {
                    _logger.LogWarning("OTP SMS delivery failed for portal {PortalId}, mobile {MaskedMobile}: {Reason}",
                        mapping.PortalId, MaskMobileNumber(mobileNo), smsMessage);

                    return new ApplicantResponse
                    {
                        Status = false,
                        Code = (int)HttpStatusCode.BadGateway,
                        StatusCode = (int)HttpStatusCode.BadGateway,
                        Message = $"Failed to send OTP SMS: {smsMessage}",
                        Data = null
                    };
                }

                // Store hashed OTP securely in tbl_otp with expiration and registration binding
                bool saved = await _applicantRepository.SaveRegistrationOtpAsync(
                    mapping.PortalId,
                    mobileNo,
                    deviceId,
                    session,
                    parsedUserTypeId,
                    otpHash);

                if (!saved)
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        Code = (int)HttpStatusCode.InternalServerError,
                        StatusCode = (int)HttpStatusCode.InternalServerError,
                        Message = "Failed to record OTP for applicant registration.",
                        Data = null
                    };
                }

                int expiryMinutes = _smsService.GetOtpExpiryMinutes();

                _logger.LogInformation("Initiated OTP-based applicant registration for portal '{PortalName}' (ID {PortalId}), mobile {MaskedMobile}",
                    mapping.PortalName, mapping.PortalId, MaskMobileNumber(mobileNo));

                return new ApplicantResponse
                {
                    Status = true,
                    Code = (int)HttpStatusCode.OK,
                    StatusCode = (int)HttpStatusCode.OK,
                    Message = "OTP sent successfully to registered mobile number.",
                    Data = new ApplicantOtpSendData
                    {
                        PortalId = mapping.PortalId,
                        PortalName = mapping.PortalName,
                        MobileNo = mobileNo,
                        MaskedMobile = MaskMobileNumber(mobileNo),
                        DeviceId = deviceId,
                        Session = string.IsNullOrWhiteSpace(session) ? null : session,
                        UserType = parsedUserTypeId?.ToString() ?? (string.IsNullOrWhiteSpace(userType) ? null : userType),
                        OtpRequired = true,
                        OtpExpiryMinutes = expiryMinutes
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating OTP registration for portal ID {PortalId}, mobile {MaskedMobile}",
                    mapping.PortalId, MaskMobileNumber(mobileNo));
                _exceptionLogging.SendErrorToText(ex);

                return new ApplicantResponse
                {
                    Status = false,
                    Code = (int)HttpStatusCode.InternalServerError,
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Message = "An error occurred while initiating OTP-based applicant registration.",
                    Data = null
                };
            }
        }

        /// <summary>
        /// Step 3, 4 & 5: Verifies OTP for OTP-based applicant registration and inserts mapping into tbl_map_user_portal.
        /// </summary>
        public async Task<ApplicantResponse> VerifyApplicantOtpAsync(VerifyApplicantOtpRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return new ApplicantResponse
                {
                    Status = false,
                    Code = (int)HttpStatusCode.BadRequest,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "Request payload is required.",
                    Data = null
                };
            }

            string clientKey = DecryptClientKey(request.ClientKey);
            string mobileNo = NormalizeMobileNumber(DecryptField(request.ResolveMobileNo(), clientKey));
            string otp = DecryptField(request.Otp, clientKey).Trim();
            string? deviceId = DecryptField(request.ResolveDeviceId(), clientKey);
            string? session = DecryptField(request.Session, clientKey);
            string? userType = DecryptField(request.ResolveUserType(), clientKey);
            long? explicitPortalId = request.ResolvePortalId();

            if (string.IsNullOrWhiteSpace(mobileNo) || !IsValidMobileNumber(mobileNo))
            {
                return new ApplicantResponse
                {
                    Status = false,
                    Code = (int)HttpStatusCode.BadRequest,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "A valid 10-digit mobile number is required.",
                    Data = null
                };
            }

            if (string.IsNullOrWhiteSpace(otp))
            {
                return new ApplicantResponse
                {
                    Status = false,
                    Code = (int)HttpStatusCode.BadRequest,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "A valid OTP is required.",
                    Data = null
                };
            }

            return await VerifyOtpAndMapApplicantInternalAsync(
                explicitPortalId,
                mobileNo,
                otp,
                deviceId,
                session,
                userType,
                cancellationToken);
        }

        private async Task<ApplicantResponse> VerifyOtpAndMapApplicantInternalAsync(
            long? requestedPortalId,
            string mobileNo,
            string otp,
            string? deviceId,
            string? session,
            string? userType,
            CancellationToken cancellationToken)
        {
            string cleanOtp = otp?.Trim() ?? string.Empty;
            if (!Regex.IsMatch(cleanOtp, @"^[0-9]{6}$"))
            {
                return new ApplicantResponse
                {
                    Status = false,
                    Code = (int)HttpStatusCode.BadRequest,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "OTP must be a valid 6-digit numeric code.",
                    Data = null
                };
            }

            long? parsedUserTypeId = null;
            if (!string.IsNullOrWhiteSpace(userType))
            {
                if (!long.TryParse(userType.Trim(), out long utId) || utId <= 0)
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        Code = (int)HttpStatusCode.BadRequest,
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Message = "A valid numeric UserType is required.",
                        Data = null
                    };
                }
                parsedUserTypeId = utId;
            }

            try
            {
                // Generate secret key and client token using existing project mechanisms
                string secretKey = TotpHelper.GenerateBase32Secret(20);
                string clientToken = _clientTokenService.GenerateToken();
                string tokenHash = ClientTokenHasher.Hash(clientToken);
                int expiryMinutes = _smsService.GetOtpExpiryMinutes();

                var result = await _applicantRepository.VerifyOtpAndInsertPortalMappingAsync(
                    requestedPortalId,
                    mobileNo,
                    cleanOtp,
                    deviceId,
                    session,
                    parsedUserTypeId,
                    expiryMinutes,
                    MaxOtpVerifyRetryAttempts,
                    secretKey,
                    clientToken,
                    tokenHash);

                if (!result.Success)
                {
                    _logger.LogWarning("OTP verification failed for mobile {MaskedMobile}: {Reason}",
                        MaskMobileNumber(mobileNo), result.Message);

                    return new ApplicantResponse
                    {
                        Status = false,
                        Code = result.StatusCode,
                        StatusCode = result.StatusCode,
                        Message = result.Message,
                        Data = null
                    };
                }

                _logger.LogInformation("Successfully verified OTP and inserted portal mapping (user_portal_id={UserPortalId}) for portal {PortalId}, mobile {MaskedMobile}",
                    result.Data?.UserPortalId, result.Data?.PortalId, MaskMobileNumber(mobileNo));

                return new ApplicantResponse
                {
                    Status = true,
                    Code = (int)HttpStatusCode.OK,
                    StatusCode = (int)HttpStatusCode.OK,
                    Message = result.Message,
                    Data = result.Data
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying OTP and mapping applicant for mobile {MaskedMobile}", MaskMobileNumber(mobileNo));
                _exceptionLogging.SendErrorToText(ex);

                return new ApplicantResponse
                {
                    Status = false,
                    Code = (int)HttpStatusCode.InternalServerError,
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Message = "An error occurred while verifying OTP and mapping applicant.",
                    Data = null
                };
            }
        }

        /// <summary>
        /// Dynamically resolves portal information and user mapping from mst_portal & tbl_map_user_portal.
        /// Supports all registered portals (including portals where portal_url is empty/unavailable such as NIC Portal 5).
        /// </summary>
        private async Task<PortalUserMappingDto?> ResolvePortalMappingAsync(long portalId, string? mobileNo)
        {
            long effectivePortalId = portalId <= 0 ? 1 : portalId;

            // 1. First attempt: resolve portal with user credentials mapping
            var mapping = await _portalRepository.GetPortalAndUserMappingAsync(effectivePortalId, mobileNo);
            if (mapping != null)
            {
                if (effectivePortalId == 1 && string.IsNullOrWhiteSpace(mapping.PortalUrl))
                {
                    mapping.PortalUrl = DefaultPortalUrl;
                }
                return mapping;
            }

            // 2. Second attempt: resolve portal directly from mst_portal (e.g. Portal 4 Postmatric, Portal 3 Ashwin, Portal 5 NIC)
            var portal = await _portalRepository.GetPortalByIdAsync(effectivePortalId);
            if (portal != null)
            {
                return new PortalUserMappingDto
                {
                    PortalId = portal.PortalId,
                    PortalName = portal.PortalName ?? string.Empty,
                    PortalUrl = (effectivePortalId == 1 && string.IsNullOrWhiteSpace(portal.PortalUrl))
                        ? DefaultPortalUrl
                        : portal.PortalUrl,
                    IsActive = portal.IsActive
                };
            }

            // 3. Fallback to default eLabharthi configuration only if portal ID was 0 or 1
            if (effectivePortalId == 1)
            {
                return new PortalUserMappingDto
                {
                    PortalId = effectivePortalId,
                    PortalName = "eLabharthi",
                    PortalUrl = DefaultPortalUrl,
                    IsActive = "Y"
                };
            }

            return null;
        }

        private static bool IsPortalUrlAvailable(string? portalUrl)
        {
            if (string.IsNullOrWhiteSpace(portalUrl))
                return false;

            return Uri.TryCreate(portalUrl.Trim(), UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static string NormalizeMobileNumber(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
                return string.Empty;

            string trimmed = mobile.Trim();
            string digits = new string(trimmed.Where(char.IsDigit).ToArray());
            if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal))
            {
                digits = digits.Substring(2);
            }
            else if (digits.Length == 11 && digits.StartsWith("0", StringComparison.Ordinal))
            {
                digits = digits.Substring(1);
            }

            return digits;
        }

        private static bool IsValidMobileNumber(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
                return false;

            return Regex.IsMatch(mobile.Trim(), @"^[0-9]{10}$");
        }

        private static string MaskMobileNumber(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile) || mobile.Length < 4)
                return mobile ?? string.Empty;

            return new string('X', mobile.Length - 4) + mobile.Substring(mobile.Length - 4);
        }

        /// <summary>
        /// Dynamically resolves portal metadata from mst_portal.
        /// </summary>
        private async Task<PortalDto?> ResolvePortalAsync(long portalId)
        {
            long effectivePortalId = portalId <= 0 ? 1 : portalId;

            var portal = await _portalRepository.GetPortalByIdAsync(effectivePortalId);
            if (portal != null && !string.IsNullOrWhiteSpace(portal.PortalUrl))
            {
                return portal;
            }

            // Fallback for default eLabharthi portal
            if (effectivePortalId == 1)
            {
                return new PortalDto
                {
                    PortalId = effectivePortalId,
                    PortalName = "eLabharthi",
                    PortalUrl = DefaultPortalUrl,
                    IsActive = "Y"
                };
            }

            return null;
        }

        /// <summary>
        /// Builds the target external API URL combining the portal's base URL and the endpoint.
        /// </summary>
        private static string BuildTargetUrl(string? baseUrl, string endpoint)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = DefaultPortalUrl;
            }

            baseUrl = baseUrl.Trim().TrimEnd('/');

            // If the URL already ends with the target endpoint, use as is
            if (baseUrl.EndsWith(endpoint, StringComparison.OrdinalIgnoreCase))
            {
                return baseUrl;
            }

            // Strip any existing /api/Beneficiary suffix to avoid duplicate paths
            int apiIndex = baseUrl.IndexOf("/api/Beneficiary", StringComparison.OrdinalIgnoreCase);
            if (apiIndex >= 0)
            {
                baseUrl = baseUrl.Substring(0, apiIndex).TrimEnd('/');
            }

            return $"{baseUrl}/{endpoint.TrimStart('/')}";
        }

        private static string DecryptClientKey(string? clientKey)
        {
            if (string.IsNullOrWhiteSpace(clientKey))
                return string.Empty;

            try
            {
                return RSA_Enc_Dec.Decryption(clientKey);
            }
            catch
            {
                return clientKey;
            }
        }

        private static string DecryptField(string? text, string? clientKey)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            if (string.IsNullOrWhiteSpace(clientKey))
                return text;

            try
            {
                return Aes256CbcEncrypterApp.Decrypt(text, clientKey);
            }
            catch
            {
                return text;
            }
        }

        private static List<BeneficiaryData> ExtractBeneficiaries(string responseBody)
        {
            var list = new List<BeneficiaryData>();
            if (string.IsNullOrWhiteSpace(responseBody))
                return list;

            try
            {
                var token = JToken.Parse(responseBody);
                JArray? array = null;

                if (token is JObject obj)
                {
                    if (obj["data"] is JArray dataArr)
                    {
                        array = dataArr;
                    }
                    else if (obj["data"] is JObject dataObj)
                    {
                        var single = dataObj.ToObject<BeneficiaryData>();
                        if (single != null) list.Add(single);
                        return list;
                    }
                    else if (obj["BeneficiaryID"] != null || obj["beneficiaryID"] != null ||
                             obj["A_Id"] != null || obj["a_Id"] != null ||
                             obj["Name"] != null || obj["name"] != null)
                    {
                        var single = obj.ToObject<BeneficiaryData>();
                        if (single != null) list.Add(single);
                        return list;
                    }
                }
                else if (token is JArray rootArr)
                {
                    array = rootArr;
                }

                if (array != null)
                {
                    foreach (var item in array)
                    {
                        var itemDto = item.ToObject<BeneficiaryData>();
                        if (itemDto != null)
                        {
                            list.Add(itemDto);
                        }
                    }
                }
            }
            catch { }

            return list;
        }

        public async Task<ApplicantResponse> GetBeneficiaryMessagesAsync(GetBeneficiaryMessagesRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Token))
            {
                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "Token is required.",
                    Data = null
                };
            }

            string token = request.Token.Trim();
            long portalId = request.PortalId;
            string targetUrl = string.Empty;

            try
            {
                // 1. Resolve user, portal, mobile, and device information from the database using token
                var (resolvedPortalId, mobileNo, deviceId) = await _applicantRepository.GetUserByTokenAsync(token);
                portalId = resolvedPortalId;

                // 2. Resolve portal information (URL)
                var portal = await ResolvePortalAsync(portalId);
                if (portal == null)
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        StatusCode = (int)HttpStatusCode.NotFound,
                        Message = $"Portal ID {portalId} was not found in mst_portal.",
                        Data = null
                    };
                }

                if (!string.IsNullOrWhiteSpace(portal.IsActive) &&
                    (portal.IsActive.Equals("N", StringComparison.OrdinalIgnoreCase) || portal.IsActive == "0"))
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Message = $"Portal '{portal.PortalName}' (ID {portalId}) is currently marked inactive.",
                        Data = null
                    };
                }

                string portalUrl = !string.IsNullOrWhiteSpace(portal.PortalUrl) ? portal.PortalUrl : DefaultPortalUrl;
                string portalName = !string.IsNullOrWhiteSpace(portal.PortalName) ? portal.PortalName : "Unknown Portal";

                // 3. Construct target external API URL (http://10.133.20.151/eLabharthiAPI/api/Beneficiary/Get_Beneficiary_Messages)
                targetUrl = BuildTargetUrl(portalUrl, GetBeneficiaryMessagesEndpoint);
                if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out var targetUri))
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Message = $"Invalid portal URL configured for portal '{portalName}': '{targetUrl}'",
                        Data = null
                    };
                }

                // 4. Call external eLabharthi API passing {"token": token}
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, targetUri);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpRequest.Headers.TryAddWithoutValidation("X-Correlation-ID", Guid.NewGuid().ToString("N"));

                var forwardPayload = new { token = token };
                string jsonContent = JsonConvert.SerializeObject(forwardPayload);
                httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

                _logger.LogInformation("Calling external Beneficiary Messages API for portal '{PortalName}' at {TargetUrl} with token",
                    portalName, targetUri);

                using var httpResponse = await client.SendAsync(httpRequest, cancellationToken);
                string responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                int httpStatusCode = (int)httpResponse.StatusCode;

                _logger.LogInformation("External portal response received with status code {StatusCode}", httpStatusCode);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    string errMessage = $"External portal returned HTTP {httpStatusCode}.";
                    try
                    {
                        var tokenObj = JToken.Parse(responseBody);
                        if (tokenObj is JObject obj && obj["message"] != null)
                        {
                            errMessage = obj["message"]!.ToString();
                        }
                    }
                    catch { }

                    return new ApplicantResponse
                    {
                        Status = false,
                        StatusCode = httpStatusCode,
                        Message = errMessage,
                        Data = null
                    };
                }

                // 5. Extract beneficiary messages from response
                var messages = ExtractBeneficiaryMessages(responseBody);

                if (messages == null || messages.Count == 0)
                {
                    return new ApplicantResponse
                    {
                        Status = true,
                        StatusCode = (int)HttpStatusCode.OK,
                        Message = "No beneficiary messages found.",
                        Data = new List<SavedBeneficiaryMessageDto>()
                    };
                }

                // 6. Save each message in tbl_Message with mobile, and synced data in tbl_Message_log
                var savedList = new List<SavedBeneficiaryMessageDto>();

                foreach (var msg in messages)
                {
                    string itemMobile = !string.IsNullOrWhiteSpace(msg.MobNo) ? msg.MobNo : (mobileNo ?? string.Empty);
                    string? safeDeviceId = !string.IsNullOrWhiteSpace(deviceId) ? deviceId : (!string.IsNullOrWhiteSpace(itemMobile) ? itemMobile : "DEFAULT_DEVICE");

                    var (msgId, msgLogId) = await _applicantRepository.SaveBeneficiaryMessageAsync(portalId, itemMobile, safeDeviceId, msg);

                    savedList.Add(new SavedBeneficiaryMessageDto
                    {
                        MsgId = msgId,
                        MsgLogId = msgLogId,
                        PortalId = portalId,
                        DeviceId = safeDeviceId,
                        IsSynced = "Yes",
                        SyncDate = DateTime.Now,
                        Message_Id = msg.Message_Id,
                        Benificiary_No = msg.Benificiary_No,
                        MobNo = itemMobile,
                        Message_Head = msg.Message_Head,
                        Message_Body = msg.Message_Body,
                        Type = msg.Type,
                        Pic_Path1 = msg.Pic_Path1,
                        Pic_Path2 = msg.Pic_Path2,
                        Video_Path = msg.Video_Path,
                        Doc_Path = msg.Doc_Path,
                        CreatedBy = msg.CreatedBy,
                        CreatedDate = msg.CreatedDate
                    });
                }

                _logger.LogInformation("Successfully saved and synced {Count} beneficiary message(s) to tbl_Message and tbl_Message_log", savedList.Count);

                return new ApplicantResponse
                {
                    Status = true,
                    StatusCode = (int)HttpStatusCode.OK,
                    Message = $"Successfully retrieved and synced {savedList.Count} beneficiary message(s).",
                    Data = savedList
                };
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Timeout connecting to external portal ID {PortalId} at {TargetUrl}", portalId, targetUrl);
                _exceptionLogging.SendErrorToText(ex);

                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = (int)HttpStatusCode.GatewayTimeout,
                    Message = $"Gateway Timeout: The external portal at '{targetUrl}' took too long to respond (host may be offline or unreachable).",
                    Data = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get beneficiary messages for portal ID {PortalId} at {TargetUrl}", portalId, targetUrl);
                _exceptionLogging.SendErrorToText(ex);

                return new ApplicantResponse
                {
                    Status = false,
                    StatusCode = (int)HttpStatusCode.BadGateway,
                    Message = $"Bad Gateway: Unable to connect or process external messages API at '{targetUrl}' ({ex.Message})",
                    Data = null
                };
            }
        }

      

        private static List<BeneficiaryMessageItem> ExtractBeneficiaryMessages(string responseBody)
        {
            var list = new List<BeneficiaryMessageItem>();
            if (string.IsNullOrWhiteSpace(responseBody)) return list;

            try
            {
                var token = JToken.Parse(responseBody);
                JArray? array = null;

                if (token is JObject obj)
                {
                    if (obj["data"] is JArray dataArr)
                    {
                        array = dataArr;
                    }
                    else if (obj["Data"] is JArray dataArrUpper)
                    {
                        array = dataArrUpper;
                    }
                    else if (obj["data"] is JObject dataObj)
                    {
                        var single = dataObj.ToObject<BeneficiaryMessageItem>();
                        if (single != null) list.Add(single);
                        return list;
                    }
                    else if (obj["message_Id"] != null || obj["Message_Id"] != null ||
                             obj["benificiary_No"] != null || obj["mobNo"] != null)
                    {
                        var single = obj.ToObject<BeneficiaryMessageItem>();
                        if (single != null) list.Add(single);
                        return list;
                    }
                }
                else if (token is JArray rootArr)
                {
                    array = rootArr;
                }

                if (array != null)
                {
                    foreach (var item in array)
                    {
                        var itemDto = item.ToObject<BeneficiaryMessageItem>();
                        if (itemDto != null)
                        {
                            list.Add(itemDto);
                        }
                    }
                }
            }
            catch { }

            return list;
        }

        public async Task<ApplicantResponse> GetBeneficiaryStatusAsync(GetBeneficiaryStatusRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return new ApplicantResponse
                {
                    Status = false,
                    Code = (int)HttpStatusCode.BadRequest,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "Request payload is required.",
                    Data = null
                };
            }

            string mobileNo = request.MobileNo?.Trim() ?? string.Empty;
            string beneficiaryNo = request.BeneficiaryNo?.Trim() ?? string.Empty;
            long portalId = request.PortalId;

            if (portalId < 0)
            {
                return new ApplicantResponse
                {
                    Status = false,
                    Code = (int)HttpStatusCode.BadRequest,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "A valid portalId is required.",
                    Data = null
                };
            }

            if (string.IsNullOrWhiteSpace(mobileNo))
            {
                return new ApplicantResponse
                {
                    Status = false,
                    Code = (int)HttpStatusCode.BadRequest,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "A valid mobile number is required.",
                    Data = null
                };
            }

            if (string.IsNullOrWhiteSpace(beneficiaryNo))
            {
                return new ApplicantResponse
                {
                    Status = false,
                    Code = (int)HttpStatusCode.BadRequest,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Message = "Beneficiary number is required.",
                    Data = null
                };
            }

            string targetUrl = string.Empty;

            try
            {
                // 1. Resolve portal information from database (mst_portal)
                var portal = await ResolvePortalAsync(portalId);
                if (portal == null)
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        Code = (int)HttpStatusCode.NotFound,
                        StatusCode = (int)HttpStatusCode.NotFound,
                        Message = $"Portal ID {portalId} was not found in mst_portal.",
                        Data = null
                    };
                }

                string portalUrl = !string.IsNullOrWhiteSpace(portal.PortalUrl) ? portal.PortalUrl : DefaultPortalUrl;
                string portalName = !string.IsNullOrWhiteSpace(portal.PortalName) ? portal.PortalName : "Unknown Portal";

                if (!string.IsNullOrWhiteSpace(portal.IsActive) &&
                    (portal.IsActive.Equals("N", StringComparison.OrdinalIgnoreCase) || portal.IsActive == "0"))
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        Code = (int)HttpStatusCode.BadRequest,
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Message = $"Portal '{portalName}' (ID {portalId}) is currently marked inactive.",
                        Data = null
                    };
                }

                // 2. Construct target external API URL (http://10.133.20.151/eLabharthi/api/Beneficiary/Get_Beneficiary_Status)
                targetUrl = BuildTargetUrl(portalUrl, Get_Beneficiary_Status);
                if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out var targetUri))
                {
                    return new ApplicantResponse
                    {
                        Status = false,
                        Code = (int)HttpStatusCode.BadRequest,
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Message = $"Invalid portal URL configured for portal '{portalName}': '{targetUrl}'",
                        Data = null
                    };
                }

                // 3. Prepare forward payload
                var forwardPayload = new Dictionary<string, object?>
                {
                    ["portalId"] = portalId,
                    ["mobileNo"] = mobileNo,
                    ["beneficiaryNo"] = beneficiaryNo
                };

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, targetUri);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpRequest.Headers.TryAddWithoutValidation("X-Correlation-ID", Guid.NewGuid().ToString("N"));

                string jsonContent = JsonConvert.SerializeObject(forwardPayload);
                httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

                _logger.LogInformation("Calling external Beneficiary Status API for portal '{PortalName}' at {TargetUrl} for mobile {Mobile}, benNo {BenNo}",
                    portalName, targetUri, mobileNo, beneficiaryNo);

                using var httpResponse = await client.SendAsync(httpRequest, cancellationToken);
                string responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                int httpStatusCode = (int)httpResponse.StatusCode;

                _logger.LogInformation("External portal response received with status code {StatusCode}", httpStatusCode);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    string errMessage = $"External portal returned HTTP {httpStatusCode}.";
                    try
                    {
                        var tokenObj = JToken.Parse(responseBody);
                        if (tokenObj is JObject obj && obj["message"] != null)
                        {
                            errMessage = obj["message"]!.ToString();
                        }
                    }
                    catch { }

                    return new ApplicantResponse
                    {
                        Status = false,
                        Code = httpStatusCode,
                        StatusCode = httpStatusCode,
                        Message = errMessage,
                        Data = null
                    };
                }

                // 4. Parse response from external API
                bool isStatus = true;
                int code = (int)HttpStatusCode.OK;
                string message = "Pensioner status fetched successfully.";
                object? dataPayload = null;

                if (!string.IsNullOrWhiteSpace(responseBody))
                {
                    try
                    {
                        var tokenObj = JToken.Parse(responseBody);
                        if (tokenObj is JObject obj)
                        {
                            if (obj["status"] != null)
                            {
                                isStatus = obj["status"]!.Value<bool>();
                            }
                            if (obj["code"] != null)
                            {
                                code = obj["code"]!.Value<int>();
                            }
                            if (obj["message"] != null)
                            {
                                message = obj["message"]!.ToString();
                            }
                            if (obj["data"] != null)
                            {
                                dataPayload = obj["data"]!.ToObject<BeneficiaryStatusData>() ?? (object)obj["data"]!;
                            }
                        }
                    }
                    catch (Exception parseEx)
                    {
                        _logger.LogWarning(parseEx, "Failed to parse strongly-typed BeneficiaryStatusData from response.");
                        try
                        {
                            dataPayload = JsonConvert.DeserializeObject(responseBody);
                        }
                        catch { }
                    }
                }

                return new ApplicantResponse
                {
                    Status = isStatus,
                    Code = code,
                    StatusCode = (int)HttpStatusCode.OK,
                    Message = message,
                    Data = dataPayload
                };
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Timeout connecting to external portal ID {PortalId} at {TargetUrl}", portalId, targetUrl);
                _exceptionLogging.SendErrorToText(ex);

                return new ApplicantResponse
                {
                    Status = false,
                    Code = (int)HttpStatusCode.GatewayTimeout,
                    StatusCode = (int)HttpStatusCode.GatewayTimeout,
                    Message = $"Gateway Timeout: The external portal at '{targetUrl}' took too long to respond (host may be offline or unreachable).",
                    Data = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get beneficiary status for portal ID {PortalId} at {TargetUrl}", portalId, targetUrl);
                _exceptionLogging.SendErrorToText(ex);

                return new ApplicantResponse
                {
                    Status = false,
                    Code = (int)HttpStatusCode.BadGateway,
                    StatusCode = (int)HttpStatusCode.BadGateway,
                    Message = $"Bad Gateway: Unable to connect or process external beneficiary status API at '{targetUrl}' ({ex.Message})",
                    Data = null
                };
            }
        }
    }
}
