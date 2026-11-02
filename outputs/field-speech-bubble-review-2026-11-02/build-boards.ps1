Add-Type -AssemblyName System.Drawing
$out = $PSScriptRoot
$config = Get-Content -LiteralPath (Join-Path $out 'render-config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$fontName = 'Malgun Gothic'
$title = [System.Drawing.Font]::new($fontName,25,[System.Drawing.FontStyle]::Bold)
$heading = [System.Drawing.Font]::new($fontName,15,[System.Drawing.FontStyle]::Bold)
$body = [System.Drawing.Font]::new($fontName,12)
$small = [System.Drawing.Font]::new($fontName,10)
$white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(238,242,249))
$muted = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(168,182,201))
$cyan = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(94,217,225))
$yellow = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(247,196,112))
$names = @{Default='기본 / 플레이어';Slime='슬라임';Dragon='드래곤';Witch='마녀';DemonKing='마왕'}
$columnTitles = @('기존 원본','첨부본 그대로 교체','첨부 색상 읽기 ON','자동 외곽선 OFF','권장 조합 · 미적용')
$columnNotes = @('현재 게임 설정','색상 읽기 OFF / 자동 외곽선 ON','RGB × 테마색 / 자동 외곽선 ON','RGB × 테마색 / 직접 그린 선 사용','음영만 사용 / 테마 외곽선 ON')

function New-Board([int]$width,[int]$height) {
    $script:board = [System.Drawing.Bitmap]::new($width,$height)
    $script:g = [System.Drawing.Graphics]::FromImage($script:board)
    $script:g.Clear([System.Drawing.Color]::FromArgb(22,27,35))
    $script:g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
}
function Save-Board([string]$name) {
    $script:board.Save((Join-Path $out $name),[System.Drawing.Imaging.ImageFormat]::Png)
    $script:g.Dispose(); $script:board.Dispose()
}
function Tile([string]$file,[int]$x,[int]$y) {
    $bitmap = [System.Drawing.Bitmap]::FromFile((Join-Path $out ('native/'+$file+'.png')))
    $script:g.DrawImageUnscaled($bitmap,$x,$y); $bitmap.Dispose()
}
function Swatch($values,[int]$x,[int]$y) {
    $color=[System.Drawing.Color]::FromArgb(255,[int][math]::Round($values[0]*255),[int][math]::Round($values[1]*255),[int][math]::Round($values[2]*255))
    $brush=[System.Drawing.SolidBrush]::new($color)
    $script:g.FillRectangle($brush,$x,$y,18,18);$brush.Dispose()
}

foreach($background in @('dark','light')) {
    New-Board 2120 1330
    $g.DrawString('첨부 말풍선 · 실제 Unity 셰이더 비교',$title,$white,35,24)
    $g.DrawString('19×16 원본 / 같은 9분할 / 현재 보스 테마 4종 + 기본 테마 / '+$(if($background -eq 'dark'){'어두운 배경'}else{'밝은 배경'}),$body,$muted,38,83)
    for($col=0;$col -lt 5;$col++) {
        $x=220+$col*374
        $g.DrawString($columnTitles[$col],$heading,$(if($col -eq 4){$cyan}else{$white}),$x,139)
        $g.DrawString($columnNotes[$col],$small,$muted,$x,173)
    }
    for($row=0;$row -lt 5;$row++) {
        $theme=$config.themes[$row];$y=218+$row*195
        $g.DrawString($names[$theme.name],$heading,$white,38,$y+35)
        Swatch $theme.fill 38 ($y+79); Swatch $theme.border 64 ($y+79)
        $g.DrawString(('배경 알파 '+[math]::Round($theme.fill[3],2)),$small,$muted,38,$y+106)
        for($col=0;$col -lt 5;$col++){Tile ('color_'+$background+'_'+$theme.name+'_'+$col) (220+$col*374) $y}
    }
    $g.DrawString('판정: 그대로 교체하면 그려 둔 선·음영이 사라집니다. 색만 켜면 어두운 선 + 테마 외곽선이 겹칩니다.',$body,$yellow,38,1211)
    $g.DrawString('권장 조합은 첨부본의 불투명 음영 15픽셀만 남기고, 원본 모양 + 기존 테마 외곽선을 사용하는 진단안입니다.',$body,$muted,38,1250)
    $g.DrawString('게임 미적용 | 실제 UGUI Image + 프로젝트 셰이더 렌더 | 배경 검증 전용, 전체 씬·텍스트 레이아웃 검증 제외',$small,$muted,38,1288)
    Save-Board ('all-colors-'+$background+'.png')
}

