$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$workspace = 'C:\Users\user\Desktop\CapstoneProject'
$outputDir = $PSScriptRoot
$bitmap = New-Object System.Drawing.Bitmap 1440,960
$g = [System.Drawing.Graphics]::FromImage($bitmap)
$g.Clear([System.Drawing.Color]::FromArgb(22,26,35))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$fontCollection = New-Object System.Drawing.Text.PrivateFontCollection
$fontCollection.AddFontFile((Join-Path $workspace 'Assets\_Project\Data\Localization\Fonts\Galmuri\Galmuri9.ttf'))
$family = $fontCollection.Families[0]
$skin = [System.Drawing.Image]::FromFile((Join-Path $workspace 'Assets\_Project\Art\Sprites\UI\ApprovedUiSkin\RewardPopupSkin.png'))
function Draw-Part($img,[int]$sx,[int]$sy,[int]$sw,[int]$sh,[int]$dx,[int]$dy,[int]$dw,[int]$dh) {
    $dst = New-Object System.Drawing.Rectangle $dx,$dy,$dw,$dh
    $src = New-Object System.Drawing.Rectangle $sx,$sy,$sw,$sh
    $g.DrawImage($img,$dst,$src,[System.Drawing.GraphicsUnit]::Pixel)
}
function Draw-NineSlice([int]$sx,[int]$sy,[int]$sw,[int]$sh,[int]$border,[int]$dx,[int]$dy,[int]$dw,[int]$dh,[int]$corner) {
    $xs = @($sx,($sx+$border),($sx+$sw-$border))
    $ys = @($sy,($sy+$border),($sy+$sh-$border))
    $ws = @($border,($sw-2*$border),$border)
    $hs = @($border,($sh-2*$border),$border)
    $ox = @($dx,($dx+$corner),($dx+$dw-$corner))
    $oy = @($dy,($dy+$corner),($dy+$dh-$corner))
    $ow = @($corner,($dw-2*$corner),$corner)
    $oh = @($corner,($dh-2*$corner),$corner)
    for($row=0;$row -lt 3;$row++) {
        for($col=0;$col -lt 3;$col++) {
            Draw-Part $skin $xs[$col] $ys[$row] $ws[$col] $hs[$row] $ox[$col] $oy[$row] $ow[$col] $oh[$row]
        }
    }
}
function Draw-Centered([string]$value,[int]$y,[int]$size,[string]$hex) {
    $font = New-Object System.Drawing.Font $family,$size,([System.Drawing.FontStyle]::Regular),([System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($hex))
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $rect = New-Object System.Drawing.RectangleF 50,$y,1340,90
    $g.DrawString($value,$font,$brush,$rect,$format)
    $font.Dispose(); $brush.Dispose(); $format.Dispose()
}
Draw-Centered '스피리 · 호감도 2단계 보상' 48 32 '#EBD080'
Draw-Centered '기존 보상 창 구성에 맞춘 시안' 98 20 '#AEB7C9'
# Existing RewardPopupSkin sprite rectangles: Unity origin is bottom-left.
Draw-NineSlice 0 0 128 128 32 240 174 960 626 48
Draw-Part $skin 136 0 48 20 672 162 96 40
Draw-Centered '호감도 보상!' 246 43 '#FFFFFF'
$icons = @('Icon_FireDragonFang','Icon_SunFragment','Icon_DrunkenRush')
$centers = @(520,720,920)
$iconInfo = @()
for($i=0;$i -lt 3;$i++) {
    $x = $centers[$i]-77
    Draw-NineSlice 136 28 48 48 12 $x 362 154 154 24
    $path = Join-Path $workspace ('Assets\_Project\Art\Sprites\Items\RelicIcon\'+$icons[$i]+'.png')
    $icon = [System.Drawing.Image]::FromFile($path)
    $iconInfo += @{ path=$path; width=$icon.Width; height=$icon.Height }
    # Match UnlockSlotUI: 80px icon inside a 96px slot, preserving its aspect.
    $scale = [Math]::Min(128.0/$icon.Width,128.0/$icon.Height)
    $w = [int]($icon.Width*$scale); $h = [int]($icon.Height*$scale)
    Draw-Part $icon 0 0 $icon.Width $icon.Height ($centers[$i]-[int]($w/2)) (439-[int]($h/2)) $w $h
    $icon.Dispose()
}
Draw-Centered '- 화룡의 송곳니, 태양의 파편, 독한 한잔이 해금되었습니다.' 565 22 '#FFFFFF'
Draw-Centered '이제 상자와 상점에서 발견할 수 있습니다.' 611 24 '#FFFFFF'
Draw-NineSlice 192 28 48 32 6 610 692 220 62 12
Draw-Centered '확인' 706 26 '#FFFFFF'
Draw-Centered '세 아이콘은 각각 유물 툴팁에 연결됩니다.' 848 23 '#C8CEDB'
Draw-Centered '실제 아이콘·보상 창 스킨 사용 / 설명 문구 제안 / 게임 화면 캡처 아님' 896 18 '#9EA7B9'
$bitmap.Save((Join-Path $outputDir 'spiri-tier-two-reward-preview.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$iconInfo | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputDir 'icon-sources.json') -Encoding UTF8
$skin.Dispose(); $g.Dispose(); $bitmap.Dispose(); $fontCollection.Dispose()
Write-Output 'Preview PNG rendered.'
