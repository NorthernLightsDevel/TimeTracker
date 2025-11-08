#!/usr/bin/env bash
set -euo pipefail

SCRIPT_NAME=$(basename "$0")

notify() {
    local level="$1"
    shift || true
    local message="$*"

    if command -v notify-send >/dev/null 2>&1; then
        notify-send "TimeTracker" "$message" --icon=appointment-soon --urgency="${level}"
    else
        printf '%s: %s\n' "$level" "$message" >&2
    fi
}

error() {
    notify critical "$*"
    exit 1
}

if ! command -v jq >/dev/null 2>&1; then
    error "jq is required for timetracker-waybar."
fi

format_with_jq() {
    local input="$1"
    local formatted

    if formatted=$(printf '%s\n' "$input" | jq -c 2>/dev/null); then
        printf '%s' "$formatted"
    else
        printf '%s' "$input"
    fi
}

resolve_db_path() {
    if [[ -n "${TIMETRACKER_DB_PATH:-}" ]]; then
        printf '%s' "$TIMETRACKER_DB_PATH"
        return
    fi

    local base
    if [[ -n "${XDG_DATA_HOME:-}" ]]; then
        base="$XDG_DATA_HOME"
    else
        base="$HOME/.local/share"
    fi

    printf '%s/TimeTracker/timetracker.db' "$base"
}

run_cli() {
    local raw output
    if ! raw=$(timetracker "$@" 2>&1); then
        output=$(format_with_jq "$raw")
        notify critical "$output"
        return 1
    fi

    output=$(format_with_jq "$raw")

    if [[ -n "$output" ]]; then
        notify normal "$output"
    fi

    return 0
}

prompt_text() {
    local prompt="$1"
    local prefill="$2"

    if command -v rofi >/dev/null 2>&1; then
        printf '%s\n' "$prefill" | rofi -dmenu -p "$prompt"
        return
    fi

    if command -v wofi >/dev/null 2>&1; then
        wofi --dmenu --prompt "$prompt" <<<"$prefill"
        return
    fi

    if command -v zenity >/dev/null 2>&1; then
        zenity --entry --title "$prompt" --text "$prompt" --entry-text "$prefill"
        return
    fi

    read -rp "$prompt: " line || true
    printf '%s' "$line"
}

current_note() {
    local raw
    raw=$(timetracker comment --show 2>/dev/null || true)

    if [[ -n "$raw" ]]; then
        format_with_jq "$raw"
    fi
}

set_note() {
    local current existing new_note
    current=$(current_note || true)
    existing="${current:-}"
    new_note=$(prompt_text "TimeTracker note" "$existing") || exit 0

    if [[ -z "$new_note" ]]; then
        run_cli comment --clear
    else
        run_cli comment "$new_note"
    fi
}

select_project() {
    local raw options selection project_id customer_id
    local cli_args=(projects --json)

    if [[ -n "${TIMETRACKER_CUSTOMER_ID:-}" ]]; then
        cli_args+=("$TIMETRACKER_CUSTOMER_ID")
    fi

    if ! raw=$(timetracker "${cli_args[@]}" 2>/dev/null); then
        notify critical "Unable to fetch projects."
        return 1
    fi

    if [[ -z "$raw" ]]; then
        notify critical "No projects returned."
        return 1
    fi

    mapfile -t options < <(printf '%s' "$raw" | jq -r '.[] | "\(.CustomerName)|\(.CustomerId)|\(.ProjectName)|\(.ProjectId)"')

    if [[ ${#options[@]} -eq 0 ]]; then
        notify critical "No active projects available."
        return 1
    fi

    local display=()
    for entry in "${options[@]}"; do
        IFS='|' read -r customerName customerId projectName projectId <<<"$entry"
        display+=("${customerName} ▸ ${projectName}::${customerId}::${projectId}")
    done

    if command -v rofi >/dev/null 2>&1; then
        selection=$(printf '%s\n' "${display[@]}" | rofi -dmenu -p "Select project") || return 0
    elif command -v wofi >/dev/null 2>&1; then
        selection=$(printf '%s\n' "${display[@]}" | wofi --dmenu --prompt "Select project") || return 0
    elif command -v zenity >/dev/null 2>&1; then
        selection=$(printf '%s\n' "${display[@]}" | zenity --list --title "Select project" --column "Project" --hide-header) || return 0
    else
        printf '%s\n' "${display[@]}" >&2
        read -rp "Enter project UUID: " project_id || return 0
        if [[ -z "${TIMETRACKER_CUSTOMER_ID:-}" ]]; then
            read -rp "Enter customer UUID: " customer_id || return 0
        else
            customer_id="$TIMETRACKER_CUSTOMER_ID"
        fi
        run_cli set "$project_id" --customer "$customer_id"
        return
    fi

    if [[ -z "$selection" ]]; then
        return 0
    fi

    customer_id="$(printf '%s' "$selection" | awk -F'::' '{print $(NF-1)}')"
    project_id="${selection##*::}"
    run_cli set "$project_id" --customer "$customer_id"
}

format_status() {
    local raw payload
    if ! raw=$(timetracker waybar 2>/dev/null); then
        cat <<'JSON'
{"text":"","tooltip":"TimeTracker CLI unavailable","class":"timetracker-error","alt":"error","icon":""}
JSON
        return
    fi

    payload=$(format_with_jq "$raw")

    if [[ -z "$payload" ]]; then
        cat <<'JSON'
{"text":"","tooltip":"Empty response from timetracker","class":"timetracker-error","alt":"error","icon":""}
JSON
        return
    fi

    printf '%s\n' "$payload"
}

command="${1:-status}"
shift || true

case "$command" in
    status)
        format_status
        ;;
    toggle)
        run_cli toggle
        ;;
    pause)
        run_cli pause
        ;;
    resume)
        run_cli resume
        ;;
    stop)
        run_cli stop
        ;;
    prompt-note)
        set_note
        ;;
    project-menu)
        select_project
        ;;
    comment)
        run_cli comment "$@"
        ;;
    set)
        run_cli set "$@"
        ;;
    *)
        printf 'Usage: %s [status|toggle|pause|resume|stop|prompt-note|project-menu|comment|set]\n' "$SCRIPT_NAME" >&2
        exit 1
        ;;
esac