New-Board 1380 1260
$g.DrawString('이 그림을 그대로 넣으면?',$title,$white,35,25)
$g.DrawString('실제 Unity 셰이더 렌더 · 색상이 다른 5개 테마 비교',$body,$muted,38,82)
$overviewColumns=@(1,2,4)
for($col=0;$col -lt 3;$col++){
    $index=$overviewColumns[$col];$x=220+$col*374
    $g.DrawString($columnTitles[$index],$heading,$(if($index -eq 4){$cyan}else{$white}),$x,133)
    $g.DrawString($columnNotes[$index],$small,$muted,$x,170)
}
for($row=0;$row -lt 5;$row++){
    $theme=$config.themes[$row];$y=210+$row*185
    $g.DrawString($names[$theme.name],$heading,$white,38,$y+44)
    $g.DrawString(('배경 알파 '+[math]::Round($theme.fill[3],2)),$small,$muted,38,$y+79)
    for($col=0;$col -lt 3;$col++){Tile ('color_dark_'+$theme.name+'_'+$overviewColumns[$col]) (220+$col*374) $y}
}
$g.DrawString('왼쪽: 선·음영 소실   /   가운데: 직접 그린 선과 자동 외곽선 중첩   /   오른쪽: 테마 테두리 + 음영',$body,$yellow,38,1165)
$g.DrawString('오른쪽은 미리보기 진단안이며 게임에 반영하지 않았습니다. 전체 설정 비교와 밝은 배경 검사는 별도 이미지에 포함했습니다.',$small,$muted,38,1208)
Save-Board 'overview.png'

foreach($background in @('dark','light')){
    New-Board 2340 1000
    $g.DrawString('9분할 확대 · 꼬리 반전 확인 (마녀 테마)',$title,$white,35,25)
    $g.DrawString('배경 알파 0.392 / 같은 border L7 B6 R3 T3 / '+$(if($background -eq 'dark'){'어두운 배경'}else{'밝은 배경'}),$body,$muted,38,82)
    $layoutTitles=@('짧은 박스 150×90','일반 박스 280×110','긴 박스 440×110','오른쪽 꼬리 280×110')
    for($col=0;$col -lt 4;$col++){$g.DrawString($layoutTitles[$col],$heading,$white,(220+$col*526),145)}
    $layoutRows=@('첨부본 / 현재 설정','첨부본 / 색 ON, 자동 선 OFF','권장 / 음영 + 테마 선')
    for($row=0;$row -lt 3;$row++){
        $y=205+$row*225
        $g.DrawString($layoutRows[$row],$body,$(if($row -eq 2){$cyan}else{$white}),[System.Drawing.RectangleF]::new(35,$y+42,170,100))
        for($col=0;$col -lt 4;$col++){Tile ('layout_'+$background+'_'+$row+'_'+$col) (220+$col*526) $y}
    }
    $g.DrawString('확인: 박스 폭과 꼬리 방향을 바꿔도 음영은 아래 변에 남습니다. 반투명 배경에서는 직접 그린 선도 함께 투명해집니다.',$body,$yellow,38,923)
    Save-Board ('layout-check-'+$background+'.png')
}

New-Board 1280 900
$g.DrawString('알파로 음영을 만들 때의 함정',$title,$white,35,25)
$g.DrawString('첨부본의 음영은 불투명 RGB입니다. 아래 오른쪽은 비교를 위해 음영 부분의 알파만 50%로 낮춘 진단입니다.',$body,$muted,38,83)
$g.DrawString('불투명 RGB 음영 + 테마 선',$heading,$cyan,235,145)
$g.DrawString('음영 알파 50% + 같은 테마 선',$heading,$yellow,705,145)
for($row=0;$row -lt 2;$row++){
    $name=@('Default','Witch')[$row];$y=210+$row*240
    $g.DrawString($names[$name],$heading,$white,38,$y+55)
    Tile ('color_dark_'+$name+'_4') 235 $y
    Tile ('alpha_warning_'+$name) 705 $y
}
$g.DrawString('원인: 셰이더는 texture alpha × 배경 alpha가 0.341보다 낮으면 내부가 아닌 것으로 판정합니다.',$body,$yellow,38,722)
$g.DrawString('마녀는 배경 alpha가 0.392이므로, 음영 alpha가 약 0.87 미만이면 음영 위치에도 외곽선 색이 나올 수 있습니다.',$body,$muted,38,766)
$g.DrawString('이 디자인에서는 음영을 불투명 회색 RGB로 유지하는 편이 현재 셰이더와 잘 맞습니다. 게임 변경은 적용하지 않았습니다.',$body,$muted,38,810)
Save-Board 'alpha-shadow-warning.png'

$title.Dispose();$heading.Dispose();$body.Dispose();$small.Dispose()
$white.Dispose();$muted.Dispose();$cyan.Dispose();$yellow.Dispose()
Write-Output 'Composed 6 comparison boards from native Unity captures.'
