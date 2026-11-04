$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$taskOut=$PSScriptRoot
$taskData=@(Get-Content -LiteralPath (Join-Path $taskOut 'data.json') -Raw -Encoding UTF8 | ConvertFrom-Json)
$taskBitmap=[Drawing.Bitmap]::new(1560,1334)
$taskG=[Drawing.Graphics]::FromImage($taskBitmap)
$taskG.Clear([Drawing.ColorTranslator]::FromHtml('#111827'))
$taskG.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
function TaskBrush($hex) { [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($hex)) }
$taskWhite=TaskBrush '#F1F5F9'; $taskMuted=TaskBrush '#94A3B8'; $taskCyan=TaskBrush '#7DD3FC'; $taskCard=TaskBrush '#1E293B'; $taskCheckA=TaskBrush '#293649'; $taskCheckB=TaskBrush '#334155'
$taskPen=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#475569'))
$taskTitle=[Drawing.Font]::new('Malgun Gothic',36,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$taskBody=[Drawing.Font]::new('Malgun Gothic',18,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
$taskName=[Drawing.Font]::new('Malgun Gothic',20,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$taskSmall=[Drawing.Font]::new('Malgun Gothic',14,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
$taskG.DrawString('마왕성 · 변경 전 유물 26종',$taskTitle,$taskWhite,30,22)
$taskG.DrawString('변경 후 자료와 같은 26종 · 적용 전 이미지와 이름 · 작성 2026-11-05',$taskBody,$taskMuted,32,82)
$taskG.DrawString('변경 후와 같은 번호·배치    |    원래 크기 유지 · 정수 배율 확대    |    효과는 상세 카드 3장',$taskBody,$taskCyan,32,116)
$taskFormat=[Drawing.StringFormat]::new(); $taskFormat.Alignment=[Drawing.StringAlignment]::Center; $taskFormat.LineAlignment=[Drawing.StringAlignment]::Near
for($i=0;$i -lt $taskData.Count;$i++) {
 $r=$taskData[$i]; $col=$i%6; $row=[int][Math]::Floor($i/6.0)
 if($row -eq 4){$col+=2}
 $x=20+$col*256; $y=164+$row*224
 $taskPath=[Drawing.Drawing2D.GraphicsPath]::new()
 $taskPath.AddArc($x,$y,20,20,180,90); $taskPath.AddArc(($x+220),$y,20,20,270,90); $taskPath.AddArc(($x+220),($y+190),20,20,0,90); $taskPath.AddArc($x,($y+190),20,20,90,90); $taskPath.CloseFigure()
 $taskG.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias; $taskG.FillPath($taskCard,$taskPath);$taskG.DrawPath($taskPen,$taskPath);$taskG.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::None;$taskPath.Dispose()
 $taskG.DrawString(('{0:00}' -f [int]$r.number),$taskSmall,$taskMuted,($x+10),($y+8))
 $ix=$x+56;$iy=$y+16
 for($cy=0;$cy -lt 8;$cy++){for($cx=0;$cx -lt 8;$cx++){
  $brush=$taskCheckA;if((($cx+$cy)%2) -eq 1){$brush=$taskCheckB}
  $taskG.FillRectangle($brush,($ix+$cx*16),($iy+$cy*16),16,16)
 }}
 $src=[Drawing.Bitmap]::new((Join-Path $taskOut $r.nativeFile))
 $sw=$src.Width;$sh=$src.Height;$scale=[int][Math]::Floor(128.0/[Math]::Max($sw,$sh));$dw=$sw*$scale;$dh=$sh*$scale;$drawX=$ix+[int][Math]::Floor((128-$dw)/2);$drawY=$iy+[int][Math]::Floor((128-$dh)/2)
 $taskG.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor;$taskG.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
 $taskG.DrawImage($src,[Drawing.Rectangle]::new($drawX,$drawY,$dw,$dh),0,0,$sw,$sh,[Drawing.GraphicsUnit]::Pixel)
 $taskG.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Default;$src.Dispose()
 $taskG.DrawString([string]$r.name,$taskName,$taskWhite,[Drawing.RectangleF]::new(($x+8),($y+154),224,52),$taskFormat)
}
$taskG.DrawString('변경 전 보기용 자료 · 당시 백업·아이콘 사본 대조 · 현재 게임 에셋 변경 없음',$taskBody,$taskMuted,30,1298)
$taskBitmap.Save((Join-Path $taskOut 'overview-26.png'),[Drawing.Imaging.ImageFormat]::Png)
$taskFormat.Dispose();$taskG.Dispose();$taskBitmap.Dispose()
Write-Output 'Saved overview for 26 relics.'
