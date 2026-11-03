using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Diagnostic fixture only. Production sources, assets and settings are read-only.
public static class SpeechBubbleVisualReview
{
    [Serializable] private sealed class Theme
    {
        public string name;
        public float[] border;
        public float[] fill;
        public float[] font;
    }
    [Serializable] private sealed class Config
    {
        public string output;
        public Theme[] themes;
    }
    private static readonly List<string> checks = new List<string>();
    private static readonly List<Object> owned = new List<Object>();
    private static Config config;
    private static Canvas canvas;
    private static Camera camera;
    private static Image image;
    private static Material source;
    private static string output;
    private static int captures;

    public static void Run()
    {
        try
        {
            config = JsonUtility.FromJson<Config>(File.ReadAllText("Assets/Inputs/config.json"));
            output = config.output;
            Directory.CreateDirectory(output + "/native");
            Sprite original = Import("Assets/Inputs/Original.png");
            Sprite candidate = Import("Assets/Inputs/Candidate.png");
            Check(original.rect.size == new Vector2(19, 16), "Original is 19x16");
            Check(candidate.rect.size == original.rect.size, "Candidate preserves canvas size");
            Check(candidate.border == new Vector4(7, 6, 3, 3), "Candidate uses existing 9-slice borders");
            Check(candidate.pixelsPerUnit == 32 && candidate.texture.filterMode == FilterMode.Point,
                "Point filter and 32 PPU preserved");
            source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Inputs/SpeechBubbleOutLine.mat");
            Check(source != null && source.shader != null && source.shader.isSupported,
                "Exact production material and shader are supported by native graphics device");
            Check(Mathf.Abs(source.GetFloat("_OutlineThickness") - .52f) < .0001f,
                "Production outline thickness is 0.52");
            Check(Mathf.Abs(source.GetFloat("_AlphaThreshold") - .341f) < .0001f,
                "Production alpha threshold is 0.341");
            SetupScene();

            Color[] backgrounds = { new Color32(37, 33, 46, 255), new Color32(225, 217, 204, 255) };
            string[] backgroundNames = { "dark", "light" };
            for (int b = 0; b < backgrounds.Length; b++)
            foreach (Theme theme in config.themes)
            for (int mode = 0; mode < 4; mode++)
                Capture("color_" + backgroundNames[b] + "_" + theme.name + "_" + mode,
                    mode == 0 ? original : candidate, theme, mode >= 2 ? 1 : 0,
                    mode == 3 ? 0 : .52f, new Vector2(300, 105), false, backgrounds[b], 360, 160);

            // Short, medium, long and right-tail layouts at the same pixels-per-unit.
            Vector2[] sizes = { new Vector2(150, 90), new Vector2(280, 110),
                new Vector2(440, 110), new Vector2(280, 110) };
            Theme witch = config.themes.First(t => t.name == "Witch");
            for (int b = 0; b < backgrounds.Length; b++)
            for (int mode = 0; mode < 2; mode++)
            for (int layout = 0; layout < sizes.Length; layout++)
                Capture("layout_" + backgroundNames[b] + "_" + mode + "_" + layout,
                    candidate, witch, mode == 0 ? 0 : 1, mode == 0 ? .52f : 0,
                    sizes[layout], layout == 3, backgrounds[b], 520, 180);

            // Diagnostic option: use the existing shape's alpha and the submitted interior shade.
            // This is constructed in memory; no replacement asset is written or applied to the game.
            Color32[] fillMask = original.texture.GetPixels32();
            Color32[] paintedPixels = candidate.texture.GetPixels32();
            Color32[] safePixels = new Color32[paintedPixels.Length];
            int removedBorder = 0, retainedShade = 0;
            for (int i = 0; i < safePixels.Length; i++)
            {
                Color32 p = paintedPixels[i];
                if (p.a > 0 && fillMask[i].a == 0) removedBorder++;
                if (fillMask[i].a > 0 && p.r < 250) retainedShade++;
                safePixels[i] = new Color32(p.r, p.g, p.b, fillMask[i].a);
            }
            Check(removedBorder == 53 && retainedShade == 15,
                "Submitted art adds 53 opaque border pixels outside original mask and shades 15 interior pixels");
            Sprite safeSprite = FromPixels(safePixels);
            foreach (Theme theme in config.themes)
            for (int b = 0; b < backgrounds.Length; b++)
                Capture("color_" + backgroundNames[b] + "_" + theme.name + "_4", safeSprite, theme,
                    1, .52f, new Vector2(300, 105), false, backgrounds[b], 360, 160);
            for (int b = 0; b < backgrounds.Length; b++)
            for (int layout = 0; layout < sizes.Length; layout++)
                Capture("layout_" + backgroundNames[b] + "_2_" + layout, safeSprite, witch,
                    1, .52f, sizes[layout], layout == 3, backgrounds[b], 520, 180);

            Color32[] alphaShade = (Color32[])safePixels.Clone();
            for (int i = 0; i < alphaShade.Length; i++)
                if (alphaShade[i].a > 0 && alphaShade[i].r < 250) alphaShade[i].a = 128;
            Sprite alphaShadeSprite = FromPixels(alphaShade);
            foreach (Theme theme in config.themes.Where(t => t.name == "Default" || t.name == "Witch"))
                Capture("alpha_warning_" + theme.name, alphaShadeSprite, theme,
                    1, .52f, new Vector2(300, 105), false, backgrounds[0], 360, 160);

            Texture2D neutral = Own(new Texture2D(19, 16, TextureFormat.RGBA32, false));
            neutral.filterMode = FilterMode.Point;
            neutral.wrapMode = TextureWrapMode.Clamp;
            Color32[] originalPixels = candidate.texture.GetPixels32();
            Color32[] neutralPixels = originalPixels.Select(p => new Color32(255, 255, 255, p.a)).ToArray();
            neutral.SetPixels32(neutralPixels); neutral.Apply();
            Sprite neutralSprite = Own(Sprite.Create(neutral, new Rect(0, 0, 19, 16),
                new Vector2(.05832906f, .06748692f), 32, 1, SpriteMeshType.FullRect, new Vector4(7, 6, 3, 3)));
            foreach (Theme theme in config.themes)
            {
                Color32[] a = Capture(null, candidate, theme, 0, .52f, new Vector2(300, 105), false, backgrounds[0], 360, 160);
                Color32[] n = Capture(null, neutralSprite, theme, 0, .52f, new Vector2(300, 105), false, backgrounds[0], 360, 160);
                Check(DifferentPixels(a, n) == 0, theme.name + ": candidate RGB removal makes no difference at influence=0");
                Color32[] rgb = Capture(null, candidate, theme, 1, .52f, new Vector2(300, 105), false, backgrounds[0], 360, 160);
                Check(DifferentPixels(a, rgb) > 100, theme.name + ": influence=1 exposes painted border and shade");
                Color32[] noOutline = Capture(null, candidate, theme, 1, 0, new Vector2(300, 105), false, backgrounds[0], 360, 160);
                Check(DifferentPixels(rgb, noOutline) > 20, theme.name + ": generated outline adds a visible extra contour");
            }
            Check(captures == 76, "76 native PNG captures completed");
            checks.Add("Graphics device: " + SystemInfo.graphicsDeviceType + " / " + SystemInfo.graphicsDeviceName);
            checks.Add("Color space: " + QualitySettings.activeColorSpace);
            checks.Add("Fixture: exact production shader/material; copied theme values; native UGUI Sliced; point/32PPU/border 7,6,3,3.");
            checks.Add("Scope: background rendering only. Full prefab text/layout lifecycle, runtime scene flow, atlas/URP parity, Player build and manual playtest are not covered.");
            File.WriteAllLines(output + "/native-validation.txt", checks);
            Debug.Log("SPEECH_BUBBLE_VISUAL_REVIEW_PASS " + checks.Count + " entries, 76 captures");
            Cleanup();
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            if (output != null) File.WriteAllText(output + "/native-failure.txt", error.ToString());
            Debug.LogException(error);
            Cleanup();
            EditorApplication.Exit(1);
        }
    }

