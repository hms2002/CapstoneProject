#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using Unity.Profiling;
using Stopwatch = System.Diagnostics.Stopwatch;
using Object = UnityEngine.Object;

/// <summary>Isolated authored UI A/B probes. Results are operation costs, never whole-game FPS.</summary>
public static class FramePerformanceUiBenchmarks
{
    private const int Rounds = 21;
    private const int Frames = 120;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static string BaselineRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..",
        "Docs/SessionLogs/2026-10-07-frame-performance-measurement/original-operation-sources"));
    private const string UiRootPrefab = "Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab";
    private const string StatusPrefab = "Assets/_Project/Prefabs/UI/status_HUD_EntryView.prefab";
    private const string DamagePrefab = "Assets/_Project/Prefabs/UI/Combat/DamagePopupUI_World.prefab";
    private static object allocationSink;
    private static string stringSink;
    private delegate void BindStatus(in StatusHudEntry entry);
    private delegate DamagePopupWorldText AcquirePopup(Vector3 position, Transform parent);

    [Serializable]
    public sealed class Report
    {
        public string scope = "Warmed isolated operation A/B; CPU timing without a GC recorder, allocation in separate paired passes; not game frame time or FPS.";
        public int warmedRounds = 4;
        public int sampleRounds = Rounds;
        public string gcStatus;
        public bool gcCalibrated;
        public long calibrationPayloadDeltaBytes = -1;
        public string allocationUnit;
        public long calibrationNoAllocationBytes = -1, calibrationSmallBytes = -1, calibrationLargeBytes = -1;
        public int calibrationNoAllocationEvents, calibrationSmallEvents, calibrationLargeEvents;
        public AllocationEventCalibration allocationEvents = new();
        public bool allCorrect;
        public bool damageCompleted;
        public bool baselineDeferredDestroyConfirmed;
        public List<Source> originalSources = new();
        public List<Result> results = new();
    }

    [Serializable]
    public sealed class Source { public string path; public string sha256; }

    [Serializable]
    public sealed class AllocationEventCalibration
    {
        public bool available;
        public string unit, reason;
        public int canaryCalls = 32;
        public long noWorkEvents = -1, payload1024Events = -1, payload2048Events = -1;
    }

    [Serializable]
    public sealed class Result
    {
        public string name;
        public string operation;
        public string limits;
        public bool correct;
        public double warmupMs;
        public int operationsPerBatch;
        public int baselineVisualSetupsBeforeSamples, optimizedVisualSetupsBeforeSamples;
        public double baselineMedianMs, baselineP95Ms, optimizedMedianMs, optimizedP95Ms, medianReductionPercent;
        public double baselineGcBytesPerOperation = -1, optimizedGcBytesPerOperation = -1;
        public double baselineMedianAllocationEventsPerOperation = -1, optimizedMedianAllocationEventsPerOperation = -1;
        public string allocationEventStatus;
        public string gcStatus;
        public int allocationSampleRounds;
        public int allocationEventSampleRounds;
        public string counterScope = "After warmup, across timing and separate allocation samples; setup excluded.";
        public int baselineVisualSetupsDuringSamples, optimizedVisualSetupsDuringSamples;
        public int optimizedHoverLayoutPassesDuringSamples;
        public double[] baselineRawMs, optimizedRawMs;
        public long[] baselineRawAllocationBytes, optimizedRawAllocationBytes;
        public long[] baselineRawAllocationEvents, optimizedRawAllocationEvents;
    }

    /// <summary>Synchronous UI probes. Use RunCoroutine to include the deferred-destruction damage probe.</summary>
    public static Report Run()
    {
        RequirePlayMode();
        var report = new Report();
        CaptureProvenance(report);
        report.gcCalibrated = CalibrateAllocationRecorder(report);
        report.allocationEvents = CalibrateAllocationEvents();

        Type cacheType = typeof(WeaponSkillHudSlotPresenter).Assembly.GetType("HudDisplayTextCache", true);
        object decimalCache = Activator.CreateInstance(cacheType);
        object chargeCache = Activator.CreateInstance(cacheType);
        var decimalFormat = (Func<float, string, IFormatProvider, string>)Delegate.CreateDelegate(
            typeof(Func<float, string, IFormatProvider, string>), decimalCache, cacheType.GetMethod("Decimal"));
        var chargeFormat = (Func<int, int, string>)Delegate.CreateDelegate(
            typeof(Func<int, int, string>), chargeCache, cacheType.GetMethod("Charges"));
        foreach (bool ticking in new[] { false, true })
        {
            float[] values = Values(Frames, ticking);
            Action before = () => { for (int i = 0; i < values.Length; i++) stringSink = values[i].ToString("0.0"); };
            Action after = () => { for (int i = 0; i < values.Length; i++) stringSink = decimalFormat(values[i], null, null); };
            Result result = Measure(report, "decimal-" + (ticking ? "ticking" : "stable"), "One exact 0.0 numeric formatting request",
                before, after, Frames,
                () => values[Frames - 1].ToString("0.0") == decimalFormat(values[Frames - 1], null, null));
            result.limits = "Formatter/cache only; no TMP setter, layout, rendering or timer gameplay.";
        }
        {
            Action before = () => { for (int i = 0; i < Frames; i++) stringSink = $"{2}/{3}"; };
            Action after = () => { for (int i = 0; i < Frames; i++) stringSink = chargeFormat(2, 3); };
            Result result = Measure(report, "charges-stable", "One exact charge/max formatting request", before, after, Frames,
                () => chargeFormat(2, 3) == $"{2}/{3}");
            result.limits = "Same current-culture interpolation output; formatter/cache only.";
        }

        var rootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPrefab);
        if (rootPrefab == null) throw new InvalidOperationException("Authored GlobalUIRoot prefab is missing.");
        using (var fixture = new TimerFixture(rootPrefab))
        {
            float[] times = Values(Frames, true, 301f);
            Action before = () => { for (int i = 0; i < Frames; i++) fixture.Before(times[i], false, false); };
            Action after = () => { for (int i = 0; i < Frames; i++) fixture.After(times[i], false, false); };
            var result = Measure(report, "run-timer", "One authored timer text/color refresh",
                before, after, Frames, fixture.Equivalent);
            result.limits = "Original UpdateTimeText vs current UpdateTimeText; no timer state tick, pause pulse or whole-frame render.";
        }

        foreach (int count in new[] { 8, 20 })
            foreach (bool ticking in new[] { false, true })
                using (var fixture = new StatusFixture(count))
                {
                    StatusHudEntry[] entries = Entries(Frames, ticking);
                    Action before = () => { for (int frame = 0; frame < Frames; frame++) fixture.Before(entries[frame]); };
                    Action after = () => { for (int frame = 0; frame < Frames; frame++) fixture.After(entries[frame]); };
                    int oldSetup = 0, newSetup = 0;
                    var result = Measure(report, $"status-{count}-" + (ticking ? "ticking" : "stable"),
                        $"One simulated refresh of {count} authored status entries", before, after, Frames, fixture.Equivalent,
                        () => { oldSetup = fixture.BeforeSetups; newSetup = fixture.AfterSetups; });
                    result.baselineVisualSetupsBeforeSamples = oldSetup;
                    result.optimizedVisualSetupsBeforeSamples = newSetup;
                    result.baselineVisualSetupsDuringSamples = fixture.BeforeSetups - oldSetup;
                    result.optimizedVisualSetupsDuringSamples = fixture.AfterSetups - newSetup;
                    result.limits = "Exact original/current Bind; pointer hover is off. Source collection/sorting and native end-of-frame Canvas render are excluded.";
                }

        using (var fixture = new PopupFixture())
        {
            Action before = () => { for (int i = 0; i < Frames; i++) fixture.Before(); };
            Action after = () => { for (int i = 0; i < Frames; i++) fixture.After(); };
            var result = Measure(report, "popup-canvas-query", "One HasActivePopupInCanvas lookup over 24 entries",
                before, after, Frames, fixture.Equivalent);
            result.limits = "Same original query body and real SUT Snapshot/ReadEntries; mutation snapshots remain unchanged.";
        }

        foreach (bool ticking in new[] { false, true })
            using (var fixture = new HoverFixture(rootPrefab))
            {
                const int hoverFrames = 30;
                StatusHudEntry[] entries = Entries(hoverFrames, ticking);
                Action before = () => { for (int i = 0; i < hoverFrames; i++) fixture.Before(entries[i], i); };
                Action after = () => { for (int i = 0; i < hoverFrames; i++) fixture.After(entries[i], i); };
                int layoutBefore = 0;
                var result = Measure(report, "status-hover-" + (ticking ? "ticking" : "stable"),
                    "One status tooltip refresh plus anchor-follow LateUpdate", before, after, hoverFrames, fixture.Equivalent,
                    () => layoutBefore = fixture.AfterLayouts);
                result.optimizedHoverLayoutPassesDuringSamples = fixture.AfterLayouts - layoutBefore;
                result.limits = "Original ShowHover/LateUpdate vs current typed ShowStatusHover/LateUpdate, same authored tooltip and moving anchor. Deferred coroutine completion is outside these synchronous batches; native forced layout calls inside methods are included.";
            }

        report.allCorrect = report.results.TrueForAll(r => r.correct);
        return report;
    }

    /// <summary>Runs the UI suite and damage burst pairs, yielding between bursts so queued Destroy work really finishes.</summary>
    public static IEnumerator RunCoroutine(Action<Report> completed)
    {
        Report report = Run();
        using (var fixture = new DamageFixture())
        {
            const int burst = 16;
            double setupStart = Now();
            for (int warm = 0; warm < 4; warm++)
            {
                fixture.Before();
                fixture.VerifyBaselineContent();
                yield return null;
                fixture.VerifyDestroyed();
                fixture.After();
                fixture.VerifyPooledContent();
                yield return null;
            }
            var result = NewResult("damage-popup-burst", $"One spawn/setup/release request in a burst of {burst}", Rounds);
            result.warmupMs = Now() - setupStart;
            result.limits = "Actual original Instantiate/Setup/Destroy enqueue vs current Acquire/Setup/Return. Stopwatch/GC scope ends at release request; deferred native destruction, renderer work and yielded frames are not included. Each baseline burst is fenced and verified destroyed before the next sample.";
            bool fencesPassed = true;
            for (int sample = 0; sample < Rounds; sample++)
            {
                if ((sample & 1) == 0)
                {
                    result.baselineRawMs[sample] = RecordTime(fixture.Before);
                    fixture.VerifyBaselineContent();
                    yield return null;
                    fencesPassed &= fixture.VerifyDestroyed();
                    result.optimizedRawMs[sample] = RecordTime(fixture.After);
                    fixture.VerifyPooledContent();
                    yield return null;
                }
                else
                {
                    result.optimizedRawMs[sample] = RecordTime(fixture.After);
                    fixture.VerifyPooledContent();
                    yield return null;
                    result.baselineRawMs[sample] = RecordTime(fixture.Before);
                    fixture.VerifyBaselineContent();
                    yield return null;
                    fencesPassed &= fixture.VerifyDestroyed();
                }
            }
            if (report.gcCalibrated || report.allocationEvents.available)
            {
                for (int sample = 0; sample < Rounds; sample++)
                {
                    if ((sample & 1) == 0)
                    {
                        CaptureAllocationMetrics(fixture.Before, report, out result.baselineRawAllocationBytes[sample], out result.baselineRawAllocationEvents[sample]);
                        fixture.VerifyBaselineContent();
                        yield return null;
                        fencesPassed &= fixture.VerifyDestroyed();
                        CaptureAllocationMetrics(fixture.After, report, out result.optimizedRawAllocationBytes[sample], out result.optimizedRawAllocationEvents[sample]);
                        fixture.VerifyPooledContent();
                        yield return null;
                    }
                    else
                    {
                        CaptureAllocationMetrics(fixture.After, report, out result.optimizedRawAllocationBytes[sample], out result.optimizedRawAllocationEvents[sample]);
                        fixture.VerifyPooledContent();
                        yield return null;
                        CaptureAllocationMetrics(fixture.Before, report, out result.baselineRawAllocationBytes[sample], out result.baselineRawAllocationEvents[sample]);
                        fixture.VerifyBaselineContent();
                        yield return null;
                        fencesPassed &= fixture.VerifyDestroyed();
                    }
                }
                result.allocationSampleRounds = report.gcCalibrated ? Rounds : 0;
                result.allocationEventSampleRounds = report.allocationEvents.available ? Rounds : 0;
            }
            result.correct = fixture.Correct && fencesPassed;
            Finish(result, burst, report.gcStatus, report.allocationEvents.reason);
            report.results.Add(result);
            report.damageCompleted = true;
            report.baselineDeferredDestroyConfirmed = fencesPassed;
        }
        report.allCorrect = report.results.TrueForAll(r => r.correct);
        completed?.Invoke(report);
    }

    private static void RequirePlayMode()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run this fixture in compiled Play Mode.");
    }

    private static void CaptureProvenance(Report report)
    {
        string[] paths =
        {
            "Common/PopupStackState.cs", "Common/UIManager.cs", "Common/HoverUIController.cs",
            "HUD/RunTimer/RunTimerHUD.cs", "HUD/Status/StatusHudEntryView.cs", "HUD/Status/StatusHudTooltipView.cs",
            "HUD/WeaponSkillHudSlotPresenter.cs", "Combat/DamagePopup/DamagePopupWorldText.cs", "Combat/DamagePopup/DamagePopupService.cs"
        };
        using (SHA256 hash = SHA256.Create())
            foreach (string relative in paths)
            {
                string path = Path.Combine(BaselineRoot, "Assets/_Project/Runtime/UI", relative + ".txt");
                if (!File.Exists(path)) throw new FileNotFoundException("Original comparison source is missing.", path);
                byte[] digest = hash.ComputeHash(File.ReadAllBytes(path));
                report.originalSources.Add(new Source { path = path, sha256 = BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant() });
            }
    }

    private static float[] Values(int frames, bool ticking, float start = 10f)
    {
        var values = new float[frames];
        for (int i = 0; i < frames; i++) values[i] = ticking ? start - i / 60f : start;
        return values;
    }

    private static StatusHudEntry[] Entries(int frames, bool ticking)
    {
        var entries = new StatusHudEntry[frames];
        for (int i = 0; i < frames; i++)
            entries[i] = new StatusHudEntry("frame-benchmark", "status", "Name", "Story", "Effect", null,
                5, true, ticking ? 10f - i / 60f : 10f, 10f, true, default, 0, false, true);
        return entries;
    }

    private static Result Measure(Report report, string name, string operation, Action before, Action after,
        int operations, Func<bool> verify, Action warmed = null)
    {
        double setupStart = Now();
        for (int i = 0; i < 4; i++) { before(); after(); }
        warmed?.Invoke();
        var result = NewResult(name, operation, Rounds);
        result.warmupMs = Now() - setupStart;
        for (int sample = 0; sample < Rounds; sample++)
        {
            if ((sample & 1) == 0)
            {
                result.baselineRawMs[sample] = RecordTime(before);
                result.optimizedRawMs[sample] = RecordTime(after);
            }
            else
            {
                result.optimizedRawMs[sample] = RecordTime(after);
                result.baselineRawMs[sample] = RecordTime(before);
            }
        }
        if (report.gcCalibrated || report.allocationEvents.available)
        {
            for (int sample = 0; sample < Rounds; sample++)
            {
                if ((sample & 1) == 0)
                {
                    CaptureAllocationMetrics(before, report, out result.baselineRawAllocationBytes[sample], out result.baselineRawAllocationEvents[sample]);
                    CaptureAllocationMetrics(after, report, out result.optimizedRawAllocationBytes[sample], out result.optimizedRawAllocationEvents[sample]);
                }
                else
                {
                    CaptureAllocationMetrics(after, report, out result.optimizedRawAllocationBytes[sample], out result.optimizedRawAllocationEvents[sample]);
                    CaptureAllocationMetrics(before, report, out result.baselineRawAllocationBytes[sample], out result.baselineRawAllocationEvents[sample]);
                }
            }
            result.allocationSampleRounds = report.gcCalibrated ? Rounds : 0;
            result.allocationEventSampleRounds = report.allocationEvents.available ? Rounds : 0;
        }
        result.correct = verify();
        if (!result.correct) throw new InvalidOperationException("UI A/B output mismatch: " + name);
        Finish(result, operations, report.gcStatus, report.allocationEvents.reason);
        report.results.Add(result);
        return result;
    }

    private static Result NewResult(string name, string operation, int rounds) => new()
    {
        name = name, operation = operation,
        baselineRawMs = new double[rounds], optimizedRawMs = new double[rounds],
        baselineRawAllocationBytes = UnavailableAllocations(rounds), optimizedRawAllocationBytes = UnavailableAllocations(rounds),
        baselineRawAllocationEvents = UnavailableAllocations(rounds), optimizedRawAllocationEvents = UnavailableAllocations(rounds)
    };

    private static long[] UnavailableAllocations(int count)
    {
        var values = new long[count];
        for (int i = 0; i < count; i++) values[i] = -1;
        return values;
    }

    private static double Now() => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    private static void Finish(Result result, int operations, string gcStatus, string eventStatus)
    {
        result.operationsPerBatch = operations;
        double[] before = (double[])result.baselineRawMs.Clone();
        double[] after = (double[])result.optimizedRawMs.Clone();
        Array.Sort(before); Array.Sort(after);
        int p95 = Math.Min(before.Length - 1, (int)Math.Ceiling(before.Length * 0.95) - 1);
        result.baselineMedianMs = before[before.Length / 2] / operations;
        result.optimizedMedianMs = after[after.Length / 2] / operations;
        result.baselineP95Ms = before[p95] / operations;
        result.optimizedP95Ms = after[p95] / operations;
        result.medianReductionPercent = result.baselineMedianMs > 0 ? (1 - result.optimizedMedianMs / result.baselineMedianMs) * 100 : 0;
        result.gcStatus = gcStatus;
        if (Array.Exists(result.baselineRawAllocationBytes, x => x < 0) || Array.Exists(result.optimizedRawAllocationBytes, x => x < 0))
            result.gcStatus += "; per-case allocation bytes unavailable or recorder capacity reached";
        else
        {
            long[] oldBytes = (long[])result.baselineRawAllocationBytes.Clone();
            long[] newBytes = (long[])result.optimizedRawAllocationBytes.Clone();
            Array.Sort(oldBytes); Array.Sort(newBytes);
            result.baselineGcBytesPerOperation = (double)oldBytes[oldBytes.Length / 2] / operations;
            result.optimizedGcBytesPerOperation = (double)newBytes[newBytes.Length / 2] / operations;
        }
        result.allocationEventStatus = eventStatus;
        if (Array.Exists(result.baselineRawAllocationEvents, x => x < 0) || Array.Exists(result.optimizedRawAllocationEvents, x => x < 0))
            result.allocationEventStatus += "; per-case event counts unavailable or recorder capacity reached";
        else
        {
            long[] oldEvents = (long[])result.baselineRawAllocationEvents.Clone();
            long[] newEvents = (long[])result.optimizedRawAllocationEvents.Clone();
            Array.Sort(oldEvents); Array.Sort(newEvents);
            result.baselineMedianAllocationEventsPerOperation = (double)oldEvents[oldEvents.Length / 2] / operations;
            result.optimizedMedianAllocationEventsPerOperation = (double)newEvents[newEvents.Length / 2] / operations;
        }
    }

    private static double RecordTime(Action work)
    {
        long start = Stopwatch.GetTimestamp();
        work();
        return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
    }

    private static void CaptureAllocationMetrics(Action work, Report report, out long bytes, out long events)
    {
        bytes = -1; events = -1;
        if (report.gcCalibrated)
        {
            bytes = CaptureAllocation(work, out _, out int capturedEvents, out _);
            if (report.allocationEvents.available && bytes >= 0) events = capturedEvents;
        }
        else if (report.allocationEvents.available)
        {
            // A time-valued allocation marker can still expose calibrated event counts.
            // Its sample.Value is deliberately never interpreted as allocated bytes.
            events = CaptureAllocationEvents(work, out _, out _);
        }
    }

    private static long CaptureAllocationEvents(Action work, out string reason, out string unit)
    {
        reason = null; unit = null;
        const int capacity = 65536;
        try
        {
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", capacity,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread | ProfilerRecorderOptions.StartImmediately);
            if (!recorder.Valid) { reason = "GC.Alloc event marker unavailable."; return -1; }
            unit = recorder.UnitType.ToString();
            recorder.Reset();
            work();
            recorder.Stop();
            if (recorder.WrappedAround || recorder.Count >= capacity)
            { reason = "GC.Alloc event recorder capacity reached."; return -1; }
            long events = 0;
            for (int i = 0; i < recorder.Count; i++) events += (long)recorder.GetSample(i).Count;
            return events;
        }
        catch (Exception ex)
        {
            reason = "GC.Alloc events unavailable: " + ex.GetType().Name + " " + ex.Message;
            return -1;
        }
    }

    private static AllocationEventCalibration CalibrateAllocationEvents()
    {
        var calibration = new AllocationEventCalibration();
        Action noWork = () => { for (int i = 0; i < 32; i++) allocationSink = string.Empty; };
        Action small = () => { for (int i = 0; i < 32; i++) { allocationSink = new byte[1024]; GC.KeepAlive(allocationSink); } };
        Action large = () => { for (int i = 0; i < 32; i++) { allocationSink = new byte[2048]; GC.KeepAlive(allocationSink); } };
        noWork(); small(); large();
        calibration.noWorkEvents = CaptureAllocationEvents(noWork, out string noReason, out string noUnit);
        calibration.payload1024Events = CaptureAllocationEvents(small, out string smallReason, out string smallUnit);
        calibration.payload2048Events = CaptureAllocationEvents(large, out string largeReason, out string largeUnit);
        calibration.unit = noUnit ?? smallUnit ?? largeUnit;
        calibration.available = noReason == null && smallReason == null && largeReason == null &&
            noUnit == smallUnit && smallUnit == largeUnit &&
            calibration.noWorkEvents == 0 && calibration.payload1024Events == 32 && calibration.payload2048Events == 32;
        calibration.reason = calibration.available
            ? "Native current-thread GC.Alloc events, sum of sample.Count. Passed no-work=0 and 32 array calls=32 events at both 1KB and 2KB. Marker value unit is " +
                calibration.unit + "; these are allocation event counts, not allocated bytes or heap growth."
            : (noReason ?? smallReason ?? largeReason ?? "GC.Alloc zero-work/32-call event calibration failed") +
                $" (control={calibration.noWorkEvents}, 1KB={calibration.payload1024Events}, 2KB={calibration.payload2048Events}); event counts are unavailable.";
        return calibration;
    }

    private static long CaptureAllocation(Action work, out string reason, out int events, out string unit)
    {
        reason = null; events = 0; unit = null;
        const int capacity = 65536;
        try
        {
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", capacity,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread | ProfilerRecorderOptions.StartImmediately);
            if (!recorder.Valid) { reason = "GC.Alloc marker unavailable."; return -1; }
            unit = recorder.UnitType.ToString();
            if (recorder.UnitType != ProfilerMarkerDataUnit.Bytes)
            { reason = "GC.Alloc metadata unit is not Bytes."; return -1; }
            // Recorder construction/metadata lookup are outside the allocation interval.
            recorder.Reset();
            work();
            recorder.Stop();
            if (recorder.WrappedAround || recorder.Count >= capacity)
            { reason = "GC.Alloc recorder capacity reached."; return -1; }
            long bytes = 0;
            for (int i = 0; i < recorder.Count; i++)
            {
                ProfilerRecorderSample sample = recorder.GetSample(i);
                bytes += sample.Value;
                events += (int)sample.Count;
            }
            return bytes;
        }
        catch (Exception ex)
        {
            reason = "GC.Alloc recorder unavailable: " + ex.GetType().Name + " " + ex.Message;
            return -1;
        }
    }

    private static bool CalibrateAllocationRecorder(Report report)
    {
        try
        {
            Action noAllocation = () => { for (int i = 0; i < 32; i++) allocationSink = string.Empty; };
            Action small = () => { for (int i = 0; i < 32; i++) { allocationSink = new byte[1024]; GC.KeepAlive(allocationSink); } };
            Action large = () => { for (int i = 0; i < 32; i++) { allocationSink = new byte[2048]; GC.KeepAlive(allocationSink); } };
            noAllocation(); small(); large();
            report.calibrationNoAllocationBytes = CaptureAllocation(noAllocation, out string noReason,
                out report.calibrationNoAllocationEvents, out string noUnit);
            report.calibrationSmallBytes = CaptureAllocation(small, out string smallReason,
                out report.calibrationSmallEvents, out string smallUnit);
            report.calibrationLargeBytes = CaptureAllocation(large, out string largeReason,
                out report.calibrationLargeEvents, out string largeUnit);
            report.allocationUnit = noUnit ?? smallUnit ?? largeUnit;
            report.calibrationPayloadDeltaBytes = report.calibrationSmallBytes < 0 || report.calibrationLargeBytes < 0
                ? -1 : report.calibrationLargeBytes - report.calibrationSmallBytes;
            const long smallPayload = 32L * 1024L, largePayload = 32L * 2048L;
            bool valid = noReason == null && smallReason == null && largeReason == null &&
                noUnit == "Bytes" && smallUnit == "Bytes" && largeUnit == "Bytes" &&
                report.calibrationNoAllocationBytes == 0 && report.calibrationNoAllocationEvents == 0 &&
                report.calibrationSmallBytes >= smallPayload && report.calibrationSmallBytes <= smallPayload + 128L * 32L &&
                report.calibrationLargeBytes >= largePayload && report.calibrationLargeBytes <= largePayload + 128L * 32L &&
                report.calibrationPayloadDeltaBytes == 32L * 1024L &&
                report.calibrationSmallEvents >= 32 && report.calibrationLargeEvents >= 32;
            report.gcStatus = valid
                ? "GC.Alloc native recorder, UnitType.Bytes, current thread, individual events. Passed zero-byte/zero-event control, 32 x 1KB/2KB absolute payload+header bounds and exact 32768-byte delta; CPU timings use separate recorder-free passes."
                : (noReason ?? smallReason ?? largeReason ?? "GC.Alloc unit/no-allocation/absolute-size/payload-delta calibration failed") +
                    $" (control={report.calibrationNoAllocationBytes}, small={report.calibrationSmallBytes}, large={report.calibrationLargeBytes}); allocation bytes are unavailable.";
            return valid;
        }
        catch (Exception ex)
        {
            report.gcStatus = "GC.Alloc recorder unavailable: " + ex.GetType().Name + " " + ex.Message;
            return false;
        }
    }

    private sealed class Owned : IDisposable
    {
        public readonly List<GameObject> Objects = new();
        public GameObject Add(GameObject go) { go.hideFlags = HideFlags.DontSave; Objects.Add(go); return go; }
        public GameObject New(string name, params Type[] components) => Add(new GameObject(name, components));
        public Canvas Canvas(string name)
        {
            var canvas = New(name, typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return canvas;
        }
        public void Dispose() { for (int i = Objects.Count - 1; i >= 0; i--) if (Objects[i] != null) Object.DestroyImmediate(Objects[i]); }
    }

    private sealed class SingletonLease : IDisposable
    {
        private readonly FieldInfo field;
        private readonly object previous;
        public SingletonLease(Type type)
        {
            field = type.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic) ??
                    type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null) return;
            previous = field.GetValue(null);
            field.SetValue(null, null);
        }
        public void Dispose() { field?.SetValue(null, previous); }
    }

    private static T CloneView<T>(Component authored, Owned owned, Canvas canvas) where T : MonoBehaviour
    {
        var staging = owned.New("UI A/B inactive staging");
        staging.SetActive(false);
        var go = owned.Add(Object.Instantiate(authored.gameObject, staging.transform));
        go.SetActive(true);
        var source = go.GetComponent(authored.GetType());
        foreach (MonoBehaviour component in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (component != source && component is not UIBehaviour && component is not LocalizeTmpFontEvent)
                Object.DestroyImmediate(component);
        T target;
        if (source is T current) target = current;
        else
        {
            target = go.AddComponent<T>();
            CopyAuthoring(source, target);
            Object.DestroyImmediate(source);
        }
        using (new SingletonLease(typeof(T)))
            go.transform.SetParent(canvas.transform, false);
        go.transform.SetParent(canvas.transform, false); // Awake may adopt to the live global UI canvas.
        return target;
    }

    private static void CopyAuthoring(Component source, Component target)
    {
        foreach (FieldInfo field in target.GetType().GetFields(Fields | BindingFlags.DeclaredOnly))
        {
            if (!field.IsPublic && !field.IsDefined(typeof(SerializeField), true)) continue;
            FieldInfo from = source.GetType().GetField(field.Name, Fields);
            if (from != null && from.FieldType == field.FieldType) field.SetValue(target, from.GetValue(source));
        }
    }

    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Fields).GetValue(owner);
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Fields).SetValue(owner, value);
    private static T Bound<T>(object owner, string method) where T : Delegate =>
        (T)Delegate.CreateDelegate(typeof(T), owner, owner.GetType().GetMethod(method, Fields));
    private static string Text(object owner, string name) => Field<TMP_Text>(owner, name).text;

    private sealed class TimerFixture : IDisposable
    {
        private readonly Owned owned = new();
        private readonly FramePerformanceUiBaseline.RunTimerHUD oldView;
        private readonly RunTimerHUD newView;
        public readonly Action<float, bool, bool> Before, After;
        public TimerFixture(GameObject rootPrefab)
        {
            var authored = rootPrefab.GetComponentInChildren<RunTimerHUD>(true);
            oldView = CloneView<FramePerformanceUiBaseline.RunTimerHUD>(authored, owned, owned.Canvas("Original timer canvas"));
            newView = CloneView<RunTimerHUD>(authored, owned, owned.Canvas("Current timer canvas"));
            Before = Bound<Action<float, bool, bool>>(oldView, "UpdateTimeText");
            After = Bound<Action<float, bool, bool>>(newView, "UpdateTimeText");
        }
        public bool Equivalent() => Text(oldView, "timeText") == Text(newView, "timeText") &&
            Field<TMP_Text>(oldView, "timeText").color == Field<TMP_Text>(newView, "timeText").color;
        public void Dispose() => owned.Dispose();
    }

    private sealed class StatusFixture : IDisposable
    {
        private readonly Owned owned = new();
        private readonly FramePerformanceUiBaseline.StatusHudEntryView[] oldViews;
        private readonly StatusHudEntryView[] newViews;
        private readonly BindStatus[] before, after;
        public StatusFixture(int count)
        {
            var authored = AssetDatabase.LoadAssetAtPath<GameObject>(StatusPrefab).GetComponent<StatusHudEntryView>();
            oldViews = new FramePerformanceUiBaseline.StatusHudEntryView[count];
            newViews = new StatusHudEntryView[count];
            before = new BindStatus[count]; after = new BindStatus[count];
            Canvas oldCanvas = owned.Canvas("Original status canvas"), newCanvas = owned.Canvas("Current status canvas");
            for (int i = 0; i < count; i++)
            {
                oldViews[i] = CloneView<FramePerformanceUiBaseline.StatusHudEntryView>(authored, owned, oldCanvas);
                newViews[i] = CloneView<StatusHudEntryView>(authored, owned, newCanvas);
                before[i] = (BindStatus)Delegate.CreateDelegate(typeof(BindStatus), oldViews[i], oldViews[i].GetType().GetMethod("Bind"));
                after[i] = newViews[i].Bind;
            }
        }
        public void Before(in StatusHudEntry entry) { for (int i = 0; i < before.Length; i++) before[i](entry); }
        public void After(in StatusHudEntry entry) { for (int i = 0; i < after.Length; i++) after[i](entry); }
        public int BeforeSetups { get { int value = 0; foreach (var view in oldViews) value += view.DiagnosticVisualSetupCount; return value; } }
        public int AfterSetups { get { int value = 0; foreach (var view in newViews) value += view.DiagnosticVisualSetupCount; return value; } }
        public bool Equivalent()
        {
            for (int i = 0; i < oldViews.Length; i++)
                if (Text(oldViews[i], "stackText") != Text(newViews[i], "stackText") ||
                    Text(oldViews[i], "durationText") != Text(newViews[i], "durationText") ||
                    Field<UnityEngine.UI.Image>(oldViews[i], "durationFillImage").fillAmount != Field<UnityEngine.UI.Image>(newViews[i], "durationFillImage").fillAmount)
                    return false;
            return true;
        }
        public void Dispose() => owned.Dispose();
    }

    // A fixture component with passive getters; no input, gameplay state, save data or events.
    public sealed class PopupProbe : MonoBehaviour, IStackableUI
    {
        public bool IsActive => gameObject.activeSelf;
        public bool CanCloseOnEscape => true;
        public UIOpenGroup OpenGroup => UIOpenGroup.Overlay;
        public UIOpenGroup BlockedOpenGroups => UIOpenGroup.None;
        public UIGameplayLockProfile GameplayLockProfile => UIGameplayLockProfile.None;
        public void OpenUI() => gameObject.SetActive(true);
        public void CloseUI() => gameObject.SetActive(false);
    }

    private sealed class PopupFixture : IDisposable
    {
        private readonly Owned owned = new();
        private readonly PopupStackState stack = new();
        private readonly Transform queryCanvas;
        private bool oldResult, newResult;
        public PopupFixture()
        {
            queryCanvas = owned.Canvas("Queried popup canvas").transform;
            Transform other = owned.Canvas("Other popup canvas").transform;
            for (int i = 0; i < 24; i++)
            {
                var go = owned.New("Popup query probe " + i);
                go.transform.SetParent(i == 23 ? queryCanvas : other, false);
                stack.Push(go.AddComponent<PopupProbe>());
            }
        }
        public void Before() => oldResult = HasActive(stack.Snapshot(), queryCanvas);
        public void After() => newResult = HasActive(stack.ReadEntries(), queryCanvas);
        private static bool HasActive(IReadOnlyList<IStackableUI> entries, Transform canvas)
        {
            if (entries == null || entries.Count == 0) return false;
            for (int i = 0; i < entries.Count; i++)
            {
                IStackableUI ui = entries[i];
                if (ui == null || !ui.IsActive) continue;
                if (ui is not MonoBehaviour behaviour || behaviour == null) continue;
                if (behaviour.transform.IsChildOf(canvas)) return true;
            }
            return false;
        }
        public bool Equivalent() => oldResult && newResult && stack.ReadEntries().Count == 24;
        public void Dispose() => owned.Dispose();
    }

    private sealed class HoverFixture : IDisposable
    {
        private readonly Owned owned = new();
        private readonly FramePerformanceUiBaseline.HoverUIController oldController;
        private readonly HoverUIController newController;
        private readonly FramePerformanceUiBaseline.StatusHudTooltipView oldView;
        private readonly StatusHudTooltipView newView;
        private readonly RectTransform oldAnchor, newAnchor;
        private readonly Action oldTick, newTick;
        public int AfterLayouts => newController.DiagnosticLayoutRefreshCount;
        public HoverFixture(GameObject rootPrefab)
        {
            var authored = rootPrefab.GetComponentInChildren<StatusHudTooltipView>(true);
            Canvas oldCanvas = owned.Canvas("Original hover canvas"), newCanvas = owned.Canvas("Current hover canvas");
            oldView = CloneView<FramePerformanceUiBaseline.StatusHudTooltipView>(authored, owned, oldCanvas);
            newView = CloneView<StatusHudTooltipView>(authored, owned, newCanvas);
            oldAnchor = owned.New("Original hover anchor", typeof(RectTransform)).GetComponent<RectTransform>();
            newAnchor = owned.New("Current hover anchor", typeof(RectTransform)).GetComponent<RectTransform>();
            oldAnchor.SetParent(oldCanvas.transform, false); newAnchor.SetParent(newCanvas.transform, false);
            oldAnchor.sizeDelta = newAnchor.sizeDelta = new Vector2(48, 48);
            oldController = NewController<FramePerformanceUiBaseline.HoverUIController>(oldCanvas);
            newController = NewController<HoverUIController>(newCanvas);
            oldTick = Bound<Action>(oldController, "LateUpdate"); newTick = Bound<Action>(newController, "LateUpdate");
        }
        private T NewController<T>(Canvas canvas) where T : MonoBehaviour
        {
            GameObject go = owned.New(typeof(T).FullName);
            go.SetActive(false);
            T component = go.AddComponent<T>();
            Set(component, "canvas", canvas);
            go.SetActive(true);
            go.transform.SetParent(canvas.transform, false);
            return component;
        }
        public void Before(in StatusHudEntry entry, int frame)
        {
            oldAnchor.anchoredPosition = new Vector2(0, frame % 10);
            oldController.ShowHover(oldView, oldAnchor, entry, null);
            oldTick();
        }
        public void After(in StatusHudEntry entry, int frame)
        {
            newAnchor.anchoredPosition = new Vector2(0, frame % 10);
            newController.ShowStatusHover(newView, newAnchor, entry);
            newTick();
        }
        public bool Equivalent() => Text(oldView, "nameText") == Text(newView, "nameText") &&
            Text(oldView, "storyText") == Text(newView, "storyText") && Text(oldView, "effectText") == Text(newView, "effectText") &&
            Vector2.Distance(oldView.Rect.anchoredPosition, newView.Rect.anchoredPosition) < 0.01f;
        public void Dispose() => owned.Dispose();
    }

    private sealed class DamageFixture : IDisposable
    {
        private const int Burst = 16;
        private readonly Owned owned = new();
        private readonly Transform parent;
        private readonly FramePerformanceUiBaseline.DamagePopupWorldText prototype;
        private readonly FramePerformanceUiBaseline.DamagePopupWorldText[] oldPopups = new FramePerformanceUiBaseline.DamagePopupWorldText[Burst];
        private readonly DamagePopupWorldText[] newPopups = new DamagePopupWorldText[Burst];
        private readonly AcquirePopup acquire;
        private readonly Action<DamagePopupWorldText> release;
        private readonly DamagePopupViewModel model = new("42", Color.white, Vector3.up, 10000f, 0.55f, 0.9f, 1.1f, 0f, false);
        private readonly DamagePopupService service;
        public bool Correct { get; private set; } = true;
        public DamageFixture()
        {
            GameObject authored = AssetDatabase.LoadAssetAtPath<GameObject>(DamagePrefab);
            var staging = owned.New("Original popup prototype staging");
            staging.SetActive(false);
            GameObject prototypeObject = owned.Add(Object.Instantiate(authored, staging.transform));
            var source = prototypeObject.GetComponent<DamagePopupWorldText>();
            prototype = prototypeObject.AddComponent<FramePerformanceUiBaseline.DamagePopupWorldText>();
            CopyAuthoring(source, prototype);
            Object.DestroyImmediate(source);
            parent = owned.New("Popup A/B active parent").transform;
            using (new SingletonLease(typeof(DamagePopupService)))
            {
                GameObject owner = owned.New("Popup A/B service");
                owner.SetActive(false);
                service = owner.AddComponent<DamagePopupService>();
                Set(service, "popupPrefab", authored.GetComponent<DamagePopupWorldText>());
                Set(service, "logPopupRequests", false);
                owner.SetActive(true);
            }
            acquire = Bound<AcquirePopup>(service, "AcquirePopup");
            release = Bound<Action<DamagePopupWorldText>>(service, "ReturnPopup");
        }
        public void Before()
        {
            for (int i = 0; i < Burst; i++)
            {
                oldPopups[i] = Object.Instantiate(prototype, new Vector3(i * 0.25f, 0, 0), Quaternion.identity, parent);
                oldPopups[i].Setup(model);
            }
            for (int i = 0; i < Burst; i++) Object.Destroy(oldPopups[i].gameObject);
        }
        public void After()
        {
            for (int i = 0; i < Burst; i++)
            {
                newPopups[i] = acquire(new Vector3(i * 0.25f, 0, 0), parent);
                newPopups[i].Setup(model);
                if (!newPopups[i].gameObject.activeSelf) newPopups[i].gameObject.SetActive(true);
            }
            for (int i = 0; i < Burst; i++) release(newPopups[i]);
        }
        public bool VerifyDestroyed()
        {
            bool valid = true;
            foreach (var popup in oldPopups) valid &= popup == null;
            if (!valid) throw new InvalidOperationException("The baseline deferred popup destruction fence did not complete.");
            return valid;
        }
        public void VerifyBaselineContent()
        {
            foreach (var popup in oldPopups)
                Correct &= popup != null && popup.gameObject.activeSelf && popup.transform.parent == parent &&
                    Field<TextMeshPro>(popup, "text").text == model.Text;
        }
        public void VerifyPooledContent()
        {
            foreach (var popup in newPopups)
                Correct &= popup != null && !popup.gameObject.activeSelf && popup.transform.parent == service.transform &&
                    Field<TextMeshPro>(popup, "text").text == model.Text;
        }
        public void Dispose()
        {
            owned.Dispose();
        }
    }
}
#endif

