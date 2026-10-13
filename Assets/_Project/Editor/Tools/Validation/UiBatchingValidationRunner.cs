#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Profiling;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Captures an already prepared, authored UI fixture. Native UI Profiler batches
/// are measured independently of graphic/material inventories. No scene is saved,
/// no material is cloned, and reflection/pixel readback run outside timing windows.
/// </summary>
[InitializeOnLoad]
public static class UiBatchingValidationRunner
{
    const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const string Pending = "Capstone.UiBatchValidation.Pending";
    static readonly Stack<IEnumerator> routines = new();
    static bool started;
    static int lastFrame = -1;
    static double deadline;

    [Serializable] public sealed class Report
    {
        public string label, variant, locale, fixture, status, reason, device, api, unity;
        public string scope = "Authored UI in Editor Play Mode. Native UI batches are filtered to the fixture; Canvas.BuildBatch is aggregate CPU time across all active canvases, not fixture-only CPU time, GPU time or whole-game FPS.";
        public int frameIndex = -1, activeGraphics, totalGraphics, activeCanvases, heartTokens;
        public int nativeSelectedBatches = -1, nativeMixedBatches = -1;
        public bool nativeUiDataAvailable, nativeMembersAvailable, timingAvailable, timingCanaryPositive;
        public bool profilerRecordingEnabled, profilerFrameAdvanced;
        public int profilerFirstFrame = -1, profilerLastFrame = -1, profilerConnectionId;
        public string profilerConnection;
        public string timingUnit, timingReason, screenshotReason;
        public double medianBuildBatchMs = -1, p95BuildBatchMs = -1;
        public long[] buildBatchNanoseconds;
        public int pixelWidth, pixelHeight, screenshotDifferentPixels;
        public bool screenshotNonblank;
        public string screenshotPath;
        public List<GraphicInfo> graphics = new();
        public List<CanvasInfo> canvases = new();
        public List<BatchInfo> batches = new();
        public List<string> counterInventory = new();
    }
    [Serializable] public sealed class GraphicInfo
    {
        public string path, type, canvas, textureName, textureAsset, assignedMaterial, submittedMaterial, shader, font, fontAsset;
        public int instanceId, textureId, textureWidth, textureHeight, assignedMaterialId, submittedMaterialId;
        public int materialSlots, vertexCount = -1, depth;
        public bool active, culled, maskable, rectMask, stencilMask;
        public List<int> fontAtlasIds = new();
    }
    [Serializable] public sealed class CanvasInfo
    {
        public string path, name;
        public int instanceId, directBatchCount, totalBatchCount, vertexCount, renderDataIndex, renderDataCount;
    }
    [Serializable] public sealed class BatchInfo
    {
        public string name, breakingReason;
        public int vertexCount, renderDataIndex, renderDataCount, selectedMembers, totalMembers;
        public bool mixedWithOtherFixture;
        public List<string> graphicPaths = new();
    }
    [Serializable] public sealed class PixelComparison
    {
        public string status, reason, before, after;
        public int width, height, maximumChannelDelta, pixelsAboveTolerance, channelTolerance;
    }

    static UiBatchingValidationRunner() => EditorApplication.update += Update;

