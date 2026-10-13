---
status: complete
authority: active-task
category: implementation
last_reviewed: 2026-10-07
---

# TestScene and Lightning Spear Placement

- Mode: Implementation, explicitly requested in the current chat.
- Goal: Create `TestScene`, a 30 by 30 test arena with an authored scene-local Canvas, and implement/measure the agreed Lightning Spear placement optimizations.
- Intent: Make portfolio measurements reproducible while exercising existing gameplay content through its normal runtime owners.
- Scope: ItemDatabase weapons/relics; authored boss/monster prefabs; level progression, reward selection/removal; chests and authored event-room content; Lightning Spear mark placement.
- Current-plan correction: RunSpecialNpc construction/teleport content is abandoned and excluded from the test catalog. Production legacy code/assets remain outside removal scope.
- Allowed: New test scene, test-only UI/code/assets, editor scene-authoring and validation tools; narrow reward-removal API; source-only placement helpers and Editor-only baseline probe.
- Preserve: Existing dirty UI/font changes, production scenes/prefabs, serialized schemas, bootstrap/DDOL flow, layer definitions, save formats, and Architecture/Contracts documents.
- Placement boundary: Preserve current placement results and RNG behavior in the optimization slice. Internal soft-wall passage remains a separate future authoring/gameplay change; walls cannot contain spawned marks.
- Runtime UI: Saved in the scene by the Editor builder, driven via serialized references. No runtime Canvas/button construction.
- Verification: Unity compile/import, scene-reference validation, Play Mode functional exercise, baseline/optimized seeded placement comparison with time/allocation/counters. Report unavailable checks explicitly.
- Risks: Boss/event lifecycle dependencies, irreversible instantaneous reward effects, persistent profile side effects, and changes to Unity-generated project files during refresh.
- Documentation: SessionLog and focused StructureMemory; update placement backlog where needed. Presentation HTML is not in scope.

## Outcome

- TestScene authored through Unity APIs; separate saved Canvas, six boss/21 mob/six chest catalogs and 16 current event snapshots.
- Abandoned RunSpecialNpc construction/teleport templates excluded per user correction.
- Unity final Play Mode: 77 checks passed with no collected errors, including both instantaneous reward undo paths, all current boss/mob/event entries, companion cleanup, run backend restoration and unchanged save hashes.
- Placement: 496 seeded A/B comparisons passed; current position/count/order and layer rules preserved. Targeted examples improve approximately 44–52%; ordinary/edge cases are not uniformly faster.
- Allocation-byte measurement unavailable (canary test failed); raw measured data and final scene preview preserved under `Docs/SessionLogs/2026-10-07-test-scene/`.
- See `Docs/SessionLogs/2026-10-07.md` and `Docs/StructureMemory/TestScene.md` for changes, measurement conditions and remaining limits.

## Central Icon Grid Follow-up

- User explicitly requested centered icon grids for weapons, relics, monsters and bosses because the dropdown catalogs were hard to read.
- Implementation scope: TestScene UI hierarchy, test-only UI components and Editor authoring/validation tools. Production gameplay, GlobalUIRoot authoring and placement algorithms remain outside this follow-up.
- Complete: four tabs, six columns, scrollable authored cards, selected-card highlight/name, explicit grant/spawn commands, slot/count options, and central command feedback.
- Catalogs: six weapons, 66 relics, 21 monsters and six bosses, with all 99 card sprites/references validated.
- Unity 6000.4.2f1 compilation/import and Play Mode: 107 checks passed, zero collected errors. Last-card scrolling, click/reopen/listener behavior and existing sandbox/reward/spawn cleanup were exercised. Equivalent Editor.csproj MSBuild passed; generated project files were not manually edited.
- Native captures and final report: `Docs/SessionLogs/2026-10-07-test-scene-icon-grid/`.
- Documentation impact: SessionLog and existing TestScene StructureMemory updated. No new ErrorLog/DecisionLog entry or Presentation HTML stale candidate.
