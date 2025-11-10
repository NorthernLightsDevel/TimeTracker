# Time Tracking System Requirements

The goal is a small, local-first tracker that feels native on Windows, macOS, and Linux while staying friendly to tiling window managers and headless automation. The API service, desktop client, CLI, and Waybar helper all sit on the same stack so behavior stays consistent regardless of entry point.

## Functional Requirements

- **Cross-platform timer window**: Avalonia provides an always-on-top mini controller that shows the selected project, elapsed time, note field, and Start/Pause/Resume/Stop actions.
- **Project and customer management**: Users can create, rename, archive, and reassign projects/customers without leaving the desktop app; archived items drop out of selectors but stay queryable for history.
- **Timer orchestration**: Start/Resume immediately creates an entry, Pause/Stop close it, Cancel removes it, and every boundary snaps to 15-minute midpoint rounding so totals are deterministic.
- **Local API**: `TimeTracker.Api` is the single writer to SQLite/PostgreSQL, applies migrations on startup, and exposes timer/project/customer/reporting endpoints for every other client.
- **CLI + Waybar integration**: `timetracker` mirrors the desktop commands (`status`, `toggle`, `set`, `comment`, `report`) and emits JSON/plain text for Waybar or scripts. The Waybar helper simply shells out to the CLI.
- **Reporting/export**: The “Today” view and CLI `report` command group entries by day, show totals, and stream CSV for quick exports.

## Non-Functional Requirements

- **Offline-first storage**: Data lives under OS-appropriate user directories (`AppData`, `~/.local/share`, etc.) with no cloud dependency; a future PostgreSQL deployment can be selected via configuration.
- **Lightweight footprint**: UI updates once per second, background timers avoid busy waits, and the API host keeps concurrency simple by running in-process in most desktop deployments.
- **Testability**: Application services must run under xUnit with SQLite/PostgreSQL harnesses so rounding and repository behavior stay regression-free.
- **Distribution**: Self-contained .NET publishes are required for each platform, plus Arch/Debian packages and a WiX installer once validated.
- **Extensibility**: Keep the domain model sync-ready (`ServerId`, `PendingSync`, etc.) even if cloud sync is out of scope so future multi-user features do not force a schema rewrite.
