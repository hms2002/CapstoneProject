using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Native asset authoring entry point. Stage in the project's Editor assembly to run;
// this retained source is not a runtime component or a test fixture.
public static class BossPortalPaletteImport
{
    private const string Samples = "outputs/boss-portal-color-samples-2026-11-03";
    private const string Output = "outputs/boss-portal-integration-2026-11-03";
    private const string Art = "Assets/_Project/Art/Sprites/Map/Environment/Level/BossPortals";
    private const string BasePortal = "Assets/_Project/Prefabs/Map/Procedural/ProceduralSceneTravelPortal.prefab";
    private const string SceneFolder = "Assets/_Project/Scenes/";
    private static readonly List<string> Applied = new();

    private sealed class Theme
    {
        public string Key, Sample, ParticleMin, ParticleMax, HallObject, BossScene;
        public Sprite Sprite;
        public GameObject Prefab;
        public string PrefabPath => "Assets/_Project/Prefabs/Map/Procedural/ProceduralSceneTravelPortal_" + Key + ".prefab";
    }

    public static void Apply()
    {
        try
        {
            var themes = new[]
            {
                new Theme { Key="Shadow", Sample="01-chloe", ParticleMin="998CB6", ParticleMax="DAD0E8",
                    HallObject="Portal_To_ShadowCorridor", BossScene="HeoMinSeok_Boss_Shadow" },
                new Theme { Key="Dragon", Sample="02-spiri", ParticleMin="FFAF4D", ParticleMax="FFE6A8",
                    HallObject="Portal_To_DragonCorridor", BossScene="HeoMinSeok_Boss_Dragon" },
                new Theme { Key="Slime", Sample="03-melta", ParticleMin="43DBB1", ParticleMax="B9FFE8",
                    HallObject="Portal_To_SlimeCorridor", BossScene="SangHyup_Boss_SlimeQueen" },
                new Theme { Key="DemonKing", Sample="04-demon-king", ParticleMin="B66AFF", ParticleMax="EBC2FF",
                    HallObject="Portal_To_DemonkingCorridor" }
            };
            EnsureFolder(Art);
            Directory.CreateDirectory(Art + "/Source~");
            foreach (Theme theme in themes)
                ImportSprite(theme);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(BasePortal);
            if (source == null) throw new InvalidOperationException("Missing base travel prefab.");
            foreach (Theme theme in themes.Where(t => t.BossScene != null))
            {
                GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(source);
                try
                {
                    root.name = "ProceduralSceneTravelPortal_" + theme.Key;
                    ApplyVisual(root, theme, "variant:" + theme.Key);
                    theme.Prefab = PrefabUtility.SaveAsPrefabAsset(root, theme.PrefabPath);
                    if (theme.Prefab == null) throw new InvalidOperationException("Prefab save failed.");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
                string roomPath = "Assets/_Project/Data/Dungeon/Rooms/BossThemes/" + theme.Key + "/" + theme.Key + "_Boss.asset";
                var room = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(roomPath);
                if (room == null) throw new InvalidOperationException("Missing room: " + roomPath);
                var data = new SerializedObject(room);
                var slots = data.FindProperty("buildData.travelEndpointPlacements");
                int matched = 0;
                for (int i = 0; i < slots.arraySize; i++)
                {
                    var slot = slots.GetArrayElementAtIndex(i);
                    if (slot.FindPropertyRelative("slotId").stringValue != "BossGate") continue;
                    slot.FindPropertyRelative("mediumPrefab").objectReferenceValue = theme.Prefab;
                    matched++;
                }
                if (matched != 1) throw new InvalidOperationException("Expected one BossGate in " + roomPath);
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(room);
                Applied.Add("Room binding: " + roomPath + " => " + theme.PrefabPath);
            }

            Scene hall = EditorSceneManager.OpenScene(SceneFolder + "Grand Hall.unity", OpenSceneMode.Single);
            foreach (Theme theme in themes)
                ApplyVisual(FindNamed(hall, theme.HallObject), theme, "Grand Hall/" + theme.HallObject);
            EditorSceneManager.MarkSceneDirty(hall);
            if (!EditorSceneManager.SaveScene(hall)) throw new InvalidOperationException("Grand Hall save failed.");

            foreach (Theme theme in themes.Where(t => t.BossScene != null))
            {
                Scene scene = EditorSceneManager.OpenScene(SceneFolder + theme.BossScene + ".unity", OpenSceneMode.Single);
                ApplyVisual(FindNamed(scene, "ScenePortal"), theme, theme.BossScene + "/ScenePortal");
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Boss scene save failed.");
            }
            Scene demon = EditorSceneManager.OpenScene(SceneFolder + "DemonkingCorridor.unity", OpenSceneMode.Single);
            ApplyVisual(FindNamed(demon, "ScenePortal"), themes[3], "DemonkingCorridor/ScenePortal");
            EditorSceneManager.MarkSceneDirty(demon);
            if (!EditorSceneManager.SaveScene(demon)) throw new InvalidOperationException("Demon corridor save failed.");
            AssetDatabase.SaveAssets();
            File.WriteAllLines(Output + "/native-authoring.txt", Applied);
            Debug.Log("Boss portal palettes applied: 4 sprites, 3 prefab variants, 3 room bindings, 8 scene portals.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllText(Output + "/native-authoring-error.txt", exception.ToString());
            EditorApplication.Exit(1);
        }
    }

    private static void ImportSprite(Theme theme)
    {
        string path = Art + "/Portal_" + theme.Key + ".png";
        File.Copy(Samples + "/" + theme.Sample + "-portal.png", path, true);
        File.Copy(Samples + "/" + theme.Sample + "-portal.aseprite", Art + "/Source~/Portal_" + theme.Key + ".aseprite", true);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var original = (TextureImporter)AssetImporter.GetAtPath("Assets/_Project/Art/Sprites/Map/Environment/Level/Portal.png");
        var settings = new TextureImporterSettings();
        original.ReadTextureSettings(settings);
        importer.SetTextureSettings(settings);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = original.spritePixelsPerUnit;
        importer.spritePivot = original.spritePivot;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
        theme.Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (theme.Sprite == null) throw new InvalidOperationException("Sprite import failed: " + path);
        Applied.Add("Sprite: " + path + "; rect=" + theme.Sprite.rect + "; pivot=" + theme.Sprite.pivot + "; PPU=" + theme.Sprite.pixelsPerUnit);
    }

    private static void ApplyVisual(GameObject root, Theme theme, string label)
    {
        var renderer = root.GetComponent<SpriteRenderer>();
        var particles = root.GetComponentsInChildren<ParticleSystem>(true).Where(p => p.name == "PortalParticle").ToArray();
        if (renderer == null || particles.Length != 1)
            throw new InvalidOperationException("Expected root sprite and one PortalParticle: " + label);
        renderer.sprite = theme.Sprite;
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        EditorUtility.SetDirty(renderer);
        ParticleSystem.MainModule main = particles[0].main;
        main.startColor = new ParticleSystem.MinMaxGradient(ParseColor(theme.ParticleMin), ParseColor(theme.ParticleMax));
        PrefabUtility.RecordPrefabInstancePropertyModifications(particles[0]);
        EditorUtility.SetDirty(particles[0]);
        Applied.Add(label + ": sprite=" + AssetDatabase.GetAssetPath(renderer.sprite) +
            "; particle mode=" + main.startColor.mode +
            "; min=" + ColorUtility.ToHtmlStringRGBA(main.startColor.colorMin) +
            "; max=" + ColorUtility.ToHtmlStringRGBA(main.startColor.colorMax));
    }

    private static Color ParseColor(string hex)
    {
        if (!ColorUtility.TryParseHtmlString("#" + hex, out Color value))
            throw new ArgumentException("Invalid color: " + hex);
        return value;
    }

    private static GameObject FindNamed(Scene scene, string name)
    {
        var matches = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name == name).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("Expected one " + name + " in " + scene.path + ", found " + matches.Length);
        return matches[0].gameObject;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
