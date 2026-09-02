using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임:
/// - 기획자가 가로(+X)와 세로(+Y) 전용의 짧은 복도 조각을 기존 8개 Tilemap 레이어와 Pivot 기반 GroundProp으로 제작·편집·검증하게 한다.
/// - CorridorDecorationModuleSO를 Bake하고 테마 DungeonGenerationProfileSO의 장식 프로필에 등록한다.
/// - Unity Tile Palette를 그대로 사용하면서 선택한 축에 맞는 안전한 제작 범위를 안내한다.
/// </summary>
public sealed class CorridorDecorationEditorWindow : EditorWindow
{
    private const string DecorationRootFolder =
        "Assets/_Project/Data/Dungeon/CorridorDecorations";

    [SerializeField] private DungeonGenerationProfileSO generationProfile;
    [SerializeField] private CorridorDecorationProfileSO decorationProfile;
    [SerializeField] private CorridorDecorationModuleSO moduleToLoad;
    [SerializeField] private string newModuleId = "Corridor_Middle_01";
    [SerializeField] private CorridorDecorationAxis newModuleAxis =
        CorridorDecorationAxis.Horizontal;
    [SerializeField] private CorridorDecorationModuleRole newModuleRole =
        CorridorDecorationModuleRole.Middle;
    [SerializeField] private int newModuleLength = 4;
    [SerializeField] private GameObject propPrefab;
    [SerializeField] private CorridorDecorationModuleAuthoring selectedAuthoring;
    [SerializeField] private int completedPreviewCorridorLength = 16;
    [SerializeField] private int completedPreviewSeed = 20260902;
    [SerializeField] private int completedPreviewConnectionIndex;
    [SerializeField] private CorridorDecorationAxis completedPreviewAxis =
        CorridorDecorationAxis.Horizontal;

    private readonly List<string> validationMessages = new();
    private Vector2 scroll;
    private string statusMessage = string.Empty;
    private MessageType statusType = MessageType.None;
    private string completedPreviewSummary = string.Empty;

    [MenuItem("Tools/Dungeon/Corridor Decoration Editor")]
    public static void Open()
    {
        GetWindow<CorridorDecorationEditorWindow>("Corridor Decoration");
    }

    /// <summary>
    /// 책임 : Room Piece 미리보기에서 선택한 테마 프로필을 유지한 채 복도 장식 제작 창을 연다.
    /// </summary>
    public static void OpenWithProfile(DungeonGenerationProfileSO profile)
    {
        CorridorDecorationEditorWindow window =
            GetWindow<CorridorDecorationEditorWindow>("Corridor Decoration");
        window.generationProfile = profile;
        window.decorationProfile = profile != null
            ? profile.CorridorDecorationProfile
            : null;
        window.Repaint();
    }

    private void OnEnable()
    {
        selectedAuthoring = RoomAuthoringWorkspace.FindCorridorAuthoring();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawProfileSection();
        EditorGUILayout.Space(8f);
        DrawWorkspaceSection();
        EditorGUILayout.Space(8f);
        DrawCompletedPreviewSection();
        EditorGUILayout.Space(8f);

        selectedAuthoring = ResolveSelectedAuthoring();
        if (selectedAuthoring == null)
        {
            DrawCreateSection();
            EditorGUILayout.Space(8f);
            DrawModuleLibrary();
        }
        else
        {
            DrawAuthoringSection();
        }

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawProfileSection()
    {
        EditorGUILayout.LabelField("테마 복도 장식", EditorStyles.boldLabel);
        DungeonGenerationProfileSO requestedGeneration = EditorGUILayout.ObjectField(
            "생성 프로필",
            generationProfile,
            typeof(DungeonGenerationProfileSO),
            false) as DungeonGenerationProfileSO;
        if (requestedGeneration != generationProfile)
        {
            CorridorDecorationCompletedPreview.Clear();
            completedPreviewSummary = string.Empty;
            generationProfile = requestedGeneration;
            decorationProfile = generationProfile != null
                ? generationProfile.CorridorDecorationProfile
                : null;
        }

        CorridorDecorationProfileSO requestedDecoration = EditorGUILayout.ObjectField(
            "장식 프로필",
            decorationProfile,
            typeof(CorridorDecorationProfileSO),
            false) as CorridorDecorationProfileSO;
        if (requestedDecoration != decorationProfile)
        {
            CorridorDecorationCompletedPreview.Clear();
            completedPreviewSummary = string.Empty;
            decorationProfile = requestedDecoration;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(generationProfile == null))
            {
                if (GUILayout.Button("테마 장식 프로필 찾기/만들기"))
                    EnsureDecorationProfile();
            }

            using (new EditorGUI.DisabledScope(
                       generationProfile == null || decorationProfile == null))
            {
                if (GUILayout.Button("생성 프로필에 연결"))
                    AssignDecorationProfileToGenerationProfile();
            }
        }

        if (decorationProfile == null)
        {
            EditorGUILayout.HelpBox(
                "장식 프로필이 없어도 모듈은 만들 수 있지만, 실제 절차 복도에는 등록된 테마 장식 프로필만 적용됩니다.",
                MessageType.Info);
            return;
        }

        SerializedObject serializedProfile = new(decorationProfile);
        serializedProfile.Update();
        EditorGUILayout.PropertyField(
            serializedProfile.FindProperty("maxLandmarksPerCorridor"),
            new GUIContent("복도당 Landmark 제한"));
        EditorGUILayout.PropertyField(
            serializedProfile.FindProperty("modules"),
            new GUIContent("등록 모듈"),
            includeChildren: true);
        if (serializedProfile.ApplyModifiedProperties())
            EditorUtility.SetDirty(decorationProfile);
    }

