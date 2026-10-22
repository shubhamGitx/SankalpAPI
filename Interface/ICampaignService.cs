using System.Collections.Generic;
using System.Threading.Tasks;
using SankalpAPI.Model;

namespace SankalpAPI.Interface
{
    public class EstimateRecipientsRequest
    {
        public string TargetScope { get; set; } = "ALL";
        public long? PortalId { get; set; }
        public long? UserTypeId { get; set; }
        public string IsAllDistrict { get; set; } = "Y";
        public string IsAllBlock { get; set; } = "Y";
        public string IsAllPanchayat { get; set; } = "Y";
        public List<CampaignLocationDto>? Locations { get; set; }
    }

    public interface ICampaignService
    {
        Task<(bool isSuccess, string message, long campaignId, CampaignDto? data)> CreateCampaignAsync(CreateCampaignRequest request);
        Task<(bool isSuccess, string message)> UpdateCampaignAsync(long campaignId, UpdateCampaignRequest request);
        Task<CampaignDto?> GetCampaignByIdAsync(long campaignId);
        Task<(List<CampaignDto> items, int totalCount)> GetAllCampaignsAsync(CampaignListFilter filter);
        Task<(bool isSuccess, string message)> VerifyCampaignAsync(VerifyCampaignRequest request, string? userRole);
        Task<(bool isSuccess, string message)> ApproveCampaignAsync(ApproveCampaignRequest request, string? userRole);
        Task<(bool isSuccess, string message)> SendCampaignAsync(long campaignId, string? sentBy);
        Task<(bool isSuccess, string message)> CancelCampaignAsync(CancelCampaignRequest request);
        Task<CampaignDeliveryReportDto?> GetDeliveryReportAsync(long campaignId);
        Task<EstimateRecipientsResponse> EstimateRecipientsAsync(EstimateRecipientsRequest request);
        Task<List<CampaignAuditLogDto>> GetAuditHistoryAsync(long campaignId);
        Task<string?> GetUserRoleAsync(string userId);
        Task ExecuteScheduledCampaignsJobAsync();
    }
}
