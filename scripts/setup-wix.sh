#!/usr/bin/env bash
set -euo pipefail

if ! command -v dotnet >/dev/null 2>&1; then
    echo "dotnet SDK is required to install WiX tools. Install .NET 9.0+ and re-run this script." >&2
    exit 1
fi

tools_dir="${DOTNET_TOOLS_DIR:-$HOME/.dotnet/tools}"

install_or_update_wix() {
    if command -v wix >/dev/null 2>&1; then
        echo "Updating WiX CLI..."
        dotnet tool update --global wix >/dev/null || true
    else
        echo "Installing WiX CLI..."
        dotnet tool install --global wix
    }
}

ensure_path() {
    if ! command -v wix >/dev/null 2>&1 && [[ ":$PATH:" != *":$tools_dir:"* ]]; then
        export PATH="$tools_dir:$PATH"
        echo "Temporarily added $tools_dir to PATH for this session."
    fi

    if ! command -v wix >/dev/null 2>&1; then
        cat <<EOF >&2
WiX CLI is installed under $tools_dir but not on PATH.
Add the following line to your shell profile (e.g., ~/.bashrc) and re-run:
    export PATH="$tools_dir:\$PATH"
EOF
        exit 1
    fi
}

install_or_update_wix
ensure_path

echo "Installing WiX extensions..."
wix extension add -g WixToolset.Util.wixext
wix extension add -g WixToolset.Harvest.wixext
wix extension enable -g WixToolset.Util.wixext
wix extension enable -g WixToolset.Harvest.wixext

echo "WiX CLI ready. You can now run packaging/windows/build.ps1 on Windows."
