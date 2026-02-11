using UnityEngine;
using System.Collections.Generic;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [SerializeField] private GameObject upgradeTreePanel;
    [SerializeField] private UpgradeDatabase upgradeDatabase;

    private Dictionary<int, UpgradeNodeSO> upgradeMap = new Dictionary<int, UpgradeNodeSO>();
    public System.Action OnDataChanged;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
        InitDB();
    }

    private void Start()
    {
        CheckAndUnlockStartingNodes();
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

    private void CheckAndUnlockStartingNodes()
    {
        if (upgradeDatabase == null || GameDataManager.Instance == null) return;
        var data = GameDataManager.Instance.Data.upgradeData;
        bool isChanged = false;
        foreach (var node in upgradeDatabase.allUpgrades)
        {
            if (node == null) continue;
            if (node.gridY == 0 || node.requiredParentIDs.Count == 0)
            {
                if (data.purchasedIDs.Contains(node.nodeID) || data.unlockedIDs.Contains(node.nodeID)) continue;
                data.unlockedIDs.Add(node.nodeID);
                isChanged = true;
            }
        }
        if (isChanged) { GameDataManager.Instance.SaveData(); OnDataChanged?.Invoke(); }
    }

    public void TryBuyUpgrade(int id)
    {
        if (GetNodeStatus(id) != LockType.UnLocked) return;
        var node = GetUpgradeByID(id);
        if (node == null || GameDataManager.Instance.Data.magicStone < node.price) return;

        GameDataManager.Instance.Data.magicStone -= node.price;
        var data = GameDataManager.Instance.Data.upgradeData;
        data.unlockedIDs.Remove(id);
        data.purchasedIDs.Add(id);

        node.ApplyEffect(SampleTopDownPlayer.Instance);

        if (RewardDisplayUI.Instance != null)
            RewardDisplayUI.Instance.ShowReward(node.effects, null);

        foreach (var nextId in node.unlockedNodeIDs)
        {
            var nextNode = GetUpgradeByID(nextId);
            if (nextNode != null && CheckDependencies(nextNode)) data.unlockedIDs.Add(nextId);
        }

        GameDataManager.Instance.SaveData();
        OnDataChanged?.Invoke();
    }

    private bool CheckDependencies(UpgradeNodeSO node)
    {
        if (node.requiredParentIDs == null || node.requiredParentIDs.Count == 0) return true;
        var purchasedList = GameDataManager.Instance.Data.upgradeData.purchasedIDs;
        foreach (int reqId in node.requiredParentIDs) if (!purchasedList.Contains(reqId)) return false;
        return true;
    }

    private void ReapplyAllEffects()
    {
        if (SampleTopDownPlayer.Instance == null) return;
        foreach (var id in GameDataManager.Instance.Data.upgradeData.purchasedIDs)
            GetUpgradeByID(id)?.ApplyEffect(SampleTopDownPlayer.Instance);
    }

    public void ToggleUI()
    {
        bool nextState = !upgradeTreePanel.activeSelf;
        upgradeTreePanel.SetActive(nextState);
        if (nextState) UIManager.Instance.RegisterUI("UpgradeTree");
        else CloseUI();
    }

    public void CloseUI()
    {
        upgradeTreePanel.SetActive(false);
        UIManager.Instance.UnregisterUI("UpgradeTree");
        if (DialogueManager.GetInstance() != null && DialogueManager.GetInstance().dialogueIsPlaying)
            DialogueManager.GetInstance().ResumeDialogueAfterUI();
    }

    public LockType GetNodeStatus(int id)
    {
        var data = GameDataManager.Instance.Data.upgradeData;
        if (data.purchasedIDs.Contains(id)) return LockType.Purchased;
        if (data.unlockedIDs.Contains(id)) return LockType.UnLocked;
        return LockType.Locked;
    }

    public UpgradeNodeSO GetUpgradeByID(int id) { upgradeMap.TryGetValue(id, out var node); return node; }
    public List<UpgradeNodeSO> GetAllUpgrades() => upgradeDatabase.allUpgrades;
}