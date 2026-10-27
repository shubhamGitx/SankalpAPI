using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SankalpAPI.Interface;
using SankalpAPI.Model;

namespace SankalpAPI.Services
{
    public class CampaignService : ICampaignService
    {
        private readonly ICampaignRepository _campaignRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IFirebaseNotificationService _firebaseService;
        private readonly ILogger<CampaignService> _logger;

        public CampaignService(
            ICampaignRepository campaignRepository,
            IMessageRepository messageRepository,
            IFirebaseNotificationService firebaseService,
            ILogger<CampaignService> logger)
        {
            _campaignRepository = campaignRepository;
            _messageRepository = messageRepository;
            _firebaseService = firebaseService;
            _logger = logger;
        }

        public async Task<(bool isSuccess, string message, long campaignId, CampaignDto? data)> CreateCampaignAsync(CreateCampaignRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CampaignName))
            {
                return (false, "Campaign name is required.", 0, null);
            }

            // Role restriction: Only Makers (MKR) can create campaigns
            if (!string.IsNullOrWhiteSpace(request.CreatedBy))
            {
                var role = await _campaignRepository.GetUserRoleAsync(request.CreatedBy);
                if (CampaignRoles.IsChecker(role))
                {
                    return (false, "Permission Denied: Checkers (CKR) are not permitted to create campaigns. Only Makers (MKR) can create campaigns.", 0, null);
                }
                if (CampaignRoles.IsApprover(role))
                {
                    return (false, "Permission Denied: Approvers (APR) are not permitted to create campaigns. Only Makers (MKR) can create campaigns.", 0, null);
                }
            }

