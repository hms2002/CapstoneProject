---
status: complete
authority: active-task
category: implementation
last_reviewed: 2026-10-10
---

# Lightning Spear Functional Tool Composition

- Mode: Implementation, user authorized proceeding after the concrete tool-composition design.
- Goal: Express third strike, ordinary rain and targeted rain with the same reusable placement tools and generic flow operations. Shared tools must not depend on Lightning Spear data, actors, relic IDs or mode switches.
- Allowed: Plain internal C# placement tools in the existing Gameplay assembly, a thin Spear binding of settings/target inclusion/position access, source-only runtime delegation, Editor-only frozen comparisons and focused functional tests, task memory.
- Preserve: Current candidate order/count/position/room/RNG, distance-only target sort and distance/x/y point heap, exact preferred-first evaluation, duplicate fixed fallback checks, budgets including fixed points, no-progress round-robin stop, lazy random sampling, spacing-zero repeats, requested masks/triggers/initial-overlap handling, capacities and synchronous request cache scope. Actor spawning, E plan copy before yield, 0.07-second targeted spawn delays, cancellation and lifecycle remain with existing owners.
- Excluded: Other production consumers' migration, new search algorithms, map-role/layer migration, serialization/assets/meta/GUID/asmdef/settings/bootstrap changes, managers/singletons/DDOL, new UI or content-authoring frameworks, Architecture/Contracts/Presentation HTML updates.
- Success: Common tools accept coordinates/settings/readers/resolvers rather than Spear types; all three patterns compose generic first-success/bounded-fill/round-robin/fill-remaining operations; a non-Spear test config uses the same tools; output/RNG/work matches an independent frozen current implementation, with warmed/fresh timing and ownership/cancellation regressions reported honestly.
- Verification: Freeze current sources/assets first. Run real Unity fixture A/B and third-strike cases, generic tool behavior/budget/zero-spacing/reuse cases, existing E/lifecycle regressions. Run MSBuild once regenerated projects include all new files. Preserve unrelated dirty and concurrent work.
- Risk: Additional hot-loop dispatch, result duplication, request-cache leakage across recipes, actor-position snapshot semantic changes, diagnostic coupling, fallback budget/RNG drift. Keep values/cursors and reusable buffers; retain live reads rather than introducing a snapshot change.
- Documentation: SessionLog, placement StructureMemory, narrow retained debt/context. Report any source-of-truth promotion candidate without editing source-of-truth docs.

## Outcome

- Added six generic placement source files in the existing Gameplay assembly. Common tools contain no weapon/actor/relic/GAS identifiers. Spear binding passes numeric settings, a target resolver and a value-type live-position reader; all three patterns compose the same generic flow operations.
- Independent current Planner/World snapshots are Editor-only. Old Search differs only by the Editor wrapper and remains outside production. Candidate collection and heap bodies retain the old algorithm.
- Real final Unity comparison: 496 rain +240 third-strike +312 fresh-buffer pairs =1,048 sampled pairs, exact output/order/room/RNG/work agreement. Final new/related PlayMode17/17 and MSBuild Editor/ProceduralPlayModeTests exit0.
- CPU paired-time median change: repeated rain −6.5%~+6.8%, third strike −4.6%~+8.9%, fresh buffers −5.2%~+13.3%. Operation counts match but constant costs/variance are reported; no universal latency/FPS/byte-memory equality or speedup claim. Player/IL2CPP and working allocation profiler remain unmeasured.
- Preserved actor execution and cleanup bodies, borrowed E copy and delay. Single-value 3rd binding clears committed scratch after transferring the value, retaining its original lifecycle boundary.
- Source-only scope: protected weapon assets/meta/TestScene unchanged; concurrent GlobalUIRoot edits preserved. Other production consumers were not migrated. No new MonoBehaviour/Manager/singleton/DDOL/serialized changes.
- Doc Impact: SessionLog + StructureMemory + existing classification backlog + DecisionLog; common ownership Architecture/Contracts promotion candidate for later multi-consumer validation. No task ErrorLog update, source-of-truth/Presentation HTML edits or stale candidate.
- Evidence: [report/raw data/tests](../SessionLogs/2026-10-10-lightning-tools/README.md), [common structure](../StructureMemory/PlacementTools.md).
