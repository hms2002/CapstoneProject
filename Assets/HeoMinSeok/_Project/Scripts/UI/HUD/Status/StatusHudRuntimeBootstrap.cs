using UnityEngine;

/// <summary>
/// 책임 :
/// - 씬/프리팹 authoring 없이도 상태 HUD 서비스, 프레젠터, 툴팁, 태양/월영 source 부트스트랩을 자동으로 초기화한다.
/// - 상태 HUD 인프라를 "필요할 때 자동으로 살아나는" 런타임 기반 구조로 열어 도입 비용을 낮춘다.
/// </summary>
public static class StatusHudRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        StatusHudService.EnsureInstance();
        StatusHudTooltipView.EnsureInstance();
        StatusHudPresenter.EnsureInstance();
        PlayerStatusHudBootstrap.EnsureInstance();
        SunMoonStatusHudBootstrap.EnsureInstance();
    }
}
