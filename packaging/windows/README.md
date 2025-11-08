# Windows MSI Template

This folder contains a WiX-based template for producing a Windows Installer
package that deploys the desktop app, CLI and the local API service. The
generated MSI:

1. Copies the desktop (`TimeTracker.Desktop`) and CLI (`TimeTracker.Cli`)
   publish outputs into `Program Files\TimeTracker`.
2. Installs the API publish output and registers it as a Windows service that
   automatically starts on boot.
3. Runs the API service under the built-in `LocalService` account and forces it
   to listen only on `http://127.0.0.1:5058`.
4. Stores the SQLite database under `%ProgramData%\TimeTracker` and points the
   API at that location via the `TIMETRACKER_DB_PATH` environment variable.

## Prerequisites

* .NET 9.0 SDK
* PowerShell 7+
* [WiX v4](https://wixtoolset.org/docs/wix4/) (`wix.exe` available on `PATH`)

Install WiX via the .NET tool feed so the CLI and its extensions are available locally (Linux users can simply run `bash scripts/setup-wix.sh` from the repository root):

```powershell
dotnet tool install --global wix
# Add the tools directory to PATH if needed:
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"

# One-time extension install to enable harvest + service authoring:
wix extension add -g WixToolset.Harvest.wixext
wix extension add -g WixToolset.Util.wixext
```

## Building the MSI

```powershell
cd packaging/windows
pwsh ./build.ps1 -Version 1.5.0 -OutputName TimeTracker-1.5.0.msi
```

The script performs the following steps:

1. Publishes the desktop, CLI, and API projects for `win-x64` into
   `packaging/windows/publish/<desktop|cli|api>`.
2. Uses `wix harvest` to generate component fragments for the desktop and CLI
   payloads.
3. Copies `appsettings.json` / `appsettings.Windows.json` into the API publish
   directory so the service has its configuration at runtime.
4. Builds `TimeTracker.wxs` (plus the harvested fragments) into the MSI defined
   by `-OutputName`.

> **Note:** The template assumes WiX harvests create `DesktopFiles.wxs` and
> `CliFiles.wxs` with component group IDs `DesktopFiles` and `CliFiles`. If you
> change the IDs or add additional fragments (e.g., shortcuts), update
> `TimeTracker.wxs` accordingly.

## Customising the Installer

* Modify `TimeTracker.wxs` to add shortcuts, additional files, or registry keys.
* Change the service account or start parameters inside the
  `ApiServiceComponents` component group if your environment requires
  additional permissions.
* To include extra configuration files, copy them into the API publish folder in
  `build.ps1` and mirror them inside the `ApiServiceComponents` group.

## Installing / Removing the Service Manually

The MSI installs a service named **TimeTrackerApi**. After installation you can
control it with:

```powershell
Start-Service TimeTrackerApi
Stop-Service TimeTrackerApi
Get-Service TimeTrackerApi
```

Uninstalling the MSI stops and removes the service automatically.
