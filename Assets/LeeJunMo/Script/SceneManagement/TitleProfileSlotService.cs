using System;
using System.Collections.Generic;
using UnityEngine;

public enum TitleProfileSlotPanelMode
{
    NewGame = 0,
    Continue = 1
}

public enum TitleProfileLaunchAction
{
    None = 0,
    StartNewRun = 1,
    ContinueRun = 2
}

[Serializable]
public struct TitleProfileLaunchRequest
{
    [SerializeField] private int slotIndex;
    [SerializeField] private TitleProfileLaunchAction action;
    [SerializeField] private string targetSceneName;

    public TitleProfileLaunchRequest(int slotIndex, TitleProfileLaunchAction action, string targetSceneName)
    {
        this.slotIndex = slotIndex;
        this.action = action;
        this.targetSceneName = targetSceneName;
    }

    public int SlotIndex => slotIndex;
    public TitleProfileLaunchAction Action => action;
    public string TargetSceneName => targetSceneName;
    public bool IsValid => action != TitleProfileLaunchAction.None && !string.IsNullOrWhiteSpace(targetSceneName);
}

public static class TitleProfileLaunchContext
{
    private static TitleProfileLaunchRequest pendingRequest;

    public static bool HasPendingRequest => pendingRequest.IsValid;
    public static TitleProfileLaunchRequest PendingRequest => pendingRequest;

    public static void SetPendingRequest(TitleProfileLaunchRequest request)
    {
        pendingRequest = request;
    }

    public static bool TryConsumePendingRequest(out TitleProfileLaunchRequest request)
    {
        request = pendingRequest;
        pendingRequest = default;
        return request.IsValid;
    }

    public static void Clear()
    {
        pendingRequest = default;
    }
}

[Serializable]
public struct TitleProfileSlotSummary
{
    [SerializeField] private int slotIndex;
    [SerializeField] private bool hasProfile;
    [SerializeField] private bool hasActiveRun;
    [SerializeField] private string slotLabel;
    [SerializeField] private string runLabel;
    [SerializeField] private string metaProgressLabel;
    [SerializeField] private string lastPlayedLabel;

    public TitleProfileSlotSummary(
        int slotIndex,
        bool hasProfile,
        bool hasActiveRun,
        string slotLabel,
        string runLabel,
        string metaProgressLabel,
        string lastPlayedLabel)
    {
        this.slotIndex = slotIndex;
        this.hasProfile = hasProfile;
        this.hasActiveRun = hasActiveRun;
        this.slotLabel = slotLabel;
        this.runLabel = runLabel;
        this.metaProgressLabel = metaProgressLabel;
        this.lastPlayedLabel = lastPlayedLabel;
    }

    public int SlotIndex => slotIndex;
    public bool HasProfile => hasProfile;
    public bool HasActiveRun => hasActiveRun;
    public string SlotLabel => slotLabel;
    public string RunLabel => runLabel;
    public string MetaProgressLabel => metaProgressLabel;
    public string LastPlayedLabel => lastPlayedLabel;
}

[Serializable]
public sealed class TitleProfileSlotDebugState
{
    [SerializeField] private bool hasProfile;
    [SerializeField] private bool hasActiveRun;
    [SerializeField] private string slotLabelOverride = string.Empty;
    [SerializeField] private string runLabel = "진행 중 런 없음";
    [SerializeField] private string metaProgressLabel = "해금 진행도 없음";
    [SerializeField] private string lastPlayedLabel = "최근 플레이 없음";

    public TitleProfileSlotSummary BuildSummary(int slotIndex)
    {
        string resolvedSlotLabel = string.IsNullOrWhiteSpace(slotLabelOverride)
            ? $"슬롯 {slotIndex + 1}"
            : slotLabelOverride;

        string resolvedRunLabel = hasActiveRun
            ? NormalizeLabel(runLabel, "진행 중 런")
            : "진행 중 런 없음";

        string resolvedMetaLabel = hasProfile
            ? NormalizeLabel(metaProgressLabel, "메타 진행도 없음")
            : "새 프로필 생성 가능";

        string resolvedLastPlayed = hasProfile
            ? NormalizeLabel(lastPlayedLabel, "최근 플레이 기록 없음")
            : "최근 플레이 없음";

        return new TitleProfileSlotSummary(
            slotIndex,
            hasProfile,
            hasActiveRun,
            resolvedSlotLabel,
            resolvedRunLabel,
            resolvedMetaLabel,
            resolvedLastPlayed);
    }

    private static string NormalizeLabel(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}

[DisallowMultipleComponent]
public sealed class TitleProfileSlotService : MonoBehaviour
{
    public static TitleProfileSlotService Instance { get; private set; }

    [Header("Slots")]
    [SerializeField, Min(1)] private int slotCount = 3;
    [SerializeField] private string targetSceneName = "ProtoTypeHub";

    [Header("Debug Preview")]
    [SerializeField] private bool useDebugSlotData = true;
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

        if (!useDebugSlotData || slotIndex >= debugSlots.Count || debugSlots[slotIndex] == null)
            return BuildEmptySummary(slotIndex);

        return debugSlots[slotIndex].BuildSummary(slotIndex);
    }

    public bool HasAnyContinuableRun()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (GetSlotSummary(i).HasActiveRun)
                return true;
        }

        return false;
    }

    public bool CanContinue(int slotIndex)
    {
        return GetSlotSummary(slotIndex).HasActiveRun;
    }

    public bool NeedsOverwriteConfirmationForNewGame(int slotIndex)
    {
        return GetSlotSummary(slotIndex).HasActiveRun;
    }

    public bool TryCreateLaunchRequest(
        TitleProfileSlotPanelMode mode,
        int slotIndex,
        out TitleProfileLaunchRequest request)
    {
        request = default;

        if (slotIndex < 0 || slotIndex >= SlotCount || string.IsNullOrWhiteSpace(targetSceneName))
            return false;

        TitleProfileSlotSummary summary = GetSlotSummary(slotIndex);
        TitleProfileLaunchAction action = mode switch
        {
            TitleProfileSlotPanelMode.NewGame => TitleProfileLaunchAction.StartNewRun,
            TitleProfileSlotPanelMode.Continue when summary.HasActiveRun => TitleProfileLaunchAction.ContinueRun,
            _ => TitleProfileLaunchAction.None
        };

        if (action == TitleProfileLaunchAction.None)
            return false;

        request = new TitleProfileLaunchRequest(slotIndex, action, targetSceneName);
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
            slotLabel: $"슬롯 {slotIndex + 1}",
            runLabel: "진행 중 런 없음",
            metaProgressLabel: "새 프로필 생성 가능",
            lastPlayedLabel: "최근 플레이 없음");
    }
}
