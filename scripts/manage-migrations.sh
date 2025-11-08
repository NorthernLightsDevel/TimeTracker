#!/usr/bin/env bash

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")"/.. && pwd)"

usage() {
  cat <<'HELP'
Usage: manage-migrations.sh <add|remove> [MigrationName]

Commands:
  add <MigrationName>    Create the migration in every provider project.
  remove                 Remove the latest migration from every provider project.

The script wires each provider to its dedicated startup tool so containers or
file-backed databases are provisioned automatically.
HELP
}

if [[ $# -lt 1 ]]; then
  usage
  exit 1
fi

command="$1"
shift || true

declare -a providers=(
  "Sqlite|src/TimeTracker.Persistence.SqliteMigrations|tools/TimeTracker.Tools.SqliteMigrations"
  "PgSql|src/TimeTracker.Persistence.PgSqlMigrations|tools/TimeTracker.Tools.PgSqlMigrations"
)

run_for_provider() {
  local provider="$1"
  local project_path="$2"
  local startup_path="$3"

  case "$command" in
    add)
      local migration_name="${migration:-}"
      if [[ -z "$migration_name" ]]; then
        echo "[ERROR] Missing migration name for 'add' command." >&2
        usage
        exit 1
      fi
      echo "[INFO] Adding migration '$migration_name' for $provider..."
      dotnet ef migrations add "$migration_name" \
        --context TimeTrackerDbContext \
        --project "$ROOT_DIR/$project_path" \
        --startup-project "$ROOT_DIR/$startup_path" \
        --output-dir Migrations \
        --no-build
      ;;
    remove)
      echo "[INFO] Removing latest migration for $provider..."
      dotnet ef migrations remove \
        --context TimeTrackerDbContext \
        --project "$ROOT_DIR/$project_path" \
        --startup-project "$ROOT_DIR/$startup_path" \
        --force \
        --no-build

      ;;
    *)
      echo "[ERROR] Unknown command: $command" >&2
      usage
      exit 1
      ;;
  esac
}

case "$command" in
  add)
    if [[ $# -lt 1 ]]; then
      usage
      exit 1
    fi
    migration="$1"
    ;;
  remove)
    migration=""
    ;;
  *)
    echo "[ERROR] Unknown command: $command" >&2
    usage
    exit 1
    ;;
esac

echo "[INFO] Restoring solution dependencies (if needed)..."
dotnet restore "$ROOT_DIR/TimeTracker.sln" >/dev/null
dotnet build "$ROOT_DIR/TimeTracker.sln" >/dev/null

for entry in "${providers[@]}"; do
  IFS='|' read -r provider project startup <<<"$entry"
  run_for_provider "$provider" "$project" "$startup"
done

echo "[SUCCESS] Completed '$command' across all providers."
