# Waybar Integration

The CLI already exposes everything Waybar needs, so the desktop app does not have to stay open to keep the bar updated. This guide shows how to connect Waybar to the CLI helper script and interpret the output.

## Prerequisites

1. **Run the local API**: start `TimeTracker.Api` (`dotnet run --project src/TimeTracker.Api` or enable the packaged systemd unit). It applies migrations and keeps the SQLite/PostgreSQL database ready for every client.
2. **Install the CLI**: publish `src/TimeTracker.Cli` (or install a package) so a `timetracker` binary exists on your `$PATH`.
3. **Optional desktop UI**: launch the Avalonia client if you want a visual timer, but Waybar only requires the API + CLI.

## Helper Script (`timetracker-waybar`)

`scripts/waybar-timetracker.sh` is a thin shell wrapper that speaks Waybar’s protocol and simply shells out to `timetracker` commands. Install it into your local bin directory:

```bash
install -Dm755 scripts/waybar-timetracker.sh ~/.local/bin/timetracker-waybar
```

Supported verbs:

- `status` (default) — prints Waybar-friendly JSON by running `timetracker waybar`.
- `toggle`, `pause`, `resume`, `stop`, `comment`, `set` — forward arguments to the CLI. `toggle` also prompts for a note (reuse of `prompt-note`) whenever a new session starts and no comment exists yet, so the note field never stays blank by accident.
- `switch` — stops the active session if necessary, opens the project picker, switches to the selected project, and then prompts for a note when the new timer starts with an empty comment.
- `prompt-note` — prompts via `rofi/wofi/zenity` and forwards the result to `timetracker comment`.
- `project-menu` — calls `timetracker projects --json`, displays a picker, and executes `timetracker set <projectId>`.

Set `TIMETRACKER_CUSTOMER_ID` if you want the project picker scoped to a single customer. No direct database access is required; everything flows through the API-hosted CLI.

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

- `text` — what Waybar renders (the example module formats `{icon} {text}`).
- `tooltip` — multi-line summary on hover.
- `status`, `project`, `customer`, `elapsed`, `notes` — raw values for custom scripts.
- `alt`, `class`, `icon` — convenience hints for styling Nerd Font glyphs.

Use `timetracker status --json` if you need the full timer snapshot with history.

## Installation Steps Recap

1. Publish the API and CLI (`dotnet publish -c Release src/TimeTracker.Api`, same for `TimeTracker.Cli`) or install from the packages under `packaging/`.
2. Ensure `timetracker` resolves on your `$PATH`.
3. Install the helper script: `install -Dm755 scripts/waybar-timetracker.sh ~/.local/bin/timetracker-waybar`.
4. Add the module config shown above to your Waybar `config` and CSS (optional: import `tools/waybar/timetracker.css`).
5. Reload Waybar: `pkill -SIGUSR2 waybar`.

## Troubleshooting

- **API not running**: `timetracker-waybar status` prints an error blob if the API is offline. Start `TimeTracker.Api` and retry.
- **Slow updates**: Reduce the module `interval` or run the helper manually to confirm CLI latency (<50 ms once warm).
- **Project picker empty**: Ensure at least one active project exists; archived projects are filtered and the CLI mirrors that behavior.
- **Missing icons**: Swap the Nerd Font glyphs in the module definition for plain text if your font lacks them.

## Quick Validation

Run these commands to sanity check the integration:

```bash
# Ensure the helper returns JSON
TIMETRACKER_STATUS=$(timetracker-waybar status) && echo "$TIMETRACKER_STATUS"

# Toggle the active session directly; Waybar should update within the next interval
timetracker-waybar toggle
```

Once the module shows the correct status and responds to clicks, your Waybar integration is ready.