#if UNITY_EDITOR
namespace FramePerformanceUiBaseline
{
    using System;
    using System.Collections;
    using UnityEngine;
    using UnityEngine.UI;
    using UnityEngine.EventSystems;
    using TMPro;

// Frozen source: HoverUIController.cs; exact pre-change snapshot. No production type is replaced.
public class HoverUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas canvas;

    [Header("Global Positioning")]
    [SerializeField] private float offset = 12f;
    [SerializeField] private float edgePadding = 8f;
    [SerializeField] private bool followAnchor = true;

    [Header("Global Hide Timing")]
    [SerializeField] private bool delayHideOneFrame = true;
    [SerializeField] private float extraHideDelay = 0f;

    [Header("Debug")]
    [SerializeField] private bool logPositioningDebug;

    private IHoverView _currentView;
    private RectTransform _targetSlotRect;
    private bool _isHovering;

    private Coroutine _hideRoutine;
    private Coroutine _repositionRoutine;
    private int _serial;

    private void Awake()
    {
        GlobalUIRoot.AdoptService(transform);
        RefreshCanvasReference();
    }

    private void LateUpdate()
    {
        RefreshCanvasReference(_targetSlotRect, _currentView);

        if (!followAnchor || !_isHovering || _targetSlotRect == null || _currentView == null || !_currentView.IsActive)
            return;

        PositionNextToTarget(_targetSlotRect);
    }

