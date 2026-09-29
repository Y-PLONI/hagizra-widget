# One-off generator for the package images (run again only to change the artwork).
Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot '..\Package\ProviderAssets'
New-Item -ItemType Directory -Force $out | Out-Null

function New-Canvas([int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    return $bmp, $g
}

function Get-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Save-Icon([string]$name, [int]$w, [int]$h) {
    $bmp, $g = New-Canvas $w $h
    $size = [Math]::Min($w, $h)
    $pad = $size * 0.06
    $box = $size - 2 * $pad
    $x = ($w - $box) / 2
    $y = ($h - $box) / 2
    $path = Get-RoundedPath $x $y $box $box ($box * 0.22)
    $rect = New-Object System.Drawing.RectangleF $x, $y, $box, $box
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 30, 58, 95)), ([System.Drawing.Color]::FromArgb(255, 14, 26, 46)), 60
    $g.FillPath($brush, $path)

    $font = New-Object System.Drawing.Font 'Segoe UI', ($box * 0.56), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = 'Center'
    $fmt.LineAlignment = 'Center'
    $textRect = New-Object System.Drawing.RectangleF $x, ($y + $box * 0.03), $box, $box
    $g.DrawString([string][char]0x05D2, $font, [System.Drawing.Brushes]::White, $textRect, $fmt)

    $dot = $box * 0.17
    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 229, 57, 53))), ($x + $box * 0.70), ($y + $box * 0.10), $dot, $dot)

    $bmp.Save((Join-Path $out $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}

Save-Icon 'StoreLogo.png' 50 50
Save-Icon 'Square44x44Logo.png' 44 44
Save-Icon 'Square150x150Logo.png' 150 150
Save-Icon 'Wide310x150Logo.png' 310 150
Save-Icon 'WidgetIcon.png' 256 256

# Widget picker preview: the medium card, 300x304 with transparent rounded corners, as the picker requires.
$bmp, $g = New-Canvas 300 304
$g.Clear([System.Drawing.Color]::Transparent)
$card = Get-RoundedPath 0 0 300 304 12
$g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 32, 32, 32))), $card)
$fmt = New-Object System.Drawing.StringFormat
$fmt.FormatFlags = 'DirectionRightToLeft'
$fmt.Alignment = 'Near'
$bold = New-Object System.Drawing.Font 'Segoe UI', 15, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$reg = New-Object System.Drawing.Font 'Segoe UI', 15, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
$lines = @(
    @('11:11', 'טיסת אל על למיאמי |', 'טיסה חלופית תמריא בשעה 16:50'),
    @('11:04', 'קמפיין בחירות |', 'ראש הממשלה הגיע כעת לאשדוד.'),
    @('10:52', 'שוב |', 'רשות הסחר הבריטית מדווחת על ספינה שנפגעה.')
)
$y = 22
foreach ($l in $lines) {
    $g.DrawString($l[0], $reg, [System.Drawing.Brushes]::Gray, (New-Object System.Drawing.RectangleF 16, $y, 268, 22), $fmt)
    $g.DrawString($l[1], $bold, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 16, $y, 224, 22), $fmt)
    $g.DrawString($l[2], $reg, [System.Drawing.Brushes]::Gainsboro, (New-Object System.Drawing.RectangleF 16, ($y + 22), 268, 40), $fmt)
    $y += 92
}
$bmp.Save((Join-Path $out 'WidgetPreview.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
