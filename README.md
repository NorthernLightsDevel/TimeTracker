# Time Tracking System Documentation

This project is a hoby project I build with the help of Codex because I wanted a small and simple waybar time-tracker. The code is built using .NET 9 with Avalonia for UI, most of the Avalonia code is generated and "designed" by Codex, if anyone want to make better UI for it, feel free to create a pull-request.
I have added packaging files for being able to build for Windows, Debian- and Arch- based distros, but since I currently don't have access to a windows computer, I'm not able to ensure the wix installer generation is correct.
Contributions are welcome, I would like to expand the the time-tracking API to add multi-user support and login capabilities in the future. In addition, I have added PostgreSQL migrations already, to enable installing the backend on a server, and possibly making the clients configurable to communicate with an external server. But this is out of scope at the moment.

## Document Map

- [Overview](docs/overview.md) — high-level context and concluding goals for the time tracking application.
- [Requirements](docs/requirements.md) — functional and non-functional expectations for the cross-platform tracker.
- [Design & Technology Stack](docs/design_and_tech_stack.md) — architecture decisions, supporting technologies, and reference links.
- [Development Timeline](docs/development_timeline.md) — week-by-week plan from project setup through post-MVP enhancements.
- [Gap Analysis](docs/gap-analysis.md) — snapshot of the remaining gaps between the blueprint and the implemented system.
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
