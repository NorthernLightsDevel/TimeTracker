# Repository Guidelines

This TimeTracker automation workspace is tuned to help agents conserve tokens while executing the action items enumerated in `TODO.md`, so lean on the shared context here before expanding prompts.

## Project Structure & Module Organization
The solution file `TimeTracker.sln` orchestrates the codebase. Domain services live in `src/TimeTracker.Application`, persistence infrastructure in `src/TimeTracker.Persistence`, and UI entry points in `src/TimeTracker.Desktop` (Avalonia client) and `src/TimeTracker.Web` (minimal API shell). Provider-specific migrations sit in `src/TimeTracker.Persistence.SqliteMigrations` and `src/TimeTracker.Persistence.PgSqlMigrations`, with helper startup hosts under `tools/`. Automated docs stay in `docs/`, while solution-wide tests are collected in `tests/TimeTracker.Application.Tests`. Use `scripts/manage-migrations.sh` when a change must touch every provider.

## Build, Test, and Development Commands
Restore and compile with `dotnet restore TimeTracker.sln` followed by `dotnet build TimeTracker.sln` (`-c Release` for production parity). Launch the desktop client via `dotnet run --project src/TimeTracker.Desktop`, which also applies pending EF Core migrations. Spin up the API stub with `dotnet run --project src/TimeTracker.Web`. For schema work, run `bash scripts/manage-migrations.sh add <Name>` to scaffold all provider migrations, or `remove` to roll them back in sync, and do not invoke EF Core migrations directly; always review the generated migration for accuracy or roll back, fix the data model, and rerun the script until it produces the correct result.

## Coding Style & Naming Conventions
The projects target .NET 9 with implicit usings, nullable reference types disabled, and file-scoped namespaces. Keep indentation at four spaces, prefer `var` when the right-hand side is obvious, and align naming with the existing pattern: PascalCase for types/methods, camelCase for locals and parameters, with `_camelCase` reserved for private fields. Maintain project and namespace prefixes as `TimeTracker.*`, and group shared abstractions in `Application` before introducing UI or persistence dependencies. Run `dotnet format TimeTracker.sln` prior to committing to enforce analyzers. Leave nullable reference types alone—do not add `#nullable` directives or sprinkle `?` annotations when touching files unless the user explicitly requests it, and preserve any existing `#nullable disable` headers.

## Testing Guidelines
Tests use xUnit plus coverlet and Testcontainers. Execute `dotnet test TimeTracker.sln --collect:"XPlat Code Coverage"` to verify regressions and emit coverage reports under `TestResults/`. Ensure Docker Desktop (or your container runtime) is available so PostgreSQL-backed fixtures succeed. Follow the `MethodUnderTest_Scenario_Outcome` naming convention seen in `TimerSessionServiceTests`, keep async flows awaited, and place reusable fixtures in `tests/.../Infrastructure`.

## Commit & Pull Request Guidelines
History currently favors concise, Title Case subjects (e.g., `Initial commit`); continue writing imperative, single-line summaries kept under ~70 characters, with optional detail in the body. Reference related issues in the PR description, outline behavioral changes, and attach screenshots or logs for UI or migration-impacting work. Note database schema adjustments explicitly and call out any coordination required for seeded environments.
