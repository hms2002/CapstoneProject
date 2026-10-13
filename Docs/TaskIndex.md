---
status: active
authority: task-router
category: task-index
last_reviewed: 2026-10-12
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
| spiri-alcohol-exposure | 2026-10-12 | [Completed scope](ActiveTasks/spiri-alcohol-exposure.md): recipient3s charge/1.5s drain, confusion, Spiri level1 five-second speed reward and authored mug; initial38 native cases passed. Directional dash reversal follow-up compiled with15 direction cases; current open Editor PlayMode rerun/manual combat/Player build remain unexecuted. |
| encyclopedia-tab-visibility-reveal | 2026-10-12 | [Completed scope](ActiveTasks/encyclopedia-tab-visibility-reveal.md): complete Item detail hiding with bookmarks/selection preserved; shared Reveal order and mask-top correction. Unity26/26, Editor/test MSBuild and preservation23/23 passed; native phases use real Item/font presenters. Full hub manual flow/Player build remain. |
| electric-scaling-and-relics | 2026-10-12 | [Completed scope](ActiveTasks/electric-scaling-and-relics.md): active direct re-electrocution doubles the requirement, shock end resets to base; chain/Echo and game explanations excluded. Native96/96 and Editor/test MSBuild passed. Earlier ten-relic/29-level actual descriptions and exact150 locale values verified; manual combat balance/Player build remain. |
| encyclopedia-book-inner-padding | 2026-10-11 | [Completed scope](ActiveTasks/encyclopedia-book-inner-padding.md): titles25-unit, latest illustration594×389 near the book rule with fixed bottom; initial Unity21/21 and follow-up authoring/MSBuild/preservation20/20 passed. Latest frame subsequently covered by the real-presenter Reveal integration26/26 and native captures above. |
| encyclopedia-book-flat-top | 2026-10-11 | [Completed scope](ActiveTasks/encyclopedia-book-flat-top.md): flat upper book art in nine used stationary regions; original palette, lower/moving pixels, metadata and UI geometry preserved; pixel60/60 and Unity21/21 passed. |
| encyclopedia-creature-tabs | 2026-10-11 | [Completed scope](ActiveTasks/encyclopedia-creature-tabs.md): Monster14/Boss4/NPC5, actual27 portraits, profile species kills and dedicated creature pages; Unity8/8 EditMode +19/19 PlayMode, MSBuild and three native captures passed. Full hub manual flow/Player build remain unexecuted; [memory](StructureMemory/EncyclopediaCreatureTabs.md). |
| lightning-spear-tool-composition | 2026-10-10 | [Completed scope](ActiveTasks/lightning-spear-tool-composition.md): six generic tools + thin binding for three patterns;1,048 output/RNG/work pairs, Unity17/17 and MSBuild passed. Constant-cost/timing/allocation limitations documented in [evidence](SessionLogs/2026-10-10-lightning-tools/README.md). |
| lightning-spear-placement-structure | 2026-10-10 | [Completed scope](ActiveTasks/lightning-spear-placement-structure.md): Planner/WorldQuery responsibility split; each final backend808 A/B pairs match output/RNG/work, own Unity5/5 and MSBuild passed. Existing exact-color assertion remains a separately reported failure; [evidence](SessionLogs/2026-10-10-lightning-structure/README.md). |
| chest-offer-width-consistency | 2026-10-08 | [Completed scope](ActiveTasks/chest-offer-width-consistency.md): inactive/active width consistency removes fourth-offer occlusion; Unity11/11 selected tests and MSBuild passed, nine authoring files unchanged. |
| ui-batching-structural-optimization | 2026-10-08 | [Completed scope](ActiveTasks/ui-batching-structural-optimization.md):147/147 selected native tests; Keybinding165→18 and Settings39→20; normal30/60-actor total draws89→60 /149→90, dense fallback148→148 preserves sorting. Original fonts/GUIDs/profiles protected; [report](SessionLogs/2026-10-08-ui-batching-structural/README.md). |
| ui-batching-optimization | 2026-10-08 | [Completed scope](ActiveTasks/ui-batching-optimization.md): 119/119 native tests; reward batches 12→9 / 9→6, Pause 7→6 and Keybind 166→165 in a matched 18-screen pair; behavior-preserving source caches/styles/slot reuse and two narrow atlases. Conditional structural candidates remain in the [backlog](RefactorBacklog/UIBatchingStructuralCandidates.md); [report](SessionLogs/2026-10-07-ui-batching/README.md). |
| frame-performance-measurement | 2026-10-07 | [Completed scope](ActiveTasks/frame-performance-measurement.md): four valid Player runs, 28 equivalent operation cases and two GPU ABBA runs; popup GC/frame −81.6%, global FPS claim withheld and legacy shader cost follow-up recorded in the [report](SessionLogs/2026-10-07-frame-performance-measurement/README.md). |
| test-scene-boss-combat-and-breath-lock | 2026-10-07 | [Completed scope](ActiveTasks/test-scene-boss-combat-and-breath-lock.md): one test boss/direct combat, blank hurtbox fallback and blink-time aim lock; 149 TestScene checks and 6/6 Unity regressions passed. |
| frame-performance-optimization | 2026-10-07 | Behavior-preserving hot-path changes implemented; Unity 5/5 EditMode and final 28/28 PlayMode; unchanged balance/Editor-trace follow-ups recorded in [scope](ActiveTasks/frame-performance-optimization.md). |
| test-scene-and-lightning-placement | 2026-10-07 | [Completed scope](ActiveTasks/test-scene-and-lightning-placement.md): authored TestScene and central icon grids (107 UI follow-up Play Mode checks), 496 placement A/B comparisons; abandoned NPC templates excluded. |
