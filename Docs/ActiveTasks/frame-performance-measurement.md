---
status: complete
authority: active-task
category: verification
last_reviewed: 2026-10-07
---

# Frame Performance Measurement

- Mode: Verification/performance measurement, requested after the completed optimization slice.
- Goal: Record matched before/after operation costs, controlled TestScene player-loop frame times, and available native allocation/GPU samples.
- Allowed: Editor-only benchmark helpers, transient isolated Play Mode fixtures, generated measurement artifacts, scoped temporary baseline-source replacement while the Editor is closed, and task memory.
- Preserve: Current user/concurrent changes; authored scenes/prefabs and serialized schemas; gameplay/bootstrap settings; persistent profiles. No new runtime manager or saved scene object.
- Player measurement: Development Windows build adds a measurement-only component to the build copy of the existing TestScene control object. No saved scene/ProjectSettings changes. Runtime resolution/rate overrides are transient; test profile isolation remains with the existing sandbox.
- Baseline: Exact pre-optimization source snapshots retained in the task's temporary backup; clean FSM source from the recorded starting commit. For the prefab, change only the three investigated raycast flags.
- Conditions: Same TestScene, seed, content, uncapped runtime settings restored afterward, 5-second warmup and 8-second capture windows. Distinguish Editor frame rate from standalone player performance and individual operation/GPU segment costs from total frame costs.
- Representative state: Normal HP/resource and seven authored HUD heart tokens. One counted test-owned State.Invulnerable tag keeps the target alive; physical pushing/AI remain. The first inflated-HP fixture was excluded after it generated about 100000 heart tokens.
- Validity: Native counters require calibration; missing/zero unsupported measurements are unavailable. Store raw samples, median/p95 and measured workload counts; do not substitute CPU submission time for GPU time.
- Concurrency: Serialize Unity runs; do not overwrite another task's edits or close unrelated/running batch jobs.
- Documentation: SessionLog and measured report; update the completed optimization outcome with actual measured limits. Architecture/Contracts/Presentation HTML remain unchanged.

## Outcome

- Two complete matching Windows Development builds, corrected corresponding scripts-only rebuilds, four valid ABBA Player runs and two isolated shader GPU ABBA runs completed. Current production source/prefab hashes match all twenty optimized snapshots; saved profiles remain unchanged.
- All 16 CPU operation and 12 UI comparison cases returned equivalent results. Busy chase for 60 mobs: 1.628 → 0.785 ms. Stable tooltip: 0.969 → 0.00720 ms. Popup request CPU: 0.244 → 0.0715 ms, excluding deferred native destruction/rendering.
- Popup stream Player frame median: 6.339 → 4.833 ms; GC median: 17198 → 3168 B/frame (81.6% reduction). p95 did not improve. Whole-game FPS gain is not established; 30-mob average FPS was unchanged and other runs had overlapping ranges.
- Native isolated ground shader GPU segments improved 16–19%. Legacy preparing control cost increased 4–10% and projectile control 2–4% in repeat measurements. Suggested Later: evaluate conditional sampling/compiler cost in those legacy branches; no new shader fix in Verification mode.
- Editor operation shutdown logged one assertion/service-root recreation through unchanged OutOfCombatMoveSpeedRuntime effect-end presentation and RuntimeServiceOwnership. This happened at ExitPlayMode after the measured comparisons. Suggested Later: investigate shutdown spawn ownership; no Console-clean shutdown claim or lifecycle fix in this slice.
- Player frame GC-byte calibration passed. Operation-specific GC bytes/event counts were unavailable and are reported as such, never as zero allocation.
- [Measurement report and raw artifacts](../SessionLogs/2026-10-07-frame-performance-measurement/README.md), [measurement structure](../StructureMemory/FramePerformanceMeasurement.md), and [SessionLog](../SessionLogs/2026-10-07.md). ErrorLog records the discarded fixture pitfall; no new DecisionLog entry or Presentation HTML change.
