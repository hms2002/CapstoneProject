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
        // GameDataManager 로드 완료 후 효과 재적용
        ReapplyAllEffects();
    }

    private void InitDB()
    {
        foreach (var node in upgradeDatabase.allUpgrades)
        {
            if (!upgradeMap.ContainsKey(node.nodeID))
                upgradeMap.Add(node.nodeID, node);
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
        TempPlayer player = FindAnyObjectByType<TempPlayer>();
        if (player != null) node.ApplyEffect(player);

        // 6. 다음 노드 해금 처리 (조건부 해금)
        foreach (var nextId in node.unlockedNodeIDs)
        {
            // 이미 해금/구매된 노드는 패스
            if (data.purchasedIDs.Contains(nextId) || data.unlockedIDs.Contains(nextId))
                continue;

            var nextNode = GetUpgradeByID(nextId);
            if (nextNode != null)
            {
                // [핵심] AND 조건 체크: 필수 부모들을 다 샀는지 확인
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

    // 필수 조건 충족 여부 확인
    private bool CheckDependencies(UpgradeNodeSO node)
    {
        // 조건이 없으면 무조건 통과 (OR 조건 방식)
        if (node.requiredParentIDs == null || node.requiredParentIDs.Count == 0)
            return true;

        var purchasedList = GameDataManager.Instance.Data.upgradeData.purchasedIDs;

        // 필수 조건 중 하나라도 누락되면 실패
        foreach (int reqId in node.requiredParentIDs)
        {
            if (!purchasedList.Contains(reqId))
                return false;
        }

        return true;
    }

    private void ReapplyAllEffects()
    {
        TempPlayer player = FindAnyObjectByType<TempPlayer>();
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