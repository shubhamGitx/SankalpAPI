using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using SankalpAPI.DTO;
using SankalpAPI.Helper;
using SankalpAPI.Interface;

namespace SankalpAPI.Services
{
    public class FcmNotificationService : IFcmNotificationService
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<FcmNotificationService> _logger;
        private readonly ExceptionLogging _exceptionLogging;
        private readonly FcmMessageFactory _messageFactory;

        public FcmNotificationService(
            IUserRepository userRepository,
            ILogger<FcmNotificationService> logger,
            ExceptionLogging exceptionLogging,
            FcmMessageFactory messageFactory)
        {
            _userRepository = userRepository;
            _logger = logger;
            _exceptionLogging = exceptionLogging;
            _messageFactory = messageFactory;
        }

        public async Task<ServiceResult<SendUserNotificationResponse>> SendToUserAsync(
            SendUserNotificationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.UserId) ||
                string.IsNullOrWhiteSpace(request.Title) ||
                string.IsNullOrWhiteSpace(request.Body))
            {
                return ServiceResult<SendUserNotificationResponse>.Ok(
                    StatusCodes.Status400BadRequest,
                    new SendUserNotificationResponse
                    {
                        Success = false,
                        Message = "UserId, Title, and Body are required."
                    });
            }

            if (FirebaseApp.DefaultInstance == null || FirebaseMessaging.DefaultInstance == null)
            {
                _logger.LogError("FirebaseApp.DefaultInstance is not initialized. Verify Firebase service-account credentials on the server.");
                return ServiceResult<SendUserNotificationResponse>.Ok(
                    StatusCodes.Status503ServiceUnavailable,
                    new SendUserNotificationResponse
                    {
                        Success = false,
                        Message = "Push notification service is currently unavailable."
                    });
            }

            string cleanUserId = request.UserId.Trim();

            try
            {
                var devices = await _userRepository.GetActiveUserFcmDevicesAsync(cleanUserId, cancellationToken);

                var tokens = devices
                    .Select(d => d.FcmToken)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct()
                    .ToList();

                if (tokens.Count == 0)
                {
                    return ServiceResult<SendUserNotificationResponse>.Ok(
                        StatusCodes.Status404NotFound,
                        new SendUserNotificationResponse
                        {
                            Success = false,
                            Message = "No active FCM devices found for the specified user.",
                            UserId = cleanUserId,
                            TotalDevices = 0,
                            SuccessCount = 0,
                            FailureCount = 0,
                            DeactivatedTokenCount = 0
                        });
                }

                var dataPayload = request.Data != null
                    ? new Dictionary<string, string>(request.Data)
                    : new Dictionary<string, string>();

                var multicastMessage = new MulticastMessage
                {
                    Tokens = tokens,
                    Notification = _messageFactory.CreateNotification(
                        request.Title,
                        request.Body,
                        request.ImageUrl),
                    Data = dataPayload,
                    Android = _messageFactory.CreateAndroidConfig()
                };

                BatchResponse batchResponse = await FirebaseMessaging.DefaultInstance
                    .SendEachForMulticastAsync(multicastMessage, cancellationToken);

                var invalidTokens = new List<string>();

                for (int i = 0; i < batchResponse.Responses.Count; i++)
                {
                    SendResponse resp = batchResponse.Responses[i];
                    if (!resp.IsSuccess && resp.Exception != null)
                    {
                        var errorCode = resp.Exception.MessagingErrorCode;
                        if (errorCode == MessagingErrorCode.Unregistered ||
                            errorCode == MessagingErrorCode.InvalidArgument)
                        {
                            invalidTokens.Add(tokens[i]);
                        }

                        _logger.LogWarning(
                            "FCM delivery failed for UserId {UserId} (ErrorCode: {ErrorCode}): {ErrorMessage}",
                            cleanUserId,
                            errorCode,
                            resp.Exception.Message);
                    }
                }

                int deactivatedCount = 0;
                if (invalidTokens.Count > 0)
                {
                    deactivatedCount = await _userRepository.DeactivateInvalidFcmTokensAsync(
                        invalidTokens,
                        cancellationToken);
                }

                bool anySuccess = batchResponse.SuccessCount > 0;

                return ServiceResult<SendUserNotificationResponse>.Ok(
                    StatusCodes.Status200OK,
                    new SendUserNotificationResponse
                    {
                        Success = anySuccess,
                        Message = anySuccess
                            ? $"Notification sent to {batchResponse.SuccessCount} of {tokens.Count} active device(s)."
                            : "Failed to deliver notification to any active device.",
                        UserId = cleanUserId,
                        TotalDevices = tokens.Count,
                        SuccessCount = batchResponse.SuccessCount,
                        FailureCount = batchResponse.FailureCount,
                        DeactivatedTokenCount = deactivatedCount
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending FCM notification to UserId {UserId}", cleanUserId);
                _exceptionLogging.SendErrorToText(ex);

                return ServiceResult<SendUserNotificationResponse>.Ok(
                    StatusCodes.Status500InternalServerError,
                    new SendUserNotificationResponse
                    {
                        Success = false,
                        Message = "An error occurred while sending push notification.",
                        UserId = cleanUserId
                    });
            }
        }
    }
}
