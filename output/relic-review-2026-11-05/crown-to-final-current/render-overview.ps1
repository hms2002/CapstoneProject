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
$taskG.DrawString('마왕성 · 최근 교체 유물 26종',$taskTitle,$taskWhite,30,22)
$taskG.DrawString('부서진 왕관 적용 묶음부터 마지막 5종까지 · 현재 프로젝트 연결 기준 · 2026-11-05',$taskBody,$taskMuted,32,82)
$taskG.DrawString('적용 순서대로 왼쪽 → 오른쪽    |    32 × 32 원본을 4배 확대    |    효과는 상세 카드 3장에 수록',$taskBody,$taskCyan,32,116)
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
 $taskG.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor;$taskG.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
 $taskG.DrawImage($src,[Drawing.Rectangle]::new($ix,$iy,128,128),0,0,32,32,[Drawing.GraphicsUnit]::Pixel)
 $taskG.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Default;$src.Dispose()
 $taskG.DrawString([string]$r.name,$taskName,$taskWhite,[Drawing.RectangleF]::new(($x+8),($y+154),224,52),$taskFormat)
}
$taskG.DrawString('보기용 자료 · 게임 에셋 변경 없음 · 현재 이름과 전체 효과 설명은 README 참조',$taskBody,$taskMuted,30,1298)
$taskBitmap.Save((Join-Path $taskOut 'overview-26.png'),[Drawing.Imaging.ImageFormat]::Png)
$taskFormat.Dispose();$taskG.Dispose();$taskBitmap.Dispose()
Write-Output 'Saved overview for 26 relics.'
