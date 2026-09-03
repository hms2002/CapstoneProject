using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임:
/// - Shadow, Dragon, Slime 테마의 기존 방/절차 복도 데이터에서 실제 사용 중인 타일을 수집해 가로·세로 장식 예제 모듈을 만든다.
/// - 예제 장식 프로필과 모듈을 정해진 경로에 반복 설치하고 각 DungeonGenerationProfileSO에 연결한다.
/// - 세로 전용 설치에서는 기존 가로 모듈과 기획자가 편집한 데이터를 덮어쓰지 않는다.
/// </summary>
public static class CorridorDecorationExampleInstaller
{
    private const string RootFolder =
        "Assets/_Project/Data/Dungeon/CorridorDecorations";
    private const string PropRoot =
        "Assets/_Project/Art/Sprites/ThirdParty/WeaponAndSandBack/" +
        "Pixel Art Top Down - Basic/Prefab/Props";

    private static readonly ThemeSpec[] Themes =
    {
        new(
            "Shadow",
            "Assets/_Project/Data/Dungeon/GenerationProfiles/" +
            "ProceduralShadowGenerationProfile.asset",
            "Assets/_Project/Scenes/ShadowCorridor.unity",
            "Assets/_Project/Scenes/ProceduralShadowCorridor.unity",
            $"{PropRoot}/PF Props - Gravestone 02.prefab"),
        new(
            "Dragon",
            "Assets/_Project/Data/Dungeon/GenerationProfiles/" +
            "ProceduralDragonGenerationProfile.asset",
            "Assets/_Project/Scenes/DragonCorridor.unity",
            "Assets/_Project/Scenes/ProceduralDragonCorridor.unity",
            $"{PropRoot}/PF Props - Rune Pillar Broken.prefab"),
        new(
            "Slime",
            "Assets/_Project/Data/Dungeon/GenerationProfiles/" +
            "ProceduralSlimeGenerationProfile.asset",
            "Assets/_Project/Scenes/SlimeCorridor.unity",
            "Assets/_Project/Scenes/ProceduralSlimeCorridor.unity",
            $"{PropRoot}/PF Props - Barrel 01.prefab")
    };

    private static readonly ModuleSpec[] Modules =
    {
        new("Start_02", CorridorDecorationAxis.Horizontal, CorridorDecorationModuleRole.Start, 2),
        new("Middle_03", CorridorDecorationAxis.Horizontal, CorridorDecorationModuleRole.Middle, 3),
        new("Landmark_04", CorridorDecorationAxis.Horizontal, CorridorDecorationModuleRole.Landmark, 4),
        new("Filler_01", CorridorDecorationAxis.Horizontal, CorridorDecorationModuleRole.Filler, 1),
        new("End_02", CorridorDecorationAxis.Horizontal, CorridorDecorationModuleRole.End, 2),
        new("Short_02", CorridorDecorationAxis.Horizontal, CorridorDecorationModuleRole.Short, 2),
        new("Vertical_Start_02", CorridorDecorationAxis.Vertical, CorridorDecorationModuleRole.Start, 2),
        new("Vertical_Middle_03", CorridorDecorationAxis.Vertical, CorridorDecorationModuleRole.Middle, 3),
        new("Vertical_Landmark_04", CorridorDecorationAxis.Vertical, CorridorDecorationModuleRole.Landmark, 4),
        new("Vertical_Filler_01", CorridorDecorationAxis.Vertical, CorridorDecorationModuleRole.Filler, 1),
        new("Vertical_End_02", CorridorDecorationAxis.Vertical, CorridorDecorationModuleRole.End, 2),
        new("Vertical_Short_02", CorridorDecorationAxis.Vertical, CorridorDecorationModuleRole.Short, 2)
    };

