param(
  [Parameter(Mandatory = $true)]
  [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$assetDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $assetDirectory | Out-Null

function New-LinkoraBitmap {
  param(
    [int]$Width,
    [int]$Height,
    [string]$Path,
    [switch]$Wordmark
  )

  $bitmap = [System.Drawing.Bitmap]::new($Width, $Height)
  $bitmap.SetResolution(96, 96)
  $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
  $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
  $graphics.Clear([System.Drawing.ColorTranslator]::FromHtml("#050A11"))

  $scale = [Math]::Min($Width, $Height)
  $markSize = [Math]::Max(18, [int]($scale * 0.44))
  $markX = if ($Wordmark) { [int]($Width * 0.12) } else { [int](($Width - $markSize) / 2) }
  $markY = [int](($Height - $markSize) / 2)
  $stroke = [Math]::Max(2, [int]($markSize * 0.09))

  $cyan = [System.Drawing.ColorTranslator]::FromHtml("#38D6F4")
  $lime = [System.Drawing.ColorTranslator]::FromHtml("#B7FF4A")
  $white = [System.Drawing.ColorTranslator]::FromHtml("#F5F8FC")
  $pen = [System.Drawing.Pen]::new($cyan, $stroke)
  $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Square
  $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Square
  $graphics.DrawLine($pen, $markX, $markY, $markX, $markY + $markSize)
  $graphics.DrawLine($pen, $markX, $markY + $markSize, $markX + $markSize, $markY + $markSize)
  $dotSize = [Math]::Max(5, [int]($markSize * 0.22))
  $dotBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml("#050A11"))
  $dotPen = [System.Drawing.Pen]::new($lime, [Math]::Max(2, [int]($stroke * 0.7)))
  $graphics.FillEllipse(
    $dotBrush,
    $markX + $markSize - [int]($dotSize / 2),
    $markY + $markSize - [int]($dotSize / 2),
    $dotSize,
    $dotSize)
  $graphics.DrawEllipse(
    $dotPen,
    $markX + $markSize - [int]($dotSize / 2),
    $markY + $markSize - [int]($dotSize / 2),
    $dotSize,
    $dotSize)

  if ($Wordmark) {
    $fontSize = [Math]::Max(14, [int]($Height * 0.20))
    $font = [System.Drawing.Font]::new("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold)
    $textBrush = [System.Drawing.SolidBrush]::new($white)
    $graphics.DrawString(
      "linkora local",
      $font,
      $textBrush,
      [float]($markX + $markSize + $Width * 0.07),
      [float](($Height - $font.Height) / 2))
    $font.Dispose()
    $textBrush.Dispose()
  }

  $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
  $dotPen.Dispose()
  $dotBrush.Dispose()
  $pen.Dispose()
  $graphics.Dispose()
  $bitmap.Dispose()
}

New-LinkoraBitmap 44 44 (Join-Path $assetDirectory "Square44x44Logo.png")
New-LinkoraBitmap 50 50 (Join-Path $assetDirectory "StoreLogo.png")
New-LinkoraBitmap 150 150 (Join-Path $assetDirectory "Square150x150Logo.png")
New-LinkoraBitmap 310 310 (Join-Path $assetDirectory "Square310x310Logo.png")
New-LinkoraBitmap 310 150 (Join-Path $assetDirectory "Wide310x150Logo.png") -Wordmark
New-LinkoraBitmap 620 300 (Join-Path $assetDirectory "SplashScreen.png") -Wordmark

Write-Host "Generated Microsoft Store assets in $assetDirectory"
