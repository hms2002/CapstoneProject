#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

/// <summary>Compares packed UI Images with the original imported textures, including their import compression.</summary>
public sealed class UiAtlasPixelEquivalencePlayModeTests
{
    const string CardAtlas = "Assets/_Project/Art/SpriteAtlases/UI_LevelRewardCards.spriteatlasv2";
    const string MenuAtlas = "Assets/_Project/Art/SpriteAtlases/UI_MenuChrome.spriteatlasv2";
    const string CardFolder = "Assets/_Project/Art/Sprites/UI/LevelRewardCards/";
    const string MenuFolder = "Assets/_Project/Art/Sprites/UI/";
    static readonly Type ImageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true);

    [TestCase("Base", false)]
    [TestCase("Description", false)]
    [TestCase("Title_1", false)]
    [TestCase("Outline_1", false)]
    [TestCase("Base", true)]
    [TestCase("Description", true)]
    [TestCase("Title_1", true)]
    [TestCase("Outline_1", true)]
    public void CardAtlas_ActualPackedImage_MatchesOriginalImportedPixels(string name, bool masked)
    {
        SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(CardAtlas);
        Assert.That(atlas, Is.Not.Null, "The approved four-card atlas must exist before this required regression runs.");
        Assert.That(atlas.spriteCount, Is.EqualTo(4), "Membership alone is insufficient: the atlas must contain four actually packed sprites.");
        CompareImportedAndPacked(atlas, CardFolder + name + ".png", "Simple", masked);
    }

    [TestCase("ButtonUI", "Simple", false)]
    [TestCase("ButtonUI", "Simple", true)]
    [TestCase("ButtonUI", "Sliced", false)]
    [TestCase("ButtonUI", "Sliced", true)]
    [TestCase("UIPanel", "Simple", false)]
    [TestCase("UIPanel", "Simple", true)]
    [TestCase("UIPanel", "Sliced", false)]
    [TestCase("UIPanel", "Sliced", true)]
    [TestCase("UITitle", "Simple", false)]
    [TestCase("UITitle", "Simple", true)]
    public void MenuAtlas_ActualPackedImage_MatchesOriginalImportedPixels(string name, string imageMode, bool masked)
    {
        SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(MenuAtlas);
        if (atlas == null)
            Assert.Ignore("The conditional UI_MenuChrome atlas has not been authored. This case provides no menu-atlas visual evidence until authoring is approved and performed.");
        Assert.That(atlas.spriteCount, Is.EqualTo(3), "An authored menu atlas must contain exactly ButtonUI, UIPanel and UITitle as packed sprites.");
        CompareImportedAndPacked(atlas, MenuFolder + name + ".png", imageMode, masked);
    }

    static void CompareImportedAndPacked(SpriteAtlas atlas, string path, string imageMode, bool masked)
    {
        Sprite[] sourceSprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        Assert.That(sourceSprites.Length, Is.EqualTo(1), "Expected one authored Sprite at " + path);
        Sprite source = sourceSprites[0];
        Assert.That(atlas.CanBindTo(source), Is.True, "The required source Sprite is not a member of this atlas: " + path);
        // false deliberately returns the original imported texture. Decoding the PNG
        // would erase source import compression/resizing and produce a false baseline.
        Texture2D originalTexture = SpriteUtility.GetSpriteTexture(source, false);
        Assert.That(originalTexture, Is.Not.Null, "The Unity Sprite source texture API must return an imported texture.");
        Assert.That(source.rect.width, Is.GreaterThan(0));
        Assert.That(source.rect.height, Is.GreaterThan(0));
        Assert.That(source.rect.xMin, Is.GreaterThanOrEqualTo(0));
        Assert.That(source.rect.yMin, Is.GreaterThanOrEqualTo(0));
        Assert.That(source.rect.xMax, Is.LessThanOrEqualTo(originalTexture.width));
        Assert.That(source.rect.yMax, Is.LessThanOrEqualTo(originalTexture.height));
        Sprite packed = atlas.GetSprite(source.name);
        Sprite reference = null;
        try
        {
            Assert.That(packed, Is.Not.Null, "Atlas.GetSprite must return the actual packed clone: " + source.name);
            Assert.That(packed.packed, Is.True, "This comparison must render an actual packed Sprite, not two source sprites.");
            Assert.That(packed.packingRotation, Is.EqualTo(SpritePackingRotation.None));
            Assert.That(packed.packingMode, Is.EqualTo(SpritePackingMode.Rectangle));
            Assert.That(packed.rect, Is.EqualTo(source.rect), "Atlas packing must preserve the authored Sprite rect.");
            Assert.That(packed.border, Is.EqualTo(source.border));
            Assert.That(packed.pivot, Is.EqualTo(source.pivot));
            Assert.That(packed.pixelsPerUnit, Is.EqualTo(source.pixelsPerUnit));
            Assert.That(packed.texture, Is.Not.SameAs(originalTexture), "Atlas and original imported texture must be distinct resources.");
            Assert.That(SpriteUtility.GetSpriteTexture(packed, true), Is.SameAs(packed.texture));
            if (imageMode == "Sliced") Assert.That(source.border, Is.Not.EqualTo(Vector4.zero), "The sliced canary must exercise authored nonzero borders.");

            Vector2 pivot = new(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height);
            reference = Sprite.Create(originalTexture, source.rect, pivot, source.pixelsPerUnit, 0,
                SpriteMeshType.FullRect, source.border, false);
            reference.name = source.name + "_OriginalImportedReference";
            Assert.That(reference.packed, Is.False);
            Assert.That(reference.texture, Is.SameAs(originalTexture));
            Assert.That(reference.rect, Is.EqualTo(source.rect));
            Assert.That(reference.border, Is.EqualTo(source.border));
            Assert.That(reference.pivot, Is.EqualTo(source.pivot));
            Assert.That(reference.pixelsPerUnit, Is.EqualTo(source.pixelsPerUnit));

            using var render = new ImageRenderFixture(imageMode, masked);
            foreach (Color tint in new[] { Color.white, new Color(0.8f, 0.7f, 0.6f, 0.75f) })
            {
                Color32[] original = render.Capture(reference, tint);
                Color32[] current = render.Capture(packed, tint);
                Assert.That((Texture)ImageType.GetProperty("mainTexture").GetValue(render.Image), Is.SameAs(packed.texture));
                ComparePixels(original, current, path, imageMode, masked, tint, originalTexture, packed.texture);
            }
        }
        finally
        {
            if (reference != null) Object.DestroyImmediate(reference);
            if (packed != null) Object.DestroyImmediate(packed);
        }
    }

    static void ComparePixels(Color32[] original, Color32[] current, string path, string mode, bool masked,
        Color tint, Texture2D sourceTexture, Texture2D atlasTexture)
    {
        Assert.That(current.Length, Is.EqualTo(original.Length));
        int painted = 0, different = 0, maximum = 0;
        for (int i = 0; i < original.Length; i++)
        {
            Color32 a = original[i], b = current[i];
            if (a.a > 8) painted++;
            int delta = Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b), Mathf.Abs(a.a - b.a));
            if (delta > 0) different++;
            maximum = Mathf.Max(maximum, delta);
        }
        var report = new PixelReport
        {
            sourcePath = path, imageMode = mode, masked = masked, tint = tint,
            sourceFormat = sourceTexture.format.ToString(), atlasFormat = atlasTexture.format.ToString(),
            sourceSize = new Vector2Int(sourceTexture.width, sourceTexture.height),
            atlasSize = new Vector2Int(atlasTexture.width, atlasTexture.height),
            visibleOriginalPixels = painted, differentRgbaPixels = different, maximumChannelDelta = maximum,
        };
        string context = JsonUtility.ToJson(report);
        TestContext.Out.WriteLine("UiAtlasImportedPixelEquivalence=" + context);
        Assert.That(painted, Is.GreaterThan(5), "Empty captures do not prove Sprite equivalence. " + context);
        Assert.That(different, Is.Zero, "Packed Image pixels must exactly match the original imported Sprite. " + context);
    }

    [Serializable]
    sealed class PixelReport
    {
        public string sourcePath, imageMode, sourceFormat, atlasFormat;
        public bool masked;
        public Color tint;
        public Vector2Int sourceSize, atlasSize;
        public int visibleOriginalPixels, differentRgbaPixels, maximumChannelDelta;
    }

    sealed class ImageRenderFixture : IDisposable
    {
        readonly GameObject root;
        readonly GameObject cameraRoot;
        readonly Camera camera;
        readonly RenderTexture target;
        readonly Texture2D readback;
        public readonly Component Image;

        public ImageRenderFixture(string mode, bool masked)
        {
            const int layer = 31;
            root = new GameObject("ImportedSpriteComparisonCanvas", typeof(RectTransform), typeof(Canvas));
            root.layer = layer;
            cameraRoot = new GameObject("ImportedSpriteComparisonCamera", typeof(Camera));
            camera = cameraRoot.GetComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 1f;
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << layer; camera.allowHDR = false; camera.allowMSAA = false;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            target.Create(); camera.targetTexture = target;
            readback = new Texture2D(256, 256, TextureFormat.RGBA32, false, true);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
            Transform parent = root.transform;
            if (masked)
            {
                Type maskType = Type.GetType("UnityEngine.UI.Mask, UnityEngine.UI", true);
                var mask = new GameObject("SpriteStencilCanary", typeof(RectTransform), ImageType, maskType);
                mask.layer = layer; mask.transform.SetParent(parent, false);
                // Clip the left/right while retaining the top/bottom of outline-only sprites.
                ((RectTransform)mask.transform).sizeDelta = new Vector2(124f, 192f);
                maskType.GetProperty("showMaskGraphic").SetValue(mask.GetComponent(maskType), false);
                parent = mask.transform;
            }
            var imageRoot = new GameObject("ActualUguiImage", typeof(RectTransform), ImageType);
            imageRoot.layer = layer; imageRoot.transform.SetParent(parent, false);
            var rect = (RectTransform)imageRoot.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = Vector3.zero; rect.sizeDelta = new Vector2(192f, 128f);
            Image = imageRoot.GetComponent(ImageType);
            ImageType.GetProperty("type").SetValue(Image, Enum.Parse(ImageType.GetProperty("type").PropertyType, mode));
            ImageType.GetProperty("preserveAspect").SetValue(Image, false);
            ImageType.GetProperty("useSpriteMesh").SetValue(Image, false);
            ImageType.GetProperty("raycastTarget").SetValue(Image, false);
        }

        public Color32[] Capture(Sprite sprite, Color tint)
        {
            ImageType.GetProperty("sprite").SetValue(Image, sprite);
            ImageType.GetProperty("color").SetValue(Image, tint);
            Canvas.ForceUpdateCanvases();
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True, "Native Camera requests must be supported for this pixel comparison.");
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else camera.Render();
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                readback.Apply();
                return readback.GetPixels32();
            }
            finally { RenderTexture.active = previous; }
        }

        public void Dispose()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(cameraRoot);
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(readback);
        }
    }
}
#endif
