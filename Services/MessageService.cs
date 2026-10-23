using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SankalpAPI.Helper;
using SankalpAPI.Interface;
using SankalpAPI.Model;
using SankalpAPI.Security;

namespace SankalpAPI.Services
{
    public class MessageService
    {
        private readonly IMessageRepository _messageRepository;
        private readonly IPortalRepository _portalRepository;
        private readonly ILogger<MessageService> _logger;
        private readonly ExceptionLogging _exceptionLogging;
        private readonly BroadcastAttachmentStorageService _attachmentStorage;

        public MessageService(
            IMessageRepository messageRepository,
            IPortalRepository portalRepository,
            ILogger<MessageService> logger,
            ExceptionLogging exceptionLogging,
            BroadcastAttachmentStorageService attachmentStorage)
        {
            _messageRepository = messageRepository;
            _portalRepository = portalRepository;
            _logger = logger;
            _exceptionLogging = exceptionLogging;
            _attachmentStorage = attachmentStorage;
        }

        public async Task<GetUnsyncedMessagesResponse> GetUnsyncedMessagesAsync(GetUnsyncedMessagesRequest request)
        {
            if (request == null)
            {
                return new GetUnsyncedMessagesResponse
                {
                    Status = false,
                    Message = "Invalid request."
                };
            }

            string DeviceID = DecryptIfNeeded(request.DeviceID, request.ClientKey);
            return await GetUnsyncedMessagesAsync(DeviceID, request.PortalId);
        }

