using System;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 업그레이드 진행도, 구매 처리, 런타임 효과 적용을 관리한다.
/// - 업그레이드 화면 표시는 구체 UI 타입 대신 IUpgradeUiBackend playback 계약으로 요청한다.
/// </summary>
public class UpgradeManager : MonoBehaviour
{
    private static readonly SoundRef UpgradeSuccessSound = SoundRef.FromKey("sound_ui_UpgradeSuccess");
    private static readonly SoundRef CantUpgradeSound = SoundRef.FromKey("sound_ui_CantUpgrade");

    public static UpgradeManager Instance { get; private set; }

    [SerializeField] private MonoBehaviour upgradeTreeUI;
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
    private IUpgradeUiBackend upgradeUiBackend;
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
        UpgradeUiPlayback.Cleanup();

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
        UpgradeUiPlayback.Cleanup();
        runtimeLifecycleService?.Unsubscribe();
    }

    private void ResolveUpgradeTreeUiReference()
    {
        if (upgradeUiBackend != null && upgradeUiBackend.BackendComponent != null)
            return;

        upgradeUiBackend = upgradeTreeUI as IUpgradeUiBackend;
        if (upgradeUiBackend != null)
        {
            UpgradeUiPlayback.RegisterBackend(upgradeUiBackend);
            return;
        }

        upgradeUiBackend = UpgradeUiPlayback.ResolveBackend();
        if (upgradeUiBackend != null && upgradeTreeUI == null)
            upgradeTreeUI = upgradeUiBackend.BackendComponent as MonoBehaviour;
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
            SoundPlaybackUtility.Play(CantUpgradeSound, sourceObject: this);
            ShowPurchaseWarning(purchaseResult.FailureReason);
            return;
        }

        SoundPlaybackUtility.Play(UpgradeSuccessSound, sourceObject: this);
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
            WarningPopupPlayback.Show(warningCode);
    }

    private static bool IsRunActive()
    {
        return RunSessionStore.IsRunActive;
    }

    public void ToggleUI()
    {
        ResolveUpgradeTreeUiReference();
        UpgradeUiPlayback.Toggle(useFadePresentationOnOpen, openFadeOutDuration, openFadeInDuration);
    }

    public void CloseUI()
    {
        ResolveUpgradeTreeUiReference();
        UpgradeUiPlayback.Close();
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
