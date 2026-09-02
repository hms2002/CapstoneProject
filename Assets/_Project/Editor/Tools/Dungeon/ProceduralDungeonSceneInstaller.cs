using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임:
/// - 전용 V0 테스트 씬에 절차 던전 런타임 Grid, Tilemap, Builder, Generator를 설치한다.
/// - 서로 다른 크기의 검증용 방과 방 크기 기반 가변 연결 복도, 테마 라이브러리, 연결 문과 방 오브젝트 설정을 재현 가능하게 구성한다.
/// - 몬스터 방의 기존 encounter 영역과 연결 문 Kill Lock 구성을 설치하고 검증한다.
/// - DragonCorridor와 동일한 제물 석상, 비영구 잠금 문, 보상 상자 흐름을 Treasure 방에 조립하고 검증한다.
/// - 각 보스 테마에 50x50 이상 ㄴ/ㄱ 대형 방과 복수 종류/개체의 몬스터 샘플을 설치하고 검증한다.
/// - 원본 보스 복도의 바닥 변형과 벽 연결 형태별 타일 팔레트를 추출해 절차 방 데이터와 복도 설정에 베이크한다.
/// - V0 테스트 씬이 필수 스폰 인프라만 하나씩 가지고 authored 몬스터 스폰 포인트는 갖지 않는지 컴포넌트 기준으로 검증한다.
/// - ShadowCorridor의 전역 어둠/플레이어 시야 마스크 프리팹을 절차 그림자 씬에 동일하게 설치하고 검증한다.
/// - 세 일반 보스의 절차 Corridor와 최종보스 전용 고정 휴식 Corridor 경로, Build Settings와 로딩 매니페스트를 함께 설치한다.
/// - 테마별 생성 수치를 DungeonGenerationProfileSO에 보존하고 설치 재실행 시 기획자 조정값을 덮어쓰지 않는다.
/// - 테스트 씬의 GlobalUIRoot를 ProtoTypeHub의 프리팹 오버라이드와 동기화한다.
/// - 기존 절차 복도 씬에 고정 방 Tilemap 슬롯을 비파괴적으로 보강하고 렌더·물리 계약을 검증한다.
/// </summary>
public static class ProceduralDungeonSceneInstaller
{
    private const int SingleSocketSeedSweepCount = 128;
    private const int GraphPolicySeedSweepCount = 64;
    private const int ExplorationCorridorRoomCount = 15;
    private const int PrototypeMinimumCorridorLength = 2;
    private const float PrototypeCorridorLengthPerRoomCell = 0.05f;
    private const int PrototypeCorridorLengthVariation = 2;
    private const string HubScenePath = "Assets/_Project/Scenes/ProtoTypeHub.unity";
    private const string TargetScenePath = "Assets/_Project/Scenes/ProceduralDungeonV0Test.unity";
    private const string SourceRoomPath = "Assets/_Project/Data/Dungeon/Rooms/TestTypeStart.asset";
    private const string GeneratedRoomFolder = "Assets/_Project/Data/Dungeon/Rooms/PrototypeV0";
    private const string GeneratedProceduralPrefabFolder = "Assets/_Project/Prefabs/Map/Procedural";
    private const string MonsterSpawnSetFolder = "Assets/_Project/Data/Monsters/SpawnSets";
    private const string LibraryFolder = "Assets/_Project/Data/Dungeon/Libraries";
    private const string LibraryPath = LibraryFolder + "/PrototypeCorridorV0Library.asset";
    private const string ExplorationLayoutPolicyPath =
        LibraryFolder + "/ExplorationCorridorPrototypePolicy.asset";
    private const string RunRouteCatalogPath =
        "Assets/_Project/Data/SceneFlow/Routes/RunRouteCatalog.asset";
    private const string FinalDemonKingRouteSetPath =
        "Assets/_Project/Data/SceneFlow/Routes/DemonkingRouteSet.asset";
    private const string FinalDemonKingCorridorSceneName = "DemonkingCorridor";
    private const string FinalDemonKingCorridorScenePath =
        "Assets/_Project/Scenes/DemonkingCorridor.unity";
    private const string RetiredProceduralDemonKingCorridorScenePath =
        "Assets/_Project/Scenes/ProceduralDemonkingCorridor.unity";
    private const string RetiredDemonKingLobbyConnectionPath =
        "Assets/_Project/Data/SceneFlow/Connections/Lobby_demon_king_Corridor.asset";
    private const string RetiredDemonKingBossConnectionPath =
        "Assets/_Project/Data/SceneFlow/Connections/Corridor_demon_king_Boss.asset";
    private const string DoorPrefabPath = "Assets/_Project/Prefabs/Map/ShortCut/Door.prefab";
    private const string StatuePrefabPath = "Assets/_Project/Prefabs/Map/ShortCut/Statue.prefab";
    private const string SacrificeRewardAlcovePrefabPath =
        GeneratedProceduralPrefabFolder + "/SacrificeRewardAlcove.prefab";
    private const string MonsterPrefabPath = "Assets/_Project/Prefabs/Monsters/CommonCorridor/GoblinWarrior.prefab";
    private const string ChestPrefabPath = "Assets/_Project/Prefabs/Items/Chests/TreasureChest.prefab";
    private const string PortalPrefabPath = "Assets/_Project/Prefabs/Map/Portal/ScenePortal.prefab";
    private const string ShadowVisionMaskPrefabPath =
        "Assets/_Project/Prefabs/Map/Gimmicks/Witch/GlobalVisionMaskRoot.prefab";
    private const string GeneratedRootName = "[ProceduralDungeonV0]";
    private const string GlobalUiRootName = "GlobalUIRoot";
    private const string GlobalVisionMaskRootName = "GlobalVisionMaskRoot";

    /// <summary>
    /// 책임:
    /// - 한 보스 테마의 원본 씬, 절차 생성 대상 씬, 경로 에셋, 룸 데이터 경로, 몬스터 후보와 Seed 선호값을 묶는다.
    /// - 런타임 레이아웃 코드가 보스 테마를 알지 않고도 설치기가 동일한 파이프라인을 반복 적용하게 한다.
    /// </summary>
    private readonly struct BossThemeInstallSpec
    {
        public string ThemeId { get; }
        public string SourceScenePath { get; }
        public string TargetScenePath { get; }
        public string TargetSceneName { get; }
        public string RoomFolder { get; }
        public string LibraryPath { get; }
        public string GenerationProfilePath { get; }
        public string RouteSetPath { get; }
        public IReadOnlyList<string> MonsterPrefabPaths { get; }
        public int PreferredSeed { get; }

        public BossThemeInstallSpec(
            string themeId,
            string sourceScenePath,
            string targetSceneName,
            string routeSetPath,
            int preferredSeed,
            params string[] monsterPrefabPaths)
        {
            ThemeId = themeId;
            SourceScenePath = sourceScenePath;
            TargetSceneName = targetSceneName;
            TargetScenePath = $"Assets/_Project/Scenes/{targetSceneName}.unity";
            RoomFolder = $"Assets/_Project/Data/Dungeon/Rooms/BossThemes/{themeId}";
            LibraryPath = $"{LibraryFolder}/Procedural{themeId}Library.asset";
            GenerationProfilePath =
                $"{DungeonGenerationProfileAssetUtility.ProfileFolder}/Procedural{themeId}GenerationProfile.asset";
            RouteSetPath = routeSetPath;
            MonsterPrefabPaths = monsterPrefabPaths;
            PreferredSeed = preferredSeed;
        }
    }

    /// <summary>
    /// 책임:
    /// - 원본 보스 복도 Ground/Wall Tilemap에서 추출한 테마별 타일 후보를 보관한다.
    /// - 방 벽의 8방향/4방향 이웃 형태와 복도 벽 진행 방향에 맞는 안전한 TileBase 후보를 설치기에 제공한다.
    /// </summary>
    private sealed class BossThemeTilePalette
    {
        public TileBase PrimaryFloor { get; }
        public TileBase PrimaryWall { get; }
        public IReadOnlyList<TileBase> FloorVariants { get; }
        public IReadOnlyList<TileBase> GeneralWallVariants { get; }
        public IReadOnlyList<TileBase> HorizontalWallVariants { get; }
        public IReadOnlyList<TileBase> VerticalWallVariants { get; }
        public IReadOnlyDictionary<int, List<TileBase>> WallVariantsByNeighborMask { get; }
        public IReadOnlyDictionary<int, List<TileBase>> WallVariantsByCardinalMask { get; }

        public BossThemeTilePalette(
            TileBase primaryFloor,
            TileBase primaryWall,
            IReadOnlyList<TileBase> floorVariants,
            IReadOnlyList<TileBase> generalWallVariants,
            IReadOnlyList<TileBase> horizontalWallVariants,
            IReadOnlyList<TileBase> verticalWallVariants,
            IReadOnlyDictionary<int, List<TileBase>> wallVariantsByNeighborMask,
            IReadOnlyDictionary<int, List<TileBase>> wallVariantsByCardinalMask)
        {
            PrimaryFloor = primaryFloor;
            PrimaryWall = primaryWall;
            FloorVariants = floorVariants;
            GeneralWallVariants = generalWallVariants;
            HorizontalWallVariants = horizontalWallVariants;
            VerticalWallVariants = verticalWallVariants;
            WallVariantsByNeighborMask = wallVariantsByNeighborMask;
            WallVariantsByCardinalMask = wallVariantsByCardinalMask;
        }
    }

    private enum LargeCornerRoomShape
    {
        Nieun,
        Giyeok
    }

