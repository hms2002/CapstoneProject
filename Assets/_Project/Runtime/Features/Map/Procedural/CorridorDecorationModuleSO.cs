using UnityEngine;

/// <summary>
/// 책임 : 복도 장식 조각이 시작·본문·랜드마크·필러·종료·짧은 복도 중 어떤 위치 역할을 맡는지 정의한다.
/// </summary>
public enum CorridorDecorationModuleRole
{
    Start = 0,
    Middle = 1,
    Landmark = 2,
    Filler = 3,
    End = 4,
    Short = 5
}

/// <summary>
/// 책임:
/// - +X 진행 방향과 2칸 폭을 기준으로 제작한 복도 조각의 길이, 역할, 8개 타일 레이어와 GroundProp 배치를 보관한다.
/// - 오브젝트의 localCell·localOffset을 조각 원점 기준 Pivot 데이터로 제공해 런타임 방향 변환 후 같은 위치에 재생성하게 한다.
/// </summary>
[CreateAssetMenu(
    fileName = "CorridorDecorationModule",
    menuName = "Gameplay/Dungeon/Corridor Decoration Module")]
public sealed class CorridorDecorationModuleSO : ScriptableObject
{
    [SerializeField] private string moduleId = "Corridor_Module";
    [SerializeField] private CorridorDecorationModuleRole role =
        CorridorDecorationModuleRole.Middle;
    [SerializeField, Min(1)] private int length = 2;
    [SerializeField] private RoomBuildData buildData;

    public string ModuleId => string.IsNullOrWhiteSpace(moduleId) ? name : moduleId;
    public CorridorDecorationModuleRole Role => role;
    public int Length => Mathf.Max(1, length);
    public RoomBuildData BuildData => buildData;

#if UNITY_EDITOR
    /// <summary>
    /// 책임 : 복도 제작 툴이 검증한 메타데이터와 레이어별 구현 데이터를 모듈 에셋에 저장한다.
    /// </summary>
    public void EditorSetData(
        string id,
        CorridorDecorationModuleRole moduleRole,
        int moduleLength,
        RoomBuildData moduleBuildData)
    {
        moduleId = id ?? string.Empty;
        role = moduleRole;
        length = Mathf.Max(1, moduleLength);
        buildData = moduleBuildData;
    }

    private void OnValidate()
    {
        moduleId ??= string.Empty;
        length = Mathf.Max(1, length);
    }
#endif
}
