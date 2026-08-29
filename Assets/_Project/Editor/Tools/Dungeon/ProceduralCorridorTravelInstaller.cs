using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임 : 네 보스 테마 절차 복도의 로비·보스 이동 슬롯, 연결 에셋, 씬별 binding과 보스 씬 도착 endpoint를 재현 가능하게 설치하고 검증한다.
/// </summary>
public static class ProceduralCorridorTravelInstaller
{
    private const string LobbySceneName = "ProtoTypeHub";
    private const string RoomRootFolder = "Assets/_Project/Data/Dungeon/Rooms/BossThemes";
    private const string ConnectionFolder = "Assets/_Project/Data/SceneFlow/Connections";
    private const string TravelProfileFolder = "Assets/_Project/Data/SceneFlow/TravelProfiles";
    private const string TravelProfilePath =
        TravelProfileFolder + "/DefaultCorridorPortalTravel.asset";
    private const string SourcePortalPrefabPath =
        "Assets/_Project/Prefabs/Map/Portal/ScenePortal.prefab";
    private const string TravelPortalPrefabPath =
        "Assets/_Project/Prefabs/Map/Procedural/ProceduralSceneTravelPortal.prefab";
    private const string GeneratedTravelRootName = "GeneratedTravelEndpoints";
    private const string BossSceneTravelRootName = "[DataDrivenSceneTravel]";
    private const string LobbySlotId = "LobbyGate";
    private const string BossSlotId = "BossGate";

    /// <summary>
    /// 책임 : 한 테마의 route 에셋 경로와 방 Id를 이동 콘텐츠 설치 단위로 묶는다.
    /// </summary>
    private readonly struct CorridorTravelSpec
    {
        public string ThemeName { get; }
        public string RouteSetPath { get; }
        public string StartRoomId => $"{ThemeName}_Start";
        public string BossRoomId => $"{ThemeName}_Boss";
        public string StartRoomPath => $"{RoomRootFolder}/{ThemeName}/{StartRoomId}.asset";
        public string BossRoomPath => $"{RoomRootFolder}/{ThemeName}/{BossRoomId}.asset";

        public CorridorTravelSpec(string themeName, string routeSetPath)
        {
            ThemeName = themeName;
            RouteSetPath = routeSetPath;
        }
    }

