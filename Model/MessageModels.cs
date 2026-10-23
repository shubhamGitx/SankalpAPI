using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using SankalpAPI.Helper;

namespace SankalpAPI.Model
{
    public class GetUnsyncedMessagesRequest
    {
        public string DeviceID { get; set; } = string.Empty;
        public long? PortalId { get; set; }
        public string? ClientKey { get; set; }
    }

    public class PersonalizedMessageDto
    {
        public long msg_id { get; set; }
        public long? msg_log_id { get; set; }
        public long? notification_id => msg_log_id;
        public long? PortalId { get; set; }
        public string? PortalName { get; set; }
        public string? MessageHead { get; set; }
        public string? MessageBody { get; set; }
        public string? photo1path { get; set; }
        public string? photo2path { get; set; }
        public string? videopath { get; set; }
        public string? documentpath { get; set; }
        public string? youtubeurl { get; set; }
        public string? facebookurl { get; set; }
        public string? instagramurl { get; set; }
        public string? Xurl { get; set; }
        public string? Interest_ID { get; set; }
        public string DeviceID { get; set; } = string.Empty;
        public string IsDeleted { get; set; } = string.Empty;

        /// <summary>
        /// Matches the 'Synced' column from the architecture diagram table.
        /// </summary>
        public string Synced { get; set; } = "Yes";

        public string IsSynced { get; set; } = "Yes";
        public short? ValidityInMinutes { get; set; }
        public DateTime? SyncDate { get; set; }
        public DateTime? EntryDate { get; set; }

        /// <summary>
        /// If the message text contains structured JSON, it is parsed and exposed here as an object.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object? MessageJson
        {
            get
            {
                if (string.IsNullOrWhiteSpace(MessageBody)) return null;
                string trimmed = MessageBody.Trim();
                if ((trimmed.StartsWith("{") && trimmed.EndsWith("}")) || (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
                {
                    try
                    {
                        return JsonConvert.DeserializeObject(trimmed);
                    }
                    catch
                    {
                        return null;
                    }
                }
                return null;
            }
        }
    }

    public class GetUnsyncedMessagesResponse
    {
        public bool Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public int Count { get; set; }
        public List<PersonalizedMessageDto> Data { get; set; } = new List<PersonalizedMessageDto>();
    }

    public class CreateNotificationRequest
    {
       
        public string DeviceID { get; set; } = string.Empty;
        public string? msg_id { get; set; }
        public string? Interest_ID { get; set; }       
        public string? EntryBy { get; set; } = "System";
    }

    public class MarkSyncedRequest
    {
        public List<long> NotificationIds { get; set; } = new List<long>();
        public string? DeviceID { get; set; }
        public string? ClientKey { get; set; }
    }

    public class GetBroadCastMessageRequest
    {
        public string DeviceID { get; set; } = string.Empty;            
   
    }

    public class GetMessageHistoryRequest
    {
        public string DeviceID { get; set; } = string.Empty;
        public string? ClientKey { get; set; }

        /// <summary>
        /// Interest ID(s) to filter by. Can pass a single ID ("1"), comma-separated IDs ("1,2,3,4,5"), or an array [1, 2, 3].
        /// </summary>
        /// <example>1,2,3,4,5</example>
        [System.Text.Json.Serialization.JsonPropertyName("interest_id")]
        [Newtonsoft.Json.JsonProperty("interest_id")]
        [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleStringConverter))]
        [Newtonsoft.Json.JsonConverter(typeof(FlexibleNewtonsoftStringConverter))]
        public string? interest_id { get; set; }

