# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet restore Leaf.sln
dotnet build Leaf.sln
dotnet run --project Leaf.Web/Leaf.Web.csproj    # listens on http://0.0.0.0:8080
podman-compose up --build                        # production-like run
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
- **`Infrastructure/`** — `SqliteLeafRepository` implements `ILeafRepository` with raw ADO.NET (`Microsoft.Data.Sqlite`), no ORM. `SqliteSchemaInitializer` runs at startup (via `LeafStartupService` as an `IHostedService`) — it creates tables with `CREATE TABLE IF NOT EXISTS` inside a single transaction, runs schema migrations (`ALTER TABLE` with try/catch), then seeds demo data if tables are empty. `MetricsRefreshWorker` is a `BackgroundService` that ticks every 5 minutes to update `SprintDailyMetrics` and `DashboardCache`. `Pbkdf2PasswordHasher` implements `IPasswordHasher`.
- **`UI/`** — Controllers inherit from `LeafControllerBase`, which exposes `CurrentUserId` (from session) and `BuildShellAsync()` (loads sidebar context including `CurrentUser` for role checks). `ApiController` handles all JSON endpoints. Views are Razor `.cshtml`; all interactivity is vanilla JS in `wwwroot/js/app.js`. CSS is a custom design system in `wwwroot/css/app.css` (dark sidebar, indigo accent, slate color palette).
- **`Runtime/Bootstrap/`** — `ServiceCollectionExtensions.AddLeafRuntime()` registers all services. `LeafStartupService` runs on app start to initialize the SQLite schema. Data Protection keys are persisted to `Runtime/dp-keys` (on the Docker volume).

## Key conventions

- **No ORM** — `SqliteLeafRepository` hand-writes SQL with `SqliteParameter`. Dates are stored as ISO 8601 strings, DateOnly as `yyyy-MM-dd`. Booleans are stored as `INTEGER` (0/1).
- **Error handling** — Services return `Result<T>` or `Result`. Controllers check `result.IsFailure` and return `this.ToErrorJson(...)` for API or `View()` with model errors for pages.
- **Auth** — Session-based (`HttpContext.Session`) with an optional persistent "remember me" cookie (`leaf_remember_user`). The middleware in `Program.cs` checks session on every non-public path. Role checks via `CurrentUser.IsAdmin` in views.
- **Database path** — Configured via `LeafDatabase:Path` (default `Runtime/leaf.db`). For tests, `IntegrationTestContext` creates a temp-file database.
- **Schema migrations** — `SqliteSchemaInitializer` runs `CREATE TABLE IF NOT EXISTS` for fresh installs and `ALTER TABLE` wrapped in try/catch for existing databases. Old databases are deleted on container rebuild during development.
- **Work item keys** — Format is `{ProjectKey}-{sequence}` (e.g., `LEAF-1`). Generated in `WorkItemService.CreateAsync` via `GetNextWorkItemKeyAsync`. Stored in `WorkItems.Key` column. Lookup by key via `GetWorkItemByKeyAsync`.
- **Project routing** — Projects are routed by their unique `Key` (e.g., `/projects/LEAF`), not by integer ID. `GetProjectByKeyAsync` handles lookups. Work item pages are at `/projects/{key}/{itemKey}`.
- **Labels** — Stored as CSV in `WorkItems.LabelsCsv` column (no join table). Filtered with `LIKE %label%`.
- **Files** — Attachments stored on disk under `wwwroot/uploads/`, served via static files. Metadata in `Attachments` table.
- **Frontend patterns** — Right-side slide-out panel for work item details (not modal). Click-to-edit fields with auto-save on blur/change. Debounced live search (300ms) on backlog. Drag-and-drop on backlog rows and board cards.

## Design

- Color palette: indigo accent (#4f46e5), dark slate sidebar (#1e293b), cool gray backgrounds (#f1f5f9)
- 14px base font size, Manrope typeface, monospace for issue keys
- CSS custom properties in `:root` for spacing, radii, and colors
- Dark sidebar with light text, sticky, 210px wide
- Board: 4 equal-width columns with subtle card shadows
