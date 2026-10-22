using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SankalpAPI.Model;

namespace SankalpAPI.Interface
{
    public interface ICampaignRepository
    {
        Task<long> CreateCampaignAsync(CreateCampaignRequest request, long bmsgId);
        Task<bool> UpdateCampaignAsync(long campaignId, UpdateCampaignRequest request);
        Task<CampaignDto?> GetCampaignByIdAsync(long campaignId);
        Task<(List<CampaignDto> items, int totalCount)> GetAllCampaignsAsync(CampaignListFilter filter);
        Task<bool> VerifyCampaignAsync(VerifyCampaignRequest request);
        Task<bool> ApproveCampaignAsync(ApproveCampaignRequest request);
        Task<bool> CancelCampaignAsync(CancelCampaignRequest request);
        Task<bool> SetCampaignSendingStateAsync(long campaignId);
        Task<bool> FinalizeCampaignExecutionAsync(long campaignId, string status, int successCount, int failedCount, int skippedCount);
        Task<List<CampaignRecipientCandidate>> ResolveEligibleRecipientsAsync(long campaignId);
        Task<int> RecordDeliveryLogsAsync(long campaignId, long bmsgId, List<CampaignDeliveryLogDto> logs);
        Task<CampaignDeliveryReportDto?> GetDeliveryReportAsync(long campaignId);
        Task<List<CampaignDto>> GetDueScheduledCampaignsAsync();
        Task<EstimateRecipientsResponse> EstimateRecipientsAsync(string targetScope, long? portalId, long? userTypeId, string isAllDistrict, string isAllBlock, string isAllPanchayat, List<CampaignLocationDto>? locations);
        Task<bool> DeactivateInvalidTokenAsync(string fcmToken);
        Task<bool> RecordAuditLogAsync(long campaignId, string action, string? fromStatus, string toStatus, string actionBy, string? actionRole, string? remarks, string? actionIp = null);
        Task<List<CampaignAuditLogDto>> GetAuditHistoryAsync(long campaignId);
        Task<string?> GetUserRoleAsync(string userId);
    }
}
