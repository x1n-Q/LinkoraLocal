param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern("^[A-Za-z0-9.-]+$")]
  [string]$IdentityName,

  [Parameter(Mandatory = $true)]
  [ValidatePattern("^CN=.+")]
  [string]$Publisher,

  [Parameter(Mandatory = $true)]
  [ValidateLength(1, 50)]
  [string]$PublisherDisplayName,

  [Parameter(Mandatory = $true)]
  [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
  [string]$CloudflaredPath,

  [ValidatePattern("^\d+\.\d+\.\d+\.\d+$")]
  [string]$Version = "0.1.0.0"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$artifactsDirectory = Join-Path $repositoryRoot "artifacts"
$runId = "$PID-$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())"
$workDirectory = Join-Path $artifactsDirectory "work\$runId"
$publishDirectory = Join-Path $workDirectory "publish-win-x64"
$stagingDirectory = Join-Path $workDirectory "msix-staging"
$projectPath = Join-Path $repositoryRoot "src\Linkora.Local\Linkora.Local.csproj"

foreach ($target in @($workDirectory, $publishDirectory, $stagingDirectory)) {
  $resolvedTarget = [System.IO.Path]::GetFullPath($target)
  if (-not $resolvedTarget.StartsWith($artifactsDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clear a packaging directory outside the repository artifacts folder."
  }
  New-Item -ItemType Directory -Force -Path $resolvedTarget | Out-Null
}

dotnet publish $projectPath `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -p:Version=$($Version.Substring(0, $Version.LastIndexOf("."))) `
  -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
  throw "dotnet publish failed."
}

Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $stagingDirectory -Recurse
Copy-Item -LiteralPath $CloudflaredPath -Destination (Join-Path $stagingDirectory "cloudflared.exe")

$assetDirectory = Join-Path $stagingDirectory "Assets"
& (Join-Path $PSScriptRoot "New-Assets.ps1") -OutputDirectory $assetDirectory

$manifestTemplate = Get-Content -Raw (Join-Path $repositoryRoot "packaging\AppxManifest.xml.template")
$manifest = $manifestTemplate.Replace(
  "__IDENTITY_NAME__",
  [System.Security.SecurityElement]::Escape($IdentityName))
$manifest = $manifest.Replace(
  "__PUBLISHER__",
  [System.Security.SecurityElement]::Escape($Publisher))
$manifest = $manifest.Replace(
  "__PUBLISHER_DISPLAY_NAME__",
  [System.Security.SecurityElement]::Escape($PublisherDisplayName))
$manifest = $manifest.Replace("__VERSION__", $Version)
if ($manifest -match "__[A-Z_]+__") {
  throw "The generated package manifest contains an unresolved template value."
}
[System.IO.File]::WriteAllText(
  (Join-Path $stagingDirectory "AppxManifest.xml"),
  $manifest,
  [System.Text.UTF8Encoding]::new($false))

$windowsKitsBin = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
$makeAppx = Get-ChildItem -Path $windowsKitsBin -Filter makeappx.exe -Recurse |
  Where-Object { $_.FullName -match "\\x64\\makeappx\.exe$" } |
  Sort-Object FullName -Descending |
  Select-Object -First 1
if (-not $makeAppx) {
  throw "makeappx.exe was not found. Install the Windows SDK."
}

$packagePath = Join-Path $artifactsDirectory "LinkoraLocal_${Version}_x64.msix"
if (Test-Path -LiteralPath $packagePath) {
  Remove-Item -LiteralPath $packagePath -Force
}
& $makeAppx.FullName pack /d $stagingDirectory /p $packagePath /o
if ($LASTEXITCODE -ne 0) {
  throw "MSIX packaging failed."
}

Write-Host ""
Write-Host "Created $packagePath"
Write-Host "Submit this unsigned package to Partner Center, or sign it before local installation."

Remove-Item -LiteralPath $workDirectory -Recurse -Force -ErrorAction SilentlyContinue
