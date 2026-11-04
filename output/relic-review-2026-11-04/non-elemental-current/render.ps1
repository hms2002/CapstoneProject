$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root='C:/Users/user/Desktop/CapstoneProject'
$outDir=$PSScriptRoot
$pages=Get-Content -LiteralPath (Join-Path $outDir 'pages.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach($page in $pages) {
$data=$page.items
$output=Join-Path $outDir ($page.stem+'.png')
$height=164+([int]$page.rows)*294+50
$bmp=[Drawing.Bitmap]::new(1260,$height)
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
$g.DrawString([string]$page.title,$titleFont,$white,[Drawing.RectangleF]::new(38,22,1180,60))
$g.DrawString([string]$page.subtitle,$bodyFont,$light,38,82)
$g.DrawString([string]$page.hint,$bodyFont,$cyan,38,116)
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
 if($sw -eq $src.Width -and $sh -eq $src.Height -and [int]$r.rect[0] -eq 0 -and [int]$r.rect[1] -eq 0) {
 [IO.File]::WriteAllBytes((Join-Path $outDir $r.nativeFile),[IO.File]::ReadAllBytes((Join-Path $root $r.source)))
 } else { $native.Save((Join-Path $outDir $r.nativeFile),[Drawing.Imaging.ImageFormat]::Png) }
 $native.Dispose()
 $src.Dispose()
 $status='기존 이미지'; $statusBrush=$muted
 if($r.updated){$status='교체 완료';$statusBrush=$cyan}
 $g.DrawString($status,$bodyFont,$statusBrush,($x+168),($y+61))
 $labelRect=[Drawing.RectangleF]::new(($x+168),($y+91),205,54)
 $g.DrawString([string]$r.sprite,$smallFont,$light,$labelRect)
 $g.DrawString(('{0} × {1} px' -f $sw,$sh),$smallFont,$muted,($x+168),($y+151))
 $effectRect=[Drawing.RectangleF]::new(($x+16),($y+204),358,65)
 $effectFormat=[Drawing.StringFormat]::new()
 $effectFormat.Trimming=[Drawing.StringTrimming]::EllipsisCharacter
 $effectFormat.FormatFlags=[Drawing.StringFormatFlags]::LineLimit
 $g.DrawString([string]$r.effect,$bodyFont,$light,$effectRect,$effectFormat)
 $effectFormat.Dispose()
}
$g.DrawString('보기용 자료 · 긴 효과는 요약 · 전체 설명은 README 참조 · 최신 설명 기준',$bodyFont,$muted,38,($height-36))
$bmp.Save($output,[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()

}
