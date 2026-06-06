using UnityEngine;

/// <summary>
/// 책임 :
/// - 씬/프리팹 authoring 없이도 상태 HUD 서비스, 프레젠터, 툴팁 같은 공용 인프라를 자동으로 초기화한다.
/// - 개별 status source는 실제 owner(PlayerStatusRuntime, WeaponInventory2D)가 생길 때 직접 붙도록 두어 타이틀 씬에서 빈 bootstrap 오브젝트가 생성되지 않게 한다.
/// </summary>
public static class StatusHudRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        StatusHudService.EnsureInstance();
        StatusHudTooltipView.EnsureInstance();
        StatusHudPresenter.EnsureInstance();
    }
}
