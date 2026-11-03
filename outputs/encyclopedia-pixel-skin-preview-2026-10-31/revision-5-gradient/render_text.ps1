param([string]$OutputDir,[string]$ProjectDir)
Add-Type -AssemblyName System.Drawing
$fonts=New-Object System.Drawing.Text.PrivateFontCollection
$fonts.AddFontFile((Join-Path $ProjectDir 'Assets/_Project/Data/Localization/Fonts/Galmuri/Galmuri9.ttf'))
$format=[System.Drawing.StringFormat]::GenericTypographic.Clone()
$format.FormatFlags=[System.Drawing.StringFormatFlags]::NoWrap
$checks=New-Object System.Collections.Generic.List[string]
function New-Canvas {
 $script:canvas=New-Object System.Drawing.Bitmap(1920,1080,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
 $script:graphics=[System.Drawing.Graphics]::FromImage($canvas)
 $graphics.Clear([System.Drawing.Color]::Transparent)
 $graphics.TextRenderingHint=[System.Drawing.Text.TextRenderingHint]::SingleBitPerPixelGridFit
}
function Text([string]$Value,[int]$X,[int]$Y,[int]$Size=18,[string]$Color='412B22',[string]$Align='Left',[int]$MaxWidth=450) {
 $font=New-Object System.Drawing.Font($fonts.Families[0],$Size,[System.Drawing.FontStyle]::Regular,[System.Drawing.GraphicsUnit]::Pixel)
 $brush=New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#'+$Color))
 $width=[int][Math]::Ceiling($graphics.MeasureString($Value,$font,2000,$format).Width)
 if($width -gt $MaxWidth) {throw "Text overflow: $Value ($width > $MaxWidth)"}
 if($Align -eq 'Center') {$X-=[int][Math]::Floor($width/2)}
 if($Align -eq 'Right') {$X-=$width}
 $graphics.DrawString($Value,$font,$brush,[single]$X,[single]$Y,$format)
 $checks.Add("PASS text width $width/$MaxWidth : $Value")
 $brush.Dispose();$font.Dispose()
}
function Save-Canvas([string]$Name) {
 $canvas.Save((Join-Path $OutputDir $Name),[System.Drawing.Imaging.ImageFormat]::Png)
 $graphics.Dispose();$canvas.Dispose()
}
New-Canvas
Text '몬스터' 688 294 27 '412B22' 'Center'
Text 'Beer Monster' 1233 294 27 '412B22' 'Center'
$labels=@(@('Beer','Monster'),@('Treasure','Monster'),@('Frog'),@('Frog'),@('Pawn'),@('Rook'),@('Knight'),@('Bishop'),@('???'),@('???'),@('???'),@('???'))
for($i=0;$i -lt 12;$i++) {
 $x=520+($i%4)*112;$y=350+[int][Math]::Floor($i/4)*150+102
 $lines=$labels[$i]
 for($j=0;$j -lt $lines.Count;$j++) {
  Text $lines[$j] $x ($y+$(if($lines.Count -gt 1){3+$j*13}else{8})) 12 'FFF0C9' 'Center' 90
 }
}
Text '누적 처치 수 0회' 1229 575 18 '412B22' 'Center' 222
Text '설명' 1024 644 18
Text '술기운에 잠식된' 1024 680 18
Text '하급 몬스터입니다.' 1024 708 18
Text '1 / 2' 688 824 18 '412B22' 'Center' 100
Text '평면 픽셀 UI + 검은 그라데이션 · 이름 패널 제거 / 미조우 실루엣' 960 938 12 'F8D9A7' 'Center' 1000
Save-Canvas 'monster-text.png'
New-Canvas
Text '보스' 688 294 27 '412B22' 'Center'
Text '멜타' 576 516 18 'FFF0C9' 'Center' 180
Text '클로에' 800 516 18 'FFF0C9' 'Center' 180
Text '???' 576 766 18 'FFF0C9' 'Center' 180
Text '???' 800 766 18 'FFF0C9' 'Center' 180
Text '멜타' 1233 305 18 'FFF0C9' 'Center' 164
Text '현재 호감도' 1024 609 18 '412B22' 'Left' 200
Text '호감도 보상' 1024 656 18
Text '호감도 5' 1094 696 18 '412B22' 'Left' 168
Text '미해금' 1426 698 12 '6F4932' 'Right' 80
Text '특별 NPC 이용 가능' 1094 728 18 '412B22' 'Left' 326
Text '설명 · 스토리' 1024 804 18
Text '슬라임 루트의 여왕 보스.' 1024 834 18 '412B22' 'Left' 420
Text '1 / 1' 688 824 18 '412B22' 'Center' 100
Text '평면 픽셀 UI + 검은 그라데이션 · 이름 패널 제거 / 일러스트 표시 최대 5개' 960 938 12 'F8D9A7' 'Center' 1000
Save-Canvas 'boss-text.png'
$checks | Set-Content -LiteralPath (Join-Path $OutputDir 'text-validation.txt') -Encoding UTF8
$format.Dispose();$fonts.Dispose()
Write-Output 'PASS text overlays and measured label bounds.'
