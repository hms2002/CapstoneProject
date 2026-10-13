---
status: complete
authority: active-task
category: implementation
last_reviewed: 2026-10-10
---

# Lightning Spear Placement Structure

- Mode: Implementation, explicitly authorized after the responsibility-split plan.
- Goal: Separate synchronous mark placement planning and map/physics queries while retaining current placement results and optimized performance.
- Allowed: Plain internal planner/query/request collaborators in existing Gameplay assembly, narrow RuntimeState delegation, Editor benchmark/validation adaptation and focused tests; task memory.
- Preserve: Existing heap algorithm, exact target-first selection, distance/x/y tie order, RNG consumption, reservations/spacing, wall/layer/name policies, cast/landing semantics and buffer capacity reuse. Preserve all public MonoBehaviour/serialized APIs, assets/GUIDs, coroutine copy-before-yield, mark ownership, cancellation/cleanup, data schemas and unrelated dirty work.
- Ownership: RuntimeState retains live actors, input, GAS/coroutines/spawn/audio/cleanup. Planner owns synchronous candidate/target/request scratch and composes PlacementSearch. WorldQuery owns frame-scoped tilemap discovery and physics result scratch, providing the same facts to placement and rush callers without merging their masks.
- Excluded: New algorithm, map-role/layer migration, safety policy change, mark registry/rush/recovered-volley redesign, managers/singletons/DDOL, scene/prefab/SO/asmdef/settings and Architecture/Contracts/HTML edits.
- Validation: Freeze current optimized sources before editing, run existing seeded fixture suite before/after, compare current optimized output/count/order/rooms/RNG and work counters, retain independent legacy rules, cold/warm time distributions and meaningful third-strike/scratch/cancellation/lifecycle regressions. Run MSBuild once generated projects include new files, and real Unity compile/tests with serialized launches.
- Risks: Scratch result alias across E yield and third-strike reuse, stale request cache, dynamic spacing state, diagnostics coupling or changed trigger/order semantics. No per-candidate callback allocation.
- Success: RuntimeState no longer owns placement pipeline/physical discovery buffers; results and operation counts remain identical; warmed/cold comparisons have no unexplained material regression. Report unavailable allocation or noisy timing rather than declaring equal costs.
- Documentation: SessionLog, narrow Lightning Spear StructureMemory and retained classification backlog context. No automatic source-of-truth or Presentation HTML update.

## Outcome

- RuntimeState composes internal PlacementPlanner and WorldQuery. Planner owns synchronous target/candidate/reservation/search state; WorldQuery owns frame map caches and physical result buffers. RuntimeState still owns actors, coroutine execution and cleanup. No serialized API/asset changes in this slice.
- PlacementSearch source is identical to the frozen snapshot. The frozen optimized Editor comparison contains 26 unchanged method bodies and no new Planner/WorldQuery calls; the earlier legacy comparison remains available.
- Final DX11 and Null graphics CPU suites each passed 496 repeated-request pairs and 312 fresh-buffer/first-request pairs, plus first-use and nearest-order checks. Position/count/order/room/RNG and all 10 work counters matched.
- CPU paired-time median change: warmed −5.8%~+4.6%, fresh buffers −15.0%~+3.6%. DX11 timing/p95 was noisier; exact per-call latency, FPS and memory-byte equality are not asserted. Constructor/first-query measurements exclude process/JIT/MonoBehaviour/scene startup; allocation API canary unavailable.
- Final new Unity PlayMode regressions 5/5 passed. Extended earlier run passed 10/11; the unchanged MarkActor exact-color assertion failed outside the new placement path and remains Suggested Later. Both actual results are retained, not merged into an all-pass claim.
- Final Unity compile/import and MSBuild Editor/ProceduralPlayModeTests passed. Generated projects were not manually edited. Protected Lightning Spear assets/meta and TestScene unchanged; existing concurrent GlobalUIRoot localization changes preserved.
- Doc Impact: SessionLog + StructureMemory + existing RefactorBacklog context. No task update to ErrorLog/DecisionLog, Architecture/Contracts or Presentation HTML; no HTML stale candidate identified.
- Evidence: [report and raw results](../SessionLogs/2026-10-10-lightning-structure/README.md).
