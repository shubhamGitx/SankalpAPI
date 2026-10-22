using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace SankalpAPI.Model
{
    public static class CampaignStatusConsts
    {
        public const string Draft = "DRAFT";
        public const string SubmittedForVerification = "SUBMITTED_FOR_VERIFICATION";
        public const string RejectedByCkr = "REJECTED_BY_CKR";
        public const string PendingApproval = "PENDING_APPROVAL";
        public const string RejectedByApr = "REJECTED_BY_APR";
        public const string Approved = "APPROVED";
        public const string Sending = "SENDING";
        public const string Sent = "SENT";
        public const string SendFailed = "SEND_FAILED";

        // Aliases for backwards compatibility
        public const string LegacyPendingVerification = "Pending Verification";
        public const string LegacyPendingApproval = "Pending Approval";
        public const string LegacyApproved = "Approved";
        public const string LegacyScheduled = "Scheduled";
        public const string LegacySending = "Sending";
        public const string LegacyCompleted = "Completed";
        public const string LegacyFailed = "Failed";
        public const string Cancelled = "Cancelled";

        public static string Normalize(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return Draft;
            string s = status.Trim();
            if (string.Equals(s, "Draft", StringComparison.OrdinalIgnoreCase) || string.Equals(s, Draft, StringComparison.OrdinalIgnoreCase))
                return Draft;
            if (string.Equals(s, "Pending Verification", StringComparison.OrdinalIgnoreCase) || string.Equals(s, SubmittedForVerification, StringComparison.OrdinalIgnoreCase))
                return SubmittedForVerification;
            if (string.Equals(s, RejectedByCkr, StringComparison.OrdinalIgnoreCase))
                return RejectedByCkr;
            if (string.Equals(s, "Pending Approval", StringComparison.OrdinalIgnoreCase) || string.Equals(s, PendingApproval, StringComparison.OrdinalIgnoreCase))
                return PendingApproval;
            if (string.Equals(s, RejectedByApr, StringComparison.OrdinalIgnoreCase))
                return RejectedByApr;
            if (string.Equals(s, "Approved", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "Scheduled", StringComparison.OrdinalIgnoreCase) || string.Equals(s, Approved, StringComparison.OrdinalIgnoreCase))
                return Approved;
            if (string.Equals(s, "Sending", StringComparison.OrdinalIgnoreCase) || string.Equals(s, Sending, StringComparison.OrdinalIgnoreCase))
                return Sending;
            if (string.Equals(s, "Completed", StringComparison.OrdinalIgnoreCase) || string.Equals(s, Sent, StringComparison.OrdinalIgnoreCase))
                return Sent;
            if (string.Equals(s, "Failed", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "Partially Failed", StringComparison.OrdinalIgnoreCase) || string.Equals(s, SendFailed, StringComparison.OrdinalIgnoreCase))
                return SendFailed;

            return s.ToUpperInvariant();
        }
    }

    public static class CampaignRoles
    {
        public const string Maker = "MKR";
        public const string Checker = "CKR";
        public const string Approver = "APR";

        public static bool IsMaker(string? role) =>
            string.Equals(role?.Trim(), Maker, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role?.Trim(), "MAKER", StringComparison.OrdinalIgnoreCase);

        public static bool IsChecker(string? role) =>
            string.Equals(role?.Trim(), Checker, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role?.Trim(), "CHECKER", StringComparison.OrdinalIgnoreCase);

        public static bool IsApprover(string? role) =>
            string.Equals(role?.Trim(), Approver, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role?.Trim(), "APPROVER", StringComparison.OrdinalIgnoreCase);

        public static string NormalizeRole(string? role)
        {
            if (IsMaker(role)) return Maker;
            if (IsChecker(role)) return Checker;
            if (IsApprover(role)) return Approver;
            return role?.Trim().ToUpperInvariant() ?? Maker;
        }
    }

    public class CreateCampaignRequest
    {
        [JsonPropertyName("campaign_name")]
        [JsonProperty("campaign_name")]
        public string CampaignName { get; set; } = string.Empty;

        [JsonPropertyName("campaign_description")]
        [JsonProperty("campaign_description")]
        public string? CampaignDescription { get; set; }

        [JsonPropertyName("bmsg_id")]
        [JsonProperty("bmsg_id")]
        public long? BmsgId { get; set; }

        [JsonPropertyName("new_broadcast_message")]
        [JsonProperty("new_broadcast_message")]
        public InsertBroadcastMessageRequest? NewBroadcastMessage { get; set; }

        [JsonPropertyName("message_details")]
        [JsonProperty("message_details")]
        public InsertBroadcastMessageRequest? MessageDetails
        {
            get => NewBroadcastMessage;
            set => NewBroadcastMessage = value;
        }

        [JsonPropertyName("start_date")]
        [JsonProperty("start_date")]
        public DateTime? StartDate { get; set; }

        [JsonPropertyName("end_date")]
        [JsonProperty("end_date")]
        public DateTime? EndDate { get; set; }

        [JsonPropertyName("scheduled_send_time")]
        [JsonProperty("scheduled_send_time")]
        public DateTime? ScheduledSendTime { get; set; }

        [JsonPropertyName("target_scope")]
        [JsonProperty("target_scope")]
        public string TargetScope { get; set; } = "ALL"; // ALL, PORTAL, USER_TYPE, LOCATION

        [JsonPropertyName("portal_id")]
        [JsonProperty("portal_id")]
        public long? PortalId { get; set; }

        [JsonPropertyName("user_type_id")]
        [JsonProperty("user_type_id")]
        public long? UserTypeId { get; set; }

        [JsonPropertyName("is_all_district")]
        [JsonProperty("is_all_district")]
        public string IsAllDistrict { get; set; } = "Y";

        [JsonPropertyName("is_all_block")]
        [JsonProperty("is_all_block")]
        public string IsAllBlock { get; set; } = "Y";

        [JsonPropertyName("is_all_panchayat")]
        [JsonProperty("is_all_panchayat")]
        public string IsAllPanchayat { get; set; } = "Y";

        [JsonPropertyName("target_locations")]
        [JsonProperty("target_locations")]
        public List<CampaignLocationDto>? TargetLocations { get; set; }

        [JsonPropertyName("submit_for_verification")]
        [JsonProperty("submit_for_verification")]
        public bool SubmitForVerification { get; set; } = false;

        [JsonPropertyName("created_by")]
        [JsonProperty("created_by")]
        public string CreatedBy { get; set; } = "Admin";
    }

    public class UpdateCampaignRequest
    {
        [JsonPropertyName("campaign_name")]
        [JsonProperty("campaign_name")]
        public string CampaignName { get; set; } = string.Empty;

        [JsonPropertyName("campaign_description")]
        [JsonProperty("campaign_description")]
        public string? CampaignDescription { get; set; }

        [JsonPropertyName("start_date")]
        [JsonProperty("start_date")]
        public DateTime? StartDate { get; set; }

        [JsonPropertyName("end_date")]
        [JsonProperty("end_date")]
        public DateTime? EndDate { get; set; }

        [JsonPropertyName("scheduled_send_time")]
        [JsonProperty("scheduled_send_time")]
        public DateTime? ScheduledSendTime { get; set; }

        [JsonPropertyName("target_scope")]
        [JsonProperty("target_scope")]
        public string TargetScope { get; set; } = "ALL";

        [JsonPropertyName("portal_id")]
        [JsonProperty("portal_id")]
        public long? PortalId { get; set; }

        [JsonPropertyName("user_type_id")]
        [JsonProperty("user_type_id")]
        public long? UserTypeId { get; set; }

        [JsonPropertyName("is_all_district")]
        [JsonProperty("is_all_district")]
        public string IsAllDistrict { get; set; } = "Y";

        [JsonPropertyName("is_all_block")]
        [JsonProperty("is_all_block")]
        public string IsAllBlock { get; set; } = "Y";

        [JsonPropertyName("is_all_panchayat")]
        [JsonProperty("is_all_panchayat")]
        public string IsAllPanchayat { get; set; } = "Y";

        [JsonPropertyName("target_locations")]
        [JsonProperty("target_locations")]
        public List<CampaignLocationDto>? TargetLocations { get; set; }

        [JsonPropertyName("submit_for_verification")]
        [JsonProperty("submit_for_verification")]
        public bool SubmitForVerification { get; set; } = false;

        [JsonPropertyName("updated_by")]
        [JsonProperty("updated_by")]
        public string UpdatedBy { get; set; } = "Admin";
    }

    public class CampaignLocationDto
    {
        [JsonPropertyName("district_code")]
        [JsonProperty("district_code")]
        public long? DistrictCode { get; set; }

        [JsonPropertyName("district_name")]
        [JsonProperty("district_name")]
        public string? DistrictName { get; set; }

        [JsonPropertyName("block_code")]
        [JsonProperty("block_code")]
        public long? BlockCode { get; set; }

        [JsonPropertyName("block_name")]
        [JsonProperty("block_name")]
        public string? BlockName { get; set; }

        [JsonPropertyName("panchayat_code")]
        [JsonProperty("panchayat_code")]
        public long? PanchayatCode { get; set; }
    }

    public class VerifyCampaignRequest
    {
        [JsonPropertyName("campaign_id")]
        [JsonProperty("campaign_id")]
        public long CampaignId { get; set; }

        [JsonPropertyName("is_verified")]
        [JsonProperty("is_verified")]
        public string IsVerified { get; set; } = "Y"; // Y or N

        [JsonPropertyName("verified_by")]
        [JsonProperty("verified_by")]
        public string VerifiedBy { get; set; } = string.Empty;

        [JsonPropertyName("verified_ip")]
        [JsonProperty("verified_ip")]
        public string? VerifiedIP { get; set; }

        [JsonPropertyName("remarks")]
        [JsonProperty("remarks")]
        public string? Remarks { get; set; }

        [JsonPropertyName("verified_remarks")]
        [JsonProperty("verified_remarks")]
        public string? VerifiedRemarks
        {
            get => Remarks;
            set => Remarks = value;
        }

        [JsonPropertyName("rejection_reason")]
        [JsonProperty("rejection_reason")]
        public string? RejectionReason { get; set; }
    }

    public class ApproveCampaignRequest
    {
        [JsonPropertyName("campaign_id")]
        [JsonProperty("campaign_id")]
        public long CampaignId { get; set; }

        [JsonPropertyName("is_approved")]
        [JsonProperty("is_approved")]
        public string IsApproved { get; set; } = "Y"; // Y or N

        [JsonPropertyName("approved_by")]
        [JsonProperty("approved_by")]
        public string ApprovedBy { get; set; } = string.Empty;

        [JsonPropertyName("approved_ip")]
        [JsonProperty("approved_ip")]
        public string? ApprovedIP { get; set; }

        [JsonPropertyName("remarks")]
        [JsonProperty("remarks")]
        public string? Remarks { get; set; }

        [JsonPropertyName("approval_remarks")]
        [JsonProperty("approval_remarks")]
        public string? ApprovalRemarks
        {
            get => Remarks;
            set => Remarks = value;
        }

        [JsonPropertyName("approved_remarks")]
        [JsonProperty("approved_remarks")]
        public string? ApprovedRemarks
        {
            get => Remarks;
            set => Remarks = value;
        }

        [JsonPropertyName("rejection_reason")]
        [JsonProperty("rejection_reason")]
        public string? RejectionReason { get; set; }
    }

    public class CancelCampaignRequest
    {
        [JsonPropertyName("campaign_id")]
        [JsonProperty("campaign_id")]
        public long CampaignId { get; set; }

        [JsonPropertyName("cancellation_reason")]
        [JsonProperty("cancellation_reason")]
        public string CancellationReason { get; set; } = string.Empty;

        [JsonPropertyName("reason")]
        [JsonProperty("reason")]
        public string Reason
        {
            get => CancellationReason;
            set => CancellationReason = value;
        }

        [JsonPropertyName("cancelled_by")]
        [JsonProperty("cancelled_by")]
        public string CancelledBy { get; set; } = string.Empty;
    }

    public class CampaignDto
    {
        [JsonPropertyName("campaign_id")]
        [JsonProperty("campaign_id")]
        public long CampaignId { get; set; }

        [JsonPropertyName("campaign_name")]
        [JsonProperty("campaign_name")]
        public string CampaignName { get; set; } = string.Empty;

        [JsonPropertyName("campaign_description")]
        [JsonProperty("campaign_description")]
        public string? CampaignDescription { get; set; }

        [JsonPropertyName("bmsg_id")]
        [JsonProperty("bmsg_id")]
        public long BmsgId { get; set; }

        [JsonPropertyName("campaign_status")]
        [JsonProperty("campaign_status")]
        public string CampaignStatus { get; set; } = CampaignStatusConsts.Draft;

        [JsonPropertyName("start_date")]
        [JsonProperty("start_date")]
        public DateTime? StartDate { get; set; }

        [JsonPropertyName("end_date")]
        [JsonProperty("end_date")]
        public DateTime? EndDate { get; set; }

        [JsonPropertyName("scheduled_send_time")]
        [JsonProperty("scheduled_send_time")]
        public DateTime? ScheduledSendTime { get; set; }

        [JsonPropertyName("actual_send_time")]
        [JsonProperty("actual_send_time")]
        public DateTime? ActualSendTime { get; set; }

        [JsonPropertyName("completed_time")]
        [JsonProperty("completed_time")]
        public DateTime? CompletedTime { get; set; }

        [JsonPropertyName("target_scope")]
        [JsonProperty("target_scope")]
        public string TargetScope { get; set; } = "ALL";

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

        [JsonPropertyName("is_all_district")]
        [JsonProperty("is_all_district")]
        public string IsAllDistrict { get; set; } = "Y";

        [JsonPropertyName("is_all_block")]
        [JsonProperty("is_all_block")]
        public string IsAllBlock { get; set; } = "Y";

        [JsonPropertyName("is_all_panchayat")]
        [JsonProperty("is_all_panchayat")]
        public string IsAllPanchayat { get; set; } = "Y";

        [JsonPropertyName("total_recipients_targeted")]
        [JsonProperty("total_recipients_targeted")]
        public int TotalRecipientsTargeted { get; set; }

        [JsonPropertyName("total_sent_success")]
        [JsonProperty("total_sent_success")]
        public int TotalSentSuccess { get; set; }

        [JsonPropertyName("total_sent_failed")]
        [JsonProperty("total_sent_failed")]
        public int TotalSentFailed { get; set; }

        [JsonPropertyName("total_skipped")]
        [JsonProperty("total_skipped")]
        public int TotalSkipped { get; set; }

        [JsonPropertyName("is_verified")]
        [JsonProperty("is_verified")]
        public string IsVerified { get; set; } = "N";

        [JsonPropertyName("verified_by")]
        [JsonProperty("verified_by")]
        public string? VerifiedBy { get; set; }

        [JsonPropertyName("verified_date")]
        [JsonProperty("verified_date")]
        public DateTime? VerifiedDate { get; set; }

        [JsonPropertyName("is_approved")]
        [JsonProperty("is_approved")]
        public string IsApproved { get; set; } = "N";

        [JsonPropertyName("approved_by")]
        [JsonProperty("approved_by")]
        public string? ApprovedBy { get; set; }

        [JsonPropertyName("approved_date")]
        [JsonProperty("approved_date")]
        public DateTime? ApprovedDate { get; set; }

        [JsonPropertyName("cancellation_reason")]
        [JsonProperty("cancellation_reason")]
        public string? CancellationReason { get; set; }

        [JsonPropertyName("cancelled_by")]
        [JsonProperty("cancelled_by")]
        public string? CancelledBy { get; set; }

        [JsonPropertyName("cancelled_date")]
        [JsonProperty("cancelled_date")]
        public DateTime? CancelledDate { get; set; }

        [JsonPropertyName("created_by")]
        [JsonProperty("created_by")]
        public string CreatedBy { get; set; } = string.Empty;

        [JsonPropertyName("created_date")]
        [JsonProperty("created_date")]
        public DateTime CreatedDate { get; set; }

        [JsonPropertyName("updated_by")]
        [JsonProperty("updated_by")]
        public string? UpdatedBy { get; set; }

        [JsonPropertyName("updated_date")]
        [JsonProperty("updated_date")]
        public DateTime? UpdatedDate { get; set; }

        [JsonPropertyName("rejection_reason")]
        [JsonProperty("rejection_reason")]
        public string? RejectionReason { get; set; }

        [JsonPropertyName("verified_remarks")]
        [JsonProperty("verified_remarks")]
        public string? VerifiedRemarks { get; set; }

        [JsonPropertyName("approved_remarks")]
        [JsonProperty("approved_remarks")]
        public string? ApprovedRemarks { get; set; }

        [JsonPropertyName("last_action_role")]
        [JsonProperty("last_action_role")]
        public string? LastActionRole { get; set; }

        [JsonPropertyName("audit_history")]
        [JsonProperty("audit_history")]
        public List<CampaignAuditLogDto> AuditHistory { get; set; } = new List<CampaignAuditLogDto>();

        // Associated Broadcast Message Details
        [JsonPropertyName("broadcast_message")]
        [JsonProperty("broadcast_message")]
        public BroadcastMessageDto? BroadcastMessage { get; set; }

        [JsonPropertyName("message_head")]
        [JsonProperty("message_head")]
        public string? MessageHead => BroadcastMessage?.MessageHead;

        [JsonPropertyName("message_body")]
        [JsonProperty("message_body")]
        public string? MessageBody => BroadcastMessage?.MessageBody;

        [JsonPropertyName("photo1path")]
        [JsonProperty("photo1path")]
        public string? Photo1Path => BroadcastMessage?.photo1path;

        [JsonPropertyName("photo2path")]
        [JsonProperty("photo2path")]
        public string? Photo2Path => BroadcastMessage?.photo2path;

        [JsonPropertyName("videopath")]
        [JsonProperty("videopath")]
        public string? VideoPath => BroadcastMessage?.videopath;

        [JsonPropertyName("documentpath")]
        [JsonProperty("documentpath")]
        public string? DocumentPath => BroadcastMessage?.documentpath;

        // Associated Target Locations
        [JsonPropertyName("target_locations")]
        [JsonProperty("target_locations")]
        public List<CampaignLocationDto> TargetLocations { get; set; } = new List<CampaignLocationDto>();
    }

    public class CampaignListFilter
    {
        [JsonPropertyName("status")]
        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonPropertyName("portal_id")]
        [JsonProperty("portal_id")]
        public long? PortalId { get; set; }

        [JsonPropertyName("user_type_id")]
        [JsonProperty("user_type_id")]
        public long? UserTypeId { get; set; }

        [JsonPropertyName("search_term")]
        [JsonProperty("search_term")]
        public string? SearchTerm { get; set; }

        [JsonPropertyName("from_date")]
        [JsonProperty("from_date")]
        public DateTime? FromDate { get; set; }

        [JsonPropertyName("to_date")]
        [JsonProperty("to_date")]
        public DateTime? ToDate { get; set; }

        [JsonPropertyName("created_by")]
        [JsonProperty("created_by")]
        public string? CreatedBy { get; set; }

        [JsonPropertyName("current_user_id")]
        [JsonProperty("current_user_id")]
        public string? CurrentUserId { get; set; }

        [JsonPropertyName("current_user_role")]
        [JsonProperty("current_user_role")]
        public string? CurrentUserRole { get; set; }

        [JsonPropertyName("page_number")]
        [JsonProperty("page_number")]
        public int PageNumber { get; set; } = 1;

        [JsonPropertyName("page")]
        [JsonProperty("page")]
        public int Page
        {
            get => PageNumber;
            set => PageNumber = value;
        }

        [JsonPropertyName("page_size")]
        [JsonProperty("page_size")]
        public int PageSize { get; set; } = 10;
    }

    public class CampaignAuditLogDto
    {
        [JsonPropertyName("audit_id")]
        [JsonProperty("audit_id")]
        public long AuditId { get; set; }

        [JsonPropertyName("campaign_id")]
        [JsonProperty("campaign_id")]
        public long CampaignId { get; set; }

        [JsonPropertyName("action")]
        [JsonProperty("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("from_status")]
        [JsonProperty("from_status")]
        public string? FromStatus { get; set; }

        [JsonPropertyName("to_status")]
        [JsonProperty("to_status")]
        public string ToStatus { get; set; } = string.Empty;

        [JsonPropertyName("action_by")]
        [JsonProperty("action_by")]
        public string ActionBy { get; set; } = string.Empty;

        [JsonPropertyName("action_role")]
        [JsonProperty("action_role")]
        public string? ActionRole { get; set; }

        [JsonPropertyName("action_date")]
        [JsonProperty("action_date")]
        public DateTime ActionDate { get; set; }

        [JsonPropertyName("remarks")]
        [JsonProperty("remarks")]
        public string? Remarks { get; set; }

        [JsonPropertyName("action_ip")]
        [JsonProperty("action_ip")]
        public string? ActionIP { get; set; }

        public string? ActionIp
        {
            get => ActionIP;
            set => ActionIP = value;
        }
    }

    public class CampaignDeliveryReportDto
    {
        [JsonPropertyName("campaign_id")]
        [JsonProperty("campaign_id")]
        public long CampaignId { get; set; }

        [JsonPropertyName("campaign_name")]
        [JsonProperty("campaign_name")]
        public string CampaignName { get; set; } = string.Empty;

        [JsonPropertyName("bmsg_id")]
        [JsonProperty("bmsg_id")]
        public long BmsgId { get; set; }

        [JsonPropertyName("message_head")]
        [JsonProperty("message_head")]
        public string? MessageHead { get; set; }

        [JsonPropertyName("message_body")]
        [JsonProperty("message_body")]
        public string? MessageBody { get; set; }

        [JsonPropertyName("campaign_status")]
        [JsonProperty("campaign_status")]
        public string CampaignStatus { get; set; } = string.Empty;

        [JsonPropertyName("scheduled_send_time")]
        [JsonProperty("scheduled_send_time")]
        public DateTime? ScheduledSendTime { get; set; }

        [JsonPropertyName("actual_send_time")]
        [JsonProperty("actual_send_time")]
        public DateTime? ActualSendTime { get; set; }

        [JsonPropertyName("completed_time")]
        [JsonProperty("completed_time")]
        public DateTime? CompletedTime { get; set; }

        [JsonPropertyName("total_recipients_targeted")]
        [JsonProperty("total_recipients_targeted")]
        public int TotalRecipientsTargeted { get; set; }

        [JsonPropertyName("total_sent_success")]
        [JsonProperty("total_sent_success")]
        public int TotalSentSuccess { get; set; }

        [JsonPropertyName("total_sent_failed")]
        [JsonProperty("total_sent_failed")]
        public int TotalSentFailed { get; set; }

        [JsonPropertyName("total_skipped")]
        [JsonProperty("total_skipped")]
        public int TotalSkipped { get; set; }

        [JsonPropertyName("delivery_logs")]
        [JsonProperty("delivery_logs")]
        public List<CampaignDeliveryLogDto> DeliveryLogs { get; set; } = new List<CampaignDeliveryLogDto>();
    }

    public class CampaignDeliveryLogDto
    {
        [JsonPropertyName("delivery_log_id")]
        [JsonProperty("delivery_log_id")]
        public long DeliveryLogId { get; set; }

        [JsonPropertyName("campaign_id")]
        [JsonProperty("campaign_id")]
        public long CampaignId { get; set; }

        [JsonPropertyName("bmsg_id")]
        [JsonProperty("bmsg_id")]
        public long BmsgId { get; set; }

        [JsonPropertyName("user_id")]
        [JsonProperty("user_id")]
        public string? UserId { get; set; }

        [JsonPropertyName("device_id")]
        [JsonProperty("device_id")]
        public string? DeviceId { get; set; }

        [JsonPropertyName("fcm_token")]
        [JsonProperty("fcm_token")]
        public string? FcmToken { get; set; }

        [JsonPropertyName("platform")]
        [JsonProperty("platform")]
        public string? Platform { get; set; }

        [JsonPropertyName("status")]
        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("delivery_status")]
        [JsonProperty("delivery_status")]
        public string DeliveryStatus
        {
            get => Status;
            set => Status = value;
        }

        [JsonPropertyName("fcm_message_id")]
        [JsonProperty("fcm_message_id")]
        public string? FcmMessageId { get; set; }

        [JsonPropertyName("message_id")]
        [JsonProperty("message_id")]
        public string? MessageId
        {
            get => FcmMessageId;
            set => FcmMessageId = value;
        }

        [JsonPropertyName("error_code")]
        [JsonProperty("error_code")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("error_message")]
        [JsonProperty("error_message")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("attempt_count")]
        [JsonProperty("attempt_count")]
        public int AttemptCount { get; set; } = 1;

        [JsonPropertyName("sent_at_utc")]
        [JsonProperty("sent_at_utc")]
        public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("dispatched_at")]
        [JsonProperty("dispatched_at")]
        public DateTime DispatchedAt
        {
            get => SentAtUtc;
            set => SentAtUtc = value;
        }
    }

    public class CampaignRecipientCandidate
    {
        public string? UserId { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string FcmToken { get; set; } = string.Empty;
        public string Platform { get; set; } = "Android";
    }

    public class EstimateRecipientsResponse
    {
        [JsonPropertyName("estimated_recipient_count")]
        [JsonProperty("estimated_recipient_count")]
        public int EstimatedRecipientCount { get; set; }

        [JsonPropertyName("estimated_devices_count")]
        [JsonProperty("estimated_devices_count")]
        public int EstimatedDevicesCount { get; set; }

        [JsonPropertyName("platform_breakdown")]
        [JsonProperty("platform_breakdown")]
        public Dictionary<string, int> PlatformBreakdown { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }
}
