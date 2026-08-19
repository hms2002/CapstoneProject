using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임:
/// - 절차 생성용 방 조각 authoring 루트, 2칸 연결 소켓, 런타임 오브젝트 배치를 빠르게 만든다.
/// - Floor/Wall Tilemap과 몬스터·상자·포털·프롭 배치를 RoomTemplateSO로 bake하고 다시 편집 상태로 복원한다.
/// - 타일 페인팅은 Unity 기본 Tile Palette에 맡기고, 이 창은 생성/검증/데이터화 흐름만 담당한다.
/// </summary>
public sealed class RoomPieceEditorWindow : EditorWindow
{
    private const string DefaultOutputFolder = "Assets/_Project/Data/Dungeon/Rooms";
    private string newRoomId = "Room_New";
    private Vector2Int newRoomSize = new(12, 8);
    private RoomType newRoomType = RoomType.Combat;
    private int newDifficultyTier;
    private float newSelectionWeight = 1f;
    private string outputFolder = DefaultOutputFolder;
    [SerializeField] private RoomTemplateSO templateToLoad;
    [SerializeField] private RoomObjectKind objectKindToPlace = RoomObjectKind.Prop;
    [SerializeField] private GameObject objectPrefabToPlace;
    private RoomPieceAuthoring selectedAuthoring;
    private Vector2 scroll;
    private readonly List<string> validationMessages = new();

    [MenuItem("Tools/Dungeon/Room Piece Editor")]
    public static void Open()
    {
        GetWindow<RoomPieceEditorWindow>("Room Piece");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += DrawSocketSceneHandles;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawSocketSceneHandles;
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawCreateSection();
        EditorGUILayout.Space(10f);
        DrawLoadSection();
        EditorGUILayout.Space(10f);
        DrawSelectionSection();
        EditorGUILayout.Space(10f);
        DrawBakeSection();
        EditorGUILayout.EndScrollView();
    }

