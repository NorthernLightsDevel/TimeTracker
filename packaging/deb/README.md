# Debian Package Template

This folder provides a simple script for producing a `.deb` that bundles the
desktop application, CLI, and local API service. The resulting package installs
everything under `/usr/lib/timetracker`, exposes `/usr/bin/timetracker` and
`/usr/bin/timetracker-desktop`, and registers the API as a systemd service that
listens only on `http://127.0.0.1:5058`.

## Prerequisites

* Debian/Ubuntu host with `dotnet-sdk-9.0` (or newer) installed
* `dpkg-deb`
* PowerShell is _not_ required (the script is Bash)

## Building

```bash
cd packaging/deb
VERSION=1.5.0 ./build.sh
```

Optional environment variables:

| variable       | default    | description                          |
|----------------|------------|--------------------------------------|
| `VERSION`      | `1.0.0`    | Package version (used in control file)|
| `CONFIGURATION`| `Release`  | Build configuration                  |
| `RUNTIME`      | `linux-x64`| Runtime identifier for publishing    |

The output `timetracker_<version>_amd64.deb` lands in the same directory.

## Service Behaviour

* Installs `timetracker-api.service` under `/lib/systemd/system`.
* Runs under the default (root) service account but only binds to
  `127.0.0.1:5058` and uses `/var/lib/timetracker/timetracker.db` for storage.
* `postinst` enables and starts the service automatically. `prerm` stops it on
  removal, and `postrm` disables it after uninstall.

You can control the service with the usual systemctl commands:

```bash
sudo systemctl status timetracker-api.service
sudo systemctl stop timetracker-api.service
```

> **Note:** The `.deb` is a template for local use and is not signed. Adjust the
> control metadata, dependencies, and maintainer fields before distributing to
> others.
