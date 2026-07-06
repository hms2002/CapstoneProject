using UnityEngine;

// 책임: 무덤/상자/상점/보스 보상 기반 런 변형치를 저장 데이터와 현재 런 상태에 맞춰 집계한다.
public class RunModifierService : MonoBehaviour
{
    public static RunModifierService Instance { get; private set; }

    public event System.Action OnModifiersChanged;

    private static bool s_isQuitting;
    private const string UpgradeNodeResourcesPath = "Upgrades/Nodes";

    private GraveRunModifierDelta graveModifiers;
    private ChestRunModifierDelta chestModifiers;
    private ShopRunModifierDelta shopModifiers;
    private BossRewardModifierAggregate bossRewardModifiers;
    private bool hasLoadedFromSave;
    private UpgradeNodeSO[] cachedUpgradeNodes;

    public GraveRunModifierDelta GraveModifiers
    {
        get
        {
            return RewardSnapshot.GraveModifiers;
        }
    }

    public ChestRunModifierDelta ChestModifiers
    {
        get
        {
            return RewardSnapshot.ChestModifiers;
        }
    }

    public ShopRunModifierDelta ShopModifiers
    {
        get
        {
            return RewardSnapshot.ShopModifiers;
        }
    }

    public BossRunModifierDelta BossModifiers
    {
        get
        {
            return RewardSnapshot.BossModifiers;
        }
    }

    public BossRewardModifierAggregate BossRewardModifiers
    {
        get
        {
            return RewardSnapshot.BossRewardModifiers;
        }
    }

    public RunRewardModifierSnapshot RewardSnapshot
    {
        get
        {
            EnsureLoadedFromPurchases();
            return new RunRewardModifierSnapshot(
                graveModifiers,
                chestModifiers,
                shopModifiers,
                bossRewardModifiers);
        }
    }

    public static RunRewardModifierSnapshot CurrentRewardSnapshot =>
        Instance != null ? Instance.RewardSnapshot : RunRewardModifierSnapshot.Empty;

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
        RefreshFromSources();
    }

    public void RebuildFromPurchasedUpgrades()
    {
        RefreshFromSources();
    }

    private void EnsureLoadedFromPurchases()
    {
        if (hasLoadedFromSave)
            return;

        UpgradeSaveData saveData = TryGetUpgradeSaveData();
        RunModifierRebuildResult result = RunModifierRebuildService.Rebuild(
            new RunModifierRebuildRequest(
                saveData,
                cachedUpgradeNodes,
                UpgradeManager.Instance,
                NPCManager.Instance,
                GameDataStore.Data,
                RunSessionStore.Data,
                UpgradeNodeResourcesPath));

        ApplyRebuildResult(result);
        hasLoadedFromSave = true;
    }

    private void RefreshFromSources()
    {
        hasLoadedFromSave = false;
        cachedUpgradeNodes = null;
        EnsureLoadedFromPurchases();
        OnModifiersChanged?.Invoke();
    }

    private void ApplyRebuildResult(RunModifierRebuildResult result)
    {
        cachedUpgradeNodes = result.CachedUpgradeNodes;
        graveModifiers = result.GraveModifiers;
        chestModifiers = result.ChestModifiers;
        shopModifiers = result.ShopModifiers;
        bossRewardModifiers = result.BossRewardModifiers;
    }

    private static UpgradeSaveData TryGetUpgradeSaveData()
    {
        GameData data = GameDataStore.Data;
        if (data == null)
            return null;

        if (data.upgradeData == null)
            data.upgradeData = new UpgradeSaveData();

        return data.upgradeData;
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
