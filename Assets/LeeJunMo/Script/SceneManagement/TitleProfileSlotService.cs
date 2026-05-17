using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TitleProfileSlotService : MonoBehaviour
{
    public static TitleProfileSlotService Instance { get; private set; }

    [Header("Slots")]
    [SerializeField, Min(1)] private int slotCount = 3;
    [SerializeField] private string targetSceneName = "ProtoTypeHub";
    [SerializeField] private UpgradeDatabase upgradeDatabase;

    [Header("Debug Preview")]
    [SerializeField] private bool useDebugSlotData;
    [SerializeField] private List<TitleProfileSlotDebugState> debugSlots = new();

    public int SlotCount => Mathf.Max(1, slotCount);

    public static TitleProfileSlotService EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        TitleProfileSlotService existing = FindFirstObjectByType<TitleProfileSlotService>();
        if (existing != null)
        {
            Instance = existing;
            existing.EnsureDebugSlots();
            return existing;
        }

        var host = new GameObject(nameof(TitleProfileSlotService));
        Instance = host.AddComponent<TitleProfileSlotService>();
        Instance.EnsureDebugSlots();
        return Instance;
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
        EnsureDebugSlots();
    }

    private void OnValidate()
    {
        EnsureDebugSlots();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public TitleProfileSlotSummary GetSlotSummary(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            return BuildEmptySummary(slotIndex);

        EnsureDebugSlots();

        if (useDebugSlotData)
        {
            if (slotIndex >= debugSlots.Count || debugSlots[slotIndex] == null)
                return BuildEmptySummary(slotIndex);

            return debugSlots[slotIndex].BuildSummary(slotIndex);
        }

        return BuildRealSummary(slotIndex);
    }

    public TitleProfileLaunchAction GetPrimaryActionForSlot(int slotIndex)
    {
        TitleProfileSlotSummary summary = GetSlotSummary(slotIndex);
        if (summary.HasProfile || summary.HasActiveRun)
            return TitleProfileLaunchAction.ContinueRun;

        return TitleProfileLaunchAction.StartNewRun;
    }

    public bool CanDeleteSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            return false;

        return GetSlotSummary(slotIndex).HasProfile;
    }

    public bool TryCreateLaunchRequest(int slotIndex, out TitleProfileLaunchRequest request)
    {
        request = default;

        if (slotIndex < 0 || slotIndex >= SlotCount || string.IsNullOrWhiteSpace(targetSceneName))
            return false;

        TitleProfileLaunchAction action = GetPrimaryActionForSlot(slotIndex);
        if (action == TitleProfileLaunchAction.None)
            return false;

        request = new TitleProfileLaunchRequest(slotIndex, action, targetSceneName);
        return true;
    }

    public bool DeleteSlot(int slotIndex)
    {
        if (!CanDeleteSlot(slotIndex))
            return false;

        EnsureDebugSlots();

        if (useDebugSlotData && slotIndex < debugSlots.Count)
        {
            debugSlots[slotIndex] = new TitleProfileSlotDebugState();
            return true;
        }

        GameDataRepository repository = new GameDataRepository(slotIndex);
        repository.Delete();

        if (GameDataManager.Instance != null)
            GameDataManager.Instance.ResetLoadedSlotIfActive(slotIndex);

        return true;
    }

    private void EnsureDebugSlots()
    {
        if (debugSlots == null)
            debugSlots = new List<TitleProfileSlotDebugState>();

        while (debugSlots.Count < SlotCount)
            debugSlots.Add(new TitleProfileSlotDebugState());

        if (debugSlots.Count > SlotCount)
            debugSlots.RemoveRange(SlotCount, debugSlots.Count - SlotCount);
    }

    private static TitleProfileSlotSummary BuildEmptySummary(int slotIndex)
    {
        return new TitleProfileSlotSummary(
            slotIndex,
            hasProfile: false,
            hasActiveRun: false,
            slotLabel: "\uC2AC\uB86F " + (slotIndex + 1),
            playTimeLabel: "--\uC2DC\uAC04 --\uBD84",
            upgradeProgressLabel: "--%",
            magicStoneLabel: "--\uAC1C",
            clearCountLabel: "--\uD68C");
    }

    private TitleProfileSlotSummary BuildRealSummary(int slotIndex)
    {
        GameDataRepository repository = new GameDataRepository(slotIndex);
        if (!repository.TryLoad(out GameData data) || data == null)
            return BuildEmptySummary(slotIndex);

        bool hasProfile = data.hasInitializedProfile || LooksPopulated(data);
        if (!hasProfile)
            return BuildEmptySummary(slotIndex);

        int purchasedCount = data.upgradeData?.purchasedIDs?.Count ?? 0;
        int totalUpgradeCount = data.knownTotalUpgradeCount > 0
            ? data.knownTotalUpgradeCount
            : ResolveTotalUpgradeCount();
        string upgradeProgressLabel = totalUpgradeCount > 0
            ? Mathf.Clamp(Mathf.RoundToInt((float)purchasedCount / totalUpgradeCount * 100f), 0, 100) + "%"
            : "--%";

        return new TitleProfileSlotSummary(
            slotIndex,
            hasProfile: hasProfile,
            hasActiveRun: false,
            slotLabel: "\uC2AC\uB86F " + (slotIndex + 1),
            playTimeLabel: FormatPlayTimeLabel(data.totalPlaySeconds),
            upgradeProgressLabel: upgradeProgressLabel,
            magicStoneLabel: FormatMagicStoneValue(data.magicStone),
            clearCountLabel: FormatClearCountValue(data.clearCount));
    }

    private int ResolveTotalUpgradeCount()
    {
        if (upgradeDatabase != null && upgradeDatabase.allUpgrades != null && upgradeDatabase.allUpgrades.Count > 0)
            return upgradeDatabase.allUpgrades.Count;

        if (UpgradeManager.Instance != null)
        {
            List<UpgradeNodeSO> upgrades = UpgradeManager.Instance.GetAllUpgrades();
            if (upgrades != null && upgrades.Count > 0)
                return upgrades.Count;
        }

        UpgradeDatabase[] loadedDatabases = Resources.FindObjectsOfTypeAll<UpgradeDatabase>();
        for (int i = 0; i < loadedDatabases.Length; i++)
        {
            UpgradeDatabase candidate = loadedDatabases[i];
            if (candidate == null || candidate.allUpgrades == null || candidate.allUpgrades.Count == 0)
                continue;

            upgradeDatabase = candidate;
            return candidate.allUpgrades.Count;
        }

        return 0;
    }

    private static string FormatPlayTimeLabel(float totalPlaySeconds)
    {
        int safeSeconds = Mathf.Max(0, Mathf.RoundToInt(totalPlaySeconds));
        TimeSpan playTime = TimeSpan.FromSeconds(safeSeconds);
        int totalHours = Mathf.Max(0, (int)playTime.TotalHours);
        return $"{totalHours}\uC2DC\uAC04 {playTime.Minutes}\uBD84";
    }

    private static string FormatMagicStoneValue(int magicStone)
    {
        return $"{Mathf.Max(0, magicStone)}\uAC1C";
    }

    private static string FormatClearCountValue(int clearCount)
    {
        return $"{Mathf.Max(0, clearCount)}\uD68C";
    }

    private static bool LooksPopulated(GameData data)
    {
        if (data == null)
            return false;

        if (data.magicStone > 0 || data.totalPlaySeconds > 0f || data.clearCount > 0)
            return true;

        if (data.upgradeData != null)
        {
            if (data.upgradeData.purchasedIDs != null && data.upgradeData.purchasedIDs.Count > 0)
                return true;

            if (data.upgradeData.unlockedIDs != null && data.upgradeData.unlockedIDs.Count > 1)
                return true;
        }

        if (data.mapData != null
            && data.mapData.stageProgressList != null
            && data.mapData.stageProgressList.Count > 0)
        {
            return true;
        }

        if (data.affectionData != null
            && data.affectionData.affectionRecords != null
            && data.affectionData.affectionRecords.Count > 0)
        {
            return true;
        }

        if (data.itemData != null)
        {
            if (data.itemData.unlockedWeaponIDs != null && data.itemData.unlockedWeaponIDs.Count > 0)
                return true;

            if (data.itemData.unlockedRelicIDs != null && data.itemData.unlockedRelicIDs.Count > 0)
                return true;
        }

        return false;
    }
}
