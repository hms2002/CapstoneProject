using UnityEngine;

/// <summary>
/// 책임 :
/// - PopupCanvas에 열린 stackable UI가 실제로 존재할 때만 GraphicRaycaster를 켠다.
/// - 항상 살아 있는 매니저 오브젝트가 아니라 UIManager가 추적하는 실제 팝업 열림 상태를 기준으로 입력 차단 범위를 맞춘다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PopupCanvasRaycastGate : CanvasRaycastGateBase
{
    [SerializeField] private GlobalCanvasLayer targetCanvasLayer = GlobalCanvasLayer.Popup;

    protected override bool ShouldEnableRaycast()
    {
        Canvas popupCanvas = GlobalUIRoot.GetCanvas(targetCanvasLayer);
        if (popupCanvas == null)
            return false;

        UIManager uiManager = UIManager.Instance;
        if (uiManager != null)
            return uiManager.HasActivePopupInCanvas(popupCanvas);

        return HasActiveStackableUiFallback(popupCanvas.transform);
    }

    private static bool HasActiveStackableUiFallback(Transform canvasRoot)
    {
        if (canvasRoot == null)
            return false;

        MonoBehaviour[] behaviours = canvasRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not IStackableUI stackableUi)
                continue;

            if (stackableUi.IsActive)
                return true;
        }

        return false;
    }
}
