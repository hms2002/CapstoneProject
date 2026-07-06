using System;

// 책임: 업그레이드 노드 잠금 해제 결과를 저장 요청과 UI 갱신 알림으로 연결한다.
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
        if (progressService == null || !GameDataStore.IsAvailable)
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

// 책임: 업그레이드 진행도 변경, 저장 요청, UI 닫힘 이벤트를 외부로 알린다.
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
        GameDataStore.RequestImmediateSave(saveRequester);
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
