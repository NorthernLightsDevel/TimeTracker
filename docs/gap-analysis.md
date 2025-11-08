# Local Waybar MVP — Gap Analysis

## Scope

Assess the current repository against the MVP expectations in `Design.md`, `docs/overview.md`, `docs/requirements.md`, and `docs/development_timeline.md`, with a focus on local-first tracking, project maintenance, rounding, reporting, and Waybar/CLI integration.

## What Exists Today

- **Domain layer** (`src/TimeTracker.Domain`)
  - Rich `Customer`, `Project`, and `TimeEntry` entities with validation, audit fields, and relationships.
  - DTO records for CRUD operations.
- **Persistence layer**
  - `TimeTrackerDbContext` plus fluent configurations for all entities.
  - Provider-specific projects for SQLite/Postgres migrations (no migrations added yet).
- **Application layer**
  - Repository abstractions (`ICustomerRepository`, `IProjectRepository`, `ITimeEntryRepository`) with EF Core-backed implementations.
- **Desktop shell** (`src/TimeTracker.Desktop`)
  - Avalonia bootstrapping, dummy `MainViewModel`, simple window binding to in-memory data.
  - No dependency injection or persistence wiring.
- **Web project**
  - Minimal `Hello World` endpoint; no timer or reporting APIs.
- **Tooling/tests**
  - Database harness using SQLite/Postgres (Testcontainers) and repository CRUD tests.
  - Helper script for running migrations across providers.

## Gaps vs. MVP Requirements

| Area | Missing Pieces |
| --- | --- |
| Timer orchestration | No service coordinating start/pause/resume/stop, rounding, or active session persistence. No abstraction for exposing status to UI/CLI. |
| Rounding policy | No implementation of 15-minute midpoint rounding or guardrails for zero-length entries. No configuration surface. |
| Persistence bootstrap | Desktop app does not configure `HostBuilder`, DI, or DbContext. SQLite path handling, migration application, and repository injection are absent. |
| UI (main window) | Dummy VM only; no project/customer loading, selection, note entry, elapsed timer, or button state management. No restoration of active session on app restart. |
| Project maintenance UI | No dialogs/windows for adding, renaming, archiving projects or managing customers. |
| Reporting | No aggregation service or UI to show “Today” totals, grouped entries, or historical data. |
| CLI / Waybar integration | No command-line entry point, status/toggle commands, or JSON/plain-text output tailored for Waybar custom modules. No documentation on integration. |
| Rounding & timer tests | Only repository CRUD tests exist; no coverage for rounding edge cases, timer transitions, or CLI behaviors. |
| Migrations | Migration projects are empty; schema has not been materialized. CI/scripts do not ensure SQLite migrations run. |
| Configuration | No central configuration model for rounding increment, default project selection, or CLI/Waybar settings. |
| Synchronization hooks | PendingSync fields exist but no service updates them; acceptable for MVP but should be acknowledged. |

## Dependencies and Ordering Notes

1. **Composition root** (DI + SQLite path + migrations) must land before wiring timer services or real view models (Prompts 2 & 4). Amplifies reliability of later steps.
2. **Timer orchestration** underpins the UI, CLI, and reporting flows (Prompts 3, 4, 6, 7). Build it once and share.
3. **Rounding utilities** should ship alongside the timer service so both UI and CLI display consistent durations.
4. **Project/customer CRUD** is already available via repositories, enabling project management UI after DI is in place.
5. **Reporting aggregation** can extend the timer service or a dedicated query service after persistence wiring exists.
6. **CLI project** requires the same DI/persistence setup. Consider a shared library for registering services across Desktop/CLI/Web.
7. **Testing** should be expanded gradually: start with rounding/timer unit tests, then add integration tests for CLI once it exists.

Delivering these gaps in the order laid out in `TODO.md` will produce a cohesive, local-first experience ready for Waybar polling and future backend sync work.
