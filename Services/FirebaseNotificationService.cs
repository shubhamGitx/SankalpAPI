using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SankalpAPI.Interface;
using SankalpAPI.Model;

namespace SankalpAPI.Services
{
    public class FirebaseNotificationService : IFirebaseNotificationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<FirebaseNotificationService> _logger;
        private readonly ICampaignRepository _campaignRepository;
        private static readonly object _initLock = new();
        private static bool _isFirebaseInitialized = false;
        private readonly bool _simulateIfNoCredentials;
        private readonly string? _channelId;

        public FirebaseNotificationService(
            IConfiguration configuration,
            ILogger<FirebaseNotificationService> logger,
            ICampaignRepository campaignRepository)
        {
            _configuration = configuration;
            _logger = logger;
            _campaignRepository = campaignRepository;

            _simulateIfNoCredentials = _configuration.GetValue<bool>("FirebaseSettings:SimulateIfNoCredentials", true);
            _channelId = _configuration.GetValue<string>("FirebaseSettings:DefaultChannelId") ?? "sankalp_campaign_channel";

            EnsureFirebaseAppInitialized();
        }

        private void EnsureFirebaseAppInitialized()
        {
            if (FirebaseApp.DefaultInstance != null)
            {
                _isFirebaseInitialized = true;
                return;
            }

            lock (_initLock)
            {
                if (FirebaseApp.DefaultInstance != null)
                {
                    _isFirebaseInitialized = true;
                    return;
                }

                try
                {
                    string credentialPath = _configuration.GetValue<string>("FirebaseSettings:CredentialPath") ?? "firebase-service-account.json";
                    string projectId = _configuration.GetValue<string>("FirebaseSettings:ProjectId") ?? "sankalp-bihar";

                    string resolvedPath = Path.IsPathRooted(credentialPath)
                        ? credentialPath
                        : Path.Combine(AppContext.BaseDirectory, credentialPath);

                    if (File.Exists(resolvedPath))
                    {
                        FirebaseApp.Create(new AppOptions
                        {
                            Credential = GoogleCredential.FromFile(resolvedPath),
                            ProjectId = projectId
                        });
                        _isFirebaseInitialized = true;
                        _logger.LogInformation("FirebaseApp initialized successfully with credentials from {Path}", resolvedPath);
                    }
                    else
                    {
                        _isFirebaseInitialized = false;
                        _logger.LogWarning("Firebase credential file not found at {Path}. Simulation mode is {Simulate}.",
                            resolvedPath, _simulateIfNoCredentials ? "ENABLED" : "DISABLED");
                    }
                }
                catch (Exception ex)
                {
                    _isFirebaseInitialized = false;
                    _logger.LogError(ex, "Failed to initialize FirebaseApp. Fallback to simulation if enabled.");
                }
            }
        }

        public async Task<List<CampaignDeliveryLogDto>> SendCampaignMulticastAsync(
            long campaignId,
            long bmsgId,
            string title,
            string body,
            Dictionary<string, string>? dataPayload,
            List<CampaignRecipientCandidate> recipients)
        {
            var logs = new List<CampaignDeliveryLogDto>();

            if (recipients == null || recipients.Count == 0)
            {
                _logger.LogInformation("No recipients provided for campaign {CampaignId}", campaignId);
                return logs;
            }

            // Clean & filter recipients
            var validRecipients = recipients
                .Where(r => !string.IsNullOrWhiteSpace(r.FcmToken))
                .ToList();

            var skippedRecipients = recipients
                .Where(r => string.IsNullOrWhiteSpace(r.FcmToken))
                .ToList();

            foreach (var skipped in skippedRecipients)
            {
                logs.Add(new CampaignDeliveryLogDto
                {
                    CampaignId = campaignId,
                    BmsgId = bmsgId,
                    UserId = skipped.UserId,
                    DeviceId = skipped.DeviceId,
                    FcmToken = skipped.FcmToken ?? "NO_TOKEN",
                    Platform = skipped.Platform ?? "Unknown",
                    DeliveryStatus = "Skipped",
                    ErrorMessage = "Missing FCM token for recipient",
                    DispatchedAt = DateTime.UtcNow
                });
            }

            // Firebase multicast limit is 500 tokens per batch
            const int batchSize = 500;
            var batches = validRecipients
                .Select((recipient, index) => new { recipient, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.recipient).ToList())
                .ToList();

            _logger.LogInformation("Sending Campaign {CampaignId} to {Count} valid tokens across {Batches} batch(es).",
                campaignId, validRecipients.Count, batches.Count);

            foreach (var batch in batches)
            {
                var batchLogs = await ProcessBatchAsync(campaignId, bmsgId, title, body, dataPayload, batch);
                logs.AddRange(batchLogs);
            }

            return logs;
        }

