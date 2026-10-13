#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityGAS;
using Object = UnityEngine.Object;

// Test-only owner of a reproducible Editor player-loop capture. No saved scene changes.
[InitializeOnLoad]
public static class FramePerformanceSceneBenchmarks
{
    const string Pending = "FramePerformanceSceneBenchmarks.Pending";
    const string VariantKey = "FramePerformanceSceneBenchmarks.Variant";
    const string OutputKey = "FramePerformanceSceneBenchmarks.Output";
    static readonly Stack<IEnumerator> routines = new();
    static readonly List<GameObject> fixtures = new();
    static bool started, finishing;
    static int lastFrame = -1;
    static double deadline;
    static Report report;
    static string uiOutputPath;
    static IInputActionQueryBackend previousInput;
    static BenchmarkInput input;
    static int previousTargetRate, previousVsync;
    static TagSystem protectionTags;
    static GameplayTag protectionTag;
    static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Serializable] public sealed class Report
    {
        public string variant, status, reason, unity, cpu, gpu, api;
        public int width, height;
        public string scope = "Unity Editor TestScene player loop, uncapped; not a standalone player FPS claim.";
        public string[] availableCounters;
        public List<Scenario> scenarios = new();
    }
    [Serializable] public sealed class Scenario
    {
        public string name;
        public int requestedEnemies, actualEnemies, warmupFrames, captureFrames;
        public double medianFrameMs, p95FrameMs, averageFps, medianMainThreadMs, p95MainThreadMs;
        public double medianRenderThreadMs;
        public bool mainThreadValid, renderThreadValid, allocationCounterValid;
        public long medianGcBytes, p95GcBytes;
        public float timeScale;
        public List<Sample> samples = new();
    }
    [Serializable] public struct Sample
    {
        public double frameMs, mainMs, renderMs;
        public long gcBytes, drawCalls;
    }
    static FramePerformanceSceneBenchmarks() => EditorApplication.update += Update;

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before starting a capture.");
        if (!Application.isBatchMode && UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the current scene before measurement.");
        started = finishing = false; lastFrame = -1;
        routines.Clear(); fixtures.Clear();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Tests/TestScene.unity", OpenSceneMode.Single);
        SessionState.SetString(VariantKey, Argument("-framePerfVariant", "optimized"));
        SessionState.SetString(OutputKey, Argument("-framePerfOutput", "Library/FramePerformanceMeasurement/scene.json"));
        SessionState.SetBool(Pending, true);
        TestScenePresentationTraceIsolation.Enter();
        EditorApplication.isPlaying = true;
    }

    static string Argument(string flag, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == flag) return args[i + 1];
        return fallback;
    }
    static void Update()
    {
        if (!SessionState.GetBool(Pending, false) || finishing) return;
        if (!EditorApplication.isPlaying) return;
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (!started)
            {
                if (PlayerRuntimeRegistry.CurrentPlayer == null || Time.frameCount < 15) return;
                started = true;
                deadline = EditorApplication.timeSinceStartup + 600;
                routines.Push(Suite());
            }
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Capture timed out.");
            while (routines.Count > 0)
            {
                IEnumerator top = routines.Peek();
                if (!top.MoveNext()) { (top as IDisposable)?.Dispose(); routines.Pop(); continue; }
                if (top.Current is IEnumerator nested) { routines.Push(nested); continue; }
                return;
            }
            Finish(null);
        }
        catch (Exception exception) { Finish(exception.ToString()); }
    }

    static IEnumerator Suite()
    {
        report = new Report
        {
            variant = SessionState.GetString(VariantKey, "unknown"), status = "running",
            unity = Application.unityVersion, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
            api = SystemInfo.graphicsDeviceType.ToString(), width = Screen.width, height = Screen.height
        };
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        var names = new List<string>();
        foreach (var handle in handles) names.Add(ProfilerRecorderHandle.GetDescription(handle).Name);
        report.availableCounters = names.ToArray();
        var panel = Object.FindAnyObjectByType<TestSceneControlPanel>();
        if (panel == null) throw new InvalidOperationException("TestScene sandbox is missing.");
        panel.ClearSpawned();
        var panelRect = (RectTransform)typeof(TestSceneControlPanel).GetField("panelRoot", PrivateInstance).GetValue(panel);
        if (panelRect != null && panelRect.gameObject.activeSelf) panel.TogglePanel();
        previousTargetRate = Application.targetFrameRate; previousVsync = QualitySettings.vSyncCount;
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
        previousInput = (IInputActionQueryBackend)typeof(InputActionQuery).GetField("backend", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        input = new BenchmarkInput { owner = PlayerRuntimeRegistry.CurrentPlayer };
        InputActionQuery.RegisterBackend(input);
        protectionTags = input.owner.GetComponent<TagSystem>();
        protectionTag = Resources.Load<GameplayTag>("Tags/State.Invulnerable");
        if (protectionTags == null || protectionTag == null) throw new InvalidOperationException("Test protection is unavailable.");
        protectionTags.AddTag(protectionTag, 1);
        bool operationsOnly = Argument("-framePerfOpsOnly", "false") == "true";
        bool gpuOnly = Argument("-framePerfGpuOnly", "false") == "true";
        if (operationsOnly) report.scope = "Isolated operation comparisons in Editor Play Mode; no Editor FPS result.";
        if (gpuOnly) report.scope = "Isolated shader GPU repeat in Editor Play Mode; no Editor FPS result.";
        if (!operationsOnly)
        {
        foreach (int enemies in new[] { 0, 30, 60 })
        {
            panel.ClearSpawned(); yield return null; yield return null;
            UnityEngine.Random.InitState(28147 + enemies);
            SpawnCrowd(panel, enemies);
            yield return Capture("crowd-" + enemies, enemies, false);
        }
        panel.ClearSpawned(); yield return null; yield return null;
        UnityEngine.Random.InitState(28147);
        yield return Capture("popup-stream", 0, true);
        }
        if (report.variant == "optimized")
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(SessionState.GetString(OutputKey, "Library/FramePerformanceMeasurement/scene.json")));
            Directory.CreateDirectory(directory);
            if (!gpuOnly)
            {
            File.WriteAllText(Path.Combine(directory, "cpu-operations.json"), FramePerformanceCpuBenchmarks.Run());
            uiOutputPath = Path.Combine(directory, "ui-operations.json");
            Type uiType = Type.GetType("FramePerformanceUiBenchmarks, Editor", true);
            MethodInfo uiRun = uiType.GetMethod("RunCoroutine");
            Type callbackType = uiRun.GetParameters()[0].ParameterType;
            MethodInfo save = typeof(FramePerformanceSceneBenchmarks).GetMethod("SaveUi", BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(callbackType.GetGenericArguments()[0]);
            Delegate callback = Delegate.CreateDelegate(callbackType, save);
            yield return (IEnumerator)uiRun.Invoke(null, new object[] { callback });
            }
            yield return FramePerformanceGpuBenchmarks.Run(Path.Combine(directory, "gpu.json"), measuredFrames: 30);
        }
    }
    static void SaveUi<T>(T result) => File.WriteAllText(uiOutputPath, JsonUtility.ToJson(result, true));

    static void SpawnCrowd(TestSceneControlPanel panel, int count)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Monsters/CommonCorridor/GoblinWarrior.prefab");
        var area = (MonsterRoomArea2D)typeof(TestSceneControlPanel).GetField("roomArea", PrivateInstance).GetValue(panel);
        var group = (MonsterSpawnRoomGroup)typeof(TestSceneControlPanel).GetField("roomGroup", PrivateInstance).GetValue(panel);
        Vector3 player = PlayerRuntimeRegistry.CurrentPlayer.transform.position;
        for (int i = 0; i < count; i++)
        {
            float angle = i * 2.399963f;
            float radius = 5f + (i % 4) * 1.2f;
            Vector3 position = player + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
            var request = new MonsterSpawnRequest(prefab, position, Quaternion.identity, area, null, group);
            GameObject instance = MonsterSpawner.Instance != null ? MonsterSpawner.Instance.SpawnOne(request) : Object.Instantiate(prefab, position, Quaternion.identity);
            if (MonsterSpawner.Instance == null && instance != null)
            {
                var context = new MonsterSpawnContext(position, Quaternion.identity, area, Object.FindAnyObjectByType<TilemapPathfinder2D>());
                foreach (var component in instance.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component is IMonsterSpawnContextReceiver receiver) receiver.ApplySpawnContext(context);
                group?.NotifyMonsterSpawned(instance);
            }
            if (instance == null) throw new InvalidOperationException("Crowd spawn failed.");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, panel.gameObject.scene);
            fixtures.Add(instance);
        }
    }

    static IEnumerator Capture(string name, int enemies, bool popups)
    {
        const double warmupSeconds = 5, captureSeconds = 8;
        var scenario = new Scenario { name = name, requestedEnemies = enemies, samples = new List<Sample>(8000) };
        using var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        using var render = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
        using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        using var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
        byte[] allocationCanary = new byte[8192];
        long calibrationBytes = 0;
        for (int i = 0; i < 4; i++) { yield return null; if (gc.Valid) calibrationBytes = Math.Max(calibrationBytes, gc.LastValue); }
        GC.KeepAlive(allocationCanary);
        scenario.allocationCounterValid = gc.Valid && calibrationBytes >= 8192;
        int previousFrame = Time.frameCount;
        double previousTime = Time.realtimeSinceStartupAsDouble;
        double begin = previousTime, captureBegin = begin + warmupSeconds;
        double popupBudget = 0;
        while (previousTime < captureBegin + captureSeconds)
        {
            if (popups)
            {
                popupBudget += Time.unscaledDeltaTime * 120;
                int count = Math.Min(60, (int)popupBudget); popupBudget -= count;
                for (int i = 0; i < count; i++) DamagePopupService.Show(10 + i, input.owner.transform.position + new Vector3(i % 4 * .3f, i / 4 * .25f, 0));
            }
            yield return null;
            double now = Time.realtimeSinceStartupAsDouble;
            int elapsed = Time.frameCount - previousFrame;
            if (previousTime >= captureBegin)
            {
                if (elapsed != 1) throw new InvalidOperationException("Capture did not observe each player-loop frame.");
                scenario.samples.Add(new Sample { frameMs = (now - previousTime) * 1000,
                    mainMs = main.Valid ? main.LastValue * 1e-6 : -1, renderMs = render.Valid ? render.LastValue * 1e-6 : -1,
                    gcBytes = scenario.allocationCounterValid ? gc.LastValue : -1, drawCalls = draws.Valid ? draws.LastValue : -1 });
            }
            else scenario.warmupFrames++;
            previousTime = now; previousFrame = Time.frameCount;
        }
        scenario.actualEnemies = Object.FindObjectsByType<Enemy>(FindObjectsInactive.Exclude).Length;
        scenario.timeScale = Time.timeScale;
        scenario.mainThreadValid = main.Valid && main.LastValue > 0;
        scenario.renderThreadValid = render.Valid && render.LastValue > 0;
        scenario.captureFrames = scenario.samples.Count;
        var wall = new List<double>(); var cpu = new List<double>(); var rt = new List<double>(); var bytes = new List<double>();
        double total = 0;
        foreach (var sample in scenario.samples) { wall.Add(sample.frameMs); cpu.Add(sample.mainMs); rt.Add(sample.renderMs); bytes.Add(sample.gcBytes); total += sample.frameMs; }
        scenario.medianFrameMs = Quantile(wall, .5); scenario.p95FrameMs = Quantile(wall, .95);
        scenario.averageFps = scenario.captureFrames * 1000 / total; scenario.medianMainThreadMs = Quantile(cpu, .5); scenario.p95MainThreadMs = Quantile(cpu, .95);
        scenario.medianRenderThreadMs = Quantile(rt, .5); scenario.medianGcBytes = (long)Quantile(bytes, .5); scenario.p95GcBytes = (long)Quantile(bytes, .95);
        report.scenarios.Add(scenario);
        if (scenario.timeScale != 1f || scenario.actualEnemies != enemies) throw new InvalidOperationException("Workload changed during capture: pause/death/missing enemies.");
    }
    static double Quantile(List<double> values, double fraction) { values.Sort(); return values[Mathf.Clamp((int)Math.Ceiling(values.Count * fraction) - 1, 0, values.Count - 1)]; }
    static void Finish(string error)
    {
        finishing = true;
        try
        {
            while (routines.Count > 0) (routines.Pop() as IDisposable)?.Dispose();
            InputActionQuery.RegisterBackend(previousInput);
            if (protectionTags != null && protectionTag != null) protectionTags.RemoveTag(protectionTag, 1);
            protectionTags = null; protectionTag = null;
            Application.targetFrameRate = previousTargetRate; QualitySettings.vSyncCount = previousVsync;
            foreach (var go in fixtures) if (go != null) Object.Destroy(go);
            if (report == null) report = new Report { variant = SessionState.GetString(VariantKey, "unknown") };
            report.status = error == null ? "measured" : "failed"; report.reason = error;
            string path = Path.GetFullPath(SessionState.GetString(OutputKey, "Library/FramePerformanceMeasurement/scene.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }
        finally
        {
            SessionState.SetBool(Pending, false); EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
    sealed class BenchmarkInput : IInputActionQueryBackend
    {
        public PlayerInteractor2D owner;
        public Component BackendComponent => owner;
        public Sprite GetBindingIcon(InputActionId action) => previousInput?.GetBindingIcon(action);
        public bool WasPressedThisFrame(InputActionId action) => false;
        public bool WasReleasedThisFrame(InputActionId action) => false;
        public bool IsPressed(InputActionId action) => false;
        public bool IsKeyPressed(KeyCode key) => false;
        public bool WasKeyPressedThisFrame(KeyCode key) => false;
        public bool WasKeyReleasedThisFrame(KeyCode key) => false;
        public Vector2 GetMoveVectorRaw() => Vector2.zero;
        public Vector2 GetMoveVectorNormalized() => Vector2.zero;
        public Vector3 GetPointerWorldPosition(Camera camera, float z = 0) => owner != null ? owner.transform.position + Vector3.right * 5 : Vector3.zero;
    }
}
#endif
