using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임 : 세 일반 보스 테마 절차 복도의 로비·보스·단방향 복도 간 이동 슬롯, 연결 에셋, 씬별 binding과 도착 endpoint를 재현 가능하게 설치하고 검증한다.
/// </summary>
public static class ProceduralCorridorTravelInstaller
{
    private const string LobbySceneName = "ProtoTypeHub";
    private const string LobbyScenePath = "Assets/_Project/Scenes/ProtoTypeHub.unity";
    private const string RoomRootFolder = "Assets/_Project/Data/Dungeon/Rooms/BossThemes";
    private const string ConnectionFolder = "Assets/_Project/Data/SceneFlow/Connections";
    private const string TravelProfileFolder = "Assets/_Project/Data/SceneFlow/TravelProfiles";
    private const string DefaultTravelProfilePath =
        TravelProfileFolder + "/DefaultCorridorPortalTravel.asset";
    private const string LobbyToCorridorTravelProfilePath =
        TravelProfileFolder + "/LobbyToCorridorWipeTravel.asset";
    private const string CorridorPipeTravelProfilePath =
        TravelProfileFolder + "/CorridorToSlimePipeTravel.asset";
    private const string SourcePortalPrefabPath =
        "Assets/_Project/Prefabs/Map/Portal/ScenePortal.prefab";
    private const string TravelPortalPrefabPath =
        "Assets/_Project/Prefabs/Map/Procedural/ProceduralSceneTravelPortal.prefab";
    private const string SourceDrainPipePrefabPath =
        "Assets/_Project/Prefabs/Map/Gimmicks/SlimeCorridor/DrainPipe.prefab";
    private const string CorridorTravelPipePrefabPath =
        "Assets/_Project/Prefabs/Map/Procedural/ProceduralCorridorTravelPipe.prefab";
    private const string WaterArrivalVfxPrefabPath =
        "Assets/_Project/Prefabs/VFX/Effect_SlimeCorridorTeleport.prefab";
    private const string GeneratedTravelRootName = "GeneratedTravelEndpoints";
    private const string BossSceneTravelRootName = "[DataDrivenSceneTravel]";
    private const string LobbyGateObjectPrefix = "LobbyGate_";
    private const string RetiredDemonKingLobbyGateName = "LobbyGate_demon_king";
    private const string LobbySlotId = "LobbyGate";
    private const string BossSlotId = "BossGate";
    private const int CorridorLinkMinimumGraphDistance = 3;

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
        public string LibraryPath =>
            $"Assets/_Project/Data/Dungeon/Libraries/Procedural{ThemeName}Library.asset";
        public string GenerationProfilePath =>
            $"Assets/_Project/Data/Dungeon/GenerationProfiles/Procedural{ThemeName}GenerationProfile.asset";

