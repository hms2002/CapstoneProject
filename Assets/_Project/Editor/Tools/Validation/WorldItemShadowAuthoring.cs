using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Bakes the approved 32-pixel preview placement without changing texture import settings.</summary>
public static class WorldItemShadowAuthoring
{
    private const string PrefabPath = "Assets/_Project/Prefabs/Items/Pickups/PF_WorldItemPickup2D.prefab";
    private const string ShadowPath = "Assets/_Project/Art/Sprites/Player/Pixel_Plains_Premium_Pack/Characters 24x24/Shadow.png";
    private const string Output = "output/world-item-shadows-2026-11-02";
    private static readonly List<string> Checks = new();
    private static readonly MethodInfo Refresh = typeof(WorldItemShadowPresenter2D).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

    [MenuItem("Tools/Items/Bake Approved Field Item Shadows")]
    public static void Apply()
    {
        Sprite shadow = AssetDatabase.LoadAllAssetsAtPath(ShadowPath).OfType<Sprite>().Single();
        List<ScriptableObject> definitions = Definitions();
        Sprite[] icons = definitions.Select(d => d.AsDef().Icon).Where(s => s != null).Distinct()
            .OrderBy(AssetDatabase.GetAssetPath).ThenBy(s => s.name, StringComparer.Ordinal).ToArray();
        var textures = new Dictionary<string, Texture2D>();
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            SpriteRenderer source = contents.transform.Find("Render").GetComponent<SpriteRenderer>();
            Transform existing = contents.transform.Find("Shadow");
            GameObject shadowObject = existing != null ? existing.gameObject : new GameObject("Shadow");
            shadowObject.transform.SetParent(contents.transform, false);
            SpriteRenderer renderer = shadowObject.GetComponent<SpriteRenderer>() ?? shadowObject.AddComponent<SpriteRenderer>();
            renderer.sprite = shadow;
            renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            renderer.color = new Color(1f, 1f, 1f, 0.34f);
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder - 1;
            renderer.enabled = false;
            shadowObject.transform.localPosition = new Vector3(0f, 0f, source.transform.localPosition.z);
            shadowObject.transform.localScale = new Vector3(0.75f / shadow.bounds.size.x, 0.25f / shadow.bounds.size.y, 1f);

            var presenter = contents.GetComponent<WorldItemShadowPresenter2D>() ?? contents.AddComponent<WorldItemShadowPresenter2D>();
            var data = new SerializedObject(presenter);
            data.FindProperty("sourceRenderer").objectReferenceValue = source;
            data.FindProperty("shadowRenderer").objectReferenceValue = renderer;
            SerializedProperty contacts = data.FindProperty("contacts");
            contacts.arraySize = icons.Length;
            bool[,] shadowMask = Raster(shadow, 24, 8, textures);
            for (int i = 0; i < icons.Length; i++)
            {
                Sprite icon = icons[i];
                int width = Mathf.Max(1, Mathf.RoundToInt(32f * icon.rect.width / Mathf.Max(icon.rect.width, icon.rect.height)));
                int height = Mathf.Max(1, Mathf.RoundToInt(32f * icon.rect.height / Mathf.Max(icon.rect.width, icon.rect.height)));
                int contact = ContactOffset(Raster(icon, width, height, textures), shadowMask);
                SerializedProperty entry = contacts.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("sprite").objectReferenceValue = icon;
                // Preview: shadow top = icon top + contact - 4; shadow half-height = 4.
                entry.FindPropertyRelative("centerFromTop").floatValue = (contact - 4f + 4f) / 32f;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            Debug.Log($"Baked approved shadows for {icons.Length} icons / {definitions.Count} relic and consumable definitions.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
            foreach (Texture2D texture in textures.Values) Object.DestroyImmediate(texture);
        }
    }

    private static List<ScriptableObject> Definitions()
    {
        string[] folders = { "Assets/_Project/Data/Items/Relics/Definitions", "Assets/_Project/Data/Items/Consumables" };
        return AssetDatabase.FindAssets("t:RelicDefinition", folders)
            .Concat(AssetDatabase.FindAssets("t:ConsumableDefinition", folders))
            .Distinct().Select(g => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(g))).ToList();
    }

