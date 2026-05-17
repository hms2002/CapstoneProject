using System;
using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [SerializeField] private UpgradeTreeUI upgradeTreeUI;
    [SerializeField] private UpgradeDatabase upgradeDatabase;

    [Header("Open Presentation")]
    [SerializeField] private bool useFadePresentationOnOpen = true;
    [SerializeField, Min(0f)] private float openFadeOutDuration = 0.18f;
    [SerializeField, Min(0f)] private float openFadeInDuration = 0.22f;

    public event Action OnDataChanged
    {
        add => EnsureNotifications().DataChanged += value;
        remove => EnsureNotifications().DataChanged -= value;
    }

    public event Action OnUIClosed
    {
        add => EnsureNotifications().UIClosed += value;
        remove => EnsureNotifications().UIClosed -= value;
    }

    private UpgradeProgressService progressService;
    private UpgradeEffectApplier effectApplier;
    private readonly Queue<UpgradeCinematicRequest> pendingCinematics = new Queue<UpgradeCinematicRequest>();
    private UpgradeUiOpenFlow uiOpenFlow;
    private UpgradeRuntimeEffectService runtimeEffectService;
    private UpgradePurchaseCompletionService purchaseCompletionService;
    private UpgradeRuntimeLifecycleService runtimeLifecycleService;
    private UpgradeProgressSaveService progressSaveService;
    private UpgradeNotificationService notifications;

    private void Awake()
    {
        if (!UpgradeManagerLifetimeService.TryClaimInstance(this, () => Instance, value => Instance = value))
            return;

        ResolveUpgradeTreeUiReference();
        notifications = new UpgradeNotificationService(this);
        uiOpenFlow = new UpgradeUiOpenFlow(this, ResolveUpgradeTreeUiForFlow);
        progressService = new UpgradeProgressService(upgradeDatabase);
        progressSaveService = new UpgradeProgressSaveService(
            progressService,
            notifications);
        effectApplier = new UpgradeEffectApplier();
        runtimeEffectService = new UpgradeRuntimeEffectService(
            ResolveCurrentPlayer,
            progressService,
            effectApplier,
            IsRunActive);
        runtimeLifecycleService = new UpgradeRuntimeLifecycleService(
            ResolveUpgradeTreeUiReference,
            CheckAndUnlockNodes,
            RebuildRunModifiers,
            runtimeEffectService.ResetAppliedPlayerEffects,
            runtimeEffectService.TryReapplyAllEffects,
            runtimeEffectService.TryApplyRunStartEffects,
            IsRunActive);
        purchaseCompletionService = new UpgradePurchaseCompletionService(
            ResolveCurrentPlayer,
            effectApplier,
            runtimeEffectService.MarkNodeAppliedForCurrentPlayer,
            runtimeEffectService.TryApplyHubTargetStates,
            EnqueueUpgradeCinematic,
            progressSaveService.CheckAndUnlockNodesAfterPurchase,
            notifications);
    }

    private void OnDestroy()
    {
        runtimeLifecycleService?.Unsubscribe();
        uiOpenFlow?.Cleanup();

        if (Instance == this)
            UpgradeManagerLifetimeService.ReleaseInstance(this, () => Instance, value => Instance = value);
    }

    private void OnEnable()
    {
        runtimeLifecycleService?.Subscribe();
    }

    private void Start()
    {
        runtimeLifecycleService?.RunStartupFlow();
    }

    private void OnDisable()
    {
        uiOpenFlow?.Cleanup();
        runtimeLifecycleService?.Unsubscribe();
    }

    private void ResolveUpgradeTreeUiReference()
    {
        if (upgradeTreeUI != null)
            return;

        upgradeTreeUI = UpgradeTreeUI.EnsureInstance();
    }

    private UpgradeTreeUI ResolveUpgradeTreeUiForFlow()
    {
        ResolveUpgradeTreeUiReference();
        return upgradeTreeUI;
    }

    private PlayerInteractor2D ResolveCurrentPlayer()
    {
        if (PlayerRuntimeRegistry.CurrentPlayer != null)
            return PlayerRuntimeRegistry.CurrentPlayer;

        return PlayerInteractor2D.Instance;
    }

    public void CheckAndUnlockNodes(bool requestSaveOnChange = true)
    {
        progressSaveService?.CheckAndUnlockNodes(requestSaveOnChange);
    }

    public void TryBuyUpgrade(int id)
    {
        var purchaseResult = UpgradePurchaseService.TryPurchase(
            new UpgradePurchaseRequest(id, progressService, CurrencyManager.Instance));
        if (!purchaseResult.Succeeded)
        {
            ShowPurchaseWarning(purchaseResult.FailureReason);
            return;
        }

        purchaseCompletionService.Complete(purchaseResult.Node);
    }

    private static void ShowPurchaseWarning(UpgradePurchaseFailureReason failureReason)
    {
        WarningPopupCode warningCode = failureReason switch
        {
            UpgradePurchaseFailureReason.NotEnoughMagicStone => WarningPopupCode.UpgradeNotEnoughMagicStone,
            UpgradePurchaseFailureReason.CurrencySpendFailed => WarningPopupCode.UpgradeNotEnoughMagicStone,
            UpgradePurchaseFailureReason.NotUnlocked => WarningPopupCode.UpgradeLocked,
            UpgradePurchaseFailureReason.MissingProgressService => WarningPopupCode.UpgradeUnavailable,
            UpgradePurchaseFailureReason.MissingNode => WarningPopupCode.UpgradeUnavailable,
            UpgradePurchaseFailureReason.MissingCurrencyManager => WarningPopupCode.UpgradeUnavailable,
            UpgradePurchaseFailureReason.PurchaseRejected => WarningPopupCode.UpgradeUnavailable,
            _ => WarningPopupCode.None
        };

        if (warningCode != WarningPopupCode.None)
            UIManager.Instance?.ShowWarning(warningCode);
    }

    private static bool IsRunActive()
    {
        return GamePlayDataManager.Instance != null
            && GamePlayDataManager.Instance.Data != null
            && GamePlayDataManager.Instance.Data.isRunActive;
    }

    public void ToggleUI()
    {
        EnsureUiOpenFlow().Toggle(useFadePresentationOnOpen, openFadeOutDuration, openFadeInDuration);
    }

    public void CloseUI()
    {
        EnsureUiOpenFlow().Close();
    }

    private UpgradeUiOpenFlow EnsureUiOpenFlow()
    {
        if (uiOpenFlow == null)
            uiOpenFlow = new UpgradeUiOpenFlow(this, ResolveUpgradeTreeUiForFlow);

        return uiOpenFlow;
    }

    public void NotifyUIClosed()
    {
        EnsureNotifications().NotifyUIClosed();
    }

    private UpgradeNotificationService EnsureNotifications()
    {
        notifications ??= new UpgradeNotificationService(this);
        return notifications;
    }

    public LockType GetNodeStatus(int id)
    {
        return progressService != null ? progressService.GetNodeStatus(id) : LockType.Locked;
    }

    public UpgradeNodeSO GetUpgradeByID(int id)
    {
        return progressService != null ? progressService.GetUpgradeByID(id) : null;
    }

    public List<UpgradeNodeSO> GetAllUpgrades()
    {
        return progressService != null ? progressService.GetAllUpgrades() : null;
    }

    public bool TryDequeuePendingCinematic(out UpgradeCinematicRequest request)
    {
        if (pendingCinematics.Count > 0)
        {
            request = pendingCinematics.Dequeue();
            return true;
        }

        request = default;
        return false;
    }

    private void EnqueueUpgradeCinematic(UpgradeCinematicRequest request)
    {
        pendingCinematics.Enqueue(request);
    }

    private static void RebuildRunModifiers()
    {
        RunModifierService.Instance?.RebuildFromPurchasedUpgrades();
    }
}
