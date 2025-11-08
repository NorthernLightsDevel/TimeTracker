# Time Tracking System Design & Technology Stack

This document summarizes the architectural approach and supporting technologies required to satisfy the cross-platform, always-on-top time tracking experience.

## Core Technology Choices

- **Primary language and runtime**: C# on modern .NET (7 or 8) enables shared business logic across Windows, macOS, and Linux without sacrificing performance or tooling.[1]
- **UI framework**: Avalonia UI provides a single XAML-based desktop interface, supports custom chrome-less windows, and offers a path to Wayland environments such as Hyprland.[1][2][3]
- **Data layer**: SQLite delivers lightweight, file-based persistence with excellent cross-platform support via `Microsoft.Data.Sqlite`, keeping the door open for ORMs such as Entity Framework Core or micro-ORM alternatives.[4][5]
- **Time tracking services**: Encapsulate timer control, rounding behavior, and persistence updates inside reusable C# services so the desktop UI, CLI tools, and status bar integrations operate on the same logic.
- **Status bar integration**: Provide CLI entry points (e.g., `timetracker --status`, `timetracker --toggle`) that Waybar or similar bars can poll. Emit structured output (plain text or JSON) to avoid per-platform rewrites and allow scripted automation.[6]
- **Tooling**: Use Visual Studio or VS Code with the Avalonia tooling extensions for development, and target self-contained .NET deployments per operating system to simplify distribution.

## Architectural Notes

- Separate the solution into a core library, the Avalonia desktop shell, and optional integration utilities to keep UI-free logic testable and shareable.
- Persist rounding configuration either globally or per project within the database so future features (CLI, Waybar module) can read the same settings.
- Plan for IPC or database-driven signaling when toggling from the status bar, minimizing concurrency issues while keeping implementation simple for a single developer.
- Package each platform build with the required native SQLite libraries and ensure the app stores its database in user-writable paths.

## References

1. Avalonia cross-platform GUI support – works on Windows, macOS, and Linux. https://www.reddit.com/r/learncsharp/comments/12qovcq/is_avalonia_the_best_solution_for_cross_platform/
2. Avalonia borderless window (no title bar) via window properties. https://stackoverflow.com/questions/65748375/avaloniaui-how-to-change-the-style-of-the-window-borderless-toolbox-etc
3. Discussion of .NET MAUI desktop limitations. https://www.reddit.com/r/learncsharp/comments/12qovcq/is_avalonia_the_best_solution_for_cross_platform/
4. .NET 8 WinForms Migration – System.Data.SQLite vs Microsoft.Data.Sqlite. https://learn.microsoft.com/en-us/answers/questions/2284458/net-8-winforms-migration-system-data-sqlite-vs-mic
5. Cross-platform SQLite considerations. https://stackoverflow.com/questions/13016578/cross-platform-sqlite
6. Waybar custom module reference. https://man.archlinux.org/man/extra/waybar/waybar-custom.5.en
