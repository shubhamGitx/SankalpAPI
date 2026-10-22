using System.Collections.Generic;
using System.Threading.Tasks;
using SankalpAPI.Model;

namespace SankalpAPI.Interface
{
    public interface IFirebaseNotificationService
    {
        Task<List<CampaignDeliveryLogDto>> SendCampaignMulticastAsync(
            long campaignId,
            long bmsgId,
            string title,
            string body,
            Dictionary<string, string>? dataPayload,
            List<CampaignRecipientCandidate> recipients);
    }
}
