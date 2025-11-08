# Time Tracking System Documentation

The specification derived from *Time Tracking System — Requirements Review, Tech Stack, and Development Timeline.pdf* is now split into focused documents under `docs/` for easier navigation.

## Document Map

- [Overview](docs/overview.md) — high-level context and concluding goals for the time tracking application.
- [Requirements](docs/requirements.md) — functional and non-functional expectations for the cross-platform tracker.
- [Design & Technology Stack](docs/design_and_tech_stack.md) — architecture decisions, supporting technologies, and reference links.
- [Development Timeline](docs/development_timeline.md) — week-by-week plan from project setup through post-MVP enhancements.
- [Waybar Integration](docs/waybar.md) — configure the custom Waybar module and CLI hooks.

Each document can be maintained independently while keeping the shared vision consistent across platforms and integration touchpoints.

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
