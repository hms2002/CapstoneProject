using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 버피 이벤트의 임시 플레이테스트 프리팹, 테마별 방, 이벤트 정의와 생성 풀 연결을 반복 생성한다.
/// </summary>
public static class BuffyHealthTimeEventInstaller
{
    private const string PrefabFolder = "Assets/_Project/Prefabs/Map/Procedural/Events/BuffyHealthTime";
    private const string EventModulePrefabPath = PrefabFolder + "/BuffyHealthTimeEventModule.prefab";
    private const string EventDataFolder = "Assets/_Project/Data/Dungeon/MapEvents/BuffyHealthTime";
    private const string ParcelEventDataFolder = "Assets/_Project/Data/Dungeon/MapEvents/ParcelDelivery";
    private const string SharedEventId = "buffy_health_time";
    private const string EventSortingLayerName = "Entity";
    private const string AttackAttributePath = "Assets/_Project/Data/Attributes/Definitions/DamageCalcAttribute/AttackBaseAttribute.asset";
    private const string MoveSpeedAttributePath = "Assets/_Project/Data/Attributes/Definitions/MoveSpeedMulAttribute.asset";
    private const string LevelProgressionConfigPath = "Assets/_Project/Data/Progression/Leveling/LevelProgressionConfig.asset";

    private static readonly ThemeInstallData[] Themes =
    {
        new(
            "Shadow",
            "Assets/_Project/Data/Dungeon/Rooms/BossThemes/Shadow/Shadow_Treasure_Sacrifice.asset",
            "Assets/_Project/Data/Dungeon/Rooms/BossThemes/Shadow/Shadow_Event_BuffyHealthTime.asset",
            "Assets/_Project/Data/Dungeon/GenerationProfiles/ProceduralShadowGenerationProfile.asset"),
        new(
            "Dragon",
            "Assets/_Project/Data/Dungeon/Rooms/BossThemes/Dragon/Dragon_Treasure_Sacrifice.asset",
            "Assets/_Project/Data/Dungeon/Rooms/BossThemes/Dragon/Dragon_Event_BuffyHealthTime.asset",
            "Assets/_Project/Data/Dungeon/GenerationProfiles/ProceduralDragonGenerationProfile.asset"),
        new(
            "Slime",
            "Assets/_Project/Data/Dungeon/Rooms/BossThemes/Slime/Slime_Treasure_Sacrifice.asset",
            "Assets/_Project/Data/Dungeon/Rooms/BossThemes/Slime/Slime_Event_BuffyHealthTime.asset",
            "Assets/_Project/Data/Dungeon/GenerationProfiles/ProceduralSlimeGenerationProfile.asset")
    };