    private static bool[,] Raster(Sprite sprite, int width, int height, Dictionary<string, Texture2D> textures)
    {
        string path = AssetDatabase.GetAssetPath(sprite);
        if (!textures.TryGetValue(path, out Texture2D texture))
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
                throw new InvalidOperationException("Cannot decode icon source: " + path);
            textures.Add(path, texture);
        }
        Rect rect = sprite.rect;
        var mask = new bool[width, height];
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            int sx = Mathf.Min((int)rect.width - 1, Mathf.FloorToInt((x + 0.5f) * rect.width / width));
            int sy = Mathf.Min((int)rect.height - 1, Mathf.FloorToInt((y + 0.5f) * rect.height / height));
            mask[x, y] = texture.GetPixel((int)rect.x + sx, (int)(rect.y + rect.height) - 1 - sy).a > 0f;
        }
        return mask;
    }

    private static int ContactOffset(bool[,] icon, bool[,] shadow)
    {
        int offset = int.MinValue;
        int left = Mathf.RoundToInt((icon.GetLength(0) - shadow.GetLength(0)) * 0.5f);
        for (int sx = 0; sx < shadow.GetLength(0); sx++)
        {
            int ix = left + sx;
            if (ix < 0 || ix >= icon.GetLength(0)) continue;
            int bottom = -1, top = -1;
            for (int y = icon.GetLength(1) - 1; y >= 0; y--) if (icon[ix, y]) { bottom = y; break; }
            for (int y = 0; y < shadow.GetLength(1); y++) if (shadow[sx, y]) { top = y; break; }
            if (bottom >= 0 && top >= 0) offset = Mathf.Max(offset, bottom - top + 1);
        }
        if (offset == int.MinValue) throw new InvalidOperationException("Icon and shadow have no shared visible contour.");
        return offset;
    }

    public static void ApplyAndValidateBatch()
    {
        try
        {
            Apply();
            Validate();
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    [MenuItem("Tools/Items/Validate Approved Field Item Shadows")]
    public static void Validate()
    {
        Checks.Clear(); Directory.CreateDirectory(Output);
        GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        try
        {
            var pickup = instance.GetComponent<WorldItemPickup2D>();
            var presenter = instance.GetComponent<ItemDisplayVisualPresenter2D>();
            var shadow = instance.GetComponent<WorldItemShadowPresenter2D>();
            var source = instance.transform.Find("Render").GetComponent<SpriteRenderer>();
            var renderer = instance.transform.Find("Shadow").GetComponent<SpriteRenderer>();
            Check(renderer.sprite.name == "Shadow_0" && Mathf.Approximately(renderer.color.a, 0.34f), "Approved sprite and alpha");
            Check(instance.GetComponentsInChildren<SpriteRenderer>(true).Length == 2, "Exactly one authored icon and shadow");
            foreach (ScriptableObject item in Definitions())
            {
                pickup.SetItem(item); Refresh.Invoke(shadow, null);
                bool hasIcon = item.AsDef().Icon != null;
                Check(renderer.enabled == hasIcon, item.name + ": shadow follows actual icon availability");
                if (!hasIcon) continue;
                Check(Mathf.Abs(renderer.bounds.size.x - Mathf.Max(source.bounds.size.x, source.bounds.size.y) * 0.75f) < 0.0001f,
                    item.name + ": normalized shadow width");
                Check(Mathf.Abs(renderer.bounds.size.y - Mathf.Max(source.bounds.size.x, source.bounds.size.y) * 0.25f) < 0.0001f,
                    item.name + ": normalized shadow height");
                Check(renderer.sortingLayerID == source.sortingLayerID && renderer.sortingOrder == source.sortingOrder - 1,
                    item.name + ": shadow behind icon");
                presenter.TryResolveVisualBoundsWorld(out Bounds bounds);
                Check(bounds == source.bounds, item.name + ": shadow excluded from drop spin/hover bounds");
            }
            var representative = AssetDatabase.LoadAssetAtPath<RelicDefinition>("Assets/_Project/Data/Items/Relics/Definitions/RD_MeltedBell.asset");
            pickup.SetItem(representative); Refresh.Invoke(shadow, null);
            Vector3 sourcePosition = source.transform.localPosition, sourceScale = source.transform.localScale;
            presenter.SetOutline(true);
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            Check(block.GetFloat(Shader.PropertyToID("_OutlineEnabled")) == 0f, "Highlight does not outline the shadow");
            shadow.OnDropTravelStarted(); Refresh.Invoke(shadow, null);
            Check(!renderer.enabled, "Shadow hidden during rotating drop travel");
            shadow.PlayDropLandingRoutine().MoveNext();
            Check(renderer.enabled, "Shadow restored by existing landing interface");
            Check(source.transform.localPosition == sourcePosition && source.transform.localScale == sourceScale,
                "Shadow never moves or resizes the actual icon");
            instance.SetActive(false);
            // Edit-mode callbacks are invoked explicitly; actual Play Mode dispatch remains a separate check.
            typeof(WorldItemShadowPresenter2D).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shadow, null);
            Check(!renderer.enabled, "Disable callback hides shadow");
            instance.SetActive(true); Refresh.Invoke(shadow, null);
            Check(renderer.enabled, "Re-enable refreshes placement");
            pickup.SetItem(null); Refresh.Invoke(shadow, null);
            Check(!renderer.enabled, "Null/cleared item hides shadow");
            Capture();
        }
        finally { Object.DestroyImmediate(instance); }
        File.WriteAllLines(Output + "/native-checks.txt", Checks);
        Debug.Log($"World item shadow native checks passed: {Checks.Count}.");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("World item shadow validation: " + name);
        Checks.Add("PASS " + name);
    }

    private static void Capture()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var objects = new List<GameObject>();
        RenderTexture target = null;
        Texture2D readback = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            var cameraObject = new GameObject("Shadow review camera"); objects.Add(cameraObject);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.cameraType = CameraType.Preview;
            camera.orthographic = true; camera.orthographicSize = 1.5f;
            camera.transform.position = new Vector3(0f, 0.5f, -20f);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.32f, 0.33f, 0.38f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            string[] names = { "TinglingShackles", "SpeedBoots", "CrimsonKing", "BurningFeather", "CurrentJar", "MeltedBell" };
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < 3; col++)
            {
                string path = AssetDatabase.FindAssets("t:RelicDefinition RD_" + names[row * 3 + col], new[] { "Assets/_Project/Data/Items" })
                    .Select(AssetDatabase.GUIDToAssetPath).First();
                GameObject item = Object.Instantiate(prefab); objects.Add(item);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(item, scene);
                item.transform.position = new Vector3((col - 1) * 1.7f, row == 0 ? 0.7f : -0.7f, 0f);
                item.GetComponent<WorldItemPickup2D>().SetItem(AssetDatabase.LoadAssetAtPath<RelicDefinition>(path));
                Refresh.Invoke(item.GetComponent<WorldItemShadowPresenter2D>(), null);
            }
            target = new RenderTexture(960, 480, 24); target.Create(); camera.targetTexture = target;
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            else camera.Render();
            RenderTexture.active = target;
            readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); readback.Apply();
            File.WriteAllBytes(Output + "/native-shadow-review.png", readback.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            if (readback != null) Object.DestroyImmediate(readback);
            foreach (GameObject obj in objects) if (obj != null) Object.DestroyImmediate(obj);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
