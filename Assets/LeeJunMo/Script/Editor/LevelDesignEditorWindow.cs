using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public sealed class LevelDesignEditorWindow : EditorWindow
{
    private const string DoorPrefabPath = "Assets/LeeJunMo/Prefab/Map/ShortCut/Door.prefab";
    private const string LeverPrefabPath = "Assets/LeeJunMo/Prefab/Map/ShortCut/Lever.prefab";
    private const string StatuePrefabPath = "Assets/LeeJunMo/Prefab/Map/ShortCut/Statue.prefab";
    private const string ChestPrefabPath = "Assets/HeoMinSeok/_Project/Prefabs/Gameplay/Items/TreasureChest.prefab";
    private const string KillLockChestPrefabPath = "Assets/HeoMinSeok/_Project/Prefabs/Gameplay/Items/KillLockTresureChest.prefab";
    private const string PortalPrefabPath = "Assets/LeeJunMo/Prefab/Map/Portal/ScenePortal.prefab";
    private const string MonsterPrefabRoot = "Assets/Prefabs/Enemies/Mobs";
    private const string MonsterPrefabDragKey = "LevelDesignEditor.MonsterPrefab";
    private const string StageMonsterSetDragKey = "LevelDesignEditor.StageMonsterSet";
    private const string ObjectPlacementDragKey = "LevelDesignEditor.ObjectPlacement";
    private const string LevelDesignRootName = "LevelDesignRoot";
    private const float PolygonEdgeInsertPixelThreshold = 14f;
    private const float PolygonVertexClickExclusionPixels = 13f;
    private const float PolygonVertexSelectionPixelThreshold = 16f;

    private enum ToolMode
    {
        Review,
        Link,
        BattleRoom,
        Place,
        Options
    }

    private enum SearchScope
    {
        ActiveScene,
        LoadedScenes
    }

    private enum PlacementKind
    {
        None,
        Door,
        Lever,
        Statue,
        Chest,
        KillLockChest,
        Portal,
        MonsterSpawn
    }

    private enum MonsterSpawnPlacementSource
    {
        FixedPrefab,
        StageMonsterSet
    }

    private enum RoomDrawShape
    {
        Rectangle,
        Polygon
    }

    private enum DoorPlacementOrientation
    {
        Auto,
        Horizontal,
        Vertical
    }

    private enum Severity
    {
        Info,
        Warning,
        Error
    }

    private sealed class ValidationResult
    {
        public Severity SeverityLevel;
        public string Message;
        public Object Context;
        public string ObjectPath;
    }

    private ToolMode mode;
    private SearchScope searchScope;
    private PlacementKind placementKind = PlacementKind.MonsterSpawn;
    private readonly List<ValidationResult> validationResults = new();
    private readonly List<GameObject> monsterPrefabs = new();
    private readonly List<StageMonsterSetSO> stageMonsterSets = new();
    private readonly Dictionary<string, bool> monsterFolderFoldouts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> stageMonsterSetFolderFoldouts = new(StringComparer.Ordinal);

    private DoorObject[] doors = Array.Empty<DoorObject>();
    private ShortcutBase[] shortcuts = Array.Empty<ShortcutBase>();
    private MonsterSpawnRoomGroup[] roomGroups = Array.Empty<MonsterSpawnRoomGroup>();
    private MonsterRoomArea2D[] roomAreas = Array.Empty<MonsterRoomArea2D>();
    private MonsterSpawnContainer[] spawnContainers = Array.Empty<MonsterSpawnContainer>();
    private TreasureChest[] chests = Array.Empty<TreasureChest>();
    private ChestMonsterKillLock[] chestLocks = Array.Empty<ChestMonsterKillLock>();
    private RoomDoorMonsterKillLock[] doorLocks = Array.Empty<RoomDoorMonsterKillLock>();
    private ScenePortal[] portals = Array.Empty<ScenePortal>();

    private Vector2 scrollPosition;
    private Vector2 monsterPaletteScroll;
    private ShortcutBase linkingShortcut;
    private ChestMonsterKillLock linkingChestLock;
    private MonsterSpawnContainer linkingSpawn;
    private MonsterSpawnRoomGroup linkingRoomGroup;
    private DoorObject linkingDoor;
    private MonsterSpawnRoomGroup selectedRoomGroup;
    private PolygonCollider2D selectedPolygonVertexCollider;
    private GameObject selectedMonsterPrefab;
    private StageMonsterSetSO selectedStageMonsterSet;
    private MonsterSpawnPlacementSource selectedMonsterSpawnSource;
    private GameObject doorPrefab;
    private GameObject leverPrefab;
    private GameObject statuePrefab;
    private GameObject chestPrefab;
    private GameObject killLockChestPrefab;
    private GameObject portalPrefab;
    private string monsterSearch = string.Empty;
    private float gridSize = 1f;
    private bool snapToGrid = true;
    private bool showGrid = true;
    private bool showLabels = true;
    private bool showValidationMarkers = true;
    private bool autoNavigateFromScene = true;
    private bool showPlacementPrefabSettings;
    private bool drawRoomMode;
    private bool isDraggingRoom;
    private bool editRoomGridMode;
    private int selectedPolygonVertexIndex = -1;
    private RoomDrawShape roomDrawShape;
    private DoorPlacementOrientation doorPlacementOrientation;
    private Vector3 roomDragStart;
    private Vector3 roomDragCurrent;
    private readonly List<Vector3> roomPolygonPoints = new();
    private Vector3 roomPolygonPreviewPoint;
    private Vector2 defaultRoomSize = new(12f, 8f);

    [MenuItem("Tools/Level Design/Level Design Editor")]
    public static void OpenWindow()
    {
        LevelDesignEditorWindow window = GetWindow<LevelDesignEditorWindow>("Level Design");
        window.minSize = new Vector2(680f, 520f);
        window.RefreshAll();
    }

    private void OnEnable()
    {
        LoadDefaultPrefabs();
        RefreshAll();
        SceneView.duringSceneGui += DrawSceneView;
        Selection.selectionChanged += Repaint;
        EditorSceneManager.sceneOpened += HandleSceneOpened;
        EditorSceneManager.sceneClosed += HandleSceneClosed;
        EditorSceneManager.activeSceneChangedInEditMode += HandleActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawSceneView;
        Selection.selectionChanged -= Repaint;
        EditorSceneManager.sceneOpened -= HandleSceneOpened;
        EditorSceneManager.sceneClosed -= HandleSceneClosed;
        EditorSceneManager.activeSceneChangedInEditMode -= HandleActiveSceneChanged;
    }

    private void OnGUI()
    {
        HandleCancelLinkInput(Event.current);
        DrawHeader();
        DrawToolbar();
        DrawContextPanel();
        DrawSummary();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        switch (mode)
        {
            case ToolMode.Review:
                DrawReviewTab();
                break;
            case ToolMode.Link:
                DrawLinkTab();
                break;
            case ToolMode.BattleRoom:
                DrawBattleRoomTab();
                break;
            case ToolMode.Place:
                DrawPlaceTab();
                break;
            case ToolMode.Options:
                DrawOptionsTab();
                break;
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                RefreshAll();

            if (GUILayout.Button("검사", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                ValidateActiveScope();

            if (GUILayout.Button("문 ID 정리", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                FixMissingAndDuplicateDoorIds();

            GUILayout.Space(8f);

            EditorGUI.BeginChangeCheck();
            searchScope = (SearchScope)EditorGUILayout.Popup(
                (int)searchScope,
                new[] { "현재 씬", "로드된 씬" },
                EditorStyles.toolbarPopup,
                GUILayout.Width(96f));
            if (EditorGUI.EndChangeCheck())
                RefreshAll();

            if (HasActiveLinkSource() && GUILayout.Button("연결 취소", EditorStyles.toolbarButton, GUILayout.Width(78f)))
                ClearLinkSources();

            GUILayout.FlexibleSpace();

            mode = (ToolMode)GUILayout.Toolbar(
                (int)mode,
                new[] { "검사", "연결", "방", "배치", "속성" },
                EditorStyles.toolbarButton,
                GUILayout.Width(310f));
        }

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            showGrid = GUILayout.Toggle(showGrid, "그리드", EditorStyles.toolbarButton, GUILayout.Width(62f));
            showLabels = GUILayout.Toggle(showLabels, "이름표", EditorStyles.toolbarButton, GUILayout.Width(62f));
            showValidationMarkers = GUILayout.Toggle(showValidationMarkers, "문제", EditorStyles.toolbarButton, GUILayout.Width(54f));
            autoNavigateFromScene = GUILayout.Toggle(autoNavigateFromScene, "점 클릭 이동", EditorStyles.toolbarButton, GUILayout.Width(92f));

            GUILayout.FlexibleSpace();
            GUILayout.Label(GetSceneHint(), EditorStyles.miniLabel);
        }
    }

    private void DrawHeader()
    {
        Rect rect = GUILayoutUtility.GetRect(0f, 58f, GUILayout.ExpandWidth(true));
        Color background = EditorGUIUtility.isProSkin
            ? new Color(0.1f, 0.13f, 0.16f, 1f)
            : new Color(0.72f, 0.78f, 0.84f, 1f);
        Color accent = new Color(0.18f, 0.55f, 0.95f, 1f);
        EditorGUI.DrawRect(rect, background);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), accent);

        GUIStyle titleStyle = new(EditorStyles.boldLabel)
        {
            fontSize = 16,
            normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : new Color(0.08f, 0.1f, 0.13f, 1f) }
        };
        GUIStyle subtitleStyle = new(EditorStyles.miniLabel)
        {
            normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.78f, 0.86f, 0.92f, 1f) : new Color(0.16f, 0.22f, 0.28f, 1f) }
        };

        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 22f), "레벨 디자인 에디터", titleStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 31f, rect.width - 24f, 18f), "씬의 점을 클릭해서 검사, 연결, 방 설정, 몬스터 배치를 바로 처리합니다.", subtitleStyle);
    }

    private void DrawSummary()
    {
        int errors = validationResults.Count(result => result.SeverityLevel == Severity.Error);
        int warnings = validationResults.Count(result => result.SeverityLevel == Severity.Warning);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                $"대상: {GetScopeLabel()}   문 {doors.Length} | 숏컷 {shortcuts.Length} | 방 {roomGroups.Length} | 스폰 {spawnContainers.Length} | 상자 {chests.Length} | 포탈 {portals.Length}",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"검사 결과: 오류 {errors}개, 경고 {warnings}개");
        }
    }

    private void DrawContextPanel()
    {
        Component context = ResolveSelectedLevelDesignComponent();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("선택", EditorStyles.boldLabel, GUILayout.Width(44f));
                if (context == null)
                {
                    EditorGUILayout.LabelField("SceneView에서 점 또는 맵 오브젝트를 클릭하세요.");
                    return;
                }

                EditorGUILayout.ObjectField(context, context.GetType(), true);
                if (GUILayout.Button("보기", GUILayout.Width(52f)))
                    FrameInSceneView(context);
            }

            DrawActiveLinkPanel();
            DrawContextActions(context);
        }
    }

    private void DrawContextActions(Component context)
    {
        if (context == null)
            return;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (context is ShortcutBase shortcut)
            {
                if (GUILayout.Button("문 연결 시작"))
                    BeginShortcutLink(shortcut);

                using (new EditorGUI.DisabledScope(shortcut.TargetDoor == null))
                {
                    if (GUILayout.Button("대상 문 선택"))
                    {
                        Selection.activeObject = shortcut.TargetDoor;
                        mode = ToolMode.Options;
                    }
                }
            }
            else if (context is DoorObject door)
            {
                using (new EditorGUI.DisabledScope(linkingShortcut == null))
                {
                    if (GUILayout.Button("숏컷 연결 완료"))
                        LinkShortcutToDoor(linkingShortcut, door);
                }

                using (new EditorGUI.DisabledScope(linkingRoomGroup == null))
                {
                    if (GUILayout.Button("방-문 연결 완료"))
                        LinkRoomToDoor(linkingRoomGroup, door);
                }

                if (GUILayout.Button("이 문으로 방 연결"))
                    BeginDoorRoomLink(door);

                if (GUILayout.Button("문 속성"))
                    mode = ToolMode.Options;

                using (new EditorGUI.DisabledScope(selectedRoomGroup == null))
                {
                    if (GUILayout.Button("선택 방과 문락 연결"))
                        LinkRoomToDoor(selectedRoomGroup, door);
                }
            }
            else if (context is MonsterSpawnRoomGroup roomGroup)
            {
                if (GUILayout.Button("방 도구 열기"))
                {
                    selectedRoomGroup = roomGroup;
                    mode = ToolMode.BattleRoom;
                }

                if (GUILayout.Button("문 연결 시작"))
                    BeginRoomDoorLink(roomGroup);

                if (GUILayout.Button("방 자동 연결"))
                    AutoWireRoom(roomGroup);
            }
            else if (context is MonsterSpawnContainer spawn)
            {
                using (new EditorGUI.DisabledScope(linkingChestLock == null))
                {
                    if (GUILayout.Button("상자락 연결 완료"))
                        LinkSpawnToChestLock(spawn, linkingChestLock);
                }

                if (GUILayout.Button("이 스폰으로 상자 연결"))
                    BeginSpawnChestLink(spawn);

                if (GUILayout.Button("스폰 속성"))
                    mode = ToolMode.Options;

                using (new EditorGUI.DisabledScope(!TryUseSpawnSourceForPlacement(spawn)))
                {
                    if (GUILayout.Button("스폰 설정 배치에 사용"))
                        UseSpawnSourceForPlacement(spawn);
                }
            }
            else if (context is ChestMonsterKillLock chestLock)
            {
                using (new EditorGUI.DisabledScope(linkingSpawn == null))
                {
                    if (GUILayout.Button("몬스터 연결 완료"))
                        LinkSpawnToChestLock(linkingSpawn, chestLock);
                }

                if (GUILayout.Button("몬스터 연결 시작"))
                    BeginChestLockLink(chestLock);

                if (GUILayout.Button("상자락 속성"))
                    mode = ToolMode.Options;

                using (new EditorGUI.DisabledScope(selectedRoomGroup == null))
                {
                    if (GUILayout.Button("선택 방 스폰 전부 연결"))
                        WireSpawnsInRoomToChestLock(selectedRoomGroup, chestLock);
                }
            }
            else if (context is TreasureChest)
            {
                if (GUILayout.Button("상자 속성"))
                    mode = ToolMode.Options;
            }
            else if (context is RoomDoorMonsterKillLock doorLock)
            {
                if (GUILayout.Button("문락 속성"))
                    mode = ToolMode.Options;

                using (new EditorGUI.DisabledScope(linkingRoomGroup == null))
                {
                    if (GUILayout.Button("방 연결 완료"))
                        LinkRoomToDoorLock(linkingRoomGroup, doorLock);
                }

                using (new EditorGUI.DisabledScope(linkingDoor == null))
                {
                    if (GUILayout.Button("문 연결 완료"))
                        LinkDoorToDoorLock(linkingDoor, doorLock);
                }

                MonsterSpawnRoomGroup linkedRoomGroup = ReadReference<MonsterSpawnRoomGroup>(doorLock, "targetRoomGroup");
                using (new EditorGUI.DisabledScope(linkedRoomGroup == null))
                {
                    if (GUILayout.Button("방 선택"))
                    {
                        selectedRoomGroup = linkedRoomGroup;
                        Selection.activeObject = linkedRoomGroup;
                        mode = ToolMode.BattleRoom;
                    }
                }
            }
            else if (context is ScenePortal)
            {
                if (GUILayout.Button("포탈 속성"))
                    mode = ToolMode.Options;
            }
        }
    }

    private void DrawActiveLinkPanel()
    {
        if (!HasActiveLinkSource())
            return;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(GetSceneHint(), EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("연결 취소", GUILayout.Width(78f)))
                ClearLinkSources();
        }
    }

    private bool HasActiveLinkSource()
    {
        return linkingShortcut != null ||
               linkingChestLock != null ||
               linkingSpawn != null ||
               linkingRoomGroup != null ||
               linkingDoor != null;
    }

    private void ClearLinkSources()
    {
        linkingShortcut = null;
        linkingChestLock = null;
        linkingSpawn = null;
        linkingRoomGroup = null;
        linkingDoor = null;
        Repaint();
        SceneView.RepaintAll();
    }

    private void BeginShortcutLink(ShortcutBase shortcut)
    {
        ClearLinkSources();
        if (shortcut == null)
            return;

        linkingShortcut = shortcut;
        mode = ToolMode.Link;
        Selection.activeObject = shortcut;
        ShowNotification(new GUIContent("숏컷 선택됨. 문 점을 클릭해 연결하세요."));
        Repaint();
        SceneView.RepaintAll();
    }

    private void BeginChestLockLink(ChestMonsterKillLock chestLock)
    {
        ClearLinkSources();
        if (chestLock == null)
            return;

        linkingChestLock = chestLock;
        mode = ToolMode.Link;
        Selection.activeObject = chestLock;
        ShowNotification(new GUIContent("상자 킬락 선택됨. 몬스터 스폰 점을 클릭해 연결하세요."));
        Repaint();
        SceneView.RepaintAll();
    }

    private void BeginSpawnChestLink(MonsterSpawnContainer spawn)
    {
        ClearLinkSources();
        if (spawn == null)
            return;

        linkingSpawn = spawn;
        mode = ToolMode.Link;
        Selection.activeObject = spawn;
        ShowNotification(new GUIContent("몬스터 스폰 선택됨. 상자 킬락 점을 클릭해 연결하세요."));
        Repaint();
        SceneView.RepaintAll();
    }

    private void BeginRoomDoorLink(MonsterSpawnRoomGroup roomGroup)
    {
        ClearLinkSources();
        if (roomGroup == null)
            return;

        linkingRoomGroup = roomGroup;
        selectedRoomGroup = roomGroup;
        mode = ToolMode.Link;
        Selection.activeObject = roomGroup;
        ShowNotification(new GUIContent("방 선택됨. 문 점을 클릭해 Door KillLock을 연결하세요."));
        Repaint();
        SceneView.RepaintAll();
    }

    private void BeginDoorRoomLink(DoorObject door)
    {
        ClearLinkSources();
        if (door == null)
            return;

        linkingDoor = door;
        mode = ToolMode.Link;
        Selection.activeObject = door;
        ShowNotification(new GUIContent("문 선택됨. 방 점을 클릭해 Door KillLock을 연결하세요."));
        Repaint();
        SceneView.RepaintAll();
    }

    private string GetSceneHint()
    {
        if (linkingShortcut != null)
            return $"연결: {linkingShortcut.name} -> 문 점 클릭";

        if (linkingChestLock != null)
            return $"연결: {linkingChestLock.name} -> 몬스터 스폰 점 클릭";

        if (linkingSpawn != null)
            return $"연결: {linkingSpawn.name} -> 상자 킬락 점 클릭";

        if (linkingRoomGroup != null)
            return $"연결: {linkingRoomGroup.name} -> 문 점 클릭";

        if (linkingDoor != null)
            return $"연결: {linkingDoor.name} -> 방 점 클릭";

        if (mode == ToolMode.Place)
            return $"배치: 빈 SceneView 공간 클릭 - {placementKind}";

        if (mode == ToolMode.BattleRoom && drawRoomMode)
            return "방: SceneView에서 드래그해 범위 생성";

        return autoNavigateFromScene
            ? "SceneView 점 클릭으로 기능 이동/연결"
            : "점 클릭 이동 꺼짐";
    }

    private Component ResolveSelectedLevelDesignComponent()
    {
        if (Selection.activeObject is Component directComponent)
            return NormalizeLevelDesignComponent(directComponent);

        GameObject selected = Selection.activeGameObject;
        if (selected == null)
            return null;

        return ResolveLevelDesignComponent(selected);
    }

    private Component ResolveLevelDesignComponent(GameObject gameObject)
    {
        if (gameObject == null)
            return null;

        ShortcutBase shortcut = gameObject.GetComponentInParent<ShortcutBase>(true);
        if (shortcut != null)
            return shortcut;

        DoorObject door = gameObject.GetComponentInParent<DoorObject>(true);
        if (door != null)
            return door;

        MonsterSpawnContainer spawn = gameObject.GetComponentInParent<MonsterSpawnContainer>(true);
        if (spawn != null)
            return spawn;

        ChestMonsterKillLock chestLock = gameObject.GetComponentInParent<ChestMonsterKillLock>(true);
        if (chestLock != null)
            return chestLock;

        TreasureChest chest = gameObject.GetComponentInParent<TreasureChest>(true);
        if (chest != null)
            return chest;

        RoomDoorMonsterKillLock doorLock = gameObject.GetComponentInParent<RoomDoorMonsterKillLock>(true);
        if (doorLock != null)
            return doorLock;

        MonsterSpawnRoomGroup roomGroup = gameObject.GetComponentInParent<MonsterSpawnRoomGroup>(true);
        if (roomGroup != null)
            return roomGroup;

        ScenePortal portal = gameObject.GetComponentInParent<ScenePortal>(true);
        return portal != null ? portal : null;
    }

    private Component NormalizeLevelDesignComponent(Component component)
    {
        if (component == null)
            return null;

        if (component is ShortcutBase shortcut)
            return shortcut;

        if (component is DoorObject door)
            return door;

        if (component is MonsterSpawnRoomGroup roomGroup)
            return roomGroup;

        if (component is MonsterSpawnContainer spawn)
            return spawn;

        if (component is ChestMonsterKillLock chestLock)
            return chestLock;

        if (component is TreasureChest chest)
        {
            ChestMonsterKillLock lockOnChest = chest.GetComponent<ChestMonsterKillLock>();
            return lockOnChest != null ? lockOnChest : chest;
        }

        if (component is RoomDoorMonsterKillLock doorLock)
            return doorLock;

        if (component is ScenePortal portal)
            return portal;

        ShortcutBase parentShortcut = component.GetComponentInParent<ShortcutBase>(true);
        if (parentShortcut != null)
            return parentShortcut;

        DoorObject parentDoor = component.GetComponentInParent<DoorObject>(true);
        if (parentDoor != null)
            return parentDoor;

        MonsterSpawnContainer parentSpawn = component.GetComponentInParent<MonsterSpawnContainer>(true);
        if (parentSpawn != null)
            return parentSpawn;

        ChestMonsterKillLock parentChestLock = component.GetComponentInParent<ChestMonsterKillLock>(true);
        if (parentChestLock != null)
            return parentChestLock;

        TreasureChest parentChest = component.GetComponentInParent<TreasureChest>(true);
        if (parentChest != null)
            return parentChest;

        RoomDoorMonsterKillLock parentDoorLock = component.GetComponentInParent<RoomDoorMonsterKillLock>(true);
        if (parentDoorLock != null)
            return parentDoorLock;

        MonsterSpawnRoomGroup parentRoom = component.GetComponentInParent<MonsterSpawnRoomGroup>(true);
        if (parentRoom != null)
            return parentRoom;

        ScenePortal parentPortal = component.GetComponentInParent<ScenePortal>(true);
        return parentPortal != null ? parentPortal : null;
    }

    private void NavigateFromSceneComponent(Component component, bool allowLinking)
    {
        Component context = NormalizeLevelDesignComponent(component);
        if (context == null)
            return;

        Selection.activeObject = context;

        if (context is ShortcutBase shortcut)
        {
            BeginShortcutLink(shortcut);
        }
        else if (context is DoorObject door)
        {
            if (allowLinking && linkingShortcut != null)
            {
                LinkShortcutToDoor(linkingShortcut, door);
                ShowNotification(new GUIContent("숏컷과 문을 연결했습니다."));
            }
            else if (allowLinking && linkingRoomGroup != null)
            {
                LinkRoomToDoor(linkingRoomGroup, door);
                ShowNotification(new GUIContent("방과 문 KillLock을 연결했습니다."));
            }
            else
            {
                BeginDoorRoomLink(door);
            }
        }
        else if (context is MonsterSpawnRoomGroup roomGroup)
        {
            if (allowLinking && linkingDoor != null)
            {
                LinkRoomToDoor(roomGroup, linkingDoor);
                ShowNotification(new GUIContent("문과 방 KillLock을 연결했습니다."));
            }
            else
            {
                BeginRoomDoorLink(roomGroup);
            }
        }
        else if (context is MonsterSpawnContainer spawn)
        {
            UseSpawnSourceForPlacement(spawn, switchToPlace: false);

            if (allowLinking && linkingChestLock != null)
            {
                LinkSpawnToChestLock(spawn, linkingChestLock);
                ShowNotification(new GUIContent("몬스터 스폰과 상자 킬락을 연결했습니다."));
            }
            else
            {
                BeginSpawnChestLink(spawn);
            }
        }
        else if (context is ChestMonsterKillLock chestLock)
        {
            if (allowLinking && linkingSpawn != null)
            {
                LinkSpawnToChestLock(linkingSpawn, chestLock);
                ShowNotification(new GUIContent("스폰과 상자 킬락을 연결했습니다."));
            }
            else
            {
                selectedRoomGroup ??= FindRoomGroupAt(context.transform.position);
                BeginChestLockLink(chestLock);
            }
        }
        else if (context is TreasureChest)
        {
            mode = ToolMode.Options;
        }
        else if (context is RoomDoorMonsterKillLock doorLock)
        {
            DoorObject lockedDoor = ReadReference<DoorObject>(doorLock, "targetDoor") ??
                                    doorLock.GetComponent<DoorObject>() ??
                                    doorLock.GetComponentInParent<DoorObject>(true);
            if (allowLinking && linkingShortcut != null && lockedDoor != null)
            {
                LinkShortcutToDoor(linkingShortcut, lockedDoor);
                ShowNotification(new GUIContent("숏컷과 문을 연결했습니다."));
            }
            else if (allowLinking && linkingRoomGroup != null)
            {
                LinkRoomToDoorLock(linkingRoomGroup, doorLock);
                ShowNotification(new GUIContent("방과 문 KillLock을 연결했습니다."));
            }
            else if (allowLinking && linkingDoor != null)
            {
                LinkDoorToDoorLock(linkingDoor, doorLock);
                ShowNotification(new GUIContent("문과 Door KillLock을 연결했습니다."));
            }
            else
            {
                selectedRoomGroup = ReadReference<MonsterSpawnRoomGroup>(doorLock, "targetRoomGroup") ?? selectedRoomGroup;
                mode = ToolMode.Options;
            }
        }
        else if (context is ScenePortal)
        {
            mode = ToolMode.Options;
        }

        Repaint();
        SceneView.RepaintAll();
    }

    private static void FrameInSceneView(Component component)
    {
        if (component == null || SceneView.lastActiveSceneView == null)
            return;

        Bounds bounds = new(component.transform.position, Vector3.one * 3f);
        SceneView.lastActiveSceneView.Frame(bounds, false);
    }

    private void DrawReviewTab()
    {
        EditorGUILayout.LabelField("검사 결과", EditorStyles.boldLabel);
        if (validationResults.Count == 0)
        {
            EditorGUILayout.HelpBox("검사 결과가 없습니다. 검사 또는 새로고침을 실행하세요.", MessageType.Info);
            return;
        }

        foreach (ValidationResult result in validationResults)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{result.SeverityLevel}: {result.Message}", EditorStyles.wordWrappedLabel);
                if (!string.IsNullOrEmpty(result.ObjectPath))
                    EditorGUILayout.LabelField("오브젝트", result.ObjectPath);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(result.Context == null))
                    {
                        if (GUILayout.Button("핑", GUILayout.Width(48f)))
                            EditorGUIUtility.PingObject(result.Context);

                        if (GUILayout.Button("선택", GUILayout.Width(56f)))
                            Selection.activeObject = result.Context;
                    }
                }
            }
        }
    }

    private void DrawLinkTab()
    {
        EditorGUILayout.LabelField("클릭 연결", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("점 클릭 순서: 숏컷 -> 문, 상자 킬락 -> 몬스터 스폰, 방 -> 문. 반대로 문 -> 방, 몬스터 -> 상자도 연결됩니다.", MessageType.Info);
        DrawActiveLinkPanel();

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.ObjectField("활성 숏컷", linkingShortcut, typeof(ShortcutBase), true);
            if (GUILayout.Button("선택 사용", GUILayout.Width(88f)))
                BeginShortcutLink(ResolveSelection<ShortcutBase>());
            if (GUILayout.Button("해제", GUILayout.Width(48f)))
                ClearLinkSources();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("숏컷 연결 목록", EditorStyles.boldLabel);
        foreach (ShortcutBase shortcut in shortcuts.OrderBy(shortcut => shortcut.name))
        {
            if (shortcut == null)
                continue;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.ObjectField(shortcut, typeof(ShortcutBase), true);
                EditorGUILayout.ObjectField(shortcut.TargetDoor, typeof(DoorObject), true, GUILayout.Width(170f));

                if (GUILayout.Button("연결", GUILayout.Width(52f)))
                {
                    BeginShortcutLink(shortcut);
                }
            }
        }
    }

    private void DrawBattleRoomTab()
    {
        EditorGUILayout.LabelField("배틀룸 설정", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("SceneView에서 방을 그리거나 기존 방을 선택하세요. 자동 연결은 방 범위 안의 기존 오브젝트만 연결합니다.", MessageType.Info);

        selectedRoomGroup = EditorGUILayout.ObjectField("선택된 방", selectedRoomGroup, typeof(MonsterSpawnRoomGroup), true) as MonsterSpawnRoomGroup;
        defaultRoomSize = EditorGUILayout.Vector2Field("기본 방 크기", defaultRoomSize);
        EditorGUI.BeginChangeCheck();
        roomDrawShape = (RoomDrawShape)EditorGUILayout.Popup(
            "방 그리기 방식",
            (int)roomDrawShape,
            new[] { "사각형 드래그", "다각형 점 찍기" });
        if (EditorGUI.EndChangeCheck())
            ClearRoomDrawingState();

        using (new EditorGUILayout.HorizontalScope())
        {
            bool nextDrawRoomMode = GUILayout.Toggle(drawRoomMode, drawRoomMode ? "방 그리는 중..." : "SceneView에서 방 그리기", EditorStyles.miniButton);
            if (drawRoomMode && !nextDrawRoomMode)
                ClearRoomDrawingState();
            if (!drawRoomMode && nextDrawRoomMode)
            {
                editRoomGridMode = false;
                ClearSelectedPolygonVertex();
            }
            drawRoomMode = nextDrawRoomMode;

            if (GUILayout.Button("씬 중앙에 생성"))
                CreateBattleRoomAtSceneCenter();
        }

        using (new EditorGUI.DisabledScope(selectedRoomGroup == null))
        {
            bool nextEditRoomGridMode = GUILayout.Toggle(
                editRoomGridMode,
                editRoomGridMode ? "방 그리드 수정 중..." : "선택 방 그리드 수정",
                EditorStyles.miniButton);
            if (nextEditRoomGridMode != editRoomGridMode)
            {
                editRoomGridMode = nextEditRoomGridMode;
                if (editRoomGridMode)
                {
                    drawRoomMode = false;
                    ClearRoomDrawingState();
                }
                else
                {
                    ClearSelectedPolygonVertex();
                }

                SceneView.RepaintAll();
            }

            DrawSelectedRoomGridEditActions();

            if (GUILayout.Button("선택 방 안의 오브젝트 자동 연결"))
                AutoWireRoom(selectedRoomGroup);

            if (GUILayout.Button("선택 문에 Door KillLock 생성"))
                CreateDoorKillLockForSelection(selectedRoomGroup);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("방 목록", EditorStyles.boldLabel);
        foreach (MonsterSpawnRoomGroup roomGroup in roomGroups.OrderBy(group => group.name))
        {
            if (roomGroup == null)
                continue;

            MonsterRoomArea2D area = ResolveRoomArea(roomGroup);
            int spawnCount = spawnContainers.Count(spawn => spawn != null && ResolveRoomGroup(spawn) == roomGroup);
            int chestLockCount = chestLocks.Count(lockTarget => lockTarget != null && area != null && Contains(area, lockTarget.transform.position));
            int doorLockCount = doorLocks.Count(lockTarget => lockTarget != null && ReadReference<MonsterSpawnRoomGroup>(lockTarget, "targetRoomGroup") == roomGroup);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.ObjectField(roomGroup, typeof(MonsterSpawnRoomGroup), true);
                EditorGUILayout.LabelField($"스폰 {spawnCount}  상자락 {chestLockCount}  문락 {doorLockCount}", GUILayout.Width(210f));
                if (GUILayout.Button("선택", GUILayout.Width(58f)))
                {
                    selectedRoomGroup = roomGroup;
                    Selection.activeObject = roomGroup;
                    SceneView.RepaintAll();
                }
            }
        }
    }

    private void DrawSelectedRoomGridEditActions()
    {
        MonsterRoomArea2D area = ResolveRoomArea(selectedRoomGroup);
        Collider2D collider = ResolveAreaCollider(area);
        if (collider == null)
        {
            EditorGUILayout.HelpBox("선택된 방에 수정할 Room Area Collider가 없습니다.", MessageType.Warning);
            return;
        }

        if (collider is BoxCollider2D)
        {
            if (GUILayout.Button("선택 방을 다각형으로 변환"))
                ConvertSelectedRoomBoxToPolygon();
        }
        else if (collider is PolygonCollider2D polygonCollider)
        {
            List<Vector3> polygonWorldPoints = GetPolygonWorldPoints(polygonCollider);
            int selectedVertexIndex = GetSelectedPolygonVertexIndex(polygonCollider, polygonWorldPoints.Count);
            string selectedLabel = selectedVertexIndex >= 0
                ? $"{selectedVertexIndex + 1} / {polygonWorldPoints.Count}"
                : "없음";

            EditorGUILayout.LabelField("선택 꼭짓점", selectedLabel);
            EditorGUILayout.HelpBox("SceneView에서 다각형 꼭짓점을 클릭하면 선택됩니다. Delete/Backspace로 선택 꼭짓점을 삭제합니다.", MessageType.Info);

            using (new EditorGUI.DisabledScope(selectedVertexIndex < 0 || polygonWorldPoints.Count <= 3))
            {
                if (GUILayout.Button("선택 꼭짓점 삭제"))
                    DeleteSelectedPolygonRoomPoint(polygonCollider);
            }
        }
        else
        {
            EditorGUILayout.HelpBox($"{collider.GetType().Name} 방은 그리드 핸들 수정 대상이 아닙니다.", MessageType.Info);
        }
    }

    private void DrawPlaceTab()
    {
        EditorGUILayout.LabelField("배치", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("팔레트에서 배치할 오브젝트를 고른 뒤 SceneView 빈 공간을 클릭하세요. 카드 드래그로도 배치할 수 있습니다.", MessageType.Info);

        DrawObjectPlacementPalette();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField($"현재 배치: {GetPlacementKindLabel(placementKind)}", EditorStyles.boldLabel);
        snapToGrid = EditorGUILayout.Toggle("그리드 스냅", snapToGrid);
        gridSize = Mathf.Max(0.1f, EditorGUILayout.FloatField("그리드 크기", gridSize));
        doorPlacementOrientation = (DoorPlacementOrientation)EditorGUILayout.Popup(
            "문 배치 방향",
            (int)doorPlacementOrientation,
            new[] { "자동", "가로", "세로" });

        EditorGUILayout.Space(6f);
        showPlacementPrefabSettings = EditorGUILayout.Foldout(showPlacementPrefabSettings, "배치 프리팹 설정", true);
        if (showPlacementPrefabSettings)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                doorPrefab = EditorGUILayout.ObjectField("문 프리팹", doorPrefab, typeof(GameObject), false) as GameObject;
                leverPrefab = EditorGUILayout.ObjectField("레버 프리팹", leverPrefab, typeof(GameObject), false) as GameObject;
                statuePrefab = EditorGUILayout.ObjectField("석상 프리팹", statuePrefab, typeof(GameObject), false) as GameObject;
                chestPrefab = EditorGUILayout.ObjectField("상자 프리팹", chestPrefab, typeof(GameObject), false) as GameObject;
                killLockChestPrefab = EditorGUILayout.ObjectField("킬락 상자 프리팹", killLockChestPrefab, typeof(GameObject), false) as GameObject;
                portalPrefab = EditorGUILayout.ObjectField("포탈 프리팹", portalPrefab, typeof(GameObject), false) as GameObject;
            }
        }

        EditorGUILayout.Space(10f);
        DrawMonsterPalette();
    }

    private void DrawObjectPlacementPalette()
    {
        EditorGUILayout.LabelField("오브젝트 팔레트", EditorStyles.boldLabel);
        List<(PlacementKind Kind, string Label, GameObject Prefab)> items = new()
        {
            (PlacementKind.Door, "문", doorPrefab),
            (PlacementKind.Lever, "레버", leverPrefab),
            (PlacementKind.Statue, "석상", statuePrefab),
            (PlacementKind.Chest, "상자", chestPrefab),
            (PlacementKind.KillLockChest, "킬락 상자", killLockChestPrefab != null ? killLockChestPrefab : chestPrefab),
            (PlacementKind.Portal, "포탈", portalPrefab),
            (PlacementKind.MonsterSpawn, "몬스터 스폰", GetSelectedMonsterSpawnPreviewPrefab())
        };

        int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 42f) / 108f));
        for (int i = 0; i < items.Count; i += columns)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int column = 0; column < columns && i + column < items.Count; column++)
                {
                    (PlacementKind kind, string label, GameObject prefab) = items[i + column];
                    DrawObjectPlacementPaletteItem(kind, label, prefab);
                }
            }
        }
    }

    private void DrawObjectPlacementPaletteItem(PlacementKind kind, string label, GameObject previewObject)
    {
        Rect itemRect = GUILayoutUtility.GetRect(100f, 98f, GUILayout.Width(100f), GUILayout.Height(98f));
        bool selected = placementKind == kind;
        Color background = selected ? new Color(0.18f, 0.45f, 0.78f, 0.36f) : new Color(0f, 0f, 0f, 0.12f);
        EditorGUI.DrawRect(itemRect, background);

        Color edge = selected ? new Color(0.35f, 0.75f, 1f, 0.9f) : new Color(1f, 1f, 1f, 0.18f);
        EditorGUI.DrawRect(new Rect(itemRect.x, itemRect.y, itemRect.width, 1f), edge);
        EditorGUI.DrawRect(new Rect(itemRect.x, itemRect.yMax - 1f, itemRect.width, 1f), edge);
        EditorGUI.DrawRect(new Rect(itemRect.x, itemRect.y, 1f, itemRect.height), edge);
        EditorGUI.DrawRect(new Rect(itemRect.xMax - 1f, itemRect.y, 1f, itemRect.height), edge);

        Texture preview = previewObject != null ? AssetPreview.GetAssetPreview(previewObject) : null;
        if (preview == null && previewObject != null)
            preview = AssetPreview.GetMiniThumbnail(previewObject);

        Rect previewRect = new(itemRect.x + 12f, itemRect.y + 7f, 76f, 55f);
        if (preview != null)
        {
            GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
        }
        else
        {
            GUI.Label(previewRect, GetPlacementKindIcon(kind), EditorStyles.centeredGreyMiniLabel);
        }

        GUIStyle labelStyle = selected ? EditorStyles.boldLabel : EditorStyles.centeredGreyMiniLabel;
        GUI.Label(new Rect(itemRect.x + 4f, itemRect.y + 65f, itemRect.width - 8f, 28f), label, labelStyle);

        Event e = Event.current;
        if (!itemRect.Contains(e.mousePosition))
            return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            placementKind = kind;
            mode = ToolMode.Place;
            Repaint();
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0)
        {
            placementKind = kind;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = previewObject != null ? new Object[] { previewObject } : Array.Empty<Object>();
            DragAndDrop.SetGenericData(ObjectPlacementDragKey, kind);
            DragAndDrop.StartDrag(label);
            e.Use();
        }
    }

    private void DrawMonsterPalette()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("몬스터 팔레트", EditorStyles.boldLabel);
            if (GUILayout.Button("몬스터 새로고침", GUILayout.Width(116f)))
                RefreshMonsterPalette();
        }

        monsterSearch = EditorGUILayout.TextField("검색", monsterSearch);
        selectedMonsterSpawnSource = (MonsterSpawnPlacementSource)EditorGUILayout.EnumPopup("스폰 소스", selectedMonsterSpawnSource);
        selectedMonsterPrefab = EditorGUILayout.ObjectField("선택 고정 몬스터", selectedMonsterPrefab, typeof(GameObject), false) as GameObject;
        selectedStageMonsterSet = EditorGUILayout.ObjectField("선택 공통 세트", selectedStageMonsterSet, typeof(StageMonsterSetSO), false) as StageMonsterSetSO;

        List<GameObject> filtered = monsterPrefabs
            .Where(prefab => prefab != null && (string.IsNullOrWhiteSpace(monsterSearch) || prefab.name.IndexOf(monsterSearch, StringComparison.OrdinalIgnoreCase) >= 0))
            .ToList();
        List<StageMonsterSetSO> filteredSets = stageMonsterSets
            .Where(set => set != null && (string.IsNullOrWhiteSpace(monsterSearch) || set.name.IndexOf(monsterSearch, StringComparison.OrdinalIgnoreCase) >= 0))
            .ToList();

        monsterPaletteScroll = EditorGUILayout.BeginScrollView(monsterPaletteScroll, GUILayout.MinHeight(180f));
        DrawStageMonsterSetPalette(filteredSets);
        int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 60f) / 100f));
        foreach (IGrouping<string, GameObject> folderGroup in filtered
                     .GroupBy(GetMonsterFolderLabel)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!monsterFolderFoldouts.ContainsKey(folderGroup.Key))
                monsterFolderFoldouts[folderGroup.Key] = true;

            monsterFolderFoldouts[folderGroup.Key] = EditorGUILayout.Foldout(
                monsterFolderFoldouts[folderGroup.Key],
                $"{folderGroup.Key} ({folderGroup.Count()})",
                true);

            if (!monsterFolderFoldouts[folderGroup.Key])
                continue;

            List<GameObject> folderPrefabs = folderGroup
                .OrderBy(prefab => prefab.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < folderPrefabs.Count; i += columns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int column = 0; column < columns && i + column < folderPrefabs.Count; column++)
                        DrawMonsterPaletteItem(folderPrefabs[i + column]);
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawStageMonsterSetPalette(List<StageMonsterSetSO> filteredSets)
    {
        if (filteredSets.Count == 0)
            return;

        int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 60f) / 100f));
        foreach (IGrouping<string, StageMonsterSetSO> folderGroup in filteredSets
                     .GroupBy(GetStageMonsterSetFolderLabel)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            string foldoutKey = $"StageSet/{folderGroup.Key}";
            if (!stageMonsterSetFolderFoldouts.ContainsKey(foldoutKey))
                stageMonsterSetFolderFoldouts[foldoutKey] = true;

            stageMonsterSetFolderFoldouts[foldoutKey] = EditorGUILayout.Foldout(
                stageMonsterSetFolderFoldouts[foldoutKey],
                $"공통 세트/{folderGroup.Key} ({folderGroup.Count()})",
                true);

            if (!stageMonsterSetFolderFoldouts[foldoutKey])
                continue;

            List<StageMonsterSetSO> folderSets = folderGroup
                .OrderBy(set => set.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < folderSets.Count; i += columns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int column = 0; column < columns && i + column < folderSets.Count; column++)
                        DrawStageMonsterSetPaletteItem(folderSets[i + column]);
                }
            }
        }
    }

    private void DrawStageMonsterSetPaletteItem(StageMonsterSetSO monsterSet)
    {
        Rect itemRect = GUILayoutUtility.GetRect(92f, 92f, GUILayout.Width(92f), GUILayout.Height(92f));
        bool selected = monsterSet == selectedStageMonsterSet && selectedMonsterSpawnSource == MonsterSpawnPlacementSource.StageMonsterSet;
        EditorGUI.DrawRect(itemRect, selected ? new Color(0.25f, 0.62f, 0.35f, 0.35f) : new Color(0f, 0f, 0f, 0.12f));

        Texture preview = AssetPreview.GetMiniThumbnail(monsterSet);
        Rect previewRect = new Rect(itemRect.x + 10f, itemRect.y + 6f, 72f, 34f);
        if (preview != null)
            GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);

        string stageZeroName = monsterSet != null && monsterSet.TryResolveMonsterPrefab(0, out GameObject prefab) && prefab != null
            ? prefab.name
            : "미설정";
        GUI.Label(new Rect(itemRect.x + 4f, itemRect.y + 42f, itemRect.width - 8f, 18f), "COMMON", EditorStyles.centeredGreyMiniLabel);
        GUI.Label(new Rect(itemRect.x + 4f, itemRect.y + 58f, itemRect.width - 8f, 16f), monsterSet.name, EditorStyles.centeredGreyMiniLabel);
        GUI.Label(new Rect(itemRect.x + 4f, itemRect.y + 73f, itemRect.width - 8f, 16f), $"0: {stageZeroName}", EditorStyles.centeredGreyMiniLabel);

        Event e = Event.current;
        if (!itemRect.Contains(e.mousePosition))
            return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            selectedStageMonsterSet = monsterSet;
            selectedMonsterSpawnSource = MonsterSpawnPlacementSource.StageMonsterSet;
            Repaint();
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0)
        {
            selectedStageMonsterSet = monsterSet;
            selectedMonsterSpawnSource = MonsterSpawnPlacementSource.StageMonsterSet;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new Object[] { monsterSet };
            DragAndDrop.SetGenericData(StageMonsterSetDragKey, monsterSet);
            DragAndDrop.StartDrag(monsterSet.name);
            e.Use();
        }
    }

    private void DrawMonsterPaletteItem(GameObject prefab)
    {
        Rect itemRect = GUILayoutUtility.GetRect(92f, 92f, GUILayout.Width(92f), GUILayout.Height(92f));
        bool selected = prefab == selectedMonsterPrefab;
        EditorGUI.DrawRect(itemRect, selected ? new Color(0.2f, 0.45f, 0.8f, 0.35f) : new Color(0f, 0f, 0f, 0.12f));

        Texture preview = AssetPreview.GetAssetPreview(prefab);
        if (preview == null)
            preview = AssetPreview.GetMiniThumbnail(prefab);

        Rect previewRect = new Rect(itemRect.x + 10f, itemRect.y + 6f, 72f, 54f);
        if (preview != null)
            GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);

        GUI.Label(new Rect(itemRect.x + 4f, itemRect.y + 62f, itemRect.width - 8f, 28f), prefab.name, EditorStyles.centeredGreyMiniLabel);

        Event e = Event.current;
        if (!itemRect.Contains(e.mousePosition))
            return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            selectedMonsterPrefab = prefab;
            selectedMonsterSpawnSource = MonsterSpawnPlacementSource.FixedPrefab;
            Repaint();
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0)
        {
            selectedMonsterPrefab = prefab;
            selectedMonsterSpawnSource = MonsterSpawnPlacementSource.FixedPrefab;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new Object[] { prefab };
            DragAndDrop.SetGenericData(MonsterPrefabDragKey, prefab);
            DragAndDrop.StartDrag(prefab.name);
            e.Use();
        }
    }

    private void DrawOptionsTab()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorGUILayout.HelpBox("씬에서 문, 숏컷, 석상, 스폰, 문락, 상자, 포탈을 선택하세요.", MessageType.Info);
            return;
        }

        DoorObject door = selected.GetComponentInParent<DoorObject>(true);
        StatueShortcut statue = selected.GetComponentInParent<StatueShortcut>(true);
        ShortcutBase shortcut = selected.GetComponentInParent<ShortcutBase>(true);
        MonsterSpawnContainer spawn = selected.GetComponentInParent<MonsterSpawnContainer>(true);
        RoomDoorMonsterKillLock doorLock = selected.GetComponentInParent<RoomDoorMonsterKillLock>(true);
        ChestMonsterKillLock chestLock = selected.GetComponentInParent<ChestMonsterKillLock>(true);
        ScenePortal portal = selected.GetComponentInParent<ScenePortal>(true);

        if (door != null)
            DrawObjectProperties("문", door, "mapID", "doorID", "doorType", "isPermanent", "oneWayOpenSide", "oneWayOpenThreshold", "affectionTargetNpcId", "requiredAffection");

        if (shortcut != null)
            DrawObjectProperties("숏컷", shortcut, "targetDoor", "promptAnchor");

        if (statue != null)
            DrawObjectProperties("석상", statue, "costType", "costAmount", "allowLethalPayment", "healthAttribute", "magicStoneIcon", "hpIcon");

        if (spawn != null)
            DrawObjectProperties("몬스터 스폰", spawn, "sourceKind", "monsterPrefab", "stageMonsterSet", "spawnByDefault", "allowExtraSpawn", "spawnAnchor", "roomArea", "roomGroup", "linkedChestKillLock");

        if (doorLock != null)
            DrawObjectProperties("문 KillLock", doorLock, "targetDoor", "targetRoomGroup", "logDebug");

        if (chestLock != null)
            DrawObjectProperties("상자 KillLock", chestLock, "presentationAnchor", "unlockPresentation");

        if (portal != null)
            DrawObjectProperties("포탈", portal, "portalId", "transitionType", "startRunRouteCatalog", "promptAnchor", "interactPromptText", "sceneTravelCleanupTagSets");
    }

    private void DrawObjectProperties(string label, Object target, params string[] propertyNames)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            SerializedObject serializedObject = new(target);
            serializedObject.Update();
            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property = serializedObject.FindProperty(propertyName);
                if (property != null)
                    EditorGUILayout.PropertyField(property, includeChildren: true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawSceneView(SceneView sceneView)
    {
        Event e = Event.current;
        if (showGrid)
            DrawGrid(sceneView);

        DrawSceneMarkers();
        DrawSceneConnections();

        if (mode == ToolMode.BattleRoom && editRoomGridMode)
            HandleRoomGridEditPreInput(e);

        if (mode == ToolMode.BattleRoom && editRoomGridMode)
            DrawSelectedRoomGridEditor();

        if (showValidationMarkers)
            DrawValidationMarkers();

        DrawSceneInstructions();
        HandleSceneInput(e);
    }

    private void DrawGrid(SceneView sceneView)
    {
        if (gridSize <= 0f || Event.current.type != EventType.Repaint)
            return;

        Bounds bounds = CalculateLevelBounds();
        float minX = Mathf.Floor((bounds.min.x - 4f) / gridSize) * gridSize;
        float maxX = Mathf.Ceil((bounds.max.x + 4f) / gridSize) * gridSize;
        float minY = Mathf.Floor((bounds.min.y - 4f) / gridSize) * gridSize;
        float maxY = Mathf.Ceil((bounds.max.y + 4f) / gridSize) * gridSize;

        Handles.color = new Color(0.35f, 0.55f, 0.75f, 0.17f);
        for (float x = minX; x <= maxX; x += gridSize)
            Handles.DrawLine(new Vector3(x, minY, 0f), new Vector3(x, maxY, 0f));

        for (float y = minY; y <= maxY; y += gridSize)
            Handles.DrawLine(new Vector3(minX, y, 0f), new Vector3(maxX, y, 0f));

        Handles.color = new Color(0.4f, 0.8f, 1f, 0.38f);
        Handles.DrawLine(new Vector3(0f, minY, 0f), new Vector3(0f, maxY, 0f));
        Handles.DrawLine(new Vector3(minX, 0f, 0f), new Vector3(maxX, 0f, 0f));
    }

    private void DrawSceneMarkers()
    {
        foreach (MonsterRoomArea2D area in roomAreas)
        {
            if (area == null)
                continue;

            DrawRoomArea(area);
        }

        foreach (DoorObject door in doors)
            DrawMarker(door, new Color(0.95f, 0.4f, 0.25f, 1f), BuildDoorLabel(door));

        foreach (ShortcutBase shortcut in shortcuts)
            DrawMarker(shortcut, ResolveShortcutColor(shortcut), shortcut.GetType().Name.Replace("Shortcut", string.Empty));

        foreach (MonsterSpawnRoomGroup roomGroup in roomGroups)
            DrawMarker(roomGroup, new Color(0.2f, 0.75f, 1f, 1f), "Room");

        foreach (MonsterSpawnContainer spawn in spawnContainers)
            DrawMarker(spawn, new Color(0.25f, 1f, 0.25f, 1f), BuildSpawnLabel(spawn));

        foreach (TreasureChest chest in chests)
            DrawMarker(chest, new Color(1f, 0.85f, 0.25f, 1f), chest.GetComponent<ChestMonsterKillLock>() != null ? "상자락" : "상자");

        foreach (ChestMonsterKillLock chestLock in chestLocks)
        {
            if (chestLock != null && chestLock.GetComponent<TreasureChest>() == null)
                DrawMarker(chestLock, new Color(1f, 0.72f, 0.18f, 1f), "상자락");
        }

        foreach (RoomDoorMonsterKillLock doorLock in doorLocks)
            DrawMarker(doorLock, new Color(1f, 0.25f, 0.18f, 1f), "문락");

        foreach (ScenePortal portal in portals)
            DrawMarker(portal, new Color(0.5f, 0.45f, 1f, 1f), $"포탈 {portal.PortalTransitionType}");
    }

    private void DrawRoomArea(MonsterRoomArea2D area)
    {
        Collider2D collider = ResolveAreaCollider(area);
        if (collider == null)
            return;

        if (collider is PolygonCollider2D polygonCollider)
        {
            Handles.color = new Color(0.2f, 0.9f, 1f, 0.85f);
            for (int pathIndex = 0; pathIndex < polygonCollider.pathCount; pathIndex++)
            {
                Vector2[] path = polygonCollider.GetPath(pathIndex);
                if (path == null || path.Length < 2)
                    continue;

                Vector3[] points = BuildClosedWorldPath(polygonCollider.transform, path);
                Handles.DrawAAPolyLine(2.5f, points);
            }

            return;
        }

        Handles.color = new Color(0.2f, 0.7f, 1f, 0.22f);
        Handles.DrawSolidRectangleWithOutline(
            BuildRectangle(collider.bounds),
            new Color(0.2f, 0.7f, 1f, 0.08f),
            new Color(0.2f, 0.9f, 1f, 0.6f));
    }

    private void DrawSelectedRoomGridEditor()
    {
        Collider2D collider = ResolveSelectedRoomAreaCollider();
        if (collider == null)
            return;

        if (collider is BoxCollider2D boxCollider)
        {
            DrawBoxRoomGridEditor(boxCollider);
            return;
        }

        if (collider is PolygonCollider2D polygonCollider)
        {
            DrawPolygonRoomGridEditor(polygonCollider);
            return;
        }

        Handles.Label(collider.transform.position, $"{collider.GetType().Name} 수정 미지원");
    }

    private void DrawBoxRoomGridEditor(BoxCollider2D boxCollider)
    {
        Vector3[] corners = GetBoxWorldCorners(boxCollider);
        Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
        Handles.DrawAAPolyLine(3f, BuildClosedWorldPath(corners));

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 corner = corners[i];
            float handleSize = HandleUtility.GetHandleSize(corner) * 0.08f;

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(corner, handleSize, Vector3.zero, Handles.DotHandleCap);
            if (!EditorGUI.EndChangeCheck())
                continue;

            ApplyBoxRoomCornerMove(boxCollider, i, Snap(moved));
            return;
        }
    }

    private void DrawPolygonRoomGridEditor(PolygonCollider2D polygonCollider)
    {
        List<Vector3> worldPoints = GetPolygonWorldPoints(polygonCollider);
        if (worldPoints.Count < 2)
            return;

        ValidateSelectedPolygonVertex(polygonCollider, worldPoints.Count);

        Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
        Handles.DrawAAPolyLine(3f, BuildClosedWorldPath(worldPoints));

        for (int i = 0; i < worldPoints.Count; i++)
        {
            Vector3 point = worldPoints[i];
            float handleSize = HandleUtility.GetHandleSize(point) * 0.075f;
            bool selected = GetSelectedPolygonVertexIndex(polygonCollider, worldPoints.Count) == i;

            DrawPolygonVertexSelectionVisual(point, handleSize, i, selected);

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(point, handleSize, Vector3.zero, Handles.DotHandleCap);
            if (!EditorGUI.EndChangeCheck())
                continue;

            TryApplyPolygonVertexMove(polygonCollider, i, Snap(moved));
            return;
        }

        for (int i = 0; i < worldPoints.Count; i++)
        {
            int nextIndex = (i + 1) % worldPoints.Count;
            Vector3 midpoint = Snap((worldPoints[i] + worldPoints[nextIndex]) * 0.5f);
            float handleSize = HandleUtility.GetHandleSize(midpoint) * 0.05f;

            Handles.color = new Color(0.2f, 1f, 0.35f, 0.9f);
            if (!Handles.Button(midpoint, Quaternion.identity, handleSize, handleSize * 1.45f, Handles.CircleHandleCap))
                continue;

            InsertPolygonRoomPoint(polygonCollider, i + 1, midpoint);
            Event.current.Use();
            return;
        }
    }

    private void DrawPolygonVertexSelectionVisual(Vector3 point, float handleSize, int vertexIndex, bool selected)
    {
        float ringRadius = handleSize * (selected ? 2.1f : 1.35f);
        Handles.color = selected
            ? new Color(1f, 0.25f, 0.12f, 0.24f)
            : new Color(1f, 1f, 1f, 0.08f);
        Handles.DrawSolidDisc(point, Vector3.forward, ringRadius);

        Handles.color = selected
            ? new Color(1f, 0.95f, 0.2f, 1f)
            : new Color(1f, 1f, 1f, 0.35f);
        Handles.DrawWireDisc(point, Vector3.forward, ringRadius);

        GUIStyle labelStyle = new(EditorStyles.boldLabel);
        labelStyle.normal.textColor = selected
            ? new Color(1f, 0.95f, 0.2f, 1f)
            : new Color(1f, 1f, 1f, 0.6f);
        Handles.Label(point + Vector3.up * (handleSize * 2.5f), selected ? $"#{vertexIndex + 1} 선택" : $"#{vertexIndex + 1}", labelStyle);
    }

    private void ApplyBoxRoomCornerMove(BoxCollider2D boxCollider, int cornerIndex, Vector3 movedCorner)
    {
        if (!IsAxisAlignedRoomTransform(boxCollider.transform))
        {
            ShowNotification(new GUIContent("회전된 BoxCollider 방은 그리드 수정이 지원되지 않습니다."));
            return;
        }

        Vector3[] corners = GetBoxWorldCorners(boxCollider);
        Vector3 opposite = corners[(cornerIndex + 2) % corners.Length];
        Rect rect = CreateRect(opposite, movedCorner);
        if (rect.width < Mathf.Max(0.1f, gridSize * 0.5f) || rect.height < Mathf.Max(0.1f, gridSize * 0.5f))
        {
            ShowNotification(new GUIContent("방 크기가 너무 작습니다."));
            return;
        }

        Undo.RecordObjects(new Object[] { boxCollider, boxCollider.transform }, "Edit Room Grid");

        Vector3 center = rect.center;
        center.z = boxCollider.transform.position.z;
        boxCollider.transform.position = center;
        boxCollider.offset = Vector2.zero;

        Vector3 scale = boxCollider.transform.lossyScale;
        float scaleX = Mathf.Max(0.0001f, Mathf.Abs(scale.x));
        float scaleY = Mathf.Max(0.0001f, Mathf.Abs(scale.y));
        boxCollider.size = new Vector2(rect.width / scaleX, rect.height / scaleY);

        EditorUtility.SetDirty(boxCollider);
        EditorUtility.SetDirty(boxCollider.transform);
        EditorSceneManager.MarkSceneDirty(boxCollider.gameObject.scene);
        SceneView.RepaintAll();
    }

    private void TryApplyPolygonVertexMove(PolygonCollider2D polygonCollider, int vertexIndex, Vector3 movedPoint)
    {
        List<Vector3> worldPoints = GetPolygonWorldPoints(polygonCollider);
        if (vertexIndex < 0 || vertexIndex >= worldPoints.Count)
            return;

        worldPoints[vertexIndex] = movedPoint;
        if (TryApplyPolygonWorldPoints(polygonCollider, worldPoints, "Edit Room Polygon Vertex"))
            SetSelectedPolygonVertex(polygonCollider, vertexIndex);
    }

    private void InsertPolygonRoomPoint(PolygonCollider2D polygonCollider, int insertIndex, Vector3 point)
    {
        List<Vector3> worldPoints = GetPolygonWorldPoints(polygonCollider);
        insertIndex = Mathf.Clamp(insertIndex, 0, worldPoints.Count);
        worldPoints.Insert(insertIndex, point);
        if (TryApplyPolygonWorldPoints(polygonCollider, worldPoints, "Insert Room Polygon Vertex"))
            SetSelectedPolygonVertex(polygonCollider, insertIndex);
    }

    private bool TryInsertPolygonPointFromMouse(PolygonCollider2D polygonCollider, Vector2 mousePosition)
    {
        List<Vector3> worldPoints = GetPolygonWorldPoints(polygonCollider);
        if (worldPoints.Count < 3)
            return false;

        if (IsMouseNearPolygonVertex(worldPoints, mousePosition, PolygonVertexClickExclusionPixels))
            return false;

        int bestEdgeIndex = -1;
        float bestEdgeT = 0f;
        float bestDistance = PolygonEdgeInsertPixelThreshold;

        for (int i = 0; i < worldPoints.Count; i++)
        {
            int nextIndex = (i + 1) % worldPoints.Count;
            Vector2 guiA = HandleUtility.WorldToGUIPoint(worldPoints[i]);
            Vector2 guiB = HandleUtility.WorldToGUIPoint(worldPoints[nextIndex]);
            if ((guiB - guiA).sqrMagnitude <= 0.0001f)
                continue;

            float edgeT = ClosestPointOnSegment01(mousePosition, guiA, guiB);
            Vector2 closest = Vector2.Lerp(guiA, guiB, edgeT);
            float distance = Vector2.Distance(mousePosition, closest);
            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            bestEdgeIndex = i;
            bestEdgeT = edgeT;
        }

        if (bestEdgeIndex < 0)
            return false;

        int bestNextIndex = (bestEdgeIndex + 1) % worldPoints.Count;
        Vector3 insertPoint = Snap(Vector3.Lerp(worldPoints[bestEdgeIndex], worldPoints[bestNextIndex], bestEdgeT));
        if (IsSameGridPoint(insertPoint, worldPoints[bestEdgeIndex]) ||
            IsSameGridPoint(insertPoint, worldPoints[bestNextIndex]))
        {
            ShowNotification(new GUIContent("새 꼭짓점이 기존 꼭짓점과 겹칩니다."));
            return true;
        }

        InsertPolygonRoomPoint(polygonCollider, bestEdgeIndex + 1, insertPoint);
        return true;
    }

    private bool TrySelectPolygonVertexFromMouse(PolygonCollider2D polygonCollider, Vector2 mousePosition)
    {
        List<Vector3> worldPoints = GetPolygonWorldPoints(polygonCollider);
        int vertexIndex = FindClosestPolygonVertexIndex(worldPoints, mousePosition, PolygonVertexSelectionPixelThreshold);
        if (vertexIndex < 0)
            return false;

        SetSelectedPolygonVertex(polygonCollider, vertexIndex);
        SceneView.RepaintAll();
        return true;
    }

    private void SetSelectedPolygonVertex(PolygonCollider2D polygonCollider, int vertexIndex)
    {
        if (polygonCollider == null)
        {
            ClearSelectedPolygonVertex();
            return;
        }

        List<Vector3> worldPoints = GetPolygonWorldPoints(polygonCollider);
        if (vertexIndex < 0 || vertexIndex >= worldPoints.Count)
        {
            ClearSelectedPolygonVertex();
            return;
        }

        selectedPolygonVertexCollider = polygonCollider;
        selectedPolygonVertexIndex = vertexIndex;
    }

    private void ClearSelectedPolygonVertex()
    {
        selectedPolygonVertexCollider = null;
        selectedPolygonVertexIndex = -1;
    }

    private int GetSelectedPolygonVertexIndex(PolygonCollider2D polygonCollider, int vertexCount)
    {
        if (polygonCollider == null ||
            selectedPolygonVertexCollider == null ||
            selectedPolygonVertexCollider != polygonCollider ||
            selectedPolygonVertexIndex < 0 ||
            selectedPolygonVertexIndex >= vertexCount)
        {
            return -1;
        }

        return selectedPolygonVertexIndex;
    }

    private void ValidateSelectedPolygonVertex(PolygonCollider2D polygonCollider, int vertexCount)
    {
        if (GetSelectedPolygonVertexIndex(polygonCollider, vertexCount) < 0)
            ClearSelectedPolygonVertex();
    }

    private void DeleteLastPolygonRoomPoint(PolygonCollider2D polygonCollider)
    {
        List<Vector3> worldPoints = GetPolygonWorldPoints(polygonCollider);
        if (worldPoints.Count <= 3)
        {
            ShowNotification(new GUIContent("다각형 방은 꼭짓점이 3개 이상 필요합니다."));
            return;
        }

        worldPoints.RemoveAt(worldPoints.Count - 1);
        TryApplyPolygonWorldPoints(polygonCollider, worldPoints, "Delete Room Polygon Vertex");
    }

    private void DeleteSelectedPolygonRoomPoint(PolygonCollider2D polygonCollider)
    {
        List<Vector3> worldPoints = GetPolygonWorldPoints(polygonCollider);
        int vertexIndex = GetSelectedPolygonVertexIndex(polygonCollider, worldPoints.Count);
        if (vertexIndex < 0)
        {
            ShowNotification(new GUIContent("삭제할 꼭짓점을 먼저 선택하세요."));
            return;
        }

        if (worldPoints.Count <= 3)
        {
            ShowNotification(new GUIContent("다각형 방은 꼭짓점이 3개 이상 필요합니다."));
            return;
        }

        worldPoints.RemoveAt(vertexIndex);
        if (!TryApplyPolygonWorldPoints(polygonCollider, worldPoints, "Delete Selected Room Polygon Vertex"))
            return;

        int nextSelectedIndex = Mathf.Min(vertexIndex, worldPoints.Count - 1);
        SetSelectedPolygonVertex(polygonCollider, nextSelectedIndex);
    }

    private bool TryApplyPolygonWorldPoints(PolygonCollider2D polygonCollider, List<Vector3> worldPoints, string undoName)
    {
        if (worldPoints.Count < 3)
        {
            ShowNotification(new GUIContent("다각형 방은 꼭짓점이 3개 이상 필요합니다."));
            return false;
        }

        if (HasDuplicatePolygonPoints(worldPoints))
        {
            ShowNotification(new GUIContent("다각형 방에 중복 꼭짓점이 있습니다."));
            return false;
        }

        if (HasSelfIntersection(worldPoints))
        {
            ShowNotification(new GUIContent("자기 교차 다각형 방은 적용할 수 없습니다."));
            return false;
        }

        Bounds bounds = CalculateBounds(worldPoints);
        Vector3 center = bounds.center;
        center.z = polygonCollider.transform.position.z;

        Undo.RecordObjects(new Object[] { polygonCollider, polygonCollider.transform }, undoName);
        polygonCollider.transform.position = center;
        polygonCollider.pathCount = 1;
        polygonCollider.SetPath(0, BuildLocalPolygonPath(worldPoints, polygonCollider.transform));

        EditorUtility.SetDirty(polygonCollider);
        EditorUtility.SetDirty(polygonCollider.transform);
        EditorSceneManager.MarkSceneDirty(polygonCollider.gameObject.scene);
        SceneView.RepaintAll();
        return true;
    }

    private void ConvertSelectedRoomBoxToPolygon()
    {
        MonsterRoomArea2D area = ResolveRoomArea(selectedRoomGroup);
        BoxCollider2D boxCollider = ResolveAreaCollider(area) as BoxCollider2D;
        if (area == null || boxCollider == null)
            return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Convert Room To Polygon");

        Vector3[] worldCorners = GetBoxWorldCorners(boxCollider);
        PolygonCollider2D polygonCollider = Undo.AddComponent<PolygonCollider2D>(boxCollider.gameObject);
        polygonCollider.isTrigger = boxCollider.isTrigger;
        polygonCollider.pathCount = 1;
        polygonCollider.SetPath(0, BuildLocalPolygonPath(worldCorners, polygonCollider.transform));

        AssignReference(area, "areaCollider", polygonCollider);
        Undo.DestroyObjectImmediate(boxCollider);

        EditorUtility.SetDirty(area);
        EditorUtility.SetDirty(polygonCollider);
        EditorSceneManager.MarkSceneDirty(polygonCollider.gameObject.scene);
        Undo.CollapseUndoOperations(undoGroup);
        RefreshAll();
    }

    private void DrawMarker(Component component, Color color, string label)
    {
        if (component == null)
            return;

        Vector3 position = component.transform.position;
        bool selected = IsSelectedContext(component);
        bool activeLinkSource = IsActiveLinkSource(component);
        float size = HandleUtility.GetHandleSize(position) * (selected || activeLinkSource ? 0.12f : 0.09f);

        Handles.color = selected || activeLinkSource ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        Handles.DrawWireDisc(position, Vector3.forward, size * 1.22f);

        Handles.color = new Color(color.r, color.g, color.b, 0.18f);
        Handles.DrawSolidDisc(position, Vector3.forward, size * 1.55f);

        bool markerClickEnabled = mode != ToolMode.Place && !(mode == ToolMode.BattleRoom && editRoomGridMode);
        Handles.color = color;
        if (markerClickEnabled && Handles.Button(position, Quaternion.identity, size, size * 1.35f, Handles.CircleHandleCap))
        {
            NavigateFromSceneComponent(component, allowLinking: true);
            Event.current.Use();
        }

        Handles.color = color;
        Handles.DrawSolidDisc(position, Vector3.forward, size * 0.7f);

        if (!showLabels)
            return;

        GUIStyle style = new(EditorStyles.boldLabel);
        style.normal.textColor = color;
        Handles.Label(position + Vector3.up * (size * 1.85f), BuildMarkerLabel(component, label), style);
    }

    private void DrawSceneConnections()
    {
        foreach (ShortcutBase shortcut in shortcuts)
        {
            if (shortcut == null || shortcut.TargetDoor == null)
                continue;

            DrawLine(shortcut.transform.position, shortcut.TargetDoor.transform.position, ResolveShortcutColor(shortcut), 3f);
        }

        foreach (MonsterSpawnContainer spawn in spawnContainers)
        {
            if (spawn == null || spawn.LinkedChestKillLock == null)
                continue;

            DrawLine(spawn.transform.position, spawn.LinkedChestKillLock.transform.position, new Color(1f, 0.85f, 0.25f, 0.9f), 2f);
        }

        foreach (RoomDoorMonsterKillLock doorLock in doorLocks)
        {
            DoorObject targetDoor = ReadReference<DoorObject>(doorLock, "targetDoor");
            MonsterSpawnRoomGroup targetGroup = ReadReference<MonsterSpawnRoomGroup>(doorLock, "targetRoomGroup");
            if (targetDoor == null || targetGroup == null)
                continue;

            DrawLine(doorLock.transform.position, targetGroup.transform.position, new Color(1f, 0.35f, 0.2f, 0.9f), 2f);
        }

        if (linkingShortcut != null)
        {
            Handles.color = Color.green;
            Handles.DrawAAPolyLine(3f, linkingShortcut.transform.position, GetMouseWorldPosition(Event.current));
        }

        if (linkingChestLock != null)
        {
            Handles.color = new Color(1f, 0.85f, 0.25f, 1f);
            Handles.DrawAAPolyLine(3f, linkingChestLock.transform.position, GetMouseWorldPosition(Event.current));
        }

        if (linkingSpawn != null)
        {
            Handles.color = new Color(1f, 0.85f, 0.25f, 1f);
            Handles.DrawAAPolyLine(3f, linkingSpawn.transform.position, GetMouseWorldPosition(Event.current));
        }

        if (linkingRoomGroup != null)
        {
            Handles.color = new Color(0.2f, 0.75f, 1f, 1f);
            Handles.DrawAAPolyLine(3f, linkingRoomGroup.transform.position, GetMouseWorldPosition(Event.current));
        }

        if (linkingDoor != null)
        {
            Handles.color = new Color(1f, 0.35f, 0.2f, 1f);
            Handles.DrawAAPolyLine(3f, linkingDoor.transform.position, GetMouseWorldPosition(Event.current));
        }
    }

    private void DrawLine(Vector3 start, Vector3 end, Color color, float width)
    {
        Handles.color = color;
        Handles.DrawAAPolyLine(width, start, end);
    }

    private void DrawValidationMarkers()
    {
        foreach (ValidationResult result in validationResults)
        {
            if (result.Context is not Component component || component == null)
                continue;

            Color color = result.SeverityLevel == Severity.Error ? Color.red : new Color(1f, 0.72f, 0.1f, 1f);
            Vector3 position = component.transform.position + Vector3.up * (HandleUtility.GetHandleSize(component.transform.position) * 0.22f);
            Handles.color = color;
            Handles.DrawSolidDisc(position, Vector3.forward, HandleUtility.GetHandleSize(position) * 0.055f);
            Handles.Label(position + Vector3.right * 0.12f, result.SeverityLevel == Severity.Error ? "!" : "?");
        }
    }

    private void DrawSceneInstructions()
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10f, 10f, 410f, 92f), EditorStyles.helpBox);
        GUILayout.Label($"레벨 디자인: {GetModeLabel(mode)}", EditorStyles.boldLabel);
        if (HasActiveLinkSource())
            GUILayout.Label(GetSceneHint());
        else if (mode == ToolMode.Link)
            GUILayout.Label("연결할 점을 먼저 클릭하세요.");
        else if (mode == ToolMode.BattleRoom && drawRoomMode)
            GUILayout.Label(roomDrawShape == RoomDrawShape.Polygon
                ? "점을 찍어 방을 만들고 Enter/첫 점 클릭으로 확정, Esc로 취소합니다."
                : "SceneView에서 드래그해 BattleRoom을 생성합니다.");
        else if (mode == ToolMode.BattleRoom && editRoomGridMode)
            GUILayout.Label("꼭짓점 클릭으로 선택, 드래그로 이동, Delete로 삭제합니다. 선분 클릭/edge 점 클릭으로 꼭짓점을 추가합니다.");
        else if (mode == ToolMode.Place)
            GUILayout.Label($"배치: {placementKind}");
        else
            GUILayout.Label("점 클릭으로 기능 이동. 숏컷->문, 방->문, 상자락->스폰 연결.");
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private void HandleSceneInput(Event e)
    {
        if (e == null || e.type == EventType.Used)
            return;

        if (HandleCancelLinkInput(e))
            return;

        if (e.alt)
            return;

        if (mode == ToolMode.Place)
            HandlePlacementInput(e);

        if (mode == ToolMode.Link)
            HandleLinkInput(e);

        if (mode == ToolMode.BattleRoom && drawRoomMode)
            HandleBattleRoomDrawingInput(e);

        if (mode == ToolMode.BattleRoom && editRoomGridMode)
            HandleRoomGridEditInput(e);

        if (autoNavigateFromScene && mode != ToolMode.Place && !(mode == ToolMode.BattleRoom && (drawRoomMode || editRoomGridMode)))
            HandleSceneNavigationInput(e);
    }

    private bool HandleCancelLinkInput(Event e)
    {
        if (e.type != EventType.KeyDown || e.keyCode != KeyCode.Escape || !HasActiveLinkSource())
            return false;

        ClearLinkSources();
        ShowNotification(new GUIContent("연결을 취소했습니다."));
        e.Use();
        return true;
    }

    private void HandleRoomGridEditPreInput(Event e)
    {
        if (e == null || e.type == EventType.Used || e.alt)
            return;

        if (e.type != EventType.MouseDown || e.button != 0)
            return;

        PolygonCollider2D polygonCollider = ResolveSelectedRoomAreaCollider() as PolygonCollider2D;
        if (polygonCollider == null)
            return;

        TrySelectPolygonVertexFromMouse(polygonCollider, e.mousePosition);
    }

    private void HandleRoomGridEditInput(Event e)
    {
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            editRoomGridMode = false;
            ClearSelectedPolygonVertex();
            ShowNotification(new GUIContent("방 그리드 수정을 종료했습니다."));
            e.Use();
            SceneView.RepaintAll();
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            PolygonCollider2D targetPolygonCollider = ResolveSelectedRoomAreaCollider() as PolygonCollider2D;
            if (targetPolygonCollider != null && TryInsertPolygonPointFromMouse(targetPolygonCollider, e.mousePosition))
            {
                e.Use();
                return;
            }
        }

        if (e.type != EventType.KeyDown || (e.keyCode != KeyCode.Backspace && e.keyCode != KeyCode.Delete))
            return;

        PolygonCollider2D polygonCollider = ResolveSelectedRoomAreaCollider() as PolygonCollider2D;
        if (polygonCollider == null)
            return;

        DeleteSelectedPolygonRoomPoint(polygonCollider);
        e.Use();
    }

    private void HandleLinkInput(Event e)
    {
        if (e.type != EventType.MouseDown || e.button != 0)
            return;

        Component picked = PickLevelDesignComponent(e.mousePosition);
        if (picked == null)
            picked = ResolveLevelDesignComponent(HandleUtility.PickGameObject(e.mousePosition, false));

        if (picked == null)
            return;

        NavigateFromSceneComponent(picked, allowLinking: true);
        e.Use();
    }

    private void HandleSceneNavigationInput(Event e)
    {
        if (e.type != EventType.MouseDown || e.button != 0)
            return;

        Component picked = PickLevelDesignComponent(e.mousePosition);
        if (picked == null)
            picked = ResolveLevelDesignComponent(HandleUtility.PickGameObject(e.mousePosition, false));

        if (picked == null)
            return;

        NavigateFromSceneComponent(picked, allowLinking: true);
        e.Use();
    }

    private void HandlePlacementInput(Event e)
    {
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        object draggedPlacement = DragAndDrop.GetGenericData(ObjectPlacementDragKey);
        if (draggedPlacement is PlacementKind draggedPlacementKind &&
            (e.type == EventType.DragUpdated || e.type == EventType.DragPerform))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                placementKind = draggedPlacementKind;
                CreatePlacementAt(Snap(GetMouseWorldPosition(e)));
                DragAndDrop.SetGenericData(ObjectPlacementDragKey, null);
                RefreshAll();
            }

            e.Use();
            return;
        }

        GameObject draggedMonster = DragAndDrop.GetGenericData(MonsterPrefabDragKey) as GameObject;
        if (draggedMonster != null && (e.type == EventType.DragUpdated || e.type == EventType.DragPerform))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                selectedMonsterPrefab = draggedMonster;
                CreateMonsterSpawnAt(Snap(GetMouseWorldPosition(e)), draggedMonster);
                RefreshAll();
            }

            e.Use();
            return;
        }

        StageMonsterSetSO draggedMonsterSet = DragAndDrop.GetGenericData(StageMonsterSetDragKey) as StageMonsterSetSO;
        if (draggedMonsterSet != null && (e.type == EventType.DragUpdated || e.type == EventType.DragPerform))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                selectedStageMonsterSet = draggedMonsterSet;
                selectedMonsterSpawnSource = MonsterSpawnPlacementSource.StageMonsterSet;
                CreateMonsterSpawnAt(Snap(GetMouseWorldPosition(e)), draggedMonsterSet);
                RefreshAll();
            }

            e.Use();
            return;
        }

        if (placementKind == PlacementKind.None || e.type != EventType.MouseDown || e.button != 0)
            return;

        CreatePlacementAt(Snap(GetMouseWorldPosition(e)));
        e.Use();
        RefreshAll();
    }

    private void HandleBattleRoomDrawingInput(Event e)
    {
        if (roomDrawShape == RoomDrawShape.Polygon)
            HandlePolygonRoomDrawingInput(e);
        else
            HandleRectangleRoomDrawingInput(e);
    }

    private void HandleRectangleRoomDrawingInput(Event e)
    {
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            roomDragStart = Snap(GetMouseWorldPosition(e));
            roomDragCurrent = roomDragStart;
            isDraggingRoom = true;
            e.Use();
            return;
        }

        if (isDraggingRoom && e.type == EventType.MouseDrag)
        {
            roomDragCurrent = Snap(GetMouseWorldPosition(e));
            SceneView.RepaintAll();
            e.Use();
            return;
        }

        if (isDraggingRoom && e.type == EventType.Repaint)
        {
            Handles.DrawSolidRectangleWithOutline(
                BuildRectangle(CreateRect(roomDragStart, roomDragCurrent)),
                new Color(0.1f, 0.65f, 1f, 0.11f),
                new Color(0.2f, 0.9f, 1f, 0.95f));
        }

        if (!isDraggingRoom || e.type != EventType.MouseUp || e.button != 0)
            return;

        roomDragCurrent = Snap(GetMouseWorldPosition(e));
        Rect roomRect = CreateRect(roomDragStart, roomDragCurrent);
        isDraggingRoom = false;
        drawRoomMode = false;

        if (roomRect.width >= 0.5f && roomRect.height >= 0.5f)
        {
            selectedRoomGroup = CreateBattleRoom(roomRect);
            RefreshAll();
        }

        e.Use();
    }

    private void HandlePolygonRoomDrawingInput(Event e)
    {
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            ClearRoomDrawingState();
            drawRoomMode = false;
            ShowNotification(new GUIContent("다각형 방 생성을 취소했습니다."));
            e.Use();
            return;
        }

        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Backspace || e.keyCode == KeyCode.Delete))
        {
            if (roomPolygonPoints.Count > 0)
            {
                roomPolygonPoints.RemoveAt(roomPolygonPoints.Count - 1);
                SceneView.RepaintAll();
            }

            e.Use();
            return;
        }

        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
        {
            TryCreatePolygonBattleRoom();
            e.Use();
            return;
        }

        roomPolygonPreviewPoint = Snap(GetMouseWorldPosition(e));

        if (e.type == EventType.Repaint)
            DrawPolygonRoomPreview();

        if (e.type != EventType.MouseDown || e.button != 0)
            return;

        Vector3 point = Snap(GetMouseWorldPosition(e));
        if (ShouldCloseRoomPolygon(e.mousePosition, point))
        {
            TryCreatePolygonBattleRoom();
            e.Use();
            return;
        }

        if (ContainsRoomPolygonPoint(point))
        {
            ShowNotification(new GUIContent("이미 찍은 방 꼭짓점입니다."));
            e.Use();
            return;
        }

        roomPolygonPoints.Add(point);
        SceneView.RepaintAll();
        e.Use();
    }

    private void CreatePlacementAt(Vector3 position)
    {
        switch (placementKind)
        {
            case PlacementKind.Door:
                CreateDoorAt(position);
                break;
            case PlacementKind.Lever:
                InstantiatePrefabAt(leverPrefab, "Shortcuts", position);
                break;
            case PlacementKind.Statue:
                InstantiatePrefabAt(statuePrefab, "Shortcuts", position);
                break;
            case PlacementKind.Chest:
                InstantiatePrefabAt(chestPrefab, "Chests", position);
                break;
            case PlacementKind.KillLockChest:
                CreateKillLockChestAt(position);
                break;
            case PlacementKind.Portal:
                InstantiatePrefabAt(portalPrefab, "Portals", position);
                break;
            case PlacementKind.MonsterSpawn:
                if (selectedMonsterSpawnSource == MonsterSpawnPlacementSource.StageMonsterSet)
                    CreateMonsterSpawnAt(position, selectedStageMonsterSet);
                else
                    CreateMonsterSpawnAt(position, selectedMonsterPrefab);
                break;
        }
    }

    private void CreateDoorAt(Vector3 position)
    {
        (Vector3 doorPosition, Quaternion doorRotation) = ResolveDoorPlacementPose(position);
        GameObject instance = InstantiatePrefabAt(doorPrefab, "Doors", doorPosition);
        if (instance == null)
            return;

        Undo.RecordObject(instance.transform, "Rotate Door");
        instance.transform.rotation = doorRotation;
        EditorUtility.SetDirty(instance.transform);
        EditorSceneManager.MarkSceneDirty(instance.scene);
    }

    private GameObject InstantiatePrefabAt(GameObject prefab, string parentName, Vector3 position)
    {
        if (prefab == null)
        {
            ShowNotification(new GUIContent("Missing prefab reference."));
            return null;
        }

        Scene scene = SceneManager.GetActiveScene();
        Object instanceObject = PrefabUtility.InstantiatePrefab(prefab, scene);
        if (instanceObject is not GameObject instance)
            return null;

        Undo.RegisterCreatedObjectUndo(instance, $"Create {prefab.name}");
        instance.transform.position = position;
        SetParent(instance.transform, FindOrCreateLevelRootChild(parentName));
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeObject = instance;
        return instance;
    }

    private void CreateKillLockChestAt(Vector3 position)
    {
        GameObject instance = InstantiatePrefabAt(killLockChestPrefab != null ? killLockChestPrefab : chestPrefab, "Chests", position);
        if (instance == null)
            return;

        ChestMonsterKillLock chestLock = instance.GetComponent<ChestMonsterKillLock>();
        if (chestLock == null)
            chestLock = Undo.AddComponent<ChestMonsterKillLock>(instance);

        MonsterSpawnRoomGroup roomGroup = selectedRoomGroup != null ? selectedRoomGroup : FindRoomGroupAt(position);
        if (roomGroup != null)
            WireSpawnsInRoomToChestLock(roomGroup, chestLock);

        EditorUtility.SetDirty(instance);
    }

    private void CreateMonsterSpawnAt(Vector3 position, GameObject monsterPrefab)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject spawnObject = new(monsterPrefab != null ? $"MonsterSpawn_{monsterPrefab.name}" : "MonsterSpawn");
        Undo.RegisterCreatedObjectUndo(spawnObject, "Create Monster Spawn");
        spawnObject.transform.position = position;
        SetParent(spawnObject.transform, FindOrCreateLevelRootChild("MonsterSpawns"));

        MonsterSpawnContainer spawn = Undo.AddComponent<MonsterSpawnContainer>(spawnObject);
        AssignEnum(spawn, "sourceKind", (int)MonsterSpawnSourceKind.FixedPrefab);
        AssignReference(spawn, "monsterPrefab", monsterPrefab);
        ConfigureMonsterSpawnRoomReferences(spawn, position);

        EditorUtility.SetDirty(spawn);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeObject = spawnObject;
    }

    private void CreateMonsterSpawnAt(Vector3 position, StageMonsterSetSO monsterSet)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject spawnObject = new(monsterSet != null ? $"MonsterSpawn_{monsterSet.name}" : "MonsterSpawn_Common");
        Undo.RegisterCreatedObjectUndo(spawnObject, "Create Common Monster Spawn");
        spawnObject.transform.position = position;
        SetParent(spawnObject.transform, FindOrCreateLevelRootChild("MonsterSpawns"));

        MonsterSpawnContainer spawn = Undo.AddComponent<MonsterSpawnContainer>(spawnObject);
        AssignEnum(spawn, "sourceKind", (int)MonsterSpawnSourceKind.StageMonsterSet);
        AssignReference(spawn, "stageMonsterSet", monsterSet);
        ConfigureMonsterSpawnRoomReferences(spawn, position);

        EditorUtility.SetDirty(spawn);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeObject = spawnObject;
    }

    private void ConfigureMonsterSpawnRoomReferences(MonsterSpawnContainer spawn, Vector3 position)
    {
        MonsterSpawnRoomGroup roomGroup = selectedRoomGroup != null ? selectedRoomGroup : FindRoomGroupAt(position);
        MonsterRoomArea2D area = roomGroup != null ? ResolveRoomArea(roomGroup) : FindRoomAreaAt(position);
        AssignReference(spawn, "roomGroup", roomGroup);
        AssignReference(spawn, "roomArea", area);

        ChestMonsterKillLock roomChestLock = roomGroup != null ? FindSingleChestLockInRoom(roomGroup) : null;
        if (roomChestLock != null)
            AssignReference(spawn, "linkedChestKillLock", roomChestLock);
    }

    private void CreateBattleRoomAtSceneCenter()
    {
        Vector3 center = GetSceneViewCenter();
        Vector2 halfSize = new(Mathf.Max(0.5f, defaultRoomSize.x) * 0.5f, Mathf.Max(0.5f, defaultRoomSize.y) * 0.5f);
        Rect roomRect = new(center.x - halfSize.x, center.y - halfSize.y, halfSize.x * 2f, halfSize.y * 2f);
        selectedRoomGroup = CreateBattleRoom(roomRect);
        RefreshAll();
    }

    private MonsterSpawnRoomGroup CreateBattleRoom(Rect roomRect)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject roomObject = new(BuildUniqueName("BattleRoom"));
        Undo.RegisterCreatedObjectUndo(roomObject, "Create Battle Room");
        roomObject.transform.position = roomRect.center;
        SetParent(roomObject.transform, FindOrCreateLevelRootChild("BattleRooms"));

        MonsterSpawnRoomGroup group = Undo.AddComponent<MonsterSpawnRoomGroup>(roomObject);
        BoxCollider2D collider = Undo.AddComponent<BoxCollider2D>(roomObject);
        collider.isTrigger = true;
        collider.size = new Vector2(roomRect.width, roomRect.height);

        MonsterRoomArea2D area = Undo.AddComponent<MonsterRoomArea2D>(roomObject);
        AssignReference(area, "areaCollider", collider);

        RoomEncounterEntryTrigger2D trigger = Undo.AddComponent<RoomEncounterEntryTrigger2D>(roomObject);
        AssignReference(trigger, "targetRoomGroup", group);

        EditorUtility.SetDirty(roomObject);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeObject = roomObject;
        return group;
    }

    private MonsterSpawnRoomGroup CreateBattleRoom(IReadOnlyList<Vector3> polygonPoints)
    {
        Scene scene = SceneManager.GetActiveScene();
        Bounds bounds = CalculateBounds(polygonPoints);
        Vector3 center = bounds.center;
        center.z = 0f;

        GameObject roomObject = new(BuildUniqueName("BattleRoom"));
        Undo.RegisterCreatedObjectUndo(roomObject, "Create Battle Room");
        roomObject.transform.position = center;
        SetParent(roomObject.transform, FindOrCreateLevelRootChild("BattleRooms"));

        MonsterSpawnRoomGroup group = Undo.AddComponent<MonsterSpawnRoomGroup>(roomObject);
        PolygonCollider2D collider = Undo.AddComponent<PolygonCollider2D>(roomObject);
        collider.isTrigger = true;
        collider.pathCount = 1;
        collider.SetPath(0, BuildLocalPolygonPath(polygonPoints, center));

        MonsterRoomArea2D area = Undo.AddComponent<MonsterRoomArea2D>(roomObject);
        AssignReference(area, "areaCollider", collider);

        RoomEncounterEntryTrigger2D trigger = Undo.AddComponent<RoomEncounterEntryTrigger2D>(roomObject);
        AssignReference(trigger, "targetRoomGroup", group);

        EditorUtility.SetDirty(roomObject);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeObject = roomObject;
        return group;
    }

    private void CreateDoorKillLockForSelection(MonsterSpawnRoomGroup roomGroup)
    {
        if (roomGroup == null)
            return;

        DoorObject selectedDoor = ResolveSelection<DoorObject>();
        if (selectedDoor == null)
        {
            ShowNotification(new GUIContent("Select a DoorObject first."));
            return;
        }

        RoomDoorMonsterKillLock doorLock = selectedDoor.GetComponent<RoomDoorMonsterKillLock>();
        if (doorLock == null)
            doorLock = Undo.AddComponent<RoomDoorMonsterKillLock>(selectedDoor.gameObject);

        AssignReference(doorLock, "targetDoor", selectedDoor);
        AssignReference(doorLock, "targetRoomGroup", roomGroup);
        EditorUtility.SetDirty(doorLock);
        EditorSceneManager.MarkSceneDirty(selectedDoor.gameObject.scene);
        Selection.activeObject = doorLock;
        RefreshAll();
    }

    private void AutoWireRoom(MonsterSpawnRoomGroup roomGroup)
    {
        if (roomGroup == null)
            return;

        MonsterRoomArea2D area = ResolveRoomArea(roomGroup);
        if (area == null)
        {
            ShowNotification(new GUIContent("Selected room has no MonsterRoomArea2D."));
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Auto Link Battle Room");

        ChestMonsterKillLock roomChestLock = FindSingleChestLockInRoom(roomGroup);
        foreach (MonsterSpawnContainer spawn in spawnContainers)
        {
            if (spawn == null || !Contains(area, spawn.transform.position))
                continue;

            AssignReference(spawn, "roomGroup", roomGroup);
            AssignReference(spawn, "roomArea", area);
            if (roomChestLock != null)
                AssignReference(spawn, "linkedChestKillLock", roomChestLock);

            EditorUtility.SetDirty(spawn);
        }

        foreach (RoomDoorMonsterKillLock doorLock in doorLocks)
        {
            if (doorLock == null || !Contains(area, doorLock.transform.position))
                continue;

            AssignReference(doorLock, "targetRoomGroup", roomGroup);
            if (ReadReference<DoorObject>(doorLock, "targetDoor") == null)
            {
                DoorObject door = doorLock.GetComponent<DoorObject>() ?? doorLock.GetComponentInChildren<DoorObject>();
                AssignReference(doorLock, "targetDoor", door);
            }

            EditorUtility.SetDirty(doorLock);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(roomGroup.gameObject.scene);
        RefreshAll();
    }

    private void WireSpawnsInRoomToChestLock(MonsterSpawnRoomGroup roomGroup, ChestMonsterKillLock chestLock)
    {
        MonsterRoomArea2D area = ResolveRoomArea(roomGroup);
        if (area == null || chestLock == null)
            return;

        foreach (MonsterSpawnContainer spawn in spawnContainers)
        {
            if (spawn == null || !Contains(area, spawn.transform.position))
                continue;

            AssignReference(spawn, "linkedChestKillLock", chestLock);
            if (ResolveRoomGroup(spawn) == null)
                AssignReference(spawn, "roomGroup", roomGroup);
            if (spawn.RoomArea == null)
                AssignReference(spawn, "roomArea", area);
            EditorUtility.SetDirty(spawn);
        }
    }

    private void LinkShortcutToDoor(ShortcutBase shortcut, DoorObject door)
    {
        if (shortcut == null || door == null)
            return;

        Undo.RecordObjects(new Object[] { shortcut, door }, "Link Shortcut To Door");
        AssignReference(shortcut, "targetDoor", door);
        AssignReference(shortcut, "lastSyncedTargetDoor", door);
        if (shortcut.TryGetRequiredDoorConfiguration(out DoorObject.DoorType requiredType, out bool requiredPermanent))
            door.ApplyConfigurationFromShortcut(requiredType, requiredPermanent, shortcut);
        door.EditorSyncConfigurationFromLinkedShortcuts();

        EditorUtility.SetDirty(shortcut);
        EditorUtility.SetDirty(door);
        EditorSceneManager.MarkSceneDirty(shortcut.gameObject.scene);
        ClearLinkSources();
        RefreshAll();
    }

    private void LinkSpawnToChestLock(MonsterSpawnContainer spawn, ChestMonsterKillLock chestLock)
    {
        if (spawn == null || chestLock == null)
            return;

        AssignReference(spawn, "linkedChestKillLock", chestLock);

        MonsterSpawnRoomGroup roomGroup = ResolveRoomGroup(spawn) ?? FindRoomGroupAt(spawn.transform.position);
        MonsterRoomArea2D roomArea = spawn.RoomArea != null ? spawn.RoomArea : FindRoomAreaAt(spawn.transform.position);
        if (roomGroup != null)
            AssignReference(spawn, "roomGroup", roomGroup);
        if (roomArea != null)
            AssignReference(spawn, "roomArea", roomArea);

        EditorUtility.SetDirty(spawn);
        EditorSceneManager.MarkSceneDirty(spawn.gameObject.scene);

        Selection.activeObject = spawn;
        ClearLinkSources();
        RefreshAll();
    }

    private RoomDoorMonsterKillLock LinkRoomToDoor(MonsterSpawnRoomGroup roomGroup, DoorObject door)
    {
        if (roomGroup == null || door == null)
            return null;

        RoomDoorMonsterKillLock doorLock = door.GetComponent<RoomDoorMonsterKillLock>();
        if (doorLock == null)
            doorLock = Undo.AddComponent<RoomDoorMonsterKillLock>(door.gameObject);

        LinkRoomToDoorLock(roomGroup, doorLock);
        LinkDoorToDoorLock(door, doorLock);

        selectedRoomGroup = roomGroup;
        Selection.activeObject = doorLock;
        ClearLinkSources();
        RefreshAll();
        return doorLock;
    }

    private void LinkRoomToDoorLock(MonsterSpawnRoomGroup roomGroup, RoomDoorMonsterKillLock doorLock)
    {
        if (roomGroup == null || doorLock == null)
            return;

        AssignReference(doorLock, "targetRoomGroup", roomGroup);
        if (ReadReference<DoorObject>(doorLock, "targetDoor") == null)
        {
            DoorObject door = doorLock.GetComponent<DoorObject>() ?? doorLock.GetComponentInParent<DoorObject>(true);
            AssignReference(doorLock, "targetDoor", door);
        }

        selectedRoomGroup = roomGroup;
        EditorUtility.SetDirty(doorLock);
        EditorSceneManager.MarkSceneDirty(doorLock.gameObject.scene);
        ClearLinkSources();
        RefreshAll();
    }

    private void LinkDoorToDoorLock(DoorObject door, RoomDoorMonsterKillLock doorLock)
    {
        if (door == null || doorLock == null)
            return;

        AssignReference(doorLock, "targetDoor", door);
        EditorUtility.SetDirty(doorLock);
        EditorSceneManager.MarkSceneDirty(doorLock.gameObject.scene);
        ClearLinkSources();
        RefreshAll();
    }

    private void FixMissingAndDuplicateDoorIds()
    {
        ValidateActiveScope();
        List<DoorObject> targets = FindDoorIdFixTargets();
        if (targets.Count == 0)
        {
            ShowNotification(new GUIContent("No Door ID issues found."));
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Fix Door IDs",
            $"Generate new IDs for {targets.Count} empty or duplicate DoorObject entries? Existing saved shortcut data can stop matching changed IDs.",
            "Fix",
            "Cancel");

        if (!confirmed)
            return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Fix Door IDs");

        foreach (DoorObject door in targets)
        {
            if (door == null)
                continue;

            Undo.RecordObject(door, "Fix Door ID");
            door.GenerateID();
            EditorUtility.SetDirty(door);
            EditorSceneManager.MarkSceneDirty(door.gameObject.scene);
        }

        Undo.CollapseUndoOperations(undoGroup);
        RefreshAll();
    }

    private List<DoorObject> FindDoorIdFixTargets()
    {
        List<DoorObject> targets = new();
        Dictionary<string, DoorObject> seen = new(StringComparer.Ordinal);
        foreach (DoorObject door in doors)
        {
            if (door == null)
                continue;

            string key = BuildDoorKey(door);
            if (string.IsNullOrWhiteSpace(door.doorID) || string.IsNullOrWhiteSpace(key))
            {
                targets.Add(door);
                continue;
            }

            if (seen.ContainsKey(key))
                targets.Add(door);
            else
                seen.Add(key, door);
        }

        return targets;
    }

    private void RefreshAll()
    {
        RefreshSceneCache();
        RefreshMonsterPalette();
        ValidateActiveScope();
        Repaint();
        SceneView.RepaintAll();
    }

    private void RefreshSceneCache()
    {
        doors = FindSceneObjects<DoorObject>();
        shortcuts = FindSceneObjects<ShortcutBase>();
        roomGroups = FindSceneObjects<MonsterSpawnRoomGroup>();
        roomAreas = FindSceneObjects<MonsterRoomArea2D>();
        spawnContainers = FindSceneObjects<MonsterSpawnContainer>();
        chests = FindSceneObjects<TreasureChest>();
        chestLocks = FindSceneObjects<ChestMonsterKillLock>();
        doorLocks = FindSceneObjects<RoomDoorMonsterKillLock>();
        portals = FindSceneObjects<ScenePortal>();

        if (selectedRoomGroup != null && !roomGroups.Contains(selectedRoomGroup))
        {
            selectedRoomGroup = null;
            editRoomGridMode = false;
        }
    }

    private void RefreshMonsterPalette()
    {
        monsterPrefabs.Clear();
        stageMonsterSets.Clear();
        string[] folders = AssetDatabase.IsValidFolder(MonsterPrefabRoot)
            ? new[] { MonsterPrefabRoot }
            : new[] { "Assets" };

        string[] guids = AssetDatabase.FindAssets("t:Prefab", folders);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            if (prefab.GetComponentInChildren<Mob>(true) == null && prefab.GetComponentInChildren<Enemy>(true) == null)
                continue;

            if (!monsterPrefabs.Contains(prefab))
                monsterPrefabs.Add(prefab);
        }

        if (selectedMonsterPrefab == null && monsterPrefabs.Count > 0)
            selectedMonsterPrefab = monsterPrefabs[0];

        string[] setGuids = AssetDatabase.FindAssets("t:StageMonsterSetSO");
        foreach (string guid in setGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StageMonsterSetSO monsterSet = AssetDatabase.LoadAssetAtPath<StageMonsterSetSO>(path);
            if (monsterSet != null && !stageMonsterSets.Contains(monsterSet))
                stageMonsterSets.Add(monsterSet);
        }

        if (selectedStageMonsterSet == null && stageMonsterSets.Count > 0)
            selectedStageMonsterSet = stageMonsterSets[0];
    }

    private void ValidateActiveScope()
    {
        validationResults.Clear();
        ValidateDoors();
        ValidateShortcuts();
        ValidateRooms();
        ValidateSpawns();
        ValidateChests();
        ValidateDoorLocks();
        ValidatePortals();
    }

    private void ValidateDoors()
    {
        Dictionary<string, List<DoorObject>> groups = new(StringComparer.Ordinal);
        foreach (DoorObject door in doors)
        {
            if (door == null)
                continue;

            if (string.IsNullOrWhiteSpace(door.doorID))
            {
                AddResult(Severity.Error, "DoorObject.doorID is empty.", door);
                continue;
            }

            if (string.IsNullOrWhiteSpace(door.mapID))
                AddResult(Severity.Warning, "DoorObject.mapID is empty. Runtime fills active scene name, but authoring review is safer.", door);

            string key = BuildDoorKey(door);
            if (!groups.TryGetValue(key, out List<DoorObject> group))
            {
                group = new List<DoorObject>();
                groups.Add(key, group);
            }

            group.Add(door);
        }

        foreach (List<DoorObject> group in groups.Values)
        {
            if (group.Count <= 1)
                continue;

            foreach (DoorObject duplicate in group)
                AddResult(Severity.Error, $"Duplicate Door ID: {BuildDoorKey(duplicate)}", duplicate);
        }
    }

    private void ValidateShortcuts()
    {
        Dictionary<DoorObject, List<ShortcutBase>> byDoor = new();
        foreach (ShortcutBase shortcut in shortcuts)
        {
            if (shortcut == null)
                continue;

            if (shortcut.TargetDoor == null)
            {
                AddResult(Severity.Error, $"{shortcut.GetType().Name} has no targetDoor.", shortcut);
                continue;
            }

            if (!byDoor.TryGetValue(shortcut.TargetDoor, out List<ShortcutBase> linked))
            {
                linked = new List<ShortcutBase>();
                byDoor.Add(shortcut.TargetDoor, linked);
            }

            linked.Add(shortcut);

            if (shortcut.TryGetRequiredDoorConfiguration(out DoorObject.DoorType doorType, out bool permanent))
            {
                if (shortcut.TargetDoor.doorType != doorType || shortcut.TargetDoor.isPermanent != permanent)
                    AddResult(Severity.Warning, $"{shortcut.GetType().Name} requires {doorType}/{permanent}, but target Door has {shortcut.TargetDoor.doorType}/{shortcut.TargetDoor.isPermanent}.", shortcut);
            }

            if (shortcut is StatueShortcut statue)
                ValidateStatue(statue);
        }

        foreach (KeyValuePair<DoorObject, List<ShortcutBase>> pair in byDoor)
        {
            if (pair.Value.Count <= 1)
                continue;

            DoorObject.DoorType? type = null;
            bool? permanent = null;
            foreach (ShortcutBase shortcut in pair.Value)
            {
                if (!shortcut.TryGetRequiredDoorConfiguration(out DoorObject.DoorType shortcutType, out bool shortcutPermanent))
                    continue;

                type ??= shortcutType;
                permanent ??= shortcutPermanent;
                if (type != shortcutType || permanent != shortcutPermanent)
                    AddResult(Severity.Warning, $"Door '{pair.Key.name}' has shortcuts with conflicting required door configuration.", pair.Key);
            }
        }
    }

    private void ValidateStatue(StatueShortcut statue)
    {
        SerializedObject serializedObject = new(statue);
        SerializedProperty costAmount = serializedObject.FindProperty("costAmount");
        SerializedProperty costType = serializedObject.FindProperty("costType");
        SerializedProperty healthAttribute = serializedObject.FindProperty("healthAttribute");

        if (costAmount != null && costAmount.intValue <= 0)
            AddResult(Severity.Warning, "Statue costAmount should be greater than 0.", statue);

        if (costType != null &&
            costType.enumValueIndex == (int)StatueShortcut.CostType.HP &&
            healthAttribute != null &&
            healthAttribute.objectReferenceValue == null)
        {
            AddResult(Severity.Error, "HP-cost Statue is missing healthAttribute.", statue);
        }
    }

    private void ValidateRooms()
    {
        foreach (MonsterSpawnRoomGroup group in roomGroups)
        {
            if (group == null)
                continue;

            MonsterRoomArea2D area = ResolveRoomArea(group);
            if (area == null)
                AddResult(Severity.Error, "MonsterSpawnRoomGroup has no MonsterRoomArea2D in children.", group);
            else if (ResolveAreaCollider(area) == null)
                AddResult(Severity.Error, "MonsterRoomArea2D has no area Collider2D.", area);

            if (group.SpawnProfile == null)
                AddResult(Severity.Warning, "MonsterSpawnRoomGroup.spawnProfile is empty. Direct spawn containers can still work, but random room profile spawning will not.", group);
        }
    }

    private void ValidateSpawns()
    {
        foreach (MonsterSpawnContainer spawn in spawnContainers)
        {
            if (spawn == null)
                continue;

            if (!spawn.TryResolveMonsterPrefab(0, out _))
            {
                if (spawn.SourceKind == MonsterSpawnSourceKind.StageMonsterSet)
                    AddResult(Severity.Error, "MonsterSpawnContainer.stageMonsterSet is empty or cannot resolve stage 0.", spawn);
                else
                    AddResult(Severity.Error, "MonsterSpawnContainer.monsterPrefab is empty.", spawn);
            }

            MonsterSpawnRoomGroup group = ResolveRoomGroup(spawn);
            MonsterRoomArea2D area = spawn.RoomArea != null ? spawn.RoomArea : FindRoomAreaAt(spawn.transform.position);
            if (group == null)
                AddResult(Severity.Warning, "MonsterSpawnContainer.roomGroup is empty.", spawn);

            if (area == null)
                AddResult(Severity.Warning, "MonsterSpawnContainer is not inside a known MonsterRoomArea2D.", spawn);

            if (spawn.LinkedChestKillLock != null && area != null && !Contains(area, spawn.LinkedChestKillLock.transform.position))
                AddResult(Severity.Warning, "Spawn is linked to a ChestMonsterKillLock outside its room area.", spawn);
        }
    }

    private void ValidateChests()
    {
        foreach (TreasureChest chest in chests)
        {
            if (chest == null)
                continue;

            if (chest.GetComponent<ChestInteractable>() == null)
                AddResult(Severity.Warning, "TreasureChest has no ChestInteractable on the same GameObject.", chest);
        }

        foreach (ChestMonsterKillLock chestLock in chestLocks)
        {
            if (chestLock == null)
                continue;

            bool hasLinkedSpawn = spawnContainers.Any(spawn => spawn != null && spawn.LinkedChestKillLock == chestLock);
            if (!hasLinkedSpawn)
                AddResult(Severity.Warning, "ChestMonsterKillLock has no linked MonsterSpawnContainer.", chestLock);
        }
    }

    private void ValidateDoorLocks()
    {
        foreach (RoomDoorMonsterKillLock doorLock in doorLocks)
        {
            if (doorLock == null)
                continue;

            if (ReadReference<DoorObject>(doorLock, "targetDoor") == null)
                AddResult(Severity.Error, "RoomDoorMonsterKillLock.targetDoor is empty.", doorLock);

            if (ReadReference<MonsterSpawnRoomGroup>(doorLock, "targetRoomGroup") == null)
                AddResult(Severity.Error, "RoomDoorMonsterKillLock.targetRoomGroup is empty.", doorLock);
        }
    }

    private void ValidatePortals()
    {
        Dictionary<string, List<ScenePortal>> portalIds = new(StringComparer.Ordinal);
        foreach (ScenePortal portal in portals)
        {
            if (portal == null)
                continue;

            if (string.IsNullOrWhiteSpace(portal.PortalId))
                AddResult(Severity.Error, "ScenePortal.portalId is empty.", portal);
            else
            {
                if (!portalIds.TryGetValue(portal.PortalId, out List<ScenePortal> group))
                {
                    group = new List<ScenePortal>();
                    portalIds.Add(portal.PortalId, group);
                }

                group.Add(portal);
            }

            if (portal.PortalTransitionType == TransitionType.HubToRunStart && portal.StartRunRouteCatalog == null)
                AddResult(Severity.Error, "Hub start ScenePortal is missing startRunRouteCatalog.", portal);

            if (portal.PortalTransitionType != TransitionType.HubToRunStart && portal.StartRunRouteCatalog != null)
                AddResult(Severity.Warning, "Non-hub ScenePortal carries startRunRouteCatalog. Clear it unless this is intentionally a hub start portal.", portal);
        }

        foreach (List<ScenePortal> group in portalIds.Values)
        {
            if (group.Count <= 1)
                continue;

            foreach (ScenePortal portal in group)
                AddResult(Severity.Error, $"Duplicate ScenePortal portalId: {portal.PortalId}", portal);
        }
    }

    private void AddResult(Severity severity, string message, Object context)
    {
        validationResults.Add(new ValidationResult
        {
            SeverityLevel = severity,
            Message = message,
            Context = context,
            ObjectPath = context is Component component ? GetObjectPath(component.transform) : string.Empty
        });
    }

    private T[] FindSceneObjects<T>() where T : Component
    {
        T[] all = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        Scene activeScene = SceneManager.GetActiveScene();
        List<T> result = new();
        foreach (T component in all)
        {
            if (component == null || !component.gameObject.scene.IsValid())
                continue;

            if (searchScope == SearchScope.ActiveScene && component.gameObject.scene.handle != activeScene.handle)
                continue;

            result.Add(component);
        }

        return result.ToArray();
    }

    private void LoadDefaultPrefabs()
    {
        doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
        leverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LeverPrefabPath);
        statuePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StatuePrefabPath);
        chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefabPath);
        killLockChestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KillLockChestPrefabPath);
        portalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PortalPrefabPath);
    }

    private Bounds CalculateLevelBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new(Vector3.zero, new Vector3(24f, 16f, 0f));
        foreach (Component component in doors.Cast<Component>()
                     .Concat(shortcuts)
                     .Concat(roomAreas)
                     .Concat(spawnContainers)
                     .Concat(chests)
                     .Concat(portals))
        {
            if (component == null)
                continue;

            Vector3 position = component.transform.position;
            if (!hasBounds)
            {
                bounds = new Bounds(position, Vector3.one);
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(position);
            }
        }

        if (!hasBounds)
            bounds.center = GetSceneViewCenter();

        bounds.Expand(new Vector3(8f, 8f, 0f));
        return bounds;
    }

    private static Vector3 GetSceneViewCenter()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
            return Vector3.zero;

        Vector3 center = sceneView.pivot;
        center.z = 0f;
        return center;
    }

    private Vector3 Snap(Vector3 position)
    {
        if (!snapToGrid || gridSize <= 0f)
            return position;

        position.x = Mathf.Round(position.x / gridSize) * gridSize;
        position.y = Mathf.Round(position.y / gridSize) * gridSize;
        position.z = 0f;
        return position;
    }

    private static Vector3 GetMouseWorldPosition(Event e)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Plane plane = new(Vector3.forward, Vector3.zero);
        if (plane.Raycast(ray, out float distance))
        {
            Vector3 point = ray.GetPoint(distance);
            point.z = 0f;
            return point;
        }

        Vector3 fallback = ray.origin;
        fallback.z = 0f;
        return fallback;
    }

    private void ClearRoomDrawingState()
    {
        isDraggingRoom = false;
        roomPolygonPoints.Clear();
        roomPolygonPreviewPoint = Vector3.zero;
    }

    private void DrawPolygonRoomPreview()
    {
        if (roomPolygonPoints.Count == 0)
            return;

        Vector3[] points = BuildPolygonPreviewPath();
        Handles.color = new Color(0.2f, 0.9f, 1f, 0.95f);
        Handles.DrawAAPolyLine(3f, points);

        for (int i = 0; i < roomPolygonPoints.Count; i++)
        {
            Vector3 point = roomPolygonPoints[i];
            float size = HandleUtility.GetHandleSize(point) * 0.06f;
            Handles.color = i == 0
                ? new Color(0.2f, 1f, 0.35f, 1f)
                : new Color(0.2f, 0.9f, 1f, 1f);
            Handles.DrawSolidDisc(point, Vector3.forward, size);
        }
    }

    private Vector3[] BuildPolygonPreviewPath()
    {
        int pointCount = roomPolygonPoints.Count;
        bool canClose = pointCount >= 3;
        int extraPointCount = canClose ? 2 : 1;
        Vector3[] points = new Vector3[pointCount + extraPointCount];

        for (int i = 0; i < pointCount; i++)
            points[i] = roomPolygonPoints[i];

        points[pointCount] = roomPolygonPreviewPoint;
        if (canClose)
            points[pointCount + 1] = roomPolygonPoints[0];

        return points;
    }

    private bool ShouldCloseRoomPolygon(Vector2 mousePosition, Vector3 point)
    {
        if (roomPolygonPoints.Count < 3)
            return false;

        Vector3 firstPoint = roomPolygonPoints[0];
        if (IsSameGridPoint(firstPoint, point))
            return true;

        return Vector2.Distance(mousePosition, HandleUtility.WorldToGUIPoint(firstPoint)) <= 18f;
    }

    private bool ContainsRoomPolygonPoint(Vector3 point)
    {
        for (int i = 0; i < roomPolygonPoints.Count; i++)
        {
            if (IsSameGridPoint(roomPolygonPoints[i], point))
                return true;
        }

        return false;
    }

    private void TryCreatePolygonBattleRoom()
    {
        if (roomPolygonPoints.Count < 3)
        {
            ShowNotification(new GUIContent("다각형 방은 꼭짓점이 3개 이상 필요합니다."));
            return;
        }

        if (HasDuplicatePolygonPoints(roomPolygonPoints))
        {
            ShowNotification(new GUIContent("다각형 방에 중복 꼭짓점이 있습니다."));
            return;
        }

        if (HasSelfIntersection(roomPolygonPoints))
        {
            ShowNotification(new GUIContent("자기 교차 다각형 방은 생성할 수 없습니다."));
            return;
        }

        selectedRoomGroup = CreateBattleRoom(roomPolygonPoints);
        ClearRoomDrawingState();
        drawRoomMode = false;
        RefreshAll();
    }

    private (Vector3 Position, Quaternion Rotation) ResolveDoorPlacementPose(Vector3 position)
    {
        Vector3 resolvedPosition = Snap(position);
        DoorPlacementOrientation resolvedOrientation = doorPlacementOrientation;

        if (TryResolveWallTilePlacement(position, out Vector3 wallPosition, out DoorPlacementOrientation wallOrientation))
        {
            resolvedPosition = wallPosition;
            if (resolvedOrientation == DoorPlacementOrientation.Auto)
                resolvedOrientation = wallOrientation;
        }

        if (resolvedOrientation == DoorPlacementOrientation.Auto &&
            TryResolveWallColliderOrientation(resolvedPosition, out DoorPlacementOrientation colliderOrientation))
        {
            resolvedOrientation = colliderOrientation;
        }

        if (resolvedOrientation == DoorPlacementOrientation.Auto)
            resolvedOrientation = DoorPlacementOrientation.Horizontal;

        float zRotation = resolvedOrientation == DoorPlacementOrientation.Vertical ? 90f : 0f;
        return (resolvedPosition, Quaternion.Euler(0f, 0f, zRotation));
    }

    private bool TryResolveWallTilePlacement(Vector3 position, out Vector3 snappedPosition, out DoorPlacementOrientation orientation)
    {
        snappedPosition = Snap(position);
        orientation = DoorPlacementOrientation.Auto;

        Tilemap[] tilemaps = FindSceneObjects<Tilemap>();
        float maxDistance = Mathf.Max(0.75f, gridSize * 1.25f);
        float maxDistanceSqr = maxDistance * maxDistance;
        float bestDistanceSqr = maxDistanceSqr;
        Tilemap bestTilemap = null;
        Vector3Int bestCell = default;
        Vector3 bestPosition = snappedPosition;

        foreach (Tilemap tilemap in tilemaps)
        {
            if (!IsWallTilemap(tilemap))
                continue;

            Vector3Int baseCell = tilemap.WorldToCell(position);
            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    Vector3Int cell = baseCell + new Vector3Int(x, y, 0);
                    if (!tilemap.HasTile(cell))
                        continue;

                    Vector3 cellCenter = tilemap.GetCellCenterWorld(cell);
                    cellCenter.z = 0f;
                    float distanceSqr = (cellCenter - position).sqrMagnitude;
                    if (distanceSqr > bestDistanceSqr)
                        continue;

                    bestDistanceSqr = distanceSqr;
                    bestTilemap = tilemap;
                    bestCell = cell;
                    bestPosition = cellCenter;
                }
            }
        }

        if (bestTilemap == null)
            return false;

        snappedPosition = bestPosition;
        orientation = ResolveWallTileOrientation(bestTilemap, bestCell);
        return true;
    }

    private DoorPlacementOrientation ResolveWallTileOrientation(Tilemap tilemap, Vector3Int cell)
    {
        int horizontal = CountWallNeighbor(tilemap, cell, Vector3Int.left) +
                         CountWallNeighbor(tilemap, cell, Vector3Int.right);
        int vertical = CountWallNeighbor(tilemap, cell, Vector3Int.up) +
                       CountWallNeighbor(tilemap, cell, Vector3Int.down);

        if (vertical > horizontal)
            return DoorPlacementOrientation.Vertical;

        if (horizontal > vertical)
            return DoorPlacementOrientation.Horizontal;

        return DoorPlacementOrientation.Auto;
    }

    private static int CountWallNeighbor(Tilemap tilemap, Vector3Int cell, Vector3Int offset)
    {
        return tilemap != null && tilemap.HasTile(cell + offset) ? 1 : 0;
    }

    private static bool IsWallTilemap(Tilemap tilemap)
    {
        if (tilemap == null)
            return false;

        int wallLayer = LayerMask.NameToLayer("Wall");
        return (wallLayer >= 0 && tilemap.gameObject.layer == wallLayer) ||
               tilemap.name.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool TryResolveWallColliderOrientation(Vector3 position, out DoorPlacementOrientation orientation)
    {
        orientation = DoorPlacementOrientation.Auto;
        float step = Mathf.Max(0.25f, gridSize);
        int horizontal = CountWallColliderAt(position + Vector3.left * step) +
                         CountWallColliderAt(position + Vector3.right * step);
        int vertical = CountWallColliderAt(position + Vector3.up * step) +
                       CountWallColliderAt(position + Vector3.down * step);

        if (vertical > horizontal)
        {
            orientation = DoorPlacementOrientation.Vertical;
            return true;
        }

        if (horizontal > vertical)
        {
            orientation = DoorPlacementOrientation.Horizontal;
            return true;
        }

        return false;
    }

    private int CountWallColliderAt(Vector3 position)
    {
        int wallLayer = LayerMask.NameToLayer("Wall");
        if (wallLayer < 0)
            return 0;

        Collider2D collider = Physics2D.OverlapPoint(position, 1 << wallLayer);
        if (collider == null)
            return 0;

        Scene activeScene = SceneManager.GetActiveScene();
        if (searchScope == SearchScope.ActiveScene && collider.gameObject.scene.handle != activeScene.handle)
            return 0;

        return 1;
    }

    private Component PickLevelDesignComponent(Vector2 guiPosition)
    {
        const float MaxDistance = 28f;
        Component best = null;
        float bestDistance = MaxDistance;

        IEnumerable<Component> candidates;
        if (linkingShortcut != null)
        {
            candidates = doors.Cast<Component>().Concat(doorLocks).Concat(shortcuts);
        }
        else if (linkingChestLock != null)
        {
            candidates = spawnContainers.Cast<Component>().Concat(chestLocks).Concat(chests);
        }
        else if (linkingSpawn != null)
        {
            candidates = chestLocks.Cast<Component>().Concat(chests).Concat(spawnContainers);
        }
        else if (linkingRoomGroup != null)
        {
            candidates = doors.Cast<Component>().Concat(doorLocks).Concat(roomGroups);
        }
        else if (linkingDoor != null)
        {
            candidates = roomGroups.Cast<Component>().Concat(doorLocks).Concat(doors);
        }
        else
        {
            candidates = shortcuts.Cast<Component>()
                .Concat(doors)
                .Concat(doorLocks)
                .Concat(chestLocks)
                .Concat(spawnContainers)
                .Concat(chests)
                .Concat(portals)
                .Concat(roomGroups);
        }

        foreach (Component component in candidates)
        {
            if (component == null)
                continue;

            float distance = Vector2.Distance(guiPosition, HandleUtility.WorldToGUIPoint(component.transform.position));
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = component;
        }

        return best;
    }

    private static Rect CreateRect(Vector3 a, Vector3 b)
    {
        float minX = Mathf.Min(a.x, b.x);
        float maxX = Mathf.Max(a.x, b.x);
        float minY = Mathf.Min(a.y, b.y);
        float maxY = Mathf.Max(a.y, b.y);
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static Vector3[] BuildRectangle(Rect rect)
    {
        return new[]
        {
            new Vector3(rect.xMin, rect.yMin, 0f),
            new Vector3(rect.xMin, rect.yMax, 0f),
            new Vector3(rect.xMax, rect.yMax, 0f),
            new Vector3(rect.xMax, rect.yMin, 0f)
        };
    }

    private static Vector3[] BuildRectangle(Bounds bounds)
    {
        return new[]
        {
            new Vector3(bounds.min.x, bounds.min.y, 0f),
            new Vector3(bounds.min.x, bounds.max.y, 0f),
            new Vector3(bounds.max.x, bounds.max.y, 0f),
            new Vector3(bounds.max.x, bounds.min.y, 0f)
        };
    }

    private static Vector3[] GetBoxWorldCorners(BoxCollider2D boxCollider)
    {
        Vector2 offset = boxCollider.offset;
        Vector2 halfSize = boxCollider.size * 0.5f;
        return new[]
        {
            boxCollider.transform.TransformPoint(new Vector3(offset.x - halfSize.x, offset.y - halfSize.y, 0f)),
            boxCollider.transform.TransformPoint(new Vector3(offset.x - halfSize.x, offset.y + halfSize.y, 0f)),
            boxCollider.transform.TransformPoint(new Vector3(offset.x + halfSize.x, offset.y + halfSize.y, 0f)),
            boxCollider.transform.TransformPoint(new Vector3(offset.x + halfSize.x, offset.y - halfSize.y, 0f))
        };
    }

    private static List<Vector3> GetPolygonWorldPoints(PolygonCollider2D polygonCollider)
    {
        List<Vector3> points = new();
        if (polygonCollider == null || polygonCollider.pathCount == 0)
            return points;

        Vector2[] path = polygonCollider.GetPath(0);
        for (int i = 0; i < path.Length; i++)
        {
            Vector2 localPoint = path[i];
            points.Add(polygonCollider.transform.TransformPoint(new Vector3(localPoint.x, localPoint.y, 0f)));
        }

        return points;
    }

    private static bool IsMouseNearPolygonVertex(IReadOnlyList<Vector3> worldPoints, Vector2 mousePosition, float pixelThreshold)
    {
        return FindClosestPolygonVertexIndex(worldPoints, mousePosition, pixelThreshold) >= 0;
    }

    private static int FindClosestPolygonVertexIndex(IReadOnlyList<Vector3> worldPoints, Vector2 mousePosition, float pixelThreshold)
    {
        int bestIndex = -1;
        float bestDistance = pixelThreshold;
        for (int i = 0; i < worldPoints.Count; i++)
        {
            Vector2 vertexPosition = HandleUtility.WorldToGUIPoint(worldPoints[i]);
            float distance = Vector2.Distance(mousePosition, vertexPosition);
            if (distance > bestDistance)
                continue;

            bestIndex = i;
            bestDistance = distance;
        }

        return bestIndex;
    }

    private static float ClosestPointOnSegment01(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
    {
        Vector2 segment = segmentEnd - segmentStart;
        float lengthSqr = segment.sqrMagnitude;
        if (lengthSqr <= 0.0001f)
            return 0f;

        return Mathf.Clamp01(Vector2.Dot(point - segmentStart, segment) / lengthSqr);
    }

    private static Vector3[] BuildClosedWorldPath(IReadOnlyList<Vector3> worldPoints)
    {
        Vector3[] points = new Vector3[worldPoints.Count + 1];
        for (int i = 0; i < worldPoints.Count; i++)
            points[i] = worldPoints[i];

        points[worldPoints.Count] = points[0];
        return points;
    }

    private static Vector3[] BuildClosedWorldPath(Transform transform, IReadOnlyList<Vector2> localPoints)
    {
        Vector3[] points = new Vector3[localPoints.Count + 1];
        for (int i = 0; i < localPoints.Count; i++)
        {
            Vector2 localPoint = localPoints[i];
            points[i] = transform.TransformPoint(new Vector3(localPoint.x, localPoint.y, 0f));
        }

        points[localPoints.Count] = points[0];
        return points;
    }

    private static Vector2[] BuildLocalPolygonPath(IReadOnlyList<Vector3> worldPoints, Vector3 center)
    {
        Vector2[] localPoints = new Vector2[worldPoints.Count];
        for (int i = 0; i < worldPoints.Count; i++)
        {
            Vector3 localPoint = worldPoints[i] - center;
            localPoints[i] = new Vector2(localPoint.x, localPoint.y);
        }

        return localPoints;
    }

    private static Vector2[] BuildLocalPolygonPath(IReadOnlyList<Vector3> worldPoints, Transform targetTransform)
    {
        Vector2[] localPoints = new Vector2[worldPoints.Count];
        for (int i = 0; i < worldPoints.Count; i++)
        {
            Vector3 localPoint = targetTransform.InverseTransformPoint(worldPoints[i]);
            localPoints[i] = new Vector2(localPoint.x, localPoint.y);
        }

        return localPoints;
    }

    private static Bounds CalculateBounds(IReadOnlyList<Vector3> points)
    {
        Bounds bounds = new(points[0], Vector3.zero);
        for (int i = 1; i < points.Count; i++)
            bounds.Encapsulate(points[i]);

        return bounds;
    }

    private static bool HasDuplicatePolygonPoints(IReadOnlyList<Vector3> points)
    {
        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                if (IsSameGridPoint(points[i], points[j]))
                    return true;
            }
        }

        return false;
    }

    private static bool HasSelfIntersection(IReadOnlyList<Vector3> points)
    {
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Count];

            for (int j = i + 1; j < points.Count; j++)
            {
                if (Mathf.Abs(i - j) <= 1 || (i == 0 && j == points.Count - 1))
                    continue;

                Vector2 c = points[j];
                Vector2 d = points[(j + 1) % points.Count];
                if (SegmentsIntersect(a, b, c, d))
                    return true;
            }
        }

        return false;
    }

    private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float abC = Cross(a, b, c);
        float abD = Cross(a, b, d);
        float cdA = Cross(c, d, a);
        float cdB = Cross(c, d, b);

        if (((abC > 0f && abD < 0f) || (abC < 0f && abD > 0f)) &&
            ((cdA > 0f && cdB < 0f) || (cdA < 0f && cdB > 0f)))
        {
            return true;
        }

        return IsPointOnSegment(a, b, c) ||
               IsPointOnSegment(a, b, d) ||
               IsPointOnSegment(c, d, a) ||
               IsPointOnSegment(c, d, b);
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 ab = b - a;
        Vector2 ac = c - a;
        return (ab.x * ac.y) - (ab.y * ac.x);
    }

    private static bool IsPointOnSegment(Vector2 a, Vector2 b, Vector2 point)
    {
        const float Epsilon = 0.0001f;
        if (Mathf.Abs(Cross(a, b, point)) > Epsilon)
            return false;

        return point.x >= Mathf.Min(a.x, b.x) - Epsilon &&
               point.x <= Mathf.Max(a.x, b.x) + Epsilon &&
               point.y >= Mathf.Min(a.y, b.y) - Epsilon &&
               point.y <= Mathf.Max(a.y, b.y) + Epsilon;
    }

    private static bool IsSameGridPoint(Vector3 a, Vector3 b)
    {
        return ((Vector2)a - (Vector2)b).sqrMagnitude <= 0.0001f;
    }

    private static bool IsAxisAlignedRoomTransform(Transform targetTransform)
    {
        if (targetTransform == null)
            return false;

        Vector3 euler = targetTransform.eulerAngles;
        return Mathf.Abs(Mathf.DeltaAngle(0f, euler.x)) <= 0.1f &&
               Mathf.Abs(Mathf.DeltaAngle(0f, euler.y)) <= 0.1f &&
               Mathf.Abs(Mathf.DeltaAngle(0f, euler.z)) <= 0.1f;
    }

    private static Color ResolveShortcutColor(ShortcutBase shortcut)
    {
        return shortcut switch
        {
            LeverShortcut => new Color(0.2f, 1f, 0.35f, 1f),
            StatueShortcut => new Color(0.3f, 0.7f, 1f, 1f),
            AffectionShortcut => new Color(0.8f, 0.4f, 1f, 1f),
            _ => new Color(0.9f, 0.9f, 0.9f, 1f)
        };
    }

    private static string BuildDoorLabel(DoorObject door)
    {
        if (door == null)
            return "문";

        string id = string.IsNullOrWhiteSpace(door.doorID) ? "ID 없음" : door.doorID;
        return $"문 {door.doorType} {id}";
    }

    private static string BuildSpawnLabel(MonsterSpawnContainer spawn)
    {
        if (spawn == null)
            return "스폰?";

        if (!spawn.TryResolveMonsterPrefab(0, out GameObject stageZeroPrefab) || stageZeroPrefab == null)
            return spawn.SourceKind == MonsterSpawnSourceKind.StageMonsterSet ? "공통 스폰?" : "스폰?";

        return spawn.SourceKind == MonsterSpawnSourceKind.StageMonsterSet && spawn.StageMonsterSet != null
            ? $"{spawn.StageMonsterSet.name} -> {stageZeroPrefab.name}"
            : stageZeroPrefab.name;
    }

    private GameObject GetSelectedMonsterSpawnPreviewPrefab()
    {
        if (selectedMonsterSpawnSource == MonsterSpawnPlacementSource.StageMonsterSet &&
            selectedStageMonsterSet != null &&
            selectedStageMonsterSet.TryResolveMonsterPrefab(0, out GameObject resolvedPrefab))
        {
            return resolvedPrefab;
        }

        return selectedMonsterPrefab;
    }

    private bool TryUseSpawnSourceForPlacement(MonsterSpawnContainer spawn)
    {
        return spawn != null &&
               ((spawn.SourceKind == MonsterSpawnSourceKind.StageMonsterSet && spawn.StageMonsterSet != null) ||
                (spawn.SourceKind == MonsterSpawnSourceKind.FixedPrefab && spawn.MonsterPrefab != null));
    }

    private void UseSpawnSourceForPlacement(MonsterSpawnContainer spawn, bool switchToPlace = true)
    {
        if (!TryUseSpawnSourceForPlacement(spawn))
            return;

        if (spawn.SourceKind == MonsterSpawnSourceKind.StageMonsterSet)
        {
            selectedStageMonsterSet = spawn.StageMonsterSet;
            selectedMonsterSpawnSource = MonsterSpawnPlacementSource.StageMonsterSet;
        }
        else
        {
            selectedMonsterPrefab = spawn.MonsterPrefab;
            selectedMonsterSpawnSource = MonsterSpawnPlacementSource.FixedPrefab;
        }

        if (switchToPlace)
        {
            placementKind = PlacementKind.MonsterSpawn;
            mode = ToolMode.Place;
        }
    }

    private string BuildMarkerLabel(Component component, string label)
    {
        if (component == null)
            return label;

        if (component == linkingShortcut)
            return $"{label} -> 문";

        if (component == linkingChestLock)
            return $"{label} -> 스폰";

        if (component == linkingSpawn)
            return $"{label} -> 상자락";

        if (component == linkingRoomGroup)
            return $"{label} -> 문";

        if (component == linkingDoor)
            return $"{label} -> 방";

        if (linkingShortcut != null && component is DoorObject)
            return $"{label} [대상]";

        if (linkingChestLock != null && component is MonsterSpawnContainer)
            return $"{label} [대상]";

        if (linkingSpawn != null && component is ChestMonsterKillLock)
            return $"{label} [대상]";

        if (linkingRoomGroup != null && component is DoorObject)
            return $"{label} [대상]";

        if (linkingDoor != null && component is MonsterSpawnRoomGroup)
            return $"{label} [대상]";

        return IsSelectedContext(component) ? $"* {label}" : label;
    }

    private bool IsActiveLinkSource(Component component)
    {
        return component != null &&
               (component == linkingShortcut ||
                component == linkingChestLock ||
                component == linkingSpawn ||
                component == linkingRoomGroup ||
                component == linkingDoor);
    }

    private static string GetModeLabel(ToolMode toolMode)
    {
        return toolMode switch
        {
            ToolMode.Review => "검사",
            ToolMode.Link => "연결",
            ToolMode.BattleRoom => "방",
            ToolMode.Place => "배치",
            ToolMode.Options => "속성",
            _ => toolMode.ToString()
        };
    }

    private static string GetPlacementKindLabel(PlacementKind kind)
    {
        return kind switch
        {
            PlacementKind.None => "없음",
            PlacementKind.Door => "문",
            PlacementKind.Lever => "레버",
            PlacementKind.Statue => "석상",
            PlacementKind.Chest => "상자",
            PlacementKind.KillLockChest => "킬락 상자",
            PlacementKind.Portal => "포탈",
            PlacementKind.MonsterSpawn => "몬스터 스폰",
            _ => kind.ToString()
        };
    }

    private static string GetPlacementKindIcon(PlacementKind kind)
    {
        return kind switch
        {
            PlacementKind.Door => "DOOR",
            PlacementKind.Lever => "LEVER",
            PlacementKind.Statue => "STATUE",
            PlacementKind.Chest => "CHEST",
            PlacementKind.KillLockChest => "LOCK",
            PlacementKind.Portal => "PORTAL",
            PlacementKind.MonsterSpawn => "SPAWN",
            _ => "NONE"
        };
    }

    private static string GetMonsterFolderLabel(GameObject prefab)
    {
        if (prefab == null)
            return "기타";

        string path = AssetDatabase.GetAssetPath(prefab).Replace('\\', '/');
        int slashIndex = path.LastIndexOf('/');
        if (slashIndex < 0)
            return "기타";

        string folder = path.Substring(0, slashIndex);
        if (string.Equals(folder, MonsterPrefabRoot, StringComparison.OrdinalIgnoreCase))
            return "기본";

        string rootPrefix = MonsterPrefabRoot + "/";
        if (folder.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            return folder.Substring(rootPrefix.Length);

        return folder;
    }

    private static string GetStageMonsterSetFolderLabel(StageMonsterSetSO monsterSet)
    {
        if (monsterSet == null)
            return "기타";

        string path = AssetDatabase.GetAssetPath(monsterSet).Replace('\\', '/');
        int slashIndex = path.LastIndexOf('/');
        if (slashIndex < 0)
            return "기타";

        string folder = path.Substring(0, slashIndex);
        int commonIndex = folder.IndexOf("/Common", StringComparison.OrdinalIgnoreCase);
        if (commonIndex >= 0)
            return folder.Substring(commonIndex + 1);

        return folder;
    }

    private static bool IsSelectedContext(Component component)
    {
        if (component == null)
            return false;

        if (Selection.activeObject == component)
            return true;

        GameObject selected = Selection.activeGameObject;
        if (selected == null)
            return false;

        return selected == component.gameObject ||
               selected.transform.IsChildOf(component.transform) ||
               component.transform.IsChildOf(selected.transform);
    }

    private static string BuildDoorKey(DoorObject door)
    {
        if (door == null || string.IsNullOrWhiteSpace(door.doorID))
            return string.Empty;

        string mapId = string.IsNullOrWhiteSpace(door.mapID)
            ? door.gameObject.scene.name
            : door.mapID;
        return $"{mapId}:{door.doorID}";
    }

    private MonsterSpawnRoomGroup FindRoomGroupAt(Vector3 position)
    {
        MonsterRoomArea2D area = FindRoomAreaAt(position);
        return area != null ? area.GetComponentInParent<MonsterSpawnRoomGroup>() : null;
    }

    private MonsterRoomArea2D FindRoomAreaAt(Vector3 position)
    {
        foreach (MonsterRoomArea2D area in roomAreas)
        {
            if (area != null && Contains(area, position))
                return area;
        }

        return null;
    }

    private MonsterRoomArea2D ResolveRoomArea(MonsterSpawnRoomGroup group)
    {
        return group != null ? group.GetComponentInChildren<MonsterRoomArea2D>(true) : null;
    }

    private Collider2D ResolveSelectedRoomAreaCollider()
    {
        MonsterRoomArea2D area = ResolveRoomArea(selectedRoomGroup);
        return ResolveAreaCollider(area);
    }

    private MonsterSpawnRoomGroup ResolveRoomGroup(MonsterSpawnContainer spawn)
    {
        return spawn != null ? spawn.RoomGroup : null;
    }

    private bool Contains(MonsterRoomArea2D area, Vector3 worldPosition)
    {
        Collider2D collider = ResolveAreaCollider(area);
        return collider != null && collider.OverlapPoint(worldPosition);
    }

    private static Collider2D ResolveAreaCollider(MonsterRoomArea2D area)
    {
        if (area == null)
            return null;

        return area.AreaCollider != null ? area.AreaCollider : area.GetComponent<Collider2D>();
    }

    private ChestMonsterKillLock FindSingleChestLockInRoom(MonsterSpawnRoomGroup roomGroup)
    {
        MonsterRoomArea2D area = ResolveRoomArea(roomGroup);
        if (area == null)
            return null;

        List<ChestMonsterKillLock> matches = chestLocks
            .Where(lockTarget => lockTarget != null && Contains(area, lockTarget.transform.position))
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    private Transform FindOrCreateLevelRootChild(string childName)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject root = GameObject.Find(LevelDesignRootName);
        if (root == null || root.scene.handle != scene.handle)
        {
            root = new GameObject(LevelDesignRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create LevelDesignRoot");
        }

        Transform child = root.transform.Find(childName);
        if (child != null)
            return child;

        GameObject childObject = new(childName);
        Undo.RegisterCreatedObjectUndo(childObject, $"Create {childName}");
        childObject.transform.SetParent(root.transform, false);
        return childObject.transform;
    }

    private static void SetParent(Transform child, Transform parent)
    {
        if (child == null || parent == null)
            return;

        Undo.SetTransformParent(child, parent, "Parent Level Design Object");
    }

    private static string BuildUniqueName(string baseName)
    {
        string name = baseName;
        int index = 1;
        while (GameObject.Find(name) != null)
            name = $"{baseName}_{index++}";

        return name;
    }

    private T ResolveSelection<T>() where T : Component
    {
        GameObject selected = Selection.activeGameObject;
        return selected != null ? selected.GetComponentInParent<T>(true) : null;
    }

    private static T ReadReference<T>(Object target, string propertyName) where T : Object
    {
        if (target == null)
            return null;

        SerializedObject serializedObject = new(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as T : null;
    }

    private static void AssignReference(Object target, string propertyName, Object value)
    {
        if (target == null)
            return;

        Undo.RecordObject(target, $"Assign {propertyName}");
        SerializedObject serializedObject = new(target);
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedProperties();
    }

    private static void AssignEnum(Object target, string propertyName, int enumValueIndex)
    {
        if (target == null)
            return;

        Undo.RecordObject(target, $"Assign {propertyName}");
        SerializedObject serializedObject = new(target);
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.enumValueIndex = enumValueIndex;
        serializedObject.ApplyModifiedProperties();
    }

    private static string GetObjectPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        List<string> parts = new();
        Transform current = transform;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private string GetScopeLabel()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        return searchScope == SearchScope.ActiveScene ? activeScene.name : "Loaded Scenes";
    }

    private void HandleSceneOpened(Scene scene, OpenSceneMode mode)
    {
        RefreshAll();
    }

    private void HandleSceneClosed(Scene scene)
    {
        RefreshAll();
    }

    private void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        RefreshAll();
    }
}