    public void ShowHover(IHoverView view, RectTransform targetRect, object data, object context)
    {
        if (view == null || targetRect == null)
            return;

        if (!RefreshCanvasReference(targetRect, view))
            return;

        _serial++;
        _isHovering = true;
        _targetSlotRect = targetRect;

        CancelHide();

        if (_currentView != null && _currentView != view)
            _currentView.HideHover();

        _currentView = view;

        if (_currentView is MonoBehaviour currentViewBehaviour)
        {
            Canvas viewCanvas = currentViewBehaviour.GetComponent<Canvas>();
            if (viewCanvas != null)
                viewCanvas.sortingOrder = 999;
        }

        _currentView.ShowHover(data, context);
        PositionNextToTarget(_targetSlotRect);
        ScheduleDeferredReposition();
    }

    public void HideHover(IHoverView view, RectTransform targetRect, bool immediate = false)
    {
        if (immediate)
        {
            if (_currentView == view && _targetSlotRect == targetRect) HideImmediate();
            return;
        }
        if (_currentView == view && _targetSlotRect == targetRect)
            _isHovering = false;

        TryScheduleHide();
    }

    public void HideImmediate()
    {
        CancelHide();
        CancelDeferredReposition();
        _isHovering = false;
        _targetSlotRect = null;

        if (_currentView != null)
        {
            _currentView.HideHover();
            _currentView = null;
        }
    }

