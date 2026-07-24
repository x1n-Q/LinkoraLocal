param(
  [ValidatePattern("^\d+\.\d+\.\d+$")]
  [string]$Version = "0.1.0",

  [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
  [string]$CloudflaredPath = "C:\Program Files (x86)\cloudflared\cloudflared.exe"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$artifactsDirectory = Join-Path $repositoryRoot "artifacts"
$runId = "$PID-$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())"
$workDirectory = Join-Path $artifactsDirectory "installer-work\$runId"
$publishDirectory = Join-Path $workDirectory "publish"
$iconPath = Join-Path $workDirectory "Linkora.Local.ico"
$projectPath = Join-Path $repositoryRoot "src\Linkora.Local\Linkora.Local.csproj"
$installerScript = Join-Path $repositoryRoot "packaging\LinkoraLocal.iss"
$installerPath = Join-Path $artifactsDirectory "Linkora-Local-Windows-x64.exe"
$checksumPath = "$installerPath.sha256"
$sbomPath = Join-Path $artifactsDirectory "Linkora-Local-Windows-x64.sbom.spdx.json"

$resolvedArtifacts = [System.IO.Path]::GetFullPath($artifactsDirectory)
$resolvedWork = [System.IO.Path]::GetFullPath($workDirectory)
if (-not $resolvedWork.StartsWith(
    $resolvedArtifacts,
    [System.StringComparison]::OrdinalIgnoreCase)) {
  throw "Refusing to use a packaging directory outside the repository artifacts folder."
}
New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null

& (Join-Path $PSScriptRoot "Test-Source.ps1")
if ($LASTEXITCODE -ne 0) {
  throw "Source checks failed."
}

$cloudflaredSignature = Get-AuthenticodeSignature -LiteralPath $CloudflaredPath
if (($cloudflaredSignature.Status -ne "Valid") -or
    ($cloudflaredSignature.SignerCertificate.Subject -notmatch "Cloudflare, Inc\.")) {
  throw "cloudflared.exe is not signed by Cloudflare, Inc."
}
$cloudflaredVersionLine = & $CloudflaredPath --version
if ($LASTEXITCODE -ne 0 -or $cloudflaredVersionLine -notmatch "cloudflared version ([0-9.]+)") {
  throw "The Cloudflare connector version could not be verified."
}
$cloudflaredVersion = $Matches[1]

& (Join-Path $PSScriptRoot "New-AppIcon.ps1") -OutputPath $iconPath

dotnet publish $projectPath `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -p:ApplicationIcon=$iconPath `
  -p:Version=$Version `
  -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
  throw "dotnet publish failed."
}

$applicationPath = Join-Path $publishDirectory "Linkora.Local.exe"
if (-not (Test-Path -LiteralPath $applicationPath -PathType Leaf)) {
  throw "The Windows application executable was not produced."
}

$secretPatterns = [ordered]@{
  CloudflareTunnelToken = "eyJhIjoi[A-Za-z0-9_-]{40,}"
  TunnelCommand = "cloudflared\s+(?:service\s+install|tunnel\s+run)\s+--token\s+\S+"
  SecretAssignment = "(?:TURNSTILE_SECRET_KEY|CLOUDFLARE_API_TOKEN|DATABASE_URL|SMTP_(?:PASS|PASSWORD))\s*=\s*\S+"
  PrivateKey = "-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"
}

function Assert-NoEmbeddedSecret {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Path
  )

  $bytes = [System.IO.File]::ReadAllBytes($Path)
  $ascii = [System.Text.Encoding]::ASCII.GetString($bytes)
  $unicode = [System.Text.Encoding]::Unicode.GetString($bytes)
  foreach ($entry in $secretPatterns.GetEnumerator()) {
    if ($ascii -match $entry.Value -or $unicode -match $entry.Value) {
      throw "$($entry.Key) content was detected in $(Split-Path -Leaf $Path)."
    }
  }
}

Assert-NoEmbeddedSecret -Path $applicationPath

$innoInstallLocation = Get-ItemProperty `
  "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*" `
  -ErrorAction SilentlyContinue |
  Where-Object DisplayName -like "Inno Setup version*" |
  Select-Object -First 1 -ExpandProperty InstallLocation
$innoCompiler = if ($innoInstallLocation) {
  Join-Path $innoInstallLocation "ISCC.exe"
} else {
  "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
}
if (-not (Test-Path -LiteralPath $innoCompiler -PathType Leaf)) {
  throw "Inno Setup 6 was not found. Install JRSoftware.InnoSetup with winget."
}

& $innoCompiler `
  "/DMyAppVersion=$Version" `
  "/DPublishDir=$publishDirectory" `
  "/DCloudflaredPath=$CloudflaredPath" `
  "/DAppIconPath=$iconPath" `
  "/DOutputDir=$artifactsDirectory" `
  $installerScript
if ($LASTEXITCODE -ne 0) {
  throw "Windows Setup compilation failed."
}
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
  throw "The Windows Setup executable was not produced."
}

Assert-NoEmbeddedSecret -Path $installerPath

$installerHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText(
  $checksumPath,
  "$installerHash  $(Split-Path -Leaf $installerPath)`n",
  [System.Text.UTF8Encoding]::new($false))

$cloudflaredHash = (Get-FileHash -LiteralPath $CloudflaredPath -Algorithm SHA256).Hash.ToLowerInvariant()
$created = [DateTimeOffset]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
$namespaceSeed = "$Version-$installerHash"
$sbom = [ordered]@{
  spdxVersion = "SPDX-2.3"
  dataLicense = "CC0-1.0"
  SPDXID = "SPDXRef-DOCUMENT"
  name = "Linkora-Local-$Version"
  documentNamespace = "https://github.com/x1n-Q/LinkoraLocal/releases/tag/v$Version/$namespaceSeed"
  creationInfo = [ordered]@{
    created = $created
    creators = @("Tool: LinkoraLocal-Build-Installer", "Organization: Linkora")
  }
  packages = @(
    [ordered]@{
      name = "Linkora Local"
      SPDXID = "SPDXRef-Package-LinkoraLocal"
      versionInfo = $Version
      downloadLocation = "https://github.com/x1n-Q/LinkoraLocal"
      filesAnalyzed = $false
      licenseConcluded = "MIT"
      licenseDeclared = "MIT"
      copyrightText = "Copyright (c) 2026 Daniel Depaor and Linkora contributors"
      checksums = @([ordered]@{ algorithm = "SHA256"; checksumValue = $installerHash })
    },
    [ordered]@{
      name = "cloudflared"
      SPDXID = "SPDXRef-Package-cloudflared"
      versionInfo = $cloudflaredVersion
      downloadLocation = "https://github.com/cloudflare/cloudflared"
      filesAnalyzed = $false
      licenseConcluded = "NOASSERTION"
      licenseDeclared = "NOASSERTION"
      copyrightText = "Copyright Cloudflare, Inc."
      checksums = @([ordered]@{ algorithm = "SHA256"; checksumValue = $cloudflaredHash })
    }
  )
  relationships = @(
    [ordered]@{
      spdxElementId = "SPDXRef-Package-LinkoraLocal"
      relationshipType = "DEPENDS_ON"
      relatedSpdxElement = "SPDXRef-Package-cloudflared"
    }
  )
}
[System.IO.File]::WriteAllText(
  $sbomPath,
  ($sbom | ConvertTo-Json -Depth 10),
  [System.Text.UTF8Encoding]::new($false))

Remove-Item -LiteralPath $workDirectory -Recurse -Force

Write-Host ""
Write-Host "Created $installerPath"
Write-Host "SHA256 $installerHash"
Write-Host "Bundled cloudflared $cloudflaredVersion with a valid Cloudflare signature."
