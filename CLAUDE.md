# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet restore Leaf.sln
dotnet build Leaf.sln
dotnet run --project Leaf.Web/Leaf.Web.csproj    # listens on http://0.0.0.0:8080
docker compose up --build                         # production-like run
```

## Test

```bash
dotnet test Leaf.sln                              # all tests
dotnet test Leaf.sln --filter "FullyQualifiedName~LeafWorkflowTests"  # single test class
```

Tests use xUnit with no mocking — `IntegrationTestContext` creates a fresh per-test SQLite database, runs schema init + seed, and wires real services against it. The `FixedClock` (implements `IClock`) pins time to `2026-04-20T12:00:00Z`.

## Architecture

Dependency direction: `UI / Runtime / Infrastructure → Features → Core`

- **`Core/`** — `Result`/`Result<T>` discriminated unions for error handling, `Error` with codes (`validation`, `unauthorized`, `not-found`, `conflict`). `IClock` abstraction and `IPasswordHasher`.
- **`Features/`** — Vertical slices (Auth, Dashboard, Projects, Sprints, WorkItems, Users, Search, Reports). Each has an `IXxxService` interface and a service class. `Features/Shared/` contains `ILeafRepository` (single large data-access interface), domain models, and input DTOs. Services delegate to the repository and own domain logic; controllers inject both services and the repository.
- **`Infrastructure/`** — `SqliteLeafRepository` implements `ILeafRepository` with raw ADO.NET (`Microsoft.Data.Sqlite`), no ORM. `SqliteSchemaInitializer` runs at startup (via `LeafStartupService` as an `IHostedService`) — it creates tables with `CREATE TABLE IF NOT EXISTS` inside a single transaction, then seeds demo users/project/sprint/work items if tables are empty. `MetricsRefreshWorker` is a `BackgroundService` that ticks every 5 minutes to update `SprintDailyMetrics` and `DashboardCache`.
- **`UI/`** — Controllers inherit from `LeafControllerBase`, which exposes `CurrentUserId` (from session) and `BuildShellAsync()` (loads sidebar context). `ApiController` handles JSON endpoints for the SPA-like frontend. Views are Razor `.cshtml`; all dynamic interactivity is vanilla JS in `wwwroot/js/app.js` (drag-and-drop backlog/board, modals, toast notifications, form submissions via `fetch`).
- **`Runtime/Bootstrap/`** — `ServiceCollectionExtensions.AddLeafRuntime()` registers all services. `LeafStartupService` runs on app start to initialize the SQLite schema.

## Key conventions

- **No ORM** — `SqliteLeafRepository` hand-writes SQL with `SqliteParameter`. Dates are stored as ISO 8601 strings, DateOnly as `yyyy-MM-dd`. Booleans are stored as `INTEGER` (0/1).
- **Error handling** — Services return `Result<T>` or `Result`. Controllers check `result.IsFailure` and return `this.ToErrorJson(...)` for API or `View()` with model errors for pages.
- **Auth** — Session-based (`HttpContext.Session`) with an optional persistent "remember me" cookie (`leaf_remember_user`). The middleware in `Program.cs` checks session on every non-public path. Demo credentials are in the README.
- **Database path** — Configured via `LeafDatabase:Path` (default `Runtime/leaf.db`). For tests, `IntegrationTestContext` creates a temp-file database at a random path.
- **ProjectKey format** — Short uppercase string (e.g., `LEAF`) unique across projects.
- **Labels** — Stored as CSV in `WorkItems.LabelsCsv` column (no join table). Filtered with `LIKE %label%`.
