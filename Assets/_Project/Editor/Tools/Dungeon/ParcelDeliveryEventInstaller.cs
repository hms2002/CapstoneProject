using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임 : 소포 이벤트의 임시 플레이테스트용 유물, 상호작용 프리팹, 테마별 방과 생성 프로필 연결을 반복 생성한다.
/// </summary>
public static class ParcelDeliveryEventInstaller
{
    private const string ItemDatabasePath = "Assets/_Project/Data/Items/ItemDatabase.asset";
    private const string ParcelDefinitionPath = "Assets/_Project/Data/Items/Relics/RD_EventParcel.asset";
    private const string PrefabFolder = "Assets/_Project/Prefabs/Map/Procedural/Events/ParcelDelivery";
    private const string EventModulePrefabPath = PrefabFolder + "/ParcelPickupEventModule.prefab";
    private const string DeliveryModulePrefabPath = PrefabFolder + "/ParcelDeliveryPointModule.prefab";
    private const string EventDataFolder = "Assets/_Project/Data/Dungeon/MapEvents/ParcelDelivery";
    private const string SharedEventId = "parcel_delivery";
    private const string DeliveryFollowUpId = "parcel_delivery_destination";
    private const string EventSortingLayerName = "Entity";

    private static readonly ThemeInstallData[] Themes =
    {
        new(
            "Shadow",
            "Assets/_Project/Data/Dungeon/Rooms/BossThemes/Shadow/Shadow_Treasure_Sacrifice.asset",
            "Assets/_Project/Data/Dungeon/Rooms/BossThemes/Shadow/Shadow_Event_ParcelPickup.asset",
            "Assets/_Project/Data/Dungeon/Rooms/BossThemes/Shadow/Shadow_Event_ParcelDeliveryPoint.asset",
            "Assets/_Project/Data/Dungeon/GenerationProfiles/ProceduralShadowGenerationProfile.asset"),
        new(
            "Dragon",
            "Assets/_Project/Data/Dungeon/Rooms/BossThemes/Dragon/Dragon_Treasure_Sacrifice.asset",
            "Assets/_Project/Data/Dungeon/Rooms/BossThemes/Dragon/Dragon_Event_ParcelPickup.asset",
            "Assets/_Project/Data/Dungeon/Rooms/BossThemes/Dragon/Dragon_Event_ParcelDeliveryPoint.asset",
            "Assets/_Project/Data/Dungeon/GenerationProfiles/ProceduralDragonGenerationProfile.asset"),
        new(
            "Slime",
            "Assets/_Project/Data/Dungeon/Rooms/BossThemes/Slime/Slime_Treasure_Sacrifice.asset",
            "Assets/_Project/Data/Dungeon/Rooms/BossThemes/Slime/Slime_Event_ParcelPickup.asset",
            "Assets/_Project/Data/Dungeon/Rooms/BossThemes/Slime/Slime_Event_ParcelDeliveryPoint.asset",
            "Assets/_Project/Data/Dungeon/GenerationProfiles/ProceduralSlimeGenerationProfile.asset")
    };

    [MenuItem("Tools/Dungeon/Run Events/Install Parcel Pickup Test Content")]
    public static void Install()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(EventDataFolder);

        ParcelRelicDefinition parcelDefinition = CreateOrUpdateParcelDefinition();
        GameObject eventModulePrefab = CreateOrUpdateEventModulePrefab(parcelDefinition);
        GameObject deliveryModulePrefab = CreateOrUpdateDeliveryModulePrefab(parcelDefinition);
        RegisterParcelForRuntimeRestore(parcelDefinition);

