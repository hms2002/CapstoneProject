Add-Type -AssemblyName System.Drawing
$out=$PSScriptRoot
$title=[System.Drawing.Font]::new('Malgun Gothic',27,[System.Drawing.FontStyle]::Bold)
$heading=[System.Drawing.Font]::new('Malgun Gothic',20,[System.Drawing.FontStyle]::Bold)
$body=[System.Drawing.Font]::new('Malgun Gothic',14)
$small=[System.Drawing.Font]::new('Malgun Gothic',11)
$white=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(239,243,249))
$muted=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(174,187,207))
$cyan=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(100,224,222))
$cases=@(
    @{name='npc-opacity-stone.png';label='NPC 위에서 비교 — 기본 말풍선';left='현재 · 배경 알파 52%';right='변경안 · 배경 알파 100%';prefix='npc_0';foot='실제 상인 스프라이트·말풍선 프리팹·픽셀 폰트·바닥 타일을 사용한 정적 미리보기입니다. 실제 게임 씬 캡처는 아닙니다.'},
    @{name='npc-opacity-pattern.png';label='복잡한 배경에서 비교 — 기본 말풍선';left='현재 · 배경 알파 52%';right='변경안 · 배경 알파 100%';prefix='npc_1';foot='색과 무늬가 강한 프로젝트 타일을 반복한 가독성 비교용 배치입니다. 실제 맵 배치와는 다릅니다.'},
    @{name='npc-opacity-witch.png';label='마녀 테마의 투명도 비교 — 같은 상인에 임시 대입';left='현재 · 배경 알파 약 39%';right='변경안 · 배경 알파 100%';prefix='npc_witch';foot='색상 차이를 보기 위해 마녀 말풍선 테마를 같은 상인 위에 대입했습니다. 실제 상인 테마를 변경한 것이 아닙니다.'}
)
foreach($case in $cases){
    $board=[System.Drawing.Bitmap]::new(1680,870)
    $g=[System.Drawing.Graphics]::FromImage($board)
    $g.Clear([System.Drawing.Color]::FromArgb(22,27,35))
    $g.TextRenderingHint=[System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.DrawString($case.label,$title,$white,27,21)
    $g.DrawString('이미지·색상 읽기 ON·자동 외곽선·글자·위치를 맞추고, 배경 투명도만 비교했습니다.',$body,$muted,30,82)
    $g.DrawString($case.left,$heading,$white,28,128)
    $g.DrawString($case.right,$heading,$cyan,854,128)
    $left=[System.Drawing.Bitmap]::FromFile((Join-Path $out ('native/'+$case.prefix+'_current.png')))
    $right=[System.Drawing.Bitmap]::FromFile((Join-Path $out ('native/'+$case.prefix+'_opaque.png')))
    $g.DrawImageUnscaled($left,24,180);$g.DrawImageUnscaled($right,852,180)
    $left.Dispose();$right.Dispose()
    $g.DrawString($case.foot,$small,$muted,28,802)
    $g.DrawString('투명도 변경은 미리보기에서만 수행했습니다. 게임 설정은 현재 상태 그대로입니다.',$small,$muted,28,835)
    $board.Save((Join-Path $out $case.name),[System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose();$board.Dispose()
}
$title.Dispose();$heading.Dispose();$body.Dispose();$small.Dispose()
$white.Dispose();$muted.Dispose();$cyan.Dispose()
Write-Output 'Created three NPC opacity comparison boards from six native renders.'
