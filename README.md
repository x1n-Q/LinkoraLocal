# Linkora Local

Linkora Local is an open-source Windows application that finds local web
services and publishes one through a Linkora managed tunnel.

```text
LocalHost found

Portfolio
localhost:3318

[ Publish securely ]
```

It is designed for previews and development. Publishing does **not** turn a
local process into a permanent production deployment: the public hostname is
reachable only while the local application and Linkora Local are running.

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
- A Linkora member account
- An available managed-tunnel hostname slot
- `cloudflared.exe`

Release packages are expected to include a pinned official `cloudflared`
binary. Development builds also search the normal PATH and standard Program
Files locations.

## Build

Install the .NET 8 SDK, then run:

```powershell
dotnet build src\Linkora.Local\Linkora.Local.csproj -c Release
```

The production API is used by default. To use a local API:

```powershell
$env:LINKORA_API_URL = "http://127.0.0.1:4000"
dotnet run --project src\Linkora.Local\Linkora.Local.csproj
```

Run the source and secret checks:

```powershell
.\scripts\Test-Source.ps1
```

## Windows Setup release

The GitHub release is a self-contained Windows 10/11 x64 Setup executable. It
does not require a separate .NET installation and bundles an official,
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