        public CorridorTravelSpec(string themeName, string routeSetPath)
        {
            ThemeName = themeName;
            RouteSetPath = routeSetPath;
        }
    }

    /// <summary>
    /// 책임 : 한 단방향 복도 연결의 출발·도착 테마, 전용 방·슬롯·기반 템플릿과 연결 에셋 경로를 설치 단위로 묶는다.
    /// </summary>
    private readonly struct CorridorLinkSpec
    {
        public CorridorTravelSpec Source { get; }
        public CorridorTravelSpec Destination { get; }
        public string SourceRoomId { get; }
        public string DestinationRoomId { get; }
        public string SourceSlotId { get; }
        public string DestinationSlotId { get; }
        public string SourceTemplatePath { get; }
        public string DestinationTemplatePath { get; }
        public string ConnectionId { get; }
        public string ConnectionPath { get; }

        public string SourceRoomPath =>
            $"{RoomRootFolder}/{Source.ThemeName}/{SourceRoomId}.asset";
        public string DestinationRoomPath =>
            $"{RoomRootFolder}/{Destination.ThemeName}/{DestinationRoomId}.asset";

        public CorridorLinkSpec(
            CorridorTravelSpec source,
            CorridorTravelSpec destination,
            string sourceTemplatePath,
            string destinationTemplatePath)
        {
            Source = source;
            Destination = destination;
            SourceRoomId = $"{source.ThemeName}_Event_TravelTo{destination.ThemeName}";
            DestinationRoomId = $"{destination.ThemeName}_Event_ArrivalFrom{source.ThemeName}";
            SourceSlotId = $"CorridorLink.{source.ThemeName}To{destination.ThemeName}.Departure";
            DestinationSlotId = $"CorridorLink.{source.ThemeName}To{destination.ThemeName}.Arrival";
            SourceTemplatePath = sourceTemplatePath;
            DestinationTemplatePath = destinationTemplatePath;
            ConnectionId =
                $"corridor_{source.ThemeName.ToLowerInvariant()}_to_{destination.ThemeName.ToLowerInvariant()}";
            ConnectionPath =
                $"{ConnectionFolder}/Corridor_{source.ThemeName.ToLowerInvariant()}_To_{destination.ThemeName.ToLowerInvariant()}.asset";
        }
    }

    [MenuItem("Tools/Dungeon/Install Procedural Corridor Travel Configuration")]
    public static void Install()
    {
        EnsureFolder(ConnectionFolder);
        EnsureFolder(TravelProfileFolder);
        EnsureFolder("Assets/_Project/Prefabs/Map/Procedural");

        SceneTravelPresentationProfileSO defaultPresentationProfile =
            CreateOrUpdateDefaultPresentationProfile();
        SceneTravelPresentationProfileSO lobbyToCorridorPresentationProfile =
            CreateOrUpdateLobbyToCorridorPresentationProfile();
        SceneTravelPresentationProfileSO corridorPipePresentationProfile =
            CreateOrUpdateCorridorPipePresentationProfile();
        GameObject travelPortalPrefab = CreateOrUpdateTravelPortalPrefab();
        GameObject corridorTravelPipePrefab = CreateOrUpdateCorridorTravelPipePrefab();
        corridorPipePresentationProfile =
            AssetDatabase.LoadAssetAtPath<SceneTravelPresentationProfileSO>(
                CorridorPipeTravelProfilePath);
        corridorTravelPipePrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(CorridorTravelPipePrefabPath);
        if (corridorPipePresentationProfile == null || corridorTravelPipePrefab == null)
        {
            throw new InvalidOperationException(
                "Failed to reload the persistent Corridor pipe travel assets.");
        }

        CorridorTravelSpec[] specs = CreateSpecs();
        CorridorLinkSpec[] corridorLinks = CreateCorridorLinkSpecs(specs);

        for (int i = 0; i < specs.Length; i++)
        {
            InstallTheme(
                specs[i],
                lobbyToCorridorPresentationProfile,
                defaultPresentationProfile,
                travelPortalPrefab);
        }

        for (int linkIndex = 0; linkIndex < corridorLinks.Length; linkIndex++)
        {
            InstallCorridorLink(
                corridorLinks[linkIndex],
                corridorPipePresentationProfile,
                corridorTravelPipePrefab);
        }

        ConfigureCorridorLinkScenes(specs, corridorLinks);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log(
            $"Installed and verified data-driven travel configuration for {specs.Length} procedural Corridors " +
            $"and {corridorLinks.Length} one-way Corridor links. " +
            "Lobby-to-Corridor and Corridor-to-Lobby travel share the authored right-to-left black wipe. " +
            "ProtoTypeHub placement is intentionally left for the lobby integration step.");
    }

    /// <summary>
    /// 책임:
    /// - 로비의 일반 보스 A측 이동 게이트 세 개를 설치하거나 누락된 구성을 복구한다.
    /// - 이미 배치된 게이트의 위치와 활성 상태는 보존하고, 새 게이트만 안전한 비활성 상태로 만든다.
    /// </summary>
    [MenuItem("Tools/Dungeon/Install Lobby Procedural Corridor Gate Staging Objects")]
    public static void InstallLobbyGateStagingObjects()
    {
        RequireSceneAsset(LobbyScenePath);
        Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        GameObject travelRoot = FindRoot(scene, BossSceneTravelRootName);
        if (travelRoot == null)
            travelRoot = new GameObject(BossSceneTravelRootName);

        RemoveRetiredDemonKingLobbyGate(travelRoot.transform);

        CorridorTravelSpec[] specs = CreateSpecs();
        int createdGateCount = 0;
        for (int i = 0; i < specs.Length; i++)
        {
            CorridorBossRouteSetSO routeSet =
                AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(specs[i].RouteSetPath);
            if (routeSet == null || !routeSet.IsValid)
                throw new InvalidOperationException($"Invalid route set: {specs[i].RouteSetPath}");

            string connectionPath =
                $"{ConnectionFolder}/Lobby_{SanitizeAssetName(routeSet.StableThemeId)}_Corridor.asset";
            SceneConnectionSO connection =
                AssetDatabase.LoadAssetAtPath<SceneConnectionSO>(connectionPath);
            if (connection == null ||
                !string.Equals(connection.EndpointA.SceneName, LobbySceneName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Missing or invalid Lobby connection: {connectionPath}");
            }

            if (CreateOrUpdateLobbyGateStagingObject(travelRoot.transform, routeSet, connection))
                createdGateCount++;
        }

        EditorUtility.SetDirty(travelRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException($"Failed to save Lobby scene: {LobbyScenePath}");

        Debug.Log(
            $"Installed or repaired {specs.Length} Lobby procedural Corridor gates. " +
            $"CreatedInactive={createdGateCount}, PreservedExisting={specs.Length - createdGateCount}. " +
            "New gates must be placed and enabled after layout authoring is complete.");
    }

    /// <summary>
    /// 책임 : 최종보스 전용 고정 휴식 복도로 이동해야 하는 DemonKing의 설치기 소유 로비 게이트를 제거한다.
    /// </summary>
    private static void RemoveRetiredDemonKingLobbyGate(Transform travelRoot)
    {
        Transform retiredGate = travelRoot.Find(RetiredDemonKingLobbyGateName);
        if (retiredGate != null)
            UnityEngine.Object.DestroyImmediate(retiredGate.gameObject);
    }

    /// <summary>
    /// 책임 : 한 테마의 로비 A측 endpoint, 자동 trigger, collider와 출발·도착 anchor를 중복 없이 구성한다.
    /// </summary>
    private static bool CreateOrUpdateLobbyGateStagingObject(
        Transform travelRoot,
        CorridorBossRouteSetSO routeSet,
        SceneConnectionSO connection)
    {
        string themeId = routeSet.StableThemeId;
        string objectName = LobbyGateObjectPrefix + SanitizeAssetName(themeId);
        Transform existing = travelRoot.Find(objectName);
        bool wasCreated = existing == null;
        GameObject gateObject;
        if (wasCreated)
        {
            gateObject = new GameObject(objectName);
            gateObject.transform.SetParent(travelRoot, false);
        }
        else
        {
            gateObject = existing.gameObject;
        }

        SceneTravelEndpoint endpoint = gateObject.GetComponent<SceneTravelEndpoint>();
        if (endpoint == null)
            endpoint = gateObject.AddComponent<SceneTravelEndpoint>();

        endpoint.EditorConfigure(
            connection.EndpointA.EndpointId,
            string.Empty,
            connection,
            SceneConnectionEndpointSide.A);

        Transform departureAnchor = FindOrCreateAnchor(gateObject.transform, "DepartureAnchor");
        Transform arrivalAnchor = FindOrCreateAnchor(gateObject.transform, "ArrivalAnchor");
        SerializedObject serializedEndpoint = new(endpoint);
        serializedEndpoint.FindProperty("departureAnchor").objectReferenceValue = departureAnchor;
        serializedEndpoint.FindProperty("arrivalAnchor").objectReferenceValue = arrivalAnchor;
        serializedEndpoint.ApplyModifiedPropertiesWithoutUndo();

        BoxCollider2D triggerCollider = gateObject.GetComponent<BoxCollider2D>();
        if (triggerCollider == null)
        {
            triggerCollider = gateObject.AddComponent<BoxCollider2D>();
            triggerCollider.size = new Vector2(2f, 2f);
        }

        triggerCollider.isTrigger = true;

        SceneTravelTrigger2D trigger = gateObject.GetComponent<SceneTravelTrigger2D>();
        if (trigger == null)
            trigger = gateObject.AddComponent<SceneTravelTrigger2D>();

        SerializedObject serializedTrigger = new(trigger);
        serializedTrigger.FindProperty("endpoint").objectReferenceValue = endpoint;
        serializedTrigger.ApplyModifiedPropertiesWithoutUndo();

        SceneTravelInteractable interactable = gateObject.GetComponent<SceneTravelInteractable>();
        if (interactable != null)
            interactable.enabled = false;

        EditorUtility.SetDirty(endpoint);
        EditorUtility.SetDirty(trigger);
        EditorUtility.SetDirty(triggerCollider);
        EditorUtility.SetDirty(gateObject);

        if (wasCreated)
            gateObject.SetActive(false);

        return wasCreated;
    }

    /// <summary>
    /// 책임 : 게이트의 시각 목표와 도착 위치로 사용할 로컬 원점 anchor를 중복 없이 제공한다.
    /// </summary>
    private static Transform FindOrCreateAnchor(Transform parent, string anchorName)
    {
        Transform anchor = parent.Find(anchorName);
        if (anchor != null)
            return anchor;

        GameObject anchorObject = new(anchorName);
        anchor = anchorObject.transform;
        anchor.SetParent(parent, false);
        anchor.localPosition = Vector3.zero;
        anchor.localRotation = Quaternion.identity;
        anchor.localScale = Vector3.one;
        return anchor;
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
                "Assets/_Project/Data/SceneFlow/Routes/SlimeRouteSet.asset")
        };
    }

    private static CorridorLinkSpec[] CreateCorridorLinkSpecs(
        IReadOnlyList<CorridorTravelSpec> specs)
    {
        if (specs == null || specs.Count != 3)
            throw new InvalidOperationException("Expected Shadow, Dragon and Slime travel specs.");

        CorridorTravelSpec shadow = specs[0];
        CorridorTravelSpec dragon = specs[1];
        CorridorTravelSpec slime = specs[2];
        return new[]
        {
            new CorridorLinkSpec(
                shadow,
                slime,
                $"{RoomRootFolder}/Shadow/Shadow_Combat_Wide.asset",
                $"{RoomRootFolder}/Slime/Slime_Combat_Wide.asset"),
            new CorridorLinkSpec(
                dragon,
                slime,
                $"{RoomRootFolder}/Dragon/Dragon_Combat_Wide.asset",
                $"{RoomRootFolder}/Slime/Slime_Treasure_Sacrifice.asset")
        };
    }

    private static void InstallTheme(
        CorridorTravelSpec spec,
        SceneTravelPresentationProfileSO lobbyToCorridorPresentationProfile,
        SceneTravelPresentationProfileSO defaultPresentationProfile,
        GameObject travelPortalPrefab)
    {
        CorridorBossRouteSetSO routeSet =
            AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(spec.RouteSetPath);
        if (routeSet == null || !routeSet.IsValid)
            throw new InvalidOperationException($"Invalid route set: {spec.RouteSetPath}");

        SceneConnectionSO lobbyConnection = CreateOrUpdateLobbyConnection(
            routeSet,
            lobbyToCorridorPresentationProfile);
        SceneConnectionSO bossConnection = CreateOrUpdateBossConnection(
            routeSet,
            defaultPresentationProfile);

        ConfigureRoomTravelSlots(spec, travelPortalPrefab);
        ConfigureCorridorScene(spec, routeSet, lobbyConnection, bossConnection);
        ConfigureBossArrivalEndpoint(routeSet, bossConnection);
        VerifyThemeConfiguration(
            spec,
            routeSet,
            lobbyConnection,
            bossConnection,
            lobbyToCorridorPresentationProfile,
            defaultPresentationProfile);
    }

    private static SceneTravelPresentationProfileSO CreateOrUpdateDefaultPresentationProfile()
    {
        return CreateOrUpdatePresentationProfile(
            DefaultTravelProfilePath,
            SceneTravelDepartureMode.PullIntoEndpoint,
            departureDuration: 0.55f,
            departureRotationDegrees: 720f,
            transitionVisualMode: SceneTransitionVisualMode.AlphaFade,
            coverDuration: 0.2f,
            revealDuration: 0.2f);
    }

    private static SceneTravelPresentationProfileSO CreateOrUpdateLobbyToCorridorPresentationProfile()
    {
        return CreateOrUpdatePresentationProfile(
            LobbyToCorridorTravelProfilePath,
            SceneTravelDepartureMode.None,
            departureDuration: 0f,
            departureRotationDegrees: 0f,
            transitionVisualMode: SceneTransitionVisualMode.HorizontalWipeRightToLeft,
            coverDuration: 0.2f,
            revealDuration: 0.2f);
    }

    private static SceneTravelPresentationProfileSO CreateOrUpdateCorridorPipePresentationProfile()
    {
        SceneTravelPresentationProfileSO profile = CreateOrUpdatePresentationProfile(
            CorridorPipeTravelProfilePath,
            SceneTravelDepartureMode.PullIntoEndpoint,
            departureDuration: 0.55f,
            departureRotationDegrees: 720f,
            transitionVisualMode: SceneTransitionVisualMode.AlphaFade,
            coverDuration: 0.2f,
            revealDuration: 0.2f);
        GameObject waterArrivalVfx =
            AssetDatabase.LoadAssetAtPath<GameObject>(WaterArrivalVfxPrefabPath);
        if (waterArrivalVfx == null)
        {
            throw new InvalidOperationException(
                $"Missing Corridor pipe arrival VFX: {WaterArrivalVfxPrefabPath}");
        }

        SerializedObject serialized = new(profile);
        serialized.FindProperty("arrivalMode").enumValueIndex =
            (int)SceneTravelArrivalMode.MoveFromOffset;
        serialized.FindProperty("arrivalDuration").floatValue = 0.45f;
        serialized.FindProperty("arrivalStartOffset").vector3Value = new Vector3(0f, -1.2f, 0f);
        serialized.FindProperty("arrivalRotationDegrees").floatValue = 0f;
        ConfigureSoundRef(
            serialized.FindProperty("departureSound"),
            "sound_drainpipe_open");
        ConfigureSoundRef(
            serialized.FindProperty("arrivalSound"),
            "sound_drainPipe_SlimeFall1");
        SerializedProperty arrivalParticle = serialized
            .FindProperty("arrivalPresentation")
            .FindPropertyRelative("presentationOnExecute")
            .FindPropertyRelative("particle");
        ConfigureSpawnedPresentationHook(
            arrivalParticle,
            waterArrivalVfx,
            new Vector3(0f, -1.2f, 0f));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        return profile;
    }

    private static void ConfigureSoundRef(SerializedProperty sound, string key)
    {
        sound.FindPropertyRelative("key").stringValue = key ?? string.Empty;
        sound.FindPropertyRelative("volumeMultiplier").floatValue = 1f;
        sound.FindPropertyRelative("anchorPolicy").enumValueIndex = 0;
        sound.FindPropertyRelative("localOffset").vector3Value = Vector3.zero;
    }

    private static void ConfigureSpawnedPresentationHook(
        SerializedProperty hook,
        GameObject prefab,
        Vector3 localOffset)
    {
        hook.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        hook.FindPropertyRelative("localOffset").vector3Value = localOffset;
        hook.FindPropertyRelative("rotationOffsetZ").floatValue = 0f;
        hook.FindPropertyRelative("scaleMultiplier").vector3Value = Vector3.one;
        hook.FindPropertyRelative("attachToTarget").boolValue = false;
        hook.FindPropertyRelative("anchorMode").enumValueIndex = 0;
        hook.FindPropertyRelative("scaleMode").enumValueIndex = 0;
        hook.FindPropertyRelative("boundsMode").enumValueIndex = 0;
        hook.FindPropertyRelative("targetBoundsReferenceSize").floatValue = 1f;
        hook.FindPropertyRelative("targetBoundsScaleMultiplier").floatValue = 1f;
        hook.FindPropertyRelative("lifetimeMode").enumValueIndex = 0;
        hook.FindPropertyRelative("lifetimeOverrideSeconds").floatValue = 0f;
        hook.FindPropertyRelative("useUnscaledTime").boolValue = false;
    }

    private static SceneTravelPresentationProfileSO CreateOrUpdatePresentationProfile(
        string assetPath,
        SceneTravelDepartureMode departureMode,
        float departureDuration,
        float departureRotationDegrees,
        SceneTransitionVisualMode transitionVisualMode,
        float coverDuration,
        float revealDuration)
    {
        SceneTravelPresentationProfileSO profile =
            AssetDatabase.LoadAssetAtPath<SceneTravelPresentationProfileSO>(assetPath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<SceneTravelPresentationProfileSO>();
            AssetDatabase.CreateAsset(profile, assetPath);
        }

        SerializedObject serialized = new(profile);
        serialized.FindProperty("departureMode").enumValueIndex =
            (int)departureMode;
        serialized.FindProperty("departureDuration").floatValue = departureDuration;
        serialized.FindProperty("departureRotationDegrees").floatValue = departureRotationDegrees;
        serialized.FindProperty("departureTargetOffset").vector3Value = Vector3.zero;
        serialized.FindProperty("transitionVisualMode").enumValueIndex =
            (int)transitionVisualMode;
        serialized.FindProperty("coverDuration").floatValue = coverDuration;
        serialized.FindProperty("revealDuration").floatValue = revealDuration;
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

    private static GameObject CreateOrUpdateCorridorTravelPipePrefab()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceDrainPipePrefabPath);
        if (source == null)
            throw new InvalidOperationException($"Missing source drain pipe: {SourceDrainPipePrefabPath}");

        SpriteRenderer sourceRenderer = source.GetComponent<SpriteRenderer>();
        CircleCollider2D sourceCollider = source.GetComponent<CircleCollider2D>();
        DrainPipe sourcePipe = source.GetComponent<DrainPipe>();
        if (sourceRenderer == null || sourceCollider == null || sourcePipe == null)
            throw new InvalidOperationException("Source drain pipe is missing its visual or collider contract.");

        SerializedObject serializedSourcePipe = new(sourcePipe);
        Sprite openPipeSprite = serializedSourcePipe
            .FindProperty("holeSprite")
            .objectReferenceValue as Sprite;
        var root = new GameObject("ProceduralCorridorTravelPipe");
        try
        {
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            EditorUtility.CopySerialized(sourceRenderer, renderer);
            if (openPipeSprite != null)
                renderer.sprite = openPipeSprite;

            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.offset = sourceCollider.offset;
            collider.radius = Mathf.Max(0.1f, sourceCollider.radius);
            collider.isTrigger = true;

            SceneTravelEndpoint endpoint = root.AddComponent<SceneTravelEndpoint>();
            Transform departureAnchor = FindOrCreateAnchor(root.transform, "DepartureAnchor");
            departureAnchor.localPosition = new Vector3(0f, 0.3f, 0f);
            Transform arrivalAnchor = FindOrCreateAnchor(root.transform, "ArrivalAnchor");
            arrivalAnchor.localPosition = new Vector3(0f, 1.5f, 0f);
            endpoint.EditorConfigure(
                string.Empty,
                string.Empty,
                null,
                SceneConnectionEndpointSide.A);

            SerializedObject serializedEndpoint = new(endpoint);
            serializedEndpoint.FindProperty("departureAnchor").objectReferenceValue = departureAnchor;
            serializedEndpoint.FindProperty("arrivalAnchor").objectReferenceValue = arrivalAnchor;
            serializedEndpoint.ApplyModifiedPropertiesWithoutUndo();

            SceneTravelInteractable interactable = root.AddComponent<SceneTravelInteractable>();
            SerializedObject serializedInteractable = new(interactable);
            serializedInteractable.FindProperty("endpoint").objectReferenceValue = endpoint;
            serializedInteractable.FindProperty("interactPromptText").stringValue = "토관 이용하기";
            serializedInteractable.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, CorridorTravelPipePrefabPath);
            if (saved == null)
            {
                throw new InvalidOperationException(
                    $"Failed to save Corridor travel pipe: {CorridorTravelPipePrefabPath}");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        AssetDatabase.ImportAsset(
            CorridorTravelPipePrefabPath,
            ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<GameObject>(CorridorTravelPipePrefabPath);
    }

    private static SceneConnectionSO CreateOrUpdateLobbyConnection(
        CorridorBossRouteSetSO routeSet,
        SceneTravelPresentationProfileSO lobbyTravelProfile)
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
            lobbyTravelProfile,
            SceneTravelGateKind.None,
            null);
        ConfigureDirection(
            serialized.FindProperty("bToA"),
            SceneTravelRunAction.None,
            lobbyTravelProfile,
            SceneTravelGateKind.None,
            null);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(connection);
        AssetDatabase.SaveAssetIfDirty(connection);
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
        AssetDatabase.SaveAssetIfDirty(connection);
        return connection;
    }

    private static void InstallCorridorLink(
        CorridorLinkSpec link,
        SceneTravelPresentationProfileSO presentationProfile,
        GameObject corridorTravelPipePrefab)
    {
        presentationProfile =
            AssetDatabase.LoadAssetAtPath<SceneTravelPresentationProfileSO>(
                CorridorPipeTravelProfilePath);
        corridorTravelPipePrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(CorridorTravelPipePrefabPath);
        if (presentationProfile == null || corridorTravelPipePrefab == null)
        {
            throw new InvalidOperationException(
                $"Missing persistent travel assets for Corridor link '{link.ConnectionId}'.");
        }

        CorridorBossRouteSetSO sourceRoute = LoadRequiredRouteSet(link.Source);
        CorridorBossRouteSetSO destinationRoute = LoadRequiredRouteSet(link.Destination);
        SceneConnectionSO connection = CreateOrUpdateCorridorLinkConnection(
            link,
            sourceRoute,
            destinationRoute,
            presentationProfile);
        RoomTemplateSO sourceRoom = CreateOrUpdateCorridorLinkRoom(
            link.SourceTemplatePath,
            link.SourceRoomPath,
            link.SourceRoomId,
            link.SourceSlotId,
            RoomTravelEndpointKind.Interaction,
            corridorTravelPipePrefab);
        RoomTemplateSO destinationRoom = CreateOrUpdateCorridorLinkRoom(
            link.DestinationTemplatePath,
            link.DestinationRoomPath,
            link.DestinationRoomId,
            link.DestinationSlotId,
            RoomTravelEndpointKind.ArrivalOnly,
            corridorTravelPipePrefab);

        RegisterGuaranteedRoom(link.Source, sourceRoom);
        RegisterGuaranteedRoom(link.Destination, destinationRoom);
        VerifyCorridorLinkConfiguration(
            link,
            sourceRoute,
            destinationRoute,
            connection,
            sourceRoom,
            destinationRoom,
            presentationProfile,
            corridorTravelPipePrefab);
    }

    private static CorridorBossRouteSetSO LoadRequiredRouteSet(CorridorTravelSpec spec)
    {
        CorridorBossRouteSetSO routeSet =
            AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(spec.RouteSetPath);
        if (routeSet == null || !routeSet.IsValid)
            throw new InvalidOperationException($"Invalid route set: {spec.RouteSetPath}");

        return routeSet;
    }

    private static SceneConnectionSO CreateOrUpdateCorridorLinkConnection(
        CorridorLinkSpec link,
        CorridorBossRouteSetSO sourceRoute,
        CorridorBossRouteSetSO destinationRoute,
        SceneTravelPresentationProfileSO profile)
    {
        SceneConnectionSO connection = LoadOrCreateConnection(link.ConnectionPath);
        SerializedObject serialized = new(connection);
        serialized.FindProperty("connectionId").stringValue = link.ConnectionId;
        ConfigureEndpoint(
            serialized.FindProperty("endpointA"),
            sourceRoute.CorridorSceneName,
            $"Corridor.{sourceRoute.StableThemeId}.To.{destinationRoute.StableThemeId}");
        ConfigureEndpoint(
            serialized.FindProperty("endpointB"),
            destinationRoute.CorridorSceneName,
            $"Corridor.{destinationRoute.StableThemeId}.From.{sourceRoute.StableThemeId}");
        ConfigureDirection(
            serialized.FindProperty("aToB"),
            SceneTravelRunAction.None,
            profile,
            SceneTravelGateKind.None,
            null);
        ConfigureDirection(
            serialized.FindProperty("bToA"),
            SceneTravelRunAction.None,
            profile: null,
            SceneTravelGateKind.None,
            gateSubjectId: null,
            enabled: false);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(connection);
        AssetDatabase.SaveAssetIfDirty(connection);
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
        string gateSubjectId,
        bool enabled = true)
    {
        direction.FindPropertyRelative("enabled").boolValue = enabled;
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

    private static RoomTemplateSO CreateOrUpdateCorridorLinkRoom(
        string sourceTemplatePath,
        string roomPath,
        string roomId,
        string slotId,
        RoomTravelEndpointKind endpointKind,
        GameObject corridorTravelPipePrefab)
    {
        RoomTemplateSO sourceRoom =
            AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(sourceTemplatePath);
        if (sourceRoom == null)
            throw new InvalidOperationException($"Missing Corridor link room source: {sourceTemplatePath}");

        if (AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(roomPath) == null)
        {
            if (!AssetDatabase.CopyAsset(sourceTemplatePath, roomPath))
                throw new InvalidOperationException($"Failed to create Corridor link room: {roomPath}");

            AssetDatabase.ImportAsset(roomPath, ImportAssetOptions.ForceSynchronousImport);
        }

        RoomTemplateSO room = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(roomPath);
        if (room == null)
            throw new InvalidOperationException($"Invalid Corridor link room asset: {roomPath}");

        RoomLayoutData layout = room.LayoutData;
        layout.roomId = roomId;
        layout.roomType = RoomType.Event;
        layout.difficultyTier = 0;
        layout.selectionWeight = 1f;
        layout.topologyPlacement = new RoomTopologyPlacementData
        {
            mode = RoomTopologyPlacementMode.FarthestFromStart,
            minimumGraphDistanceFromStart = CorridorLinkMinimumGraphDistance,
            requireDeadEnd = false
        };

        RoomBuildData build = room.BuildData;
        build.objectPlacements = new List<RoomObjectPlacementData>();
        build.travelEndpointPlacements = new List<RoomTravelEndpointPlacementData>
        {
            new()
            {
                slotId = slotId,
                kind = endpointKind,
                mediumPrefab = corridorTravelPipePrefab,
                localCell = ResolveTravelRoomFloorCell(build.floorTiles, layout.localBounds),
                localOffset = Vector2.zero,
                localRotationDegrees = 0f,
                localScale = Vector3.one
            }
        };
        room.name = roomId;
        room.EditorSetData(layout, build);
        EditorUtility.SetDirty(room);
        AssetDatabase.SaveAssetIfDirty(room);
        return room;
    }

    private static Vector2Int ResolveTravelRoomFloorCell(
        IReadOnlyList<RoomTileData> floorTiles,
        RectInt bounds)
    {
        if (floorTiles == null || floorTiles.Count == 0)
            throw new InvalidOperationException("Corridor link room has no Floor tile.");

        Vector2Int preferred = new(
            bounds.xMin + bounds.width / 2,
            bounds.yMin + bounds.height / 2);
        Vector2Int selected = floorTiles[0].localCell;
        int bestDistance = int.MaxValue;
        for (int tileIndex = 0; tileIndex < floorTiles.Count; tileIndex++)
        {
            Vector2Int cell = floorTiles[tileIndex].localCell;
            if (!bounds.Contains(cell) || floorTiles[tileIndex].tile == null)
                continue;

            int distance = (cell - preferred).sqrMagnitude;
            if (distance >= bestDistance)
                continue;

            selected = cell;
            bestDistance = distance;
        }

        if (bestDistance == int.MaxValue)
            throw new InvalidOperationException("Corridor link room has no valid Floor tile inside its bounds.");

        return selected;
    }

    private static void RegisterGuaranteedRoom(
        CorridorTravelSpec spec,
        RoomTemplateSO room)
    {
        RoomThemeLibrarySO library =
            AssetDatabase.LoadAssetAtPath<RoomThemeLibrarySO>(spec.LibraryPath);
        DungeonGenerationProfileSO profile =
            AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(spec.GenerationProfilePath);
        if (library == null || profile == null)
        {
            throw new InvalidOperationException(
                $"Missing Corridor library or generation profile. Library={spec.LibraryPath}, " +
                $"Profile={spec.GenerationProfilePath}");
        }

        library.EditorAddRoom(room);
        var guaranteedRooms = new List<RoomTemplateSO>();
        IReadOnlyList<RoomTemplateSO> existingRooms = profile.GuaranteedRoomTemplates;
        for (int roomIndex = 0; existingRooms != null && roomIndex < existingRooms.Count; roomIndex++)
        {
            RoomTemplateSO existingRoom = existingRooms[roomIndex];
            if (existingRoom != null && !guaranteedRooms.Contains(existingRoom))
                guaranteedRooms.Add(existingRoom);
        }

        if (!guaranteedRooms.Contains(room))
            guaranteedRooms.Add(room);

        profile.EditorSetGuaranteedRooms(guaranteedRooms);
        EditorUtility.SetDirty(library);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(library);
        AssetDatabase.SaveAssetIfDirty(profile);
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
        SceneConnectionSO bossConnection,
        IReadOnlyList<CorridorLinkSpec> corridorLinks = null)
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
        bindings.arraySize = 2 + CountCorridorLinkBindings(spec, corridorLinks);
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
        int bindingIndex = 2;
        for (int linkIndex = 0; corridorLinks != null && linkIndex < corridorLinks.Count; linkIndex++)
        {
            CorridorLinkSpec link = corridorLinks[linkIndex];
            bool isSource = string.Equals(
                link.Source.ThemeName,
                spec.ThemeName,
                StringComparison.Ordinal);
            bool isDestination = string.Equals(
                link.Destination.ThemeName,
                spec.ThemeName,
                StringComparison.Ordinal);
            if (!isSource && !isDestination)
                continue;

            SceneConnectionSO corridorConnection =
                AssetDatabase.LoadAssetAtPath<SceneConnectionSO>(link.ConnectionPath);
            if (corridorConnection == null)
                throw new InvalidOperationException($"Missing Corridor link: {link.ConnectionPath}");

            ConfigureBinding(
                bindings.GetArrayElementAtIndex(bindingIndex++),
                isSource ? link.SourceRoomId : link.DestinationRoomId,
                isSource ? link.SourceSlotId : link.DestinationSlotId,
                corridorConnection,
                isSource ? SceneConnectionEndpointSide.A : SceneConnectionEndpointSide.B);
        }
        serializedBuilder.ApplyModifiedPropertiesWithoutUndo();

        generator.EditorConfigureReentryPolicy(
            $"procedural_corridor_{routeSet.StableThemeId}",
            DungeonReentryPolicy.PreserveDuringRun);
        if (generator.ReentryPolicy != DungeonReentryPolicy.PreserveDuringRun)
        {
            throw new InvalidOperationException(
                $"Corridor '{routeSet.StableThemeId}' must preserve its layout and content during the active run.");
        }

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

        VerifyGeneratedCorridorEndpoints(
            builder,
            lobbyConnection,
            bossConnection,
            spec,
            corridorLinks);
        builder.ClearGeneratedContent();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static int CountCorridorLinkBindings(
        CorridorTravelSpec spec,
        IReadOnlyList<CorridorLinkSpec> corridorLinks)
    {
        int count = 0;
        for (int linkIndex = 0; corridorLinks != null && linkIndex < corridorLinks.Count; linkIndex++)
        {
            CorridorLinkSpec link = corridorLinks[linkIndex];
            if (string.Equals(link.Source.ThemeName, spec.ThemeName, StringComparison.Ordinal) ||
                string.Equals(link.Destination.ThemeName, spec.ThemeName, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static void ConfigureCorridorLinkScenes(
        IReadOnlyList<CorridorTravelSpec> specs,
        IReadOnlyList<CorridorLinkSpec> corridorLinks)
    {
        for (int specIndex = 0; specs != null && specIndex < specs.Count; specIndex++)
        {
            CorridorTravelSpec spec = specs[specIndex];
            CorridorBossRouteSetSO routeSet = LoadRequiredRouteSet(spec);
            SceneConnectionSO lobbyConnection = AssetDatabase.LoadAssetAtPath<SceneConnectionSO>(
                $"{ConnectionFolder}/Lobby_{SanitizeAssetName(routeSet.StableThemeId)}_Corridor.asset");
            SceneConnectionSO bossConnection = AssetDatabase.LoadAssetAtPath<SceneConnectionSO>(
                $"{ConnectionFolder}/Corridor_{SanitizeAssetName(routeSet.StableThemeId)}_Boss.asset");
            if (lobbyConnection == null || bossConnection == null)
            {
                throw new InvalidOperationException(
                    $"Missing base Corridor connection for theme '{routeSet.StableThemeId}'.");
            }

            ConfigureCorridorScene(
                spec,
                routeSet,
                lobbyConnection,
                bossConnection,
                corridorLinks);
        }
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
        SceneConnectionSO bossConnection,
        CorridorTravelSpec spec,
        IReadOnlyList<CorridorLinkSpec> corridorLinks)
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

        for (int linkIndex = 0; corridorLinks != null && linkIndex < corridorLinks.Count; linkIndex++)
        {
            CorridorLinkSpec link = corridorLinks[linkIndex];
            bool isSource = string.Equals(
                link.Source.ThemeName,
                spec.ThemeName,
                StringComparison.Ordinal);
            bool isDestination = string.Equals(
                link.Destination.ThemeName,
                spec.ThemeName,
                StringComparison.Ordinal);
            if (!isSource && !isDestination)
                continue;

            string slotId = isSource ? link.SourceSlotId : link.DestinationSlotId;
            SceneConnectionSO connection =
                AssetDatabase.LoadAssetAtPath<SceneConnectionSO>(link.ConnectionPath);
            SceneTravelEndpoint endpoint = FindGeneratedEndpoint(builder, slotId);
            SceneTravelInteractable interactable =
                endpoint != null ? endpoint.GetComponent<SceneTravelInteractable>() : null;
            SceneTravelTrigger2D trigger =
                endpoint != null ? endpoint.GetComponent<SceneTravelTrigger2D>() : null;
            bool mediumMatches = isSource
                ? interactable != null && interactable.enabled && (trigger == null || !trigger.enabled)
                : (interactable == null || !interactable.enabled) && (trigger == null || !trigger.enabled);
            if (endpoint == null ||
                endpoint.Connection != connection ||
                endpoint.ConnectionSide !=
                    (isSource ? SceneConnectionEndpointSide.A : SceneConnectionEndpointSide.B) ||
                !mediumMatches)
            {
                throw new InvalidOperationException(
                    $"Generated Corridor endpoint does not match one-way link slot '{slotId}'.");
            }
        }
    }

    private static SceneTravelEndpoint FindGeneratedEndpoint(
        DungeonRoomBuilder builder,
        string slotId)
    {
        IReadOnlyList<SceneTravelEndpoint> endpoints = builder.GeneratedTravelEndpoints;
        for (int endpointIndex = 0; endpointIndex < endpoints.Count; endpointIndex++)
        {
            SceneTravelEndpoint endpoint = endpoints[endpointIndex];
            if (endpoint != null && endpoint.ProceduralSlotId == slotId)
                return endpoint;
        }

        return null;
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

    private static void VerifyCorridorLinkConfiguration(
        CorridorLinkSpec link,
        CorridorBossRouteSetSO sourceRoute,
        CorridorBossRouteSetSO destinationRoute,
        SceneConnectionSO connection,
        RoomTemplateSO sourceRoom,
        RoomTemplateSO destinationRoom,
        SceneTravelPresentationProfileSO presentationProfile,
        GameObject corridorTravelPipePrefab)
    {
        RoomThemeLibrarySO sourceLibrary =
            AssetDatabase.LoadAssetAtPath<RoomThemeLibrarySO>(link.Source.LibraryPath);
        RoomThemeLibrarySO destinationLibrary =
            AssetDatabase.LoadAssetAtPath<RoomThemeLibrarySO>(link.Destination.LibraryPath);
        DungeonGenerationProfileSO sourceProfile =
            AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(
                link.Source.GenerationProfilePath);
        DungeonGenerationProfileSO destinationProfile =
            AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(
                link.Destination.GenerationProfilePath);
        if (connection == null ||
            sourceRoom == null ||
            destinationRoom == null ||
            sourceLibrary == null ||
            destinationLibrary == null ||
            sourceProfile == null ||
            destinationProfile == null ||
            presentationProfile == null ||
            corridorTravelPipePrefab == null ||
            connection.ConnectionId != link.ConnectionId ||
            connection.EndpointA.SceneName != sourceRoute.CorridorSceneName ||
            connection.EndpointB.SceneName != destinationRoute.CorridorSceneName ||
            !connection.AToB.Enabled ||
            connection.BToA.Enabled ||
            connection.AToB.RunAction != SceneTravelRunAction.None ||
            connection.AToB.PresentationProfile != presentationProfile ||
            presentationProfile.DepartureMode != SceneTravelDepartureMode.PullIntoEndpoint ||
            presentationProfile.ArrivalMode != SceneTravelArrivalMode.MoveFromOffset ||
            !presentationProfile.ArrivalPresentation.HasAnyContent ||
            !HasTravelSlot(
                sourceRoom,
                link.SourceSlotId,
                RoomTravelEndpointKind.Interaction,
                corridorTravelPipePrefab) ||
            !HasTravelSlot(
                destinationRoom,
                link.DestinationSlotId,
                RoomTravelEndpointKind.ArrivalOnly,
                corridorTravelPipePrefab) ||
            !sourceLibrary.ContainsRoom(sourceRoom) ||
            !destinationLibrary.ContainsRoom(destinationRoom) ||
            !ContainsGuaranteedRoom(sourceProfile, sourceRoom) ||
            !ContainsGuaranteedRoom(destinationProfile, destinationRoom))
        {
            throw new InvalidOperationException(
                $"One-way Corridor link verification failed: {link.ConnectionId}");
        }
    }

    private static bool ContainsGuaranteedRoom(
        DungeonGenerationProfileSO profile,
        RoomTemplateSO room)
    {
        IReadOnlyList<RoomTemplateSO> guaranteedRooms = profile.GuaranteedRoomTemplates;
        for (int roomIndex = 0; guaranteedRooms != null && roomIndex < guaranteedRooms.Count; roomIndex++)
        {
            if (guaranteedRooms[roomIndex] == room)
                return true;
        }

        return false;
    }

    private static void VerifyThemeConfiguration(
        CorridorTravelSpec spec,
        CorridorBossRouteSetSO routeSet,
        SceneConnectionSO lobbyConnection,
        SceneConnectionSO bossConnection,
        SceneTravelPresentationProfileSO lobbyToCorridorPresentationProfile,
        SceneTravelPresentationProfileSO defaultPresentationProfile)
    {
        RoomTemplateSO startRoom = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(spec.StartRoomPath);
        RoomTemplateSO bossRoom = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(spec.BossRoomPath);
        if (!HasTravelSlot(startRoom, LobbySlotId, RoomTravelEndpointKind.Trigger) ||
            !HasTravelSlot(bossRoom, BossSlotId, RoomTravelEndpointKind.Interaction) ||
            HasLegacyExitPortal(bossRoom) ||
            lobbyConnection.EndpointB.SceneName != routeSet.CorridorSceneName ||
            bossConnection.EndpointA.SceneName != routeSet.CorridorSceneName ||
            bossConnection.EndpointB.SceneName != routeSet.BossSceneName ||
            lobbyToCorridorPresentationProfile == null ||
            lobbyToCorridorPresentationProfile == defaultPresentationProfile ||
            lobbyToCorridorPresentationProfile.DepartureMode != SceneTravelDepartureMode.None ||
            lobbyToCorridorPresentationProfile.TransitionVisualMode !=
                SceneTransitionVisualMode.HorizontalWipeRightToLeft ||
            lobbyConnection.AToB.PresentationProfile != lobbyToCorridorPresentationProfile ||
            lobbyConnection.BToA.PresentationProfile != lobbyToCorridorPresentationProfile ||
            bossConnection.AToB.PresentationProfile != defaultPresentationProfile ||
            bossConnection.BToA.PresentationProfile != defaultPresentationProfile)
        {
            throw new InvalidOperationException(
                $"Travel content verification failed for theme '{routeSet.StableThemeId}'.");
        }
    }

    private static bool HasTravelSlot(
        RoomTemplateSO room,
        string slotId,
        RoomTravelEndpointKind kind,
        GameObject mediumPrefab = null)
    {
        IReadOnlyList<RoomTravelEndpointPlacementData> placements =
            room != null ? room.BuildData.travelEndpointPlacements : null;
        for (int i = 0; placements != null && i < placements.Count; i++)
        {
            if (placements[i].slotId == slotId &&
                placements[i].kind == kind &&
                (mediumPrefab == null || placements[i].mediumPrefab == mediumPrefab))
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
