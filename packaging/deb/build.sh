#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(cd -- "${SCRIPT_DIR}/../.." && pwd)

: "${VERSION:=1.0.0}"
: "${CONFIGURATION:=Release}"
: "${RUNTIME:=linux-x64}"
ARCHITECTURE="amd64"

BUILD_ROOT="${SCRIPT_DIR}/build"
PUBLISH_ROOT="${BUILD_ROOT}/publish"
STAGING_ROOT="${BUILD_ROOT}/timetracker_${VERSION}_${ARCHITECTURE}"
PKG_DIR="${STAGING_ROOT}/timetracker"
DEBIAN_DIR="${PKG_DIR}/DEBIAN"

rm -rf "${BUILD_ROOT}"
mkdir -p "${PUBLISH_ROOT}" "${PKG_DIR}" "${DEBIAN_DIR}"

publish_project() {
  local project="$1"
  local output="$2"

  dotnet publish "${project}" \
    -c "${CONFIGURATION}" \
    -r "${RUNTIME}" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "${output}"
}

DESKTOP_PUBLISH="${PUBLISH_ROOT}/desktop"
CLI_PUBLISH="${PUBLISH_ROOT}/cli"
API_PUBLISH="${PUBLISH_ROOT}/api"

publish_project "${REPO_ROOT}/src/TimeTracker.Desktop/TimeTracker.Desktop.csproj" "${DESKTOP_PUBLISH}"
publish_project "${REPO_ROOT}/src/TimeTracker.Cli/TimeTracker.Cli.csproj" "${CLI_PUBLISH}"
publish_project "${REPO_ROOT}/src/TimeTracker.Api/TimeTracker.Api.csproj" "${API_PUBLISH}"

install -d "${PKG_DIR}/usr/lib/timetracker/desktop" \
           "${PKG_DIR}/usr/lib/timetracker/cli" \
           "${PKG_DIR}/usr/lib/timetracker/api" \
           "${PKG_DIR}/usr/bin" \
           "${PKG_DIR}/usr/share/applications" \
           "${PKG_DIR}/lib/systemd/system"

cp -r "${DESKTOP_PUBLISH}/"* "${PKG_DIR}/usr/lib/timetracker/desktop/"
cp -r "${CLI_PUBLISH}/"* "${PKG_DIR}/usr/lib/timetracker/cli/"
cp -r "${API_PUBLISH}/"* "${PKG_DIR}/usr/lib/timetracker/api/"

cat > "${PKG_DIR}/usr/bin/timetracker" <<'EOF'
#!/usr/bin/env bash
exec /usr/lib/timetracker/cli/TimeTracker.Cli "$@"
EOF

cat > "${PKG_DIR}/usr/bin/timetracker-desktop" <<'EOF'
#!/usr/bin/env bash
exec /usr/lib/timetracker/desktop/TimeTracker.Desktop "$@"
EOF

chmod 0755 "${PKG_DIR}/usr/bin/timetracker" "${PKG_DIR}/usr/bin/timetracker-desktop"

cp "${REPO_ROOT}/packaging/systemd/timetracker-api.service" "${PKG_DIR}/lib/systemd/system/timetracker-api.service"

install -d "${PKG_DIR}/usr/share/applications"
cat > "${PKG_DIR}/usr/share/applications/timetracker.desktop" <<'EOF'
[Desktop Entry]
Version=1.0
Type=Application
Name=TimeTracker
Comment=Track work hours and tasks
Exec=/usr/bin/timetracker-desktop
Terminal=false
Categories=Office;Utility;TimeTracking;
EOF

cat > "${DEBIAN_DIR}/control" <<EOF
Package: timetracker
Version: ${VERSION}
Section: misc
Priority: optional
Architecture: ${ARCHITECTURE}
Maintainer: TimeTracker <support@example.com>
Depends: systemd (>= 247)
Description: Local-first time tracking desktop app, CLI and API service
 Provides the Avalonia desktop UI, CLI helpers, and a background API that
 exposes timer services over localhost for Waybar and automation hooks.
EOF

cat > "${DEBIAN_DIR}/postinst" <<'EOF'
#!/bin/sh
set -e

if command -v systemctl >/dev/null 2>&1; then
    systemctl daemon-reload || true
    systemctl enable --now timetracker-api.service || true
fi

exit 0
EOF

cat > "${DEBIAN_DIR}/prerm" <<'EOF'
#!/bin/sh
set -e

if [ "$1" = "remove" ] && command -v systemctl >/dev/null 2>&1; then
    systemctl stop timetracker-api.service || true
fi

exit 0
EOF

cat > "${DEBIAN_DIR}/postrm" <<'EOF'
#!/bin/sh
set -e

if command -v systemctl >/dev/null 2>&1; then
    systemctl disable --now timetracker-api.service 2>/dev/null || true
    systemctl daemon-reload || true
fi

exit 0
EOF

chmod 0755 "${DEBIAN_DIR}/postinst" "${DEBIAN_DIR}/prerm" "${DEBIAN_DIR}/postrm"

dpkg-deb --build "${PKG_DIR}" "${SCRIPT_DIR}/timetracker_${VERSION}_${ARCHITECTURE}.deb"

echo "Built ${SCRIPT_DIR}/timetracker_${VERSION}_${ARCHITECTURE}.deb"