    private static Sprite Import(string path)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32;
        importer.spriteBorder = new Vector4(7, 6, 3, 3);
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(.05832906f, .06748692f);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void SetupScene()
    {
        GameObject root = Own(new GameObject("Diagnostic canvas", typeof(RectTransform), typeof(Canvas)));
        canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.referencePixelsPerUnit = 100;
        GameObject cameraObject = Own(new GameObject("Diagnostic camera"));
        camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
        camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor;
        canvas.worldCamera = camera;
        GameObject graphic = new GameObject("Native sliced background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        graphic.transform.SetParent(canvas.transform, false);
        image = graphic.GetComponent<Image>(); image.type = Image.Type.Sliced;
        image.fillCenter = true; image.raycastTarget = false; image.color = Color.white;
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
        image.rectTransform.pivot = new Vector2(.5f, .5f);
    }

    private static Color32[] Capture(string name, Sprite sprite, Theme theme, float influence,
        float thickness, Vector2 size, bool flipped, Color background, int width, int height)
    {
        Material material = new Material(source);
        material.SetColor("_FillColor", ColorOf(theme.fill));
        material.SetColor("_OutlineColor", ColorOf(theme.border));
        material.SetFloat("_TextureInfluence", influence);
        material.SetFloat("_OutlineThickness", thickness);
        image.sprite = sprite; image.material = material;
        image.rectTransform.sizeDelta = size;
        image.rectTransform.localScale = new Vector3(flipped ? -1 : 1, 1, 1);
        image.rectTransform.anchoredPosition = Vector2.zero;
        canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
        camera.orthographicSize = height / 2f; camera.backgroundColor = background;
        RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            Canvas.ForceUpdateCanvases();
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            Color32[] pixels = texture.GetPixels32();
            Check(pixels.Count(p => Math.Abs(p.r - pixels[0].r) + Math.Abs(p.g - pixels[0].g) + Math.Abs(p.b - pixels[0].b) > 10) > 400,
                "Native render contains visible bubble: " + (name ?? "comparison"));
            if (name != null)
            {
                File.WriteAllBytes(output + "/native/" + name + ".png", texture.EncodeToPNG());
                captures++;
            }
            return pixels;
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = null; image.material = null;
            Object.DestroyImmediate(texture); Object.DestroyImmediate(target); Object.DestroyImmediate(material);
        }
    }

    private static Color ColorOf(float[] value) => new Color(value[0], value[1], value[2], value[3]);
    private static Sprite FromPixels(Color32[] pixels)
    {
        Texture2D texture = Own(new Texture2D(19, 16, TextureFormat.RGBA32, false));
        texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels); texture.Apply();
        return Own(Sprite.Create(texture, new Rect(0, 0, 19, 16),
            new Vector2(.05832906f, .06748692f), 32, 1, SpriteMeshType.FullRect, new Vector4(7, 6, 3, 3)));
    }
    private static int DifferentPixels(Color32[] a, Color32[] b)
    {
        int count = 0;
        for (int i = 0; i < a.Length; i++) if (!a[i].Equals(b[i])) count++;
        return count;
    }
    private static T Own<T>(T item) where T : Object { owned.Add(item); return item; }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        checks.Add("PASS " + label);
    }
    private static void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }
}
