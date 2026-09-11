using System;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Responsibility: migrate approved scene/boss music authoring and validate it without runtime route inference.
public static class SceneBgmAuthoringUtility
{
    private const string SceneFolder = "Assets/_Project/Scenes/";
    private const string RouteFolder = "Assets/_Project/Data/SceneFlow/Routes/";

    // Explicit migration entry point for batch execution, not an automatic import callback.
    public static void MigrateProjectScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Scene BGM migration requires Edit Mode.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var configurations = BuildConfigurations();
        ConfigureBossPrefab("Assets/_Project/Prefabs/Bosses/DragonBoss/DragonBoss.prefab", LoadRoute("Dragon_CorridorBossRouteSet").BossCombatBgm);
        ConfigureBossPrefab("Assets/_Project/Prefabs/Bosses/ShadowBoss/Witch.prefab", LoadRoute("ShadowCorridorBossRouteSet").BossCombatBgm);
        ConfigureBossPrefab("Assets/_Project/Prefabs/Bosses/DemonKing/DemonKing.prefab", LoadRoute("DemonkingRouteSet").BossCombatBgm);

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (var configuration in configurations)
            {
                Scene scene = EditorSceneManager.OpenScene(configuration.path, OpenSceneMode.Single);
                List<SceneBgmRequester> requesters = FindInScene<SceneBgmRequester>(scene);
                if (requesters.Count > 1)
                    throw new InvalidOperationException($"Duplicate scene BGM requesters: {scene.path}");
                SceneBgmRequester requester;
                if (requesters.Count == 0)
                {
                    var root = new GameObject("SceneBgm");
                    SceneManager.MoveGameObjectToScene(root, scene);
                    requester = root.AddComponent<SceneBgmRequester>();
                }
                else
                {
                    requester = requesters[0];
                }

                WriteSound(requester, "sceneMusic", configuration.entry);
                if (configuration.combat.IsSet)
                    foreach (GameObject root in scene.GetRootGameObjects())
                        ConfigureBossObjects(root, configuration.combat);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            if (setup.Length > 0 && Array.TrueForAll(setup, item => !string.IsNullOrEmpty(item.path)) &&
                Array.Exists(setup, item => item.isActive && item.isLoaded))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        ValidateProjectScenes();
        Debug.Log($"[SceneBgmAuthoring] Configured {configurations.Count} scenes and 3 boss prefabs.");
    }

    [MenuItem("Tools/Audio/Validate Scene BGM")]
    public static void ValidateProjectScenes()
    {
        AudioCatalogSO catalog = AssetDatabase.LoadAssetAtPath<AudioCatalogSO>("Assets/_Project/Resources/Audio/DefaultAudioCatalog.asset");
        if (catalog == null)
            throw new InvalidOperationException("Default audio catalog is missing.");
        int count = 0;
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled)
                paths.Add(scene.path);
        paths.Add(SceneFolder + "ProceduralDemonkingCorridor.unity");

        foreach (string path in paths)
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                List<SceneBgmRequester> requesters = FindInScene<SceneBgmRequester>(scene);
                if (requesters.Count != 1 || !requesters[0].enabled || !requesters[0].gameObject.activeInHierarchy)
                    throw new InvalidOperationException($"Expected one enabled SceneBgmRequester: {path}");
                SceneBgmRequester requester = requesters[0];
                if (requester.transform.parent != null || requester.GetComponents<MonoBehaviour>().Length != 1)
                    throw new InvalidOperationException($"Keep SceneBgm on a separate scene-only root: {path}");
                ValidateSound(requester.SceneMusic, catalog, path);
                foreach (BossEncounterDirector director in FindInScene<BossEncounterDirector>(scene))
                    ValidateSound(ReadSound(director, "bossCombatBgm"), catalog, path);
                foreach (BossTalkManager director in FindInScene<BossTalkManager>(scene))
                    ValidateSound(ReadSound(director, "bossCombatBgm"), catalog, path);
                count++;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        Debug.Log($"[SceneBgmAuthoring] Validated {count} scenes: unique scene-only requester and valid music keys.");
    }

    private static List<(string path, SoundRef entry, SoundRef combat)> BuildConfigurations()
    {
        var result = new List<(string, SoundRef, SoundRef)>
        {
            (SceneFolder + "TitleScene.unity", SoundRef.FromKey("TitleSceneBGM"), default),
            (SceneFolder + "ProtoTypeHub.unity", SoundRef.FromKey("bgm.hub"), default),
            (SceneFolder + "Grand Hall.unity", SoundRef.FromKey("bgm.hub"), default),
            (SceneFolder + "TutorialCorridor.unity", default, default),
            (SceneFolder + "DarkLord_Tutorial.unity", default, default),
            (SceneFolder + "SangHyup_Hallway.unity", default, default)
        };
        AddRoute(result, "ShadowCorridorBossRouteSet", "ShadowCorridor");
        AddRoute(result, "Dragon_CorridorBossRouteSet", "DragonCorridor");
        AddRoute(result, "SlimeRouteSet", "SlimeCorridor");
        AddRoute(result, "DemonkingRouteSet", "ProceduralDemonkingCorridor");
        return result;
    }

    private static void AddRoute(List<(string path, SoundRef entry, SoundRef combat)> result, string asset, string alternateCorridor)
    {
        CorridorBossRouteSetSO route = LoadRoute(asset);
        result.Add((SceneFolder + route.CorridorSceneName + ".unity", route.CorridorBgm, default));
        result.Add((SceneFolder + alternateCorridor + ".unity", route.CorridorBgm, default));
        result.Add((SceneFolder + route.BossSceneName + ".unity", route.CorridorBgm, route.BossCombatBgm));
    }

    private static CorridorBossRouteSetSO LoadRoute(string asset)
    {
        var route = AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(RouteFolder + asset + ".asset");
        if (route == null) throw new InvalidOperationException($"Missing route migration source: {asset}");
        return route;
    }

    private static void ConfigureBossPrefab(string path, SoundRef music)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            ConfigureBossObjects(root, music);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureBossObjects(GameObject root, SoundRef music)
    {
        foreach (BossEncounterDirector director in root.GetComponentsInChildren<BossEncounterDirector>(true))
            WriteSound(director, "bossCombatBgm", music);
        foreach (BossTalkManager director in root.GetComponentsInChildren<BossTalkManager>(true))
            WriteSound(director, "bossCombatBgm", music);
    }

    private static List<T> FindInScene<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            result.AddRange(root.GetComponentsInChildren<T>(true));
        return result;
    }

    private static void WriteSound(UnityEngine.Object target, string property, SoundRef value)
    {
        using var serialized = new SerializedObject(target);
        SerializedProperty sound = serialized.FindProperty(property);
        sound.FindPropertyRelative("key").stringValue = value.key ?? string.Empty;
        sound.FindPropertyRelative("volumeMultiplier").floatValue = value.IsSet ? value.EffectiveVolumeMultiplier : 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    private static SoundRef ReadSound(UnityEngine.Object target, string property)
    {
        using var serialized = new SerializedObject(target);
        return SoundRef.FromKey(serialized.FindProperty(property).FindPropertyRelative("key").stringValue);
    }

    private static void ValidateSound(SoundRef sound, AudioCatalogSO catalog, string path)
    {
        if (!sound.IsSet) return;
        if (!catalog.TryGetEntry(sound.key, out AudioCatalogEntry entry) || entry.bus != AudioBus.BGM || !entry.HasPlayableClip)
            throw new InvalidOperationException($"Invalid BGM '{sound.key}' in {path}");
    }
}
