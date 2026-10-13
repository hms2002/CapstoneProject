#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityGAS;
using Stopwatch = System.Diagnostics.Stopwatch;
using Object = UnityEngine.Object;

/// <summary>Controlled operation A/B measurements only. Does not estimate complete frame time.</summary>
public static class FramePerformanceCpuBenchmarks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string SnapshotRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "SessionLogs",
        "2026-10-07-frame-performance-measurement", "original-operation-sources"));
    private const string RuntimeRoot = "Assets/_Project/Runtime/";
    private const int RecorderCapacity = 65536;
    private static int sink;

    [Serializable]
    public sealed class Report
    {
        public string unityVersion;
        public string environment;
        public string method;
        public string physicsCounters;
        public string baselineImplementation;
        public int warmup;
        public int samples;
        public int batchRepeats;
        public bool correctnessPassed = true;
        public AllocationCalibration allocation;
        public AllocationEventCalibration allocationEvents;
        public Source[] baselineSources;
        public List<Result> results = new();
    }

    [Serializable]
    public sealed class Source { public string path; public string sha256; }

    [Serializable]
    public sealed class AllocationCalibration
    {
        public bool available;
        public string method;
        public string reason;
        public long noAllocationBytes;
        public long canary4096Bytes;
        public long canary8192Bytes;
        public int canaryIterations;
    }

    [Serializable]
    public sealed class AllocationEventCalibration
    {
        public bool available;
        public string method;
        public string nativeUnit;
        public string reason;
        public long noWorkEvents;
        public long canary4096Events;
        public long canary8192Events;
        public int canaryIterations;
    }

    [Serializable]
    public sealed class Result
    {
        public string operation;
        public string scope;
        public int mobs;
        public int entries;
        public bool correct;
        public int baselineChecksum;
        public int optimizedChecksum;
        public double baselineMedianMs;
        public double baselineP95Ms;
        public double optimizedMedianMs;
        public double optimizedP95Ms;
        public double baselineSightRaycastsPerOperation;
        public double optimizedSightRaycastsPerOperation;
        public double baselineOverlapCallsPerOperation;
        public double optimizedOverlapCallsPerOperation;
        public bool allocationAvailable;
        public string allocationReason;
        public double baselineMedianAllocationBytesPerOperation = -1;
        public double baselineP95AllocationBytesPerOperation = -1;
        public double optimizedMedianAllocationBytesPerOperation = -1;
        public double optimizedP95AllocationBytesPerOperation = -1;
        public bool allocationEventsAvailable;
        public string allocationEventsReason;
        public double baselineMedianAllocationEventsPerOperation = -1;
        public double baselineP95AllocationEventsPerOperation = -1;
        public double optimizedMedianAllocationEventsPerOperation = -1;
        public double optimizedP95AllocationEventsPerOperation = -1;
        public long[] baselineRawAllocationEvents = Array.Empty<long>();
        public long[] optimizedRawAllocationEvents = Array.Empty<long>();
    }

    public static string Run() => RunWithOptions(16, 61, 8);

    public static string RunWithOptions(int warmup, int samples, int batchRepeats)
    {
        if (!EditorApplication.isPlaying)
            throw new InvalidOperationException("Run CPU fixtures in an isolated Play Mode scene.");
        if (warmup < 1 || samples < 9 || batchRepeats < 1)
            throw new ArgumentOutOfRangeException("Use at least one warmup, nine samples and one batch repeat.");
        var report = new Report
        {
            unityVersion = Application.unityVersion,
            environment = "Unity Editor Play Mode; synchronous controlled native Physics2D fixtures; no frame extrapolation",
            method = "Warm both variants; alternating AB/BA Stopwatch batches; median/p95 ms per one operation over all configured mobs. Allocation capture is separate from timing.",
            physicsCounters = "Sight rays observed by one pass-through ICombatPathBlocker2D on each ray; native overlap calls counted at their API callsite. These are query counts, not physics simulation CPU counters.",
            baselineImplementation = "Detection/lock algorithms and four repository LINQ lookup closures extracted from the retained source hashes below; private detection helpers bound as cached delegates before measurement. FSM busy path matches the pre-change HEAD Chase/attack guard order (FSM files were not in the initial snapshot). Flowering measures only the original native All query versus the current native List query, not complete attack cost.",
            warmup = warmup, samples = samples, batchRepeats = batchRepeats,
            baselineSources = ReadBaselineSources(), allocation = CalibrateAllocation(), allocationEvents = CalibrateAllocationEvents()
        };
        UnityEngine.Random.State originalRandom = UnityEngine.Random.state;
        try
        {
            foreach (int count in new[] { 10, 30, 60 })
            {
                using var fixture = new Fixture(count);
                fixture.SetTargetsOutsideRange();
                report.results.Add(Measure("outside_range_detection", "IsTargetWithinDetectionRange over all mobs; restored original includes Editor log argument construction", fixture,
                    fixture.BaselineDetection, fixture.OptimizedDetection, warmup, samples, batchRepeats, report.allocation, report.allocationEvents));
                fixture.SetTargetsInsideRange();
                report.results.Add(Measure("chase_busy_tick", "Original/current Chase Tick with busy bridge; each real actor uses its original/current detection implementation", fixture,
                    fixture.BaselineFsm, fixture.OptimizedFsm, warmup, samples, batchRepeats, report.allocation, report.allocationEvents));
                fixture.ValidateLockDeaths();
                report.results.Add(Measure("kill_lock_alive_poll", "One HasAliveMember call per single-member lock unit; cached delegates remove reflection invocation overhead from both variants", fixture,
                    fixture.BaselineLock, fixture.OptimizedLock, warmup, samples, batchRepeats, report.allocation, report.allocationEvents));
                fixture.ValidateOverlapOrder();
                report.results.Add(Measure("flowering_raw_overlap_query", "Only the exact old All/current List native overlap query, excluding hit resolution, damage, spawning and presentation", fixture,
                    fixture.BaselineOverlap, fixture.OptimizedOverlap, warmup, samples, batchRepeats, report.allocation, report.allocationEvents));
            }
            foreach (int count in new[] { 32, 128 })
                foreach (bool match in new[] { true, false })
                {
                    using var fixture = new EffectLookupFixture(count, match);
                    fixture.Validate();
                    Result result = Measure("effect_repository_" + (match ? "last_match" : "miss"),
                        "Four read-only lookups (FindFirst/HasActive, two- and three-parameter overloads) over identical effect references; original LINQ closures versus actual ordered loops. Setup is outside timing; removal/tick is unchanged and excluded.",
                        null, fixture.Baseline, fixture.Optimized, warmup, samples, batchRepeats, report.allocation, report.allocationEvents);
                    result.entries = count;
                    report.results.Add(result);
                }
            foreach (Result result in report.results) report.correctnessPassed &= result.correct;
            return JsonUtility.ToJson(report, true);
        }
        finally { UnityEngine.Random.state = originalRandom; }
    }

    private static Result Measure(string operation, string scope, Fixture fixture, Func<int> baseline,
        Func<int> optimized, int warmup, int samples, int repeats, AllocationCalibration calibration, AllocationEventCalibration eventCalibration)
    {
        for (int i = 0; i < warmup; i++) { sink = baseline(); sink = optimized(); }
        fixture?.ResetCounters();
        int oldResult = baseline();
        int oldRays = fixture?.SightQueries ?? 0, oldOverlaps = fixture?.OverlapQueries ?? 0;
        fixture?.ResetCounters();
        int newResult = optimized();
        int newRays = fixture?.SightQueries ?? 0, newOverlaps = fixture?.OverlapQueries ?? 0;
        if (oldResult != newResult) throw new InvalidOperationException(operation + " correctness mismatch.");
        if (operation == "outside_range_detection" && (oldRays != fixture.Count || newRays != 0))
            throw new InvalidOperationException("Range fixture did not observe the expected native rays.");
        if (operation == "chase_busy_tick" && (oldRays != fixture.Count * 2 || newRays != fixture.Count))
            throw new InvalidOperationException("FSM fixture did not observe the expected native rays.");
        if (operation == "flowering_raw_overlap_query" && (oldOverlaps != 1 || newOverlaps != 1))
            throw new InvalidOperationException("Overlap fixture did not issue one native query per variant.");
        var oldTimes = new double[samples];
        var newTimes = new double[samples];
        for (int i = 0; i < samples; i++)
        {
            if ((i & 1) == 0) { oldTimes[i] = TimeBatch(baseline, repeats); newTimes[i] = TimeBatch(optimized, repeats); }
            else { newTimes[i] = TimeBatch(optimized, repeats); oldTimes[i] = TimeBatch(baseline, repeats); }
        }
        var result = new Result
        {
            operation = operation, scope = scope, mobs = fixture?.Count ?? 0, correct = true,
            baselineChecksum = oldResult, optimizedChecksum = newResult,
            baselineMedianMs = Percentile(oldTimes, 0.5), baselineP95Ms = Percentile(oldTimes, 0.95),
            optimizedMedianMs = Percentile(newTimes, 0.5), optimizedP95Ms = Percentile(newTimes, 0.95),
            baselineSightRaycastsPerOperation = oldRays, optimizedSightRaycastsPerOperation = newRays,
            baselineOverlapCallsPerOperation = oldOverlaps, optimizedOverlapCallsPerOperation = newOverlaps,
            allocationAvailable = calibration.available, allocationReason = calibration.reason,
            allocationEventsAvailable = eventCalibration.available, allocationEventsReason = eventCalibration.reason
        };
        if (calibration.available)
        {
            var oldBytes = new double[9];
            var newBytes = new double[9];
            for (int i = 0; i < oldBytes.Length; i++)
            {
                if ((i & 1) == 0)
                {
                    oldBytes[i] = CaptureAllocation(baseline, repeats, out string a, out _);
                    newBytes[i] = CaptureAllocation(optimized, repeats, out string b, out _);
                    if (a != null || b != null) { result.allocationAvailable = false; result.allocationReason = a ?? b; break; }
                }
                else
                {
                    newBytes[i] = CaptureAllocation(optimized, repeats, out string a, out _);
                    oldBytes[i] = CaptureAllocation(baseline, repeats, out string b, out _);
                    if (a != null || b != null) { result.allocationAvailable = false; result.allocationReason = a ?? b; break; }
                }
                oldBytes[i] /= repeats;
                newBytes[i] /= repeats;
            }
            if (result.allocationAvailable)
            {
                result.baselineMedianAllocationBytesPerOperation = Percentile(oldBytes, 0.5);
                result.baselineP95AllocationBytesPerOperation = Percentile(oldBytes, 0.95);
                result.optimizedMedianAllocationBytesPerOperation = Percentile(newBytes, 0.5);
                result.optimizedP95AllocationBytesPerOperation = Percentile(newBytes, 0.95);
            }
        }
        if (eventCalibration.available)
        {
            result.baselineRawAllocationEvents = new long[9];
            result.optimizedRawAllocationEvents = new long[9];
            var oldEvents = new double[9];
            var newEvents = new double[9];
            for (int i = 0; i < oldEvents.Length; i++)
            {
                string a, b;
                if ((i & 1) == 0)
                {
                    result.baselineRawAllocationEvents[i] = CaptureAllocationEvents(baseline, repeats, out a, out _);
                    result.optimizedRawAllocationEvents[i] = CaptureAllocationEvents(optimized, repeats, out b, out _);
                }
                else
                {
                    result.optimizedRawAllocationEvents[i] = CaptureAllocationEvents(optimized, repeats, out a, out _);
                    result.baselineRawAllocationEvents[i] = CaptureAllocationEvents(baseline, repeats, out b, out _);
                }
                if (a != null || b != null)
                { result.allocationEventsAvailable = false; result.allocationEventsReason = a ?? b; break; }
                oldEvents[i] = (double)result.baselineRawAllocationEvents[i] / repeats;
                newEvents[i] = (double)result.optimizedRawAllocationEvents[i] / repeats;
            }
            if (result.allocationEventsAvailable)
            {
                result.baselineMedianAllocationEventsPerOperation = Percentile(oldEvents, 0.5);
                result.baselineP95AllocationEventsPerOperation = Percentile(oldEvents, 0.95);
                result.optimizedMedianAllocationEventsPerOperation = Percentile(newEvents, 0.5);
                result.optimizedP95AllocationEventsPerOperation = Percentile(newEvents, 0.95);
            }
        }
        return result;
    }

    private static double TimeBatch(Func<int> operation, int repeats)
    {
        long start = Stopwatch.GetTimestamp();
        int checksum = 0;
        for (int i = 0; i < repeats; i++) checksum += operation();
        long elapsed = Stopwatch.GetTimestamp() - start;
        sink = checksum;
        return elapsed * (1000d / Stopwatch.Frequency) / repeats;
    }

    private static double Percentile(double[] values, double percentile)
    {
        Array.Sort(values);
        return values[Math.Min(values.Length - 1, Math.Max(0, (int)Math.Ceiling(values.Length * percentile) - 1))];
    }

    private static AllocationCalibration CalibrateAllocation()
    {
        var result = new AllocationCalibration
        {
            method = "Native ProfilerRecorder Memory/GC.Alloc; current thread, individual events, no per-frame aggregation. Sum event values only after Stop. No GC.GetAllocatedBytesForCurrentThread or total-memory growth.",
            canaryIterations = 32
        };
        Func<int> empty = () => ++sink;
        Func<int> small = () => { var bytes = new byte[4096]; bytes[0] = 1; GC.KeepAlive(bytes); return bytes.Length; };
        Func<int> large = () => { var bytes = new byte[8192]; bytes[0] = 1; GC.KeepAlive(bytes); return bytes.Length; };
        sink = empty(); sink = small(); sink = large();
        result.noAllocationBytes = CaptureAllocation(empty, 32, out string noReason, out _);
        result.canary4096Bytes = CaptureAllocation(small, 32, out string smallReason, out int smallEvents);
        result.canary8192Bytes = CaptureAllocation(large, 32, out string largeReason, out int largeEvents);
        result.reason = noReason ?? smallReason ?? largeReason;
        long smallPayload = 4096L * 32, largePayload = 8192L * 32;
        bool plausible = result.noAllocationBytes >= 0 && result.noAllocationBytes <= 256 &&
            result.canary4096Bytes >= smallPayload && result.canary4096Bytes <= smallPayload + 128 * 32 &&
            result.canary8192Bytes >= largePayload && result.canary8192Bytes <= largePayload + 128 * 32 &&
            Math.Abs((result.canary8192Bytes - result.canary4096Bytes) - smallPayload) <= 256 &&
            smallEvents >= 32 && largeEvents >= 32;
        result.available = result.reason == null && plausible;
        if (!result.available && result.reason == null) result.reason = "Native GC.Alloc canary/no-allocation calibration failed; byte figures withheld.";
        if (result.available) result.reason = "Calibrated against 32 x 4KB/8KB arrays and a no-allocation control.";
        return result;
    }

    private static long CaptureAllocation(Func<int> operation, int repeats, out string reason, out int events)
    {
        reason = null; events = 0;
        try
        {
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", RecorderCapacity,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread | ProfilerRecorderOptions.StartImmediately);
            if (!recorder.Valid) { reason = "Native GC.Alloc marker unavailable."; return -1; }
            if (recorder.UnitType != ProfilerMarkerDataUnit.Bytes)
            { reason = "Native GC.Alloc unit is not Bytes; byte figures withheld."; return -1; }
            recorder.Reset();
            for (int i = 0; i < repeats; i++) sink = operation();
            recorder.Stop();
            if (recorder.WrappedAround || recorder.Count >= RecorderCapacity)
            { reason = "Native GC.Alloc recorder capacity reached; byte figures withheld."; return -1; }
            var samples = new List<ProfilerRecorderSample>(recorder.Count);
            recorder.CopyTo(samples);
            long bytes = 0;
            foreach (ProfilerRecorderSample sample in samples) { bytes += sample.Value; events += (int)sample.Count; }
            return bytes;
        }
        catch (Exception error) { reason = "Native GC.Alloc unavailable: " + error.GetType().Name; return -1; }
    }

    private static AllocationEventCalibration CalibrateAllocationEvents()
    {
        var calibration = new AllocationEventCalibration
        {
            method = "Native GC.Alloc current-thread individual marker sample.Count totals only; raw timing Value is never reinterpreted as bytes. Nine separate capture windows per result; raw counts are per batch.",
            canaryIterations = 32
        };
        Func<int> empty = () => ++sink;
        Func<int> small = () => { var bytes = new byte[4096]; bytes[0] = 1; GC.KeepAlive(bytes); return bytes.Length; };
        Func<int> large = () => { var bytes = new byte[8192]; bytes[0] = 1; GC.KeepAlive(bytes); return bytes.Length; };
        sink = empty(); sink = small(); sink = large();
        calibration.noWorkEvents = CaptureAllocationEvents(empty, 32, out string noReason, out string emptyUnit);
        calibration.canary4096Events = CaptureAllocationEvents(small, 32, out string smallReason, out string smallUnit);
        calibration.canary8192Events = CaptureAllocationEvents(large, 32, out string largeReason, out string largeUnit);
        calibration.nativeUnit = smallUnit;
        calibration.reason = noReason ?? smallReason ?? largeReason;
        calibration.available = calibration.reason == null && emptyUnit == smallUnit && smallUnit == largeUnit &&
            calibration.noWorkEvents == 0 && calibration.canary4096Events == 32 && calibration.canary8192Events == 32;
        if (calibration.available)
            calibration.reason = "No-work control produced 0 events; both 32-array controls produced exactly 32 GC.Alloc events. Counts describe allocation events, not bytes or surviving objects.";
        else if (calibration.reason == null)
            calibration.reason = "Native GC.Alloc event canary/no-work calibration failed; event figures withheld.";
        return calibration;
    }

    private static long CaptureAllocationEvents(Func<int> operation, int repeats, out string reason, out string unit)
    {
        reason = null; unit = "unavailable";
        try
        {
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", RecorderCapacity,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread | ProfilerRecorderOptions.StartImmediately);
            if (!recorder.Valid) { reason = "Native GC.Alloc marker unavailable."; return -1; }
            unit = recorder.UnitType.ToString();
            recorder.Reset();
            for (int i = 0; i < repeats; i++) sink = operation();
            recorder.Stop();
            if (recorder.WrappedAround || recorder.Count >= RecorderCapacity)
            { reason = "Native GC.Alloc event recorder capacity reached; counts withheld."; return -1; }
            long events = 0;
            for (int i = 0; i < recorder.Count; i++) events += recorder.GetSample(i).Count;
            return events;
        }
        catch (Exception error) { reason = "Native GC.Alloc event capture unavailable: " + error.GetType().Name; return -1; }
    }

    private static Source[] ReadBaselineSources()
    {
        string[] paths =
        {
            RuntimeRoot + "Features/Monsters/Common/EnemyChaseIntent2D.cs",
            RuntimeRoot + "Features/Monsters/Common/Mob.cs",
            RuntimeRoot + "Features/Items/Weapons/Flowering/FloweringDashSlashHitboxActor.cs",
            RuntimeRoot + "Core/Effects/ActiveGameplayEffectRepository.cs"
        };
        var sources = new Source[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            string path = Path.Combine(SnapshotRoot, paths[i] + ".txt");
            byte[] contents = File.ReadAllBytes(path);
            using var sha = SHA256.Create();
            sources[i] = new Source { path = path, sha256 = BitConverter.ToString(sha.ComputeHash(contents)).Replace("-", "").ToLowerInvariant() };
        }
        return sources;
    }

    private sealed class Detection
    {
        public readonly Mob Owner;
        public readonly EnemyChaseIntent2D Chase;
        private readonly Func<bool> ignoreRange;
        private readonly Action acquire;
        private readonly Action<string> log;
        public Detection(Mob owner)
        {
            Owner = owner; Chase = owner.GetComponent<EnemyChaseIntent2D>();
            ignoreRange = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), Chase,
                typeof(EnemyChaseIntent2D).GetMethod("CanIgnoreDetectionRange", Private));
            acquire = (Action)Delegate.CreateDelegate(typeof(Action), Chase,
                typeof(EnemyChaseIntent2D).GetMethod("TryAcquireMissingTarget", Private));
            log = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), Chase,
                typeof(EnemyChaseIntent2D).GetMethod("LogChaseThrottled", Private));
        }
        // Restored snapshot's complete IsTargetWithinDetectionRange decision/log order.
        public bool Baseline()
        {
            if (Owner == null || Owner.Target == null) acquire();
            if (Owner == null || Owner.Target == null) { log("감지 실패: enemy 또는 target이 없습니다."); return false; }
            if (!Owner.CanPerceiveTarget(Owner.Target)) { log("Detection failed: closed door blocks target perception."); return false; }
            Vector2 toTarget = (Vector2)(Owner.Target.position - Owner.transform.position);
            float sqrDistance = toTarget.sqrMagnitude;
            float range = Chase.DetectionRange;
            if (!ignoreRange() && sqrDistance > range * range)
            { log($"감지 실패: 범위 밖입니다. distance={Mathf.Sqrt(sqrDistance):0.00}, detectionRange={range:0.00}"); return false; }
            log($"감지 성공. distance={Mathf.Sqrt(sqrDistance):0.00}, detectionRange={range:0.00}");
            return true;
        }
    }

    private sealed class OriginalChaseIntent : IEnemyChaseIntent
    {
        private readonly Detection detection;
        public OriginalChaseIntent(Detection value) => detection = value;
        public bool IsTargetWithinDetectionRange() => detection.Baseline();
        public void StartChase() => detection.Chase.StartChase();
        public void StopChase() => detection.Chase.StopChase();
    }

    private sealed class BusyBridge : IMobAbilityBridge
    {
        public bool IsAbilityExecutionBusy => true;
        public bool IsAbilityExecutionSuppressed => false;
        public bool TryStartAbility(AbilityDefinition ability, GameObject target = null) => false;
        public void CancelActiveAbility(bool force) { }
        public bool HasStateTag(GameplayTag tag) => false;
        public bool TryBeginRunner(IMobPatternRunner runner) => false;
        public void EndRunner(IMobPatternRunner runner) { }
    }

    private sealed class NoDecision : IMobAttackDecisionSource
    {
        public bool TryBuildAttackRequest(out MobAttackRequest request) { request = default; return false; }
        public void OnAttackStateEntered(MobAttackRequest request) { }
        public void OnAttackStateExited(MobAttackRequest request, bool wasCancelled) { }
    }

    private static bool OriginalAttackTransition(MobStateMachine machine, MobAIContext context)
    {
        if (context == null || context.Owner == null || context.Owner.IsDead) return false;
        if (!context.HasDetectedTarget()) return false;
        if (context.AbilityBridge == null || context.AttackDecisionSource == null) return false;
        if (context.AbilityBridge.IsAbilityExecutionBusy) return false;
        if (!context.ConsumeAttackThinkBudget()) return false;
        // This fixture deliberately stops at the original busy guard before custom decisions.
        throw new InvalidOperationException("The controlled original busy guard was not reached.");
    }

    private sealed class OriginalChaseState : IMobState
    {
        public void Enter(MobStateMachine machine, MobAIContext context)
        {
            context?.Owner?.LogFsmDebug("Chase Enter.");
            context?.ChaseIntent?.StartChase();
        }
        public void Tick(MobStateMachine machine, MobAIContext context)
        {
            if (MobStateTransitionUtility.TryHandleStaggerTransition(machine, context)) return;
            if (OriginalAttackTransition(machine, context)) return;
            if (context == null || !context.HasDetectedTarget())
            {
                context?.Owner?.LogFsmDebug("Chase -> Idle 전이. 타겟 감지 실패.");
                machine.ChangeState(new MobIdleState(), context);
            }
        }
        public void Exit(MobStateMachine machine, MobAIContext context)
        {
            context?.Owner?.LogFsmDebug("Chase Exit.");
            context?.ChaseIntent?.StopChase();
        }
    }

    // Exact restored single-member source algorithm, with per-unit GameObject ownership.
    private sealed class OriginalLockUnit
    {
        private readonly List<GameObject> members = new();
        public void AddMember(GameObject member)
        {
            if (member == null) return;
            Compact();
            for (int i = 0; i < members.Count; i++) if (members[i] == member) return;
            members.Add(member);
        }
        private void Compact()
        {
            for (int i = members.Count - 1; i >= 0; i--) if (members[i] == null) members.RemoveAt(i);
        }
        public bool HasAliveMember()
        {
            return TryGetAliveRepresentative(out _);
        }
        private bool TryGetAliveRepresentative(out GameObject representative)
        {
            Compact();
            for (int i = 0; i < members.Count; i++)
            {
                GameObject member = members[i];
                if (IsAliveMember(member)) { representative = member; return true; }
            }
            representative = null;
            return false;
        }
        private static bool IsAliveMember(GameObject member)
        {
            if (member == null) return false;
            Enemy enemy = member.GetComponent<Enemy>();
            if (enemy == null) enemy = member.GetComponentInChildren<Enemy>(includeInactive: true);
            return enemy == null || !enemy.IsDead;
        }
    }

    private sealed class EffectLookupFixture : IDisposable
    {
        private readonly GameObject root;
        private readonly GameObject target;
        private readonly Object source;
        private readonly GameplayEffect effect;
        private readonly GameplayEffect otherEffect;
        private readonly List<ActiveGameplayEffect> original = new();
        private readonly ActiveGameplayEffectRepository current = new();
        private readonly ActiveGameplayEffect expected;

        public EffectLookupFixture(int count, bool match)
        {
            root = new GameObject("Effect lookup fixture") { hideFlags = HideFlags.HideAndDontSave };
            target = new GameObject("Lookup target");
            target.transform.SetParent(root.transform, false);
            var sourceObject = new GameObject("Lookup source");
            sourceObject.transform.SetParent(root.transform, false);
            source = sourceObject;
            effect = ScriptableObject.CreateInstance<GE_StatusOnlyDuration>();
            otherEffect = ScriptableObject.CreateInstance<GE_StatusOnlyDuration>();
            effect.duration = otherEffect.duration = 10f;
            effect.hideFlags = otherEffect.hideFlags = HideFlags.HideAndDontSave;
            for (int i = 0; i < count; i++)
            {
                var entry = new ActiveGameplayEffect(match && i == count - 1 ? effect : otherEffect, target)
                    { SourceObject = source, TimeRemaining = 10f };
                original.Add(entry);
                current.Add(entry);
                if (match && i == count - 1) expected = entry;
            }
        }

        // The archived source's exact predicate guards/closures, including their captures.
        private ActiveGameplayEffect OriginalFind(GameplayEffect requestedEffect, GameObject requestedTarget)
        {
            if (requestedEffect == null || requestedTarget == null) return null;
            return original.FirstOrDefault(e => e.Effect == requestedEffect && e.Target == requestedTarget);
        }
        private ActiveGameplayEffect OriginalFind(GameplayEffect requestedEffect, GameObject requestedTarget, Object requestedSource)
        {
            if (requestedEffect == null || requestedTarget == null) return null;
            return original.FirstOrDefault(e => e.Effect == requestedEffect && e.Target == requestedTarget && e.SourceObject == requestedSource);
        }
        private bool OriginalHas(GameplayEffect requestedEffect, GameObject requestedTarget)
        {
            if (requestedEffect == null || requestedTarget == null) return false;
            return original.Any(e => e.Effect == requestedEffect && e.Target == requestedTarget && e.TimeRemaining > 0f);
        }
        private bool OriginalHas(GameplayEffect requestedEffect, GameObject requestedTarget, Object requestedSource)
        {
            if (requestedEffect == null || requestedTarget == null) return false;
            return original.Any(e => e.Effect == requestedEffect && e.Target == requestedTarget &&
                e.SourceObject == requestedSource && e.TimeRemaining > 0f);
        }
        public int Baseline() =>
            (OriginalFind(effect, target) != null ? 1 : 0) +
            (OriginalFind(effect, target, source) != null ? 2 : 0) +
            (OriginalHas(effect, target) ? 4 : 0) +
            (OriginalHas(effect, target, source) ? 8 : 0);
        public int Optimized() =>
            (current.FindFirst(effect, target) != null ? 1 : 0) +
            (current.FindFirst(effect, target, source) != null ? 2 : 0) +
            (current.HasActive(effect, target) ? 4 : 0) +
            (current.HasActive(effect, target, source) ? 8 : 0);
        public void Validate()
        {
            if (!ReferenceEquals(OriginalFind(effect, target), expected) ||
                !ReferenceEquals(OriginalFind(effect, target, source), expected) ||
                !ReferenceEquals(current.FindFirst(effect, target), expected) ||
                !ReferenceEquals(current.FindFirst(effect, target, source), expected) ||
                OriginalHas(effect, target) != (expected != null) ||
                OriginalHas(effect, target, source) != (expected != null) ||
                current.HasActive(effect, target) != (expected != null) ||
                current.HasActive(effect, target, source) != (expected != null))
                throw new InvalidOperationException("Repository lookup reference/boolean mismatch.");
        }
        public void Dispose()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (effect != null) Object.DestroyImmediate(effect);
            if (otherEffect != null) Object.DestroyImmediate(otherEffect);
        }
    }

    private sealed class Fixture : IDisposable
    {
        public readonly int Count;
        private readonly GameObject root;
        private readonly Mob[] mobs;
        private readonly Transform[] targets;
        private readonly Detection[] detections;
        private readonly MobAIContext[] originalContexts;
        private readonly MobAIContext[] currentContexts;
        private readonly MobStateMachine[] originalMachines;
        private readonly MobStateMachine[] currentMachines;
        private readonly Func<bool>[] originalLocks;
        private readonly Func<bool>[] currentLocks;
        private readonly FramePerformanceSightCounter counter;
        private readonly Vector2 overlapCenter;
        private readonly Vector2 overlapSize;
        private readonly List<Collider2D> overlapResults = new(16);
        public int OverlapQueries { get; private set; }
        public int SightQueries => counter.Queries;

        public Fixture(int count)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Monsters/CommonCorridor/GoblinWarrior.prefab");
            if (prefab == null) throw new InvalidOperationException("GoblinWarrior benchmark prefab missing.");
            Count = count;
            root = new GameObject("CPU frame fixture " + count) { hideFlags = HideFlags.HideAndDontSave };
            mobs = new Mob[count]; targets = new Transform[count]; detections = new Detection[count];
            originalContexts = new MobAIContext[count]; currentContexts = new MobAIContext[count];
            originalMachines = new MobStateMachine[count]; currentMachines = new MobStateMachine[count];
            originalLocks = new Func<bool>[count]; currentLocks = new Func<bool>[count];
            Vector3 origin = new(20000f, 20000f, 0f);
            var wall = new GameObject("Pass-through sight counter");
            wall.transform.SetParent(root.transform, false);
            wall.transform.position = origin + new Vector3(2f, (count - 1) * 1.5f, 0f);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.1f, count * 3f + 4f);
            counter = wall.AddComponent<FramePerformanceSightCounter>();
            var group = new GameObject("Lock owner").AddComponent<MonsterSpawnRoomGroup>();
            group.transform.SetParent(root.transform, false);
            group.enabled = false;
            for (int i = 0; i < count; i++)
            {
                GameObject actor = Object.Instantiate(prefab, origin + Vector3.up * (i * 3f), Quaternion.identity, root.transform);
                foreach (MonoBehaviour component in actor.GetComponentsInChildren<MonoBehaviour>()) component.enabled = false;
                foreach (Animator animator in actor.GetComponentsInChildren<Animator>()) animator.enabled = false;
                foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                actor.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                mobs[i] = actor.GetComponent<Mob>();
                var target = new GameObject("Target " + i);
                target.transform.SetParent(root.transform, false);
                target.transform.position = actor.transform.position + Vector3.right * 3f;
                targets[i] = target.transform;
                typeof(Enemy).GetField("target", Private).SetValue(mobs[i], targets[i]);
                detections[i] = new Detection(mobs[i]);
                originalContexts[i] = Context(mobs[i], new OriginalChaseIntent(detections[i]));
                currentContexts[i] = Context(mobs[i], detections[i].Chase);
                originalMachines[i] = new MobStateMachine(); currentMachines[i] = new MobStateMachine();
                originalMachines[i].SetInitialState(new OriginalChaseState(), originalContexts[i]);
                currentMachines[i].SetInitialState(new MobChaseState(), currentContexts[i]);
                var oldUnit = new OriginalLockUnit(); oldUnit.AddMember(actor); originalLocks[i] = oldUnit.HasAliveMember;
                group.NotifyMonsterSpawned(actor);
            }
            IList units = (IList)typeof(MonsterSpawnRoomGroup).GetField("runtimeSpawnedMonsterUnits", Private).GetValue(group);
            for (int i = 0; i < count; i++) currentLocks[i] = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), units[i], units[i].GetType().GetMethod("HasAliveMember"));
            overlapCenter = origin + new Vector3(0f, (count - 1) * 1.5f, 0f);
            overlapSize = new Vector2(6f, count * 3f + 4f);
            Physics2D.SyncTransforms();
        }

        private static MobAIContext Context(Mob owner, IEnemyChaseIntent chase) => new(owner, chase,
            new BusyBridge(), new NoDecision(), Array.Empty<IMobPatternRunner>(), Array.Empty<IMobPresentationCleanup>(), 0f, 0f, 0f);
        public void ResetCounters() { counter.Queries = 0; OverlapQueries = 0; }
        public void SetTargetsOutsideRange()
        {
            for (int i = 0; i < Count; i++) targets[i].position = mobs[i].transform.position + Vector3.right * 15f;
            Physics2D.SyncTransforms();
        }
        public void SetTargetsInsideRange()
        {
            for (int i = 0; i < Count; i++) targets[i].position = mobs[i].transform.position + Vector3.right * 3f;
            Physics2D.SyncTransforms();
        }
        public int BaselineDetection() { int value = 0; for (int i = 0; i < Count; i++) if (detections[i].Baseline()) value++; return value; }
        public int OptimizedDetection() { int value = 0; for (int i = 0; i < Count; i++) if (detections[i].Chase.IsTargetWithinDetectionRange()) value++; return value; }
        public int BaselineFsm()
        {
            int value = 0;
            for (int i = 0; i < Count; i++) { originalMachines[i].Tick(originalContexts[i]); if (originalMachines[i].CurrentState is OriginalChaseState) value++; }
            return value;
        }
        public int OptimizedFsm()
        {
            int value = 0;
            for (int i = 0; i < Count; i++) { currentMachines[i].Tick(currentContexts[i]); if (currentMachines[i].CurrentState is MobChaseState) value++; }
            return value;
        }
        public int BaselineLock() { int value = 0; for (int i = 0; i < Count; i++) if (originalLocks[i]()) value++; return value; }
        public int OptimizedLock() { int value = 0; for (int i = 0; i < Count; i++) if (currentLocks[i]()) value++; return value; }
        public void ValidateLockDeaths()
        {
            if (BaselineLock() != Count || OptimizedLock() != Count) throw new InvalidOperationException("Live lock fixture mismatch.");
            var field = typeof(Enemy).GetField("isDead", Private);
            field.SetValue(mobs[0], true);
            try { if (BaselineLock() != Count - 1 || OptimizedLock() != Count - 1) throw new InvalidOperationException("Death lock fixture mismatch."); }
            finally { field.SetValue(mobs[0], false); }
        }
        public int BaselineOverlap()
        {
            OverlapQueries++;
            return Physics2D.OverlapBoxAll(overlapCenter, overlapSize, 0f).Length;
        }
        public int OptimizedOverlap()
        {
            ContactFilter2D filter = new();
            filter.SetLayerMask(Physics2D.DefaultRaycastLayers);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            filter.useTriggers = Physics2D.queriesHitTriggers;
            OverlapQueries++;
            return Physics2D.OverlapBox(overlapCenter, overlapSize, 0f, filter, overlapResults);
        }
        public void ValidateOverlapOrder()
        {
            Collider2D[] expected = Physics2D.OverlapBoxAll(overlapCenter, overlapSize, 0f);
            int count = OptimizedOverlap();
            if (expected.Length != count) throw new InvalidOperationException("Native overlap result count mismatch.");
            for (int i = 0; i < count; i++) if (!ReferenceEquals(expected[i], overlapResults[i])) throw new InvalidOperationException("Native overlap reference order mismatch.");
        }
        public void Dispose() { if (root != null) Object.DestroyImmediate(root); }
    }
}

/// <summary>Editor fixture probe: one native sight-ray hit produces one query observation.</summary>
public sealed class FramePerformanceSightCounter : MonoBehaviour, ICombatPathBlocker2D
{
    public int Queries;
    public bool BlocksCombatPath(Collider2D collider, GameObject requester, CombatPathBlockerQuery query)
    {
        if (query == CombatPathBlockerQuery.Sight) Queries++;
        return false;
    }
}
#endif