    public bool RefreshCanvasReference()
    {
        return RefreshCanvasReference(null, null);
    }

    private bool RefreshCanvasReference(RectTransform targetRect, IHoverView view)
    {
        Canvas resolvedCanvas = canvas;

        if (resolvedCanvas == null && view is MonoBehaviour viewBehaviour && viewBehaviour != null)
            resolvedCanvas = viewBehaviour.GetComponentInParent<Canvas>();

        if (resolvedCanvas == null && targetRect != null)
            resolvedCanvas = targetRect.GetComponentInParent<Canvas>();

        if (resolvedCanvas == null)
            resolvedCanvas = GlobalUIRoot.GetCanvas(GlobalCanvasLayer.Hover);

        if (resolvedCanvas == null)
            resolvedCanvas = FindFirstObjectByType<Canvas>();

        canvas = resolvedCanvas != null ? resolvedCanvas.rootCanvas : null;
        return canvas != null;
    }

    private void TryScheduleHide()
    {
        if (_isHovering)
            return;

        if (_currentView == null || !_currentView.IsActive)
            return;

        if (_hideRoutine != null)
            return;

        int mySerial = _serial;
        _hideRoutine = StartCoroutine(CoHideIfStillNotHover(mySerial));
    }

    private IEnumerator CoHideIfStillNotHover(int serialAtStart)
    {
        if (delayHideOneFrame)
            yield return null;

        if (extraHideDelay > 0f)
            yield return new WaitForSecondsRealtime(extraHideDelay);

        if (_serial != serialAtStart)
            yield break;

        if (_isHovering)
            yield break;

        HideImmediate();
    }

