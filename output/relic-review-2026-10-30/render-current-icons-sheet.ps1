$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root='C:/Users/user/Desktop/CapstoneProject'
$outDir=Join-Path $root 'output/relic-review-2026-10-30'
$data=Get-Content -LiteralPath (Join-Path $outDir 'current-icons-sheet-data.json') -Raw | ConvertFrom-Json
$output=Join-Path $outDir 'dash-electric-relics-current-2026-10-30.png'
$protected=@{}
foreach($r in $data) {
 foreach($p in @($r.source,($r.source+'.meta'),$r.definition,($r.definition+'.meta'))) {
  if(-not $protected.ContainsKey($p)) { $protected[$p]=(Get-FileHash -LiteralPath (Join-Path $root $p) -Algorithm SHA256).Hash }
 }
}
$bmp=[Drawing.Bitmap]::new(1260,1390)
$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.ColorTranslator]::FromHtml('#111827'))
$g.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
function Brush($hex) { return [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($hex)) }
$white=Brush '#F1F5F9'; $muted=Brush '#94A3B8'; $light=Brush '#CBD5E1'; $cyan=Brush '#7DD3FC'
$bg=Brush '#1E293B'; $checkA=Brush '#293649'; $checkB=Brush '#334155'
$line=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#475569'))
$titleFont=[Drawing.Font]::new('Malgun Gothic',36,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$headFont=[Drawing.Font]::new('Malgun Gothic',23,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$bodyFont=[Drawing.Font]::new('Malgun Gothic',16,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
$smallFont=[Drawing.Font]::new('Malgun Gothic',14,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
function RoundBox([int]$x,[int]$y,[int]$w,[int]$h) {
 $p=[Drawing.Drawing2D.GraphicsPath]::new()
 $p.AddArc($x,$y,24,24,180,90); $p.AddArc(($x+$w-24),$y,24,24,270,90)
 $p.AddArc(($x+$w-24),($y+$h-24),24,24,0,90); $p.AddArc($x,($y+$h-24),24,24,90,90); $p.CloseFigure()
 $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $g.FillPath($bg,$p); $g.DrawPath($line,$p); $p.Dispose()
 $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::None
}
$g.DrawString('마왕성 · 대쉬 / 전기 유물',$titleFont,$white,[Drawing.RectangleF]::new(38,22,1180,60))
$g.DrawString('현재 프로젝트 연결 기준 · 11종 / 개별 이미지 10개 · 2026-10-30',$bodyFont,$light,38,82)
$g.DrawString('교체 완료 6종 · 기존 유지 5종    |    원본 픽셀을 정수 배율로 확대',$bodyFont,$cyan,38,116)
for($i=0;$i -lt $data.Count;$i++) {
 $r=$data[$i]; $x=30+($i%3)*410; $y=164+[int][Math]::Floor($i/3.0)*294
 RoundBox $x $y 390 277
 $g.DrawString(('{0:00}  {1}' -f ($i+1),$r.name),$headFont,$white,($x+16),($y+12))
 $ix=$x+18; $iy=$y+53
 for($cy=0;$cy -lt 11;$cy++) { for($cx=0;$cx -lt 11;$cx++) {
  $brush=$checkA; if((($cx+$cy)%2) -eq 1){$brush=$checkB}
  $g.FillRectangle($brush,($ix+$cx*12),($iy+$cy*12),12,12)
 }}
 $src=[Drawing.Bitmap]::new((Join-Path $root $r.source))
 $sw=[int]$r.rect[2]; $sh=[int]$r.rect[3]
 $scale=[int][Math]::Floor(132.0/[Math]::Max($sw,$sh))
 $dw=$sw*$scale; $dh=$sh*$scale
 $dest=[Drawing.Rectangle]::new(($ix+[int][Math]::Floor((132-$dw)/2)),($iy+[int][Math]::Floor((132-$dh)/2)),$dw,$dh)
 $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
 $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
 $g.DrawImage($src,$dest,[int]$r.rect[0],[int]$r.rect[1],$sw,$sh,[Drawing.GraphicsUnit]::Pixel)
 $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Default
 $src.Dispose()
 $status='기존 유지'; $statusBrush=$muted
 if($r.updated){$status='교체 완료';$statusBrush=$cyan}
 $g.DrawString($status,$bodyFont,$statusBrush,($x+168),($y+61))
 $labelRect=[Drawing.RectangleF]::new(($x+168),($y+91),205,54)
 $g.DrawString([string]$r.sprite,$smallFont,$light,$labelRect)
 $g.DrawString(('{0} × {1} px' -f $sw,$sh),$smallFont,$muted,($x+168),($y+151))
 $effectRect=[Drawing.RectangleF]::new(($x+16),($y+204),358,65)
 $g.DrawString([string]$r.effect,$bodyFont,$light,$effectRect)
}
$g.DrawString('보기용 정리 이미지 · 에셋 / 메타데이터 변경 없음 · GUID + fileID 대조',$bodyFont,$muted,38,1350)
$bmp.Save($output,[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()
foreach($p in $protected.Keys) { if((Get-FileHash -LiteralPath (Join-Path $root $p) -Algorithm SHA256).Hash -ne $protected[$p]) { throw ('Source changed: '+$p) } }
$proof=[PSCustomObject]@{Date='2026-10-30';Count=$data.Count;Replaced=@($data|Where-Object updated).Count;Retained=@($data|Where-Object {-not $_.updated}).Count;ProtectedSourceFiles=$protected.Count;SourceHashesPreserved=$true;NearestIntegerScaling=$true;ProjectAssetsChanged=$false;UnityVerification='not executed';DocImpact='no doc update needed';SessionLogs='unchanged';ErrorLog='unchanged';DecisionLog='unchanged';PresentationHTMLAffected=$false}
$proof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outDir 'current-icons-sheet-verification.json') -Encoding utf8
$proof | ConvertTo-Json

