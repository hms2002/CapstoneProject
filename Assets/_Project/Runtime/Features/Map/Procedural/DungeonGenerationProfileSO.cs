using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 한 테마의 룸 라이브러리, 레이아웃 정책과 반복 조정 가능한 맵 생성 수치를 하나의 영속 에셋으로 보관한다.
/// - 편집기 미리보기와 런타임 DungeonGenerator가 같은 생성 계약을 읽게 해 씬별 수치 복제와 설치기 하드코딩을 방지한다.
/// </summary>
[CreateAssetMenu(fileName = "DungeonGenerationProfile", menuName = "Gameplay/Dungeon/Generation Profile")]
public sealed class DungeonGenerationProfileSO : ScriptableObject
{
    [Header("Dependencies")]
    [SerializeField] private RoomThemeLibrarySO roomLibrary;
    [SerializeField] private DungeonLayoutPolicySO layoutPolicy;
    [SerializeField] private CorridorDecorationProfileSO corridorDecorationProfile;

    [Header("Generation")]
    [SerializeField] private int seed = 12345;
    [SerializeField, Min(1)] private int roomCount = 8;
    [SerializeField] private bool includeBossRoom = true;
    [SerializeField, Min(1)] private int maxPlacementAttemptsPerRoom = 128;
    [Tooltip("충돌 회피 중에도 줄이지 않는 복도의 절대 최소 셀 길이입니다.")]
    [SerializeField, Min(0)] private int minimumCorridorLength = 2;
    [Tooltip("두 방 크기에 비례해 우선 시도할 추가 복도 길이입니다. 배치 충돌 시 자동으로 줄어듭니다.")]
    [SerializeField, Range(0f, 1f)] private float corridorLengthPerRoomCell = 0.05f;
    [Tooltip("우선 시도 길이에 더하는 난수 폭입니다. 배치 충돌 시 자동으로 줄어듭니다.")]
    [SerializeField, Range(0, 32)] private int corridorLengthVariation = 2;

    [Header("Guaranteed Content")]
    [Tooltip("Specific expansion rooms that graph-first generation must place exactly once.")]
    [SerializeField] private List<RoomTemplateSO> guaranteedRoomTemplates = new();

    [Header("Run Map Events")]
    [SerializeField] private RunMapEventGenerationProfileSO runMapEventProfile;

    public RoomThemeLibrarySO RoomLibrary => roomLibrary;
    public DungeonLayoutPolicySO LayoutPolicy => layoutPolicy;
    public CorridorDecorationProfileSO CorridorDecorationProfile => corridorDecorationProfile;
    public int Seed => seed;
    public int RoomCount => Mathf.Max(includeBossRoom ? 2 : 1, roomCount);
    public bool IncludeBossRoom => includeBossRoom;
    public int MaxPlacementAttemptsPerRoom => Mathf.Max(1, maxPlacementAttemptsPerRoom);
    public int MinimumCorridorLength => Mathf.Max(0, minimumCorridorLength);
    public float CorridorLengthPerRoomCell => float.IsFinite(corridorLengthPerRoomCell)
        ? Mathf.Clamp01(corridorLengthPerRoomCell)
        : 0f;
    public int CorridorLengthVariation => Mathf.Clamp(corridorLengthVariation, 0, 32);
    public IReadOnlyList<RoomTemplateSO> GuaranteedRoomTemplates =>
        guaranteedRoomTemplates ?? (IReadOnlyList<RoomTemplateSO>)System.Array.Empty<RoomTemplateSO>();
    public RunMapEventGenerationProfileSO RunMapEventProfile => runMapEventProfile;

#if UNITY_EDITOR
    /// <summary>
    /// 책임 : 제작 툴이 검증한 미리보기 설정을 이 프로필의 단일 영속 생성 계약으로 저장한다.
    /// </summary>
    public void EditorConfigure(
        RoomThemeLibrarySO generationRoomLibrary,
        DungeonLayoutPolicySO generationLayoutPolicy,
        int generationSeed,
        int targetRoomCount,
        bool shouldIncludeBossRoom,
        int placementAttemptsPerRoom,
        int connectionMinimumCorridorLength,
        float connectionCorridorLengthPerRoomCell,
        int connectionCorridorLengthVariation)
    {
        roomLibrary = generationRoomLibrary;
        layoutPolicy = generationLayoutPolicy;
        seed = generationSeed;
        includeBossRoom = shouldIncludeBossRoom;
        roomCount = Mathf.Max(includeBossRoom ? 2 : 1, targetRoomCount);
        maxPlacementAttemptsPerRoom = Mathf.Max(1, placementAttemptsPerRoom);
        minimumCorridorLength = Mathf.Max(0, connectionMinimumCorridorLength);
        corridorLengthPerRoomCell = float.IsFinite(connectionCorridorLengthPerRoomCell)
            ? Mathf.Clamp01(connectionCorridorLengthPerRoomCell)
            : 0f;
        corridorLengthVariation = Mathf.Clamp(connectionCorridorLengthVariation, 0, 32);
    }

    /// <summary>
    /// 책임 : 콘텐츠 설치기와 제작 툴이 특정 테마에서 정확히 한 번 포함할 방 템플릿 목록을 중복 없이 저장한다.
    /// </summary>
    public void EditorSetGuaranteedRooms(IReadOnlyList<RoomTemplateSO> rooms)
    {
        guaranteedRoomTemplates ??= new List<RoomTemplateSO>();
        guaranteedRoomTemplates.Clear();
        if (rooms == null)
            return;

        for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
        {
            RoomTemplateSO room = rooms[roomIndex];
            if (room != null && !guaranteedRoomTemplates.Contains(room))
                guaranteedRoomTemplates.Add(room);
        }
    }

    /// <summary>
    /// 책임 : 복도 장식 제작 툴이 현재 테마 생성 프로필에 장식 프로필 참조를 연결한다.
    /// </summary>
    public void EditorSetCorridorDecorationProfile(CorridorDecorationProfileSO profile)
    {
        corridorDecorationProfile = profile;
    }

    /// <summary>
    /// 책임 : 제작 툴이 복도 생성 프로필에 런 이벤트 선택 프로필을 연결한다.
    /// </summary>
    public void EditorSetRunMapEventProfile(RunMapEventGenerationProfileSO profile)
    {
        runMapEventProfile = profile;
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
        guaranteedRoomTemplates ??= new List<RoomTemplateSO>();
        for (int roomIndex = guaranteedRoomTemplates.Count - 1; roomIndex >= 0; roomIndex--)
        {
            RoomTemplateSO room = guaranteedRoomTemplates[roomIndex];
            if (room == null || guaranteedRoomTemplates.IndexOf(room) != roomIndex)
                guaranteedRoomTemplates.RemoveAt(roomIndex);
        }
    }
#endif
}
