---
status: active
authority: task-router
category: task-index
last_reviewed: 2026-10-08
---

# Task Index

This document is a router/dashboard for Codex task scope documents. It is not the active scope for any single thread.

Use this file to find a matching `Docs/ActiveTasks/<task-id>.md` or to decide that the current prompt Task Brief should stand alone for the current thread.

## Routing Rules

- If the prompt names a task id, use the matching ActiveTask.
- If the prompt clearly matches one active task, use that ActiveTask for scope.
- If no ActiveTask matches, use the prompt Task Brief as the scope and do not invent one.
- Do not let this dashboard override `Docs/Contracts/` or `Docs/Architecture/`.

## Active Tasks

| Task ID | Mode | Risk | Target Type | Status | ActiveTask |
| --- | --- | --- | --- | --- | --- |
| full-game-localization | Implementation | Approved serialized UI/font edits | Runtime + UI assets + tables | Japanese Galmuri9 applied; 55 representative Play Mode captures, timer atlas repaired; encyclopedia hint overlap and full-flow review remain (SOLD excluded) | [Scope](ActiveTasks/full-game-localization.md) |

## Proposed Tasks

| Task ID | Reason | Owner/Thread | Notes |
| --- | --- | --- | --- |

## Closed Or Superseded Tasks

| Task ID | Closed Date | Replacement / Outcome |
| --- | --- | --- |
| lightning-spear-tool-composition | 2026-10-10 | [Completed scope](ActiveTasks/lightning-spear-tool-composition.md): six generic tools + thin binding for three patterns;1,048 output/RNG/work pairs, Unity17/17 and MSBuild passed. Constant-cost/timing/allocation limitations documented in [evidence](SessionLogs/2026-10-10-lightning-tools/README.md). |
| lightning-spear-placement-structure | 2026-10-10 | [Completed scope](ActiveTasks/lightning-spear-placement-structure.md): Planner/WorldQuery responsibility split; each final backend808 A/B pairs match output/RNG/work, own Unity5/5 and MSBuild passed. Existing exact-color assertion remains a separately reported failure; [evidence](SessionLogs/2026-10-10-lightning-structure/README.md). |
| chest-offer-width-consistency | 2026-10-08 | [Completed scope](ActiveTasks/chest-offer-width-consistency.md): inactive/active width consistency removes fourth-offer occlusion; Unity11/11 selected tests and MSBuild passed, nine authoring files unchanged. |
| ui-batching-structural-optimization | 2026-10-08 | [Completed scope](ActiveTasks/ui-batching-structural-optimization.md):147/147 selected native tests; Keybinding165→18 and Settings39→20; normal30/60-actor total draws89→60 /149→90, dense fallback148→148 preserves sorting. Original fonts/GUIDs/profiles protected; [report](SessionLogs/2026-10-08-ui-batching-structural/README.md). |
| ui-batching-optimization | 2026-10-08 | [Completed scope](ActiveTasks/ui-batching-optimization.md): 119/119 native tests; reward batches 12→9 / 9→6, Pause 7→6 and Keybind 166→165 in a matched 18-screen pair; behavior-preserving source caches/styles/slot reuse and two narrow atlases. Conditional structural candidates remain in the [backlog](RefactorBacklog/UIBatchingStructuralCandidates.md); [report](SessionLogs/2026-10-07-ui-batching/README.md). |
| frame-performance-measurement | 2026-10-07 | [Completed scope](ActiveTasks/frame-performance-measurement.md): four valid Player runs, 28 equivalent operation cases and two GPU ABBA runs; popup GC/frame −81.6%, global FPS claim withheld and legacy shader cost follow-up recorded in the [report](SessionLogs/2026-10-07-frame-performance-measurement/README.md). |
| test-scene-boss-combat-and-breath-lock | 2026-10-07 | [Completed scope](ActiveTasks/test-scene-boss-combat-and-breath-lock.md): one test boss/direct combat, blank hurtbox fallback and blink-time aim lock; 149 TestScene checks and 6/6 Unity regressions passed. |
| frame-performance-optimization | 2026-10-07 | Behavior-preserving hot-path changes implemented; Unity 5/5 EditMode and final 28/28 PlayMode; unchanged balance/Editor-trace follow-ups recorded in [scope](ActiveTasks/frame-performance-optimization.md). |
| test-scene-and-lightning-placement | 2026-10-07 | [Completed scope](ActiveTasks/test-scene-and-lightning-placement.md): authored TestScene and central icon grids (107 UI follow-up Play Mode checks), 496 placement A/B comparisons; abandoned NPC templates excluded. |
