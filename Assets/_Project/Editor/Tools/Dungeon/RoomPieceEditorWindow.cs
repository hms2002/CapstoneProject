using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임:
/// - 절차 생성용 방 조각 authoring 루트, 2칸 연결 소켓, 런타임 오브젝트 배치를 빠르게 만든다.
/// - 고정 시각 Tilemap 슬롯과 몬스터·상자·포털·프롭 배치를 RoomTemplateSO로 bake하고 다시 편집 상태로 복원한다.
/// - 타일 페인팅은 Unity 기본 Tile Palette에 맡기고, 이 창은 생성/검증/데이터화 흐름만 담당한다.
/// - 기획자가 테마 라이브러리에서 방을 탐색하고 임시 작업 공간에서 안전하게 편집한 뒤 검증·등록하게 한다.
/// - 실제 DungeonLayoutAssembler와 시각 전용 DungeonRoomBuilder를 사용해 저장 전 방을 포함한 동적 맵을 미리 보여준다.
/// - 미리보기에서 검증한 생성 수치를 테마별 DungeonGenerationProfileSO에 저장해 실제 복도 생성과 공유한다.
/// </summary>
public sealed class RoomPieceEditorWindow : EditorWindow
{
    private const string MonsterSpawnSetFolder =
        "Assets/_Project/Data/Monsters/SpawnSets";

    /// <summary>
    /// 책임:
    /// - 새 Monster 지점을 진행도형 공통 역할과 고정 스테이지 몬스터 중 어떤 소스로 제작할지 구분한다.
    /// </summary>
    private enum MonsterPlacementSourceMode
    {
        CommonRole,
        StageFixed
    }

    private static readonly string[] MonsterPlacementSourceLabels =
    {
        "공통 역할 (진행도에 따라 변경)",
        "스테이지 몬스터 (고정 프리팹)"
    };

    private enum AuthoringStep
    {
        Basic,
        Tiles,
        Sockets,
        Objects,
        Publish,
        Preview
    }

    private string newRoomId = "Room_New";
    private Vector2Int newRoomSize = new(12, 8);
    private RoomType newRoomType = RoomType.Combat;
    private int newDifficultyTier;
    private float newSelectionWeight = 1f;
    [SerializeField] private RoomTemplateSO templateToLoad;
    [SerializeField] private RoomThemeLibrarySO selectedLibrary;
    [SerializeField] private RoomObjectKind objectKindToPlace = RoomObjectKind.Prop;
    [SerializeField] private MonsterPlacementSourceMode monsterSourceModeToPlace =
        MonsterPlacementSourceMode.CommonRole;
    [SerializeField] private RoomMonsterSpawnRole monsterRoleToPlace =
        RoomMonsterSpawnRole.Warrior;
    [SerializeField] private GameObject objectPrefabToPlace;
    [SerializeField] private RoomTravelEndpointKind travelEndpointKindToPlace = RoomTravelEndpointKind.Interaction;
    [SerializeField] private GameObject travelMediumPrefabToPlace;
    [SerializeField] private Vector2 travelTriggerSizeToPlace = Vector2.one;
    [SerializeField] private RoomPieceAuthoring selectedAuthoring;
    [SerializeField] private AuthoringStep currentStep;
    [SerializeField] private bool registerWithSelectedLibrary = true;
    [SerializeField] private bool filterLibraryByRoomType;
    [SerializeField] private RoomType libraryRoomTypeFilter = RoomType.Combat;
    [SerializeField] private string librarySearch = string.Empty;
    [SerializeField] private bool socketPlacementMode;
    [SerializeField] private bool previewIncludeCurrentRoom = true;
    [SerializeField] private DungeonGenerationProfileSO previewGenerationProfile;
    [SerializeField] private DungeonLayoutPolicySO previewLayoutPolicy;
    [SerializeField] private int previewSeed = 12345;
    [SerializeField] private int previewRoomCount = 8;
    [SerializeField] private bool previewIncludeBossRoom = true;
    [SerializeField] private int previewMaxPlacementAttemptsPerRoom = 128;
    [SerializeField] private int previewMinimumCorridorLength = 2;
    [SerializeField] private float previewCorridorLengthPerRoomCell = 0.05f;
    [SerializeField] private int previewCorridorLengthVariation = 2;
    [SerializeField] private TileBase previewCorridorFloorTile;
    [SerializeField] private TileBase previewCorridorWallTile;
    [SerializeField] private bool previewAdvancedSettings;
    private Vector2 scroll;
    private double nextAutomaticValidationTime;
    private readonly List<string> validationMessages = new();
    private string previewStatusMessage = string.Empty;
    private MessageType previewStatusType = MessageType.None;
    private DungeonGenerationProfileSO cachedSceneReferenceProfile;
    private int cachedSceneReferenceCount;
    private bool hasCachedSceneReferenceCount;

    [MenuItem("Tools/Dungeon/Room Piece Editor")]
    public static void Open()
    {
        GetWindow<RoomPieceEditorWindow>("Room Piece");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += DrawSocketSceneHandles;
        selectedAuthoring = RoomAuthoringWorkspace.FindAuthoring();
        InvalidateSceneReferenceCount();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawSocketSceneHandles;
    }

    private void OnInspectorUpdate()
    {
        if (currentStep == AuthoringStep.Preview)
            return;

        if (EditorApplication.timeSinceStartup < nextAutomaticValidationTime)
            return;

        nextAutomaticValidationTime = EditorApplication.timeSinceStartup + 0.5d;
        selectedAuthoring = ResolveSelectedAuthoring();
        if (selectedAuthoring != null)
            ValidateSelectedRoomPiece(showDialog: false);
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawWorkspaceSection();
        EditorGUILayout.Space(8f);
        DrawLibrarySection();
        EditorGUILayout.Space(8f);

        selectedAuthoring = ResolveSelectedAuthoring();
        if (selectedAuthoring == null)
        {
            DrawCreateSection();
            EditorGUILayout.Space(10f);
            DrawLoadSection();
            EditorGUILayout.Space(10f);
            DrawLibraryBrowser();
        }
        else
        {
            DrawAuthoringHeader();
            DrawStepToolbar();
            EditorGUILayout.Space(8f);
            switch (currentStep)
            {
                case AuthoringStep.Basic:
                    DrawSelectionSection();
                    EditorGUILayout.Space(8f);
                    DrawLibraryBrowser();
                    break;
                case AuthoringStep.Tiles:
                    DrawTileSection();
                    break;
                case AuthoringStep.Sockets:
                    DrawSocketSection();
                    break;
                case AuthoringStep.Objects:
                    DrawObjectSection();
                    break;
                case AuthoringStep.Publish:
                    DrawBakeSection();
                    break;
                case AuthoringStep.Preview:
                    DrawDungeonPreviewSection();
                    break;
            }

            DrawValidationSummary();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawWorkspaceSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("안전 작업 공간", EditorStyles.boldLabel);
            if (RoomAuthoringWorkspace.IsOpen)
            {
                EditorGUILayout.HelpBox(
                    "방 제작 오브젝트는 저장되지 않는 전용 additive 씬에 격리되어 있습니다. 현재 게임 씬은 변경하지 않습니다.",
                    MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("작업 공간 활성화"))
                        RoomAuthoringWorkspace.Open();

                    if (GUILayout.Button("작업 공간 닫기"))
                    {
                        if (RoomAuthoringWorkspace.Close(confirmDiscard: true))
                        {
                            RoomAuthoringDungeonPreview.Clear();
                            selectedAuthoring = null;
                            socketPlacementMode = false;
                            validationMessages.Clear();
                        }
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "새 방 생성이나 기존 방 편집을 시작하면 전용 임시 작업 공간이 자동으로 열립니다.",
                    MessageType.None);
                if (GUILayout.Button("빈 작업 공간 열기"))
                    RoomAuthoringWorkspace.Open();
            }
        }
    }