    [MenuItem("Tools/Dungeon/Install Boss Theme Procedural Corridor Scenes")]
    public static void InstallBossThemeProceduralCorridorScenes()
    {
        BossThemeInstallSpec[] specs = CreateBossThemeInstallSpecs();
        GameObject chestPrefab = LoadRequiredObjectPrefab(ChestPrefabPath, RoomObjectKind.Chest);
        GameObject killLockChestPrefab = LoadConfiguredRoomObjectPrefab(
            GeneratedRoomFolder + "/Prototype_Combat.asset",
            "KillLockChest",
            RoomObjectKind.Chest,
            requireChestKillLock: true);
        GameObject portalPrefab = LoadRequiredObjectPrefab(PortalPrefabPath, RoomObjectKind.Portal);
        GameObject statuePrefab = LoadRequiredObjectPrefab(StatuePrefabPath, RoomObjectKind.Prop);
        GameObject doorPrefabObject = LoadRequiredObjectPrefab(DoorPrefabPath, RoomObjectKind.Prop);

        EnsureFolder(GeneratedProceduralPrefabFolder);
        EnsureFolder(LibraryFolder);
        DungeonLayoutPolicySO layoutPolicy = CreateOrUpdateExplorationLayoutPolicy();
        GameObject sacrificeRewardAlcovePrefab =
            CreateOrUpdateSacrificeRewardAlcovePrefab(
                statuePrefab,
                doorPrefabObject,
                chestPrefab);

        for (int i = 0; i < specs.Length; i++)
        {
            BossThemeInstallSpec spec = specs[i];
            EnsureFolder(spec.RoomFolder);
            BossThemeTilePalette tilePalette = LoadThemeTilePalette(spec);
            RoomThemeLibrarySO library = CreateOrUpdateBossThemeLibrary(
                spec,
                tilePalette,
                chestPrefab,
                killLockChestPrefab,
                portalPrefab,
                sacrificeRewardAlcovePrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                spec.LibraryPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            library = AssetDatabase.LoadAssetAtPath<RoomThemeLibrarySO>(spec.LibraryPath);
            if (library == null)
                throw new InvalidOperationException($"Failed to reload theme library: {spec.LibraryPath}");
            layoutPolicy =
                AssetDatabase.LoadAssetAtPath<DungeonLayoutPolicySO>(ExplorationLayoutPolicyPath);
            if (layoutPolicy == null)
                throw new InvalidOperationException("Exploration Corridor layout policy became unavailable.");

            DungeonGenerationProfileSO generationProfile =
                CreateOrLoadThemeGenerationProfile(
                    spec,
                    library,
                    layoutPolicy);
            InstallBossThemeScene(
                spec,
                library,
                generationProfile,
                tilePalette,
                doorPrefabObject.GetComponent<DoorObject>());
            UpdateRouteSetCorridorScene(spec);
            EnsureSceneInBuildSettings(spec.TargetScenePath);
        }

        RestoreFinalDemonKingRestCorridorRoute();
        EnsureSceneInBuildSettings(FinalDemonKingCorridorScenePath);
        DisableRetiredDemonKingProceduralRoute();
        DemonKingHubPortalInstaller.Install();
        AssetDatabase.SaveAssets();
        VerifyBossThemeRoutesAndHubDemonKingPortal(specs);
        VerifyFinalDemonKingRestCorridorRoute();
        ProceduralCorridorTravelInstaller.Install();
        RouteSetLoadManifestBuilderWindow.BuildAllRouteSetsBatch();
        Debug.Log(
            $"Installed and connected {specs.Length} normal boss procedural Corridors. " +
            $"The final DemonKing route remains on fixed rest scene '{FinalDemonKingCorridorSceneName}'.");
    }

    /// <summary>
    /// 책임 : 기존 세 일반 보스 절차 복도 씬의 현재 생성값을 테마 프로필로 마이그레이션하고 Generator 참조만 연결한다.
    /// </summary>
    [MenuItem("Tools/Dungeon/Install Boss Theme Generation Profiles Only")]
    public static void InstallBossThemeGenerationProfilesOnly()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureFolder(DungeonGenerationProfileAssetUtility.ProfileFolder);
        DungeonLayoutPolicySO fallbackPolicy =
            AssetDatabase.LoadAssetAtPath<DungeonLayoutPolicySO>(ExplorationLayoutPolicyPath);
        BossThemeInstallSpec[] specs = CreateBossThemeInstallSpecs();
        int linkedSceneCount = 0;

        for (int specIndex = 0; specIndex < specs.Length; specIndex++)
        {
            BossThemeInstallSpec spec = specs[specIndex];
            RoomThemeLibrarySO library =
                AssetDatabase.LoadAssetAtPath<RoomThemeLibrarySO>(spec.LibraryPath);
            if (library == null)
                throw new InvalidOperationException($"Missing boss-theme room library: {spec.LibraryPath}");

            Scene scene = EditorSceneManager.OpenScene(spec.TargetScenePath, OpenSceneMode.Single);
            List<DungeonGenerator> generators = FindComponentsInScene<DungeonGenerator>(scene);
            if (generators.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one DungeonGenerator in {spec.TargetScenePath}, found {generators.Count}.");
            }

            DungeonGenerator generator = generators[0];
            DungeonGenerationProfileSO profile =
                DungeonGenerationProfileAssetUtility.FindForLibrary(library);
            if (profile == null)
            {
                profile = DungeonGenerationProfileAssetUtility.FindOrCreateForLibrary(
                    library,
                    generator.LayoutPolicy != null ? generator.LayoutPolicy : fallbackPolicy,
                    generator.Seed,
                    generator.RoomCount,
                    generator.IncludeBossRoom,
                    generator.MaxPlacementAttemptsPerRoom,
                    generator.MinimumCorridorLength,
                    generator.CorridorLengthPerRoomCell,
                    generator.CorridorLengthVariation);
            }

            generator.EditorAssignGenerationProfile(profile);
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            linkedSceneCount++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log(
            $"Installed persistent generation profiles for {linkedSceneCount} normal boss Corridor scenes.");
    }

    /// <summary>
    /// 책임:
    /// - 세 일반 보스 테마 라이브러리에 기존 테마별 몬스터 후보를 스테이지 고정 프리팹 빠른 선택 목록으로 동기화한다.
    /// - 방·씬·생성 프로필은 변경하지 않고 Room Piece Editor의 추천 목록 데이터만 갱신한다.
    /// </summary>
    [MenuItem("Tools/Dungeon/Sync Boss Theme Stage Monster Catalogs")]
    public static void SyncBossThemeStageMonsterCatalogs()
    {
        BossThemeInstallSpec[] specs = CreateBossThemeInstallSpecs();
        for (int specIndex = 0; specIndex < specs.Length; specIndex++)
        {
            BossThemeInstallSpec spec = specs[specIndex];
            RoomThemeLibrarySO library =
                AssetDatabase.LoadAssetAtPath<RoomThemeLibrarySO>(spec.LibraryPath);
            if (library == null)
                throw new InvalidOperationException($"Missing boss-theme room library: {spec.LibraryPath}");

            ApplyStageMonsterCatalog(library, spec);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"Synced stage-monster quick-pick catalogs for {specs.Length} boss themes.");
    }

    /// <summary>
    /// 책임:
    /// - 이미 제작된 세 일반 보스 방의 Monster 위치를 Warrior/Mage/Tank 역할 지점으로 변환한다.
    /// - 타일, 소켓, Placement Id, 오브젝트 위치와 Kill Lock 연결은 변경하지 않는다.
    /// </summary>
    [MenuItem("Tools/Dungeon/Migrate Procedural Rooms To Role-Based Monster Spawning")]
    public static void MigrateProceduralRoomsToRoleBasedMonsterSpawning()
    {
        BossThemeInstallSpec[] specs = CreateBossThemeInstallSpecs();
        StageMonsterSetSO[] roleStageSets = LoadRoleStageMonsterSets();
        int migratedRoomCount = 0;
        for (int specIndex = 0; specIndex < specs.Length; specIndex++)
        {
            BossThemeInstallSpec spec = specs[specIndex];
            RoomThemeLibrarySO library =
                AssetDatabase.LoadAssetAtPath<RoomThemeLibrarySO>(spec.LibraryPath);
            if (library == null)
                throw new InvalidOperationException($"Missing theme library: {spec.LibraryPath}");

            IReadOnlyList<RoomTemplateSO> rooms = library.Rooms;
            for (int roomIndex = 0; rooms != null && roomIndex < rooms.Count; roomIndex++)
            {
                RoomTemplateSO room = rooms[roomIndex];
                if (room == null ||
                    room.LayoutData.roomType != RoomType.Combat ||
                    !HasMonsterPlacements(room.BuildData.objectPlacements))
                {
                    continue;
                }

                RoomBuildData build = room.BuildData;
                int monsterIndex = 0;
                for (int placementIndex = 0;
                     placementIndex < build.objectPlacements.Count;
                     placementIndex++)
                {
                    RoomObjectPlacementData placement = build.objectPlacements[placementIndex];
                    if (placement.kind != RoomObjectKind.Monster)
                        continue;

                    RoomMonsterSpawnRole role =
                        (RoomMonsterSpawnRole)(monsterIndex % roleStageSets.Length);
                    placement.prefab = null;
                    placement.monsterSpawnRole = role;
                    placement.monsterStageSet = roleStageSets[(int)role];
                    placement.localScale = Vector3.one;
                    build.objectPlacements[placementIndex] = placement;
                    monsterIndex++;
                }

                room.EditorSetData(room.LayoutData, build);
                EditorUtility.SetDirty(room);
                migratedRoomCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log(
            $"Migrated {migratedRoomCount} procedural Combat rooms to explicit " +
            "Warrior/Mage/Tank spawn positions with stage-based prefab resolution.");
    }

    private static bool HasMonsterPlacements(
        IReadOnlyList<RoomObjectPlacementData> placements)
    {
        for (int i = 0; placements != null && i < placements.Count; i++)
        {
            if (placements[i].kind == RoomObjectKind.Monster)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 책임:
    /// - 현재 기획 피드백의 콘텐츠 등록 변경과 역할별 몬스터 지점 마이그레이션을 한 번에 적용한다.
    /// - 토관/NPC 원본 에셋은 보존하면서 실제 생성 씬과 데이터만 플레이 가능한 최신 계약으로 동기화한다.
    /// </summary>
    [MenuItem("Tools/Dungeon/Apply Current Designer Feedback Migration")]
    public static void ApplyCurrentDesignerFeedbackMigration()
    {
        MigrateProceduralRoomsToRoleBasedMonsterSpawning();
        SyncBossThemeStageMonsterCatalogs();
        ProceduralSlimeNpcRoomInstaller.SealInstalledContent();
        ProceduralCorridorTravelInstaller.RemoveDeprecatedCorridorPipeTravel();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("Applied current procedural Corridor designer feedback migration.");
    }

    /// <summary>
    /// 책임 : 기존 세 일반 보스 절차 복도와 V0 테스트 씬의 생성 Grid에 누락된 고정 Tilemap 슬롯만 추가한다.
    /// </summary>
    [MenuItem("Tools/Dungeon/Install Fixed Room Tile Layers Only")]
    public static void InstallFixedRoomTileLayersOnly()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        List<string> scenePaths = new();
        BossThemeInstallSpec[] specs = CreateBossThemeInstallSpecs();
        for (int i = 0; i < specs.Length; i++)
            scenePaths.Add(specs[i].TargetScenePath);
        scenePaths.Add(TargetScenePath);

        for (int sceneIndex = 0; sceneIndex < scenePaths.Count; sceneIndex++)
        {
            string scenePath = scenePaths[sceneIndex];
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            List<DungeonRoomBuilder> builders = FindComponentsInScene<DungeonRoomBuilder>(scene);
            if (builders.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one DungeonRoomBuilder in {scenePath}, found {builders.Count}.");
            }

            InstallFixedRoomTileLayers(builders[0], scenePath);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Installed fixed room Tilemap slots in {scenePaths.Count} procedural scenes.");
    }

    /// <summary>
    /// 책임:
    /// 기존 제물 보상 Alcove의 수동 자식 배치를 보존하면서 방별 편집용 복합 Pose 슬롯 계약만 설치한다.
    /// </summary>
    [MenuItem("Tools/Dungeon/Update Sacrifice Reward Alcove Pose Slots")]
    public static void UpdateSacrificeRewardAlcovePoseSlots()
    {
        GameObject statuePrefab = LoadRequiredObjectPrefab(StatuePrefabPath, RoomObjectKind.Prop);
        GameObject doorPrefab = LoadRequiredObjectPrefab(DoorPrefabPath, RoomObjectKind.Prop);
        GameObject chestPrefab = LoadRequiredObjectPrefab(ChestPrefabPath, RoomObjectKind.Chest);
        EnsureFolder(GeneratedProceduralPrefabFolder);

        GameObject alcove = CreateOrUpdateSacrificeRewardAlcovePrefab(
            statuePrefab,
            doorPrefab,
            chestPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        VerifySacrificeRewardAlcove(alcove, SacrificeRewardAlcovePrefabPath);
        Debug.Log("Updated SacrificeRewardAlcove with three room-specific pose slots.");
    }

    /// <summary>
    /// 책임 : 세 일반 보스의 생성 프로필, 활성 씬 참조와 동일 설정의 대표 레이아웃 생성 성공 여부를 검증한다.
    /// </summary>
    [MenuItem("Tools/Dungeon/Validate Boss Theme Generation Profiles")]
    public static void ValidateBossThemeGenerationProfiles()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        HashSet<string> enabledScenePaths = new(StringComparer.Ordinal);
        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        for (int sceneIndex = 0; sceneIndex < buildScenes.Length; sceneIndex++)
        {
            if (buildScenes[sceneIndex].enabled)
                enabledScenePaths.Add(buildScenes[sceneIndex].path);
        }

        BossThemeInstallSpec[] specs = CreateBossThemeInstallSpecs();
        for (int specIndex = 0; specIndex < specs.Length; specIndex++)
        {
            BossThemeInstallSpec spec = specs[specIndex];
            RoomThemeLibrarySO library =
                AssetDatabase.LoadAssetAtPath<RoomThemeLibrarySO>(spec.LibraryPath);
            DungeonGenerationProfileSO profile =
                AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(
                    spec.GenerationProfilePath);
            if (library == null || profile == null || profile.RoomLibrary != library)
            {
                throw new InvalidOperationException(
                    $"Theme generation profile contract is invalid: {spec.ThemeId}");
            }

            if (!enabledScenePaths.Contains(spec.TargetScenePath))
                throw new InvalidOperationException($"Procedural Corridor scene is not enabled: {spec.TargetScenePath}");

            Scene scene = EditorSceneManager.OpenScene(spec.TargetScenePath, OpenSceneMode.Single);
            List<DungeonGenerator> generators = FindComponentsInScene<DungeonGenerator>(scene);
            if (generators.Count != 1 || generators[0].GenerationProfile != profile)
            {
                throw new InvalidOperationException(
                    $"Procedural Corridor scene does not reference its theme generation profile: " +
                    $"{spec.TargetScenePath}");
            }

            DungeonGenerator generator = generators[0];
            if (generator.RoomLibrary != profile.RoomLibrary ||
                generator.LayoutPolicy != profile.LayoutPolicy ||
                generator.Seed != profile.Seed ||
                generator.RoomCount != profile.RoomCount ||
                generator.IncludeBossRoom != profile.IncludeBossRoom ||
                generator.MaxPlacementAttemptsPerRoom != profile.MaxPlacementAttemptsPerRoom ||
                generator.MinimumCorridorLength != profile.MinimumCorridorLength ||
                !Mathf.Approximately(
                    generator.CorridorLengthPerRoomCell,
                    profile.CorridorLengthPerRoomCell) ||
                generator.CorridorLengthVariation != profile.CorridorLengthVariation ||
                DungeonGenerationProfileAssetUtility.CountEnabledBuildSceneReferences(profile) < 1)
            {
                throw new InvalidOperationException(
                    $"DungeonGenerator does not resolve the saved theme profile values: {spec.ThemeId}");
            }

            DungeonLayoutResult result = profile.LayoutPolicy != null && profile.IncludeBossRoom
                ? new DungeonGraphLayoutAssembler().Assemble(
                    profile.RoomLibrary,
                    profile.LayoutPolicy,
                    profile.Seed,
                    profile.RoomCount,
                    profile.MaxPlacementAttemptsPerRoom,
                    profile.MinimumCorridorLength,
                    profile.CorridorLengthPerRoomCell,
                    profile.CorridorLengthVariation,
                    profile.GuaranteedRoomTemplates)
                : new DungeonLayoutAssembler().Assemble(
                    profile.RoomLibrary,
                    profile.Seed,
                    profile.RoomCount,
                    profile.IncludeBossRoom,
                    profile.MaxPlacementAttemptsPerRoom,
                    profile.MinimumCorridorLength,
                    profile.CorridorLengthPerRoomCell,
                    profile.CorridorLengthVariation);
            if (!result.IsComplete || result.Rooms.Count != profile.RoomCount)
            {
                throw new InvalidOperationException(
                    $"Saved generation profile cannot produce its requested layout. " +
                    $"Theme={spec.ThemeId}, Reason={result.FailureReason}");
            }

            Debug.Log(
                $"Generation profile verified. Theme={spec.ThemeId}, " +
                $"Scene={spec.TargetSceneName}, Rooms={profile.RoomCount}, " +
                $"Corridor={profile.MinimumCorridorLength}+size*{profile.CorridorLengthPerRoomCell}" +
                $"+random(0..{profile.CorridorLengthVariation})");
        }
    }

    [MenuItem("Tools/Dungeon/Validate Exploration Corridor Layout Policy")]
    public static void ValidateExplorationCorridorLayoutPolicy()
    {
        EnsureFolder(LibraryFolder);
        DungeonLayoutPolicySO policy = CreateOrUpdateExplorationLayoutPolicy();
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(
            ExplorationLayoutPolicyPath,
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        policy = AssetDatabase.LoadAssetAtPath<DungeonLayoutPolicySO>(ExplorationLayoutPolicyPath);
        if (policy == null)
            throw new InvalidOperationException("Failed to reload the exploration Corridor layout policy.");

        BossThemeInstallSpec[] specs = CreateBossThemeInstallSpecs();
        for (int specIndex = 0; specIndex < specs.Length; specIndex++)
        {
            BossThemeInstallSpec spec = specs[specIndex];
            RoomThemeLibrarySO library =
                AssetDatabase.LoadAssetAtPath<RoomThemeLibrarySO>(spec.LibraryPath);
            if (library == null)
                throw new InvalidOperationException($"Missing boss-theme room library: {spec.LibraryPath}");

            DungeonGenerationProfileSO profile =
                DungeonGenerationProfileAssetUtility.FindForLibrary(library);
            int roomCount = profile != null ? profile.RoomCount : ExplorationCorridorRoomCount;
            int maxPlacementAttempts = profile != null
                ? profile.MaxPlacementAttemptsPerRoom
                : 512;
            DungeonLayoutPolicySO resolvedPolicy = profile != null && profile.LayoutPolicy != null
                ? profile.LayoutPolicy
                : policy;
            int representativeSeed = profile != null
                ? profile.Seed
                : ResolveThemeSeed(library, policy, spec.PreferredSeed);
            VerifyGraphFirstPolicyAcrossSeeds(
                library,
                resolvedPolicy,
                roomCount,
                maxPlacementAttempts,
                profile != null ? profile.MinimumCorridorLength : PrototypeMinimumCorridorLength,
                profile != null ? profile.CorridorLengthPerRoomCell : PrototypeCorridorLengthPerRoomCell,
                profile != null ? profile.CorridorLengthVariation : PrototypeCorridorLengthVariation);
            Debug.Log(
                $"Exploration layout policy verified. Theme={spec.ThemeId}, " +
                $"RepresentativeSeed={representativeSeed}");
        }
    }

    /// <summary>
    /// 책임 : HUB의 최종보스 포탈 목적지가 절차 복도가 아니라 고정 무전투 휴식 복도를 거치는지 수동 검증 진입점을 제공한다.
    /// </summary>
    [MenuItem("Tools/Dungeon/Validate Final DemonKing Rest Corridor Route")]
    public static void ValidateFinalDemonKingRestCorridorRoute()
    {
        VerifyFinalDemonKingRestCorridorRoute();
    }

    private static BossThemeInstallSpec[] CreateBossThemeInstallSpecs()
    {
        return new[]
        {
            new BossThemeInstallSpec(
                "Shadow",
                "Assets/_Project/Scenes/ShadowCorridor.unity",
                "ProceduralShadowCorridor",
                "Assets/_Project/Data/SceneFlow/Routes/ShadowCorridorBossRouteSet.asset",
                20260821,
                "Assets/_Project/Prefabs/Monsters/ShadowCorridor/ShadowMonster.prefab",
                "Assets/_Project/Prefabs/Monsters/ShadowCorridor/CorridorCandlestickMonster.prefab",
                "Assets/_Project/Prefabs/Monsters/ShadowCorridor/Dead'sSkeleton.prefab",
                "Assets/_Project/Prefabs/Monsters/ShadowCorridor/ShadowServant/ShadowServant.prefab",
                "Assets/_Project/Prefabs/Monsters/ShadowCorridor/StrangeCandlestick/StrangeCandlestick.prefab"),
            new BossThemeInstallSpec(
                "Dragon",
                "Assets/_Project/Scenes/DragonCorridor.unity",
                "ProceduralDragonCorridor",
                "Assets/_Project/Data/SceneFlow/Routes/Dragon_CorridorBossRouteSet.asset",
                20261831,
                "Assets/_Project/Prefabs/Monsters/BeerMonster.prefab",
                "Assets/_Project/Prefabs/Monsters/CommonCorridor/GoblinWarrior.prefab",
                "Assets/_Project/Prefabs/Monsters/CommonCorridor/LizardWarrior.prefab",
                "Assets/_Project/Prefabs/Monsters/CommonCorridor/LizardMage.prefab",
                "Assets/_Project/Prefabs/Monsters/CommonCorridor/GoblinGunner.prefab"),
            new BossThemeInstallSpec(
                "Slime",
                "Assets/_Project/Scenes/SlimeCorridor.unity",
                "ProceduralSlimeCorridor",
                "Assets/_Project/Data/SceneFlow/Routes/SlimeRouteSet.asset",
                20262841,
                "Assets/_Project/Prefabs/Monsters/SlimeCorridor/Pawn.prefab",
                "Assets/_Project/Prefabs/Monsters/SlimeCorridor/Knight.prefab",
                "Assets/_Project/Prefabs/Monsters/SlimeCorridor/Wizard.prefab",
                "Assets/_Project/Prefabs/Monsters/SlimeCorridor/Bishop.prefab",
                "Assets/_Project/Prefabs/Monsters/SlimeCorridor/Rook.prefab")
        };
    }

    /// <summary>
    /// 책임:
    /// - 기획서의 12~18 Area, 보스 거리 6~8, 분기 2~4, 순환 1~2와 필수 보상/전투 수를 공유 정책 에셋으로 설치한다.
    /// - 세 일반 보스 테마가 같은 탐색 구조를 사용하면서도 개별 씬이나 조립기 코드에 수치를 중복 저장하지 않게 한다.
    /// </summary>
    private static DungeonLayoutPolicySO CreateOrUpdateExplorationLayoutPolicy()
    {
        DungeonLayoutPolicySO policy =
            AssetDatabase.LoadAssetAtPath<DungeonLayoutPolicySO>(ExplorationLayoutPolicyPath);
        bool wasCreated = policy == null;
        if (policy == null)
        {
            policy = ScriptableObject.CreateInstance<DungeonLayoutPolicySO>();
            AssetDatabase.CreateAsset(policy, ExplorationLayoutPolicyPath);
        }

        if (wasCreated)
        {
            policy.EditorConfigure(
                recommendedMinimumRooms: 12,
                recommendedMaximumRooms: 18,
                minimumBossDistance: 6,
                maximumBossDistance: 8,
                minimumBranches: 2,
                maximumBranches: 4,
                minimumCycles: 1,
                maximumCycles: 2,
                topologyAttempts: 512,
                requiredTreasureRooms: 1,
                requiredEventRooms: 0,
                requiredShopRooms: 0,
                requiredMinimumCombatRooms: 4,
                shouldPreferSpecialRoomsAtDeadEnds: true);
            policy.name = "ExplorationCorridorPrototypePolicy";
            EditorUtility.SetDirty(policy);
        }

        return policy;
    }

    private static BossThemeTilePalette LoadThemeTilePalette(
        BossThemeInstallSpec spec)
    {
        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene sourceScene = EditorSceneManager.OpenScene(spec.SourceScenePath, OpenSceneMode.Additive);
        try
        {
            Tilemap floorTilemap = FindPopulatedTilemap(sourceScene, "Ground");
            Tilemap wallTilemap = FindPopulatedTilemap(sourceScene, "Wall");
            Dictionary<TileBase, int> floorCounts = CountTiles(floorTilemap, false);
            Dictionary<TileBase, int> wallCounts = CountTiles(wallTilemap, true);
            if (wallCounts.Count == 0)
                wallCounts = CountTiles(wallTilemap, false);

            List<TileBase> floorVariants = RankTileCounts(floorCounts, 8);
            List<TileBase> generalWallVariants = RankTileCounts(wallCounts, 8);
            TileBase floorTile = floorVariants.Count > 0 ? floorVariants[0] : null;
            TileBase wallTile = generalWallVariants.Count > 0 ? generalWallVariants[0] : null;
            if (floorTile == null || wallTile == null)
            {
                throw new InvalidOperationException(
                    $"Theme source scene must contain populated Ground and Wall Tilemaps: {spec.SourceScenePath}");
            }

            BuildWallTopologyPalettes(
                wallTilemap,
                out Dictionary<int, List<TileBase>> wallVariantsByNeighborMask,
                out Dictionary<int, List<TileBase>> wallVariantsByCardinalMask,
                out List<TileBase> horizontalWallVariants,
                out List<TileBase> verticalWallVariants);
            EnsurePaletteFallback(horizontalWallVariants, generalWallVariants, wallTile);
            EnsurePaletteFallback(verticalWallVariants, generalWallVariants, wallTile);

            Debug.Log(
                $"Resolved {spec.ThemeId} theme tile palette. " +
                $"Floor={AssetDatabase.GetAssetPath(floorTile)}, " +
                $"Wall={AssetDatabase.GetAssetPath(wallTile)}, " +
                $"FloorVariants={floorVariants.Count}, " +
                $"WallVariants={generalWallVariants.Count}, " +
                $"WallMasks={wallVariantsByNeighborMask.Count}");
            return new BossThemeTilePalette(
                floorTile,
                wallTile,
                floorVariants,
                generalWallVariants,
                horizontalWallVariants,
                verticalWallVariants,
                wallVariantsByNeighborMask,
                wallVariantsByCardinalMask);
        }
        finally
        {
            EditorSceneManager.CloseScene(sourceScene, true);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
        }
    }

    private static Tilemap FindPopulatedTilemap(Scene scene, string objectName)
    {
        Tilemap fallback = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Tilemap[] tilemaps = roots[rootIndex].GetComponentsInChildren<Tilemap>(true);
            for (int tilemapIndex = 0; tilemapIndex < tilemaps.Length; tilemapIndex++)
            {
                Tilemap candidate = tilemaps[tilemapIndex];
                if (!string.Equals(candidate.name, objectName, StringComparison.Ordinal))
                    continue;

                fallback ??= candidate;
                if (candidate.GetUsedTilesCount() > 0)
                    return candidate;
            }
        }

        return fallback;
    }

    private static Dictionary<TileBase, int> CountTiles(
        Tilemap tilemap,
        bool requireWallCollider)
    {
        Dictionary<TileBase, int> counts = new();
        if (tilemap == null)
            return counts;

        foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
        {
            TileBase tile = tilemap.GetTile(cell);
            if (tile == null || (requireWallCollider && !CanUseAsGeneratedWall(tile)))
                continue;

            counts.TryGetValue(tile, out int count);
            counts[tile] = count + 1;
        }

        return counts;
    }

    private static bool CanUseAsGeneratedWall(TileBase tile)
    {
        if (tile == null)
            return false;

        Tile concreteTile = tile as Tile;
        return concreteTile == null || concreteTile.colliderType != Tile.ColliderType.None;
    }

    private static List<TileBase> RankTileCounts(
        IReadOnlyDictionary<TileBase, int> counts,
        int maximumCount)
    {
        List<KeyValuePair<TileBase, int>> ranked = new(counts);
        ranked.Sort((left, right) =>
        {
            int countComparison = right.Value.CompareTo(left.Value);
            if (countComparison != 0)
                return countComparison;

            return string.CompareOrdinal(
                AssetDatabase.GetAssetPath(left.Key),
                AssetDatabase.GetAssetPath(right.Key));
        });

        int resultCount = Mathf.Min(maximumCount, ranked.Count);
        List<TileBase> result = new(resultCount);
        for (int i = 0; i < resultCount; i++)
            result.Add(ranked[i].Key);
        return result;
    }

    private static void BuildWallTopologyPalettes(
        Tilemap wallTilemap,
        out Dictionary<int, List<TileBase>> variantsByNeighborMask,
        out Dictionary<int, List<TileBase>> variantsByCardinalMask,
        out List<TileBase> horizontalVariants,
        out List<TileBase> verticalVariants)
    {
        Dictionary<int, Dictionary<TileBase, int>> neighborCounts = new();
        Dictionary<int, Dictionary<TileBase, int>> cardinalCounts = new();
        Dictionary<TileBase, int> horizontalCounts = new();
        Dictionary<TileBase, int> verticalCounts = new();
        if (wallTilemap != null)
        {
            foreach (Vector3Int cell in wallTilemap.cellBounds.allPositionsWithin)
            {
                TileBase tile = wallTilemap.GetTile(cell);
                if (!CanUseAsGeneratedWall(tile))
                    continue;

                int neighborMask = CalculateTilemapNeighborMask(wallTilemap, cell);
                int cardinalMask = neighborMask & 0x0F;
                IncrementNestedTileCount(neighborCounts, neighborMask, tile);
                IncrementNestedTileCount(cardinalCounts, cardinalMask, tile);
                if ((cardinalMask & 0x0A) == 0x0A)
                    IncrementTileCount(horizontalCounts, tile);
                if ((cardinalMask & 0x05) == 0x05)
                    IncrementTileCount(verticalCounts, tile);
            }
        }

        variantsByNeighborMask = RankNestedTileCounts(neighborCounts, 4);
        variantsByCardinalMask = RankNestedTileCounts(cardinalCounts, 4);
        horizontalVariants = RankTileCounts(horizontalCounts, 6);
        verticalVariants = RankTileCounts(verticalCounts, 6);
    }

    private static int CalculateTilemapNeighborMask(Tilemap tilemap, Vector3Int cell)
    {
        Vector3Int[] offsets =
        {
            Vector3Int.up,
            Vector3Int.right,
            Vector3Int.down,
            Vector3Int.left,
            Vector3Int.up + Vector3Int.right,
            Vector3Int.down + Vector3Int.right,
            Vector3Int.down + Vector3Int.left,
            Vector3Int.up + Vector3Int.left
        };

        int mask = 0;
        for (int i = 0; i < offsets.Length; i++)
        {
            if (tilemap.HasTile(cell + offsets[i]))
                mask |= 1 << i;
        }
        return mask;
    }

    private static void IncrementNestedTileCount(
        Dictionary<int, Dictionary<TileBase, int>> countsByMask,
        int mask,
        TileBase tile)
    {
        if (!countsByMask.TryGetValue(mask, out Dictionary<TileBase, int> tileCounts))
        {
            tileCounts = new Dictionary<TileBase, int>();
            countsByMask.Add(mask, tileCounts);
        }

        IncrementTileCount(tileCounts, tile);
    }

    private static void IncrementTileCount(
        Dictionary<TileBase, int> counts,
        TileBase tile)
    {
        counts.TryGetValue(tile, out int count);
        counts[tile] = count + 1;
    }

    private static Dictionary<int, List<TileBase>> RankNestedTileCounts(
        IReadOnlyDictionary<int, Dictionary<TileBase, int>> countsByMask,
        int maximumCountPerMask)
    {
        Dictionary<int, List<TileBase>> result = new();
        foreach (KeyValuePair<int, Dictionary<TileBase, int>> pair in countsByMask)
            result.Add(pair.Key, RankTileCounts(pair.Value, maximumCountPerMask));
        return result;
    }

    private static void EnsurePaletteFallback(
        List<TileBase> target,
        IReadOnlyList<TileBase> generalCandidates,
        TileBase primary)
    {
        if (target.Count > 0)
            return;

        for (int i = 0; i < generalCandidates.Count; i++)
        {
            TileBase tile = generalCandidates[i];
            if (tile != null && !target.Contains(tile))
                target.Add(tile);
        }

        if (target.Count == 0 && primary != null)
            target.Add(primary);
    }

    private static StageMonsterSetSO LoadRequiredStageMonsterSet(string fileName)
    {
        string assetPath = $"{MonsterSpawnSetFolder}/{fileName}";
        StageMonsterSetSO monsterSet =
            AssetDatabase.LoadAssetAtPath<StageMonsterSetSO>(assetPath);
        if (monsterSet == null)
            throw new InvalidOperationException($"Missing stage monster set: {assetPath}");

        return monsterSet;
    }

    /// <summary>
    /// 책임:
    /// - 설치기와 마이그레이션이 Warrior, Mage, Tank enum 순서와 동일한 공용 진행도 세트를 사용하게 한다.
    /// </summary>
    private static StageMonsterSetSO[] LoadRoleStageMonsterSets()
    {
        return new[]
        {
            LoadRequiredStageMonsterSet("CommonMeleeStageMonsterSet.asset"),
            LoadRequiredStageMonsterSet("CommonRangedStageMonsterSet.asset"),
            LoadRequiredStageMonsterSet("CommonTankStageMonsterSet.asset")
        };
    }

    /// <summary>
    /// 책임:
    /// - 설치 사양의 테마 몬스터 경로를 검증된 Enemy 프리팹 목록으로 해석해 해당 룸 라이브러리에 저장한다.
    /// </summary>
    private static void ApplyStageMonsterCatalog(
        RoomThemeLibrarySO library,
        BossThemeInstallSpec spec)
    {
        if (library == null)
            throw new ArgumentNullException(nameof(library));

        var stageMonsterPrefabs = new List<GameObject>();
        IReadOnlyList<string> prefabPaths = spec.MonsterPrefabPaths;
        if (prefabPaths != null)
        {
            for (int prefabIndex = 0; prefabIndex < prefabPaths.Count; prefabIndex++)
            {
                stageMonsterPrefabs.Add(
                    LoadRequiredObjectPrefab(
                        prefabPaths[prefabIndex],
                        RoomObjectKind.Monster));
            }
        }

        library.EditorSetStageMonsterPrefabs(stageMonsterPrefabs);
        EditorUtility.SetDirty(library);
    }

    private static RoomThemeLibrarySO CreateOrUpdateBossThemeLibrary(
        BossThemeInstallSpec spec,
        BossThemeTilePalette tilePalette,
        GameObject chestPrefab,
        GameObject killLockChestPrefab,
        GameObject portalPrefab,
        GameObject sacrificeRewardAlcovePrefab)
    {
        TileBase floorTile = tilePalette.PrimaryFloor;
        TileBase wallTile = tilePalette.PrimaryWall;
        StageMonsterSetSO[] roleStageSets = LoadRoleStageMonsterSets();

        Vector2Int startSize = new(12, 8);
        Vector2Int compactCombatSize = new(10, 8);
        Vector2Int wideCombatSize = new(18, 8);
        Vector2Int tallCombatSize = new(10, 14);
        Vector2Int largeNieunSize = new(56, 56);
        Vector2Int largeGiyeokSize = new(60, 52);
        Vector2Int rewardSize = new(18, 12);
        Vector2Int bossSize = new(18, 12);

        RoomTemplateSO startRoom = CreateOrUpdateRoom(
            $"{spec.RoomFolder}/{spec.ThemeId}_Start.asset",
            $"{spec.ThemeId}_Start",
            RoomType.Start,
            startSize,
            CreateFourWaySockets(startSize),
            floorTile,
            wallTile,
            new List<RoomObjectPlacementData>
            {
                CreateObjectPlacement(
                    "StartChest",
                    RoomObjectKind.Chest,
                    chestPrefab,
                    GetRoomCenterCell(startSize))
            },
            0);
        RoomTemplateSO compactCombatRoom = CreateThemeCombatRoom(
            spec,
            "Combat_Compact",
            compactCombatSize,
            floorTile,
            wallTile,
            roleStageSets,
            killLockChestPrefab,
            new Vector2Int(3, 3),
            new Vector2Int(6, 4));
        RoomTemplateSO wideCombatRoom = CreateThemeCombatRoom(
            spec,
            "Combat_Wide",
            wideCombatSize,
            floorTile,
            wallTile,
            roleStageSets,
            killLockChestPrefab,
            new Vector2Int(4, 3),
            new Vector2Int(8, 5),
            new Vector2Int(12, 3),
            new Vector2Int(15, 5));
        RoomTemplateSO tallCombatRoom = CreateThemeCombatRoom(
            spec,
            "Combat_Tall",
            tallCombatSize,
            floorTile,
            wallTile,
            roleStageSets,
            killLockChestPrefab,
            new Vector2Int(3, 3),
            new Vector2Int(6, 5),
            new Vector2Int(3, 9),
            new Vector2Int(6, 11));
        RoomTemplateSO largeNieunRoom = CreateOrUpdateLargeCornerCombatRoom(
            spec,
            "Combat_Large_Nieun",
            largeNieunSize,
            LargeCornerRoomShape.Nieun,
            floorTile,
            wallTile,
            roleStageSets,
            killLockChestPrefab,
            new Vector2Int(8, 46),
            new Vector2Int(13, 36),
            new Vector2Int(7, 25),
            new Vector2Int(13, 13),
            new Vector2Int(24, 8),
            new Vector2Int(34, 13),
            new Vector2Int(44, 7),
            new Vector2Int(51, 13));
        RoomTemplateSO largeGiyeokRoom = CreateOrUpdateLargeCornerCombatRoom(
            spec,
            "Combat_Large_Giyeok",
            largeGiyeokSize,
            LargeCornerRoomShape.Giyeok,
            floorTile,
            wallTile,
            roleStageSets,
            killLockChestPrefab,
            new Vector2Int(8, 42),
            new Vector2Int(20, 47),
            new Vector2Int(32, 40),
            new Vector2Int(44, 46),
            new Vector2Int(50, 35),
            new Vector2Int(46, 25),
            new Vector2Int(53, 15),
            new Vector2Int(47, 7));
        RoomTemplateSO rewardRoom = CreateOrUpdateSacrificeRewardRoom(
            $"{spec.RoomFolder}/{spec.ThemeId}_Treasure_Sacrifice.asset",
            $"{spec.ThemeId}_Treasure_Sacrifice",
            rewardSize,
            floorTile,
            wallTile,
            sacrificeRewardAlcovePrefab);
        RoomTemplateSO bossRoom = CreateOrUpdateRoom(
            $"{spec.RoomFolder}/{spec.ThemeId}_Boss.asset",
            $"{spec.ThemeId}_Boss",
            RoomType.Boss,
            bossSize,
            CreateSingleSocket(bossSize, RoomSocketDirection.Left),
            floorTile,
            wallTile,
            new List<RoomObjectPlacementData>
            {
                CreateObjectPlacement(
                    "ExitPortal",
                    RoomObjectKind.Portal,
                    portalPrefab,
                    GetRoomCenterCell(bossSize))
            },
            1);

        RoomTemplateSO[] themedRooms =
        {
            startRoom,
            compactCombatRoom,
            wideCombatRoom,
            tallCombatRoom,
            largeNieunRoom,
            largeGiyeokRoom,
            rewardRoom,
            bossRoom
        };
        for (int roomIndex = 0; roomIndex < themedRooms.Length; roomIndex++)
        {
            ApplyThemeTilePalette(
                themedRooms[roomIndex],
                tilePalette,
                spec.PreferredSeed ^ unchecked(roomIndex * 486187739));
        }

        RoomThemeLibrarySO library = CreateOrUpdateLibrary(
            spec.LibraryPath,
            $"Procedural{spec.ThemeId}",
            startRoom,
            compactCombatRoom,
            wideCombatRoom,
            tallCombatRoom,
            largeNieunRoom,
            largeGiyeokRoom,
            rewardRoom,
            bossRoom);
        ApplyStageMonsterCatalog(library, spec);
        VerifyBossThemeLibrarySamples(library);
        VerifyBossThemeTilePalette(library, tilePalette);
        return library;
    }

    private static void ApplyThemeTilePalette(
        RoomTemplateSO template,
        BossThemeTilePalette palette,
        int seedSalt)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));

        RoomBuildData buildData = template.BuildData;
        List<RoomTileData> floorTiles = buildData.floorTiles ?? new List<RoomTileData>();
        for (int i = 0; i < floorTiles.Count; i++)
        {
            RoomTileData tileData = floorTiles[i];
            tileData.tile = ResolvePaletteVariant(
                palette.FloorVariants,
                palette.PrimaryFloor,
                tileData.localCell,
                seedSalt);
            floorTiles[i] = tileData;
        }

        List<RoomTileData> wallTiles = buildData.wallTiles ?? new List<RoomTileData>();
        HashSet<Vector2Int> wallCells = new();
        for (int i = 0; i < wallTiles.Count; i++)
            wallCells.Add(wallTiles[i].localCell);

        for (int i = 0; i < wallTiles.Count; i++)
        {
            RoomTileData tileData = wallTiles[i];
            int neighborMask = CalculateRoomNeighborMask(wallCells, tileData.localCell);
            tileData.tile = ResolveWallPaletteVariant(
                palette,
                neighborMask,
                tileData.localCell,
                seedSalt ^ 0x5A17C9E3);
            wallTiles[i] = tileData;
        }

        buildData.floorTiles = floorTiles;
        buildData.wallTiles = wallTiles;
        template.EditorSetData(template.LayoutData, buildData);
        EditorUtility.SetDirty(template);
    }

    private static int CalculateRoomNeighborMask(
        HashSet<Vector2Int> occupiedCells,
        Vector2Int cell)
    {
        Vector2Int[] offsets =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.up + Vector2Int.right,
            Vector2Int.down + Vector2Int.right,
            Vector2Int.down + Vector2Int.left,
            Vector2Int.up + Vector2Int.left
        };

        int mask = 0;
        for (int i = 0; i < offsets.Length; i++)
        {
            if (occupiedCells.Contains(cell + offsets[i]))
                mask |= 1 << i;
        }
        return mask;
    }

    private static TileBase ResolveWallPaletteVariant(
        BossThemeTilePalette palette,
        int neighborMask,
        Vector2Int cell,
        int seedSalt)
    {
        if (palette.WallVariantsByNeighborMask.TryGetValue(
                neighborMask,
                out List<TileBase> exactVariants) &&
            exactVariants.Count > 0)
        {
            return ResolvePaletteVariant(
                exactVariants,
                palette.PrimaryWall,
                cell,
                seedSalt);
        }

        int cardinalMask = neighborMask & 0x0F;
        if (palette.WallVariantsByCardinalMask.TryGetValue(
                cardinalMask,
                out List<TileBase> cardinalVariants) &&
            cardinalVariants.Count > 0)
        {
            return ResolvePaletteVariant(
                cardinalVariants,
                palette.PrimaryWall,
                cell,
                seedSalt);
        }

        bool isHorizontal = (cardinalMask & 0x0A) == 0x0A;
        bool isVertical = (cardinalMask & 0x05) == 0x05;
        IReadOnlyList<TileBase> directionalVariants = isHorizontal && !isVertical
            ? palette.HorizontalWallVariants
            : isVertical && !isHorizontal
                ? palette.VerticalWallVariants
                : palette.GeneralWallVariants;
        return ResolvePaletteVariant(
            directionalVariants,
            palette.PrimaryWall,
            cell,
            seedSalt);
    }

    private static TileBase ResolvePaletteVariant(
        IReadOnlyList<TileBase> variants,
        TileBase fallback,
        Vector2Int cell,
        int seedSalt)
    {
        if (variants == null || variants.Count == 0)
            return fallback;

        int index = ResolveStableVariantIndex(cell, seedSalt, variants.Count);
        TileBase selected = variants[index];
        return selected != null ? selected : fallback;
    }

    private static int ResolveStableVariantIndex(
        Vector2Int cell,
        int seedSalt,
        int variantCount)
    {
        unchecked
        {
            uint hash = (uint)(cell.x * 73856093);
            hash ^= (uint)(cell.y * 19349663);
            hash ^= (uint)(seedSalt * 83492791);
            hash ^= hash >> 16;
            return (int)(hash % (uint)variantCount);
        }
    }

    private static void VerifyBossThemeTilePalette(
        RoomThemeLibrarySO library,
        BossThemeTilePalette palette)
    {
        HashSet<TileBase> usedFloorTiles = new();
        HashSet<TileBase> usedWallTiles = new();
        for (int roomIndex = 0; roomIndex < library.Rooms.Count; roomIndex++)
        {
            RoomTemplateSO room = library.Rooms[roomIndex];
            if (room == null)
                continue;

            AddUsedTiles(room.BuildData.floorTiles, usedFloorTiles);
            AddUsedTiles(room.BuildData.wallTiles, usedWallTiles);
        }

        int requiredFloorCount = Mathf.Min(2, palette.FloorVariants.Count);
        int requiredWallCount = Mathf.Min(2, palette.GeneralWallVariants.Count);
        if (usedFloorTiles.Count < requiredFloorCount ||
            usedWallTiles.Count < requiredWallCount)
        {
            throw new InvalidOperationException(
                $"Theme room palette bake did not preserve tile variation. " +
                $"Floor={usedFloorTiles.Count}/{requiredFloorCount}, " +
                $"Wall={usedWallTiles.Count}/{requiredWallCount}");
        }

        Debug.Log(
            $"Verified theme room tile palette. " +
            $"UsedFloorTiles={usedFloorTiles.Count}, UsedWallTiles={usedWallTiles.Count}");
    }

    private static void AddUsedTiles(
        IReadOnlyList<RoomTileData> tileData,
        HashSet<TileBase> destination)
    {
        if (tileData == null)
            return;

        for (int i = 0; i < tileData.Count; i++)
        {
            if (tileData[i].tile != null)
                destination.Add(tileData[i].tile);
        }
    }

    private static RoomTemplateSO CreateThemeCombatRoom(
        BossThemeInstallSpec spec,
        string roomSuffix,
        Vector2Int roomSize,
        TileBase floorTile,
        TileBase wallTile,
        IReadOnlyList<StageMonsterSetSO> roleStageSets,
        GameObject killLockChestPrefab,
        params Vector2Int[] monsterCells)
    {
        string roomId = $"{spec.ThemeId}_{roomSuffix}";
        RoomTemplateSO room = CreateOrUpdateRoom(
            $"{spec.RoomFolder}/{roomId}.asset",
            roomId,
            RoomType.Combat,
            roomSize,
            CreateFourWaySockets(roomSize),
            floorTile,
            wallTile,
            CreateCombatPlacements(
                roomSuffix,
                roleStageSets,
                monsterCells,
                killLockChestPrefab,
            GetKillLockChestCell(roomSize)),
            0);
        return room;
    }

    private static RoomTemplateSO CreateOrUpdateLargeCornerCombatRoom(
        BossThemeInstallSpec spec,
        string roomSuffix,
        Vector2Int roomSize,
        LargeCornerRoomShape shape,
        TileBase floorTile,
        TileBase wallTile,
        IReadOnlyList<StageMonsterSetSO> roleStageSets,
        GameObject killLockChestPrefab,
        params Vector2Int[] monsterCells)
    {
        if (roomSize.x < 50 || roomSize.y < 50)
        {
            throw new InvalidOperationException(
                $"Large corner room must reserve at least 50x50 cells. Size={roomSize}");
        }

        string roomId = $"{spec.ThemeId}_{roomSuffix}";
        string assetPath = $"{spec.RoomFolder}/{roomId}.asset";
        RoomTemplateSO template = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(assetPath);
        if (template == null)
        {
            template = ScriptableObject.CreateInstance<RoomTemplateSO>();
            AssetDatabase.CreateAsset(template, assetPath);
        }

        List<RoomSocketData> sockets = CreateLargeCornerRoomSockets(roomSize, shape);
        CreateLargeCornerRoomTiles(
            roomSize,
            shape,
            floorTile,
            wallTile,
            out List<RoomTileData> floorTiles,
            out List<RoomTileData> wallTiles);
        for (int socketIndex = 0; socketIndex < sockets.Count; socketIndex++)
        {
            RoomSocketData socket = sockets[socketIndex];
            int width = RoomSocketGeometry.ResolveWidth(socket);
            for (int cellIndex = 0; cellIndex < width; cellIndex++)
            {
                Vector2Int socketCell = RoomSocketGeometry.GetLocalCell(socket, cellIndex);
                EnsureTileAtCell(floorTiles, socketCell, floorTile);
                EnsureTileAtCell(wallTiles, socketCell, wallTile);
            }
        }

        Vector2Int chestCell = shape == LargeCornerRoomShape.Nieun
            ? new Vector2Int(13, 8)
            : new Vector2Int(roomSize.x - 9, roomSize.y - 9);
        RoomLayoutData layout = new()
        {
            roomId = roomId,
            roomType = RoomType.Combat,
            size = roomSize,
            localBounds = new RectInt(Vector2Int.zero, roomSize),
            sockets = sockets,
            difficultyTier = 1,
            selectionWeight = 0.5f
        };
        RoomBuildData build = new()
        {
            floorTiles = floorTiles,
            wallTiles = wallTiles,
            objectPlacements = CreateCombatPlacements(
                roomSuffix,
                roleStageSets,
                monsterCells,
                killLockChestPrefab,
                chestCell)
        };
        template.EditorSetData(layout, build);
        template.name = roomId;
        EditorUtility.SetDirty(template);
        return template;
    }

    private static List<RoomObjectPlacementData> CreateCombatPlacements(
        string roomSuffix,
        IReadOnlyList<StageMonsterSetSO> roleStageSets,
        IReadOnlyList<Vector2Int> monsterCells,
        GameObject killLockChestPrefab,
        Vector2Int chestCell)
    {
        if (roleStageSets == null || roleStageSets.Count != 3)
            throw new InvalidOperationException("Theme Combat rooms require Warrior, Mage and Tank stage sets.");
        if (monsterCells == null || monsterCells.Count < 2)
            throw new InvalidOperationException("Theme Combat rooms require at least two monsters.");

        List<RoomObjectPlacementData> placements = new(monsterCells.Count + 1);
        for (int i = 0; i < monsterCells.Count; i++)
        {
            RoomMonsterSpawnRole role = (RoomMonsterSpawnRole)(i % roleStageSets.Count);
            RoomObjectPlacementData placement = CreateObjectPlacement(
                $"{roomSuffix}Monster_{i + 1}",
                RoomObjectKind.Monster,
                null,
                monsterCells[i],
                linkedChestLockPlacementId: "KillLockChest");
            placement.monsterSpawnRole = role;
            placement.monsterStageSet = roleStageSets[(int)role];
            placements.Add(placement);
        }

        placements.Add(CreateObjectPlacement(
            "KillLockChest",
            RoomObjectKind.Chest,
            killLockChestPrefab,
            chestCell));
        return placements;
    }

    private static List<RoomSocketData> CreateLargeCornerRoomSockets(
        Vector2Int roomSize,
        LargeCornerRoomShape shape)
    {
        int width = RoomSocketGeometry.RequiredWidth;
        return shape switch
        {
            LargeCornerRoomShape.Nieun => new List<RoomSocketData>
            {
                new() { socketId = "Up", localCell = new Vector2Int(8, roomSize.y - 1), direction = RoomSocketDirection.Up, width = width },
                new() { socketId = "Right", localCell = new Vector2Int(roomSize.x - 1, 8), direction = RoomSocketDirection.Right, width = width },
                new() { socketId = "Down", localCell = new Vector2Int(34, 0), direction = RoomSocketDirection.Down, width = width },
                new() { socketId = "Left", localCell = new Vector2Int(0, 36), direction = RoomSocketDirection.Left, width = width }
            },
            LargeCornerRoomShape.Giyeok => new List<RoomSocketData>
            {
                new() { socketId = "Up", localCell = new Vector2Int(22, roomSize.y - 1), direction = RoomSocketDirection.Up, width = width },
                new() { socketId = "Right", localCell = new Vector2Int(roomSize.x - 1, 24), direction = RoomSocketDirection.Right, width = width },
                new() { socketId = "Down", localCell = new Vector2Int(roomSize.x - 10, 0), direction = RoomSocketDirection.Down, width = width },
                new() { socketId = "Left", localCell = new Vector2Int(0, roomSize.y - 10), direction = RoomSocketDirection.Left, width = width }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null)
        };
    }

    private static void CreateLargeCornerRoomTiles(
        Vector2Int roomSize,
        LargeCornerRoomShape shape,
        TileBase floorTile,
        TileBase wallTile,
        out List<RoomTileData> floorTiles,
        out List<RoomTileData> wallTiles)
    {
        int legThickness = 18;
        HashSet<Vector2Int> footprint = new();
        for (int y = 0; y < roomSize.y; y++)
        {
            for (int x = 0; x < roomSize.x; x++)
            {
                bool included = shape switch
                {
                    LargeCornerRoomShape.Nieun => x < legThickness || y < legThickness,
                    LargeCornerRoomShape.Giyeok =>
                        x >= roomSize.x - legThickness || y >= roomSize.y - legThickness,
                    _ => false
                };
                if (included)
                    footprint.Add(new Vector2Int(x, y));
            }
        }

        floorTiles = new List<RoomTileData>();
        wallTiles = new List<RoomTileData>();
        Vector2Int[] neighbors =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };
        for (int y = 0; y < roomSize.y; y++)
        {
            for (int x = 0; x < roomSize.x; x++)
            {
                Vector2Int cell = new(x, y);
                if (!footprint.Contains(cell))
                    continue;

                bool isBoundary = false;
                for (int neighborIndex = 0; neighborIndex < neighbors.Length; neighborIndex++)
                {
                    if (footprint.Contains(cell + neighbors[neighborIndex]))
                        continue;

                    isBoundary = true;
                    break;
                }

                if (isBoundary)
                {
                    wallTiles.Add(new RoomTileData
                    {
                        localCell = cell,
                        tile = wallTile
                    });
                }
                else
                {
                    floorTiles.Add(new RoomTileData
                    {
                        localCell = cell,
                        tile = floorTile
                    });
                }
            }
        }
    }

