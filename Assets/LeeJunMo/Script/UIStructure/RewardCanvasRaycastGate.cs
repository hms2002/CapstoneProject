using UnityEngine;

/// <summary>
/// 책임 :
/// - RewardCanvas에 실제 보상 패널이 열려 있을 때만 GraphicRaycaster를 켠다.
/// - reward view가 닫혀 있을 때는 캔버스가 살아 있어도 마우스 입력을 가로채지 않게 유지한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RewardCanvasRaycastGate : CanvasRaycastGateBase
{
    protected override bool ShouldEnableRaycast()
    {
        RewardDisplayUI rewardDisplayUi = RewardDisplayUI.Instance;
        if (rewardDisplayUi == null || !rewardDisplayUi.IsActive)
            return false;

        Canvas rewardCanvas = GlobalUIRoot.GetCanvas(GlobalCanvasLayer.Reward);
        if (rewardCanvas == null)
            return false;

        return rewardDisplayUi.transform.IsChildOf(rewardCanvas.transform);
    }
}
