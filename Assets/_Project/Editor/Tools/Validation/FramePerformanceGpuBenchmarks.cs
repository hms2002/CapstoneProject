#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Editor-only, isolated shader draw benchmark. Uses GPU timestamp samples from
/// CommandBuffer markers; CPU submission duration is never reported as GPU time.
/// Run serially in Play Mode, outside other benchmarks or interactive gameplay.
/// </summary>
public static class FramePerformanceGpuBenchmarks
{
    [Serializable]
    public sealed class Report
    {
        public string status;
        public string reason;
        public string device;
        public string graphicsApi;
        public string unityVersion;
        public string scope = "Isolated puddle shader GPU command segment; not whole-game frame time.";
        public string measurement = "CustomSampler(collectGpuData=true), CommandBuffer.BeginSample/EndSample, Recorder.gpuElapsedNanoseconds";
        public string delayedResults = "GPU Recorder results are delayed by three frames; each segment has 20 warmup frames.";
        public bool frameTimingFeatureEnabled;
        public bool profilerWasEnabled;
        public bool gpuAreaWasEnabled;
        public int renderSize;
        public int drawCount;
        public int measuredFrames;
        public bool calibrationPassed;
        public string[] availableGpuMarkers;
        public List<Segment> segments = new();
    }

    [Serializable]
    public sealed class Segment
    {
        public string label;
        public string variant;
        public int draws;
        public int attemptedFrames;
        public int positiveGpuFrames;
        public int unexpectedBlockCountFrames;
        public int visiblePixels;
        public double medianGpuMs;
        public double p95GpuMs;
        public long[] gpuNanoseconds;
    }

    private const int WarmupFrames = 20;
    private static int runSequence;