    private void CancelHide()
    {
        if (_hideRoutine == null)
            return;

        StopCoroutine(_hideRoutine);
        _hideRoutine = null;
    }

    private void ScheduleDeferredReposition()
    {
        CancelDeferredReposition();
        _repositionRoutine = StartCoroutine(CoDeferredReposition(_serial));
    }

    private void CancelDeferredReposition()
    {
        if (_repositionRoutine == null)
            return;

        StopCoroutine(_repositionRoutine);
        _repositionRoutine = null;
    }

    private IEnumerator CoDeferredReposition(int serialAtStart)
    {
        yield return null;

        if (_serial != serialAtStart)
            yield break;

        if (!RefreshCanvasReference(_targetSlotRect, _currentView))
            yield break;

        if (_currentView == null || _targetSlotRect == null)
            yield break;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_currentView.Rect);
        PositionNextToTarget(_targetSlotRect);
        _repositionRoutine = null;
    }

    private void PositionNextToTarget(RectTransform targetRect)
    {
        if (!RefreshCanvasReference(targetRect, _currentView))
            return;

        if (canvas == null || _currentView == null || targetRect == null)
            return;

        RectTransform viewRect = _currentView.Rect;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(viewRect);

        RectTransform canvasRect = canvas.transform as RectTransform;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        Rect targetLocalRect = GetLocalRect(targetRect, canvasRect, cam);
        Rect canvasLocalRect = canvasRect.rect;

        Vector3[] corners = new Vector3[4];
        targetRect.GetWorldCorners(corners);

        Vector2 rightTopScreen = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        Vector2 rightBottomScreen = RectTransformUtility.WorldToScreenPoint(cam, corners[3]);
        Vector2 leftTopScreen = RectTransformUtility.WorldToScreenPoint(cam, corners[1]);
        Vector2 leftBottomScreen = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);

        Vector2 centerRightScreen = (rightTopScreen + rightBottomScreen) * 0.5f;
        Vector2 centerLeftScreen = (leftTopScreen + leftBottomScreen) * 0.5f;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, centerRightScreen, cam, out Vector2 rightLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, centerLeftScreen, cam, out Vector2 leftLocal);

        Vector2 size = viewRect.rect.size;
        Vector2 pivot = viewRect.pivot;

        float rightLeftEdge = rightLocal.x + offset;
        Vector2 posRight = new Vector2(rightLeftEdge + pivot.x * size.x, rightLocal.y);

        float leftRightEdge = leftLocal.x - offset;
        Vector2 posLeft = new Vector2(leftRightEdge - (1f - pivot.x) * size.x, leftLocal.y);

        Vector2 chosen = ChoosePosAvoidOverlap(canvasLocalRect, targetLocalRect, size, pivot, posRight, posLeft);

        float chosenX = chosen.x;
        float y = ChooseYAlignSlot(canvasLocalRect, targetLocalRect, size, pivot, chosenX);
        Vector2 presentationOffset = _currentView is IHoverPositionOffsetProvider offsetProvider
            ? offsetProvider.HoverPositionOffset
            : Vector2.zero;
        viewRect.anchoredPosition = new Vector2(chosenX, y) + presentationOffset;

        if (logPositioningDebug)
        {
            CapstoneDiagnostics.EditorOnlyLog.Log(
                $"[HoverUIController] Positioned hover view. " +
                $"canvas={canvas.name}, canvasRect={canvasLocalRect}, " +
                $"target={targetRect.name}, targetRect={targetLocalRect}, " +
                $"view={viewRect.name}, viewSize={size}, pivot={pivot}, " +
                $"rightCandidate={posRight}, leftCandidate={posLeft}, chosen={new Vector2(chosenX, y)}, " +
                $"presentationOffset={presentationOffset}, " +
                $"offset={offset}, edgePadding={edgePadding}",
                this);
        }
    }

    private Vector2 ChoosePosAvoidOverlap(Rect canvasLocalRect, Rect targetRect, Vector2 panelSize, Vector2 pivot, Vector2 rightPos, Vector2 leftPos)
    {
        Rect targetExpanded = Expand(targetRect, 6f);
        Vector2 rightClamped = ClampToCanvas(canvasLocalRect, rightPos, panelSize, pivot);
        Vector2 leftClamped = ClampToCanvas(canvasLocalRect, leftPos, panelSize, pivot);
        Rect rightPanel = PanelRectAt(rightClamped, panelSize, pivot);
        Rect leftPanel = PanelRectAt(leftClamped, panelSize, pivot);

        bool rightOverlap = Intersects(targetExpanded, rightPanel);
        bool leftOverlap = Intersects(targetExpanded, leftPanel);

        if (!rightOverlap && leftOverlap)
            return rightClamped;

        if (!leftOverlap && rightOverlap)
            return leftClamped;

        if (!rightOverlap && !leftOverlap)
            return rightClamped;

        float rightArea = OverlapArea(targetExpanded, rightPanel);
        float leftArea = OverlapArea(targetExpanded, leftPanel);

        return rightArea <= leftArea ? rightClamped : leftClamped;
    }

    private float OverlapArea(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float yMax = Mathf.Min(a.yMax, b.yMax);
        float w = xMax - xMin;
        float h = yMax - yMin;

        if (w <= 0f || h <= 0f)
            return 0f;

        return w * h;
    }

    private Rect GetLocalRect(RectTransform rectTransform, RectTransform canvasRect, Camera cam)
    {
        Vector3[] worldCorners = new Vector3[4];
        rectTransform.GetWorldCorners(worldCorners);
        Vector2 p0 = ScreenToCanvasLocal(worldCorners[0], canvasRect, cam);
        Vector2 p2 = ScreenToCanvasLocal(worldCorners[2], canvasRect, cam);
        float xMin = Mathf.Min(p0.x, p2.x);
        float xMax = Mathf.Max(p0.x, p2.x);
        float yMin = Mathf.Min(p0.y, p2.y);
        float yMax = Mathf.Max(p0.y, p2.y);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private Vector2 ScreenToCanvasLocal(Vector3 world, RectTransform canvasRect, Camera cam)
    {
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, cam, out Vector2 localPoint);
        return localPoint;
    }

    private Rect PanelRectAt(Vector2 pos, Vector2 size, Vector2 pivot)
    {
        float left = pos.x - pivot.x * size.x;
        float bottom = pos.y - pivot.y * size.y;
        return new Rect(left, bottom, size.x, size.y);
    }

    private bool Intersects(Rect a, Rect b)
    {
        return a.Overlaps(b);
    }

    private Rect Expand(Rect rect, float amount)
    {
        return new Rect(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2f, rect.height + amount * 2f);
    }

    private Vector2 ClampToCanvas(Rect canvasRect, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        float minX = canvasRect.xMin + edgePadding + pivot.x * size.x;
        float maxX = canvasRect.xMax - edgePadding - (1f - pivot.x) * size.x;
        float minY = canvasRect.yMin + edgePadding + pivot.y * size.y;
        float maxY = canvasRect.yMax - edgePadding - (1f - pivot.y) * size.y;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        return pos;
    }

    private float ChooseYAlignSlot(Rect canvasRect, Rect targetRect, Vector2 panelSize, Vector2 panelPivot, float xFixed)
    {
        float targetTop = targetRect.yMax - targetRect.height / 2f;
        float targetBottom = targetRect.yMin + targetRect.height / 2f;
        float yTopAlign = targetTop - (1f - panelPivot.y) * panelSize.y;
        float yBottomAlign = targetBottom + panelPivot.y * panelSize.y;

        float yTopClamped = ClampY(canvasRect, panelSize, panelPivot, yTopAlign);
        float yBottomClamped = ClampY(canvasRect, panelSize, panelPivot, yBottomAlign);

        Rect targetExpanded = Expand(targetRect, 4f);
        Rect panelTopRect = PanelRectAt(new Vector2(xFixed, yTopClamped), panelSize, panelPivot);
        Rect panelBottomRect = PanelRectAt(new Vector2(xFixed, yBottomClamped), panelSize, panelPivot);

        float topOverlap = OverlapArea(targetExpanded, panelTopRect);
        float bottomOverlap = OverlapArea(targetExpanded, panelBottomRect);

        if (topOverlap < bottomOverlap)
            return yTopClamped;

        if (bottomOverlap < topOverlap)
            return yBottomClamped;

        return yTopClamped;
    }

    private float ClampY(Rect canvasRect, Vector2 size, Vector2 pivot, float y)
    {
        float minY = canvasRect.yMin + edgePadding + pivot.y * size.y;
        float maxY = canvasRect.yMax - edgePadding - (1f - pivot.y) * size.y;
        return Mathf.Clamp(y, minY, maxY);
    }
}