        /// <summary>
        /// Extracts and consolidates all interest IDs into a clean list of unique IDs.
        /// </summary>
        public List<string> GetInterestIdList()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(interest_id))
            {
                var parts = interest_id.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var p in parts)
                {
                    if (!string.IsNullOrWhiteSpace(p))
                    {
                        result.Add(p);
                    }
                }
            }

            return result.ToList();
        }
    }

    public class BroadcastMessageDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("bmsg_id")]
        [Newtonsoft.Json.JsonProperty("bmsg_id")]
        public long Bmsg_id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("msg_id")]
        [Newtonsoft.Json.JsonProperty("msg_id")]
        public long msg_id => Bmsg_id;

        [System.Text.Json.Serialization.JsonPropertyName("mesg_type_id")]
        [Newtonsoft.Json.JsonProperty("mesg_type_id")]
        public long? MesgTypeId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message_head")]
        [Newtonsoft.Json.JsonProperty("message_head")]
        public string? MessageHead { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message_body")]
        [Newtonsoft.Json.JsonProperty("message_body")]
        public string? MessageBody { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("validity_in_minutes")]
        [Newtonsoft.Json.JsonProperty("validity_in_minutes")]
        public short? ValidityInMinutes { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("is_active")]
        [Newtonsoft.Json.JsonProperty("is_active")]
        public string? IsActive { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("portal_id")]
        [Newtonsoft.Json.JsonProperty("portal_id")]
        public long? PortalId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("user_type_id")]
        [Newtonsoft.Json.JsonProperty("user_type_id")]
        public string? UserTypeId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("photo1path")]
        [Newtonsoft.Json.JsonProperty("photo1path")]
        public string? photo1path { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("photo2path")]
        [Newtonsoft.Json.JsonProperty("photo2path")]
        public string? photo2path { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("videopath")]
        [Newtonsoft.Json.JsonProperty("videopath")]
        public string? videopath { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("documentpath")]
        [Newtonsoft.Json.JsonProperty("documentpath")]
        public string? documentpath { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("entryby")]
        [Newtonsoft.Json.JsonProperty("entryby")]
        public string? EntryBy { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("entrydate")]
        [Newtonsoft.Json.JsonProperty("entrydate")]
        public DateTime? EntryDate { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("updatedby")]
        [Newtonsoft.Json.JsonProperty("updatedby")]
        public string? UpdatedBy { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("updateddate")]
        [Newtonsoft.Json.JsonProperty("updateddate")]
        public DateTime? UpdatedDate { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("is_synced")]
        [Newtonsoft.Json.JsonProperty("is_synced")]
        public string? IsSynced { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("is_deleted")]
        [Newtonsoft.Json.JsonProperty("IsDeleted")]
        public string? IsDeleted { get; set; }


        [System.Text.Json.Serialization.JsonPropertyName("MesgTypeID")]
        [Newtonsoft.Json.JsonProperty("MesgTypeID")]
        public string? MesgTypeID { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("is_verified")]
        [Newtonsoft.Json.JsonProperty("is_verified")]
        public bool? IsVerified { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("is_approved")]
        [Newtonsoft.Json.JsonProperty("is_approved")]
        public bool? IsApproved { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("is_all_district")]
        [Newtonsoft.Json.JsonProperty("is_all_district")]
        public string? IsAllDistrict { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("verified_by")]
        [Newtonsoft.Json.JsonProperty("verified_by")]
        public string? VerifiedBy { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("verified_date")]
        [Newtonsoft.Json.JsonProperty("verified_date")]
        public DateTime? VerifiedDate { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("verified_ip")]
        [Newtonsoft.Json.JsonProperty("verified_ip")]
        public string? VerifiedIP { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("approved_by")]
        [Newtonsoft.Json.JsonProperty("approved_by")]
        public string? ApprovedBy { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("approved_date")]
        [Newtonsoft.Json.JsonProperty("approved_date")]
        public DateTime? ApprovedDate { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("approved_ip")]
        [Newtonsoft.Json.JsonProperty("approved_ip")]
        public string? ApprovedIP { get; set; }

        /// <summary>
        /// If the message text contains structured JSON, it is parsed and exposed here as an object.
        /// </summary>
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public object? MessageJson
        {
            get
            {
                if (string.IsNullOrWhiteSpace(MessageBody)) return null;
                string trimmed = MessageBody.Trim();
                if ((trimmed.StartsWith("{") && trimmed.EndsWith("}")) || (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
                {
                    try
                    {
                        return Newtonsoft.Json.JsonConvert.DeserializeObject(trimmed);
                    }
                    catch
                    {
                        return null;
                    }
                }
                return null;
            }
        }
    }

    public class InsertBroadcastMessageRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("message_head")]
        [Newtonsoft.Json.JsonProperty("message_head")]
        public string? MessageHead { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message_body")]
        [Newtonsoft.Json.JsonProperty("message_body")]
        public string MessageBody { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("validity_in_minutes")]
        [Newtonsoft.Json.JsonProperty("validity_in_minutes")]
        public short? ValidityInMinutes { get; set; } = 60;

        [System.Text.Json.Serialization.JsonPropertyName("is_active")]
        [Newtonsoft.Json.JsonProperty("is_active")]
        public string? IsActive { get; set; } = "Y";

        [System.Text.Json.Serialization.JsonPropertyName("portal_id")]
        [Newtonsoft.Json.JsonProperty("portal_id")]
        public long? PortalId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("user_type_id")]
        [Newtonsoft.Json.JsonProperty("user_type_id")]
        public string? UserTypeId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("photo1path")]
        [Newtonsoft.Json.JsonProperty("photo1path")]
        public string? Photo1Path { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("photo2path")]
        [Newtonsoft.Json.JsonProperty("photo2path")]
        public string? Photo2Path { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("videopath")]
        [Newtonsoft.Json.JsonProperty("videopath")]
        public string? VideoPath { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("documentpath")]
        [Newtonsoft.Json.JsonProperty("documentpath")]
        public string? DocumentPath { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("mesg_type_id")]
        [Newtonsoft.Json.JsonProperty("mesg_type_id")]
        public long? MesgTypeId { get; set; } = 1;

        [System.Text.Json.Serialization.JsonPropertyName("is_all_district")]
        [Newtonsoft.Json.JsonProperty("is_all_district")]
        public bool IsAllDistrict { get; set; } = true;

        [System.Text.Json.Serialization.JsonPropertyName("district_codes")]
        [Newtonsoft.Json.JsonProperty("district_codes")]
        public List<string>? DistrictCodes { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("entryby")]
        [Newtonsoft.Json.JsonProperty("entryby")]
        public string? EntryBy { get; set; } = "Admin";

        [System.Text.Json.Serialization.JsonPropertyName("client_key")]
        [Newtonsoft.Json.JsonProperty("client_key")]
        public string? ClientKey { get; set; }
    }

    public class MessageTypeDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("mesg_type_id")]
        [Newtonsoft.Json.JsonProperty("mesg_type_id")]
        public long MesgTypeId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("mesg_type_name")]
        [Newtonsoft.Json.JsonProperty("mesg_type_name")]
        public string MesgTypeName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("msg_abbr_code")]
        [Newtonsoft.Json.JsonProperty("msg_abbr_code")]
        public string MsgAbbrCode { get; set; } = string.Empty;
    }

    public class InsertBroadcastMessageResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("status")]
        [Newtonsoft.Json.JsonProperty("status")]
        public bool Status { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message")]
        [Newtonsoft.Json.JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("bmsg_id")]
        [Newtonsoft.Json.JsonProperty("bmsg_id", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public long? Bmsg_id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("data")]
        [Newtonsoft.Json.JsonProperty("data", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public BroadcastMessageDto? Data { get; set; }
    }

    public class InsertInterestWiseMessageRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("interest_id")]
        [Newtonsoft.Json.JsonProperty("interest_id")]
        public long InterestId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message_head")]
        [Newtonsoft.Json.JsonProperty("message_head")]
        public string? MessageHead { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message_body")]
        [Newtonsoft.Json.JsonProperty("message_body")]
        public string MessageBody { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("validity_in_minutes")]
        [Newtonsoft.Json.JsonProperty("validity_in_minutes")]
        public short? ValidityInMinutes { get; set; } = 60;

        [System.Text.Json.Serialization.JsonPropertyName("is_active")]
        [Newtonsoft.Json.JsonProperty("is_active")]
        public string? IsActive { get; set; } = "Y";

        [System.Text.Json.Serialization.JsonPropertyName("photo1path")]
        [Newtonsoft.Json.JsonProperty("photo1path")]
        public string? Photo1Path { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("photo2path")]
        [Newtonsoft.Json.JsonProperty("photo2path")]
        public string? Photo2Path { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("videopath")]
        [Newtonsoft.Json.JsonProperty("videopath")]
        public string? VideoPath { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("documentpath")]
        [Newtonsoft.Json.JsonProperty("documentpath")]
        public string? DocumentPath { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("youtubeurl")]
        [Newtonsoft.Json.JsonProperty("youtubeurl")]
        public string? YoutubeUrl { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("facebookurl")]
        [Newtonsoft.Json.JsonProperty("facebookurl")]
        public string? FacebookUrl { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("instagramurl")]
        [Newtonsoft.Json.JsonProperty("instagramurl")]
        public string? InstagramUrl { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("Xurl")]
        [Newtonsoft.Json.JsonProperty("Xurl")]
        public string? XUrl { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("entryby")]
        [Newtonsoft.Json.JsonProperty("entryby")]
        public string? EntryBy { get; set; } = "Admin";

        [System.Text.Json.Serialization.JsonPropertyName("client_key")]
        [Newtonsoft.Json.JsonProperty("client_key")]
        public string? ClientKey { get; set; }
    }

    public class InsertInterestWiseMessageResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("status")]
        [Newtonsoft.Json.JsonProperty("status")]
        public bool Status { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message")]
        [Newtonsoft.Json.JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("msg_id")]
        [Newtonsoft.Json.JsonProperty("msg_id", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public long? MsgId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("data")]
        [Newtonsoft.Json.JsonProperty("data", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public PersonalizedMessageDto? Data { get; set; }
    }

    public class InterestWiseMessageDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("msg_id")]
        [Newtonsoft.Json.JsonProperty("msg_id")]
        public long MsgId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("interest_id")]
        [Newtonsoft.Json.JsonProperty("interest_id")]
        public long? InterestId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("interest_name")]
        [Newtonsoft.Json.JsonProperty("interest_name")]
        public string? InterestName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message_head")]
        [Newtonsoft.Json.JsonProperty("message_head")]
        public string? MessageHead { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message_body")]
        [Newtonsoft.Json.JsonProperty("message_body")]
        public string? MessageBody { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("validity_in_minutes")]
        [Newtonsoft.Json.JsonProperty("validity_in_minutes")]
        public short? ValidityInMinutes { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("is_active")]
        [Newtonsoft.Json.JsonProperty("is_active")]
        public string? IsActive { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("photo1path")]
        [Newtonsoft.Json.JsonProperty("photo1path")]
        public string? Photo1Path { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("photo2path")]
        [Newtonsoft.Json.JsonProperty("photo2path")]
        public string? Photo2Path { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("videopath")]
        [Newtonsoft.Json.JsonProperty("videopath")]
        public string? VideoPath { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("documentpath")]
        [Newtonsoft.Json.JsonProperty("documentpath")]
        public string? DocumentPath { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("youtubeurl")]
        [Newtonsoft.Json.JsonProperty("youtubeurl")]
        public string? YoutubeUrl { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("facebookurl")]
        [Newtonsoft.Json.JsonProperty("facebookurl")]
        public string? FacebookUrl { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("instagramurl")]
        [Newtonsoft.Json.JsonProperty("instagramurl")]
        public string? InstagramUrl { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("Xurl")]
        [Newtonsoft.Json.JsonProperty("Xurl")]
        public string? XUrl { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("entryby")]
        [Newtonsoft.Json.JsonProperty("entryby")]
        public string? EntryBy { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("entrydate")]
        [Newtonsoft.Json.JsonProperty("entrydate")]
        public DateTime? EntryDate { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("is_deleted")]
        [Newtonsoft.Json.JsonProperty("is_deleted")]
        public string? IsDeleted { get; set; }
    }

    public class ForwardDraftMessagesRequest
    {
        [JsonPropertyName("bmsg_ids")]
        [JsonProperty("bmsg_ids")]
        public List<long> BmsgIds { get; set; } = new();

        [JsonPropertyName("user_role")]
        [JsonProperty("user_role")]
        public string? UserRole { get; set; }

        [JsonPropertyName("action_by")]
        [JsonProperty("action_by")]
        public string? ActionBy { get; set; }

        [JsonPropertyName("action_ip")]
        [JsonProperty("action_ip")]
        public string? ActionIP { get; set; }
    }

    public class ForwardDraftMessagesResponse
    {
        public bool Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ProcessedCount { get; set; }
    }

    /// <summary>
    /// Request payload to update verification status of a broadcast message.
    /// </summary>
    public class UpdateBroadcastVerifiedRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("Bmsg_id")]
        [Newtonsoft.Json.JsonProperty("Bmsg_id")]
        public long Bmsg_id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("IsVerified")]
        [Newtonsoft.Json.JsonProperty("IsVerified")]
        public bool IsVerified { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("VerifiedBy")]
        [Newtonsoft.Json.JsonProperty("VerifiedBy")]
        public string? VerifiedBy { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("VerifiedIP")]
        [Newtonsoft.Json.JsonProperty("VerifiedIP")]
        public string? VerifiedIP { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("client_key")]
        [Newtonsoft.Json.JsonProperty("client_key")]
        public string? ClientKey { get; set; }
    }

    /// <summary>
    /// Request payload to update approval status of a broadcast message.
    /// </summary>
    public class UpdateBroadcastApprovedRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("Bmsg_id")]
        [Newtonsoft.Json.JsonProperty("Bmsg_id")]
        public long Bmsg_id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("IsApproved")]
        [Newtonsoft.Json.JsonProperty("IsApproved")]
        public bool IsApproved { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("ApprovedBy")]
        [Newtonsoft.Json.JsonProperty("ApprovedBy")]
        public string? ApprovedBy { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("ApprovedIP")]
        [Newtonsoft.Json.JsonProperty("ApprovedIP")]
        public string? ApprovedIP { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("client_key")]
        [Newtonsoft.Json.JsonProperty("client_key")]
        public string? ClientKey { get; set; }
    }

    /// <summary>
    /// Standard response for updating broadcast message status (IsVerified or IsApproved).
    /// </summary>
    public class UpdateBroadcastStatusResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("status")]
        [Newtonsoft.Json.JsonProperty("status")]
        public bool Status { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message")]
        [Newtonsoft.Json.JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("bmsg_id")]
        [Newtonsoft.Json.JsonProperty("bmsg_id", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public long? Bmsg_id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("is_verified")]
        [Newtonsoft.Json.JsonProperty("is_verified", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool? IsVerified { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("is_approved")]
        [Newtonsoft.Json.JsonProperty("is_approved", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool? IsApproved { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public int StatusCode { get; set; } = 200;
    }
}