    public static IEnumerator Run(string reportPath, int renderSize = 512, int drawCount = 32, int measuredFrames = 60)
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException("GPU benchmark requires Play Mode so GPU query results advance across frames.");
        renderSize = Mathf.Clamp(renderSize, 128, 1024);
        drawCount = Mathf.Clamp(drawCount, 4, 128);
        measuredFrames = Mathf.Clamp(measuredFrames, 30, 240);
        var report = new Report
        {
            status = "unavailable",
            device = SystemInfo.graphicsDeviceName,
            graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
            unityVersion = Application.unityVersion,
            frameTimingFeatureEnabled = FrameTimingManager.IsFeatureEnabled(),
            profilerWasEnabled = Profiler.enabled,
            gpuAreaWasEnabled = Profiler.GetAreaEnabled(ProfilerArea.GPU),
            renderSize = renderSize,
            drawCount = drawCount,
            measuredFrames = measuredFrames,
            availableGpuMarkers = GetGpuMarkerInventory()
        };
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            report.reason = "The process has no graphics device (GraphicsDeviceType.Null).";
            SaveReport(reportPath, report);
            yield break;
        }

        string fixture = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "SessionLogs",
            "2026-10-07-frame-performance", "puddle-shader-before.txt"));
        if (!File.Exists(fixture))
            throw new FileNotFoundException("The archived original shader is required for the matched comparison.", fixture);
        var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_PuddleShaderVisual.mat");
        if (template == null) throw new InvalidOperationException("Production puddle material is missing.");
        Shader baseline = null;
        Material before = null;
        Material after = null;
        RenderTexture target = null;
        bool previousAsyncCompilation = ShaderUtil.allowAsyncCompilation;
        Vector4 previousTime = Shader.GetGlobalVector("_Time");
        string run = (++runSequence).ToString();
        try
        {
            Profiler.enabled = true;
            Profiler.SetAreaEnabled(ProfilerArea.GPU, true);
            ShaderUtil.allowAsyncCompilation = false;
            string source = File.ReadAllText(fixture).Replace("Shader \"Custom/PuddleShaderVisual\"",
                "Shader \"Hidden/Validation/PuddleGpuBefore\"");
            baseline = ShaderUtil.CreateShaderAsset(source, true);
            before = new Material(template) { shader = baseline };
            after = new Material(template);
            ShaderUtil.CompilePass(before, 0, true);
            ShaderUtil.CompilePass(after, 0, true);
            if (ShaderUtil.ShaderHasError(baseline) || ShaderUtil.ShaderHasError(after.shader))
                throw new InvalidOperationException("Shader compilation failed; GPU comparison is invalid.");
            target = new RenderTexture(renderSize, renderSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            if (!target.Create()) throw new InvalidOperationException("Benchmark render target creation failed.");
            Configure(after, mode: 0f, element: 0f, progress: 0f);

            var low = new Segment { label = "calibration-single", variant = "current", draws = 1 };
            var high = new Segment { label = "calibration-many", variant = "current", draws = drawCount };
            report.segments.Add(low);
            report.segments.Add(high);
            yield return MeasureSegment(after, target, low, measuredFrames, run);
            yield return MeasureSegment(after, target, high, measuredFrames, run);
            report.calibrationPassed = HasReliableSamples(low) && HasReliableSamples(high) &&
                low.visiblePixels > 32 && high.visiblePixels > 32 && high.medianGpuMs > low.medianGpuMs * 1.5;
            if (!report.calibrationPassed)
            {
                report.reason = "GPU timestamp calibration failed: require positive samples in >=90% of frames, one GPU block per frame, nonblank output, and draw-count scaling >1.5x. Zero/unsupported timings are not performance measurements.";
                SaveReport(reportPath, report);
                yield break;
            }

            var states = new[]
            {
                ("ground-alcohol", 0f, 0f, 0f),
                ("ground-fire", 0f, 1f, 0f),
                ("legacy-preparing-control", 2f, 0f, 0.95f),
                ("legacy-projectile-control", 3f, 1f, 0.5f)
            };
            bool reliable = true;
            foreach (var state in states)
            {
                var (label, mode, element, progress) = state;
                Configure(before, mode, element, progress);
                Configure(after, mode, element, progress);
                // ABBA order exposes ordering/thermal drift without treating it as a gameplay benchmark.
                for (int order = 0; order < 4; order++)
                {
                    bool useBefore = order == 0 || order == 3;
                    var segment = new Segment
                    {
                        label = label + "-" + order,
                        variant = useBefore ? "original" : "current",
                        draws = drawCount
                    };
                    report.segments.Add(segment);
                    yield return MeasureSegment(useBefore ? before : after, target, segment, measuredFrames, run);
                    reliable &= HasReliableSamples(segment) && segment.visiblePixels > 32;
                }
            }
            report.status = reliable ? "measured" : "incomplete";
            report.reason = reliable ?
                "Positive GPU timestamp samples and draw-count calibration passed; inspect both ABBA runs and their distributions before claiming improvement." :
                "At least one segment lacks reliable GPU samples or nonblank rendered output; no aggregate improvement claim is valid.";
            SaveReport(reportPath, report);
        }
        finally
        {
            Shader.SetGlobalVector("_Time", previousTime);
            ShaderUtil.allowAsyncCompilation = previousAsyncCompilation;
            Profiler.SetAreaEnabled(ProfilerArea.GPU, report.gpuAreaWasEnabled);
            Profiler.enabled = report.profilerWasEnabled;
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            if (before != null) Object.DestroyImmediate(before);
            if (after != null) Object.DestroyImmediate(after);
            if (baseline != null) Object.DestroyImmediate(baseline);
        }
    }

    private static IEnumerator MeasureSegment(Material material, RenderTexture target, Segment result, int frames, string run)
    {
        string markerName = "Capstone.PuddleGpu." + run + "." + result.label;
        CustomSampler sampler = CustomSampler.Create(markerName, true);
        Recorder recorder = sampler.GetRecorder();
        var samples = new List<long>(frames);
        var commands = new CommandBuffer { name = markerName };
        try
        {
            if (!recorder.isValid) yield break;
            recorder.enabled = true;
            commands.SetRenderTarget(target);
            commands.ClearRenderTarget(false, true, Color.clear);
            commands.SetGlobalVector("_Time", new Vector4(0.3125f, 6.25f, 12.5f, 18.75f));
            commands.BeginSample(sampler);
            Texture mask = material.GetTexture("_MainTex") ?? Texture2D.whiteTexture;
            for (int draw = 0; draw < result.draws; draw++)
                commands.Blit(mask, target, material, 0);
            commands.EndSample(sampler);
            for (int frame = 0; frame < WarmupFrames + frames; frame++)
            {
                RenderTexture previousTarget = RenderTexture.active;
                try { Graphics.ExecuteCommandBuffer(commands); }
                finally { RenderTexture.active = previousTarget; }
                yield return null;
                if (frame < WarmupFrames) continue;
                result.attemptedFrames++;
                int blocks = recorder.gpuSampleBlockCount;
                long nanoseconds = recorder.gpuElapsedNanoseconds;
                if (blocks != 1) result.unexpectedBlockCountFrames++;
                if (blocks == 1 && nanoseconds > 0) samples.Add(nanoseconds);
            }
            result.visiblePixels = CountVisiblePixels(target);
            result.positiveGpuFrames = samples.Count;
            result.gpuNanoseconds = samples.ToArray();
            if (samples.Count > 0)
            {
                samples.Sort();
                result.medianGpuMs = samples[samples.Count / 2] / 1000000.0;
                result.p95GpuMs = samples[Mathf.Min(samples.Count - 1, Mathf.CeilToInt(samples.Count * 0.95f) - 1)] / 1000000.0;
            }
            Debug.Log($"PUDDLEGPU,{result.label},variant={result.variant},draws={result.draws},positive={result.positiveGpuFrames}/{result.attemptedFrames},unexpectedBlocks={result.unexpectedBlockCountFrames},medianGpuMs={result.medianGpuMs:F6},p95GpuMs={result.p95GpuMs:F6},visiblePixels={result.visiblePixels}");
        }
        finally
        {
            recorder.enabled = false;
            commands.Release();
        }
    }

    private static bool HasReliableSamples(Segment segment) => segment.attemptedFrames > 0 &&
        segment.positiveGpuFrames >= Mathf.CeilToInt(segment.attemptedFrames * 0.9f) && segment.unexpectedBlockCountFrames == 0;

    private static void Configure(Material material, float mode, float element, float progress)
    {
        material.SetFloat("_Mode", mode);
        material.SetFloat("_ElementType", element);
        material.SetFloat("_IgnitionProgress", element);
        material.SetFloat("_AbsorbProgress", progress);
        material.SetFloat("_TimeOffset", 2.75f);
        material.SetVector("_AbsorbDirection", new Vector4(0.6f, 0.8f, 0f, 0f));
    }

    private static int CountVisiblePixels(RenderTexture target)
    {
        var readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
        RenderTexture previous = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            readback.Apply();
            int visible = 0;
            foreach (Color32 pixel in readback.GetPixels32()) if (pixel.a > 16) visible++;
            return visible;
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(readback); }
    }

    private static string[] GetGpuMarkerInventory()
    {
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        var names = new List<string>();
        foreach (ProfilerRecorderHandle handle in handles)
        {
            ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(handle);
            if (description.Name.IndexOf("GPU", StringComparison.OrdinalIgnoreCase) >= 0)
                names.Add($"{description.Name};category={description.Category};unit={description.UnitType};flags={description.Flags}");
        }
        return names.ToArray();
    }

    private static void SaveReport(string path, Report report)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllText(fullPath, JsonUtility.ToJson(report, true));
        Debug.Log($"PUDDLEGPU_REPORT,status={report.status},reason={report.reason},path={fullPath}");
    }
}
#endif
