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
    private const string LoadingAssetDirectory = "Assets/LeeJunMo/Datas/Loading";
    private const string BootstrapConfigSourceAssetPath = LoadingBootstrapConfigSO.SourceAssetPath;
    private static readonly string[] IgnoredPathFragments =
    {
        "/Editor/",
        "/Tests/",
        "/Test/",
        "/Gizmos/",
        "/Resources/Loading/"
    };

    private static readonly string[] IgnoredBootPathFragments =
    {
        "/Datas/Looting/GraveLootTable.asset",
        "/Datas/Looting/Table_Stage",
        "/Datas/Dialogue/SpeechData/",
        "/Datas/Dialogue/NPC/DialogueTheme/ShadowBoss",
        "/Audio/BGM/Boss",
        "/Audio/BGM/ShadowCorridor",
        "/Sprites/Characters/Boss/",
        "/Sprites/UI/Dialogue/Boss1"
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
    private string bootSeedSceneName = "ProtoTypeHub";
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
        saveAssetsAfterBuild = EditorGUILayout.ToggleLeft("Save Assets After Build", saveAssetsAfterBuild);
        verboseLogging = EditorGUILayout.ToggleLeft("Verbose Logging", verboseLogging);

        EditorGUILayout.Space(8f);
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

        string[] guids = AssetDatabase.FindAssets("t:CorridorBossRouteSetSO");
        if (guids == null || guids.Length == 0)
        {
            statusMessage = "CorridorBossRouteSetSO 자산을 찾지 못했습니다.";
            return;
        }

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        int builtCount = 0;

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                CorridorBossRouteSetSO target = AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(path);
                if (target == null)
                    continue;

                BuildRouteSetManifest(target);
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
            string scenePath = FindScenePathByName(bootSeedSceneName);
            if (string.IsNullOrEmpty(scenePath))
                throw new InvalidOperationException($"Boot seed scene '{bootSeedSceneName}' 경로를 찾지 못했습니다.");

            Dictionary<string, UnityEngine.Object> sceneAssets = CollectSceneAssets(scenePath);
            ApplyBootSpecificExclusions(sceneAssets);
            EnsureBootManifest();
            EnsureBootstrapConfigAsset();
            WriteManifest(bootManifest, CategorizeAssets(new List<UnityEngine.Object>(sceneAssets.Values)));

            if (saveAssetsAfterBuild)
                AssetDatabase.SaveAssets();

            statusMessage = $"Built boot manifest from {bootSeedSceneName}.";

            if (verboseLogging)
            {
                Debug.Log(
                    $"[RouteSetLoadManifestBuilder] Built boot manifest from {bootSeedSceneName}. assetCount={sceneAssets.Count}",
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

    private void BuildRouteSetManifest(CorridorBossRouteSetSO targetRouteSet)
    {
        if (targetRouteSet == null)
            throw new InvalidOperationException("RouteSet이 null입니다.");

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

        WriteManifest(sharedManifest, CategorizeAssets(sharedAssets));
        WriteManifest(corridorManifest, CategorizeAssets(corridorOnlyAssets));
        WriteManifest(bossManifest, CategorizeAssets(bossOnlyAssets));

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

        List<LoadManifestSO> runCommonManifests = FindRunCommonManifests(targetRouteSet);
        for (int i = 0; i < runCommonManifests.Count; i++)
            AddManifestAssetKeys(runCommonManifests[i], excludedKeys);

        return excludedKeys;
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

        if (asset is GameObject)
            return PrefabUtility.IsPartOfPrefabAsset(asset);

        if (asset is Component)
            return false;

        if (asset is Texture || asset is Shader || asset is ComputeShader)
            return false;

        string typeName = asset.GetType().Name;
        if (IsIgnoredAssetTypeName(typeName))
            return false;

        return true;
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
        return string.Equals(typeName, "LightingDataAsset", StringComparison.Ordinal) ||
               string.Equals(typeName, "LightingSettings", StringComparison.Ordinal) ||
               string.Equals(typeName, "SpriteAtlas", StringComparison.Ordinal);
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

    private void EnsureBootstrapConfigAsset()
    {
        LoadingBootstrapConfigSO sourceConfig = EnsureBootstrapConfigAtPath(BootstrapConfigSourceAssetPath);
        ApplyBootManifest(sourceConfig);
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

    private void ApplyBootManifest(LoadingBootstrapConfigSO config)
    {
        if (config == null)
            return;

        SerializedObject configSerialized = new SerializedObject(config);
        configSerialized.FindProperty("bootManifest").objectReferenceValue = bootManifest;
        configSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
    }

    private static void EnsureLoadingAssetDirectory()
    {
        if (AssetDatabase.IsValidFolder(LoadingAssetDirectory))
            return;

        const string datasDirectory = "Assets/LeeJunMo/Datas";
        if (!AssetDatabase.IsValidFolder(datasDirectory))
            AssetDatabase.CreateFolder("Assets/LeeJunMo", "Datas");

        AssetDatabase.CreateFolder(datasDirectory, "Loading");
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

    private static void WriteManifest(LoadManifestSO manifest, AssetBuckets buckets)
    {
        SerializedObject manifestSerialized = new SerializedObject(manifest);
        manifestSerialized.FindProperty("scopeKind").enumValueIndex = (int)LoadScopeKind.RouteSet;
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
