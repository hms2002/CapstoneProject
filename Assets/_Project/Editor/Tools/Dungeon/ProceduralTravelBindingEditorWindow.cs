using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임:
/// - 재사용 방 템플릿의 이동 Slot과 현재 씬 DungeonRoomBuilder의 SceneConnection binding을 기획자가 한 화면에서 연결하게 한다.
/// - SceneConnectionSO의 양방향 정책과 선택 방향의 출발·도착 연출 프로필을 방 데이터에 하드코딩하지 않고 편집하게 한다.
/// - 방·슬롯·씬·연결 방향의 불일치를 저장 전에 검증해 런타임 비활성 endpoint나 잘못된 역방향 이동을 예방한다.
/// </summary>
public sealed class ProceduralTravelBindingEditorWindow : EditorWindow
{
    [SerializeField] private RoomTemplateSO roomTemplate;
    [SerializeField] private int selectedSlotIndex;
    [SerializeField] private DungeonRoomBuilder targetBuilder;
    [SerializeField] private SceneConnectionSO connection;
    [SerializeField] private SceneConnectionEndpointSide connectionSide;
    [SerializeField] private bool showConnectionData = true;
    [SerializeField] private bool showPresentationData = true;

    [MenuItem("Tools/Dungeon/Procedural Travel Binding Editor")]
    public static void Open()
    {
        GetWindow<ProceduralTravelBindingEditorWindow>("Travel Binding");
    }

    public static void Open(RoomTemplateSO initialRoomTemplate)
    {
        ProceduralTravelBindingEditorWindow window =
            GetWindow<ProceduralTravelBindingEditorWindow>("Travel Binding");
        window.roomTemplate = initialRoomTemplate;
        window.selectedSlotIndex = 0;
        window.Show();
        window.Focus();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("절차 방 이동 연결", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "방에는 재사용 가능한 Slot만 저장하고, 현재 씬 Builder에서 SceneConnectionSO와 A/B를 결합합니다. " +
            "씬 오브젝트 변경은 Undo와 Scene Dirty로 남으며 자동 저장하지 않습니다.",
            MessageType.Info);

        DrawRoomSlotSection();
        EditorGUILayout.Space(6f);
        DrawSceneBindingSection();
        EditorGUILayout.Space(6f);
        DrawConnectionSection();
        EditorGUILayout.Space(8f);
        DrawValidationAndApplySection();
    }

    private void DrawRoomSlotSection()
    {
        EditorGUILayout.LabelField("1. 방 이동 슬롯", EditorStyles.miniBoldLabel);
        RoomTemplateSO requestedRoom = EditorGUILayout.ObjectField(
            "방 템플릿",
            roomTemplate,
            typeof(RoomTemplateSO),
            false) as RoomTemplateSO;
        if (requestedRoom != roomTemplate)
        {
            roomTemplate = requestedRoom;
            selectedSlotIndex = 0;
        }

        IReadOnlyList<RoomTravelEndpointPlacementData> endpoints = GetTravelEndpoints();
        if (endpoints.Count == 0)
        {
            EditorGUILayout.HelpBox(
                roomTemplate == null
                    ? "이동 슬롯이 있는 RoomTemplateSO를 선택하세요."
                    : "선택한 방에 이동 Endpoint 슬롯이 없습니다. Room Piece Editor에서 먼저 추가하세요.",
                MessageType.Warning);
            return;
        }

        selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, endpoints.Count - 1);
        string[] labels = new string[endpoints.Count];
        for (int index = 0; index < endpoints.Count; index++)
        {
            RoomTravelEndpointPlacementData endpoint = endpoints[index];
            labels[index] = $"{endpoint.slotId} ({endpoint.kind})";
        }