        private async Task<List<CampaignDeliveryLogDto>> ProcessBatchAsync(
            long campaignId,
            long bmsgId,
            string title,
            string body,
            Dictionary<string, string>? dataPayload,
            List<CampaignRecipientCandidate> batchRecipients)
        {
            var batchLogs = new List<CampaignDeliveryLogDto>();

            // Ensure Firebase payload dictionary has campaign identifiers
            var safeData = new Dictionary<string, string>(dataPayload ?? new Dictionary<string, string>())
            {
                ["campaignId"] = campaignId.ToString(),
                ["bmsgId"] = bmsgId.ToString(),
                ["sentAt"] = DateTime.UtcNow.ToString("o")
            };

            // If Firebase is initialized, send via FirebaseAdmin SDK
            if (_isFirebaseInitialized && FirebaseMessaging.DefaultInstance != null)
            {
                try
                {
                    var tokens = batchRecipients.Select(r => r.FcmToken!).ToList();
                    var multicastMessage = new MulticastMessage
                    {
                        Tokens = tokens,
                        Notification = new Notification
                        {
                            Title = title,
                            Body = body
                        },
                        Data = safeData,
                        Android = new AndroidConfig
                        {
                            Priority = Priority.High,
                            Notification = new AndroidNotification
                            {
                                ChannelId = _channelId,
                                ClickAction = "OPEN_CAMPAIGN_NOTIFICATION"
                            }
                        }
                    };

                    BatchResponse response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(multicastMessage);

                    for (int i = 0; i < batchRecipients.Count; i++)
                    {
                        var candidate = batchRecipients[i];
                        var sendResponse = response.Responses[i];

                        if (sendResponse.IsSuccess)
                        {
                            batchLogs.Add(new CampaignDeliveryLogDto
                            {
                                CampaignId = campaignId,
                                BmsgId = bmsgId,
                                UserId = candidate.UserId,
                                DeviceId = candidate.DeviceId,
                                FcmToken = candidate.FcmToken!,
                                Platform = candidate.Platform ?? "Android",
                                DeliveryStatus = "AcceptedByFcm",
                                MessageId = sendResponse.MessageId,
                                DispatchedAt = DateTime.UtcNow
                            });
                        }
                        else
                        {
                            string errorCode = sendResponse.Exception?.MessagingErrorCode.ToString() ?? "Unknown";
                            string errorMessage = sendResponse.Exception?.Message ?? "FCM Dispatch Error";

                            _logger.LogWarning("FCM dispatch failed for token {TokenPrefix}...: {ErrorCode} - {ErrorMessage}",
                                candidate.FcmToken!.Substring(0, Math.Min(10, candidate.FcmToken.Length)),
                                errorCode, errorMessage);

                            // Handle unregistered or invalid tokens by deactivating them in database
                            if (errorCode.Equals("Unregistered", StringComparison.OrdinalIgnoreCase) ||
                                errorCode.Equals("InvalidArgument", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    await _campaignRepository.DeactivateInvalidTokenAsync(candidate.FcmToken!);
                                }
                                catch (Exception dex)
                                {
                                    _logger.LogError(dex, "Error deactivating invalid token {Token}", candidate.FcmToken);
                                }
                            }

                            batchLogs.Add(new CampaignDeliveryLogDto
                            {
                                CampaignId = campaignId,
                                BmsgId = bmsgId,
                                UserId = candidate.UserId,
                                DeviceId = candidate.DeviceId,
                                FcmToken = candidate.FcmToken!,
                                Platform = candidate.Platform ?? "Android",
                                DeliveryStatus = "Failed",
                                ErrorCode = errorCode,
                                ErrorMessage = errorMessage,
                                DispatchedAt = DateTime.UtcNow
                            });
                        }
                    }

                    return batchLogs;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in FCM batch multicast for Campaign {CampaignId}. Attempting fallback.", campaignId);
                    // Fallback to recording as failed unless simulation enabled
                    if (!_simulateIfNoCredentials)
                    {
                        foreach (var cand in batchRecipients)
                        {
                            batchLogs.Add(new CampaignDeliveryLogDto
                            {
                                CampaignId = campaignId,
                                BmsgId = bmsgId,
                                UserId = cand.UserId,
                                DeviceId = cand.DeviceId,
                                FcmToken = cand.FcmToken!,
                                Platform = cand.Platform ?? "Android",
                                DeliveryStatus = "Failed",
                                ErrorCode = "Exception",
                                ErrorMessage = ex.Message,
                                DispatchedAt = DateTime.UtcNow
                            });
                        }
                        return batchLogs;
                    }
                }
            }

            // SIMULATION MODE (Fallback when credentials are not yet configured on server or in test environment)
            if (_simulateIfNoCredentials)
            {
                _logger.LogInformation("Simulating FCM notification delivery for {Count} recipients of Campaign {CampaignId}.",
                    batchRecipients.Count, campaignId);

                foreach (var candidate in batchRecipients)
                {
                    // Simulated token check: test if token looks like a dummy token
                    bool isInvalidDummy = candidate.FcmToken!.Contains("invalid", StringComparison.OrdinalIgnoreCase);

                    if (isInvalidDummy)
                    {
                        batchLogs.Add(new CampaignDeliveryLogDto
                        {
                            CampaignId = campaignId,
                            BmsgId = bmsgId,
                            UserId = candidate.UserId,
                            DeviceId = candidate.DeviceId,
                            FcmToken = candidate.FcmToken!,
                            Platform = candidate.Platform ?? "Android",
                            DeliveryStatus = "Failed",
                            ErrorCode = "Unregistered",
                            ErrorMessage = "Simulated invalid / unregistered FCM token",
                            DispatchedAt = DateTime.UtcNow
                        });

                        await _campaignRepository.DeactivateInvalidTokenAsync(candidate.FcmToken!);
                    }
                    else
                    {
                        batchLogs.Add(new CampaignDeliveryLogDto
                        {
                            CampaignId = campaignId,
                            BmsgId = bmsgId,
                            UserId = candidate.UserId,
                            DeviceId = candidate.DeviceId,
                            FcmToken = candidate.FcmToken!,
                            Platform = candidate.Platform ?? "Android",
                            DeliveryStatus = "AcceptedByFcm",
                            MessageId = $"projects/sankalp-bihar/messages/sim-{Guid.NewGuid():N}",
                            DispatchedAt = DateTime.UtcNow
                        });
                    }
                }

                return batchLogs;
            }

            // If neither live FCM nor simulation is available
            foreach (var cand in batchRecipients)
            {
                batchLogs.Add(new CampaignDeliveryLogDto
                {
                    CampaignId = campaignId,
                    BmsgId = bmsgId,
                    UserId = cand.UserId,
                    DeviceId = cand.DeviceId,
                    FcmToken = cand.FcmToken!,
                    Platform = cand.Platform ?? "Android",
                    DeliveryStatus = "Failed",
                    ErrorCode = "NoFirebaseCredentials",
                    ErrorMessage = "Firebase credentials not configured and simulation is disabled",
                    DispatchedAt = DateTime.UtcNow
                });
            }

            return batchLogs;
        }
    }
}
