using System;
using System.Collections.Generic;
using System.IO;
using CapstonePresentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RouteSetLoadManifestBuilderWindow : EditorWindow
{
    private const string LoadingAssetDirectory = "Assets/_Project/Data/SceneFlow/LoadingManifests";
    private const string BootstrapConfigSourceAssetPath = LoadingBootstrapConfigSO.SourceAssetPath;
    private static readonly string[] IgnoredPathFragments =
    {
        "/Editor/",
        "/Tests/",
        "/Test/",
        "/Gizmos/",
        "/Resources/Loading/",
        "/_Project/Data/SceneFlow/LoadingManifests/"
    };

    private static readonly string[] IgnoredBootPathFragments =
    {
        "/_Project/Data/Abilities/",
        "/_Project/Data/Items/Relics/",
        "/_Project/Data/Items/Weapons/",
        "/_Project/Data/Loot/Tables/GraveLootTable.asset",
        "/_Project/Data/Loot/Tables/Table_Stage",
        "/_Project/Data/Monsters/",
        "/_Project/Data/SceneFlow/Routes/",
        "/_Project/Prefabs/Bosses/",
        "/_Project/Prefabs/Items/Relics/",
        "/_Project/Prefabs/Items/Weapons/",
        "/_Project/Prefabs/Monsters/",
        "/_Project/Art/Sprites/Bosses/",
        "/_Project/Art/Sprites/Items/",
        "/_Project/Art/Sprites/ThirdParty/WeaponAndSandBack/",
        "/_Project/Art/Sprites/UI/Tutorial/",
        "/_Project/Audio/Bosses/",
        "/_Project/Audio/Monsters/",
        "/Datas/Dialogue/SpeechData/",
        "/Datas/Dialogue/NPC/DialogueTheme/ShadowBoss",
        "/Data/Dialogue/NPC/DarkLord",
        "/Audio/BGM/Dragon",
        "/Audio/BGM/Boss",
        "/Audio/BGM/ShadowCorridor",
        "/Audio/BGM/Slime",
        "/Sprites/Characters/Boss/",
        "/Sprites/UI/Dialogue/Boss1"
    };

    private static readonly string[] FirstRunIntroExcludedPathFragments =
    {
        "/_Project/Data/Loot/",
        "/_Project/Data/Monsters/",
        "/_Project/Data/SceneFlow/Routes/",
        "/_Project/Prefabs/Bosses/DragonBoss/",
        "/_Project/Prefabs/Bosses/ShadowBoss/",
        "/_Project/Prefabs/Bosses/SlimeQueen/",
        "/_Project/Prefabs/Monsters/CommonCorridor/",
        "/_Project/Prefabs/Monsters/ShadowCorridor/",
        "/_Project/Prefabs/Monsters/SlimeCorridor/",
        "/_Project/Audio/BGM/Dragon",
        "/_Project/Audio/BGM/ShadowCorridor",
        "/_Project/Audio/BGM/Slime",
        "/_Project/Audio/Bosses/Dragon",
        "/_Project/Audio/Bosses/Shadow",
        "/_Project/Audio/Bosses/Slime"
    };

    private readonly struct AssetBuckets
    {
        public AssetBuckets(
            List<GameObject> prefabs,
            List<PresentationCueSO> cues,
            List<ScriptableObject> dataAssets,
            List<UnityEngine.Object> extraAssets)
        {
            Prefabs = prefabs;
            Cues = cues;
            DataAssets = dataAssets;
            ExtraAssets = extraAssets;
        }

        public List<GameObject> Prefabs { get; }
        public List<PresentationCueSO> Cues { get; }
        public List<ScriptableObject> DataAssets { get; }
        public List<UnityEngine.Object> ExtraAssets { get; }
    }

    private CorridorBossRouteSetSO routeSet;
    private LoadManifestSO bootManifest;
    private LoadManifestSO firstRunIntroManifest;
    private string bootSeedSceneName = "ProtoTypeHub";
    private string firstRunIntroSceneNames = "TutorialCorridor,DarkLord_Tutorial,ProtoTypeHub";
    private bool saveAssetsAfterBuild = true;
    private bool verboseLogging;
    private Vector2 scrollPosition;
    private string statusMessage;

    [MenuItem("Tools/Loading/RouteSet Manifest Builder")]
    public static void ShowWindow()
    {
        GetWindow<RouteSetLoadManifestBuilderWindow>("RouteSet Manifest");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("RouteSet Load Manifest Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Corridor/Boss 씬을 스캔해서 RouteSetLoadManifestSO의 shared/corridor/boss manifest를 자동으로 갱신합니다.",
            MessageType.Info);

        EditorGUILayout.Space(6f);
        routeSet = (CorridorBossRouteSetSO)EditorGUILayout.ObjectField("Route Set", routeSet, typeof(CorridorBossRouteSetSO), false);
        bootManifest = (LoadManifestSO)EditorGUILayout.ObjectField("Boot Exclusion Manifest", bootManifest, typeof(LoadManifestSO), false);
        bootSeedSceneName = EditorGUILayout.TextField("Boot Seed Scene", bootSeedSceneName);
        firstRunIntroManifest = (LoadManifestSO)EditorGUILayout.ObjectField("FirstRun Intro Manifest", firstRunIntroManifest, typeof(LoadManifestSO), false);
        firstRunIntroSceneNames = EditorGUILayout.TextField("FirstRun Intro Scenes", firstRunIntroSceneNames);
        saveAssetsAfterBuild = EditorGUILayout.ToggleLeft("Save Assets After Build", saveAssetsAfterBuild);
        verboseLogging = EditorGUILayout.ToggleLeft("Verbose Logging", verboseLogging);

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Build Release Loading Set", GUILayout.Height(32f)))
            BuildReleaseLoadingSet();

        EditorGUILayout.HelpBox(
            "Build order: Boot -> FirstRun Intro -> all RouteSets -> Addressable Registry. Addressables content build is still a separate release step.",
            MessageType.None);

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Build Selected RouteSet", GUILayout.Height(28f)))
                BuildSelectedRouteSet();

            if (GUILayout.Button("Build All RouteSets", GUILayout.Height(28f)))
                BuildAllRouteSets();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Build Boot Manifest From Seed Scene", GUILayout.Height(24f)))
                BuildBootManifestFromSeedScene();

            if (GUILayout.Button("Build FirstRun Intro Manifest", GUILayout.Height(24f)))
                BuildFirstRunIntroManifestFromSeedScenes();

            using (new EditorGUI.DisabledScope(bootManifest == null))
            {
                if (GUILayout.Button("Ping Boot Manifest", GUILayout.Height(24f)))
                    EditorGUIUtility.PingObject(bootManifest);
            }
        }

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(routeSet == null || routeSet.LoadManifest == null))
        {
            if (GUILayout.Button("Ping Manifest", GUILayout.Height(24f)) && routeSet != null)
                EditorGUIUtility.PingObject(routeSet.LoadManifest);
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        }

        EditorGUILayout.EndScrollView();
    }

    private void BuildSelectedRouteSet()
    {
        if (routeSet == null)
        {
            statusMessage = "RouteSet이 지정되지 않았습니다.";
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            BuildRouteSetManifest(routeSet);
            statusMessage = $"Built manifest for {routeSet.name}.";
        }
        catch (Exception ex)
        {
            statusMessage = $"Build failed: {ex.Message}";
            Debug.LogException(ex);
        }
        finally
        {
            RestoreSceneSetup(originalSetup);
        }
    }

    private void BuildAllRouteSets()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        CorridorBossRouteSetSO[] routeSets = FindAllRouteSets();
        if (routeSets.Length == 0)
        {
            statusMessage = "CorridorBossRouteSetSO 자산을 찾지 못했습니다.";
            return;
        }

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        int builtCount = 0;

        try
        {
            for (int i = 0; i < routeSets.Length; i++)
            {
                BuildRouteSetManifest(routeSets[i]);
                builtCount++;
            }

            statusMessage = $"Built manifests for {builtCount} RouteSets.";
        }
        catch (Exception ex)
        {
            statusMessage = $"Bulk build failed: {ex.Message}";
            Debug.LogException(ex);
        }
        finally
        {
            RestoreSceneSetup(originalSetup);
        }
    }

    private void BuildReleaseLoadingSet()
    {
        if (string.IsNullOrWhiteSpace(bootSeedSceneName))
        {
            statusMessage = "Boot seed scene 이름이 비어 있습니다.";
            return;
        }

        List<string> firstRunSceneNames = ParseSceneNames(firstRunIntroSceneNames);
        if (firstRunSceneNames.Count == 0)
        {
            statusMessage = "FirstRun Intro scene 이름이 비어 있습니다.";
            return;
        }

        CorridorBossRouteSetSO[] routeSets = FindAllRouteSets();
        if (routeSets.Length == 0)
        {
            statusMessage = "CorridorBossRouteSetSO 자산을 찾지 못했습니다.";
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        int builtRouteSetCount = 0;

        try
        {
            EditorUtility.DisplayProgressBar("Build Release Loading Set", "Building Boot manifest...", 0.1f);
            int bootAssetCount = BuildBootManifestCore();

            EditorUtility.DisplayProgressBar("Build Release Loading Set", "Building FirstRun Intro manifest...", 0.3f);
            int firstRunAssetCount = BuildFirstRunIntroManifestCore(firstRunSceneNames, out int bootExcludedCount);

            for (int i = 0; i < routeSets.Length; i++)
            {
                float progress = 0.4f + (0.45f * i / routeSets.Length);
                EditorUtility.DisplayProgressBar(
                    "Build Release Loading Set",
                    $"Building RouteSet manifest {i + 1}/{routeSets.Length}: {routeSets[i].name}",
                    progress);

                BuildRouteSetManifest(routeSets[i]);
                builtRouteSetCount++;
            }

            EditorUtility.DisplayProgressBar("Build Release Loading Set", "Building Addressable registry...", 0.9f);
            string registrySummary = LoadingAddressableRegistryBuilder.BuildRegistry();
            if (string.IsNullOrWhiteSpace(registrySummary))
                throw new InvalidOperationException("Addressable registry build failed.");

            if (saveAssetsAfterBuild)
                AssetDatabase.SaveAssets();

            statusMessage =
                $"Built release loading set. Boot={bootAssetCount}, FirstRun={firstRunAssetCount} assets, FirstRun boot-excluded={bootExcludedCount}, RouteSets={builtRouteSetCount}. Addressables content build is still required.";
        }
        catch (Exception ex)
        {
            statusMessage = $"Release loading set build failed after {builtRouteSetCount} RouteSets: {ex.Message}";
            Debug.LogException(ex);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            RestoreSceneSetup(originalSetup);
        }
    }

    private void BuildBootManifestFromSeedScene()
    {
        if (string.IsNullOrWhiteSpace(bootSeedSceneName))
        {
            statusMessage = "Boot seed scene 이름이 비어 있습니다.";
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            int assetCount = BuildBootManifestCore();
            statusMessage = $"Built boot manifest from {bootSeedSceneName}.";

            if (verboseLogging)
            {
                Debug.Log(
                    $"[RouteSetLoadManifestBuilder] Built boot manifest from {bootSeedSceneName}. assetCount={assetCount}",
                    bootManifest);
            }
        }
        catch (Exception ex)
        {
            statusMessage = $"Boot build failed: {ex.Message}";
            Debug.LogException(ex);
        }
        finally
        {
            RestoreSceneSetup(originalSetup);
        }
    }

    private int BuildBootManifestCore()
    {
        string scenePath = FindScenePathByName(bootSeedSceneName);
        if (string.IsNullOrEmpty(scenePath))
            throw new InvalidOperationException($"Boot seed scene '{bootSeedSceneName}' 경로를 찾지 못했습니다.");

        Dictionary<string, UnityEngine.Object> sceneAssets = CollectSceneAssets(scenePath);
        ApplyBootSpecificExclusions(sceneAssets);
        EnsureBootManifest();
        EnsureBootstrapConfigAsset();
        WriteManifest(bootManifest, CategorizeAssets(new List<UnityEngine.Object>(sceneAssets.Values)), LoadScopeKind.Boot);

        if (saveAssetsAfterBuild)
            AssetDatabase.SaveAssets();

        return sceneAssets.Count;
    }

    private void BuildFirstRunIntroManifestFromSeedScenes()
    {
        List<string> sceneNames = ParseSceneNames(firstRunIntroSceneNames);
        if (sceneNames.Count == 0)
        {
            statusMessage = "FirstRun Intro scene 이름이 비어 있습니다.";
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ResolveBootstrapManifestReferencesIfMissing();
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            int assetCount = BuildFirstRunIntroManifestCore(sceneNames, out int bootExcludedCount);
            statusMessage = $"Built FirstRun Intro manifest from {sceneNames.Count} scenes.";

            if (verboseLogging)
            {
                Debug.Log(
                    $"[RouteSetLoadManifestBuilder] Built FirstRun Intro manifest. scenes={sceneNames.Count}, assetCount={assetCount}, bootExcluded={bootExcludedCount}",
                    firstRunIntroManifest);
            }
        }
        catch (Exception ex)
        {
            statusMessage = $"FirstRun Intro build failed: {ex.Message}";
            Debug.LogException(ex);
        }
        finally
        {
            RestoreSceneSetup(originalSetup);
        }
    }

    private int BuildFirstRunIntroManifestCore(List<string> sceneNames, out int bootExcludedCount)
    {
        if (sceneNames == null || sceneNames.Count == 0)
            throw new InvalidOperationException("FirstRun Intro scene 이름이 비어 있습니다.");

        ResolveBootstrapManifestReferencesIfMissing();

        var firstRunAssets = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
        for (int i = 0; i < sceneNames.Count; i++)
        {
            string sceneName = sceneNames[i];
            string scenePath = FindScenePathByName(sceneName);
            if (string.IsNullOrEmpty(scenePath))
                throw new InvalidOperationException($"FirstRun Intro scene '{sceneName}' 경로를 찾지 못했습니다.");

            MergeAssets(firstRunAssets, CollectSceneAssets(scenePath));
        }

        var excludedAssetKeys = new HashSet<string>(StringComparer.Ordinal);
        AddManifestAssetKeys(bootManifest, excludedAssetKeys);
        bootExcludedCount = excludedAssetKeys.Count;
        ApplyExclusions(firstRunAssets, excludedAssetKeys);
        ApplyFirstRunIntroSpecificExclusions(firstRunAssets);

        EnsureFirstRunIntroManifest();
        EnsureBootstrapConfigAsset();
        WriteManifest(
            firstRunIntroManifest,
            CategorizeAssets(new List<UnityEngine.Object>(firstRunAssets.Values)),
            LoadScopeKind.FirstRunIntro);

        if (saveAssetsAfterBuild)
            AssetDatabase.SaveAssets();

        return firstRunAssets.Count;
    }

    private void BuildRouteSetManifest(CorridorBossRouteSetSO targetRouteSet)
    {
        if (targetRouteSet == null)
            throw new InvalidOperationException("RouteSet이 null입니다.");

        ResolveBootstrapManifestReferencesIfMissing();

        string corridorScenePath = FindScenePathByName(targetRouteSet.CorridorSceneName);
        string bossScenePath = FindScenePathByName(targetRouteSet.BossSceneName);

        if (string.IsNullOrEmpty(corridorScenePath))
            throw new InvalidOperationException($"Corridor scene '{targetRouteSet.CorridorSceneName}' 경로를 찾지 못했습니다.");

        if (string.IsNullOrEmpty(bossScenePath))
            throw new InvalidOperationException($"Boss scene '{targetRouteSet.BossSceneName}' 경로를 찾지 못했습니다.");

        Dictionary<string, UnityEngine.Object> corridorAssets = CollectSceneAssets(corridorScenePath);
        Dictionary<string, UnityEngine.Object> bossAssets = CollectSceneAssets(bossScenePath);
        HashSet<string> excludedAssetKeys = CollectExcludedAssetKeys(targetRouteSet);

        ApplyExclusions(corridorAssets, excludedAssetKeys);
        ApplyExclusions(bossAssets, excludedAssetKeys);

        List<UnityEngine.Object> sharedAssets = new();
        List<UnityEngine.Object> corridorOnlyAssets = new();
        List<UnityEngine.Object> bossOnlyAssets = new();

        BuildAssetPartitions(corridorAssets, bossAssets, sharedAssets, corridorOnlyAssets, bossOnlyAssets);

        RouteSetLoadManifestSO routeManifest = EnsureRouteManifest(targetRouteSet);
        LoadManifestSO sharedManifest = EnsureChildManifest(routeManifest, "sharedManifest", $"{targetRouteSet.name}_Shared");
        LoadManifestSO corridorManifest = EnsureChildManifest(routeManifest, "corridorManifest", $"{targetRouteSet.name}_Corridor");
        LoadManifestSO bossManifest = EnsureChildManifest(routeManifest, "bossManifest", $"{targetRouteSet.name}_Boss");

        WriteManifest(sharedManifest, CategorizeAssets(sharedAssets), LoadScopeKind.RouteSet);
        WriteManifest(corridorManifest, CategorizeAssets(corridorOnlyAssets), LoadScopeKind.RouteSet);
        WriteManifest(bossManifest, CategorizeAssets(bossOnlyAssets), LoadScopeKind.RouteSet);

        EditorUtility.SetDirty(routeManifest);
        EditorUtility.SetDirty(targetRouteSet);

        if (saveAssetsAfterBuild)
            AssetDatabase.SaveAssets();

        if (verboseLogging)
        {
            Debug.Log(
                $"[RouteSetLoadManifestBuilder] Built {targetRouteSet.name}. excluded={excludedAssetKeys.Count}, shared={sharedAssets.Count}, corridor={corridorOnlyAssets.Count}, boss={bossOnlyAssets.Count}",
                targetRouteSet);
        }
    }

    private HashSet<string> CollectExcludedAssetKeys(CorridorBossRouteSetSO targetRouteSet)
    {
        var excludedKeys = new HashSet<string>(StringComparer.Ordinal);

        AddManifestAssetKeys(bootManifest, excludedKeys);
        AddManifestAssetKeys(firstRunIntroManifest, excludedKeys);

        List<LoadManifestSO> runCommonManifests = FindRunCommonManifests(targetRouteSet);
        for (int i = 0; i < runCommonManifests.Count; i++)
            AddManifestAssetKeys(runCommonManifests[i], excludedKeys);

        return excludedKeys;
    }

    private static CorridorBossRouteSetSO[] FindAllRouteSets()
    {
        string[] guids = AssetDatabase.FindAssets("t:CorridorBossRouteSetSO");
        if (guids == null || guids.Length == 0)
            return Array.Empty<CorridorBossRouteSetSO>();

        Array.Sort(guids, CompareAssetGuidByPath);

        var routeSets = new List<CorridorBossRouteSetSO>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            CorridorBossRouteSetSO routeSetAsset = AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(path);
            if (routeSetAsset != null)
                routeSets.Add(routeSetAsset);
        }

        return routeSets.ToArray();
    }

    private static int CompareAssetGuidByPath(string leftGuid, string rightGuid)
    {
        string leftPath = AssetDatabase.GUIDToAssetPath(leftGuid);
        string rightPath = AssetDatabase.GUIDToAssetPath(rightGuid);
        return string.Compare(leftPath, rightPath, StringComparison.OrdinalIgnoreCase);
    }

    private static void AddManifestAssetKeys(LoadManifestSO manifest, HashSet<string> keys)
    {
        if (manifest == null || keys == null)
            return;

        foreach (UnityEngine.Object asset in manifest.EnumerateReferencedAssets())
        {
            if (asset == null)
                continue;

            keys.Add(GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString());
        }
    }

    private static List<LoadManifestSO> FindRunCommonManifests(CorridorBossRouteSetSO targetRouteSet)
    {
        var manifests = new List<LoadManifestSO>();
        if (targetRouteSet == null)
            return manifests;

        string[] guids = AssetDatabase.FindAssets("t:RunRouteCatalogSO");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            RunRouteCatalogSO catalog = AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(path);
            if (catalog == null || catalog.RunCommonLoadManifest == null)
                continue;

            if (ReferencesRouteSet(catalog, targetRouteSet) && !manifests.Contains(catalog.RunCommonLoadManifest))
                manifests.Add(catalog.RunCommonLoadManifest);
        }

        return manifests;
    }

    private static bool ReferencesRouteSet(RunRouteCatalogSO catalog, CorridorBossRouteSetSO targetRouteSet)
    {
        if (catalog == null || targetRouteSet == null)
            return false;

        if (catalog.FinalRouteSet == targetRouteSet)
            return true;

        IReadOnlyList<CorridorBossRouteSetSO> normalRoutes = catalog.NormalRouteSets;
        for (int i = 0; i < normalRoutes.Count; i++)
        {
            if (normalRoutes[i] == targetRouteSet)
                return true;
        }

        return false;
    }

    private static void ApplyExclusions(
        Dictionary<string, UnityEngine.Object> assets,
        HashSet<string> excludedKeys)
    {
        if (assets == null || assets.Count == 0 || excludedKeys == null || excludedKeys.Count == 0)
            return;

        var keysToRemove = new List<string>();
        foreach (KeyValuePair<string, UnityEngine.Object> pair in assets)
        {
            if (excludedKeys.Contains(pair.Key))
                keysToRemove.Add(pair.Key);
        }

        for (int i = 0; i < keysToRemove.Count; i++)
            assets.Remove(keysToRemove[i]);
    }

    private static void BuildAssetPartitions(
        Dictionary<string, UnityEngine.Object> corridorAssets,
        Dictionary<string, UnityEngine.Object> bossAssets,
        List<UnityEngine.Object> sharedAssets,
        List<UnityEngine.Object> corridorOnlyAssets,
        List<UnityEngine.Object> bossOnlyAssets)
    {
        foreach (KeyValuePair<string, UnityEngine.Object> pair in corridorAssets)
        {
            if (bossAssets.ContainsKey(pair.Key))
                sharedAssets.Add(pair.Value);
            else
                corridorOnlyAssets.Add(pair.Value);
        }

        foreach (KeyValuePair<string, UnityEngine.Object> pair in bossAssets)
        {
            if (!corridorAssets.ContainsKey(pair.Key))
                bossOnlyAssets.Add(pair.Value);
        }
    }

    private static void MergeAssets(
        Dictionary<string, UnityEngine.Object> destination,
        Dictionary<string, UnityEngine.Object> source)
    {
        if (destination == null || source == null || source.Count == 0)
            return;

        foreach (KeyValuePair<string, UnityEngine.Object> pair in source)
        {
            if (!destination.ContainsKey(pair.Key))
                destination.Add(pair.Key, pair.Value);
        }
    }

    private static Dictionary<string, UnityEngine.Object> CollectSceneAssets(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        UnityEngine.Object[] dependencies = EditorUtility.CollectDependencies(roots);
        Dictionary<string, UnityEngine.Object> results = new();

        for (int i = 0; i < dependencies.Length; i++)
        {
            UnityEngine.Object asset = dependencies[i];
            if (!ShouldIncludeAsset(asset))
                continue;

            string key = GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString();
            if (!results.ContainsKey(key))
                results.Add(key, asset);
        }

        return results;
    }

    private static void ApplyBootSpecificExclusions(Dictionary<string, UnityEngine.Object> assets)
    {
        if (assets == null || assets.Count == 0)
            return;

        var keysToRemove = new List<string>();
        foreach (KeyValuePair<string, UnityEngine.Object> pair in assets)
        {
            if (ShouldExcludeBootAsset(pair.Value))
                keysToRemove.Add(pair.Key);
        }

        for (int i = 0; i < keysToRemove.Count; i++)
            assets.Remove(keysToRemove[i]);
    }

    private static void ApplyFirstRunIntroSpecificExclusions(Dictionary<string, UnityEngine.Object> assets)
    {
        if (assets == null || assets.Count == 0)
            return;

        var keysToRemove = new List<string>();
        foreach (KeyValuePair<string, UnityEngine.Object> pair in assets)
        {
            if (ShouldExcludeFirstRunIntroAsset(pair.Value))
                keysToRemove.Add(pair.Key);
        }

        for (int i = 0; i < keysToRemove.Count; i++)
            assets.Remove(keysToRemove[i]);
    }

    private static bool ShouldIncludeAsset(UnityEngine.Object asset)
    {
        if (asset == null)
            return false;

        if (!EditorUtility.IsPersistent(asset))
            return false;

        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            return false;

        if (IsIgnoredAssetPath(assetPath))
            return false;

        if (asset is MonoScript || asset is SceneAsset || asset is DefaultAsset)
            return false;

        if (!AssetDatabase.IsMainAsset(asset))
            return false;

        if (asset is GameObject)
            return PrefabUtility.IsPartOfPrefabAsset(asset);

        if (asset is Component)
            return false;

        if (IsDependencyOnlyAsset(asset))
            return false;

        string typeName = asset.GetType().Name;
        if (IsIgnoredAssetTypeName(typeName))
            return false;

        return IsSupportedManifestRoot(asset);
    }

    private static bool ShouldExcludeBootAsset(UnityEngine.Object asset)
    {
        if (asset == null)
            return false;

        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(assetPath))
            return false;

        string normalizedPath = assetPath.Replace('\\', '/');
        for (int i = 0; i < IgnoredBootPathFragments.Length; i++)
        {
            if (normalizedPath.IndexOf(IgnoredBootPathFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        string typeName = asset.GetType().Name;
        return string.Equals(typeName, "CorridorBossRouteSetSO", StringComparison.Ordinal) ||
               string.Equals(typeName, "RouteSetLoadManifestSO", StringComparison.Ordinal) ||
               string.Equals(typeName, "LoadManifestSO", StringComparison.Ordinal) ||
               string.Equals(typeName, "StageLootTable", StringComparison.Ordinal) ||
               string.Equals(typeName, "GraveLootTable", StringComparison.Ordinal) ||
               string.Equals(typeName, "BossSpeechData", StringComparison.Ordinal) ||
               string.Equals(typeName, "PlayerSpeechData", StringComparison.Ordinal);
    }

    private static bool ShouldExcludeFirstRunIntroAsset(UnityEngine.Object asset)
    {
        if (asset == null)
            return false;

        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(assetPath))
            return false;

        string normalizedPath = assetPath.Replace('\\', '/');
        for (int i = 0; i < FirstRunIntroExcludedPathFragments.Length; i++)
        {
            if (normalizedPath.IndexOf(FirstRunIntroExcludedPathFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        if (normalizedPath.IndexOf("/_Project/Prefabs/Monsters/", StringComparison.OrdinalIgnoreCase) >= 0 &&
            normalizedPath.IndexOf("TrainingDummy", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return true;
        }

        return false;
    }

    private static bool IsIgnoredAssetPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return true;

        string normalizedPath = assetPath.Replace('\\', '/');
        if (normalizedPath.StartsWith("Assets/Editor Default Resources/", StringComparison.OrdinalIgnoreCase))
            return true;

        for (int i = 0; i < IgnoredPathFragments.Length; i++)
        {
            if (normalizedPath.IndexOf(IgnoredPathFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static bool IsIgnoredAssetTypeName(string typeName)
    {
        return string.Equals(typeName, "CorridorBossRouteSetSO", StringComparison.Ordinal) ||
               string.Equals(typeName, "RunRouteCatalogSO", StringComparison.Ordinal) ||
               string.Equals(typeName, "RouteSetLoadManifestSO", StringComparison.Ordinal) ||
               string.Equals(typeName, "LoadManifestSO", StringComparison.Ordinal) ||
               string.Equals(typeName, "LightingDataAsset", StringComparison.Ordinal) ||
               string.Equals(typeName, "LightingSettings", StringComparison.Ordinal) ||
               string.Equals(typeName, "SpriteAtlas", StringComparison.Ordinal);
    }

    private static bool IsDependencyOnlyAsset(UnityEngine.Object asset)
    {
        return asset is Sprite ||
               asset is Texture ||
               asset is Material ||
               asset is Shader ||
               asset is ComputeShader ||
               asset is AnimationClip ||
               asset is RuntimeAnimatorController ||
               asset is UnityEngine.Tilemaps.TileBase;
    }

    private static bool IsSupportedManifestRoot(UnityEngine.Object asset)
    {
        return asset is GameObject ||
               asset is PresentationCueSO ||
               asset is ScriptableObject ||
               asset is AudioClip ||
               asset is TextAsset ||
               asset is Font;
    }

    private static AssetBuckets CategorizeAssets(List<UnityEngine.Object> assets)
    {
        var prefabs = new List<GameObject>();
        var cues = new List<PresentationCueSO>();
        var dataAssets = new List<ScriptableObject>();
        var extraAssets = new List<UnityEngine.Object>();

        for (int i = 0; i < assets.Count; i++)
        {
            UnityEngine.Object asset = assets[i];
            if (asset == null)
                continue;

            switch (asset)
            {
                case GameObject prefab:
                    prefabs.Add(prefab);
                    break;

                case PresentationCueSO cue:
                    cues.Add(cue);
                    break;

                case ScriptableObject scriptableObject:
                    dataAssets.Add(scriptableObject);
                    break;

                default:
                    extraAssets.Add(asset);
                    break;
            }
        }

        SortAssets(prefabs);
        SortAssets(cues);
        SortAssets(dataAssets);
        SortAssets(extraAssets);

        return new AssetBuckets(prefabs, cues, dataAssets, extraAssets);
    }

    private static void SortAssets<T>(List<T> assets) where T : UnityEngine.Object
    {
        assets.Sort((left, right) =>
        {
            string leftPath = AssetDatabase.GetAssetPath(left);
            string rightPath = AssetDatabase.GetAssetPath(right);
            int pathCompare = string.Compare(leftPath, rightPath, StringComparison.OrdinalIgnoreCase);
            if (pathCompare != 0)
                return pathCompare;

            return string.Compare(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static RouteSetLoadManifestSO EnsureRouteManifest(CorridorBossRouteSetSO targetRouteSet)
    {
        SerializedObject routeSetSerialized = new SerializedObject(targetRouteSet);
        SerializedProperty manifestProperty = routeSetSerialized.FindProperty("loadManifest");
        RouteSetLoadManifestSO manifest = manifestProperty.objectReferenceValue as RouteSetLoadManifestSO;

        if (manifest == null)
        {
            EnsureLoadingAssetDirectory();
            string manifestPath = AssetDatabase.GenerateUniqueAssetPath($"{LoadingAssetDirectory}/{targetRouteSet.name}_LoadManifest.asset");

            manifest = CreateInstance<RouteSetLoadManifestSO>();
            manifest.name = $"{targetRouteSet.name}_LoadManifest";
            AssetDatabase.CreateAsset(manifest, manifestPath);
            manifestProperty.objectReferenceValue = manifest;
            routeSetSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        return manifest;
    }

    private void EnsureBootManifest()
    {
        if (bootManifest != null)
            return;

        EnsureLoadingAssetDirectory();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{LoadingAssetDirectory}/BootLoadManifest.asset");
        bootManifest = CreateInstance<LoadManifestSO>();
        bootManifest.name = "BootLoadManifest";
        AssetDatabase.CreateAsset(bootManifest, assetPath);
        EditorUtility.SetDirty(bootManifest);
    }

    private void EnsureFirstRunIntroManifest()
    {
        if (firstRunIntroManifest != null)
            return;

        EnsureLoadingAssetDirectory();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{LoadingAssetDirectory}/FirstRunIntroTutorialLoadManifest.asset");
        firstRunIntroManifest = CreateInstance<LoadManifestSO>();
        firstRunIntroManifest.name = "FirstRunIntroTutorialLoadManifest";
        AssetDatabase.CreateAsset(firstRunIntroManifest, assetPath);
        EditorUtility.SetDirty(firstRunIntroManifest);
    }

    private void ResolveBootstrapManifestReferencesIfMissing()
    {
        if (bootManifest != null && firstRunIntroManifest != null)
            return;

        LoadingBootstrapConfigSO config = AssetDatabase.LoadAssetAtPath<LoadingBootstrapConfigSO>(BootstrapConfigSourceAssetPath);
        if (config == null)
            return;

        if (bootManifest == null)
            bootManifest = config.BootManifest;

        if (firstRunIntroManifest == null)
            firstRunIntroManifest = config.FirstRunIntroManifest;
    }

    private void EnsureBootstrapConfigAsset()
    {
        LoadingBootstrapConfigSO sourceConfig = EnsureBootstrapConfigAtPath(BootstrapConfigSourceAssetPath);
        ApplyBootstrapManifestReferences(sourceConfig);
        EnsureBootstrapConfigInPreloadedAssets(sourceConfig);
    }

    private static LoadingBootstrapConfigSO EnsureBootstrapConfigAtPath(string assetPath)
    {
        LoadingBootstrapConfigSO config = AssetDatabase.LoadAssetAtPath<LoadingBootstrapConfigSO>(assetPath);
        if (config != null)
            return config;

        config = CreateInstance<LoadingBootstrapConfigSO>();
        config.name = "LoadingBootstrapConfig";
        AssetDatabase.CreateAsset(config, assetPath);
        return config;
    }

    private void ApplyBootstrapManifestReferences(LoadingBootstrapConfigSO config)
    {
        if (config == null)
            return;

        SerializedObject configSerialized = new SerializedObject(config);
        SerializedProperty bootManifestProperty = configSerialized.FindProperty("bootManifest");
        if (bootManifestProperty != null && bootManifest != null)
            bootManifestProperty.objectReferenceValue = bootManifest;

        SerializedProperty firstRunIntroManifestProperty = configSerialized.FindProperty("firstRunIntroManifest");
        if (firstRunIntroManifestProperty != null && firstRunIntroManifest != null)
            firstRunIntroManifestProperty.objectReferenceValue = firstRunIntroManifest;

        configSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
    }

    private static void EnsureLoadingAssetDirectory()
    {
        if (AssetDatabase.IsValidFolder(LoadingAssetDirectory))
            return;

        EnsureAssetFolderPath(LoadingAssetDirectory);
    }

    // 책임: AssetDatabase 경로의 중간 폴더를 순서대로 보장한다.
    private static void EnsureAssetFolderPath(string assetFolderPath)
    {
        if (string.IsNullOrWhiteSpace(assetFolderPath) ||
            assetFolderPath == "Assets" ||
            AssetDatabase.IsValidFolder(assetFolderPath))
        {
            return;
        }

        string[] parts = assetFolderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private static void EnsureBootstrapConfigInPreloadedAssets(LoadingBootstrapConfigSO config)
    {
        if (config == null)
            return;

        UnityEngine.Object[] preloadedAssets = PlayerSettings.GetPreloadedAssets();
        for (int i = 0; i < preloadedAssets.Length; i++)
        {
            if (preloadedAssets[i] == config)
                return;
        }

        Array.Resize(ref preloadedAssets, preloadedAssets.Length + 1);
        preloadedAssets[^1] = config;
        PlayerSettings.SetPreloadedAssets(preloadedAssets);
    }

    private static LoadManifestSO EnsureChildManifest(
        RouteSetLoadManifestSO routeManifest,
        string propertyName,
        string childName)
    {
        SerializedObject routeManifestSerialized = new SerializedObject(routeManifest);
        SerializedProperty childProperty = routeManifestSerialized.FindProperty(propertyName);
        LoadManifestSO childManifest = childProperty.objectReferenceValue as LoadManifestSO;

        if (childManifest == null)
        {
            childManifest = CreateInstance<LoadManifestSO>();
            childManifest.name = childName;
            AssetDatabase.AddObjectToAsset(childManifest, routeManifest);
            childProperty.objectReferenceValue = childManifest;
            routeManifestSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        return childManifest;
    }

    private static void WriteManifest(LoadManifestSO manifest, AssetBuckets buckets, LoadScopeKind scopeKind)
    {
        SerializedObject manifestSerialized = new SerializedObject(manifest);
        manifestSerialized.FindProperty("scopeKind").enumValueIndex = (int)scopeKind;
        SetObjectList(manifestSerialized.FindProperty("prefabAssets"), buckets.Prefabs);
        SetObjectList(manifestSerialized.FindProperty("cueAssets"), buckets.Cues);
        SetObjectList(manifestSerialized.FindProperty("dataAssets"), buckets.DataAssets);
        SetObjectList(manifestSerialized.FindProperty("extraAssets"), buckets.ExtraAssets);
        manifestSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manifest);
    }

    private static void SetObjectList<T>(SerializedProperty property, List<T> assets) where T : UnityEngine.Object
    {
        property.arraySize = assets.Count;
        for (int i = 0; i < assets.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = assets[i];
    }

    private static List<string> ParseSceneNames(string sceneNames)
    {
        var results = new List<string>();
        if (string.IsNullOrWhiteSpace(sceneNames))
            return results;

        string[] tokens = sceneNames.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            string sceneName = tokens[i].Trim();
            if (!string.IsNullOrWhiteSpace(sceneName) && !ContainsSceneName(results, sceneName))
                results.Add(sceneName);
        }

        return results;
    }

    private static bool ContainsSceneName(List<string> sceneNames, string sceneName)
    {
        if (sceneNames == null || string.IsNullOrWhiteSpace(sceneName))
            return false;

        for (int i = 0; i < sceneNames.Count; i++)
        {
            if (string.Equals(sceneNames[i], sceneName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string FindScenePathByName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return null;

        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        for (int i = 0; i < buildScenes.Length; i++)
        {
            EditorBuildSettingsScene buildScene = buildScenes[i];
            if (buildScene == null || string.IsNullOrEmpty(buildScene.path))
                continue;

            string buildSceneName = Path.GetFileNameWithoutExtension(buildScene.path);
            if (string.Equals(buildSceneName, sceneName, StringComparison.OrdinalIgnoreCase))
                return buildScene.path;
        }

        string[] guids = AssetDatabase.FindAssets($"{sceneName} t:Scene");
        string matchedPath = null;

        for (int i = 0; i < guids.Length; i++)
        {
            string candidatePath = AssetDatabase.GUIDToAssetPath(guids[i]);
            string candidateName = Path.GetFileNameWithoutExtension(candidatePath);
            if (!string.Equals(candidateName, sceneName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (matchedPath == null)
            {
                matchedPath = candidatePath;
                continue;
            }

            throw new InvalidOperationException($"동일한 이름의 scene이 둘 이상 있습니다: {sceneName}");
        }

        return matchedPath;
    }

    private static void RestoreSceneSetup(SceneSetup[] originalSetup)
    {
        if (originalSetup == null || originalSetup.Length == 0)
            return;

        EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
    }
}