// Frozen source: StatusHudEntryView.cs; exact pre-change snapshot. No production type is replaced.
/// <summary>
/// 책임 :
/// - 상태 HUD 엔트리 하나의 아이콘, 스택, 지속시간, 강조 표시를 실제 UI로 그린다.
/// - 마우스 hover 시 공통 hover 시스템을 통해 상태 툴팁을 띄워 상태 UI와 툴팁 계층을 분리한다.
/// </summary>
public sealed class StatusHudEntryView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Refs")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image durationFillImage;
    [SerializeField] private TMP_Text stackText;
    [SerializeField] private TMP_Text durationText;

    [Header("Style")]
    [SerializeField] private Vector2 entrySize = new(48f, 48f);
    [SerializeField] private Color backgroundColor = new(0.1f, 0.12f, 0.18f, 0.9f);
    [SerializeField] private Color highlightedBackgroundColor = new(0.33f, 0.21f, 0.08f, 0.92f);
    [SerializeField] private Color iconColor = Color.white;
    [SerializeField] private Color highlightedIconColor = new(1f, 0.95f, 0.72f, 1f);
    [SerializeField] private Color durationFillColor = new(0.95f, 0.77f, 0.26f, 0.95f);
    [SerializeField] private Color outlineColor = new(0.02f, 0.04f, 0.08f, 0.6f);
    [SerializeField] private Vector2 outlineDistance = new(1f, -1f);

    [Header("Fallback Layout")]
    [SerializeField] private Vector2 iconOffsetMin = new(8f, 8f);
    [SerializeField] private Vector2 iconOffsetMax = new(-8f, -8f);
    [SerializeField] private Vector2 durationFillOffsetMin = new(4f, 40f);
    [SerializeField] private Vector2 durationFillOffsetMax = new(-4f, -4f);
    [SerializeField] private int stackFontSize = 13;
    [SerializeField] private int durationFontSize = 11;
    [SerializeField] private Vector2 stackOffsetMin = new(-4f, -3f);
    [SerializeField] private Vector2 stackOffsetMax = new(-4f, -3f);
    [SerializeField] private Vector2 durationOffsetMin = new(4f, 2f);
    [SerializeField] private Vector2 durationOffsetMax = new(-4f, 14f);

    private RectTransform rectTransform;
    private StatusHudEntry currentEntry;
    private bool isPointerHovering;
    private bool progressLayoutActive;
    private Vector2 originalStackAnchorMin, originalStackAnchorMax, originalStackOffsetMin, originalStackOffsetMax;
    private TextAlignmentOptions originalStackAlignment;
    private float originalStackFontSize, originalStackOutlineWidth;
    private Color32 originalStackOutlineColor;
    private Color originalStackColor;
    private Outline entryOutline;
    private bool visualTreeReady;
    private bool hasBoundEntry;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public int DiagnosticVisualSetupCount { get; private set; }
    public int DiagnosticStackFormatCount { get; private set; }
#endif


    private void Awake()
    {
        EnsureVisualTree();
    }

    private void OnDisable()
    {
        hasBoundEntry = false;
        HideHoverIfNeeded();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        visualTreeReady = false;
        hasBoundEntry = false;
    }
#endif

    public void Bind(in StatusHudEntry entry)
    {
        EnsureVisualTree();
        bool updateStack = !hasBoundEntry || currentEntry.ShowStacks != entry.ShowStacks ||
            currentEntry.StackCount != entry.StackCount || currentEntry.ProgressText != entry.ProgressText;
        currentEntry = entry;

        gameObject.SetActive(entry.IsVisible);
        if (!entry.IsVisible)
        {
            HideHoverIfNeeded();
            return;
        }

        backgroundImage.color = entry.IsHighlighted
            ? highlightedBackgroundColor
            : backgroundColor;

        iconImage.sprite = entry.Icon;
        iconImage.enabled = entry.Icon != null;
        iconImage.color = entry.IsHighlighted
            ? highlightedIconColor
            : iconColor;

        bool hasProgress = !string.IsNullOrWhiteSpace(entry.ProgressText);
        SetProgressLayout(hasProgress);
        if (updateStack)
            stackText.text = hasProgress ? entry.ProgressText
                : entry.ShowStacks && entry.StackCount > 0 ? FormatStack(entry.StackCount) : string.Empty;
        durationText.text = entry.ShowDuration && entry.RemainingTime > 0f
            ? entry.RemainingTime.ToString("0.0")
            : string.Empty;

        if (entry.ShowDuration && entry.MaxTime > 0f && entry.RemainingTime > 0f)
        {
            durationFillImage.enabled = true;
            durationFillImage.fillAmount = Mathf.Clamp01(entry.RemainingTime / entry.MaxTime);
        }
        else
        {
            durationFillImage.enabled = false;
            durationFillImage.fillAmount = 0f;
        }

        hasBoundEntry = true;
        if (isPointerHovering && UIManager.Instance != null)
            UIManager.Instance.ShowHover(StatusHudTooltipView.Instance, rectTransform, currentEntry);
    }

    private string FormatStack(int count)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DiagnosticStackFormatCount++;
#endif
        return count.ToString();
    }

    private void SetProgressLayout(bool enabled)
    {
        if (progressLayoutActive == enabled)
            return;

        RectTransform textRect = stackText.rectTransform;
        if (enabled)
        {
            originalStackAnchorMin = textRect.anchorMin;
            originalStackAnchorMax = textRect.anchorMax;
            originalStackOffsetMin = textRect.offsetMin;
            originalStackOffsetMax = textRect.offsetMax;
            originalStackAlignment = stackText.alignment;
            originalStackFontSize = stackText.fontSize;
            originalStackColor = stackText.color;
            originalStackOutlineColor = stackText.outlineColor;
            originalStackOutlineWidth = stackText.outlineWidth;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(1f, 1f);
            textRect.offsetMax = new Vector2(-1f, -1f);
            stackText.alignment = TextAlignmentOptions.Center;
            stackText.fontSize = Mathf.Max(13f, originalStackFontSize);
            stackText.color = Color.white;
            stackText.outlineColor = Color.black;
            stackText.outlineWidth = 0.2f;
        }
        else
        {
            textRect.anchorMin = originalStackAnchorMin;
            textRect.anchorMax = originalStackAnchorMax;
            textRect.offsetMin = originalStackOffsetMin;
            textRect.offsetMax = originalStackOffsetMax;
            stackText.alignment = originalStackAlignment;
            stackText.fontSize = originalStackFontSize;
            stackText.color = originalStackColor;
            stackText.outlineColor = originalStackOutlineColor;
            stackText.outlineWidth = originalStackOutlineWidth;
        }
        progressLayoutActive = enabled;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!currentEntry.IsVisible || UIManager.Instance == null)
            return;

        isPointerHovering = true;
        UIManager.Instance.ShowHover(StatusHudTooltipView.Instance, rectTransform, currentEntry);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideHoverIfNeeded();
    }

    private void HideHoverIfNeeded()
    {
        if (!isPointerHovering)
            return;

        if (UIManager.Instance != null)
            UIManager.Instance.HideHover(StatusHudTooltipView.Instance, rectTransform);

        isPointerHovering = false;
    }

    /// <summary>
    /// 책임 :
    /// - 프리팹 없이도 상태 HUD 엔트리가 동작하도록 배경, 아이콘, 스택, 지속시간 UI를 코드로 생성한다.
    /// - presenter가 런타임 풀링만으로 엔트리 뷰를 만들고 재사용할 수 있게 최소 시각 구조를 보장한다.
    /// </summary>
    private void EnsureVisualTree()
    {
        // Binding updates presentation values, not the already configured hierarchy/style.
        if (visualTreeReady && rectTransform != null && backgroundImage != null &&
            iconImage != null && durationFillImage != null && stackText != null &&
            durationText != null && entryOutline != null)
            return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DiagnosticVisualSetupCount++;
#endif
        rectTransform ??= gameObject.GetComponent<RectTransform>();
        if (rectTransform == null ||
            backgroundImage == null ||
            iconImage == null ||
            durationFillImage == null ||
            stackText == null ||
            durationText == null)
        {
            RuntimePresentationFallbackAudit.Record(
                this,
                "Status HUD entry visual fallback",
                "an authored StatusHudEntryView prefab with icon, duration, stack, and text references");
        }

        rectTransform ??= gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        if (rectTransform.sizeDelta == Vector2.zero)
            rectTransform.sizeDelta = entrySize;

        backgroundImage ??= gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        backgroundImage.color = backgroundColor;
        entryOutline = gameObject.GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
        entryOutline.effectColor = outlineColor;
        entryOutline.effectDistance = outlineDistance;

        iconImage ??= CreateImage("Icon", iconOffsetMin, iconOffsetMax, 0);
        iconImage.preserveAspect = true;

        durationFillImage ??= CreateImage("DurationFill", durationFillOffsetMin, durationFillOffsetMax, 1);
        durationFillImage.type = Image.Type.Filled;
        durationFillImage.fillMethod = Image.FillMethod.Horizontal;
        durationFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        durationFillImage.color = durationFillColor;

        stackText ??= CreateText("StackText", stackFontSize, TextAlignmentOptions.TopRight, new Vector2(0f, 0f), new Vector2(0f, 0f), stackOffsetMin, stackOffsetMax);
        durationText ??= CreateText("DurationText", durationFontSize, TextAlignmentOptions.Bottom, new Vector2(0f, 0f), new Vector2(1f, 0f), durationOffsetMin, durationOffsetMax);
        visualTreeReady = true;
    }

    private Image CreateImage(string name, Vector2 offsetMin, Vector2 offsetMax, int siblingIndex)
    {
        Transform existing = transform.Find(name);
        GameObject child = existing != null ? existing.gameObject : new GameObject(name);
        child.transform.SetParent(transform, false);
        child.transform.SetSiblingIndex(siblingIndex);

        RectTransform childRect = child.GetComponent<RectTransform>() ?? child.AddComponent<RectTransform>();
        childRect.anchorMin = Vector2.zero;
        childRect.anchorMax = Vector2.one;
        childRect.offsetMin = offsetMin;
        childRect.offsetMax = offsetMax;

        Image image = child.GetComponent<Image>() ?? child.AddComponent<Image>();
        return image;
    }

    private TMP_Text CreateText(
        string name,
        int fontSize,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        Transform existing = transform.Find(name);
        GameObject child = existing != null ? existing.gameObject : new GameObject(name);
        child.transform.SetParent(transform, false);

        RectTransform childRect = child.GetComponent<RectTransform>() ?? child.AddComponent<RectTransform>();
        childRect.anchorMin = anchorMin;
        childRect.anchorMax = anchorMax;
        childRect.offsetMin = offsetMin;
        childRect.offsetMax = offsetMax;

        TMP_Text text = child.GetComponent<TMP_Text>() ?? child.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.color = Color.white;
        text.richText = false;
        text.enableWordWrapping = false;
        return text;
    }
}

