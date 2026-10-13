#if UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Native correctness and operation measurements that gate lake preset synchronization changes.</summary>
public sealed class UpgradeLakeMaterialSynchronizationPlayModeTests
{
    private const string PresetGuid = "a7f89c4cb2e6d9742a81af669b7bfb2d";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Type LakeType = Type.GetType("UpgradeLakePresentation, UI", true);
    private static readonly Type SettingsType = Type.GetType("UpgradeLakePresentationSettings, UI", true);
    private static readonly Type ImageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true);

    [TestCase("float")]
    [TestCase("color")]
    [TestCase("vector")]
    [TestCase("texture")]
    [TestCase("textureOffset")]
    [TestCase("textureScale")]
    [TestCase("keyword")]
    [TestCase("renderQueue")]
    [TestCase("giFlags")]
    [TestCase("instancing")]
    [TestCase("doubleSidedGI")]
    public void NativeMaterialCrc_RequiredPresetMutation_ChangesSignature(string mutation)
    {
        Material authored = LoadPreset();
        var source = new Material(authored);
        var texture = new Texture2D(2, 2);
        try
        {
            int before = source.ComputeCRC();
            Mutate(source, texture, mutation);
            Assert.That(source.ComputeCRC(), Is.Not.EqualTo(before),
                $"A CRC-only synchronization guard is unsafe if it misses {mutation} changes.");
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(texture);
        }
    }

    [UnityTest]
    public IEnumerator LakeProjection_RuntimePresetChangesAndDynamicTime_AreObservedOnNextUpdate()
    {
        using (var fixture = new LakeFixture())
        {
            Material owned = fixture.RuntimeMaterial;
            Assert.That(owned, Is.Not.SameAs(fixture.Preset));
            float previousTime = owned.GetFloat("_UnscaledTime");
            yield return null;
            fixture.Tick();
            Assert.That(fixture.RuntimeMaterial, Is.SameAs(owned));
            Assert.That(owned.GetFloat("_UnscaledTime"), Is.GreaterThan(previousTime));
            Assert.That(owned.GetVector("_ContentOffset"), Is.EqualTo(new Vector4(7f, -4f, 0f, 0f)));

            var replacementTexture = new Texture2D(2, 2);
            try
            {
                fixture.Preset.SetFloat("_WaveSpeed", 1.234f);
                fixture.Preset.SetColor("_DeepColor", new Color(0.4f, 0.2f, 0.3f, 1f));
                fixture.Preset.SetVector("_FoamSpeed", new Vector4(0.17f, 0.19f, 0.23f, 0.29f));
                fixture.Preset.SetTexture("_BottomTex", replacementTexture);
                fixture.Preset.EnableKeyword("UNITY_UI_ALPHACLIP");
                fixture.Preset.renderQueue += 1;
                fixture.Tick();
                Assert.That(owned.GetFloat("_WaveSpeed"), Is.EqualTo(1.234f));
                Assert.That(owned.GetColor("_DeepColor"), Is.EqualTo(fixture.Preset.GetColor("_DeepColor")));
                Assert.That(owned.GetVector("_FoamSpeed"), Is.EqualTo(fixture.Preset.GetVector("_FoamSpeed")));
                Assert.That(owned.GetTexture("_BottomTex"), Is.SameAs(replacementTexture));
                Assert.That(owned.IsKeywordEnabled("UNITY_UI_ALPHACLIP"), Is.True);
                Assert.That(owned.renderQueue, Is.EqualTo(fixture.Preset.renderQueue));

                fixture.Content.anchoredPosition = new Vector2(13f, -11f);
                fixture.Tick();
                Assert.That(owned.GetVector("_ContentOffset"), Is.EqualTo(new Vector4(13f, -11f, 0f, 0f)),
                    "Dynamic projection must continue even when the authored preset signature is unchanged.");
            }
            finally
            {
                Object.DestroyImmediate(replacementTexture);
            }
        }
    }

    [Test]
    public void LakeProjection_UnchangedUpdatesSkipPresetCopies_ButSourceChangesSynchronizeImmediately()
    {
        using (var fixture = new LakeFixture())
        {
            int initialCopies = fixture.PresetCopyCount;
            Assert.That(initialCopies, Is.EqualTo(1));
            Material owned = fixture.RuntimeMaterial;
            for (int i = 0; i < 64; i++)
            {
                fixture.Content.anchoredPosition = new Vector2(i, -i);
                fixture.Tick();
                Assert.That(owned.GetVector("_ContentOffset"), Is.EqualTo(new Vector4(i, -i, 0f, 0f)));
            }
            Assert.That(fixture.PresetCopyCount, Is.EqualTo(initialCopies));

            fixture.Preset.SetFloat("_WaveSpeed", 1.234f);
            fixture.Tick();
            Assert.That(fixture.PresetCopyCount, Is.EqualTo(initialCopies + 1));
            Assert.That(owned.GetFloat("_WaveSpeed"), Is.EqualTo(1.234f));
            fixture.Tick();
            Assert.That(fixture.PresetCopyCount, Is.EqualTo(initialCopies + 1));

            fixture.Preset.EnableKeyword("UNITY_UI_ALPHACLIP");
            fixture.Tick();
            Assert.That(fixture.PresetCopyCount, Is.EqualTo(initialCopies + 2));
            Assert.That(owned.IsKeywordEnabled("UNITY_UI_ALPHACLIP"), Is.True);
        }
    }

    [Test]
    public void NativePresetCopyVersusUnchangedIdentityCrc_ReportsMatchedWarmedOperationCosts()
    {
        const int samples = 31;
        const int operationsPerSample = 2048;
        var source = new Material(LoadPreset());
        var copy = new Material(source);
        Material cachedSource = source;
        int cachedCrc = source.ComputeCRC();
        int canary = cachedCrc;
        Action baseline = () => copy.CopyPropertiesFromMaterial(source);
        Action signature = () =>
        {
            int crc = source.ComputeCRC();
            canary = crc;
            if (source == cachedSource && crc == cachedCrc)
                return;
            copy.CopyPropertiesFromMaterial(source);
            cachedSource = source;
            cachedCrc = crc;
        };
        Action empty = () => { };
        var baselineNs = new double[samples];
        var signatureNs = new double[samples];
        var emptyNs = new double[samples];
        try
        {
            for (int i = 0; i < 64; i++)
            {
                baseline();
                signature();
            }

            for (int i = 0; i < samples; i++)
            {
                emptyNs[i] = Measure(empty, operationsPerSample);
                if ((i & 1) == 0)
                {
                    baselineNs[i] = Measure(baseline, operationsPerSample);
                    signatureNs[i] = Measure(signature, operationsPerSample);
                }
                else
                {
                    signatureNs[i] = Measure(signature, operationsPerSample);
                    baselineNs[i] = Measure(baseline, operationsPerSample);
                }
            }

            Assert.That(canary, Is.EqualTo(source.ComputeCRC()));
            TestContext.Out.WriteLine("LakeMaterialOperationBenchmark=" + JsonUtility.ToJson(new OperationReport
            {
                unityVersion = Application.unityVersion,
                samples = samples,
                operationsPerSample = operationsPerSample,
                baselineMedianNs = Percentile(baselineNs, 0.5),
                baselineP95Ns = Percentile(baselineNs, 0.95),
                unchangedIdentityCrcMedianNs = Percentile(signatureNs, 0.5),
                unchangedIdentityCrcP95Ns = Percentile(signatureNs, 0.95),
                emptyMedianNs = Percentile(emptyNs, 0.5),
                baselineRawNs = baselineNs,
                unchangedIdentityCrcRawNs = signatureNs,
                note = "Editor native operation cost; ns per operation from warmed batch means, not frame-time percentiles or Player measurements. No profiler recorder or allocation-byte inference.",
            }));
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(copy);
        }
    }

    private static Material LoadPreset()
    {
        Material preset = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(PresetGuid));
        Assert.That(preset, Is.Not.Null, "Use the same authored material as UpgradeTreePanel.");
        return preset;
    }

    private static void Mutate(Material material, Texture2D texture, string mutation)
    {
        switch (mutation)
        {
            case "float": material.SetFloat("_WaveSpeed", material.GetFloat("_WaveSpeed") + 0.123f); break;
            case "color": material.SetColor("_DeepColor", new Color(0.4f, 0.2f, 0.3f, 0.8f)); break;
            case "vector": material.SetVector("_FoamSpeed", new Vector4(0.17f, 0.19f, 0.23f, 0.29f)); break;
            case "texture": material.SetTexture("_BottomTex", texture); break;
            case "textureOffset": material.SetTextureOffset("_BottomTex", new Vector2(0.13f, 0.17f)); break;
            case "textureScale": material.SetTextureScale("_BottomTex", new Vector2(1.13f, 1.17f)); break;
            case "keyword": material.SetKeyword(new UnityEngine.Rendering.LocalKeyword(material.shader, "UNITY_UI_ALPHACLIP"), !material.IsKeywordEnabled("UNITY_UI_ALPHACLIP")); break;
            case "renderQueue": material.renderQueue += 1; break;
            case "giFlags": material.globalIlluminationFlags = material.globalIlluminationFlags == MaterialGlobalIlluminationFlags.None ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.None; break;
            case "instancing": material.enableInstancing = !material.enableInstancing; break;
            case "doubleSidedGI": material.doubleSidedGI = !material.doubleSidedGI; break;
            default: Assert.Fail("Unknown mutation: " + mutation); break;
        }
    }

    private static double Measure(Action operation, int iterations)
    {
        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < iterations; i++) operation();
        return (Stopwatch.GetTimestamp() - start) * 1e9 / Stopwatch.Frequency / iterations;
    }

    private static double Percentile(double[] values, double percentile)
    {
        var sorted = (double[])values.Clone();
        Array.Sort(sorted);
        return sorted[(int)Math.Ceiling((sorted.Length - 1) * percentile)];
    }

    [Serializable]
    private sealed class OperationReport
    {
        public string unityVersion;
        public int samples;
        public int operationsPerSample;
        public double baselineMedianNs;
        public double baselineP95Ns;
        public double unchangedIdentityCrcMedianNs;
        public double unchangedIdentityCrcP95Ns;
        public double emptyMedianNs;
        public double[] baselineRawNs;
        public double[] unchangedIdentityCrcRawNs;
        public string note;
    }

    private sealed class LakeFixture : IDisposable
    {
        private readonly GameObject root;
        private readonly Component presentation;
        public readonly RectTransform Content;
        public readonly Material Preset;
        public Material RuntimeMaterial => (Material)LakeType.GetField("surfaceMaterial", Fields).GetValue(presentation);
        public int PresetCopyCount => (int)LakeType.GetProperty("DiagnosticSurfacePresetCopyCount").GetValue(presentation);

        public LakeFixture()
        {
            Preset = new Material(LoadPreset());
            root = new GameObject("LakeMaterialSynchronizationFixture", typeof(RectTransform));
            root.SetActive(false);
            var viewport = (RectTransform)root.transform;
            viewport.sizeDelta = new Vector2(320f, 200f);
            Content = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
            Content.SetParent(viewport, false);
            Content.anchoredPosition = new Vector2(7f, -4f);
            var surface = new GameObject("Surface", typeof(RectTransform), ImageType);
            surface.transform.SetParent(viewport, false);
            Component image = surface.GetComponent(ImageType);
            object settings = SettingsType.GetMethod("CreateDefault").Invoke(null, null);
            SettingsType.GetField("surfaceInteractionEnabled").SetValue(settings, false);
            SettingsType.GetField("pointerWakeEnabled").SetValue(settings, false);
            SettingsType.GetField("ambientRippleInterval").SetValue(settings, 0f);
            SettingsType.GetField("ambientRippleIntensity").SetValue(settings, 0f);
            presentation = root.AddComponent(LakeType);
            ((Behaviour)presentation).enabled = false;
            root.SetActive(true);
            LakeType.GetMethod("Initialize").Invoke(presentation, new[] { viewport, Content, image, settings, Preset, (object)true });
        }

        public void Tick() => LakeType.GetMethod("Update", Fields).Invoke(presentation, null);

        public void Dispose()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(Preset);
        }
    }
}
#endif
