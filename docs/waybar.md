# Waybar Integration

This guide shows how to surface the current TimeTracker status inside Waybar and wire clicks to the CLI commands shipped with the project.

## Prerequisites

1. Install the CLI and desktop application so the `timetracker` binary is on your `$PATH` (see `packaging/arch/PKGBUILD` for Arch packages or run `dotnet publish src/TimeTracker.Cli` manually).
2. Launch the desktop client once so the SQLite database is created at `~/.local/share/TimeTracker/timetracker.db`.
3. Ensure the Waybar process can read and write that database; if you run Waybar under a different user, adjust permissions accordingly.

## Helper Script (`timetracker-waybar`)

The repository ships `scripts/waybar-timetracker.sh`, a small wrapper that exposes the CLI to Waybar. Install it somewhere on your `$PATH`:

```bash
install -Dm755 scripts/waybar-timetracker.sh ~/.local/bin/timetracker-waybar
```

The script understands:

- `status` (default) — emits Waybar-friendly JSON.
- `toggle`, `pause`, `resume`, `stop` — forward to the matching CLI verbs.
- `prompt-note` — pops up a note entry dialog via `rofi`, `wofi`, or `zenity`, then calls `timetracker comment`.
- `project-menu` — queries the SQLite database for active projects, shows a drop-down picker, then runs `timetracker set <projectId>`.

The helper now relies exclusively on the .NET CLI: `timetracker waybar` produces the Waybar JSON payload and `timetracker comment --show` returns the current note when prompting for updates, so no Python runtime is required.

Set `TIMETRACKER_DB_PATH` if your database is not at the default `~/.local/share/TimeTracker/timetracker.db`.

## Waybar Module Example

Add a custom module to your `~/.config/waybar/config` file:

```json
"custom/timetracker": {
    "interval": 5,
    "return-type": "json",
    "exec": "timetracker-waybar status",
    "exec-if": "command -v timetracker-waybar >/dev/null",
    "format": "{icon} {text}",
    "on-click": "timetracker-waybar toggle",
    "on-click-right": "timetracker-waybar stop",
    "on-click-middle": "timetracker-waybar project-menu",
    "on-scroll-up": "timetracker-waybar prompt-note",
    "on-scroll-down": "timetracker-waybar comment --clear"
}
```

Then reference it in the top-level `modules-right` (or preferred position):

```json
"modules-right": [
    "pulseaudio",
    "network",
    "custom/timetracker"
]
```

Waybar only exposes `{text}`, `{icon}`, `{alt}`, `{tooltip}`, and `{percentage}` placeholders when `return-type` is `json`, so the module shown above keeps `format` to `{icon} {text}` and lets the helper embed the project and customer names into `text`. Drop `tools/waybar/timetracker.css` next to your Waybar `style.css` (or import it via `@import "timetracker.css";`) to pick up the default padding, font, and status colors for the module.

The `format` tokens map to values from the JSON payload described below. Adjust icons and click handlers to fit your workflow—for example, replace `prompt-note` with a custom script or remap the scroll events.

## Sample JSON Output

Running `timetracker waybar` returns a concise payload for status bars:

```json
{
"text": "00:17 Waybar Integration · Internal Tooling",
"tooltip": "Status: Running\nProject: Waybar Integration\nElapsed: 00:17\nNotes: Documenting the setup\nLast entry: Waybar Integration 01:15",
"alt": "running",
"class": "timetracker-running",
"icon": "",
"status": "Running",
"project": "Waybar Integration",
"customer": "Internal Tooling",
"elapsed": "00:17",
"notes": "Documenting the setup"
}
```

Key fields:

- `text` — value shown in the bar (the module example renders `{icon} {elapsed} {project}`).
- `tooltip` — multi-line summary displayed on hover.
- `status`, `project`, `elapsed`, `notes` — raw values you can reuse in alternate formats or scripts.
- `customer` — displayed alongside the project in the default `text` value so you can immediately spot the client you are tracking time against.
- `alt`, `class`, `icon` — convenience fields for styling and glyph selection (icons assume a Nerd Font).

If you need the full timer snapshot (active entry metadata, history, etc.), continue to call `timetracker status --json --pretty`.

## Installation Steps Recap

1. `dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true src/TimeTracker.Cli`
2. Copy the published `TimeTracker.Cli` binary to a directory in your `$PATH` (for example `/usr/local/bin/timetracker`). The output bundles the .NET runtime, so no additional framework install is required.
3. Install the helper via `install -Dm755 scripts/waybar-timetracker.sh ~/.local/bin/timetracker-waybar` (the Arch package installs this as `timetracker-waybar`).
4. Repeat for the desktop client if desired: `dotnet publish ... src/TimeTracker.Desktop` and place it somewhere Waybar can invoke it.
5. Reload Waybar: `pkill -SIGUSR2 waybar` or restart the session.

## Troubleshooting

- **Permission denied**: Waybar runs as your user, but if the SQLite file lives on a different volume or has restrictive permissions, adjust ownership with `chown $USER:$USER ~/.local/share/TimeTracker -R`.
- **Stale output**: Increase the module `interval` or call `timetracker-waybar status` manually to confirm the helper responds quickly. The CLI usually completes in under 50 ms once the database is warm.
- **Timer not toggling**: Verify the CLI binary is executable (`chmod +x /usr/local/bin/timetracker`) and that another instance is not holding the database lock. The CLI retries transient SQLite locks up to five times.
- **Missing icons**: Ensure the selected glyphs exist in your configured font (e.g., Nerd Fonts). Replace `format-icons` with plain text if needed.
- **Project picker empty**: Confirm `sqlite3` is installed and that at least one active project exists. Archived projects are filtered out by default.

## Quick Validation

Run these commands to sanity check the integration:

```bash
# Ensure the helper returns JSON
TIMETRACKER_STATUS=$(timetracker-waybar status) && echo "$TIMETRACKER_STATUS"

# Toggle the active session directly; Waybar should update within the next interval
timetracker-waybar toggle
```

Once the module shows the correct status and responds to clicks, your Waybar integration is ready.
