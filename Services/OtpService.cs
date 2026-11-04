using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SankalpAPI.DTO;
using SankalpAPI.Helper;
using SankalpAPI.Interface;
using SankalpAPI.Model;
using SankalpAPI.Security;
using VerifyOtpRequest = SankalpAPI.DTO.VerifyOtpRequest;
using VerifyOtpResponse = SankalpAPI.DTO.VerifyOtpResponse;

namespace SankalpAPI.Services
{
    public class OtpService : IOtpService
    {
        private readonly IOtpRepository _otpRepository;
        private readonly IUserRepository _userRepository;
        private readonly ISmsNotificationService _smsService;
        private readonly IHostEnvironment _env;
        private readonly ILogger<OtpService> _logger;
        private readonly ExceptionLogging _exceptionLogging;

        public OtpService(
            IOtpRepository otpRepository,
            IUserRepository userRepository,
            ISmsNotificationService smsService,
            IHostEnvironment env,
            ILogger<OtpService> logger,
            ExceptionLogging exceptionLogging)
        {
            _otpRepository = otpRepository;
            _userRepository = userRepository;
            _smsService = smsService;
            _env = env;
            _logger = logger;
            _exceptionLogging = exceptionLogging;
        }

        public async Task<OtpSendResponse> SendOtpAsync(SendOtpRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Mobile))
            {
                return new OtpSendResponse
                {
                    Status = false,
                    Message = "Mobile number is required."
                };
            }

            if (request == null || string.IsNullOrWhiteSpace(request.ClientKey))
            {
                return new OtpSendResponse
                {
                    Status = false,
                    Message = "Client key is required."
                };
            }



            string mobile = DecryptIfNeeded(request.Mobile, request.ClientKey).Trim();
            string key = RSA_Enc_Dec.Decryption(request.ClientKey);
            if (!ValidateMobileNumber(mobile))
            {
                return new OtpSendResponse
                {
                    Status = false,
                    Message = "Invalid 10-digit mobile number format."
                };
            }

            try
            {
                // 1. Generate 6-digit cryptographically secure OTP
                int otpNumber = RandomNumberGenerator.GetInt32(100000, 1000000);
                string otp = otpNumber.ToString();

                // 2. Save OTP in database
                await _otpRepository.SaveOtpAsync(mobile, otp, request.DeviceId);

                // 3. Send OTP SMS via Bihar Health SMS Gateway
                var (smsSuccess, smsMessage) = await _smsService.SendOtpAsync(mobile, otp);

                if (!smsSuccess)
                {
                    _logger.LogWarning("SMS dispatch unsuccessful for mobile {Mobile}: {Reason}", mobile, smsMessage);

                    if (!_env.IsDevelopment())
                    {
                        return new OtpSendResponse
                        {
                            Status = false,
                            Message = $"Failed to send OTP SMS: {smsMessage}"
                        };
                    }
                }

                int expiryMinutes = _smsService.GetOtpExpiryMinutes();

                return new OtpSendResponse
                {
                    Status = true,
                    Message = "OTP sent successfully to registered mobile number.",
                    Data = new OtpSendData
                    {
                        Mobile = Aes256CbcEncrypterApp.Encrypt(mobile, key),
                        MaskedMobile = MaskMobile(mobile),
                        OtpExpiryMinutes = expiryMinutes,
                        DevOtp = _env.IsDevelopment() ? otp : null
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while sending OTP to mobile {Mobile}", mobile);
                _exceptionLogging.SendErrorToText(ex);

                return new OtpSendResponse
                {
                    Status = false,
                    Message = "An error occurred while sending OTP."
                };
            }
        }

        // ============================================================
        // VERIFY OTP (Checks tbl_user only AFTER successful OTP verification)
        // ============================================================
        public async Task<VerifyOtpResponse> VerifyOtpAsync(VerifyOtpRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.MobileNo) || string.IsNullOrWhiteSpace(request.Otp))
            {
                return new VerifyOtpResponse
                {
                    Success = false,
                    UserExists = false,
                    Message = "Mobile number and OTP are required.",
                    Data = null
                };
            }

            string mobile = DecryptIfNeeded(request.MobileNo, request.ClientKey).Trim();
            string otp = DecryptIfNeeded(request.Otp, request.ClientKey).Trim();
            string deviceId = DecryptIfNeeded(request.DeviceId, request.ClientKey).Trim();

            if (!ValidateMobileNumber(mobile))
            {
                return new VerifyOtpResponse
                {
                    Success = false,
                    UserExists = false,
                    Message = "Invalid 10-digit mobile number format.",
                    Data = null
                };
            }

            if (string.IsNullOrWhiteSpace(otp))
            {
                return new VerifyOtpResponse
                {
                    Success = false,
                    UserExists = false,
                    Message = "OTP is required.",
                    Data = null
                };
            }

