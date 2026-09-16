#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class ShadowLightVisionPlayModeTests
{
    private readonly List<Object> objects = new();
    private long lastRgbEnergy;
    private static readonly Type LightType = Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.2D.Runtime", true);

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            if (objects[i] != null) Object.DestroyImmediate(objects[i]);
        objects.Clear();
    }

    [Test]
    public void RevealUsesStableRadius_NotGlobalBrightnessOrPulseRadius()
    {
        var field = CreateField(out var global, out _);
        var source = CreateSource(out var light);
        light.transform.position = new Vector3(4f, 0f);
        SetLight(light, "pointLightOuterRadius", 0.1f);
        field.SetFogWeight(1f);
        Assert.AreEqual(0f, GetLight<float>(global, "intensity"));
        Assert.IsTrue(source.TryGetArea(out var area));
        Assert.AreEqual(3f, area.z);
        Assert.IsTrue(SceneLightVisionPresentation.Contains(area, new Vector3(7f, 0f)));
        Assert.IsFalse(SceneLightVisionPresentation.Contains(area, new Vector3(7.01f, 0f)));
        SetLight(light, "intensity", 0f);
        Assert.IsFalse(source.TryGetArea(out _));
    }

    [Test]
    public void MultipleFogOwners_RestoreOnlyAfterLastRelease_AndOnDisable()
    {
        CreateField(out var light, out var controller);
        var first = NewObject("First requester");
        var second = NewObject("Second requester");
        controller.AcquireDarkness(first);
        controller.AcquireDarkness(second);
        controller.AcquireDarkness(first);
        Assert.AreEqual(0f, GetLight<float>(light, "intensity"));
        controller.ReleaseDarkness(first);
        Assert.AreEqual(0f, GetLight<float>(light, "intensity"));
        controller.ReleaseDarkness(second);
        Assert.AreEqual(0.2f, GetLight<float>(light, "intensity"), 0.0001f);
        controller.AcquireDarkness(first);
        controller.enabled = false;
        Assert.AreEqual(0.2f, GetLight<float>(light, "intensity"), 0.0001f);
    }

    [UnityTest]
    public IEnumerator FogDurationCanExtend_ThenExpire_AndDisableReleasesImmediately()
    {
        CreateField(out var light, out var controller);
        var player = NewObject("Fog recipient");
        var fog = player.AddComponent<RestrictedVisionVisualController>();
        Set(fog, "logStatusUiFlow", false);
        Set(fog, "visionMaskController", controller);
        fog.ApplyFog(0.1f);
        fog.ApplyFog(0.35f);
        yield return new WaitForSeconds(0.15f);
        Assert.AreEqual(0f, GetLight<float>(light, "intensity"));
        yield return new WaitForSeconds(0.3f);
        Assert.AreEqual(0.2f, GetLight<float>(light, "intensity"), 0.0001f);
        fog.ApplyFog(2f);
        fog.enabled = false;
        Assert.AreEqual(0.2f, GetLight<float>(light, "intensity"), 0.0001f);
    }

    [UnityTest]
    public IEnumerator FogFadeUsesUnscaledTime_AndReturnsToBaseline()
    {
        CreateField(out var light, out var controller);
        Set(controller, "enterFogFadeDuration", 0.3f);
        Set(controller, "exitFogFadeDuration", 0.2f);
        float oldTimeScale = Time.timeScale;
        try
        {
            Time.timeScale = 0f;
            controller.AcquireDarkness(controller);
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(GetLight<float>(light, "intensity"), Is.InRange(0.001f, 0.199f));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(0f, GetLight<float>(light, "intensity"), 0.0001f);
            controller.ReleaseDarkness(controller);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(0.2f, GetLight<float>(light, "intensity"), 0.0001f);
        }
        finally { Time.timeScale = oldTimeScale; }
    }

    [Test]
    public void PlayerRebindAndDetach_MoveTheAuthoredLightAndRemoveItsArea()
    {
        var field = CreateField(out _, out var controller);
        var source = CreateSource(out _);
        Set(field, "playerLight", source);
        var first = NewObject("First player");
        var second = NewObject("Second player");
        second.transform.position = new Vector3(12f, 4f);
        controller.AttachToPlayer(first.transform);
        controller.AttachToPlayer(second.transform);
        controller.DetachFromPlayer(first.transform);
        Assert.IsTrue(source.gameObject.activeSelf);
        Assert.AreEqual(new Vector3(12f, 4.5f), source.transform.position);
        controller.DetachFromPlayer(second.transform);
        Assert.IsFalse(source.gameObject.activeSelf);
        var areas = new Vector4[32];
        Assert.AreEqual(0, field.CopyAreas(new Bounds(Vector3.zero, Vector3.one * 100), areas));
    }

    [Test]
    public void GaugeFailsClosed_UsesSameAreasAsSprite_AndUnionsLights()
    {
        CreateField(out _, out _);
        var monster = NewObject("Shadow monster");
        monster.SetActive(false);
        var renderer = monster.AddComponent<SpriteRenderer>();
        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        var original = renderer.sharedMaterial;
        var filter = monster.AddComponent<ShadowMonsterGaugeVisibilityFilter>();
        var adapter = monster.AddComponent<ShadowMonsterLightVisibility>();
        Set(adapter, "targetRenderers", new[] { renderer });
        Set(adapter, "gaugeFilter", filter);
        Set(adapter, "revealMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_ShadowMonsterRevealLit.mat"));
        monster.SetActive(true);
        Assert.IsFalse(filter.ShouldShowGauge(), "No source must not reveal the gauge.");
        var first = CreateSource(out _);
        var second = CreateSource(out _);
        Assert.IsTrue(filter.ShouldShowGauge());
        first.gameObject.SetActive(false);
        Assert.IsTrue(filter.ShouldShowGauge(), "Another light still reveals this target.");
        second.gameObject.SetActive(false);
        Assert.IsFalse(filter.ShouldShowGauge());
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        Assert.AreEqual(0, block.GetInt("_RevealCount"));
        adapter.enabled = false;
        Assert.AreEqual(SpriteMaskInteraction.VisibleInsideMask, renderer.maskInteraction);
        Assert.AreSame(original, renderer.sharedMaterial);
    }

    [Test]
    public void WithoutOptInScene_SourceDoesNotEnableLightOrSuppressMask()
    {
        var source = CreateSource(out var light, false);
        var mask = source.gameObject.AddComponent<SpriteMask>();
        Set(source, "legacyMasks", new[] { mask });
        source.gameObject.SetActive(true);
        Assert.IsFalse(((Behaviour)light).enabled);
        Assert.IsTrue(mask.enabled);
        Assert.IsFalse(source.TryGetArea(out _));
    }

    [Test]
    public void SealStateDisablesBothLightAndArea_AndCanRelight()
    {
        CreateField(out _, out _);
        var source = CreateSource(out var light, false);
        var seal = source.gameObject.AddComponent<CandlestickSeal>();
        Set(source, "seal", seal);
        source.gameObject.SetActive(true);
        Set(seal, "isSealed", true);
        var changed = (Action<bool>)typeof(CandlestickSeal).GetField("SealChanged", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(seal);
        changed?.Invoke(true);
        Assert.IsFalse(((Behaviour)light).enabled);
        Assert.IsFalse(source.TryGetArea(out _));
        Set(seal, "isSealed", false);
        changed?.Invoke(false);
        Assert.IsTrue(((Behaviour)light).enabled);
        Assert.IsTrue(source.TryGetArea(out _));
    }

    [UnityTest]
    public IEnumerator LitShaderRevealsOnlyPartialSprite_AndFlashCannotLeakOutsideSources()
    {
        var previousPipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
        var previousQualityPipeline = QualitySettings.renderPipeline;
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/_Project/Settings/UniversalRP.asset");
        Assert.IsNotNull(pipeline);
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        var target = new RenderTexture(64, 64, 24);
        objects.Add(target);
        try
        {
            CreateField(out var global, out _);
            var kind = LightType.GetProperty("lightType").PropertyType;
            SetLight(global, "lightType", Enum.Parse(kind, "Global"));
            var source = CreateSource(out var spot);
            source.transform.position = new Vector3(-0.5f, 0f);
            Set(source, "revealRadius", 0.55f);
            SetLight(spot, "lightType", Enum.Parse(kind, "Point"));
            SetLight(spot, "pointLightOuterRadius", 0.7f);
            SetLight(spot, "pointLightInnerRadius", 0.6f);
            var texture = new Texture2D(16, 16);
            var colors = new Color[256];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            texture.SetPixels(colors);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 8f);
            objects.Add(texture);
            objects.Add(sprite);
            var monster = NewObject("Rendered shadow monster");
            monster.SetActive(false);
            var renderer = monster.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            var filter = monster.AddComponent<ShadowMonsterGaugeVisibilityFilter>();
            var adapter = monster.AddComponent<ShadowMonsterLightVisibility>();
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_ShadowMonsterRevealLit.mat");
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
            Set(adapter, "targetRenderers", new[] { renderer });
            Set(adapter, "gaugeFilter", filter);
            Set(adapter, "revealMaterial", material);
            monster.SetActive(true);
            var camera = NewObject("Verification camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.targetTexture = target;
            yield return null;
            yield return null;
            int visible = CountBrightPixels(target, true);
            Assert.Greater(visible, 10);
            Assert.Less(visible, 900, "Only part of the 32x32 sprite should be revealed.");
            long fullLightEnergy = lastRgbEnergy;
            SetLight(spot, "intensity", 0.15f);
            yield return null;
            yield return null;
            Assert.Greater(CountBrightPixels(target, false), 10);
            Assert.Less(lastRgbEnergy, fullLightEnergy, "Visible pixels must respond to actual Light2D intensity.");
            source.gameObject.SetActive(false);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetFloat("_FlashAmount", 1f);
            renderer.SetPropertyBlock(block);
            yield return null;
            yield return null;
            Assert.AreEqual(0, CountBrightPixels(target, false), "Ambient light and hit flash must not reveal hidden pixels.");
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
        }
        finally
        {
            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = previousPipeline;
            QualitySettings.renderPipeline = previousQualityPipeline;
        }
    }

    private int CountBrightPixels(RenderTexture target, bool save)
    {
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        objects.Add(image);
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        RenderTexture.active = previous;
        int count = 0;
        lastRgbEnergy = 0;
        foreach (var pixel in image.GetPixels32())
        {
            if (pixel.r > 5 || pixel.g > 5 || pixel.b > 5) count++;
            lastRgbEnergy += pixel.r + pixel.g + pixel.b;
        }
        if (save)
            System.IO.File.WriteAllBytes("Library/ShadowLightVerification.png", image.EncodeToPNG());
        TestContext.WriteLine($"Rendered bright pixels: {count}, RGB energy: {lastRgbEnergy}");
        return count;
    }

    private SceneLightVisionPresentation CreateField(out Component light, out GlobalVisionMaskController controller)
    {
        var root = NewObject("Scene lighting");
        root.SetActive(false);
        light = root.AddComponent(LightType);
        var field = root.AddComponent<SceneLightVisionPresentation>();
        Set(field, "globalLight", light);
        controller = root.AddComponent<GlobalVisionMaskController>();
        Set(controller, "presentationOverride", field);
        Set(controller, "enterFogFadeDuration", 0f);
        Set(controller, "exitFogFadeDuration", 0f);
        root.SetActive(true);
        return field;
    }

    private LightRevealSource CreateSource(out Component light, bool activate = true)
    {
        var root = NewObject("Reveal source");
        root.SetActive(false);
        light = root.AddComponent(LightType);
        ((Behaviour)light).enabled = false;
        var source = root.AddComponent<LightRevealSource>();
        Set(source, "sourceLight", light);
        if (activate) root.SetActive(true);
        return source;
    }

    private GameObject NewObject(string name)
    {
        var value = new GameObject(name);
        objects.Add(value);
        return value;
    }

    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void SetLight(Component target, string property, object value) => LightType.GetProperty(property).SetValue(target, value);
    private static T GetLight<T>(Component target, string property) => (T)LightType.GetProperty(property).GetValue(target);
}
#endif
