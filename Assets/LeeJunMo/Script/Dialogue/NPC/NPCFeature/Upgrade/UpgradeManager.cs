using System;
using System;
using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [SerializeField] private UpgradeTreeUI upgradeTreeUI;
    [SerializeField] private UpgradeDatabase upgradeDatabase;

    public Action OnDataChanged;
    public Action OnUIClosed;

    private UpgradeProgressService progressService;
    private UpgradeEffectApplier effectApplier;
    private SampleTopDownPlayer appliedPlayer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        progressService = new UpgradeProgressService(upgradeDatabase);
        effectApplier = new UpgradeEffectApplier();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
    }

    private void Start()
    {
        CheckAndUnlockNodes();
        TryReapplyAllEffects();
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
    }

    private void HandlePlayerRegistered(SampleTopDownPlayer player)
    {
        TryReapplyAllEffects();
    }

    private SampleTopDownPlayer ResolveCurrentPlayer()
    {
        if (PlayerRuntimeRegistry.CurrentPlayer != null)
            return PlayerRuntimeRegistry.CurrentPlayer;

        return SampleTopDownPlayer.Instance;
    }

    private void TryReapplyAllEffects()
    {
        SampleTopDownPlayer player = ResolveCurrentPlayer();
        if (player == null)
            return;

        if (appliedPlayer == player)
            return;

        ReapplyAllEffects(player);
        appliedPlayer = player;
    }

    public void CheckAndUnlockNodes()
    {
        if (progressService == null || GameDataManager.Instance == null)
            return;

        bool isChanged = progressService.CheckAndUnlockNodes();
        if (!isChanged)
            return;

        GameDataManager.Instance.SaveData();
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

        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.SpendMagicStone(node.price))
            return;

        if (!progressService.TryPurchase(id, out node))
            return;

        SampleTopDownPlayer player = ResolveCurrentPlayer();
        if (player != null)
            effectApplier.ApplyUpgrade(node, player);

        if (RewardDisplayUI.Instance != null)
            RewardDisplayUI.Instance.ShowReward(node.effects, null);

        CheckAndUnlockNodes();
        GameDataManager.Instance.SaveData();
        OnDataChanged?.Invoke();
    }

    private void ReapplyAllEffects(SampleTopDownPlayer player)
    {
        if (player == null || GameDataManager.Instance == null || progressService == null || effectApplier == null)
            return;

        effectApplier.ReapplyPurchasedEffects(GameDataManager.Instance.Data.upgradeData.purchasedIDs, progressService, player);
    }

    public void ToggleUI()
    {
        if (upgradeTreeUI == null)
            return;

        if (!upgradeTreeUI.IsActive)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.PushUI(upgradeTreeUI);
            else
                upgradeTreeUI.OpenUI();
        }
        else
        {
            if (UIManager.Instance != null)
                UIManager.Instance.PopUI(upgradeTreeUI);
            else
                upgradeTreeUI.CloseUI();
        }
    }

    public void CloseUI()
    {
        if (upgradeTreeUI == null)
            return;

        if (UIManager.Instance != null)
            UIManager.Instance.PopUI(upgradeTreeUI);
        else
            upgradeTreeUI.CloseUI();
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
