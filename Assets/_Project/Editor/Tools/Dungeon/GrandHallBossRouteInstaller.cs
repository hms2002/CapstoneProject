using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임 : Grand Hall 씬에 보스 처치 낙하 지점과 보스 복도 진입 포탈 placeholder를 멱등적으로 설치하고 검증한다.
/// </summary>
public static class GrandHallBossRouteInstaller
{
    private const string GrandHallSceneName = "Grand Hall";
    private const string GrandHallScenePath = "Assets/_Project/Scenes/Grand Hall.unity";
    private const string HubSceneName = "ProtoTypeHub";
    private const string HubScenePath = "Assets/_Project/Scenes/ProtoTypeHub.unity";
    private const string PortalPrefabPath = "Assets/_Project/Prefabs/Map/Portal/ScenePortal.prefab";
    private const string TravelPortalPrefabPath =
        "Assets/_Project/Prefabs/Map/Procedural/ProceduralSceneTravelPortal.prefab";
    private const string SourceRunRouteCatalogPath = "Assets/_Project/Data/SceneFlow/Routes/RunRouteCatalog.asset";
    private const string CatalogFolderPath = "Assets/_Project/Data/SceneFlow/Routes/GrandHall";
    private const string ConnectionFolderPath = "Assets/_Project/Data/SceneFlow/Connections";
    private const string TravelProfilePath =
        "Assets/_Project/Data/SceneFlow/TravelProfiles/LobbyToCorridorWipeTravel.asset";
    private const string HubGrandHallConnectionPath =
        ConnectionFolderPath + "/Hub_GrandHall.asset";
    private const string DataDrivenTravelRootName = "[DataDrivenSceneTravel]";
    private const string ObjectRootName = "[GrandHallBossRouteObjects]";
    private const string DropRootName = "BossDropSpawnPoints";
    private const string PortalRootName = "BossCorridorPortals";
    private const string DefaultHubEntryPointId = "Default";
    private const string HubGrandHallPortalObjectName = "Portal_To_GrandHall";
    private const string HubGrandHallConnectionId = "hub_grand_hall";
    private const string HubGrandHallEndpointId = "Hub.GrandHall";
    private const string GrandHallHubEndpointId = "GrandHall.Hub";
    private const string GrandHallHubArrivalObjectName = "Arrival_From_Hub";
    private const string GrandHallBossClearEndpointId = "GrandHall.BossClear";
    private const string GrandHallBossClearArrivalObjectName = "Arrival_From_BossClear";
    private const string DemonKingPortalBlockedMessage = "세 보스를 모두 처치해야 이동할 수 있습니다.";

    private static readonly BossDropSpec[] DropSpecs =
    {
        new(GrandHallBossDropPointId.Slime, "DropPoint_SlimeBoss", new Vector3(-2f, 1f, 0f)),
        new(GrandHallBossDropPointId.Dragon, "DropPoint_DragonBoss", new Vector3(0f, 1f, 0f)),
        new(GrandHallBossDropPointId.Shadow, "DropPoint_ShadowBoss", new Vector3(2f, 1f, 0f))
    };

    private static readonly BossPortalSpec[] PortalSpecs =
    {
        new(
            "Portal_To_SlimeCorridor",
            "grand_hall_to_slime_corridor",
            "슬라임 복도로 이동",
            "Assets/_Project/Data/SceneFlow/Routes/SlimeRouteSet.asset",
            $"{CatalogFolderPath}/GrandHall_SlimeRouteCatalog.asset",
            new Vector3(-3f, -1f, 0f)),
        new(
            "Portal_To_DragonCorridor",
            "grand_hall_to_dragon_corridor",
            "취룡 복도로 이동",
            "Assets/_Project/Data/SceneFlow/Routes/Dragon_CorridorBossRouteSet.asset",
            $"{CatalogFolderPath}/GrandHall_DragonRouteCatalog.asset",
            new Vector3(-1f, -1f, 0f)),
        new(
            "Portal_To_ShadowCorridor",
            "grand_hall_to_shadow_corridor",
            "그림자 복도로 이동",
            "Assets/_Project/Data/SceneFlow/Routes/ShadowCorridorBossRouteSet.asset",
            $"{CatalogFolderPath}/GrandHall_ShadowRouteCatalog.asset",
            new Vector3(1f, -1f, 0f)),
        new(
            "Portal_To_DemonkingCorridor",
            "grand_hall_to_demonking_corridor",
            "마왕 복도로 이동",
            "Assets/_Project/Data/SceneFlow/Routes/DemonkingRouteSet.asset",
            $"{CatalogFolderPath}/GrandHall_DemonkingRouteCatalog.asset",
            new Vector3(3f, -1f, 0f),
            new[]
            {
                "Assets/_Project/Data/SceneFlow/Routes/SlimeRouteSet.asset",
                "Assets/_Project/Data/SceneFlow/Routes/Dragon_CorridorBossRouteSet.asset",
                "Assets/_Project/Data/SceneFlow/Routes/ShadowCorridorBossRouteSet.asset"
            })
    };