    private void DrawLibrarySection()
    {
        RoomThemeLibrarySO previousLibrary = selectedLibrary;
        if (Selection.activeObject is RoomThemeLibrarySO selectedThemeLibrary)
            selectedLibrary = selectedThemeLibrary;

        selectedLibrary = EditorGUILayout.ObjectField(
            "테마 룸 라이브러리",
            selectedLibrary,
            typeof(RoomThemeLibrarySO),
            false) as RoomThemeLibrarySO;

        if (selectedLibrary != previousLibrary)
            SelectPreviewGenerationProfileForLibrary();

        if (selectedLibrary == null)
        {
            EditorGUILayout.HelpBox(
                "라이브러리를 선택하면 방 검색, 프리팹 추천과 저장 후 자동 등록을 사용할 수 있습니다.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField(
            "현재 테마",
            $"{selectedLibrary.ThemeId} · 방 {selectedLibrary.Rooms.Count}개");
    }

    private void DrawAuthoringHeader()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                $"편집 중: {selectedAuthoring.RoomId}",
                EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                $"{selectedAuthoring.RoomType} · {selectedAuthoring.Size.x}×{selectedAuthoring.Size.y}",
                GUILayout.MaxWidth(180f));
        }
    }

    private void DrawStepToolbar()
    {
        string[] stepLabels =
        {
            "1. 기본",
            "2. 타일",
            "3. 출입구",
            "4. 오브젝트",
            "5. 검증·저장",
            "6. 맵 미리보기"
        };
        AuthoringStep requestedStep =
            (AuthoringStep)GUILayout.Toolbar((int)currentStep, stepLabels);
        if (requestedStep != currentStep)
        {
            currentStep = requestedStep;
            if (currentStep != AuthoringStep.Sockets)
            {
                socketPlacementMode = false;
                SceneView.RepaintAll();
            }
        }
    }

    private void DrawLibraryBrowser()
    {
        EditorGUILayout.LabelField("라이브러리 방 목록", EditorStyles.boldLabel);
        if (selectedLibrary == null)
        {
            EditorGUILayout.HelpBox("테마 룸 라이브러리를 먼저 선택하세요.", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            librarySearch = EditorGUILayout.TextField("검색", librarySearch ?? string.Empty);
            filterLibraryByRoomType = EditorGUILayout.ToggleLeft(
                "역할 필터",
                filterLibraryByRoomType,
                GUILayout.Width(80f));
            using (new EditorGUI.DisabledScope(!filterLibraryByRoomType))
            {
                libraryRoomTypeFilter = (RoomType)EditorGUILayout.EnumPopup(
                    libraryRoomTypeFilter,
                    GUILayout.Width(100f));
            }
        }

        RoomTemplateSO requestedTemplate = null;
        bool duplicateRequested = false;
        int visibleRoomCount = 0;
        IReadOnlyList<RoomTemplateSO> rooms = selectedLibrary.Rooms;
        for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
        {
            RoomTemplateSO room = rooms[roomIndex];
            if (room == null || !MatchesLibraryFilter(room))
                continue;

            visibleRoomCount++;
            RoomLayoutData layout = room.LayoutData;
            int socketCount = layout.sockets != null ? layout.sockets.Count : 0;
            int monsterCount = CountRoomObjects(room, RoomObjectKind.Monster);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    $"{layout.roomId}  ·  {layout.roomType}  ·  " +
                    $"{layout.size.x}×{layout.size.y}  ·  소켓 {socketCount}  ·  몬스터 {monsterCount}");
                if (GUILayout.Button("편집", GUILayout.Width(48f)))
                {
                    requestedTemplate = room;
                    duplicateRequested = false;
                }

                if (GUILayout.Button("복제", GUILayout.Width(48f)))
                {
                    requestedTemplate = room;
                    duplicateRequested = true;
                }
            }
        }

        if (visibleRoomCount == 0)
            EditorGUILayout.HelpBox("현재 검색 조건에 맞는 방이 없습니다.", MessageType.None);

        if (requestedTemplate != null)
        {
            templateToLoad = requestedTemplate;
            LoadTemplateForEditing(duplicateRequested);
            GUIUtility.ExitGUI();
        }
    }

    private bool MatchesLibraryFilter(RoomTemplateSO room)
    {
        if (filterLibraryByRoomType && room.LayoutData.roomType != libraryRoomTypeFilter)
            return false;

        if (string.IsNullOrWhiteSpace(librarySearch))
            return true;

        string roomId = room.LayoutData.roomId ?? string.Empty;
        return roomId.IndexOf(librarySearch, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               room.name.IndexOf(librarySearch, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int CountRoomObjects(RoomTemplateSO room, RoomObjectKind kind)
    {
        List<RoomObjectPlacementData> placements = room != null
            ? room.BuildData.objectPlacements
            : null;
        if (placements == null)
            return 0;

        int count = 0;
        for (int placementIndex = 0; placementIndex < placements.Count; placementIndex++)
        {
            if (placements[placementIndex].kind == kind)
                count++;
        }

        return count;
    }

    private void DrawValidationSummary()
    {
        EditorGUILayout.Space(10f);
        if (validationMessages.Count == 0)
        {
            EditorGUILayout.HelpBox("자동 검증 통과 · 저장할 수 있습니다.", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox(
            $"확인할 항목 {validationMessages.Count}개 · '검증·저장' 단계에서 자세히 볼 수 있습니다.",
            MessageType.Warning);
    }

    private void DrawCreateSection()
    {
        EditorGUILayout.LabelField("새 방 만들기", EditorStyles.boldLabel);
        newRoomId = EditorGUILayout.TextField("방 ID", newRoomId);
        newRoomType = (RoomType)EditorGUILayout.EnumPopup("방 역할", newRoomType);
        newRoomSize = EditorGUILayout.Vector2IntField("예약 크기", newRoomSize);
        newDifficultyTier = EditorGUILayout.IntField("난이도 단계", Mathf.Max(0, newDifficultyTier));
        newSelectionWeight = EditorGUILayout.FloatField("등장 가중치", Mathf.Max(0f, newSelectionWeight));

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newRoomId)))
        {
            if (GUILayout.Button("전용 작업 공간에서 새 방 만들기"))
                CreateAuthoringRoomPiece();
        }

        EditorGUILayout.HelpBox(
            "타일 그리기는 이 창이 직접 구현하지 않습니다. 생성된 Floor/Wall Tilemap을 선택한 뒤 Unity Tile Palette/Tilemap Brush로 그리면 됩니다.",
            MessageType.Info);
    }

    private void DrawLoadSection()
    {
        EditorGUILayout.LabelField("기존 방 직접 선택", EditorStyles.boldLabel);

        if (Selection.activeObject is RoomTemplateSO selectedTemplate)
            templateToLoad = selectedTemplate;

        templateToLoad = EditorGUILayout.ObjectField(
            "방 템플릿",
            templateToLoad,
            typeof(RoomTemplateSO),
            false) as RoomTemplateSO;

        using (new EditorGUI.DisabledScope(templateToLoad == null))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("원본 편집"))
                    LoadTemplateForEditing(asDuplicate: false);

                if (GUILayout.Button("복제해서 새 방 만들기"))
                    LoadTemplateForEditing(asDuplicate: true);
            }
        }

        EditorGUILayout.HelpBox(
            "선택한 에셋의 메타데이터, Floor/Wall 타일, 연결 소켓, 오브젝트 배치를 씬의 authoring 복사본으로 복원합니다. 원본 에셋은 Apply 전까지 변경되지 않습니다.",
            MessageType.Info);
    }

    private void DrawSelectionSection()
    {
        EditorGUILayout.LabelField("방 기본 정보", EditorStyles.boldLabel);
        selectedAuthoring = EditorGUILayout.ObjectField(
            "편집 대상",
            ResolveSelectedAuthoring(),
            typeof(RoomPieceAuthoring),
            true) as RoomPieceAuthoring;

        if (selectedAuthoring == null)
        {
            EditorGUILayout.HelpBox("RoomPieceAuthoring 컴포넌트가 있는 방 조각 루트를 선택하세요.", MessageType.Warning);
            return;
        }

        SerializedObject serializedAuthoring = new(selectedAuthoring);
        serializedAuthoring.Update();
        EditorGUILayout.PropertyField(serializedAuthoring.FindProperty("roomId"), new GUIContent("방 ID"));
        EditorGUILayout.PropertyField(serializedAuthoring.FindProperty("roomType"), new GUIContent("방 역할"));
        EditorGUILayout.PropertyField(serializedAuthoring.FindProperty("size"), new GUIContent("예약 크기"));
        EditorGUILayout.PropertyField(
            serializedAuthoring.FindProperty("difficultyTier"),
            new GUIContent("난이도 단계"));
        EditorGUILayout.PropertyField(
            serializedAuthoring.FindProperty("selectionWeight"),
            new GUIContent("등장 가중치"));
        EditorGUILayout.PropertyField(
            serializedAuthoring.FindProperty("topologyPlacement"),
            new GUIContent("필수 방 배치 규칙"),
            includeChildren: true);
        serializedAuthoring.ApplyModifiedProperties();

        EditorGUILayout.ObjectField(
            "원본 템플릿",
            selectedAuthoring.SourceTemplate,
            typeof(RoomTemplateSO),
            false);
        EditorGUILayout.HelpBox(
            "예약 크기는 레이아웃 배치 충돌을 검사하는 직사각형 영역입니다. 실제 방 모양은 바닥 타일로 결정됩니다.",
            MessageType.Info);
        EditorGUILayout.HelpBox(
            "필수 방 배치 규칙은 생성 프로필의 '필수 포함 방'으로 등록된 템플릿에 적용됩니다. " +
            "순환 지름길은 메인 경로를 유지하는 우회로에, 최원거리는 시작 방에서 가장 먼 호환 후보에 배치합니다.",
            MessageType.None);
    }

    private void DrawTileSection()
    {
        EditorGUILayout.LabelField("방 타일 레이어 제작", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Unity Tile Palette를 그대로 사용합니다. 아래 버튼으로 그릴 레이어를 선택한 뒤 Scene View에서 페인팅하세요.",
            MessageType.Info);

        int totalOutsideCount = 0;
        for (int index = 0; index < RoomTileLayerContract.OrderedLayers.Count; index += 2)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawTileLayerSelection(RoomTileLayerContract.OrderedLayers[index], ref totalOutsideCount);
                if (index + 1 < RoomTileLayerContract.OrderedLayers.Count)
                {
                    DrawTileLayerSelection(
                        RoomTileLayerContract.OrderedLayers[index + 1],
                        ref totalOutsideCount);
                }
            }
        }

        if (totalOutsideCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"예약 영역 밖 타일이 {totalOutsideCount}개 있습니다. 저장 데이터에서는 제외됩니다.",
                MessageType.Warning);
        }

        EditorGUILayout.HelpBox(
            "소켓 위치도 샘플 데이터에서는 Floor와 Wall을 모두 칠해 막아 두세요. 실제 연결 시 DungeonRoomBuilder가 해당 벽만 엽니다.",
            MessageType.None);
        EditorGUILayout.HelpBox(
            "GroundProp은 타일이 아니라 '오브젝트' 단계에서 Prop으로 배치합니다. 충돌 여부와 물리 레이어는 배치한 프리팹이 소유합니다.",
            MessageType.None);
    }

    private void DrawTileLayerSelection(RoomTileLayerKind layer, ref int totalOutsideCount)
    {
        Tilemap tilemap = selectedAuthoring.GetTilemap(layer);
        int tileCount = CountTiles(tilemap, selectedAuthoring.Size, out int outsideCount);
        totalOutsideCount += outsideCount;

        using (new EditorGUI.DisabledScope(tilemap == null))
        {
            string label =
                $"{RoomTileLayerContract.GetLayerName(layer)} · " +
                $"{RoomTileLayerContract.GetDisplayName(layer)} ({tileCount})";
            if (GUILayout.Button(new GUIContent(label, GetTileLayerTooltip(layer))))
                SelectAndFrame(tilemap);
        }
    }

    private static string GetTileLayerTooltip(RoomTileLayerKind layer)
    {
        return layer switch
        {
            RoomTileLayerKind.UnderFloor => "통과 가능한 바닥 아래 배경. 물리 Ground 판정에 참여하지 않습니다.",
            RoomTileLayerKind.Floor => "기본 이동 바닥이며 물리 Ground 판정의 기준입니다.",
            RoomTileLayerKind.FloorDetail => "Floor 위에 그리는 통과 가능한 평면 장식입니다.",
            RoomTileLayerKind.GroundDecoration => "Floor 위에 그리는 통과 가능한 장식입니다.",
            RoomTileLayerKind.Wall => "통과할 수 없는 기본 벽이며 Tilemap Collider를 가집니다.",
            RoomTileLayerKind.WallDetail => "Wall 위에 그리며 아래 Wall의 충돌을 따르는 장식입니다.",
            RoomTileLayerKind.Foreground => "캐릭터 앞 ForeGround 정렬 레이어에 표시되는 장식입니다.",
            RoomTileLayerKind.OverlayFX => "안개·빛·어둠 같은 ForeGround 오버레이 효과입니다.",
            _ => RoomTileLayerContract.GetDisplayName(layer)
        };
    }

    private static void SelectAndFrame(Object target)
    {
        if (target == null)
            return;

        Selection.activeObject = target;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.FrameSelected();
    }

    private void DrawSocketSection()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("출입구 소켓", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(selectedAuthoring.Grid == null))
        {
            bool requestedPlacementMode = GUILayout.Toggle(
                socketPlacementMode,
                socketPlacementMode
                    ? "Scene View 클릭 배치 중 · Esc로 종료"
                    : "Scene View에서 경계를 클릭해 소켓 배치",
                "Button");
            if (requestedPlacementMode != socketPlacementMode)
            {
                socketPlacementMode = requestedPlacementMode;
                SceneView.RepaintAll();
            }

            EditorGUILayout.LabelField("빠른 중앙 배치", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("위"))
                    AddConnectionSocket(RoomSocketDirection.Up);
                if (GUILayout.Button("오른쪽"))
                    AddConnectionSocket(RoomSocketDirection.Right);
                if (GUILayout.Button("아래"))
                    AddConnectionSocket(RoomSocketDirection.Down);
                if (GUILayout.Button("왼쪽"))
                    AddConnectionSocket(RoomSocketDirection.Left);
            }
        }

        RoomSocketAuthoring[] sockets = GetSockets(selectedAuthoring);
        if (sockets.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "소켓이 없습니다. 소켓은 방 bounds의 경계 셀에 두고, Direction은 방 바깥쪽을 향하게 설정하세요.",
                MessageType.Warning);
            return;
        }

        for (int i = 0; i < sockets.Length; i++)
        {
            RoomSocketAuthoring socket = sockets[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedObject serializedSocket = new(socket);
                serializedSocket.Update();
                EditorGUILayout.PropertyField(serializedSocket.FindProperty("socketId"), new GUIContent("Socket Id"));
                EditorGUILayout.PropertyField(serializedSocket.FindProperty("direction"), new GUIContent("Direction"));
                EditorGUILayout.LabelField("Width", $"{socket.Width} cells");
                serializedSocket.ApplyModifiedProperties();

                if (socket.TryGetLocalCell(out Vector2Int currentCell))
                {
                    EditorGUI.BeginChangeCheck();
                    Vector2Int desiredCell = EditorGUILayout.Vector2IntField("Local Cell", currentCell);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(socket.transform, "Move Room Socket");
                        socket.EditorSetLocalCell(desiredCell);
                        EditorUtility.SetDirty(socket.transform);
                        SceneView.RepaintAll();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("부모 RoomPieceAuthoring의 Grid를 찾을 수 없습니다.", MessageType.Error);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Scene View에서 선택"))
                        Selection.activeObject = socket.gameObject;

                    if (GUILayout.Button("삭제"))
                    {
                        Undo.DestroyObjectImmediate(socket.gameObject);
                        SceneView.RepaintAll();
                        Repaint();
                        return;
                    }
                }
            }
        }

        EditorGUILayout.HelpBox(
            "소켓은 경계의 2칸을 차지하며 방향은 방 바깥쪽을 향합니다. 소켓을 선택하면 Scene View에 이동 핸들이 나타납니다.",
            MessageType.Info);
    }

    private void DrawObjectSection()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("방 오브젝트", EditorStyles.boldLabel);
        objectKindToPlace = (RoomObjectKind)EditorGUILayout.EnumPopup(
            "종류",
            objectKindToPlace);
        bool placingMonster = objectKindToPlace == RoomObjectKind.Monster;
        bool placingCommonRoleMonster = false;
        StageMonsterSetSO selectedRoleSet = null;
        if (placingMonster)
        {
            monsterSourceModeToPlace = (MonsterPlacementSourceMode)EditorGUILayout.Popup(
                "몬스터 배치 방식",
                (int)monsterSourceModeToPlace,
                MonsterPlacementSourceLabels);
            placingCommonRoleMonster =
                monsterSourceModeToPlace == MonsterPlacementSourceMode.CommonRole;
            if (placingCommonRoleMonster)
            {
                monsterRoleToPlace = (RoomMonsterSpawnRole)EditorGUILayout.EnumPopup(
                    "몬스터 역할",
                    monsterRoleToPlace);
                selectedRoleSet = LoadRoleStageMonsterSet(monsterRoleToPlace);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(
                        "진행도 몬스터 세트",
                        selectedRoleSet,
                        typeof(StageMonsterSetSO),
                        false);
                }

                DrawStageMonsterSetPreview(selectedRoleSet);
            }
            else
            {
                DrawRecommendedPrefabPopup();
                objectPrefabToPlace = EditorGUILayout.ObjectField(
                    "스테이지 몬스터 프리팹",
                    objectPrefabToPlace,
                    typeof(GameObject),
                    false) as GameObject;
                if (objectPrefabToPlace != null &&
                    !IsPrefabCompatibleWithKind(objectPrefabToPlace, RoomObjectKind.Monster))
                {
                    EditorGUILayout.HelpBox(
                        "스테이지 몬스터 프리팹에는 자식 포함 Enemy 컴포넌트가 필요합니다.",
                        MessageType.Error);
                }
            }
        }
        else
        {
            DrawRecommendedPrefabPopup();
            objectPrefabToPlace = EditorGUILayout.ObjectField(
                "배치할 프리팹",
                objectPrefabToPlace,
                typeof(GameObject),
                false) as GameObject;
        }

        bool sourceIsReady = placingCommonRoleMonster
            ? selectedRoleSet != null
            : objectPrefabToPlace != null &&
              IsPrefabCompatibleWithKind(objectPrefabToPlace, objectKindToPlace);
        using (new EditorGUI.DisabledScope(selectedAuthoring.Grid == null || !sourceIsReady))
        {
            string addButtonLabel = placingCommonRoleMonster
                ? $"{monsterRoleToPlace} 스폰 지점을 방 중앙에 배치"
                : placingMonster
                    ? "스테이지 몬스터 지점을 방 중앙에 배치"
                    : "방 중앙에 배치 후 Scene View에서 이동";
            if (GUILayout.Button(addButtonLabel))
                AddRoomObject();
        }

        DrawTravelEndpointSection();

        EditorGUILayout.HelpBox(
            "함정·레버·퍼즐은 현재 Prop 프리팹으로 배치합니다. NPC 기능이 찾을 런타임 앵커는 " +
            "별도 방 데이터가 아니라 자체 완결형 Prop 프리팹 안의 ProceduralRoomAnchor로 구성하세요. " +
            "프리팹 내부 단계 상태는 자동 보존되지 않으므로 필요하면 별도 저장·복원 계약이 필요합니다.",
            MessageType.None);

        RoomObjectAuthoring[] objects = GetRoomObjects(selectedAuthoring);
        if (objects.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "배치된 오브젝트가 없습니다. Monster는 공통 역할 또는 스테이지 고정 프리팹을, 나머지는 프리팹을 선택해 추가하세요.",
                MessageType.Info);
            return;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            RoomObjectAuthoring roomObject = objects[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedObject serializedObject = new(roomObject);
                serializedObject.Update();
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("placementId"),
                    new GUIContent("Placement Id"));
                SerializedProperty kindProperty = serializedObject.FindProperty("kind");
                EditorGUILayout.PropertyField(kindProperty, new GUIContent("Kind"));
                if ((RoomObjectKind)kindProperty.enumValueIndex == RoomObjectKind.Monster)
                {
                    SerializedProperty prefabProperty = serializedObject.FindProperty("prefab");
                    SerializedProperty stageSetProperty =
                        serializedObject.FindProperty("monsterStageSet");
                    MonsterPlacementSourceMode sourceMode =
                        stageSetProperty.objectReferenceValue != null
                            ? MonsterPlacementSourceMode.CommonRole
                            : MonsterPlacementSourceMode.StageFixed;
                    sourceMode = (MonsterPlacementSourceMode)EditorGUILayout.Popup(
                        "몬스터 배치 방식",
                        (int)sourceMode,
                        MonsterPlacementSourceLabels);
                    if (sourceMode == MonsterPlacementSourceMode.CommonRole)
                    {
                        prefabProperty.objectReferenceValue = null;
                        SerializedProperty roleProperty =
                            serializedObject.FindProperty("monsterSpawnRole");
                        EditorGUILayout.PropertyField(roleProperty, new GUIContent("몬스터 역할"));
                        RoomMonsterSpawnRole role =
                            (RoomMonsterSpawnRole)roleProperty.enumValueIndex;
                        StageMonsterSetSO roleSet = LoadRoleStageMonsterSet(role);
                        stageSetProperty.objectReferenceValue = roleSet;
                        using (new EditorGUI.DisabledScope(true))
                        {
                            EditorGUILayout.ObjectField(
                                "진행도 몬스터 세트",
                                roleSet,
                                typeof(StageMonsterSetSO),
                                false);
                        }

                        DrawStageMonsterSetPreview(roleSet);
                    }
                    else
                    {
                        stageSetProperty.objectReferenceValue = null;
                        prefabProperty.objectReferenceValue = EditorGUILayout.ObjectField(
                            "스테이지 몬스터 프리팹",
                            prefabProperty.objectReferenceValue,
                            typeof(GameObject),
                            false);
                        GameObject stageMonsterPrefab =
                            prefabProperty.objectReferenceValue as GameObject;
                        if (stageMonsterPrefab != null &&
                            !IsPrefabCompatibleWithKind(
                                stageMonsterPrefab,
                                RoomObjectKind.Monster))
                        {
                            EditorGUILayout.HelpBox(
                                "스테이지 몬스터 프리팹에는 자식 포함 Enemy 컴포넌트가 필요합니다.",
                                MessageType.Error);
                        }
                    }

                    DrawLinkedChestLockPopup(
                        serializedObject.FindProperty("linkedChestLockPlacementId"),
                        objects);
                }
                serializedObject.ApplyModifiedProperties();

                if (roomObject.Kind == RoomObjectKind.Chest)
                    DrawChestKillLockLinks(roomObject, objects);

                if (roomObject.Kind != RoomObjectKind.Monster)
                    EditorGUILayout.ObjectField("Prefab", roomObject.Prefab, typeof(GameObject), false);
                if (roomObject.TryGetPlacementData(out RoomObjectPlacementData placement))
                {
                    EditorGUILayout.Vector2IntField("Local Cell", placement.localCell);
                    EditorGUILayout.Vector2Field("Cell Offset", placement.localOffset);
                    EditorGUILayout.FloatField("Rotation", placement.localRotationDegrees);
                }

                DrawCompositePoseOverrideEditor(roomObject);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select"))
                        Selection.activeObject = roomObject.gameObject;

                    if (GUILayout.Button("Delete"))
                    {
                        Undo.DestroyObjectImmediate(roomObject.gameObject);
                        SceneView.RepaintAll();
                        Repaint();
                        return;
                    }
                }
            }
        }

        EditorGUILayout.HelpBox(
            "Transform을 이동/회전/크기 조절하면 Grid 셀, 셀 중심 오프셋, 로컬 회전과 크기로 저장됩니다. " +
            "공통 역할 Monster는 Warrior/Mage/Tank의 수와 위치를 저장하고 보스 처치 수에 맞는 적을 생성합니다. " +
            "스테이지 Monster는 선택한 고정 프리팹을 같은 방 진입 지연 스폰 흐름으로 생성합니다. " +
            "선택한 Kill Lock 상자 연결도 최종 스폰 요청으로 전달됩니다.",
            MessageType.Info);
    }

    /// <summary>
    /// 책임:
    /// 복합 프리팹이 공개한 슬롯을 감지해 현재 방에서만 적용되는 위치·회전·크기 재정의를 편집한다.
    /// </summary>
    private void DrawCompositePoseOverrideEditor(RoomObjectAuthoring roomObject)
    {
        if (roomObject == null ||
            !roomObject.TryGetCompositePoseAuthoring(out RoomCompositePoseAuthoring composite))
        {
            return;
        }

        IReadOnlyList<RoomCompositePoseSlotData> slots = composite.PoseSlots;
        if (slots == null || slots.Count == 0)
            return;

        roomObject.EditorCaptureCompositePoseOverrides();
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("복합 오브젝트 세부 배치", EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox(
            "체크한 슬롯만 현재 방 데이터로 재정의됩니다. 체크하지 않은 슬롯은 프리팹 기본 배치를 계속 따릅니다.",
            MessageType.None);

        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            RoomCompositePoseSlotData slot = slots[slotIndex];
            Transform target = slot.Target;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(slot.DisplayName, EditorStyles.miniBoldLabel);
                if (target == null)
                {
                    EditorGUILayout.HelpBox(
                        $"슬롯 '{slot.SlotId}'의 대상 Transform이 비어 있습니다.",
                        MessageType.Error);
                    continue;
                }

                bool hasOverride = roomObject.EditorTryGetChildPoseOverride(
                    slot.SlotId,
                    out RoomObjectChildPoseOverrideData poseOverride);
                bool requestedOverride = EditorGUILayout.ToggleLeft(
                    "이 방에서 자세 재정의",
                    hasOverride);
                if (requestedOverride != hasOverride)
                {
                    Undo.RecordObject(roomObject, "Toggle Composite Object Pose Override");
                    Undo.RecordObject(target, "Toggle Composite Object Pose Override");
                    if (requestedOverride)
                    {
                        poseOverride = new RoomObjectChildPoseOverrideData
                        {
                            slotId = slot.SlotId,
                            overridePosition = slot.AllowPosition,
                            localPosition = target.localPosition,
                            overrideRotation = slot.AllowRotation,
                            localRotationDegrees = Mathf.DeltaAngle(0f, target.localEulerAngles.z),
                            overrideScale = slot.AllowScale,
                            localScale = target.localScale
                        };
                        roomObject.EditorSetChildPoseOverride(poseOverride);
                    }
                    else
                    {
                        roomObject.EditorRemoveChildPoseOverride(
                            slot.SlotId,
                            restorePrefabPose: true);
                    }

                    EditorUtility.SetDirty(roomObject);
                    EditorUtility.SetDirty(target);
                    RoomAuthoringWorkspace.MarkDirty();
                    SceneView.RepaintAll();
                    hasOverride = requestedOverride;
                }

                if (hasOverride)
                {
                    roomObject.EditorTryGetChildPoseOverride(slot.SlotId, out poseOverride);
                    EditorGUI.BeginChangeCheck();
                    if (slot.AllowPosition)
                    {
                        poseOverride.overridePosition = EditorGUILayout.Toggle(
                            "위치 적용",
                            poseOverride.overridePosition);
                        using (new EditorGUI.DisabledScope(!poseOverride.overridePosition))
                        {
                            poseOverride.localPosition = EditorGUILayout.Vector3Field(
                                "Local Position",
                                poseOverride.localPosition);
                        }
                    }

                    if (slot.AllowRotation)
                    {
                        poseOverride.overrideRotation = EditorGUILayout.Toggle(
                            "회전 적용",
                            poseOverride.overrideRotation);
                        using (new EditorGUI.DisabledScope(!poseOverride.overrideRotation))
                        {
                            poseOverride.localRotationDegrees = EditorGUILayout.FloatField(
                                "Local Rotation Z",
                                poseOverride.localRotationDegrees);
                        }
                    }

                    if (slot.AllowScale)
                    {
                        poseOverride.overrideScale = EditorGUILayout.Toggle(
                            "크기 적용",
                            poseOverride.overrideScale);
                        using (new EditorGUI.DisabledScope(!poseOverride.overrideScale))
                        {
                            poseOverride.localScale = EditorGUILayout.Vector3Field(
                                "Local Scale",
                                poseOverride.localScale);
                        }
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(roomObject, "Edit Composite Object Pose Override");
                        Undo.RecordObject(target, "Edit Composite Object Pose Override");
                        roomObject.EditorSetChildPoseOverride(poseOverride);
                        EditorUtility.SetDirty(roomObject);
                        EditorUtility.SetDirty(target);
                        RoomAuthoringWorkspace.MarkDirty();
                        SceneView.RepaintAll();
                    }
                }

                if (GUILayout.Button("Scene View에서 대상 선택"))
                    Selection.activeObject = target.gameObject;
            }
        }
    }

    private void DrawTravelEndpointSection()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("씬 이동 Endpoint 슬롯", EditorStyles.boldLabel);
        travelEndpointKindToPlace = (RoomTravelEndpointKind)EditorGUILayout.EnumPopup(
            "매개체 방식",
            travelEndpointKindToPlace);
        if (travelEndpointKindToPlace == RoomTravelEndpointKind.Trigger)
        {
            travelTriggerSizeToPlace = RoomTravelEndpointGeometry.SanitizeTriggerSize(
                EditorGUILayout.Vector2Field(
                    "Trigger Size (월드 단위)",
                    travelTriggerSizeToPlace));
        }
        travelMediumPrefabToPlace = EditorGUILayout.ObjectField(
            "매개체 프리팹 (선택)",
            travelMediumPrefabToPlace,
            typeof(GameObject),
            false) as GameObject;

        using (new EditorGUI.DisabledScope(selectedAuthoring.Grid == null))
        {
            if (GUILayout.Button("이동 슬롯을 방 중앙에 추가"))
                AddTravelEndpoint();
        }

        RoomTravelEndpointAuthoring[] endpoints = GetTravelEndpoints(selectedAuthoring);
        for (int i = 0; i < endpoints.Length; i++)
        {
            RoomTravelEndpointAuthoring endpoint = endpoints[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedObject serializedEndpoint = new(endpoint);
                serializedEndpoint.Update();
                SerializedProperty endpointKindProperty = serializedEndpoint.FindProperty("kind");
                EditorGUILayout.PropertyField(
                    serializedEndpoint.FindProperty("slotId"),
                    new GUIContent("Slot Id"));
                EditorGUILayout.PropertyField(
                    endpointKindProperty,
                    new GUIContent("Medium Kind"));
                EditorGUILayout.PropertyField(
                    serializedEndpoint.FindProperty("mediumPrefab"),
                    new GUIContent("Medium Prefab"));
                if ((RoomTravelEndpointKind)endpointKindProperty.enumValueIndex ==
                    RoomTravelEndpointKind.Trigger)
                {
                    EditorGUILayout.PropertyField(
                        serializedEndpoint.FindProperty("triggerSize"),
                        new GUIContent("Trigger Size (월드 단위)"));
                    EditorGUILayout.HelpBox(
                        "판정 크기는 매개체 Transform Scale과 별개입니다. " +
                        "Scene View의 파란 사각형이 런타임 Trigger 범위입니다.",
                        MessageType.None);
                }

                if (serializedEndpoint.ApplyModifiedProperties())
                {
                    RoomAuthoringWorkspace.MarkDirty();
                    SceneView.RepaintAll();
                }

                bool requestedSeparateArrival = EditorGUILayout.Toggle(
                    "도착 위치 별도 지정",
                    endpoint.UseSeparateArrivalPoint);
                if (requestedSeparateArrival != endpoint.UseSeparateArrivalPoint)
                {
                    Undo.RecordObject(endpoint, "Toggle Separate Travel Arrival Point");
                    endpoint.EditorSetUseSeparateArrivalPoint(requestedSeparateArrival);
                    EditorUtility.SetDirty(endpoint);
                    RoomAuthoringWorkspace.MarkDirty();
                    SceneView.RepaintAll();
                }

                if (endpoint.TryGetPlacementData(out RoomTravelEndpointPlacementData placement))
                {
                    EditorGUILayout.Vector2IntField("Local Cell", placement.localCell);
                    EditorGUILayout.Vector2Field("Cell Offset", placement.localOffset);
                    EditorGUILayout.FloatField("Rotation", placement.localRotationDegrees);

                    if (placement.useSeparateArrivalPoint)
                    {
                        EditorGUI.BeginChangeCheck();
                        Vector2Int requestedArrivalCell = EditorGUILayout.Vector2IntField(
                            "도착 Local Cell",
                            placement.arrivalLocalCell);
                        Vector2 requestedArrivalOffset = EditorGUILayout.Vector2Field(
                            "도착 Cell Offset",
                            placement.arrivalLocalOffset);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(endpoint, "Edit Travel Arrival Point");
                            endpoint.EditorSetArrivalPlacement(
                                requestedArrivalCell,
                                requestedArrivalOffset);
                            EditorUtility.SetDirty(endpoint);
                            RoomAuthoringWorkspace.MarkDirty();
                            SceneView.RepaintAll();
                        }

                        EditorGUILayout.HelpBox(
                            "Select를 누른 뒤 Scene View의 녹색 도착 핸들을 끌어 착지 위치를 조정할 수 있습니다.",
                            MessageType.None);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select"))
                        Selection.activeObject = endpoint.gameObject;

                    if (GUILayout.Button("Delete"))
                    {
                        Undo.DestroyObjectImmediate(endpoint.gameObject);
                        RoomAuthoringWorkspace.MarkDirty();
                        SceneView.RepaintAll();
                        Repaint();
                        return;
                    }
                }
            }
        }

        EditorGUILayout.HelpBox(
            "방 템플릿에는 Slot Id와 배치만 저장됩니다. 실제 SceneConnectionSO와 A/B 방향은 " +
            "각 복도 씬의 DungeonRoomBuilder Travel Endpoint Bindings에서 연결하므로 방 데이터를 다른 테마에도 재사용할 수 있습니다. " +
            "도착 위치를 분리하면 이동 트리거와 겹치지 않는 방 안쪽 안전 지점에 플레이어를 배치할 수 있습니다.",
            MessageType.Info);

        if (GUILayout.Button("이동 연결·연출 편집기 열기"))
            ProceduralTravelBindingEditorWindow.Open(selectedAuthoring.SourceTemplate);
    }

    private void DrawRecommendedPrefabPopup()
    {
        List<GameObject> recommendedPrefabs = CollectLibraryPrefabs(objectKindToPlace);
        if (recommendedPrefabs.Count == 0)
        {
            if (selectedLibrary != null)
            {
                EditorGUILayout.HelpBox(
                    "이 라이브러리에서 같은 종류의 기존 프리팹을 찾지 못했습니다. 아래 필드에서 직접 선택할 수 있습니다.",
                    MessageType.None);
            }

            return;
        }

        string[] displayNames = new string[recommendedPrefabs.Count + 1];
        displayNames[0] = "라이브러리에서 선택...";
        int selectedIndex = 0;
        for (int prefabIndex = 0; prefabIndex < recommendedPrefabs.Count; prefabIndex++)
        {
            GameObject prefab = recommendedPrefabs[prefabIndex];
            displayNames[prefabIndex + 1] = prefab.name;
            if (prefab == objectPrefabToPlace)
                selectedIndex = prefabIndex + 1;
        }

        int requestedIndex = EditorGUILayout.Popup(
            "추천 프리팹",
            selectedIndex,
            displayNames);
        if (requestedIndex > 0)
            objectPrefabToPlace = recommendedPrefabs[requestedIndex - 1];
    }

    /// <summary>
    /// 책임:
    /// - 툴의 Warrior/Mage/Tank 선택을 프로젝트 공용 StageMonsterSetSO 에셋에 연결한다.
    /// - 기획자가 개별 몬스터 프리팹이나 세트 에셋 경로를 직접 찾지 않게 한다.
    /// </summary>
    private static StageMonsterSetSO LoadRoleStageMonsterSet(RoomMonsterSpawnRole role)
    {
        string fileName = role switch
        {
            RoomMonsterSpawnRole.Warrior => "CommonMeleeStageMonsterSet.asset",
            RoomMonsterSpawnRole.Mage => "CommonRangedStageMonsterSet.asset",
            RoomMonsterSpawnRole.Tank => "CommonTankStageMonsterSet.asset",
            _ => string.Empty
        };
        return string.IsNullOrEmpty(fileName)
            ? null
            : AssetDatabase.LoadAssetAtPath<StageMonsterSetSO>(
                $"{MonsterSpawnSetFolder}/{fileName}");
    }

    /// <summary>
    /// 책임:
    /// - 선택한 역할 지점에서 보스 처치 수 0/1/2에 실제로 생성될 몬스터를 즉시 보여준다.
    /// </summary>
    private static void DrawStageMonsterSetPreview(StageMonsterSetSO stageSet)
    {
        if (stageSet == null)
        {
            EditorGUILayout.HelpBox(
                "이 역할에 연결할 StageMonsterSetSO를 찾지 못했습니다.",
                MessageType.Error);
            return;
        }

        IReadOnlyList<GameObject> stagePrefabs = stageSet.StagePrefabs;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("진행도별 실제 몬스터", EditorStyles.miniBoldLabel);
            for (int stageIndex = 0; stageIndex < 3; stageIndex++)
            {
                GameObject prefab = stagePrefabs != null && stagePrefabs.Count > 0
                    ? stagePrefabs[Mathf.Min(stageIndex, stagePrefabs.Count - 1)]
                    : null;
                EditorGUILayout.ObjectField(
                    $"보스 {stageIndex}마리 처치",
                    prefab,
                    typeof(GameObject),
                    false);
            }
        }
    }

    private List<GameObject> CollectLibraryPrefabs(RoomObjectKind kind)
    {
        List<GameObject> results = new();
        if (selectedLibrary == null)
            return results;

        HashSet<GameObject> seen = new();
        if (kind == RoomObjectKind.Monster)
        {
            IReadOnlyList<GameObject> stageMonsterPrefabs =
                selectedLibrary.StageMonsterPrefabs;
            if (stageMonsterPrefabs != null)
            {
                for (int prefabIndex = 0;
                     prefabIndex < stageMonsterPrefabs.Count;
                     prefabIndex++)
                {
                    GameObject prefab = stageMonsterPrefabs[prefabIndex];
                    if (IsPrefabCompatibleWithKind(prefab, kind) && seen.Add(prefab))
                        results.Add(prefab);
                }
            }
        }

        IReadOnlyList<RoomTemplateSO> rooms = selectedLibrary.Rooms;
        for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
        {
            RoomTemplateSO room = rooms[roomIndex];
            List<RoomObjectPlacementData> placements = room != null
                ? room.BuildData.objectPlacements
                : null;
            if (placements == null)
                continue;

            for (int placementIndex = 0; placementIndex < placements.Count; placementIndex++)
            {
                RoomObjectPlacementData placement = placements[placementIndex];
                if (placement.kind == kind &&
                    placement.prefab != null &&
                    IsPrefabCompatibleWithKind(placement.prefab, kind) &&
                    seen.Add(placement.prefab))
                {
                    results.Add(placement.prefab);
                }
            }
        }

        results.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        return results;
    }

    private static void DrawLinkedChestLockPopup(
        SerializedProperty linkedChestPlacementIdProperty,
        IReadOnlyList<RoomObjectAuthoring> roomObjects)
    {
        List<string> placementIds = new() { string.Empty };
        List<string> displayNames = new() { "없음" };
        for (int i = 0; i < roomObjects.Count; i++)
        {
            RoomObjectAuthoring candidate = roomObjects[i];
            if (!IsKillLockChest(candidate) ||
                string.IsNullOrWhiteSpace(candidate.PlacementId) ||
                placementIds.Contains(candidate.PlacementId))
            {
                continue;
            }

            placementIds.Add(candidate.PlacementId);
            displayNames.Add(candidate.PlacementId);
        }

        string currentId = linkedChestPlacementIdProperty.stringValue ?? string.Empty;
        int currentIndex = placementIds.IndexOf(currentId);
        if (currentIndex < 0)
        {
            placementIds.Add(currentId);
            displayNames.Add($"누락됨: {currentId}");
            currentIndex = placementIds.Count - 1;
        }

        int selectedIndex = EditorGUILayout.Popup(
            new GUIContent(
                "Kill Lock Chest",
                "기존 MonsterSpawnRequest.LinkedChestKillLock 계약으로 연결할 같은 방의 상자입니다."),
            currentIndex,
            displayNames.ToArray());
        linkedChestPlacementIdProperty.stringValue = placementIds[selectedIndex];
    }

    private static void DrawChestKillLockLinks(
        RoomObjectAuthoring chest,
        IReadOnlyList<RoomObjectAuthoring> roomObjects)
    {
        if (!IsKillLockChest(chest))
        {
            EditorGUILayout.HelpBox(
                "Kill Lock 동작과 연출은 ChestMonsterKillLock이 구성된 상자 Prefab에서 제공합니다.",
                MessageType.None);
            return;
        }

        int monsterCount = 0;
        int linkedMonsterCount = 0;
        for (int i = 0; i < roomObjects.Count; i++)
        {
            RoomObjectAuthoring candidate = roomObjects[i];
            if (candidate.Kind != RoomObjectKind.Monster)
                continue;

            monsterCount++;
            if (candidate.LinkedChestLockPlacementId == chest.PlacementId)
                linkedMonsterCount++;
        }

        EditorGUILayout.LabelField("Linked Monsters", $"{linkedMonsterCount} / {monsterCount}");
        using (new EditorGUI.DisabledScope(
                   monsterCount == 0 || string.IsNullOrWhiteSpace(chest.PlacementId)))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("모두 연결"))
                SetMonsterChestLockLinks(roomObjects, chest.PlacementId, clearOnlyMatching: false);

            if (GUILayout.Button("연결 해제"))
                SetMonsterChestLockLinks(roomObjects, chest.PlacementId, clearOnlyMatching: true);
        }
    }

    private static void SetMonsterChestLockLinks(
        IReadOnlyList<RoomObjectAuthoring> roomObjects,
        string chestPlacementId,
        bool clearOnlyMatching)
    {
        for (int i = 0; i < roomObjects.Count; i++)
        {
            RoomObjectAuthoring candidate = roomObjects[i];
            if (candidate.Kind != RoomObjectKind.Monster ||
                (clearOnlyMatching && candidate.LinkedChestLockPlacementId != chestPlacementId))
            {
                continue;
            }

            Undo.RecordObject(candidate, "Configure Room Monster Chest Lock");
            candidate.EditorSetLinkedChestLockPlacementId(
                clearOnlyMatching ? string.Empty : chestPlacementId);
            EditorUtility.SetDirty(candidate);
        }
    }

    private static bool IsKillLockChest(RoomObjectAuthoring roomObject)
    {
        return roomObject != null &&
               roomObject.Kind == RoomObjectKind.Chest &&
               roomObject.Prefab != null &&
               roomObject.Prefab.GetComponentInChildren<ChestMonsterKillLock>(true) != null;
    }

    private void DrawBakeSection()
    {
        EditorGUILayout.LabelField("검증과 저장", EditorStyles.boldLabel);

        if (GUILayout.Button("지금 전체 검증"))
            ValidateSelectedRoomPiece(showDialog: true);

        registerWithSelectedLibrary = EditorGUILayout.ToggleLeft(
            "저장 후 선택한 테마 라이브러리에 자동 등록",
            registerWithSelectedLibrary);
        if (registerWithSelectedLibrary && selectedLibrary == null)
        {
            EditorGUILayout.HelpBox(
                "자동 등록을 사용하려면 상단에서 테마 룸 라이브러리를 선택하세요.",
                MessageType.Warning);
        }

        if (selectedAuthoring != null && selectedAuthoring.SourceTemplate != null)
        {
            EditorGUILayout.ObjectField(
                "갱신할 원본",
                selectedAuthoring.SourceTemplate,
                typeof(RoomTemplateSO),
                false);

            if (GUILayout.Button("검증 후 원본 템플릿 갱신"))
                ApplyChangesToLoadedTemplate();

            EditorGUILayout.Space(6f);
        }

        using (new EditorGUI.DisabledScope(selectedAuthoring == null))
        {
            if (GUILayout.Button("검증 후 새 RoomTemplateSO로 저장"))
                SaveAsNewRoomTemplate();
        }

        if (validationMessages.Count <= 0)
            return;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("검증 결과", EditorStyles.boldLabel);
        for (int i = 0; i < validationMessages.Count; i++)
            EditorGUILayout.HelpBox(validationMessages[i], MessageType.Warning);
    }

    private RoomPieceAuthoring ResolveSelectedAuthoring()
    {
        if (Selection.activeGameObject != null)
        {
            RoomPieceAuthoring selectionAuthoring =
                Selection.activeGameObject.GetComponentInParent<RoomPieceAuthoring>();
            if (selectionAuthoring != null &&
                RoomAuthoringWorkspace.IsInWorkspace(selectionAuthoring.gameObject))
            {
                return selectionAuthoring;
            }
        }

        if (selectedAuthoring != null &&
            RoomAuthoringWorkspace.IsInWorkspace(selectedAuthoring.gameObject))
        {
            return selectedAuthoring;
        }

        return RoomAuthoringWorkspace.FindAuthoring();
    }

    private void DrawDungeonPreviewSection()
    {
        EditorGUILayout.LabelField("절차 던전 동적 미리보기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "실제 DungeonLayoutAssembler로 방과 복도를 배치합니다. 타일은 실제 데이터로 만들고, 문과 게임플레이 오브젝트는 실행하지 않고 Scene View 표식으로만 보여줍니다.",
            MessageType.Info);

        previewIncludeCurrentRoom = EditorGUILayout.ToggleLeft(
            "저장하지 않은 현재 편집 방 포함",
            previewIncludeCurrentRoom);
        if (previewIncludeCurrentRoom)
        {
            EditorGUILayout.HelpBox(
                "원본 방을 편집 중이면 라이브러리의 저장본 대신 현재 작업 복사본을 임시로 사용합니다. 원본 에셋과 라이브러리는 변경하지 않습니다.",
                MessageType.None);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("테마 생성 프로필", EditorStyles.miniBoldLabel);
        DungeonGenerationProfileSO requestedProfile = EditorGUILayout.ObjectField(
            "생성 프로필",
            previewGenerationProfile,
            typeof(DungeonGenerationProfileSO),
            false) as DungeonGenerationProfileSO;
        if (requestedProfile != previewGenerationProfile)
        {
            previewGenerationProfile = requestedProfile;
            InvalidateSceneReferenceCount();
            if (previewGenerationProfile != null)
                LoadPreviewSettingsFromProfile();
        }

        bool profileMatchesLibrary = previewGenerationProfile == null ||
            previewGenerationProfile.RoomLibrary == selectedLibrary;
        if (!profileMatchesLibrary)
        {
            EditorGUILayout.HelpBox(
                "선택한 생성 프로필이 현재 테마 룸 라이브러리와 연결되어 있지 않습니다. 테마 프로필 찾기/만들기를 사용하세요.",
                MessageType.Warning);
        }
        else if (previewGenerationProfile != null)
        {
            int sceneReferenceCount = ResolveSceneReferenceCount();
            EditorGUILayout.HelpBox(
                sceneReferenceCount > 0
                    ? $"이 프로필은 활성 Build Settings 씬 {sceneReferenceCount}곳에서 사용됩니다. 적용한 값은 다음 씬 생성부터 바로 반영됩니다."
                    : "아직 활성 Build Settings 씬에서 이 프로필을 참조하지 않습니다. 보스 테마 생성 프로필 설치를 먼저 실행하세요.",
                sceneReferenceCount > 0 ? MessageType.Info : MessageType.Warning);
            DrawGuaranteedRoomTemplates(previewGenerationProfile, selectedLibrary);
            EditorGUILayout.LabelField(
                "복도 장식",
                previewGenerationProfile.CorridorDecorationProfile != null
                    ? $"{previewGenerationProfile.CorridorDecorationProfile.name} · " +
                      $"모듈 {previewGenerationProfile.CorridorDecorationProfile.Modules.Count}개"
                    : "미설정");
            if (GUILayout.Button("복도 장식 제작 툴 열기"))
                CorridorDecorationEditorWindow.OpenWithProfile(previewGenerationProfile);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("테마 프로필 찾기/만들기"))
                EnsurePreviewGenerationProfile();

            using (new EditorGUI.DisabledScope(previewGenerationProfile == null || !profileMatchesLibrary))
            {
                if (GUILayout.Button("프로필 값 불러오기"))
                    LoadPreviewSettingsFromProfile();
            }
        }

        previewLayoutPolicy = EditorGUILayout.ObjectField(
            "레이아웃 정책",
            previewLayoutPolicy,
            typeof(DungeonLayoutPolicySO),
            false) as DungeonLayoutPolicySO;
        if (previewLayoutPolicy != null)
        {
            EditorGUILayout.HelpBox(
                "그래프를 먼저 만든 뒤 보스 거리, 분기, 순환로와 필수 방 역할을 보장하는 탐색형 배치를 사용합니다.",
                MessageType.None);
        }

        previewSeed = EditorGUILayout.IntField("시드", previewSeed);
        previewIncludeBossRoom = EditorGUILayout.Toggle("보스 방 포함", previewIncludeBossRoom);
        previewRoomCount = EditorGUILayout.IntField(
            "방 개수",
            Mathf.Max(previewIncludeBossRoom ? 2 : 1, previewRoomCount));

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("복도 타일", EditorStyles.miniBoldLabel);
        previewCorridorFloorTile = EditorGUILayout.ObjectField(
            "바닥 타일 오버라이드",
            previewCorridorFloorTile,
            typeof(TileBase),
            false) as TileBase;
        previewCorridorWallTile = EditorGUILayout.ObjectField(
            "벽 타일 오버라이드",
            previewCorridorWallTile,
            typeof(TileBase),
            false) as TileBase;
        if (previewCorridorFloorTile == null || previewCorridorWallTile == null)
        {
            EditorGUILayout.HelpBox(
                "비어 있는 항목은 선택한 라이브러리에서 가장 많이 사용된 Floor/Wall 타일을 자동으로 고릅니다.",
                MessageType.None);
        }

        previewAdvancedSettings = EditorGUILayout.Foldout(
            previewAdvancedSettings,
            "고급 배치 설정",
            true);
        if (previewAdvancedSettings)
        {
            EditorGUI.indentLevel++;
            previewMaxPlacementAttemptsPerRoom = EditorGUILayout.IntField(
                "방당 최대 배치 시도",
                Mathf.Max(1, previewMaxPlacementAttemptsPerRoom));
            previewMinimumCorridorLength = EditorGUILayout.IntField(
                "절대 최소 복도 길이",
                Mathf.Max(0, previewMinimumCorridorLength));
            previewCorridorLengthPerRoomCell = EditorGUILayout.Slider(
                "방 크기 권장 추가 비율",
                Mathf.Clamp01(previewCorridorLengthPerRoomCell),
                0f,
                1f);
            previewCorridorLengthVariation = EditorGUILayout.IntSlider(
                "권장 난수 추가 폭",
                Mathf.Clamp(previewCorridorLengthVariation, 0, 32),
                0,
                32);
            EditorGUI.indentLevel--;
            EditorGUILayout.HelpBox(
                "방 크기 비율과 난수 폭은 우선 시도할 추가 길이입니다. " +
                "충돌하면 절대 최소 길이까지 줄이고, 여러 성공 배치 중 권장 길이 초과가 가장 작은 결과를 우선합니다. " +
                "짧은 후보가 없어도 가장 나은 성공 배치를 사용하므로 생성 자체를 막지는 않습니다.",
                MessageType.None);
        }

        using (new EditorGUI.DisabledScope(selectedLibrary == null || !profileMatchesLibrary))
        {
            if (GUILayout.Button("현재 미리보기 설정을 테마에 적용", GUILayout.Height(26f)))
                ApplyPreviewSettingsToProfile();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(selectedLibrary == null))
            {
                if (GUILayout.Button("동적 생성 미리보기", GUILayout.Height(28f)))
                    GenerateDungeonPreview();
            }

            if (GUILayout.Button("랜덤 시드", GUILayout.Height(28f)))
            {
                previewSeed = System.Guid.NewGuid().GetHashCode();
                GenerateDungeonPreview();
            }

            using (new EditorGUI.DisabledScope(!RoomAuthoringDungeonPreview.HasPreview))
            {
                if (GUILayout.Button("미리보기 지우기", GUILayout.Height(28f)))
                {
                    RoomAuthoringDungeonPreview.Clear();
                    previewStatusMessage = "미리보기를 지웠습니다.";
                    previewStatusType = MessageType.None;
                }
            }
        }

        if (selectedLibrary == null)
        {
            EditorGUILayout.HelpBox(
                "상단에서 테마 룸 라이브러리를 선택해야 배치 후보를 구성할 수 있습니다.",
                MessageType.Warning);
        }

        if (!string.IsNullOrWhiteSpace(previewStatusMessage))
            EditorGUILayout.HelpBox(previewStatusMessage, previewStatusType);
        else if (RoomAuthoringDungeonPreview.HasPreview)
            EditorGUILayout.HelpBox("Scene View에 마지막 생성 결과가 표시되어 있습니다.", MessageType.Info);

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "표식: 초록 테두리=현재 편집 방, 청록 점선=소켓 연결, M=몬스터, C=상자, P=포털, O=프롭, EI/ET/EA=이동 슬롯",
            MessageType.None);
    }

    private void GenerateDungeonPreview()
    {
        selectedAuthoring = ResolveSelectedAuthoring();
        if (selectedLibrary == null || selectedAuthoring == null || selectedAuthoring.Grid == null)
        {
            previewStatusMessage = "라이브러리와 편집 중인 방의 Grid가 필요합니다.";
            previewStatusType = MessageType.Error;
            return;
        }

        RoomLayoutData currentLayout = default;
        RoomBuildData currentBuild = default;
        if (previewIncludeCurrentRoom &&
            !TryCollectSelectedRoomData(
                out currentLayout,
                out currentBuild,
                showFailureDialog: false))
        {
            previewStatusMessage = validationMessages.Count > 0
                ? "현재 방을 먼저 수정해 주세요.\n" + string.Join("\n", validationMessages)
                : "현재 방 데이터를 미리보기용으로 수집하지 못했습니다.";
            previewStatusType = MessageType.Error;
            return;
        }

        previewRoomCount = Mathf.Max(
            previewIncludeBossRoom ? 2 : 1,
            previewRoomCount);
        previewMaxPlacementAttemptsPerRoom = Mathf.Max(
            1,
            previewMaxPlacementAttemptsPerRoom);
        previewMinimumCorridorLength = Mathf.Max(0, previewMinimumCorridorLength);
        previewCorridorLengthPerRoomCell = Mathf.Clamp01(
            previewCorridorLengthPerRoomCell);
        previewCorridorLengthVariation = Mathf.Clamp(
            previewCorridorLengthVariation,
            0,
            32);

        RoomAuthoringDungeonPreviewRequest request = new(
            selectedLibrary,
            previewLayoutPolicy,
            previewIncludeCurrentRoom,
            selectedAuthoring.SourceTemplate,
            currentLayout,
            currentBuild,
            selectedAuthoring.Grid,
            selectedAuthoring.Size,
            previewCorridorFloorTile,
            previewCorridorWallTile,
            previewSeed,
            previewRoomCount,
            previewIncludeBossRoom,
            previewMaxPlacementAttemptsPerRoom,
            previewMinimumCorridorLength,
            previewCorridorLengthPerRoomCell,
            previewCorridorLengthVariation,
            previewGenerationProfile != null &&
            previewGenerationProfile.RoomLibrary == selectedLibrary
                ? previewGenerationProfile.GuaranteedRoomTemplates
                : null,
            previewGenerationProfile != null &&
            previewGenerationProfile.RoomLibrary == selectedLibrary
                ? previewGenerationProfile.CorridorDecorationProfile
                : null);
        RoomAuthoringDungeonPreviewResult result =
            RoomAuthoringDungeonPreview.Generate(request);

        if (!result.WasBuilt)
        {
            previewStatusMessage = $"미리보기 생성 실패 · {result.Message}";
            previewStatusType = MessageType.Error;
            return;
        }

        string completionText = result.IsComplete
            ? "완성"
            : "부분 생성";
        string currentRoomText = previewIncludeCurrentRoom
            ? $" · 현재 방 {result.CurrentRoomPlacementCount}회"
            : string.Empty;
        string failureText = string.IsNullOrWhiteSpace(result.Message)
            ? string.Empty
            : $"\n배치 중단 사유: {result.Message}";
        string relaxationText = result.UsedCorridorLengthRelaxation
            ? " · 충돌 회피 자동 축소"
            : string.Empty;
        previewStatusMessage =
            $"{completionText} · Seed {previewSeed} · 방 {result.RoomCount}/{result.RequestedRoomCount} · " +
            $"연결 {result.ConnectionCount}개{currentRoomText}\n" +
            $"복도 길이: {result.ShortestCorridorLength}..{result.LongestCorridorLength}{relaxationText}\n" +
            $"복도 타일: {result.CorridorFloorTileName} / {result.CorridorWallTileName}" +
            failureText;
        previewStatusType = result.IsComplete &&
            (!previewIncludeCurrentRoom || result.CurrentRoomPlacementCount > 0)
                ? MessageType.Info
                : MessageType.Warning;
    }

    /// <summary>
    /// 책임 : 현재 테마 라이브러리에 대응하는 영속 생성 프로필을 선택하고 저장값을 미리보기 입력란에 복원한다.
    /// </summary>
    private void SelectPreviewGenerationProfileForLibrary()
    {
        previewGenerationProfile =
            DungeonGenerationProfileAssetUtility.FindForLibrary(selectedLibrary);
        InvalidateSceneReferenceCount();
        if (previewGenerationProfile != null)
            LoadPreviewSettingsFromProfile();
    }

    /// <summary>
    /// 책임 : 현재 미리보기 값을 초기값으로 사용해 선택 테마의 생성 프로필을 찾거나 새로 만든다.
    /// </summary>
    private void EnsurePreviewGenerationProfile()
    {
        if (selectedLibrary == null)
        {
            previewStatusMessage = "테마 룸 라이브러리를 먼저 선택하세요.";
            previewStatusType = MessageType.Warning;
            return;
        }

        previewGenerationProfile =
            DungeonGenerationProfileAssetUtility.FindOrCreateForLibrary(
                selectedLibrary,
                previewLayoutPolicy,
                previewSeed,
                previewRoomCount,
                previewIncludeBossRoom,
                previewMaxPlacementAttemptsPerRoom,
                previewMinimumCorridorLength,
                previewCorridorLengthPerRoomCell,
                previewCorridorLengthVariation);
        InvalidateSceneReferenceCount();
        LoadPreviewSettingsFromProfile();
        Selection.activeObject = previewGenerationProfile;
        EditorGUIUtility.PingObject(previewGenerationProfile);
        previewStatusMessage = $"테마 생성 프로필을 준비했습니다: {previewGenerationProfile.name}";
        previewStatusType = MessageType.Info;
    }

    /// <summary>
    /// 책임 : 선택 프로필의 영속 생성 수치를 미리보기 입력값으로 복사한다.
    /// </summary>
    private void LoadPreviewSettingsFromProfile()
    {
        if (previewGenerationProfile == null)
            return;

        previewLayoutPolicy = previewGenerationProfile.LayoutPolicy;
        previewSeed = previewGenerationProfile.Seed;
        previewRoomCount = previewGenerationProfile.RoomCount;
        previewIncludeBossRoom = previewGenerationProfile.IncludeBossRoom;
        previewMaxPlacementAttemptsPerRoom = previewGenerationProfile.MaxPlacementAttemptsPerRoom;
        previewMinimumCorridorLength = previewGenerationProfile.MinimumCorridorLength;
        previewCorridorLengthPerRoomCell = previewGenerationProfile.CorridorLengthPerRoomCell;
        previewCorridorLengthVariation = previewGenerationProfile.CorridorLengthVariation;
        Repaint();
    }

    /// <summary>
    /// 책임 : 기획자가 미리보기로 확인한 수치를 테마 프로필에 저장해 해당 프로필을 참조하는 실제 복도 생성에 반영한다.
    /// </summary>
    private void ApplyPreviewSettingsToProfile()
    {
        if (selectedLibrary == null)
            return;

        if (previewGenerationProfile == null)
        {
            EnsurePreviewGenerationProfile();
            if (previewGenerationProfile == null)
                return;
        }

        if (previewGenerationProfile.RoomLibrary != selectedLibrary)
        {
            previewStatusMessage = "현재 테마와 다른 생성 프로필에는 적용할 수 없습니다.";
            previewStatusType = MessageType.Error;
            return;
        }

        previewRoomCount = Mathf.Max(previewIncludeBossRoom ? 2 : 1, previewRoomCount);
        previewMaxPlacementAttemptsPerRoom = Mathf.Max(1, previewMaxPlacementAttemptsPerRoom);
        previewMinimumCorridorLength = Mathf.Max(0, previewMinimumCorridorLength);
        previewCorridorLengthPerRoomCell = Mathf.Clamp01(previewCorridorLengthPerRoomCell);
        previewCorridorLengthVariation = Mathf.Clamp(previewCorridorLengthVariation, 0, 32);

        Undo.RecordObject(previewGenerationProfile, "Apply Dungeon Generation Profile");
        previewGenerationProfile.EditorConfigure(
            selectedLibrary,
            previewLayoutPolicy,
            previewSeed,
            previewRoomCount,
            previewIncludeBossRoom,
            previewMaxPlacementAttemptsPerRoom,
            previewMinimumCorridorLength,
            previewCorridorLengthPerRoomCell,
            previewCorridorLengthVariation);
        EditorUtility.SetDirty(previewGenerationProfile);
        AssetDatabase.SaveAssets();

        int sceneReferenceCount = ResolveSceneReferenceCount(forceRefresh: true);
        previewStatusMessage = sceneReferenceCount > 0
            ? $"테마 생성 프로필을 저장했습니다. 실제 복도 씬 {sceneReferenceCount}곳에 적용됩니다."
            : "프로필을 저장했지만 참조하는 활성 복도 씬이 없습니다. 보스 테마 생성 프로필 설치가 필요합니다.";
        previewStatusType = sceneReferenceCount > 0
            ? MessageType.Info
            : MessageType.Warning;
    }

    /// <summary>
    /// 책임 : 재귀 씬 의존성 검색 결과를 프로필별로 캐시해 EditorWindow의 반복 OnGUI가 AssetDatabase 전체 검색을 재실행하지 않게 한다.
    /// </summary>
    private int ResolveSceneReferenceCount(bool forceRefresh = false)
    {
        if (previewGenerationProfile == null)
            return 0;

        if (!forceRefresh &&
            hasCachedSceneReferenceCount &&
            cachedSceneReferenceProfile == previewGenerationProfile)
        {
            return cachedSceneReferenceCount;
        }

        cachedSceneReferenceProfile = previewGenerationProfile;
        cachedSceneReferenceCount =
            DungeonGenerationProfileAssetUtility.CountEnabledBuildSceneReferences(
                previewGenerationProfile);
        hasCachedSceneReferenceCount = true;
        return cachedSceneReferenceCount;
    }

    /// <summary>
    /// 책임 : 선택 프로필 또는 씬 연결 상태가 바뀐 뒤 다음 표시 시 씬 참조 수를 한 번만 다시 계산하게 한다.
    /// </summary>
    private void InvalidateSceneReferenceCount()
    {
        cachedSceneReferenceProfile = null;
        cachedSceneReferenceCount = 0;
        hasCachedSceneReferenceCount = false;
    }

    private static void DrawGuaranteedRoomTemplates(
        DungeonGenerationProfileSO profile,
        RoomThemeLibrarySO library)
    {
        if (profile == null)
            return;

        var serializedProfile = new SerializedObject(profile);
        serializedProfile.Update();
        SerializedProperty roomsProperty =
            serializedProfile.FindProperty("guaranteedRoomTemplates");
        if (roomsProperty == null)
            return;

        EditorGUILayout.Space(3f);
        EditorGUILayout.PropertyField(
            roomsProperty,
            new GUIContent("반드시 포함할 방"),
            includeChildren: true);
        if (serializedProfile.ApplyModifiedProperties())
            EditorUtility.SetDirty(profile);

        IReadOnlyList<RoomTemplateSO> guaranteedRooms = profile.GuaranteedRoomTemplates;
        for (int roomIndex = 0; roomIndex < guaranteedRooms.Count; roomIndex++)
        {
            RoomTemplateSO room = guaranteedRooms[roomIndex];
            if (room == null)
            {
                EditorGUILayout.HelpBox(
                    $"반드시 포함할 방 {roomIndex + 1}번 항목이 비어 있습니다.",
                    MessageType.Error);
                continue;
            }

            RoomType roomType = room.LayoutData.roomType;
            if (library == null || !library.ContainsRoom(room))
            {
                EditorGUILayout.HelpBox(
                    $"'{room.name}'은 현재 테마 룸 라이브러리에 등록되어 있지 않습니다.",
                    MessageType.Error);
            }
            else if (roomType == RoomType.Start ||
                     roomType == RoomType.Boss ||
                     roomType == RoomType.Exit)
            {
                EditorGUILayout.HelpBox(
                    $"'{room.name}'의 {roomType} 역할은 반드시 포함할 확장 방으로 지정할 수 없습니다.",
                    MessageType.Error);
            }
        }

        EditorGUILayout.HelpBox(
            "목록의 방은 그래프 배치에서 정확히 한 번 사용되며 일반 랜덤 후보에서는 제외됩니다.",
            MessageType.None);
    }

    private void CreateAuthoringRoomPiece()
    {
        if (!TryPrepareWorkspaceForRoom())
            return;

        Vector2Int roomSize = new(Mathf.Max(1, newRoomSize.x), Mathf.Max(1, newRoomSize.y));

        selectedAuthoring = CreateAuthoringRoomPiece(
            newRoomId,
            newRoomType,
            roomSize,
            newDifficultyTier,
            newSelectionWeight,
            default,
            null);
        currentStep = AuthoringStep.Basic;
        RoomAuthoringWorkspace.MarkDirty();
        validationMessages.Clear();
    }

    private void LoadTemplateForEditing(bool asDuplicate)
    {
        if (templateToLoad == null || !TryPrepareWorkspaceForRoom())
            return;

        RoomLayoutData layout = templateToLoad.LayoutData;
        RoomBuildData build = templateToLoad.BuildData;
        Vector2Int roomSize = ResolveTemplateSize(layout);
        string roomId = string.IsNullOrWhiteSpace(layout.roomId)
            ? templateToLoad.name
            : layout.roomId;
        if (asDuplicate)
            roomId += "_Copy";

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Load Room Template for Editing");

        selectedAuthoring = CreateAuthoringRoomPiece(
            roomId,
            layout.roomType,
            roomSize,
            layout.difficultyTier,
            layout.selectionWeight,
            layout.topologyPlacement,
            asDuplicate ? null : templateToLoad);

        for (int i = 0; i < RoomTileLayerContract.OrderedLayers.Count; i++)
        {
            RoomTileLayerKind layer = RoomTileLayerContract.OrderedLayers[i];
            RestoreTiles(selectedAuthoring.GetTilemap(layer), build.GetTiles(layer));
        }
        RestoreSockets(selectedAuthoring, layout.sockets);
        RestoreObjects(selectedAuthoring, build.objectPlacements);
        RestoreTravelEndpoints(selectedAuthoring, build.travelEndpointPlacements);

        Undo.CollapseUndoOperations(undoGroup);
        validationMessages.Clear();
        currentStep = AuthoringStep.Basic;
        Selection.activeObject = selectedAuthoring.gameObject;
        if (asDuplicate)
            RoomAuthoringWorkspace.MarkDirty();
        else
            RoomAuthoringWorkspace.MarkSaved();

        SceneView.RepaintAll();
        Repaint();
    }

    private bool TryPrepareWorkspaceForRoom()
    {
        if (!RoomAuthoringWorkspace.Open().IsValid())
            return false;

        RoomPieceAuthoring existingAuthoring = RoomAuthoringWorkspace.FindAuthoring();
        CorridorDecorationModuleAuthoring existingCorridor =
            RoomAuthoringWorkspace.FindCorridorAuthoring();
        if (existingAuthoring == null && existingCorridor == null)
            return true;

        if (RoomAuthoringWorkspace.HasUnsavedChanges &&
            !EditorUtility.DisplayDialog(
                "편집 중인 던전 조각 교체",
                existingAuthoring != null
                    ? $"'{existingAuthoring.RoomId}'에 저장되지 않은 변경 내용이 있습니다. 버리고 다른 방을 열까요?"
                    : $"'{existingCorridor.ModuleId}' 복도 장식에 저장되지 않은 변경 내용이 있습니다. 버리고 방을 열까요?",
                "변경 내용 버리기",
                "계속 편집"))
        {
            return false;
        }

        if (existingAuthoring != null && selectedAuthoring == existingAuthoring)
            selectedAuthoring = null;

        RoomAuthoringDungeonPreview.Clear();
        if (existingAuthoring != null)
            UnityEngine.Object.DestroyImmediate(existingAuthoring.gameObject);
        if (existingCorridor != null)
            UnityEngine.Object.DestroyImmediate(existingCorridor.gameObject);
        RoomAuthoringWorkspace.MarkSaved();
        return true;
    }

    private static RoomPieceAuthoring CreateAuthoringRoomPiece(
        string roomId,
        RoomType roomType,
        Vector2Int roomSize,
        int difficultyTier,
        float selectionWeight,
        RoomTopologyPlacementData topologyPlacement,
        RoomTemplateSO sourceTemplate)
    {
        string rootName = sourceTemplate != null ? $"{roomId}_Editing" : roomId;

        GameObject root = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Room Piece");
        RoomAuthoringWorkspace.MoveToWorkspace(root);

        RoomPieceAuthoring authoring = root.AddComponent<RoomPieceAuthoring>();
        SerializedObject serializedAuthoring = new(authoring);
        serializedAuthoring.FindProperty("roomId").stringValue = roomId;
        serializedAuthoring.FindProperty("roomType").enumValueIndex = (int)roomType;
        serializedAuthoring.FindProperty("size").vector2IntValue = roomSize;
        serializedAuthoring.FindProperty("difficultyTier").intValue = Mathf.Max(0, difficultyTier);
        serializedAuthoring.FindProperty("selectionWeight").floatValue = Mathf.Max(0f, selectionWeight);
        SerializedProperty topologyProperty = serializedAuthoring.FindProperty("topologyPlacement");
        topologyProperty.FindPropertyRelative("mode").enumValueIndex = (int)topologyPlacement.mode;
        topologyProperty.FindPropertyRelative("minimumGraphDistanceFromStart").intValue =
            Mathf.Max(0, topologyPlacement.minimumGraphDistanceFromStart);
        topologyProperty.FindPropertyRelative("requireDeadEnd").boolValue =
            topologyPlacement.requireDeadEnd;
        serializedAuthoring.ApplyModifiedPropertiesWithoutUndo();

        GameObject gridObject = new GameObject("AuthoringGrid");
        Undo.RegisterCreatedObjectUndo(gridObject, "Create Room Piece Grid");
        gridObject.transform.SetParent(root.transform, false);
        Grid grid = gridObject.AddComponent<Grid>();

        Tilemap underFloor = CreateTilemapLayer(gridObject.transform, RoomTileLayerKind.UnderFloor);
        Tilemap floor = CreateTilemapLayer(gridObject.transform, RoomTileLayerKind.Floor);
        Tilemap floorDetail = CreateTilemapLayer(gridObject.transform, RoomTileLayerKind.FloorDetail);
        Tilemap groundDecoration = CreateTilemapLayer(
            gridObject.transform,
            RoomTileLayerKind.GroundDecoration);
        Tilemap wall = CreateTilemapLayer(gridObject.transform, RoomTileLayerKind.Wall);
        Tilemap wallDetail = CreateTilemapLayer(gridObject.transform, RoomTileLayerKind.WallDetail);
        Tilemap foreground = CreateTilemapLayer(gridObject.transform, RoomTileLayerKind.Foreground);
        Tilemap overlayFx = CreateTilemapLayer(gridObject.transform, RoomTileLayerKind.OverlayFX);

        authoring.EditorAssignTilemaps(
            grid,
            underFloor,
            floor,
            floorDetail,
            groundDecoration,
            wall,
            wallDetail,
            foreground,
            overlayFx);
        authoring.EditorAssignSourceTemplate(sourceTemplate);
        EditorUtility.SetDirty(authoring);

        Selection.activeObject = root;
        return authoring;
    }

    private void AddConnectionSocket(RoomSocketDirection direction)
    {
        selectedAuthoring = ResolveSelectedAuthoring();
        if (selectedAuthoring == null || selectedAuthoring.Grid == null)
            return;

        RoomSocketAuthoring[] existingSockets = GetSockets(selectedAuthoring);
        int boundaryLength = direction == RoomSocketDirection.Up ||
                             direction == RoomSocketDirection.Down
            ? selectedAuthoring.Size.x
            : selectedAuthoring.Size.y;
        int preferredStart = Mathf.Max(
            0,
            (boundaryLength - RoomSocketGeometry.RequiredWidth) / 2);
        if (!TryFindAvailableSocketCell(
                selectedAuthoring,
                direction,
                preferredStart,
                out Vector2Int localCell))
        {
            EditorUtility.DisplayDialog(
                "소켓 배치 실패",
                $"{direction} 경계에 겹치지 않는 {RoomSocketGeometry.RequiredWidth}칸 소켓을 배치할 공간이 없습니다.",
                "확인");
            return;
        }

        string socketId = CreateNextSocketId(existingSockets);
        RoomSocketAuthoring socket = CreateConnectionSocket(
            selectedAuthoring,
            socketId,
            direction,
            localCell);

        Selection.activeObject = socket.gameObject;
        EditorUtility.SetDirty(socket);
        RoomAuthoringWorkspace.MarkDirty();
        SceneView.RepaintAll();
        Repaint();
    }

    private static string CreateNextSocketId(IReadOnlyList<RoomSocketAuthoring> sockets)
    {
        HashSet<string> existingIds = new(System.StringComparer.Ordinal);
        for (int socketIndex = 0; socketIndex < sockets.Count; socketIndex++)
        {
            if (sockets[socketIndex] != null)
                existingIds.Add(sockets[socketIndex].SocketId);
        }

        for (int sequence = 1; sequence < int.MaxValue; sequence++)
        {
            string candidate = $"Socket_{sequence:00}";
            if (!existingIds.Contains(candidate))
                return candidate;
        }

        return $"Socket_{System.Guid.NewGuid():N}";
    }

    private static bool TryFindAvailableSocketCell(
        RoomPieceAuthoring authoring,
        RoomSocketDirection direction,
        int preferredStart,
        out Vector2Int localCell)
    {
        localCell = default;
        int boundaryLength = direction == RoomSocketDirection.Up ||
                             direction == RoomSocketDirection.Down
            ? authoring.Size.x
            : authoring.Size.y;
        int maxStart = boundaryLength - RoomSocketGeometry.RequiredWidth;
        if (maxStart < 0)
            return false;

        HashSet<Vector2Int> occupiedCells = CollectOccupiedSocketCells(authoring);
        int clampedPreferredStart = Mathf.Clamp(preferredStart, 0, maxStart);
        for (int distance = 0; distance <= maxStart; distance++)
        {
            int forwardCandidate = clampedPreferredStart + distance;
            if (forwardCandidate <= maxStart &&
                TryUseSocketStart(
                    authoring.Size,
                    direction,
                    forwardCandidate,
                    occupiedCells,
                    out localCell))
            {
                return true;
            }

            int backwardCandidate = clampedPreferredStart - distance;
            if (distance > 0 &&
                backwardCandidate >= 0 &&
                TryUseSocketStart(
                    authoring.Size,
                    direction,
                    backwardCandidate,
                    occupiedCells,
                    out localCell))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryUseSocketStart(
        Vector2Int size,
        RoomSocketDirection direction,
        int tangentStart,
        HashSet<Vector2Int> occupiedCells,
        out Vector2Int localCell)
    {
        localCell = direction switch
        {
            RoomSocketDirection.Up => new Vector2Int(tangentStart, size.y - 1),
            RoomSocketDirection.Right => new Vector2Int(size.x - 1, tangentStart),
            RoomSocketDirection.Down => new Vector2Int(tangentStart, 0),
            RoomSocketDirection.Left => new Vector2Int(0, tangentStart),
            _ => default
        };

        RoomSocketData candidate = new()
        {
            localCell = localCell,
            direction = direction,
            width = RoomSocketGeometry.RequiredWidth
        };
        if (!RoomSocketGeometry.IsValid(candidate, new RectInt(Vector2Int.zero, size)))
            return false;

        for (int cellIndex = 0; cellIndex < RoomSocketGeometry.RequiredWidth; cellIndex++)
        {
            if (occupiedCells.Contains(RoomSocketGeometry.GetLocalCell(candidate, cellIndex)))
                return false;
        }

        return true;
    }

    private static HashSet<Vector2Int> CollectOccupiedSocketCells(RoomPieceAuthoring authoring)
    {
        HashSet<Vector2Int> occupiedCells = new();
        RoomSocketAuthoring[] sockets = GetSockets(authoring);
        for (int socketIndex = 0; socketIndex < sockets.Length; socketIndex++)
        {
            RoomSocketAuthoring socket = sockets[socketIndex];
            if (!socket.TryGetLocalCell(out Vector2Int localCell))
                continue;

            RoomSocketData data = new()
            {
                localCell = localCell,
                direction = socket.Direction,
                width = socket.Width
            };
            for (int cellIndex = 0; cellIndex < socket.Width; cellIndex++)
                occupiedCells.Add(RoomSocketGeometry.GetLocalCell(data, cellIndex));
        }

        return occupiedCells;
    }

    private static RoomSocketAuthoring CreateConnectionSocket(
        RoomPieceAuthoring authoring,
        string socketId,
        RoomSocketDirection direction,
        Vector2Int localCell)
    {
        string objectName = string.IsNullOrWhiteSpace(socketId) ? "Socket" : socketId;
        GameObject socketObject = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(socketObject, "Add Room Connection Socket");
        Undo.SetTransformParent(
            socketObject.transform,
            authoring.Grid.transform,
            "Parent Room Connection Socket");

        RoomSocketAuthoring socket = socketObject.AddComponent<RoomSocketAuthoring>();
        SerializedObject serializedSocket = new(socket);
        serializedSocket.FindProperty("socketId").stringValue = socketId ?? string.Empty;
        serializedSocket.FindProperty("direction").enumValueIndex = (int)direction;
        serializedSocket.ApplyModifiedPropertiesWithoutUndo();
        socket.EditorSetLocalCell(localCell);
        EditorUtility.SetDirty(socket);
        return socket;
    }

    private static Tilemap CreateTilemapLayer(Transform parent, RoomTileLayerKind layer)
    {
        string layerName = RoomTileLayerContract.GetLayerName(layer);
        GameObject layerObject = new GameObject(layerName);
        Undo.RegisterCreatedObjectUndo(layerObject, $"Create {layerName} Tilemap");
        layerObject.transform.SetParent(parent, false);

        Tilemap tilemap = layerObject.AddComponent<Tilemap>();
        TilemapRenderer renderer = layerObject.AddComponent<TilemapRenderer>();
        renderer.sortingLayerName = RoomTileLayerContract.GetSortingLayerName(layer);
        renderer.sortingOrder = RoomTileLayerContract.GetSortingOrder(layer);
        return tilemap;
    }

    private static Vector2Int ResolveTemplateSize(RoomLayoutData layout)
    {
        if (layout.size.x > 0 && layout.size.y > 0)
            return layout.size;

        if (layout.localBounds.width > 0 && layout.localBounds.height > 0)
            return layout.localBounds.size;

        return Vector2Int.one;
    }

    private static void RestoreTiles(Tilemap tilemap, List<RoomTileData> tileData)
    {
        if (tilemap == null || tileData == null)
            return;

        for (int i = 0; i < tileData.Count; i++)
        {
            RoomTileData entry = tileData[i];
            if (entry.tile == null)
                continue;

            tilemap.SetTile(
                new Vector3Int(entry.localCell.x, entry.localCell.y, 0),
                entry.tile);
        }

        tilemap.CompressBounds();
        EditorUtility.SetDirty(tilemap);
    }

    private static void RestoreSockets(RoomPieceAuthoring authoring, List<RoomSocketData> socketData)
    {
        if (authoring == null || authoring.Grid == null || socketData == null)
            return;

        for (int i = 0; i < socketData.Count; i++)
        {
            RoomSocketData entry = socketData[i];
            CreateConnectionSocket(
                authoring,
                entry.socketId,
                entry.direction,
                entry.localCell);
        }
    }

    private void AddRoomObject()
    {
        selectedAuthoring = ResolveSelectedAuthoring();
        if (selectedAuthoring == null ||
            selectedAuthoring.Grid == null)
        {
            return;
        }

        bool placingMonster = objectKindToPlace == RoomObjectKind.Monster;
        bool placingCommonRoleMonster = placingMonster &&
            monsterSourceModeToPlace == MonsterPlacementSourceMode.CommonRole;
        StageMonsterSetSO roleSet = placingCommonRoleMonster
            ? LoadRoleStageMonsterSet(monsterRoleToPlace)
            : null;
        if (placingCommonRoleMonster && roleSet == null)
            return;
        if (!placingCommonRoleMonster &&
            !IsPrefabCompatibleWithKind(objectPrefabToPlace, objectKindToPlace))
        {
            return;
        }

        RoomObjectAuthoring[] existingObjects = GetRoomObjects(selectedAuthoring);
        Vector2Int defaultCell = ResolveDefaultObjectCell(selectedAuthoring);
        RoomObjectPlacementData placement = new()
        {
            placementId = placingCommonRoleMonster
                ? $"{monsterRoleToPlace}_{existingObjects.Length + 1:00}"
                : placingMonster
                    ? $"StageMonster_{existingObjects.Length + 1:00}"
                    : $"Object_{existingObjects.Length + 1:00}",
            kind = objectKindToPlace,
            prefab = placingCommonRoleMonster ? null : objectPrefabToPlace,
            monsterSpawnRole = monsterRoleToPlace,
            monsterStageSet = roleSet,
            localCell = defaultCell,
            localOffset = Vector2.zero,
            localRotationDegrees = 0f,
            localScale = placingMonster ? Vector3.one : objectPrefabToPlace.transform.localScale
        };

        RoomObjectAuthoring roomObject = CreateRoomObjectAuthoring(selectedAuthoring, placement);
        Selection.activeObject = roomObject.gameObject;
        EditorUtility.SetDirty(roomObject);
        SceneView.RepaintAll();
        Repaint();
    }

    private void AddTravelEndpoint()
    {
        selectedAuthoring = ResolveSelectedAuthoring();
        if (selectedAuthoring == null || selectedAuthoring.Grid == null)
            return;

        RoomTravelEndpointAuthoring[] existing = GetTravelEndpoints(selectedAuthoring);
        Vector2Int defaultCell = ResolveDefaultObjectCell(selectedAuthoring);
        RoomTravelEndpointPlacementData placement = new()
        {
            slotId = CreateNextTravelSlotId(existing),
            kind = travelEndpointKindToPlace,
            mediumPrefab = travelMediumPrefabToPlace,
            localCell = defaultCell,
            localOffset = Vector2.zero,
            localRotationDegrees = 0f,
            localScale = travelMediumPrefabToPlace != null
                ? travelMediumPrefabToPlace.transform.localScale
                : Vector3.one,
            triggerSize = travelEndpointKindToPlace == RoomTravelEndpointKind.Trigger
                ? RoomTravelEndpointGeometry.SanitizeTriggerSize(travelTriggerSizeToPlace)
                : Vector2.one,
            useSeparateArrivalPoint = false,
            arrivalLocalCell = defaultCell,
            arrivalLocalOffset = Vector2.zero
        };

        RoomTravelEndpointAuthoring endpoint = CreateTravelEndpointAuthoring(
            selectedAuthoring,
            placement);
        Selection.activeObject = endpoint.gameObject;
        RoomAuthoringWorkspace.MarkDirty();
        SceneView.RepaintAll();
        Repaint();
    }

    private static string CreateNextTravelSlotId(
        IReadOnlyList<RoomTravelEndpointAuthoring> endpoints)
    {
        var existingIds = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < endpoints.Count; i++)
        {
            if (endpoints[i] != null)
                existingIds.Add(endpoints[i].SlotId);
        }

        for (int sequence = 1; sequence < int.MaxValue; sequence++)
        {
            string candidate = $"Travel_{sequence:00}";
            if (!existingIds.Contains(candidate))
                return candidate;
        }

        return $"Travel_{System.Guid.NewGuid():N}";
    }

    private static Vector2Int ResolveDefaultObjectCell(RoomPieceAuthoring authoring)
    {
        Vector2Int size = authoring.Size;
        Vector2Int center = new(
            Mathf.Clamp(size.x / 2, 0, Mathf.Max(0, size.x - 1)),
            Mathf.Clamp(size.y / 2, 0, Mathf.Max(0, size.y - 1)));
        Tilemap floor = authoring.FloorTilemap;
        if (floor == null || floor.HasTile(new Vector3Int(center.x, center.y, 0)))
            return center;

        Vector2Int bestCell = center;
        int bestDistance = int.MaxValue;
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                if (!floor.HasTile(new Vector3Int(x, y, 0)))
                    continue;

                int distance = Mathf.Abs(x - center.x) + Mathf.Abs(y - center.y);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestCell = new Vector2Int(x, y);
            }
        }

        return bestCell;
    }

    private static RoomObjectAuthoring CreateRoomObjectAuthoring(
        RoomPieceAuthoring authoring,
        RoomObjectPlacementData placement)
    {
        string placementId = string.IsNullOrWhiteSpace(placement.placementId)
            ? "RoomObject"
            : placement.placementId;
        GameObject instance = null;
        if (placement.prefab != null)
        {
            instance = PrefabUtility.InstantiatePrefab(
                placement.prefab,
                authoring.Grid.transform) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(
                    placement.prefab,
                    authoring.Grid.transform);
            }
        }

        if (instance == null)
        {
            instance = new GameObject(placementId);
            Undo.RegisterCreatedObjectUndo(instance, "Add Room Object");
            Undo.SetTransformParent(
                instance.transform,
                authoring.Grid.transform,
                "Parent Room Object");
        }
        else
        {
            Undo.RegisterCreatedObjectUndo(instance, "Add Room Object Prefab");
        }

        instance.name = placement.prefab != null
            ? $"{placementId}_{placement.prefab.name}"
            : placementId;

        RoomObjectAuthoring marker = instance.GetComponent<RoomObjectAuthoring>();
        if (marker == null)
            marker = Undo.AddComponent<RoomObjectAuthoring>(instance);

        marker.EditorConfigure(
            placement.placementId,
            placement.kind,
            placement.prefab,
            placement.monsterSpawnRole,
            placement.monsterStageSet);
        marker.EditorSetPlacement(placement);
        EditorUtility.SetDirty(marker);
        EditorUtility.SetDirty(instance.transform);
        return marker;
    }

    private static RoomTravelEndpointAuthoring CreateTravelEndpointAuthoring(
        RoomPieceAuthoring authoring,
        RoomTravelEndpointPlacementData placement)
    {
        string slotId = string.IsNullOrWhiteSpace(placement.slotId)
            ? "TravelEndpoint"
            : placement.slotId;
        GameObject instance = null;
        if (placement.mediumPrefab != null)
        {
            instance = PrefabUtility.InstantiatePrefab(
                placement.mediumPrefab,
                authoring.Grid.transform) as GameObject;
            instance ??= UnityEngine.Object.Instantiate(
                placement.mediumPrefab,
                authoring.Grid.transform);
        }

        if (instance == null)
        {
            instance = new GameObject(slotId);
            Undo.RegisterCreatedObjectUndo(instance, "Add Room Travel Endpoint");
            Undo.SetTransformParent(
                instance.transform,
                authoring.Grid.transform,
                "Parent Room Travel Endpoint");
        }
        else
        {
            Undo.RegisterCreatedObjectUndo(instance, "Add Room Travel Endpoint Prefab");
        }

        instance.name = placement.mediumPrefab != null
            ? $"{slotId}_{placement.mediumPrefab.name}"
            : slotId;
        RoomTravelEndpointAuthoring marker =
            instance.GetComponent<RoomTravelEndpointAuthoring>();
        if (marker == null)
            marker = Undo.AddComponent<RoomTravelEndpointAuthoring>(instance);

        marker.EditorConfigure(placement.slotId, placement.kind, placement.mediumPrefab);
        marker.EditorSetPlacement(placement);
        EditorUtility.SetDirty(marker);
        EditorUtility.SetDirty(instance.transform);
        return marker;
    }

    private static void RestoreObjects(
        RoomPieceAuthoring authoring,
        List<RoomObjectPlacementData> objectPlacements)
    {
        if (authoring == null || authoring.Grid == null || objectPlacements == null)
            return;

        for (int i = 0; i < objectPlacements.Count; i++)
            CreateRoomObjectAuthoring(authoring, objectPlacements[i]);
    }

    private static void RestoreTravelEndpoints(
        RoomPieceAuthoring authoring,
        List<RoomTravelEndpointPlacementData> placements)
    {
        if (authoring == null || authoring.Grid == null || placements == null)
            return;

        for (int i = 0; i < placements.Count; i++)
            CreateTravelEndpointAuthoring(authoring, placements[i]);
    }

    private bool ValidateSelectedRoomPiece(bool showDialog)
    {
        validationMessages.Clear();
        selectedAuthoring = ResolveSelectedAuthoring();

        if (selectedAuthoring == null)
        {
            validationMessages.Add("RoomPieceAuthoring 선택이 필요합니다.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(selectedAuthoring.RoomId))
            validationMessages.Add("Room Id가 비어 있습니다.");
        else
            ValidateRoomIdAgainstLibrary(selectedAuthoring, validationMessages);

        if (selectedAuthoring.Grid == null)
            validationMessages.Add("Grid 참조가 비어 있습니다.");

        Dictionary<Tilemap, RoomTileLayerKind> assignedTilemapLayers = new();
        for (int layerIndex = 0;
             layerIndex < RoomTileLayerContract.OrderedLayers.Count;
             layerIndex++)
        {
            RoomTileLayerKind layer = RoomTileLayerContract.OrderedLayers[layerIndex];
            Tilemap tilemap = selectedAuthoring.GetTilemap(layer);
            if (tilemap == null)
            {
                validationMessages.Add(
                    $"{RoomTileLayerContract.GetLayerName(layer)} Tilemap 참조가 비어 있습니다.");
            }
            else if (assignedTilemapLayers.TryGetValue(
                         tilemap,
                         out RoomTileLayerKind existingLayer))
            {
                validationMessages.Add(
                    $"{existingLayer}와 {layer}가 같은 Tilemap을 공유하고 있습니다.");
            }
            else
            {
                assignedTilemapLayers.Add(tilemap, layer);
            }
        }

        if (selectedAuthoring.Size.x <= 0 || selectedAuthoring.Size.y <= 0)
            validationMessages.Add("Size는 1 이상의 값이어야 합니다.");

        int floorCount = CountTiles(
            selectedAuthoring.FloorTilemap,
            selectedAuthoring.Size,
            out _);
        int wallCount = CountTiles(
            selectedAuthoring.WallTilemap,
            selectedAuthoring.Size,
            out _);

        if (floorCount + wallCount <= 0)
            validationMessages.Add("방 bounds 안에 Floor 또는 Wall 타일이 하나 이상 필요합니다.");

        ValidateTilesRestOnBaseLayer(
            selectedAuthoring.FloorDetailTilemap,
            selectedAuthoring.FloorTilemap,
            selectedAuthoring.Size,
            "FloorDetail",
            "Floor",
            validationMessages);
        ValidateTilesRestOnBaseLayer(
            selectedAuthoring.GroundDecorationTilemap,
            selectedAuthoring.FloorTilemap,
            selectedAuthoring.Size,
            "GroundDecoration",
            "Floor",
            validationMessages);
        ValidateTilesRestOnBaseLayer(
            selectedAuthoring.WallDetailTilemap,
            selectedAuthoring.WallTilemap,
            selectedAuthoring.Size,
            "WallDetail",
            "Wall",
            validationMessages);

        ValidateSockets(selectedAuthoring, validationMessages);
        ValidateObjectPlacements(selectedAuthoring, validationMessages);
        ValidateTravelEndpointPlacements(selectedAuthoring, validationMessages);

        bool valid = validationMessages.Count == 0;

        for (int layerIndex = 0;
             layerIndex < RoomTileLayerContract.OrderedLayers.Count;
             layerIndex++)
        {
            RoomTileLayerKind layer = RoomTileLayerContract.OrderedLayers[layerIndex];
            CountTiles(
                selectedAuthoring.GetTilemap(layer),
                selectedAuthoring.Size,
                out int outsideCount);
            if (outsideCount > 0)
            {
                validationMessages.Add(
                    $"{RoomTileLayerContract.GetLayerName(layer)} Tilemap에 방 bounds 밖 타일 " +
                    $"{outsideCount}개가 있습니다. bake에서는 제외됩니다.");
            }
        }

        if (showDialog)
        {
            string message = validationMessages.Count == 0
                ? "검증 통과"
                : string.Join("\n", validationMessages);
            EditorUtility.DisplayDialog("Room Piece Validation", message, "OK");
        }

        Repaint();
        return valid;
    }

    private static void ValidateTilesRestOnBaseLayer(
        Tilemap detailTilemap,
        Tilemap baseTilemap,
        Vector2Int roomSize,
        string detailLayerName,
        string baseLayerName,
        List<string> messages)
    {
        if (detailTilemap == null || baseTilemap == null)
            return;

        BoundsInt bounds = detailTilemap.cellBounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cell = new(x, y, 0);
                if (!IsInsideRoomBounds(cell, roomSize) || !detailTilemap.HasTile(cell))
                    continue;

                if (!baseTilemap.HasTile(cell))
                {
                    messages.Add(
                        $"{detailLayerName} 셀 ({x}, {y}) 아래에 {baseLayerName} 타일이 필요합니다.");
                }
            }
        }
    }

    private void ValidateRoomIdAgainstLibrary(
        RoomPieceAuthoring authoring,
        List<string> messages)
    {
        if (selectedLibrary == null)
            return;

        IReadOnlyList<RoomTemplateSO> rooms = selectedLibrary.Rooms;
        for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
        {
            RoomTemplateSO room = rooms[roomIndex];
            if (room == null || room == authoring.SourceTemplate)
                continue;

            if (string.Equals(
                    room.LayoutData.roomId,
                    authoring.RoomId,
                    System.StringComparison.Ordinal))
            {
                messages.Add(
                    $"선택한 라이브러리에 Room Id '{authoring.RoomId}'를 사용하는 다른 방이 있습니다.");
                return;
            }
        }
    }

    private void SaveAsNewRoomTemplate()
    {
        if (!TryCollectSelectedRoomData(out RoomLayoutData layout, out RoomBuildData build))
            return;

        string assetPath = EditorUtility.SaveFilePanelInProject(
            "새 방 템플릿 저장",
            selectedAuthoring.RoomId,
            "asset",
            "저장 위치와 에셋 이름을 확인하세요.",
            ResolveSuggestedOutputFolder());
        if (string.IsNullOrWhiteSpace(assetPath))
            return;
        if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
        {
            EditorUtility.DisplayDialog(
                "새 방 저장 취소",
                "선택한 경로에 이미 에셋이 있습니다. 기존 방을 덮어쓰지 않도록 다른 이름이나 위치를 선택하세요.",
                "확인");
            return;
        }

        RoomTemplateSO template = CreateInstance<RoomTemplateSO>();
        template.EditorSetData(layout, build);
        AssetDatabase.CreateAsset(template, assetPath);
        bool addedToLibrary = RegisterTemplateWithSelectedLibrary(template);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Undo.RecordObject(selectedAuthoring, "Link Room Authoring Source Template");
        selectedAuthoring.EditorAssignSourceTemplate(template);
        EditorUtility.SetDirty(selectedAuthoring);
        templateToLoad = template;
        Selection.activeObject = template;
        RoomAuthoringWorkspace.MarkSaved();
        EditorUtility.DisplayDialog(
            "방 저장 완료",
            $"RoomTemplateSO 생성 완료:\n{assetPath}" +
            (addedToLibrary ? $"\n\n{selectedLibrary.ThemeId} 라이브러리에 등록했습니다." : string.Empty),
            "확인");
    }

    private void ApplyChangesToLoadedTemplate()
    {
        selectedAuthoring = ResolveSelectedAuthoring();
        RoomTemplateSO targetTemplate = selectedAuthoring != null
            ? selectedAuthoring.SourceTemplate
            : null;

        if (targetTemplate == null)
        {
            EditorUtility.DisplayDialog("Apply Failed", "적용할 원본 RoomTemplateSO가 없습니다.", "OK");
            return;
        }

        if (!TryCollectSelectedRoomData(out RoomLayoutData layout, out RoomBuildData build))
            return;

        string assetPath = AssetDatabase.GetAssetPath(targetTemplate);
        bool confirmed = EditorUtility.DisplayDialog(
            "Apply Room Template Changes",
            $"다음 원본 에셋을 현재 authoring 데이터로 갱신합니다.\n\n{assetPath}",
            "Apply",
            "Cancel");
        if (!confirmed)
            return;

        Undo.RecordObject(targetTemplate, "Apply Room Template Changes");
        targetTemplate.EditorSetData(layout, build);
        EditorUtility.SetDirty(targetTemplate);
        bool addedToLibrary = RegisterTemplateWithSelectedLibrary(targetTemplate);
        AssetDatabase.SaveAssets();

        templateToLoad = targetTemplate;
        Selection.activeObject = targetTemplate;
        RoomAuthoringWorkspace.MarkSaved();
        EditorUtility.DisplayDialog(
            "방 갱신 완료",
            $"RoomTemplateSO 갱신 완료:\n{assetPath}" +
            (addedToLibrary ? $"\n\n{selectedLibrary.ThemeId} 라이브러리에 등록했습니다." : string.Empty),
            "확인");
    }

    private string ResolveSuggestedOutputFolder()
    {
        RoomTemplateSO source = selectedAuthoring != null
            ? selectedAuthoring.SourceTemplate
            : null;
        if (source == null)
            source = templateToLoad;

        string sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
        if (!string.IsNullOrWhiteSpace(sourcePath))
            return Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? "Assets";

        if (selectedLibrary != null)
        {
            IReadOnlyList<RoomTemplateSO> rooms = selectedLibrary.Rooms;
            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                string roomPath = rooms[roomIndex] != null
                    ? AssetDatabase.GetAssetPath(rooms[roomIndex])
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(roomPath))
                    return Path.GetDirectoryName(roomPath)?.Replace('\\', '/') ?? "Assets";
            }
        }

        return "Assets";
    }

    private bool RegisterTemplateWithSelectedLibrary(RoomTemplateSO template)
    {
        if (!registerWithSelectedLibrary || selectedLibrary == null || template == null)
            return false;

        Undo.RecordObject(selectedLibrary, "Register Room Template With Theme Library");
        if (!selectedLibrary.EditorAddRoom(template))
            return false;

        EditorUtility.SetDirty(selectedLibrary);
        return true;
    }

    private bool TryCollectSelectedRoomData(
        out RoomLayoutData layout,
        out RoomBuildData build,
        bool showFailureDialog = true)
    {
        layout = default;
        build = default;

        if (!ValidateSelectedRoomPiece(showDialog: false))
        {
            if (showFailureDialog)
            {
                EditorUtility.DisplayDialog(
                    "Save Failed",
                    string.Join("\n", validationMessages),
                    "OK");
            }
            return false;
        }

        layout = new RoomLayoutData
        {
            roomId = selectedAuthoring.RoomId,
            roomType = selectedAuthoring.RoomType,
            size = selectedAuthoring.Size,
            localBounds = new RectInt(Vector2Int.zero, selectedAuthoring.Size),
            sockets = CollectSockets(selectedAuthoring),
            difficultyTier = selectedAuthoring.DifficultyTier,
            selectionWeight = selectedAuthoring.SelectionWeight,
            topologyPlacement = selectedAuthoring.TopologyPlacement
        };

        build = new RoomBuildData
        {
            underFloorTiles = CollectTiles(
                selectedAuthoring.UnderFloorTilemap,
                selectedAuthoring.Size),
            floorTiles = CollectTiles(selectedAuthoring.FloorTilemap, selectedAuthoring.Size),
            floorDetailTiles = CollectTiles(
                selectedAuthoring.FloorDetailTilemap,
                selectedAuthoring.Size),
            groundDecorationTiles = CollectTiles(
                selectedAuthoring.GroundDecorationTilemap,
                selectedAuthoring.Size),
            wallTiles = CollectTiles(selectedAuthoring.WallTilemap, selectedAuthoring.Size),
            wallDetailTiles = CollectTiles(
                selectedAuthoring.WallDetailTilemap,
                selectedAuthoring.Size),
            foregroundTiles = CollectTiles(
                selectedAuthoring.ForegroundTilemap,
                selectedAuthoring.Size),
            overlayFxTiles = CollectTiles(
                selectedAuthoring.OverlayFxTilemap,
                selectedAuthoring.Size),
            objectPlacements = CollectObjectPlacements(selectedAuthoring),
            travelEndpointPlacements = CollectTravelEndpointPlacements(selectedAuthoring)
        };
        return true;
    }

    private static void ValidateSockets(RoomPieceAuthoring authoring, List<string> messages)
    {
        RoomSocketAuthoring[] sockets = GetSockets(authoring);
        if (sockets.Length == 0)
        {
            messages.Add("연결 소켓이 하나 이상 필요합니다.");
            return;
        }

        HashSet<string> socketIds = new();
        HashSet<Vector2Int> occupiedSocketCells = new();

        for (int i = 0; i < sockets.Length; i++)
        {
            RoomSocketAuthoring socket = sockets[i];
            string displayName = string.IsNullOrWhiteSpace(socket.SocketId)
                ? socket.gameObject.name
                : socket.SocketId;

            if (string.IsNullOrWhiteSpace(socket.SocketId))
                messages.Add($"{socket.gameObject.name}: Socket Id가 비어 있습니다.");
            else if (!socketIds.Add(socket.SocketId))
                messages.Add($"Socket Id '{socket.SocketId}'가 중복됩니다.");

            if (!socket.TryGetLocalCell(out Vector2Int localCell))
            {
                messages.Add($"{displayName}: Grid 셀 좌표를 계산할 수 없습니다.");
                continue;
            }

            RoomSocketData socketData = new()
            {
                socketId = socket.SocketId,
                localCell = localCell,
                direction = socket.Direction,
                width = socket.Width
            };
            RectInt roomBounds = new(Vector2Int.zero, authoring.Size);
            if (!RoomSocketGeometry.IsValid(socketData, roomBounds))
            {
                messages.Add(
                    $"{displayName}: 시작 셀 {localCell}에서 {socket.Direction} 경계를 따라가는 " +
                    $"{socket.Width}칸 소켓이 방 bounds 안의 올바른 경계에 있어야 합니다.");
                continue;
            }

            for (int cellIndex = 0; cellIndex < socket.Width; cellIndex++)
            {
                Vector2Int socketCell = RoomSocketGeometry.GetLocalCell(socketData, cellIndex);
                if (!occupiedSocketCells.Add(socketCell))
                {
                    messages.Add(
                        $"{displayName}: 소켓 셀 {socketCell}이 다른 논리 소켓과 겹칩니다.");
                }

                Vector3Int tileCell = new(socketCell.x, socketCell.y, 0);
                if (authoring.FloorTilemap != null && !authoring.FloorTilemap.HasTile(tileCell))
                {
                    messages.Add(
                        $"{displayName}: 연결 후 통행할 수 있도록 소켓 셀 {socketCell}에 Floor 타일이 필요합니다.");
                }

                if (authoring.WallTilemap != null && !authoring.WallTilemap.HasTile(tileCell))
                {
                    messages.Add(
                        $"{displayName}: 연결되지 않았을 때 출구를 막도록 소켓 셀 {socketCell}에 Wall 타일이 필요합니다.");
                }
            }
        }
    }

    private static void ValidateObjectPlacements(
        RoomPieceAuthoring authoring,
        List<string> messages)
    {
        RoomObjectAuthoring[] roomObjects = GetRoomObjects(authoring);
        Dictionary<string, RoomObjectAuthoring> objectsByPlacementId =
            new(System.StringComparer.Ordinal);
        for (int i = 0; i < roomObjects.Length; i++)
        {
            RoomObjectAuthoring roomObject = roomObjects[i];
            if (string.IsNullOrWhiteSpace(roomObject.PlacementId))
                messages.Add($"{roomObject.gameObject.name}: Placement Id가 비어 있습니다.");
            else if (!objectsByPlacementId.TryAdd(roomObject.PlacementId, roomObject))
                messages.Add($"Object Placement Id '{roomObject.PlacementId}'가 중복됩니다.");
        }

        for (int i = 0; i < roomObjects.Length; i++)
        {
            RoomObjectAuthoring roomObject = roomObjects[i];
            string displayName = string.IsNullOrWhiteSpace(roomObject.PlacementId)
                ? roomObject.gameObject.name
                : roomObject.PlacementId;

            if (roomObject.Kind == RoomObjectKind.Monster)
            {
                if (roomObject.MonsterStageSet != null && roomObject.Prefab != null)
                {
                    messages.Add(
                        $"{displayName}: 공통 역할 StageMonsterSet과 스테이지 고정 프리팹을 동시에 사용할 수 없습니다.");
                }
                else if (roomObject.MonsterStageSet == null && roomObject.Prefab == null)
                {
                    messages.Add(
                        $"{displayName}: 공통 역할 StageMonsterSet 또는 스테이지 몬스터 프리팹이 필요합니다.");
                }
                else if (roomObject.MonsterStageSet == null &&
                         !IsPrefabCompatibleWithKind(roomObject.Prefab, RoomObjectKind.Monster))
                {
                    messages.Add(
                        $"{displayName}: 스테이지 몬스터 프리팹에 자식 포함 Enemy 컴포넌트가 필요합니다.");
                }
            }
            else if (roomObject.Prefab == null)
            {
                messages.Add($"{displayName}: 원본 Prefab 참조가 비어 있습니다.");
            }
            else if (!IsPrefabCompatibleWithKind(roomObject.Prefab, roomObject.Kind))
            {
                messages.Add($"{displayName}: Prefab이 {roomObject.Kind} 종류에 필요한 컴포넌트를 포함하지 않습니다.");
            }

            ValidateMonsterChestLockLink(
                roomObject,
                displayName,
                objectsByPlacementId,
                messages);

            if (!roomObject.TryGetPlacementData(out RoomObjectPlacementData placement))
            {
                messages.Add($"{displayName}: Grid 기준 배치 데이터를 계산할 수 없습니다.");
                continue;
            }

            ValidateCompositePoseOverrides(roomObject, placement, displayName, messages);

            if (!IsInsideRoomBounds(placement.localCell, authoring.Size))
            {
                messages.Add($"{displayName}: 배치 셀 {placement.localCell}이 방 bounds 밖에 있습니다.");
                continue;
            }

            Vector3Int tileCell = new(placement.localCell.x, placement.localCell.y, 0);
            if (authoring.FloorTilemap != null && !authoring.FloorTilemap.HasTile(tileCell))
                messages.Add($"{displayName}: 배치 셀 {placement.localCell}에 Floor 타일이 필요합니다.");
        }
    }

    /// <summary>
    /// 책임:
    /// 방별 복합 오브젝트 재정의의 슬롯 누락·중복·허용되지 않은 채널과 0 크기를 저장 전에 검증한다.
    /// </summary>
    private static void ValidateCompositePoseOverrides(
        RoomObjectAuthoring roomObject,
        RoomObjectPlacementData placement,
        string displayName,
        List<string> messages)
    {
        IReadOnlyList<RoomObjectChildPoseOverrideData> overrides = placement.childPoseOverrides;
        if (overrides == null || overrides.Count == 0)
            return;

        if (!roomObject.TryGetCompositePoseAuthoring(out RoomCompositePoseAuthoring composite))
        {
            messages.Add($"{displayName}: 세부 배치 데이터가 있지만 Prefab에 복합 Pose 슬롯이 없습니다.");
            return;
        }

        if (!composite.TryValidateSlots(out string slotFailureReason))
        {
            messages.Add($"{displayName}: {slotFailureReason}");
            return;
        }

        var overrideIds = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < overrides.Count; i++)
        {
            RoomObjectChildPoseOverrideData poseOverride = overrides[i];
            if (string.IsNullOrWhiteSpace(poseOverride.slotId) ||
                !overrideIds.Add(poseOverride.slotId))
            {
                messages.Add($"{displayName}: 비어 있거나 중복된 복합 Pose Override Slot Id가 있습니다.");
                continue;
            }

            if (!composite.TryGetSlot(
                    poseOverride.slotId,
                    out RoomCompositePoseSlotData slot))
            {
                messages.Add(
                    $"{displayName}: Prefab에서 Pose 슬롯 '{poseOverride.slotId}'를 찾을 수 없습니다.");
                continue;
            }

            if ((poseOverride.overridePosition && !slot.AllowPosition) ||
                (poseOverride.overrideRotation && !slot.AllowRotation) ||
                (poseOverride.overrideScale && !slot.AllowScale))
            {
                messages.Add(
                    $"{displayName}: Pose 슬롯 '{poseOverride.slotId}'이 허용하지 않는 Transform 채널을 재정의합니다.");
            }

            if (poseOverride.overrideScale &&
                (Mathf.Approximately(poseOverride.localScale.x, 0f) ||
                 Mathf.Approximately(poseOverride.localScale.y, 0f) ||
                 Mathf.Approximately(poseOverride.localScale.z, 0f)))
            {
                messages.Add(
                    $"{displayName}: Pose 슬롯 '{poseOverride.slotId}'의 Local Scale은 0이 될 수 없습니다.");
            }
        }
    }

    private static void ValidateMonsterChestLockLink(
        RoomObjectAuthoring roomObject,
        string displayName,
        IReadOnlyDictionary<string, RoomObjectAuthoring> objectsByPlacementId,
        List<string> messages)
    {
        string targetPlacementId = roomObject.LinkedChestLockPlacementId;
        if (roomObject.Kind != RoomObjectKind.Monster ||
            string.IsNullOrWhiteSpace(targetPlacementId))
        {
            return;
        }

        if (!objectsByPlacementId.TryGetValue(
                targetPlacementId,
                out RoomObjectAuthoring targetObject))
        {
            messages.Add(
                $"{displayName}: Kill Lock 상자 Placement Id '{targetPlacementId}'를 찾을 수 없습니다.");
            return;
        }

        if (targetObject.Kind != RoomObjectKind.Chest)
        {
            messages.Add(
                $"{displayName}: Kill Lock 대상 '{targetPlacementId}'가 Chest 종류가 아닙니다.");
            return;
        }

        if (!IsKillLockChest(targetObject))
        {
            messages.Add(
                $"{displayName}: 대상 상자 '{targetPlacementId}' Prefab에 ChestMonsterKillLock이 없습니다.");
        }
    }

    private static void ValidateTravelEndpointPlacements(
        RoomPieceAuthoring authoring,
        List<string> messages)
    {
        RoomTravelEndpointAuthoring[] endpoints = GetTravelEndpoints(authoring);
        var slotIds = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < endpoints.Length; i++)
        {
            RoomTravelEndpointAuthoring endpoint = endpoints[i];
            string displayName = string.IsNullOrWhiteSpace(endpoint.SlotId)
                ? endpoint.gameObject.name
                : endpoint.SlotId;
            if (string.IsNullOrWhiteSpace(endpoint.SlotId))
                messages.Add($"{endpoint.gameObject.name}: Travel Slot Id가 비어 있습니다.");
            else if (!slotIds.Add(endpoint.SlotId))
                messages.Add($"Travel Slot Id '{endpoint.SlotId}'가 중복됩니다.");

            if (!endpoint.TryGetPlacementData(out RoomTravelEndpointPlacementData placement))
            {
                messages.Add($"{displayName}: Grid 기준 배치 데이터를 계산할 수 없습니다.");
                continue;
            }

            if (!IsInsideRoomBounds(placement.localCell, authoring.Size))
            {
                messages.Add($"{displayName}: 이동 슬롯 셀 {placement.localCell}이 방 bounds 밖에 있습니다.");
                continue;
            }

            Vector3Int tileCell = new(placement.localCell.x, placement.localCell.y, 0);
            if (authoring.FloorTilemap != null && !authoring.FloorTilemap.HasTile(tileCell))
                messages.Add($"{displayName}: 이동 슬롯 셀 {placement.localCell}에 Floor 타일이 필요합니다.");

            if (!placement.useSeparateArrivalPoint)
                continue;

            if (!IsInsideRoomBounds(placement.arrivalLocalCell, authoring.Size))
            {
                messages.Add(
                    $"{displayName}: 도착 셀 {placement.arrivalLocalCell}이 방 bounds 밖에 있습니다.");
                continue;
            }

            Vector3Int arrivalTileCell = new(
                placement.arrivalLocalCell.x,
                placement.arrivalLocalCell.y,
                0);
            if (authoring.FloorTilemap != null &&
                !authoring.FloorTilemap.HasTile(arrivalTileCell))
            {
                messages.Add(
                    $"{displayName}: 도착 셀 {placement.arrivalLocalCell}에 Floor 타일이 필요합니다.");
            }
        }
    }

    private static bool IsPrefabCompatibleWithKind(GameObject prefab, RoomObjectKind kind)
    {
        if (prefab == null)
            return false;

        return kind switch
        {
            RoomObjectKind.Monster => prefab.GetComponentInChildren<Enemy>(true) != null,
            RoomObjectKind.Chest => prefab.GetComponentInChildren<TreasureChest>(true) != null,
            RoomObjectKind.Portal => prefab.GetComponentInChildren<ScenePortal>(true) != null,
            _ => true
        };
    }

    private static List<RoomObjectPlacementData> CollectObjectPlacements(
        RoomPieceAuthoring authoring)
    {
        RoomObjectAuthoring[] roomObjects = GetRoomObjects(authoring);
        List<RoomObjectPlacementData> results = new(roomObjects.Length);
        for (int i = 0; i < roomObjects.Length; i++)
        {
            if (roomObjects[i].TryGetPlacementData(out RoomObjectPlacementData placement))
                results.Add(placement);
        }

        results.Sort(CompareObjectPlacements);
        return results;
    }

    private static List<RoomTravelEndpointPlacementData> CollectTravelEndpointPlacements(
        RoomPieceAuthoring authoring)
    {
        RoomTravelEndpointAuthoring[] endpoints = GetTravelEndpoints(authoring);
        var results = new List<RoomTravelEndpointPlacementData>(endpoints.Length);
        for (int i = 0; i < endpoints.Length; i++)
        {
            if (endpoints[i].TryGetPlacementData(out RoomTravelEndpointPlacementData placement))
                results.Add(placement);
        }

        results.Sort((left, right) =>
        {
            int xComparison = left.localCell.x.CompareTo(right.localCell.x);
            if (xComparison != 0)
                return xComparison;

            int yComparison = left.localCell.y.CompareTo(right.localCell.y);
            return yComparison != 0
                ? yComparison
                : string.CompareOrdinal(left.slotId, right.slotId);
        });
        return results;
    }

    private static int CompareObjectPlacements(
        RoomObjectPlacementData left,
        RoomObjectPlacementData right)
    {
        int kindComparison = left.kind.CompareTo(right.kind);
        if (kindComparison != 0)
            return kindComparison;

        int xComparison = left.localCell.x.CompareTo(right.localCell.x);
        if (xComparison != 0)
            return xComparison;

        int yComparison = left.localCell.y.CompareTo(right.localCell.y);
        return yComparison != 0
            ? yComparison
            : string.CompareOrdinal(left.placementId, right.placementId);
    }

    private static List<RoomSocketData> CollectSockets(RoomPieceAuthoring authoring)
    {
        RoomSocketAuthoring[] sockets = GetSockets(authoring);
        List<RoomSocketData> results = new(sockets.Length);

        for (int i = 0; i < sockets.Length; i++)
        {
            RoomSocketAuthoring socket = sockets[i];
            if (!socket.TryGetLocalCell(out Vector2Int localCell))
                continue;

            results.Add(new RoomSocketData
            {
                socketId = socket.SocketId,
                localCell = localCell,
                direction = socket.Direction,
                width = socket.Width
            });
        }

        results.Sort(CompareSockets);
        return results;
    }

    private static int CompareSockets(RoomSocketData left, RoomSocketData right)
    {
        int directionComparison = left.direction.CompareTo(right.direction);
        if (directionComparison != 0)
            return directionComparison;

        int xComparison = left.localCell.x.CompareTo(right.localCell.x);
        if (xComparison != 0)
            return xComparison;

        int yComparison = left.localCell.y.CompareTo(right.localCell.y);
        return yComparison != 0
            ? yComparison
            : string.CompareOrdinal(left.socketId, right.socketId);
    }

    private static int CountTiles(Tilemap tilemap, Vector2Int size, out int outsideCount)
    {
        outsideCount = 0;
        if (tilemap == null)
            return 0;

        int insideCount = 0;
        BoundsInt bounds = tilemap.cellBounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cell = new(x, y, 0);
                if (!tilemap.HasTile(cell))
                    continue;

                if (IsInsideRoomBounds(cell, size))
                    insideCount++;
                else
                    outsideCount++;
            }
        }

        return insideCount;
    }

    private static List<RoomTileData> CollectTiles(Tilemap tilemap, Vector2Int size)
    {
        List<RoomTileData> results = new();
        if (tilemap == null)
            return results;

        BoundsInt bounds = tilemap.cellBounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cell = new(x, y, 0);
                if (!IsInsideRoomBounds(cell, size))
                    continue;

                TileBase tile = tilemap.GetTile(cell);
                if (tile == null)
                    continue;

                results.Add(new RoomTileData
                {
                    localCell = new Vector2Int(cell.x, cell.y),
                    tile = tile
                });
            }
        }

        return results;
    }

    private static bool IsInsideRoomBounds(Vector3Int cell, Vector2Int size)
    {
        return cell.x >= 0 &&
               cell.y >= 0 &&
               cell.x < size.x &&
               cell.y < size.y;
    }

    private static bool IsInsideRoomBounds(Vector2Int cell, Vector2Int size)
    {
        return cell.x >= 0 &&
               cell.y >= 0 &&
               cell.x < size.x &&
               cell.y < size.y;
    }

    private static RoomSocketAuthoring[] GetSockets(RoomPieceAuthoring authoring)
    {
        return authoring != null
            ? authoring.GetComponentsInChildren<RoomSocketAuthoring>(true)
            : new RoomSocketAuthoring[0];
    }

    private static RoomObjectAuthoring[] GetRoomObjects(RoomPieceAuthoring authoring)
    {
        return authoring != null
            ? authoring.GetComponentsInChildren<RoomObjectAuthoring>(true)
            : new RoomObjectAuthoring[0];
    }

    private static RoomTravelEndpointAuthoring[] GetTravelEndpoints(RoomPieceAuthoring authoring)
    {
        return authoring != null
            ? authoring.GetComponentsInChildren<RoomTravelEndpointAuthoring>(true)
            : new RoomTravelEndpointAuthoring[0];
    }

    private void DrawSocketSceneHandles(SceneView sceneView)
    {
        RoomAuthoringDungeonPreview.DrawSceneHandles();

        RoomPieceAuthoring authoring = ResolveSelectedAuthoring();
        if (authoring == null || authoring.Grid == null)
            return;

        DrawTravelEndpointSceneHandle(sceneView, authoring);
        HandleSocketPlacementInput(sceneView, authoring);

        RoomSocketAuthoring[] sockets = GetSockets(authoring);
        for (int i = 0; i < sockets.Length; i++)
        {
            RoomSocketAuthoring socket = sockets[i];
            if (!socket.TryGetLocalCell(out Vector2Int localCell))
                continue;

            RoomSocketData socketData = new()
            {
                socketId = socket.SocketId,
                localCell = localCell,
                direction = socket.Direction,
                width = socket.Width
            };
            Vector2Int firstCell = RoomSocketGeometry.GetLocalCell(socketData, 0);
            Vector2Int lastCell = RoomSocketGeometry.GetLocalCell(socketData, socket.Width - 1);
            Vector3 firstCenter = authoring.Grid.GetCellCenterWorld(
                new Vector3Int(firstCell.x, firstCell.y, 0));
            Vector3 lastCenter = authoring.Grid.GetCellCenterWorld(
                new Vector3Int(lastCell.x, lastCell.y, 0));
            Vector3 spanCenter = Vector3.Lerp(firstCenter, lastCenter, 0.5f);
            Vector3 origin = socket.transform.position;
            Vector3 outward = authoring.Grid.transform.TransformDirection(DirectionToVector(socket.Direction)).normalized;
            Vector3 endpoint = spanCenter + outward * 0.75f;

            Handles.color = Color.cyan;
            Handles.DrawLine(firstCenter, lastCenter, 5f);
            Handles.DrawSolidDisc(firstCenter, authoring.Grid.transform.forward, 0.08f);
            Handles.DrawSolidDisc(lastCenter, authoring.Grid.transform.forward, 0.08f);
            Handles.DrawLine(spanCenter, endpoint, 3f);
            Handles.DrawSolidDisc(endpoint, authoring.Grid.transform.forward, 0.08f);
            Handles.Label(
                spanCenter + Vector3.up * 0.25f,
                $"{socket.SocketId} [{socket.Direction}, {socket.Width} cells]");

            if (Selection.activeGameObject != socket.gameObject)
                continue;

            EditorGUI.BeginChangeCheck();
            Vector3 movedWorldPosition = Handles.PositionHandle(origin, Quaternion.identity);
            if (!EditorGUI.EndChangeCheck())
                continue;

            Undo.RecordObject(socket.transform, "Move Room Socket");
            Vector3Int movedCell = authoring.Grid.WorldToCell(movedWorldPosition);
            socket.EditorSetLocalCell(new Vector2Int(movedCell.x, movedCell.y));
            EditorUtility.SetDirty(socket.transform);
            Repaint();
        }
    }

    /// <summary>
    /// 책임 : 선택된 이동 슬롯의 별도 도착점을 Scene View 녹색 핸들로 표시하고 방 Grid 로컬 좌표에 기록한다.
    /// </summary>
    private void DrawTravelEndpointSceneHandle(
        SceneView sceneView,
        RoomPieceAuthoring authoring)
    {
        if (Selection.activeGameObject == null)
            return;

        RoomTravelEndpointAuthoring endpoint =
            Selection.activeGameObject.GetComponentInParent<RoomTravelEndpointAuthoring>();
        if (endpoint == null ||
            endpoint.GetComponentInParent<RoomPieceAuthoring>() != authoring ||
            !endpoint.TryGetArrivalWorldPosition(out Vector3 arrivalPosition))
        {
            return;
        }

        Handles.color = new Color(0.25f, 1f, 0.45f, 1f);
        Handles.DrawDottedLine(endpoint.transform.position, arrivalPosition, 4f);
        Handles.DrawWireDisc(arrivalPosition, authoring.Grid.transform.forward, 0.3f);
        Handles.Label(arrivalPosition + Vector3.up * 0.35f, "도착 위치");

        EditorGUI.BeginChangeCheck();
        Vector3 movedPosition = Handles.PositionHandle(
            arrivalPosition,
            authoring.Grid.transform.rotation);
        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(endpoint, "Move Travel Arrival Point");
        endpoint.EditorSetArrivalWorldPosition(movedPosition);
        EditorUtility.SetDirty(endpoint);
        RoomAuthoringWorkspace.MarkDirty();
        Repaint();
        sceneView.Repaint();
    }

    private void HandleSocketPlacementInput(
        SceneView sceneView,
        RoomPieceAuthoring authoring)
    {
        if (!socketPlacementMode)
            return;

        Event currentEvent = Event.current;
        if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
        {
            socketPlacementMode = false;
            currentEvent.Use();
            Repaint();
            sceneView.Repaint();
            return;
        }

        if (currentEvent.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        Ray mouseRay = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
        Plane gridPlane = new(
            authoring.Grid.transform.forward,
            authoring.Grid.transform.position);
        if (!gridPlane.Raycast(mouseRay, out float enter))
            return;

        Vector3 worldPosition = mouseRay.GetPoint(enter);
        Vector3Int rawCell3D = authoring.Grid.WorldToCell(worldPosition);
        Vector2Int rawCell = new(rawCell3D.x, rawCell3D.y);
        ResolveNearestBoundary(
            rawCell,
            authoring.Size,
            out RoomSocketDirection direction,
            out int preferredStart);

        bool hasPlacement = TryFindAvailableSocketCell(
            authoring,
            direction,
            preferredStart,
            out Vector2Int localCell);
        if (hasPlacement)
            DrawSocketPlacementPreview(authoring, direction, localCell);

        if (currentEvent.type != EventType.MouseDown ||
            currentEvent.button != 0 ||
            currentEvent.alt)
        {
            if (currentEvent.type == EventType.MouseMove)
                sceneView.Repaint();
            return;
        }

        currentEvent.Use();
        if (!hasPlacement)
        {
            sceneView.ShowNotification(new GUIContent("이 경계에는 빈 2칸 소켓 공간이 없습니다."));
            return;
        }

        RoomSocketAuthoring[] existingSockets = GetSockets(authoring);
        RoomSocketAuthoring socket = CreateConnectionSocket(
            authoring,
            CreateNextSocketId(existingSockets),
            direction,
            localCell);
        Selection.activeObject = socket.gameObject;
        EditorUtility.SetDirty(socket);
        RoomAuthoringWorkspace.MarkDirty();
        Repaint();
        sceneView.Repaint();
    }

    private static void ResolveNearestBoundary(
        Vector2Int rawCell,
        Vector2Int size,
        out RoomSocketDirection direction,
        out int preferredStart)
    {
        int maxX = Mathf.Max(0, size.x - 1);
        int maxY = Mathf.Max(0, size.y - 1);
        int leftDistance = Mathf.Abs(rawCell.x);
        int rightDistance = Mathf.Abs(rawCell.x - maxX);
        int downDistance = Mathf.Abs(rawCell.y);
        int upDistance = Mathf.Abs(rawCell.y - maxY);
        int minimumDistance = Mathf.Min(leftDistance, rightDistance, downDistance, upDistance);

        if (minimumDistance == upDistance)
        {
            direction = RoomSocketDirection.Up;
            preferredStart = rawCell.x;
        }
        else if (minimumDistance == rightDistance)
        {
            direction = RoomSocketDirection.Right;
            preferredStart = rawCell.y;
        }
        else if (minimumDistance == downDistance)
        {
            direction = RoomSocketDirection.Down;
            preferredStart = rawCell.x;
        }
        else
        {
            direction = RoomSocketDirection.Left;
            preferredStart = rawCell.y;
        }
    }

    private static void DrawSocketPlacementPreview(
        RoomPieceAuthoring authoring,
        RoomSocketDirection direction,
        Vector2Int localCell)
    {
        RoomSocketData preview = new()
        {
            localCell = localCell,
            direction = direction,
            width = RoomSocketGeometry.RequiredWidth
        };
        Vector2Int firstCell = RoomSocketGeometry.GetLocalCell(preview, 0);
        Vector2Int lastCell = RoomSocketGeometry.GetLocalCell(
            preview,
            RoomSocketGeometry.RequiredWidth - 1);
        Vector3 firstCenter = authoring.Grid.GetCellCenterWorld(
            new Vector3Int(firstCell.x, firstCell.y, 0));
        Vector3 lastCenter = authoring.Grid.GetCellCenterWorld(
            new Vector3Int(lastCell.x, lastCell.y, 0));
        Vector3 spanCenter = Vector3.Lerp(firstCenter, lastCenter, 0.5f);
        Vector3 outward = authoring.Grid.transform.TransformDirection(
            DirectionToVector(direction)).normalized;

        Handles.color = Color.green;
        Handles.DrawLine(firstCenter, lastCenter, 7f);
        Handles.DrawWireDisc(firstCenter, authoring.Grid.transform.forward, 0.18f);
        Handles.DrawWireDisc(lastCenter, authoring.Grid.transform.forward, 0.18f);
        Handles.DrawLine(spanCenter, spanCenter + outward, 4f);
        Handles.Label(spanCenter + outward * 1.1f, "클릭하여 출입구 배치");
    }

    private static Vector3 DirectionToVector(RoomSocketDirection direction)
    {
        return direction switch
        {
            RoomSocketDirection.Up => Vector3.up,
            RoomSocketDirection.Right => Vector3.right,
            RoomSocketDirection.Down => Vector3.down,
            RoomSocketDirection.Left => Vector3.left,
            _ => Vector3.zero
        };
    }

}