        for (int i = 0; i < Themes.Length; i++)
            InstallTheme(Themes[i], eventModulePrefab, deliveryModulePrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ValidateInstalledContent(parcelDefinition);
        Debug.Log("[ParcelDeliveryEventInstaller] Parcel pickup test content installed for Shadow, Dragon, and Slime corridors.");
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

    private static ParcelRelicDefinition CreateOrUpdateParcelDefinition()
    {
        ParcelRelicDefinition definition =
            AssetDatabase.LoadAssetAtPath<ParcelRelicDefinition>(ParcelDefinitionPath);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<ParcelRelicDefinition>();
            AssetDatabase.CreateAsset(definition, ParcelDefinitionPath);
        }

        definition.relicId = "Relic.Event.Parcel";
        definition.displayName = "소포";
        definition.description = "다음 구역의 배송지까지 운반해야 하는 소포입니다. 유물 슬롯을 차지하며 버릴 수 없습니다.";
        definition.maxLevel = 1;
        definition.dropLevel = 1;
        definition.logic = null;
        definition.param = null;
        definition.icon = LoadTemporarySprite();
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static GameObject CreateOrUpdateEventModulePrefab(ParcelRelicDefinition parcelDefinition)
    {
        Sprite temporarySprite = LoadTemporarySprite();
        var root = new GameObject("ParcelPickupEventModule");
        try
        {
            GameObject npc = new("DeliveryGuideNpc");
            npc.transform.SetParent(root.transform, false);
            npc.transform.localPosition = new Vector3(-2.25f, 0f, 0f);
            var npcCollider = npc.AddComponent<CapsuleCollider2D>();
            npcCollider.isTrigger = true;
            npcCollider.size = new Vector2(1.1f, 1.8f);
            npc.AddComponent<ParcelGuideNpcInteractable>();
            CreateSpriteChild(npc.transform, "NpcBody", temporarySprite, new Color(0.35f, 0.75f, 1f), Vector3.zero, new Vector3(0.9f, 1.55f, 1f), 0);

            GameObject pile = new("PermanentParcelPile");
            pile.transform.SetParent(root.transform, false);
            pile.transform.localPosition = new Vector3(2.25f, 0f, 0f);
            var pileCollider = pile.AddComponent<BoxCollider2D>();
            pileCollider.isTrigger = true;
            pileCollider.size = new Vector2(2.4f, 1.8f);
            ParcelPickupInteractable pickup = pile.AddComponent<ParcelPickupInteractable>();
            SetObjectReference(pickup, "parcelDefinition", parcelDefinition);

            Color boxColor = new(0.62f, 0.38f, 0.18f);
            CreateSpriteChild(pile.transform, "ParcelBox_Left", temporarySprite, boxColor, new Vector3(-0.6f, -0.25f, 0f), new Vector3(1.15f, 0.85f, 1f), 0);
            CreateSpriteChild(pile.transform, "ParcelBox_Right", temporarySprite, boxColor, new Vector3(0.6f, -0.25f, 0f), new Vector3(1.15f, 0.85f, 1f), 0);
            CreateSpriteChild(pile.transform, "ParcelBox_Top", temporarySprite, new Color(0.72f, 0.48f, 0.25f), new Vector3(0f, 0.55f, 0f), new Vector3(1.15f, 0.85f, 1f), 1);

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

    private static GameObject CreateOrUpdateDeliveryModulePrefab(ParcelRelicDefinition parcelDefinition)
    {
        Sprite temporarySprite = LoadTemporarySprite();
        var root = new GameObject("ParcelDeliveryPointModule");
        try
        {
            var collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(2.6f, 2f);
            ParcelDeliveryPointInteractable delivery = root.AddComponent<ParcelDeliveryPointInteractable>();
            SetObjectReference(delivery, "parcelDefinition", parcelDefinition);
            CreateSpriteChild(
                root.transform,
                "DeliveryPointBody",
                temporarySprite,
                new Color(0.3f, 0.65f, 0.9f),
                Vector3.zero,
                new Vector3(2.4f, 1.8f, 1f),
                0);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, DeliveryModulePrefabPath);
            if (saved == null)
                throw new InvalidOperationException($"Could not save prefab: {DeliveryModulePrefabPath}");
            return saved;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void InstallTheme(
        ThemeInstallData theme,
        GameObject eventModulePrefab,
        GameObject deliveryModulePrefab)
    {
        RoomTemplateSO sourceRoom = LoadRequiredAsset<RoomTemplateSO>(theme.SourceRoomPath);
        RoomTemplateSO eventRoom = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(theme.EventRoomPath);
        if (eventRoom == null)
        {
            eventRoom = ScriptableObject.CreateInstance<RoomTemplateSO>();
            AssetDatabase.CreateAsset(eventRoom, theme.EventRoomPath);
        }

        RoomLayoutData layout = sourceRoom.LayoutData;
        layout.roomId = theme.Name + "_Event_ParcelPickup";
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
        ConfigureSimpleRectangularRoom(ref build, layout);
        build.objectPlacements = new List<RoomObjectPlacementData>
        {
            new()
            {
                placementId = theme.Name + "ParcelPickupEventModule",
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

        RoomTemplateSO deliveryRoom = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(theme.DeliveryRoomPath);
        if (deliveryRoom == null)
        {
            deliveryRoom = ScriptableObject.CreateInstance<RoomTemplateSO>();
            AssetDatabase.CreateAsset(deliveryRoom, theme.DeliveryRoomPath);
        }

        RoomLayoutData deliveryLayout = sourceRoom.LayoutData;
        deliveryLayout.roomId = theme.Name + "_Event_ParcelDeliveryPoint";
        deliveryLayout.roomType = RoomType.Event;
        deliveryLayout.difficultyTier = 0;
        deliveryLayout.selectionWeight = 1f;
        deliveryLayout.topologyPlacement = new RoomTopologyPlacementData
        {
            mode = RoomTopologyPlacementMode.FarthestFromStart,
            minimumGraphDistanceFromStart = 3,
            requireDeadEnd = true
        };

        RoomBuildData deliveryBuild = sourceRoom.BuildData;
        ConfigureSimpleRectangularRoom(ref deliveryBuild, deliveryLayout);
        deliveryBuild.objectPlacements = new List<RoomObjectPlacementData>
        {
            new()
            {
                placementId = theme.Name + "ParcelDeliveryPointModule",
                kind = RoomObjectKind.Prop,
                prefab = deliveryModulePrefab,
                localCell = ResolveRoomCenter(deliveryLayout.localBounds),
                localOffset = new Vector2(0.5f, 0.5f),
                localRotationDegrees = 0f,
                localScale = Vector3.one,
                childPoseOverrides = new List<RoomObjectChildPoseOverrideData>()
            }
        };
        deliveryBuild.travelEndpointPlacements = new List<RoomTravelEndpointPlacementData>();
        deliveryRoom.EditorSetData(deliveryLayout, deliveryBuild);
        EditorUtility.SetDirty(deliveryRoom);

        string definitionPath = EventDataFolder + "/" + theme.Name + "_ParcelDeliveryEvent.asset";
        RunMapEventDefinitionSO definition =
            AssetDatabase.LoadAssetAtPath<RunMapEventDefinitionSO>(definitionPath);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<RunMapEventDefinitionSO>();
            AssetDatabase.CreateAsset(definition, definitionPath);
        }
        definition.EditorConfigure(
            SharedEventId,
            "소포 배달 대행",
            1f,
            configuredAllowRepeatInRun: false,
            configuredRequireBossRouteContext: true,
            configuredMinimumBossRouteVisitOrder: 1,
            configuredMaximumBossRouteVisitOrder: 2,
            eventRoom);
        var deliveryFollowUp = new RunMapEventFollowUpDefinition();
        deliveryFollowUp.EditorConfigure(
            DeliveryFollowUpId,
            "소포 배송 지점",
            deliveryRoom,
            RunMapEventFollowUpPlacementTiming.NextUnvisitedBossRoute);
        definition.EditorSetFollowUps(new[] { deliveryFollowUp });
        EditorUtility.SetDirty(definition);

        IReadOnlyList<RunMapEventDefinitionSO> eventPool = ResolveEventPool(theme.Name, definition);
        DungeonGenerationProfileSO generationProfile =
            LoadRequiredAsset<DungeonGenerationProfileSO>(theme.GenerationProfilePath);
        RoomThemeLibrarySO roomLibrary = generationProfile.RoomLibrary;
        if (roomLibrary == null)
            throw new InvalidOperationException($"Generation profile has no room library: {theme.GenerationProfilePath}");
        if (roomLibrary.EditorAddRoom(deliveryRoom))
            EditorUtility.SetDirty(roomLibrary);
        for (int definitionIndex = 0; definitionIndex < eventPool.Count; definitionIndex++)
        {
            RoomTemplateSO poolRoom = eventPool[definitionIndex]?.EventRoomTemplate;
            if (poolRoom != null && roomLibrary.EditorAddRoom(poolRoom))
                EditorUtility.SetDirty(roomLibrary);
        }

        string profilePath = EventDataFolder + "/" + theme.Name + "_ParcelEventGenerationProfile.asset";
        RunMapEventGenerationProfileSO eventProfile =
            AssetDatabase.LoadAssetAtPath<RunMapEventGenerationProfileSO>(profilePath);
        if (eventProfile == null)
        {
            eventProfile = ScriptableObject.CreateInstance<RunMapEventGenerationProfileSO>();
            AssetDatabase.CreateAsset(eventProfile, profilePath);
        }
        eventProfile.EditorConfigure(
            eventPool,
            configuredMaximumStartEventsPerCorridor: 1,
            configuredPlannedBossRouteVisitCount: 3,
            configuredLogSelection: true);
        EditorUtility.SetDirty(eventProfile);

        generationProfile.EditorSetRunMapEventProfile(eventProfile);
        EditorUtility.SetDirty(generationProfile);
    }

    private static void RegisterParcelForRuntimeRestore(ParcelRelicDefinition parcelDefinition)
    {
        ItemDatabase database = LoadRequiredAsset<ItemDatabase>(ItemDatabasePath);
        database.allRelics ??= new List<RelicDefinition>();
        if (!database.allRelics.Contains(parcelDefinition))
            database.allRelics.Add(parcelDefinition);
        EditorUtility.SetDirty(database);
    }

    private static IReadOnlyList<RunMapEventDefinitionSO> ResolveEventPool(
        string themeName,
        RunMapEventDefinitionSO parcelDefinition)
    {
        var definitions = new List<RunMapEventDefinitionSO> { parcelDefinition };
        string buffyPath =
            "Assets/_Project/Data/Dungeon/MapEvents/BuffyHealthTime/" +
            themeName +
            "_BuffyHealthTimeEvent.asset";
        RunMapEventDefinitionSO buffyDefinition =
            AssetDatabase.LoadAssetAtPath<RunMapEventDefinitionSO>(buffyPath);
        if (buffyDefinition != null)
            definitions.Add(buffyDefinition);
        return definitions;
    }

    private static void ValidateInstalledContent(ParcelRelicDefinition parcelDefinition)
    {
        if (parcelDefinition == null || parcelDefinition.logic != null)
            throw new InvalidOperationException("Parcel definition must exist and have no relic logic.");

        GameObject module = LoadRequiredAsset<GameObject>(EventModulePrefabPath);
        if (module.GetComponentInChildren<ParcelPickupInteractable>(true) == null ||
            module.GetComponentInChildren<ParcelGuideNpcInteractable>(true) == null)
        {
            throw new InvalidOperationException("Parcel event module is missing its pickup or guide interaction.");
        }
        ValidateEventRendererSorting(module);

        GameObject deliveryModule = LoadRequiredAsset<GameObject>(DeliveryModulePrefabPath);
        if (deliveryModule.GetComponentInChildren<ParcelDeliveryPointInteractable>(true) == null)
            throw new InvalidOperationException("Parcel delivery module is missing its interaction.");
        ValidateEventRendererSorting(deliveryModule);

        for (int i = 0; i < Themes.Length; i++)
        {
            ThemeInstallData theme = Themes[i];
            RoomTemplateSO room = LoadRequiredAsset<RoomTemplateSO>(theme.EventRoomPath);
            RoomTemplateSO deliveryRoom = LoadRequiredAsset<RoomTemplateSO>(theme.DeliveryRoomPath);
            if (room.LayoutData.roomType != RoomType.Event || room.BuildData.objectPlacements.Count != 1)
                throw new InvalidOperationException($"Invalid parcel event room: {theme.EventRoomPath}");
            if (deliveryRoom.LayoutData.roomType != RoomType.Event || deliveryRoom.BuildData.objectPlacements.Count != 1)
                throw new InvalidOperationException($"Invalid parcel delivery room: {theme.DeliveryRoomPath}");
            ValidateEmptyEventRoom(room);
            ValidateEmptyEventRoom(deliveryRoom);

            DungeonGenerationProfileSO generationProfile =
                LoadRequiredAsset<DungeonGenerationProfileSO>(theme.GenerationProfilePath);
            RunMapEventGenerationProfileSO eventProfile = generationProfile.RunMapEventProfile;
            RoomThemeLibrarySO roomLibrary = generationProfile.RoomLibrary;
            if (eventProfile == null || roomLibrary == null)
                throw new InvalidOperationException($"Map event profile was not connected: {theme.GenerationProfilePath}");
            if (!roomLibrary.ContainsRoom(deliveryRoom) ||
                !eventProfile.TryGetDefinition(SharedEventId, out RunMapEventDefinitionSO parcelEvent) ||
                !parcelEvent.TryGetFollowUp(DeliveryFollowUpId, out RunMapEventFollowUpDefinition followUp) ||
                followUp.RoomTemplate != deliveryRoom)
            {
                throw new InvalidOperationException($"Parcel delivery follow-up was not connected: {theme.Name}");
            }

            IReadOnlyList<RunMapEventDefinitionSO> definitions = eventProfile.EventDefinitions;
            for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                RoomTemplateSO eventRoom = definitions[definitionIndex]?.EventRoomTemplate;
                if (eventRoom == null || !roomLibrary.ContainsRoom(eventRoom))
                {
                    throw new InvalidOperationException(
                        $"Event room does not belong to {roomLibrary.name}: " +
                        $"{(eventRoom != null ? eventRoom.name : "<missing>")}");
                }
            }

            ValidateGuaranteedLayouts(generationProfile, definitions);
            ValidateGuaranteedLayout(generationProfile, deliveryRoom);
        }
    }

    private static void ValidateGuaranteedLayouts(
        DungeonGenerationProfileSO generationProfile,
        IReadOnlyList<RunMapEventDefinitionSO> definitions)
    {
        if (generationProfile == null || definitions == null)
            return;

        for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
        {
            RoomTemplateSO eventRoom = definitions[definitionIndex]?.EventRoomTemplate;
            if (eventRoom == null)
                continue;

            ValidateGuaranteedLayout(generationProfile, eventRoom);
        }
    }

    private static void ValidateGuaranteedLayout(
        DungeonGenerationProfileSO generationProfile,
        RoomTemplateSO eventRoom)
    {
        DungeonLayoutResult layout = new DungeonGraphLayoutAssembler().Assemble(
            generationProfile.RoomLibrary,
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

    private static Vector2Int ResolveRoomCenter(RectInt bounds)
    {
        return new Vector2Int(bounds.xMin + bounds.width / 2, bounds.yMin + bounds.height / 2);
    }

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

    internal static void ConfigureSimpleRectangularRoom(ref RoomBuildData build, RoomLayoutData layout)
    {
        List<RoomTileData> sourceFloorTiles = build.floorTiles;
        List<RoomTileData> sourceWallTiles = build.wallTiles;
        TileBase floorTile = FindFirstTile(sourceFloorTiles);
        TileBase wallTile = FindFirstTile(sourceWallTiles);
        if (floorTile == null || wallTile == null)
            throw new InvalidOperationException($"Room '{layout.roomId}' needs a floor tile and a wall tile.");

        var floorTiles = new List<RoomTileData>(layout.localBounds.width * layout.localBounds.height);
        var wallTiles = new List<RoomTileData>();
        for (int y = layout.localBounds.yMin; y < layout.localBounds.yMax; y++)
        {
            for (int x = layout.localBounds.xMin; x < layout.localBounds.xMax; x++)
            {
                Vector2Int cell = new(x, y);
                floorTiles.Add(new RoomTileData
                {
                    localCell = cell,
                    tile = FindTileAtCell(sourceFloorTiles, cell) ?? floorTile
                });

                if (IsBoundaryCell(cell, layout.localBounds))
                {
                    wallTiles.Add(new RoomTileData
                    {
                        localCell = cell,
                        tile = FindTileAtCell(sourceWallTiles, cell) ?? wallTile
                    });
                }
            }
        }

        build.underFloorTiles = new List<RoomTileData>();
        build.floorTiles = floorTiles;
        build.floorDetailTiles = new List<RoomTileData>();
        build.groundDecorationTiles = new List<RoomTileData>();
        build.wallTiles = wallTiles;
        build.wallDetailTiles = new List<RoomTileData>();
        build.foregroundTiles = new List<RoomTileData>();
        build.overlayFxTiles = new List<RoomTileData>();
    }

    private static TileBase FindFirstTile(List<RoomTileData> tiles)
    {
        if (tiles == null)
            return null;

        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i].tile != null)
                return tiles[i].tile;
        }

        return null;
    }

    private static TileBase FindTileAtCell(List<RoomTileData> tiles, Vector2Int cell)
    {
        if (tiles == null)
            return null;

        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i].localCell == cell && tiles[i].tile != null)
                return tiles[i].tile;
        }

        return null;
    }

    private static bool IsBoundaryCell(Vector2Int cell, RectInt bounds) =>
        cell.x == bounds.xMin ||
        cell.x == bounds.xMax - 1 ||
        cell.y == bounds.yMin ||
        cell.y == bounds.yMax - 1;

    private static void ValidateEventRendererSorting(GameObject module)
    {
        SpriteRenderer[] renderers = module.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException("Parcel event module has no visible renderer.");

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
        public string DeliveryRoomPath { get; }
        public string GenerationProfilePath { get; }

        public ThemeInstallData(
            string name,
            string sourceRoomPath,
            string eventRoomPath,
            string deliveryRoomPath,
            string generationProfilePath)
        {
            Name = name;
            SourceRoomPath = sourceRoomPath;
            EventRoomPath = eventRoomPath;
            DeliveryRoomPath = deliveryRoomPath;
            GenerationProfilePath = generationProfilePath;
        }
    }
}
