using Leaf.Web.Core.Security;
using Leaf.Web.Core.Time;
using Leaf.Web.Features.Auth;
using Leaf.Web.Features.Auth.Services;
using Leaf.Web.Features.Dashboard;
using Leaf.Web.Features.Dashboard.Services;
using Leaf.Web.Features.Projects;
using Leaf.Web.Features.Projects.Services;
using Leaf.Web.Features.Reports;
using Leaf.Web.Features.Reports.Services;
using Leaf.Web.Features.Search;
using Leaf.Web.Features.Search.Services;
using Leaf.Web.Features.Shared;
using Leaf.Web.Features.Sprints;
using Leaf.Web.Features.Sprints.Services;
using Leaf.Web.Features.Users;
using Leaf.Web.Features.Users.Services;
using Leaf.Web.Features.WorkItems;
using Leaf.Web.Features.WorkItems.Services;
using Leaf.Web.Infrastructure.Data;
using Leaf.Web.Infrastructure.Security;
using Leaf.Web.Infrastructure.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leaf.Web.Runtime.Bootstrap;

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

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IWorkItemService, WorkItemService>();
        services.AddScoped<ISprintService, SprintService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ISearchService, SearchService>();

        services.AddScoped<MetricsWorkerRunner>();
        services.AddHostedService<LeafStartupService>();
        services.AddHostedService<MetricsRefreshWorker>();

        return services;
    }
}
