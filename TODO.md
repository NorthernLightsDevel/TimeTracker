# TODO — Local Waybar MVP

1. [x] Prompt: Review `Design.md` plus the docs/ folder and map the required local-first features (start/pause/stop, project maintenance, rounding, Waybar hooks) onto the existing domain/repository code. Produce a short gap analysis that lists which services, view models, and CLI surfaces are missing.
    - [x] Re-read `Design.md` sections on goals, architecture, and rounding; capture explicit functional requirements in notes.
    - [x] Skim `docs/overview.md`, `docs/requirements.md`, `docs/design_and_tech_stack.md`, and `docs/development_timeline.md` for Waybar, CLI, and reporting expectations not already implemented.
    - [x] Inventory current projects under `src/` to see which repositories, services, and view models exist today (Desktop, Application, Domain, Persistence, Web).
    - [x] Summarize the missing application services (timer orchestration, project maintenance, reporting aggregation) and required UI/CLI surfaces in a concise gap analysis document or issue comment (`docs/gap-analysis.md`).
    - [x] Identify dependencies (e.g., configuration, DI bootstrapping, migrations) that must land before other prompts.

2. [x] Prompt: Bootstrap real persistence for the desktop app. Update `src/TimeTracker.Desktop/Program.cs` to configure DI, open a SQLite database in the user profile, and auto-apply migrations from `TimeTracker.Persistence.SqliteMigrations`. Verify that repository calls work end-to-end with the existing DbContext.
    - [x] Introduce a composition root in `Program.cs` that wires up HostBuilder, logging, configuration, DbContext factory, and repositories.
    - [x] Resolve the SQLite connection path (e.g., `%LocalAppData%`/`$XDG_DATA_HOME`) and ensure directories exist before opening the database.
    - [x] Register EF Core with `UseSqlite` pointing to `TimeTracker.Persistence.SqliteMigrations` and apply migrations on startup within a scope.
    - [x] Inject repositories and services into the Avalonia application lifetime so view models can request them.
    - [x] Create a smoke test or integration harness proving repository CRUD works against the real SQLite file.

3. [x] Prompt: Implement an application-layer timer orchestration service (e.g. `TimerSessionService`) that wraps the repositories, enforces 15-minute midpoint rounding, and supports Start, Pause, Resume, Stop, and Cancel for a single active entry. Expose DTOs the UI and CLI can consume.
    - [x] Design service interfaces and DTOs capturing active session state, historical entries, and command results.
    - [x] Implement quarter-hour midpoint rounding utilities shared between start/stop flows; add unit tests for edge cases.
    - [x] Persist active sessions through `TimeEntryRepository`, handling creation, updates, and cancellation semantics.
    - [x] Add pause/resume logic (pause closes current entry, resume starts new entry linked to same project/notes if desired).
    - [x] Surface read APIs to fetch current status and today’s entries for reuse in UI and CLI layers.

4. [x] Prompt: Replace the dummy desktop UI with real MVVM wiring. Load customers/projects from the repositories, provide project selection + note entry, surface elapsed time bindings, and hook the Start/Pause/Stop buttons to the new timer service. Persist the active session to the database so restarts restore the current state.
    - [x] Refactor `MainViewModel` to accept injected services (timer, repositories, dispatcher) and expose observable properties for project list, selected project, notes, elapsed time, and status.
    - [x] Replace dummy data with live queries on initialization; handle empty-state UX (e.g., prompt to add projects).
    - [x] Implement commands for Start, Pause, Resume, Stop that call the timer service and update UI state.
    - [x] Add a timer tick mechanism (dispatcher timer) that updates elapsed display while a session is running.
    - [x] On app startup, query the timer service for any persisted active session and restore selection/elapsed tracking.
    - [x] Update `MainWindow.axaml` bindings to the new view model properties and ensure validation/command states refresh correctly.

5. [x] Prompt: Add a project management window/dialog (Avalonia) that lets users create, rename, archive/unarchive projects and assign them to customers. Persist changes through the existing repositories and refresh the main view selections.
    - [x] Create a new window/dialog XAML + view model for managing customers/projects with observable collections.
    - [x] Wire commands for create, rename, archive toggle, and customer assignment that interact with repositories.
    - [x] Handle validation errors (duplicate names, missing selection) with user-friendly messages.
    - [x] Notify the main view model when project data changes so dropdowns refresh without restarting the app.
    - [ ] Persist user dialog size/position if helpful for repeated use (optional, deferred).

