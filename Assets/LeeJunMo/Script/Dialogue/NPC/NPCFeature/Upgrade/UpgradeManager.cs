using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    private PlayerInteractor2D appliedPlayer;
    private Coroutine openPresentationRoutine;

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
        progressService = new UpgradeProgressService(upgradeDatabase);
        effectApplier = new UpgradeEffectApplier();
    }

    private void OnDestroy()
    {
        if (openPresentationRoutine != null)
        {
            StopCoroutine(openPresentationRoutine);
            openPresentationRoutine = null;
            SceneFadeTransitionService.Instance?.EndOverlayFadeSession();
        }

        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        CheckAndUnlockNodes();
        TryReapplyAllEffects();
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandlePlayerRegistered(PlayerInteractor2D player)
    {
        TryReapplyAllEffects();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveUpgradeTreeUiReference();
        CheckAndUnlockNodes(false);
        appliedPlayer = null;
        TryReapplyAllEffects();
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

    private PlayerInteractor2D ResolveCurrentPlayer()
    {
        if (PlayerRuntimeRegistry.CurrentPlayer != null)
            return PlayerRuntimeRegistry.CurrentPlayer;

        return PlayerInteractor2D.Instance;
    }

    private void TryReapplyAllEffects()
    {
        PlayerInteractor2D player = ResolveCurrentPlayer();
        if (player == null)
            return;

        if (appliedPlayer == player)
            return;

        ReapplyAllEffects(player);
        appliedPlayer = player;
    }

    public void CheckAndUnlockNodes(bool requestSaveOnChange = true)
    {
        if (progressService == null || GameDataManager.Instance == null)
            return;

        bool isChanged = progressService.CheckAndUnlockNodes();
        if (!isChanged)
            return;

        if (requestSaveOnChange)
            GameDataSaveCoordinator.RequestImmediateSave(this);

        OnDataChanged?.Invoke();
    }

    public void TryBuyUpgrade(int id)
    {
        if (progressService == null)
            return;

        UpgradeNodeSO node = progressService.GetUpgradeByID(id);
        if (node == null)
            return;

        if (progressService.GetNodeStatus(id) != LockType.UnLocked)
            return;

        if (CurrencyManager.Instance == null)
            return;

        if (CurrencyManager.Instance.GetMagicStone() < node.price)
            return;

        if (!progressService.TryPurchase(id, out node))
            return;

        if (!CurrencyManager.Instance.SpendMagicStone(node.price))
        {
            progressService.RevertPurchase(id);
            return;
        }

        PlayerInteractor2D player = ResolveCurrentPlayer();
        effectApplier.ApplyUpgrade(node, player);

        if (RewardDisplayService.Instance != null)
            RewardDisplayService.Instance.ShowReward(node.effects, null);

        CheckAndUnlockNodes(false);
        GameDataSaveCoordinator.RequestImmediateSave(this);
        OnDataChanged?.Invoke();
    }

    private void ReapplyAllEffects(PlayerInteractor2D player)
    {
        if (player == null || GameDataManager.Instance == null || progressService == null || effectApplier == null)
            return;

        GameData data = GameDataManager.Instance.EnsureData();
        if (data == null)
            return;

        data.upgradeData ??= new UpgradeSaveData();
        effectApplier.ReapplyPurchasedEffects(data.upgradeData.purchasedIDs, progressService, player);
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
        if (openPresentationRoutine != null)
        {
            StopCoroutine(openPresentationRoutine);
            openPresentationRoutine = null;
            SceneFadeTransitionService.Instance?.EndOverlayFadeSession();
        }

        ResolveUpgradeTreeUiReference();
        if (upgradeTreeUI == null)
            return;

        if (UIManager.Instance != null)
            UIManager.Instance.PopUI(upgradeTreeUI);
        else
            upgradeTreeUI.CloseUI();
    }

    private void OpenUI()
    {
        ResolveUpgradeTreeUiReference();
        if (upgradeTreeUI == null || upgradeTreeUI.IsActive)
            return;

        if (UIManager.Instance != null && !UIManager.Instance.CanOpenUI(upgradeTreeUI))
            return;

        if (!useFadePresentationOnOpen)
        {
            OpenUIImmediate();
            return;
        }

        if (openPresentationRoutine != null)
            return;

        openPresentationRoutine = StartCoroutine(OpenUIWithFadePresentation());
    }

    private IEnumerator OpenUIWithFadePresentation()
    {
        SceneFadeTransitionService fadeService = SceneFadeTransitionService.EnsureInstance(allowRuntimeFallback: true);
        bool hasFadeOverlay = fadeService != null && fadeService.TryBeginOverlayFadeSession(initialAlpha: 0f);

        if (hasFadeOverlay)
            yield return fadeService.FadeOutAsync(openFadeOutDuration);

        bool opened = OpenUIImmediate();

        if (hasFadeOverlay)
        {
            yield return null;
            yield return fadeService.FadeInAsync(opened ? openFadeInDuration : openFadeOutDuration);
            fadeService.EndOverlayFadeSession();
        }

        openPresentationRoutine = null;
    }

    private bool OpenUIImmediate()
    {
        ResolveUpgradeTreeUiReference();
        if (upgradeTreeUI == null)
            return false;

        if (upgradeTreeUI.IsActive)
            return true;

        if (UIManager.Instance != null)
            return UIManager.Instance.TryPushUI(upgradeTreeUI);

        upgradeTreeUI.OpenUI();
        return true;
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
}