            if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate.Value < request.StartDate.Value)
            {
                return (false, "Campaign End Date must be greater than or equal to Start Date.", 0, null);
            }

            long resolvedBmsgId = 0;

            // Determine if linking to existing broadcast message or creating new one
            if (request.BmsgId.HasValue && request.BmsgId.Value > 0)
            {
                var existingMsg = await _messageRepository.GetBroadcastMessageByIdAsync(request.BmsgId.Value);
                if (existingMsg == null || string.Equals(existingMsg.IsDeleted, "Y", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"Broadcast message with ID {request.BmsgId.Value} does not exist or has been deleted.", 0, null);
                }
                resolvedBmsgId = request.BmsgId.Value;
            }
            else if (request.MessageDetails != null)
            {
                if (string.IsNullOrWhiteSpace(request.MessageDetails.MessageHead) && string.IsNullOrWhiteSpace(request.MessageDetails.MessageBody))
                {
                    return (false, "Message header or message body must be provided when creating new broadcast content.", 0, null);
                }

                var insertMsgReq = new InsertBroadcastMessageRequest
                {
                    MessageHead = request.MessageDetails.MessageHead,
                    MessageBody = request.MessageDetails.MessageBody,
                    ValidityInMinutes = request.MessageDetails.ValidityInMinutes ?? 1440,
                    IsActive = "Y",
                    Photo1Path = request.MessageDetails.Photo1Path,
                    Photo2Path = request.MessageDetails.Photo2Path,
                    VideoPath = request.MessageDetails.VideoPath,
                    DocumentPath = request.MessageDetails.DocumentPath,
                    EntryBy = request.CreatedBy ?? "Admin"
                };

                resolvedBmsgId = await _messageRepository.InsertBroadcastMessageAsync(insertMsgReq);
                if (resolvedBmsgId <= 0)
                {
                    return (false, "Failed to create broadcast message record in database.", 0, null);
                }
            }
            else
            {
                return (false, "Either an existing BmsgId or MessageDetails must be provided to create a campaign.", 0, null);
            }

            // Scheduled send time validation
            if (request.ScheduledSendTime.HasValue && request.ScheduledSendTime.Value < DateTime.UtcNow.AddMinutes(-5))
            {
                return (false, "Scheduled sending time cannot be in the past.", 0, null);
            }

            long campaignId = await _campaignRepository.CreateCampaignAsync(request, resolvedBmsgId);
            if (campaignId <= 0)
            {
                return (false, "Failed to create campaign record in database.", 0, null);
            }

            var createdCampaign = await _campaignRepository.GetCampaignByIdAsync(campaignId);
            return (true, "Campaign created successfully.", campaignId, createdCampaign);
        }

        public async Task<(bool isSuccess, string message)> UpdateCampaignAsync(long campaignId, UpdateCampaignRequest request)
        {
            var existing = await _campaignRepository.GetCampaignByIdAsync(campaignId);
            if (existing == null)
            {
                return (false, $"Campaign with ID {campaignId} was not found.");
            }

            // Role restriction: If updatedBy is provided, check if CKR or APR
            if (!string.IsNullOrWhiteSpace(request.UpdatedBy))
            {
                var role = await _campaignRepository.GetUserRoleAsync(request.UpdatedBy);
                if (CampaignRoles.IsChecker(role) || CampaignRoles.IsApprover(role))
                {
                    return (false, "Permission Denied: Only Makers (MKR) can edit or resubmit campaigns.");
                }

                // Maker can only edit their own campaigns
                if (!string.IsNullOrWhiteSpace(existing.CreatedBy) &&
                    !string.Equals(existing.CreatedBy.Trim(), request.UpdatedBy.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "Permission Denied: You can only edit campaigns created by your user account.");
                }
            }

            string normalizedStatus = CampaignStatusConsts.Normalize(existing.CampaignStatus);
            if (!string.Equals(normalizedStatus, CampaignStatusConsts.Draft, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalizedStatus, CampaignStatusConsts.SubmittedForVerification, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalizedStatus, CampaignStatusConsts.RejectedByCkr, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalizedStatus, CampaignStatusConsts.RejectedByApr, StringComparison.OrdinalIgnoreCase))
            {
                return (false, $"Campaign cannot be edited in its current status ('{existing.CampaignStatus}'). Only '{CampaignStatusConsts.Draft}', '{CampaignStatusConsts.SubmittedForVerification}', '{CampaignStatusConsts.RejectedByCkr}', or '{CampaignStatusConsts.RejectedByApr}' campaigns can be modified.");
            }

            if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate.Value < request.StartDate.Value)
            {
                return (false, "Campaign End Date must be greater than or equal to Start Date.");
            }

            bool updated = await _campaignRepository.UpdateCampaignAsync(campaignId, request);
            return updated
                ? (true, "Campaign updated successfully.")
                : (false, "No changes were applied or update failed.");
        }

        public async Task<CampaignDto?> GetCampaignByIdAsync(long campaignId)
        {
            return await _campaignRepository.GetCampaignByIdAsync(campaignId);
        }

        public async Task<(List<CampaignDto> items, int totalCount)> GetAllCampaignsAsync(CampaignListFilter filter)
        {
            return await _campaignRepository.GetAllCampaignsAsync(filter);
        }

        public async Task<(bool isSuccess, string message)> VerifyCampaignAsync(VerifyCampaignRequest request, string? userRole)
        {
            var existing = await _campaignRepository.GetCampaignByIdAsync(request.CampaignId);
            if (existing == null)
            {
                return (false, $"Campaign with ID {request.CampaignId} was not found.");
            }

            // Resolve role from DB if not provided or to ensure authenticity
            string? verifiedBy = request.VerifiedBy;
            string? effectiveRole = userRole;
            if (!string.IsNullOrWhiteSpace(verifiedBy))
            {
                var dbRole = await _campaignRepository.GetUserRoleAsync(verifiedBy);
                if (!string.IsNullOrWhiteSpace(dbRole)) effectiveRole = dbRole;
            }

            if (CampaignRoles.IsMaker(effectiveRole))
            {
                return (false, "Permission Denied: Makers (MKR) are not permitted to verify campaigns. Verification must be performed by a Checker (CKR).");
            }
            if (CampaignRoles.IsApprover(effectiveRole))
            {
                return (false, "Permission Denied: Approvers (APR) cannot perform Checker verification. Only Checkers (CKR) can verify campaigns.");
            }

            // Dual control: Maker cannot verify own campaign
            if (!string.IsNullOrWhiteSpace(existing.CreatedBy) &&
                !string.IsNullOrWhiteSpace(verifiedBy) &&
                string.Equals(existing.CreatedBy.Trim(), verifiedBy.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Permission Denied: The campaign creator (Maker) cannot verify their own campaign. A separate Checker is required.");
            }

            string currentStatus = CampaignStatusConsts.Normalize(existing.CampaignStatus);
            if (!string.Equals(currentStatus, CampaignStatusConsts.SubmittedForVerification, StringComparison.OrdinalIgnoreCase))
            {
                return (false, $"Campaign cannot be verified in its current status ('{existing.CampaignStatus}'). Only campaigns with status '{CampaignStatusConsts.SubmittedForVerification}' can be verified by Checker.");
            }

            // Mandatory rejection reason check
            bool isVerifying = string.Equals(request.IsVerified, "Y", StringComparison.OrdinalIgnoreCase);
            if (!isVerifying)
            {
                if (string.IsNullOrWhiteSpace(request.RejectionReason) && string.IsNullOrWhiteSpace(request.VerifiedRemarks))
                {
                    return (false, "A mandatory rejection reason must be provided when rejecting a campaign.");
                }
                if (string.IsNullOrWhiteSpace(request.RejectionReason))
                {
                    request.RejectionReason = request.VerifiedRemarks;
                }
            }

            bool success = await _campaignRepository.VerifyCampaignAsync(request);
            if (!success)
            {
                return (false, "Failed to record campaign verification status.");
            }

            return isVerifying
                ? (true, $"Campaign verified successfully and forwarded to Approver queue ({CampaignStatusConsts.PendingApproval}).")
                : (true, $"Campaign verification rejected and transitioned to {CampaignStatusConsts.RejectedByCkr}.");
        }

        public async Task<(bool isSuccess, string message)> ApproveCampaignAsync(ApproveCampaignRequest request, string? userRole)
        {
            var existing = await _campaignRepository.GetCampaignByIdAsync(request.CampaignId);
            if (existing == null)
            {
                return (false, $"Campaign with ID {request.CampaignId} was not found.");
            }

            // Resolve role from DB if not provided or to ensure authenticity
            string? approvedBy = request.ApprovedBy;
            string? effectiveRole = userRole;
            if (!string.IsNullOrWhiteSpace(approvedBy))
            {
                var dbRole = await _campaignRepository.GetUserRoleAsync(approvedBy);
                if (!string.IsNullOrWhiteSpace(dbRole)) effectiveRole = dbRole;
            }

            if (CampaignRoles.IsMaker(effectiveRole))
            {
                return (false, "Permission Denied: Makers (MKR) are not permitted to approve campaigns. Only Approvers (APR) can approve campaigns.");
            }
            if (CampaignRoles.IsChecker(effectiveRole))
            {
                return (false, "Permission Denied: Checkers (CKR) are not permitted to give final approval. Only Approvers (APR) can approve campaigns.");
            }

            // Dual control rule: Creator cannot approve their own campaign
            if (!string.IsNullOrWhiteSpace(existing.CreatedBy) &&
                !string.IsNullOrWhiteSpace(approvedBy) &&
                string.Equals(existing.CreatedBy.Trim(), approvedBy.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Permission Denied: The campaign creator (Maker) is not permitted to approve their own campaign. Dual control requires an independent Approver.");
            }

            // Sequential workflow: Must not bypass CKR verification!
            string currentStatus = CampaignStatusConsts.Normalize(existing.CampaignStatus);
            if (!string.Equals(currentStatus, CampaignStatusConsts.PendingApproval, StringComparison.OrdinalIgnoreCase))
            {
                return (false, $"Campaign cannot be approved in its current status ('{existing.CampaignStatus}'). It must be verified by Checker and have status '{CampaignStatusConsts.PendingApproval}' before Approver review.");
            }

            // Mandatory rejection reason check
            bool isApproving = string.Equals(request.IsApproved, "Y", StringComparison.OrdinalIgnoreCase);
            if (!isApproving)
            {
                if (string.IsNullOrWhiteSpace(request.RejectionReason) && string.IsNullOrWhiteSpace(request.ApprovedRemarks))
                {
                    return (false, "A mandatory rejection reason must be provided when rejecting a campaign.");
                }
                if (string.IsNullOrWhiteSpace(request.RejectionReason))
                {
                    request.RejectionReason = request.ApprovedRemarks;
                }
            }

            bool success = await _campaignRepository.ApproveCampaignAsync(request);
            if (!success)
            {
                return (false, "Failed to record campaign approval status.");
            }

            return isApproving
                ? (true, $"Campaign approved successfully ({CampaignStatusConsts.Approved}). Maker may now send the campaign message.")
                : (true, $"Campaign approval rejected and transitioned to {CampaignStatusConsts.RejectedByApr}.");
        }

        public async Task<(bool isSuccess, string message)> SendCampaignAsync(long campaignId, string? sentBy)
        {
            var campaign = await _campaignRepository.GetCampaignByIdAsync(campaignId);
            if (campaign == null)
            {
                return (false, $"Campaign with ID {campaignId} was not found.");
            }

            // Check role permissions: CKR and APR must NOT send campaign messages
            if (!string.IsNullOrWhiteSpace(sentBy) && !string.Equals(sentBy, "ScheduledWorker", StringComparison.OrdinalIgnoreCase))
            {
                var role = await _campaignRepository.GetUserRoleAsync(sentBy);
                if (CampaignRoles.IsChecker(role))
                {
                    return (false, "Permission Denied: Checkers (CKR) are not permitted to send campaign messages.");
                }
                if (CampaignRoles.IsApprover(role))
                {
                    return (false, "Permission Denied: Approvers (APR) are not permitted to send campaign messages.");
                }
            }

            // Sequential workflow enforcement:
            // "Only after APR approves the campaign can MKR send the campaign message. The system must prevent sending messages before final approval."
            string normalizedStatus = CampaignStatusConsts.Normalize(campaign.CampaignStatus);
            if (!string.Equals(normalizedStatus, CampaignStatusConsts.Approved, StringComparison.OrdinalIgnoreCase))
            {
                return (false, $"Cannot send campaign. Only campaigns with final status '{CampaignStatusConsts.Approved}' can be sent. Current status: '{campaign.CampaignStatus}'.");
            }

            // Verify approval flag
            if (!string.Equals(campaign.IsApproved, "Y", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Cannot send an unapproved campaign. The campaign must receive final approval before dispatch.");
            }

            // Verify expiration
            if (campaign.EndDate.HasValue && DateTime.UtcNow > campaign.EndDate.Value)
            {
                return (false, $"Campaign expired on {campaign.EndDate.Value:yyyy-MM-dd HH:mm} UTC and cannot be sent.");
            }

            // Check associated broadcast message status
            var broadcastMsg = await _messageRepository.GetBroadcastMessageByIdAsync(campaign.BmsgId);
            if (broadcastMsg == null || string.Equals(broadcastMsg.IsDeleted, "Y", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Associated broadcast message has been deleted or cannot be found.");
            }

            if (!string.Equals(broadcastMsg.IsActive, "Y", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Associated broadcast message is inactive.");
            }

            // Atomically switch campaign status to 'SENDING'
            bool stateUpdated = await _campaignRepository.SetCampaignSendingStateAsync(campaignId);
            if (!stateUpdated)
            {
                return (false, "Unable to transition campaign to SENDING state. It may be currently in progress by another worker.");
            }

            _logger.LogInformation("Resolving recipients for Campaign {CampaignId} ({CampaignName})...", campaignId, campaign.CampaignName);

            // Deduplicated eligible recipients
            var recipients = await _campaignRepository.ResolveEligibleRecipientsAsync(campaignId);

            if (recipients == null || recipients.Count == 0)
            {
                _logger.LogWarning("No eligible recipients with active FCM tokens found for Campaign {CampaignId}.", campaignId);
                await _campaignRepository.FinalizeCampaignExecutionAsync(campaignId, CampaignStatusConsts.Sent, 0, 0, 0);
                return (true, "No eligible registered recipients found matching target criteria. Campaign marked as SENT with 0 dispatches.");
            }

            // Prepare notification payload
            string notificationTitle = !string.IsNullOrWhiteSpace(campaign.MessageHead)
                ? campaign.MessageHead
                : campaign.CampaignName;

            string notificationBody = !string.IsNullOrWhiteSpace(campaign.MessageBody)
                ? campaign.MessageBody
                : (campaign.CampaignDescription ?? "New notification from Sankalp Setu");

            var dataPayload = new Dictionary<string, string>
            {
                ["campaignId"] = campaign.CampaignId.ToString(),
                ["campaignName"] = campaign.CampaignName,
                ["bmsgId"] = campaign.BmsgId.ToString(),
                ["portalId"] = campaign.PortalId?.ToString() ?? "",
                ["portalName"] = campaign.PortalName ?? "Sankalp",
                ["portal_name"] = campaign.PortalName ?? "Sankalp",
                ["photo1"] = campaign.Photo1Path ?? "",
                ["photo2"] = campaign.Photo2Path ?? "",
                ["video"] = campaign.VideoPath ?? "",
                ["document"] = campaign.DocumentPath ?? ""
            };

            // Multicast batch dispatch via Firebase service
            var deliveryLogs = await _firebaseService.SendCampaignMulticastAsync(
                campaign.CampaignId,
                campaign.BmsgId,
                notificationTitle,
                notificationBody,
                dataPayload,
                recipients);

            // Persist delivery logs with idempotency safeguards
            await _campaignRepository.RecordDeliveryLogsAsync(campaign.CampaignId, campaign.BmsgId, deliveryLogs);

            // Compute counters
            int successCount = deliveryLogs.Count(l => string.Equals(l.DeliveryStatus, "AcceptedByFcm", StringComparison.OrdinalIgnoreCase));
            int failedCount = deliveryLogs.Count(l => string.Equals(l.DeliveryStatus, "Failed", StringComparison.OrdinalIgnoreCase));
            int skippedCount = deliveryLogs.Count(l => string.Equals(l.DeliveryStatus, "Skipped", StringComparison.OrdinalIgnoreCase));

            string finalStatus = CampaignStatusConsts.Sent;
            if (successCount == 0 && failedCount > 0)
            {
                finalStatus = CampaignStatusConsts.SendFailed;
            }

            await _campaignRepository.FinalizeCampaignExecutionAsync(
                campaignId,
                finalStatus,
                successCount,
                failedCount,
                skippedCount);

            _logger.LogInformation("Campaign {CampaignId} dispatch finalized with status {Status}: {Success} succeeded, {Failed} failed, {Skipped} skipped.",
                campaignId, finalStatus, successCount, failedCount, skippedCount);

            return (true, $"Campaign sending finished. Result: {finalStatus} ({successCount} accepted by FCM, {failedCount} failed, {skippedCount} skipped out of {deliveryLogs.Count} total).");
        }

        public async Task<(bool isSuccess, string message)> CancelCampaignAsync(CancelCampaignRequest request)
        {
            var existing = await _campaignRepository.GetCampaignByIdAsync(request.CampaignId);
            if (existing == null)
            {
                return (false, $"Campaign with ID {request.CampaignId} was not found.");
            }

            string normalizedStatus = CampaignStatusConsts.Normalize(existing.CampaignStatus);
            if (string.Equals(normalizedStatus, CampaignStatusConsts.Sent, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedStatus, CampaignStatusConsts.Sending, StringComparison.OrdinalIgnoreCase))
            {
                return (false, $"Cannot cancel a campaign with status '{existing.CampaignStatus}'.");
            }

            bool cancelled = await _campaignRepository.CancelCampaignAsync(request);
            return cancelled
                ? (true, "Campaign has been cancelled.")
                : (false, "Failed to cancel campaign.");
        }

        public async Task<CampaignDeliveryReportDto?> GetDeliveryReportAsync(long campaignId)
        {
            return await _campaignRepository.GetDeliveryReportAsync(campaignId);
        }

        public async Task<EstimateRecipientsResponse> EstimateRecipientsAsync(EstimateRecipientsRequest request)
        {
            return await _campaignRepository.EstimateRecipientsAsync(
                request.TargetScope,
                request.PortalId,
                request.UserTypeId,
                request.IsAllDistrict,
                request.IsAllBlock,
                request.IsAllPanchayat,
                request.Locations);
        }

        public Task<List<CampaignAuditLogDto>> GetAuditHistoryAsync(long campaignId)
        {
            return _campaignRepository.GetAuditHistoryAsync(campaignId);
        }

        public Task<string?> GetUserRoleAsync(string userId)
        {
            return _campaignRepository.GetUserRoleAsync(userId);
        }

        public async Task ExecuteScheduledCampaignsJobAsync()
        {
            var dueList = await _campaignRepository.GetDueScheduledCampaignsAsync();
            if (dueList == null || dueList.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Found {Count} scheduled campaign(s) ready for automatic dispatch.", dueList.Count);

            foreach (var campaign in dueList)
            {
                try
                {
                    _logger.LogInformation("Executing scheduled dispatch for Campaign {CampaignId} ('{CampaignName}')...",
                        campaign.CampaignId, campaign.CampaignName);

                    var result = await SendCampaignAsync(campaign.CampaignId, "ScheduledWorker");
                    _logger.LogInformation("Scheduled dispatch outcome for Campaign {CampaignId}: {Success} - {Message}",
                        campaign.CampaignId, result.isSuccess, result.message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing scheduled campaign {CampaignId}", campaign.CampaignId);
                }
            }
        }
    }
}
