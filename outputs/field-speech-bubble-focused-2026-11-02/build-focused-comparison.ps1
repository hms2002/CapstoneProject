Add-Type -AssemblyName System.Drawing
$out=$PSScriptRoot
$source=Join-Path (Split-Path $out -Parent) 'field-speech-bubble-review-2026-11-02/native'
$title=[System.Drawing.Font]::new('Malgun Gothic',28,[System.Drawing.FontStyle]::Bold)
$heading=[System.Drawing.Font]::new('Malgun Gothic',20,[System.Drawing.FontStyle]::Bold)
$body=[System.Drawing.Font]::new('Malgun Gothic',15)
$small=[System.Drawing.Font]::new('Malgun Gothic',12)
$white=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(239,242,248))
$muted=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(174,185,203))
$accent=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(105,220,227))
$gold=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(246,198,111))
$mark=[System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(246,198,111),2)
$names=@{Default='기본 흰색 말풍선';Slime='슬라임 색상';Dragon='드래곤 색상';Witch='마녀 색상';DemonKing='마왕 색상'}

function New-Board([int]$w,[int]$h){
    $script:board=[System.Drawing.Bitmap]::new($w,$h)
    $script:g=[System.Drawing.Graphics]::FromImage($script:board)
    $script:g.Clear([System.Drawing.Color]::FromArgb(22,27,35))
    $script:g.TextRenderingHint=[System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $script:g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $script:g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
}
function Save-Board([string]$name){
    $script:board.Save((Join-Path $out $name),[System.Drawing.Imaging.ImageFormat]::Png)
    $script:g.Dispose();$script:board.Dispose()
}
function Native([string]$name,[int]$x,[int]$y,[int]$scale){
    $im=[System.Drawing.Bitmap]::FromFile((Join-Path $source ($name+'.png')))
    $g.DrawImage($im,[System.Drawing.Rectangle]::new($x,$y,360*$scale,160*$scale),0,0,360,160,[System.Drawing.GraphicsUnit]::Pixel)
    $im.Dispose()
}
function Corner([string]$name,[int]$x,[int]$y){
    $im=[System.Drawing.Bitmap]::FromFile((Join-Path $source ($name+'.png')))
    $g.DrawImage($im,[System.Drawing.Rectangle]::new($x,$y,540,204),250,101,90,34,[System.Drawing.GraphicsUnit]::Pixel)
    $im.Dispose()
}

foreach($theme in @('Default','Slime')){
    New-Board 1560 1030
    $g.DrawString(('기존 vs 첨부 색상 읽기 ON — '+$names[$theme]),$title,$white,38,26)
    $g.DrawString('두 버전 모두 현재 자동 외곽선을 유지했습니다. 같은 크기 · 같은 테마 · 같은 배경으로 비교합니다.',$body,$muted,42,91)
    $g.DrawString('기존 말풍선',$heading,$white,42,153)
    $g.DrawString('내가 올린 말풍선 · 색상 읽기 ON',$heading,$accent,802,153)
    Native ('color_dark_'+$theme+'_0') 40 218 2
    Native ('color_dark_'+$theme+'_2') 800 218 2
    $g.DrawRectangle($mark,40+250*2,218+101*2,180,68)
    $g.DrawRectangle($mark,800+250*2,218+101*2,180,68)
    $g.DrawString('자동으로 그린 외곽선 + 평평한 채움',$body,$white,44,559)
    $g.DrawString('자동 외곽선 + 직접 그린 어두운 선 + 아래 음영',$body,$accent,804,559)
    $g.DrawString('노란 상자 부분을 6배 확대',$body,$gold,42,620)
    Corner ('color_dark_'+$theme+'_0') 40 663
    Corner ('color_dark_'+$theme+'_2') 800 663
    $g.DrawString('기존: 안쪽부터 바깥쪽까지 구조가 단순합니다.',$small,$muted,44,895)
    $g.DrawString('첨부: 아래 음영과 어두운 선이 생겨 두께감이 늘어납니다.',$small,$muted,804,895)
    $g.DrawString('차이는 오류 여부보다 테두리의 두께와 입체감에 있습니다. 색상이 바뀌면 추가한 선과 음영도 그 테마색을 따라 어두워집니다.',$body,$gold,42,951)
    Save-Board ('comparison-closeup-'+$theme.ToLower()+'.png')
}

foreach($background in @('dark','light')){
    New-Board 1040 1240
    $g.DrawString('기존 / 첨부 색상 읽기 ON',$title,$white,30,24)
    $g.DrawString('같은 테마끼리 좌우 비교 · 두 버전 모두 자동 외곽선 ON',$small,$muted,33,86)
    $g.DrawString('기존 말풍선',$heading,$white,246,136)
    $g.DrawString('첨부 색상 읽기 ON',$heading,$accent,637,136)
    $index=0
    foreach($theme in @('Default','Slime','Dragon','Witch','DemonKing')){
        $y=200+$index*185
        $g.DrawString($names[$theme],$body,$white,32,$y+58)
        Native ('color_'+$background+'_'+$theme+'_0') 235 $y 1
        Native ('color_'+$background+'_'+$theme+'_2') 625 $y 1
        $index++
    }
    $g.DrawString('오른쪽에서 볼 부분: 위·옆의 어두운 안쪽 선, 아래쪽 음영, 그 바깥의 테마 외곽선.',$small,$gold,33,1153)
    $g.DrawString('이전에 검증한 실제 Unity 렌더를 재배치한 비교 자료입니다. 게임 변경은 적용하지 않았습니다.',$small,$muted,33,1193)
    Save-Board ('comparison-all-colors-'+$background+'.png')
}

$title.Dispose();$heading.Dispose();$body.Dispose();$small.Dispose()
$white.Dispose();$muted.Dispose();$accent.Dispose();$gold.Dispose();$mark.Dispose()
Write-Output 'Created four focused comparison boards using only existing and submitted-RGB-ON native renders.'
