using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Scoped Editor-only migration of the existing shared monster HUD; invoked explicitly by the authoring owner.</summary>
public static class MonsterHealthBarMeshAuthoring
{
    public const string MeshPath = "Assets/_Project/Art/Meshes/MonsterHealthBarQuad.asset";
    public const string MaterialPath = "Assets/_Project/Art/Materials/MonsterHealthBarInstanced.mat";
    public const string ShaderName = "Project/Monster HP Instanced";

    [MenuItem("Tools/Authoring/Upgrade Monster HP To Instanced Mesh")]
    public static void ApplyToSharedHud()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null) throw new InvalidOperationException("Import the monster HP shader before authoring.");
        Mesh mesh = EnsureMesh();
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "MonsterHealthBarInstanced", enableInstancing = true };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        if (material.shader != shader) throw new InvalidOperationException("Unexpected HP material shader: " + MaterialPath);
        material.enableInstancing = true;
        var fonts = CaptureOriginalFonts();
        var originalMaterials = CaptureOriginalSharedMaterials();
        GameObject root = PrefabUtility.LoadPrefabContents(MonsterWorldHudAuthoring.HudPath);
        try
        {
            ApplyToLoadedHud(root, mesh, material);
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!fonts.TryGetValue(RelativePath(root.transform, text.transform), out TMP_FontAsset font)) continue;
                var serializedText = new SerializedObject(text);
                serializedText.FindProperty("m_fontAsset").objectReferenceValue = font;
                serializedText.ApplyModifiedPropertiesWithoutUndo();
            }
            // Font validation may assign its default material. Restore the serialized style field last,
            // without invoking TMP's public setters or another SerializedObject validation callback.
            FieldInfo sharedMaterial = typeof(TMP_Text).GetField("m_sharedMaterial", BindingFlags.Instance | BindingFlags.NonPublic);
            if (sharedMaterial == null) throw new InvalidOperationException("TMP's serialized shared material field is unavailable.");
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                if (originalMaterials.TryGetValue(RelativePath(root.transform, text.transform), out Material original))
                    sharedMaterial.SetValue(text, original);
            PrefabUtility.SaveAsPrefabAsset(root, MonsterWorldHudAuthoring.HudPath);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Dictionary<string, Material> CaptureOriginalSharedMaterials()
    {
        var result = new Dictionary<string, Material>();
        GameObject persistent = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterWorldHudAuthoring.HudPath);
        if (persistent == null) throw new InvalidOperationException("The existing shared HUD prefab is missing.");
        string baseline = null;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-uiOriginalHudBaseline") baseline = args[i + 1];
        bool originalAllNull = false;
        if (baseline != null)
        {
            if (!File.Exists(baseline)) throw new FileNotFoundException("The explicitly requested original HUD snapshot is missing.", baseline);
            MatchCollection materials = Regex.Matches(File.ReadAllText(baseline), @"(?m)^\s*m_sharedMaterial: \{([^}]+)\}");
            if (materials.Count == 0) throw new InvalidOperationException("The original HUD snapshot has no serialized TMP material fields.");
            originalAllNull = true;
            foreach (Match material in materials)
                if (material.Groups[1].Value.Trim() != "fileID: 0") originalAllNull = false;
            if (!originalAllNull) throw new InvalidOperationException("Snapshot material overrides require explicit per-text preservation; do not guess styles.");
        }
        foreach (TMP_Text text in persistent.GetComponentsInChildren<TMP_Text>(true))
        {
            var data = new SerializedObject(text);
            result.Add(RelativePath(persistent.transform, text.transform), originalAllNull ? null :
                data.FindProperty("m_sharedMaterial").objectReferenceValue as Material);
        }
        return result;
    }

    private static Dictionary<string, TMP_FontAsset> CaptureOriginalFonts()
    {
        var result = new Dictionary<string, TMP_FontAsset>();
        GameObject persistent = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterWorldHudAuthoring.HudPath);
        if (persistent == null) throw new InvalidOperationException("The existing shared HUD prefab is missing.");
        // The external immutable snapshot supplies the original serialized reference after a previous load drifted.
        string baseline = null;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == "-uiOriginalHudBaseline") baseline = args[i + 1];
        TMP_FontAsset baselineFont = null;
        if (baseline != null)
        {
            if (!File.Exists(baseline)) throw new FileNotFoundException("The explicitly requested original HUD snapshot is missing.", baseline);
            string guid = null;
            foreach (Match match in Regex.Matches(File.ReadAllText(baseline), @"m_fontAsset: \{fileID: \d+, guid: ([0-9a-f]+), type: \d+\}"))
            {
                string next = match.Groups[1].Value;
                if (guid != null && guid != next) throw new InvalidOperationException("Original HUD has multiple font references; preserve them explicitly rather than guessing.");
                guid = next;
            }
            if (guid != null)
            {
                baselineFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (baselineFont == null) throw new InvalidOperationException("The snapshot's original authored font asset is unavailable: " + guid);
            }
        }
        foreach (TMP_Text text in persistent.GetComponentsInChildren<TMP_Text>(true))
        {
            var data = new SerializedObject(text);
            result.Add(RelativePath(persistent.transform, text.transform), baselineFont != null ? baselineFont :
                data.FindProperty("m_fontAsset").objectReferenceValue as TMP_FontAsset);
        }
        return result;
    }

    private static string RelativePath(Transform root, Transform target)
    {
        string path = target.name;
        while (target.parent != null && target.parent != root) { target = target.parent; path = target.name + "/" + path; }
        return path;
    }

    // Also usable by an explicit scene-instance authoring pass; it does not discover or save scenes.
    public static void ApplyToLoadedHud(GameObject hud, Mesh mesh, Material material)
    {
        var view = hud.GetComponent<MonsterStackStatusWorldView>();
        var canvas = hud.GetComponent<Canvas>();
        Transform bar = hud.transform.Find("Visual/HealthBar");
        if (view == null || canvas == null || bar == null || mesh == null || material == null)
            throw new InvalidOperationException("Expected the existing authored monster HUD geometry and shared assets.");
        string[] paths = { "", "Frame", "Frame/Track", "Frame/Track/FillArea/DamageTrail", "Frame/Track/FillArea/Fill", "Frame/Track/FillArea/Fill/Highlight" };
        string[] colors = { "_BorderColor", "_FrameColor", "_TrackColor", "_TrailColor", "_FillColor", "_ShineColor" };
        var legacyGraphics = new Image[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            Transform node = paths[i].Length == 0 ? bar : bar.Find(paths[i]);
            Image image = node != null ? node.GetComponent<Image>() : null;
            if (image == null || image.sprite != null || image.type != Image.Type.Simple || image.material != image.defaultMaterial)
                throw new InvalidOperationException("HP migration supports the existing plain default-UI rectangles only: " + paths[i]);
            // UGUI stores Graphic vertex colors as Color32; keep that exact palette in the mesh material.
            material.SetColor(colors[i], (Color)(Color32)image.color);
            legacyGraphics[i] = image;
        }
        Transform geometry = bar.Find("HPGeometry");
        if (geometry == null)
        {
            geometry = new GameObject("HPGeometry", typeof(MeshFilter), typeof(MeshRenderer), typeof(MonsterHealthBarMeshView)).transform;
            geometry.SetParent(bar, false);
        }
        geometry.localPosition = Vector3.zero;
        geometry.gameObject.layer = bar.gameObject.layer;
        geometry.localRotation = Quaternion.identity;
        geometry.localScale = new Vector3(((RectTransform)bar).rect.width, ((RectTransform)bar).rect.height, 1f);
        var filter = geometry.GetComponent<MeshFilter>();
        var renderer = geometry.GetComponent<MeshRenderer>();
        var projection = geometry.GetComponent<MonsterHealthBarMeshView>();
        if (filter == null || renderer == null || projection == null)
            throw new InvalidOperationException("Unexpected existing HPGeometry components.");
        filter.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.sortingLayerID = canvas.sortingLayerID;
        renderer.sortingOrder = canvas.sortingOrder;
        renderer.enabled = false;
        var projectionData = new SerializedObject(projection);
        projectionData.FindProperty("targetRenderer").objectReferenceValue = renderer;
        projectionData.FindProperty("targetMeshFilter").objectReferenceValue = filter;
        SerializedProperty graphics = projectionData.FindProperty("legacyGraphics");
        graphics.arraySize = legacyGraphics.Length;
        for (int i = 0; i < legacyGraphics.Length; i++) graphics.GetArrayElementAtIndex(i).objectReferenceValue = legacyGraphics[i];
        projectionData.ApplyModifiedPropertiesWithoutUndo();
        var data = new SerializedObject(view);
        data.FindProperty("meshHealthBar").objectReferenceValue = projection;
        data.FindProperty("statusCanvas").objectReferenceValue = canvas;
        data.ApplyModifiedPropertiesWithoutUndo();
        foreach (string path in paths)
            (path.Length == 0 ? bar : bar.Find(path)).GetComponent<Image>().enabled = false;
    }

    private static Mesh EnsureMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh != null) return mesh;
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Meshes"))
            AssetDatabase.CreateFolder("Assets/_Project/Art", "Meshes");
        mesh = new Mesh { name = "MonsterHealthBarQuad" };
        mesh.vertices = new[] { new Vector3(-.5f, -.5f, 0f), new Vector3(-.5f, .5f, 0f), new Vector3(.5f, .5f, 0f), new Vector3(.5f, -.5f, 0f) };
        mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        mesh.triangles = new[] { 0, 1, 2, 2, 3, 0 };
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, MeshPath);
        return mesh;
    }
}
