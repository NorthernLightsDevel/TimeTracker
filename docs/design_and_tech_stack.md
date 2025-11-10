# Time Tracking System Design & Technology Stack

This repository is organized so every host (desktop, CLI, Waybar, API) reuses the same services and persistence rules. The notes below capture the current stack and how the pieces fit together.

## Stack Snapshot

- **Runtime**: .NET 9 with implicit usings, nullable disabled, and four-space indentation.
- **UI**: Avalonia provides the cross-platform desktop shell with an always-on-top mini controller.
- **Backend**: ASP.NET Core minimal API (`src/TimeTracker.Api`) exposes timer/project/customer endpoints over HTTP.
- **Data**: Entity Framework Core talks to SQLite by default, with PostgreSQL supported through provider-specific migrations.
- **Testing & tooling**: xUnit + coverlet + Testcontainers validate application services against SQLite/PostgreSQL; `scripts/manage-migrations.sh` keeps every provider in sync.

## Architecture Highlights

- `TimeTracker.Domain` contains POCOs and DTOs only.
- `TimeTracker.Application` holds the timer orchestration service (15-minute midpoint rounding), reporting queries, and repository abstractions.
- `TimeTracker.Persistence` implements the repositories and `TimeTrackerDbContext`; provider-specific migrations live in the `SqliteMigrations` and `PgSqlMigrations` projects.
- `TimeTracker.Infrastructure` centralizes host wiring (paths, provider selection, DI extensions).
- `TimeTracker.Api` is the canonical host: it resolves the data directory, applies migrations on startup, and serves `/api/*`.
- `TimeTracker.ApiClient` offers typed clients that match the repository/service interfaces so other hosts never deal with raw HTTP.
- `TimeTracker.Desktop` and `TimeTracker.Cli` both depend on the API client, keeping all state changes funneled through the same API. The CLI powers Waybar and other automation hooks.

## Operational Notes

- Run the API (systemd service or `dotnet run --project src/TimeTracker.Api`) before launching the desktop app or CLI so migrations apply and a single process owns the database.
- Desktop and CLI binaries are self-contained per platform; publish scripts live under `packaging/`.
- The Waybar helper (`scripts/waybar-timetracker.sh`) shells out to the CLI, which in turn talks to the API, so no direct SQLite access or IPC hacks are required.
- Use `scripts/manage-migrations.sh add <Name>` when schema changes need to touch SQLite and PostgreSQL together; review the generated files before committing.
