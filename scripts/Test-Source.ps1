$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repositoryRoot "src\Linkora.Local\Linkora.Local.csproj"

dotnet build $projectPath -c Release -warnaserror
if ($LASTEXITCODE -ne 0) {
  throw "Release build failed."
}

$sourceFiles = Get-ChildItem -Path $repositoryRoot -Recurse -File |
  Where-Object {
    $_.FullName -notmatch "\\(bin|obj|artifacts|\.git)\\" -and
    $_.Extension -in @(
      ".cs",
      ".xaml",
      ".ps1",
      ".iss",
      ".json",
      ".xml",
      ".md",
      ".yml",
      ".yaml"
    )
  }

$forbiddenPatterns = @(
  "eyJhIjoi[A-Za-z0-9_-]{40,}",
  "cloudflared\s+(?:service\s+install|tunnel\s+run)\s+--token\s+\S+",
  "TURNSTILE_SECRET_KEY\s*=\s*\S+",
  "CLOUDFLARE_API_TOKEN\s*=\s*\S+",
  "DATABASE_URL\s*=\s*\S+",
  "SMTP_(?:PASS|PASSWORD)\s*=\s*\S+",
  "-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"
)

foreach ($pattern in $forbiddenPatterns) {
  $matches = $sourceFiles | Select-String -Pattern $pattern
  if ($matches) {
    $matches | ForEach-Object { Write-Error "$($_.Path):$($_.LineNumber) contains forbidden secret-like content." }
    throw "Source secret scan failed."
  }
}

Write-Host "Release build and source secret scan passed."
