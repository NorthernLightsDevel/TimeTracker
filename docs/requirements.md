# Time Tracking System Requirements

The time tracking application must deliver a minimal, always-on-top experience across Windows, Linux (Wayland), and macOS while remaining unobtrusive for tiling window manager users. The requirements below focus on the user-facing capabilities and data expectations for the first release.

## Functional Requirements

- **Cross-platform timer window** with no standard OS chrome, exposing the active project, play/pause control, and elapsed time. The window must stay on top, remain compact, and integrate with status bars (for example, Waybar) instead of opening a separate window in tiling environments.
- **Project selection and management** that lets users pick the active project from the main window and maintain their project list through a dedicated settings surface. Users must be able to add projects, remove them, or toggle them inactive to keep the selection list focused.
- **Time tracking controls** that start, pause, and stop sessions for the selected project. The UI should present the active session duration and optionally totals for the current day or all time. Stopping or pausing must persist the session.
- **Custom rounding rules** for tracked time, including rounding to the nearest configurable increment, discarding short sessions under a chosen threshold, and snapping session boundaries to the nearest increment.
- **Local data storage** of projects and time entries in SQLite to guarantee offline access and privacy. The schema must support future synchronization or export features.
- **Status bar integration** on Linux (e.g., Waybar) so the active project and timer can appear in the bar with minimal duplication of application logic.
- **Consistency across platforms** so that Windows, macOS, and Linux deployments share the same codebase, UX conventions, and lightweight presentation. Secondary configuration and project management experiences can open in separate dialogs as needed.

## Non-Functional Considerations

- Favor a small footprint and low CPU usage while updating the UI once per second.
- Ensure the application respects user data and stores files in OS-appropriate locations (AppData on Windows, `~/.local/share` on Linux, and equivalent paths on macOS).
- Maintain flexibility for later headless integrations or automation via CLI entry points.
