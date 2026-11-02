using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Static visual verification only; no game asset or runtime code is changed.
public static class NpcBubbleOpaqueDefaultValidation
{
    private static readonly List<Object> owned = new List<Object>();
    private static readonly List<string> checks = new List<string>();
    private static readonly List<SpriteRenderer> floor = new List<SpriteRenderer>();
    private static string output;
    private static Camera camera;
    private static Image background;
    private static TextMeshProUGUI text;
    private static Material bubbleMaterial;
    private static Material textMaterial;
    private static SpriteRenderer npc;
    private static RectInt probe;
    private const int Width = 800, Height = 600;

    [Serializable] private sealed class Theme
    {
        public string name;
        public float[] fill, border, font;
    }
    [Serializable] private sealed class Config
    {
        public Theme beforeDefault, afterDefault, beforeWitch, afterWitch;
        public Theme[] bosses;
    }
    private static Color C(float[] v) => new Color(v[0], v[1], v[2], v[3]);
    private static void Use(Theme t) => SetTheme(C(t.fill), C(t.border), C(t.font));
    public static void Run()
    {
        try
        {
            output = File.ReadAllText("Assets/Inputs/output.txt");
            var config = JsonUtility.FromJson<Config>(File.ReadAllText("Assets/Inputs/config.json"));
            Check(config.afterDefault.fill[3] == 1 && config.afterDefault.border[3] == 0,
                "Applied default fill is opaque and default automatic border is transparent");
            Check(config.afterWitch.fill[3] == 1 && config.afterWitch.border[3] == 1,
                "Applied witch fill is opaque and its colored automatic outline is retained");
            Setup();
            var stone = Tile(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Inputs/SlimeBossLevelSet.png"), new Rect(32, 0, 16, 16));
            var red = Tile(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Inputs/GrandHall_Tilemap.png"), new Rect(32, 32, 16, 16));
            SetFloor(stone);
            Use(config.beforeDefault); Capture("default_before");
            Use(config.afterDefault); var actualDefault = Capture("default_after");
            bubbleMaterial.SetFloat("_OutlineThickness", 0);
            SetTheme(C(config.afterDefault.fill), Color.black, C(config.afterDefault.font));
            var explicitDisabled = Capture(null);
            Check(Different(actualDefault, explicitDisabled) == 0,
                "Default border alpha=0 is pixel-identical to disabling generated outline thickness");
            bubbleMaterial.SetFloat("_OutlineThickness", .52f);
            Use(config.beforeWitch); Capture("witch_before");
            Use(config.afterWitch); var actualWitch = Capture("witch_after");
            foreach (var boss in config.bosses)
            {
                Check(boss.fill[3] == 1 && boss.border[3] == 1,
                    boss.name + " uses opaque fill and retains opaque themed border");
                Use(boss); var outlined = Capture("boss_" + boss.name);
                var transparentBorder = C(boss.border); transparentBorder.a = 0;
                SetTheme(C(boss.fill), transparentBorder, C(boss.font));
                var borderless = Capture(null);
                Check(Different(outlined, borderless) > 20, boss.name + " automatic outline remains visibly active");
            }
            SetFloor(red);
            Use(config.afterDefault); var defaultOnRed = Capture("default_after_red");
            Use(config.afterWitch); var witchOnRed = Capture("witch_after_red");
            Check(PatchDifference(actualDefault, defaultOnRed) == 0,
                "Opaque default body is independent of background floor pattern");
            Check(PatchDifference(actualWitch, witchOnRed) == 0,
                "Opaque witch body is independent of background floor pattern");
            checks.Add("Native NPC/TMP previews: 10 PNGs, exact current art/material; original font dependency resources included.");
            checks.Add("Scope: authored visual prefab and copied effective color data in isolated static rendering; full game runtime/Play Mode/Player build not executed.");
            File.WriteAllLines(output + "/native-validation.txt", checks);
            Debug.Log("NPC_OPAQUE_DEFAULT_BORDER_PASS");
            Cleanup(); EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            if (output != null) File.WriteAllText(output + "/native-failure.txt", e.ToString());
            Debug.LogException(e); Cleanup(); EditorApplication.Exit(1);
        }
    }
    private static int Different(Color32[] a, Color32[] b)
    {
        int n = 0;
        for (int i = 0; i < a.Length; i++) if (!a[i].Equals(b[i])) n++;
        return n;
    }

    private static void Setup()
    {
        GameObject npcObject = Own(new GameObject("Project merchant world sprite"));
        npc = npcObject.AddComponent<SpriteRenderer>();
        npc.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Inputs/Merchant 1.png");
        npc.sortingOrder = 10; npc.transform.position = new Vector3(0, -.5f, 0);
        Check(npc.sprite != null && npc.sprite.pixelsPerUnit == 28, "Actual merchant world sprite imports at authored 28 PPU");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Inputs/SpeechBubblePrefab.prefab");
        GameObject root = Own(Object.Instantiate(prefab));
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
        root.name = "Static speech prefab visual preview";
        root.transform.position = new Vector3(.3f, .7f, 0);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.overrideSorting = true;
        canvas.sortingLayerID = 0; canvas.sortingOrder = 1000;
        root.GetComponent<CanvasGroup>().alpha = 1;
        background = root.GetComponentsInChildren<Image>(true).First(i => i.name == "Background");
        text = root.GetComponentInChildren<TextMeshProUGUI>(true);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Inputs/Galmuri9 SDF.asset");
        Check(font != null && font.HasCharacters("골드가부족하군."), "Project pixel font covers the Korean glyphs in the authored merchant line");
        text.font = font; text.text = "골드가 부족하군."; text.fontSize = 20;
        text.enableAutoSizing = false; text.enableWordWrapping = false;
        text.maxVisibleCharacters = int.MaxValue;
        textMaterial = Own(new Material(font.material)); text.fontMaterial = textMaterial;
        Material source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Inputs/SpeechBubbleOutLine.mat");
        bubbleMaterial = Own(new Material(source)); background.material = bubbleMaterial; background.color = Color.white;
        bubbleMaterial.SetFloat("_TextureInfluence", 1);
        Check(background.type == Image.Type.Sliced && background.sprite.border == new Vector4(7, 6, 3, 3),
            "Authored Image uses current art and existing 9-slice borders");
        Check(Mathf.Abs(bubbleMaterial.GetFloat("_OutlineThickness") - .52f) < .0001f,
            "Existing automatic outline remains at 0.52");
        text.ForceMeshUpdate();
        for (int i = 0; i < 3; i++)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(background.rectTransform);
            Canvas.ForceUpdateCanvases();
        }
        text.ForceMeshUpdate(true, true);
        Check(text.textInfo.characterCount == "골드가 부족하군.".Length,
            "Full sample line generated by native TMP: " + text.textInfo.characterCount + " characters");

        GameObject cameraObject = Own(new GameObject("Diagnostic framing camera"));
        camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
        camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.aspect = (float)Width / Height;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.17f, .16f, .20f);
        canvas.worldCamera = camera;
        Vector3[] corners = new Vector3[4]; background.rectTransform.GetWorldCorners(corners);
        Bounds bounds = npc.bounds;
        foreach (Vector3 corner in corners) bounds.Encapsulate(corner);
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10);
        camera.orthographicSize = Mathf.Max(bounds.extents.y + .45f, (bounds.extents.x + .45f) / camera.aspect);
        Vector3 a = camera.WorldToViewportPoint(background.rectTransform.TransformPoint(
            new Vector3(background.rectTransform.rect.x + background.rectTransform.rect.width * .3f,
                background.rectTransform.rect.y + background.rectTransform.rect.height * .45f)));
        Vector3 b = camera.WorldToViewportPoint(background.rectTransform.TransformPoint(
            new Vector3(background.rectTransform.rect.x + background.rectTransform.rect.width * .75f,
                background.rectTransform.rect.y + background.rectTransform.rect.height * .8f)));
        probe = new RectInt(Mathf.CeilToInt(a.x * Width), Mathf.CeilToInt(a.y * Height),
            Mathf.FloorToInt((b.x - a.x) * Width), Mathf.FloorToInt((b.y - a.y) * Height));
        Check(probe.width > 20 && probe.height > 5, "Opaque-body pixel probe lies within the speech background");
    }

    private static Sprite Tile(Texture2D texture, Rect rect)
    {
        Check(texture != null, "Project floor texture imported");
        return Own(Sprite.Create(texture, rect, new Vector2(.5f, .5f), 16, 0, SpriteMeshType.FullRect));
    }
    private static void SetFloor(Sprite sprite)
    {
        if (floor.Count == 0)
            for (int y = -5; y <= 6; y++) for (int x = -6; x <= 8; x++)
            {
                GameObject tile = Own(new GameObject("Preview floor tile"));
                SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = -100; tile.transform.position = new Vector3(x, y, 2);
                floor.Add(renderer);
            }
        foreach (SpriteRenderer renderer in floor) renderer.sprite = sprite;
    }
    private static void SetTheme(Color fill, Color border, Color font)
    {
        bubbleMaterial.SetColor("_FillColor", fill); bubbleMaterial.SetColor("_OutlineColor", border);
        textMaterial.SetColor("_FaceColor", font); text.color = Color.white;
    }
    private static Color32[] Capture(string filename)
    {
        RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            Canvas.ForceUpdateCanvases(); camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); texture.Apply();
            if (filename != null) File.WriteAllBytes(output + "/native/" + filename + ".png", texture.EncodeToPNG());
            return texture.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = null;
            Object.DestroyImmediate(texture); Object.DestroyImmediate(target);
        }
    }
    private static int PatchDifference(Color32[] a, Color32[] b)
    {
        int count = 0;
        for (int y = probe.yMin; y < probe.yMax; y++) for (int x = probe.xMin; x < probe.xMax; x++)
            if (!a[y * Width + x].Equals(b[y * Width + x])) count++;
        return count;
    }
    private static T Own<T>(T item) where T : Object { owned.Add(item); return item; }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks.Add("PASS " + message);
    }
    private static void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear(); floor.Clear();
    }
}
