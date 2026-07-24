# Linkora Local

[![Build](https://github.com/x1n-Q/LinkoraLocal/actions/workflows/build.yml/badge.svg)](https://github.com/x1n-Q/LinkoraLocal/actions/workflows/build.yml)
[![CodeQL](https://github.com/x1n-Q/LinkoraLocal/actions/workflows/codeql.yml/badge.svg)](https://github.com/x1n-Q/LinkoraLocal/actions/workflows/codeql.yml)
[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-32d8ff)](https://github.com/x1n-Q/LinkoraLocal/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-c9ff54.svg)](LICENSE)

Publish a localhost website through a short Linkora hostname—without router
port forwarding or a public inbound firewall rule.

Linkora Local is an open-source Windows application that discovers local HTTP
services, lets you select one, and connects it through a Linkora-managed
Cloudflare Tunnel.

**[Download Linkora Local 0.1.0 for Windows](https://github.com/x1n-Q/LinkoraLocal/releases/download/v0.1.0/Linkora-Local-Windows-x64.exe)**
· [Release notes and checksum](https://github.com/x1n-Q/LinkoraLocal/releases/tag/v0.1.0)
· [Linkora website](https://linkora.top/#linkora-local)
· [Manual tunnel guide](https://linkora.top/guides/cloudflare-tunnel)

> **Windows preview:** the current installer is not yet code-signed by
> Linkora. Windows SmartScreen may display an **Unknown publisher** warning.
> Verify the published SHA-256 checksum or build the app from this repository
> before installing.

## How it works

```text
Localhost found        Select a hostname       Publish securely
127.0.0.1:3318    →    portfolio.nx1.lol  →    Cloudflare edge
```

1. Install and open Linkora Local on Windows.
2. Start your website on `localhost`.
3. Choose **Connect account**. Approval happens on `linkora.top` in your
   browser; your password never enters the desktop app.
4. Select the detected local service and an available Linkora hostname.
5. Choose **Publish securely**.

The public hostname remains reachable while the local website, Linkora Local,
and the computer are running. This is intended for development, previews, home
labs, and self-hosted services—not as a replacement for an always-running
production server.

## Trust model

- Account approval happens in the system browser. Linkora Local never receives
  the account password.
- The browser shows a short code that must match the desktop application.
- The resulting device token is revocable, expires after 30 days, and is stored
  in Windows Credential Manager.
- Tunnel credentials are kept in memory and are never written to application
  settings or logs.
- Database, SSH, mail, remote desktop, Docker, and other sensitive ports are
  excluded from discovery.
- No router port forwarding or public inbound firewall rule is required.
- No analytics or telemetry is collected by the client.

See [SECURITY.md](SECURITY.md) and [PRIVACY.md](PRIVACY.md) for the complete
security and data-handling model.

## Current MVP

- Native, lightweight Windows UI
- IPv4 localhost listener discovery
- HTTP and HTTPS health probing
- Browser-approved Linkora device authorization
- Existing hostname selection
- New managed-tunnel hostname creation
- Secure `cloudflared` process management
- Public edge health check
- System tray operation after the editor closes
- Optional start-with-Windows and automatic managed-tunnel reconnect
- Immediate stop-sharing and device-disconnect controls

## Always-on mode

Enable **Always on while this PC is on** before publishing. Linkora Local then:

- starts hidden after the Windows user signs in;
- waits for the saved loopback web service to become reachable;
- requests a fresh, short-lived tunnel credential from Linkora;
- restores the last published managed-tunnel hostname; and
- retries automatically if the local service or connector starts late.

The computer and the local website process must both be running. Linkora Local
cannot keep a home-hosted site online while the computer is shut down. Configure
the website itself as a Windows startup task or service if it must return after
a reboot.

## Requirements

- Windows 10 version 1809 or later, or Windows 11
- x64 processor
- A Linkora member account
- An available managed-tunnel hostname slot

The GitHub Setup release is self-contained: it includes the required .NET
runtime and an official Cloudflare-signed `cloudflared.exe`. Development builds
also search `PATH` and the standard Program Files locations.

## Verify the download

The v0.1.0 Windows installer has this SHA-256 digest:

```text
c8d6b4e24348ee24da7df3465a7129c8d3ff17ecc5d9d14c95a8a7922acb55d3
```

Verify it in PowerShell:

```powershell
Get-FileHash .\Linkora-Local-Windows-x64.exe -Algorithm SHA256
```

The release also includes the
[checksum file](https://github.com/x1n-Q/LinkoraLocal/releases/download/v0.1.0/Linkora-Local-Windows-x64.exe.sha256)
and an
[SPDX SBOM](https://github.com/x1n-Q/LinkoraLocal/releases/download/v0.1.0/Linkora-Local-Windows-x64.sbom.spdx.json).

## Build from source

Install the .NET 8 SDK, then run:

```powershell
dotnet build src\Linkora.Local\Linkora.Local.csproj -c Release
```

The production API is used by default. To use a local API:

```powershell
$env:LINKORA_API_URL = "http://127.0.0.1:4000"
dotnet run --project src\Linkora.Local\Linkora.Local.csproj -c Debug
```

The API override is available only in Debug builds. Release builds are pinned
to `https://api.linkora.top`.

Run the build, analyzer, and source-secret checks:

```powershell
.\scripts\Test-Source.ps1
```

## Windows Setup release

The release script creates a self-contained Windows 10/11 x64 Setup executable.
It does not require a separate .NET installation and bundles an official,
Authenticode-signed `cloudflared.exe`.

Install Inno Setup 6, install the official Cloudflare connector, then build:

```powershell
winget install --id JRSoftware.InnoSetup --exact
winget install --id Cloudflare.cloudflared --exact
.\scripts\Build-Installer.ps1
```

The release artifacts are written to `artifacts/`:

- `Linkora-Local-Windows-x64.exe`
- `Linkora-Local-Windows-x64.exe.sha256`
- `Linkora-Local-Windows-x64.sbom.spdx.json`

The build refuses an unsigned or non-Cloudflare connector and scans the source,
application executable, and final installer for secret-like material.

## Contributing

Bug reports and focused pull requests are welcome. Read
[CONTRIBUTING.md](CONTRIBUTING.md) before submitting a change. Report security
issues privately as described in [SECURITY.md](SECURITY.md).

## Microsoft Store package

1. Reserve **Linkora Local** in Microsoft Partner Center.
2. Replace the placeholder publisher with the exact Partner Center publisher
   identity.
3. Supply a trusted official `cloudflared.exe`.
4. Build the MSIX:

```powershell
.\scripts\Build-MSIX.ps1 `
  -Publisher "CN=YOUR_PARTNER_CENTER_PUBLISHER" `
  -CloudflaredPath "C:\Program Files (x86)\cloudflared\cloudflared.exe"
```

The package is written to `artifacts/`. Partner Center signs the accepted Store
package. A locally installed test package must be signed with a certificate
trusted by the test computer.

## Linkora API integration

The hosted Linkora service provides:

- a ten-minute device authorization code;
- a browser approval page;
- a revocable desktop access token;
- hostname and domain-pool information;
- managed tunnel creation and reconnection.

The desktop token is restricted server-side to the member workspace, tunnel
creation, analytics reads, and Linkora Local endpoints. It cannot access the
admin API or delete a hostname.

## License

Linkora Local is released under the [MIT License](LICENSE). `cloudflared` is a
separate Cloudflare project and is subject to its own license and notices.
