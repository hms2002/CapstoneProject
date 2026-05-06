using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public struct GraveRunModifierDelta
{
    public int weaponGraveMinBonus;
    public int weaponGraveMaxBonus;
    public int relicGraveMinBonus;
    public int relicGraveMaxBonus;
    public int weaponDropMinBonus;
    public int weaponDropMaxBonus;
    public int relicDropMinBonus;
    public int relicDropMaxBonus;
    public float extraRareChance;
    public float extraEpicChance;

    public static GraveRunModifierDelta FromSave(RunModifierSaveData data)
    {
        if (data == null)
            return default;

        return new GraveRunModifierDelta
        {
            weaponGraveMinBonus = data.weaponGraveMinBonus,
            weaponGraveMaxBonus = data.weaponGraveMaxBonus != 0 ? data.weaponGraveMaxBonus : data.extraWeaponGraveCount,
            relicGraveMinBonus = data.relicGraveMinBonus,
            relicGraveMaxBonus = data.relicGraveMaxBonus != 0 ? data.relicGraveMaxBonus : data.extraRelicGraveCount,
            weaponDropMinBonus = data.weaponDropMinBonus,
            weaponDropMaxBonus = data.weaponDropMaxBonus != 0 ? data.weaponDropMaxBonus : data.extraWeaponDropCount,
            relicDropMinBonus = data.relicDropMinBonus,
            relicDropMaxBonus = data.relicDropMaxBonus != 0 ? data.relicDropMaxBonus : data.extraRelicDropCount,
            extraRareChance = data.extraRareChance,
            extraEpicChance = data.extraEpicChance
        };
    }

    public void Add(GraveRunModifierDelta other)
    {
        weaponGraveMinBonus += other.weaponGraveMinBonus;
        weaponGraveMaxBonus += other.weaponGraveMaxBonus;
        relicGraveMinBonus += other.relicGraveMinBonus;
        relicGraveMaxBonus += other.relicGraveMaxBonus;
        weaponDropMinBonus += other.weaponDropMinBonus;
        weaponDropMaxBonus += other.weaponDropMaxBonus;
        relicDropMinBonus += other.relicDropMinBonus;
        relicDropMaxBonus += other.relicDropMaxBonus;
        extraRareChance += other.extraRareChance;
        extraEpicChance += other.extraEpicChance;
    }
}

[System.Serializable]
public struct ChestRunModifierDelta
{
    public int chestWeaponMinBonus;
    public int chestWeaponMaxBonus;
    public int chestRelicMinBonus;
    public int chestRelicMaxBonus;
    public int chestRefreshCount;
    public float relicLevelBonusChance;

    public static ChestRunModifierDelta FromSave(RunModifierSaveData data)
    {
        if (data == null)
            return default;

        return new ChestRunModifierDelta
        {
            chestWeaponMinBonus = data.chestWeaponMinBonus,
            chestWeaponMaxBonus = data.chestWeaponMaxBonus,
            chestRelicMinBonus = data.chestRelicMinBonus,
            chestRelicMaxBonus = data.chestRelicMaxBonus
        };
    }

    public void Add(ChestRunModifierDelta other)
    {
        chestWeaponMinBonus += other.chestWeaponMinBonus;
        chestWeaponMaxBonus += other.chestWeaponMaxBonus;
        chestRelicMinBonus += other.chestRelicMinBonus;
        chestRelicMaxBonus += other.chestRelicMaxBonus;
        chestRefreshCount += other.chestRefreshCount;
        relicLevelBonusChance += other.relicLevelBonusChance;
    }
}

[System.Serializable]
public struct ShopRunModifierDelta
{
    public bool shopEnabled;
    public int shopSlotBonus;
    public float discountRate;
    public int shopRefreshCount;

    public void Add(ShopRunModifierDelta other)
    {
        shopEnabled |= other.shopEnabled;
        shopSlotBonus += other.shopSlotBonus;
        discountRate += other.discountRate;
        shopRefreshCount += other.shopRefreshCount;
    }
}

