using System;

internal sealed class UpgradeProgressSaveService
{
    private readonly UpgradeProgressService progressService;
    private readonly UpgradeNotificationService notifications;

    public UpgradeProgressSaveService(
        UpgradeProgressService progressService,
        UpgradeNotificationService notifications)
    {
        this.progressService = progressService;
        this.notifications = notifications;
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
        notifications?.RequestImmediateSave();
    }

    public void NotifyDataChanged()
    {
        notifications?.NotifyDataChanged();
    }
}

internal sealed class UpgradeNotificationService
{
    private readonly UnityEngine.Object saveRequester;

    public event Action DataChanged;
    public event Action UIClosed;

    public UpgradeNotificationService(UnityEngine.Object saveRequester)
    {
        this.saveRequester = saveRequester;
    }

    public void RequestImmediateSave()
    {
        GameDataSaveCoordinator.RequestImmediateSave(saveRequester);
    }

    public void NotifyDataChanged()
    {
        DataChanged?.Invoke();
    }

    public void NotifyUIClosed()
    {
        UIClosed?.Invoke();
    }
}