6. [x] Prompt: Build a lightweight reporting view for “Today” inside the desktop app (or an embedded web view) that lists entries grouped by day with total durations, reusing DTOs returned by the timer service.
    - [x] Extend the timer service or add a reporting service to aggregate entries by local day with totals and durations.
    - [x] Create a view/view model pair that binds to grouped results and supports refresh.
    - [x] Integrate the reporting UI into the main window (tab, flyout, or separate panel) with clear navigation.
    - [x] Ensure data updates live when sessions start/stop or when entries are edited/deleted.
    - [x] Consider export hooks (copy to clipboard/CSV) for future extensibility, marking as stretch if out of scope.

7. [x] Prompt: Ship a command-line entry point (`timetracker status|toggle|set <projectId>`). Reuse the same timer service to print JSON/plain-text status for Waybar polling and to toggle the active session. Ensure the command exits quickly and can run while the desktop UI is open.
    - [x] Add a console project or expand an existing tool to parse command-line verbs and options.
    - [x] Implement `status` output in both plain text and JSON (flag-controlled) for Waybar compatibility.
    - [x] Implement `toggle` to pause/resume the active session; optionally allow specifying project/note overrides.
    - [x] Implement `set <projectId>` (and optional `--note`) to switch the active project before starting/resuming.
    - [x] Ensure commands coordinate with the desktop app via shared database/IPC without locking issues; add retry/backoff as needed.
    - [x] Provide usage help and exit codes suitable for scripting.

8. [x] Prompt: Add a custom waybar module that can interface and show current status for the TimeTracker.Cli application
    - [x] Draft the Waybar custom module config (interval, exec command, click handlers) referencing project IDs or friendly names.
    - [x] Add an input field for writing notes
    - [x] Add a drop down to select project
    - [x] Add play/pause button to start/pause current entry
    - [x] Add stop buttin to stop current entry

9. [x] Prompt: Document the Waybar integration. Add a `docs/waybar.md` that shows the custom module config, sample JSON output, and instructions for wiring click handlers to `timetracker toggle`.
    - [x] Include sample `timetracker status --json` output and explain each field (project, elapsed, running state).
    - [x] Document installation steps: build CLI, place binary in PATH, ensure SQLite database path accessible from Waybar process.
    - [x] Add troubleshooting tips (permissions, stale sessions, command latency) and describe how to test integration quickly.
    - [x] Link back to the new doc from README or relevant design docs for discoverability.

10. [x] Prompt: Extract an API that both TimeTracker.Desktop and TimeTracker.Cli can call locally in order to execute
    - [x] Create the API in the directory `src/TimeTracker.Api`
    - [x] Create a Systemd service definition that exposes the API to localhost only, that can be installed when installing
    - [x] Update `src/TimeTracker.Cli` to communicate through the systemd service.
    - [x] Update `src/TimeTracker.Desktop` to communigate with the API.
    - [x] Create a configuration to allow switching between SQLite and PostgreSQL for the API default to SQLite
    - [x] Update the `packaging/arch/PKGBUILd` package file to install the service and systemd as default enabled.

11. [ ] Prompt: Add automated tests covering rounding edge cases, timer state transitions, CLI commands, and repository interactions (reuse the existing provider harness). Update CI scripts if needed so SQLite tests run on every PR.
    - [ ] Expand test projects with new fixtures for timer service, rounding utility, and CLI handlers.
    - [ ] Reuse `DatabaseHarness` to write integration tests ensuring timer operations persist correctly across Sqlite/Postgres providers.
    - [ ] Add CLI smoke tests using `dotnet test` or `dotnet run` via integration harness to validate command outputs.
    - [ ] Update CI pipeline definitions (GitHub Actions, Azure Pipelines, etc.) so the new test assemblies execute and artifacts (logs) upload on failure.
    - [ ] Document how to run the expanded test suite locally (commands, environment variables) in README or docs.
