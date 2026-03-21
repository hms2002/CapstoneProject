using UnityEngine;
using System.Collections.Generic;
using System;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    // [핵심 수정] GameObject가 아니라 우리가 만든 UI 스크립트(IStackableUI)를 연결해야 합니다!
    // 만약 에러가 난다면 UpgradeTreeUI 스크립트에 "IStackableUI" 인터페이스를 꼭 달아주세요!
    [SerializeField] private UpgradeTreeUI upgradeTreeUI;

    [SerializeField] private UpgradeDatabase upgradeDatabase;

    private Dictionary<int, UpgradeNodeSO> upgradeMap = new Dictionary<int, UpgradeNodeSO>();

    public Action OnDataChanged;
    public Action OnUIClosed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitDB();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }


    private void Start()
    {
        CheckAndUnlockNodes();
        ReapplyAllEffects();
    }

    private void InitDB()
    {
        if (upgradeDatabase == null) return;
        foreach (var node in upgradeDatabase.allUpgrades)
        {
            if (!upgradeMap.ContainsKey(node.nodeID)) upgradeMap.Add(node.nodeID, node);
        }
    }

    public void CheckAndUnlockNodes()
    {
        if (upgradeDatabase == null || GameDataManager.Instance == null) return;

        var data = GameDataManager.Instance.Data.upgradeData;
        bool isChanged = false;

        foreach (var node in upgradeDatabase.allUpgrades)
        {
            if (node == null) continue;

            if (data.purchasedIDs.Contains(node.nodeID) || data.unlockedIDs.Contains(node.nodeID)) continue;

            if (node.requiredParentIDs == null || node.requiredParentIDs.Count == 0)
            {
                data.unlockedIDs.Add(node.nodeID);
                isChanged = true;
                continue;
            }

            if (CheckParentsPurchased(node))
            {
                data.unlockedIDs.Add(node.nodeID);
                isChanged = true;
            }
        }

        if (isChanged)
        {
            GameDataManager.Instance.SaveData();
            OnDataChanged?.Invoke();
        }
    }

    private bool CheckParentsPurchased(UpgradeNodeSO node)
    {
        if (node.requiredParentIDs == null || node.requiredParentIDs.Count == 0) return true;
        var purchasedList = GameDataManager.Instance.Data.upgradeData.purchasedIDs;
        foreach (int parentID in node.requiredParentIDs)
        {
            if (!purchasedList.Contains(parentID)) return false;
        }
        return true;
    }

    public void TryBuyUpgrade(int id)
    {
        if (GetNodeStatus(id) != LockType.UnLocked) return;

        var node = GetUpgradeByID(id);
        if (node == null) return;
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.SpendMagicStone(node.price)) return;
        var data = GameDataManager.Instance.Data.upgradeData;

        data.unlockedIDs.Remove(id);
        data.purchasedIDs.Add(id);

        node.ApplyEffect(SampleTopDownPlayer.Instance);

        if (RewardDisplayUI.Instance != null)
            RewardDisplayUI.Instance.ShowReward(node.effects, null);

        CheckAndUnlockNodes();
        GameDataManager.Instance.SaveData();
        OnDataChanged?.Invoke();
    }

    private void ReapplyAllEffects()
    {
        if (SampleTopDownPlayer.Instance == null) return;
        foreach (var id in GameDataManager.Instance.Data.upgradeData.purchasedIDs)
        {
            GetUpgradeByID(id)?.ApplyEffect(SampleTopDownPlayer.Instance);
        }
    }

    public void ToggleUI()
    {
        if (upgradeTreeUI == null) return;

        // [수정] 직접 켜지 않고 UIManager에게 위임
        if (!upgradeTreeUI.IsActive)
        {
            if (UIManager.Instance != null) UIManager.Instance.PushUI(upgradeTreeUI);
            else upgradeTreeUI.OpenUI();
        }
        else
        {
            if (UIManager.Instance != null) UIManager.Instance.PopUI(upgradeTreeUI);
            else upgradeTreeUI.CloseUI();
        }
    }

    public void CloseUI()
    {
        if (upgradeTreeUI != null)
        {
            if (UIManager.Instance != null) UIManager.Instance.PopUI(upgradeTreeUI);
            else upgradeTreeUI.CloseUI();
        }

        OnUIClosed?.Invoke();
    }

    public LockType GetNodeStatus(int id)
    {
        var data = GameDataManager.Instance.Data.upgradeData;
        if (data.purchasedIDs.Contains(id)) return LockType.Purchased;
        if (data.unlockedIDs.Contains(id)) return LockType.UnLocked;
        return LockType.Locked;
    }

    public UpgradeNodeSO GetUpgradeByID(int id)
    {
        upgradeMap.TryGetValue(id, out var node);
        return node;
    }

    public List<UpgradeNodeSO> GetAllUpgrades() => upgradeDatabase.allUpgrades;
}