    private static void VerifyBossThemeLibrarySamples(RoomThemeLibrarySO library)
    {
        if (library == null || library.Rooms == null || library.Rooms.Count != 8)
            throw new InvalidOperationException("Boss theme library must contain exactly eight room samples.");

        RoomTemplateSO largeNieunRoom = null;
        RoomTemplateSO largeGiyeokRoom = null;
        HashSet<RoomMonsterSpawnRole> monsterRoles = new();
        int totalMonsterCount = 0;
        for (int roomIndex = 0; roomIndex < library.Rooms.Count; roomIndex++)
        {
            RoomTemplateSO room = library.Rooms[roomIndex];
            if (room == null)
                continue;

            if (room.LayoutData.roomId.EndsWith("_Combat_Large_Nieun", StringComparison.Ordinal))
                largeNieunRoom = room;
            else if (room.LayoutData.roomId.EndsWith("_Combat_Large_Giyeok", StringComparison.Ordinal))
                largeGiyeokRoom = room;

            List<RoomObjectPlacementData> placements = room.BuildData.objectPlacements;
            if (placements == null)
                continue;

            for (int placementIndex = 0; placementIndex < placements.Count; placementIndex++)
            {
                RoomObjectPlacementData placement = placements[placementIndex];
                if (placement.kind != RoomObjectKind.Monster)
                    continue;

                totalMonsterCount++;
                if (placement.monsterStageSet == null)
                {
                    throw new InvalidOperationException(
                        $"Monster spawn '{placement.placementId}' is missing its role StageMonsterSetSO.");
                }

                monsterRoles.Add(placement.monsterSpawnRole);
            }
        }

        VerifyLargeCornerRoomSample(largeNieunRoom, "ㄴ");
        VerifyLargeCornerRoomSample(largeGiyeokRoom, "ㄱ");
        if (monsterRoles.Count != 3 || totalMonsterCount < 26)
        {
            throw new InvalidOperationException(
                $"Theme monster role population is invalid. " +
                $"Roles={monsterRoles.Count}/3, Count={totalMonsterCount}");
        }

        Debug.Log(
            $"Boss theme room samples verified. Theme={library.ThemeId}, " +
            $"Rooms={library.Rooms.Count}, MonsterRoles={monsterRoles.Count}, " +
            $"MonsterPlacements={totalMonsterCount}, LargeShapes=ㄴ/ㄱ");
    }

    private static void VerifyLargeCornerRoomSample(RoomTemplateSO room, string shapeLabel)
    {
        if (room == null ||
            room.LayoutData.localBounds.width < 50 ||
            room.LayoutData.localBounds.height < 50)
        {
            throw new InvalidOperationException(
                $"Missing 50x50-or-larger {shapeLabel}-shaped room sample.");
        }

        List<RoomTileData> floorTiles = room.BuildData.floorTiles;
        List<RoomTileData> wallTiles = room.BuildData.wallTiles;
        RectInt bounds = room.LayoutData.localBounds;
        Vector2Int cutoutCell = new(
            bounds.xMin + bounds.width / 2,
            bounds.yMin + bounds.height / 2);
        if (HasTileAtLocalCell(floorTiles, cutoutCell) ||
            HasTileAtLocalCell(wallTiles, cutoutCell))
        {
            throw new InvalidOperationException(
                $"Large {shapeLabel}-shaped room must keep its inner corner cutout empty.");
        }

        int monsterCount = 0;
        List<RoomObjectPlacementData> placements = room.BuildData.objectPlacements;
        for (int i = 0; placements != null && i < placements.Count; i++)
        {
            if (placements[i].kind == RoomObjectKind.Monster)
                monsterCount++;
        }

        if (monsterCount < 8)
        {
            throw new InvalidOperationException(
                $"Large {shapeLabel}-shaped room requires at least eight monsters.");
        }

        List<RoomSocketData> sockets = room.LayoutData.sockets;
        for (int socketIndex = 0; sockets != null && socketIndex < sockets.Count; socketIndex++)
        {
            RoomSocketData socket = sockets[socketIndex];
            int width = RoomSocketGeometry.ResolveWidth(socket);
            for (int cellIndex = 0; cellIndex < width; cellIndex++)
            {
                Vector2Int socketCell = RoomSocketGeometry.GetLocalCell(socket, cellIndex);
                if (!HasTileAtLocalCell(floorTiles, socketCell) ||
                    !HasTileAtLocalCell(wallTiles, socketCell))
                {
                    throw new InvalidOperationException(
                        $"Large {shapeLabel}-shaped room has an open-authored socket at {socketCell}.");
                }
            }
        }
    }

    /// <summary>
    /// 책임 : 기존 테마 생성 프로필은 그대로 보존하고, 최초 설치에서만 대표 Seed와 기본 생성 수치로 새 프로필을 만든다.
    /// </summary>
    private static DungeonGenerationProfileSO CreateOrLoadThemeGenerationProfile(
        BossThemeInstallSpec spec,
        RoomThemeLibrarySO library,
        DungeonLayoutPolicySO layoutPolicy)
    {
        DungeonGenerationProfileSO profile =
            AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(
                spec.GenerationProfilePath);
        profile ??= DungeonGenerationProfileAssetUtility.FindForLibrary(library);
        if (profile != null)
        {
            if (profile.RoomLibrary != library)
            {
                throw new InvalidOperationException(
                    $"Generation profile belongs to a different room library: " +
                    $"{AssetDatabase.GetAssetPath(profile)}");
            }

            return profile;
        }

        int resolvedSeed = ResolveThemeSeed(library, layoutPolicy, spec.PreferredSeed);
        profile = DungeonGenerationProfileAssetUtility.FindOrCreateForLibrary(
            library,
            layoutPolicy,
            resolvedSeed,
            ExplorationCorridorRoomCount,
            includeBossRoom: true,
            maxPlacementAttemptsPerRoom: 512,
            minimumCorridorLength: PrototypeMinimumCorridorLength,
            corridorLengthPerRoomCell: PrototypeCorridorLengthPerRoomCell,
            corridorLengthVariation: PrototypeCorridorLengthVariation);
        return profile;
    }

    private static int ResolveThemeSeed(
        RoomThemeLibrarySO library,
        DungeonLayoutPolicySO layoutPolicy,
        int preferredSeed)
    {
        const int searchCount = 4096;
        for (int offset = 0; offset < searchCount; offset++)
        {
            int seed = preferredSeed + offset;
            DungeonLayoutResult result = new DungeonGraphLayoutAssembler().Assemble(
                library,
                layoutPolicy,
                seed,
                ExplorationCorridorRoomCount,
                maxPlacementAttemptsPerRoom: 512,
                minimumCorridorLength: PrototypeMinimumCorridorLength,
                corridorLengthPerRoomCell: PrototypeCorridorLengthPerRoomCell,
                corridorLengthVariation: PrototypeCorridorLengthVariation);
            if (!IsRepresentativeThemeLayout(result))
                continue;

            Debug.Log($"Resolved representative seed for {library.ThemeId}: {seed}");
            return seed;
        }

        throw new InvalidOperationException(
            $"Could not find a representative seed for theme '{library.ThemeId}' " +
            $"within {searchCount} attempts.");
    }

