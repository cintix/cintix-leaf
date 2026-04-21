# Leaf

Leaf is a modular Scrum project management platform built with .NET 8, SQLite, MVC, and vanilla JavaScript.

## Tech

- .NET 8 (ASP.NET Core MVC)
- SQLite (`Microsoft.Data.Sqlite`)
- Vanilla JavaScript + modern CSS
- xUnit tests
- Docker + Docker Compose

## Architecture

```text
App/
  Core/
  Features/
  Runtime/
  Infrastructure/
  UI/
  Tests/
```

Dependency direction:

```text
UI / Runtime / Infrastructure -> Features -> Core
```

## Run locally

```bash
dotnet restore Leaf.sln
dotnet build Leaf.sln
dotnet run --project App/UI/Leaf.Web/Leaf.Web.csproj
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
```

The SQLite database is persisted to a Docker volume mounted at `/app/Runtime`.
