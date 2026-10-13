#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Editor-only actual-render draw evidence for an already prepared world-HUD scenario.</summary>
public static class MonsterHealthBarGpuValidation
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Serializable]
    public sealed class Report
    {
        public string variant, label, unity, device, status = "unavailable", reason;
        public int requestedActors, requestedStatusActors;
        public bool isBatchMode, gameViewOpened, standardRequestSupported, pixelsValid;
        public bool frameDebuggerLocallySupported, frameDebuggerScopeApiAvailable, frameDebuggerDataAvailable;
        public bool unityStatsDrawsAvailable, unityStatsCameraCalibrationPassed;
        public int disabledCameraMedianDraws = -1, enabledCameraMedianDraws = -1;
        public int nativeTotalDraws = -1, nativeHpDraws = -1, nativeHpInstances;
        public int nativeRecordedDraws = -1; // Recognized FrameDebugger events; not a complete frame total.
        public int nativeInstancedHpEvents, nativeFrameEventCount, finalDrawCallCount;
        public int pixelWidth, pixelHeight, nonzeroPixels;
        public int[] disabledCameraDrawSamples, enabledCameraDrawSamples;
        public List<Event> events = new();
        public List<string> apiDiagnostics = new();
        public string scope = "Native UnityStats or FrameDebugger draw records after actual rendering; no renderer/component-count inference. UnityStats is the completed GameView frame, including all rendered cameras/UI. Offscreen StandardRequest pixels alone do not prove draw counts.";
    }

    [Serializable]
    public sealed class Event
    {
        public int index, drawCalls, instances, vertices, indices;
        public string type, name, shader, pass, lightMode, renderTarget, objectName;
        public bool hpShader;
    }

    /// <summary>Same live steady actors, temporarily project the existing legacy Images, then restore mesh projection.</summary>
    public static IEnumerator CapturePair(Camera camera, IEnumerable<Component> monsterViews, string variant,
        string label, string outputDirectory, Action<Report, Report> completed = null, bool showGameView = true)
    {
        var states = new List<ProjectionState>();
        if (monsterViews != null) foreach (Component view in monsterViews)
        {
            if (view == null) throw new ArgumentException("A prepared actor's HUD view is missing.");
            var state = new ProjectionState { View = view };
            state.MeshField = view.GetType().GetField("meshHealthBar", Instance);
            if (state.MeshField == null) throw new InvalidOperationException("Current authored mesh projection field is missing.");
            state.Mesh = state.MeshField.GetValue(view) as Component;
            state.Canvas = view.GetComponent<Canvas>();
            state.CanvasEnabled = state.Canvas != null && state.Canvas.enabled;
            state.Renderer = state.Mesh != null ? state.Mesh.GetComponent<MeshRenderer>() : null;
            state.RendererEnabled = state.Renderer != null && state.Renderer.enabled;
            var bar = view.GetType().GetField("healthBar", Instance)?.GetValue(view) as RectTransform;
            if (bar == null || state.Mesh == null || state.Renderer == null)
                throw new InvalidOperationException("CapturePair requires the already-authored current HP renderer and original Image hierarchy.");
            state.Images = bar.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            state.ImageEnabled = new bool[state.Images.Length];
            for (int i = 0; i < state.Images.Length; i++) state.ImageEnabled[i] = state.Images[i].enabled;
            states.Add(state);
        }
        Report legacy = null, mesh = null;
        try
        {
            foreach (ProjectionState state in states)
            {
                state.MeshField.SetValue(state.View, null);
                state.Renderer.enabled = false;
                foreach (var image in state.Images) image.enabled = true;
                state.Canvas.enabled = true;
            }
            for (int i = 0; i < 10; i++) yield return null;
            yield return Capture(camera, states.Count, 0, variant, label + "-legacy-images",
                Path.Combine(outputDirectory, label + "-legacy-images.json"), result => legacy = result, showGameView);
            RestoreProjection(states);
            for (int i = 0; i < 10; i++) yield return null;
            yield return Capture(camera, states.Count, 0, variant, label + "-instanced-mesh",
                Path.Combine(outputDirectory, label + "-instanced-mesh.json"), result => mesh = result, showGameView);
        }
        finally { RestoreProjection(states); }
        completed?.Invoke(legacy, mesh);
    }

    private sealed class ProjectionState
    {
        public Component View, Mesh;
        public FieldInfo MeshField;
        public MeshRenderer Renderer;
        public Canvas Canvas;
        public bool CanvasEnabled, RendererEnabled;
        public UnityEngine.UI.Image[] Images;
        public bool[] ImageEnabled;
    }

    private static void RestoreProjection(List<ProjectionState> states)
    {
        foreach (ProjectionState state in states)
        {
            if (state.View == null) continue;
            state.MeshField.SetValue(state.View, state.Mesh);
            if (state.Renderer != null) state.Renderer.enabled = state.RendererEnabled;
            if (state.Canvas != null) state.Canvas.enabled = state.CanvasEnabled;
            for (int i = 0; i < state.Images.Length; i++) if (state.Images[i] != null) state.Images[i].enabled = state.ImageEnabled[i];
        }
    }

    /// <summary>Call inside the root's existing scenario coroutine after preparing its real actors and workload.</summary>
    public static IEnumerator Capture(Camera camera, int actors, int statusActors, string variant, string label,
        string outputPath, Action<Report> completed = null, bool showGameView = true)
    {
        var report = new Report { variant = variant, label = label, requestedActors = actors, requestedStatusActors = statusActors,
            unity = Application.unityVersion, device = SystemInfo.graphicsDeviceName, isBatchMode = Application.isBatchMode };
        if (!EditorApplication.isPlaying || camera == null || !camera.gameObject.activeInHierarchy)
        {
            report.reason = "Requires the prepared Play Mode world and its actual active camera.";
            Finish(report, outputPath, completed); yield break;
        }

        Type editor = typeof(EditorWindow);
        Type stats = editor.Assembly.GetType("UnityEditor.UnityStats") ?? editor.Assembly.GetType("UnityEditorInternal.UnityStats");
        Type fd = editor.Assembly.GetType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility") ??
            editor.Assembly.GetType("UnityEditorInternal.FrameDebuggerUtility");
        Type eventData = editor.Assembly.GetType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerEventData");
        MethodInfo enter = fd?.GetMethod("EnterCapturingScope", Static);
        MethodInfo leave = fd?.GetMethod("LeaveCapturingScope", Static);
        report.frameDebuggerScopeApiAvailable = enter != null && leave != null;
        report.frameDebuggerLocallySupported = Bool(fd, "locallySupported");
        bool oldFdEnabled = FrameDebugger.enabled;
        int oldRemote = Int(fd, "GetRemotePlayerGUID", 0);
        int oldLimit = Int(fd, "limit", -1);
        bool oldCameraEnabled = camera.enabled;
        RenderTexture oldTarget = camera.targetTexture;
        EditorWindow gameView = null;
        var target = new RenderTexture(Mathf.Max(64, camera.pixelWidth), Mathf.Max(64, camera.pixelHeight), 24,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { name = "MonsterHealthBarGpuValidation_StandardRequest" };
        try
        {
            target.Create();
            camera.targetTexture = target;
            var request = new RenderPipeline.StandardRequest { destination = target };
            report.standardRequestSupported = GraphicsSettings.currentRenderPipeline == null || RenderPipeline.SupportsRenderRequest(camera, request);
            if (report.standardRequestSupported)
            {
                Canvas.ForceUpdateCanvases();
                if (report.frameDebuggerScopeApiAvailable && !oldFdEnabled && report.frameDebuggerLocallySupported)
                {
                    fd.GetMethod("SetEnabled", Static).Invoke(null, new object[] { true, 0 });
                    enter.Invoke(null, null);
                }
                try
                {
                    if (GraphicsSettings.currentRenderPipeline != null) RenderPipeline.SubmitRenderRequest(camera, request);
                    else camera.Render();
                }
                finally { if (report.frameDebuggerScopeApiAvailable && !oldFdEnabled && report.frameDebuggerLocallySupported) leave.Invoke(null, null); }
                ReadPixels(target, report);
                if (report.frameDebuggerScopeApiAvailable) ReadFrameDebugger(fd, eventData, report);
                if (!oldFdEnabled && FrameDebugger.enabled) fd.GetMethod("SetEnabled", Static).Invoke(null, new object[] { false, 0 });
            }
            camera.targetTexture = oldTarget;

            // Unity 6000.4 lacks the newer offscreen capture scope. A real GameView render is required there.
            if (showGameView)
            {
                Type gameViewType = editor.Assembly.GetType("UnityEditor.GameView");
                if (gameViewType != null)
                {
                    gameView = EditorWindow.GetWindow(gameViewType);
                    gameView.Show(); gameView.Focus(); gameView.Repaint(); report.gameViewOpened = true;
                }
            }
            for (int i = 0; i < 4; i++) { RequestFrame(gameView); yield return null; }
            var disabled = new List<int>();
            camera.enabled = false;
            for (int i = 0; i < 8; i++)
            {
                RequestFrame(gameView); yield return null;
                if (i >= 3) disabled.Add(Int(stats, "drawCalls", -1));
            }
            camera.enabled = true;
            var enabled = new List<int>();
            for (int i = 0; i < 12; i++)
            {
                RequestFrame(gameView); yield return null;
                if (i >= 3) enabled.Add(Int(stats, "drawCalls", -1));
            }
            report.disabledCameraDrawSamples = disabled.ToArray(); report.enabledCameraDrawSamples = enabled.ToArray();
            report.disabledCameraMedianDraws = Median(disabled); report.enabledCameraMedianDraws = Median(enabled);
            report.unityStatsDrawsAvailable = enabled.Count > 0 && report.enabledCameraMedianDraws > 0;
            report.unityStatsCameraCalibrationPassed = report.unityStatsDrawsAvailable && report.disabledCameraMedianDraws >= 0 &&
                report.enabledCameraMedianDraws > report.disabledCameraMedianDraws;

            if (!oldFdEnabled && report.frameDebuggerLocallySupported && fd != null)
            {
                fd.GetMethod("SetEnabled", Static).Invoke(null, new object[] { true, 0 });
                for (int i = 0; i < 5; i++) { RequestFrame(gameView); yield return null; }
                ReadFrameDebugger(fd, eventData, report);
            }
            if (report.pixelsValid && report.unityStatsCameraCalibrationPassed)
            { report.status = report.frameDebuggerDataAvailable ? "captured-total-and-hp" : "captured-total-only";
                report.nativeTotalDraws = report.enabledCameraMedianDraws;
                report.reason = "Native UnityStats completed-frame total draws responded to camera disable/enable. Recognized FrameDebugger draw events are recorded separately and are not summed as the frame total."; }
            else if (report.pixelsValid && report.frameDebuggerDataAvailable)
            { report.status = "captured-hp-only"; report.nativeTotalDraws = -1;
                report.reason = "Positive actual-render pixels and native per-event draw records; complete frame total unavailable."; }
            else
            {
                report.reason = "Actual pixels alone are insufficient: native FrameDebugger draws and responsive UnityStats total draws unavailable. Run in a normal Editor GameView; do not report component counts or zero counters as GPU savings.";
            }
        }
        finally
        {
            camera.enabled = oldCameraEnabled; camera.targetTexture = oldTarget;
            if (fd != null && FrameDebugger.enabled != oldFdEnabled) fd.GetMethod("SetEnabled", Static).Invoke(null, new object[] { oldFdEnabled, oldRemote });
            if (fd != null && oldFdEnabled && oldLimit >= 0) fd.GetProperty("limit", Static)?.SetValue(null, oldLimit);
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
        Finish(report, outputPath, completed);
    }

    private static void RequestFrame(EditorWindow view) { view?.Repaint(); EditorApplication.QueuePlayerLoopUpdate(); }
    private static void Finish(Report report, string path, Action<Report> completed)
    {
        if (!string.IsNullOrEmpty(path))
        { string parent = Path.GetDirectoryName(Path.GetFullPath(path)); Directory.CreateDirectory(parent); File.WriteAllText(path, JsonUtility.ToJson(report, true)); }
        completed?.Invoke(report);
        Debug.Log($"MONSTER_HP_GPU,{report.label},{report.variant},{report.status},total={report.nativeTotalDraws},HP={report.nativeHpDraws},instances={report.nativeHpInstances}");
    }

    private static void ReadFrameDebugger(Type utility, Type dataType, Report report)
    {
        if (utility == null || dataType == null) return;
        try
        {
            var events = utility.GetMethod("GetFrameEvents", Static)?.Invoke(null, null) as Array;
            if (events == null || events.Length == 0) { report.apiDiagnostics.Add("FrameDebugger event array empty."); return; }
            report.events.Clear(); report.nativeRecordedDraws = 0; report.nativeHpDraws = 0; report.nativeHpInstances = 0; report.nativeInstancedHpEvents = 0;
            report.nativeFrameEventCount = events.Length; report.finalDrawCallCount = Int(utility, "count", -1);
            bool allDrawDetails = true;
            for (int i = 0; i < events.Length; i++)
            {
                object record = events.GetValue(i);
                string type = Read(record, "m_Type")?.ToString();
                if (!IsDraw(type)) continue;
                object data = Activator.CreateInstance(dataType);
                bool success = (bool)utility.GetMethod("GetFrameEventData", Static).Invoke(null, new object[] { i, data });
                if (!success) { allDrawDetails = false; report.apiDiagnostics.Add("Draw detail unavailable for event " + i); continue; }
                var entry = new Event { index = i, type = type, drawCalls = Number(data, "m_DrawCallCount"), instances = Number(data, "m_InstanceCount"),
                    vertices = Number(data, "m_VertexCount"), indices = Number(data, "m_IndexCount"), shader = Read(data, "m_OriginalShaderName") as string,
                    pass = Read(data, "m_PassName") as string, lightMode = Read(data, "m_PassLightMode") as string,
                    renderTarget = Read(data, "m_RenderTargetName") as string,
                    name = utility.GetMethod("GetFrameEventInfoName", Static)?.Invoke(null, new object[] { i }) as string,
                    objectName = (Read(record, "m_Obj") as UnityEngine.Object)?.name };
                entry.hpShader = entry.shader == "Project/Monster HP Instanced";
                if (entry.drawCalls <= 0) allDrawDetails = false;
                report.events.Add(entry); report.nativeRecordedDraws += Mathf.Max(0, entry.drawCalls);
                if (entry.hpShader) { report.nativeHpDraws += Mathf.Max(0, entry.drawCalls); report.nativeHpInstances += Mathf.Max(0, entry.instances);
                    if (type == "InstancedMesh" || entry.instances > 1) report.nativeInstancedHpEvents++; }
            }
            report.frameDebuggerDataAvailable = allDrawDetails && report.nativeRecordedDraws > 0;
            if (!report.frameDebuggerDataAvailable) report.nativeRecordedDraws = report.nativeHpDraws = -1;
        }
        catch (Exception exception) { report.apiDiagnostics.Add(exception.GetBaseException().ToString()); }
    }

    private static bool IsDraw(string type) => type == "Mesh" || type == "InstancedMesh" || type == "StaticBatch" || type == "DynamicBatch" ||
        type == "DynamicGeometry" || type == "GLDraw" || type == "SkinOnGPU" || type == "SRPBatch" || type == "HybridBatch" ||
        (type != null && type.StartsWith("DrawProcedural", StringComparison.Ordinal));
    private static object Read(object value, string name) => value?.GetType().GetField(name, Instance)?.GetValue(value);
    private static int Number(object value, string name) => Read(value, name) is int number ? number : -1;
    private static int Int(Type type, string name, int fallback)
    {
        if (type == null) return fallback;
        object value = type.GetProperty(name, Static)?.GetValue(null) ?? type.GetField(name, Static)?.GetValue(null) ??
            type.GetMethod(name, Static, null, Type.EmptyTypes, null)?.Invoke(null, null);
        return value is int number ? number : fallback;
    }
    private static bool Bool(Type type, string name) => type?.GetProperty(name, Static)?.GetValue(null) is bool value && value;
    private static int Median(List<int> samples) { if (samples.Count == 0 || samples.Exists(value => value < 0)) return -1; var sorted = samples.ToArray(); Array.Sort(sorted); return sorted[sorted.Length / 2]; }
    private static void ReadPixels(RenderTexture target, Report report)
    {
        RenderTexture previous = RenderTexture.active;
        var readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
        try
        {
            RenderTexture.active = target; readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); readback.Apply();
            Color32[] pixels = readback.GetPixels32(); foreach (Color32 pixel in pixels) if (pixel.r != 0 || pixel.g != 0 || pixel.b != 0 || pixel.a != 0) report.nonzeroPixels++;
            report.pixelWidth = target.width; report.pixelHeight = target.height; report.pixelsValid = report.nonzeroPixels > 128;
        }
        finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(readback); }
    }
}
#endif
