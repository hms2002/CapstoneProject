#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>Exercises ready-flash material ownership through the real shared slot presenter and HUD lifecycle.</summary>
public sealed class ReadyFlashMaterialLifecyclePlayModeTests
{
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Type Presenter = Type.GetType("WeaponSkillHudSlotPresenter, UI", true);
    private static readonly Type SlotType = Type.GetType("WeaponSkillHUD2D+SkillSlotUI, UI", true);
    private static readonly Type ImageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true);

    [Test]
    public void IdleSlots_ReadyPolling_ShareFrozenReadyZeroWithoutCreatingActiveInstances()
    {
        using (var fixture = new SlotFixture())
        using (var second = new SlotFixture())
        {
            for (int i = 0; i < 32; i++)
                fixture.UpdateReady(true);

            Assert.That(fixture.Material, Is.SameAs(second.Material));
            Assert.That(fixture.Material.shader.name, Is.EqualTo("UI/Icon Ready Flash"));
            Assert.That(fixture.Material.GetFloat("_FlashAmount"), Is.Zero);
            Assert.That(fixture.OwnedMaterial, Is.Null);
            Assert.That(fixture.Color, Is.EqualTo(fixture.NormalColor));
            Assert.That(fixture.Icon.transform.localScale, Is.EqualTo(fixture.BaseScale));
        }
    }

    [UnityTest]
    public IEnumerator Pulse_UnscaledDuration_RestoresSharedMaterialAndReusesOwnedInstance()
    {
        float previousTimeScale = Time.timeScale;
        using (var fixture = new SlotFixture())
        {
            try
            {
                Time.timeScale = 0f;
                Material sharedIdle = fixture.Material;
                fixture.UpdateReady(false);
                fixture.UpdateReady(true);
                Material owned = fixture.OwnedMaterial;
                Assert.That(owned, Is.Not.Null);
                Assert.That(fixture.Material, Is.SameAs(owned));
                Assert.That(owned.shader.name, Is.EqualTo("UI/Icon Ready Flash"));
                Assert.That(owned.GetFloat("_FlashAmount"), Is.EqualTo(1f));
                Assert.That(owned.GetFloat("_FlashMultiply"), Is.EqualTo(1f));
                Assert.That(owned.GetColor("_FlashColor"), Is.EqualTo(Color.white));
                Assert.That(fixture.Remaining, Is.EqualTo(fixture.Duration));
                Assert.That(fixture.Icon.transform.localScale, Is.EqualTo(fixture.BaseScale * 1.16f));

                float deadline = Time.realtimeSinceStartup + 2f;
                while (fixture.Remaining > 0f && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    fixture.UpdateReady(true);
                }

                Assert.That(fixture.Remaining, Is.Zero, "Ready flashes must expire while gameplay time is paused.");
                Assert.That(fixture.Material, Is.SameAs(sharedIdle));
                Assert.That(fixture.OwnedMaterial, Is.SameAs(owned));
                Assert.That(owned.GetFloat("_FlashAmount"), Is.Zero);
                Assert.That(fixture.Icon.transform.localScale, Is.EqualTo(fixture.BaseScale));
                fixture.UpdateReady(false);
                fixture.UpdateReady(true);
                Assert.That(fixture.Material, Is.SameAs(owned), "Subsequent pulses reuse the owned material.");
            }
            finally
            {
                Time.timeScale = previousTimeScale;
            }
        }
    }

    [Test]
    public void SimultaneousSlots_PulseAmountsAndRestoration_AreIndependent()
    {
        using (var first = new SlotFixture())
        using (var second = new SlotFixture())
        {
            first.UpdateReady(false);
            second.UpdateReady(false);
            first.UpdateReady(true);
            second.UpdateReady(true);
            Material secondOwned = second.OwnedMaterial;
            Assert.That(first.OwnedMaterial, Is.Not.SameAs(secondOwned));
            first.ApplyAmount(0.25f);
            Assert.That(first.OwnedMaterial.GetFloat("_FlashAmount"), Is.EqualTo(0.25f));
            Assert.That(secondOwned.GetFloat("_FlashAmount"), Is.EqualTo(1f));
            first.Cancel();
            Assert.That(first.Material, Is.SameAs(first.OriginalMaterial));
            Assert.That(second.Material, Is.SameAs(secondOwned));
            Assert.That(second.Remaining, Is.EqualTo(second.Duration));
        }
    }

    [UnityTest]
    public IEnumerator ExternalMaterialReplacement_ActiveAndIdle_PreservesLatestWriterAndSharedAssets()
    {
        using (var fixture = new SlotFixture())
        {
            var original = new Material(Shader.Find("UI/Default"));
            var replacement = new Material(original);
            var texture = new Texture2D(2, 2);
            try
            {
                replacement.mainTexture = texture;
                fixture.Material = original;
                fixture.UpdateReady(false);
                fixture.UpdateReady(true);
                Material owned = fixture.OwnedMaterial;
                fixture.Material = replacement;
                fixture.ApplyAmount(0.5f);
                Assert.That(fixture.Material, Is.SameAs(owned));
                Assert.That(owned.mainTexture, Is.SameAs(texture));
                fixture.Cancel();
                Assert.That(fixture.Material, Is.SameAs(replacement));

                fixture.Material = original;
                fixture.Cancel();
                Assert.That(fixture.Material, Is.SameAs(original), "Idle cleanup must not overwrite an external writer.");
                fixture.Release();
                yield return null;
                Assert.That(owned == null, Is.True);
                Assert.That(original != null && replacement != null, Is.True, "Only the slot-owned instance is destroyed.");
                Assert.That(fixture.Material, Is.SameAs(original));
            }
            finally
            {
                Object.DestroyImmediate(original);
                Object.DestroyImmediate(replacement);
                Object.DestroyImmediate(texture);
            }
        }
    }

    [Test]
    public void PublicCooldownAndCastingSequence_PreservesCooldownTintAndReadyTransition()
    {
        using (var fixture = new SlotFixture())
        {
            AbilitySystem abilities = fixture.Root.AddComponent<AbilitySystem>();
            abilities.enabled = false;
            abilities.GiveAbility(fixture.Definition);
            Assert.That(abilities.TrySetCooldownRemaining(fixture.Definition, 0.5f), Is.True);
            fixture.UpdateCooldownAndCasting(abilities);
            Assert.That(fixture.OwnedMaterial, Is.Null);
            Assert.That(fixture.Color.r, Is.EqualTo(fixture.NormalColor.r * 0.6f).Within(0.0001f));
            Assert.That(fixture.Color.a, Is.EqualTo(fixture.NormalColor.a));

            Assert.That(abilities.TrySetCooldownRemaining(fixture.Definition, 0f), Is.True);
            fixture.UpdateCooldownAndCasting(abilities);
            Assert.That(fixture.Material, Is.SameAs(fixture.OwnedMaterial));
            Assert.That(fixture.OwnedMaterial, Is.Not.Null);
            Assert.That(fixture.Color, Is.EqualTo(fixture.NormalColor), "Casting projection still runs after the ready flash update.");
        }
    }

    [UnityTest]
    public IEnumerator CurrentHud_DisableReenableDestroy_RestoresReusesAndReleasesOwnedMaterial()
    {
        yield return VerifyHudLifecycle("WeaponSkillHUD2D");
    }

    [UnityTest]
    public IEnumerator SwapHud_DisableReenableDestroy_RestoresReusesAndReleasesOwnedMaterial()
    {
        yield return VerifyHudLifecycle("SwapWeaponSkillHUD2D");
    }

    [Test]
    public void ReadyShader_SharedIdlePixels_MatchFrozenZeroAndActivePixelsRetainFlash()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("Requires a graphics device for the UI shader pixel equivalence check.");

        using (var fixture = new SlotFixture())
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels(new[] { new Color(0.2f, 0.4f, 0.6f, 1f), new Color(0.7f, 0.1f, 0.3f, 1f),
                    new Color(0.1f, 0.7f, 0.2f, 0.5f), new Color(0.5f, 0.3f, 0.1f, 0f) });
                texture.Apply();
                Material sharedIdle = fixture.Material;
                var frozenZero = new Material(Shader.Find("UI/Icon Ready Flash"));
                frozenZero.SetFloat("_FlashAmount", 0f);
                frozenZero.SetFloat("_FlashMultiply", 1f);
                Color32[] baseline;
                try { baseline = RenderPixels(texture, frozenZero); }
                finally { Object.DestroyImmediate(frozenZero); }
                Assert.That(baseline[0].a, Is.GreaterThan(240), "Pixel fixture must render an opaque canary.");
                fixture.UpdateReady(false);
                fixture.UpdateReady(true);
                Color32[] active = RenderPixels(texture, fixture.OwnedMaterial);
                Assert.That(active[0].r, Is.GreaterThan(baseline[0].r + 10));
                Assert.That(active[0].a, Is.EqualTo(baseline[0].a));
                fixture.ApplyAmount(0f);
                Color32[] idle = RenderPixels(texture, fixture.Material);
                for (int i = 0; i < baseline.Length; i++)
                {
                    Assert.That((int)idle[i].r, Is.EqualTo((int)baseline[i].r).Within(2));
                    Assert.That((int)idle[i].g, Is.EqualTo((int)baseline[i].g).Within(2));
                    Assert.That((int)idle[i].b, Is.EqualTo((int)baseline[i].b).Within(2));
                    Assert.That((int)idle[i].a, Is.EqualTo((int)baseline[i].a).Within(2));
                }
                Assert.That(fixture.Material, Is.SameAs(sharedIdle));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }

    [UnityTest]
    public IEnumerator AuthoredSkillIcon_FrozenReadyZeroVersusDefault_ReportsActualCanvasRgba()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("Requires a graphics device for the authored Image Canvas rendering diagnostic.");

        foreach (bool masked in new[] { false, true })
        foreach (Color tint in new[] { Color.white, new Color(0.8f, 0.7f, 0.6f, 0.75f) })
        {
            using (var fixture = new AuthoredIconRenderFixture(masked, tint))
            {
                yield return null;
                Color32[] frozen = fixture.Capture(fixture.FrozenReadyZero);
                Color32[] original = fixture.Capture(fixture.OriginalMaterial);
                int nontransparent = 0;
                int differingPixels = 0;
                int maximumRgbDifference = 0;
                int maximumAlphaDifference = 0;
                for (int i = 0; i < frozen.Length; i++)
                {
                    if (frozen[i].a > 0) nontransparent++;
                    int rgb = Mathf.Max(Mathf.Abs(frozen[i].r - original[i].r),
                        Mathf.Abs(frozen[i].g - original[i].g), Mathf.Abs(frozen[i].b - original[i].b));
                    int alpha = Mathf.Abs(frozen[i].a - original[i].a);
                    maximumRgbDifference = Mathf.Max(maximumRgbDifference, rgb);
                    maximumAlphaDifference = Mathf.Max(maximumAlphaDifference, alpha);
                    if (rgb > 2 || alpha > 2) differingPixels++;
                }
                Assert.That(nontransparent, Is.GreaterThan(128), "The actual authored Icon must render a visible canary.");
                TestContext.Out.WriteLine("AuthoredReadyIdleCanvasDiagnostic=" + JsonUtility.ToJson(new CanvasRgbaReport
                {
                    masked = masked,
                    tint = tint,
                    nontransparentPixels = nontransparent,
                    differingPixels = differingPixels,
                    maximumRgbDifference = maximumRgbDifference,
                    maximumAlphaDifference = maximumAlphaDifference,
                    frozenOpaquePixel = frozen[56 + 56 * 128],
                    originalOpaquePixel = original[56 + 56 * 128],
                    frozenTranslucentPixel = frozen[56 + 72 * 128],
                    originalTranslucentPixel = original[56 + 72 * 128],
                }));
            }
        }
    }

    [UnityTest]
    public IEnumerator AuthoredSkillIcon_CurrentSharedIdle_MatchesFrozenZeroAllCanvasPixels()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("Requires a graphics device for the authored Image Canvas pixel regression.");
        foreach (bool masked in new[] { false, true })
        foreach (Color tint in new[] { Color.white, new Color(0.8f, 0.7f, 0.6f, 0.75f) })
        {
            using (var fixture = new AuthoredIconRenderFixture(masked, tint))
            {
                yield return null;
                Material sharedIdle = fixture.ProjectSharedIdle();
                Color32[] frozen = fixture.Capture(fixture.FrozenReadyZero);
                Color32[] current = fixture.Capture(sharedIdle);
                int visible = 0;
                for (int i = 0; i < frozen.Length; i++)
                {
                    if (frozen[i].a > 0) visible++;
                    Assert.That((int)current[i].r, Is.EqualTo((int)frozen[i].r).Within(2), $"R pixel {i}, mask={masked}");
                    Assert.That((int)current[i].g, Is.EqualTo((int)frozen[i].g).Within(2), $"G pixel {i}, mask={masked}");
                    Assert.That((int)current[i].b, Is.EqualTo((int)frozen[i].b).Within(2), $"B pixel {i}, mask={masked}");
                    Assert.That((int)current[i].a, Is.EqualTo((int)frozen[i].a).Within(2), $"A pixel {i}, mask={masked}");
                }
                Assert.That(visible, Is.GreaterThan(128));
            }
        }
    }

    [Test]
    public void AuthoredMaskedSlots_MatchingSetup_ShareIdleAndStencilMaterialReferences()
    {
        using (var first = new AuthoredIconRenderFixture(true, Color.white))
        using (var second = new AuthoredIconRenderFixture(true, Color.white))
        {
            Assert.That(first.ProjectSharedIdle(), Is.SameAs(second.ProjectSharedIdle()));
            Assert.That(first.MaterialForRendering, Is.SameAs(second.MaterialForRendering),
                "Equal stencil depth can reuse the same derived material; different sprite textures remain PerRendererData.");
        }
    }

    [UnityTest]
    public IEnumerator SharedIdle_OriginalTextureKeysAndLastLeaseRelease_AreBounded()
    {
        int before = (int)Presenter.GetProperty("DiagnosticSharedIdleMaterialCount").GetValue(null);
        var texture = new Texture2D(2, 2);
        var original = new Material(Shader.Find("UI/Default")) { mainTexture = texture };
        using (var first = new SlotFixture())
        using (var second = new SlotFixture())
        {
            try
            {
                first.Material = original;
                second.Material = original;
                first.ApplyAmount(0f);
                second.ApplyAmount(0f);
                Material shared = first.Material;
                Assert.That(shared, Is.SameAs(second.Material));
                Assert.That(shared.mainTexture, Is.SameAs(texture));
                first.Release();
                Assert.That(shared != null, Is.True);
                Assert.That(second.Material, Is.SameAs(shared));
                second.Release();
                Assert.That((int)Presenter.GetProperty("DiagnosticSharedIdleMaterialCount").GetValue(null), Is.EqualTo(before));
                yield return null;
                Assert.That(shared == null, Is.True);
                Assert.That(original != null, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(original);
                Object.DestroyImmediate(texture);
            }
        }
    }

    [Test]
    public void SharedIdle_DifferentOriginalTextureSetups_KeepDistinctMaterials()
    {
        var firstTexture = new Texture2D(2, 2);
        var secondTexture = new Texture2D(2, 2);
        var firstOriginal = new Material(Shader.Find("UI/Default")) { mainTexture = firstTexture };
        var secondOriginal = new Material(Shader.Find("UI/Default")) { mainTexture = secondTexture };
        try
        {
            using (var first = new SlotFixture())
            using (var second = new SlotFixture())
            {
                first.Material = firstOriginal;
                second.Material = secondOriginal;
                first.ApplyAmount(0f);
                second.ApplyAmount(0f);
                Assert.That(first.Material, Is.Not.SameAs(second.Material));
                Assert.That(first.Material.mainTexture, Is.SameAs(firstTexture));
                Assert.That(second.Material.mainTexture, Is.SameAs(secondTexture));
            }
            Assert.That(firstOriginal != null && secondOriginal != null, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(firstOriginal);
            Object.DestroyImmediate(secondOriginal);
            Object.DestroyImmediate(firstTexture);
            Object.DestroyImmediate(secondTexture);
        }
    }

    private static IEnumerator VerifyHudLifecycle(string hudTypeName)
    {
        using (var fixture = new SlotFixture())
        {
            Type hudType = Type.GetType(hudTypeName + ", UI", true);
            var hudRoot = new GameObject("ReadyFlashHudLifecycle");
            hudRoot.SetActive(false);
            var external = new Material(Shader.Find("UI/Default"));
            try
            {
                var hud = (Behaviour)hudRoot.AddComponent(hudType);
                hudType.GetField("skill1UI", InstanceFields).SetValue(hud, fixture.Slot);
                hudRoot.SetActive(true);
                fixture.ApplySlot();
                fixture.UpdateReady(false);
                fixture.UpdateReady(true);
                Material owned = fixture.OwnedMaterial;
                hud.enabled = false;
                Assert.That(fixture.Remaining, Is.Zero);
                Assert.That(fixture.Material, Is.SameAs(fixture.OriginalMaterial));
                Assert.That(fixture.Icon.transform.localScale, Is.EqualTo(fixture.BaseScale));
                Assert.That(fixture.OwnedMaterial, Is.SameAs(owned));
                hud.enabled = true;
                fixture.ApplySlot();
                fixture.UpdateReady(false);
                fixture.UpdateReady(true);
                Assert.That(fixture.Material, Is.SameAs(owned));

                fixture.Material = external;
                Object.Destroy(hudRoot);
                yield return null;
                Assert.That(owned == null, Is.True);
                Assert.That(fixture.Material, Is.SameAs(external));
                Assert.That(external != null, Is.True);
                Assert.That(fixture.OwnedMaterial, Is.Null);
            }
            finally
            {
                if (hudRoot != null) Object.DestroyImmediate(hudRoot);
                Object.DestroyImmediate(external);
            }
        }
    }

    private static Color32[] RenderPixels(Texture2D texture, Material material)
    {
        var target = new RenderTexture(2, 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var readback = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        RenderTexture previous = RenderTexture.active;
        try
        {
            target.Create();
            RenderTexture.active = target;
            GL.Clear(true, true, Color.clear);
            Graphics.Blit(texture, target, material);
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, 2, 2), 0, 0);
            readback.Apply();
            return readback.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previous;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(readback);
        }
    }

    private static void Invoke(string name, params object[] args)
    {
        Presenter.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, args);
    }

    [Serializable]
    private sealed class CanvasRgbaReport
    {
        public bool masked;
        public Color tint;
        public int nontransparentPixels;
        public int differingPixels;
        public int maximumRgbDifference;
        public int maximumAlphaDifference;
        public Color32 frozenOpaquePixel;
        public Color32 originalOpaquePixel;
        public Color32 frozenTranslucentPixel;
        public Color32 originalTranslucentPixel;
    }

    private sealed class AuthoredIconRenderFixture : IDisposable
    {
        private readonly GameObject root;
        private readonly GameObject cameraRoot;
        private readonly Camera camera;
        private readonly Component icon;
        private readonly RenderTexture target;
        private readonly Texture2D texture;
        private readonly Sprite sprite;
        private readonly Color tint;
        private object projectedSlot;
        private AbilityDefinition projectedDefinition;
        public readonly Material OriginalMaterial;
        public readonly Material FrozenReadyZero;
        public Material MaterialForRendering => (Material)ImageType.GetProperty("materialForRendering").GetValue(icon);

        public AuthoredIconRenderFixture(bool masked, Color tint)
        {
            this.tint = tint;
            const int layer = 31;
            root = new GameObject("AuthoredReadyIconCanvas", typeof(RectTransform), typeof(Canvas));
            root.layer = layer;
            cameraRoot = new GameObject("AuthoredReadyIconCamera", typeof(Camera));
            camera = cameraRoot.GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << layer;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            target.Create();
            camera.targetTexture = target;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;

            Transform parent = root.transform;
            if (masked)
            {
                Type maskType = Type.GetType("UnityEngine.UI.Mask, UnityEngine.UI", true);
                var maskRoot = new GameObject("AuthoredIconMask", typeof(RectTransform), ImageType, maskType);
                maskRoot.layer = layer;
                maskRoot.transform.SetParent(parent, false);
                ((RectTransform)maskRoot.transform).sizeDelta = new Vector2(32f, 48f);
                maskType.GetProperty("showMaskGraphic").SetValue(maskRoot.GetComponent(maskType), false);
                parent = maskRoot.transform;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/WeaponHUDUI/Skill1UI.prefab");
            Assert.That(prefab, Is.Not.Null);
            Transform authoredIcon = prefab.transform.Find("Icon");
            Assert.That(authoredIcon, Is.Not.Null);
            var iconRoot = Object.Instantiate(authoredIcon.gameObject, parent, false);
            iconRoot.layer = layer;
            icon = iconRoot.GetComponent(ImageType);
            Assert.That(icon, Is.Not.Null);
            var rect = (RectTransform)icon.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(64f, 64f);
            rect.localScale = Vector3.one;
            ImageType.GetProperty("color").SetValue(icon, tint);
            OriginalMaterial = (Material)ImageType.GetProperty("material").GetValue(icon);
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            texture.SetPixels(new[] { new Color(0.2f, 0.4f, 0.6f, 1f), new Color(0.7f, 0.1f, 0.3f, 1f),
                new Color(0.1f, 0.7f, 0.2f, 0.5f), new Color(0.5f, 0.3f, 0.1f, 0f) });
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
            ImageType.GetProperty("sprite").SetValue(icon, sprite);
            // Frozen original TryApplyReadyFlashMaterial setup, with amount zero.
            FrozenReadyZero = new Material(Shader.Find("UI/Icon Ready Flash"));
            if (OriginalMaterial != null && OriginalMaterial.HasProperty("_MainTex"))
                FrozenReadyZero.mainTexture = OriginalMaterial.mainTexture;
            FrozenReadyZero.SetColor("_FlashColor", new Color(1f, 0.95f, 0.55f, 1f));
            FrozenReadyZero.SetFloat("_FlashAmount", 0f);
            FrozenReadyZero.SetFloat("_FlashMultiply", 1f);
        }

        public Material ProjectSharedIdle()
        {
            if (projectedSlot == null)
            {
                projectedSlot = Activator.CreateInstance(SlotType);
                SlotType.GetField("root").SetValue(projectedSlot, icon.gameObject);
                SlotType.GetField("icon").SetValue(projectedSlot, icon);
                projectedDefinition = ScriptableObject.CreateInstance<AbilityDefinition>();
                projectedDefinition.icon = sprite;
                Invoke("ApplySlot", projectedSlot, projectedDefinition, tint, null);
            }
            return (Material)ImageType.GetProperty("material").GetValue(icon);
        }

        public Color32[] Capture(Material material)
        {
            ImageType.GetProperty("material").SetValue(icon, material);
            Canvas.ForceUpdateCanvases();
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True,
                    "The active pipeline must support a standard Camera render request for this fixture.");
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else
            {
                camera.Render();
            }

            var readback = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                readback.Apply();
                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(readback);
            }
        }

        public void Dispose()
        {
            if (projectedSlot != null) Invoke("ReleaseReadyFlashMaterial", projectedSlot);
            if (projectedDefinition != null) Object.DestroyImmediate(projectedDefinition);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(cameraRoot);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(FrozenReadyZero);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }

    private sealed class SlotFixture : IDisposable
    {
        public readonly GameObject Root;
        public readonly Component Icon;
        public readonly object Slot;
        public readonly AbilityDefinition Definition;
        public readonly Material OriginalMaterial;
        public readonly Color NormalColor = new Color(0.8f, 0.7f, 0.6f, 0.75f);
        public readonly Vector3 BaseScale = new Vector3(1.2f, 0.9f, 1f);
        public float Duration => 0.12f;
        public Material Material
        {
            get => (Material)ImageType.GetProperty("material").GetValue(Icon);
            set => ImageType.GetProperty("material").SetValue(Icon, value);
        }
        public Material OwnedMaterial => (Material)SlotType.GetField("readyFlashMaterialInstance").GetValue(Slot);
        public Color Color => (Color)ImageType.GetProperty("color").GetValue(Icon);
        public float Remaining => (float)SlotType.GetField("readyFlashRemaining").GetValue(Slot);

        public SlotFixture()
        {
            Root = new GameObject("ReadyFlashSlotFixture", typeof(RectTransform));
            var iconRoot = new GameObject("Icon", typeof(RectTransform), ImageType);
            iconRoot.transform.SetParent(Root.transform, false);
            Icon = iconRoot.GetComponent(ImageType);
            Icon.transform.localScale = BaseScale;
            OriginalMaterial = Material;
            Slot = Activator.CreateInstance(SlotType);
            SlotType.GetField("root").SetValue(Slot, Root);
            SlotType.GetField("icon").SetValue(Slot, Icon);
            SlotType.GetField("readyFlashDuration").SetValue(Slot, Duration);
            Definition = ScriptableObject.CreateInstance<AbilityDefinition>();
            Definition.cooldown = 1f;
            ApplySlot();
        }

        public void ApplySlot() => Invoke("ApplySlot", Slot, Definition, NormalColor, null);
        public void UpdateReady(bool ready) => Invoke("UpdateReadyFlash", Slot, ready, -1);
        public void ApplyAmount(float amount) => Invoke("ApplyReadyFlashVisual", Slot, amount);
        public void Cancel() => Invoke("CancelReadyFlash", Slot);
        public void Release() => Invoke("ReleaseReadyFlashMaterial", Slot);

        public void UpdateCooldownAndCasting(AbilitySystem abilities)
        {
            Type slotEnum = Presenter.GetMethod("UpdateCooldownAndCharge").GetParameters()[1].ParameterType;
            Invoke("UpdateCooldownAndCharge", Slot, Enum.Parse(slotEnum, "Skill1"), Definition, abilities, null);
            Invoke("UpdateCastingVisual", Slot, Definition, abilities, NormalColor, UnityEngine.Color.yellow, 8f, 0.18f);
        }

        public void Dispose()
        {
            Release();
            Object.DestroyImmediate(Root);
            Object.DestroyImmediate(Definition);
        }
    }
}
#endif
