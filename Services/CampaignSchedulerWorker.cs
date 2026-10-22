using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SankalpAPI.Interface;

namespace SankalpAPI.Services
{
    public class CampaignSchedulerWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CampaignSchedulerWorker> _logger;
        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);

        public CampaignSchedulerWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<CampaignSchedulerWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CampaignSchedulerWorker background service started. Polling every {Seconds} seconds.", _pollInterval.TotalSeconds);

            // Allow the application to finish startup
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var campaignService = scope.ServiceProvider.GetRequiredService<ICampaignService>();
                        await campaignService.ExecuteScheduledCampaignsJobAsync();
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error occurred during CampaignSchedulerWorker execution cycle.");
                }

                try
                {
                    await Task.Delay(_pollInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation("CampaignSchedulerWorker background service has stopped.");
        }
    }
}
