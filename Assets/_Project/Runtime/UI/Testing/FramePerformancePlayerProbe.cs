using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityGAS;

// Measurement-only component injected into a build copy of TestScene, never a saved scene.
public sealed class FramePerformancePlayerProbe : MonoBehaviour
{
    [Serializable] public sealed class Report
    {
        public string variant, status, reason, unity, cpu, gpu, api;
        public int width, height;
        public bool renderingAreaWasEnabled;
        public string mainTimingMeaning = "Main Thread elapsed time can include waits; wait markers are reported separately and are not additive.";
        public List<string> counterInventory = new();
        public GcCalibration gcCalibration = new();
        public string scope = "Development Windows player, existing authored TestScene, uncapped 1280x720, no movement input, normal HP/resource/HUD counts, test-owned State.Invulnerable tag, authored GoblinWarrior crowd and fixed-rate popup stream.";
        public List<Scenario> scenarios = new();
    }
    [Serializable] public sealed class Scenario
    {
        public string name;
        public int requestedEnemies, actualEnemies;
        public double medianFrameMs, p95FrameMs, averageFps, medianMainMs, p95MainMs;
        public bool mainCounterValid, allocationCounterValid;
        public bool drawCounterValid, setPassCounterValid, verticesCounterValid, renderingProofValid;
        public bool screenshotComplete, screenshotNonuniform, focusedAtStart, focusedAtEnd;
        public string screenshotPath, screenshotReason;
        public int renderedContexts, renderedCameraCallbacks, screenshotDifferentPixels;
        public long medianGcBytes, p95GcBytes, medianDrawCalls;
        public long medianSetPassCalls, medianVertices;
        public List<WaitMarker> waitMarkers = new();
        public int popupRequests;
        public int hudHeartTokens;
        public Vector3 playerStart, playerEnd;
        public List<Sample> samples = new();
    }
    [Serializable] public struct Sample { public double frameMs, mainMs; public long gcBytes, draws, setPassCalls, vertices; }
    [Serializable] public sealed class WaitMarker
    {
        public string name;
        public bool available;
        public int positiveFrames;
        public double medianMs, p95Ms;
    }
    [Serializable] public sealed class GcCalibration
    {
        public bool valid;
        public string unit, reason;
        public double medianNoPayload, median4096Payload, median8192Payload;
        public List<long> noPayload = new(), payload4096 = new(), payload8192 = new();
    }
    static readonly string[] WaitNames = { "WaitForTargetFPS", "Gfx.WaitForPresentOnGfxThread", "Gfx.PresentFrame", "Semaphore.WaitForSignal", "BehaviourUpdate", "PlayerLoop" };
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    TestSceneControlPanel panel;
    PlayerInteractor2D player;
    Report report;
    IInputActionQueryBackend previousInput;
    int previousTarget, previousVsync;
    bool changedSettings;
    GameSettingsService settings;
    bool previousSettingsEnabled;
    TagSystem protectionTags;
    GameplayTag protectionTag;
    bool protectionApplied;
    bool renderingHooksInstalled;
    Scenario currentScenario;
    Coroutine screenshotWorker;
    Vector3 playerOrigin;