            try
            {
                // 1. FIRST: Perform OTP verification
                int expiryMinutes = _smsService.GetOtpExpiryMinutes();
                bool isOtpValid = await _otpRepository.ValidateOtpAsync(mobile, otp, expiryMinutes);

                // 2. If OTP is incorrect or expired: DO NOT query tbl_user!
                if (!isOtpValid)
                {
                    return new VerifyOtpResponse
                    {
                        Success = false,
                        UserExists = false,
                        Message = "Invalid or expired OTP.",
                        Data = null
                    };
                }

                // 3. SECOND: Query tbl_user only after OTP verification succeeds
                UserData? existingUser = await _userRepository.GetUserByMobileAsync(mobile);

                // 4. If user already exists in tbl_user: verify DeviceId and return existing user info
                if (existingUser != null)
                {
                    // Device ID verification and update logic
                    if (!string.IsNullOrWhiteSpace(deviceId))
                    {
                        string? dbDeviceId = existingUser.DeviceId?.Trim();

                        // If DB DeviceId is NULL/empty or different from requested DeviceId, update DB
                        if (string.IsNullOrWhiteSpace(dbDeviceId) || !string.Equals(dbDeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                        {
                            await _userRepository.UpdateUserDeviceIdAsync(mobile, deviceId);
                            existingUser.DeviceId = deviceId;
                        }

                        // If FcmToken was also supplied during OTP verification, upsert in dbo.UserFcmDevices
                        string fcmToken = DecryptIfNeeded(request.FcmToken ?? string.Empty, request.ClientKey).Trim();
                        string platform = string.IsNullOrWhiteSpace(request.Platform)
                            ? "Android"
                            : DecryptIfNeeded(request.Platform, request.ClientKey).Trim();

                        if (!string.IsNullOrWhiteSpace(fcmToken))
                        {
                            await _userRepository.UpsertUserFcmDeviceAsync(existingUser.UserId.ToString(), deviceId, fcmToken, platform);
                        }
                    }

                    return new VerifyOtpResponse
                    {
                        Success = true,
                        UserExists = true,
                        Message = "User already exists.",
                        Data = existingUser
                    };
                }

                // 5. If user does not exist: Continue new-user registration flow
                return new VerifyOtpResponse
                {
                    Success = true,
                    UserExists = false,
                    Message = "OTP verified successfully. User can continue registration.",
                    Data = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while verifying OTP for mobile {Mobile}", mobile);
                _exceptionLogging.SendErrorToText(ex);

                return new VerifyOtpResponse
                {
                    Success = false,
                    UserExists = false,
                    Message = "An error occurred while verifying OTP.",
                    Data = null
                };
            }
        }

        // ============================================================
        // LEGACY VERIFY OTP (Preserved for backwards compatibility)
        // ============================================================
        public async Task<OtpVerifyResponse> VerifyOtpAsync(SmsVerifyOtpRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Mobile) || string.IsNullOrWhiteSpace(request.Otp))
            {
                return new OtpVerifyResponse
                {
                    Status = false,
                    Message = "Mobile number and OTP are required."
                };
            }

            string mobile = DecryptIfNeeded(request.Mobile, request.ClientKey).Trim();
            string otp = DecryptIfNeeded(request.Otp, request.ClientKey).Trim();

            if (!ValidateMobileNumber(mobile))
            {
                return new OtpVerifyResponse
                {
                    Status = false,
                    Message = "Invalid 10-digit mobile number format."
                };
            }

            if (string.IsNullOrWhiteSpace(otp))
            {
                return new OtpVerifyResponse
                {
                    Status = false,
                    Message = "OTP is required."
                };
            }

            try
            {
                int expiryMinutes = _smsService.GetOtpExpiryMinutes();
                bool isValid = await _otpRepository.ValidateOtpAsync(mobile, otp, expiryMinutes);

                if (isValid)
                {
                    return new OtpVerifyResponse
                    {
                        Status = true,
                        Message = "OTP verified successfully.",
                        Data = new OtpVerifyData
                        {
                            Mobile = mobile,
                            IsVerified = true,
                            VerifiedAt = DateTime.UtcNow
                        }
                    };
                }

                return new OtpVerifyResponse
                {
                    Status = false,
                    Message = "Invalid or expired OTP."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while verifying OTP for mobile {Mobile}", mobile);
                _exceptionLogging.SendErrorToText(ex);

                return new OtpVerifyResponse
                {
                    Status = false,
                    Message = "An error occurred while verifying OTP."
                };
            }
        }

        private static bool ValidateMobileNumber(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
                return false;

            string cleaned = mobile.Trim();
            return Regex.IsMatch(cleaned, @"^[0-9]{10}$");
        }

        private static string DecryptIfNeeded(string text, string? clientKey)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            if (string.IsNullOrWhiteSpace(clientKey))
                return text;

            try
            {
                string key;
                try
                {
                    key = RSA_Enc_Dec.Decryption(clientKey);
                }
                catch
                {
                    key = clientKey;
                }

                return Aes256CbcEncrypterApp.Decrypt(text, key);
            }
            catch
            {
                return text;
            }
        }

        private static string MaskMobile(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile) || mobile.Length < 4)
                return mobile;

            return new string('X', mobile.Length - 4) + mobile.Substring(mobile.Length - 4);
        }
    }
}
