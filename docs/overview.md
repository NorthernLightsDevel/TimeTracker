# Time Tracking System Overview

The project aims to deliver a cross-platform time tracking application that runs on Windows, Linux (Wayland), and macOS with a minimal always-on-top interface that can also surface status information inside tiling window manager bars such as Waybar.

## Summary

The application combines a lightweight borderless timer window, local SQLite storage for projects and time entries, configurable rounding rules, and optional status bar integrations. A modular architecture keeps timer logic, data access, and presentation layers independent so that desktop and command-line experiences share the same behavior.

## Conclusion

By leveraging C#, Avalonia, and SQLite, the team can maintain a single codebase that satisfies the UI, storage, and integration requirements. The staged timeline targets an MVP in roughly ten weeks, yielding a functional tracker with project management, rounding logic, and Waybar support, while leaving room for synchronization, enhanced reporting, and additional platform integrations in subsequent iterations.