    private static bool IsRepresentativeThemeLayout(DungeonLayoutResult result)
    {
        if (result == null ||
            !result.IsComplete ||
            result.Rooms.Count != ExplorationCorridorRoomCount ||
            !result.UsesGraphFirstLayout ||
            result.BossGraphDistance < 6 ||
            result.BossGraphDistance > 8 ||
            result.MeaningfulBranchCount < 2 ||
            result.MeaningfulBranchCount > 4 ||
            result.CycleConnectionCount < 1 ||
            result.CycleConnectionCount > 2 ||
            result.Connections.Count != result.Rooms.Count - 1 + result.CycleConnectionCount)
        {
            return false;
        }

        bool hasTreasureRoom = false;
        bool hasLargeNieunRoom = false;
        bool hasLargeGiyeokRoom = false;
        HashSet<Vector2Int> combatSizes = new();
        for (int i = 0; i < result.Rooms.Count; i++)
        {
            DungeonRoomPlacement placement = result.Rooms[i];
            RoomType roomType = placement.Template.LayoutData.roomType;
            if (roomType == RoomType.Treasure)
                hasTreasureRoom = true;
            else if (roomType == RoomType.Combat)
            {
                combatSizes.Add(placement.WorldBounds.size);
                string roomId = placement.Template.LayoutData.roomId;
                if (roomId.EndsWith("_Combat_Large_Nieun", StringComparison.Ordinal))
                    hasLargeNieunRoom = true;
                else if (roomId.EndsWith("_Combat_Large_Giyeok", StringComparison.Ordinal))
                    hasLargeGiyeokRoom = true;
            }
        }

        HashSet<int> corridorLengths = new();
        for (int i = 0; i < result.Connections.Count; i++)
            corridorLengths.Add(result.Connections[i].CorridorLength);

        return hasTreasureRoom &&
            hasLargeNieunRoom &&
            hasLargeGiyeokRoom &&
            combatSizes.Count >= 2 &&
            corridorLengths.Count >= 2;
    }

    private static void InstallBossThemeScene(
        BossThemeInstallSpec spec,
        RoomThemeLibrarySO library,
        DungeonGenerationProfileSO generationProfile,
        BossThemeTilePalette tilePalette,
        DoorObject doorPrefab)
    {
        if (doorPrefab == null)
            throw new InvalidOperationException($"Connected Door prefab is invalid: {DoorPrefabPath}");

        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(spec.TargetScenePath))
        {
            if (!AssetDatabase.CopyAsset(TargetScenePath, spec.TargetScenePath))
            {
                throw new InvalidOperationException(
                    $"Failed to create boss theme scene: {spec.TargetScenePath}");
            }

            AssetDatabase.ImportAsset(
                spec.TargetScenePath,
                ImportAssetOptions.ForceSynchronousImport);
        }

        Scene scene = EditorSceneManager.OpenScene(spec.TargetScenePath, OpenSceneMode.Single);
        library = AssetDatabase.LoadAssetAtPath<RoomThemeLibrarySO>(spec.LibraryPath);
        if (library == null)
            throw new InvalidOperationException($"Theme library is missing: {spec.LibraryPath}");
        string generationProfilePath = AssetDatabase.GetAssetPath(generationProfile);
        generationProfile =
            AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(generationProfilePath);
        if (generationProfile == null || generationProfile.RoomLibrary != library)
            throw new InvalidOperationException("Theme generation profile is missing after scene load.");

        SyncHubUiIntoScene(scene);
        InstallThemeSceneGimmicks(spec, scene);

        GameObject existingGeneratedRoot = FindInScene(scene, GeneratedRootName);
        if (existingGeneratedRoot != null)
            UnityEngine.Object.DestroyImmediate(existingGeneratedRoot);

        GameObject authoredGrid = FindInScene(scene, "Grid");
        if (authoredGrid != null)
            authoredGrid.SetActive(false);

        Vector2Int startSize = new(12, 8);
        GameObject playerSpawner = FindInScene(scene, "PlayerSpawner");
        Vector3 generatedOrigin = playerSpawner != null
            ? playerSpawner.transform.position -
                new Vector3(startSize.x * 0.5f, startSize.y * 0.5f, 0f)
            : Vector3.zero;

        GameObject generatedRoot = new(GeneratedRootName);
        generatedRoot.transform.position = generatedOrigin;

        GameObject gridObject = new("GeneratedGrid");
        gridObject.transform.SetParent(generatedRoot.transform, false);
        gridObject.AddComponent<Grid>();

        Tilemap underFloor = CreateRuntimeTilemap(
            gridObject.transform,
            RoomTileLayerKind.UnderFloor);
        Tilemap floor = CreateRuntimeTilemap(gridObject.transform, RoomTileLayerKind.Floor);
        Tilemap floorDetail = CreateRuntimeTilemap(
            gridObject.transform,
            RoomTileLayerKind.FloorDetail);
        Tilemap groundDecoration = CreateRuntimeTilemap(
            gridObject.transform,
            RoomTileLayerKind.GroundDecoration);
        Tilemap wall = CreateRuntimeTilemap(gridObject.transform, RoomTileLayerKind.Wall);
        Tilemap wallDetail = CreateRuntimeTilemap(
            gridObject.transform,
            RoomTileLayerKind.WallDetail);
        Tilemap foreground = CreateRuntimeTilemap(
            gridObject.transform,
            RoomTileLayerKind.Foreground);
        Tilemap overlayFx = CreateRuntimeTilemap(
            gridObject.transform,
            RoomTileLayerKind.OverlayFX);

        GameObject doorRootObject = new("GeneratedDoors");
        doorRootObject.transform.SetParent(generatedRoot.transform, false);
        GameObject socketBlockerRootObject = new("GeneratedSocketBlockers");
        socketBlockerRootObject.transform.SetParent(generatedRoot.transform, false);
        GameObject objectRootObject = new("GeneratedRoomObjects");
        objectRootObject.transform.SetParent(generatedRoot.transform, false);
        GameObject encounterRootObject = new("GeneratedRoomEncounters");
        encounterRootObject.transform.SetParent(generatedRoot.transform, false);

        DungeonRoomBuilder builder = generatedRoot.AddComponent<DungeonRoomBuilder>();
        builder.EditorAssignTilemaps(
            underFloor,
            floor,
            floorDetail,
            groundDecoration,
            wall,
            wallDetail,
            foreground,
            overlayFx);
        builder.EditorAssignCorridorTiles(
            tilePalette.PrimaryFloor,
            tilePalette.PrimaryWall);
        builder.EditorAssignCorridorTilePalette(
            tilePalette.FloorVariants,
            tilePalette.HorizontalWallVariants,
            tilePalette.VerticalWallVariants);
        builder.EditorAssignConnectedDoorSetup(
            doorPrefab,
            doorRootObject.transform,
            shouldOpenInitially: true);
        builder.EditorAssignSocketBlockerRoot(socketBlockerRootObject.transform);
        builder.EditorAssignObjectRoot(objectRootObject.transform);
        builder.EditorAssignEncounterRoot(encounterRootObject.transform);

