using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace SankalpAPI.Model
{
    /// <summary>
    /// Request model for fetching applicant/beneficiary details from an external portal API.
    /// </summary>
    public class ApplicantRequest
    {
        [JsonPropertyName("client_key")]
        [JsonProperty("client_key")]
        public string ClientKey { get; set; } = string.Empty;

        /// <summary>
        /// Portal identifier from mst_portal table.
        /// </summary>
        /// <example>1</example>
        [JsonPropertyName("portal_id")]
        [JsonProperty("portal_id")]
        public long PortalId { get; set; }

        /// <summary>
        /// Registered 10-digit mobile number of the applicant.
        /// </summary>
        /// <example>9876543210</example>
        [JsonPropertyName("mobile_no")]
        [JsonProperty("mobile_no")]
        public string MobileNo { get; set; } = string.Empty;

        /// <summary>
        /// Unique installation/device identifier.
        /// </summary>
        /// <example>DEVICE123456</example>
        [JsonPropertyName("deviceid")]
        [JsonProperty("deviceid")]
        public string? DeviceId { get; set; }

        /// <summary>
        /// Optional Financial Year (e.g. "2024-2025"), Academic Year, or Session Token.
        /// </summary>
        /// <example>2024-2025</example>
        [JsonPropertyName("session")]
        [JsonProperty("session")]
        public string? Session { get; set; }

        [JsonPropertyName("UserType")]
        [JsonProperty("UserType")]
        public string? UserType { get; set; }
    }

    /// <summary>
    /// Beneficiary details returned from the external portal API.
    /// </summary>
    public class BeneficiaryData
    {
        [JsonPropertyName("a_Id")]
        [JsonProperty("a_Id")]
        public long A_Id { get; set; }

        [JsonPropertyName("beneficiaryID")]
        [JsonProperty("beneficiaryID")]
        public string? BeneficiaryID { get; set; }

        [JsonPropertyName("name")]
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonPropertyName("dob")]
        [JsonProperty("dob")]
        public string? DOB { get; set; }

        [JsonPropertyName("gender")]
        [JsonProperty("gender")]
        public string? Gender { get; set; }

        [JsonPropertyName("mobileNO")]
        [JsonProperty("mobileNO")]
        public string? MobileNO { get; set; }

        [JsonPropertyName("categoryName")]
        [JsonProperty("categoryName")]
        public string? CategoryName { get; set; }

        [JsonPropertyName("schemeCode")]
        [JsonProperty("schemeCode")]
        public string? SchemeCode { get; set; }

        [JsonPropertyName("schemeName")]
        [JsonProperty("schemeName")]
        public string? SchemeName { get; set; }

        [JsonPropertyName("area")]
        [JsonProperty("area")]
        public string? Area { get; set; }

        [JsonPropertyName("villageName")]
        [JsonProperty("villageName")]
        public string? VillageName { get; set; }

        [JsonPropertyName("villageCode")]
        [JsonProperty("villageCode")]
        public string? villageCode { get; set; }

        [JsonPropertyName("wardName")]
        [JsonProperty("wardName")]
        public string? WardName { get; set; }

        [JsonPropertyName("wardCode")]
        [JsonProperty("wardCode")]
        public string? WardCode { get; set; }

        [JsonPropertyName("panchyatCode")]
        [JsonProperty("panchyatCode")]
        public string? PanchyatCode { get; set; }

        [JsonPropertyName("panchyatName")]
        [JsonProperty("panchyatName")]
        public string? PanchyatName { get; set; }

        [JsonPropertyName("blockName")]
        [JsonProperty("blockName")]
        public string? BlockName { get; set; }

        [JsonPropertyName("blockId")]
        [JsonProperty("blockId")]
        public string? BlockId { get; set; }

        [JsonPropertyName("districtName")]
        [JsonProperty("districtName")]
        public string? DistrictName { get; set; }

        [JsonPropertyName("districtId")]
        [JsonProperty("districtId")]
        public string? DistrictId { get; set; }

        [JsonPropertyName("otp")]
        [JsonProperty("otp")]
        public string? OTP { get; set; }

        [JsonPropertyName("is_verified")]
        [JsonProperty("is_verified")]
        public string? IsVerified { get; set; }
    }

    /// <summary>
    /// Backward compatible alias for BeneficiaryData.
    /// </summary>
    public class BeneficiaryDetailDto : BeneficiaryData { }

    /// <summary>
    /// Response model returning data in standard { status, message, data } format.
    /// </summary>
    public class ApplicantResponse
    {
        [JsonPropertyName("status")]
        [JsonProperty("status")]
        public bool Status { get; set; }

        [JsonPropertyName("code")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        [JsonProperty("code", NullValueHandling = NullValueHandling.Ignore)]
        public int? Code { get; set; }

        [JsonPropertyName("message")]
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        [JsonProperty("data")]
        public object? Data { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public int StatusCode { get; set; } = 200;

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public Dictionary<string, string> ResponseHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Encrypted request envelope used by the Sankalp applicant-list endpoint.
    /// The value of <see cref="Data"/> is encrypted in the browser with the
    /// client AES key obtained during the OTP flow.
    /// </summary>
    public class SankalpApplicantLookupRequest
    {
        [JsonPropertyName("data")]
        [JsonProperty("data")]
        public string Data { get; set; } = string.Empty;
    }

    /// <summary>
    /// Schema documentation model for Swagger describing the list format in data.
    /// </summary>
    public class ApplicantListResponse
    {
        [JsonPropertyName("status")]
        [JsonProperty("status")]
        public bool Status { get; set; }

        [JsonPropertyName("message")]
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        [JsonProperty("data")]
        public List<BeneficiaryData> Data { get; set; } = new List<BeneficiaryData>();
    }

    /// <summary>
    /// Request model for verifying OTP.
    /// Supports a_Id as a single number (23) or an array of numbers ([23, 24]).
    /// Also supports OTP-based applicant registration when portal URL is unavailable.
    /// </summary>
    public class VerifyOtpRequest
    {
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public bool OtpOnlyForward { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public string OtpId { get; set; } = string.Empty;

        private long _portalId = 1;
        private bool _portalIdExplicitlySet;

        [JsonPropertyName("a_Id")]
        [JsonProperty("a_Id")]
        [System.Text.Json.Serialization.JsonConverter(typeof(SankalpAPI.Helper.FlexibleLongListConverter))]
        [Newtonsoft.Json.JsonConverter(typeof(SankalpAPI.Helper.FlexibleNewtonsoftLongListConverter))]
        public List<long> A_Id { get; set; } = new List<long>();

        [JsonPropertyName("mobileNo")]
        [JsonProperty("mobileNo")]
        public string MobileNo { get; set; } = string.Empty;

        [JsonPropertyName("otp")]
        [JsonProperty("otp")]
        public string Otp { get; set; } = string.Empty;

        [JsonPropertyName("deviceId")]
        [JsonProperty("deviceId")]
        public string? DeviceId { get; set; }

        [JsonPropertyName("session")]
        [JsonProperty("session")]
        public string? Session { get; set; }

        [JsonPropertyName("portal_id")]
        [JsonProperty("portal_id")]
        public long PortalId
        {
            get => _portalId;
            set
            {
                _portalId = value;
                _portalIdExplicitlySet = true;
            }
        }

        [JsonPropertyName("UserType")]
        [JsonProperty("UserType")]
        public string? UserType { get; set; }

        [JsonPropertyName("client_key")]
        [JsonProperty("client_key")]
        public string? ClientKey { get; set; }

        [JsonPropertyName("user_id")]
        [JsonProperty("user_id")]
        public string? UserId { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public bool IsPortalIdExplicitlySet => _portalIdExplicitlySet;

        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? AdditionalFields { get; set; }

        public string ResolveMobileNo()
        {
            if (!string.IsNullOrWhiteSpace(MobileNo))
                return MobileNo;

            return TryGetExtensionString("mobile_no")
                ?? TryGetExtensionString("mobile")
                ?? string.Empty;
        }

        public string? ResolveDeviceId()
        {
            if (!string.IsNullOrWhiteSpace(DeviceId))
                return DeviceId;

            return TryGetExtensionString("deviceid")
                ?? TryGetExtensionString("device_id");
        }

        public string? ResolveUserType()
        {
            if (!string.IsNullOrWhiteSpace(UserType))
                return UserType;

            return TryGetExtensionString("user_type")
                ?? TryGetExtensionString("user_type_id")
                ?? TryGetExtensionString("User_Type_ID");
        }

        public long? ResolveExplicitPortalId()
        {
            if (_portalIdExplicitlySet && _portalId > 0)
                return _portalId;

            if (AdditionalFields != null)
            {
                foreach (var key in new[] { "portalId", "PortalId", "portal_id" })
                {
                    if (AdditionalFields.TryGetValue(key, out var elem))
                    {
                        if (elem.ValueKind == System.Text.Json.JsonValueKind.Number && elem.TryGetInt64(out long val) && val > 0)
                            return val;
                        if (elem.ValueKind == System.Text.Json.JsonValueKind.String && long.TryParse(elem.GetString(), out long strVal) && strVal > 0)
                            return strVal;
                    }
                }
            }

            return null;
        }

        private string? TryGetExtensionString(string key)
        {
            if (AdditionalFields == null)
                return null;

            foreach (var kvp in AdditionalFields)
            {
                if (string.Equals(kvp.Key, key, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (kvp.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                        return kvp.Value.GetString();
                    if (kvp.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                        return kvp.Value.GetRawText();
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Request model for verifying OTP during OTP-based applicant registration and portal mapping.
    /// </summary>
    public class VerifyApplicantOtpRequest
    {
        [JsonPropertyName("portal_id")]
        [JsonProperty("portal_id")]
        public long PortalId { get; set; }

        [JsonPropertyName("mobile_no")]
        [JsonProperty("mobile_no")]
        public string MobileNo { get; set; } = string.Empty;

        [JsonPropertyName("otp")]
        [JsonProperty("otp")]
        public string Otp { get; set; } = string.Empty;

        [JsonPropertyName("deviceid")]
        [JsonProperty("deviceid")]
        public string? DeviceId { get; set; }

        [JsonPropertyName("session")]
        [JsonProperty("session")]
        public string? Session { get; set; }

        [JsonPropertyName("UserType")]
        [JsonProperty("UserType")]
        public string? UserType { get; set; }

        [JsonPropertyName("client_key")]
        [JsonProperty("client_key")]
        public string? ClientKey { get; set; }

        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? AdditionalFields { get; set; }

        public string ResolveMobileNo()
        {
            if (!string.IsNullOrWhiteSpace(MobileNo))
                return MobileNo;

            return TryGetExtensionString("mobileNo")
                ?? TryGetExtensionString("mobile")
                ?? string.Empty;
        }

        public string? ResolveDeviceId()
        {
            if (!string.IsNullOrWhiteSpace(DeviceId))
                return DeviceId;

            return TryGetExtensionString("deviceId")
                ?? TryGetExtensionString("device_id");
        }

        public string? ResolveUserType()
        {
            if (!string.IsNullOrWhiteSpace(UserType))
                return UserType;

            return TryGetExtensionString("user_type")
                ?? TryGetExtensionString("user_type_id")
                ?? TryGetExtensionString("User_Type_ID");
        }

        public long? ResolvePortalId()
        {
            if (PortalId > 0)
                return PortalId;

            if (AdditionalFields != null)
            {
                foreach (var key in new[] { "portalId", "PortalId", "portal_id" })
                {
                    if (AdditionalFields.TryGetValue(key, out var elem))
                    {
                        if (elem.ValueKind == System.Text.Json.JsonValueKind.Number && elem.TryGetInt64(out long val) && val > 0)
                            return val;
                        if (elem.ValueKind == System.Text.Json.JsonValueKind.String && long.TryParse(elem.GetString(), out long strVal) && strVal > 0)
                            return strVal;
                    }
                }
            }

            return null;
        }

        private string? TryGetExtensionString(string key)
        {
            if (AdditionalFields == null)
                return null;

            foreach (var kvp in AdditionalFields)
            {
                if (string.Equals(kvp.Key, key, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (kvp.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                        return kvp.Value.GetString();
                    if (kvp.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                        return kvp.Value.GetRawText();
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Response payload returned when OTP is generated and sent for portals without an external portal URL.
    /// </summary>
    public class ApplicantOtpSendData
    {
        [JsonPropertyName("portal_id")]
        [JsonProperty("portal_id")]
        public long PortalId { get; set; }

        [JsonPropertyName("portal_name")]
        [JsonProperty("portal_name")]
        public string PortalName { get; set; } = string.Empty;

        [JsonPropertyName("mobile_no")]
        [JsonProperty("mobile_no")]
        public string MobileNo { get; set; } = string.Empty;

        [JsonPropertyName("masked_mobile")]
        [JsonProperty("masked_mobile")]
        public string MaskedMobile { get; set; } = string.Empty;

        [JsonPropertyName("deviceid")]
        [JsonProperty("deviceid")]
        public string? DeviceId { get; set; }

        [JsonPropertyName("session")]
        [JsonProperty("session")]
        public string? Session { get; set; }

        [JsonPropertyName("UserType")]
        [JsonProperty("UserType")]
        public string? UserType { get; set; }

        [JsonPropertyName("otp_required")]
        [JsonProperty("otp_required")]
        public bool OtpRequired { get; set; } = true;

        [JsonPropertyName("otp_expiry_minutes")]
        [JsonProperty("otp_expiry_minutes")]
        public int OtpExpiryMinutes { get; set; }
    }

    /// <summary>
    /// Response payload returned after successful OTP verification and insertion into tbl_map_user_portal.
    /// Does not expose OTP or secretkey.
    /// </summary>
    public class ApplicantPortalRegistrationData
    {
        [JsonPropertyName("user_portal_id")]
        [JsonProperty("user_portal_id")]
        public long UserPortalId { get; set; }

        [JsonPropertyName("portal_id")]
        [JsonProperty("portal_id")]
        public long PortalId { get; set; }

        [JsonPropertyName("User_Type_ID")]
        [JsonProperty("User_Type_ID")]
        public long? UserTypeId { get; set; }

        [JsonPropertyName("mobile")]
        [JsonProperty("mobile")]
        public string Mobile { get; set; } = string.Empty;

        [JsonPropertyName("userid")]
        [JsonProperty("userid")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("token")]
        [JsonProperty("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("deviceid")]
        [JsonProperty("deviceid")]
        public string? DeviceId { get; set; }

        [JsonPropertyName("district_code")]
        [JsonProperty("district_code")]
        public long? DistrictCode { get; set; }

        [JsonPropertyName("block_code")]
        [JsonProperty("block_code")]
        public long? BlockCode { get; set; }

        [JsonPropertyName("panchayatcode")]
        [JsonProperty("panchayatcode")]
        public long? PanchayatCode { get; set; }

        [JsonPropertyName("is_verified")]
        [JsonProperty("is_verified")]
        public string IsVerified { get; set; } = "Y";

        [JsonPropertyName("verifyDate")]
        [JsonProperty("verifyDate")]
        public DateTime VerifyDate { get; set; }
    }

    /// <summary>
    /// Result returned from repository after atomic OTP verification and tbl_map_user_portal insertion.
    /// </summary>
    public class ApplicantOtpVerificationResult
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public ApplicantPortalRegistrationData? Data { get; set; }
    }

    /// <summary>
    /// Payload returned after verifying OTP and generating secret key & token.
    /// </summary>
    public class VerifyOtpData
    {
        [JsonPropertyName("a_Id")]
        [JsonProperty("a_Id")]
        public long A_Id { get; set; }

        [JsonPropertyName("beneficiaryID")]
        [JsonProperty("beneficiaryID")]
        public string? BeneficiaryID { get; set; }

        [JsonPropertyName("mobileNo")]
        [JsonProperty("mobileNo")]
        public string MobileNo { get; set; } = string.Empty;

        [JsonPropertyName("secretKey")]
        [JsonProperty("secretKey")]
        public string SecretKey { get; set; } = string.Empty;

        [JsonPropertyName("token")]
        [JsonProperty("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("verifyDate")]
        [JsonProperty("verifyDate")]
        public DateTime? VerifyDate { get; set; }

        [JsonPropertyName("otpauth_url")]
        [JsonProperty("otpauth_url")]
        public string? OtpauthUrl { get; set; }

        [JsonPropertyName("portal_id")]
        [JsonProperty("portal_id")]
        public long PortalId { get; set; } = 1;

        [JsonPropertyName("user_id")]
        [JsonProperty("user_id")]
        public string? UserId { get; set; }

        [JsonPropertyName("is_verified")]
        [JsonProperty("is_verified")]
        public string IsVerified { get; set; } = "Y";
    }

    /// <summary>
    /// Model representing the response from external portal verify-otp API.
    /// </summary>
    public class ExternalVerifyOtpResponse
    {
        [JsonPropertyName("status")]
        [JsonProperty("status")]
        public bool Status { get; set; }

        [JsonPropertyName("message")]
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        [JsonProperty("data")]
        public ExternalVerifyOtpData? Data { get; set; }
    }

    /// <summary>
    /// Model representing the payload inside external portal verify-otp response data.
    /// </summary>
    public class ExternalVerifyOtpData
    {
        [JsonPropertyName("a_Id")]
        [JsonProperty("a_Id")]
        public long A_Id { get; set; }

        [JsonPropertyName("beneficiaryID")]
        [JsonProperty("beneficiaryID")]
        public string? BeneficiaryID { get; set; }

        [JsonPropertyName("mobileNo")]
        [JsonProperty("mobileNo")]
        public string? MobileNo { get; set; }

        [JsonPropertyName("secretKey")]
        [JsonProperty("secretKey")]
        public string? SecretKey { get; set; }

        [JsonPropertyName("token")]
        [JsonProperty("token")]
        public string? Token { get; set; }

        [JsonPropertyName("verifyDate")]
        [JsonProperty("verifyDate")]
        public DateTime? VerifyDate { get; set; }
    }

    /// <summary>
    /// Swagger schema documentation model for VerifyOtp response.
    /// </summary>
    public class VerifyOtpResponse
    {
        [JsonPropertyName("status")]
        [JsonProperty("status")]
        public bool Status { get; set; }

        [JsonPropertyName("message")]
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        [JsonProperty("data")]
        public VerifyOtpData? Data { get; set; }
    }

    /// <summary>
    /// Model representing a single beneficiary message retrieved from external portal API (eLabharthi).
    /// </summary>
    public class BeneficiaryMessageItem
    {
        [JsonPropertyName("message_Id")]
        [JsonProperty("message_Id")]
        public long Message_Id { get; set; }

        [JsonPropertyName("benificiary_No")]
        [JsonProperty("benificiary_No")]
        public string? Benificiary_No { get; set; }

        [JsonPropertyName("mobNo")]
        [JsonProperty("mobNo")]
        public string? MobNo { get; set; }

        [JsonPropertyName("message_Head")]
        [JsonProperty("message_Head")]
        public string? Message_Head { get; set; }

        [JsonPropertyName("message_Body")]
        [JsonProperty("message_Body")]
        public string? Message_Body { get; set; }

        [JsonPropertyName("type")]
        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonPropertyName("pic_Path1")]
        [JsonProperty("pic_Path1")]
        public string? Pic_Path1 { get; set; }

        [JsonPropertyName("pic_Path2")]
        [JsonProperty("pic_Path2")]
        public string? Pic_Path2 { get; set; }

        [JsonPropertyName("video_Path")]
        [JsonProperty("video_Path")]
        public string? Video_Path { get; set; }

        [JsonPropertyName("doc_Path")]
        [JsonProperty("doc_Path")]
        public string? Doc_Path { get; set; }

        [JsonPropertyName("createdBy")]
        [JsonProperty("createdBy")]
        public string? CreatedBy { get; set; }

        [JsonPropertyName("createdDate")]
        [JsonProperty("createdDate")]
        public DateTime? CreatedDate { get; set; }
    }

    /// <summary>
    /// Payload returned by external eLabharthi API for Get_Beneficiary_Messages.
    /// </summary>
    public class ExternalBeneficiaryMessageResponse
    {
        [JsonPropertyName("status")]
        [JsonProperty("status")]
        public bool Status { get; set; }

        [JsonPropertyName("message")]
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        [JsonProperty("data")]
        public List<BeneficiaryMessageItem>? Data { get; set; }
    }

    /// <summary>
    /// Request model for fetching beneficiary messages from eLabharthi API and syncing to database.
    /// </summary>
    public class GetBeneficiaryMessagesRequest
    {
        /// <summary>
        /// Portal identifier from mst_portal table.
        /// </summary>
        /// <example>1</example>
        [JsonPropertyName("portal_id")]
        [JsonProperty("portal_id")]
        public long PortalId { get; set; }
        /// <summary>
        /// Authorization token for accessing Get_Beneficiary_Messages API.
        /// </summary>
        /// <example>408C6E3F4D04A7F7C1608193407356D8</example>
        [JsonPropertyName("token")]
        [JsonProperty("token")]
        public string Token { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request model for fetching beneficiary/pensioner status from external eLabharthi API.
    /// </summary>
    public class GetBeneficiaryStatusRequest
    {
        /// <summary>
        /// Portal identifier from mst_portal table (defaults to 1 for eLabharthi).
        /// </summary>
        /// <example>1</example>
        [JsonPropertyName("portalId")]
        [JsonProperty("portalId")]
        public long PortalId { get; set; } = 1;

        /// <summary>
        /// Registered 10-digit mobile number of the applicant.
        /// </summary>
        /// <example>9097372728</example>
        [JsonPropertyName("mobileNo")]
        [JsonProperty("mobileNo")]
        public string MobileNo { get; set; } = string.Empty;

        /// <summary>
        /// Beneficiary identification number.
        /// </summary>
        /// <example>000000200577</example>
        [JsonPropertyName("beneficiaryNo")]
        [JsonProperty("beneficiaryNo")]
        public string BeneficiaryNo { get; set; } = string.Empty;
    }

    /// <summary>
    /// Key-value label pair inside beneficiary details list.
    /// </summary>
    public class BeneficiaryDetailItem
    {
        [JsonPropertyName("label")]
        [JsonProperty("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        [JsonProperty("value")]
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data payload returned by Get_Beneficiary_Status API.
    /// </summary>
    public class BeneficiaryStatusData
    {
        [JsonPropertyName("firstName")]
        [JsonProperty("firstName")]
        public string? FirstName { get; set; }

        [JsonPropertyName("fatherHusbandName")]
        [JsonProperty("fatherHusbandName")]
        public string? FatherHusbandName { get; set; }

        [JsonPropertyName("dateofBirth")]
        [JsonProperty("dateofBirth")]
        public string? DateofBirth { get; set; }

        [JsonPropertyName("gender")]
        [JsonProperty("gender")]
        public string? Gender { get; set; }

        [JsonPropertyName("mobileNo")]
        [JsonProperty("mobileNo")]
        public string? MobileNo { get; set; }

        [JsonPropertyName("beneficiaryDetails")]
        [JsonProperty("beneficiaryDetails")]
        public List<BeneficiaryDetailItem> BeneficiaryDetails { get; set; } = new List<BeneficiaryDetailItem>();
    }

    /// <summary>
    /// Swagger schema documentation model for GetBeneficiaryStatus response.
    /// </summary>
    public class BeneficiaryStatusResponse
    {
        [JsonPropertyName("status")]
        [JsonProperty("status")]
        public bool Status { get; set; }

        [JsonPropertyName("code")]
        [JsonProperty("code")]
        public int Code { get; set; } = 200;

        [JsonPropertyName("message")]
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        [JsonProperty("data")]
        public BeneficiaryStatusData? Data { get; set; }
    }

    /// <summary>
    /// Saved beneficiary message record with local database identifiers.
    /// </summary>
    public class SavedBeneficiaryMessageDto : BeneficiaryMessageItem
    {
        [JsonPropertyName("msg_id")]
        [JsonProperty("msg_id")]
        public long MsgId { get; set; }

        [JsonPropertyName("msg_log_id")]
        [JsonProperty("msg_log_id")]
        public long MsgLogId { get; set; }

        [JsonPropertyName("portal_id")]
        [JsonProperty("portal_id")]
        public long PortalId { get; set; }

        [JsonPropertyName("device_id")]
        [JsonProperty("device_id")]
        public string? DeviceId { get; set; }

        [JsonPropertyName("is_synced")]
        [JsonProperty("is_synced")]
        public string IsSynced { get; set; } = "Yes";

        [JsonPropertyName("sync_date")]
        [JsonProperty("sync_date")]
        public DateTime? SyncDate { get; set; }
    }

    /// <summary>
    /// Response model for GetBeneficiaryMessages.
    /// </summary>
    public class GetBeneficiaryMessagesResponse
    {
        [JsonPropertyName("status")]
        [JsonProperty("status")]
        public bool Status { get; set; }

        [JsonPropertyName("message")]
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("count")]
        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonPropertyName("data")]
        [JsonProperty("data")]
        public List<SavedBeneficiaryMessageDto> Data { get; set; } = new List<SavedBeneficiaryMessageDto>();
    }
}
