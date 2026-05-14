using UnityEngine;

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
        if (isBlocking || UIManager.Instance == null)
            return;

        UIManager.Instance.SetExternalUiInputBlocked(this, true);
        isBlocking = true;
    }

    public void Release()
    {
        if (!isBlocking)
            return;

        UIManager.Instance?.SetExternalUiInputBlocked(this, false);
        isBlocking = false;
    }

    public bool TryPushOwnedUI(IStackableUI ui)
    {
        if (ui == null)
            return false;

        if (UIManager.Instance != null)
            return UIManager.Instance.TryPushUIForExternalBlockOwner(this, ui);

        ui.OpenUI();
        return true;
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
