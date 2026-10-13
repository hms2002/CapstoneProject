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
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Profiling;
using UnityEngine.UI;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>Measurement-only, panel-owned actors and their real authored world HUDs. No hierarchy reparenting or asset saves.</summary>
public static class UiBatchingStructuralScenarios
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    [Serializable] public sealed class Scenario
    {
        public string label, variant;
        public string scope = "Normal-HP GoblinWarrior world HUDs, existing TestScene panel spawn/cleanup, authored Mob spawn-idle pause. Native batches/vertices are one completed frame filtered by real HUD membership. Canvas.BuildBatch is aggregate CPU time across all active canvases; no isolated HUD CPU, GPU or game FPS claim.";
        public string statusScope = "Dynamic scenario uses the authored GE_ElectrocutedStatus, including its world VFX. Native UI membership excludes world ParticleSystem rendering; aggregate Canvas.BuildBatch does not measure that GPU work.";
        public int requestedActors, actualActors, visibleActors, requestedStatusActors, activeStatusActors, hpWriteEvents, worldParticleSystems;
        public int meshProjectionActors, legacyProjectionActors;
        public bool physicsSuspendedForOverlap;
        public string healthProjectionScope = "Actual visible HP renderer or enabled original Image/Canvas path after the completed presentation phase. Overlapping HUD bounds conservatively restore all current actors to the original Canvas sorting path.";
        public bool globalDrawCounterAvailable;
        public string globalDrawCounterScope = "Native Draw Calls Count across all rendered cameras/UI/world objects in this Editor frame; not UI-only or actor-only. Require positive samples before comparing equal workloads.";
        public long[] globalDrawCallSamples;
        public long medianGlobalDrawCalls = -1;
        public bool normalMaximumHpPreserved, placementStable;
        public UiBatchingValidationRunner.Report ui;
        public List<Actor> actors = new();
        public string[] remainingCandidates = { "HP SpriteRenderer conversion/shared Canvas require equivalent representation/sorting comparisons; this fixture measures the current cost only.", "UI particle aggregation needs deterministic authored playback/age and lifecycle verification; a warmed one-shot may already have expired.", "Chest opening rolls sandbox loot and owns pause/reveal/UI locks; use its normal Open flow in a separate fixture, not synthetic slot activation." };
    }
    [Serializable] public sealed class Actor
    {
        public int index, instanceId, canvasId;
        public string name;
        public Vector3 requestedPosition, position;
        public float initialHp, initialMaxHp, hp, maxHp, electrocutedSeconds;
        public bool visible, meshProjection, legacyProjection;
        public Rect viewportBounds;
        [NonSerialized] public GameObject root;
        [NonSerialized] public AttributeSet attributes;
        [NonSerialized] public AttributeDefinition health, maximum;
        [NonSerialized] public MonsterStackStatusWorldView view;
        [NonSerialized] public GameplayEffectRunner effects;
        [NonSerialized] public MonsterStatusRuntime statuses;
    }

    /// <summary>CLI: -uiBatchSetupMethod UiBatchingStructuralScenarios.Setup -uiBatchRoot GameplayHUDCanvas; output/variant/exit flags use the existing runner.</summary>
    public static void Run()
    {
        if (Arg("-uiBatchSetupMethod", "") != "UiBatchingStructuralScenarios.Setup")
            throw new ArgumentException("Pass -uiBatchSetupMethod UiBatchingStructuralScenarios.Setup -uiBatchRoot GameplayHUDCanvas.");
        TestScenePresentationTraceIsolation.Enter();
        UiBatchingValidationRunner.Run();
    }
    public static IEnumerator Setup() => RunSuite();

    public static IEnumerator RunSuite()
    {
        if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "TestScene")
            throw new InvalidOperationException("Structural UI scenarios require the existing TestScene sandbox in Play Mode.");
        double until = Time.realtimeSinceStartupAsDouble + 60;
        while ((PlayerRuntimeRegistry.CurrentPlayer == null || GlobalUIRoot.Instance == null || UIManager.Instance == null) && Time.realtimeSinceStartupAsDouble < until) yield return null;
        var player = PlayerRuntimeRegistry.CurrentPlayer;
        var panel = Object.FindAnyObjectByType<TestSceneControlPanel>();
        if (player == null || panel == null || GlobalUIRoot.Instance == null || UIManager.Instance == null ||
            ReadField(panel, "testProfile") == null || ReadField(panel, "testRun") == null)
            throw new InvalidOperationException("Authored player/UI and disposable TestScene profile/run backends are required.");
        var catalog = ReadField(panel, "catalogGrid") as TestSceneCatalogGrid;
        var countInput = ReadField(panel, "spawnCountInput") as TMP_InputField;
        var prefabs = ReadField(panel, "mobPrefabs") as GameObject[];
        var tracked = ReadField(panel, "spawned") as List<GameObject>;
        var area = ReadField(panel, "roomArea") as MonsterRoomArea2D;
        var panelRoot = ReadField(panel, "panelRoot") as RectTransform;
        int prefabIndex = prefabs != null ? Array.FindIndex(prefabs, p => p != null && p.name == "GoblinWarrior") : -1;
        var protection = Resources.Load<GameplayTag>("Tags/State.Invulnerable");
        var tags = player.GetComponent<TagSystem>();
        var effect = AssetDatabase.LoadAssetAtPath<GameplayEffect>("Assets/_Project/Data/Abilities/Effects/GE_ElectrocutedStatus.asset");
        if (catalog == null || countInput == null || tracked == null || prefabIndex < 0 || tags == null || protection == null || effect == null)
            throw new InvalidOperationException("Existing panel catalog/spawn ownership, normal test protection or authored status effect is missing.");
        int originalSelection = catalog.GetSelectedIndex(TestSceneCatalogGrid.MonsterCategory);
        string originalCount = countInput.text;
        bool panelWasOpen = panelRoot != null && panelRoot.gameObject.activeSelf;
        Vector3 originalPosition = player.transform.position;
        Quaternion originalRotation = player.transform.rotation;
        var body = player.GetComponent<Rigidbody2D>();
        Vector2 originalVelocity = body != null ? body.linearVelocity : Vector2.zero;
        float originalAngularVelocity = body != null ? body.angularVelocity : 0;
        var originalRandom = UnityEngine.Random.state;
        FieldInfo inputField = typeof(InputActionQuery).GetField("backend", BindingFlags.Static | BindingFlags.NonPublic);
        if (inputField == null) throw new MissingFieldException("InputActionQuery.backend");
        var previousInput = inputField.GetValue(null) as IInputActionQueryBackend;
        var testInput = new NeutralInput { player = player, original = previousInput };
        string output = Path.GetFullPath(Arg("-uiBatchOutput", "Library/UiBatchValidation"));
        string variant = Arg("-uiBatchVariant", "unknown");
        tags.AddTag(protection, 1);
        InputActionQuery.RegisterBackend(testInput);
        try
        {
            UIManager.Instance.CloseAllPopups();
            if (panelWasOpen) panel.TogglePanel();
            catalog.Close();
            catalog.Select(TestSceneCatalogGrid.MonsterCategory, prefabIndex);
            countInput.SetTextWithoutNotify("1");
            for (int frame = 0; frame < 10; frame++) yield return null;
            if (Time.timeScale <= 0f) throw new InvalidOperationException("The normal sandbox is paused; do not force gameplay time for a fixture.");
            foreach (int count in new[] { 0, 30, 60 })
            {
                panel.ClearSpawned();
                for (int frame = 0; frame < 10; frame++) yield return null;
                Camera camera = Camera.main;
                if (camera == null || !camera.isActiveAndEnabled || !camera.orthographic)
                    throw new InvalidOperationException("An active authored orthographic world camera is required.");
                UnityEngine.Random.InitState(28147 + count);
                var actors = new List<Actor>(count);
                float depth = camera.WorldToViewportPoint(player.transform.position).z;
                int columns = count == 30 ? 6 : 10;
                int rows = count == 0 ? 0 : Mathf.CeilToInt(count / (float)columns);
                for (int i = 0; i < count; i++)
                {
                    int previous = tracked.Count;
                    panel.SpawnMonster();
                    if (tracked.Count != previous + 1 || tracked[previous] == null)
                        throw new InvalidOperationException("The panel must own exactly one actual monster per spawn command.");
                    GameObject root = tracked[previous];
                    Mob mob = root.GetComponent<Mob>();
                    var attributes = root.GetComponent<AttributeSet>();
                    var size = root.GetComponent<MonsterSizeProfile>();
                    var view = root.GetComponentInChildren<MonsterStackStatusWorldView>(true);
                    var motor = root.GetComponent<MovementMotor2D>();
                    if (mob == null || attributes == null || size == null || !size.ShowHealthBar || view == null || motor == null)
                        throw new InvalidOperationException("Expected the existing normal GoblinWarrior and authored HP world HUD.");
                    Vector3 position = camera.ViewportToWorldPoint(new Vector3(Mathf.Lerp(.15f, .85f, i % columns / (float)(columns - 1)),
                        Mathf.Lerp(.24f, .65f, i / columns / (float)Mathf.Max(1, rows - 1)), depth));
                    position.z = root.transform.position.z;
                    if (area != null && !area.Contains(position)) throw new InvalidOperationException("The visible fixture grid leaves the authored sandbox room area.");
                    mob.ApplySpawnIdlePause(600f); // Existing spawn gate: keeps real HUD/attributes/effects enabled.
                    motor.WarpTo(position, true, true);
                    var health = typeof(Enemy).GetField("healthDef", Fields)?.GetValue(mob) as AttributeDefinition;
                    var maximum = typeof(Enemy).GetField("maxHealthDef", Fields)?.GetValue(mob) as AttributeDefinition;
                    if (health == null || maximum == null) throw new InvalidOperationException("Authored HP attributes are missing.");
                    actors.Add(new Actor { index = i, root = root, name = root.name, instanceId = root.GetInstanceID(), attributes = attributes,
                        health = health, maximum = maximum, view = view, requestedPosition = position,
                        initialHp = attributes.GetAttributeValue(health), initialMaxHp = attributes.GetAttributeValue(maximum),
                        effects = root.GetComponent<GameplayEffectRunner>(), statuses = root.GetComponent<MonsterStatusRuntime>() });
                }
                for (int frame = 0; frame < 10; frame++) yield return null;
                yield return Capture(actors, camera, count, false, effect, player.gameObject, output, variant);
                if (count > 0) yield return Capture(actors, camera, count, true, effect, player.gameObject, output, variant);
                if (count == 60 && Arg("-uiBatchOverlap", "false") == "true")
                {
                    // The same panel-owned actors, with intersecting visible HP bounds.
                    // Record the original sorting path's cost instead of extrapolating the separated grid's gain.
                    var bodyStates = new List<KeyValuePair<Rigidbody2D, bool>>();
                    try
                    {
                        for (int i = 0; i < actors.Count; i++)
                        {
                            // Suspend only these disposable actors' collision resolution while
                            // comparing a fixed overlap pose. Their actual HUD/attribute owners stay enabled.
                            if (actors[i].root.TryGetComponent(out Rigidbody2D actorBody))
                            { bodyStates.Add(new KeyValuePair<Rigidbody2D, bool>(actorBody, actorBody.simulated)); actorBody.simulated = false; }
                            Vector3 position = camera.ViewportToWorldPoint(new Vector3(.48f + (i % 6) * .008f,
                                .4f + (i / 6) * .004f, depth));
                            position.z = actors[i].root.transform.position.z;
                            actors[i].root.GetComponent<MovementMotor2D>().WarpTo(position, true, true);
                            // A nonsimulated Rigidbody position is not applied to its Transform by a physics step.
                            actors[i].root.transform.position = position;
                            actors[i].requestedPosition = position;
                        }
                        for (int frame = 0; frame < 10; frame++) yield return null;
                        yield return Capture(actors, camera, count, false, effect, player.gameObject, output, variant, true);
                    }
                    finally { foreach (var state in bodyStates) if (state.Key != null) state.Key.simulated = state.Value; }
                }
            }
        }
        finally
        {
            panel.ClearSpawned();
            catalog.Select(TestSceneCatalogGrid.MonsterCategory, originalSelection);
            countInput.SetTextWithoutNotify(originalCount);
            if (panelRoot != null && panelRoot.gameObject.activeSelf != panelWasOpen) panel.TogglePanel();
            if (ReferenceEquals(inputField.GetValue(null), testInput)) InputActionQuery.RegisterBackend(previousInput);
            if (tags != null) tags.RemoveTag(protection, 1);
            UnityEngine.Random.state = originalRandom;
            if (player != null)
            {
                player.transform.SetPositionAndRotation(originalPosition, originalRotation);
                if (player.TryGetComponent(out MovementMotor2D motor)) motor.WarpTo(originalPosition, true, true);
                if (body != null) { body.position = originalPosition; body.linearVelocity = originalVelocity; body.angularVelocity = originalAngularVelocity; }
            }
        }
    }

    static IEnumerator Capture(List<Actor> actors, Camera camera, int count, bool dynamic, GameplayEffect effect, GameObject source, string output, string variant, bool overlap = false)
    {
        string label = "world-hud-" + count + (overlap ? "-overlap" : "") + (dynamic ? "-hp-status" : "-steady");
        var ui = new UiBatchingValidationRunner.Report { label = label, variant = variant, fixture = "Unreparented panel-owned world HUD set",
            locale = LocalizationSettings.SelectedLocale?.Identifier.Code ?? "unavailable", device = SystemInfo.graphicsDeviceName,
            api = SystemInfo.graphicsDeviceType.ToString(), unity = Application.unityVersion, status = "unavailable" };
        var result = new Scenario { label = label, variant = variant, requestedActors = count, actualActors = actors.Count,
            requestedStatusActors = dynamic ? Mathf.Min(6, count) : 0, physicsSuspendedForOverlap = overlap, ui = ui, actors = actors };
        bool oldRuntime = Profiler.enabled, oldRecording = ProfilerDriver.enabled, oldEditor = ProfilerDriver.profileEditor;
        bool oldUi = Profiler.GetAreaEnabled(ProfilerArea.UI), oldDetails = Profiler.GetAreaEnabled(ProfilerArea.UIDetails);
        bool oldDriverUi = ProfilerDriver.IsAreaEnabled(ProfilerArea.UI), oldDriverDetails = ProfilerDriver.IsAreaEnabled(ProfilerArea.UIDetails);
        int initialFrame = ProfilerDriver.lastFrameIndex;
        ProfilerRecorder timing = default;
        ProfilerRecorder drawCalls = default;
        try
        {
            ProfilerDriver.profileEditor = false; ProfilerDriver.SetAreaEnabled(ProfilerArea.UI, true); ProfilerDriver.SetAreaEnabled(ProfilerArea.UIDetails, true);
            ProfilerDriver.enabled = true; Profiler.enabled = true;
            Profiler.SetAreaEnabled(ProfilerArea.UI, true); Profiler.SetAreaEnabled(ProfilerArea.UIDetails, true);
            for (int warm = 0; warm < 30; warm++) { UpdateWorkload(result, warm, dynamic, effect, source); yield return null; }
            ui.heartTokens = Object.FindObjectsByType<HeartTokenUI>(FindObjectsInactive.Include).Length;
            if (ui.heartTokens > 128) throw new InvalidOperationException("Invalid inflated-health player HUD fixture.");
            Graphic[] graphics = HudGraphics(actors);
            Canvas gameplayHud = GlobalUIRoot.GetCanvas(GlobalCanvasLayer.GameplayHUD);
            if (gameplayHud == null) throw new InvalidOperationException("The authored GameplayHUD canary is unavailable.");
            timing = (ProfilerRecorder)Runner("StartTimingRecorder", ui);
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
            if (timing.Valid)
            {
                foreach (Graphic graphic in gameplayHud.GetComponentsInChildren<Graphic>(true)) if (graphic.isActiveAndEnabled) graphic.SetVerticesDirty();
                foreach (Graphic graphic in graphics) if (graphic.isActiveAndEnabled) graphic.SetVerticesDirty();
                for (int canary = 0; canary < 4; canary++) { UpdateWorkload(result, canary, dynamic, effect, source); yield return null; ui.timingCanaryPositive |= timing.LastValue > 0; }
            }
            var times = new List<long>(30);
            var draws = new List<long>(30);
            for (int frame = 0; frame < 30; frame++)
            {
                UpdateWorkload(result, frame, dynamic, effect, source);
                yield return null;
                if (timing.Valid && ui.timingCanaryPositive) times.Add(timing.LastValue);
                if (drawCalls.Valid) draws.Add(drawCalls.LastValue);
            }
            ui.timingAvailable = timing.Valid && ui.timingCanaryPositive;
            result.globalDrawCallSamples = draws.ToArray();
            result.globalDrawCounterAvailable = draws.Count > 0 && draws.Exists(value => value > 0);
            if (result.globalDrawCounterAvailable) { draws.Sort(); result.medianGlobalDrawCalls = draws[draws.Count / 2]; }
            ui.timingReason = ui.timingAvailable ? "Aggregate Canvas.BuildBatch CPU across all active canvases, normal authored dirtiness after a positive canary. Scenario setters run outside the marker. No fixture-only CPU claim." : "Canvas.BuildBatch nanosecond recorder or positive rebuild canary unavailable.";
            ui.buildBatchNanoseconds = times.ToArray();
            if (times.Count > 0) { times.Sort(); ui.medianBuildBatchMs = times[times.Count / 2] * 1e-6; ui.p95BuildBatchMs = times[Mathf.CeilToInt(times.Count * .95f) - 1] * 1e-6; }
            ui.profilerRecordingEnabled = ProfilerDriver.enabled; ui.profilerFirstFrame = ProfilerDriver.firstFrameIndex; ui.profilerLastFrame = ProfilerDriver.lastFrameIndex;
            ui.profilerFrameAdvanced = ui.profilerLastFrame > initialFrame; ui.profilerConnectionId = ProfilerDriver.connectedProfiler;
            ui.profilerConnection = ProfilerDriver.GetConnectionIdentifier(ui.profilerConnectionId);
            // Inventories, reflection and PNG readback are outside the marker sample window.
            result.normalMaximumHpPreserved = true; result.placementStable = true;
            foreach (Actor actor in actors)
            {
                if (actor.root == null) throw new InvalidOperationException("A panel-owned actor disappeared during measurement.");
                actor.position = actor.root.transform.position;
                actor.hp = actor.attributes.GetAttributeValue(actor.health); actor.maxHp = actor.attributes.GetAttributeValue(actor.maximum);
                result.normalMaximumHpPreserved &= actor.initialMaxHp > 0 && Mathf.Approximately(actor.initialMaxHp, actor.maxHp) && actor.hp > 0 && actor.hp <= actor.maxHp;
                result.placementStable &= Vector2.Distance(actor.position, actor.requestedPosition) < .1f;
                actor.visible = VisibleHealth(actor, camera, out Rect bounds); actor.viewportBounds = bounds;
                if (actor.visible) result.visibleActors++;
                Canvas canvas = actor.view.GetComponent<Canvas>(); actor.canvasId = canvas != null ? canvas.GetInstanceID() : 0;
                var mesh = ReadField(actor.view, "meshHealthBar") as Component;
                MeshRenderer renderer = mesh != null ? mesh.GetComponent<MeshRenderer>() : null;
                actor.meshProjection = actor.visible && renderer != null && renderer.enabled;
                var bar = ReadField(actor.view, "healthBar") as RectTransform;
                actor.legacyProjection = actor.visible && canvas != null && canvas.enabled && bar != null &&
                    Array.Exists(bar.GetComponentsInChildren<Image>(true), image => image.isActiveAndEnabled);
                if (actor.meshProjection) result.meshProjectionActors++;
                if (actor.legacyProjection) result.legacyProjectionActors++;
                if (actor.statuses != null && actor.statuses.TryGetActive("Electrocuted", out IMonsterStatusSource status))
                { result.activeStatusActors++; actor.electrocutedSeconds = status.DisplayValue; }
                foreach (ParticleSystem particles in actor.root.GetComponentsInChildren<ParticleSystem>(true))
                    if (particles.isPlaying || particles.particleCount > 0) result.worldParticleSystems++;
            }
            if (!result.normalMaximumHpPreserved || !result.placementStable || result.visibleActors != count)
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, label + "-invalid.json"), JsonUtility.ToJson(result, true));
                throw new InvalidOperationException($"Fixture HP={result.normalMaximumHpPreserved}, placement={result.placementStable}, visible={result.visibleActors}/{count} failed; invalid diagnostics are not batch evidence.");
            }
            if (dynamic && result.activeStatusActors != result.requestedStatusActors)
                throw new InvalidOperationException("The real authored status projection is not active on the requested actors.");
            if (result.meshProjectionActors + result.legacyProjectionActors != count ||
                (overlap && result.legacyProjectionActors != count))
                throw new InvalidOperationException("Every visible actor must use exactly one measured HP projection; overlap must retain the original Canvas sorting path.");
            Runner("SnapshotGraphics", gameplayHud.gameObject, graphics, ui);
            CaptureNativeSet(graphics, ui, Path.Combine(output, label + ".json"));
            ui.status = ui.nativeUiDataAvailable ? "captured" : "unavailable";
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, label + ".json"), JsonUtility.ToJson(result, true));
            Debug.Log($"UIBATCH_STRUCTURAL,{label},{variant},visible={result.visibleActors}/{count},native={ui.nativeSelectedBatches},status={ui.status}");
            if (!dynamic && Arg("-uiBatchGpu", "false") == "true")
                yield return MonsterHealthBarGpuValidation.CapturePair(camera,
                    actors.ConvertAll(actor => actor.view), variant, label, Path.Combine(output, "gpu"));
        }
        finally
        {
            timing.Dispose();
            drawCalls.Dispose();
            foreach (Actor actor in actors) if (actor.root != null)
            {
                if (dynamic && actor.effects != null && actor.effects.HasActiveEffect(effect, actor.root)) actor.effects.RemoveEffect(effect, actor.root);
                actor.attributes.TrySetCurrentValue(actor.health, actor.initialHp, actor.root);
            }
            ProfilerDriver.SetAreaEnabled(ProfilerArea.UI, oldDriverUi); ProfilerDriver.SetAreaEnabled(ProfilerArea.UIDetails, oldDriverDetails);
            ProfilerDriver.profileEditor = oldEditor; ProfilerDriver.enabled = oldRecording;
            Profiler.SetAreaEnabled(ProfilerArea.UI, oldUi); Profiler.SetAreaEnabled(ProfilerArea.UIDetails, oldDetails); Profiler.enabled = oldRuntime;
        }
    }

    static void UpdateWorkload(Scenario scenario, int frame, bool dynamic, GameplayEffect effect, GameObject source)
    {
        if (!dynamic) return;
        for (int i = 0; i < Mathf.Min(10, scenario.actors.Count); i++)
        {
            Actor actor = scenario.actors[i];
            if (frame % 6 == 0 && actor.attributes.TrySetCurrentValue(actor.health, actor.initialMaxHp * (frame % 12 == 0 ? .65f : .9f), actor.root)) scenario.hpWriteEvents++;
            if (i < scenario.requestedStatusActors)
            {
                if (actor.effects == null || actor.statuses == null) throw new InvalidOperationException("The authored monster effect/status components are missing.");
                if (actor.effects.GetRemainingTime(effect, actor.root) < 2f) actor.effects.ApplyEffect(effect, actor.root, source);
            }
        }
    }

    static Graphic[] HudGraphics(List<Actor> actors)
    { var graphics = new List<Graphic>(); foreach (Actor actor in actors) graphics.AddRange(actor.view.GetComponentsInChildren<Graphic>(true)); return graphics.ToArray(); }
    static bool VisibleHealth(Actor actor, Camera camera, out Rect bounds)
    {
        bounds = new Rect();
        RectTransform health = ReadField(actor.view, "healthBar") as RectTransform;
        if (health == null || !health.gameObject.activeInHierarchy) return false;
        var corners = new Vector3[4]; health.GetWorldCorners(corners);
        Vector2 minimum = Vector2.one * float.PositiveInfinity, maximum = Vector2.one * float.NegativeInfinity;
        foreach (Vector3 corner in corners)
        { Vector3 viewport = camera.WorldToViewportPoint(corner); if (viewport.z <= camera.nearClipPlane || viewport.z >= camera.farClipPlane) return false; minimum = Vector2.Min(minimum, viewport); maximum = Vector2.Max(maximum, viewport); }
        bounds = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        if (minimum.x < 0 || minimum.y < 0 || maximum.x > 1 || maximum.y > 1) return false;
        var mesh = ReadField(actor.view, "meshHealthBar") as MonsterHealthBarMeshView;
        if (mesh != null && mesh.IsConfigured)
        {
            MeshRenderer renderer = mesh.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy &&
                renderer.sharedMaterial != null && mesh.GetComponent<MeshFilter>()?.sharedMesh != null &&
                (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0) return true;
            // Overlapping HUDs deliberately use the original Images instead.
        }
        foreach (Graphic graphic in health.GetComponentsInChildren<Graphic>())
            if (graphic.isActiveAndEnabled && !graphic.canvasRenderer.cull && graphic.color.a > 0 && graphic.canvasRenderer.GetAlpha() > 0 &&
                (camera.cullingMask & (1 << graphic.gameObject.layer)) != 0) return true;
        return false;
    }

    static void CaptureNativeSet(Graphic[] graphics, UiBatchingValidationRunner.Report report, string output)
    {
        if (!report.profilerFrameAdvanced) { report.reason = "Editor Profiler frame history did not advance; native crowd batches unavailable."; return; }
        Type type = typeof(ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerProperty");
        if (type == null) { report.reason = "Native UI Profiler API unavailable."; return; }
        object property = null;
        try
        {
            property = Activator.CreateInstance(type);
            report.frameIndex = Mathf.Max(report.profilerFirstFrame, report.profilerLastFrame - 1);
            type.GetMethod("SetRoot", Members).Invoke(property, new object[] { report.frameIndex, 0, 0 });
            Array infos = type.GetMethod("GetUISystemProfilerInfo", Members)?.Invoke(property, null) as Array;
            Array ids = (type.GetMethod("GetUISystemBatchEntityIds", Members) ?? type.GetMethod("GetUISystemBatchInstanceIDs", Members))?.Invoke(property, null) as Array;
            if (infos == null || infos.Length == 0 || ids == null) { report.reason = "Completed frame contains no native UI details/membership; unavailable is not zero."; return; }
            var selected = new HashSet<GameObject>(); var canvases = new HashSet<GameObject>();
            foreach (Graphic graphic in graphics) { selected.Add(graphic.gameObject); if (graphic.canvas != null) canvases.Add(graphic.canvas.gameObject); }
            report.nativeMembersAvailable = true; report.nativeSelectedBatches = 0; report.nativeMixedBatches = 0;
            UiBatchingValidationRunner.CanvasInfo preview = null;
            foreach (object info in infos)
            {
                string name = type.GetMethod("GetUISystemProfilerNameByOffset", Members)?.Invoke(property, new object[] { Number(info, "objectNameOffset") }) as string ?? "unknown";
                if (!Convert.ToBoolean(Read(info, "isBatch") ?? false))
                {
                    GameObject owner = GameObjectOf(Resolve(Read(info, "objectEntityId", "objectInstanceId")));
                    if (owner == null || !canvases.Contains(owner)) continue;
                    var canvas = new UiBatchingValidationRunner.CanvasInfo { name = name, path = Hierarchy(owner.transform), instanceId = owner.GetInstanceID(),
                        directBatchCount = Number(info, "batchCount"), totalBatchCount = Number(info, "totalBatchCount"), vertexCount = Number(info, "totalVertexCount"),
                        renderDataIndex = Number(info, "renderDataIndex"), renderDataCount = Number(info, "renderDataCount") };
                    report.canvases.Add(canvas); if (preview == null) preview = canvas; continue;
                }
                int index = Number(info, "entityIdsIndex", "instanceIDsIndex"), count = Number(info, "entityIdsCount", "instanceIDsCount");
                var batch = new UiBatchingValidationRunner.BatchInfo { name = name, totalMembers = count, vertexCount = Number(info, "vertexCount"),
                    breakingReason = Read(info, "batchBreakingReason")?.ToString() ?? "unknown", renderDataIndex = Number(info, "renderDataIndex"), renderDataCount = Number(info, "renderDataCount") };
                for (int at = index; at >= 0 && at < index + count && at < ids.Length; at++)
                { GameObject member = GameObjectOf(Resolve(ids.GetValue(at))); if (member != null && selected.Contains(member)) { batch.selectedMembers++; batch.graphicPaths.Add(Hierarchy(member.transform)); } }
                if (batch.selectedMembers == 0) continue;
                batch.mixedWithOtherFixture = batch.selectedMembers != batch.totalMembers;
                report.batches.Add(batch); report.nativeSelectedBatches++; if (batch.mixedWithOtherFixture) report.nativeMixedBatches++;
            }
            bool anySubmittedHudGraphic = Array.Exists(graphics, graphic => graphic != null &&
                graphic.isActiveAndEnabled && graphic.canvas != null && graphic.canvas.isActiveAndEnabled && !graphic.canvasRenderer.cull);
            report.nativeUiDataAvailable = !anySubmittedHudGraphic || (report.canvases.Count > 0 && report.nativeSelectedBatches > 0);
            report.reason = report.nativeUiDataAvailable ? "One native frame; batches selected once by actual HUD graphic membership, mixed batches marked. Zero-actor scenario has an explicitly empty HUD set." : "Native details exist but no actual HUD memberships matched; crowd batches unavailable.";
            if (!report.nativeUiDataAvailable) { report.nativeSelectedBatches = -1; report.nativeMixedBatches = -1; }
            if (preview != null && preview.renderDataCount > 0) Runner("CaptureProfilerPixels", type, preview, report, output);
        }
        catch (Exception exception) { report.nativeUiDataAvailable = false; report.nativeSelectedBatches = -1; report.reason = "Native UI details unavailable: " + exception.GetBaseException().Message; }
        finally { (property as IDisposable)?.Dispose(); }
    }

    static object Runner(string method, params object[] arguments) => (typeof(UiBatchingValidationRunner).GetMethod(method, Members) ?? throw new MissingMethodException(method)).Invoke(null, arguments);
    static object ReadField(object target, string name) => target.GetType().GetField(name, Fields)?.GetValue(target);
    static object Read(object target, params string[] names)
    { foreach (string name in names) { FieldInfo field = target.GetType().GetField(name, Members); if (field != null) return field.GetValue(target); } return null; }
    static int Number(object target, params string[] names) => Convert.ToInt32(Read(target, names) ?? 0);
    static Object Resolve(object id)
    { if (id == null) return null; if (id is int integer) return EditorUtility.InstanceIDToObject(integer); return typeof(EditorUtility).GetMethod("EntityIdToObject", Members, null, new[] { id.GetType() }, null)?.Invoke(null, new[] { id }) as Object; }
    static GameObject GameObjectOf(Object value) => value as GameObject ?? (value as Component)?.gameObject;
    static string Hierarchy(Transform transform) { string path = transform.name; while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; } return path; }
    static string Arg(string name, string fallback) { string[] args = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1]; return fallback; }

    sealed class NeutralInput : IInputActionQueryBackend
    {
        public PlayerInteractor2D player; public IInputActionQueryBackend original;
        public Component BackendComponent => player;
        public Sprite GetBindingIcon(InputActionId action) => original?.GetBindingIcon(action);
        public bool WasPressedThisFrame(InputActionId action) => false;
        public bool WasReleasedThisFrame(InputActionId action) => false;
        public bool IsPressed(InputActionId action) => false;
        public bool IsKeyPressed(KeyCode key) => false;
        public bool WasKeyPressedThisFrame(KeyCode key) => false;
        public bool WasKeyReleasedThisFrame(KeyCode key) => false;
        public Vector2 GetMoveVectorRaw() => Vector2.zero;
        public Vector2 GetMoveVectorNormalized() => Vector2.zero;
        public Vector3 GetPointerWorldPosition(Camera camera, float z = 0) => player.transform.position + Vector3.right * 5;
    }
}
#endif
