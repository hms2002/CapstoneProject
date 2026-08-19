using UnityEngine;
using UnityEngine.Serialization;

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
    public int MinimumCorridorLength => minimumCorridorLength;
    public float CorridorLengthPerRoomCell => corridorLengthPerRoomCell;
    public int CorridorLengthVariation => corridorLengthVariation;
    public DungeonLayoutResult LastLayout { get; private set; }

    private void Start()
    {
        if (generateOnStart)
            Generate();
    }

    public bool Generate()
    {
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

        layoutAssembler ??= new DungeonLayoutAssembler();
        LastLayout = layoutAssembler.Assemble(
            roomLibrary,
            seed,
            roomCount,
            includeBossRoom,
            maxPlacementAttemptsPerRoom,
            minimumCorridorLength,
            corridorLengthPerRoomCell,
            corridorLengthVariation);

        if (LastLayout.Rooms.Count == 0)
        {
            Debug.LogError($"Dungeon layout generation failed: {LastLayout.FailureReason}", this);
            return false;
        }

        if (!roomBuilder.TryBuild(LastLayout))
            return false;

        if (!LastLayout.IsComplete)
        {
            Debug.LogWarning(
                $"Dungeon layout built partially ({LastLayout.Rooms.Count}/{LastLayout.RequestedRoomCount} rooms): " +
                LastLayout.FailureReason,
                this);
            return false;
        }

        Debug.Log(
            $"Dungeon generated. Theme={roomLibrary.ThemeId}, Seed={seed}, Rooms={LastLayout.Rooms.Count}",
            this);
        return true;
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
        bool shouldGenerateOnStart)
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
    }
#endif
}
