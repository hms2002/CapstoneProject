using UnityEngine;
using System.Collections.Generic;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [SerializeField] private GameObject upgradeTreePanel;
    [SerializeField] private UpgradeDatabase upgradeDatabase;

    private Dictionary<int, UpgradeNodeSO> upgradeMap = new Dictionary<int, UpgradeNodeSO>();

    // UI 갱신용 이벤트
    public System.Action OnDataChanged;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);

        InitDB();
    }

    private void Start()
    {
        // 1. 게임 시작 시 0층(시작 노드) 자동 해금
        CheckAndUnlockStartingNodes();

        // 2. 이미 구매한 업그레이드 효과를 플레이어에게 재적용
        ReapplyAllEffects();
    }

    private void InitDB()
    {
        if (upgradeDatabase == null) return;
        foreach (var node in upgradeDatabase.allUpgrades)
        {
            if (!upgradeMap.ContainsKey(node.nodeID))
                upgradeMap.Add(node.nodeID, node);
        }
    }

    // ---------------------------------------------------------
    // 0층 노드 자동 해금 로직
    // ---------------------------------------------------------
    private void CheckAndUnlockStartingNodes()
    {
        if (upgradeDatabase == null || GameDataManager.Instance == null) return;

        var data = GameDataManager.Instance.Data.upgradeData;
        bool isChanged = false;

        foreach (var node in upgradeDatabase.allUpgrades)
        {
            if (node == null) continue;

            // 바닥층(0)이거나 부모 조건이 없는 노드는 해금 대상
            if (node.gridY == 0 || node.requiredParentIDs.Count == 0)
            {
                // 이미 구매했거나 해금된 상태면 패스
                if (data.purchasedIDs.Contains(node.nodeID)) continue;
                if (data.unlockedIDs.Contains(node.nodeID)) continue;

                // 해금 목록에 추가
                data.unlockedIDs.Add(node.nodeID);
                isChanged = true;
                Debug.Log($"[UpgradeManager] 시작 노드 자동 해금: {node.upgradeName}");
            }
        }

        if (isChanged)
        {
            GameDataManager.Instance.SaveData();
            OnDataChanged?.Invoke();
        }
    }

    public UpgradeNodeSO GetUpgradeByID(int id)
    {
        upgradeMap.TryGetValue(id, out var node);
        return node;
    }

    public List<UpgradeNodeSO> GetAllUpgrades()
    {
        return upgradeDatabase.allUpgrades;
    }

    // ---------------------------------------------------------
    // 상태 확인 및 구매 로직
    // ---------------------------------------------------------

    public LockType GetNodeStatus(int id)
    {
        if (GameDataManager.Instance == null) return LockType.Locked;

        var data = GameDataManager.Instance.Data.upgradeData;
        if (data.purchasedIDs.Contains(id)) return LockType.Purchased;
        if (data.unlockedIDs.Contains(id)) return LockType.UnLocked;
        return LockType.Locked;
    }

    public void TryBuyUpgrade(int id)
    {
        // 1. 상태 검증
        if (GetNodeStatus(id) != LockType.UnLocked) return;

        var node = GetUpgradeByID(id);
        if (node == null) return;

        // 2. 재화 검증
        if (GameDataManager.Instance.Data.magicStone < node.price)
        {
            Debug.Log("마정석이 부족합니다.");
            return;
        }

        // 3. 재화 차감
        GameDataManager.Instance.Data.magicStone -= node.price;

        // 4. 데이터 갱신 (구매 처리)
        var data = GameDataManager.Instance.Data.upgradeData;
        data.unlockedIDs.Remove(id);
        data.purchasedIDs.Add(id);

        // 5. 효과 즉시 적용
        // [수정] SampleTopDownPlayer 인스턴스 가져오기
        SampleTopDownPlayer player = SampleTopDownPlayer.Instance;

        // 플레이어가 없더라도(로비 등) 아이템 해금 효과 등은 작동해야 하므로 호출
        // (각 Effect 내부에서 player null 체크가 필요하다면 그쪽에서 처리)
        node.ApplyEffect(player);

        // 6. 다음 노드 해금 처리 (조건부 해금)
        foreach (var nextId in node.unlockedNodeIDs)
        {
            if (data.purchasedIDs.Contains(nextId) || data.unlockedIDs.Contains(nextId))
                continue;

            var nextNode = GetUpgradeByID(nextId);
            if (nextNode != null)
            {
                if (CheckDependencies(nextNode))
                {
                    data.unlockedIDs.Add(nextId);
                }
            }
        }

        // 7. 저장 및 UI 갱신
        GameDataManager.Instance.SaveData();
        OnDataChanged?.Invoke();
    }

    private bool CheckDependencies(UpgradeNodeSO node)
    {
        if (node.requiredParentIDs == null || node.requiredParentIDs.Count == 0)
            return true;

        var purchasedList = GameDataManager.Instance.Data.upgradeData.purchasedIDs;

        foreach (int reqId in node.requiredParentIDs)
        {
            if (!purchasedList.Contains(reqId))
                return false;
        }
        return true;
    }

    private void ReapplyAllEffects()
    {
        // [수정] SampleTopDownPlayer 인스턴스 사용
        SampleTopDownPlayer player = SampleTopDownPlayer.Instance;

        // 재적용은 보통 스탯 관련이 많으므로 플레이어가 없으면 의미가 없을 수 있음
        // 하지만 안전하게 호출하고 각 Effect에서 판단하게 둠
        if (player == null) return;

        var purchasedList = GameDataManager.Instance.Data.upgradeData.purchasedIDs;
        foreach (var id in purchasedList)
        {
            var node = GetUpgradeByID(id);
            if (node != null) node.ApplyEffect(player);
        }
    }

    public void ToggleUI()
    {
        if (upgradeTreePanel != null)
            upgradeTreePanel.SetActive(!upgradeTreePanel.activeSelf);
    }
}