    /// <summary>
    /// CLI: -uiBatchScene, -uiBatchSetupMethod Type.Method (optional), -uiBatchRoot
    /// hierarchy/name, -uiBatchOutput, -uiBatchVariant, -uiBatchLocales ko,en,ja,zh-Hans,zh-Hant.
    /// The root-owned setup method prepares the real screen; it may return IEnumerator.
    /// </summary>
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before the CLI runner.");
        if (!Application.isBatchMode && UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("The open scene is dirty; save or discard it before running validation.");
        EditorSceneManager.OpenScene(Arg("-uiBatchScene", "Assets/_Project/Scenes/Tests/TestScene.unity"), OpenSceneMode.Single);
        started = false; lastFrame = -1; routines.Clear();
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    static void Update()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (!started) { started = true; deadline = EditorApplication.timeSinceStartup + 900; routines.Push(Suite()); }
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("UI batching validation timed out.");
            while (routines.Count > 0)
            {
                IEnumerator iterator = routines.Peek();
                if (!iterator.MoveNext()) { (iterator as IDisposable)?.Dispose(); routines.Pop(); continue; }
                if (iterator.Current is IEnumerator nested) { routines.Push(nested); continue; }
                return;
            }
            FinishRunner(null);
        }
        catch (Exception exception) { FinishRunner(exception.ToString()); }
    }

    static IEnumerator Suite()
    {
        for (int frame = 0; frame < 30; frame++) yield return null;
        string setup = Arg("-uiBatchSetupMethod", "");
        if (!string.IsNullOrEmpty(setup))
        {
            int split = setup.LastIndexOf('.');
            Type type = FindType(setup.Substring(0, split));
            MethodInfo method = type?.GetMethod(setup.Substring(split + 1), Members);
            if (method == null) throw new MissingMethodException(setup);
            if (method.Invoke(null, null) is IEnumerator coroutine) yield return coroutine;
        }
        string wanted = Arg("-uiBatchRoot", "GlobalUIRoot");
        GameObject root = null;
        foreach (Transform transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (transform.name == wanted || Hierarchy(transform) == wanted) { root = transform.gameObject; break; }
        if (root == null || !root.activeInHierarchy) throw new InvalidOperationException("Prepared authored UI root is missing/inactive: " + wanted);
        string output = Path.GetFullPath(Arg("-uiBatchOutput", "Library/UiBatchValidation"));
        foreach (string locale in Arg("-uiBatchLocales", "ko,en,ja,zh-Hans,zh-Hant").Split(','))
            yield return Capture(root, Path.Combine(output, locale + ".json"), Arg("-uiBatchLabel", wanted),
                Arg("-uiBatchVariant", "unknown"), locale.Trim(), 30, Arg("-uiBatchForceRebuild", "false") == "true");
    }

    /// <summary>Call this from a root-owned scene/scenario coroutine after opening the actual screen.</summary>
    public static IEnumerator Capture(GameObject fixture, string outputPath, string label, string variant,
        string localeCode = null, int frames = 30, bool forceRebuild = false)
    {
        if (!Application.isPlaying || fixture == null || !fixture.activeInHierarchy)
            throw new InvalidOperationException("Capture requires an active authored fixture in Play Mode.");
        var report = new Report { label = label, variant = variant, fixture = Hierarchy(fixture.transform),
            device = SystemInfo.graphicsDeviceName, api = SystemInfo.graphicsDeviceType.ToString(), unity = Application.unityVersion, status = "unavailable" };
        bool previousProfiler = Profiler.enabled, previousEditor = ProfilerDriver.profileEditor;
        bool previousUi = Profiler.GetAreaEnabled(ProfilerArea.UI), previousDetails = Profiler.GetAreaEnabled(ProfilerArea.UIDetails);
        bool previousRecording = ProfilerDriver.enabled;
        bool previousDriverUi = ProfilerDriver.IsAreaEnabled(ProfilerArea.UI), previousDriverDetails = ProfilerDriver.IsAreaEnabled(ProfilerArea.UIDetails);
        int initialProfilerFrame = ProfilerDriver.lastFrameIndex;
        var previousLocale = LocalizationSettings.SelectedLocale;
        ProfilerRecorder timing = default;
        try
        {
            if (!string.IsNullOrEmpty(localeCode))
            {
                while (!LocalizationSettings.InitializationOperation.IsDone) yield return null;
                var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
                if (locale == null) throw new InvalidOperationException("Requested locale is unavailable: " + localeCode);
                LocalizationSettings.SelectedLocale = locale;
            }
            report.locale = LocalizationSettings.SelectedLocale?.Identifier.Code ?? "unavailable";
            // Runtime recorders do not enable the Editor's native frame-history session.
            // Use the same recording switch/areas as the Profiler window, without opening it.
            ProfilerDriver.profileEditor = false;
            ProfilerDriver.SetAreaEnabled(ProfilerArea.UI, true); ProfilerDriver.SetAreaEnabled(ProfilerArea.UIDetails, true);
            ProfilerDriver.enabled = true; Profiler.enabled = true;
            Profiler.SetAreaEnabled(ProfilerArea.UI, true); Profiler.SetAreaEnabled(ProfilerArea.UIDetails, true);
            for (int warm = 0; warm < 30; warm++) yield return null;
            report.heartTokens = Object.FindObjectsByType<HeartTokenUI>(FindObjectsInactive.Include).Length;
            if (report.heartTokens > 128) throw new InvalidOperationException("Invalid UI workload: abnormal heart HUD count.");
            Graphic[] graphics = fixture.GetComponentsInChildren<Graphic>(true);
            bool activeFound = false;
            foreach (Graphic graphic in graphics) if (graphic.isActiveAndEnabled) { activeFound = true; break; }
            if (!activeFound) throw new InvalidOperationException("Fixture has no active graphics; a blank UI cannot verify batching.");
            timing = StartTimingRecorder(report);
            if (timing.Valid)
            {
                foreach (Graphic graphic in graphics) if (graphic.isActiveAndEnabled) graphic.SetVerticesDirty();
                for (int canary = 0; canary < 4; canary++)
                { yield return null; report.timingCanaryPositive |= timing.LastValue > 0; }
            }
            var times = new List<long>(Mathf.Max(1, frames));
            for (int frame = 0; frame < Mathf.Max(1, frames); frame++)
            {
                if (forceRebuild) foreach (Graphic graphic in graphics) if (graphic.isActiveAndEnabled) graphic.SetVerticesDirty();
                yield return null;
                if (timing.Valid && report.timingCanaryPositive) times.Add(timing.LastValue);
            }
            // Reflection, inventory and native UI rendering happen after timing collection.
            report.profilerRecordingEnabled = ProfilerDriver.enabled;
            report.profilerFirstFrame = ProfilerDriver.firstFrameIndex; report.profilerLastFrame = ProfilerDriver.lastFrameIndex;
            report.profilerFrameAdvanced = report.profilerLastFrame > initialProfilerFrame;
            report.profilerConnectionId = ProfilerDriver.connectedProfiler;
            report.profilerConnection = ProfilerDriver.GetConnectionIdentifier(report.profilerConnectionId);
            report.timingAvailable = timing.Valid && report.timingCanaryPositive;
            report.timingReason = report.timingAvailable ?
                (forceRebuild ? "Aggregate Canvas.BuildBatch CPU samples across all active canvases, with fixture graphic dirtiness forced; marking cost is outside the marker." : "Aggregate Canvas.BuildBatch CPU samples across all active canvases; zero is valid after a positive rebuild canary. These samples do not isolate fixture CPU cost.") :
                "Canvas.BuildBatch nanosecond recorder missing or rebuild canary produced no positive samples.";
            report.buildBatchNanoseconds = times.ToArray();
            if (times.Count > 0) { times.Sort(); report.medianBuildBatchMs = times[times.Count / 2] * 1e-6;
                report.p95BuildBatchMs = times[Mathf.Min(times.Count - 1, Mathf.CeilToInt(times.Count * .95f) - 1)] * 1e-6; }
            SnapshotGraphics(fixture, graphics, report);
            CaptureNativeUi(fixture, report, outputPath);
            report.status = report.nativeUiDataAvailable ? "captured" : "unavailable";
            Save(outputPath, report);
        }
        finally
        {
            timing.Dispose();
            if (previousLocale != null) LocalizationSettings.SelectedLocale = previousLocale;
            ProfilerDriver.SetAreaEnabled(ProfilerArea.UI, previousDriverUi); ProfilerDriver.SetAreaEnabled(ProfilerArea.UIDetails, previousDriverDetails);
            ProfilerDriver.profileEditor = previousEditor; ProfilerDriver.enabled = previousRecording;
            Profiler.SetAreaEnabled(ProfilerArea.UI, previousUi); Profiler.SetAreaEnabled(ProfilerArea.UIDetails, previousDetails);
            Profiler.enabled = previousProfiler;
        }
    }

    static ProfilerRecorder StartTimingRecorder(Report report)
    {
        var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
        ProfilerRecorder result = default;
        foreach (var handle in handles)
        {
            var description = ProfilerRecorderHandle.GetDescription(handle);
            if (description.Category == ProfilerCategory.Gui || description.Name.IndexOf("Canvas", StringComparison.OrdinalIgnoreCase) >= 0)
                report.counterInventory.Add($"{description.Name};category={description.Category.Name};unit={description.UnitType};flags={description.Flags}");
            if (description.Name == "Canvas.BuildBatch" && description.UnitType.ToString() == "TimeNanoseconds")
            { report.timingUnit = description.UnitType.ToString(); result = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately); }
        }
        if (!result.Valid) report.timingUnit = "unavailable";
        return result;
    }

    static void SnapshotGraphics(GameObject fixture, Graphic[] graphics, Report report)
    {
        report.totalGraphics = graphics.Length;
        var activeCanvases = new HashSet<Canvas>();
        foreach (Graphic graphic in graphics)
        {
            CanvasRenderer renderer = graphic.canvasRenderer;
            Texture texture = graphic.mainTexture;
            Material assigned = graphic.material;
            Material submitted = renderer.materialCount > 0 ? renderer.GetMaterial(0) : null;
            var item = new GraphicInfo { path = Hierarchy(graphic.transform), type = graphic.GetType().FullName,
                instanceId = graphic.GetInstanceID(), active = graphic.isActiveAndEnabled, culled = renderer.cull,
                canvas = graphic.canvas != null ? Hierarchy(graphic.canvas.transform) : "none", depth = graphic.depth,
                textureId = texture != null ? texture.GetInstanceID() : 0, textureName = texture != null ? texture.name : "none",
                textureAsset = texture != null ? AssetDatabase.GetAssetPath(texture) : "", textureWidth = texture != null ? texture.width : 0,
                textureHeight = texture != null ? texture.height : 0, assignedMaterialId = assigned != null ? assigned.GetInstanceID() : 0,
                assignedMaterial = assigned != null ? assigned.name : "none", submittedMaterialId = submitted != null ? submitted.GetInstanceID() : 0,
                submittedMaterial = submitted != null ? submitted.name : "none", shader = submitted != null ? submitted.shader.name : "none",
                materialSlots = renderer.materialCount, rectMask = graphic.GetComponentInParent<RectMask2D>() != null,
                stencilMask = graphic.GetComponentInParent<Mask>() != null, maskable = graphic is MaskableGraphic maskable && maskable.maskable };
            if (graphic.isActiveAndEnabled) { report.activeGraphics++; if (graphic.canvas != null) activeCanvases.Add(graphic.canvas); }
            if (graphic is TMP_Text text && text.font != null)
            { item.font = text.font.name; item.fontAsset = AssetDatabase.GetAssetPath(text.font);
                foreach (Texture2D atlas in text.font.atlasTextures) if (atlas != null) item.fontAtlasIds.Add(atlas.GetInstanceID()); }
            report.graphics.Add(item);
        }
        report.activeCanvases = activeCanvases.Count;
    }

    static void CaptureNativeUi(GameObject fixture, Report report, string outputPath)
    {
        Type propertyType = FindType("UnityEditorInternal.ProfilerProperty");
        if (propertyType == null) { report.reason = "Native UI Profiler API is absent."; return; }
        object property = Activator.CreateInstance(propertyType);
        try
        {
            if (!report.profilerFrameAdvanced) { report.reason = "The Editor Profiler frame history did not advance during this fixture; native UI batches are unavailable."; return; }
            report.frameIndex = ProfilerDriver.lastFrameIndex - 1;
            if (report.frameIndex < 0) { report.reason = "No completed Profiler frame."; return; }
            propertyType.GetMethod("SetRoot", Members).Invoke(property, new object[] { report.frameIndex, 0, 0 });
            Array infos = propertyType.GetMethod("GetUISystemProfilerInfo", Members)?.Invoke(property, null) as Array;
            if (infos == null || infos.Length == 0) { report.reason = "Recorded frame contains no native UI details; enable UI Details and retry."; return; }
            MethodInfo getMembers = propertyType.GetMethod("GetUISystemBatchEntityIds", Members) ?? propertyType.GetMethod("GetUISystemBatchInstanceIDs", Members);
            Array memberIds = getMembers?.Invoke(property, null) as Array;
            report.nativeMembersAvailable = memberIds != null;
            report.nativeSelectedBatches = 0; report.nativeMixedBatches = 0;
            CanvasInfo preview = null;
            foreach (object info in infos)
            {
                bool isBatch = ReadBool(info, "isBatch");
                Object owner = ResolveObject(Read(info, "objectEntityId", "objectInstanceId"));
                string name = propertyType.GetMethod("GetUISystemProfilerNameByOffset", Members)?.Invoke(property,
                    new object[] { ReadInt(info, "objectNameOffset") }) as string ?? "unknown";
                if (!isBatch)
                {
                    GameObject ownerGo = owner as GameObject ?? (owner as Component)?.gameObject;
                    bool related = ownerGo != null && (ownerGo.transform.IsChildOf(fixture.transform) || fixture.transform.IsChildOf(ownerGo.transform));
                    if (!related) continue;
                    var canvas = new CanvasInfo { path = ownerGo != null ? Hierarchy(ownerGo.transform) : name, name = name,
                        instanceId = owner != null ? owner.GetInstanceID() : 0, directBatchCount = ReadInt(info, "batchCount"),
                        totalBatchCount = ReadInt(info, "totalBatchCount"), vertexCount = ReadInt(info, "totalVertexCount"),
                        renderDataIndex = ReadInt(info, "renderDataIndex"), renderDataCount = ReadInt(info, "renderDataCount") };
                    report.canvases.Add(canvas);
                    if (preview == null || canvas.renderDataCount > preview.renderDataCount) preview = canvas;
                    continue;
                }
                int index = ReadInt(info, "entityIdsIndex", "instanceIDsIndex"), count = ReadInt(info, "entityIdsCount", "instanceIDsCount");
                var batch = new BatchInfo { name = name, breakingReason = Read(info, "batchBreakingReason")?.ToString() ?? "unknown",
                    vertexCount = ReadInt(info, "vertexCount"), renderDataIndex = ReadInt(info, "renderDataIndex"),
                    renderDataCount = ReadInt(info, "renderDataCount"), totalMembers = count };
                if (memberIds != null) for (int at = index; at >= 0 && at < index + count && at < memberIds.Length; at++)
                {
                    Object member = ResolveObject(memberIds.GetValue(at));
                    GameObject go = member as GameObject ?? (member as Component)?.gameObject;
                    if (go != null && go.transform.IsChildOf(fixture.transform))
                    { batch.selectedMembers++; batch.graphicPaths.Add(Hierarchy(go.transform)); }
                }
                if (batch.selectedMembers == 0) continue;
                batch.mixedWithOtherFixture = batch.selectedMembers != batch.totalMembers;
                report.nativeSelectedBatches++; if (batch.mixedWithOtherFixture) report.nativeMixedBatches++;
                report.batches.Add(batch);
            }
            report.nativeUiDataAvailable = report.canvases.Count > 0 && report.nativeSelectedBatches > 0;
            report.reason = report.nativeUiDataAvailable ? "Native batch records captured. Mixed batches are marked; instance IDs are meaningful only within this capture." :
                "UI details exist, but no native batch membership matched the prepared fixture; no draw-count claim is valid.";
            if (preview != null && preview.renderDataCount > 0) CaptureProfilerPixels(propertyType, preview, report, outputPath);
        }
        catch (Exception exception) { report.reason = "Native UI details unavailable: " + exception.GetBaseException().Message; }
        finally { (property as IDisposable)?.Dispose(); }
    }

    static void CaptureProfilerPixels(Type propertyType, CanvasInfo preview, Report report, string outputPath)
    {
        Texture2D texture = null;
        try
        {
            texture = propertyType.GetMethod("UISystemProfilerRender", Members, null,
                new[] { typeof(int), typeof(int), typeof(int), typeof(bool) }, null)?.Invoke(null,
                new object[] { report.frameIndex, preview.renderDataIndex, preview.renderDataCount, false }) as Texture2D;
            if (texture == null) { report.screenshotReason = "Native UI render returned no texture."; return; }
            Color32[] pixels = texture.GetPixels32();
            foreach (Color32 pixel in pixels) if (pixel.a > 16 && (pixel.r > 8 || pixel.g > 8 || pixel.b > 8)) report.screenshotDifferentPixels++;
            report.screenshotNonblank = report.screenshotDifferentPixels > 32;
            report.pixelWidth = texture.width; report.pixelHeight = texture.height;
            report.screenshotPath = Path.ChangeExtension(Path.GetFullPath(outputPath), ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(report.screenshotPath));
            File.WriteAllBytes(report.screenshotPath, texture.EncodeToPNG());
            report.screenshotReason = "Native recorded-canvas rendering; compare the same canvas/viewport and static state across variants.";
        }
        catch (Exception exception) { report.screenshotReason = "UI pixel rendering unavailable: " + exception.GetBaseException().Message; }
        finally { if (texture != null) propertyType.GetMethod("ReleaseUISystemProfilerRender", Members)?.Invoke(null, new object[] { texture }); }
    }

    public static PixelComparison ComparePixels(string before, string after, int channelTolerance = 1)
    {
        var result = new PixelComparison { before = before, after = after, channelTolerance = channelTolerance, status = "unavailable" };
        var a = new Texture2D(2, 2, TextureFormat.RGBA32, false); var b = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!a.LoadImage(File.ReadAllBytes(before)) || !b.LoadImage(File.ReadAllBytes(after))) throw new InvalidOperationException("PNG decode failed.");
            if (a.width != b.width || a.height != b.height) { result.reason = "Viewport dimensions differ."; return result; }
            result.width = a.width; result.height = a.height;
            Color32[] left = a.GetPixels32(), right = b.GetPixels32();
            for (int i = 0; i < left.Length; i++)
            {
                int difference = Math.Max(Math.Max(Math.Abs(left[i].r - right[i].r), Math.Abs(left[i].g - right[i].g)),
                    Math.Max(Math.Abs(left[i].b - right[i].b), Math.Abs(left[i].a - right[i].a)));
                result.maximumChannelDelta = Math.Max(result.maximumChannelDelta, difference);
                if (difference > channelTolerance) result.pixelsAboveTolerance++;
            }
            result.status = result.pixelsAboveTolerance == 0 ? "equal" : "different";
            return result;
        }
        finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
    }

    static object Read(object target, params string[] names)
    { foreach (string name in names) { var field = target.GetType().GetField(name, Members); if (field != null) return field.GetValue(target);
        var property = target.GetType().GetProperty(name, Members); if (property != null) return property.GetValue(target); } return null; }
    static int ReadInt(object target, params string[] names) => Convert.ToInt32(Read(target, names) ?? 0);
    static bool ReadBool(object target, string name) => Convert.ToBoolean(Read(target, name) ?? false);
    static Object ResolveObject(object id)
    {
        if (id == null) return null;
        if (id is int integer) return EditorUtility.InstanceIDToObject(integer);
        MethodInfo method = typeof(EditorUtility).GetMethod("EntityIdToObject", Members, null, new[] { id.GetType() }, null);
        return method?.Invoke(null, new[] { id }) as Object;
    }
    static string Hierarchy(Transform transform)
    { string path = transform.name; while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; } return path; }
    static Type FindType(string name) { foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) { Type type = assembly.GetType(name); if (type != null) return type; } return null; }
    static string Arg(string name, string fallback) { string[] args = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1]; return fallback; }
    static void Save(string path, Report report) { string full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)); File.WriteAllText(full, JsonUtility.ToJson(report, true)); Debug.Log($"UIBATCH_CAPTURE,{report.label},{report.variant},{report.locale},status={report.status},nativeBatches={report.nativeSelectedBatches},activeGraphics={report.activeGraphics},path={full}"); }
    static void FinishRunner(string error)
    {
        while (routines.Count > 0) (routines.Pop() as IDisposable)?.Dispose();
        SessionState.SetBool(Pending, false); EditorApplication.isPlaying = false;
        if (error != null) Debug.LogError(error);
        if (Application.isBatchMode || Arg("-uiBatchExit", "false") == "true") EditorApplication.Exit(error == null ? 0 : 1);
    }
}
#endif
