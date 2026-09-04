using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

/// <summary>
/// 책임:
/// - 선택된 룸 라이브러리와 생성 설정으로 레이아웃 조립을 요청하고 결과를 DungeonRoomBuilder에 전달한다.
/// - 방 개수와 방 크기 기반 가변 복도 설정을 포함한 한 번의 던전 생성 진입점, 마지막 생성 결과의 런타임 수명을 소유한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonGenerator : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private DungeonGenerationProfileSO generationProfile;
    [SerializeField] private RoomThemeLibrarySO roomLibrary;
    [SerializeField] private DungeonRoomBuilder roomBuilder;

    [Header("Generation")]
    [SerializeField] private int seed = 12345;
    [SerializeField] private string dungeonStateId;
    [SerializeField] private DungeonReentryPolicy reentryPolicy = DungeonReentryPolicy.RegenerateOnEntry;
    [SerializeField] private DungeonLayoutPolicySO layoutPolicy;
    [SerializeField, Min(1)] private int roomCount = 8;
    [SerializeField] private bool includeBossRoom = true;
    [SerializeField, Min(1)] private int maxPlacementAttemptsPerRoom = 128;
    [FormerlySerializedAs("corridorLength")]
    [SerializeField, Min(0)] private int minimumCorridorLength = 2;
    [SerializeField, Range(0f, 1f)] private float corridorLengthPerRoomCell = 0.05f;
    [SerializeField, Range(0, 32)] private int corridorLengthVariation = 2;
    [SerializeField] private bool generateOnStart = true;

    private DungeonLayoutAssembler layoutAssembler;

    public DungeonGenerationProfileSO GenerationProfile => generationProfile;
    public RoomThemeLibrarySO RoomLibrary => generationProfile != null
        ? generationProfile.RoomLibrary
        : roomLibrary;
    public DungeonRoomBuilder RoomBuilder => roomBuilder;
    public DungeonLayoutPolicySO LayoutPolicy => generationProfile != null
        ? generationProfile.LayoutPolicy
        : layoutPolicy;
    public int Seed => generationProfile != null ? generationProfile.Seed : seed;
    public int RoomCount => generationProfile != null ? generationProfile.RoomCount : roomCount;
    public bool IncludeBossRoom => generationProfile != null
        ? generationProfile.IncludeBossRoom
        : includeBossRoom;
    public int MaxPlacementAttemptsPerRoom => generationProfile != null
        ? generationProfile.MaxPlacementAttemptsPerRoom
        : maxPlacementAttemptsPerRoom;
    public int MinimumCorridorLength => generationProfile != null
        ? generationProfile.MinimumCorridorLength
        : minimumCorridorLength;
    public float CorridorLengthPerRoomCell => generationProfile != null
        ? generationProfile.CorridorLengthPerRoomCell
        : corridorLengthPerRoomCell;
    public int CorridorLengthVariation => generationProfile != null
        ? generationProfile.CorridorLengthVariation
        : corridorLengthVariation;
    public string DungeonStateId => ResolveDungeonStateId();
    public DungeonReentryPolicy ReentryPolicy => reentryPolicy;
    public DungeonLayoutResult LastLayout { get; private set; }
    public int LastGenerationSeed { get; private set; }
    public bool HasCompletedInitialGeneration { get; private set; }
    public bool LastGenerationSucceeded { get; private set; }

    private void Start()
    {
        if (generateOnStart)
            Generate();
    }

    public bool Generate()
    {
        HasCompletedInitialGeneration = false;
        LastGenerationSucceeded = false;

        RoomThemeLibrarySO resolvedRoomLibrary = RoomLibrary;
        if (resolvedRoomLibrary == null)
        {
            Debug.LogError(
                "DungeonGenerator requires a RoomThemeLibrarySO, directly or through its generation profile.",
                this);
            return false;
        }

        if (roomBuilder == null)
        {
            Debug.LogError("DungeonGenerator requires a DungeonRoomBuilder.", this);
            return false;
        }

        string stateId = ResolveDungeonStateId();
        DungeonMapRuntimeController mapRuntime = ResolveDungeonMapRuntime();
        mapRuntime.ClearConfiguration();
        int resolvedSeed = RunSessionStore.ResolveDungeonSeed(stateId, reentryPolicy, Seed);
        LastGenerationSeed = resolvedSeed;
        layoutAssembler ??= new DungeonLayoutAssembler();
        DungeonLayoutPolicySO resolvedLayoutPolicy = LayoutPolicy;
        int resolvedRoomCount = RoomCount;
        bool resolvedIncludeBossRoom = IncludeBossRoom;
        int resolvedMaxPlacementAttempts = MaxPlacementAttemptsPerRoom;
        int resolvedMinimumCorridorLength = MinimumCorridorLength;
        float resolvedCorridorLengthPerRoomCell = CorridorLengthPerRoomCell;
        int resolvedCorridorLengthVariation = CorridorLengthVariation;
        bool useGraphFirstLayout = resolvedLayoutPolicy != null && resolvedIncludeBossRoom;
        RunMapEventGenerationPlan mapEventPlan = RunMapEventGenerationResolver.CreatePlan(
            useGraphFirstLayout && generationProfile != null
                ? generationProfile.RunMapEventProfile
                : null,
            generationProfile != null
                ? generationProfile.GuaranteedRoomTemplates
                : null,
            resolvedSeed);
        LastLayout = useGraphFirstLayout
            ? new DungeonGraphLayoutAssembler().Assemble(
                resolvedRoomLibrary,
                resolvedLayoutPolicy,
                resolvedSeed,
                resolvedRoomCount,
                resolvedMaxPlacementAttempts,
                resolvedMinimumCorridorLength,
                resolvedCorridorLengthPerRoomCell,
                resolvedCorridorLengthVariation,
                mapEventPlan.GuaranteedRoomTemplates)
            : layoutAssembler.Assemble(
                resolvedRoomLibrary,
                resolvedSeed,
                resolvedRoomCount,
                resolvedIncludeBossRoom,
                resolvedMaxPlacementAttempts,
                resolvedMinimumCorridorLength,
                resolvedCorridorLengthPerRoomCell,
                resolvedCorridorLengthVariation);

        if (LastLayout.Rooms.Count == 0)
        {
            Debug.LogError($"Dungeon layout generation failed: {LastLayout.FailureReason}", this);
            HasCompletedInitialGeneration = true;
            return false;
        }

        roomBuilder.ConfigureCorridorDecoration(
            generationProfile != null
                ? generationProfile.CorridorDecorationProfile
                : roomBuilder.CorridorDecorationProfile);
        if (!roomBuilder.TryBuild(LastLayout))
        {
            mapRuntime.ClearConfiguration();
            HasCompletedInitialGeneration = true;
            return false;
        }

        if (reentryPolicy == DungeonReentryPolicy.PreserveDuringRun)
        {
            var savedStates = new List<DungeonObjectRuntimeStateData>();
            if (RunSessionStore.TryGetDungeonObjectStates(stateId, savedStates))
                roomBuilder.RestoreGeneratedObjectStates(savedStates);
        }

        if (!LastLayout.IsComplete)
        {
            mapRuntime.ClearConfiguration();
            Debug.LogWarning(
                $"Dungeon layout built partially ({LastLayout.Rooms.Count}/{LastLayout.RequestedRoomCount} rooms): " +
                LastLayout.FailureReason,
                this);
            HasCompletedInitialGeneration = true;
            return false;
        }

        mapRuntime.Configure(LastLayout, stateId, reentryPolicy);
        mapEventPlan.Commit();

        ResolveCorridorLengthRange(
            LastLayout,
            out int shortestCorridorLength,
            out int longestCorridorLength);
        string longestCorridorDescription = ResolveLongestCorridorDescription(LastLayout);
        Debug.Log(
            $"Dungeon generated. Theme={resolvedRoomLibrary.ThemeId}, Seed={resolvedSeed}, " +
            $"Reentry={reentryPolicy}, Rooms={LastLayout.Rooms.Count}, " +
            $"GraphFirst={LastLayout.UsesGraphFirstLayout}, " +
            $"BossDistance={LastLayout.BossGraphDistance}, " +
            $"Branches={LastLayout.MeaningfulBranchCount}, " +
            $"Cycles={LastLayout.CycleConnectionCount}, " +
            $"Corridors={shortestCorridorLength}..{longestCorridorLength}, " +
            $"LongestCorridor={longestCorridorDescription}, " +
            $"CorridorRelaxed={LastLayout.UsedCorridorLengthRelaxation}",
            this);
        HasCompletedInitialGeneration = true;
        LastGenerationSucceeded = true;
        return true;
    }

    private static void ResolveCorridorLengthRange(
        DungeonLayoutResult layout,
        out int shortestLength,
        out int longestLength)
    {
        shortestLength = 0;
        longestLength = 0;
        if (layout == null || layout.Connections.Count == 0)
            return;

        shortestLength = int.MaxValue;
        for (int connectionIndex = 0;
             connectionIndex < layout.Connections.Count;
             connectionIndex++)
        {
            int length = layout.Connections[connectionIndex].CorridorLength;
            shortestLength = Mathf.Min(shortestLength, length);
            longestLength = Mathf.Max(longestLength, length);
        }
    }

    private static string ResolveLongestCorridorDescription(DungeonLayoutResult layout)
    {
        if (layout == null || layout.Connections.Count == 0)
            return "None";

        DungeonSocketConnection longest = layout.Connections[0];
        for (int connectionIndex = 1;
             connectionIndex < layout.Connections.Count;
             connectionIndex++)
        {
            DungeonSocketConnection candidate = layout.Connections[connectionIndex];
            if (candidate.CorridorLength > longest.CorridorLength)
                longest = candidate;
        }

        DungeonRoomPlacement first = layout.GetRoom(longest.FirstRoomPlacementId);
        DungeonRoomPlacement second = layout.GetRoom(longest.SecondRoomPlacementId);
        string firstRoomId = first?.Template != null
            ? first.Template.LayoutData.roomId
            : longest.FirstRoomPlacementId.ToString();
        string secondRoomId = second?.Template != null
            ? second.Template.LayoutData.roomId
            : longest.SecondRoomPlacementId.ToString();
        return $"{firstRoomId}->{secondRoomId}({longest.CorridorLength})";
    }

    /// <summary>
    /// 책임 : 씬을 떠나기 직전 PreserveDuringRun 정책의 생성 오브젝트 상태를 현재 런 저장소에 기록한다.
    /// </summary>
    public void CaptureStateBeforeSceneExit()
    {
        if (reentryPolicy != DungeonReentryPolicy.PreserveDuringRun ||
            !RunSessionStore.IsRunActive ||
            roomBuilder == null ||
            !HasCompletedInitialGeneration)
        {
            return;
        }

        RunSessionStore.SaveDungeonObjectStates(
            ResolveDungeonStateId(),
            roomBuilder.CaptureGeneratedObjectStates());
        DungeonMapRuntimeController mapRuntime =
            roomBuilder.GetComponent<DungeonMapRuntimeController>();
        mapRuntime?.CaptureDiscoveryState();
    }

    private DungeonMapRuntimeController ResolveDungeonMapRuntime()
    {
        DungeonMapRuntimeController runtime =
            roomBuilder.GetComponent<DungeonMapRuntimeController>();
        return runtime != null
            ? runtime
            : roomBuilder.gameObject.AddComponent<DungeonMapRuntimeController>();
    }

    private string ResolveDungeonStateId()
    {
        if (!string.IsNullOrWhiteSpace(dungeonStateId))
            return dungeonStateId;

        RoomThemeLibrarySO resolvedRoomLibrary = RoomLibrary;
        if (resolvedRoomLibrary != null && !string.IsNullOrWhiteSpace(resolvedRoomLibrary.ThemeId))
            return $"corridor:{resolvedRoomLibrary.ThemeId}";

        return $"scene:{gameObject.scene.name}";
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        RoomThemeLibrarySO library,
        DungeonRoomBuilder builder,
        int generationSeed,
        int targetRoomCount,
        bool shouldIncludeBossRoom,
        int placementAttemptsPerRoom,
        int connectionMinimumCorridorLength,
        float connectionCorridorLengthPerRoomCell,
        int connectionCorridorLengthVariation,
        bool shouldGenerateOnStart,
        DungeonLayoutPolicySO generationLayoutPolicy = null,
        DungeonGenerationProfileSO profile = null)
    {
        generationProfile = profile;
        roomLibrary = library;
        roomBuilder = builder;
        seed = generationSeed;
        roomCount = Mathf.Max(shouldIncludeBossRoom ? 2 : 1, targetRoomCount);
        includeBossRoom = shouldIncludeBossRoom;
        maxPlacementAttemptsPerRoom = Mathf.Max(1, placementAttemptsPerRoom);
        minimumCorridorLength = Mathf.Max(0, connectionMinimumCorridorLength);
        corridorLengthPerRoomCell = Mathf.Clamp(connectionCorridorLengthPerRoomCell, 0f, 1f);
        corridorLengthVariation = Mathf.Clamp(connectionCorridorLengthVariation, 0, 32);
        generateOnStart = shouldGenerateOnStart;
        layoutPolicy = generationLayoutPolicy;
    }

    /// <summary>
    /// 책임 : 제작/마이그레이션 툴이 기존 씬 수치를 손대지 않고 영속 생성 프로필 참조만 연결한다.
    /// </summary>
    public void EditorAssignGenerationProfile(DungeonGenerationProfileSO profile)
    {
        generationProfile = profile;
    }

    /// <summary>
    /// 책임 : 씬 설치/제작 툴이 기존 생성 설정 시그니처를 깨지 않고 던전 식별자와 재진입 정책만 지정하게 한다.
    /// </summary>
    public void EditorConfigureReentryPolicy(string stateId, DungeonReentryPolicy policy)
    {
        dungeonStateId = stateId ?? string.Empty;
        reentryPolicy = policy;
    }

    private void OnValidate()
    {
        roomCount = Mathf.Max(includeBossRoom ? 2 : 1, roomCount);
        maxPlacementAttemptsPerRoom = Mathf.Max(1, maxPlacementAttemptsPerRoom);
        minimumCorridorLength = Mathf.Max(0, minimumCorridorLength);
        corridorLengthPerRoomCell = float.IsFinite(corridorLengthPerRoomCell)
            ? Mathf.Clamp01(corridorLengthPerRoomCell)
            : 0f;
        corridorLengthVariation = Mathf.Clamp(corridorLengthVariation, 0, 32);
        dungeonStateId ??= string.Empty;
    }
#endif
}
