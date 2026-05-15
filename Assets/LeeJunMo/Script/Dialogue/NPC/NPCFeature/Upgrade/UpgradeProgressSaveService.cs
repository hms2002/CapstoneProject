using System;

internal sealed class UpgradeProgressSaveService
{
    private readonly UpgradeProgressService progressService;
    private readonly Action notifyDataChanged;
    private readonly UnityEngine.Object saveRequester;

    public UpgradeProgressSaveService(
        UpgradeProgressService progressService,
        Action notifyDataChanged,
        UnityEngine.Object saveRequester)
    {
        this.progressService = progressService;
        this.notifyDataChanged = notifyDataChanged;
        this.saveRequester = saveRequester;
    }

    public void CheckAndUnlockNodes(bool requestSaveOnChange = true)
    {
        if (progressService == null || GameDataManager.Instance == null)
            return;

        bool isChanged = progressService.CheckAndUnlockNodes();
        if (!isChanged)
            return;

        if (requestSaveOnChange)
            RequestImmediateSave();

        NotifyDataChanged();
    }

    public void CheckAndUnlockNodesAfterPurchase()
    {
        CheckAndUnlockNodes(false);
    }

    public void RequestImmediateSave()
    {
        GameDataSaveCoordinator.RequestImmediateSave(saveRequester);
    }

    public void NotifyDataChanged()
    {
        notifyDataChanged?.Invoke();
    }
}
