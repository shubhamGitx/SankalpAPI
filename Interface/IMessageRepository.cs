using System.Collections.Generic;
using System.Threading.Tasks;
using SankalpAPI.Model;

namespace SankalpAPI.Interface
{
    public interface IMessageRepository
    {
        Task<List<PersonalizedMessageDto>> GetUnsyncedMessagesAndMarkSyncedAsync(string DeviceID, long? portalId = null, long? msg_id = null);
        Task<long> CreateNotificationAsync(string DeviceID, string? msg_id);
        Task<int> MarkMessagesAsSyncedAsync(List<long> notificationIds, string? deviceId = null);
        Task<List<PersonalizedMessageDto>> GetMessageHistoryAsync(string DeviceID, List<string>? interestIds = null, long? portalId = null);
        Task<(bool hasExistingLog, int unsyncedCount)> CheckBroadcastSyncStatusAsync(string deviceId, List<string>? interestIds = null, long? portalId = null);
        Task<PersonalizedMessageDto?> GetMessageByIdAsync(long msgId);
        Task<List<BroadcastMessageDto>> GetBroadcastMessagesAsync(string deviceId);
        Task<List<MessageTypeDto>> GetMessageTypesAsync();
        Task<long> InsertBroadcastMessageAsync(InsertBroadcastMessageRequest request);
        Task<BroadcastMessageDto?> GetBroadcastMessageByIdAsync(long bmsgId);
        Task<long> InsertInterestWiseMessageAsync(InsertInterestWiseMessageRequest request);
        Task<List<InterestWiseMessageDto>> GetInterestWiseMessagesAsync(long? interestId = null);
        Task<bool> BroadcastMessageExistsAsync(long bmsgId);
        Task<int> UpdateBroadcastVerifiedStatusAsync(long bmsgId, bool isVerified);
        Task<int> UpdateBroadcastApprovedStatusAsync(long bmsgId, bool isApproved);
    }
}
