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

    private GraveRunModifierDelta graveModifiers;
    private ChestRunModifierDelta chestModifiers;
    private bool hasLoadedFromSave;

    public GraveRunModifierDelta GraveModifiers
    {
        get
        {
            EnsureLoadedFromSave();
            return graveModifiers;
        }
    }

    public ChestRunModifierDelta ChestModifiers
    {
        get
        {
            EnsureLoadedFromSave();
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
        EnsureLoadedFromSave();
    }

    public void ReloadFromSave()
    {
        hasLoadedFromSave = false;
        EnsureLoadedFromSave();
    }

    public void AddGraveModifier(GraveRunModifierDelta delta)
    {
        EnsureLoadedFromSave();
        graveModifiers.Add(delta);
        SyncToSave();
    }

    public void AddChestModifier(ChestRunModifierDelta delta)
    {
        EnsureLoadedFromSave();
        chestModifiers.Add(delta);
        SyncToSave();
    }

    private void EnsureLoadedFromSave()
    {
        if (hasLoadedFromSave)
            return;

        if (GameDataManager.Instance == null || GameDataManager.Instance.Data == null)
            return;

        RunModifierSaveData saveData = EnsureSaveData();
        graveModifiers = GraveRunModifierDelta.FromSave(saveData);
        chestModifiers = ChestRunModifierDelta.FromSave(saveData);
        hasLoadedFromSave = true;
    }

    private void SyncToSave()
    {
        RunModifierSaveData saveData = EnsureSaveData();
        saveData.extraWeaponGraveCount = 0;
        saveData.extraRelicGraveCount = 0;
        saveData.extraWeaponDropCount = 0;
        saveData.extraRelicDropCount = 0;
        saveData.weaponGraveMinBonus = graveModifiers.weaponGraveMinBonus;
        saveData.weaponGraveMaxBonus = graveModifiers.weaponGraveMaxBonus;
        saveData.relicGraveMinBonus = graveModifiers.relicGraveMinBonus;
        saveData.relicGraveMaxBonus = graveModifiers.relicGraveMaxBonus;
        saveData.weaponDropMinBonus = graveModifiers.weaponDropMinBonus;
        saveData.weaponDropMaxBonus = graveModifiers.weaponDropMaxBonus;
        saveData.relicDropMinBonus = graveModifiers.relicDropMinBonus;
        saveData.relicDropMaxBonus = graveModifiers.relicDropMaxBonus;
        saveData.chestWeaponMinBonus = chestModifiers.chestWeaponMinBonus;
        saveData.chestWeaponMaxBonus = chestModifiers.chestWeaponMaxBonus;
        saveData.chestRelicMinBonus = chestModifiers.chestRelicMinBonus;
        saveData.chestRelicMaxBonus = chestModifiers.chestRelicMaxBonus;
        saveData.extraRareChance = graveModifiers.extraRareChance;
        saveData.extraEpicChance = graveModifiers.extraEpicChance;
    }

    private static RunModifierSaveData EnsureSaveData()
    {
        if (GameDataManager.Instance == null)
            return new RunModifierSaveData();

        if (GameDataManager.Instance.Data == null)
            GameDataManager.Instance.LoadData();

        if (GameDataManager.Instance.Data.upgradeData == null)
            GameDataManager.Instance.Data.upgradeData = new UpgradeSaveData();

        if (GameDataManager.Instance.Data.upgradeData.runModifierData == null)
            GameDataManager.Instance.Data.upgradeData.runModifierData = new RunModifierSaveData();

        return GameDataManager.Instance.Data.upgradeData.runModifierData;
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