[System.Serializable]
public struct BossRunModifierDelta
{
    public int bossFieldHealPickupBonus;
    public int bossChestWeaponMinBonus;
    public int bossChestWeaponMaxBonus;
    public int bossChestRelicMinBonus;
    public int bossChestRelicMaxBonus;

    public ChestRunModifierDelta ToChestModifierDelta()
    {
        return new ChestRunModifierDelta
        {
            chestWeaponMinBonus = bossChestWeaponMinBonus,
            chestWeaponMaxBonus = bossChestWeaponMaxBonus,
            chestRelicMinBonus = bossChestRelicMinBonus,
            chestRelicMaxBonus = bossChestRelicMaxBonus
        };
    }

    public void Add(BossRunModifierDelta other)
    {
        bossFieldHealPickupBonus += other.bossFieldHealPickupBonus;
        bossChestWeaponMinBonus += other.bossChestWeaponMinBonus;
        bossChestWeaponMaxBonus += other.bossChestWeaponMaxBonus;
        bossChestRelicMinBonus += other.bossChestRelicMinBonus;
        bossChestRelicMaxBonus += other.bossChestRelicMaxBonus;
    }
}

public class RunModifierService : MonoBehaviour
{
    public static RunModifierService Instance { get; private set; }

    public event System.Action OnModifiersChanged;

    private static bool s_isQuitting;
    private const string UpgradeNodeResourcesPath = "Upgrades/Nodes";

    private GraveRunModifierDelta graveModifiers;
    private ChestRunModifierDelta chestModifiers;
    private ShopRunModifierDelta shopModifiers;
    private BossRunModifierDelta bossModifiers;
    private bool hasLoadedFromSave;
    private UpgradeNodeSO[] cachedUpgradeNodes;

    public GraveRunModifierDelta GraveModifiers
    {
        get
        {
            EnsureLoadedFromPurchases();
            return graveModifiers;
        }
    }

    public ChestRunModifierDelta ChestModifiers
    {
        get
        {
            EnsureLoadedFromPurchases();
            return chestModifiers;
        }
    }

    public ShopRunModifierDelta ShopModifiers
    {
        get
        {
            EnsureLoadedFromPurchases();
            return shopModifiers;
        }
    }