    IEnumerator Start()
    {
        report = new Report { variant = Arg("-framePerfVariant", "unknown"), status = "running",
            unity = Application.unityVersion, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
            api = SystemInfo.graphicsDeviceType.ToString(), width = Screen.width, height = Screen.height };
        panel = GetComponent<TestSceneControlPanel>();
        if (panel == null || gameObject.scene.name != "TestScene") { Finish("Authored TestScene controller is missing."); yield break; }
        float timeout = Time.realtimeSinceStartup + 60;
        while (PlayerRuntimeRegistry.CurrentPlayer == null && Time.realtimeSinceStartup < timeout) yield return null;
        player = PlayerRuntimeRegistry.CurrentPlayer;
        if (player == null) { Finish("Player startup timeout."); yield break; }
        panel.ClearSpawned(); yield return null;
        var root = (RectTransform)typeof(TestSceneControlPanel).GetField("panelRoot", Private).GetValue(panel);
        if (root != null && root.gameObject.activeSelf) panel.TogglePanel();
        previousInput = (IInputActionQueryBackend)typeof(InputActionQuery).GetField("backend", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        InputActionQuery.RegisterBackend(new ProbeInput { player = player, icons = previousInput });
        previousTarget = Application.targetFrameRate; previousVsync = QualitySettings.vSyncCount;
        Application.runInBackground = true; Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0; changedSettings = true;
        settings = GameSettingsService.Instance;
        if (settings != null) { previousSettingsEnabled = settings.enabled; settings.enabled = false; }
        Screen.SetResolution(1280,720,FullScreenMode.Windowed);
        for (int i=0;i<5;i++) yield return null;
        report.width = Screen.width; report.height = Screen.height;
        report.renderingAreaWasEnabled = Profiler.GetAreaEnabled(ProfilerArea.Rendering);
        Profiler.SetAreaEnabled(ProfilerArea.Rendering, true);
        RenderPipelineManager.endContextRendering += ContextRendered;
        renderingHooksInstalled = true;
        yield return null;
        RefreshCounterInventory();
        protectionTags = player.GetComponent<TagSystem>();
        protectionTag = Resources.Load<GameplayTag>("Tags/State.Invulnerable");
        if (protectionTags == null || protectionTag == null) { Finish("Test protection is unavailable."); yield break; }
        protectionTags.AddTag(protectionTag, 1); protectionApplied = true;
        playerOrigin = player.transform.position;
        double settleUntil = Time.realtimeSinceStartupAsDouble + 5;
        while (Time.realtimeSinceStartupAsDouble < settleUntil) yield return null;
        yield return CalibrateGcCounter();
        foreach (int count in new[] { 0, 30, 60 })
        {
            panel.ClearSpawned(); yield return null; yield return null;
            ResetPlayerOrigin(); yield return new WaitForFixedUpdate(); yield return null;
            UnityEngine.Random.InitState(28147 + count);
            if (!Spawn(count)) { Finish("Crowd creation failed."); yield break; }
            yield return Capture("crowd-" + count, count, false);
            if (report.status == "failed") { Finish(report.reason); yield break; }
        }
        panel.ClearSpawned(); yield return null; yield return null;
        ResetPlayerOrigin(); yield return new WaitForFixedUpdate(); yield return null;
        UnityEngine.Random.InitState(28147);
        yield return Capture("popup-stream-120-per-second", 0, true);
        Finish(report.status == "failed" ? report.reason : null);
    }
    bool Spawn(int count)
    {
        if (count == 0) return true;
        var prefabs = (GameObject[])typeof(TestSceneControlPanel).GetField("mobPrefabs", Private).GetValue(panel);
        GameObject prefab = Array.Find(prefabs, p => p != null && p.name == "GoblinWarrior");
        if (prefab == null) return false;
        var area = (MonsterRoomArea2D)typeof(TestSceneControlPanel).GetField("roomArea", Private).GetValue(panel);
        var group = (MonsterSpawnRoomGroup)typeof(TestSceneControlPanel).GetField("roomGroup", Private).GetValue(panel);
        var finder = UnityEngine.Object.FindAnyObjectByType<TilemapPathfinder2D>();
        for (int i = 0; i < count; i++)
        {
            float angle = i * 2.399963f, radius = 5 + i % 4 * 1.2f;
            Vector3 position = player.transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
            GameObject instance;
            if (MonsterSpawner.Instance != null)
                instance = MonsterSpawner.Instance.SpawnOne(new MonsterSpawnRequest(prefab, position, Quaternion.identity, area, null, group));
            else
            {
                instance = Instantiate(prefab, position, Quaternion.identity);
                var context = new MonsterSpawnContext(position, Quaternion.identity, area, finder);
                foreach (var component in instance.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component is IMonsterSpawnContextReceiver receiver) receiver.ApplySpawnContext(context);
                group?.NotifyMonsterSpawned(instance);
            }
            if (instance == null) return false;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
        }
        return true;
    }
    IEnumerator Capture(string name, int enemies, bool popups)
    {
        var scenario = new Scenario { name = name, requestedEnemies = enemies, samples = new List<Sample>(20000), focusedAtStart = Application.isFocused };
        scenario.playerStart = player.transform.position;
        currentScenario = scenario;
        RefreshCounterInventory();
        using var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        using var draw = StartAvailableRecorder("Draw Calls Count", false);
        using var setPass = StartAvailableRecorder("SetPass Calls Count", false);
        using var vertices = StartAvailableRecorder("Vertices Count", false);
        var waits = new ProfilerRecorder[WaitNames.Length];
        var waitValues = new List<double>[WaitNames.Length];
        for (int i = 0; i < waits.Length; i++)
        {
            waits[i] = StartAvailableRecorder(WaitNames[i], true);
            waitValues[i] = new List<double>(20000);
        }
        try
        {
            scenario.allocationCounterValid = gc.Valid && report.gcCalibration.valid && gc.UnitType.ToString() == "Bytes";
            double previous = Time.realtimeSinceStartupAsDouble, captureStart = previous + 5;
            double budget = 0; int popupIndex = 0;
            screenshotWorker = StartCoroutine(CaptureWarmupScreenshot(scenario));
            bool captureStarted = false;
            while (previous < captureStart + 8)
            {
                if (!captureStarted && previous >= captureStart)
                {
                    captureStarted = true;
                    scenario.renderedContexts = scenario.renderedCameraCallbacks = 0;
                    if (!scenario.screenshotComplete)
                    {
                        // WaitForEndOfFrame may not advance for a hidden/headless window.
                        // Do not let a late GPU readback distort the measured GC/frame interval.
                        StopCoroutine(screenshotWorker);
                        scenario.screenshotReason = "No end-of-frame screenshot during the five-second warmup; rendering proof remains unavailable.";
                    }
                    screenshotWorker = null;
                }
                if (popups)
                {
                    budget += Time.unscaledDeltaTime * 120;
                    int count = Math.Min(60, (int)budget); budget -= count;
                    for (int i = 0; i < count; i++)
                    {
                        int index = popupIndex++;
                        DamagePopupService.Show(10 + index % 12, player.transform.position + new Vector3(index % 4 * .3f, index % 12 / 4 * .25f, 0));
                        if (previous >= captureStart) scenario.popupRequests++;
                    }
                }
                yield return null;
                double now = Time.realtimeSinceStartupAsDouble;
                if (previous >= captureStart)
                {
                    scenario.samples.Add(new Sample
                    {
                        frameMs = (now - previous) * 1000, mainMs = main.Valid ? main.LastValue * 1e-6 : -1,
                        gcBytes = scenario.allocationCounterValid ? gc.LastValue : -1,
                        draws = draw.Valid ? draw.LastValue : -1,
                        setPassCalls = setPass.Valid ? setPass.LastValue : -1,
                        vertices = vertices.Valid ? vertices.LastValue : -1
                    });
                    for (int i = 0; i < waits.Length; i++)
                        if (waits[i].Valid) waitValues[i].Add(waits[i].LastValue * 1e-6);
                }
                previous = now;
            }
            for (int i = 0; i < waits.Length; i++)
            {
                var marker = new WaitMarker { name = WaitNames[i], available = waits[i].Valid, medianMs = -1, p95Ms = -1 };
                foreach (double value in waitValues[i]) if (value > 0) marker.positiveFrames++;
                if (waitValues[i].Count > 0) { marker.medianMs = Q(waitValues[i], .5); marker.p95Ms = Q(waitValues[i], .95); }
                scenario.waitMarkers.Add(marker);
            }
        }
        finally
        {
            foreach (var recorder in waits) recorder.Dispose();
            if (screenshotWorker != null) { StopCoroutine(screenshotWorker); screenshotWorker = null; }
            currentScenario = null;
        }
        scenario.actualEnemies = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Exclude).Length;
        scenario.hudHeartTokens = UnityEngine.Object.FindObjectsByType<HeartTokenUI>(FindObjectsInactive.Include).Length;
        scenario.playerEnd = player.transform.position;
        scenario.focusedAtEnd = Application.isFocused;
        var wall = new List<double>(); var cpu = new List<double>(); var bytes = new List<double>(); var draws = new List<double>();
        var passes = new List<double>(); var verts = new List<double>(); double total = 0;
        foreach (var sample in scenario.samples)
        {
            wall.Add(sample.frameMs); cpu.Add(sample.mainMs); bytes.Add(sample.gcBytes); draws.Add(sample.draws);
            passes.Add(sample.setPassCalls); verts.Add(sample.vertices); total += sample.frameMs;
        }
        if (wall.Count == 0) { report.status = "failed"; report.reason = "No frame samples."; yield break; }
        scenario.medianFrameMs = Q(wall,.5); scenario.p95FrameMs = Q(wall,.95); scenario.averageFps = wall.Count * 1000 / total;
        scenario.mainCounterValid = main.Valid && main.LastValue > 0; scenario.medianMainMs = Q(cpu,.5); scenario.p95MainMs = Q(cpu,.95);
        scenario.medianGcBytes = (long)Q(bytes,.5); scenario.p95GcBytes = (long)Q(bytes,.95); scenario.medianDrawCalls = (long)Q(draws,.5);
        scenario.medianSetPassCalls = (long)Q(passes,.5); scenario.medianVertices = (long)Q(verts,.5);
        scenario.drawCounterValid = draw.Valid; scenario.setPassCounterValid = setPass.Valid; scenario.verticesCounterValid = vertices.Valid;
        scenario.renderingProofValid = scenario.medianDrawCalls > 0 || scenario.medianSetPassCalls > 0 || scenario.medianVertices > 0 ||
            (scenario.renderedContexts > 0 && scenario.renderedCameraCallbacks > 0 && scenario.screenshotNonuniform);
        report.scenarios.Add(scenario);
        if (Time.timeScale != 1 || scenario.actualEnemies != enemies || scenario.hudHeartTokens > 128)
        { report.status = "failed"; report.reason = "Invalid workload: pause, actor count changed or abnormal heart HUD count."; }
    }
    void ContextRendered(ScriptableRenderContext context, List<Camera> cameras)
    {
        if (currentScenario == null) return;
        currentScenario.renderedContexts++;
        currentScenario.renderedCameraCallbacks += cameras.Count;
    }
    IEnumerator CaptureWarmupScreenshot(Scenario scenario)
    {
        yield return new WaitForEndOfFrame();
        Texture2D capture = null;
        try
        {
            capture = ScreenCapture.CaptureScreenshotAsTexture();
            if (capture == null) { scenario.screenshotReason = "ScreenCapture returned no texture."; yield break; }
            Color32[] pixels = capture.GetPixels32();
            if (pixels.Length > 0)
            {
                Color32 reference = pixels[0];
                foreach (Color32 pixel in pixels)
                    if (Math.Abs(pixel.r - reference.r) > 8 || Math.Abs(pixel.g - reference.g) > 8 || Math.Abs(pixel.b - reference.b) > 8)
                        scenario.screenshotDifferentPixels++;
            }
            scenario.screenshotNonuniform = scenario.screenshotDifferentPixels > 32;
            string output = Path.GetFullPath(Arg("-framePerfOutput", "frame-performance.json"));
            scenario.screenshotPath = Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "-" + scenario.name + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(scenario.screenshotPath));
            File.WriteAllBytes(scenario.screenshotPath, capture.EncodeToPNG());
            scenario.screenshotReason = scenario.screenshotNonuniform ? "Nonuniform completed-frame pixels captured during warmup." : "Screenshot was uniform; it cannot prove scene rendering.";
        }
        catch (Exception exception) { scenario.screenshotReason = "Screenshot unavailable: " + exception.Message; }
        finally { if (capture != null) Destroy(capture); scenario.screenshotComplete = true; }
    }
    void RefreshCounterInventory()
    {
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        foreach (var handle in handles)
        {
            var description = ProfilerRecorderHandle.GetDescription(handle);
            string entry = $"{description.Name};category={description.Category.Name};unit={description.UnitType};flags={description.Flags}";
            if (!report.counterInventory.Contains(entry)) report.counterInventory.Add(entry);
        }
    }
    static ProfilerRecorder StartAvailableRecorder(string name, bool requireTime)
    {
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        foreach (var handle in handles)
        {
            var description = ProfilerRecorderHandle.GetDescription(handle);
            if (description.Name == name && (!requireTime || description.UnitType.ToString() == "TimeNanoseconds"))
                return new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately);
        }
        return default;
    }
    IEnumerator CalibrateGcCounter()
    {
        using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        var calibration = report.gcCalibration;
        calibration.unit = gc.Valid ? gc.UnitType.ToString() : "unavailable";
        if (!gc.Valid || calibration.unit != "Bytes")
        { calibration.reason = "GC counter is unavailable or its declared unit is not Bytes."; yield break; }
        yield return null;
        foreach (int size in new[] { 0, 4096, 8192 })
        {
            List<long> values = size == 0 ? calibration.noPayload : size == 4096 ? calibration.payload4096 : calibration.payload8192;
            values.Capacity = 16;
            // The previous-frame counter is read after yielding the frame that allocated the payload.
            for (int frame = 0; frame < 16; frame++)
            {
                byte[] payload = size == 0 ? null : new byte[size];
                yield return null;
                values.Add(gc.LastValue);
                GC.KeepAlive(payload);
            }
        }
        calibration.medianNoPayload = MedianBytes(calibration.noPayload);
        calibration.median4096Payload = MedianBytes(calibration.payload4096);
        calibration.median8192Payload = MedianBytes(calibration.payload8192);
        double firstDelta = calibration.median4096Payload - calibration.medianNoPayload;
        double secondDelta = calibration.median8192Payload - calibration.median4096Payload;
        // Allow 256 bytes for array headers/alignment and minor baseline noise, not arbitrary scene allocations.
        calibration.valid = Math.Abs(firstDelta - 4096) <= 256 && Math.Abs(secondDelta - 4096) <= 256;
        calibration.reason = calibration.valid ? "4096-byte controlled payload steps matched the Bytes counter within 256 bytes." :
            "Payload deltas did not calibrate within 256 bytes; scene noise or unavailable accounting prevents GC-byte claims.";
    }
    static double MedianBytes(List<long> values)
    { var copy = new List<double>(values.Count); foreach (long value in values) copy.Add(value); return Q(copy, .5); }
    void ResetPlayerOrigin()
    {
        if (player.TryGetComponent(out MovementMotor2D motor)) motor.WarpTo(playerOrigin, true, true);
        else
        {
            player.transform.position = playerOrigin;
            if (player.TryGetComponent(out Rigidbody2D body)) { body.position = playerOrigin; body.linearVelocity = Vector2.zero; body.angularVelocity = 0; }
        }
    }
    static double Q(List<double> values,double q) { values.Sort(); return values[Mathf.Clamp((int)Math.Ceiling(values.Count*q)-1,0,values.Count-1)]; }
    void Finish(string reason)
    {
        bool complete = report.scenarios.TrueForAll(scenario => scenario.renderingProofValid && scenario.mainCounterValid && scenario.allocationCounterValid);
        report.status = reason != null ? "failed" : complete ? "measured" : "measured-limited";
        report.reason = reason ?? (complete ? null : "One or more rendering/main-thread/allocation measurements are unavailable; unsupported counters do not prove absent rendering.");
        CleanupRenderingProbe();
        ReleaseProtection();
        if (changedSettings) { Application.targetFrameRate = previousTarget; QualitySettings.vSyncCount = previousVsync; InputActionQuery.RegisterBackend(previousInput); }
        if (settings != null) settings.enabled = previousSettingsEnabled;
        string output = Path.GetFullPath(Arg("-framePerfOutput", "frame-performance.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, JsonUtility.ToJson(report,true));
        Application.Quit(reason == null ? 0 : 1);
    }
    void CleanupRenderingProbe()
    {
        if (!renderingHooksInstalled) return;
        RenderPipelineManager.endContextRendering -= ContextRendered;
        Profiler.SetAreaEnabled(ProfilerArea.Rendering, report.renderingAreaWasEnabled);
        renderingHooksInstalled = false;
        if (screenshotWorker != null) { StopCoroutine(screenshotWorker); screenshotWorker = null; }
    }
    void ReleaseProtection()
    {
        if (!protectionApplied) return;
        if (protectionTags != null && protectionTag != null) protectionTags.RemoveTag(protectionTag, 1);
        protectionApplied = false;
    }
    void OnDestroy() { CleanupRenderingProbe(); ReleaseProtection(); }
    static string Arg(string name,string fallback)
    { var args=Environment.GetCommandLineArgs(); for(int i=0;i+1<args.Length;i++)if(args[i]==name)return args[i+1];return fallback; }
    sealed class ProbeInput : IInputActionQueryBackend
    {
        public PlayerInteractor2D player; public IInputActionQueryBackend icons;
        public Component BackendComponent => player;
        public Sprite GetBindingIcon(InputActionId action) => icons?.GetBindingIcon(action);
        public bool WasPressedThisFrame(InputActionId action) => false;
        public bool WasReleasedThisFrame(InputActionId action) => false;
        public bool IsPressed(InputActionId action) => false;
        public bool IsKeyPressed(KeyCode key) => false;
        public bool WasKeyPressedThisFrame(KeyCode key) => false;
        public bool WasKeyReleasedThisFrame(KeyCode key) => false;
        public Vector2 GetMoveVectorRaw() => Vector2.zero;
        public Vector2 GetMoveVectorNormalized() => Vector2.zero;
        public Vector3 GetPointerWorldPosition(Camera camera,float z=0) => player.transform.position+Vector3.right*5;
    }
}
