using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace SankalpAPI.Model
{
    public class PortalDto
    {
        [JsonPropertyName("portal_id")]
        [JsonProperty("portal_id")]
        public long PortalId { get; set; }

        [JsonPropertyName("portal_name")]
        [JsonProperty("portal_name")]
        public string? PortalName { get; set; }

        [JsonPropertyName("portal_url")]
        [JsonProperty("portal_url")]
        public string? PortalUrl { get; set; }

        [JsonPropertyName("portal_image_path")]
        [JsonProperty("portal_image_path")]
        public string? PortalImagePath { get; set; }

        [JsonPropertyName("is_active")]
        [JsonProperty("is_active")]
        public string? IsActive { get; set; }
    }

    public class UserTypeDto
    {
        [JsonPropertyName("Portal_ID")]
        [JsonProperty("Portal_ID")]
        public long PortalId { get; set; }

        [JsonPropertyName("User_Type_ID")]
        [JsonProperty("User_Type_ID")]
        public string? UserType_id { get; set; }

        [JsonPropertyName("User_Type_Name")]
        [JsonProperty("User_Type_Name")]
        public string? UserType_name { get; set; }

        [JsonPropertyName("User_Type_Name_hn")]
        [JsonProperty("User_Type_Name_hn")]
        public string? UserType_name_hn { get; set; }
       
    }

    public class PortalListResponse
    {
        public bool Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<PortalDto> Data { get; set; } = new List<PortalDto>();
    }

    public class PortalGetRequest
    {
        [JsonPropertyName("portal_id")]
        [JsonProperty("portal_id")]
        public long PortalId { get; set; }
    }

    /// <summary>
    /// Request model for connecting to an external portal through SankalpAPI.
    /// Uses mst_portal to resolve URL and tbl_map_user_portal to inject credentials.
    /// </summary>
    public class PortalConnectRequest
    {
        /// <summary>
        /// Portal identifier from mst_portal table.
        /// </summary>
        /// <example>1</example>
        [JsonPropertyName("portal_id")]
        [JsonProperty("portal_id")]
        public long PortalId { get; set; }

        /// <summary>
        /// Registered mobile number of the user.
        /// </summary>
        /// <example>9876543210</example>
        [JsonPropertyName("mobile")]
        [JsonProperty("mobile")]
        public string Mobile { get; set; } = string.Empty;

        /// <summary>
        /// Optional Financial Year (e.g. "2024-2025", "2024-25"), Calendar Year (e.g. "2024"), or Session Token.
        /// </summary>
        /// <example>2024-2025</example>
        [JsonPropertyName("session")]
        [JsonProperty("session")]
        public string? Session { get; set; }

        /// <summary>
        /// Unique device identifier of the mobile installation.
        /// </summary>
        /// <example>DEVICE123456</example>
        [JsonPropertyName("device_id")]
        [JsonProperty("device_id")]
        public string? DeviceId { get; set; }

        /// <summary>
        /// Client authentication key.
        /// </summary>
        /// <example>CLIENT_KEY_SECRET_789</example>
        [JsonPropertyName("client_key")]
        [JsonProperty("client_key")]
        public string? ClientKey { get; set; }
    }

    /// <summary>
    /// Standardized response returned after communicating with an external portal.
    /// </summary>
    public class PortalConnectResponse
    {
        public bool Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public long PortalId { get; set; }
        public string? PortalName { get; set; }
        public string? TargetUrl { get; set; }
        public object? Data { get; set; }
    }

    /// <summary>
    /// DTO representing mst_portal joined with tbl_map_user_portal.
    /// </summary>
    public class PortalUserMappingDto
    {
        public long PortalId { get; set; }
        public string PortalName { get; set; } = string.Empty;
        public string? PortalUrl { get; set; }
        public string? IsActive { get; set; }
        public long? UserPortalId { get; set; }
        public string? Mobile { get; set; }
        public string? UserId { get; set; }
        public string? SecretKey { get; set; }
        public string? Token { get; set; }
    }

    /// <summary>
  
    public class MapUserPortalRequest
    {
        [JsonPropertyName("portal_id")]
        [JsonProperty("portal_id")]
        public long? PortalId { get; set; }

        [JsonPropertyName("user_type_id")]
        [JsonProperty("user_type_id")]
        public long? UserTypeId { get; set; }
    }

    /// <summary>
    /// DTO representing a registered user with an optional portal mapping, portal, and user type.
    /// </summary>
    public class MapUserPortalDto
    {
        [JsonPropertyName("user_portal_id")]
        [JsonProperty("user_portal_id")]
        public long? UserPortalId { get; set; }

        [JsonPropertyName("portal_id")]
        [JsonProperty("portal_id")]
        public long? PortalId { get; set; }

        [JsonPropertyName("portal_name")]
        [JsonProperty("portal_name")]
        public string? PortalName { get; set; }

        [JsonPropertyName("user_type_id")]
        [JsonProperty("user_type_id")]
        public long? UserTypeId { get; set; }

        [JsonPropertyName("user_type_name")]
        [JsonProperty("user_type_name")]
        public string? UserTypeName { get; set; }

        [JsonPropertyName("mobile")]
        [JsonProperty("mobile")]
        public string? Mobile { get; set; }

        [JsonPropertyName("name")]
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonPropertyName("userid")]
        [JsonProperty("userid")]
        public string? UserId { get; set; }

        [JsonPropertyName("deviceid")]
        [JsonProperty("deviceid")]
        public string? DeviceId { get; set; }

        [JsonPropertyName("district_code")]
        [JsonProperty("district_code")]
        public string? DistrictCode { get; set; }

        [JsonPropertyName("block_code")]
        [JsonProperty("block_code")]
        public string? BlockCode { get; set; }

        [JsonPropertyName("panchayatcode")]
        [JsonProperty("panchayatcode")]
        public string? PanchayatCode { get; set; }

        [JsonPropertyName("entry_by")]
        [JsonProperty("entry_by")]
        public string? EntryBy { get; set; }

        [JsonPropertyName("entry_date")]
        [JsonProperty("entry_date")]
        public DateTime? EntryDate { get; set; }
    }

    /// <summary>
    /// Standardized response for mapped user-portal records.
    /// </summary>
    public class MapUserPortalResponse
    {
        public bool Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public int Count { get; set; }
        public List<MapUserPortalDto> Data { get; set; } = new List<MapUserPortalDto>();
    }
}
