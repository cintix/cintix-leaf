using Leaf.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leaf.Runtime.Bootstrap;

public sealed class LeafStartupService(
    IServiceProvider services,
    ILogger<LeafStartupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<SqliteSchemaInitializer>();
        await initializer.InitializeAsync(cancellationToken);
        logger.LogInformation("Leaf startup initialization completed");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