    [MenuItem("Tools/Dungeon/Install Grand Hall Boss Route Objects")]
    public static void InstallFromMenu()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Install();
    }

    /// <summary>
    /// 책임 : Grand Hall 전용 단일 route catalog를 만들고 씬 안의 placement placeholder와 포탈 참조를 최신 상태로 맞춘다.
    /// </summary>
    public static void Install()
    {
        EnsureCatalogFolder();

        GameObject portalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PortalPrefabPath);
        if (portalPrefab == null)
            throw new InvalidOperationException($"ScenePortal prefab is missing: {PortalPrefabPath}");

        var catalogsByPortalId = new Dictionary<string, RunRouteCatalogSO>();
        for (int i = 0; i < PortalSpecs.Length; i++)
        {
            BossPortalSpec spec = PortalSpecs[i];
            catalogsByPortalId[spec.PortalId] = CreateOrUpdateSingleRouteCatalog(spec);
        }

        WithGrandHallScene(scene =>
        {
            ConfigureGrandHallBossClearArrival(scene);
            ConfigureGrandHallScene(scene, portalPrefab, catalogsByPortalId);
        });
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log(
            "Installed Grand Hall boss drop spawn points and boss-corridor portals. " +
            "Move the created child objects to their final authored positions in the Grand Hall scene.");
    }

    [MenuItem("Tools/Dungeon/Validate Grand Hall Boss Route Objects")]
    public static void ValidateFromMenu()
    {
        Validate();
    }

    [MenuItem("Tools/Dungeon/Install HUB Portal To Grand Hall")]
    public static void InstallHubPortalToGrandHallFromMenu()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        InstallHubPortalToGrandHall();
    }

    /// <summary>
    /// 책임 : ProtoTypeHub에 Grand Hall 진입 포탈을 설치하고 Grand Hall 쪽 도착 endpoint를 함께 보장한다.
    /// </summary>
    public static void InstallHubPortalToGrandHall()
    {
        EnsureFolder(ConnectionFolderPath);
        EnsureSceneInBuildSettings(HubScenePath);
        EnsureSceneInBuildSettings(GrandHallScenePath);

        SceneConnectionSO connection = CreateOrUpdateHubGrandHallConnection();
        GameObject travelPortalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TravelPortalPrefabPath);
        if (travelPortalPrefab == null)
            throw new InvalidOperationException($"Scene travel portal prefab is missing: {TravelPortalPrefabPath}");

        WithScene(HubScenePath, scene => ConfigureHubGrandHallPortal(scene, travelPortalPrefab, connection));
        WithScene(GrandHallScenePath, scene => ConfigureGrandHallHubArrival(scene, connection));
        AssetDatabase.SaveAssets();
        ValidateHubPortalToGrandHall();
        Debug.Log("Installed ProtoTypeHub portal to Grand Hall and Grand Hall arrival endpoint.");
    }

    [MenuItem("Tools/Dungeon/Validate HUB Portal To Grand Hall")]
    public static void ValidateHubPortalToGrandHallFromMenu()
    {
        ValidateHubPortalToGrandHall();
    }

    [MenuItem("Tools/Dungeon/Install Grand Hall Boss Clear Arrival")]
    public static void InstallBossClearArrivalFromMenu()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        InstallBossClearArrival();
    }

    /// <summary>
    /// 책임 : 보스 클리어 포탈이 Grand Hall에 진입할 때 사용할 공통 도착 전용 endpoint를 설치한다.
    /// </summary>
    public static void InstallBossClearArrival()
    {
        WithScene(GrandHallScenePath, ConfigureGrandHallBossClearArrival);
        AssetDatabase.SaveAssets();
        ValidateBossClearArrival();
        Debug.Log("Installed Grand Hall boss-clear arrival endpoint.");
    }

    [MenuItem("Tools/Dungeon/Validate Grand Hall Boss Clear Arrival")]
    public static void ValidateBossClearArrivalFromMenu()
    {
        ValidateBossClearArrival();
    }

    /// <summary>
    /// 책임 : Grand Hall 보스 클리어 공통 도착 endpoint가 연결 없이 도착 위치로 등록 가능한 상태인지 확인한다.
    /// </summary>
    public static void ValidateBossClearArrival()
    {
        WithScene(GrandHallScenePath, scene =>
        {
            ValidateBossClearArrivalInScene(scene);
        }, saveScene: false);

        Debug.Log("Verified Grand Hall boss-clear arrival endpoint.");
    }

    /// <summary>
    /// 책임 : Hub에서 Grand Hall로 향하는 단방향 연결과 양쪽 endpoint 배치가 런타임 대기 없이 해석 가능한지 확인한다.
    /// </summary>
    public static void ValidateHubPortalToGrandHall()
    {
        SceneConnectionSO connection =
            AssetDatabase.LoadAssetAtPath<SceneConnectionSO>(HubGrandHallConnectionPath);
        if (connection == null ||
            connection.ConnectionId != HubGrandHallConnectionId ||
            !IsSceneEnabledInBuildSettings(HubScenePath) ||
            !IsSceneEnabledInBuildSettings(GrandHallScenePath) ||
            connection.EndpointA.SceneName != HubSceneName ||
            connection.EndpointA.EndpointId != HubGrandHallEndpointId ||
            connection.EndpointB.SceneName != GrandHallSceneName ||
            connection.EndpointB.EndpointId != GrandHallHubEndpointId ||
            !connection.AToB.Enabled ||
            connection.AToB.RunAction != SceneTravelRunAction.None ||
            !connection.AToB.PreservePlayerRuntimeState ||
            connection.BToA.Enabled)
        {
            throw new InvalidOperationException("Hub -> Grand Hall SceneConnection is invalid.");
        }

        WithScene(HubScenePath, scene =>
        {
            SceneTravelEndpoint endpoint = FindEndpointInScene(scene, HubGrandHallEndpointId);
            SceneTravelInteractable interactable =
                endpoint != null ? endpoint.GetComponent<SceneTravelInteractable>() : null;
            if (endpoint == null ||
                endpoint.Connection != connection ||
                endpoint.ConnectionSide != SceneConnectionEndpointSide.A ||
                interactable == null ||
                !interactable.enabled)
            {
                throw new InvalidOperationException("ProtoTypeHub Grand Hall portal endpoint is invalid.");
            }
        }, saveScene: false);

        WithScene(GrandHallScenePath, scene =>
        {
            SceneTravelEndpoint endpoint = FindEndpointInScene(scene, GrandHallHubEndpointId);
            if (endpoint == null ||
                endpoint.Connection != connection ||
                endpoint.ConnectionSide != SceneConnectionEndpointSide.B)
            {
                throw new InvalidOperationException("Grand Hall arrival endpoint for Hub portal is invalid.");
            }
        }, saveScene: false);

        Debug.Log("Verified ProtoTypeHub portal to Grand Hall.");
    }

    /// <summary>
    /// 책임 : Grand Hall placeholder 오브젝트와 포탈별 단일 route catalog 연결이 누락 없이 유지되는지 확인한다.
    /// </summary>
    public static void Validate()
    {
        RunRouteCatalogSO sourceCatalog =
            AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(SourceRunRouteCatalogPath);
        if (sourceCatalog == null)
            throw new InvalidOperationException($"Source RunRouteCatalog is missing: {SourceRunRouteCatalogPath}");

        for (int i = 0; i < PortalSpecs.Length; i++)
        {
            BossPortalSpec spec = PortalSpecs[i];
            RunRouteCatalogSO catalog = AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(spec.CatalogPath);
            CorridorBossRouteSetSO routeSet =
                AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(spec.RouteSetPath);

            if (catalog == null || routeSet == null)
                throw new InvalidOperationException($"Grand Hall route catalog or route set is missing. portal={spec.ObjectName}");

            if (catalog.NormalStageCount != 0 ||
                catalog.NormalRouteSets.Count != 0 ||
                catalog.FinalRouteSet != routeSet ||
                catalog.RunCommonLoadManifest != sourceCatalog.RunCommonLoadManifest ||
                !string.Equals(catalog.HubSceneName, GrandHallSceneName, StringComparison.Ordinal) ||
                !string.Equals(catalog.HubEntryPointId, GrandHallBossClearEndpointId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Grand Hall portal catalog is not a single-route catalog or has stale hub settings. catalog={catalog.name}");
            }
        }

        WithGrandHallScene(scene =>
        {
            Transform root = FindRoot(scene, ObjectRootName);
            if (root == null)
                throw new InvalidOperationException($"Grand Hall object root is missing: {ObjectRootName}");

            Transform dropRoot = root.Find(DropRootName);
            Transform portalRoot = root.Find(PortalRootName);
            if (dropRoot == null || portalRoot == null)
                throw new InvalidOperationException("Grand Hall drop or portal root is missing.");

            for (int i = 0; i < DropSpecs.Length; i++)
            {
                BossDropSpec spec = DropSpecs[i];
                Transform dropTransform = dropRoot.Find(spec.ObjectName);
                GrandHallBossDropSpawnPoint dropPoint =
                    dropTransform != null
                        ? dropTransform.GetComponent<GrandHallBossDropSpawnPoint>()
                        : null;
                if (dropPoint == null || dropPoint.BossId != spec.BossId)
                    throw new InvalidOperationException($"Grand Hall boss drop spawn point is invalid: {spec.ObjectName}");
            }

            for (int i = 0; i < PortalSpecs.Length; i++)
            {
                BossPortalSpec spec = PortalSpecs[i];
                Transform portalTransform = portalRoot.Find(spec.ObjectName);
                ScenePortal portal =
                    portalTransform != null
                        ? portalTransform.GetComponentInChildren<ScenePortal>(includeInactive: true)
                        : null;
                RunRouteCatalogSO catalog = AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(spec.CatalogPath);

                if (portal == null ||
                    portal.PortalTransitionType != TransitionType.HubToRunStart ||
                    portal.StartRunRouteCatalog != catalog)
                {
                    throw new InvalidOperationException($"Grand Hall boss corridor portal is invalid: {spec.ObjectName}");
                }

                ValidatePortalAccessGate(spec, portal);
            }

            ValidateBossClearArrivalInScene(scene);
        }, saveScene: false);

        Debug.Log("Verified Grand Hall boss drop spawn points and boss-corridor portal route catalogs.");
    }

    /// <summary>
    /// 책임 : 보스 처치 낙하 spawn point 하나의 설치 계약을 보관한다.
    /// </summary>
    private readonly struct BossDropSpec
    {
        public readonly GrandHallBossDropPointId BossId;
        public readonly string ObjectName;
        public readonly Vector3 DefaultLocalPosition;

        public BossDropSpec(
            GrandHallBossDropPointId bossId,
            string objectName,
            Vector3 defaultLocalPosition)
        {
            BossId = bossId;
            ObjectName = objectName;
            DefaultLocalPosition = defaultLocalPosition;
        }
    }

    /// <summary>
    /// 책임 : Grand Hall에서 특정 보스 복도로 진입하는 포탈과 단일 route catalog 생성 계약을 보관한다.
    /// </summary>
    private readonly struct BossPortalSpec
    {
        public readonly string ObjectName;
        public readonly string PortalId;
        public readonly string PromptText;
        public readonly string RouteSetPath;
        public readonly string CatalogPath;
        public readonly Vector3 DefaultLocalPosition;
        public readonly string[] RequiredBossRouteSetPaths;

        public BossPortalSpec(
            string objectName,
            string portalId,
            string promptText,
            string routeSetPath,
            string catalogPath,
            Vector3 defaultLocalPosition,
            string[] requiredBossRouteSetPaths = null)
        {
            ObjectName = objectName;
            PortalId = portalId;
            PromptText = promptText;
            RouteSetPath = routeSetPath;
            CatalogPath = catalogPath;
            DefaultLocalPosition = defaultLocalPosition;
            RequiredBossRouteSetPaths = requiredBossRouteSetPaths ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// 책임 : 공용 런 카탈로그의 로딩/BGM 설정을 재사용하면서 보스 복도 하나만 시작하는 Grand Hall 전용 카탈로그를 만든다.
    /// </summary>
    private static RunRouteCatalogSO CreateOrUpdateSingleRouteCatalog(BossPortalSpec spec)
    {
        RunRouteCatalogSO sourceCatalog =
            AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(SourceRunRouteCatalogPath);
        CorridorBossRouteSetSO routeSet =
            AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(spec.RouteSetPath);
        if (sourceCatalog == null || routeSet == null || !routeSet.IsValid)
        {
            throw new InvalidOperationException(
                $"Source catalog or route set is missing/invalid. catalog={SourceRunRouteCatalogPath}, routeSet={spec.RouteSetPath}");
        }

        UnityEngine.Object existingAsset = AssetDatabase.LoadMainAssetAtPath(spec.CatalogPath);
        if (existingAsset != null && existingAsset is not RunRouteCatalogSO)
            throw new InvalidOperationException($"Asset at catalog path is not RunRouteCatalogSO: {spec.CatalogPath}");

        RunRouteCatalogSO catalog = existingAsset as RunRouteCatalogSO;
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<RunRouteCatalogSO>();
            AssetDatabase.CreateAsset(catalog, spec.CatalogPath);
        }

        SerializedObject source = new(sourceCatalog);
        SerializedObject target = new(catalog);
        target.FindProperty("normalStageCount").intValue = 0;
        target.FindProperty("normalRouteSets").ClearArray();
        target.FindProperty("useFixedNormalRouteOrder").boolValue = false;
        target.FindProperty("allowDuplicateNormalRoutes").boolValue = false;
        target.FindProperty("finalRouteSet").objectReferenceValue = routeSet;
        target.CopyFromSerializedProperty(source.FindProperty("hubBgm"));
        target.FindProperty("runCommonLoadManifest").objectReferenceValue =
            sourceCatalog.RunCommonLoadManifest;
        target.FindProperty("hubSceneName").stringValue = GrandHallSceneName;
        target.FindProperty("hubEntryPointId").stringValue = GrandHallBossClearEndpointId;
        target.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    /// <summary>
    /// 책임 : Hub에서 Grand Hall로 이동하는 데이터 기반 단방향 SceneConnection 에셋을 생성하거나 갱신한다.
    /// </summary>
    private static SceneConnectionSO CreateOrUpdateHubGrandHallConnection()
    {
        SceneConnectionSO connection = LoadOrCreateConnection(HubGrandHallConnectionPath);
        SceneTravelPresentationProfileSO presentationProfile =
            AssetDatabase.LoadAssetAtPath<SceneTravelPresentationProfileSO>(TravelProfilePath);
        if (presentationProfile == null)
            Debug.LogWarning($"Grand Hall portal travel profile is missing; travel will use default loading presentation. path={TravelProfilePath}");

        SerializedObject serialized = new(connection);
        serialized.FindProperty("connectionId").stringValue = HubGrandHallConnectionId;
        ConfigureEndpoint(
            serialized.FindProperty("endpointA"),
            HubSceneName,
            HubGrandHallEndpointId,
            routeContext: null);
        ConfigureEndpoint(
            serialized.FindProperty("endpointB"),
            GrandHallSceneName,
            GrandHallHubEndpointId,
            routeContext: null);
        ConfigureDirection(
            serialized.FindProperty("aToB"),
            SceneTravelRunAction.None,
            presentationProfile,
            enabled: true);
        ConfigureDirection(
            serialized.FindProperty("bToA"),
            SceneTravelRunAction.None,
            profile: null,
            enabled: false);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(connection);
        AssetDatabase.SaveAssetIfDirty(connection);
        return connection;
    }

    /// <summary>
    /// 책임 : ProtoTypeHub 씬에 Grand Hall로 이동하는 상호작용 포탈을 만들고 기존 위치 배치는 보존한다.
    /// </summary>
    private static void ConfigureHubGrandHallPortal(
        Scene scene,
        GameObject travelPortalPrefab,
        SceneConnectionSO connection)
    {
        Transform root = FindOrCreateRoot(scene, DataDrivenTravelRootName);
        Transform existing = root.Find(HubGrandHallPortalObjectName);
        GameObject portalObject;
        bool created = existing == null;
        if (created)
        {
            portalObject = PrefabUtility.InstantiatePrefab(travelPortalPrefab, root) as GameObject;
            if (portalObject == null)
                throw new InvalidOperationException($"Failed to instantiate travel portal prefab: {TravelPortalPrefabPath}");

            portalObject.name = HubGrandHallPortalObjectName;
            portalObject.transform.localPosition = new Vector3(0f, 2f, 0f);
            portalObject.transform.localRotation = Quaternion.identity;
            portalObject.transform.localScale = Vector3.one;
        }
        else
        {
            portalObject = existing.gameObject;
        }

        SceneTravelEndpoint endpoint =
            portalObject.GetComponentInChildren<SceneTravelEndpoint>(includeInactive: true) ??
            portalObject.AddComponent<SceneTravelEndpoint>();
        endpoint.EditorConfigure(
            connection.EndpointA.EndpointId,
            string.Empty,
            connection,
            SceneConnectionEndpointSide.A);

        Transform travelAnchor = FindChildRecursive(portalObject.transform, "TravelAnchor") ??
                                 portalObject.transform;
        SerializedObject serializedEndpoint = new(endpoint);
        serializedEndpoint.FindProperty("departureAnchor").objectReferenceValue = travelAnchor;
        serializedEndpoint.FindProperty("arrivalAnchor").objectReferenceValue = travelAnchor;
        serializedEndpoint.ApplyModifiedPropertiesWithoutUndo();

        SceneTravelInteractable interactable =
            endpoint.GetComponent<SceneTravelInteractable>() ??
            portalObject.GetComponent<SceneTravelInteractable>() ??
            endpoint.gameObject.AddComponent<SceneTravelInteractable>();
        SerializedObject serializedInteractable = new(interactable);
        serializedInteractable.FindProperty("endpoint").objectReferenceValue = endpoint;
        serializedInteractable.FindProperty("interactPromptText").stringValue = "Grand Hall로 이동";
        serializedInteractable.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(endpoint);
        EditorUtility.SetDirty(interactable);
        EditorUtility.SetDirty(portalObject);
    }

    /// <summary>
    /// 책임 : Grand Hall 씬에 Hub 포탈의 도착 endpoint를 만들고 기존 배치는 보존한다.
    /// </summary>
    private static void ConfigureGrandHallHubArrival(Scene scene, SceneConnectionSO connection)
    {
        Transform root = FindOrCreateRoot(scene, DataDrivenTravelRootName);
        Transform existing = root.Find(GrandHallHubArrivalObjectName);
        GameObject arrivalObject;
        bool created = existing == null;
        if (created)
        {
            arrivalObject = new GameObject(GrandHallHubArrivalObjectName);
            arrivalObject.transform.SetParent(root, worldPositionStays: false);
            Transform spawnPoint = ResolvePlayerSpawnPoint(scene, DefaultHubEntryPointId);
            if (spawnPoint != null)
            {
                arrivalObject.transform.SetPositionAndRotation(
                    spawnPoint.position,
                    spawnPoint.rotation);
            }
            else
            {
                arrivalObject.transform.localPosition = Vector3.zero;
                arrivalObject.transform.localRotation = Quaternion.identity;
            }
            arrivalObject.transform.localScale = Vector3.one;
        }
        else
        {
            arrivalObject = existing.gameObject;
        }

        SceneTravelEndpoint endpoint =
            arrivalObject.GetComponent<SceneTravelEndpoint>() ??
            arrivalObject.AddComponent<SceneTravelEndpoint>();
        endpoint.EditorConfigure(
            connection.EndpointB.EndpointId,
            string.Empty,
            connection,
            SceneConnectionEndpointSide.B);

        SerializedObject serializedEndpoint = new(endpoint);
        serializedEndpoint.FindProperty("departureAnchor").objectReferenceValue = arrivalObject.transform;
        serializedEndpoint.FindProperty("arrivalAnchor").objectReferenceValue = arrivalObject.transform;
        serializedEndpoint.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(endpoint);
        EditorUtility.SetDirty(arrivalObject);
    }

    /// <summary>
    /// 책임 : Grand Hall 씬에 여러 보스 클리어 이동이 공유할 도착 전용 endpoint를 만들고 기존 배치는 보존한다.
    /// </summary>
    private static void ConfigureGrandHallBossClearArrival(Scene scene)
    {
        Transform root = FindOrCreateRoot(scene, DataDrivenTravelRootName);
        Transform existing = root.Find(GrandHallBossClearArrivalObjectName);
        GameObject arrivalObject;
        bool created = existing == null;
        if (created)
        {
            arrivalObject = new GameObject(GrandHallBossClearArrivalObjectName);
            arrivalObject.transform.SetParent(root, worldPositionStays: false);

            Transform hubArrival = FindChildRecursive(root, GrandHallHubArrivalObjectName);
            if (hubArrival != null)
            {
                arrivalObject.transform.SetPositionAndRotation(
                    hubArrival.position + Vector3.right * 1.5f,
                    hubArrival.rotation);
            }
            else
            {
                Transform spawnPoint = ResolvePlayerSpawnPoint(scene, DefaultHubEntryPointId);
                if (spawnPoint != null)
                {
                    arrivalObject.transform.SetPositionAndRotation(
                        spawnPoint.position + Vector3.right * 1.5f,
                        spawnPoint.rotation);
                }
                else
                {
                    arrivalObject.transform.localPosition = Vector3.right * 1.5f;
                    arrivalObject.transform.localRotation = Quaternion.identity;
                }
            }

            arrivalObject.transform.localScale = Vector3.one;
        }
        else
        {
            arrivalObject = existing.gameObject;
        }

        SceneTravelEndpoint endpoint =
            arrivalObject.GetComponent<SceneTravelEndpoint>() ??
            arrivalObject.AddComponent<SceneTravelEndpoint>();
        endpoint.EditorConfigureArrivalOnly(GrandHallBossClearEndpointId);

        PlayerSpawnPoint bossClearPlayerSpawnPoint =
            arrivalObject.GetComponent<PlayerSpawnPoint>() ??
            arrivalObject.AddComponent<PlayerSpawnPoint>();
        bossClearPlayerSpawnPoint.pointId = GrandHallBossClearEndpointId;
        bossClearPlayerSpawnPoint.isDefault = false;
        bossClearPlayerSpawnPoint.runtimePolicy = PlayerSpawnRuntimePolicy.RestorePendingState;

        SerializedObject serializedEndpoint = new(endpoint);
        serializedEndpoint.FindProperty("departureAnchor").objectReferenceValue = arrivalObject.transform;
        serializedEndpoint.FindProperty("arrivalAnchor").objectReferenceValue = arrivalObject.transform;
        serializedEndpoint.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(endpoint);
        EditorUtility.SetDirty(bossClearPlayerSpawnPoint);
        EditorUtility.SetDirty(arrivalObject);
    }

    /// <summary>
    /// 책임 : 보스 클리어 후 Grand Hall에 도착할 endpoint와 PlayerSpawnPoint가 같은 위치 계약을 공유하는지 확인한다.
    /// </summary>
    private static void ValidateBossClearArrivalInScene(Scene scene)
    {
        SceneTravelEndpoint endpoint = FindEndpointInScene(scene, GrandHallBossClearEndpointId);
        PlayerSpawnPoint spawnPoint =
            endpoint != null ? endpoint.GetComponent<PlayerSpawnPoint>() : null;
        if (endpoint == null ||
            endpoint.Connection != null ||
            !endpoint.RegisterAsArrivalOnly ||
            spawnPoint == null ||
            spawnPoint.pointId != GrandHallBossClearEndpointId)
        {
            throw new InvalidOperationException("Grand Hall boss-clear arrival endpoint is invalid.");
        }
    }

    /// <summary>
    /// 책임 : Grand Hall 씬 안에 위치 조정용 루트, 보스 낙하 지점, 보스 복도 포탈을 생성하거나 기존 연결만 갱신한다.
    /// </summary>
    private static void ConfigureGrandHallScene(
        Scene scene,
        GameObject portalPrefab,
        IReadOnlyDictionary<string, RunRouteCatalogSO> catalogsByPortalId)
    {
        Transform root = FindOrCreateRoot(scene, ObjectRootName);
        Transform dropRoot = FindOrCreateChild(root, DropRootName);
        Transform portalRoot = FindOrCreateChild(root, PortalRootName);

        for (int i = 0; i < DropSpecs.Length; i++)
            CreateOrUpdateDropPoint(dropRoot, DropSpecs[i]);

        for (int i = 0; i < PortalSpecs.Length; i++)
        {
            BossPortalSpec spec = PortalSpecs[i];
            if (!catalogsByPortalId.TryGetValue(spec.PortalId, out RunRouteCatalogSO catalog) || catalog == null)
                throw new InvalidOperationException($"Grand Hall route catalog is missing for portal: {spec.ObjectName}");

            CreateOrUpdatePortal(portalRoot, portalPrefab, spec, catalog);
        }

        EditorUtility.SetDirty(root.gameObject);
    }

    /// <summary>
    /// 책임 : 보스 낙하 지점 marker 오브젝트를 만들고 사용자가 옮긴 Transform 배치는 보존한다.
    /// </summary>
    private static void CreateOrUpdateDropPoint(Transform parent, BossDropSpec spec)
    {
        bool created = false;
        Transform existing = parent.Find(spec.ObjectName);
        GameObject dropObject;
        if (existing == null)
        {
            dropObject = new GameObject(spec.ObjectName);
            dropObject.transform.SetParent(parent, worldPositionStays: false);
            dropObject.transform.localPosition = spec.DefaultLocalPosition;
            created = true;
        }
        else
        {
            dropObject = existing.gameObject;
        }

        GrandHallBossDropSpawnPoint dropPoint =
            dropObject.GetComponent<GrandHallBossDropSpawnPoint>() ??
            dropObject.AddComponent<GrandHallBossDropSpawnPoint>();
        dropPoint.EditorConfigure(spec.BossId);

        EditorUtility.SetDirty(dropPoint);
        if (created)
            EditorUtility.SetDirty(dropObject);
    }

    /// <summary>
    /// 책임 : 보스 복도 진입 포탈 prefab instance를 만들고 route catalog/transition 계약만 갱신해 배치와 외형 설정은 보존한다.
    /// </summary>
    private static void CreateOrUpdatePortal(
        Transform parent,
        GameObject portalPrefab,
        BossPortalSpec spec,
        RunRouteCatalogSO catalog)
    {
        bool created = false;
        Transform existing = parent.Find(spec.ObjectName);
        GameObject portalObject;
        if (existing == null)
        {
            portalObject = PrefabUtility.InstantiatePrefab(portalPrefab, parent) as GameObject;
            if (portalObject == null)
                throw new InvalidOperationException($"Failed to instantiate ScenePortal prefab: {PortalPrefabPath}");

            portalObject.name = spec.ObjectName;
            portalObject.transform.localPosition = spec.DefaultLocalPosition;
            portalObject.transform.localRotation = Quaternion.identity;
            portalObject.transform.localScale = Vector3.one;
            created = true;
        }
        else
        {
            portalObject = existing.gameObject;
        }

        ScenePortal portal = portalObject.GetComponentInChildren<ScenePortal>(includeInactive: true);
        if (portal == null)
            throw new InvalidOperationException($"ScenePortal component is missing from Grand Hall portal object: {spec.ObjectName}");

        SerializedObject serializedPortal = new(portal);
        serializedPortal.FindProperty("portalId").stringValue = spec.PortalId;
        serializedPortal.FindProperty("transitionType").enumValueIndex = (int)TransitionType.HubToRunStart;
        serializedPortal.FindProperty("startRunRouteCatalog").objectReferenceValue = catalog;
        serializedPortal.FindProperty("interactPromptText").stringValue = spec.PromptText;
        serializedPortal.ApplyModifiedPropertiesWithoutUndo();

        ConfigurePortalAccessGate(portal, spec);

        EditorUtility.SetDirty(portal);
        if (created)
            EditorUtility.SetDirty(portalObject);
    }

    /// <summary>
    /// 책임 : 마왕 복도 포탈처럼 추가 진행도 조건이 필요한 포탈에 access rule 컴포넌트를 설치하거나 제거한다.
    /// </summary>
    private static void ConfigurePortalAccessGate(ScenePortal portal, BossPortalSpec spec)
    {
        if (portal == null)
            return;

        RequiredBossClearScenePortalAccessRule gate =
            portal.GetComponent<RequiredBossClearScenePortalAccessRule>();

        if (spec.RequiredBossRouteSetPaths == null || spec.RequiredBossRouteSetPaths.Length == 0)
        {
            if (gate != null)
                UnityEngine.Object.DestroyImmediate(gate);
            return;
        }

        gate ??= portal.gameObject.AddComponent<RequiredBossClearScenePortalAccessRule>();

        SerializedObject serializedGate = new(gate);
        SerializedProperty requiredRouteSets = serializedGate.FindProperty("requiredBossRouteSets");
        requiredRouteSets.ClearArray();
        for (int i = 0; i < spec.RequiredBossRouteSetPaths.Length; i++)
        {
            CorridorBossRouteSetSO routeSet =
                AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(spec.RequiredBossRouteSetPaths[i]);
            if (routeSet == null)
                throw new InvalidOperationException($"Required boss route set is missing: {spec.RequiredBossRouteSetPaths[i]}");

            requiredRouteSets.InsertArrayElementAtIndex(requiredRouteSets.arraySize);
            requiredRouteSets.GetArrayElementAtIndex(requiredRouteSets.arraySize - 1).objectReferenceValue = routeSet;
        }

        serializedGate.FindProperty("requiredBossThemeIds").ClearArray();
        serializedGate.FindProperty("blockedMessage").stringValue = DemonKingPortalBlockedMessage;
        serializedGate.FindProperty("showDefaultPopup").boolValue = true;
        serializedGate.FindProperty("popupDuration").floatValue = 1.5f;
        serializedGate.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gate);
    }

    /// <summary>
    /// 책임 : 포탈 access rule 설정이 포탈 설치 계약과 일치하는지 검증한다.
    /// </summary>
    private static void ValidatePortalAccessGate(BossPortalSpec spec, ScenePortal portal)
    {
        RequiredBossClearScenePortalAccessRule gate =
            portal != null ? portal.GetComponent<RequiredBossClearScenePortalAccessRule>() : null;
        bool shouldHaveGate = spec.RequiredBossRouteSetPaths != null && spec.RequiredBossRouteSetPaths.Length > 0;

        if (!shouldHaveGate)
        {
            if (gate != null)
                throw new InvalidOperationException($"Unexpected access gate is attached to portal: {spec.ObjectName}");
            return;
        }

        if (gate == null)
            throw new InvalidOperationException($"Required access gate is missing from portal: {spec.ObjectName}");

        if (gate.RequiredBossRouteSets.Count != spec.RequiredBossRouteSetPaths.Length)
            throw new InvalidOperationException($"Access gate requirement count is invalid: {spec.ObjectName}");

        for (int i = 0; i < spec.RequiredBossRouteSetPaths.Length; i++)
        {
            CorridorBossRouteSetSO expected =
                AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(spec.RequiredBossRouteSetPaths[i]);
            if (expected == null || gate.RequiredBossRouteSets[i] != expected)
                throw new InvalidOperationException($"Access gate requirement is invalid: {spec.ObjectName}");
        }
    }

    /// <summary>
    /// 책임 : Grand Hall 씬의 기존 로드 상태를 보존하면서 필요한 편집 작업을 수행하고 새로 연 씬만 닫는다.
    /// </summary>
    private static void WithGrandHallScene(Action<Scene> action, bool saveScene = true)
    {
        WithScene(GrandHallScenePath, action, saveScene);
    }

    /// <summary>
    /// 책임 : 씬 root 계층에서 지정된 이름의 Transform을 찾는다.
    /// </summary>
    private static Transform FindRoot(Scene scene, string rootName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject candidate = roots[i];
            if (candidate != null && string.Equals(candidate.name, rootName, StringComparison.Ordinal))
                return candidate.transform;
        }

        return null;
    }

    /// <summary>
    /// 책임 : 씬 root 계층에 지정된 이름의 Transform이 없으면 새로 만들고 Grand Hall 씬으로 소유권을 옮긴다.
    /// </summary>
    private static Transform FindOrCreateRoot(Scene scene, string rootName)
    {
        Transform root = FindRoot(scene, rootName);
        if (root != null)
            return root;

        GameObject rootObject = new(rootName);
        SceneManager.MoveGameObjectToScene(rootObject, scene);
        rootObject.transform.position = Vector3.zero;
        EditorUtility.SetDirty(rootObject);
        return rootObject.transform;
    }

    /// <summary>
    /// 책임 : parent 바로 아래에 지정된 이름의 정리용 child Transform을 찾거나 생성한다.
    /// </summary>
    private static Transform FindOrCreateChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            return child;

        GameObject childObject = new(childName);
        childObject.transform.SetParent(parent, worldPositionStays: false);
        childObject.transform.localPosition = Vector3.zero;
        childObject.transform.localRotation = Quaternion.identity;
        childObject.transform.localScale = Vector3.one;
        EditorUtility.SetDirty(childObject);
        return childObject.transform;
    }

    /// <summary>
    /// 책임 : Grand Hall 포탈 catalog asset을 담을 전용 폴더 경로를 보장한다.
    /// </summary>
    private static void EnsureCatalogFolder()
    {
        EnsureFolder(CatalogFolderPath);
    }

    /// <summary>
    /// 책임 : SceneConnection 에셋을 로드하거나 지정 경로에 새로 생성한다.
    /// </summary>
    private static SceneConnectionSO LoadOrCreateConnection(string assetPath)
    {
        UnityEngine.Object existingAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (existingAsset != null && existingAsset is not SceneConnectionSO)
            throw new InvalidOperationException($"Asset at connection path is not SceneConnectionSO: {assetPath}");

        SceneConnectionSO connection = existingAsset as SceneConnectionSO;
        if (connection != null)
            return connection;

        connection = ScriptableObject.CreateInstance<SceneConnectionSO>();
        AssetDatabase.CreateAsset(connection, assetPath);
        return connection;
    }

    /// <summary>
    /// 책임 : SerializedObject의 endpoint 구조체 필드를 씬 이름, endpoint Id, 선택 route context로 채운다.
    /// </summary>
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

    /// <summary>
    /// 책임 : SerializedObject의 방향 구조체 필드를 고정 씬 이동에 필요한 최소 정책으로 채운다.
    /// </summary>
    private static void ConfigureDirection(
        SerializedProperty direction,
        SceneTravelRunAction runAction,
        SceneTravelPresentationProfileSO profile,
        bool enabled)
    {
        direction.FindPropertyRelative("enabled").boolValue = enabled;
        direction.FindPropertyRelative("runAction").enumValueIndex = (int)runAction;
        direction.FindPropertyRelative("runEndReason").enumValueIndex = (int)RunEndReason.None;
        direction.FindPropertyRelative("preservePlayerRuntimeState").boolValue = true;
        direction.FindPropertyRelative("fullyHealPlayer").boolValue = false;
        direction.FindPropertyRelative("resetCooldowns").boolValue = false;
        direction.FindPropertyRelative("clearAllEffects").boolValue = false;
        direction.FindPropertyRelative("clearCombatOnlyEffects").boolValue = false;
        direction.FindPropertyRelative("gates").ClearArray();
        direction.FindPropertyRelative("presentationProfile").objectReferenceValue = profile;
    }

    /// <summary>
    /// 책임 : 지정된 씬을 필요한 동안만 열어 편집하고 새로 연 경우 닫아서 사용자의 씬 작업 상태를 보존한다.
    /// </summary>
    private static void WithScene(string scenePath, Action<Scene> action, bool saveScene = true)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        Scene previousActiveScene = SceneManager.GetActiveScene();
        if (openedHere)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        try
        {
            action(scene);
            if (saveScene)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException($"Failed to save scene: {scenePath}");
            }
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, removeScene: true);

            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
        }
    }

    /// <summary>
    /// 책임 : 씬 전체에서 지정 endpoint Id를 가진 첫 SceneTravelEndpoint를 찾는다.
    /// </summary>
    private static SceneTravelEndpoint FindEndpointInScene(Scene scene, string endpointId)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            SceneTravelEndpoint[] endpoints =
                roots[rootIndex].GetComponentsInChildren<SceneTravelEndpoint>(includeInactive: true);
            for (int endpointIndex = 0; endpointIndex < endpoints.Length; endpointIndex++)
            {
                SceneTravelEndpoint endpoint = endpoints[endpointIndex];
                if (endpoint != null &&
                    string.Equals(endpoint.EndpointId, endpointId, StringComparison.Ordinal))
                {
                    return endpoint;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 책임 : 특정 이름의 직계/하위 Transform을 깊이 우선으로 찾아 prefab 내부 anchor 참조를 복구한다.
    /// </summary>
    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, childName, StringComparison.Ordinal))
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    /// <summary>
    /// 책임 : Grand Hall 도착 endpoint의 기본 배치를 기존 PlayerSpawnPoint와 맞춘다.
    /// </summary>
    private static Transform ResolvePlayerSpawnPoint(Scene scene, string pointId)
    {
        PlayerSpawnPoint fallback = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            PlayerSpawnPoint[] points =
                roots[rootIndex].GetComponentsInChildren<PlayerSpawnPoint>(includeInactive: true);
            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
            {
                PlayerSpawnPoint point = points[pointIndex];
                if (point == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(pointId) &&
                    string.Equals(point.pointId, pointId, StringComparison.Ordinal))
                {
                    return point.transform;
                }

                if (fallback == null || point.isDefault)
                    fallback = point;
            }
        }

        return fallback != null ? fallback.transform : null;
    }

    /// <summary>
    /// 책임 : 주어진 AssetDatabase 폴더 경로가 없으면 상위부터 차례대로 생성한다.
    /// </summary>
    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Split('/');
        string currentPath = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = $"{currentPath}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(nextPath))
                AssetDatabase.CreateFolder(currentPath, parts[i]);

            currentPath = nextPath;
        }
    }

    /// <summary>
    /// 책임 : 씬 전환 대상 씬이 런타임 LoadSceneAsync에서 해석되도록 Build Settings에 활성 등록한다.
    /// </summary>
    private static void EnsureSceneInBuildSettings(string scenePath)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
            throw new InvalidOperationException("Build Settings scene path is empty.");

        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
        if (sceneAsset == null)
            throw new InvalidOperationException($"Scene asset is missing: {scenePath}");

        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            EditorBuildSettingsScene scene = scenes[i];
            if (!string.Equals(scene.path, scenePath, StringComparison.Ordinal))
                continue;

            if (!scene.enabled)
            {
                scenes[i] = new EditorBuildSettingsScene(scenePath, enabled: true);
                EditorBuildSettings.scenes = scenes;
            }

            return;
        }

        Array.Resize(ref scenes, scenes.Length + 1);
        scenes[^1] = new EditorBuildSettingsScene(scenePath, enabled: true);
        EditorBuildSettings.scenes = scenes;
    }

    /// <summary>
    /// 책임 : 지정 씬이 Build Settings에 활성 상태로 등록되어 런타임 이름 기반 로드가 가능한지 확인한다.
    /// </summary>
    private static bool IsSceneEnabledInBuildSettings(string scenePath)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            EditorBuildSettingsScene scene = scenes[i];
            if (scene.enabled && string.Equals(scene.path, scenePath, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
