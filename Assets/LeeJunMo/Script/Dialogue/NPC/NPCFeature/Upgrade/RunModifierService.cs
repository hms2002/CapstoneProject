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
    }
}

public class RunModifierService : MonoBehaviour
{
    public static RunModifierService Instance { get; private set; }

    private static bool s_isQuitting;
    private const string UpgradeNodeResourcesPath = "Upgrades/Nodes";

    private GraveRunModifierDelta graveModifiers;
    private ChestRunModifierDelta chestModifiers;
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
    }

    public void RebuildFromPurchasedUpgrades()
    {
        hasLoadedFromSave = false;
        EnsureLoadedFromPurchases();
    }

    private void EnsureLoadedFromPurchases()
    {
        if (hasLoadedFromSave)
            return;

        graveModifiers = default;
        chestModifiers = default;

        UpgradeSaveData saveData = TryGetUpgradeSaveData();
        if (saveData == null || saveData.purchasedIDs == null || saveData.purchasedIDs.Count == 0)
        {
            hasLoadedFromSave = true;
            return;
        }

        UpgradeNodeSO[] nodes = LoadUpgradeNodes();
        if (nodes == null || nodes.Length == 0)
        {
            hasLoadedFromSave = true;
            return;
        }

        foreach (int purchasedId in saveData.purchasedIDs)
        {
            UpgradeNodeSO node = FindNodeById(nodes, purchasedId);
            if (node == null || node.effects == null)
                continue;

            foreach (UpgradeEffectSO effect in node.effects)
            {
                if (effect is GraveRunModifierUpgradeEffect graveEffect)
                {
                    GraveRunModifierDelta delta = graveEffect.Delta;
                    graveModifiers.Add(delta);
                    continue;
                }

                if (effect is ChestRunModifierUpgradeEffect chestEffect)
                {
                    ChestRunModifierDelta delta = chestEffect.Delta;
                    chestModifiers.Add(delta);
                }
            }
        }

        hasLoadedFromSave = true;
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