// Frozen source: StatusHudTooltipView.cs; exact pre-change snapshot. No production type is replaced.
/// <summary>
/// 책임 :
/// - 상태 HUD 엔트리에 마우스를 올렸을 때 아이콘, 이름, 서사 설명, 효과 설명을 보여주는 호버 툴팁 뷰를 제공한다.
/// - 상태 소유 계층이 직접 UI를 그리지 않고, HoverUIController를 통해 공통 hover 흐름을 재사용하게 만든다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class StatusHudTooltipView : MonoBehaviour, IHoverView
{
    private static StatusHudTooltipView instance;

    [Header("Refs")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text storyText;
    [SerializeField] private TMP_Text effectText;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;

    public static StatusHudTooltipView Instance => EnsureInstance();
    public RectTransform Rect => rectTransform;
    public bool IsActive => gameObject.activeSelf && canvasGroup != null && canvasGroup.alpha > 0.01f;

    public static StatusHudTooltipView EnsureInstance()
    {
        // Fixture-only composition; this fallback is outside all measured operations.
        if (instance == null) throw new InvalidOperationException("Create the authored baseline tooltip fixture first.");
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureVisualTree();
        GlobalUIRoot.AdoptToCanvas(GlobalCanvasLayer.Hover, transform, false);
        OpenUI();
        HideHover();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void OpenUI()
    {
        gameObject.SetActive(true);
    }

    public void CloseUI()
    {
        HideHover();
    }

    public void ShowHover(object data, object context = null)
    {
        if (data is UnityGAS.AbilityDefinition ability)
        {
            ShowAbility(ability, context as ItemDetailContext);
            return;
        }
        if (data is not StatusHudEntry entry)
        {
            HideHover();
            return;
        }

        EnsureVisualTree();

        if (iconImage != null)
        {
            iconImage.sprite = entry.Icon;
            iconImage.enabled = entry.Icon != null;
        }

        nameText.text = string.IsNullOrWhiteSpace(entry.NameText) ? entry.StatusId : entry.NameText;
        storyText.text = entry.StoryText ?? string.Empty;

        string stackLine = entry.ShowStacks ? GameText.Format("code.statushudtooltipview.1c79f7fcb9", "스택: {0}", entry.StackCount) : string.Empty;
        string durationLine = entry.ShowDuration
            ? GameText.Format("code.statushudtooltipview.8f6c61be38", "지속시간: {0:0.0}s", Mathf.Max(0f, entry.RemainingTime))
            : string.Empty;
        string runtimeStats = string.IsNullOrEmpty(stackLine)
            ? durationLine
            : string.IsNullOrEmpty(durationLine)
                ? stackLine
                : $"{stackLine}\n{durationLine}";

        effectText.text = string.IsNullOrWhiteSpace(entry.EffectText)
            ? runtimeStats
            : string.IsNullOrWhiteSpace(runtimeStats)
                ? entry.EffectText
                : $"{entry.EffectText}\n\n{runtimeStats}";

        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void HideHover()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void ShowAbility(UnityGAS.AbilityDefinition ability, ItemDetailContext context)
    {
        EnsureVisualTree();
        context ??= new ItemDetailContext();
        iconImage.sprite = ability.icon;
        iconImage.enabled = ability.icon != null;
        nameText.text = ability.DisplayName;
        storyText.text = GameText.Format("code.statushudtooltipview.618ce811ec", "재사용 대기시간: {0:0.##}초", ability.cooldown);
        var body = new System.Text.StringBuilder();
        if (ability.sourceObject is IAbilityTooltipVariantProvider variants &&
            variants.GetAbilityTooltipVariantCount(ability, context) > 0)
        {
            int count = variants.GetAbilityTooltipVariantCount(ability, context);
            for (int i = 0; i < count; i++)
            {
                var variant = variants.BuildAbilityTooltipVariant(ability, i, context);
                if (i > 0) body.AppendLine();
                body.AppendLine(variant.Title);
                body.AppendLine(variant.Body);
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(ability.Description)) body.AppendLine(ability.Description);
            if (ability.sourceObject is IDetailProvider provider)
                body.AppendLine(provider.BuildDetailBlock(context).body);
        }
        effectText.text = DetailTextFormatter.Format(body.ToString().TrimEnd(), null);
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// 책임 :
    /// - 프리팹 없이도 상태 HUD 툴팁이 동작하도록 최소 UI 트리를 코드로 생성한다.
    /// - 배경, 아이콘, 이름, 서사 설명, 효과 설명 영역을 한 곳에서 보장해 hover 시스템과의 연결만으로 툴팁이 보이게 만든다.
    /// </summary>
    private void EnsureVisualTree()
    {
        if (gameObject.GetComponent<RectTransform>() == null ||
            backgroundImage == null ||
            transform.Find("Icon") == null ||
            transform.Find("Name") == null ||
            transform.Find("Story") == null ||
            transform.Find("Effect") == null)
        {
            RuntimePresentationFallbackAudit.Record(
                this,
                "Status HUD tooltip visual fallback",
                "an authored StatusHudTooltipView prefab with background, icon, name, story, and effect references");
        }

        bool createdRectTransform = false;
        rectTransform = gameObject.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = gameObject.AddComponent<RectTransform>();
            createdRectTransform = true;
        }

        if (createdRectTransform)
        {
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(240f, 120f);
        }

        canvasGroup ??= GetComponent<CanvasGroup>();

        backgroundImage ??= gameObject.GetComponent<Image>();
        if (backgroundImage == null)
        {
            backgroundImage = gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.08f, 0.1f, 0.16f, 0.94f);
        }

        Outline outline = gameObject.GetComponent<Outline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.78f, 0.84f, 0.97f, 0.4f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        VerticalLayoutGroup layout = gameObject.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
        }

        ContentSizeFitter fitter = gameObject.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        iconImage ??= CreateIcon("Icon");
        nameText ??= CreateText("Name", 18, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Color(0.98f, 0.95f, 0.82f, 1f));
        storyText ??= CreateText("Story", 14, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white);
        effectText ??= CreateText("Effect", 13, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.79f, 0.87f, 0.98f, 1f));
    }

    /// <summary>
    /// 책임 :
    /// - 상태 툴팁이 사용하는 아이콘 이미지를 코드 fallback으로 생성한다.
    /// - 수동 authoring 프리팹이 없더라도 아이콘/이름/설명 레이아웃이 유지되게 만든다.
    /// </summary>
    private Image CreateIcon(string name)
    {
        RectTransform childRect = GetOrCreateUiChild(name);
        GameObject child = childRect.gameObject;
        childRect.anchorMin = new Vector2(0f, 1f);
        childRect.anchorMax = new Vector2(0f, 1f);
        childRect.sizeDelta = new Vector2(32f, 32f);

        LayoutElement layoutElement = child.GetComponent<LayoutElement>() ?? child.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 32f;
        layoutElement.preferredHeight = 32f;

        Image image = child.GetComponent<Image>() ?? child.AddComponent<Image>();
        image.preserveAspect = true;
        return image;
    }

    private TMP_Text CreateText(string name, int fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, Color color)
    {
        RectTransform childRect = GetOrCreateUiChild(name);
        GameObject child = childRect.gameObject;
        childRect.anchorMin = new Vector2(0f, 1f);
        childRect.anchorMax = new Vector2(1f, 1f);

        TMP_Text text = child.GetComponent<TMP_Text>() ?? child.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.richText = true;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    /// <summary>
    /// 책임 :
    /// - fallback UI 생성 시 같은 이름의 일반 Transform 자식이 있어도 RectTransform 기반 UI 자식을 안전하게 보장한다.
    /// - 잘못 authoring된 레거시 자식은 비활성화하고 새 UI 자식을 만들어 MissingComponentException을 막는다.
    /// </summary>
    private RectTransform GetOrCreateUiChild(string name)
    {
        Transform existing = transform.Find(name);
        if (existing != null && existing.TryGetComponent(out RectTransform existingRect))
            return existingRect;

        if (existing != null)
        {
            existing.gameObject.SetActive(false);
            existing.name = $"{name}_NonUiLegacy";
        }

        GameObject child = new(name, typeof(RectTransform));
        child.transform.SetParent(transform, false);
        return child.GetComponent<RectTransform>();
    }
}

// Frozen source: RunTimerHUD.cs; exact pre-change snapshot. No production type is replaced.
/// <summary>
/// 책임 :
/// - RunTimeLimitSystem의 남은 시간을 사람이 읽기 쉬운 텍스트로 출력한다.
/// - 시간이 적을 때와 외부 pause 중일 때의 색상 변화, 표시/숨김만 담당하고 타이머 로직은 소유하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RunTimerHUD : MonoBehaviour
{
    public static RunTimerHUD Instance { get; private set; }

    [Header("Binding")]
    [SerializeField] private RunTimeLimitSystem timeLimitSystem;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private GameObject visibleRoot;

    [Header("Visual")]
    [SerializeField] private string timeFormat = "{0:00}:{1:00}";
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color lowTimeColor = new(1f, 0.35f, 0.25f, 1f);
    [SerializeField] private Color pausedColor = new(0.6f, 0.85f, 1f, 1f);
    [SerializeField] private Color outlineColor = Color.black;
    [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.2f;
    [SerializeField] private bool hideWhenTimerInactive = true;
    [SerializeField] private bool pulseWhilePaused = true;
    [SerializeField] private float pausedPulseSpeed = 2.5f;
    [SerializeField, Range(0f, 1f)] private float pausedPulseMinAlpha = 0.55f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (visibleRoot == null)
            visibleRoot = gameObject;

        ApplyTextOutline();
    }

    private void OnEnable()
    {
        RunTimeLimitSystem.InstanceChanged += HandleTimeLimitSystemChanged;
        ApplyTextOutline();
        ResolveTimeLimitBinding();
    }

    private void OnValidate()
    {
        outlineWidth = Mathf.Clamp01(outlineWidth);
    }

    private void OnDisable()
    {
        RunTimeLimitSystem.InstanceChanged -= HandleTimeLimitSystemChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (timeLimitSystem == null)
        {
            SetVisible(false);
            return;
        }

        if (!timeLimitSystem.IsRunning)
        {
            if (timeLimitSystem.IsExpired)
            {
                SetVisible(true);
                UpdateTimeText(0f, true, false);
                return;
            }

            SetVisible(!hideWhenTimerInactive);

            if (!hideWhenTimerInactive)
            {
                UpdateTimeText(
                    timeLimitSystem.InactivePreviewSeconds,
                    false,
                    true);
            }

            return;
        }

        SetVisible(true);
        UpdateTimeText(
            timeLimitSystem.RemainingSeconds,
            timeLimitSystem.IsLowTime,
            timeLimitSystem.IsVisuallyPaused);
    }

    /// <summary>
    /// 책임 :
    /// - 남은 시간을 mm:ss 형식으로 표시하고, 상태에 따라 우선순위 있는 색상 규칙을 적용한다.
    /// - 외부 pause와 스테이지 정책 정지를 같은 "멈춤 상태"로 표현해, 시간이 흐르지 않는 씬에서도 즉시 읽히게 한다.
    /// </summary>
    private void UpdateTimeText(float remainingSeconds, bool isLowTime, bool isPaused)
    {
        if (timeText == null)
            return;

        float safeSeconds = Mathf.Max(0f, remainingSeconds);
        int totalSeconds = Mathf.CeilToInt(safeSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timeText.text = string.Format(timeFormat, minutes, seconds);
        Color targetColor = isPaused
            ? pausedColor
            : (isLowTime ? lowTimeColor : normalColor);

        if (isPaused && pulseWhilePaused)
        {
            float pulse = Mathf.Lerp(
                pausedPulseMinAlpha,
                1f,
                Mathf.PingPong(Time.unscaledTime * pausedPulseSpeed, 1f));

            targetColor.a *= pulse;
        }

        timeText.color = targetColor;
    }

    private void ApplyTextOutline()
    {
        if (timeText == null)
            return;

        timeText.outlineColor = outlineColor;
        timeText.outlineWidth = outlineWidth;
    }

    private void SetVisible(bool isVisible)
    {
        if (visibleRoot != null && visibleRoot.activeSelf != isVisible)
            visibleRoot.SetActive(isVisible);
    }

    public void BindTimeLimitSystem(RunTimeLimitSystem system)
    {
        timeLimitSystem = system;
    }

    public void UnbindTimeLimitSystem(RunTimeLimitSystem system)
    {
        if (system == null || timeLimitSystem != system)
            return;

        timeLimitSystem = null;
    }

    private void HandleTimeLimitSystemChanged(RunTimeLimitSystem system)
    {
        if (system == null)
        {
            UnbindTimeLimitSystem(timeLimitSystem);
            return;
        }

        BindTimeLimitSystem(system);
    }

    private void ResolveTimeLimitBinding()
    {
        if (timeLimitSystem != null)
            return;

        if (RunTimeLimitSystem.Instance != null)
        {
            BindTimeLimitSystem(RunTimeLimitSystem.Instance);
            return;
        }

        if (ShouldUseAutoFindFallback())
            timeLimitSystem = FindAnyObjectByType<RunTimeLimitSystem>();
    }

    private static bool ShouldUseAutoFindFallback()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return true;
#else
        return false;
#endif
    }
}

// Frozen source: DamagePopupWorldText.cs; exact pre-change snapshot. No production type is replaced.
/// <summary>
/// 책임 : 월드 공간에 생성된 데미지 텍스트의 표시, 이동, 크기 변화, 페이드 아웃 수명을 관리한다.
/// 스스로의 연출만 담당하며, 언제 어디에 생성할지는 담당하지 않는다.
/// </summary>
public class DamagePopupWorldText : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TextMeshPro text;
    [SerializeField] private SpriteRenderer optionalSpriteRenderer;

    [Header("Motion (World space)")]
    [SerializeField] private Vector3 moveVelocity = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private float lifetime = 0.75f;
    [SerializeField] private float fadeOutRatio = 0.55f;

    [Header("Scale")]
    [SerializeField] private float startScale = 0.9f;
    [SerializeField] private float endScale = 1.1f;

    [Header("Facing")]
    [Tooltip("항상 카메라 정면을 향하게 할지 여부. 2D 정면 게임이면 보통 꺼도 된다.")]
    [SerializeField] private bool faceCamera = false;

    private float _t;
    private Color _textColor;
    private Color _spriteColor;
    private bool _hasSprite;
    private Vector3 _runtimeMoveVelocity;
    private float _runtimeLifetime;
    private float _runtimeFadeOutRatio;
    private float _runtimeStartScale;
    private float _runtimeEndScale;
    private float _defaultFontSize;

    private void Reset()
    {
        text = GetComponentInChildren<TextMeshPro>();

        if (optionalSpriteRenderer == null)
            optionalSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Awake()
    {
        if (text == null)
            text = GetComponentInChildren<TextMeshPro>();

        if (optionalSpriteRenderer == null)
            optionalSpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (text != null)
        {
            _textColor = text.color;
            _defaultFontSize = text.fontSize;
        }

        _hasSprite = optionalSpriteRenderer != null;
        if (_hasSprite)
            _spriteColor = optionalSpriteRenderer.color;

        ResetRuntimeStyle();
    }

    /// <summary>
    /// 책임 : 표시할 데미지 값을 반영하고 연출 상태를 초기화한다.
    /// </summary>
    public void Setup(int amount)
    {
        Setup(amount.ToString());
    }

    /// <summary>
    /// 책임 : 표시할 커스텀 텍스트를 반영하고 연출 상태를 초기화한다.
    /// </summary>
    public void Setup(string content)
    {
        ResetRuntimeStyle();
        ApplyContent(content);
    }

    /// <summary>
    /// 책임 :
    /// - DamagePopupFormatProfileSO가 결정한 최종 표시 모델을 반영한다.
    /// - 팝업 인스턴스마다 색/폰트/이동/수명 값을 독립적으로 덮어쓸 수 있게 한다.
    /// </summary>
    public void Setup(DamagePopupViewModel viewModel)
    {
        _runtimeMoveVelocity = viewModel.MoveVelocity;
        _runtimeLifetime = Mathf.Max(0.05f, viewModel.Lifetime);
        _runtimeFadeOutRatio = Mathf.Clamp01(viewModel.FadeOutRatio);
        _runtimeStartScale = Mathf.Max(0.01f, viewModel.StartScale);
        _runtimeEndScale = Mathf.Max(0.01f, viewModel.EndScale);

        _textColor = viewModel.TextColor;

        if (text != null && viewModel.OverrideFontSize)
            text.fontSize = viewModel.FontSize;

        ApplyContent(viewModel.Text);
    }

    private void ApplyContent(string content)
    {
        _t = 0f;

        if (text != null)
            text.text = string.IsNullOrWhiteSpace(content) ? string.Empty : content;

        transform.localScale = Vector3.one * _runtimeStartScale;

        if (text != null)
            text.color = _textColor;

        if (_hasSprite)
            optionalSpriteRenderer.color = _spriteColor;
    }

    private void Update()
    {
        _t += TimeScalePausePlayback.PresentationDeltaTime;
        float p = Mathf.Clamp01(_t / _runtimeLifetime);

        transform.position += _runtimeMoveVelocity * TimeScalePausePlayback.PresentationDeltaTime;
        transform.localScale = Vector3.one * Mathf.Lerp(_runtimeStartScale, _runtimeEndScale, p);

        if (faceCamera)
            UpdateFacing();

        if (p >= _runtimeFadeOutRatio)
        {
            float fp = (p - _runtimeFadeOutRatio) / Mathf.Max(0.0001f, 1f - _runtimeFadeOutRatio);
            float alpha = Mathf.Lerp(1f, 0f, fp);

            if (text != null)
            {
                Color c = _textColor;
                c.a = alpha;
                text.color = c;
            }

            if (_hasSprite)
            {
                Color c = _spriteColor;
                c.a = alpha;
                optionalSpriteRenderer.color = c;
            }
        }

        if (_t >= _runtimeLifetime)
            Destroy(gameObject);
    }

    private void ResetRuntimeStyle()
    {
        _runtimeMoveVelocity = moveVelocity;
        _runtimeLifetime = Mathf.Max(0.05f, lifetime);
        _runtimeFadeOutRatio = Mathf.Clamp01(fadeOutRatio);
        _runtimeStartScale = Mathf.Max(0.01f, startScale);
        _runtimeEndScale = Mathf.Max(0.01f, endScale);

        if (text != null && _defaultFontSize > 0f)
            text.fontSize = _defaultFontSize;
    }

    /// <summary>
    /// 책임 : 필요할 때 월드 텍스트가 카메라 정면을 향하도록 보정한다.
    /// </summary>
    private void UpdateFacing()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        transform.forward = cam.transform.forward;
    }
}
}
#endif

