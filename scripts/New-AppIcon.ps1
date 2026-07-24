param(
  [Parameter(Mandatory = $true)]
  [string]$OutputPath
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$resolvedPath = [System.IO.Path]::GetFullPath($OutputPath)
$directory = Split-Path -Parent $resolvedPath
New-Item -ItemType Directory -Force -Path $directory | Out-Null

$size = 256
$bitmap = [System.Drawing.Bitmap]::new($size, $size)
$bitmap.SetResolution(96, 96)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.ColorTranslator]::FromHtml("#050A11"))

$cyan = [System.Drawing.ColorTranslator]::FromHtml("#38D6F4")
$lime = [System.Drawing.ColorTranslator]::FromHtml("#B7FF4A")
$background = [System.Drawing.ColorTranslator]::FromHtml("#050A11")
$pen = [System.Drawing.Pen]::new($cyan, 24)
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Square
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Square
$graphics.DrawLine($pen, 64, 48, 64, 190)
$graphics.DrawLine($pen, 64, 190, 188, 190)

$dotBrush = [System.Drawing.SolidBrush]::new($background)
$dotPen = [System.Drawing.Pen]::new($lime, 14)
$graphics.FillEllipse($dotBrush, 166, 168, 44, 44)
$graphics.DrawEllipse($dotPen, 166, 168, 44, 44)

$handle = $bitmap.GetHicon()
try {
  $icon = [System.Drawing.Icon]::FromHandle($handle)
  $stream = [System.IO.File]::Create($resolvedPath)
  try {
    $icon.Save($stream)
  }
  finally {
    $stream.Dispose()
    $icon.Dispose()
  }
}
finally {
  Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class LinkoraIconNative {
  [DllImport("user32.dll")]
  public static extern bool DestroyIcon(IntPtr handle);
}
"@ -ErrorAction SilentlyContinue
  [LinkoraIconNative]::DestroyIcon($handle) | Out-Null
  $dotPen.Dispose()
  $dotBrush.Dispose()
  $pen.Dispose()
  $graphics.Dispose()
  $bitmap.Dispose()
}

Write-Host "Created $resolvedPath"
