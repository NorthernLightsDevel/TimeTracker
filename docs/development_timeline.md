# Time Tracking System Development Timeline

The phased schedule below outlines a single-developer roadmap for delivering the cross-platform time tracking application.

## Week 1-2 — Planning & Setup

- Revisit and refine requirements; sketch the timer and project management interfaces and decide how rounding settings appear.
- Initialize the .NET solution with an Avalonia app and confirm a “Hello World” run on Windows, Linux (Wayland/XWayland), and macOS.
- Define the architecture (core library, Avalonia UI, CLI/integration utilities) and draft the SQLite schema or EF Core model.
- Select supporting libraries, including SQLite data access and any MVVM frameworks, verifying cross-platform compatibility.

## Week 3-4 — Core Functionality

- Implement SQLite initialization, ensuring the database file lives in user-specific locations on every OS and tables are created when missing.
- Add project management logic for adding, deactivating, and listing projects with basic validation.
- Build the timer service to start, pause, and stop sessions, applying rounding and writing time entries back to the database.
- Model rounding configuration and implement utility functions or unit tests to verify rounding behavior.
- Deliver an initial CLI status command (e.g., `--status`) to exercise the core logic outside the UI and support future integrations.
- Run ad-hoc console tests to confirm sessions persist as expected.

## Week 5-6 — User Interface

- Compose the Avalonia main window with project selector, timer display, and play/pause control inside a draggable, chrome-less, always-on-top window.
- Use MVVM bindings and a dispatcher timer to keep UI updates responsive and isolated from business logic.
- Provide a project management dialog for maintaining the project list and keeping the main selector current.
- Surface rounding settings in the UI (global or per-project) and persist them for future sessions.
- Polish visuals (icons, color cues) and verify readability across platforms.
- Test the UI on Windows, Linux/Hyprland, and macOS to confirm layout, floating behavior, and database interactions.

## Week 7-8 — Integration & Advanced Features

- Extend the CLI to support status reporting (plain text or JSON) suitable for Waybar polling.
- Configure Waybar with a custom module that calls the status command periodically and invokes a toggle command when clicked.
- Implement toggle handling (via IPC, database signaling, or simple scripts) while guarding against race conditions.
- Optionally add cross-platform tray support for desktop controls beyond Waybar.
- Offer simple data inspection or export (e.g., log view or CSV output) to validate captured sessions.
- Address rounding edge cases, UI responsiveness, mid-session project switches, and app shutdown scenarios.

## Week 9-10 — Testing, Polish, and Release Prep

- Execute thorough platform testing:
  - **Windows**: Confirm always-on-top behavior, chrome-less aesthetics, and distribution without administrator privileges.
  - **Linux (Wayland/Hyprland)**: Validate Waybar integration, floating rules, and compatibility with other desktops (GNOME, KDE).
  - **macOS**: Ensure window behavior feels native, keyboard shortcuts (e.g., Command+Q) function, and menu expectations are met.
  - **Edge cases**: Exercise rounding combinations, project list mutations, and consecutive sessions.
- Monitor performance (timer tick cost, database writes) and tune as necessary.
- Refine UI/UX details (layout adjustments, tooltips, keyboard access) with awareness of platform constraints.
- Document usage, rounding rules, and status bar setup in user-facing help or README content.
- Prepare self-contained packages for Windows (MSIX or standalone EXE), macOS (`.app` bundle), and Linux (AppImage, zip, or Flatpak) with SQLite dependencies included.
- Reserve buffer time to finish outstanding items so an MVP is ready by the end of Week 10.

## Week 11+ — Post-MVP Enhancements (Optional)

- Investigate secure synchronization or export APIs for external services.
- Add richer reports or editing tools for historical entries.
- Expand status bar support (Waybar tooltips, Polybar scripts, Windows overlays) as needed.
- Introduce advanced preferences such as auto-start, tray minimization, or auto-update flows.
- Collect feedback, iterate, and plan future releases.
