$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root='C:/Users/user/Desktop/CapstoneProject'
$outDir=$PSScriptRoot
$data=Get-Content -LiteralPath (Join-Path $outDir 'page-02-data.json') -Raw | ConvertFrom-Json
$output=Join-Path $outDir 'unremade-02.png'
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
$g.DrawString('마왕성 · 다음 리메이크 검토 2/5',$titleFont,$white,[Drawing.RectangleF]::new(38,22,1180,60))
$g.DrawString('현재 등록 유물 75종 · 이번 대화 교체 20종 제외 · 남은 55종 · 2026-11-04',$bodyFont,$light,38,82)
$g.DrawString('현재 이름·이미지·효과 요약    |    기존 전용 PNG도 포함 · 원본 정수 배율 확대',$bodyFont,$cyan,38,116)
for($i=0;$i -lt $data.Count;$i++) {
 $r=$data[$i]; $x=30+($i%3)*410; $y=164+[int][Math]::Floor($i/3.0)*294
 RoundBox $x $y 390 277
 $cardTitle=('{0:00}  {1}' -f ([int]$r.number),$r.name)
 $cardFont=$headFont
 if($g.MeasureString($cardTitle,$headFont).Width -gt 356){$cardFont=[Drawing.Font]::new('Malgun Gothic',17,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)}
 $g.DrawString($cardTitle,$cardFont,$white,($x+16),($y+12))
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
 $native=$src.Clone([Drawing.Rectangle]::new([int]$r.rect[0],[int]$r.rect[1],$sw,$sh),[Drawing.Imaging.PixelFormat]::Format32bppArgb)
 $native.Save((Join-Path $outDir $r.nativeFile),[Drawing.Imaging.ImageFormat]::Png)
 $native.Dispose()
 $src.Dispose()
 $status='기존 이미지'; $statusBrush=$muted
 if($r.updated){$status='교체 완료';$statusBrush=$cyan}
 $g.DrawString($status,$bodyFont,$statusBrush,($x+168),($y+61))
 $labelRect=[Drawing.RectangleF]::new(($x+168),($y+91),205,54)
 $g.DrawString([string]$r.sprite,$smallFont,$light,$labelRect)
 $g.DrawString(('{0} × {1} px' -f $sw,$sh),$smallFont,$muted,($x+168),($y+151))
 $effectRect=[Drawing.RectangleF]::new(($x+16),($y+204),358,65)
 $g.DrawString([string]$r.effect,$bodyFont,$light,$effectRect)
}
$g.DrawString('보기용 자료 · 이 대화의 교체 완료 20종 기준 · 프로젝트 에셋과 문서 변경 없음',$bodyFont,$muted,38,1350)
$bmp.Save($output,[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()