        DungeonGenerator generator = generatedRoot.AddComponent<DungeonGenerator>();
        generator.EditorConfigure(
            library,
            builder,
            generationProfile.Seed,
            generationProfile.RoomCount,
            generationProfile.IncludeBossRoom,
            generationProfile.MaxPlacementAttemptsPerRoom,
            generationProfile.MinimumCorridorLength,
            generationProfile.CorridorLengthPerRoomCell,
            generationProfile.CorridorLengthVariation,
            true,
            generationProfile.LayoutPolicy,
            generationProfile);
        EditorUtility.SetDirty(builder);
        EditorUtility.SetDirty(generator);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        VerifyInstalledPipeline(generator, builder);
        builder.ClearGeneratedContent();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"Installed {spec.ThemeId} procedural Corridor scene. " +
            $"Scene={spec.TargetScenePath}, Profile={generationProfile.name}, " +
            $"Seed={generationProfile.Seed}",
            generatedRoot);
    }

    private static void VerifyPrototypeSceneShell(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("The procedural test Corridor scene must be loaded.");

        GameObject[] roots = scene.GetRootGameObjects();
        int monsterSpawnerCount = 0;
        int playerSpawnerCount = 0;
        int authoredMonsterSpawnPointCount = 0;
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            GameObject root = roots[rootIndex];
            authoredMonsterSpawnPointCount +=
                root.GetComponentsInChildren<MonsterSpawnContainer>(true).Length;
            monsterSpawnerCount += root.GetComponentsInChildren<MonsterSpawner>(true).Length;
            playerSpawnerCount += root.GetComponentsInChildren<PlayerSpawner>(true).Length;
        }

        if (monsterSpawnerCount != 1 ||
            playerSpawnerCount != 1 ||
            authoredMonsterSpawnPointCount != 0)
        {
            throw new InvalidOperationException(
                $"Procedural test Corridor shell is invalid. " +
                $"MonsterSpawner={monsterSpawnerCount}, PlayerSpawner={playerSpawnerCount}, " +
                $"AuthoredMonsterSpawnPoints={authoredMonsterSpawnPointCount}");
        }

        Debug.Log(
            $"Verified procedural test Corridor shell. Roots={roots.Length}, " +
            $"MonsterSpawner={monsterSpawnerCount}, PlayerSpawner={playerSpawnerCount}, " +
            $"AuthoredMonsterSpawnPoints={authoredMonsterSpawnPointCount}");
    }

    private static void InstallThemeSceneGimmicks(
        BossThemeInstallSpec spec,
        Scene scene)
    {
        GameObject existingVisionMaskRoot = FindInScene(scene, GlobalVisionMaskRootName);
        if (!string.Equals(spec.ThemeId, "Shadow", StringComparison.Ordinal))
        {
            if (existingVisionMaskRoot != null &&
                IsPrefabInstanceOf(existingVisionMaskRoot, ShadowVisionMaskPrefabPath))
            {
                UnityEngine.Object.DestroyImmediate(existingVisionMaskRoot);
            }

            return;
        }

        if (existingVisionMaskRoot != null)
            UnityEngine.Object.DestroyImmediate(existingVisionMaskRoot);

        GameObject visionMaskPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(ShadowVisionMaskPrefabPath);
        if (visionMaskPrefab == null ||
            visionMaskPrefab.GetComponent<GlobalVisionMaskController>() == null ||
            visionMaskPrefab.GetComponent<SceneRestrictedVisionController>() == null)
        {
            throw new InvalidOperationException(
                $"Shadow darkness prefab is missing or invalid: {ShadowVisionMaskPrefabPath}");
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(visionMaskPrefab, scene) as GameObject;
        if (instance == null)
            throw new InvalidOperationException("Failed to instantiate the Shadow darkness prefab.");

        instance.name = GlobalVisionMaskRootName;
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        instance.transform.localScale = Vector3.one;
        EditorUtility.SetDirty(instance);
        VerifyShadowDarknessGimmick(scene, instance);
    }

    private static bool IsPrefabInstanceOf(GameObject instance, string prefabPath)
    {
        if (instance == null)
            return false;

        GameObject source =
            PrefabUtility.GetCorrespondingObjectFromOriginalSource(instance);
        return source != null &&
            string.Equals(
                AssetDatabase.GetAssetPath(source),
                prefabPath,
                StringComparison.Ordinal);
    }

    private static void VerifyShadowDarknessGimmick(Scene scene, GameObject expectedRoot)
    {
        int globalControllerCount = 0;
        int sceneControllerCount = 0;
        GlobalVisionMaskController globalController = null;
        SceneRestrictedVisionController sceneController = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            GlobalVisionMaskController[] globalControllers =
                roots[rootIndex].GetComponentsInChildren<GlobalVisionMaskController>(true);
            SceneRestrictedVisionController[] sceneControllers =
                roots[rootIndex].GetComponentsInChildren<SceneRestrictedVisionController>(true);
            globalControllerCount += globalControllers.Length;
            sceneControllerCount += sceneControllers.Length;
            if (globalControllers.Length > 0)
                globalController = globalControllers[0];
            if (sceneControllers.Length > 0)
                sceneController = sceneControllers[0];
        }

        if (expectedRoot == null ||
            !IsPrefabInstanceOf(expectedRoot, ShadowVisionMaskPrefabPath) ||
            globalControllerCount != 1 ||
            sceneControllerCount != 1 ||
            globalController == null ||
            sceneController == null ||
            !globalController.enabled ||
            !sceneController.enabled ||
            sceneController.RestrictedVisionDefinition == null)
        {
            throw new InvalidOperationException(
                $"Procedural Shadow scene has an invalid darkness controller setup. " +
                $"GlobalControllers={globalControllerCount}, SceneControllers={sceneControllerCount}");
        }

        SerializedObject serializedGlobalController = new(globalController);
        SerializedProperty darkMaskRoot = serializedGlobalController.FindProperty("darkMaskRoot");
        SerializedProperty playerVisionMaskPrefab =
            serializedGlobalController.FindProperty("playerVisionMaskPrefab");
        SerializedProperty defaultOverlayAlpha =
            serializedGlobalController.FindProperty("defaultOverlayAlpha");
        SerializedProperty fogOverlayAlpha =
            serializedGlobalController.FindProperty("fogOverlayAlpha");
        if (darkMaskRoot == null ||
            darkMaskRoot.objectReferenceValue == null ||
            playerVisionMaskPrefab == null ||
            playerVisionMaskPrefab.objectReferenceValue == null ||
            defaultOverlayAlpha == null ||
            defaultOverlayAlpha.floatValue < 0.75f ||
            fogOverlayAlpha == null ||
            fogOverlayAlpha.floatValue < 0.99f)
        {
            throw new InvalidOperationException(
                "Procedural Shadow darkness prefab lost its overlay or player mask configuration.");
        }

        GameObject darkMaskObject = darkMaskRoot.objectReferenceValue as GameObject;
        SpriteRenderer overlayRenderer = darkMaskObject != null
            ? darkMaskObject.GetComponentInChildren<SpriteRenderer>(true)
            : null;
        if (overlayRenderer == null ||
            overlayRenderer.maskInteraction != SpriteMaskInteraction.VisibleOutsideMask)
        {
            throw new InvalidOperationException(
                "Procedural Shadow darkness overlay must render outside the player vision mask.");
        }

        Debug.Log(
            $"Shadow darkness gimmick verified. " +
            $"Status={sceneController.RestrictedVisionDefinition.StatusId}, " +
            $"BaseAlpha={defaultOverlayAlpha.floatValue:0.00}, " +
            $"FogAlpha={fogOverlayAlpha.floatValue:0.00}",
            expectedRoot);
    }

    private static void UpdateRouteSetCorridorScene(BossThemeInstallSpec spec)
    {
        CorridorBossRouteSetSO routeSet =
            AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(spec.RouteSetPath);
        if (routeSet == null)
            throw new InvalidOperationException($"Route set is missing: {spec.RouteSetPath}");

        SerializedObject serializedRouteSet = new(routeSet);
        SerializedProperty corridorSceneName =
            serializedRouteSet.FindProperty("corridorSceneName");
        if (corridorSceneName == null)
            throw new InvalidOperationException("CorridorBossRouteSetSO serialization contract changed.");

        corridorSceneName.stringValue = spec.TargetSceneName;
        serializedRouteSet.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(routeSet);
    }

    /// <summary>
    /// 책임 : 최종보스 RouteSet이 일반 보스용 절차 복도가 아니라 전투 없는 고정 휴식 복도를 유지하게 한다.
    /// </summary>
    private static void RestoreFinalDemonKingRestCorridorRoute()
    {
        CorridorBossRouteSetSO routeSet =
            AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(FinalDemonKingRouteSetPath);
        if (routeSet == null)
            throw new InvalidOperationException($"Final DemonKing route set is missing: {FinalDemonKingRouteSetPath}");

        SerializedObject serializedRouteSet = new(routeSet);
        SerializedProperty corridorSceneName =
            serializedRouteSet.FindProperty("corridorSceneName");
        if (corridorSceneName == null)
            throw new InvalidOperationException("CorridorBossRouteSetSO serialization contract changed.");

        corridorSceneName.stringValue = FinalDemonKingCorridorSceneName;
        serializedRouteSet.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(routeSet);
    }

    /// <summary>
    /// 책임 : 런의 final RouteSet, Build Settings와 고정 휴식 씬의 무전투·보스행 포탈 계약을 함께 검증한다.
    /// </summary>
    private static void VerifyFinalDemonKingRestCorridorRoute()
    {
        RunRouteCatalogSO catalog =
            AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(RunRouteCatalogPath);
        CorridorBossRouteSetSO routeSet =
            AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(FinalDemonKingRouteSetPath);
        if (catalog == null ||
            routeSet == null ||
            catalog.FinalRouteSet != routeSet ||
            !string.Equals(
                routeSet.CorridorSceneName,
                FinalDemonKingCorridorSceneName,
                StringComparison.Ordinal) ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(FinalDemonKingCorridorScenePath) == null)
        {
            throw new InvalidOperationException(
                "The final DemonKing RouteSet is not bound to its fixed rest Corridor scene.");
        }

        bool buildSceneEnabled = false;
        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        for (int i = 0; i < buildScenes.Length; i++)
        {
            if (buildScenes[i].enabled &&
                string.Equals(
                    buildScenes[i].path,
                    FinalDemonKingCorridorScenePath,
                    StringComparison.Ordinal))
            {
                buildSceneEnabled = true;
                break;
            }
        }

        if (!buildSceneEnabled)
            throw new InvalidOperationException("The fixed DemonKing rest Corridor is disabled in Build Settings.");

        if (IsBuildSceneEnabled(RetiredProceduralDemonKingCorridorScenePath))
        {
            throw new InvalidOperationException(
                "The retired procedural DemonKing Corridor must remain disabled in Build Settings.");
        }

        VerifyRetiredConnectionDisabled(RetiredDemonKingLobbyConnectionPath);
        VerifyRetiredConnectionDisabled(RetiredDemonKingBossConnectionPath);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene restScene = EditorSceneManager.OpenScene(
            FinalDemonKingCorridorScenePath,
            OpenSceneMode.Additive);
        try
        {
            int authoredSpawnCount = 0;
            bool hasCorridorToBossPortal = false;
            GameObject[] roots = restScene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                authoredSpawnCount +=
                    root.GetComponentsInChildren<MonsterSpawnContainer>(true).Length;

                MonsterSpawner[] spawners = root.GetComponentsInChildren<MonsterSpawner>(true);
                for (int spawnerIndex = 0; spawnerIndex < spawners.Length; spawnerIndex++)
                {
                    SerializedObject serializedSpawner = new(spawners[spawnerIndex]);
                    SerializedProperty spawnPoints = serializedSpawner.FindProperty("spawnPoints");
                    SerializedProperty spawnRooms = serializedSpawner.FindProperty("spawnRooms");
                    authoredSpawnCount += spawnPoints?.arraySize ?? 0;
                    authoredSpawnCount += spawnRooms?.arraySize ?? 0;
                }

                ScenePortal[] portals = root.GetComponentsInChildren<ScenePortal>(true);
                for (int portalIndex = 0; portalIndex < portals.Length; portalIndex++)
                {
                    if (portals[portalIndex].PortalTransitionType == TransitionType.CorridorToBoss)
                    {
                        hasCorridorToBossPortal = true;
                        break;
                    }
                }
            }

            if (authoredSpawnCount != 0 || !hasCorridorToBossPortal)
            {
                throw new InvalidOperationException(
                    $"Fixed DemonKing rest Corridor contract failed. " +
                    $"CombatSpawns={authoredSpawnCount}, HasBossPortal={hasCorridorToBossPortal}");
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(restScene, true);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
        }

        Debug.Log(
            $"Verified final DemonKing fixed rest Corridor route. " +
            $"Scene={FinalDemonKingCorridorSceneName}, CombatSpawns=0, HasBossPortal=True, " +
            "RetiredProceduralRouteDisabled=True.");
    }

    /// <summary>
    /// 책임:
    /// - 폐기된 절차 DemonKing 씬을 Build Settings에서 비활성화한다.
    /// - 남아 있는 레거시 Lobby·Boss 연결 에셋의 양방향 이동을 꺼 정식 고정 휴식 복도와 충돌하지 않게 한다.
    /// </summary>
    private static void DisableRetiredDemonKingProceduralRoute()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        bool buildSettingsChanged = false;
        for (int index = 0; index < scenes.Length; index++)
        {
            if (!string.Equals(
                    scenes[index].path,
                    RetiredProceduralDemonKingCorridorScenePath,
                    StringComparison.Ordinal) ||
                !scenes[index].enabled)
            {
                continue;
            }

            scenes[index] = new EditorBuildSettingsScene(
                RetiredProceduralDemonKingCorridorScenePath,
                false);
            buildSettingsChanged = true;
        }

        if (buildSettingsChanged)
            EditorBuildSettings.scenes = scenes;

        DisableRetiredConnection(RetiredDemonKingLobbyConnectionPath);
        DisableRetiredConnection(RetiredDemonKingBossConnectionPath);
    }

    /// <summary>
    /// 책임 : 레거시 연결 에셋을 삭제하지 않고 양방향만 비활성화해 참조 자산을 안전하게 보관한다.
    /// </summary>
    private static void DisableRetiredConnection(string assetPath)
    {
        SceneConnectionSO retiredConnection =
            AssetDatabase.LoadAssetAtPath<SceneConnectionSO>(assetPath);
        if (retiredConnection == null)
            return;

        var serializedConnection = new SerializedObject(retiredConnection);
        serializedConnection.Update();
        serializedConnection.FindProperty("aToB")
            .FindPropertyRelative("enabled").boolValue = false;
        serializedConnection.FindProperty("bToA")
            .FindPropertyRelative("enabled").boolValue = false;
        serializedConnection.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(retiredConnection);
    }

    /// <summary>
    /// 책임 : 지정한 씬이 현재 Player Build 대상에 활성 상태로 남아 있는지 판정한다.
    /// </summary>
    private static bool IsBuildSceneEnabled(string scenePath)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int index = 0; index < scenes.Length; index++)
        {
            if (scenes[index].enabled &&
                string.Equals(scenes[index].path, scenePath, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 책임 : 보존 중인 레거시 연결 에셋이 다시 활성화되어 정식 DemonKing 동선을 우회하지 않는지 검증한다.
    /// </summary>
    private static void VerifyRetiredConnectionDisabled(string assetPath)
    {
        SceneConnectionSO retiredConnection =
            AssetDatabase.LoadAssetAtPath<SceneConnectionSO>(assetPath);
        if (retiredConnection != null &&
            (retiredConnection.AToB.Enabled || retiredConnection.BToA.Enabled))
        {
            throw new InvalidOperationException(
                $"Retired DemonKing connection must remain disabled: {assetPath}");
        }
    }

    private static void EnsureSceneInBuildSettings(string scenePath)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (!string.Equals(scenes[i].path, scenePath, StringComparison.Ordinal))
                continue;

            if (!scenes[i].enabled)
            {
                scenes[i] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = scenes;
            }

            return;
        }

        Array.Resize(ref scenes, scenes.Length + 1);
        scenes[scenes.Length - 1] = new EditorBuildSettingsScene(scenePath, true);
        EditorBuildSettings.scenes = scenes;
    }

    private static void VerifyBossThemeRoutesAndHubDemonKingPortal(BossThemeInstallSpec[] specs)
    {
        RunRouteCatalogSO catalog =
            AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(RunRouteCatalogPath);
        if (catalog == null)
            throw new InvalidOperationException($"Run route catalog is missing: {RunRouteCatalogPath}");

        HashSet<CorridorBossRouteSetSO> catalogRouteSets = new();
        IReadOnlyList<CorridorBossRouteSetSO> normalRouteSets = catalog.NormalRouteSets;
        if (normalRouteSets != null)
        {
            for (int i = 0; i < normalRouteSets.Count; i++)
            {
                if (normalRouteSets[i] != null)
                    catalogRouteSets.Add(normalRouteSets[i]);
            }
        }

        if (catalog.FinalRouteSet != null)
            catalogRouteSets.Add(catalog.FinalRouteSet);

        HashSet<string> enabledBuildScenes = new(StringComparer.Ordinal);
        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        for (int i = 0; i < buildScenes.Length; i++)
        {
            if (buildScenes[i].enabled)
                enabledBuildScenes.Add(buildScenes[i].path);
        }

        for (int i = 0; i < specs.Length; i++)
        {
            BossThemeInstallSpec spec = specs[i];
            CorridorBossRouteSetSO routeSet =
                AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(spec.RouteSetPath);
            if (routeSet == null ||
                !catalogRouteSets.Contains(routeSet) ||
                !routeSet.IsValid ||
                !string.Equals(
                    routeSet.CorridorSceneName,
                    spec.TargetSceneName,
                    StringComparison.Ordinal) ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(spec.TargetScenePath) == null ||
                !enabledBuildScenes.Contains(spec.TargetScenePath))
            {
                throw new InvalidOperationException(
                    $"Theme route is not completely connected: {spec.ThemeId}");
            }
        }

        DemonKingHubPortalInstaller.Validate();

        Debug.Log(
            $"Verified {specs.Length} normal themed Corridor routes and " +
            "ProtoTypeHub ScenePortal -> fixed DemonkingCorridor.");
    }

    [MenuItem("Tools/Dungeon/Sync Hub UI To V0 Test Scene")]
    public static void SyncHubUiToPrototypeTestScene()
    {
        Scene targetScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        SyncHubUiIntoScene(targetScene);
        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorSceneManager.SaveScene(targetScene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Hub UI settings synchronized to: {TargetScenePath}");
    }

    [MenuItem("Tools/Dungeon/Install V0 Prototype Corridor Test Scene")]
    public static void InstallPrototypeCorridorTestScene()
    {
        RoomTemplateSO sourceRoom = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(SourceRoomPath);
        if (sourceRoom == null)
            throw new InvalidOperationException($"Source room template is missing: {SourceRoomPath}");

        GameObject monsterPrefab = LoadRequiredObjectPrefab(
            MonsterPrefabPath,
            RoomObjectKind.Monster);
        GameObject chestPrefab = LoadRequiredObjectPrefab(
            ChestPrefabPath,
            RoomObjectKind.Chest);
        GameObject statuePrefab = LoadRequiredObjectPrefab(
            StatuePrefabPath,
            RoomObjectKind.Prop);
        GameObject doorPrefabObject = LoadRequiredObjectPrefab(
            DoorPrefabPath,
            RoomObjectKind.Prop);
        GameObject killLockChestPrefab = LoadConfiguredRoomObjectPrefab(
            GeneratedRoomFolder + "/Prototype_Combat.asset",
            "KillLockChest",
            RoomObjectKind.Chest,
            requireChestKillLock: true);
        GameObject portalPrefab = LoadRequiredObjectPrefab(
            PortalPrefabPath,
            RoomObjectKind.Portal);

        EnsureFolder(GeneratedRoomFolder);
        EnsureFolder(GeneratedProceduralPrefabFolder);
        EnsureFolder(LibraryFolder);

        GameObject sacrificeRewardAlcovePrefab =
            CreateOrUpdateSacrificeRewardAlcovePrefab(
                statuePrefab,
                doorPrefabObject,
                chestPrefab);

        Vector2Int startSize = new(12, 8);
        Vector2Int compactCombatSize = new(10, 8);
        Vector2Int wideCombatSize = new(18, 8);
        Vector2Int tallCombatSize = new(10, 14);
        Vector2Int sacrificeRewardSize = new(18, 12);
        Vector2Int bossSize = new(18, 12);
        RoomBuildData sourceBuild = sourceRoom.BuildData;
        TileBase fallbackFloor = FindFirstTile(sourceBuild.floorTiles);
        TileBase fallbackWall = FindFirstTile(sourceBuild.wallTiles);
        if (fallbackFloor == null)
            throw new InvalidOperationException("TestTypeStart requires at least one Floor tile.");
        if (fallbackWall == null)
            throw new InvalidOperationException("TestTypeStart requires at least one Wall tile.");

        RoomTemplateSO startRoom = CreateOrUpdateRoom(
            GeneratedRoomFolder + "/Prototype_Start.asset",
            "Prototype_Start",
            RoomType.Start,
            startSize,
            CreateFourWaySockets(startSize),
            fallbackFloor,
            fallbackWall,
            new List<RoomObjectPlacementData>
            {
                CreateObjectPlacement(
                    "StartChest",
                    RoomObjectKind.Chest,
                    chestPrefab,
                    GetRoomCenterCell(startSize))
            },
            0);
        RoomTemplateSO compactCombatRoom = CreateOrUpdateRoom(
            GeneratedRoomFolder + "/Prototype_Combat.asset",
            "Prototype_Combat",
            RoomType.Combat,
            compactCombatSize,
            CreateFourWaySockets(compactCombatSize),
            fallbackFloor,
            fallbackWall,
            new List<RoomObjectPlacementData>
            {
                CreateObjectPlacement(
                    "CombatGoblin",
                    RoomObjectKind.Monster,
                    monsterPrefab,
                    GetRoomCenterCell(compactCombatSize),
                    linkedChestLockPlacementId: "KillLockChest"),
                CreateObjectPlacement(
                    "KillLockChest",
                    RoomObjectKind.Chest,
                    killLockChestPrefab,
                    GetKillLockChestCell(compactCombatSize))
            },
            0);
        RoomTemplateSO wideCombatRoom = CreateOrUpdateRoom(
            GeneratedRoomFolder + "/Prototype_Combat_Wide.asset",
            "Prototype_Combat_Wide",
            RoomType.Combat,
            wideCombatSize,
            CreateFourWaySockets(wideCombatSize),
            fallbackFloor,
            fallbackWall,
            new List<RoomObjectPlacementData>
            {
                CreateObjectPlacement(
                    "CombatGoblin",
                    RoomObjectKind.Monster,
                    monsterPrefab,
                    GetRoomCenterCell(wideCombatSize),
                    linkedChestLockPlacementId: "KillLockChest"),
                CreateObjectPlacement(
                    "KillLockChest",
                    RoomObjectKind.Chest,
                    killLockChestPrefab,
                    GetKillLockChestCell(wideCombatSize))
            },
            0);
        RoomTemplateSO tallCombatRoom = CreateOrUpdateRoom(
            GeneratedRoomFolder + "/Prototype_Combat_Tall.asset",
            "Prototype_Combat_Tall",
            RoomType.Combat,
            tallCombatSize,
            CreateFourWaySockets(tallCombatSize),
            fallbackFloor,
            fallbackWall,
            new List<RoomObjectPlacementData>
            {
                CreateObjectPlacement(
                    "CombatGoblin",
                    RoomObjectKind.Monster,
                    monsterPrefab,
                    GetRoomCenterCell(tallCombatSize),
                    linkedChestLockPlacementId: "KillLockChest"),
                CreateObjectPlacement(
                    "KillLockChest",
                    RoomObjectKind.Chest,
                    killLockChestPrefab,
                    GetKillLockChestCell(tallCombatSize))
            },
            0);
        RoomTemplateSO sacrificeRewardRoom = CreateOrUpdateSacrificeRewardRoom(
            GeneratedRoomFolder + "/Prototype_Treasure_Sacrifice.asset",
            "Prototype_Treasure_Sacrifice",
            sacrificeRewardSize,
            fallbackFloor,
            fallbackWall,
            sacrificeRewardAlcovePrefab);
        RoomTemplateSO bossRoom = CreateOrUpdateRoom(
            GeneratedRoomFolder + "/Prototype_Boss.asset",
            "Prototype_Boss",
            RoomType.Boss,
            bossSize,
            CreateSingleSocket(bossSize, RoomSocketDirection.Left),
            fallbackFloor,
            fallbackWall,
            new List<RoomObjectPlacementData>
            {
                CreateObjectPlacement(
                    "ExitPortal",
                    RoomObjectKind.Portal,
                    portalPrefab,
                    GetRoomCenterCell(bossSize))
            },
            1);

        RoomThemeLibrarySO library = CreateOrUpdateLibrary(
            LibraryPath,
            "PrototypeCorridorV0",
            startRoom,
            compactCombatRoom,
            wideCombatRoom,
            tallCombatRoom,
            sacrificeRewardRoom,
            bossRoom);
        library.EditorSetStageMonsterPrefabs(new[] { monsterPrefab });
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(
            LibraryPath,
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        library = AssetDatabase.LoadAssetAtPath<RoomThemeLibrarySO>(LibraryPath);
        if (library == null)
            throw new InvalidOperationException($"Failed to reload room library: {LibraryPath}");

        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath))
        {
            throw new InvalidOperationException(
                $"Dedicated procedural test scene is missing: {TargetScenePath}");
        }

        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        library = AssetDatabase.LoadAssetAtPath<RoomThemeLibrarySO>(LibraryPath);
        if (library == null)
            throw new InvalidOperationException($"Failed to reload room library after opening scene: {LibraryPath}");

        VerifyPrototypeSceneShell(scene);
        SyncHubUiIntoScene(scene);

        DoorObject doorPrefab = doorPrefabObject != null
            ? doorPrefabObject.GetComponent<DoorObject>()
            : null;
        if (doorPrefab == null)
            throw new InvalidOperationException($"Connected Door prefab is missing or invalid: {DoorPrefabPath}");

        GameObject existingGeneratedRoot = FindInScene(scene, GeneratedRootName);
        if (existingGeneratedRoot != null)
            UnityEngine.Object.DestroyImmediate(existingGeneratedRoot);

        GameObject playerSpawner = FindInScene(scene, "PlayerSpawner");
        Vector3 generatedOrigin = playerSpawner != null
            ? playerSpawner.transform.position -
                new Vector3(startSize.x * 0.5f, startSize.y * 0.5f, 0f)
            : Vector3.zero;

        GameObject generatedRoot = new(GeneratedRootName);
        generatedRoot.transform.position = generatedOrigin;

        GameObject gridObject = new("GeneratedGrid");
        gridObject.transform.SetParent(generatedRoot.transform, false);
        gridObject.AddComponent<Grid>();

        Tilemap underFloor = CreateRuntimeTilemap(
            gridObject.transform,
            RoomTileLayerKind.UnderFloor);
        Tilemap floor = CreateRuntimeTilemap(gridObject.transform, RoomTileLayerKind.Floor);
        Tilemap floorDetail = CreateRuntimeTilemap(
            gridObject.transform,
            RoomTileLayerKind.FloorDetail);
        Tilemap groundDecoration = CreateRuntimeTilemap(
            gridObject.transform,
            RoomTileLayerKind.GroundDecoration);
        Tilemap wall = CreateRuntimeTilemap(gridObject.transform, RoomTileLayerKind.Wall);
        Tilemap wallDetail = CreateRuntimeTilemap(
            gridObject.transform,
            RoomTileLayerKind.WallDetail);
        Tilemap foreground = CreateRuntimeTilemap(
            gridObject.transform,
            RoomTileLayerKind.Foreground);
        Tilemap overlayFx = CreateRuntimeTilemap(
            gridObject.transform,
            RoomTileLayerKind.OverlayFX);

        GameObject doorRootObject = new("GeneratedDoors");
        doorRootObject.transform.SetParent(generatedRoot.transform, false);

        GameObject socketBlockerRootObject = new("GeneratedSocketBlockers");
        socketBlockerRootObject.transform.SetParent(generatedRoot.transform, false);

        GameObject objectRootObject = new("GeneratedRoomObjects");
        objectRootObject.transform.SetParent(generatedRoot.transform, false);

        GameObject encounterRootObject = new("GeneratedRoomEncounters");
        encounterRootObject.transform.SetParent(generatedRoot.transform, false);

        DungeonRoomBuilder builder = generatedRoot.AddComponent<DungeonRoomBuilder>();
        builder.EditorAssignTilemaps(
            underFloor,
            floor,
            floorDetail,
            groundDecoration,
            wall,
            wallDetail,
            foreground,
            overlayFx);
        builder.EditorAssignCorridorTiles(fallbackFloor, fallbackWall);
        builder.EditorAssignConnectedDoorSetup(
            doorPrefab,
            doorRootObject.transform,
            shouldOpenInitially: true);
        builder.EditorAssignSocketBlockerRoot(socketBlockerRootObject.transform);
        builder.EditorAssignObjectRoot(objectRootObject.transform);
        builder.EditorAssignEncounterRoot(encounterRootObject.transform);

        DungeonGenerator generator = generatedRoot.AddComponent<DungeonGenerator>();
        generator.EditorConfigure(
            library,
            builder,
            20260811,
            6,
            true,
            512,
            PrototypeMinimumCorridorLength,
            PrototypeCorridorLengthPerRoomCell,
            PrototypeCorridorLengthVariation,
            true);
        EditorUtility.SetDirty(builder);
        EditorUtility.SetDirty(generator);

        if (generator.RoomLibrary == null || generator.RoomBuilder == null)
        {
            throw new InvalidOperationException(
                "Failed to assign DungeonGenerator dependencies. " +
                $"LibraryArgNull={library == null}, " +
                $"GeneratorLibraryNull={generator.RoomLibrary == null}, " +
                $"BuilderArgNull={builder == null}, " +
                $"GeneratorBuilderNull={generator.RoomBuilder == null}");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        VerifyInstalledPipeline(generator, builder);
        builder.ClearGeneratedContent();
        VerifyPrototypeSceneShell(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Selection.activeObject = generatedRoot;
        Debug.Log(
            $"Procedural dungeon v0 scene installed and verified: {TargetScenePath}",
            generatedRoot);
    }

    private static void SyncHubUiIntoScene(Scene targetScene)
    {
        if (!targetScene.IsValid() || !targetScene.isLoaded)
            throw new InvalidOperationException("Target scene must be loaded before synchronizing Hub UI.");

        GameObject existingTargetRoot = FindInScene(targetScene, GlobalUiRootName);
        if (existingTargetRoot == null)
            throw new InvalidOperationException($"Target scene is missing {GlobalUiRootName}.");

        int targetSiblingIndex = existingTargetRoot.transform.GetSiblingIndex();
        List<(UnityEngine.Object owner, string propertyPath, UnityEngine.Object sourceObject)>
            externalReferences = CaptureExternalPrefabReferences(targetScene, existingTargetRoot);
        Scene hubScene = EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject sourceRoot = FindInScene(hubScene, GlobalUiRootName);
            if (sourceRoot == null)
                throw new InvalidOperationException($"Hub scene is missing {GlobalUiRootName}.");

            if (PrefabUtility.GetOutermostPrefabInstanceRoot(sourceRoot) != sourceRoot)
            {
                throw new InvalidOperationException(
                    $"Hub {GlobalUiRootName} must be an outermost prefab instance root.");
            }

            GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromOriginalSource(sourceRoot);
            if (prefabAsset == null)
                throw new InvalidOperationException($"Hub {GlobalUiRootName} has no source prefab asset.");

            PropertyModification[] hubModifications =
                PrefabUtility.GetPropertyModifications(sourceRoot) ??
                Array.Empty<PropertyModification>();

            UnityEngine.Object.DestroyImmediate(existingTargetRoot);
            GameObject replacement = PrefabUtility.InstantiatePrefab(
                prefabAsset,
                targetScene) as GameObject;
            if (replacement == null)
                throw new InvalidOperationException($"Failed to instantiate Hub {GlobalUiRootName} prefab.");

            replacement.transform.SetSiblingIndex(targetSiblingIndex);
            PrefabUtility.SetPropertyModifications(replacement, hubModifications);
            EditorUtility.SetDirty(replacement);

            int reboundReferenceCount = RebindExternalPrefabReferences(
                replacement,
                externalReferences);
            int repairedDialogueReferenceCount = RepairDialogueUiReferences(
                targetScene,
                replacement);
            VerifySynchronizedHubUi(sourceRoot, replacement, hubModifications);
            VerifyDialogueUiReferences(targetScene, replacement);
            Debug.Log(
                $"Hub UI scene references verified. Rebound={reboundReferenceCount}, " +
                $"RepairedDialogue={repairedDialogueReferenceCount}");
        }
        finally
        {
            EditorSceneManager.CloseScene(hubScene, true);
            SceneManager.SetActiveScene(targetScene);
        }
    }

    private static List<(UnityEngine.Object owner, string propertyPath, UnityEngine.Object sourceObject)>
        CaptureExternalPrefabReferences(Scene scene, GameObject prefabRoot)
    {
        List<(UnityEngine.Object owner, string propertyPath, UnityEngine.Object sourceObject)>
            results = new();
        GameObject[] sceneRoots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < sceneRoots.Length; rootIndex++)
        {
            Component[] components = sceneRoots[rootIndex].GetComponentsInChildren<Component>(true);
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                Component owner = components[componentIndex];
                if (owner == null || IsOwnedByRoot(owner, prefabRoot))
                    continue;

                SerializedObject serializedOwner = new(owner);
                SerializedProperty property = serializedOwner.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    UnityEngine.Object referencedObject = property.objectReferenceValue;
                    if (!IsOwnedByRoot(referencedObject, prefabRoot))
                        continue;

                    UnityEngine.Object sourceObject =
                        PrefabUtility.GetCorrespondingObjectFromOriginalSource(referencedObject);
                    if (sourceObject == null)
                    {
                        throw new InvalidOperationException(
                            $"Cannot preserve scene reference '{owner.name}.{property.propertyPath}' " +
                            $"because its {GlobalUiRootName} target has no prefab source object.");
                    }

                    results.Add((owner, property.propertyPath, sourceObject));
                }
            }
        }

        return results;
    }

    private static int RebindExternalPrefabReferences(
        GameObject replacementRoot,
        List<(UnityEngine.Object owner, string propertyPath, UnityEngine.Object sourceObject)> bindings)
    {
        Dictionary<UnityEngine.Object, UnityEngine.Object> instanceBySource =
            BuildPrefabInstanceObjectMap(replacementRoot);
        for (int i = 0; i < bindings.Count; i++)
        {
            (UnityEngine.Object owner, string propertyPath, UnityEngine.Object sourceObject) binding =
                bindings[i];
            if (binding.owner == null ||
                !instanceBySource.TryGetValue(binding.sourceObject, out UnityEngine.Object instanceObject))
            {
                throw new InvalidOperationException(
                    $"Failed to remap external {GlobalUiRootName} reference '{binding.propertyPath}'.");
            }

            SerializedObject serializedOwner = new(binding.owner);
            SerializedProperty property = serializedOwner.FindProperty(binding.propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            {
                throw new InvalidOperationException(
                    $"Failed to find serialized scene reference '{binding.propertyPath}'.");
            }

            property.objectReferenceValue = instanceObject;
            serializedOwner.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(binding.owner);
        }

        return bindings.Count;
    }

    private static Dictionary<UnityEngine.Object, UnityEngine.Object> BuildPrefabInstanceObjectMap(
        GameObject instanceRoot)
    {
        Dictionary<UnityEngine.Object, UnityEngine.Object> results = new();
        Transform[] transforms = instanceRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            GameObject instanceObject = transforms[i].gameObject;
            AddPrefabInstanceObjectMapping(instanceObject, results);

            Component[] components = instanceObject.GetComponents<Component>();
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                if (components[componentIndex] != null)
                    AddPrefabInstanceObjectMapping(components[componentIndex], results);
            }
        }

        return results;
    }

    private static void AddPrefabInstanceObjectMapping(
        UnityEngine.Object instanceObject,
        Dictionary<UnityEngine.Object, UnityEngine.Object> results)
    {
        UnityEngine.Object sourceObject =
            PrefabUtility.GetCorrespondingObjectFromOriginalSource(instanceObject);
        if (sourceObject != null)
            results[sourceObject] = instanceObject;
    }

    private static bool IsOwnedByRoot(UnityEngine.Object value, GameObject root)
    {
        Transform valueTransform = value switch
        {
            GameObject gameObject => gameObject.transform,
            Component component => component.transform,
            _ => null
        };
        return valueTransform != null &&
            (valueTransform == root.transform || valueTransform.IsChildOf(root.transform));
    }

    private static int RepairDialogueUiReferences(Scene scene, GameObject uiRoot)
    {
        DialogueView dialogueView = uiRoot.GetComponentInChildren<DialogueView>(true);
        PortraitController portraitController =
            uiRoot.GetComponentInChildren<PortraitController>(true);
        if (dialogueView == null || portraitController == null)
        {
            throw new InvalidOperationException(
                $"Synchronized {GlobalUiRootName} requires DialogueView and PortraitController.");
        }

        int repairCount = 0;
        List<DialogueController> dialogueControllers =
            FindComponentsInScene<DialogueController>(scene);
        for (int i = 0; i < dialogueControllers.Count; i++)
        {
            SerializedObject serializedController = new(dialogueControllers[i]);
            repairCount += AssignObjectReferenceIfMissing(
                serializedController,
                "view",
                dialogueView);
            repairCount += AssignObjectReferenceIfMissing(
                serializedController,
                "portraitController",
                portraitController);
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dialogueControllers[i]);
        }

        List<CinematicDirector> cinematicDirectors =
            FindComponentsInScene<CinematicDirector>(scene);
        for (int i = 0; i < cinematicDirectors.Count; i++)
        {
            SerializedObject serializedDirector = new(cinematicDirectors[i]);
            repairCount += AssignObjectReferenceIfMissing(
                serializedDirector,
                "portraitController",
                portraitController);
            serializedDirector.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cinematicDirectors[i]);
        }

        return repairCount;
    }

    private static int AssignObjectReferenceIfMissing(
        SerializedObject owner,
        string propertyPath,
        UnityEngine.Object value)
    {
        SerializedProperty property = owner.FindProperty(propertyPath);
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            throw new InvalidOperationException($"Missing serialized object property: {propertyPath}");

        if (property.objectReferenceValue != null)
            return 0;

        property.objectReferenceValue = value;
        return 1;
    }

    private static void VerifyDialogueUiReferences(Scene scene, GameObject uiRoot)
    {
        DialogueView expectedView = uiRoot.GetComponentInChildren<DialogueView>(true);
        PortraitController expectedPortrait =
            uiRoot.GetComponentInChildren<PortraitController>(true);

        List<DialogueController> dialogueControllers =
            FindComponentsInScene<DialogueController>(scene);
        for (int i = 0; i < dialogueControllers.Count; i++)
        {
            SerializedObject serializedController = new(dialogueControllers[i]);
            if (serializedController.FindProperty("view").objectReferenceValue != expectedView ||
                serializedController.FindProperty("portraitController").objectReferenceValue != expectedPortrait)
            {
                throw new InvalidOperationException(
                    "DialogueController references do not point to the synchronized Hub UI.");
            }
        }

        List<CinematicDirector> cinematicDirectors =
            FindComponentsInScene<CinematicDirector>(scene);
        for (int i = 0; i < cinematicDirectors.Count; i++)
        {
            SerializedObject serializedDirector = new(cinematicDirectors[i]);
            if (serializedDirector.FindProperty("portraitController").objectReferenceValue != expectedPortrait)
            {
                throw new InvalidOperationException(
                    "CinematicDirector does not reference the synchronized Hub portrait UI.");
            }
        }
    }

    private static List<T> FindComponentsInScene<T>(Scene scene) where T : Component
    {
        List<T> results = new();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            results.AddRange(roots[i].GetComponentsInChildren<T>(true));

        return results;
    }

    private static void VerifySynchronizedHubUi(
        GameObject sourceRoot,
        GameObject targetRoot,
        PropertyModification[] sourceModifications)
    {
        if (sourceRoot.activeSelf != targetRoot.activeSelf)
        {
            throw new InvalidOperationException(
                $"Synchronized {GlobalUiRootName} active state does not match Hub.");
        }

        List<string> sourceHierarchy = CollectHierarchySignature(sourceRoot);
        List<string> targetHierarchy = CollectHierarchySignature(targetRoot);
        if (sourceHierarchy.Count != targetHierarchy.Count)
        {
            throw new InvalidOperationException(
                $"Synchronized {GlobalUiRootName} hierarchy count does not match Hub. " +
                $"Hub={sourceHierarchy.Count}, Target={targetHierarchy.Count}");
        }

        for (int i = 0; i < sourceHierarchy.Count; i++)
        {
            if (sourceHierarchy[i] == targetHierarchy[i])
                continue;

            throw new InvalidOperationException(
                $"Synchronized {GlobalUiRootName} hierarchy differs at index {i}. " +
                $"Hub={sourceHierarchy[i]}, Target={targetHierarchy[i]}");
        }

        PropertyModification[] targetModifications =
            PrefabUtility.GetPropertyModifications(targetRoot) ??
            Array.Empty<PropertyModification>();
        List<string> sourceSignatures = CollectModificationSignatures(sourceModifications);
        List<string> targetSignatures = CollectModificationSignatures(targetModifications);
        if (sourceSignatures.Count != targetSignatures.Count)
        {
            throw new InvalidOperationException(
                $"Synchronized {GlobalUiRootName} override count does not match Hub. " +
                $"Hub={sourceSignatures.Count}, Target={targetSignatures.Count}");
        }

        for (int i = 0; i < sourceSignatures.Count; i++)
        {
            if (sourceSignatures[i] == targetSignatures[i])
                continue;

            throw new InvalidOperationException(
                $"Synchronized {GlobalUiRootName} prefab override differs at index {i}.");
        }

        Debug.Log(
            $"Hub UI verification passed. Active={targetRoot.activeSelf}, " +
            $"Overrides={targetSignatures.Count}, HierarchyEntries={targetHierarchy.Count}");
    }

    private static List<string> CollectHierarchySignature(GameObject root)
    {
        List<string> results = new();
        CollectHierarchySignature(root.transform, string.Empty, results);
        return results;
    }

    private static void CollectHierarchySignature(
        Transform current,
        string parentPath,
        List<string> results)
    {
        string path = string.IsNullOrEmpty(parentPath)
            ? current.name
            : $"{parentPath}/{current.GetSiblingIndex()}:{current.name}";
        Component[] components = current.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            string componentType = components[i] != null
                ? components[i].GetType().FullName
                : "MissingScript";
            results.Add($"{path}|{i}|{componentType}");
        }

        for (int i = 0; i < current.childCount; i++)
            CollectHierarchySignature(current.GetChild(i), path, results);
    }

    private static List<string> CollectModificationSignatures(
        PropertyModification[] modifications)
    {
        List<string> results = new(modifications.Length);
        for (int i = 0; i < modifications.Length; i++)
        {
            PropertyModification modification = modifications[i];
            results.Add(
                $"{GetPrefabObjectIdentity(modification.target)}|" +
                $"{modification.propertyPath}|{modification.value}|" +
                $"{GetPrefabObjectIdentity(modification.objectReference)}");
        }

        results.Sort(StringComparer.Ordinal);
        return results;
    }

    private static string GetPrefabObjectIdentity(UnityEngine.Object value)
    {
        if (value == null)
            return "null";

        UnityEngine.Object source =
            PrefabUtility.GetCorrespondingObjectFromOriginalSource(value);
        UnityEngine.Object identity = source != null ? source : value;
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                identity,
                out string guid,
                out long localId))
        {
            return $"{guid}:{localId}";
        }

        return $"{identity.GetType().FullName}:{identity.name}";
    }

    private static GameObject CreateOrUpdateSacrificeRewardAlcovePrefab(
        GameObject statuePrefab,
        GameObject doorPrefab,
        GameObject chestPrefab)
    {
        if (statuePrefab == null || doorPrefab == null || chestPrefab == null)
            throw new InvalidOperationException("Sacrifice reward alcove requires all source prefabs.");

        bool editsExistingPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(SacrificeRewardAlcovePrefabPath) != null;
        GameObject root = editsExistingPrefab
            ? PrefabUtility.LoadPrefabContents(SacrificeRewardAlcovePrefabPath)
            : new GameObject("SacrificeRewardAlcove");
        try
        {
            root.name = "SacrificeRewardAlcove";
            GameObject statueObject = GetOrCreatePrefabChild(
                statuePrefab,
                root.transform,
                "OfferingStatue",
                Vector3.zero);
            GameObject doorObject = GetOrCreatePrefabChild(
                doorPrefab,
                root.transform,
                "RewardDoor",
                new Vector3(2.5f, 2f, 0f));
            GameObject chestObject = GetOrCreatePrefabChild(
                chestPrefab,
                root.transform,
                "TreasureChest",
                new Vector3(3f, 4f, 0f));

            RoomCompositePoseAuthoring composite =
                root.GetComponent<RoomCompositePoseAuthoring>();
            if (composite == null)
                composite = root.AddComponent<RoomCompositePoseAuthoring>();
            composite.EditorSetPoseSlots(new[]
            {
                new RoomCompositePoseSlotData(
                    "OfferingStatue",
                    "제물 동상",
                    statueObject.transform),
                new RoomCompositePoseSlotData(
                    "RewardDoor",
                    "보상 문",
                    doorObject.transform),
                new RoomCompositePoseSlotData(
                    "TreasureChest",
                    "보상 상자",
                    chestObject.transform)
            });
            EditorUtility.SetDirty(composite);

            StatueShortcut statue = statueObject.GetComponentInChildren<StatueShortcut>(true);
            DoorObject door = doorObject.GetComponentInChildren<DoorObject>(true);
            if (statue == null || door == null)
            {
                throw new InvalidOperationException(
                    "Sacrifice reward source prefabs are missing StatueShortcut or DoorObject.");
            }

            SerializedObject serializedStatue = new(statue);
            SerializedProperty targetDoor = serializedStatue.FindProperty("targetDoor");
            SerializedProperty lastSyncedTargetDoor =
                serializedStatue.FindProperty("lastSyncedTargetDoor");
            SerializedProperty costType = serializedStatue.FindProperty("costType");
            SerializedProperty costAmount = serializedStatue.FindProperty("costAmount");
            if (targetDoor == null ||
                lastSyncedTargetDoor == null ||
                costType == null ||
                costAmount == null)
            {
                throw new InvalidOperationException(
                    "StatueShortcut serialized contract changed; reward alcove could not be wired.");
            }

            targetDoor.objectReferenceValue = door;
            lastSyncedTargetDoor.objectReferenceValue = door;
            costType.enumValueIndex = (int)StatueShortcut.CostType.MagicStone;
            costAmount.intValue = 5;
            serializedStatue.ApplyModifiedPropertiesWithoutUndo();

            door.mapID = string.Empty;
            door.doorID = "Procedural_SacrificeRewardDoor";
            door.ApplyConfigurationFromShortcut(DoorObject.DoorType.Locked, false, statue);
            EditorUtility.SetDirty(statue);
            EditorUtility.SetDirty(door);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(
                root,
                SacrificeRewardAlcovePrefabPath);
            if (savedPrefab == null)
            {
                throw new InvalidOperationException(
                    $"Failed to save sacrifice reward alcove prefab: {SacrificeRewardAlcovePrefabPath}");
            }

            VerifySacrificeRewardAlcove(savedPrefab, SacrificeRewardAlcovePrefabPath);
            return savedPrefab;
        }
        finally
        {
            if (editsExistingPrefab)
                PrefabUtility.UnloadPrefabContents(root);
            else
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// 책임:
    /// 기존 복합 프리팹의 수동 배치 Transform은 보존하고 누락된 구성 오브젝트만 기본 위치로 생성한다.
    /// </summary>
    private static GameObject GetOrCreatePrefabChild(
        GameObject sourcePrefab,
        Transform parent,
        string objectName,
        Vector3 localPosition)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
            return existing.gameObject;

        GameObject instance = PrefabUtility.InstantiatePrefab(sourcePrefab, parent) as GameObject;
        if (instance == null)
            throw new InvalidOperationException($"Failed to instantiate prefab: {sourcePrefab.name}");

        instance.name = objectName;
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = sourcePrefab.transform.localScale;
        return instance;
    }

    private static RoomTemplateSO CreateOrUpdateSacrificeRewardRoom(
        string assetPath,
        string roomId,
        Vector2Int roomSize,
        TileBase fallbackFloor,
        TileBase fallbackWall,
        GameObject sacrificeRewardAlcovePrefab)
    {
        int partitionY = roomSize.y / 2 + 1;
        int openingStartX = (roomSize.x - RoomSocketGeometry.RequiredWidth) / 2;
        Vector2Int alcoveRootCell = new(openingStartX - 2, partitionY - 2);
        RoomObjectPlacementData alcovePlacement = CreateObjectPlacement(
            "SacrificeRewardAlcove",
            RoomObjectKind.Prop,
            sacrificeRewardAlcovePrefab,
            alcoveRootCell);
        PreserveExistingRoomObjectPose(assetPath, ref alcovePlacement);
        RoomTemplateSO template = CreateOrUpdateRoom(
            assetPath,
            roomId,
            RoomType.Treasure,
            roomSize,
            CreateOppositeSockets(
                roomSize,
                RoomSocketDirection.Left,
                RoomSocketDirection.Right),
            fallbackFloor,
            fallbackWall,
            new List<RoomObjectPlacementData>
            {
                alcovePlacement
            },
            0);

        RoomBuildData build = template.BuildData;
        for (int x = 1; x < roomSize.x - 1; x++)
        {
            bool isDoorOpening =
                x >= openingStartX &&
                x < openingStartX + RoomSocketGeometry.RequiredWidth;
            if (!isDoorOpening)
                EnsureTileAtCell(build.wallTiles, new Vector2Int(x, partitionY), fallbackWall);
        }

        template.EditorSetData(template.LayoutData, build);
        EditorUtility.SetDirty(template);
        return template;
    }

    /// <summary>
    /// 책임:
    /// 설치기 재실행 시 같은 Placement Id에 기획자가 저장한 루트 자세와 복합 자식 자세 재정의를 보존한다.
    /// </summary>
    private static void PreserveExistingRoomObjectPose(
        string roomAssetPath,
        ref RoomObjectPlacementData replacement)
    {
        RoomTemplateSO existingRoom = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(roomAssetPath);
        List<RoomObjectPlacementData> existingPlacements =
            existingRoom != null ? existingRoom.BuildData.objectPlacements : null;
        for (int i = 0; existingPlacements != null && i < existingPlacements.Count; i++)
        {
            RoomObjectPlacementData existing = existingPlacements[i];
            if (!string.Equals(
                    existing.placementId,
                    replacement.placementId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            replacement.localCell = existing.localCell;
            replacement.localOffset = existing.localOffset;
            replacement.localRotationDegrees = existing.localRotationDegrees;
            replacement.localScale = existing.localScale;
            replacement.childPoseOverrides = existing.childPoseOverrides != null
                ? new List<RoomObjectChildPoseOverrideData>(existing.childPoseOverrides)
                : new List<RoomObjectChildPoseOverrideData>();
            return;
        }
    }

    private static RoomTemplateSO CreateOrUpdateRoom(
        string assetPath,
        string roomId,
        RoomType roomType,
        Vector2Int roomSize,
        List<RoomSocketData> sockets,
        TileBase fallbackFloor,
        TileBase fallbackWall,
        List<RoomObjectPlacementData> objectPlacements,
        int difficultyTier)
    {
        RoomTemplateSO template = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(assetPath);
        if (template == null)
        {
            template = ScriptableObject.CreateInstance<RoomTemplateSO>();
            AssetDatabase.CreateAsset(template, assetPath);
        }

        if (roomSize.x < 4 || roomSize.y < 4)
        {
            throw new InvalidOperationException(
                $"Prototype room '{roomId}' must be at least 4x4. Size={roomSize}");
        }

        List<RoomTileData> floorTiles = CreateInteriorFloorTiles(roomSize, fallbackFloor);
        List<RoomTileData> wallTiles = CreateBoundaryWallTiles(roomSize, fallbackWall);
        for (int i = 0; i < sockets.Count; i++)
        {
            RoomSocketData socket = sockets[i];
            int width = RoomSocketGeometry.ResolveWidth(socket);
            for (int cellIndex = 0; cellIndex < width; cellIndex++)
            {
                Vector2Int socketCell = RoomSocketGeometry.GetLocalCell(socket, cellIndex);
                EnsureTileAtCell(floorTiles, socketCell, fallbackFloor);
                EnsureTileAtCell(wallTiles, socketCell, fallbackWall);
            }
        }

        RoomLayoutData layout = new()
        {
            roomId = roomId,
            roomType = roomType,
            size = roomSize,
            localBounds = new RectInt(Vector2Int.zero, roomSize),
            sockets = new List<RoomSocketData>(sockets),
            difficultyTier = difficultyTier,
            selectionWeight = 1f
        };
        RoomBuildData build = new()
        {
            floorTiles = floorTiles,
            wallTiles = wallTiles,
            objectPlacements = objectPlacements != null
                ? new List<RoomObjectPlacementData>(objectPlacements)
                : new List<RoomObjectPlacementData>()
        };

        template.EditorSetData(layout, build);
        template.name = roomId;
        EditorUtility.SetDirty(template);
        return template;
    }

    private static RoomThemeLibrarySO CreateOrUpdateLibrary(
        string assetPath,
        string themeId,
        params RoomTemplateSO[] templates)
    {
        RoomThemeLibrarySO library = AssetDatabase.LoadAssetAtPath<RoomThemeLibrarySO>(assetPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<RoomThemeLibrarySO>();
            AssetDatabase.CreateAsset(library, assetPath);
        }

        SerializedObject serializedLibrary = new(library);
        serializedLibrary.FindProperty("themeId").stringValue = themeId;
        SerializedProperty rooms = serializedLibrary.FindProperty("rooms");
        rooms.arraySize = templates.Length;
        for (int i = 0; i < templates.Length; i++)
            rooms.GetArrayElementAtIndex(i).objectReferenceValue = templates[i];

        serializedLibrary.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(library);
        return library;
    }

    private static Tilemap CreateRuntimeTilemap(
        Transform parent,
        RoomTileLayerKind layer)
    {
        string objectName = $"Generated{RoomTileLayerContract.GetLayerName(layer)}";
        GameObject tilemapObject = new(objectName);
        tilemapObject.transform.SetParent(parent, false);

        Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
        tilemapObject.AddComponent<TilemapRenderer>();
        ConfigureRuntimeTilemap(tilemap, layer);
        return tilemap;
    }

    private static void InstallFixedRoomTileLayers(
        DungeonRoomBuilder builder,
        string scenePath)
    {
        if (builder.FloorTilemap == null || builder.WallTilemap == null)
        {
            throw new InvalidOperationException(
                $"DungeonRoomBuilder Floor/Wall references are missing: {scenePath}");
        }

        Transform grid = builder.FloorTilemap.transform.parent;
        if (grid == null || builder.WallTilemap.transform.parent != grid)
        {
            throw new InvalidOperationException(
                $"DungeonRoomBuilder Floor/Wall must share one generated Grid: {scenePath}");
        }

        Tilemap underFloor = FindOrCreateRuntimeTilemap(grid, RoomTileLayerKind.UnderFloor);
        Tilemap floor = builder.FloorTilemap;
        Tilemap floorDetail = FindOrCreateRuntimeTilemap(grid, RoomTileLayerKind.FloorDetail);
        Tilemap groundDecoration = FindOrCreateRuntimeTilemap(
            grid,
            RoomTileLayerKind.GroundDecoration);
        Tilemap wall = builder.WallTilemap;
        Tilemap wallDetail = FindOrCreateRuntimeTilemap(grid, RoomTileLayerKind.WallDetail);
        Tilemap foreground = FindOrCreateRuntimeTilemap(grid, RoomTileLayerKind.Foreground);
        Tilemap overlayFx = FindOrCreateRuntimeTilemap(grid, RoomTileLayerKind.OverlayFX);

        ConfigureRuntimeTilemap(floor, RoomTileLayerKind.Floor);
        ConfigureRuntimeTilemap(wall, RoomTileLayerKind.Wall);
        builder.EditorAssignTilemaps(
            underFloor,
            floor,
            floorDetail,
            groundDecoration,
            wall,
            wallDetail,
            foreground,
            overlayFx);
        EditorUtility.SetDirty(builder);
        VerifyFixedRoomTileLayers(builder, scenePath);
    }

    private static Tilemap FindOrCreateRuntimeTilemap(
        Transform parent,
        RoomTileLayerKind layer)
    {
        string objectName = $"Generated{RoomTileLayerContract.GetLayerName(layer)}";
        Transform existing = parent.Find(objectName);
        Tilemap tilemap = existing != null ? existing.GetComponent<Tilemap>() : null;
        if (existing != null && tilemap == null)
        {
            throw new InvalidOperationException(
                $"'{objectName}' exists but is not a Tilemap under '{parent.name}'.");
        }

        if (tilemap == null)
            tilemap = CreateRuntimeTilemap(parent, layer);
        else
            ConfigureRuntimeTilemap(tilemap, layer);

        return tilemap;
    }

    private static void ConfigureRuntimeTilemap(Tilemap tilemap, RoomTileLayerKind layer)
    {
        GameObject tilemapObject = tilemap.gameObject;

        if (RoomTileLayerContract.UsesGroundPhysicsLayer(layer))
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer < 0)
                throw new InvalidOperationException("The project requires a Ground physics layer.");

            tilemapObject.layer = groundLayer;
        }
        else
        {
            tilemapObject.layer = 0;
        }

        TilemapRenderer renderer = tilemapObject.GetComponent<TilemapRenderer>();
        if (renderer == null)
            renderer = tilemapObject.AddComponent<TilemapRenderer>();
        renderer.sortingLayerName = RoomTileLayerContract.GetSortingLayerName(layer);
        renderer.sortingOrder = RoomTileLayerContract.GetSortingOrder(layer);

        if (RoomTileLayerContract.RequiresCollider(layer))
        {
            TilemapCollider2D tilemapCollider = tilemapObject.GetComponent<TilemapCollider2D>();
            if (tilemapCollider == null)
                tilemapCollider = tilemapObject.AddComponent<TilemapCollider2D>();

            Rigidbody2D rigidbody = tilemapObject.GetComponent<Rigidbody2D>();
            if (rigidbody == null)
                rigidbody = tilemapObject.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = RigidbodyType2D.Static;
            if (tilemapObject.GetComponent<CompositeCollider2D>() == null)
                tilemapObject.AddComponent<CompositeCollider2D>();
            tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
        }
        else
        {
            Collider2D[] colliders = tilemapObject.GetComponents<Collider2D>();
            for (int colliderIndex = colliders.Length - 1; colliderIndex >= 0; colliderIndex--)
                UnityEngine.Object.DestroyImmediate(colliders[colliderIndex]);

            Rigidbody2D rigidbody = tilemapObject.GetComponent<Rigidbody2D>();
            if (rigidbody != null)
                UnityEngine.Object.DestroyImmediate(rigidbody);
        }

        EditorUtility.SetDirty(tilemapObject);
        EditorUtility.SetDirty(tilemap);
        EditorUtility.SetDirty(renderer);
    }

    private static void VerifyFixedRoomTileLayers(
        DungeonRoomBuilder builder,
        string scenePath)
    {
        if (!builder.HasCompleteTilemapSet)
            throw new InvalidOperationException($"Fixed Tilemap set is incomplete: {scenePath}");

        HashSet<Tilemap> uniqueTilemaps = new();
        for (int i = 0; i < RoomTileLayerContract.OrderedLayers.Count; i++)
        {
            RoomTileLayerKind layer = RoomTileLayerContract.OrderedLayers[i];
            Tilemap tilemap = builder.GetTilemap(layer);
            if (!uniqueTilemaps.Add(tilemap))
            {
                throw new InvalidOperationException(
                    $"Multiple fixed layer slots share one Tilemap in {scenePath}: {layer}");
            }

            int expectedPhysicsLayer = RoomTileLayerContract.UsesGroundPhysicsLayer(layer)
                ? LayerMask.NameToLayer("Ground")
                : 0;
            TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
            bool hasCollider = tilemap.GetComponent<Collider2D>() != null;
            if (tilemap.gameObject.layer != expectedPhysicsLayer ||
                renderer == null ||
                renderer.sortingLayerName != RoomTileLayerContract.GetSortingLayerName(layer) ||
                renderer.sortingOrder != RoomTileLayerContract.GetSortingOrder(layer) ||
                hasCollider != RoomTileLayerContract.RequiresCollider(layer))
            {
                throw new InvalidOperationException(
                    $"Fixed Tilemap contract mismatch in {scenePath}: {layer}");
            }
        }
    }

    private static void VerifyInstalledPipeline(
        DungeonGenerator generator,
        DungeonRoomBuilder builder)
    {
        if (!builder.HasCompleteTilemapSet)
            throw new InvalidOperationException("DungeonRoomBuilder fixed Tilemap set is incomplete.");

        if (!generator.Generate())
        {
            string reason = generator.LastLayout != null
                ? generator.LastLayout.FailureReason
                : "No layout result.";
            throw new InvalidOperationException($"Installed generator verification failed: {reason}");
        }

        DungeonLayoutResult layout = generator.LastLayout;
        int expectedConnectionCount = layout.UsesGraphFirstLayout
            ? generator.RoomCount - 1 + layout.CycleConnectionCount
            : generator.RoomCount - 1;
        if (layout.Rooms.Count != generator.RoomCount ||
            layout.Connections.Count != expectedConnectionCount)
        {
            throw new InvalidOperationException(
                $"Unexpected layout size. Rooms={layout.Rooms.Count}/{generator.RoomCount}, " +
                $"Connections={layout.Connections.Count}/{expectedConnectionCount}");
        }

        VerifyRoomSizeDiversity(generator.RoomLibrary, layout);
        VerifySingleSocketBossPlacement(layout);
        if (generator.LayoutPolicy != null)
        {
            VerifyGraphFirstPolicyAcrossSeeds(
                generator.RoomLibrary,
                generator.LayoutPolicy,
                generator.RoomCount,
                generator.MaxPlacementAttemptsPerRoom,
                generator.MinimumCorridorLength,
                generator.CorridorLengthPerRoomCell,
                generator.CorridorLengthVariation);
        }
        else
        {
            VerifySingleSocketBossAcrossSeeds(
                generator.RoomLibrary,
                generator.MinimumCorridorLength,
                generator.CorridorLengthPerRoomCell,
                generator.CorridorLengthVariation);
        }
        VerifyAdaptiveStraightCorridors(layout, builder, generator);
        VerifySocketWallAndColliderState(layout, builder);
        VerifyGeneratedRoomObjects(layout, builder);
        VerifyGeneratedRoomEncounters(layout, builder);

        for (int i = 0; i < layout.Rooms.Count; i++)
        {
            for (int j = i + 1; j < layout.Rooms.Count; j++)
            {
                if (layout.Rooms[i].WorldBounds.Overlaps(layout.Rooms[j].WorldBounds))
                    throw new InvalidOperationException($"Room overlap detected: {i} and {j}");
            }
        }

        if (builder.FloorTilemap.GetUsedTilesCount() == 0)
            throw new InvalidOperationException("Generated Floor Tilemap is empty.");

        int expectedDoorCount = layout.Connections.Count * 2;
        if (builder.GeneratedDoors.Count != expectedDoorCount)
        {
            throw new InvalidOperationException(
                $"Unexpected generated door count. Doors={builder.GeneratedDoors.Count}, " +
                $"Expected={expectedDoorCount}, Connections={layout.Connections.Count}");
        }

        for (int i = 0; i < builder.GeneratedDoors.Count; i++)
        {
            DoorObject door = builder.GeneratedDoors[i];
            if (door == null || door.doorType != DoorObject.DoorType.Normal || door.isPermanent)
                throw new InvalidOperationException($"Generated door {i} has invalid runtime configuration.");

            if (builder.OpenConnectedDoorsInitially && !door.IsOpen)
                throw new InvalidOperationException($"Generated door {i} should start open in the prototype scene.");

            int connectionIndex = i / 2;
            bool firstEndpoint = i % 2 == 0;
            DungeonSocketConnection connection = layout.Connections[connectionIndex];
            Vector3 expectedDoorPosition = GetConnectionEndpointCenter(
                layout,
                connection,
                builder.FloorTilemap,
                firstEndpoint);
            if ((door.transform.position - expectedDoorPosition).sqrMagnitude > 0.0001f)
            {
                throw new InvalidOperationException(
                    $"Generated door {i} is not centered on its connection socket endpoint. " +
                    $"Expected={expectedDoorPosition}, Actual={door.transform.position}");
            }
        }
    }

    private static void VerifyRoomSizeDiversity(
        RoomThemeLibrarySO library,
        DungeonLayoutResult layout)
    {
        HashSet<Vector2Int> librarySizes = new();
        IReadOnlyList<RoomTemplateSO> templates = library.Rooms;
        for (int i = 0; i < templates.Count; i++)
        {
            RoomTemplateSO template = templates[i];
            if (template != null)
                librarySizes.Add(template.LayoutData.localBounds.size);
        }

        if (librarySizes.Count < 5)
        {
            throw new InvalidOperationException(
                $"Prototype library requires five distinct room sizes. Actual={librarySizes.Count}");
        }

        HashSet<Vector2Int> generatedCombatSizes = new();
        for (int i = 0; i < layout.Rooms.Count; i++)
        {
            DungeonRoomPlacement placement = layout.Rooms[i];
            if (placement.Template.LayoutData.roomType == RoomType.Combat)
                generatedCombatSizes.Add(placement.WorldBounds.size);
        }

        if (generatedCombatSizes.Count < 2)
        {
            throw new InvalidOperationException(
                "The fixed prototype seed must visibly include at least two Combat room sizes.");
        }

        Debug.Log(
            $"Room size diversity verification passed. " +
            $"LibrarySizes={librarySizes.Count}, GeneratedCombatSizes={generatedCombatSizes.Count}");
    }

    private static void VerifyGeneratedRoomEncounters(
        DungeonLayoutResult layout,
        DungeonRoomBuilder builder)
    {
        HashSet<int> monsterRoomIds = new();
        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement roomPlacement = layout.Rooms[roomIndex];
            List<RoomObjectPlacementData> placements =
                roomPlacement.Template.BuildData.objectPlacements;
            if (!ContainsMonsterPlacement(placements))
                continue;

            monsterRoomIds.Add(roomPlacement.PlacementId);
            if (!builder.TryGetGeneratedRoomEncounter(
                    roomPlacement.PlacementId,
                    out MonsterSpawnRoomGroup roomGroup,
                    out MonsterRoomArea2D roomArea))
            {
                throw new InvalidOperationException(
                    $"Monster room {roomPlacement.PlacementId} is missing its generated encounter.");
            }

            PolygonCollider2D areaCollider = roomArea.GetComponent<PolygonCollider2D>();
            RoomEncounterEntryTrigger2D entryTrigger =
                roomGroup.GetComponentInChildren<RoomEncounterEntryTrigger2D>();
            PolygonCollider2D entryCollider = entryTrigger != null
                ? entryTrigger.GetComponent<PolygonCollider2D>()
                : null;
            if (areaCollider == null || !areaCollider.isTrigger ||
                areaCollider.points.Length != 4 ||
                roomArea.AreaCollider != areaCollider ||
                entryTrigger == null ||
                entryTrigger.TargetRoomGroup != roomGroup ||
                entryCollider == null ||
                !entryCollider.isTrigger ||
                entryCollider == areaCollider ||
                entryCollider.points.Length != 4)
            {
                throw new InvalidOperationException(
                    $"Monster room {roomPlacement.PlacementId} has an invalid encounter area or entry trigger.");
            }

            Vector3 expectedEntryMin = builder.FloorTilemap.CellToWorld(new Vector3Int(
                roomPlacement.WorldBounds.xMin + 1,
                roomPlacement.WorldBounds.yMin + 1,
                0));
            Vector3 expectedEntryMax = builder.FloorTilemap.CellToWorld(new Vector3Int(
                roomPlacement.WorldBounds.xMax - 1,
                roomPlacement.WorldBounds.yMax - 1,
                0));
            Vector3 actualEntryMin = entryCollider.transform.TransformPoint(entryCollider.points[0]);
            Vector3 actualEntryMax = entryCollider.transform.TransformPoint(entryCollider.points[2]);
            if ((actualEntryMin - expectedEntryMin).sqrMagnitude > 0.0001f ||
                (actualEntryMax - expectedEntryMax).sqrMagnitude > 0.0001f)
            {
                throw new InvalidOperationException(
                    $"Monster room {roomPlacement.PlacementId} entry trigger must be inset one cell " +
                    "from the full monster containment area.");
            }
        }

        if (builder.GeneratedRoomEncounterGroups.Count != monsterRoomIds.Count)
        {
            throw new InvalidOperationException(
                $"Unexpected room encounter count. Expected={monsterRoomIds.Count}, " +
                $"Actual={builder.GeneratedRoomEncounterGroups.Count}");
        }

        int expectedLockCount = 0;
        for (int connectionIndex = 0; connectionIndex < layout.Connections.Count; connectionIndex++)
        {
            DungeonSocketConnection connection = layout.Connections[connectionIndex];
            VerifyGeneratedEndpointDoorLock(
                builder,
                monsterRoomIds,
                connection.FirstRoomPlacementId,
                FindPlacement(layout, connection.FirstRoomPlacementId)
                    .Template.LayoutData.sockets[connection.FirstSocketIndex].direction,
                builder.GeneratedDoors[connectionIndex * 2],
                ref expectedLockCount);
            VerifyGeneratedEndpointDoorLock(
                builder,
                monsterRoomIds,
                connection.SecondRoomPlacementId,
                FindPlacement(layout, connection.SecondRoomPlacementId)
                    .Template.LayoutData.sockets[connection.SecondSocketIndex].direction,
                builder.GeneratedDoors[connectionIndex * 2 + 1],
                ref expectedLockCount);
        }

        if (builder.GeneratedRoomDoorLocks.Count != expectedLockCount)
        {
            throw new InvalidOperationException(
                $"Unexpected room door Kill Lock count. Expected={expectedLockCount}, " +
                $"Actual={builder.GeneratedRoomDoorLocks.Count}");
        }

        Debug.Log(
            $"Room encounter verification passed. " +
            $"MonsterRooms={monsterRoomIds.Count}, DoorKillLocks={expectedLockCount}");
    }

    private static void VerifyGeneratedEndpointDoorLock(
        DungeonRoomBuilder builder,
        HashSet<int> monsterRoomIds,
        int roomPlacementId,
        RoomSocketDirection socketDirection,
        DoorObject endpointDoor,
        ref int lockIndex)
    {
        if (!monsterRoomIds.Contains(roomPlacementId))
            return;

        if (lockIndex >= builder.GeneratedRoomDoorLocks.Count)
            throw new InvalidOperationException("Generated room door Kill Lock list is shorter than expected.");

        RoomDoorMonsterKillLock doorLock = builder.GeneratedRoomDoorLocks[lockIndex++];
        if (!builder.TryGetGeneratedRoomEncounter(
                roomPlacementId,
                out MonsterSpawnRoomGroup expectedGroup,
                out _))
        {
            throw new InvalidOperationException(
                $"Monster room {roomPlacementId} is missing while verifying its endpoint door.");
        }

        if (doorLock == null ||
            doorLock.TargetDoor != endpointDoor ||
            doorLock.TargetRoomGroup != expectedGroup ||
            doorLock.RoomInwardDirection != -DirectionToVector(socketDirection) ||
            doorLock.GetComponentInParent<MonsterSpawnRoomGroup>() != expectedGroup)
        {
            throw new InvalidOperationException(
                $"Monster room {roomPlacementId} endpoint door has an invalid Kill Lock binding.");
        }
    }

    private static bool ContainsMonsterPlacement(
        IReadOnlyList<RoomObjectPlacementData> placements)
    {
        if (placements == null)
            return false;

        for (int i = 0; i < placements.Count; i++)
        {
            if (placements[i].kind == RoomObjectKind.Monster)
                return true;
        }

        return false;
    }

    private static void VerifyAdaptiveStraightCorridors(
        DungeonLayoutResult layout,
        DungeonRoomBuilder builder,
        DungeonGenerator generator)
    {
        var generatedLengths = new HashSet<int>();
        HashSet<TileBase> generatedFloorTiles = new();
        HashSet<TileBase> generatedWallTiles = new();
        int maximumConfiguredWallVariantCount = 0;
        int shortestLength = int.MaxValue;
        int longestLength = 0;

        for (int connectionIndex = 0; connectionIndex < layout.Connections.Count; connectionIndex++)
        {
            DungeonSocketConnection connection = layout.Connections[connectionIndex];
            DungeonRoomPlacement firstPlacement =
                FindPlacement(layout, connection.FirstRoomPlacementId);
            DungeonRoomPlacement secondPlacement =
                FindPlacement(layout, connection.SecondRoomPlacementId);
            RoomSocketData firstSocket =
                firstPlacement.Template.LayoutData.sockets[connection.FirstSocketIndex];
            RoomSocketData secondSocket =
                secondPlacement.Template.LayoutData.sockets[connection.SecondSocketIndex];
            Vector2Int firstCell = firstPlacement.Origin + firstSocket.localCell;
            Vector2Int secondCell = secondPlacement.Origin + secondSocket.localCell;
            Vector2Int direction = DirectionToVector(firstSocket.direction);
            Vector2Int tangent = RoomSocketGeometry.GetTangent(firstSocket.direction);
            int width = RoomSocketGeometry.ResolveWidth(firstSocket);
            bool horizontal = firstSocket.direction == RoomSocketDirection.Left ||
                firstSocket.direction == RoomSocketDirection.Right;
            IReadOnlyList<TileBase> configuredWallVariants = horizontal
                ? builder.HorizontalCorridorWallVariants
                : builder.VerticalCorridorWallVariants;
            maximumConfiguredWallVariantCount = Mathf.Max(
                maximumConfiguredWallVariantCount,
                configuredWallVariants?.Count ?? 0);
            int firstDepth = horizontal
                ? firstPlacement.WorldBounds.width
                : firstPlacement.WorldBounds.height;
            int secondDepth = horizontal
                ? secondPlacement.WorldBounds.width
                : secondPlacement.WorldBounds.height;
            int preferredLength = generator.MinimumCorridorLength + Mathf.CeilToInt(
                (firstDepth + secondDepth) * generator.CorridorLengthPerRoomCell);
            int maximumPreferredLength = preferredLength + generator.CorridorLengthVariation;
            int actualLength = connection.CorridorLength;
            bool invalidLength = layout.UsesGraphFirstLayout
                ? actualLength < generator.MinimumCorridorLength
                : actualLength < generator.MinimumCorridorLength ||
                  actualLength > maximumPreferredLength;
            if (invalidLength)
            {
                throw new InvalidOperationException(
                    $"Adaptive corridor length is outside its allowed range at connection {connectionIndex}. " +
                    $"Expected={(layout.UsesGraphFirstLayout ? $">={generator.MinimumCorridorLength}" : $"{generator.MinimumCorridorLength}..{maximumPreferredLength}")}, " +
                    $"Preferred={preferredLength}..{maximumPreferredLength}, " +
                    $"Actual={actualLength}");
            }

            generatedLengths.Add(actualLength);
            shortestLength = Mathf.Min(shortestLength, actualLength);
            longestLength = Mathf.Max(longestLength, actualLength);

            Vector2Int expectedSecondCell =
                firstCell + direction * (actualLength + 1);
            if (secondCell != expectedSecondCell)
            {
                throw new InvalidOperationException(
                    $"Corridor {connectionIndex} socket distance is invalid. " +
                    $"Expected={expectedSecondCell}, Actual={secondCell}");
            }

            for (int step = 1; step <= actualLength; step++)
            {
                Vector2Int corridorStart = firstCell + direction * step;
                for (int cellIndex = 0; cellIndex < width; cellIndex++)
                {
                    Vector2Int floorCell = corridorStart + tangent * cellIndex;
                    if (!builder.FloorTilemap.HasTile(new Vector3Int(floorCell.x, floorCell.y, 0)))
                    {
                        throw new InvalidOperationException(
                            $"Corridor {connectionIndex} is missing Floor at {floorCell}.");
                    }

                    generatedFloorTiles.Add(builder.FloorTilemap.GetTile(
                        new Vector3Int(floorCell.x, floorCell.y, 0)));
                }

                Vector2Int firstWallCell = corridorStart - tangent;
                Vector2Int secondWallCell = corridorStart + tangent * width;
                if (!builder.WallTilemap.HasTile(
                        new Vector3Int(firstWallCell.x, firstWallCell.y, 0)) ||
                    !builder.WallTilemap.HasTile(
                        new Vector3Int(secondWallCell.x, secondWallCell.y, 0)))
                {
                    throw new InvalidOperationException(
                        $"Corridor {connectionIndex} is missing a side Wall at step {step}.");
                }

                generatedWallTiles.Add(builder.WallTilemap.GetTile(
                    new Vector3Int(firstWallCell.x, firstWallCell.y, 0)));
                generatedWallTiles.Add(builder.WallTilemap.GetTile(
                    new Vector3Int(secondWallCell.x, secondWallCell.y, 0)));
            }

            for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
            {
                if (connection.CorridorBounds.Overlaps(layout.Rooms[roomIndex].WorldBounds))
                {
                    throw new InvalidOperationException(
                        $"Corridor {connectionIndex} overlaps room {roomIndex}.");
                }
            }

            for (int previousIndex = 0; previousIndex < connectionIndex; previousIndex++)
            {
                if (connection.CorridorBounds.Overlaps(
                        layout.Connections[previousIndex].CorridorBounds))
                {
                    throw new InvalidOperationException(
                        $"Corridor overlap detected: {previousIndex} and {connectionIndex}");
                }
            }
        }

        if (layout.Connections.Count > 1 && generatedLengths.Count < 2)
        {
            throw new InvalidOperationException(
                "The fixed prototype seed did not produce varied corridor lengths.");
        }


        if (builder.CorridorFloorVariants.Count > 1 && generatedFloorTiles.Count < 2)
        {
            throw new InvalidOperationException(
                "The configured corridor Floor palette did not produce visible variation.");
        }

        if (maximumConfiguredWallVariantCount > 1 && generatedWallTiles.Count < 2)
        {
            throw new InvalidOperationException(
                "The configured corridor Wall palette did not produce visible variation.");
        }

        Debug.Log(
            $"Adaptive straight corridor verification passed. " +
            $"Count={layout.Connections.Count}, LengthRange={shortestLength}..{longestLength}, " +
            $"Width={RoomSocketGeometry.RequiredWidth}, " +
            $"FloorTiles={generatedFloorTiles.Count}, WallTiles={generatedWallTiles.Count}");
    }

    private static void VerifyGeneratedRoomObjects(
        DungeonLayoutResult layout,
        DungeonRoomBuilder builder)
    {
        int generatedIndex = 0;
        int monsterCount = 0;
        int chestCount = 0;
        int killLockChestCount = 0;
        int portalCount = 0;
        int sacrificeRewardAlcoveCount = 0;
        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement roomPlacement = layout.Rooms[roomIndex];
            List<RoomObjectPlacementData> placements =
                roomPlacement.Template.BuildData.objectPlacements;
            if (placements == null)
                continue;

            Dictionary<string, GameObject> roomInstancesByPlacementId =
                new(StringComparer.Ordinal);
            for (int objectIndex = 0; objectIndex < placements.Count; objectIndex++)
            {
                if (generatedIndex >= builder.GeneratedRoomObjects.Count)
                    throw new InvalidOperationException("Generated room object list is shorter than room data.");

                RoomObjectPlacementData placement = placements[objectIndex];
                GameObject instance = builder.GeneratedRoomObjects[generatedIndex++];
                if (instance == null)
                    throw new InvalidOperationException("Generated room object contains a null entry.");

                if (!roomInstancesByPlacementId.TryAdd(placement.placementId, instance))
                {
                    throw new InvalidOperationException(
                        $"Duplicate room object placement id: {placement.placementId}");
                }

                string expectedName =
                    $"RoomObject_{roomPlacement.PlacementId}_{placement.placementId}";
                if (instance.name != expectedName)
                {
                    throw new InvalidOperationException(
                        $"Unexpected generated room object name. Expected={expectedName}, Actual={instance.name}");
                }

                Vector2Int worldCell = roomPlacement.Origin + placement.localCell;
                Vector3 expectedPosition = builder.FloorTilemap.GetCellCenterWorld(
                    new Vector3Int(worldCell.x, worldCell.y, 0));
                GridLayout grid = builder.FloorTilemap.layoutGrid;
                Vector3 localOffset = new(placement.localOffset.x, placement.localOffset.y, 0f);
                expectedPosition += grid != null
                    ? grid.transform.TransformVector(localOffset)
                    : localOffset;
                if ((instance.transform.position - expectedPosition).sqrMagnitude > 0.0001f)
                {
                    throw new InvalidOperationException(
                        $"Generated room object '{expectedName}' is not at its authored cell position. " +
                        $"Expected={expectedPosition}, Actual={instance.transform.position}");
                }

                switch (placement.kind)
                {
                    case RoomObjectKind.Prop:
                        if (instance.GetComponentInChildren<StatueShortcut>(true) != null)
                        {
                            VerifySacrificeRewardAlcove(instance, expectedName);
                            sacrificeRewardAlcoveCount++;
                        }
                        break;
                    case RoomObjectKind.Monster:
                        MonsterSpawnContainer spawnPoint =
                            instance.GetComponent<MonsterSpawnContainer>();
                        if (spawnPoint == null ||
                            !spawnPoint.TryResolveMonsterPrefab(0, out GameObject resolvedPrefab) ||
                            resolvedPrefab == null)
                        {
                            throw new InvalidOperationException(
                                $"{expectedName} is missing a resolvable deferred monster spawn source.");
                        }

                        if (placement.monsterStageSet != null &&
                            spawnPoint.StageMonsterSet != placement.monsterStageSet)
                        {
                            throw new InvalidOperationException(
                                $"{expectedName} lost its authored role StageMonsterSetSO.");
                        }
                        monsterCount++;
                        break;
                    case RoomObjectKind.Chest:
                        TreasureChest chest = instance.GetComponentInChildren<TreasureChest>(true);
                        if (chest == null)
                            throw new InvalidOperationException($"{expectedName} is missing TreasureChest.");
                        chestCount++;
                        break;
                    case RoomObjectKind.Portal:
                        if (instance.GetComponentInChildren<ScenePortal>(true) == null)
                            throw new InvalidOperationException($"{expectedName} is missing ScenePortal.");
                        portalCount++;
                        break;
                }
            }

            Dictionary<ChestMonsterKillLock, int> expectedLockCounts = new();
            for (int objectIndex = 0; objectIndex < placements.Count; objectIndex++)
            {
                RoomObjectPlacementData placement = placements[objectIndex];
                if (placement.kind != RoomObjectKind.Monster ||
                    string.IsNullOrWhiteSpace(placement.linkedChestLockPlacementId))
                {
                    continue;
                }

                if (!roomInstancesByPlacementId.TryGetValue(
                        placement.linkedChestLockPlacementId,
                        out GameObject chestInstance))
                {
                    throw new InvalidOperationException(
                        $"Monster '{placement.placementId}' references missing chest " +
                        $"'{placement.linkedChestLockPlacementId}'.");
                }

                ChestMonsterKillLock chestLock =
                    chestInstance.GetComponentInChildren<ChestMonsterKillLock>(true);
                if (chestLock == null)
                {
                    throw new InvalidOperationException(
                        $"Linked chest '{placement.linkedChestLockPlacementId}' is missing ChestMonsterKillLock.");
                }

                expectedLockCounts.TryGetValue(chestLock, out int expectedCount);
                expectedLockCounts[chestLock] = expectedCount + 1;
            }

            foreach (KeyValuePair<ChestMonsterKillLock, int> pair in expectedLockCounts)
            {
                ChestMonsterKillLock chestLock = pair.Key;
                int expectedCount = pair.Value;
                if (Application.isPlaying &&
                    (chestLock.IsUnlocked || chestLock.RemainingAliveCount != expectedCount))
                {
                    throw new InvalidOperationException(
                        $"Room {roomPlacement.PlacementId} Kill Lock registration is invalid. " +
                        $"LinkedMonsters={expectedCount}, Remaining={chestLock.RemainingAliveCount}, " +
                        $"Unlocked={chestLock.IsUnlocked}");
                }

                killLockChestCount++;
            }
        }

        if (generatedIndex != builder.GeneratedRoomObjects.Count)
        {
            throw new InvalidOperationException(
                $"Unexpected generated room object count. Data={generatedIndex}, " +
                $"Instances={builder.GeneratedRoomObjects.Count}");
        }

        if (monsterCount <= 0 ||
            chestCount <= 0 ||
            killLockChestCount <= 0 ||
            portalCount <= 0 ||
            sacrificeRewardAlcoveCount <= 0)
        {
            throw new InvalidOperationException(
                $"Prototype requires all object kinds. Monsters={monsterCount}, " +
                $"Chests={chestCount}, KillLockChests={killLockChestCount}, " +
                $"Portals={portalCount}, SacrificeRewardAlcoves={sacrificeRewardAlcoveCount}");
        }

        Debug.Log(
            $"Room object verification passed. Total={generatedIndex}, " +
            $"Monsters={monsterCount}, Chests={chestCount}, " +
            $"KillLockChests={killLockChestCount}, Portals={portalCount}, " +
            $"SacrificeRewardAlcoves={sacrificeRewardAlcoveCount}");
    }

    private static void VerifySacrificeRewardAlcove(GameObject root, string context)
    {
        StatueShortcut[] statues = root.GetComponentsInChildren<StatueShortcut>(true);
        DoorObject[] doors = root.GetComponentsInChildren<DoorObject>(true);
        TreasureChest[] chests = root.GetComponentsInChildren<TreasureChest>(true);
        if (statues.Length != 1 || doors.Length != 1 || chests.Length != 1)
        {
            throw new InvalidOperationException(
                $"Sacrifice reward alcove '{context}' must contain one statue, door, and chest. " +
                $"Statues={statues.Length}, Doors={doors.Length}, Chests={chests.Length}");
        }

        StatueShortcut statue = statues[0];
        DoorObject door = doors[0];
        if (statue.TargetDoor != door)
        {
            throw new InvalidOperationException(
                $"Sacrifice reward alcove '{context}' statue is not linked to its internal door.");
        }

        if (door.doorType != DoorObject.DoorType.Locked || door.isPermanent)
        {
            throw new InvalidOperationException(
                $"Sacrifice reward alcove '{context}' door must be Locked and non-permanent.");
        }

        RoomCompositePoseAuthoring composite =
            root.GetComponent<RoomCompositePoseAuthoring>();
        string slotFailureReason = string.Empty;
        if (composite == null || !composite.TryValidateSlots(out slotFailureReason))
        {
            throw new InvalidOperationException(
                $"Sacrifice reward alcove '{context}' has an invalid composite pose contract: " +
                (composite == null ? "component is missing." : slotFailureReason));
        }

        ValidateSacrificeRewardPoseSlot<StatueShortcut>(
            composite,
            "OfferingStatue",
            context);
        ValidateSacrificeRewardPoseSlot<DoorObject>(
            composite,
            "RewardDoor",
            context);
        ValidateSacrificeRewardPoseSlot<TreasureChest>(
            composite,
            "TreasureChest",
            context);

        SerializedObject serializedStatue = new(statue);
        SerializedProperty costType = serializedStatue.FindProperty("costType");
        SerializedProperty costAmount = serializedStatue.FindProperty("costAmount");
        if (costType == null ||
            costAmount == null ||
            costType.enumValueIndex != (int)StatueShortcut.CostType.MagicStone ||
            costAmount.intValue != 5)
        {
            throw new InvalidOperationException(
                $"Sacrifice reward alcove '{context}' must cost five Magic Stones.");
        }
    }

    /// <summary>
    /// 책임:
    /// 제물 보상 복합 슬롯이 기대한 기능 오브젝트의 루트 Transform을 가리키는지 검증한다.
    /// </summary>
    private static void ValidateSacrificeRewardPoseSlot<TComponent>(
        RoomCompositePoseAuthoring composite,
        string slotId,
        string context)
        where TComponent : Component
    {
        if (!composite.TryGetSlot(slotId, out RoomCompositePoseSlotData slot) ||
            slot.Target == null ||
            slot.Target.GetComponentInChildren<TComponent>(true) == null)
        {
            throw new InvalidOperationException(
                $"Sacrifice reward alcove '{context}' pose slot '{slotId}' is invalid.");
        }
    }

    private static void VerifySocketWallAndColliderState(
        DungeonLayoutResult layout,
        DungeonRoomBuilder builder)
    {
        Tilemap wallTilemap = builder.WallTilemap;
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0 || wallTilemap.gameObject.layer != groundLayer)
            throw new InvalidOperationException("Generated Wall Tilemap is not on the Ground layer.");

        TilemapCollider2D tilemapCollider = wallTilemap.GetComponent<TilemapCollider2D>();
        CompositeCollider2D compositeCollider = wallTilemap.GetComponent<CompositeCollider2D>();
        Rigidbody2D rigidbody = wallTilemap.GetComponent<Rigidbody2D>();
        if (tilemapCollider == null ||
            compositeCollider == null ||
            rigidbody == null ||
            rigidbody.bodyType != RigidbodyType2D.Static ||
            tilemapCollider.compositeOperation != Collider2D.CompositeOperation.Merge)
        {
            throw new InvalidOperationException(
                "Generated Wall requires a merged TilemapCollider2D, CompositeCollider2D, and static Rigidbody2D.");
        }

        tilemapCollider.ProcessTilemapChanges();
        Physics2D.SyncTransforms();

        int connectedSocketCount = 0;
        int sealedSocketCount = 0;
        int authoredClosedSocketCount = 0;
        int connectedSocketCellCount = 0;
        int sealedSocketCellCount = 0;
        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement placement = layout.Rooms[roomIndex];
            List<RoomSocketData> sockets = placement.Template.LayoutData.sockets;
            if (sockets == null)
                continue;

            for (int socketIndex = 0; socketIndex < sockets.Count; socketIndex++)
            {
                RoomSocketData socket = sockets[socketIndex];
                int width = RoomSocketGeometry.ResolveWidth(socket);
                if (width != RoomSocketGeometry.RequiredWidth)
                {
                    throw new InvalidOperationException(
                        $"Room sample socket has unsupported width: " +
                        $"room={placement.Template.name}, socket={socketIndex}, width={width}");
                }

                authoredClosedSocketCount++;
                bool connected = IsConnectedSocket(layout, placement.PlacementId, socketIndex);
                if (connected)
                    connectedSocketCount++;
                else
                    sealedSocketCount++;

                for (int cellIndex = 0; cellIndex < width; cellIndex++)
                {
                    Vector2Int localCell = RoomSocketGeometry.GetLocalCell(socket, cellIndex);
                    bool hasAuthoredFloor = HasTileAtLocalCell(
                        placement.Template.BuildData.floorTiles,
                        localCell);
                    bool hasAuthoredWall = HasTileAtLocalCell(
                        placement.Template.BuildData.wallTiles,
                        localCell);
                    if (!hasAuthoredFloor || !hasAuthoredWall)
                    {
                        throw new InvalidOperationException(
                            $"Room sample socket cell is not authored closed with Floor and Wall: " +
                            $"room={placement.Template.name}, socket={socketIndex}, cell={localCell}");
                    }

                    Vector2Int worldCell = placement.Origin + localCell;
                    Vector3Int tileCell = new(worldCell.x, worldCell.y, 0);
                    bool hasWallTile = wallTilemap.HasTile(tileCell);
                    Vector3 socketCellCenter = wallTilemap.GetCellCenterWorld(tileCell);
                    bool hasBlocker = HasGeneratedSocketBlockerAt(
                        builder,
                        socketCellCenter,
                        groundLayer);

                    if (connected)
                    {
                        connectedSocketCellCount++;
                        if (hasWallTile || hasBlocker)
                        {
                            throw new InvalidOperationException(
                                $"Connected 2-cell socket was not fully opened: " +
                                $"room={placement.PlacementId}, socket={socketIndex}, cell={worldCell}");
                        }

                        continue;
                    }

                    sealedSocketCellCount++;
                    if (!hasWallTile || !hasBlocker)
                    {
                        throw new InvalidOperationException(
                            $"Unused 2-cell socket is not fully sealed: " +
                            $"room={placement.PlacementId}, socket={socketIndex}, cell={worldCell}");
                    }
                }
            }
        }

        int expectedConnectedCellCount = connectedSocketCount * RoomSocketGeometry.RequiredWidth;
        int expectedSealedCellCount = sealedSocketCount * RoomSocketGeometry.RequiredWidth;
        if (connectedSocketCellCount != expectedConnectedCellCount ||
            sealedSocketCellCount != expectedSealedCellCount)
        {
            throw new InvalidOperationException(
                $"Unexpected 2-cell socket coverage. ConnectedCells={connectedSocketCellCount}/" +
                $"{expectedConnectedCellCount}, SealedCells={sealedSocketCellCount}/{expectedSealedCellCount}");
        }

        if (connectedSocketCount != layout.Connections.Count * 2 || sealedSocketCount <= 0)
        {
            throw new InvalidOperationException(
                $"Unexpected socket closure totals. ConnectedEndpoints={connectedSocketCount}, " +
                $"SealedEndpoints={sealedSocketCount}");
        }

        if (authoredClosedSocketCount != connectedSocketCount + sealedSocketCount)
        {
            throw new InvalidOperationException(
                $"Unexpected authored socket total. AuthoredClosed={authoredClosedSocketCount}, " +
                $"RuntimeTotal={connectedSocketCount + sealedSocketCount}");
        }

        if (builder.GeneratedSocketBlockers.Count != sealedSocketCount)
        {
            throw new InvalidOperationException(
                $"Unexpected generated socket blocker count. " +
                $"Blockers={builder.GeneratedSocketBlockers.Count}, SealedEndpoints={sealedSocketCount}");
        }

        Debug.Log(
            $"Socket wall verification passed. " +
            $"SocketWidth={RoomSocketGeometry.RequiredWidth}, " +
            $"AuthoredClosed={authoredClosedSocketCount}, " +
            $"ConnectedEndpoints={connectedSocketCount}, SealedEndpoints={sealedSocketCount}, " +
            $"ConnectedCells={connectedSocketCellCount}, SealedCells={sealedSocketCellCount}, " +
            $"Blockers={builder.GeneratedSocketBlockers.Count}");
    }

    private static Vector3 GetConnectionEndpointCenter(
        DungeonLayoutResult layout,
        DungeonSocketConnection connection,
        Tilemap floorTilemap,
        bool firstEndpoint)
    {
        DungeonRoomPlacement firstPlacement = FindPlacement(
            layout,
            connection.FirstRoomPlacementId);
        DungeonRoomPlacement secondPlacement = FindPlacement(
            layout,
            connection.SecondRoomPlacementId);
        RoomSocketData firstSocket = firstPlacement.Template.LayoutData.sockets[connection.FirstSocketIndex];
        RoomSocketData secondSocket = secondPlacement.Template.LayoutData.sockets[connection.SecondSocketIndex];
        return firstEndpoint
            ? GetSocketCenter(firstPlacement, firstSocket, floorTilemap)
            : GetSocketCenter(secondPlacement, secondSocket, floorTilemap);
    }

    private static DungeonRoomPlacement FindPlacement(
        DungeonLayoutResult layout,
        int placementId)
    {
        for (int i = 0; i < layout.Rooms.Count; i++)
        {
            if (layout.Rooms[i].PlacementId == placementId)
                return layout.Rooms[i];
        }

        throw new InvalidOperationException($"Missing room placement: {placementId}");
    }

    private static Vector3 GetSocketCenter(
        DungeonRoomPlacement placement,
        RoomSocketData socket,
        Tilemap tilemap)
    {
        int width = RoomSocketGeometry.ResolveWidth(socket);
        Vector2Int firstCell = placement.Origin + RoomSocketGeometry.GetLocalCell(socket, 0);
        Vector2Int lastCell = placement.Origin + RoomSocketGeometry.GetLocalCell(socket, width - 1);
        Vector3 firstCenter = tilemap.GetCellCenterWorld(new Vector3Int(firstCell.x, firstCell.y, 0));
        Vector3 lastCenter = tilemap.GetCellCenterWorld(new Vector3Int(lastCell.x, lastCell.y, 0));
        return Vector3.Lerp(firstCenter, lastCenter, 0.5f);
    }

    private static bool HasTileAtLocalCell(List<RoomTileData> tiles, Vector2Int localCell)
    {
        if (tiles == null)
            return false;

        for (int i = 0; i < tiles.Count; i++)
        {
            RoomTileData tile = tiles[i];
            if (tile.localCell == localCell && tile.tile != null)
                return true;
        }

        return false;
    }

    private static bool HasGeneratedSocketBlockerAt(
        DungeonRoomBuilder builder,
        Vector3 worldPosition,
        int requiredLayer)
    {
        for (int i = 0; i < builder.GeneratedSocketBlockers.Count; i++)
        {
            BoxCollider2D blocker = builder.GeneratedSocketBlockers[i];
            if (blocker == null ||
                !blocker.enabled ||
                blocker.isTrigger ||
                blocker.gameObject.layer != requiredLayer)
            {
                continue;
            }

            if (blocker.OverlapPoint(worldPosition))
                return true;
        }

        return false;
    }

    private static bool IsConnectedSocket(
        DungeonLayoutResult layout,
        int roomPlacementId,
        int socketIndex)
    {
        for (int i = 0; i < layout.Connections.Count; i++)
        {
            DungeonSocketConnection connection = layout.Connections[i];
            if ((connection.FirstRoomPlacementId == roomPlacementId &&
                 connection.FirstSocketIndex == socketIndex) ||
                (connection.SecondRoomPlacementId == roomPlacementId &&
                 connection.SecondSocketIndex == socketIndex))
            {
                return true;
            }
        }

        return false;
    }

    private static void VerifySingleSocketBossPlacement(DungeonLayoutResult layout)
    {
        DungeonRoomPlacement bossPlacement = layout.Rooms[layout.Rooms.Count - 1];
        RoomLayoutData bossLayout = bossPlacement.Template.LayoutData;
        if (bossLayout.roomType != RoomType.Boss ||
            bossLayout.sockets == null ||
            bossLayout.sockets.Count != 1)
        {
            throw new InvalidOperationException(
                "The final prototype Boss room must be a connected single-socket terminal room.");
        }

        bool bossSocketConsumed = false;
        for (int i = 0; i < layout.Connections.Count; i++)
        {
            DungeonSocketConnection connection = layout.Connections[i];
            if ((connection.FirstRoomPlacementId == bossPlacement.PlacementId &&
                 connection.FirstSocketIndex == 0) ||
                (connection.SecondRoomPlacementId == bossPlacement.PlacementId &&
                 connection.SecondSocketIndex == 0))
            {
                bossSocketConsumed = true;
                break;
            }
        }

        if (!bossSocketConsumed)
            throw new InvalidOperationException("The single Boss socket was not consumed by a connection.");
    }

    private static void VerifySingleSocketBossAcrossSeeds(
        RoomThemeLibrarySO library,
        int minimumCorridorLength = PrototypeMinimumCorridorLength,
        float corridorLengthPerRoomCell = PrototypeCorridorLengthPerRoomCell,
        int corridorLengthVariation = PrototypeCorridorLengthVariation)
    {
        DungeonLayoutAssembler assembler = new();
        for (int seed = 0; seed < SingleSocketSeedSweepCount; seed++)
        {
            DungeonLayoutResult result = assembler.Assemble(
                library,
                seed,
                6,
                includeBossRoom: true,
                maxPlacementAttemptsPerRoom: 512,
                minimumCorridorLength,
                corridorLengthPerRoomCell,
                corridorLengthVariation);

            if (!result.IsComplete || result.Rooms.Count != 6 || result.Connections.Count != 5)
            {
                throw new InvalidOperationException(
                    $"Single-socket Boss seed sweep failed at Seed={seed}: {result.FailureReason}");
            }

            VerifySingleSocketBossPlacement(result);
        }

        Debug.Log(
            $"Single-socket Boss layout verification passed for {SingleSocketSeedSweepCount} seeds.");
    }

    /// <summary>
    /// 책임:
    /// - 탐색 정책이 여러 Seed에서도 방 수, 보스 거리, 분기, 순환, 필수 방 역할과 단일 Boss 소켓 계약을 지키는지 검증한다.
    /// </summary>
    private static void VerifyGraphFirstPolicyAcrossSeeds(
        RoomThemeLibrarySO library,
        DungeonLayoutPolicySO policy,
        int roomCount,
        int maxPlacementAttemptsPerRoom,
        int minimumCorridorLength,
        float corridorLengthPerRoomCell,
        int corridorLengthVariation)
    {
        int shortestCorridorLength = int.MaxValue;
        int longestCorridorLength = 0;
        int relaxedLayoutCount = 0;
        string longestCorridorContext = string.Empty;
        for (int seed = 0; seed < GraphPolicySeedSweepCount; seed++)
        {
            DungeonLayoutResult result = new DungeonGraphLayoutAssembler().Assemble(
                library,
                policy,
                seed,
                roomCount,
                maxPlacementAttemptsPerRoom,
                minimumCorridorLength,
                corridorLengthPerRoomCell,
                corridorLengthVariation);
            int expectedConnections = roomCount - 1 + result.CycleConnectionCount;
            if (!result.IsComplete ||
                !result.UsesGraphFirstLayout ||
                result.Rooms.Count != roomCount ||
                result.Connections.Count != expectedConnections ||
                result.BossGraphDistance < policy.MinimumBossGraphDistance ||
                result.BossGraphDistance > policy.MaximumBossGraphDistance ||
                result.MeaningfulBranchCount < policy.MinimumMeaningfulBranches ||
                result.MeaningfulBranchCount > policy.MaximumMeaningfulBranches ||
                result.CycleConnectionCount < policy.MinimumCycleConnections ||
                result.CycleConnectionCount > policy.MaximumCycleConnections ||
                CountRoomType(result, RoomType.Treasure) != policy.TreasureRoomCount ||
                CountRoomType(result, RoomType.Event) != policy.EventRoomCount ||
                CountRoomType(result, RoomType.Shop) != policy.ShopRoomCount ||
                CountRoomType(result, RoomType.Combat) < policy.MinimumCombatRoomCount)
            {
                throw new InvalidOperationException(
                    $"Graph-first policy seed sweep failed at Seed={seed}: {result.FailureReason} " +
                    $"Rooms={result.Rooms.Count}, Connections={result.Connections.Count}, " +
                    $"BossDistance={result.BossGraphDistance}, Branches={result.MeaningfulBranchCount}, " +
                    $"Cycles={result.CycleConnectionCount}.");
            }

            VerifySingleSocketBossPlacement(result);
            if (result.UsedCorridorLengthRelaxation)
                relaxedLayoutCount++;
            for (int connectionIndex = 0;
                 connectionIndex < result.Connections.Count;
                 connectionIndex++)
            {
                int corridorLength = result.Connections[connectionIndex].CorridorLength;
                shortestCorridorLength = Mathf.Min(shortestCorridorLength, corridorLength);
                if (corridorLength > longestCorridorLength)
                {
                    longestCorridorLength = corridorLength;
                    DungeonSocketConnection connection = result.Connections[connectionIndex];
                    DungeonRoomPlacement first = FindPlacement(
                        result,
                        connection.FirstRoomPlacementId);
                    DungeonRoomPlacement second = FindPlacement(
                        result,
                        connection.SecondRoomPlacementId);
                    longestCorridorContext =
                        $"Seed={seed}, Connection={connectionIndex}, " +
                        $"First={first.Template.LayoutData.roomId}{first.WorldBounds}, " +
                        $"Second={second.Template.LayoutData.roomId}{second.WorldBounds}";
                }
            }
        }

        Debug.Log(
            $"Graph-first policy verification passed for {GraphPolicySeedSweepCount} seeds. " +
            $"Rooms={roomCount}, BossDistance={policy.MinimumBossGraphDistance}..{policy.MaximumBossGraphDistance}, " +
            $"Branches={policy.MinimumMeaningfulBranches}..{policy.MaximumMeaningfulBranches}, " +
            $"Cycles={policy.MinimumCycleConnections}..{policy.MaximumCycleConnections}, " +
            $"CorridorLength={shortestCorridorLength}..{longestCorridorLength}, " +
            $"RelaxedLayouts={relaxedLayoutCount}/{GraphPolicySeedSweepCount}, " +
            $"Longest=({longestCorridorContext}).");
    }

    private static int CountRoomType(DungeonLayoutResult layout, RoomType roomType)
    {
        int count = 0;
        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            if (layout.Rooms[roomIndex].Template.LayoutData.roomType == roomType)
                count++;
        }

        return count;
    }

    private static List<RoomSocketData> CreateFourWaySockets(Vector2Int size)
    {
        int width = RoomSocketGeometry.RequiredWidth;
        int middleX = Mathf.Clamp((size.x - width) / 2, 0, Mathf.Max(0, size.x - width));
        int middleY = Mathf.Clamp((size.y - width) / 2, 0, Mathf.Max(0, size.y - width));
        return new List<RoomSocketData>
        {
            new() { socketId = "Up", localCell = new Vector2Int(middleX, size.y - 1), direction = RoomSocketDirection.Up, width = width },
            new() { socketId = "Right", localCell = new Vector2Int(size.x - 1, middleY), direction = RoomSocketDirection.Right, width = width },
            new() { socketId = "Down", localCell = new Vector2Int(middleX, 0), direction = RoomSocketDirection.Down, width = width },
            new() { socketId = "Left", localCell = new Vector2Int(0, middleY), direction = RoomSocketDirection.Left, width = width }
        };
    }

    private static List<RoomSocketData> CreateSingleSocket(
        Vector2Int size,
        RoomSocketDirection direction)
    {
        List<RoomSocketData> sockets = CreateFourWaySockets(size);
        for (int i = 0; i < sockets.Count; i++)
        {
            if (sockets[i].direction == direction)
                return new List<RoomSocketData> { sockets[i] };
        }

        throw new InvalidOperationException($"Unsupported room socket direction: {direction}");
    }

    private static List<RoomSocketData> CreateOppositeSockets(
        Vector2Int size,
        RoomSocketDirection firstDirection,
        RoomSocketDirection secondDirection)
    {
        if (DirectionToVector(firstDirection) + DirectionToVector(secondDirection) != Vector2Int.zero)
        {
            throw new InvalidOperationException(
                $"Room socket directions must be opposite: {firstDirection}, {secondDirection}");
        }

        List<RoomSocketData> allSockets = CreateFourWaySockets(size);
        List<RoomSocketData> result = new(2);
        for (int i = 0; i < allSockets.Count; i++)
        {
            RoomSocketDirection direction = allSockets[i].direction;
            if (direction == firstDirection || direction == secondDirection)
                result.Add(allSockets[i]);
        }

        if (result.Count != 2)
            throw new InvalidOperationException("Failed to create opposite room sockets.");

        return result;
    }

    private static Vector2Int DirectionToVector(RoomSocketDirection direction)
    {
        return direction switch
        {
            RoomSocketDirection.Up => Vector2Int.up,
            RoomSocketDirection.Right => Vector2Int.right,
            RoomSocketDirection.Down => Vector2Int.down,
            RoomSocketDirection.Left => Vector2Int.left,
            _ => Vector2Int.zero
        };
    }

    private static RoomObjectPlacementData CreateObjectPlacement(
        string placementId,
        RoomObjectKind kind,
        GameObject prefab,
        Vector2Int localCell,
        string linkedChestLockPlacementId = null)
    {
        return new RoomObjectPlacementData
        {
            placementId = placementId,
            kind = kind,
            prefab = prefab,
            localCell = localCell,
            localOffset = Vector2.zero,
            localRotationDegrees = 0f,
            localScale = prefab != null ? prefab.transform.localScale : Vector3.one,
            linkedChestLockPlacementId = kind == RoomObjectKind.Monster
                ? linkedChestLockPlacementId ?? string.Empty
                : string.Empty
        };
    }

    private static GameObject LoadConfiguredRoomObjectPrefab(
        string roomAssetPath,
        string placementId,
        RoomObjectKind expectedKind,
        bool requireChestKillLock)
    {
        RoomTemplateSO room = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(roomAssetPath);
        List<RoomObjectPlacementData> placements = room != null
            ? room.BuildData.objectPlacements
            : null;
        if (placements != null)
        {
            for (int i = 0; i < placements.Count; i++)
            {
                RoomObjectPlacementData placement = placements[i];
                if (placement.placementId != placementId || placement.kind != expectedKind)
                    continue;

                GameObject configuredPrefab = placement.prefab;
                if (configuredPrefab == null ||
                    (requireChestKillLock &&
                     configuredPrefab.GetComponentInChildren<ChestMonsterKillLock>(true) == null))
                {
                    break;
                }

                return configuredPrefab;
            }
        }

        throw new InvalidOperationException(
            $"Room data '{roomAssetPath}' must configure placement '{placementId}' " +
            "with a prefab that owns its required gameplay behavior and presentation.");
    }

    private static GameObject LoadRequiredObjectPrefab(
        string assetPath,
        RoomObjectKind kind)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        bool compatible = prefab != null &&
            (kind switch
            {
                RoomObjectKind.Monster => prefab.GetComponentInChildren<Enemy>(true) != null,
                RoomObjectKind.Chest => prefab.GetComponentInChildren<TreasureChest>(true) != null,
                RoomObjectKind.Portal => prefab.GetComponentInChildren<ScenePortal>(true) != null,
                RoomObjectKind.Prop => true,
                _ => false
            });
        if (!compatible)
            throw new InvalidOperationException($"Room object prefab is missing or invalid: {assetPath}");

        return prefab;
    }

    private static Vector2Int GetRoomCenterCell(Vector2Int roomSize)
    {
        return new Vector2Int(
            Mathf.Clamp(roomSize.x / 2, 1, roomSize.x - 2),
            Mathf.Clamp(roomSize.y / 2, 1, roomSize.y - 2));
    }

    private static Vector2Int GetKillLockChestCell(Vector2Int roomSize)
    {
        Vector2Int center = GetRoomCenterCell(roomSize);
        return new Vector2Int(
            Mathf.Clamp(center.x + 2, 1, roomSize.x - 2),
            center.y);
    }

    private static List<RoomTileData> CreateInteriorFloorTiles(
        Vector2Int roomSize,
        TileBase floorTile)
    {
        List<RoomTileData> tiles = new();
        for (int y = 1; y < roomSize.y - 1; y++)
        {
            for (int x = 1; x < roomSize.x - 1; x++)
            {
                tiles.Add(new RoomTileData
                {
                    localCell = new Vector2Int(x, y),
                    tile = floorTile
                });
            }
        }

        return tiles;
    }

    private static List<RoomTileData> CreateBoundaryWallTiles(
        Vector2Int roomSize,
        TileBase wallTile)
    {
        List<RoomTileData> tiles = new();
        for (int y = 0; y < roomSize.y; y++)
        {
            for (int x = 0; x < roomSize.x; x++)
            {
                bool isBoundary =
                    x == 0 ||
                    y == 0 ||
                    x == roomSize.x - 1 ||
                    y == roomSize.y - 1;
                if (!isBoundary)
                    continue;

                tiles.Add(new RoomTileData
                {
                    localCell = new Vector2Int(x, y),
                    tile = wallTile
                });
            }
        }

        return tiles;
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

    private static void EnsureTileAtCell(
        List<RoomTileData> tiles,
        Vector2Int localCell,
        TileBase fallbackTile)
    {
        if (fallbackTile == null)
            return;

        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i].localCell == localCell)
                return;
        }

        tiles.Add(new RoomTileData
        {
            localCell = localCell,
            tile = fallbackTile
        });
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindByName(roots[i].transform, objectName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static Transform FindByName(Transform current, string objectName)
    {
        if (current.name == objectName)
            return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindByName(current.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
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
    }
}