    public BossRunModifierDelta BossModifiers
    {
        get
        {
            EnsureLoadedFromPurchases();
            return bossModifiers;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        GameObject go = new GameObject(nameof(RunModifierService));
        go.AddComponent<RunModifierService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        EnsureLoadedFromPurchases();
    }

    public void ReloadFromSave()
    {
        hasLoadedFromSave = false;
        EnsureLoadedFromPurchases();
        OnModifiersChanged?.Invoke();
    }

    public void RebuildFromPurchasedUpgrades()
    {
        hasLoadedFromSave = false;
        EnsureLoadedFromPurchases();
        OnModifiersChanged?.Invoke();
    }

    private void EnsureLoadedFromPurchases()
    {
        if (hasLoadedFromSave)
            return;

        graveModifiers = default;
        chestModifiers = default;
        shopModifiers = default;
        bossModifiers = default;

        UpgradeSaveData saveData = TryGetUpgradeSaveData();
        if (saveData == null)
        {
            ApplyAffectionModifiers();
            hasLoadedFromSave = true;
            return;
        }

        if (saveData.purchasedIDs != null && saveData.purchasedIDs.Count > 0)
        {
            UpgradeNodeSO[] nodes = LoadUpgradeNodes();
            if (nodes != null && nodes.Length > 0)
            {
                foreach (int purchasedId in saveData.purchasedIDs)
                {
                    UpgradeNodeSO node = FindNodeById(nodes, purchasedId);
                    if (node == null || node.effects == null)
                        continue;

                    foreach (UpgradeEffectSO effect in node.effects)
                    {
                        ApplyUpgradeModifier(effect);
                    }
                }
            }
        }

        ApplyAffectionModifiers();
        hasLoadedFromSave = true;
    }

    private void ApplyUpgradeModifier(UpgradeEffectSO effect)
    {
        if (effect is GraveRunModifierUpgradeEffect graveEffect)
        {
            graveModifiers.Add(graveEffect.Delta);
            return;
        }

        if (effect is ChestRunModifierUpgradeEffect chestEffect)
        {
            chestModifiers.Add(chestEffect.Delta);
            return;
        }

        if (effect is ShopRunModifierUpgradeEffect shopEffect)
        {
            shopModifiers.Add(shopEffect.Delta);
        }
    }

    private void ApplyAffectionModifiers()
    {
        NPCManager npcManager = NPCManager.Instance;
        if (npcManager == null)
            return;

        Dictionary<int, int> affectionAmounts = BuildAffectionAmountMap();
        foreach (KeyValuePair<int, int> entry in affectionAmounts)
        {
            NPCData npcData = npcManager.GetNPCData(entry.Key);
            if (npcData?.affectionRewards == null)
                continue;

            foreach (AffectionReward reward in npcData.affectionRewards)
            {
                if (reward.effect == null || reward.targetLevel > entry.Value)
                    continue;

                if (reward.effect is BossAffectionRunModifierEffect bossEffect)
                    bossModifiers.Add(bossEffect.Delta);
            }
        }
    }

    private static Dictionary<int, int> BuildAffectionAmountMap()
    {
        var amounts = new Dictionary<int, int>();

        GameData data = GameDataManager.Instance != null ? GameDataManager.Instance.Data : null;
        if (data?.affectionData?.affectionRecords != null)
        {
            foreach (AffectionRecord record in data.affectionData.affectionRecords)
            {
                if (record != null)
                    amounts[record.npcId] = record.amount;
            }
        }

        GamePlayData runData = GamePlayDataManager.Instance != null ? GamePlayDataManager.Instance.Data : null;
        if (runData?.pendingRunAffectionChanges != null)
        {
            foreach (PendingRunAffectionChange change in runData.pendingRunAffectionChanges)
            {
                if (change == null)
                    continue;

                amounts.TryGetValue(change.npcId, out int currentAmount);
                amounts[change.npcId] = currentAmount + change.delta;
            }
        }

        return amounts;
    }

    private UpgradeNodeSO[] LoadUpgradeNodes()
    {
        if (cachedUpgradeNodes != null && cachedUpgradeNodes.Length > 0)
            return cachedUpgradeNodes;

        cachedUpgradeNodes = Resources.LoadAll<UpgradeNodeSO>(UpgradeNodeResourcesPath);

        if ((cachedUpgradeNodes == null || cachedUpgradeNodes.Length == 0) && UpgradeManager.Instance != null)
        {
            var upgrades = UpgradeManager.Instance.GetAllUpgrades();
            if (upgrades != null && upgrades.Count > 0)
                cachedUpgradeNodes = upgrades.ToArray();
        }

        return cachedUpgradeNodes;
    }

    private static UpgradeNodeSO FindNodeById(UpgradeNodeSO[] nodes, int nodeId)
    {
        if (nodes == null)
            return null;

        for (int i = 0; i < nodes.Length; i++)
        {
            UpgradeNodeSO node = nodes[i];
            if (node != null && node.nodeID == nodeId)
                return node;
        }

        return null;
    }

    private static UpgradeSaveData TryGetUpgradeSaveData()
    {
        if (GameDataManager.Instance == null || GameDataManager.Instance.Data == null)
            return null;

        if (GameDataManager.Instance.Data.upgradeData == null)
            GameDataManager.Instance.Data.upgradeData = new UpgradeSaveData();

        return GameDataManager.Instance.Data.upgradeData;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }
}
