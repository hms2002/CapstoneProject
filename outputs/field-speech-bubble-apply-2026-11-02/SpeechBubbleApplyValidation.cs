using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Diagnostic fixture only. Production sources, assets and settings are read-only.
public static class SpeechBubbleApplyValidation
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
            string prefab = File.ReadAllText("ValidationInput/SpeechBubblePrefab.prefab");
            var match = System.Text.RegularExpressions.Regex.Match(prefab, @"(?m)^  backgroundTextureInfluence: (\d+)\r?$");
            Check(match.Success && match.Groups[1].Value == "1", "Applied prefab reads texture RGB at influence=1");
            Check(prefab.Contains("m_Sprite: {fileID: 21300000, guid: 6896844baeba3b347b2fbd5bf032e0cc"), "Existing sprite GUID reference preserved");
            Check(prefab.Contains("m_Material: {fileID: 2100000, guid: 96d23f4b8cfcd894c895a2cebb4402fa"), "Existing material reference preserved");
            string path = "Assets/Inputs/SpeechBubble.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Check(sprite != null && sprite.rect.size == new Vector2(19, 16), "Applied PNG imports as 19x16 Sprite");
            Check(importer.spriteImportMode == SpriteImportMode.Single, "Original Single import mode preserved");
            Check(sprite.border == new Vector4(7, 6, 3, 3), "Original 9-slice border preserved");
            Check(sprite.pixelsPerUnit == 32 && sprite.texture.filterMode == FilterMode.Point, "Original 32 PPU and Point filter preserved");
            Check((sprite.pivot / sprite.rect.size - new Vector2(.05832906f, .06748692f)).sqrMagnitude < .0000001f, "Original pivot preserved");
            Check(!importer.isReadable && !importer.mipmapEnabled && importer.alphaIsTransparency, "Exact production import settings retained without rewriting metadata");
            Check(AssetDatabase.AssetPathToGUID(path) == "6896844baeba3b347b2fbd5bf032e0cc", "Exact existing image GUID imported");
            source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Inputs/SpeechBubbleOutLine.mat");
            Check(source != null && source.shader.isSupported, "Unchanged production material/shader supported");
            Check(Mathf.Abs(source.GetFloat("_OutlineThickness") - .52f) < .0001f, "Automatic outline remains enabled at 0.52");
            SetupScene();
            Color[] backgrounds = { new Color32(37, 33, 46, 255), new Color32(225, 217, 204, 255) };
            string[] names = { "dark", "light" };
            float influence = float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            for (int b = 0; b < backgrounds.Length; b++)
            foreach (Theme theme in config.themes)
                Capture("color_" + names[b] + "_" + theme.name + "_2", sprite, theme,
                    influence, source.GetFloat("_OutlineThickness"), new Vector2(300, 105), false, backgrounds[b], 360, 160);
            Check(captures == 10, "10 applied-art native renders completed");
            checks.Add("Scope: exact applied PNG/meta and prefab references; unchanged production shader; native UGUI background only.");
            checks.Add("Whole game compile, SpeechBubble/TMP/layout runtime, manual Play Mode and Player build not executed.");
            File.WriteAllLines(output + "/native-validation.txt", checks);
            Debug.Log("SPEECH_BUBBLE_APPLY_PASS " + captures + " native captures");
            Cleanup(); EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            if (output != null) File.WriteAllText(output + "/native-failure.txt", error.ToString());
            Debug.LogException(error); Cleanup(); EditorApplication.Exit(1);
        }
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
