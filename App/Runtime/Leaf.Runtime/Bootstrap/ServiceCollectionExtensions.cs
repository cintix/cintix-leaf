using Leaf.Core.Security;
using Leaf.Core.Time;
using Leaf.Features.Authentication.Core;
using Leaf.Features.Dashboard.Core;
using Leaf.Features.Projects.Core;
using Leaf.Features.Reports.Core;
using Leaf.Features.Search.Core;
using Leaf.Features.Shared.Core;
using Leaf.Features.Sprints.Core;
using Leaf.Features.Users.Core;
using Leaf.Features.WorkItems.Core;
using Leaf.Infrastructure.Data;
using Leaf.Infrastructure.Security;
using Leaf.Infrastructure.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leaf.Runtime.Bootstrap;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLeafRuntime(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<LeafDatabaseOptions>(config.GetSection(LeafDatabaseOptions.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<SqliteSchemaInitializer>();
        services.AddScoped<ILeafRepository, SqliteLeafRepository>();

        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<WorkItemService>();
        services.AddScoped<SprintService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ReportService>();
        services.AddScoped<SearchService>();

        services.AddScoped<MetricsWorkerRunner>();
        services.AddHostedService<LeafStartupService>();
        services.AddHostedService<MetricsRefreshWorker>();

        return services;
    }
}