        public async Task<GetUnsyncedMessagesResponse> GetUnsyncedMessagesAsync(string DeviceID, long? portalId = null, long? msg_id = null)
        {
            if (string.IsNullOrWhiteSpace(DeviceID))
            {
                return new GetUnsyncedMessagesResponse
                {
                    Status = false,
                    Message = "Device ID is required."
                };
            }

            try
            {
                var messages = await _messageRepository.GetUnsyncedMessagesAndMarkSyncedAsync(DeviceID.Trim(), portalId, msg_id);

                return new GetUnsyncedMessagesResponse
                {
                    Status = true,
                    Message = messages.Count > 0 ? $"Successfully fetched {messages.Count} unsynced message(s) and marked as synced." : "No unsynced messages found.",
                    Count = messages.Count,
                    Data = messages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching unsynced messages for device {DeviceID}", DeviceID);
                _exceptionLogging.SendErrorToText(ex);

                return new GetUnsyncedMessagesResponse
                {
                    Status = false,
                    Message = "Unable to fetch messages."
                };
            }
        }

        public async Task<object> CreateNotificationAsync(CreateNotificationRequest request)
        {
            if (request == null)
            {
                return new { status = false, message = "Invalid request." };
            }

            if (string.IsNullOrWhiteSpace(request.DeviceID))
            {
                return new { status = false, message = "Device ID is required." };
            }

            if (string.IsNullOrWhiteSpace(request.msg_id))
            {
                return new { status = false, message = "Message ID is required." };
            }

            try
            {
                string entryBy = !string.IsNullOrWhiteSpace(request.EntryBy) ? request.EntryBy : "System";

                long notificationId = await _messageRepository.CreateNotificationAsync(request.DeviceID.Trim(), request.msg_id);

                if (notificationId > 0)
                {
                    string? portalName = null;
                    if (long.TryParse(request.msg_id, out long parsedMsgId))
                    {
                        var msg = await _messageRepository.GetMessageByIdAsync(parsedMsgId);
                        portalName = msg?.PortalName;
                    }

                    return new
                    {
                        status = true,
                        message = "Notification created successfully (unsynced).",
                        notificationId = notificationId,
                        msg_log_id = notificationId,
                        portalName = portalName
                    };
                }

                return new { status = false, message = "Failed to create notification." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification for DeviceID {DeviceID}", request.DeviceID);
                _exceptionLogging.SendErrorToText(ex);

                return new { status = false, message = "An error occurred while creating notification." };
            }
        }

        public async Task<object> MarkMessagesAsSyncedAsync(MarkSyncedRequest request)
        {
            if (request == null || request.NotificationIds == null || request.NotificationIds.Count == 0)
            {
                return new { status = false, message = "Notification IDs are required." };
            }

            try
            {
                string? deviceId = null;
                if (!string.IsNullOrWhiteSpace(request.DeviceID))
                {
                    deviceId = DecryptIfNeeded(request.DeviceID, request.ClientKey);
                }

                int count = await _messageRepository.MarkMessagesAsSyncedAsync(request.NotificationIds, deviceId);
                return new
                {
                    status = true,
                    message = $"{count} message(s) marked as synced successfully.",
                    affectedCount = count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as synced");
                _exceptionLogging.SendErrorToText(ex);

                return new { status = false, message = "An error occurred while marking messages as synced." };
            }
        }

        public async Task<object> GetMessageHistoryAsync(GetMessageHistoryRequest request)
        {
            if (request == null)
            {
                return new { status = false, message = "Invalid request." };
            }

            string DeviceID = DecryptIfNeeded(request.DeviceID, request.ClientKey);
            var interestIds = request.GetInterestIdList();
            return await GetMessageHistoryAsync(DeviceID, interestIds);
        }

        public async Task<object> GetMessageHistoryAsync(string DeviceID, List<string>? interestIds = null, long? portalId = null)
        {
            if (string.IsNullOrWhiteSpace(DeviceID))
            {
                return new { status = false, message = "Device ID is required." };
            }

            try
            {
                string cleanDeviceId = DeviceID.Trim();

                // Check if data already exists in tbl_Message_log for this device
                var (hasExistingLog, unsyncedCount) = await _messageRepository.CheckBroadcastSyncStatusAsync(cleanDeviceId, interestIds, portalId);

                if (hasExistingLog && unsyncedCount == 0)
                {
                    return new
                    {
                        status = true,
                        message = "Already Synced"
                    };
                }

                if (!hasExistingLog && unsyncedCount == 0)
                {
                    return new
                    {
                        status = true,
                        message = "No broadcast messages found.",
                        count = 0,
                        data = new List<PersonalizedMessageDto>()
                    };
                }

                var list = await _messageRepository.GetMessageHistoryAsync(cleanDeviceId, interestIds, portalId);
                return new
                {
                    status = true,
                    message = "Message history retrieved successfully.",
                    count = list.Count,
                    data = list
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message history for device {DeviceID}", DeviceID);
                _exceptionLogging.SendErrorToText(ex);

                return new { status = false, message = "Unable to retrieve message history." };
            }
        }

        public async Task<object> GetBroadcastMessagesAsync(GetBroadCastMessageRequest request)
        {
            try
            {
                var list = await _messageRepository.GetBroadcastMessagesAsync(request.DeviceID);

                if (list == null || list.Count == 0)
                {
                    return new
                    {
                        status = true,
                        message = "No broadcast messages found.",
                        count = 0,
                        data = new List<BroadcastMessageDto>()
                    };
                }

                return new
                {
                    status = true,
                    message = "Broadcast messages retrieved successfully.",
                    count = list.Count,
                    data = list
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting broadcast messages");
                _exceptionLogging.SendErrorToText(ex);

                return new { status = false, message = "Unable to retrieve broadcast messages." };
            }
        }

        public async Task<object> GetMessageTypesAsync()
        {
            try
            {
                var messageTypes = await _messageRepository.GetMessageTypesAsync();
                return new
                {
                    status = true,
                    message = "Message types retrieved successfully.",
                    data = messageTypes
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message types");
                _exceptionLogging.SendErrorToText(ex);
                return new { status = false, message = "Unable to retrieve message types.", data = Array.Empty<MessageTypeDto>() };
            }
        }

        public async Task<InsertBroadcastMessageResponse> InsertBroadcastMessageAsync(InsertBroadcastMessageRequest request)
        {
            if (request == null)
            {
                return new InsertBroadcastMessageResponse
                {
                    Status = false,
                    Message = "Invalid request payload."
                };
            }

            // Decrypt fields if client key is provided
            string messageHead = DecryptIfNeeded(request.MessageHead ?? string.Empty, request.ClientKey);
            string messageBody = DecryptIfNeeded(request.MessageBody ?? string.Empty, request.ClientKey);

            messageHead = messageHead.Trim();
            messageBody = messageBody.Trim();

            if (messageHead.Length < 3)
            {
                return new InsertBroadcastMessageResponse
                {
                    Status = false,
                    Message = "Notice title must contain at least 3 characters."
                };
            }

            if (messageBody.Length < 10)
            {
                return new InsertBroadcastMessageResponse
                {
                    Status = false,
                    Message = "Message body must contain at least 10 characters."
                };
            }

            request.MessageHead = messageHead;
            request.MessageBody = messageBody;

            if (request.IsAllDistrict)
            {
                request.DistrictCodes = null;
            }
            else
            {
                request.DistrictCodes = request.DistrictCodes?
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Select(code => code.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (request.DistrictCodes == null || request.DistrictCodes.Count == 0)
                {
                    return new InsertBroadcastMessageResponse
                    {
                        Status = false,
                        Message = "Select at least one district or choose all districts."
                    };
                }
            }

            if (!string.IsNullOrWhiteSpace(request.Photo1Path))
                request.Photo1Path = DecryptIfNeeded(request.Photo1Path, request.ClientKey);

            if (!string.IsNullOrWhiteSpace(request.Photo2Path))
                request.Photo2Path = DecryptIfNeeded(request.Photo2Path, request.ClientKey);

            if (!string.IsNullOrWhiteSpace(request.VideoPath))
                request.VideoPath = DecryptIfNeeded(request.VideoPath, request.ClientKey);

            if (!string.IsNullOrWhiteSpace(request.DocumentPath))
                request.DocumentPath = DecryptIfNeeded(request.DocumentPath, request.ClientKey);

            if (!string.IsNullOrWhiteSpace(request.AudioPath))
                request.AudioPath = DecryptIfNeeded(request.AudioPath, request.ClientKey);

            if (!string.IsNullOrWhiteSpace(request.EntryBy))
                request.EntryBy = DecryptIfNeeded(request.EntryBy, request.ClientKey);

            try
            {
                long bmsgId = await _messageRepository.InsertBroadcastMessageAsync(request);

                if (bmsgId <= 0)
                {
                    return new InsertBroadcastMessageResponse
                    {
                        Status = false,
                        Message = "Failed to insert broadcast message."
                    };
                }

                var savedMessage = await _messageRepository.GetBroadcastMessageByIdAsync(bmsgId);

                return new InsertBroadcastMessageResponse
                {
                    Status = true,
                    Message = "Broadcast message inserted successfully.",
                    Bmsg_id = bmsgId,
                    Data = savedMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting broadcast message into tbl_BroadCastMessage");
                _exceptionLogging.SendErrorToText(ex);

                return new InsertBroadcastMessageResponse
                {
                    Status = false,
                    Message = "An error occurred while inserting broadcast message."
                };
            }
        }

        public async Task<InsertBroadcastMessageResponse> InsertBroadcastMessageWithAttachmentsAsync(
            InsertBroadcastMessageWithAttachmentsRequest form,
            CancellationToken cancellationToken)
        {
            var storedAttachments = new List<StoredBroadcastAttachment>();
            long? insertedBmsgId = null;

            async Task RollBackInsertAsync()
            {
                storedAttachments.ForEach(_attachmentStorage.DeleteIfExists);

                if (!insertedBmsgId.HasValue)
                    return;

                try
                {
                    await _messageRepository.DeleteBroadcastMessageAsync(insertedBmsgId.Value);
                }
                catch (Exception cleanupException)
                {
                    _logger.LogError(
                        cleanupException,
                        "Unable to remove broadcast message {BmsgId} after attachment storage failed",
                        insertedBmsgId.Value);
                }
            }

            try
            {
                if (!form.MesgTypeId.HasValue || form.MesgTypeId.Value <= 0)
                {
                    return new InsertBroadcastMessageResponse
                    {
                        Status = false,
                        Message = "Select a valid message type."
                    };
                }

                if (form.Photo1 != null) _attachmentStorage.Validate(form.Photo1, "photo");
                if (form.Photo2 != null) _attachmentStorage.Validate(form.Photo2, "photo");
                if (form.Video != null) _attachmentStorage.Validate(form.Video, "video");
                if (form.Document != null) _attachmentStorage.Validate(form.Document, "document");
                if (form.Audio != null) _attachmentStorage.Validate(form.Audio, "audio");

                string? storedAbbreviation = await _messageRepository.GetMessageTypeAbbreviationAsync(
                    form.MesgTypeId.Value);
                string abbreviation = new((storedAbbreviation ?? string.Empty)
                    .Trim()
                    .ToUpperInvariant()
                    .Where(char.IsLetterOrDigit)
                    .ToArray());

                if (string.IsNullOrWhiteSpace(abbreviation))
                {
                    return new InsertBroadcastMessageResponse
                    {
                        Status = false,
                        Message = "The selected message type does not have a valid abbreviation code."
                    };
                }

                var request = new InsertBroadcastMessageRequest
                {
                    MessageHead = form.MessageHead,
                    MessageBody = form.MessageBody,
                    MesgTypeId = form.MesgTypeId,
                    EntryBy = form.EntryBy,
                    IsAllDistrict = true,
                    IsActive = "Y",
                    ValidityInMinutes = 60
                };

                InsertBroadcastMessageResponse insertResult = await InsertBroadcastMessageAsync(request);
                if (!insertResult.Status || !insertResult.Bmsg_id.HasValue)
                    return insertResult;

                insertedBmsgId = insertResult.Bmsg_id.Value;
                string filePrefix = $"{abbreviation}_{insertedBmsgId.Value:D8}";

                StoredBroadcastAttachment? photo1 = form.Photo1 == null
                    ? null
                    : await _attachmentStorage.SaveAsync(
                        form.Photo1,
                        "photo",
                        $"{filePrefix}_P01",
                        cancellationToken);
                if (photo1 != null) storedAttachments.Add(photo1);

                StoredBroadcastAttachment? photo2 = form.Photo2 == null
                    ? null
                    : await _attachmentStorage.SaveAsync(
                        form.Photo2,
                        "photo",
                        $"{filePrefix}_P02",
                        cancellationToken);
                if (photo2 != null) storedAttachments.Add(photo2);

                StoredBroadcastAttachment? video = form.Video == null
                    ? null
                    : await _attachmentStorage.SaveAsync(
                        form.Video,
                        "video",
                        $"{filePrefix}_V01",
                        cancellationToken);
                if (video != null) storedAttachments.Add(video);

                StoredBroadcastAttachment? document = form.Document == null
                    ? null
                    : await _attachmentStorage.SaveAsync(
                        form.Document,
                        "document",
                        $"{filePrefix}_D01",
                        cancellationToken);
                if (document != null) storedAttachments.Add(document);

                StoredBroadcastAttachment? audio = form.Audio == null
                    ? null
                    : await _attachmentStorage.SaveAsync(
                        form.Audio,
                        "audio",
                        $"{filePrefix}_A01",
                        cancellationToken);
                if (audio != null) storedAttachments.Add(audio);

                int rowsUpdated = await _messageRepository.UpdateBroadcastAttachmentPathsAsync(
                    insertedBmsgId.Value,
                    photo1?.RelativePath,
                    photo2?.RelativePath,
                    video?.RelativePath,
                    document?.RelativePath,
                    audio?.RelativePath);

                if (rowsUpdated != 1)
                    throw new InvalidOperationException("Unable to update the stored attachment paths.");

                BroadcastMessageDto? savedMessage = await _messageRepository.GetBroadcastMessageByIdAsync(
                    insertedBmsgId.Value);
                return new InsertBroadcastMessageResponse
                {
                    Status = true,
                    Message = "Broadcast message and attachments saved successfully.",
                    Bmsg_id = insertedBmsgId.Value,
                    Data = savedMessage
                };
            }
            catch (InvalidDataException ex)
            {
                await RollBackInsertAsync();
                return new InsertBroadcastMessageResponse { Status = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                await RollBackInsertAsync();
                _logger.LogError(ex, "Error uploading broadcast message attachments");
                _exceptionLogging.SendErrorToText(ex);
                return new InsertBroadcastMessageResponse
                {
                    Status = false,
                    Message = "Unable to upload one or more attachments."
                };
            }
        }

        public async Task<ForwardDraftMessagesResponse> ForwardDraftMessagesAsync(ForwardDraftMessagesRequest request)
        {
            string role = (request.UserRole ?? string.Empty).Trim().ToUpperInvariant();
            if (role is not ("MKR" or "MAKER" or "CKR" or "CHECKER" or "APR" or "APPROVER"))
            {
                return new ForwardDraftMessagesResponse
                {
                    Status = false,
                    Message = "Only Maker, Checker and Approver roles can forward draft messages."
                };
            }

            request.BmsgIds = request.BmsgIds.Where(id => id > 0).Distinct().ToList();
            if (request.BmsgIds.Count == 0)
            {
                return new ForwardDraftMessagesResponse
                {
                    Status = false,
                    Message = "Select at least one message to forward."
                };
            }

            if (string.IsNullOrWhiteSpace(request.ActionBy))
            {
                return new ForwardDraftMessagesResponse
                {
                    Status = false,
                    Message = "The forwarding user is required."
                };
            }

            try
            {
                int processedCount = await _messageRepository.ForwardDraftMessagesAsync(
                    request.BmsgIds,
                    role,
                    request.ActionBy,
                    request.ActionIP);

                string destination = role is "MKR" or "MAKER"
                    ? "Checker"
                    : role is "CKR" or "CHECKER" ? "Approver" : "final approval";

                return new ForwardDraftMessagesResponse
                {
                    Status = processedCount > 0,
                    ProcessedCount = processedCount,
                    Message = processedCount > 0
                        ? $"{processedCount} message(s) forwarded to {destination} successfully."
                        : "No eligible messages were forwarded. Refresh the grid and try again."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding broadcast draft messages for role {Role}", role);
                _exceptionLogging.SendErrorToText(ex);
                return new ForwardDraftMessagesResponse
                {
                    Status = false,
                    Message = "Unable to forward the selected messages."
                };
            }
        }

        public async Task<UpdateBroadcastStatusResponse> UpdateBroadcastVerifiedAsync(UpdateBroadcastVerifiedRequest request)
        {
            if (request == null)
            {
                return new UpdateBroadcastStatusResponse
                {
                    Status = false,
                    StatusCode = 400,
                    Message = "Invalid request payload."
                };
            }

            if (request.Bmsg_id <= 0)
            {
                return new UpdateBroadcastStatusResponse
                {
                    Status = false,
                    StatusCode = 400,
                    Message = "A valid Bmsg_id greater than 0 is required."
                };
            }

            if (!string.IsNullOrWhiteSpace(request.VerifiedBy))
            {
                request.VerifiedBy = DecryptIfNeeded(request.VerifiedBy, request.ClientKey);
            }

            try
            {
                bool exists = await _messageRepository.BroadcastMessageExistsAsync(request.Bmsg_id);
                if (!exists)
                {
                    return new UpdateBroadcastStatusResponse
                    {
                        Status = false,
                        StatusCode = 404,
                        Bmsg_id = request.Bmsg_id,
                        Message = $"Broadcast message with ID {request.Bmsg_id} was not found."
                    };
                }

                int rowsAffected = await _messageRepository.UpdateBroadcastVerifiedStatusAsync(
                    request.Bmsg_id,
                    request.IsVerified,
                    request.VerifiedBy,
                    request.VerifiedIP);

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Broadcast message {Bmsg_id} IsVerified updated to {IsVerified} by {VerifiedBy} (IP: {VerifiedIP})",
                        request.Bmsg_id, request.IsVerified, request.VerifiedBy ?? "Unknown", request.VerifiedIP ?? "Unknown");

                    return new UpdateBroadcastStatusResponse
                    {
                        Status = true,
                        StatusCode = 200,
                        Bmsg_id = request.Bmsg_id,
                        IsVerified = request.IsVerified,
                        Message = request.IsVerified
                            ? "Broadcast message verified and forwarded to Approver successfully."
                            : "Broadcast message returned to Maker for correction."
                    };
                }

                return new UpdateBroadcastStatusResponse
                {
                    Status = false,
                    StatusCode = 500,
                    Bmsg_id = request.Bmsg_id,
                    Message = "This message is no longer pending in the Checker queue. Refresh the grid and try again."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating IsVerified status for broadcast message {Bmsg_id}", request.Bmsg_id);
                _exceptionLogging.SendErrorToText(ex);

                return new UpdateBroadcastStatusResponse
                {
                    Status = false,
                    StatusCode = 500,
                    Bmsg_id = request.Bmsg_id,
                    Message = "An error occurred while updating the verification status."
                };
            }
        }

        public async Task<UpdateBroadcastStatusResponse> UpdateBroadcastApprovedAsync(UpdateBroadcastApprovedRequest request)
        {
            if (request == null)
            {
                return new UpdateBroadcastStatusResponse
                {
                    Status = false,
                    StatusCode = 400,
                    Message = "Invalid request payload."
                };
            }

            if (request.Bmsg_id <= 0)
            {
                return new UpdateBroadcastStatusResponse
                {
                    Status = false,
                    StatusCode = 400,
                    Message = "A valid Bmsg_id greater than 0 is required."
                };
            }

            if (!string.IsNullOrWhiteSpace(request.ApprovedBy))
            {
                request.ApprovedBy = DecryptIfNeeded(request.ApprovedBy, request.ClientKey);
            }

            try
            {
                bool exists = await _messageRepository.BroadcastMessageExistsAsync(request.Bmsg_id);
                if (!exists)
                {
                    return new UpdateBroadcastStatusResponse
                    {
                        Status = false,
                        StatusCode = 404,
                        Bmsg_id = request.Bmsg_id,
                        Message = $"Broadcast message with ID {request.Bmsg_id} was not found."
                    };
                }

                int rowsAffected = await _messageRepository.UpdateBroadcastApprovedStatusAsync(
                    request.Bmsg_id,
                    request.IsApproved,
                    request.ApprovedBy,
                    request.ApprovedIP);

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Broadcast message {Bmsg_id} IsApproved updated to {IsApproved} by {ApprovedBy} (IP: {ApprovedIP})",
                        request.Bmsg_id, request.IsApproved, request.ApprovedBy ?? "Unknown", request.ApprovedIP ?? "Unknown");

                    return new UpdateBroadcastStatusResponse
                    {
                        Status = true,
                        StatusCode = 200,
                        Bmsg_id = request.Bmsg_id,
                        IsApproved = request.IsApproved,
                        Message = request.IsApproved
                            ? "Broadcast message approved successfully."
                            : "Broadcast message returned to Checker for correction."
                    };
                }

                return new UpdateBroadcastStatusResponse
                {
                    Status = false,
                    StatusCode = 500,
                    Bmsg_id = request.Bmsg_id,
                    Message = "This message is no longer pending in the Approver queue. Refresh the grid and try again."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating IsApproved status for broadcast message {Bmsg_id}", request.Bmsg_id);
                _exceptionLogging.SendErrorToText(ex);

                return new UpdateBroadcastStatusResponse
                {
                    Status = false,
                    StatusCode = 500,
                    Bmsg_id = request.Bmsg_id,
                    Message = "An error occurred while updating the approval status."
                };
            }
        }

        public async Task<InsertInterestWiseMessageResponse> InsertInterestWiseMessageAsync(InsertInterestWiseMessageRequest request)
        {
            if (request == null)
            {
                return new InsertInterestWiseMessageResponse
                {
                    Status = false,
                    Message = "Invalid request payload."
                };
            }

            if (request.InterestId <= 0)
            {
                return new InsertInterestWiseMessageResponse
                {
                    Status = false,
                    Message = "A valid interest_id is required."
                };
            }

            // Decrypt fields if client key is provided
            string messageHead = DecryptIfNeeded(request.MessageHead ?? string.Empty, request.ClientKey);
            string messageBody = DecryptIfNeeded(request.MessageBody ?? string.Empty, request.ClientKey);

            if (string.IsNullOrWhiteSpace(messageBody))
            {
                return new InsertInterestWiseMessageResponse
                {
                    Status = false,
                    Message = "Message body is required."
                };
            }

            request.MessageHead = messageHead;
            request.MessageBody = messageBody;

            if (!string.IsNullOrWhiteSpace(request.Photo1Path))
                request.Photo1Path = DecryptIfNeeded(request.Photo1Path, request.ClientKey);

            if (!string.IsNullOrWhiteSpace(request.Photo2Path))
                request.Photo2Path = DecryptIfNeeded(request.Photo2Path, request.ClientKey);

            if (!string.IsNullOrWhiteSpace(request.VideoPath))
                request.VideoPath = DecryptIfNeeded(request.VideoPath, request.ClientKey);

            if (!string.IsNullOrWhiteSpace(request.DocumentPath))
                request.DocumentPath = DecryptIfNeeded(request.DocumentPath, request.ClientKey);

            if (!string.IsNullOrWhiteSpace(request.YoutubeUrl))
                request.YoutubeUrl = DecryptIfNeeded(request.YoutubeUrl, request.ClientKey);

            if (!string.IsNullOrWhiteSpace(request.FacebookUrl))
                request.FacebookUrl = DecryptIfNeeded(request.FacebookUrl, request.ClientKey);

            if (!string.IsNullOrWhiteSpace(request.InstagramUrl))
                request.InstagramUrl = DecryptIfNeeded(request.InstagramUrl, request.ClientKey);

            if (!string.IsNullOrWhiteSpace(request.XUrl))
                request.XUrl = DecryptIfNeeded(request.XUrl, request.ClientKey);

            if (!string.IsNullOrWhiteSpace(request.EntryBy))
                request.EntryBy = DecryptIfNeeded(request.EntryBy, request.ClientKey);

            try
            {
                long msgId = await _messageRepository.InsertInterestWiseMessageAsync(request);

                if (msgId <= 0)
                {
                    return new InsertInterestWiseMessageResponse
                    {
                        Status = false,
                        Message = "Failed to insert interest-wise message."
                    };
                }

                var savedMessage = await _messageRepository.GetMessageByIdAsync(msgId);

                return new InsertInterestWiseMessageResponse
                {
                    Status = true,
                    Message = "Interest-wise message inserted successfully.",
                    MsgId = msgId,
                    Data = savedMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting interest-wise message into tbl_Message");
                _exceptionLogging.SendErrorToText(ex);

                return new InsertInterestWiseMessageResponse
                {
                    Status = false,
                    Message = "An error occurred while inserting interest-wise message."
                };
            }
        }

        public async Task<List<InterestWiseMessageDto>> GetInterestWiseMessagesAsync(long? interestId = null)
        {
            return await _messageRepository.GetInterestWiseMessagesAsync(interestId);
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
    }
}
