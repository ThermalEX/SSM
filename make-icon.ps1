Add-Type -AssemblyName System.Drawing

function New-RoundedPath {
    param([float]$X, [float]$Y, [float]$W, [float]$H, [float]$R)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $D = $R * 2
    $path.AddArc($X,           $Y,           $D, $D, 180, 90)
    $path.AddArc($X + $W - $D, $Y,           $D, $D, 270, 90)
    $path.AddArc($X + $W - $D, $Y + $H - $D, $D, $D,   0, 90)
    $path.AddArc($X,           $Y + $H - $D, $D, $D,  90, 90)
    $path.CloseFigure()
    return $path
}

function New-SSMBitmap {
    param([int]$Sz)

    $bmp = New-Object System.Drawing.Bitmap($Sz, $Sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # background
    $bgPath  = New-RoundedPath 0 0 $Sz $Sz ([int]($Sz * 0.18))
    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 11, 17, 30))
    $g.FillPath($bgBrush, $bgPath)
    $bgBrush.Dispose()
    $bgPath.Dispose()

    if ($Sz -ge 32) {
        # screen frame
        $px = [int]($Sz * 0.10)
        $py = [int]($Sz * 0.12)
        $pw = $Sz - $px * 2
        $ph = [int]($Sz * 0.54)
        $pr = [int]([Math]::Max(2, $Sz * 0.07))
        $scrPath = New-RoundedPath $px $py $pw $ph $pr

        $scrBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 4, 9, 20))
        $g.FillPath($scrBrush, $scrPath)
        $scrBrush.Dispose()

        $bW   = [float]([Math]::Max(1.0, $Sz * 0.042))
        $bPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(210, 135, 152, 190), $bW)
        $g.DrawPath($bPen, $scrPath)
        $bPen.Dispose()
        $scrPath.Dispose()

        # wave points
        $wx1   = $px + [int]($pw * 0.09)
        $wx2   = $px + [int]($pw * 0.91)
        $wMid  = $py + [int]($ph * 0.50)
        $amp   = [int]($ph * 0.31)
        $steps = [Math]::Max(20, $Sz)

        $pts = [System.Collections.Generic.List[System.Drawing.PointF]]::new()
        for ($i = 0; $i -le $steps; $i++) {
            $t   = $i / [double]$steps
            $ptX = [float]($wx1 + ($wx2 - $wx1) * $t)
            $ptY = [float]($wMid - [Math]::Sin($t * [Math]::PI * 2.7 - 0.35) * $amp)
            $pts.Add([System.Drawing.PointF]::new($ptX, $ptY))
        }
        $ptsArr = $pts.ToArray()

        # glow (64px+)
        if ($Sz -ge 64) {
            $glowW = @([float]($Sz * 0.17), [float]($Sz * 0.09), [float]($Sz * 0.045))
            $glowA = @(22, 50, 120)
            for ($gi = 0; $gi -lt 3; $gi++) {
                $gc = [System.Drawing.Color]::FromArgb($glowA[$gi], 42, 175, 255)
                $gp = New-Object System.Drawing.Pen($gc, $glowW[$gi])
                $gp.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
                $g.DrawCurve($gp, $ptsArr, [float]0.4)
                $gp.Dispose()
            }
        }

        # core line
        $coreW   = [float]([Math]::Max(1.2, $Sz * 0.032))
        $corePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 150, 215, 255), $coreW)
        $corePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $g.DrawCurve($corePen, $ptsArr, [float]0.4)
        $corePen.Dispose()

        # stand + base (48px+)
        if ($Sz -ge 48) {
            $stBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(160, 125, 140, 172))

            $stX = [int]($Sz * 0.455)
            $stY = $py + $ph
            $stW = [int]($Sz * 0.090)
            $stH = [int]($Sz * 0.105)
            $g.FillRectangle($stBrush, $stX, $stY, $stW, $stH)

            $bsW   = [int]($Sz * 0.40)
            $bsH   = [int]($Sz * 0.055)
            $bsX   = [int](($Sz - $bsW) / 2)
            $bsY   = $stY + $stH
            $bsPath = New-RoundedPath $bsX $bsY $bsW $bsH ([int]($bsH / 2))
            $g.FillPath($stBrush, $bsPath)
            $bsPath.Dispose()
            $stBrush.Dispose()
        }
    }
    else {
        # 16px: wave only
        $pts16 = [System.Collections.Generic.List[System.Drawing.PointF]]::new()
        $mid16 = $Sz / 2.0
        $amp16 = $Sz * 0.29
        for ($i = 0; $i -le 14; $i++) {
            $t   = $i / 14.0
            $ptX = [float](2 + ($Sz - 4) * $t)
            $ptY = [float]($mid16 - [Math]::Sin($t * [Math]::PI * 2.7 - 0.35) * $amp16)
            $pts16.Add([System.Drawing.PointF]::new($ptX, $ptY))
        }
        $wPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 65, 190, 255), [float]1.5)
        $wPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $g.DrawCurve($wPen, $pts16.ToArray(), [float]0.4)
        $wPen.Dispose()
    }

    $g.Dispose()
    return $bmp
}

# --- Build ICO (PNG-in-ICO, Vista+) ---

$sizes = @(16, 32, 48, 256)
$pngs  = @()

foreach ($sz in $sizes) {
    Write-Host "  Rendering ${sz}x${sz}..."
    $bmp = New-SSMBitmap $sz
    $ms  = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngs += , $ms.ToArray()
    $ms.Dispose()
}

$outDir  = "src\SSM\Assets\Icons"
$outPath = "$outDir\app.ico"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$fs = [System.IO.File]::Open($outPath, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)

$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$sizes.Count)

$dataOffset = [uint32](6 + 16 * $sizes.Count)
$offsets    = @()
$o          = $dataOffset
foreach ($png in $pngs) {
    $offsets += $o
    $o += [uint32]$png.Length
}

for ($i = 0; $i -lt $sizes.Count; $i++) {
    if ($sizes[$i] -eq 256) { $dim = [byte]0 } else { $dim = [byte]$sizes[$i] }
    $bw.Write($dim)
    $bw.Write($dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length)
    $bw.Write([uint32]$offsets[$i])
}

foreach ($png in $pngs) {
    $bw.Write($png)
}

$bw.Close()
$fs.Close()

Write-Host "Done: $outPath"