    [MenuItem("Tools/Dungeon/Examples/Install Theme Corridor Decoration Examples")]
    public static void InstallExamples()
    {
        EnsureFolder(RootFolder);
        var installedProfiles = new List<CorridorDecorationProfileSO>(Themes.Length);
        for (int themeIndex = 0; themeIndex < Themes.Length; themeIndex++)
            installedProfiles.Add(InstallTheme(Themes[themeIndex], verticalOnly: false));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = installedProfiles.Count > 0 ? installedProfiles[0] : null;
        Debug.Log(
            $"Installed {installedProfiles.Count} corridor decoration example profiles " +
            $"with {Modules.Length} modules per theme under '{RootFolder}'.");
    }

    [MenuItem("Tools/Dungeon/Examples/Install Missing Vertical Corridor Decoration Examples")]
    public static void InstallMissingVerticalExamples()
    {
        EnsureFolder(RootFolder);
        var installedProfiles = new List<CorridorDecorationProfileSO>(Themes.Length);
        for (int themeIndex = 0; themeIndex < Themes.Length; themeIndex++)
            installedProfiles.Add(InstallTheme(Themes[themeIndex], verticalOnly: true));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = installedProfiles.Count > 0 ? installedProfiles[0] : null;
        Debug.Log(
            $"Installed missing vertical corridor examples for {installedProfiles.Count} themes " +
            $"without overwriting registered horizontal modules under '{RootFolder}'.");
    }