    [MenuItem("Tools/Dungeon/Run Events/Install Buffy Health Time Test Content")]
    public static void Install()
    {
        ParcelDeliveryEventInstaller.Install();
        EnsureFolder(PrefabFolder);
        EnsureFolder(EventDataFolder);

        AttributeDefinition attackAttribute = LoadRequiredAsset<AttributeDefinition>(AttackAttributePath);
        AttributeDefinition moveSpeedAttribute = LoadRequiredAsset<AttributeDefinition>(MoveSpeedAttributePath);
        LevelProgressionConfigSO levelConfig = LoadRequiredAsset<LevelProgressionConfigSO>(LevelProgressionConfigPath);
        GameObject eventModulePrefab = CreateOrUpdateEventModulePrefab(
            attackAttribute,
            moveSpeedAttribute,
            levelConfig);

        for (int i = 0; i < Themes.Length; i++)
            InstallTheme(Themes[i], eventModulePrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ValidateInstalledContent();
        Debug.Log("[BuffyHealthTimeEventInstaller] Buffy event test content installed for Shadow, Dragon, and Slime corridors.");
    }

    public static void InstallBatch()
    {
        try
        {
            Install();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static GameObject CreateOrUpdateEventModulePrefab(
        AttributeDefinition attackAttribute,
        AttributeDefinition moveSpeedAttribute,
        LevelProgressionConfigSO levelConfig)
    {
        Sprite temporarySprite = LoadTemporarySprite();
        var root = new GameObject("BuffyHealthTimeEventModule");
        try
        {
            GameObject buffy = new("BuffyGuideNpc");
            buffy.transform.SetParent(root.transform, false);
            buffy.transform.localPosition = new Vector3(-4.5f, 0f, 0f);
            var collider = buffy.AddComponent<CapsuleCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.4f, 2.1f);
            buffy.AddComponent<BuffyGuideNpcInteractable>();
            CreateSpriteChild(buffy.transform, "BuffyBody", temporarySprite, new Color(0.86f, 0.35f, 0.62f), Vector3.zero, new Vector3(1.15f, 1.9f, 1f), 0);

            CreateWorkoutEquipment(
                root.transform,
                "StrengthEquipment",
                new Vector3(-1.5f, 0f, 0f),
                BuffyWorkoutType.Strength,
                "근력 운동하기",
                new Color(0.82f, 0.24f, 0.18f),
                temporarySprite,
                attackAttribute,
                moveSpeedAttribute,
                levelConfig);
            CreateWorkoutEquipment(
                root.transform,
                "WheelEquipment",
                new Vector3(1.5f, 0f, 0f),
                BuffyWorkoutType.Wheel,
                "바퀴 운동하기",
                new Color(0.28f, 0.68f, 0.34f),
                temporarySprite,
                attackAttribute,
                moveSpeedAttribute,
                levelConfig);
            CreateWorkoutEquipment(
                root.transform,
                "LogEquipment",
                new Vector3(4.5f, 0f, 0f),
                BuffyWorkoutType.Log,
                "통나무 운동하기",
                new Color(0.62f, 0.38f, 0.18f),
                temporarySprite,
                attackAttribute,
                moveSpeedAttribute,
                levelConfig);

            RunEventArtInstaller.ConfigureBuffy(root);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, EventModulePrefabPath);
            if (saved == null)
                throw new InvalidOperationException($"Could not save prefab: {EventModulePrefabPath}");
            return saved;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void InstallTheme(ThemeInstallData theme, GameObject eventModulePrefab)
    {
        RoomTemplateSO sourceRoom = LoadRequiredAsset<RoomTemplateSO>(theme.SourceRoomPath);
        RoomTemplateSO eventRoom = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(theme.EventRoomPath);
        if (eventRoom == null)
        {
            eventRoom = ScriptableObject.CreateInstance<RoomTemplateSO>();
            AssetDatabase.CreateAsset(eventRoom, theme.EventRoomPath);
        }

        RoomLayoutData layout = sourceRoom.LayoutData;
        layout.roomId = theme.Name + "_Event_BuffyHealthTime";
        layout.roomType = RoomType.Event;
        layout.difficultyTier = 0;
        layout.selectionWeight = 1f;
        layout.topologyPlacement = new RoomTopologyPlacementData
        {
            mode = RoomTopologyPlacementMode.FarthestFromStart,
            minimumGraphDistanceFromStart = 3,
            requireDeadEnd = true
        };

        RoomBuildData build = sourceRoom.BuildData;
        ParcelDeliveryEventInstaller.ConfigureSimpleRectangularRoom(ref build, layout);
        build.objectPlacements = new List<RoomObjectPlacementData>
        {
            new()
            {
                placementId = theme.Name + "BuffyHealthTimeEventModule",
                kind = RoomObjectKind.Prop,
                prefab = eventModulePrefab,
                localCell = ResolveRoomCenter(layout.localBounds),
                localOffset = new Vector2(0.5f, 0.5f),
                localRotationDegrees = 0f,
                localScale = Vector3.one,
                childPoseOverrides = new List<RoomObjectChildPoseOverrideData>()
            }
        };
        build.travelEndpointPlacements = new List<RoomTravelEndpointPlacementData>();
        eventRoom.EditorSetData(layout, build);
        EditorUtility.SetDirty(eventRoom);

        string definitionPath = GetDefinitionPath(theme.Name);
        RunMapEventDefinitionSO definition = AssetDatabase.LoadAssetAtPath<RunMapEventDefinitionSO>(definitionPath);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<RunMapEventDefinitionSO>();
            AssetDatabase.CreateAsset(definition, definitionPath);
        }
        definition.EditorConfigure(
            SharedEventId,
            "버피의 헬스타임",
            1f,
            configuredAllowRepeatInRun: false,
            configuredRequireBossRouteContext: true,
            configuredMinimumBossRouteVisitOrder: 1,
            configuredMaximumBossRouteVisitOrder: 3,
            eventRoom);
        EditorUtility.SetDirty(definition);

        DungeonGenerationProfileSO generationProfile =
            LoadRequiredAsset<DungeonGenerationProfileSO>(theme.GenerationProfilePath);
        RoomThemeLibrarySO roomLibrary = generationProfile.RoomLibrary;
        if (roomLibrary == null)
            throw new InvalidOperationException($"Generation profile has no room library: {theme.GenerationProfilePath}");
        if (roomLibrary.EditorAddRoom(eventRoom))
            EditorUtility.SetDirty(roomLibrary);

        string profilePath = ParcelEventDataFolder + "/" + theme.Name + "_ParcelEventGenerationProfile.asset";
        RunMapEventGenerationProfileSO eventProfile = LoadRequiredAsset<RunMapEventGenerationProfileSO>(profilePath);
        RunMapEventDefinitionSO parcelDefinition = LoadRequiredAsset<RunMapEventDefinitionSO>(
            ParcelEventDataFolder + "/" + theme.Name + "_ParcelDeliveryEvent.asset");
        eventProfile.EditorConfigure(
            new[] { parcelDefinition, definition },
            configuredMaximumStartEventsPerCorridor: 1,
            configuredPlannedBossRouteVisitCount: 3,
            configuredLogSelection: true);
        EditorUtility.SetDirty(eventProfile);

        generationProfile.EditorSetRunMapEventProfile(eventProfile);
        EditorUtility.SetDirty(generationProfile);
    }

    private static void ValidateInstalledContent()
    {
        GameObject module = LoadRequiredAsset<GameObject>(EventModulePrefabPath);
        BuffyHealthTimeInteractable[] rewardInteractions =
            module.GetComponentsInChildren<BuffyHealthTimeInteractable>(true);
        DialogueTrigger guideInteraction =
            module.GetComponentInChildren<DialogueTrigger>(true);
        if (rewardInteractions.Length != 3 || guideInteraction == null)
            throw new InvalidOperationException("Buffy event module needs one guide and three workout interactions.");

        var workoutTypes = new HashSet<BuffyWorkoutType>();
        for (int i = 0; i < rewardInteractions.Length; i++)
        {
            if (rewardInteractions[i].transform == guideInteraction.transform)
                throw new InvalidOperationException("Buffy guide NPC must not own a workout interaction.");
            workoutTypes.Add(rewardInteractions[i].WorkoutType);
        }
        if (workoutTypes.Count != 3)
            throw new InvalidOperationException("Buffy workout objects must configure three distinct rewards.");
        ValidateEventRendererSorting(module);

        for (int i = 0; i < Themes.Length; i++)
        {
            ThemeInstallData theme = Themes[i];
            RoomTemplateSO room = LoadRequiredAsset<RoomTemplateSO>(theme.EventRoomPath);
            if (room.LayoutData.roomType != RoomType.Event || room.BuildData.objectPlacements.Count != 1)
                throw new InvalidOperationException($"Invalid Buffy event room: {theme.EventRoomPath}");
            ValidateEmptyEventRoom(room);

            DungeonGenerationProfileSO generationProfile =
                LoadRequiredAsset<DungeonGenerationProfileSO>(theme.GenerationProfilePath);
            RunMapEventGenerationProfileSO profile = generationProfile.RunMapEventProfile;
            RoomThemeLibrarySO roomLibrary = generationProfile.RoomLibrary;
            if (profile == null ||
                roomLibrary == null ||
                !profile.TryGetDefinition(SharedEventId, out RunMapEventDefinitionSO installedDefinition) ||
                installedDefinition == null)
            {
                throw new InvalidOperationException($"Buffy event was not connected: {theme.GenerationProfilePath}");
            }

            IReadOnlyList<RunMapEventDefinitionSO> definitions = profile.EventDefinitions;
            for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                RoomTemplateSO eventRoom = definitions[definitionIndex]?.EventRoomTemplate;
                if (eventRoom == null || !roomLibrary.ContainsRoom(eventRoom))
                {
                    throw new InvalidOperationException(
                        $"Event room does not belong to {roomLibrary.name}: " +
                        $"{(eventRoom != null ? eventRoom.name : "<missing>")}");
                }

                DungeonLayoutResult layout = new DungeonGraphLayoutAssembler().Assemble(
                    roomLibrary,
                    generationProfile.LayoutPolicy,
                    generationProfile.Seed,
                    generationProfile.RoomCount,
                    generationProfile.MaxPlacementAttemptsPerRoom,
                    generationProfile.MinimumCorridorLength,
                    generationProfile.CorridorLengthPerRoomCell,
                    generationProfile.CorridorLengthVariation,
                    new[] { eventRoom });
                if (!layout.IsComplete || layout.Rooms.Count != generationProfile.RoomCount)
                {
                    throw new InvalidOperationException(
                        $"Event layout validation failed for {eventRoom.name}: {layout.FailureReason}");
                }
            }
        }
    }

    private static string GetDefinitionPath(string themeName) =>
        EventDataFolder + "/" + themeName + "_BuffyHealthTimeEvent.asset";

    private static SpriteRenderer CreateSpriteChild(
        Transform parent,
        string name,
        Sprite sprite,
        Color color,
        Vector3 localPosition,
        Vector3 localScale,
        int sortingOrder)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localScale = localScale;
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerName = EventSortingLayerName;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private static void CreateWorkoutEquipment(
        Transform parent,
        string name,
        Vector3 localPosition,
        BuffyWorkoutType workoutType,
        string promptText,
        Color color,
        Sprite sprite,
        AttributeDefinition attackAttribute,
        AttributeDefinition moveSpeedAttribute,
        LevelProgressionConfigSO levelConfig)
    {
        GameObject equipment = new(name);
        equipment.transform.SetParent(parent, false);
        equipment.transform.localPosition = localPosition;

        var equipmentCollider = equipment.AddComponent<BoxCollider2D>();
        equipmentCollider.isTrigger = true;
        equipmentCollider.size = new Vector2(2.2f, 1.8f);

        BuffyHealthTimeInteractable interactable = equipment.AddComponent<BuffyHealthTimeInteractable>();
        SetEnum(interactable, "workoutType", (int)workoutType);
        SetObjectReference(interactable, "attackBaseAttribute", attackAttribute);
        SetObjectReference(interactable, "moveSpeedMultiplierAttribute", moveSpeedAttribute);
        SetObjectReference(interactable, "levelProgressionConfig", levelConfig);
        SetString(interactable, "interactPromptText", promptText);
        CreateSpriteChild(
            equipment.transform,
            name + "Body",
            sprite,
            color,
            Vector3.zero,
            new Vector3(2.2f, 1.5f, 1f),
            0);
    }

    private static void ValidateEventRendererSorting(GameObject module)
    {
        SpriteRenderer[] renderers = module.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException("Buffy event module has no visible renderer.");

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].sortingLayerName != EventSortingLayerName)
                throw new InvalidOperationException($"Invalid event sorting layer: {renderers[i].name}");
        }
    }

    private static void ValidateEmptyEventRoom(RoomTemplateSO room)
    {
        RoomBuildData build = room.BuildData;
        if (HasTiles(build.floorDetailTiles) ||
            HasTiles(build.groundDecorationTiles) ||
            HasTiles(build.wallDetailTiles) ||
            HasTiles(build.foregroundTiles) ||
            HasTiles(build.overlayFxTiles))
        {
            throw new InvalidOperationException($"Event room contains decorative tiles: {room.name}");
        }
    }

    private static bool HasTiles(List<RoomTileData> tiles) => tiles != null && tiles.Count > 0;

    private static Vector2Int ResolveRoomCenter(RectInt bounds) =>
        new(bounds.xMin + bounds.width / 2, bounds.yMin + bounds.height / 2);

    private static Sprite LoadTemporarySprite()
    {
        Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (sprite == null)
            throw new InvalidOperationException("Could not load Unity built-in temporary sprite.");
        return sprite;
    }

    private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        var serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            throw new InvalidOperationException($"Missing object reference '{propertyName}' on {target.name}.");

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(UnityEngine.Object target, string propertyName, string value)
    {
        var serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.String)
            throw new InvalidOperationException($"Missing string '{propertyName}' on {target.name}.");

        property.stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEnum(UnityEngine.Object target, string propertyName, int value)
    {
        var serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.Enum)
            throw new InvalidOperationException($"Missing enum '{propertyName}' on {target.name}.");

        property.enumValueIndex = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T LoadRequiredAsset<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new InvalidOperationException($"Missing required asset: {path}");
        return asset;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        int separatorIndex = folderPath.LastIndexOf('/');
        if (separatorIndex <= 0)
            throw new InvalidOperationException($"Invalid folder path: {folderPath}");

        string parent = folderPath.Substring(0, separatorIndex);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderPath.Substring(separatorIndex + 1));
    }

    private readonly struct ThemeInstallData
    {
        public string Name { get; }
        public string SourceRoomPath { get; }
        public string EventRoomPath { get; }
        public string GenerationProfilePath { get; }

        public ThemeInstallData(
            string name,
            string sourceRoomPath,
            string eventRoomPath,
            string generationProfilePath)
        {
            Name = name;
            SourceRoomPath = sourceRoomPath;
            EventRoomPath = eventRoomPath;
            GenerationProfilePath = generationProfilePath;
        }
    }
}
