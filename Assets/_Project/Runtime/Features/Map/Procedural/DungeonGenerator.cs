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
    [SerializeField, Min(0)] private int minimumCorridorLength = 4;
    [SerializeField, Range(0f, 1f)] private float corridorLengthPerRoomCell = 0.35f;
    [SerializeField, Range(0, 32)] private int corridorLengthVariation = 8;
    [SerializeField] private bool generateOnStart = true;

    private DungeonLayoutAssembler layoutAssembler;

    public RoomThemeLibrarySO RoomLibrary => roomLibrary;
    public DungeonRoomBuilder RoomBuilder => roomBuilder;
    public DungeonLayoutPolicySO LayoutPolicy => layoutPolicy;
    public int RoomCount => roomCount;
    public bool IncludeBossRoom => includeBossRoom;
    public int MaxPlacementAttemptsPerRoom => maxPlacementAttemptsPerRoom;
    public int MinimumCorridorLength => minimumCorridorLength;
    public float CorridorLengthPerRoomCell => corridorLengthPerRoomCell;
    public int CorridorLengthVariation => corridorLengthVariation;
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

        if (roomLibrary == null)
        {
            Debug.LogError("DungeonGenerator requires a RoomThemeLibrarySO.", this);
            return false;
        }

        if (roomBuilder == null)
        {
            Debug.LogError("DungeonGenerator requires a DungeonRoomBuilder.", this);
            return false;
        }

        string stateId = ResolveDungeonStateId();
        int resolvedSeed = RunSessionStore.ResolveDungeonSeed(stateId, reentryPolicy, seed);
        LastGenerationSeed = resolvedSeed;
        layoutAssembler ??= new DungeonLayoutAssembler();
        LastLayout = layoutPolicy != null && includeBossRoom
            ? new DungeonGraphLayoutAssembler().Assemble(
                roomLibrary,
                layoutPolicy,
                resolvedSeed,
                roomCount,
                maxPlacementAttemptsPerRoom,
                minimumCorridorLength,
                corridorLengthPerRoomCell,
                corridorLengthVariation)
            : layoutAssembler.Assemble(
                roomLibrary,
                resolvedSeed,
                roomCount,
                includeBossRoom,
                maxPlacementAttemptsPerRoom,
                minimumCorridorLength,
                corridorLengthPerRoomCell,
                corridorLengthVariation);

        if (LastLayout.Rooms.Count == 0)
        {
            Debug.LogError($"Dungeon layout generation failed: {LastLayout.FailureReason}", this);
            HasCompletedInitialGeneration = true;
            return false;
        }

        if (!roomBuilder.TryBuild(LastLayout))
        {
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
            Debug.LogWarning(
                $"Dungeon layout built partially ({LastLayout.Rooms.Count}/{LastLayout.RequestedRoomCount} rooms): " +
                LastLayout.FailureReason,
                this);
            HasCompletedInitialGeneration = true;
            return false;
        }

        Debug.Log(
            $"Dungeon generated. Theme={roomLibrary.ThemeId}, Seed={resolvedSeed}, " +
            $"Reentry={reentryPolicy}, Rooms={LastLayout.Rooms.Count}, " +
            $"GraphFirst={LastLayout.UsesGraphFirstLayout}, " +
            $"BossDistance={LastLayout.BossGraphDistance}, " +
            $"Branches={LastLayout.MeaningfulBranchCount}, " +
            $"Cycles={LastLayout.CycleConnectionCount}",
            this);
        HasCompletedInitialGeneration = true;
        LastGenerationSucceeded = true;
        return true;
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
    }

    private string ResolveDungeonStateId()
    {
        if (!string.IsNullOrWhiteSpace(dungeonStateId))
            return dungeonStateId;

        if (roomLibrary != null && !string.IsNullOrWhiteSpace(roomLibrary.ThemeId))
            return $"corridor:{roomLibrary.ThemeId}";

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
        DungeonLayoutPolicySO generationLayoutPolicy = null)
    {
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
