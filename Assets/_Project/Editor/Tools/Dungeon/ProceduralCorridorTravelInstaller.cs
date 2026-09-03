using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임 : 세 일반 보스 테마 절차 복도의 로비·보스 이동 슬롯, 연결 에셋, 씬별 binding과 도착 endpoint를 재현 가능하게 설치하고 검증한다.
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
        GameObject travelPortalPrefab = CreateOrUpdateTravelPortalPrefab();

        CorridorTravelSpec[] specs = CreateSpecs();
        CorridorLinkSpec[] corridorLinks = CreateCorridorLinkSpecs(specs);
        UnregisterDeprecatedCorridorLinkRooms(corridorLinks);

        for (int i = 0; i < specs.Length; i++)
        {
            InstallTheme(
                specs[i],
                lobbyToCorridorPresentationProfile,
                defaultPresentationProfile,
                travelPortalPrefab);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log(
            $"Installed and verified data-driven Lobby/Boss travel for {specs.Length} procedural Corridors. " +
            $"Removed {corridorLinks.Length} deprecated one-way pipe link room registrations. " +
            "Lobby-to-Corridor and Corridor-to-Lobby travel share the authored right-to-left black wipe. " +
            "ProtoTypeHub placement is intentionally left for the lobby integration step.");
    }

    /// <summary>
    /// 책임:
    /// - 오픈 필드 전용이었던 복도 간 토관 방을 룸 라이브러리와 필수 방 목록에서 봉인한다.
    /// - 기존 방/연결/프리팹 에셋은 삭제하지 않아 향후 별도 기획에서 재검토할 수 있게 보존한다.
    /// </summary>
    [MenuItem("Tools/Dungeon/Remove Deprecated Corridor Pipe Travel")]
    public static void RemoveDeprecatedCorridorPipeTravel()
    {
        CorridorTravelSpec[] specs = CreateSpecs();
        CorridorLinkSpec[] corridorLinks = CreateCorridorLinkSpecs(specs);
        UnregisterDeprecatedCorridorLinkRooms(corridorLinks);
        ConfigureCorridorLinkScenes(specs, corridorLinks: null);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log(
            "Removed deprecated Corridor pipe travel from generation libraries, guaranteed rooms and scene bindings. " +
            "Dormant source assets were preserved.");
    }

    /// <summary>
    /// 책임 : 세 일반 보스의 처치 후 포탈만 데이터 기반 Boss→HUB 이동으로 전환하고 기존 순차 RouteManager 경로를 우회한다.
    /// </summary>
    [MenuItem("Tools/Dungeon/Migrate Normal Boss Exit Portals To HUB")]
    public static void InstallNormalBossHubReturns()
    {
        EnsureFolder(ConnectionFolder);
        SceneTravelPresentationProfileSO lobbyTravelProfile =
            CreateOrUpdateLobbyToCorridorPresentationProfile();
        CorridorTravelSpec[] specs = CreateSpecs();
        for (int i = 0; i < specs.Length; i++)
        {
            lobbyTravelProfile = AssetDatabase.LoadAssetAtPath<SceneTravelPresentationProfileSO>(
                LobbyToCorridorTravelProfilePath);
            if (lobbyTravelProfile == null)
            {
                throw new InvalidOperationException(
                    $"Missing lobby travel profile: {LobbyToCorridorTravelProfilePath}");
            }

            CorridorBossRouteSetSO routeSet = LoadRequiredRouteSet(specs[i]);
            SceneConnectionSO bossConnection =
                AssetDatabase.LoadAssetAtPath<SceneConnectionSO>(
                    $"{ConnectionFolder}/Corridor_{SanitizeAssetName(routeSet.StableThemeId)}_Boss.asset");
            if (bossConnection == null)
                throw new InvalidOperationException($"Missing Boss connection for '{routeSet.StableThemeId}'.");

            SceneConnectionSO hubReturnConnection =
                CreateOrUpdateBossHubConnection(routeSet, lobbyTravelProfile);
            ConfigureBossSceneTravel(
                routeSet,
                bossConnection,
                hubReturnConnection);

            // 씬 저장 중 AssetDatabase refresh가 새 에셋 인스턴스를 교체할 수 있으므로
            // 검증 직전에는 영속 경로에서 참조를 다시 얻는다.
            routeSet = LoadRequiredRouteSet(specs[i]);
            hubReturnConnection = AssetDatabase.LoadAssetAtPath<SceneConnectionSO>(
                $"{ConnectionFolder}/Boss_{SanitizeAssetName(routeSet.StableThemeId)}_Hub.asset");
            VerifyBossHubReturnConfiguration(routeSet, hubReturnConnection);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("Migrated Slime, Shadow and Dragon boss exits to data-driven HUB returns.");
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
        lobbyToCorridorPresentationProfile =
            AssetDatabase.LoadAssetAtPath<SceneTravelPresentationProfileSO>(
                LobbyToCorridorTravelProfilePath);
        if (lobbyToCorridorPresentationProfile == null)
        {
            throw new InvalidOperationException(
                $"Missing lobby travel profile: {LobbyToCorridorTravelProfilePath}");
        }

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
        SceneConnectionSO bossHubConnection = CreateOrUpdateBossHubConnection(
            routeSet,
            lobbyToCorridorPresentationProfile);

        ConfigureRoomTravelSlots(spec, travelPortalPrefab);
        ConfigureCorridorScene(spec, routeSet, lobbyConnection, bossConnection);
        ConfigureBossSceneTravel(routeSet, bossConnection, bossHubConnection);
        VerifyThemeConfiguration(
            spec,
            routeSet,
            lobbyConnection,
            bossConnection,
            bossHubConnection,
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
            $"Lobby.{themeId}.Corridor",
            routeContext: null);
        ConfigureEndpoint(
            serialized.FindProperty("endpointB"),
            routeSet.CorridorSceneName,
            $"Corridor.{themeId}.Lobby",
            routeSet);
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
            null,
            enabled: false);
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
            $"Corridor.{themeId}.Boss",
            routeSet);
        ConfigureEndpoint(
            serialized.FindProperty("endpointB"),
            routeSet.BossSceneName,
            $"Boss.{themeId}.Corridor",
            routeSet);
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

    /// <summary>
    /// 책임 : 일반 보스 씬의 처치 후 포탈을 같은 테마 HUB 게이트 도착점으로 보내는 단방향 연결 데이터를 만든다.
    /// </summary>
    private static SceneConnectionSO CreateOrUpdateBossHubConnection(
        CorridorBossRouteSetSO routeSet,
        SceneTravelPresentationProfileSO profile)
    {
        string themeId = routeSet.StableThemeId;
        string assetPath = $"{ConnectionFolder}/Boss_{SanitizeAssetName(themeId)}_Hub.asset";
        SceneConnectionSO connection = LoadOrCreateConnection(assetPath);
        SerializedObject serialized = new(connection);
        serialized.FindProperty("connectionId").stringValue = $"boss_hub_{themeId}";
        ConfigureEndpoint(
            serialized.FindProperty("endpointA"),
            routeSet.BossSceneName,
            $"Boss.{themeId}.Hub",
            routeSet);
        ConfigureEndpoint(
            serialized.FindProperty("endpointB"),
            LobbySceneName,
            $"Lobby.{themeId}.Corridor",
            routeContext: null);
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
            $"Corridor.{sourceRoute.StableThemeId}.To.{destinationRoute.StableThemeId}",
            sourceRoute);
        ConfigureEndpoint(
            serialized.FindProperty("endpointB"),
            destinationRoute.CorridorSceneName,
            $"Corridor.{destinationRoute.StableThemeId}.From.{sourceRoute.StableThemeId}",
            destinationRoute);
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
        string endpointId,
        SceneRouteContextSO routeContext)
    {
        endpoint.FindPropertyRelative("sceneName").stringValue = sceneName;
        endpoint.FindPropertyRelative("endpointId").stringValue = endpointId;
        endpoint.FindPropertyRelative("routeContext").objectReferenceValue = routeContext;
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

    /// <summary>
    /// 책임:
    /// - 모든 폐기된 토관 출발/도착 방을 생성 후보와 필수 포함 목록에서 제거한다.
    /// </summary>
    private static void UnregisterDeprecatedCorridorLinkRooms(
        IReadOnlyList<CorridorLinkSpec> corridorLinks)
    {
        for (int linkIndex = 0;
             corridorLinks != null && linkIndex < corridorLinks.Count;
             linkIndex++)
        {
            CorridorLinkSpec link = corridorLinks[linkIndex];
            UnregisterRoom(link.Source, link.SourceRoomPath);
            UnregisterRoom(link.Destination, link.DestinationRoomPath);
        }
    }

    /// <summary>
    /// 책임:
    /// - 특정 방 에셋의 보존 여부와 무관하게 해당 테마의 런 생성 등록만 해제한다.
    /// </summary>
    private static void UnregisterRoom(CorridorTravelSpec spec, string roomPath)
    {
        RoomTemplateSO room = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(roomPath);
        if (room == null)
            return;

        RoomThemeLibrarySO library =
            AssetDatabase.LoadAssetAtPath<RoomThemeLibrarySO>(spec.LibraryPath);
        DungeonGenerationProfileSO profile =
            AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(spec.GenerationProfilePath);
        if (library == null || profile == null)
        {
            throw new InvalidOperationException(
                $"Missing Corridor library or generation profile while unregistering '{roomPath}'.");
        }

        library.EditorRemoveRoom(room);
        var retainedGuaranteedRooms = new List<RoomTemplateSO>();
        IReadOnlyList<RoomTemplateSO> existingRooms = profile.GuaranteedRoomTemplates;
        for (int roomIndex = 0;
             existingRooms != null && roomIndex < existingRooms.Count;
             roomIndex++)
        {
            RoomTemplateSO existingRoom = existingRooms[roomIndex];
            if (existingRoom != null &&
                existingRoom != room &&
                !retainedGuaranteedRooms.Contains(existingRoom))
            {
                retainedGuaranteedRooms.Add(existingRoom);
            }
        }

        profile.EditorSetGuaranteedRooms(retainedGuaranteedRooms);
        EditorUtility.SetDirty(library);
        EditorUtility.SetDirty(profile);
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

    private static void ConfigureBossSceneTravel(
        CorridorBossRouteSetSO routeSet,
        SceneConnectionSO bossConnection,
        SceneConnectionSO hubReturnConnection)
    {
        string routeSetPath = AssetDatabase.GetAssetPath(routeSet);
        string bossConnectionPath = AssetDatabase.GetAssetPath(bossConnection);
        string hubReturnConnectionPath = AssetDatabase.GetAssetPath(hubReturnConnection);
        string scenePath = $"Assets/_Project/Scenes/{routeSet.BossSceneName}.unity";
        RequireSceneAsset(scenePath);
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // OpenScene이 유발하는 동기 refresh 뒤에는 전달받은 UnityEngine.Object가
        // 폐기된 인스턴스일 수 있으므로 영속 경로에서 다시 해석한다.
        routeSet = AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(routeSetPath);
        bossConnection = AssetDatabase.LoadAssetAtPath<SceneConnectionSO>(bossConnectionPath);
        hubReturnConnection = AssetDatabase.LoadAssetAtPath<SceneConnectionSO>(hubReturnConnectionPath);
        if (routeSet == null || bossConnection == null || hubReturnConnection == null)
        {
            throw new InvalidOperationException(
                $"Boss travel assets were invalidated while opening scene: {scenePath}");
        }

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

        BossEncounterEndDirector director = FindBossEncounterEndDirector(scene);
        if (director == null)
            throw new InvalidOperationException($"Boss scene has no BossEncounterEndDirector: {scenePath}");

        ConfigureBossExitPortal(director, hubReturnConnection);
        SerializedObject serializedDirector = new(director);
        serializedDirector.FindProperty("routeSet").objectReferenceValue = routeSet;
        serializedDirector.FindProperty("isFinalRouteSet").boolValue = false;
        serializedDirector.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(endpoint);
        EditorUtility.SetDirty(director);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>
    /// 책임 : 보스 종료 디렉터가 활성화하는 기존 포탈 외형을 보존하면서 이동 동작만 SceneConnection endpoint로 교체한다.
    /// </summary>
    private static void ConfigureBossExitPortal(
        BossEncounterEndDirector director,
        SceneConnectionSO hubReturnConnection)
    {
        SerializedObject serializedDirector = new(director);
        GameObject portalObject = serializedDirector
            .FindProperty("exitPortal")
            .objectReferenceValue as GameObject;
        if (portalObject == null)
            throw new InvalidOperationException($"Boss encounter '{director.name}' has no exit portal.");

        Transform promptAnchor = portalObject.transform;
        GameObject highlightTarget = null;
        string promptText = "HUB로 돌아가기";
        ScenePortal legacyPortal = portalObject.GetComponent<ScenePortal>();
        SceneTravelInteractable existingInteractable =
            portalObject.GetComponent<SceneTravelInteractable>();
        if (existingInteractable != null)
        {
            SerializedObject serializedExisting = new(existingInteractable);
            string existingPrompt = serializedExisting.FindProperty("interactPromptText")?.stringValue;
            if (!string.IsNullOrWhiteSpace(existingPrompt))
                promptText = existingPrompt;
            highlightTarget = serializedExisting
                .FindProperty("highlightTarget")?
                .objectReferenceValue as GameObject;
        }
        var cleanupTagSets = new List<UnityEngine.Object>();
        if (legacyPortal != null)
        {
            SerializedObject serializedLegacy = new(legacyPortal);
            promptAnchor = serializedLegacy.FindProperty("promptAnchor")?.objectReferenceValue as Transform
                           ?? portalObject.transform;
            highlightTarget = serializedLegacy.FindProperty("highlightTarget")?.objectReferenceValue as GameObject;
            string authoredPrompt = serializedLegacy.FindProperty("interactPromptText")?.stringValue;
            if (!string.IsNullOrWhiteSpace(authoredPrompt))
                promptText = authoredPrompt;
            SerializedProperty legacyCleanup = serializedLegacy.FindProperty("sceneTravelCleanupTagSets");
            for (int cleanupIndex = 0;
                 legacyCleanup != null && cleanupIndex < legacyCleanup.arraySize;
                 cleanupIndex++)
            {
                cleanupTagSets.Add(
                    legacyCleanup.GetArrayElementAtIndex(cleanupIndex).objectReferenceValue);
            }
            UnityEngine.Object.DestroyImmediate(legacyPortal);
        }

        SceneTravelEndpoint endpoint = portalObject.GetComponent<SceneTravelEndpoint>();
        if (endpoint == null)
            endpoint = portalObject.AddComponent<SceneTravelEndpoint>();
        endpoint.EditorConfigure(
            hubReturnConnection.EndpointA.EndpointId,
            string.Empty,
            hubReturnConnection,
            SceneConnectionEndpointSide.A);

        SerializedObject serializedEndpoint = new(endpoint);
        serializedEndpoint.FindProperty("departureAnchor").objectReferenceValue = promptAnchor;
        serializedEndpoint.FindProperty("arrivalAnchor").objectReferenceValue = portalObject.transform;
        SerializedProperty endpointCleanup = serializedEndpoint.FindProperty("sceneTravelCleanupTagSets");
        endpointCleanup.arraySize = cleanupTagSets.Count;
        for (int cleanupIndex = 0; cleanupIndex < cleanupTagSets.Count; cleanupIndex++)
        {
            endpointCleanup.GetArrayElementAtIndex(cleanupIndex).objectReferenceValue =
                cleanupTagSets[cleanupIndex];
        }
        serializedEndpoint.ApplyModifiedPropertiesWithoutUndo();

        SceneTravelInteractable interactable = existingInteractable;
        if (interactable == null)
            interactable = portalObject.AddComponent<SceneTravelInteractable>();
        SerializedObject serializedInteractable = new(interactable);
        serializedInteractable.FindProperty("endpoint").objectReferenceValue = endpoint;
        serializedInteractable.FindProperty("interactPromptText").stringValue = promptText;
        serializedInteractable.FindProperty("highlightTarget").objectReferenceValue = highlightTarget;
        serializedInteractable.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(endpoint);
        EditorUtility.SetDirty(interactable);
        EditorUtility.SetDirty(portalObject);
    }

    /// <summary>
    /// 책임 : 지정 씬에 존재하는 단일 보스 종료 디렉터를 찾아 반환한다.
    /// </summary>
    private static BossEncounterEndDirector FindBossEncounterEndDirector(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            BossEncounterEndDirector director =
                roots[rootIndex].GetComponentInChildren<BossEncounterEndDirector>(true);
            if (director != null)
                return director;
        }

        return null;
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
            connection.EndpointA.RouteContext != sourceRoute ||
            connection.EndpointB.RouteContext != destinationRoute ||
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
        SceneConnectionSO bossHubConnection,
        SceneTravelPresentationProfileSO lobbyToCorridorPresentationProfile,
        SceneTravelPresentationProfileSO defaultPresentationProfile)
    {
        RoomTemplateSO startRoom = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(spec.StartRoomPath);
        RoomTemplateSO bossRoom = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(spec.BossRoomPath);
        if (!HasTravelSlot(startRoom, LobbySlotId, RoomTravelEndpointKind.Trigger) ||
            !HasTravelSlot(bossRoom, BossSlotId, RoomTravelEndpointKind.Interaction) ||
            HasLegacyExitPortal(bossRoom) ||
            lobbyConnection.EndpointB.SceneName != routeSet.CorridorSceneName ||
            lobbyConnection.EndpointB.RouteContext != routeSet ||
            bossConnection.EndpointA.SceneName != routeSet.CorridorSceneName ||
            bossConnection.EndpointB.SceneName != routeSet.BossSceneName ||
            bossConnection.EndpointA.RouteContext != routeSet ||
            bossConnection.EndpointB.RouteContext != routeSet ||
            bossHubConnection == null ||
            bossHubConnection.EndpointA.SceneName != routeSet.BossSceneName ||
            bossHubConnection.EndpointB.SceneName != LobbySceneName ||
            !bossHubConnection.AToB.Enabled ||
            bossHubConnection.BToA.Enabled ||
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

        VerifyBossHubReturnConfiguration(routeSet, bossHubConnection);
    }

    /// <summary>
    /// 책임 : 일반 보스 출구가 레거시 ScenePortal 없이 단방향 Boss→HUB 연결을 사용하는지 씬과 연결 에셋을 함께 검증한다.
    /// </summary>
    private static void VerifyBossHubReturnConfiguration(
        CorridorBossRouteSetSO routeSet,
        SceneConnectionSO hubReturnConnection)
    {
        if (routeSet == null || hubReturnConnection == null)
        {
            throw new InvalidOperationException("Boss-to-HUB connection or route set is missing.");
        }

        SerializedObject serializedConnection = new(hubReturnConnection);
        serializedConnection.UpdateIfRequiredOrScript();
        bool aToBEnabled = serializedConnection
            .FindProperty("aToB")
            .FindPropertyRelative("enabled")
            .boolValue;
        bool bToAEnabled = serializedConnection
            .FindProperty("bToA")
            .FindPropertyRelative("enabled")
            .boolValue;
        int runAction = serializedConnection
            .FindProperty("aToB")
            .FindPropertyRelative("runAction")
            .enumValueIndex;
        UnityEngine.Object presentationProfile = serializedConnection
            .FindProperty("aToB")
            .FindPropertyRelative("presentationProfile")
            .objectReferenceValue;
        string destinationScene = serializedConnection
            .FindProperty("endpointB")
            .FindPropertyRelative("sceneName")
            .stringValue;
        if (!aToBEnabled ||
            bToAEnabled ||
            runAction != (int)SceneTravelRunAction.None ||
            presentationProfile == null ||
            !string.Equals(destinationScene, LobbySceneName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Boss-to-HUB connection contract is invalid: " +
                $"A→B={aToBEnabled}, B→A={bToAEnabled}, RunAction={runAction}, " +
                $"Profile={(presentationProfile != null ? presentationProfile.name : "None")}, " +
                $"Destination='{destinationScene}'.");
        }

        string scenePath = $"Assets/_Project/Scenes/{routeSet.BossSceneName}.unity";
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        try
        {
            BossEncounterEndDirector director = FindBossEncounterEndDirector(scene);
            SerializedObject serializedDirector = director != null ? new SerializedObject(director) : null;
            GameObject portalObject = serializedDirector != null
                ? serializedDirector.FindProperty("exitPortal").objectReferenceValue as GameObject
                : null;
            SceneTravelEndpoint endpoint =
                portalObject != null ? portalObject.GetComponent<SceneTravelEndpoint>() : null;
            if (director == null ||
                serializedDirector.FindProperty("routeSet").objectReferenceValue != routeSet ||
                serializedDirector.FindProperty("isFinalRouteSet").boolValue ||
                portalObject == null ||
                portalObject.GetComponent<ScenePortal>() != null ||
                portalObject.GetComponent<SceneTravelInteractable>() == null ||
                endpoint == null ||
                endpoint.Connection != hubReturnConnection ||
                endpoint.ConnectionSide != SceneConnectionEndpointSide.A)
            {
                throw new InvalidOperationException(
                    $"Boss scene HUB return migration failed for '{routeSet.StableThemeId}'.");
            }
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, removeScene: true);
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