    [MenuItem("Tools/Dungeon/Install Procedural Corridor Travel Configuration")]
    public static void Install()
    {
        EnsureFolder(ConnectionFolder);
        EnsureFolder(TravelProfileFolder);
        EnsureFolder("Assets/_Project/Prefabs/Map/Procedural");

        SceneTravelPresentationProfileSO presentationProfile =
            CreateOrUpdateDefaultPresentationProfile();
        GameObject travelPortalPrefab = CreateOrUpdateTravelPortalPrefab();
        CorridorTravelSpec[] specs = CreateSpecs();

        for (int i = 0; i < specs.Length; i++)
            InstallTheme(specs[i], presentationProfile, travelPortalPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log(
            $"Installed and verified data-driven travel configuration for {specs.Length} procedural Corridors. " +
            "ProtoTypeHub placement is intentionally left for the lobby integration step.");
    }

    private static CorridorTravelSpec[] CreateSpecs()
    {
        return new[]
        {
            new CorridorTravelSpec(
                "Shadow",
                "Assets/_Project/Data/SceneFlow/Routes/ShadowCorridorBossRouteSet.asset"),
            new CorridorTravelSpec(
                "Dragon",
                "Assets/_Project/Data/SceneFlow/Routes/Dragon_CorridorBossRouteSet.asset"),
            new CorridorTravelSpec(
                "Slime",
                "Assets/_Project/Data/SceneFlow/Routes/SlimeRouteSet.asset"),
            new CorridorTravelSpec(
                "DemonKing",
                "Assets/_Project/Data/SceneFlow/Routes/DemonkingRouteSet.asset")
        };
    }

    private static void InstallTheme(
        CorridorTravelSpec spec,
        SceneTravelPresentationProfileSO presentationProfile,
        GameObject travelPortalPrefab)
    {
        CorridorBossRouteSetSO routeSet =
            AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(spec.RouteSetPath);
        if (routeSet == null || !routeSet.IsValid)
            throw new InvalidOperationException($"Invalid route set: {spec.RouteSetPath}");

        SceneConnectionSO lobbyConnection = CreateOrUpdateLobbyConnection(
            routeSet,
            presentationProfile);
        SceneConnectionSO bossConnection = CreateOrUpdateBossConnection(
            routeSet,
            presentationProfile);

        ConfigureRoomTravelSlots(spec, travelPortalPrefab);
        ConfigureCorridorScene(spec, routeSet, lobbyConnection, bossConnection);
        ConfigureBossArrivalEndpoint(routeSet, bossConnection);
        VerifyThemeConfiguration(spec, routeSet, lobbyConnection, bossConnection);
    }

    private static SceneTravelPresentationProfileSO CreateOrUpdateDefaultPresentationProfile()
    {
        SceneTravelPresentationProfileSO profile =
            AssetDatabase.LoadAssetAtPath<SceneTravelPresentationProfileSO>(TravelProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<SceneTravelPresentationProfileSO>();
            AssetDatabase.CreateAsset(profile, TravelProfilePath);
        }

        SerializedObject serialized = new(profile);
        serialized.FindProperty("departureMode").enumValueIndex =
            (int)SceneTravelDepartureMode.PullIntoEndpoint;
        serialized.FindProperty("departureDuration").floatValue = 0.55f;
        serialized.FindProperty("departureRotationDegrees").floatValue = 720f;
        serialized.FindProperty("departureTargetOffset").vector3Value = Vector3.zero;
        serialized.FindProperty("transitionVisualMode").enumValueIndex =
            (int)SceneTransitionVisualMode.AlphaFade;
        serialized.FindProperty("coverDuration").floatValue = 0.2f;
        serialized.FindProperty("revealDuration").floatValue = 0.2f;
        serialized.FindProperty("arrivalMode").enumValueIndex =
            (int)SceneTravelArrivalMode.None;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static GameObject CreateOrUpdateTravelPortalPrefab()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePortalPrefabPath);
        if (source == null)
            throw new InvalidOperationException($"Missing source portal prefab: {SourcePortalPrefabPath}");

        if (AssetDatabase.LoadAssetAtPath<GameObject>(TravelPortalPrefabPath) == null)
        {
            if (!AssetDatabase.CopyAsset(SourcePortalPrefabPath, TravelPortalPrefabPath))
                throw new InvalidOperationException("Failed to copy the procedural travel portal prefab.");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(TravelPortalPrefabPath);
        try
        {
            GameObject highlightTarget = null;
            ScenePortal[] legacyPortals = root.GetComponentsInChildren<ScenePortal>(true);
            for (int i = 0; i < legacyPortals.Length; i++)
            {
                SerializedObject serializedLegacy = new(legacyPortals[i]);
                SerializedProperty highlight = serializedLegacy.FindProperty("highlightTarget");
                if (highlightTarget == null)
                    highlightTarget = highlight?.objectReferenceValue as GameObject;
                UnityEngine.Object.DestroyImmediate(legacyPortals[i]);
            }

            SceneTravelEndpoint endpoint = root.GetComponent<SceneTravelEndpoint>();
            if (endpoint == null)
                endpoint = root.AddComponent<SceneTravelEndpoint>();

            Transform travelAnchor = root.transform.Find("TravelAnchor");
            if (travelAnchor == null)
            {
                GameObject anchorObject = new("TravelAnchor");
                travelAnchor = anchorObject.transform;
                travelAnchor.SetParent(root.transform, false);
            }

            travelAnchor.localPosition = new Vector3(0f, 0.64f, 0f);
            travelAnchor.localRotation = Quaternion.identity;
            travelAnchor.localScale = Vector3.one;
            endpoint.EditorConfigure(
                string.Empty,
                string.Empty,
                null,
                SceneConnectionEndpointSide.A);

            SerializedObject serializedEndpoint = new(endpoint);
            serializedEndpoint.FindProperty("departureAnchor").objectReferenceValue = travelAnchor;
            serializedEndpoint.FindProperty("arrivalAnchor").objectReferenceValue = travelAnchor;
            serializedEndpoint.ApplyModifiedPropertiesWithoutUndo();

            SceneTravelTrigger2D trigger = root.GetComponent<SceneTravelTrigger2D>();
            if (trigger != null)
                UnityEngine.Object.DestroyImmediate(trigger);

            SceneTravelInteractable interactable = root.GetComponent<SceneTravelInteractable>();
            if (interactable == null)
                interactable = root.AddComponent<SceneTravelInteractable>();

            SerializedObject serializedInteractable = new(interactable);
            serializedInteractable.FindProperty("endpoint").objectReferenceValue = endpoint;
            serializedInteractable.FindProperty("interactPromptText").stringValue = "이동하기";
            serializedInteractable.FindProperty("highlightTarget").objectReferenceValue = highlightTarget;
            serializedInteractable.ApplyModifiedPropertiesWithoutUndo();

            Collider2D collider = root.GetComponent<Collider2D>();
            if (collider == null)
                collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            root.name = "ProceduralSceneTravelPortal";

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, TravelPortalPrefabPath);
            if (saved == null)
                throw new InvalidOperationException("Failed to save the procedural travel portal prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.ImportAsset(
            TravelPortalPrefabPath,
            ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<GameObject>(TravelPortalPrefabPath);
    }

    private static SceneConnectionSO CreateOrUpdateLobbyConnection(
        CorridorBossRouteSetSO routeSet,
        SceneTravelPresentationProfileSO profile)
    {
        string themeId = routeSet.StableThemeId;
        string assetPath = $"{ConnectionFolder}/Lobby_{SanitizeAssetName(themeId)}_Corridor.asset";
        SceneConnectionSO connection = LoadOrCreateConnection(assetPath);
        SerializedObject serialized = new(connection);
        serialized.FindProperty("connectionId").stringValue = $"lobby_corridor_{themeId}";
        ConfigureEndpoint(
            serialized.FindProperty("endpointA"),
            LobbySceneName,
            $"Lobby.{themeId}.Corridor");
        ConfigureEndpoint(
            serialized.FindProperty("endpointB"),
            routeSet.CorridorSceneName,
            $"Corridor.{themeId}.Lobby");
        ConfigureDirection(
            serialized.FindProperty("aToB"),
            SceneTravelRunAction.StartRun,
            profile,
            SceneTravelGateKind.None,
            null);
        ConfigureDirection(
            serialized.FindProperty("bToA"),
            SceneTravelRunAction.None,
            profile,
            SceneTravelGateKind.None,
            null);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(connection);
        return connection;
    }

    private static SceneConnectionSO CreateOrUpdateBossConnection(
        CorridorBossRouteSetSO routeSet,
        SceneTravelPresentationProfileSO profile)
    {
        string themeId = routeSet.StableThemeId;
        string assetPath = $"{ConnectionFolder}/Corridor_{SanitizeAssetName(themeId)}_Boss.asset";
        SceneConnectionSO connection = LoadOrCreateConnection(assetPath);
        SerializedObject serialized = new(connection);
        serialized.FindProperty("connectionId").stringValue = $"corridor_boss_{themeId}";
        ConfigureEndpoint(
            serialized.FindProperty("endpointA"),
            routeSet.CorridorSceneName,
            $"Corridor.{themeId}.Boss");
        ConfigureEndpoint(
            serialized.FindProperty("endpointB"),
            routeSet.BossSceneName,
            $"Boss.{themeId}.Corridor");
        ConfigureDirection(
            serialized.FindProperty("aToB"),
            SceneTravelRunAction.None,
            profile,
            SceneTravelGateKind.BossNotDefeatedThisRun,
            themeId);
        ConfigureDirection(
            serialized.FindProperty("bToA"),
            SceneTravelRunAction.None,
            profile,
            SceneTravelGateKind.None,
            null);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(connection);
        return connection;
    }

    private static SceneConnectionSO LoadOrCreateConnection(string assetPath)
    {
        SceneConnectionSO connection = AssetDatabase.LoadAssetAtPath<SceneConnectionSO>(assetPath);
        if (connection != null)
            return connection;

        if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null && !AssetDatabase.DeleteAsset(assetPath))
            throw new InvalidOperationException($"Failed to replace invalid connection asset: {assetPath}");

        connection = ScriptableObject.CreateInstance<SceneConnectionSO>();
        AssetDatabase.CreateAsset(connection, assetPath);
        return connection;
    }

    private static void ConfigureEndpoint(
        SerializedProperty endpoint,
        string sceneName,
        string endpointId)
    {
        endpoint.FindPropertyRelative("sceneName").stringValue = sceneName;
        endpoint.FindPropertyRelative("endpointId").stringValue = endpointId;
    }

    private static void ConfigureDirection(
        SerializedProperty direction,
        SceneTravelRunAction runAction,
        SceneTravelPresentationProfileSO profile,
        SceneTravelGateKind gateKind,
        string gateSubjectId)
    {
        direction.FindPropertyRelative("enabled").boolValue = true;
        direction.FindPropertyRelative("runAction").enumValueIndex = (int)runAction;
        direction.FindPropertyRelative("runEndReason").enumValueIndex = (int)RunEndReason.None;
        direction.FindPropertyRelative("preservePlayerRuntimeState").boolValue = true;
        direction.FindPropertyRelative("fullyHealPlayer").boolValue = false;
        direction.FindPropertyRelative("resetCooldowns").boolValue = false;
        direction.FindPropertyRelative("clearAllEffects").boolValue = false;
        direction.FindPropertyRelative("clearCombatOnlyEffects").boolValue = false;
        direction.FindPropertyRelative("presentationProfile").objectReferenceValue = profile;

        SerializedProperty gates = direction.FindPropertyRelative("gates");
        gates.arraySize = gateKind == SceneTravelGateKind.None ? 0 : 1;
        if (gates.arraySize == 0)
            return;

        SerializedProperty gate = gates.GetArrayElementAtIndex(0);
        gate.FindPropertyRelative("kind").enumValueIndex = (int)gateKind;
        gate.FindPropertyRelative("subjectId").stringValue = gateSubjectId ?? string.Empty;
        gate.FindPropertyRelative("failureWarning").enumValueIndex =
            (int)WarningPopupCode.BossAlreadyDefeatedThisRun;
    }

    private static void ConfigureRoomTravelSlots(
        CorridorTravelSpec spec,
        GameObject travelPortalPrefab)
    {
        RoomTemplateSO startRoom = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(spec.StartRoomPath);
        RoomTemplateSO bossRoom = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(spec.BossRoomPath);
        if (startRoom == null || bossRoom == null)
        {
            throw new InvalidOperationException(
                $"Missing theme room assets. Start={spec.StartRoomPath}, Boss={spec.BossRoomPath}");
        }

        RoomBuildData startBuild = startRoom.BuildData;
        startBuild.travelEndpointPlacements =
            CopyWithoutSlots(startBuild.travelEndpointPlacements, LobbySlotId);
        RectInt startBounds = startRoom.LayoutData.localBounds;
        startBuild.travelEndpointPlacements.Add(new RoomTravelEndpointPlacementData
        {
            slotId = LobbySlotId,
            kind = RoomTravelEndpointKind.Trigger,
            mediumPrefab = null,
            localCell = new Vector2Int(
                startBounds.xMin + 1,
                startBounds.yMin + startBounds.height / 2),
            localOffset = Vector2.zero,
            localRotationDegrees = 0f,
            localScale = new Vector3(2f, 3f, 1f)
        });
        startRoom.EditorSetData(startRoom.LayoutData, startBuild);
        EditorUtility.SetDirty(startRoom);

        RoomBuildData bossBuild = bossRoom.BuildData;
        bossBuild.objectPlacements ??= new List<RoomObjectPlacementData>();
        bossBuild.objectPlacements.RemoveAll(
            placement => string.Equals(
                placement.placementId,
                "ExitPortal",
                StringComparison.Ordinal));
        bossBuild.travelEndpointPlacements =
            CopyWithoutSlots(bossBuild.travelEndpointPlacements, BossSlotId);
        RectInt bossBounds = bossRoom.LayoutData.localBounds;
        bossBuild.travelEndpointPlacements.Add(new RoomTravelEndpointPlacementData
        {
            slotId = BossSlotId,
            kind = RoomTravelEndpointKind.Interaction,
            mediumPrefab = travelPortalPrefab,
            localCell = new Vector2Int(
                bossBounds.xMin + bossBounds.width / 2,
                bossBounds.yMin + bossBounds.height / 2),
            localOffset = Vector2.zero,
            localRotationDegrees = 0f,
            localScale = Vector3.one
        });
        bossRoom.EditorSetData(bossRoom.LayoutData, bossBuild);
        EditorUtility.SetDirty(bossRoom);
    }

    private static List<RoomTravelEndpointPlacementData> CopyWithoutSlots(
        IReadOnlyList<RoomTravelEndpointPlacementData> source,
        params string[] slotIds)
    {
        var result = new List<RoomTravelEndpointPlacementData>();
        for (int i = 0; source != null && i < source.Count; i++)
        {
            RoomTravelEndpointPlacementData placement = source[i];
            bool shouldRemove = false;
            for (int slotIndex = 0; slotIndex < slotIds.Length; slotIndex++)
            {
                if (!string.Equals(placement.slotId, slotIds[slotIndex], StringComparison.Ordinal))
                    continue;

                shouldRemove = true;
                break;
            }

            if (!shouldRemove)
                result.Add(placement);
        }

        return result;
    }

    private static void ConfigureCorridorScene(
        CorridorTravelSpec spec,
        CorridorBossRouteSetSO routeSet,
        SceneConnectionSO lobbyConnection,
        SceneConnectionSO bossConnection)
    {
        string scenePath = $"Assets/_Project/Scenes/{routeSet.CorridorSceneName}.unity";
        RequireSceneAsset(scenePath);
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        DungeonRoomBuilder builder = FindComponentInScene<DungeonRoomBuilder>(scene);
        DungeonGenerator generator = FindComponentInScene<DungeonGenerator>(scene);
        if (builder == null || generator == null)
            throw new InvalidOperationException($"Corridor scene is missing generator pipeline: {scenePath}");

        Transform travelRoot = builder.GeneratedTravelEndpointRoot;
        if (travelRoot == null)
        {
            Transform existing = builder.transform.Find(GeneratedTravelRootName);
            if (existing != null)
            {
                travelRoot = existing;
            }
            else
            {
                GameObject rootObject = new(GeneratedTravelRootName);
                travelRoot = rootObject.transform;
                travelRoot.SetParent(builder.transform, false);
            }
        }

        SerializedObject serializedBuilder = new(builder);
        serializedBuilder.FindProperty("generatedTravelEndpointRoot").objectReferenceValue = travelRoot;
        SerializedProperty bindings = serializedBuilder.FindProperty("travelEndpointBindings");
        bindings.arraySize = 2;
        ConfigureBinding(
            bindings.GetArrayElementAtIndex(0),
            spec.StartRoomId,
            LobbySlotId,
            lobbyConnection,
            SceneConnectionEndpointSide.B);
        ConfigureBinding(
            bindings.GetArrayElementAtIndex(1),
            spec.BossRoomId,
            BossSlotId,
            bossConnection,
            SceneConnectionEndpointSide.A);
        serializedBuilder.ApplyModifiedPropertiesWithoutUndo();

        generator.EditorConfigureReentryPolicy(
            $"procedural_corridor_{routeSet.StableThemeId}",
            DungeonReentryPolicy.RegenerateOnEntry);
        EditorUtility.SetDirty(builder);
        EditorUtility.SetDirty(generator);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (!generator.Generate())
        {
            string reason = generator.LastLayout != null
                ? generator.LastLayout.FailureReason
                : "No layout result.";
            throw new InvalidOperationException(
                $"Corridor travel generation verification failed. Scene={scenePath}, Reason={reason}");
        }

        VerifyGeneratedCorridorEndpoints(builder, lobbyConnection, bossConnection);
        builder.ClearGeneratedContent();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureBinding(
        SerializedProperty binding,
        string roomId,
        string slotId,
        SceneConnectionSO connection,
        SceneConnectionEndpointSide side)
    {
        binding.FindPropertyRelative("roomId").stringValue = roomId;
        binding.FindPropertyRelative("slotId").stringValue = slotId;
        binding.FindPropertyRelative("connection").objectReferenceValue = connection;
        binding.FindPropertyRelative("connectionSide").enumValueIndex = (int)side;
    }

    private static void VerifyGeneratedCorridorEndpoints(
        DungeonRoomBuilder builder,
        SceneConnectionSO lobbyConnection,
        SceneConnectionSO bossConnection)
    {
        SceneTravelEndpoint lobbyEndpoint = null;
        SceneTravelEndpoint bossEndpoint = null;
        IReadOnlyList<SceneTravelEndpoint> endpoints = builder.GeneratedTravelEndpoints;
        for (int i = 0; i < endpoints.Count; i++)
        {
            SceneTravelEndpoint endpoint = endpoints[i];
            if (endpoint == null)
                continue;

            if (endpoint.ProceduralSlotId == LobbySlotId)
                lobbyEndpoint = endpoint;
            else if (endpoint.ProceduralSlotId == BossSlotId)
                bossEndpoint = endpoint;
        }

        if (lobbyEndpoint == null ||
            lobbyEndpoint.Connection != lobbyConnection ||
            lobbyEndpoint.GetComponent<SceneTravelTrigger2D>() == null ||
            bossEndpoint == null ||
            bossEndpoint.Connection != bossConnection ||
            bossEndpoint.GetComponent<SceneTravelInteractable>() == null)
        {
            throw new InvalidOperationException(
                "Generated Corridor endpoints do not match the LobbyGate/BossGate binding contract.");
        }
    }

    private static void ConfigureBossArrivalEndpoint(
        CorridorBossRouteSetSO routeSet,
        SceneConnectionSO bossConnection)
    {
        string scenePath = $"Assets/_Project/Scenes/{routeSet.BossSceneName}.unity";
        RequireSceneAsset(scenePath);
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        PlayerSpawnPoint spawnPoint = ResolveSpawnPoint(scene, routeSet.BossEntryPointId);
        if (spawnPoint == null)
            throw new InvalidOperationException($"Boss scene has no PlayerSpawnPoint: {scenePath}");

        GameObject root = FindRoot(scene, BossSceneTravelRootName);
        if (root == null)
            root = new GameObject(BossSceneTravelRootName);

        string endpointObjectName = $"CorridorArrival_{SanitizeAssetName(routeSet.StableThemeId)}";
        Transform previous = root.transform.Find(endpointObjectName);
        if (previous != null)
            UnityEngine.Object.DestroyImmediate(previous.gameObject);

        GameObject endpointObject = new(endpointObjectName);
        endpointObject.transform.SetParent(root.transform, false);
        endpointObject.transform.SetPositionAndRotation(
            spawnPoint.transform.position,
            spawnPoint.transform.rotation);
        SceneTravelEndpoint endpoint = endpointObject.AddComponent<SceneTravelEndpoint>();
        endpoint.EditorConfigure(
            bossConnection.EndpointB.EndpointId,
            string.Empty,
            bossConnection,
            SceneConnectionEndpointSide.B);

        EditorUtility.SetDirty(endpoint);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static PlayerSpawnPoint ResolveSpawnPoint(Scene scene, string entryPointId)
    {
        PlayerSpawnPoint fallback = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            PlayerSpawnPoint[] points =
                roots[rootIndex].GetComponentsInChildren<PlayerSpawnPoint>(true);
            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
            {
                PlayerSpawnPoint point = points[pointIndex];
                if (point == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(entryPointId) && point.pointId == entryPointId)
                    return point;

                if (fallback == null || point.isDefault)
                    fallback = point;
            }
        }

        return fallback;
    }

    private static void VerifyThemeConfiguration(
        CorridorTravelSpec spec,
        CorridorBossRouteSetSO routeSet,
        SceneConnectionSO lobbyConnection,
        SceneConnectionSO bossConnection)
    {
        RoomTemplateSO startRoom = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(spec.StartRoomPath);
        RoomTemplateSO bossRoom = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(spec.BossRoomPath);
        if (!HasTravelSlot(startRoom, LobbySlotId, RoomTravelEndpointKind.Trigger) ||
            !HasTravelSlot(bossRoom, BossSlotId, RoomTravelEndpointKind.Interaction) ||
            HasLegacyExitPortal(bossRoom) ||
            lobbyConnection.EndpointB.SceneName != routeSet.CorridorSceneName ||
            bossConnection.EndpointA.SceneName != routeSet.CorridorSceneName ||
            bossConnection.EndpointB.SceneName != routeSet.BossSceneName)
        {
            throw new InvalidOperationException(
                $"Travel content verification failed for theme '{routeSet.StableThemeId}'.");
        }
    }

    private static bool HasTravelSlot(
        RoomTemplateSO room,
        string slotId,
        RoomTravelEndpointKind kind)
    {
        IReadOnlyList<RoomTravelEndpointPlacementData> placements =
            room != null ? room.BuildData.travelEndpointPlacements : null;
        for (int i = 0; placements != null && i < placements.Count; i++)
        {
            if (placements[i].slotId == slotId && placements[i].kind == kind)
                return true;
        }

        return false;
    }

    private static bool HasLegacyExitPortal(RoomTemplateSO room)
    {
        IReadOnlyList<RoomObjectPlacementData> placements =
            room != null ? room.BuildData.objectPlacements : null;
        for (int i = 0; placements != null && i < placements.Count; i++)
        {
            if (placements[i].placementId == "ExitPortal")
                return true;
        }

        return false;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static GameObject FindRoot(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == objectName)
                return roots[i];
        }

        return null;
    }

    private static void RequireSceneAsset(string scenePath)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            throw new InvalidOperationException($"Missing scene: {scenePath}");
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        int separator = folderPath.LastIndexOf('/');
        if (separator <= 0)
            throw new InvalidOperationException($"Invalid asset folder path: {folderPath}");

        string parent = folderPath.Substring(0, separator);
        string name = folderPath.Substring(separator + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static string SanitizeAssetName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : value.Replace(' ', '_').Replace('/', '_').Replace('\\', '_');
    }
}
