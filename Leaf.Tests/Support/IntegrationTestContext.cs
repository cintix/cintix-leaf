using Leaf.Web.Core.Security;
using Leaf.Web.Core.Time;
using Leaf.Web.Features.Auth.Services;
using Leaf.Web.Features.Reports.Services;
using Leaf.Web.Features.Search.Services;
using Leaf.Web.Features.Shared;
using Leaf.Web.Features.Shared.Contracts;
using Leaf.Web.Features.Shared.Models;
using Leaf.Web.Features.Sprints.Services;
using Leaf.Web.Features.WorkItems.Services;
using Leaf.Web.Infrastructure.Data;
using Leaf.Web.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leaf.Tests.Support;

public sealed class IntegrationTestContext : IAsyncDisposable
{
    private IntegrationTestContext(
        string dbPath,
        ILeafRepository repository,
        AuthService authService,
        WorkItemService workItemService,
        SprintService sprintService,
        SearchService searchService,
        ReportService reportService,
        FixedClock clock)
    {
        DbPath = dbPath;
        Repository = repository;
        AuthService = authService;
        WorkItemService = workItemService;
        SprintService = sprintService;
        SearchService = searchService;
        ReportService = reportService;
        Clock = clock;
    }

    public string DbPath { get; }
    public ILeafRepository Repository { get; }
    public AuthService AuthService { get; }
    public WorkItemService WorkItemService { get; }
    public SprintService SprintService { get; }
    public SearchService SearchService { get; }
    public ReportService ReportService { get; }
    public FixedClock Clock { get; }

    public static async Task<IntegrationTestContext> CreateAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"leaf-tests-{Guid.NewGuid():N}.db");
        var options = Options.Create(new LeafDatabaseOptions { Path = dbPath });
        var connectionFactory = new SqliteConnectionFactory(options);
        var hasher = new Pbkdf2PasswordHasher();
        var clock = new FixedClock(new DateTime(2026, 04, 20, 12, 00, 00, DateTimeKind.Utc));

        var initializer = new SqliteSchemaInitializer(
            connectionFactory,
            hasher,
            clock,
            NullLogger<SqliteSchemaInitializer>.Instance);

        await initializer.InitializeAsync();

        var repo = new SqliteLeafRepository(connectionFactory);
        var auth = new AuthService(repo, hasher, clock);
        var workItem = new WorkItemService(repo, clock);
        var sprint = new SprintService(repo, clock);
        var search = new SearchService(repo);
        var report = new ReportService(repo);

        return new IntegrationTestContext(dbPath, repo, auth, workItem, sprint, search, report, clock);
    }

    public async Task<int> CreateWorkItemAsync(
        string title,
        int storyPoints,
        int? assigneeUserId = null,
        string labelsCsv = "backend",
        int projectId = 1)
    {
        var result = await WorkItemService.CreateAsync(new CreateWorkItemInput(
            projectId,
            null,
            null,
            title,
            "test description",
            WorkItemType.Story,
            WorkItemPriority.Medium,
            assigneeUserId,
            ReporterUserId: 1,
            labelsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            storyPoints,
            DueDate: null));

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Message);
        }

        return result.Value;
    }

    public async ValueTask DisposeAsync()
    {
        if (File.Exists(DbPath))
        {
            // SQLite can keep journals around; remove both files when tests complete.
            File.Delete(DbPath);
            var wal = DbPath + "-wal";
            var shm = DbPath + "-shm";
            if (File.Exists(wal)) File.Delete(wal);
            if (File.Exists(shm)) File.Delete(shm);
        }

        await Task.CompletedTask;
    }
}

public sealed class FixedClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; set; } = utcNow;
}
