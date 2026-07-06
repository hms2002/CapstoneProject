using UnityEngine;

/// <summary>
/// 책임 : 컷씬/프레젠테이션 동안 게임플레이 입력 차단 소유권과 소유 UI 열기 요청을 관리한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameFlowInputBlocker : MonoBehaviour
{
    private bool isBlocking;

    public bool IsBlocking => isBlocking;

    public static GameFlowInputBlocker GetOrAdd(Component owner)
    {
        if (owner == null)
            return null;

        if (owner.TryGetComponent(out GameFlowInputBlocker blocker))
            return blocker;

        return owner.gameObject.AddComponent<GameFlowInputBlocker>();
    }

    public void Acquire()
    {
        if (isBlocking)
            return;

        isBlocking = UiStackPlayback.SetExternalUiInputBlocked(this, true);
    }

    public void Release()
    {
        if (!isBlocking)
            return;

        UiStackPlayback.SetExternalUiInputBlocked(this, false);
        isBlocking = false;
    }

    public bool TryPushOwnedUI(IStackableUI ui)
    {
        if (ui == null)
            return false;

        return UiStackPlayback.TryPushForExternalBlockOwner(this, ui);
    }

    public bool CanOpenOwnedUI(IStackableUI ui)
    {
        if (ui == null)
            return false;

        return UiStackPlayback.CanOpenForExternalBlockOwner(this, ui);
    }

    private void OnDisable()
    {
        Release();
    }

    private void OnDestroy()
    {
        Release();
    }
}