    private void DrawCreateSection()
    {
        EditorGUILayout.LabelField("Create Room Piece", EditorStyles.boldLabel);
        newRoomId = EditorGUILayout.TextField("Room Id", newRoomId);
        newRoomType = (RoomType)EditorGUILayout.EnumPopup("Room Type", newRoomType);
        newRoomSize = EditorGUILayout.Vector2IntField("Size", newRoomSize);
        newDifficultyTier = EditorGUILayout.IntField("Difficulty Tier", Mathf.Max(0, newDifficultyTier));
        newSelectionWeight = EditorGUILayout.FloatField("Selection Weight", Mathf.Max(0f, newSelectionWeight));

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newRoomId)))
        {
            if (GUILayout.Button("Create Authoring Room Piece"))
                CreateAuthoringRoomPiece();
        }

        EditorGUILayout.HelpBox(
            "타일 그리기는 이 창이 직접 구현하지 않습니다. 생성된 Floor/Wall Tilemap을 선택한 뒤 Unity Tile Palette/Tilemap Brush로 그리면 됩니다.",
            MessageType.Info);
    }

    private void DrawLoadSection()
    {
        EditorGUILayout.LabelField("Edit Existing Room Template", EditorStyles.boldLabel);

        if (Selection.activeObject is RoomTemplateSO selectedTemplate)
            templateToLoad = selectedTemplate;

        templateToLoad = EditorGUILayout.ObjectField(
            "Room Template",
            templateToLoad,
            typeof(RoomTemplateSO),
            false) as RoomTemplateSO;

        using (new EditorGUI.DisabledScope(templateToLoad == null))
        {
            if (GUILayout.Button("Load Template for Editing"))
                LoadTemplateForEditing();
        }

        EditorGUILayout.HelpBox(
            "선택한 에셋의 메타데이터, Floor/Wall 타일, 연결 소켓, 오브젝트 배치를 씬의 authoring 복사본으로 복원합니다. 원본 에셋은 Apply 전까지 변경되지 않습니다.",
            MessageType.Info);
    }

    private void DrawSelectionSection()
    {
        EditorGUILayout.LabelField("Selected Room Piece", EditorStyles.boldLabel);
        selectedAuthoring = EditorGUILayout.ObjectField(
            "Authoring",
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
        EditorGUILayout.PropertyField(serializedAuthoring.FindProperty("roomId"), new GUIContent("Room Id"));
        EditorGUILayout.PropertyField(serializedAuthoring.FindProperty("roomType"), new GUIContent("Room Type"));
        EditorGUILayout.PropertyField(serializedAuthoring.FindProperty("size"), new GUIContent("Size"));
        EditorGUILayout.PropertyField(
            serializedAuthoring.FindProperty("difficultyTier"),
            new GUIContent("Difficulty Tier"));
        EditorGUILayout.PropertyField(
            serializedAuthoring.FindProperty("selectionWeight"),
            new GUIContent("Selection Weight"));
        serializedAuthoring.ApplyModifiedProperties();

        EditorGUILayout.ObjectField(
            "Loaded Source",
            selectedAuthoring.SourceTemplate,
            typeof(RoomTemplateSO),
            false);
        EditorGUILayout.ObjectField("Grid", selectedAuthoring.Grid, typeof(Grid), true);
        EditorGUILayout.ObjectField("Floor", selectedAuthoring.FloorTilemap, typeof(Tilemap), true);
        EditorGUILayout.ObjectField("Wall", selectedAuthoring.WallTilemap, typeof(Tilemap), true);

        DrawSocketSection();
        DrawObjectSection();

        if (GUILayout.Button("Validate Selected Room Piece"))
            ValidateSelectedRoomPiece(showDialog: true);
    }

    private void DrawSocketSection()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Connection Sockets", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(selectedAuthoring.Grid == null))
        {
            if (GUILayout.Button("Add Connection Socket"))
                AddConnectionSocket();
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

                if (GUILayout.Button("Select Socket"))
                    Selection.activeObject = socket.gameObject;
            }
        }

        EditorGUILayout.HelpBox(
            "소켓은 표시된 시작 셀에서 오른쪽(Up/Down) 또는 위쪽(Left/Right)으로 2칸을 차지합니다. " +
            "소켓을 선택하면 Scene 뷰에 이동 핸들이 나타납니다.",
            MessageType.Info);
    }

    private void DrawObjectSection()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Room Objects", EditorStyles.boldLabel);
        objectKindToPlace = (RoomObjectKind)EditorGUILayout.EnumPopup(
            "Object Kind",
            objectKindToPlace);
        objectPrefabToPlace = EditorGUILayout.ObjectField(
            "Prefab",
            objectPrefabToPlace,
            typeof(GameObject),
            false) as GameObject;

        using (new EditorGUI.DisabledScope(
                   selectedAuthoring.Grid == null || objectPrefabToPlace == null))
        {
            if (GUILayout.Button("Place Object Prefab"))
                AddRoomObject();
        }

        RoomObjectAuthoring[] objects = GetRoomObjects(selectedAuthoring);
        if (objects.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "배치된 오브젝트가 없습니다. 몬스터, 상자, 포털 또는 일반 프롭 프리팹을 선택해 추가하세요.",
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
                    DrawLinkedChestLockPopup(
                        serializedObject.FindProperty("linkedChestLockPlacementId"),
                        objects);
                }
                serializedObject.ApplyModifiedProperties();

                if (roomObject.Kind == RoomObjectKind.Chest)
                    DrawChestKillLockLinks(roomObject, objects);

                EditorGUILayout.ObjectField("Prefab", roomObject.Prefab, typeof(GameObject), false);
                if (roomObject.TryGetPlacementData(out RoomObjectPlacementData placement))
                {
                    EditorGUILayout.Vector2IntField("Local Cell", placement.localCell);
                    EditorGUILayout.Vector2Field("Cell Offset", placement.localOffset);
                    EditorGUILayout.FloatField("Rotation", placement.localRotationDegrees);
                }

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
            "Monster는 런타임에서 MonsterSpawner를 통해 생성되며 선택한 Kill Lock 상자 연결도 스폰 요청으로 전달됩니다.",
            MessageType.Info);
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
        EditorGUILayout.LabelField("Save Room Template", EditorStyles.boldLabel);

        if (selectedAuthoring != null && selectedAuthoring.SourceTemplate != null)
        {
            EditorGUILayout.ObjectField(
                "Apply Target",
                selectedAuthoring.SourceTemplate,
                typeof(RoomTemplateSO),
                false);

            if (GUILayout.Button("Apply Changes to Loaded RoomTemplateSO"))
                ApplyChangesToLoadedTemplate();

            EditorGUILayout.Space(6f);
        }

        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

        using (new EditorGUI.DisabledScope(selectedAuthoring == null))
        {
            if (GUILayout.Button("Save as New RoomTemplateSO"))
                SaveAsNewRoomTemplate();
        }

        if (validationMessages.Count <= 0)
            return;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Last Validation", EditorStyles.boldLabel);
        for (int i = 0; i < validationMessages.Count; i++)
            EditorGUILayout.HelpBox(validationMessages[i], MessageType.Warning);
    }

    private RoomPieceAuthoring ResolveSelectedAuthoring()
    {
        if (Selection.activeGameObject == null)
            return selectedAuthoring;

        RoomPieceAuthoring authoring = Selection.activeGameObject.GetComponentInParent<RoomPieceAuthoring>();
        return authoring != null ? authoring : selectedAuthoring;
    }

    private void CreateAuthoringRoomPiece()
    {
        Vector2Int roomSize = new(Mathf.Max(1, newRoomSize.x), Mathf.Max(1, newRoomSize.y));

        selectedAuthoring = CreateAuthoringRoomPiece(
            newRoomId,
            newRoomType,
            roomSize,
            newDifficultyTier,
            newSelectionWeight,
            null);
        validationMessages.Clear();
    }

    private void LoadTemplateForEditing()
    {
        if (templateToLoad == null)
            return;

        RoomLayoutData layout = templateToLoad.LayoutData;
        RoomBuildData build = templateToLoad.BuildData;
        Vector2Int roomSize = ResolveTemplateSize(layout);
        string roomId = string.IsNullOrWhiteSpace(layout.roomId)
            ? templateToLoad.name
            : layout.roomId;

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Load Room Template for Editing");

        selectedAuthoring = CreateAuthoringRoomPiece(
            roomId,
            layout.roomType,
            roomSize,
            layout.difficultyTier,
            layout.selectionWeight,
            templateToLoad);

        RestoreTiles(selectedAuthoring.FloorTilemap, build.floorTiles);
        RestoreTiles(selectedAuthoring.WallTilemap, build.wallTiles);
        RestoreSockets(selectedAuthoring, layout.sockets);
        RestoreObjects(selectedAuthoring, build.objectPlacements);

        Undo.CollapseUndoOperations(undoGroup);
        validationMessages.Clear();
        Selection.activeObject = selectedAuthoring.gameObject;
        SceneView.RepaintAll();
        Repaint();
    }

    private static RoomPieceAuthoring CreateAuthoringRoomPiece(
        string roomId,
        RoomType roomType,
        Vector2Int roomSize,
        int difficultyTier,
        float selectionWeight,
        RoomTemplateSO sourceTemplate)
    {
        string rootName = sourceTemplate != null ? $"{roomId}_Editing" : roomId;

        GameObject root = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Room Piece");

        RoomPieceAuthoring authoring = root.AddComponent<RoomPieceAuthoring>();
        SerializedObject serializedAuthoring = new(authoring);
        serializedAuthoring.FindProperty("roomId").stringValue = roomId;
        serializedAuthoring.FindProperty("roomType").enumValueIndex = (int)roomType;
        serializedAuthoring.FindProperty("size").vector2IntValue = roomSize;
        serializedAuthoring.FindProperty("difficultyTier").intValue = Mathf.Max(0, difficultyTier);
        serializedAuthoring.FindProperty("selectionWeight").floatValue = Mathf.Max(0f, selectionWeight);
        serializedAuthoring.ApplyModifiedPropertiesWithoutUndo();

        GameObject gridObject = new GameObject("AuthoringGrid");
        Undo.RegisterCreatedObjectUndo(gridObject, "Create Room Piece Grid");
        gridObject.transform.SetParent(root.transform, false);
        Grid grid = gridObject.AddComponent<Grid>();

        Tilemap floor = CreateTilemapLayer(gridObject.transform, "Floor");
        Tilemap wall = CreateTilemapLayer(gridObject.transform, "Wall");

        authoring.EditorAssignTilemaps(grid, floor, wall);
        authoring.EditorAssignSourceTemplate(sourceTemplate);
        EditorUtility.SetDirty(authoring);

        Selection.activeObject = root;
        return authoring;
    }

    private void AddConnectionSocket()
    {
        selectedAuthoring = ResolveSelectedAuthoring();
        if (selectedAuthoring == null || selectedAuthoring.Grid == null)
            return;

        RoomSocketAuthoring[] existingSockets = GetSockets(selectedAuthoring);
        GetDefaultSocketPlacement(
            selectedAuthoring.Size,
            existingSockets.Length,
            out Vector2Int localCell,
            out RoomSocketDirection direction);

        string socketId = $"Socket_{existingSockets.Length + 1:00}";
        RoomSocketAuthoring socket = CreateConnectionSocket(
            selectedAuthoring,
            socketId,
            direction,
            localCell);

        Selection.activeObject = socket.gameObject;
        EditorUtility.SetDirty(socket);
        SceneView.RepaintAll();
        Repaint();
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

    private static Tilemap CreateTilemapLayer(Transform parent, string layerName)
    {
        GameObject layerObject = new GameObject(layerName);
        Undo.RegisterCreatedObjectUndo(layerObject, $"Create {layerName} Tilemap");
        layerObject.transform.SetParent(parent, false);

        Tilemap tilemap = layerObject.AddComponent<Tilemap>();
        TilemapRenderer renderer = layerObject.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = layerName == "Wall" ? 10 : 0;
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
            selectedAuthoring.Grid == null ||
            objectPrefabToPlace == null)
        {
            return;
        }

        RoomObjectAuthoring[] existingObjects = GetRoomObjects(selectedAuthoring);
        Vector2Int roomSize = selectedAuthoring.Size;
        Vector2Int defaultCell = new(
            Mathf.Clamp(roomSize.x / 2, 0, Mathf.Max(0, roomSize.x - 1)),
            Mathf.Clamp(roomSize.y / 2, 0, Mathf.Max(0, roomSize.y - 1)));
        RoomObjectPlacementData placement = new()
        {
            placementId = $"Object_{existingObjects.Length + 1:00}",
            kind = objectKindToPlace,
            prefab = objectPrefabToPlace,
            localCell = defaultCell,
            localOffset = Vector2.zero,
            localRotationDegrees = 0f,
            localScale = objectPrefabToPlace.transform.localScale
        };

        RoomObjectAuthoring roomObject = CreateRoomObjectAuthoring(selectedAuthoring, placement);
        Selection.activeObject = roomObject.gameObject;
        EditorUtility.SetDirty(roomObject);
        SceneView.RepaintAll();
        Repaint();
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

        marker.EditorConfigure(placement.placementId, placement.kind, placement.prefab);
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

        if (selectedAuthoring.Grid == null)
            validationMessages.Add("Grid 참조가 비어 있습니다.");

        if (selectedAuthoring.FloorTilemap == null)
            validationMessages.Add("Floor Tilemap 참조가 비어 있습니다.");

        if (selectedAuthoring.WallTilemap == null)
            validationMessages.Add("Wall Tilemap 참조가 비어 있습니다.");

        if (selectedAuthoring.Size.x <= 0 || selectedAuthoring.Size.y <= 0)
            validationMessages.Add("Size는 1 이상의 값이어야 합니다.");

        int floorCount = CountTiles(selectedAuthoring.FloorTilemap, selectedAuthoring.Size, out int floorOutsideCount);
        int wallCount = CountTiles(selectedAuthoring.WallTilemap, selectedAuthoring.Size, out int wallOutsideCount);

        if (floorCount + wallCount <= 0)
            validationMessages.Add("방 bounds 안에 Floor 또는 Wall 타일이 하나 이상 필요합니다.");

        ValidateSockets(selectedAuthoring, validationMessages);
        ValidateObjectPlacements(selectedAuthoring, validationMessages);

        bool valid = validationMessages.Count == 0;

        if (floorOutsideCount > 0)
            validationMessages.Add($"Floor Tilemap에 방 bounds 밖 타일 {floorOutsideCount}개가 있습니다. v0 bake에서는 제외됩니다.");

        if (wallOutsideCount > 0)
            validationMessages.Add($"Wall Tilemap에 방 bounds 밖 타일 {wallOutsideCount}개가 있습니다. v0 bake에서는 제외됩니다.");

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

    private void SaveAsNewRoomTemplate()
    {
        if (!TryCollectSelectedRoomData(out RoomLayoutData layout, out RoomBuildData build))
            return;

        EnsureFolder(outputFolder);

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{outputFolder.TrimEnd('/', '\\')}/{selectedAuthoring.RoomId}.asset");

        RoomTemplateSO template = CreateInstance<RoomTemplateSO>();
        template.EditorSetData(layout, build);
        AssetDatabase.CreateAsset(template, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Undo.RecordObject(selectedAuthoring, "Link Room Authoring Source Template");
        selectedAuthoring.EditorAssignSourceTemplate(template);
        EditorUtility.SetDirty(selectedAuthoring);
        templateToLoad = template;
        Selection.activeObject = template;
        EditorUtility.DisplayDialog("Save Complete", $"RoomTemplateSO 생성 완료:\n{assetPath}", "OK");
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
        AssetDatabase.SaveAssets();

        templateToLoad = targetTemplate;
        Selection.activeObject = targetTemplate;
        EditorUtility.DisplayDialog("Apply Complete", $"RoomTemplateSO 갱신 완료:\n{assetPath}", "OK");
    }

    private bool TryCollectSelectedRoomData(out RoomLayoutData layout, out RoomBuildData build)
    {
        layout = default;
        build = default;

        if (!ValidateSelectedRoomPiece(showDialog: false))
        {
            EditorUtility.DisplayDialog(
                "Save Failed",
                string.Join("\n", validationMessages),
                "OK");
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
            selectionWeight = selectedAuthoring.SelectionWeight
        };

        build = new RoomBuildData
        {
            floorTiles = CollectTiles(selectedAuthoring.FloorTilemap, selectedAuthoring.Size),
            wallTiles = CollectTiles(selectedAuthoring.WallTilemap, selectedAuthoring.Size),
            objectPlacements = CollectObjectPlacements(selectedAuthoring)
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

            if (roomObject.Prefab == null)
                messages.Add($"{displayName}: 원본 Prefab 참조가 비어 있습니다.");
            else if (!IsPrefabCompatibleWithKind(roomObject.Prefab, roomObject.Kind))
                messages.Add($"{displayName}: Prefab이 {roomObject.Kind} 종류에 필요한 컴포넌트를 포함하지 않습니다.");

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

    private static void GetDefaultSocketPlacement(
        Vector2Int size,
        int socketIndex,
        out Vector2Int localCell,
        out RoomSocketDirection direction)
    {
        int normalizedIndex = socketIndex % 4;
        int horizontalStart = Mathf.Max(0, (size.x - RoomSocketGeometry.RequiredWidth) / 2);
        int verticalStart = Mathf.Max(0, (size.y - RoomSocketGeometry.RequiredWidth) / 2);
        switch (normalizedIndex)
        {
            case 0:
                direction = RoomSocketDirection.Up;
                localCell = new Vector2Int(horizontalStart, size.y - 1);
                break;
            case 1:
                direction = RoomSocketDirection.Right;
                localCell = new Vector2Int(size.x - 1, verticalStart);
                break;
            case 2:
                direction = RoomSocketDirection.Down;
                localCell = new Vector2Int(horizontalStart, 0);
                break;
            default:
                direction = RoomSocketDirection.Left;
                localCell = new Vector2Int(0, verticalStart);
                break;
        }
    }

    private void DrawSocketSceneHandles(SceneView sceneView)
    {
        RoomPieceAuthoring authoring = ResolveSelectedAuthoring();
        if (authoring == null || authoring.Grid == null)
            return;

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

    private static void EnsureFolder(string folder)
    {
        string normalized = folder.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(normalized))
            return;

        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }

        if (!Directory.Exists(normalized))
            AssetDatabase.Refresh();
    }
}
