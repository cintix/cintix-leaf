# Leaf

Leaf is a modular Scrum project management platform built with .NET 8, SQLite, MVC, and vanilla JavaScript.

## Tech

- .NET 8 (ASP.NET Core MVC)
- SQLite (`Microsoft.Data.Sqlite`)
- Vanilla JavaScript + modern CSS
- xUnit tests
- Docker + Docker Compose (podman-compose compatible)

## Features

- Project workspaces with backlog, Kanban board, and sprint management
- Work items (Epic, Story, Task, Bug, Subtask) with per-project keys (`LEAF-1`, `LEAF-2`)
- Drag-and-drop backlog ranking and board column movement
- Sprint lifecycle (plan, start, close) with burndown, velocity, and throughput reports
- Right-side detail panel with click-to-edit and auto-save
- Comments, file attachments, and activity history per work item
- Live search with debounced filtering
- Role-based views (admin vs. member)

## Run locally

```bash
dotnet restore Leaf.sln
dotnet build Leaf.sln
dotnet run --project Leaf.Web/Leaf.Web.csproj
```

Leaf listens on `http://0.0.0.0:8080` by default.

## Demo credentials

- Admin: `admin@leaf.local` / `leafadmin`
- Demo users: `emma@leaf.local` / `leafdemo`, `niko@leaf.local` / `leafdemo`

## Test

```bash
dotnet test Leaf.sln
```

## Docker

```bash
docker compose up --build
# or
podman-compose up --build
```

The SQLite database and Data Protection keys are persisted to a Docker volume mounted at `/app/Runtime`.
