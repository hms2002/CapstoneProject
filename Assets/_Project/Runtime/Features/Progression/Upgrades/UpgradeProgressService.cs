using System.Collections.Generic;

// 책임: 업그레이드 데이터베이스의 노드 조회, 잠금 상태, 구매 가능 여부와 해금 변화를 계산한다.
public sealed class UpgradeProgressService
{
    private readonly UpgradeDatabase upgradeDatabase;
    private readonly Dictionary<int, UpgradeNodeSO> upgradeMap = new Dictionary<int, UpgradeNodeSO>();

    public UpgradeProgressService(UpgradeDatabase upgradeDatabase)
    {
        this.upgradeDatabase = upgradeDatabase;
        BuildLookup();
    }

    public List<UpgradeNodeSO> GetAllUpgrades()
    {
        return upgradeDatabase != null ? upgradeDatabase.allUpgrades : null;
    }

    public UpgradeNodeSO GetUpgradeByID(int id)
    {
        upgradeMap.TryGetValue(id, out var node);
        return node;
    }

    public LockType GetNodeStatus(int id)
    {
        UpgradeSaveData data = TryGetSaveData();
        if (data == null)
            return LockType.Locked;

        if (data.purchasedIDs.Contains(id))
            return LockType.Purchased;

        if (data.unlockedIDs.Contains(id))
            return LockType.UnLocked;

        return LockType.Locked;
    }

    public bool CheckAndUnlockNodes()
    {
        if (upgradeDatabase == null)
            return false;

        UpgradeSaveData data = TryGetSaveData();
        if (data == null)
            return false;

        bool isChanged = false;
        foreach (var node in upgradeDatabase.allUpgrades)
        {
            if (node == null)
                continue;

            if (data.purchasedIDs.Contains(node.nodeID) || data.unlockedIDs.Contains(node.nodeID))
                continue;

            if (node.requiredParentIDs == null || node.requiredParentIDs.Count == 0)
            {
                data.unlockedIDs.Add(node.nodeID);
                isChanged = true;
                continue;
            }

            if (AreParentsPurchased(node, data.purchasedIDs))
            {
                data.unlockedIDs.Add(node.nodeID);
                isChanged = true;
            }
        }

        return isChanged;
    }

    public bool TryPurchase(int id, out UpgradeNodeSO node)
    {
        node = GetUpgradeByID(id);
        if (node == null || GetNodeStatus(id) != LockType.UnLocked)
            return false;

        UpgradeSaveData data = TryGetSaveData();
        if (data == null)
            return false;

        data.unlockedIDs.Remove(id);
        if (!data.purchasedIDs.Contains(id))
            data.purchasedIDs.Add(id);

        return true;
    }

    public void RevertPurchase(int id)
    {
        UpgradeSaveData data = TryGetSaveData();
        if (data == null)
            return;

        data.purchasedIDs.Remove(id);
        if (!data.unlockedIDs.Contains(id))
            data.unlockedIDs.Add(id);
    }

    private void BuildLookup()
    {
        upgradeMap.Clear();
        if (upgradeDatabase == null || upgradeDatabase.allUpgrades == null)
            return;

        foreach (var node in upgradeDatabase.allUpgrades)
        {
            if (node == null || upgradeMap.ContainsKey(node.nodeID))
                continue;

            upgradeMap.Add(node.nodeID, node);
        }
    }

    private static bool AreParentsPurchased(UpgradeNodeSO node, List<int> purchasedIDs)
    {
        if (node.requiredParentIDs == null || node.requiredParentIDs.Count == 0)
            return true;

        foreach (int parentID in node.requiredParentIDs)
        {
            if (!purchasedIDs.Contains(parentID))
                return false;
        }

        return true;
    }

    private static UpgradeSaveData TryGetSaveData()
    {
        GameData data = GameDataStore.EnsureData();
        if (data == null)
            return null;

        if (data.upgradeData == null)
            data.upgradeData = new UpgradeSaveData();

        return data.upgradeData;
    }
}