    private void DrawWorkspaceSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("안전 작업 공간", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "복도 조각은 방 제작과 같은 저장되지 않는 additive 작업 공간에서 편집하므로 현재 게임 씬을 변경하지 않습니다.",
                MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("작업 공간 활성화"))
                    RoomAuthoringWorkspace.Open();

                if (GUILayout.Button("작업 공간 닫기") &&
                    RoomAuthoringWorkspace.Close(confirmDiscard: true))
                {
                    selectedAuthoring = null;
                    validationMessages.Clear();
                    completedPreviewSummary = string.Empty;
                }
            }
        }
    }

    private void DrawCompletedPreviewSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("완성 복도 미리보기", EditorStyles.boldLabel);
            completedPreviewCorridorLength = Mathf.Clamp(
                EditorGUILayout.IntField(
                    new GUIContent(
                        "전체 복도 길이",
                        "두 방 소켓 사이에서 장식 모듈이 사용할 전체 셀 수입니다."),
                    completedPreviewCorridorLength),
                1,
                512);
            completedPreviewAxis = (CorridorDecorationAxis)EditorGUILayout.EnumPopup(
                new GUIContent(
                    "복도 축",
                    "Horizontal은 +X 원본, Vertical은 +Y 원본 모듈로 조립합니다."),
                completedPreviewAxis);
            completedPreviewSeed = EditorGUILayout.IntField(
                new GUIContent(
                    "미리보기 Seed",
                    "런타임 레이아웃 Seed를 재현할 때 사용합니다."),
                completedPreviewSeed);
            completedPreviewConnectionIndex = Mathf.Max(
                0,
                EditorGUILayout.IntField(
                    new GUIContent(
                        "연결 번호",
                        "런타임 연결 목록의 인덱스입니다. 길이, Seed와 함께 모듈 순서를 결정합니다."),
                    completedPreviewConnectionIndex));

            string previewAxisLabel = completedPreviewAxis == CorridorDecorationAxis.Horizontal
                ? "가로(+X)"
                : "세로(+Y)";
            EditorGUILayout.HelpBox(
                $"{previewAxisLabel} 방향의 {completedPreviewCorridorLength}칸 복도를 조립합니다. " +
                "Start와 End 모듈은 각각 문에 맞닿은 첫 칸과 마지막 칸부터 사용할 수 있습니다.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(
                           decorationProfile == null ||
                           decorationProfile.Modules.Count == 0))
                {
                    if (GUILayout.Button("완성본 생성/갱신", GUILayout.Height(26f)))
                        GenerateCompletedPreview();
                }

                using (new EditorGUI.DisabledScope(!RoomAuthoringWorkspace.IsOpen))
                {
                    if (GUILayout.Button("미리보기 제거", GUILayout.Height(26f)))
                    {
                        CorridorDecorationCompletedPreview.Clear();
                        completedPreviewSummary = string.Empty;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(completedPreviewSummary))
            {
                EditorGUILayout.HelpBox(
                    completedPreviewSummary,
                    MessageType.Info);
            }
        }
    }

    private void GenerateCompletedPreview()
    {
        CorridorDecorationCompletedPreviewResult result =
            CorridorDecorationCompletedPreview.Show(
                generationProfile,
                decorationProfile,
                completedPreviewCorridorLength,
                completedPreviewSeed,
                completedPreviewConnectionIndex,
                completedPreviewAxis);
        completedPreviewSummary = result.Message;
        statusMessage = result.Success
            ? "입력 길이에 맞춰 완성 복도 미리보기를 생성했습니다."
            : $"완성 복도 미리보기 생성 실패: {result.Message}";
        statusType = result.Success ? MessageType.Info : MessageType.Error;
    }

    private void DrawCreateSection()
    {
        EditorGUILayout.LabelField("새 복도 모듈", EditorStyles.boldLabel);
        newModuleId = EditorGUILayout.TextField("모듈 ID", newModuleId ?? string.Empty);
        newModuleAxis = (CorridorDecorationAxis)EditorGUILayout.EnumPopup(
            "복도 축",
            newModuleAxis);
        newModuleRole = (CorridorDecorationModuleRole)EditorGUILayout.EnumPopup(
            "역할",
            newModuleRole);
        newModuleLength = EditorGUILayout.IntField(
            "진행축 길이",
            Mathf.Max(1, newModuleLength));
        EditorGUILayout.HelpBox(GetAxisAuthoringHelp(newModuleAxis), MessageType.Info);

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newModuleId)))
        {
            if (GUILayout.Button("새 복도 조각 만들기", GUILayout.Height(28f)))
                CreateNewAuthoring();
        }
    }

    private void DrawModuleLibrary()
    {
        EditorGUILayout.LabelField("기존 모듈 편집", EditorStyles.boldLabel);
        moduleToLoad = EditorGUILayout.ObjectField(
            "모듈 직접 선택",
            moduleToLoad,
            typeof(CorridorDecorationModuleSO),
            false) as CorridorDecorationModuleSO;
        using (new EditorGUI.DisabledScope(moduleToLoad == null))
        {
            if (GUILayout.Button("선택 모듈 불러오기"))
                LoadModule(moduleToLoad);
        }

        if (decorationProfile == null || decorationProfile.Modules.Count == 0)
            return;

        EditorGUILayout.Space(4f);
        for (int moduleIndex = 0;
             moduleIndex < decorationProfile.Modules.Count;
             moduleIndex++)
        {
            CorridorDecorationModuleSO module = decorationProfile.Modules[moduleIndex];
            if (module == null)
                continue;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    $"{module.ModuleId} · {module.Axis} · {module.Role} · {module.Length}칸");
                if (GUILayout.Button("편집", GUILayout.Width(55f)))
                    LoadModule(module);
            }
        }
    }

    private void DrawAuthoringSection()
    {
        EditorGUILayout.LabelField("복도 모듈 편집", EditorStyles.boldLabel);
        string requestedId = EditorGUILayout.TextField(
            "모듈 ID",
            selectedAuthoring.ModuleId ?? string.Empty);
        CorridorDecorationAxis requestedAxis =
            (CorridorDecorationAxis)EditorGUILayout.EnumPopup(
                "복도 축",
                selectedAuthoring.Axis);
        CorridorDecorationModuleRole requestedRole =
            (CorridorDecorationModuleRole)EditorGUILayout.EnumPopup(
                "역할",
                selectedAuthoring.Role);
        int requestedLength = EditorGUILayout.IntField(
            "진행축 길이",
            selectedAuthoring.Length);
        requestedLength = Mathf.Max(1, requestedLength);
        if (requestedId != selectedAuthoring.ModuleId ||
            requestedAxis != selectedAuthoring.Axis ||
            requestedRole != selectedAuthoring.Role ||
            requestedLength != selectedAuthoring.Length)
        {
            Undo.RecordObject(selectedAuthoring, "Edit Corridor Decoration Metadata");
            selectedAuthoring.EditorConfigure(
                requestedId,
                requestedAxis,
                requestedRole,
                requestedLength,
                selectedAuthoring.SourceModule);
            EditorUtility.SetDirty(selectedAuthoring);
            RoomAuthoringWorkspace.MarkDirty();
        }

        EditorGUILayout.HelpBox(
            "각 레이어 버튼으로 대상 Tilemap을 선택한 뒤 Unity Tile Palette로 그리세요. " +
            "Floor/Wall은 기본 복도 위를 선택적으로 교체하며, 나머지 레이어는 그대로 덧씌워집니다.",
            MessageType.Info);
        EditorGUILayout.HelpBox(
            GetAxisAuthoringHelp(selectedAuthoring.Axis),
            MessageType.None);
        DrawLayerSelection();
        EditorGUILayout.Space(8f);
        DrawPropSection();
        EditorGUILayout.Space(8f);
        DrawValidationAndBakeSection();
    }

    private void DrawLayerSelection()
    {
        EditorGUILayout.LabelField("타일 레이어", EditorStyles.miniBoldLabel);
        for (int layerIndex = 0;
             layerIndex < RoomTileLayerContract.OrderedLayers.Count;
             layerIndex += 2)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLayerButton(RoomTileLayerContract.OrderedLayers[layerIndex]);
                if (layerIndex + 1 < RoomTileLayerContract.OrderedLayers.Count)
                    DrawLayerButton(RoomTileLayerContract.OrderedLayers[layerIndex + 1]);
            }
        }
    }

    private void DrawLayerButton(RoomTileLayerKind layer)
    {
        Tilemap tilemap = selectedAuthoring.GetTilemap(layer);
        int tileCount = CountTiles(tilemap);
        if (GUILayout.Button(
                $"{RoomTileLayerContract.GetLayerName(layer)} ({tileCount})",
                GUILayout.MinWidth(150f)))
        {
            Selection.activeObject = tilemap != null ? tilemap.gameObject : null;
            SceneView.RepaintAll();
        }
    }

    private void DrawPropSection()
    {
        EditorGUILayout.LabelField("GroundProp 오브젝트", EditorStyles.miniBoldLabel);
        propPrefab = EditorGUILayout.ObjectField(
            "프리팹",
            propPrefab,
            typeof(GameObject),
            false) as GameObject;
        using (new EditorGUI.DisabledScope(propPrefab == null))
        {
            if (GUILayout.Button("중앙 셀에 GroundProp 추가"))
                AddGroundProp();
        }

        RoomObjectAuthoring[] objects = GetPropAuthorings(selectedAuthoring);
        EditorGUILayout.LabelField($"현재 Pivot 배치 {objects.Length}개");
        for (int objectIndex = 0; objectIndex < objects.Length; objectIndex++)
        {
            RoomObjectAuthoring roomObject = objects[objectIndex];
            string position = roomObject.TryGetPlacementData(out RoomObjectPlacementData placement)
                ? $"cell {placement.localCell} + {placement.localOffset}"
                : "Grid 위치 계산 실패";
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{roomObject.PlacementId} · {position}");
                if (GUILayout.Button("선택", GUILayout.Width(50f)))
                    Selection.activeObject = roomObject.gameObject;
            }
        }
    }

    private void DrawValidationAndBakeSection()
    {
        ValidateAuthoring();
        if (validationMessages.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "검증 통과 · 모든 타일과 오브젝트 Pivot이 복도 조각 범위 안에 있습니다.",
                MessageType.Info);
        }
        else
        {
            for (int messageIndex = 0;
                 messageIndex < validationMessages.Count;
                 messageIndex++)
            {
                EditorGUILayout.HelpBox(validationMessages[messageIndex], MessageType.Error);
            }
        }

        using (new EditorGUI.DisabledScope(validationMessages.Count > 0))
        {
            string buttonLabel = selectedAuthoring.SourceModule != null
                ? "현재 모듈 갱신 및 프로필 등록"
                : "새 모듈 저장 및 프로필 등록";
            if (GUILayout.Button(buttonLabel, GUILayout.Height(30f)))
                BakeCurrentModule();
        }
    }

    private void EnsureDecorationProfile()
    {
        if (generationProfile == null)
            return;

        if (generationProfile.CorridorDecorationProfile != null)
        {
            decorationProfile = generationProfile.CorridorDecorationProfile;
            return;
        }

        EnsureFolder(DecorationRootFolder);
        string themeName = generationProfile.RoomLibrary != null &&
                           !string.IsNullOrWhiteSpace(generationProfile.RoomLibrary.ThemeId)
            ? generationProfile.RoomLibrary.ThemeId
            : generationProfile.name;
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{DecorationRootFolder}/{SanitizeFileName(themeName)}CorridorDecorationProfile.asset");
        decorationProfile = CreateInstance<CorridorDecorationProfileSO>();
        AssetDatabase.CreateAsset(decorationProfile, assetPath);
        AssignDecorationProfileToGenerationProfile();
        AssetDatabase.SaveAssets();
        statusMessage = $"테마 장식 프로필을 만들고 연결했습니다: {assetPath}";
        statusType = MessageType.Info;
    }

    private void AssignDecorationProfileToGenerationProfile()
    {
        if (generationProfile == null || decorationProfile == null)
            return;

        Undo.RecordObject(generationProfile, "Assign Corridor Decoration Profile");
        generationProfile.EditorSetCorridorDecorationProfile(decorationProfile);
        EditorUtility.SetDirty(generationProfile);
        AssetDatabase.SaveAssets();
        statusMessage = $"{generationProfile.name}에 {decorationProfile.name}을 연결했습니다.";
        statusType = MessageType.Info;
    }

    private void CreateNewAuthoring()
    {
        if (!TryPrepareWorkspace())
            return;

        selectedAuthoring = CreateAuthoringRoot(
            newModuleId,
            newModuleAxis,
            newModuleRole,
            Mathf.Max(1, newModuleLength),
            sourceModule: null);
        RoomAuthoringWorkspace.MarkDirty();
        validationMessages.Clear();
        statusMessage = "새 복도 조각을 만들었습니다. 레이어 버튼을 선택해 타일을 그리세요.";
        statusType = MessageType.Info;
    }

    private void LoadModule(CorridorDecorationModuleSO module)
    {
        if (module == null || !TryPrepareWorkspace())
            return;

        selectedAuthoring = CreateAuthoringRoot(
            module.ModuleId,
            module.Axis,
            module.Role,
            module.Length,
            module);
        RoomBuildData buildData = module.BuildData;
        for (int layerIndex = 0;
             layerIndex < RoomTileLayerContract.OrderedLayers.Count;
             layerIndex++)
        {
            RoomTileLayerKind layer = RoomTileLayerContract.OrderedLayers[layerIndex];
            RestoreTiles(selectedAuthoring.GetTilemap(layer), buildData.GetTiles(layer));
        }

        RestoreProps(selectedAuthoring, buildData.objectPlacements);
        moduleToLoad = module;
        RoomAuthoringWorkspace.MarkSaved();
        Selection.activeObject = selectedAuthoring.gameObject;
        statusMessage = $"{module.ModuleId}을 편집 작업 공간에 불러왔습니다.";
        statusType = MessageType.Info;
    }

    private bool TryPrepareWorkspace()
    {
        if (!RoomAuthoringWorkspace.Open().IsValid())
            return false;

        RoomPieceAuthoring room = RoomAuthoringWorkspace.FindAuthoring();
        CorridorDecorationModuleAuthoring corridor =
            RoomAuthoringWorkspace.FindCorridorAuthoring();
        if (room == null && corridor == null)
            return true;

        if (RoomAuthoringWorkspace.HasUnsavedChanges &&
            !EditorUtility.DisplayDialog(
                "편집 중인 던전 조각 교체",
                room != null
                    ? $"'{room.RoomId}' 방에 저장되지 않은 내용이 있습니다. 버리고 복도 장식을 열까요?"
                    : $"'{corridor.ModuleId}' 복도 장식에 저장되지 않은 내용이 있습니다. 버릴까요?",
                "변경 내용 버리기",
                "계속 편집"))
        {
            return false;
        }

        RoomAuthoringDungeonPreview.Clear();
        CorridorDecorationCompletedPreview.Clear();
        if (room != null)
            DestroyImmediate(room.gameObject);
        if (corridor != null)
            DestroyImmediate(corridor.gameObject);
        selectedAuthoring = null;
        RoomAuthoringWorkspace.MarkSaved();
        return true;
    }

    private static CorridorDecorationModuleAuthoring CreateAuthoringRoot(
        string moduleId,
        CorridorDecorationAxis axis,
        CorridorDecorationModuleRole role,
        int length,
        CorridorDecorationModuleSO sourceModule)
    {
        GameObject root = new(sourceModule != null ? $"{moduleId}_Editing" : moduleId);
        Undo.RegisterCreatedObjectUndo(root, "Create Corridor Decoration Module");
        RoomAuthoringWorkspace.MoveToWorkspace(root);
        CorridorDecorationModuleAuthoring authoring =
            root.AddComponent<CorridorDecorationModuleAuthoring>();
        authoring.EditorConfigure(moduleId, axis, role, length, sourceModule);

        GameObject gridObject = new("AuthoringGrid");
        Undo.RegisterCreatedObjectUndo(gridObject, "Create Corridor Decoration Grid");
        gridObject.transform.SetParent(root.transform, false);
        Grid grid = gridObject.AddComponent<Grid>();
        Tilemap[] tilemaps = new Tilemap[RoomTileLayerContract.OrderedLayers.Count];
        for (int layerIndex = 0; layerIndex < tilemaps.Length; layerIndex++)
        {
            tilemaps[layerIndex] = CreateTilemapLayer(
                gridObject.transform,
                RoomTileLayerContract.OrderedLayers[layerIndex]);
        }

        authoring.EditorAssignTilemaps(
            grid,
            tilemaps[0],
            tilemaps[1],
            tilemaps[2],
            tilemaps[3],
            tilemaps[4],
            tilemaps[5],
            tilemaps[6],
            tilemaps[7]);
        EditorUtility.SetDirty(authoring);
        Selection.activeObject = root;
        return authoring;
    }

    private void AddGroundProp()
    {
        selectedAuthoring = ResolveSelectedAuthoring();
        if (selectedAuthoring == null || selectedAuthoring.Grid == null || propPrefab == null)
            return;

        RoomObjectAuthoring[] existing = GetPropAuthorings(selectedAuthoring);
        RoomObjectPlacementData placement = new()
        {
            placementId = $"GroundProp_{existing.Length + 1:00}",
            kind = RoomObjectKind.Prop,
            prefab = propPrefab,
            localCell = selectedAuthoring.Axis == CorridorDecorationAxis.Horizontal
                ? new Vector2Int(selectedAuthoring.Length / 2, 0)
                : new Vector2Int(0, selectedAuthoring.Length / 2),
            localOffset = Vector2.zero,
            localRotationDegrees = 0f,
            localScale = propPrefab.transform.localScale
        };
        RoomObjectAuthoring marker = CreatePropAuthoring(selectedAuthoring, placement);
        Selection.activeObject = marker.gameObject;
        RoomAuthoringWorkspace.MarkDirty();
        SceneView.RepaintAll();
    }

    private void ValidateAuthoring()
    {
        validationMessages.Clear();
        if (selectedAuthoring == null || selectedAuthoring.Grid == null)
        {
            validationMessages.Add("편집 중인 복도 조각과 Grid가 필요합니다.");
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedAuthoring.ModuleId))
            validationMessages.Add("모듈 ID가 비어 있습니다.");

        for (int layerIndex = 0;
             layerIndex < RoomTileLayerContract.OrderedLayers.Count;
             layerIndex++)
        {
            RoomTileLayerKind layer = RoomTileLayerContract.OrderedLayers[layerIndex];
            Tilemap tilemap = selectedAuthoring.GetTilemap(layer);
            if (tilemap == null)
            {
                validationMessages.Add($"{RoomTileLayerContract.GetLayerName(layer)} Tilemap이 없습니다.");
                continue;
            }

            BoundsInt bounds = tilemap.cellBounds;
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    Vector3Int cell = new(x, y, 0);
                    if (tilemap.GetTile(cell) != null &&
                        !IsInsideFootprint(
                            new Vector2Int(x, y),
                            selectedAuthoring.Length,
                            selectedAuthoring.Axis))
                    {
                        validationMessages.Add(
                            $"{RoomTileLayerContract.GetLayerName(layer)} 타일 {new Vector2Int(x, y)}이 " +
                            $"{GetFootprintDescription(selectedAuthoring.Length, selectedAuthoring.Axis)} " +
                            "범위 밖에 있습니다.");
                    }
                }
            }
        }

        var placementIds = new HashSet<string>(System.StringComparer.Ordinal);
        RoomObjectAuthoring[] objects = GetPropAuthorings(selectedAuthoring);
        for (int objectIndex = 0; objectIndex < objects.Length; objectIndex++)
        {
            RoomObjectAuthoring roomObject = objects[objectIndex];
            if (roomObject.Kind != RoomObjectKind.Prop)
                validationMessages.Add($"{roomObject.gameObject.name}: 복도 장식은 GroundProp만 허용합니다.");
            if (roomObject.Prefab == null)
                validationMessages.Add($"{roomObject.gameObject.name}: 원본 프리팹이 없습니다.");
            if (string.IsNullOrWhiteSpace(roomObject.PlacementId))
                validationMessages.Add($"{roomObject.gameObject.name}: Placement Id가 비어 있습니다.");
            else if (!placementIds.Add(roomObject.PlacementId))
                validationMessages.Add($"Placement Id '{roomObject.PlacementId}'가 중복됩니다.");

            if (!roomObject.TryGetPlacementData(out RoomObjectPlacementData placement))
            {
                validationMessages.Add($"{roomObject.gameObject.name}: Grid 기준 Pivot을 계산할 수 없습니다.");
            }
            else if (!IsInsideFootprint(
                         placement.localCell,
                         selectedAuthoring.Length,
                         selectedAuthoring.Axis))
            {
                validationMessages.Add(
                    $"{roomObject.PlacementId}: Pivot 셀 {placement.localCell}이 " +
                    $"{GetFootprintDescription(selectedAuthoring.Length, selectedAuthoring.Axis)} " +
                    "범위 밖에 있습니다.");
            }
        }
    }

    private void BakeCurrentModule()
    {
        ValidateAuthoring();
        if (validationMessages.Count > 0)
            return;

        RoomBuildData buildData = CollectBuildData(selectedAuthoring);
        CorridorDecorationModuleSO module = selectedAuthoring.SourceModule;
        if (module == null)
        {
            string folder = ResolveModuleFolder();
            EnsureFolder(folder);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/{SanitizeFileName(selectedAuthoring.ModuleId)}.asset");
            module = CreateInstance<CorridorDecorationModuleSO>();
            AssetDatabase.CreateAsset(module, assetPath);
        }
        else
        {
            Undo.RecordObject(module, "Update Corridor Decoration Module");
        }

        module.EditorSetData(
            selectedAuthoring.ModuleId,
            selectedAuthoring.Axis,
            selectedAuthoring.Role,
            selectedAuthoring.Length,
            buildData);
        EditorUtility.SetDirty(module);
        selectedAuthoring.EditorConfigure(
            selectedAuthoring.ModuleId,
            selectedAuthoring.Axis,
            selectedAuthoring.Role,
            selectedAuthoring.Length,
            module);
        EditorUtility.SetDirty(selectedAuthoring);

        if (decorationProfile != null)
        {
            Undo.RecordObject(decorationProfile, "Register Corridor Decoration Module");
            decorationProfile.EditorAddModule(module);
            EditorUtility.SetDirty(decorationProfile);
        }

        AssetDatabase.SaveAssets();
        moduleToLoad = module;
        RoomAuthoringWorkspace.MarkSaved();
        Selection.activeObject = module;
        EditorGUIUtility.PingObject(module);
        statusMessage = decorationProfile != null
            ? $"{module.ModuleId}을 저장하고 {decorationProfile.name}에 등록했습니다."
            : $"{module.ModuleId}을 저장했습니다. 실제 생성에 쓰려면 장식 프로필에 등록하세요.";
        statusType = MessageType.Info;
    }

    private static RoomBuildData CollectBuildData(
        CorridorDecorationModuleAuthoring authoring)
    {
        return new RoomBuildData
        {
            underFloorTiles = CollectTiles(authoring.GetTilemap(RoomTileLayerKind.UnderFloor)),
            floorTiles = CollectTiles(authoring.GetTilemap(RoomTileLayerKind.Floor)),
            floorDetailTiles = CollectTiles(authoring.GetTilemap(RoomTileLayerKind.FloorDetail)),
            groundDecorationTiles = CollectTiles(
                authoring.GetTilemap(RoomTileLayerKind.GroundDecoration)),
            wallTiles = CollectTiles(authoring.GetTilemap(RoomTileLayerKind.Wall)),
            wallDetailTiles = CollectTiles(authoring.GetTilemap(RoomTileLayerKind.WallDetail)),
            foregroundTiles = CollectTiles(authoring.GetTilemap(RoomTileLayerKind.Foreground)),
            overlayFxTiles = CollectTiles(authoring.GetTilemap(RoomTileLayerKind.OverlayFX)),
            objectPlacements = CollectProps(authoring),
            travelEndpointPlacements = new List<RoomTravelEndpointPlacementData>()
        };
    }

    private static List<RoomTileData> CollectTiles(Tilemap tilemap)
    {
        var results = new List<RoomTileData>();
        if (tilemap == null)
            return results;

        BoundsInt bounds = tilemap.cellBounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cell = new(x, y, 0);
                TileBase tile = tilemap.GetTile(cell);
                if (tile == null)
                    continue;

                results.Add(new RoomTileData
                {
                    localCell = new Vector2Int(x, y),
                    tile = tile
                });
            }
        }

        return results;
    }

    private static List<RoomObjectPlacementData> CollectProps(
        CorridorDecorationModuleAuthoring authoring)
    {
        RoomObjectAuthoring[] objects = GetPropAuthorings(authoring);
        var results = new List<RoomObjectPlacementData>(objects.Length);
        for (int objectIndex = 0; objectIndex < objects.Length; objectIndex++)
        {
            if (objects[objectIndex].TryGetPlacementData(out RoomObjectPlacementData placement))
                results.Add(placement);
        }

        results.Sort((left, right) =>
        {
            int x = left.localCell.x.CompareTo(right.localCell.x);
            if (x != 0)
                return x;
            int y = left.localCell.y.CompareTo(right.localCell.y);
            return y != 0 ? y : string.CompareOrdinal(left.placementId, right.placementId);
        });
        return results;
    }

    private static void RestoreTiles(Tilemap tilemap, IReadOnlyList<RoomTileData> tiles)
    {
        if (tilemap == null || tiles == null)
            return;

        for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
        {
            RoomTileData tile = tiles[tileIndex];
            if (tile.tile != null)
            {
                tilemap.SetTile(
                    new Vector3Int(tile.localCell.x, tile.localCell.y, 0),
                    tile.tile);
            }
        }

        tilemap.CompressBounds();
        EditorUtility.SetDirty(tilemap);
    }

    private static void RestoreProps(
        CorridorDecorationModuleAuthoring authoring,
        IReadOnlyList<RoomObjectPlacementData> placements)
    {
        if (placements == null)
            return;

        for (int placementIndex = 0; placementIndex < placements.Count; placementIndex++)
            CreatePropAuthoring(authoring, placements[placementIndex]);
    }

    private static RoomObjectAuthoring CreatePropAuthoring(
        CorridorDecorationModuleAuthoring authoring,
        RoomObjectPlacementData placement)
    {
        GameObject instance = placement.prefab != null
            ? PrefabUtility.InstantiatePrefab(
                placement.prefab,
                authoring.Grid.transform) as GameObject
            : null;
        if (instance == null && placement.prefab != null)
            instance = Instantiate(placement.prefab, authoring.Grid.transform);
        if (instance == null)
            instance = new GameObject(placement.placementId);

        Undo.RegisterCreatedObjectUndo(instance, "Add Corridor GroundProp");
        if (instance.transform.parent != authoring.Grid.transform)
            Undo.SetTransformParent(instance.transform, authoring.Grid.transform, "Parent GroundProp");
        instance.name = placement.prefab != null
            ? $"{placement.placementId}_{placement.prefab.name}"
            : placement.placementId;
        RoomObjectAuthoring marker = instance.GetComponent<RoomObjectAuthoring>();
        if (marker == null)
            marker = Undo.AddComponent<RoomObjectAuthoring>(instance);
        marker.EditorConfigure(
            placement.placementId,
            RoomObjectKind.Prop,
            placement.prefab,
            RoomMonsterSpawnRole.Warrior,
            stageMonsterSet: null);
        marker.EditorSetPlacement(placement);
        EditorUtility.SetDirty(marker);
        return marker;
    }

    private static Tilemap CreateTilemapLayer(
        Transform parent,
        RoomTileLayerKind layer)
    {
        string layerName = RoomTileLayerContract.GetLayerName(layer);
        GameObject layerObject = new(layerName);
        Undo.RegisterCreatedObjectUndo(layerObject, $"Create {layerName} Tilemap");
        layerObject.transform.SetParent(parent, false);
        Tilemap tilemap = layerObject.AddComponent<Tilemap>();
        TilemapRenderer renderer = layerObject.AddComponent<TilemapRenderer>();
        renderer.sortingLayerName = RoomTileLayerContract.GetSortingLayerName(layer);
        renderer.sortingOrder = RoomTileLayerContract.GetSortingOrder(layer);
        return tilemap;
    }

    private CorridorDecorationModuleAuthoring ResolveSelectedAuthoring()
    {
        if (Selection.activeGameObject != null)
        {
            CorridorDecorationModuleAuthoring selected =
                Selection.activeGameObject.GetComponentInParent<CorridorDecorationModuleAuthoring>();
            if (selected != null && RoomAuthoringWorkspace.IsInWorkspace(selected.gameObject))
                return selected;
        }

        if (selectedAuthoring != null &&
            RoomAuthoringWorkspace.IsInWorkspace(selectedAuthoring.gameObject))
        {
            return selectedAuthoring;
        }

        return RoomAuthoringWorkspace.FindCorridorAuthoring();
    }

    private string ResolveModuleFolder()
    {
        if (decorationProfile == null)
            return DecorationRootFolder;

        string profilePath = AssetDatabase.GetAssetPath(decorationProfile);
        string profileDirectory = Path.GetDirectoryName(profilePath)?.Replace('\\', '/');
        return string.IsNullOrWhiteSpace(profileDirectory)
            ? DecorationRootFolder
            : $"{profileDirectory}/{SanitizeFileName(decorationProfile.name)}Modules";
    }

    private static RoomObjectAuthoring[] GetPropAuthorings(
        CorridorDecorationModuleAuthoring authoring)
    {
        return authoring != null
            ? authoring.GetComponentsInChildren<RoomObjectAuthoring>(true)
            : new RoomObjectAuthoring[0];
    }

    private static bool IsInsideFootprint(
        Vector2Int cell,
        int length,
        CorridorDecorationAxis axis)
    {
        return axis == CorridorDecorationAxis.Horizontal
            ? cell.x >= 0 && cell.x < length && cell.y >= -1 && cell.y <= 2
            : cell.y >= 0 && cell.y < length && cell.x >= -1 && cell.x <= 2;
    }

    private static string GetFootprintDescription(
        int length,
        CorridorDecorationAxis axis)
    {
        return axis == CorridorDecorationAxis.Horizontal
            ? $"x=0..{length - 1}, y=-1..2"
            : $"x=-1..2, y=0..{length - 1}";
    }

    private static string GetAxisAuthoringHelp(CorridorDecorationAxis axis)
    {
        return axis == CorridorDecorationAxis.Horizontal
            ? "가로 기준은 왼쪽에서 오른쪽(+X)입니다. Floor는 y=0,1, 양쪽 Wall은 y=-1,2에 맞추세요."
            : "세로 기준은 아래에서 위(+Y)입니다. Floor는 x=0,1, 양쪽 Wall은 x=-1,2에 맞추세요.";
    }

    private static int CountTiles(Tilemap tilemap)
    {
        return tilemap != null ? tilemap.GetUsedTilesCount() : 0;
    }

    private static void EnsureFolder(string folder)
    {
        string normalized = folder.Replace('\\', '/').TrimEnd('/');
        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int partIndex = 1; partIndex < parts.Length; partIndex++)
        {
            string next = $"{current}/{parts[partIndex]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[partIndex]);
            current = next;
        }
    }

    private static string SanitizeFileName(string value)
    {
        string sanitized = string.IsNullOrWhiteSpace(value) ? "CorridorModule" : value.Trim();
        char[] invalid = Path.GetInvalidFileNameChars();
        for (int charIndex = 0; charIndex < invalid.Length; charIndex++)
            sanitized = sanitized.Replace(invalid[charIndex], '_');
        return sanitized.Replace(' ', '_');
    }
}
