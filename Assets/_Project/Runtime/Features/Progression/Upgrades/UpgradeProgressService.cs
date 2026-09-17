using System.Collections.Generic;

// 책임: 업그레이드 데이터베이스의 노드 조회, 잠금 상태, 구매 가능 여부와 해금 변화를 계산한다.
public sealed class UpgradeProgressService
{
    // Exhibition preset: travel speed, bag I/II, relic unlock, graves, chest reroll, shop/refresh/discount.
    private static readonly int[] ExhibitionNodeIds =
    {
        -198029508, -993005279, 749890120, 1433597785, -1492335095,
        674725477, -2095978215, -1633135072, 484471810
    };

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

    public bool GrantExhibitionDefaults()
    {
        UpgradeSaveData data = TryGetSaveData();
        if (data == null)
            return false;

        // Validate the whole preset before changing progress. Item unlocks need their ready owner.
        foreach (int id in ExhibitionNodeIds)
        {
            UpgradeNodeSO node = GetUpgradeByID(id);
            if (node == null)
                return false;

            if (data.purchasedIDs.Contains(id) || node.effects == null)
                continue;

            foreach (UpgradeEffectSO effect in node.effects)
            {
                if (effect is ItemUnlockUpgradeEffectSO &&
                    (ItemManager.Instance == null || !ItemManager.Instance.IsReady))
                    return false;
            }
        }

        bool changed = false;
        foreach (int id in ExhibitionNodeIds)
        {
            if (data.purchasedIDs.Contains(id))
                continue;

            data.unlockedIDs.Remove(id);
            data.purchasedIDs.Add(id);
            changed = true;

            UpgradeNodeSO node = GetUpgradeByID(id);
            if (node.effects == null)
                continue;

            // Player effects and run modifiers use the existing reapply/rebuild lifecycle.
            foreach (UpgradeEffectSO effect in node.effects)
            {
                if (effect is ItemUnlockUpgradeEffectSO)
                    effect.ApplyOnPurchase(null);
            }
        }

        return changed;
    }

    public bool TryGrantExhibitionOpeningReward(CurrencyManager currency)
    {
        UpgradeSaveData data = TryGetSaveData();
        if (data == null)
            return false;

        foreach (int id in ExhibitionNodeIds)
        {
            if (!data.purchasedIDs.Contains(id))
                return false;
        }

        if (data.exhibitionOpeningRewardGranted)
            return true;

        // The reward is durable hub currency, never an uncommitted active-run delta.
        if (currency == null || RunSessionStore.IsRunActive)
            return false;

        // Set the marker before the currency event/save, including reentrant UI listeners.
        data.exhibitionOpeningRewardGranted = true;
        currency.AddMagicStone(100);
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
