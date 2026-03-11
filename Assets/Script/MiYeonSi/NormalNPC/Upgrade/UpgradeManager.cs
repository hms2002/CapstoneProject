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
        // 시작 시 데이터 로드 후 상태 체크
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

    // [핵심 수정] 전체 노드를 순회하며 해금 상태 갱신
    public void CheckAndUnlockNodes()
    {
        if (upgradeDatabase == null || GameDataManager.Instance == null) return;

        var data = GameDataManager.Instance.Data.upgradeData;
        bool isChanged = false;

        foreach (var node in upgradeDatabase.allUpgrades)
        {
            if (node == null) continue;

            // 이미 구매했거나 해금된 상태면 패스
            if (data.purchasedIDs.Contains(node.nodeID) || data.unlockedIDs.Contains(node.nodeID)) continue;

            // 루트 노드(부모가 없는 노드)는 무조건 해금
            if (node.requiredParentIDs == null || node.requiredParentIDs.Count == 0)
            {
                data.unlockedIDs.Add(node.nodeID);
                isChanged = true;
                continue;
            }

            // [중요] 부모가 모두 '구매(Purchased)' 되었는지 확인
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

    // [핵심 로직] 모든 부모가 구매되었는지 체크
    private bool CheckParentsPurchased(UpgradeNodeSO node)
    {
        if (node.requiredParentIDs == null || node.requiredParentIDs.Count == 0) return true;

        var purchasedList = GameDataManager.Instance.Data.upgradeData.purchasedIDs;

        foreach (int parentID in node.requiredParentIDs)
        {
            // 부모 중 하나라도 구매되지 않았다면 해금 불가
            if (!purchasedList.Contains(parentID)) return false;
        }
        return true;
    }

    public void TryBuyUpgrade(int id)
    {
        if (GetNodeStatus(id) != LockType.UnLocked) return;

        var node = GetUpgradeByID(id);
        if (node == null || GameDataManager.Instance.Data.magicStone < node.price) return;

        // 구매 처리
        GameDataManager.Instance.Data.magicStone -= node.price;
        var data = GameDataManager.Instance.Data.upgradeData;

        data.unlockedIDs.Remove(id);
        data.purchasedIDs.Add(id); // 구매 목록에 추가

        node.ApplyEffect(SampleTopDownPlayer.Instance);

        // 보상 UI 표시
        if (RewardDisplayUI.Instance != null)
            RewardDisplayUI.Instance.ShowReward(node.effects, null);

        // [수정] 구매 후, 다음 노드들의 해금 조건을 다시 체크
        // 단순히 자식만 보는 게 아니라 전체를 다시 훑어서 조건 만족하는 애들을 풂
        CheckAndUnlockNodes();

        GameDataManager.Instance.SaveData();
        OnDataChanged?.Invoke();
    }

    private void ReapplyAllEffects()
    {
        if (SampleTopDownPlayer.Instance == null) return;
        foreach (var id in GameDataManager.Instance.Data.upgradeData.purchasedIDs)
            GetUpgradeByID(id)?.ApplyEffect(SampleTopDownPlayer.Instance);
    }

    // ... (ToggleUI, CloseUI 등 기존 로직 동일) ...
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