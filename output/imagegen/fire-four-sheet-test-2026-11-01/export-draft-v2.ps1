# Technical sprite export only; illustration created with built-in image_gen.
# Preserve the four generated silhouettes with proportional nearest sampling,
# enforce palette/alpha, add native 2px charcoal contours, and pack unchanged frames.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$sourcePath = 'C:/Users/user/.codex/generated_images/01a17b9a-dd0d-7952-934c-baa8ac291ab7/exec-1a56b6f5-a03e-4a83-b0fb-f10b5a641812.png'
$outputDir = $PSScriptRoot
$source = [System.Drawing.Bitmap]::new($sourcePath)
$charcoal = [System.Drawing.ColorTranslator]::FromHtml('#363232')
$items = @(
    @{ File='01-burning-feather-draft-v2-32x32.png'; Crop=@(113,117,407,475); Palette=@('#363232','#A75064','#E55C5C','#E09656','#EECF5A') },
    @{ File='02-melted-bell-draft-v2-32x32.png'; Crop=@(711,114,447,490); Palette=@('#363232','#8599B0','#C2C2C2','#E55C5C','#E09656','#EECF5A') },
    @{ File='03-smoky-incense-draft-v2-32x32.png'; Crop=@(168,687,321,474); Palette=@('#363232','#8599B0','#C2C2C2','#E55C5C','#E09656','#EECF5A') },
    @{ File='04-crimson-crown-draft-v2-32x32.png'; Crop=@(696,820,478,299); Palette=@('#363232','#A75064','#E55C5C','#E09656','#EECF5A') }
)
function Save-NewPng([System.Drawing.Bitmap]$Bitmap,[string]$Name) {
    $target = Join-Path $outputDir $Name
    if (Test-Path -LiteralPath $target) { throw "Refusing to overwrite: $target" }
    $Bitmap.Save($target,[System.Drawing.Imaging.ImageFormat]::Png)
}
function Save-NearestPreview([System.Drawing.Bitmap]$Bitmap,[int]$Factor,[string]$Name) {
    $preview = [System.Drawing.Bitmap]::new($Bitmap.Width*$Factor,$Bitmap.Height*$Factor,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        for ($y=0; $y -lt $preview.Height; $y++) {
            for ($x=0; $x -lt $preview.Width; $x++) {
                $preview.SetPixel($x,$y,$Bitmap.GetPixel([int][math]::Floor($x/$Factor),[int][math]::Floor($y/$Factor)))
            }
        }
        Save-NewPng $preview $Name
    } finally { $preview.Dispose() }
}
$frames = [System.Collections.Generic.List[System.Drawing.Bitmap]]::new()
try {
    foreach ($item in $items) {
        $bitmap = [System.Drawing.Bitmap]::new(32,32,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $colors = @($item.Palette | ForEach-Object { [System.Drawing.ColorTranslator]::FromHtml($_) })
        $crop = $item.Crop
        $factor = 24.0 / [math]::Max($crop[2],$crop[3])
        $dw = [int][math]::Round($crop[2]*$factor)
        $dh = [int][math]::Round($crop[3]*$factor)
        $ox = [int][math]::Floor((32-$dw)/2.0)
        $oy = [int][math]::Floor((32-$dh)/2.0)
        $material = [System.Collections.Generic.List[object]]::new()
        for ($dy=0; $dy -lt $dh; $dy++) {
            for ($dx=0; $dx -lt $dw; $dx++) {
                $sx = $crop[0] + [int][math]::Floor(($dx+0.5)*$crop[2]/$dw)
                $sy = $crop[1] + [int][math]::Floor(($dy+0.5)*$crop[3]/$dh)
                $sample = $source.GetPixel($sx,$sy)
                if ($sample.A -lt 200) { continue }
                $best = 0
                $distance = [double]::PositiveInfinity
                for ($j=0; $j -lt $colors.Count; $j++) {
                    $c = $colors[$j]
                    $d = [math]::Pow($sample.R-$c.R,2)+[math]::Pow($sample.G-$c.G,2)+[math]::Pow($sample.B-$c.B,2)
                    if ($d -lt $distance) { $best=$j; $distance=$d }
                }
                if ($best -gt 0) {
                    $material.Add(@{X=$ox+$dx; Y=$oy+$dy; Color=$colors[$best]})
                }
            }
        }
        foreach ($p in $material) {
            for ($dy=-2; $dy -le 2; $dy++) {
                for ($dx=-2; $dx -le 2; $dx++) {
                    $x=$p.X+$dx; $y=$p.Y+$dy
                    if ($x -ge 0 -and $x -lt 32 -and $y -ge 0 -and $y -lt 32) { $bitmap.SetPixel($x,$y,$charcoal) }
                }
            }
        }
        foreach ($p in $material) { $bitmap.SetPixel($p.X,$p.Y,$p.Color) }
        Save-NewPng $bitmap $item.File
        $frames.Add($bitmap)
    }
    $sheet = [System.Drawing.Bitmap]::new(32,128,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $grid = [System.Drawing.Bitmap]::new(64,64,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        for ($i=0; $i -lt $frames.Count; $i++) {
            $gx=($i%2)*32
            $gy=[int][math]::Floor($i/2.0)*32
            for ($y=0; $y -lt 32; $y++) {
                for ($x=0; $x -lt 32; $x++) {
                    $c=$frames[$i].GetPixel($x,$y)
                    $sheet.SetPixel($x,$i*32+$y,$c)
                    $grid.SetPixel($gx+$x,$gy+$y,$c)
                }
            }
        }
        Save-NewPng $sheet 'fire-four-draft-v2-32x128.png'
        Save-NewPng $grid 'fire-four-draft-v2-2x2-64x64.png'
        Save-NearestPreview $sheet 8 'fire-four-draft-v2-vertical-preview.png'
        Save-NearestPreview $grid 8 'fire-four-draft-v2-2x2-preview.png'
    } finally {
        $sheet.Dispose()
        $grid.Dispose()
    }
} finally {
    foreach ($frame in $frames) { $frame.Dispose() }
    $source.Dispose()
}
Write-Output 'Exported four native 32x32 frames, 32x128 strip and lossless nearest previews.'