    [MenuItem("Tools/Dungeon/Examples/Validate Theme Corridor Decoration Examples")]
    public static void ValidateExamples()
    {
        var errors = new List<string>();
        for (int themeIndex = 0; themeIndex < Themes.Length; themeIndex++)
            ValidateTheme(Themes[themeIndex], errors);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Corridor decoration example validation failed:\n- " +
                string.Join("\n- ", errors));
        }

        Debug.Log(
            $"Validated {Themes.Length} corridor decoration profiles and " +
            $"{Themes.Length * Modules.Length} example modules.");
    }

    [MenuItem("Tools/Dungeon/Examples/Validate Completed Corridor Previews")]
    public static void ValidateCompletedPreviews()
    {
        bool workspaceWasOpen = RoomAuthoringWorkspace.IsOpen;
        var errors = new List<string>();
        try
        {
            if (Application.isBatchMode && !workspaceWasOpen)
            {
                EditorSceneManager.OpenScene(
                    Themes[0].ReferenceScenePath,
                    OpenSceneMode.Single);
            }

            for (int themeIndex = 0; themeIndex < Themes.Length; themeIndex++)
            {
                ThemeSpec spec = Themes[themeIndex];
                DungeonGenerationProfileSO generationProfile =
                    AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(
                        spec.GenerationProfilePath);
                CorridorDecorationProfileSO decorationProfile =
                    generationProfile != null
                        ? generationProfile.CorridorDecorationProfile
                        : null;
                foreach (CorridorDecorationAxis axis in
                         new[] { CorridorDecorationAxis.Horizontal, CorridorDecorationAxis.Vertical })
                {
                    CorridorDecorationCompletedPreviewResult result =
                        CorridorDecorationCompletedPreview.Show(
                            generationProfile,
                            decorationProfile,
                            16 + themeIndex * 3,
                            20260902 + themeIndex,
                            connectionIndex: 0,
                            axis: axis);
                    if (!result.Success)
                        errors.Add($"{spec.ThemeId}/{axis}: {result.Message}");
                }
            }
        }
        finally
        {
            CorridorDecorationCompletedPreview.Clear();
            if (!workspaceWasOpen)
                RoomAuthoringWorkspace.Close(confirmDiscard: false);
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Completed corridor preview validation failed:\n- " +
                string.Join("\n- ", errors));
        }

        Debug.Log(
            $"Validated completed corridor previews for {Themes.Length} themes.");
    }

    private static CorridorDecorationProfileSO InstallTheme(
        ThemeSpec spec,
        bool verticalOnly)
    {
        DungeonGenerationProfileSO generationProfile =
            AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(
                spec.GenerationProfilePath);
        if (generationProfile == null)
        {
            throw new InvalidOperationException(
                $"Corridor example installation requires '{spec.GenerationProfilePath}'.");
        }

        RoomThemeLibrarySO roomLibrary = generationProfile.RoomLibrary;
        if (roomLibrary == null)
        {
            throw new InvalidOperationException(
                $"'{generationProfile.name}' has no room library.");
        }

        ThemeTilePalette palette = ThemeTilePalette.Collect(
            roomLibrary,
            spec.ReferenceScenePath,
            spec.ProceduralScenePath);
        if (!palette.Has(RoomTileLayerKind.Floor) ||
            !palette.Has(RoomTileLayerKind.Wall))
        {
            throw new InvalidOperationException(
                $"'{roomLibrary.name}' must contain Floor and Wall tiles before examples can be created.");
        }

        string themeFolder = $"{RootFolder}/{spec.ThemeId}";
        EnsureFolder(themeFolder);
        string profilePath =
            $"{themeFolder}/{spec.ThemeId}_CorridorDecorationProfile.asset";
        CorridorDecorationProfileSO decorationProfile =
            LoadOrCreate<CorridorDecorationProfileSO>(profilePath);
        GameObject landmarkProp = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PropPath);
        var moduleAssets = verticalOnly
            ? new List<CorridorDecorationModuleSO>(decorationProfile.Modules)
            : new List<CorridorDecorationModuleSO>(Modules.Length);
        for (int moduleIndex = 0; moduleIndex < Modules.Length; moduleIndex++)
        {
            ModuleSpec moduleSpec = Modules[moduleIndex];
            if (verticalOnly && moduleSpec.Axis != CorridorDecorationAxis.Vertical)
                continue;

            string moduleId = $"{spec.ThemeId}_{moduleSpec.Suffix}";
            string modulePath = $"{themeFolder}/{moduleId}.asset";
            CorridorDecorationModuleSO module =
                AssetDatabase.LoadAssetAtPath<CorridorDecorationModuleSO>(modulePath);
            bool wasMissing = module == null;
            if (wasMissing)
                module = LoadOrCreate<CorridorDecorationModuleSO>(modulePath);

            if (!verticalOnly || wasMissing)
            {
                GameObject prop = moduleSpec.Role == CorridorDecorationModuleRole.Landmark
                    ? landmarkProp
                    : null;
                module.EditorSetData(
                    moduleId,
                    moduleSpec.Axis,
                    moduleSpec.Role,
                    moduleSpec.Length,
                    CreateBuildData(spec.ThemeId, moduleSpec, palette, prop));
                EditorUtility.SetDirty(module);
            }

            if (!moduleAssets.Contains(module))
                moduleAssets.Add(module);
        }

        decorationProfile.EditorConfigure(1, moduleAssets);
        EditorUtility.SetDirty(decorationProfile);

        generationProfile.EditorSetCorridorDecorationProfile(decorationProfile);
        EditorUtility.SetDirty(generationProfile);
        return decorationProfile;
    }

    private static void ValidateTheme(ThemeSpec spec, ICollection<string> errors)
    {
        DungeonGenerationProfileSO generationProfile =
            AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(
                spec.GenerationProfilePath);
        string themeFolder = $"{RootFolder}/{spec.ThemeId}";
        string profilePath =
            $"{themeFolder}/{spec.ThemeId}_CorridorDecorationProfile.asset";
        CorridorDecorationProfileSO decorationProfile =
            AssetDatabase.LoadAssetAtPath<CorridorDecorationProfileSO>(profilePath);
        if (generationProfile == null || decorationProfile == null)
        {
            errors.Add($"{spec.ThemeId}: generation or decoration profile is missing.");
            return;
        }

        if (generationProfile.CorridorDecorationProfile != decorationProfile)
            errors.Add($"{spec.ThemeId}: generation profile is not linked to its example profile.");
        if (decorationProfile.Modules.Count != Modules.Length)
            errors.Add($"{spec.ThemeId}: expected {Modules.Length} registered modules.");

        bool hasDecorationLayer = false;
        for (int moduleIndex = 0; moduleIndex < Modules.Length; moduleIndex++)
        {
            ModuleSpec moduleSpec = Modules[moduleIndex];
            string moduleId = $"{spec.ThemeId}_{moduleSpec.Suffix}";
            CorridorDecorationModuleSO module =
                AssetDatabase.LoadAssetAtPath<CorridorDecorationModuleSO>(
                    $"{themeFolder}/{moduleId}.asset");
            if (module == null)
            {
                errors.Add($"{spec.ThemeId}: {moduleId} is missing.");
                continue;
            }

            if (module.ModuleId != moduleId ||
                module.Axis != moduleSpec.Axis ||
                module.Role != moduleSpec.Role ||
                module.Length != moduleSpec.Length)
            {
                errors.Add($"{moduleId}: metadata does not match its example specification.");
            }

            ValidateModuleFootprint(module, errors);
            RoomBuildData build = module.BuildData;
            hasDecorationLayer |= HasTiles(build.floorDetailTiles) ||
                                  HasTiles(build.groundDecorationTiles) ||
                                  HasTiles(build.wallDetailTiles) ||
                                  HasTiles(build.foregroundTiles) ||
                                  HasTiles(build.overlayFxTiles);
            if (module.Role == CorridorDecorationModuleRole.Landmark &&
                (build.objectPlacements == null ||
                 build.objectPlacements.Count != 1 ||
                 build.objectPlacements[0].kind != RoomObjectKind.Prop ||
                 build.objectPlacements[0].prefab == null))
            {
                errors.Add($"{moduleId}: landmark Pivot prop is missing or invalid.");
            }
        }

        if (!hasDecorationLayer)
            errors.Add($"{spec.ThemeId}: no example decoration layer tile was found.");
    }

    private static void ValidateModuleFootprint(
        CorridorDecorationModuleSO module,
        ICollection<string> errors)
    {
        RoomBuildData build = module.BuildData;
        if (build.floorTiles == null || build.floorTiles.Count != module.Length * 2)
            errors.Add($"{module.ModuleId}: Floor must cover both passable cells across its length.");
        if (build.wallTiles == null || build.wallTiles.Count != module.Length * 2)
            errors.Add($"{module.ModuleId}: Wall must cover both corridor borders across its length.");

        for (int layerIndex = 0;
             layerIndex < RoomTileLayerContract.OrderedLayers.Count;
             layerIndex++)
        {
            RoomTileLayerKind layer = RoomTileLayerContract.OrderedLayers[layerIndex];
            List<RoomTileData> tiles = build.GetTiles(layer);
            if (tiles == null)
                continue;

            for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
            {
                Vector2Int cell = tiles[tileIndex].localCell;
                bool inside = module.Axis == CorridorDecorationAxis.Horizontal
                    ? cell.x >= 0 && cell.x < module.Length &&
                      cell.y >= -1 && cell.y <= 2
                    : cell.y >= 0 && cell.y < module.Length &&
                      cell.x >= -1 && cell.x <= 2;
                if (tiles[tileIndex].tile == null ||
                    !inside)
                {
                    errors.Add(
                        $"{module.ModuleId}: invalid {layer} tile at {cell}.");
                }
            }
        }
    }

    private static bool HasTiles(IReadOnlyCollection<RoomTileData> tiles)
    {
        return tiles != null && tiles.Count > 0;
    }

    private static RoomBuildData CreateBuildData(
        string themeId,
        ModuleSpec module,
        ThemeTilePalette palette,
        GameObject landmarkProp)
    {
        RoomBuildData build = CreateEmptyBuildData();
        FillBaseLayer(
            build.underFloorTiles,
            palette.First(RoomTileLayerKind.UnderFloor, module.Axis),
            module.Length,
            0,
            1,
            module.Axis);
        FillBaseLayer(
            build.floorTiles,
            palette.First(RoomTileLayerKind.Floor, module.Axis),
            module.Length,
            0,
            1,
            module.Axis);
        FillBaseLayer(
            build.wallTiles,
            palette.First(RoomTileLayerKind.Wall, module.Axis),
            module.Length,
            -1,
            2,
            module.Axis);

        int anchorForward = ResolveAnchorForward(module.Role, module.Length);
        AddOptionalTileByProgress(
            build.floorDetailTiles,
            palette.First(RoomTileLayerKind.FloorDetail, module.Axis),
            anchorForward,
            0,
            module.Axis);
        AddOptionalTileByProgress(
            build.groundDecorationTiles,
            palette.First(RoomTileLayerKind.GroundDecoration, module.Axis),
            anchorForward,
            1,
            module.Axis);
        AddOptionalTileByProgress(
            build.wallDetailTiles,
            palette.First(RoomTileLayerKind.WallDetail, module.Axis),
            anchorForward,
            2,
            module.Axis);
        AddOptionalTileByProgress(
            build.foregroundTiles,
            palette.First(RoomTileLayerKind.Foreground, module.Axis),
            anchorForward,
            2,
            module.Axis);
        AddOptionalTileByProgress(
            build.overlayFxTiles,
            palette.First(RoomTileLayerKind.OverlayFX, module.Axis),
            anchorForward,
            0,
            module.Axis);

        if (module.Role == CorridorDecorationModuleRole.Landmark && landmarkProp != null)
        {
            build.objectPlacements.Add(new RoomObjectPlacementData
            {
                placementId = $"{themeId}_CorridorLandmarkProp",
                kind = RoomObjectKind.Prop,
                prefab = landmarkProp,
                localCell = ProgressCell(anchorForward, 2, module.Axis),
                localOffset = Vector2.zero,
                localRotationDegrees = 0f,
                localScale = landmarkProp.transform.localScale,
                linkedChestLockPlacementId = string.Empty
            });
        }

        return build;
    }

    private static RoomBuildData CreateEmptyBuildData()
    {
        return new RoomBuildData
        {
            underFloorTiles = new List<RoomTileData>(),
            floorTiles = new List<RoomTileData>(),
            floorDetailTiles = new List<RoomTileData>(),
            groundDecorationTiles = new List<RoomTileData>(),
            wallTiles = new List<RoomTileData>(),
            wallDetailTiles = new List<RoomTileData>(),
            foregroundTiles = new List<RoomTileData>(),
            overlayFxTiles = new List<RoomTileData>(),
            objectPlacements = new List<RoomObjectPlacementData>(),
            travelEndpointPlacements = new List<RoomTravelEndpointPlacementData>()
        };
    }

    private static void FillBaseLayer(
        ICollection<RoomTileData> destination,
        TileBase tile,
        int length,
        int firstLateral,
        int secondLateral,
        CorridorDecorationAxis axis)
    {
        if (tile == null)
            return;

        for (int progress = 0; progress < length; progress++)
        {
            destination.Add(new RoomTileData
            {
                localCell = ProgressCell(progress, firstLateral, axis),
                tile = tile
            });
            destination.Add(new RoomTileData
            {
                localCell = ProgressCell(progress, secondLateral, axis),
                tile = tile
            });
        }
    }

    private static void AddOptionalTileByProgress(
        ICollection<RoomTileData> destination,
        TileBase tile,
        int progress,
        int lateral,
        CorridorDecorationAxis axis)
    {
        if (tile == null)
            return;

        destination.Add(new RoomTileData
        {
            localCell = ProgressCell(progress, lateral, axis),
            tile = tile
        });
    }

    private static Vector2Int ProgressCell(
        int progress,
        int lateral,
        CorridorDecorationAxis axis)
    {
        return axis == CorridorDecorationAxis.Horizontal
            ? new Vector2Int(progress, lateral)
            : new Vector2Int(lateral, progress);
    }

    private static int ResolveAnchorForward(CorridorDecorationModuleRole role, int length)
    {
        return role switch
        {
            CorridorDecorationModuleRole.Start => 0,
            CorridorDecorationModuleRole.End => Mathf.Max(0, length - 1),
            _ => Mathf.Clamp(length / 2, 0, Mathf.Max(0, length - 1))
        };
    }

    private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, assetPath);
        return asset;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = folderPath.Substring(0, folderPath.LastIndexOf('/'));
        string folderName = folderPath.Substring(folderPath.LastIndexOf('/') + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    /// <summary>
    /// 책임 : 한 테마 예제 설치에 필요한 안정 ID, 생성 프로필과 랜드마크 프리팹 경로를 묶는다.
    /// </summary>
    private readonly struct ThemeSpec
    {
        public string ThemeId { get; }
        public string GenerationProfilePath { get; }
        public string ReferenceScenePath { get; }
        public string ProceduralScenePath { get; }
        public string PropPath { get; }

        public ThemeSpec(
            string themeId,
            string generationProfilePath,
            string referenceScenePath,
            string proceduralScenePath,
            string propPath)
        {
            ThemeId = themeId;
            GenerationProfilePath = generationProfilePath;
            ReferenceScenePath = referenceScenePath;
            ProceduralScenePath = proceduralScenePath;
            PropPath = propPath;
        }
    }

    /// <summary>
    /// 책임 : 생성할 예제 모듈의 파일 접미사, 전용 축, 조립 역할과 진행축 길이를 정의한다.
    /// </summary>
    private readonly struct ModuleSpec
    {
        public string Suffix { get; }
        public CorridorDecorationAxis Axis { get; }
        public CorridorDecorationModuleRole Role { get; }
        public int Length { get; }

        public ModuleSpec(
            string suffix,
            CorridorDecorationAxis axis,
            CorridorDecorationModuleRole role,
            int length)
        {
            Suffix = suffix;
            Axis = axis;
            Role = role;
            Length = length;
        }
    }

    /// <summary>
    /// 책임:
    /// - 테마 방 라이브러리의 각 고정 레이어에서 가장 많이 사용한 타일을 찾아 예제 제작 팔레트로 제공한다.
    /// - 같은 빈도에서는 에셋 경로를 기준으로 선택해 설치 결과를 반복 가능하게 유지한다.
    /// </summary>
    private sealed class ThemeTilePalette
    {
        private readonly Dictionary<RoomTileLayerKind, TileBase> tiles = new();
        private TileBase horizontalCorridorWall;
        private TileBase verticalCorridorWall;

        public bool Has(RoomTileLayerKind layer) => First(layer) != null;

        public TileBase First(RoomTileLayerKind layer)
        {
            return tiles.TryGetValue(layer, out TileBase tile) ? tile : null;
        }

        public TileBase First(RoomTileLayerKind layer, CorridorDecorationAxis axis)
        {
            if (layer != RoomTileLayerKind.Wall)
                return First(layer);

            TileBase directional = axis == CorridorDecorationAxis.Horizontal
                ? horizontalCorridorWall
                : verticalCorridorWall;
            return directional != null ? directional : First(layer);
        }

        public static ThemeTilePalette Collect(
            RoomThemeLibrarySO library,
            string referenceScenePath,
            string proceduralScenePath)
        {
            var palette = new ThemeTilePalette();
            for (int layerIndex = 0;
                 layerIndex < RoomTileLayerContract.OrderedLayers.Count;
                 layerIndex++)
            {
                RoomTileLayerKind layer = RoomTileLayerContract.OrderedLayers[layerIndex];
                TileBase tile = FindMostFrequentTile(library, layer);
                if (tile != null)
                    palette.tiles[layer] = tile;
            }

            palette.SupplementFromReferenceScene(referenceScenePath);
            palette.SupplementCorridorWalls(proceduralScenePath);
            return palette;
        }

        private void SupplementCorridorWalls(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath) ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForSampling = !scene.IsValid() || !scene.isLoaded;
            if (openedForSampling)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    DungeonRoomBuilder builder =
                        roots[rootIndex].GetComponentInChildren<DungeonRoomBuilder>(true);
                    if (builder == null)
                        continue;

                    horizontalCorridorWall = FirstNonNull(
                        builder.HorizontalCorridorWallVariants,
                        builder.CorridorWallTile);
                    verticalCorridorWall = FirstNonNull(
                        builder.VerticalCorridorWallVariants,
                        builder.CorridorWallTile);
                    return;
                }
            }
            finally
            {
                if (openedForSampling && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static TileBase FirstNonNull(
            IReadOnlyList<TileBase> candidates,
            TileBase fallback)
        {
            if (candidates != null)
            {
                for (int index = 0; index < candidates.Count; index++)
                {
                    if (candidates[index] != null)
                        return candidates[index];
                }
            }

            return fallback;
        }

        private void SupplementFromReferenceScene(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath) ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForSampling = !scene.IsValid() || !scene.isLoaded;
            if (openedForSampling)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    Tilemap[] tilemaps =
                        roots[rootIndex].GetComponentsInChildren<Tilemap>(true);
                    for (int tilemapIndex = 0; tilemapIndex < tilemaps.Length; tilemapIndex++)
                        SupplementFromLegacyTilemap(tilemaps[tilemapIndex]);
                }
            }
            finally
            {
                if (openedForSampling && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private void SupplementFromLegacyTilemap(Tilemap tilemap)
        {
            if (tilemap == null)
                return;

            string normalizedName = tilemap.name.Replace("_", string.Empty).ToLowerInvariant();
            TileBase tile = FindStableUsedTile(tilemap);
            if (tile == null)
                return;

            if (normalizedName.Contains("walladorn"))
            {
                SetIfMissing(RoomTileLayerKind.WallDetail, tile);
                return;
            }

            if (normalizedName.Contains("grounduserup") ||
                normalizedName.Contains("adornmentup"))
            {
                SetIfMissing(RoomTileLayerKind.Foreground, tile);
                return;
            }

            if (normalizedName.Contains("groundunderad") ||
                normalizedName.Contains("underadornment") ||
                normalizedName.Contains("adornmentdown"))
            {
                SetIfMissing(RoomTileLayerKind.FloorDetail, tile);
                SetIfMissing(RoomTileLayerKind.GroundDecoration, tile);
            }
        }

        private void SetIfMissing(RoomTileLayerKind layer, TileBase tile)
        {
            if (tile != null && !tiles.ContainsKey(layer))
                tiles[layer] = tile;
        }

        private static TileBase FindStableUsedTile(Tilemap tilemap)
        {
            int usedCount = tilemap.GetUsedTilesCount();
            if (usedCount <= 0)
                return null;

            var usedTiles = new TileBase[usedCount];
            int resultCount = tilemap.GetUsedTilesNonAlloc(usedTiles);
            TileBase best = null;
            string bestPath = string.Empty;
            for (int tileIndex = 0; tileIndex < resultCount; tileIndex++)
            {
                TileBase candidate = usedTiles[tileIndex];
                if (candidate == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(candidate);
                if (best == null || string.CompareOrdinal(path, bestPath) < 0)
                {
                    best = candidate;
                    bestPath = path;
                }
            }

            return best;
        }

        private static TileBase FindMostFrequentTile(
            RoomThemeLibrarySO library,
            RoomTileLayerKind layer)
        {
            var counts = new Dictionary<TileBase, int>();
            IReadOnlyList<RoomTemplateSO> rooms = library.Rooms;
            if (rooms == null)
                return null;

            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                RoomTemplateSO room = rooms[roomIndex];
                List<RoomTileData> source = room != null
                    ? room.BuildData.GetTiles(layer)
                    : null;
                if (source == null)
                    continue;

                for (int tileIndex = 0; tileIndex < source.Count; tileIndex++)
                {
                    TileBase tile = source[tileIndex].tile;
                    if (tile == null)
                        continue;
                    counts.TryGetValue(tile, out int count);
                    counts[tile] = count + 1;
                }
            }

            TileBase best = null;
            int bestCount = -1;
            string bestPath = string.Empty;
            foreach (KeyValuePair<TileBase, int> pair in counts)
            {
                string path = AssetDatabase.GetAssetPath(pair.Key);
                if (pair.Value > bestCount ||
                    (pair.Value == bestCount &&
                     string.CompareOrdinal(path, bestPath) < 0))
                {
                    best = pair.Key;
                    bestCount = pair.Value;
                    bestPath = path;
                }
            }

            return best;
        }
    }
}
