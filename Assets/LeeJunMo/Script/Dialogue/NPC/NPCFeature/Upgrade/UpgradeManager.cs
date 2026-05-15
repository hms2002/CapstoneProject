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

    public Action OnDataChanged;
    public Action OnUIClosed;

    private UpgradeProgressService progressService;
    private UpgradeEffectApplier effectApplier;
    private readonly Queue<UpgradeCinematicRequest> pendingCinematics = new Queue<UpgradeCinematicRequest>();
    private UpgradeUiOpenFlow uiOpenFlow;
    private UpgradeRuntimeEffectService runtimeEffectService;
    private UpgradePurchaseCompletionService purchaseCompletionService;
    private UpgradeRuntimeLifecycleService runtimeLifecycleService;
    private UpgradeProgressSaveService progressSaveService;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        GlobalUIRoot.AdoptService(transform);
        MarkPersistent();
        ResolveUpgradeTreeUiReference();
        uiOpenFlow = new UpgradeUiOpenFlow(this, ResolveUpgradeTreeUiForFlow);
        progressService = new UpgradeProgressService(upgradeDatabase);
        progressSaveService = new UpgradeProgressSaveService(
            progressService,
            () => OnDataChanged?.Invoke(),
            this);
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
            progressSaveService.RequestImmediateSave,
            progressSaveService.NotifyDataChanged);
    }

    private void OnDestroy()
    {
        runtimeLifecycleService?.Unsubscribe();
        uiOpenFlow?.Cleanup();

        if (Instance == this)
            Instance = null;
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

    private void MarkPersistent()
    {
        Transform persistentRoot = transform.root;
        if (persistentRoot == null || persistentRoot.parent != null)
            return;

        DontDestroyOnLoad(persistentRoot.gameObject);
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
            return;

        purchaseCompletionService.Complete(purchaseResult.Node);
    }

    private static bool IsRunActive()
    {
        return GamePlayDataManager.Instance != null
            && GamePlayDataManager.Instance.Data != null
            && GamePlayDataManager.Instance.Data.isRunActive;
    }

    public void ToggleUI()
    {
        ResolveUpgradeTreeUiReference();
        if (upgradeTreeUI == null)
            return;

        if (!upgradeTreeUI.IsActive)
        {
            OpenUI();
        }
        else
        {
            CloseUI();
        }
    }

    public void CloseUI()
    {
        EnsureUiOpenFlow().Close();
    }

    private void OpenUI()
    {
        EnsureUiOpenFlow().Open(useFadePresentationOnOpen, openFadeOutDuration, openFadeInDuration);
    }

    private UpgradeUiOpenFlow EnsureUiOpenFlow()
    {
        if (uiOpenFlow == null)
            uiOpenFlow = new UpgradeUiOpenFlow(this, ResolveUpgradeTreeUiForFlow);

        return uiOpenFlow;
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
