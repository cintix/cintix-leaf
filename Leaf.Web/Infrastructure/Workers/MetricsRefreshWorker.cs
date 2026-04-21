using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leaf.Web.Infrastructure.Workers;

public sealed class MetricsRefreshWorker(IServiceProvider services, ILogger<MetricsRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Short first delay so startup can complete quickly before recurring background work.
        await Task.Delay(TimeSpan.FromSeconds(12), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<MetricsWorkerRunner>();
                await runner.RunTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Metrics refresh tick failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
