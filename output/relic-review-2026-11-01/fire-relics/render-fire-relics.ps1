$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root='C:/Users/user/Desktop/CapstoneProject'
$outDir=Join-Path $root 'output/relic-review-2026-11-01/fire-relics'
$data=Get-Content -LiteralPath (Join-Path $outDir 'fire-relics-data.json') -Raw | ConvertFrom-Json
$output=Join-Path $outDir 'fire-burn-relics-current-2026-11-01.png'
$protected=@{}
foreach($r in $data) {
 foreach($p in @($r.source,($r.source+'.meta'),$r.definition,($r.definition+'.meta'),$r.logic,($r.logic+'.meta'))) {
  if(-not $protected.ContainsKey($p)) { $protected[$p]=(Get-FileHash -LiteralPath (Join-Path $root $p) -Algorithm SHA256).Hash }
 }
}
foreach($p in @('DataSheets/Localization/GameText.csv','Assets/_Project/Data/Localization/GameText_ko.asset','Assets/_Project/Data/Localization/GameText Shared Data.asset','Assets/_Project/Runtime/Core/Presentation/GameTextAssetKeys.g.cs')){$protected[$p]=(Get-FileHash -LiteralPath (Join-Path $root $p) -Algorithm SHA256).Hash}
foreach($p in @('Assets/_Project/Data/Items/ItemDatabase.asset','Assets/_Project/Data/Items/ItemDatabase.asset.meta')){$protected[$p]=(Get-FileHash -LiteralPath (Join-Path $root $p) -Algorithm SHA256).Hash}
[IO.Directory]::CreateDirectory((Join-Path $outDir 'icons'))|Out-Null
$bmp=[Drawing.Bitmap]::new(1260,1390)
$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.ColorTranslator]::FromHtml('#111827'))
$g.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
function Brush($hex) { return [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($hex)) }
$white=Brush '#F1F5F9'; $muted=Brush '#94A3B8'; $light=Brush '#CBD5E1'; $cyan=Brush '#FDBA74'
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
$g.DrawString('마왕성 · 화염 / 화상 유물',$titleFont,$white,[Drawing.RectangleF]::new(38,22,1180,60))
$g.DrawString('현재 프로젝트 연결 기준 · 10종 / 개별 이미지 9개 · 2026-11-01',$bodyFont,$light,38,82)
$g.DrawString('일반 화염·화상 8종 · 무기 전용 2종    |    원본 픽셀을 정수 배율로 확대',$bodyFont,$cyan,38,116)
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
 $native=$src.Clone([Drawing.Rectangle]::new([int]$r.rect[0],[int]$r.rect[1],$sw,$sh),[Drawing.Imaging.PixelFormat]::Format32bppArgb)
 $native.Save((Join-Path $outDir $r.nativeFile),[Drawing.Imaging.ImageFormat]::Png)
 $native.Dispose()
 $scale=[int][Math]::Floor(132.0/[Math]::Max($sw,$sh))
 $dw=$sw*$scale; $dh=$sh*$scale
 $dest=[Drawing.Rectangle]::new(($ix+[int][Math]::Floor((132-$dw)/2)),($iy+[int][Math]::Floor((132-$dh)/2)),$dw,$dh)
 $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
 $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
 $g.DrawImage($src,$dest,[int]$r.rect[0],[int]$r.rect[1],$sw,$sh,[Drawing.GraphicsUnit]::Pixel)
 $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Default
 $src.Dispose()
 $status='일반 화염 · 화상'; $statusBrush=$muted
 if($r.category -eq '무기 전용'){$status='홍련의 한계선 전용';$statusBrush=$cyan}
 $g.DrawString($status,$bodyFont,$statusBrush,($x+168),($y+61))
 $labelRect=[Drawing.RectangleF]::new(($x+168),($y+91),205,54)
 $g.DrawString([string]$r.sprite,$smallFont,$light,$labelRect)
 $g.DrawString(('{0} × {1} px' -f $sw,$sh),$smallFont,$muted,($x+168),($y+151))
 $effectRect=[Drawing.RectangleF]::new(($x+16),($y+204),358,65)
 $g.DrawString([string]$r.effect,$bodyFont,$light,$effectRect)
}
RoundBox 440 1046 390 277
$g.DrawString('이미지 공유 현황',$headFont,$white,[Drawing.RectangleF]::new(456,1060,358,34))
$sharing=@('일반 8종: 서로 다른 스프라이트','원본 시트: Flame Artifact.png','무기 전용 2종: FireStaff_0 공유','현재 10종 / 개별 이미지 9개')
for($j=0;$j -lt $sharing.Count;$j++){$g.DrawString($sharing[$j],$bodyFont,$light,[Drawing.RectangleF]::new(456,(1110+$j*37),358,32))}
RoundBox 850 1046 390 277
$g.DrawString('범위와 확인 기준',$headFont,$white,[Drawing.RectangleF]::new(866,1060,358,34))
$notes=@('01–08 일반 화염·화상 유물','09–10 홍련의 한계선 전용 유물','ItemDatabase 등록 10/10 확인','PNG·meta의 GUID / fileID 대조','원본 픽셀 정수 배율 확대','프로젝트 파일 수정 없음')
for($j=0;$j -lt $notes.Count;$j++){$g.DrawString($notes[$j],$bodyFont,$light,[Drawing.RectangleF]::new(866,(1107+$j*30),358,28))}
$g.DrawString('보기용 정리 이미지 · 현재 유물 정의의 효과 요약 · 프로젝트 변경 없음 · GUID + fileID 대조',$bodyFont,$muted,38,1350)
$bmp.Save($output,[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()
foreach($p in $protected.Keys) { if((Get-FileHash -LiteralPath (Join-Path $root $p) -Algorithm SHA256).Hash -ne $protected[$p]) { throw ('Source changed: '+$p) } }
$proof=[PSCustomObject]@{Date='2026-11-01';Count=$data.Count;UniqueIcons=@($data|ForEach-Object{$_.guid+':'+$_.fileID}|Sort-Object -Unique).Count;GeneralRelics=@($data|Where-Object {$_.category -eq '일반 화염'}).Count;WeaponExclusiveRelics=@($data|Where-Object {$_.category -eq '무기 전용'}).Count;DisplayNamesVerified=@($data|Where-Object {-not $_.displayNamesVerified}).Count -eq 0;DatabaseRegistrationVerified=@($data|Where-Object {-not $_.registeredInItemDatabase}).Count -eq 0;ProtectedSourceFiles=$protected.Count;SourceHashesPreserved=$true;NearestIntegerScaling=$true;NativeSpriteExports=$data.Count;ProjectAssetsChanged=$false;UnityVerification='not executed';DocImpact='no doc update needed';SessionLogs='unchanged';ErrorLog='unchanged';DecisionLog='unchanged';PresentationHTMLAffected=$false}
$proof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outDir 'fire-relics-verification.json') -Encoding utf8
$proof | ConvertTo-Json
