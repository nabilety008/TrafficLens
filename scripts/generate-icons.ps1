# TrafficLens icon pipeline.
# Regenerates the application brand icon (multi-size ICO + PNGs) into assets\branding.
# The glyph mirrors the runtime-drawn tray icon used by SystemTrayService:
#   rounded dark square background, cyan down-arrow on top, teal up-arrow below.
# Assets are deliberately replaceable: swap the drawing logic (or the files) without
# touching application wiring.

param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$BrandBackground = [System.Drawing.Color]::FromArgb(30, 30, 46)
$BrandDown = [System.Drawing.Color]::FromArgb(79, 195, 247)
$BrandUp = [System.Drawing.Color]::FromArgb(38, 166, 154)

function New-BrandBitmap {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap $Size, $Size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $margin = [Math]::Max(1, [Math]::Round($Size * 0.06))
    $radius = [Math]::Max(1, [Math]::Round($Size * 0.22))

    # Rounded-square background path.
    $bgRect = New-Object System.Drawing.Rectangle $margin, $margin, ($Size - 2 * $margin), ($Size - 2 * $margin)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($bgRect.X, $bgRect.Y, $d, $d, 180, 90)
    $path.AddArc($bgRect.Right - $d, $bgRect.Y, $d, $d, 270, 90)
    $path.AddArc($bgRect.Right - $d, $bgRect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($bgRect.X, $bgRect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $brushBg = New-Object System.Drawing.SolidBrush $BrandBackground
    $brushDown = New-Object System.Drawing.SolidBrush $BrandDown
    $brushUp = New-Object System.Drawing.SolidBrush $BrandUp

    try {
        $g.FillPath($brushBg, $path)

        # Down-arrow triangle (apex down, upper area).
        $down = [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF ($Size * 0.28), ($Size * 0.34)),
            (New-Object System.Drawing.PointF ($Size * 0.72), ($Size * 0.34)),
            (New-Object System.Drawing.PointF ($Size * 0.50), ($Size * 0.53))
        )
        $g.FillPolygon($brushDown, $down)

        # Up-arrow triangle (apex up, lower area).
        $up = [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF ($Size * 0.28), ($Size * 0.69)),
            (New-Object System.Drawing.PointF ($Size * 0.72), ($Size * 0.69)),
            (New-Object System.Drawing.PointF ($Size * 0.50), ($Size * 0.50))
        )
        $g.FillPolygon($brushUp, $up)
    }
    finally {
        $g.Dispose()
        $brushDown.Dispose()
        $brushUp.Dispose()
        $brushBg.Dispose()
        $path.Dispose()
    }

    return $bmp
}

function ConvertTo-BmpBytes {
    param([System.Drawing.Bitmap]$Bitmap, [string]$Format = 'png')

    $ms = New-Object System.IO.MemoryStream
    try {
        switch ($Format) {
            'png'  { $Bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png) }
            'bmp'  { $Bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Bmp) }
            default { throw "Unsupported format: $Format" }
        }
        return [byte[]]$ms.ToArray()
    }
    finally {
        $ms.Dispose()
    }
}

function New-BrandIco {
    param([System.Collections.Generic.List[int]]$Sizes)

    $images = @()
    foreach ($size in $Sizes) {
        $bmp = New-BrandBitmap -Size $size
        try {
            $images += @{ Size = $size; Bytes = (ConvertTo-BmpBytes -Bitmap $bmp -Format 'png') }
        }
        finally {
            $bmp.Dispose()
        }
    }

    # ICONDIR header: reserved(2) + type(2) + count(2) = 6 bytes.
    $header = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $header
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$images.Count)
    $offset = 6 + (16 * $images.Count)
    foreach ($img in $images) {
        $payload = [byte[]]$img.Bytes
        $entrySize = $img.Size
        $dimension = if ($entrySize -ge 256) { 0 } else { $entrySize }
        $writer.Write([byte]$dimension)                       # width
        $writer.Write([byte]$dimension)                       # height
        $writer.Write([byte]0)                                # palette colors
        $writer.Write([byte]0)                                # reserved
        $writer.Write([uint16]0)                              # planes (0 for PNG-encoded entries)
        $writer.Write([uint16]0)                              # bit count (0 for PNG-encoded entries)
        $writer.Write([uint32]$payload.Length)                # data size
        $writer.Write([uint32]$offset)                        # data offset
        $offset += $payload.Length
    }
    foreach ($img in $images) {
        $payload = [byte[]]$img.Bytes
        $header.Write($payload, 0, $payload.Length)
    }
    $writer.Flush()
    $result = [byte[]]$header.ToArray()
    $writer.Dispose()
    $header.Dispose()
    return $result
}

# --- Output ---
$outDir = Join-Path $RepoRoot 'assets\branding'
if (-not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

$allSizes = New-Object System.Collections.Generic.List[int]
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $allSizes.Add([int]$size)
}

$icoBytes = New-BrandIco -Sizes $allSizes
$icoPath = Join-Path $outDir 'TrafficLens.ico'
[System.IO.File]::WriteAllBytes($icoPath, $icoBytes)
Write-Host "Wrote $icoPath ($($icoBytes.Length) bytes, $($allSizes.Count) sizes): $($allSizes -join '/')"

foreach ($size in @(256, 128)) {
    $bmp = New-BrandBitmap -Size $size
    try {
        $pngPath = Join-Path $outDir "TrafficLens-$size.png"
        $bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $fi = Get-Item -LiteralPath $pngPath
        Write-Host "Wrote $pngPath ($($fi.Length) bytes)"
    }
    finally {
        $bmp.Dispose()
    }
}

Write-Host 'Done.'