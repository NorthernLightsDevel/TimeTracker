# Time Tracking System Documentation

This is a hobby project I built with the help of Codex because I wanted a small Waybar-friendly time tracker. The code targets .NET 9 with Avalonia for UI; most of that UI was generated or tweaked by Codex, so feel free to submit PRs if you want to polish it further. Packaging exists for Windows plus Debian- and Arch-based distros, though the WiX installer still needs validation on real Windows hardware. PostgreSQL migrations already live in the repo so the API can eventually back a multi-user deployment, but the current focus stays on local-first tracking.

## Document Map

- [Overview](docs/overview.md) — high-level context and project goals.
- [Requirements](docs/requirements.md) — functional and non-functional expectations for the local-first workflow.
- [Design & Technology Stack](docs/design_and_tech_stack.md) — condensed architecture notes and tech rationale.
- [Waybar Integration](docs/waybar.md) — configure the custom Waybar module and CLI hooks.

Each document can be maintained independently while keeping the shared vision consistent across platforms and integration touchpoints.

## Project Layout

- `src/TimeTracker.Domain` — aggregates, value objects, and DTOs with no infrastructure dependencies.
- `src/TimeTracker.Application` — timer/session orchestration, reporting services, and repository abstractions.
- `src/TimeTracker.Persistence` — EF Core `TimeTrackerDbContext` plus repository implementations shared by every host.
- `src/TimeTracker.Persistence.SqliteMigrations` & `src/TimeTracker.Persistence.PgSqlMigrations` — provider-specific migrations driven through `scripts/manage-migrations.sh`.
- `src/TimeTracker.Infrastructure` — host/DI helpers that choose the correct provider, locate the database path, and apply migrations on startup.
- `src/TimeTracker.ApiClient` — typed HTTP client that surfaces the same timer/project/customer interfaces over REST.
- `src/TimeTracker.Api` — minimal ASP.NET Core API that owns the database connection, exposes `/api/*` endpoints, and can run as a systemd service.
- `src/TimeTracker.Desktop` — Avalonia UI that calls the API client for all timer and project actions.
- `src/TimeTracker.Cli` — command-line interface (Waybar/automation friendly) that reuses the API client to show status and toggle sessions.
- `tests/TimeTracker.Application.Tests` — unit/integration coverage for rounding rules, repositories, CLI flows, and service behavior.
- `docs/`, `tools/`, `scripts/`, and `packaging/` — documentation, migration helpers, automation scripts, and OS-specific installers.

Desktop and CLI experiences communicate exclusively through the API, keeping state consistent even when one host is closed while the other is running.

## EF Core Migrations

The solution keeps provider-specific migration projects under `src/TimeTracker.Persistence.<Provider>Migrations`. To add a migration:

1. Install the EF Core CLI tool if you have not already: `dotnet tool install --global dotnet-ef`.
2. Restore dependencies from the repository root: `dotnet restore TimeTracker.sln`.
3. Run `dotnet ef migrations add <MigrationName>` targeting the provider project you want to update. Point `--startup-project` at the matching tool under `tools/` so Docker containers are provisioned automatically for server databases. For example:

   ```bash
   dotnet ef migrations add InitialCreate \
     --context TimeTrackerDbContext \
     --project src/TimeTracker.Persistence.SqliteMigrations \
     --startup-project tools/TimeTracker.Tools.SqliteMigrations \
     --output-dir Migrations
   ```

   Replace `TimeTrackerDbContext` with the context you are updating and point `--project` at the desired provider (e.g., `Sqlite`, `PgSql`). The startup projects are:

   - `tools/TimeTracker.Tools.SqliteMigrations` (file-backed database, no container required)
   - `tools/TimeTracker.Tools.PgSqlMigrations` (provisions PostgreSQL in Docker via Testcontainers)

Run `dotnet ef database update` with the same project arguments to apply the new migration locally when needed.

## Migration helper script

Use `scripts/manage-migrations.sh` to apply the same `dotnet ef` action across every provider simultaneously:

```bash
# remove the last migration from every provider
bash scripts/manage-migrations.sh remove

# add a new migration named InitialCreate to every provider
bash scripts/manage-migrations.sh add InitialCreate
```

The script performs a `dotnet restore` if required and routes commands through the provider startup tools under `tools/`, ensuring Testcontainers spin up the correct databases automatically.

## CSV Reporting

- **CLI**: run `timetracker report w` for the last seven days or `timetracker report m` for the last thirty. The command streams CSV to `stdout` (header + day/customer/project totals with per-entry notes), so redirect it wherever you need: `timetracker report w > ~/reports/week.csv`.
- **Desktop**: open the Daily Report tab and use the new “Export week CSV” / “Export month CSV” buttons. You will be prompted for a file location and the app writes the same CSV layout used by the CLI.

## License

This project is distributed under the [MIT License](LICENSE).