        selectedSlotIndex = EditorGUILayout.Popup("이동 Slot", selectedSlotIndex, labels);
        RoomTravelEndpointPlacementData selectedEndpoint = endpoints[selectedSlotIndex];
        EditorGUILayout.LabelField("Room Id", roomTemplate.LayoutData.roomId);
        EditorGUILayout.LabelField("매개체 방식", selectedEndpoint.kind.ToString());
    }

    private void DrawSceneBindingSection()
    {
        EditorGUILayout.LabelField("2. 현재 씬 Builder", EditorStyles.miniBoldLabel);
        targetBuilder = EditorGUILayout.ObjectField(
            "DungeonRoomBuilder",
            targetBuilder,
            typeof(DungeonRoomBuilder),
            true) as DungeonRoomBuilder;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("선택 오브젝트에서 찾기"))
                targetBuilder = ResolveBuilderFromSelection();

            if (GUILayout.Button("활성 씬에서 찾기"))
                targetBuilder = ResolveBuilderInActiveScene();
        }

        if (targetBuilder != null)
            EditorGUILayout.LabelField("대상 씬", targetBuilder.gameObject.scene.name);
    }

    private void DrawConnectionSection()
    {
        EditorGUILayout.LabelField("3. 연결과 연출", EditorStyles.miniBoldLabel);
        connection = EditorGUILayout.ObjectField(
            "SceneConnectionSO",
            connection,
            typeof(SceneConnectionSO),
            false) as SceneConnectionSO;
        connectionSide = (SceneConnectionEndpointSide)EditorGUILayout.EnumPopup(
            "현재 방의 Connection Side",
            connectionSide);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("새 연결 에셋 만들기"))
                CreateConnectionAsset();

            using (new EditorGUI.DisabledScope(connection == null))
            {
                if (GUILayout.Button("Project에서 연결 선택"))
                    Selection.activeObject = connection;
            }
        }

        if (connection == null)
            return;

        showConnectionData = EditorGUILayout.Foldout(
            showConnectionData,
            "연결 Endpoint와 방향 정책",
            true);
        if (showConnectionData)
            DrawConnectionDataEditor();

        showPresentationData = EditorGUILayout.Foldout(
            showPresentationData,
            "실제 이동 방향 연출 프로필",
            true);
        if (showPresentationData)
            DrawPresentationDataEditor();
    }

    private void DrawConnectionDataEditor()
    {
        var serializedConnection = new SerializedObject(connection);
        serializedConnection.Update();
        EditorGUILayout.PropertyField(
            serializedConnection.FindProperty("connectionId"),
            new GUIContent("연결 ID"));
        EditorGUILayout.PropertyField(
            serializedConnection.FindProperty("endpointA"),
            new GUIContent("Endpoint A"),
            includeChildren: true);
        EditorGUILayout.PropertyField(
            serializedConnection.FindProperty("endpointB"),
            new GUIContent("Endpoint B"),
            includeChildren: true);
        EditorGUILayout.PropertyField(
            serializedConnection.FindProperty("aToB"),
            new GUIContent("A → B"),
            includeChildren: true);
        EditorGUILayout.PropertyField(
            serializedConnection.FindProperty("bToA"),
            new GUIContent("B → A"),
            includeChildren: true);
        if (serializedConnection.ApplyModifiedProperties())
            EditorUtility.SetDirty(connection);
    }

    private void DrawPresentationDataEditor()
    {
        bool isArrivalOnly =
            TryGetSelectedEndpoint(out RoomTravelEndpointPlacementData endpoint) &&
            endpoint.kind == RoomTravelEndpointKind.ArrivalOnly;
        EditorGUILayout.LabelField(
            isArrivalOnly ? "현재 Side로 들어오는 도착 방향" : "현재 Side에서 나가는 출발 방향",
            EditorStyles.miniLabel);

        SceneTravelPresentationProfileSO profile = ResolveSelectedPresentationProfile();
        if (profile == null)
        {
            EditorGUILayout.HelpBox(
                "실제 이동 방향에 연출 프로필이 없습니다. 새 프로필을 만들거나 위 방향 정책에서 기존 프로필을 지정하세요.",
                MessageType.Warning);
            if (GUILayout.Button("실제 이동 방향에 새 연출 프로필 만들기"))
                CreateAndAssignPresentationProfile();
            return;
        }

        EditorGUILayout.ObjectField(
            "현재 프로필",
            profile,
            typeof(SceneTravelPresentationProfileSO),
            false);
        var serializedProfile = new SerializedObject(profile);
        serializedProfile.Update();
        EditorGUI.BeginChangeCheck();
        DrawSerializedPropertiesExcludingScript(serializedProfile);
        if (EditorGUI.EndChangeCheck() && serializedProfile.ApplyModifiedProperties())
            EditorUtility.SetDirty(profile);

        if (GUILayout.Button("Project에서 연출 프로필 선택"))
            Selection.activeObject = profile;
    }

    private void DrawValidationAndApplySection()
    {
        List<string> errors = CollectValidationMessages(out List<string> warnings);
        for (int index = 0; index < warnings.Count; index++)
            EditorGUILayout.HelpBox(warnings[index], MessageType.Warning);
        for (int index = 0; index < errors.Count; index++)
            EditorGUILayout.HelpBox(errors[index], MessageType.Error);

        using (new EditorGUI.DisabledScope(errors.Count > 0))
        {
            if (GUILayout.Button("현재 씬 Builder에 바인딩 추가/갱신", GUILayout.Height(30f)))
                ApplyBinding();
        }
    }

    private IReadOnlyList<RoomTravelEndpointPlacementData> GetTravelEndpoints()
    {
        return roomTemplate != null && roomTemplate.BuildData.travelEndpointPlacements != null
            ? roomTemplate.BuildData.travelEndpointPlacements
            : Array.Empty<RoomTravelEndpointPlacementData>();
    }

    private bool TryGetSelectedEndpoint(out RoomTravelEndpointPlacementData endpoint)
    {
        IReadOnlyList<RoomTravelEndpointPlacementData> endpoints = GetTravelEndpoints();
        if (selectedSlotIndex < 0 || selectedSlotIndex >= endpoints.Count)
        {
            endpoint = default;
            return false;
        }

        endpoint = endpoints[selectedSlotIndex];
        return true;
    }

    private List<string> CollectValidationMessages(out List<string> warnings)
    {
        var errors = new List<string>();
        warnings = new List<string>();
        if (roomTemplate == null)
            errors.Add("방 템플릿을 선택하세요.");
        if (!TryGetSelectedEndpoint(out RoomTravelEndpointPlacementData endpoint) ||
            string.IsNullOrWhiteSpace(endpoint.slotId))
        {
            errors.Add("유효한 이동 Slot을 선택하세요.");
        }
        if (targetBuilder == null || !targetBuilder.gameObject.scene.IsValid())
            errors.Add("저장된 씬의 DungeonRoomBuilder를 선택하세요.");
        if (connection == null)
            errors.Add("SceneConnectionSO를 선택하거나 만드세요.");

        if (targetBuilder == null || connection == null)
            return errors;

        SceneConnectionEndpointData sourceEndpoint = connectionSide == SceneConnectionEndpointSide.A
            ? connection.EndpointA
            : connection.EndpointB;
        SceneConnectionEndpointData destinationEndpoint = connectionSide == SceneConnectionEndpointSide.A
            ? connection.EndpointB
            : connection.EndpointA;
        SceneTravelDirectionData outboundDirection = connectionSide == SceneConnectionEndpointSide.A
            ? connection.AToB
            : connection.BToA;
        SceneTravelDirectionData inboundDirection = connectionSide == SceneConnectionEndpointSide.A
            ? connection.BToA
            : connection.AToB;

        string builderSceneName = targetBuilder.gameObject.scene.name;
        if (!sourceEndpoint.IsValid ||
            !string.Equals(sourceEndpoint.SceneName, builderSceneName, StringComparison.Ordinal))
        {
            errors.Add(
                $"선택한 {connectionSide} Endpoint의 Scene Name이 Builder 씬 '{builderSceneName}'과 일치해야 합니다.");
        }
        if (!destinationEndpoint.IsValid)
            errors.Add("반대편 목적 Endpoint의 Scene Name과 Endpoint Id를 입력하세요.");

        if (TryGetSelectedEndpoint(out endpoint))
        {
            if (endpoint.kind != RoomTravelEndpointKind.ArrivalOnly && !outboundDirection.Enabled)
                errors.Add("Interaction/Trigger 출발 슬롯의 선택 방향은 Enabled여야 합니다.");
            if (endpoint.kind == RoomTravelEndpointKind.ArrivalOnly && outboundDirection.Enabled)
            {
                errors.Add(
                    "ArrivalOnly 슬롯의 현재 Side 출발 방향은 Disabled여야 합니다. 매개체와 연결 데이터 양쪽에서 역이동을 막으세요.");
            }
            if (endpoint.kind == RoomTravelEndpointKind.ArrivalOnly && !inboundDirection.Enabled)
                errors.Add("ArrivalOnly 슬롯으로 들어오는 반대 Side 방향은 Enabled여야 합니다.");
        }

        SceneTravelDirectionData presentationDirection =
            endpoint.kind == RoomTravelEndpointKind.ArrivalOnly
                ? inboundDirection
                : outboundDirection;
        if (presentationDirection.Enabled && presentationDirection.PresentationProfile == null)
            warnings.Add("실제 이동 방향에 연출 프로필이 없어 공통 기본 전환으로 보일 수 있습니다.");

        DungeonGenerator generator = ResolveGenerator(targetBuilder);
        if (generator != null && roomTemplate != null &&
            (generator.RoomLibrary == null || !generator.RoomLibrary.ContainsRoom(roomTemplate)))
        {
            warnings.Add("선택한 방이 이 Builder의 테마 룸 라이브러리에 등록되어 있지 않습니다.");
        }

        return errors;
    }

    private void ApplyBinding()
    {
        if (!TryGetSelectedEndpoint(out RoomTravelEndpointPlacementData endpoint) ||
            targetBuilder == null ||
            connection == null ||
            roomTemplate == null)
        {
            return;
        }

        Undo.RecordObject(targetBuilder, "Configure Procedural Travel Binding");
        var serializedBuilder = new SerializedObject(targetBuilder);
        serializedBuilder.Update();
        SerializedProperty bindings = serializedBuilder.FindProperty("travelEndpointBindings");
        if (bindings == null)
            throw new InvalidOperationException("DungeonRoomBuilder travel binding serialization contract changed.");

        string roomId = roomTemplate.LayoutData.roomId;
        int targetIndex = -1;
        for (int index = 0; index < bindings.arraySize; index++)
        {
            SerializedProperty candidate = bindings.GetArrayElementAtIndex(index);
            if (candidate.FindPropertyRelative("roomId").stringValue == roomId &&
                candidate.FindPropertyRelative("slotId").stringValue == endpoint.slotId)
            {
                targetIndex = index;
                break;
            }
        }

        if (targetIndex < 0)
        {
            targetIndex = bindings.arraySize;
            bindings.InsertArrayElementAtIndex(targetIndex);
        }

        SerializedProperty binding = bindings.GetArrayElementAtIndex(targetIndex);
        binding.FindPropertyRelative("roomId").stringValue = roomId;
        binding.FindPropertyRelative("slotId").stringValue = endpoint.slotId;
        binding.FindPropertyRelative("connection").objectReferenceValue = connection;
        binding.FindPropertyRelative("connectionSide").enumValueIndex = (int)connectionSide;

        for (int index = bindings.arraySize - 1; index >= 0; index--)
        {
            if (index == targetIndex)
                continue;

            SerializedProperty candidate = bindings.GetArrayElementAtIndex(index);
            if (candidate.FindPropertyRelative("roomId").stringValue == roomId &&
                candidate.FindPropertyRelative("slotId").stringValue == endpoint.slotId)
            {
                bindings.DeleteArrayElementAtIndex(index);
            }
        }

        serializedBuilder.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetBuilder);
        EditorSceneManager.MarkSceneDirty(targetBuilder.gameObject.scene);
        Selection.activeObject = targetBuilder;
        Debug.Log(
            $"[ProceduralTravelBindingEditor] Bound {roomId}/{endpoint.slotId} to " +
            $"{connection.name} side {connectionSide} in scene {targetBuilder.gameObject.scene.name}.",
            targetBuilder);
    }

    private void CreateConnectionAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "새 SceneConnectionSO 저장",
            "SceneConnection",
            "asset",
            "연결 에셋을 저장할 위치를 선택하세요.");
        if (string.IsNullOrWhiteSpace(path))
            return;

        var asset = CreateInstance<SceneConnectionSO>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        connection = asset;
        Selection.activeObject = asset;
    }

    private void CreateAndAssignPresentationProfile()
    {
        if (connection == null)
            return;

        string path = EditorUtility.SaveFilePanelInProject(
            "새 이동 연출 프로필 저장",
            "SceneTravelPresentationProfile",
            "asset",
            "출발·전환·도착 연출 프로필을 저장할 위치를 선택하세요.");
        if (string.IsNullOrWhiteSpace(path))
            return;

        var profile = CreateInstance<SceneTravelPresentationProfileSO>();
        AssetDatabase.CreateAsset(profile, path);
        var serializedConnection = new SerializedObject(connection);
        serializedConnection.Update();
        string directionPropertyName = ResolvePresentationDirectionPropertyName();
        SerializedProperty direction = serializedConnection.FindProperty(directionPropertyName);
        direction.FindPropertyRelative("presentationProfile").objectReferenceValue = profile;
        serializedConnection.ApplyModifiedProperties();
        EditorUtility.SetDirty(connection);
        AssetDatabase.SaveAssets();
        Selection.activeObject = profile;
    }

    private SceneTravelPresentationProfileSO ResolveSelectedPresentationProfile()
    {
        if (connection == null)
            return null;

        bool isArrivalOnly =
            TryGetSelectedEndpoint(out RoomTravelEndpointPlacementData endpoint) &&
            endpoint.kind == RoomTravelEndpointKind.ArrivalOnly;
        bool useAToB = connectionSide == SceneConnectionEndpointSide.A
            ? !isArrivalOnly
            : isArrivalOnly;
        return useAToB
            ? connection.AToB.PresentationProfile
            : connection.BToA.PresentationProfile;
    }

    private string ResolvePresentationDirectionPropertyName()
    {
        bool isArrivalOnly =
            TryGetSelectedEndpoint(out RoomTravelEndpointPlacementData endpoint) &&
            endpoint.kind == RoomTravelEndpointKind.ArrivalOnly;
        bool useAToB = connectionSide == SceneConnectionEndpointSide.A
            ? !isArrivalOnly
            : isArrivalOnly;
        return useAToB ? "aToB" : "bToA";
    }

    private static void DrawSerializedPropertiesExcludingScript(SerializedObject serializedObject)
    {
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyPath == "m_Script")
                continue;

            EditorGUILayout.PropertyField(iterator, includeChildren: true);
        }
    }

    private static DungeonRoomBuilder ResolveBuilderFromSelection()
    {
        GameObject selectedObject = Selection.activeGameObject;
        return selectedObject != null
            ? selectedObject.GetComponentInParent<DungeonRoomBuilder>(includeInactive: true)
            : null;
    }

    private static DungeonRoomBuilder ResolveBuilderInActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        DungeonRoomBuilder[] builders =
            UnityEngine.Object.FindObjectsByType<DungeonRoomBuilder>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        for (int index = 0; index < builders.Length; index++)
        {
            if (builders[index] != null && builders[index].gameObject.scene == activeScene)
                return builders[index];
        }

        return null;
    }

    private static DungeonGenerator ResolveGenerator(DungeonRoomBuilder builder)
    {
        if (builder == null)
            return null;

        DungeonGenerator[] generators =
            UnityEngine.Object.FindObjectsByType<DungeonGenerator>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        for (int index = 0; index < generators.Length; index++)
        {
            if (generators[index] != null && generators[index].RoomBuilder == builder)
                return generators[index];
        }

        return null;
    }
}
