---
status: completed
authority: active-task
category: implementation
last_reviewed: 2026-10-08
---

# UI Batching Optimization

- Mode: Implementation, authorized by the user's `ㄱㄱ` after the UI audit and behavior-preserving plan.
- Goal: Reduce verified UI batch barriers and unnecessary mesh/layout/material work while preserving current visuals, input, text, five-language font/style behavior, animation/RNG timing and ownership.
- Scope: The audited, actually used UI paths. Begin with idle skill-flash material ownership, effect-free text/menu mesh refresh, status progress shared styles, identical upgrade material synchronization and duplicated chest layout work. Add narrowly scoped card/menu/minimap atlases only when compatible and validated. Higher-risk monster HP representation, shared/child Canvas, mask, particle aggregation and slot reuse are conditional on demonstrated benefit and equivalent results.
- Asset authority: The approved plan includes new shared TMP style/atlas assets and narrow prefab references where necessary, authored through Unity APIs. Preserve original Sprite IDs, existing GUIDs, serialized names/enums, layout/input ordering, all concurrent user/localization work, and gameplay state. Do not hand-edit YAML/meta, mass-pack UV-dependent shaders, or change ProjectSettings/asmdefs/packages/bootstrap/DDOL flow.
- No new runtime Managers, Singletons or DDOL objects. Reuse existing presenters/localization/material managers and explicit view-owned resources. Keep required distinct active-effect/stencil/font/clip rendering states.
- Baseline: Current dirty workspace is the starting point, not Git HEAD. UI sources/prefabs/shaders/settings/scenes are frozen under the task's external backup with SHA256 manifest; preserve and restore source swaps only under hash checks and while the Editor is closed.
- Verification: Existing authored UI in controlled Play Mode, UI Details/Frame Debugger batch information when supported, material/texture/font identities, Canvas.BuildBatch/mesh/layout CPU, image/geometry equivalence, meaningful lifecycle/input/locale tests, Unity compile/import and generated-project MSBuild. Separate batch count, rebuild CPU, vertex/overdraw and one-time opening costs; unavailable counters are unavailable.
- Representative state: Normal HP/resource/HUD counts. Do not inflate MaxHP for survival. Isolate existing sandbox/profile state and verify persistent save hashes.
- Retention criterion: Keep only changes whose required visual/functional comparisons pass and whose intended cost reduction is demonstrated. Revert unfavorable changes to this task's exact starting snapshot without overwriting concurrent edits.
- Existing limits: Prior Editor shutdown assertion through effect-end presentation/service-root creation remains out of scope. Previous shader tradeoffs are not bundled into this UI slice. Whole-game/per-platform FPS gains are not assumed from operation measurements.
- Documentation: SessionLog, narrow existing UI/locale/measurement memory and necessary backlog entries. ErrorLog only for new recurring traps; DecisionLog only durable decisions. Architecture/Contracts and Presentation HTML remain unchanged.

## Work sequence

1. Capture baselines and implement independently reviewable source changes.
2. Compile all C# together before any serialized asset authoring.
3. Apply approved shared style/atlas/prefab authoring through Unity APIs in one controlled pass.
4. Run functional/visual/lifecycle/locale comparisons and performance captures; inspect failures instead of suppressing them.
5. Evaluate structural candidates on a representative case; retain proven improvements, otherwise report Suggested Later with evidence.
6. Restore temporary conditions, verify profiles/settings/source boundaries and record the final result.

## Outcome

Completed the verified behavior-preserving source/material/atlas/slot-reuse slice. Unity authoring/compile/import, 119/119 native regressions and final generated-project MSBuild passed. The matched 18-screen native pair retained locale/active Graphic/TMP font equivalence: reward batches 12→9 (ko/en) or 9→6 (ja/Chinese), Pause 7→6 and Keybind 166→165; HUD/inventory/settings draw totals unchanged. Lake unchanged-preset operation cost was 17.117→2.124 µs. Normal visible world HUD cost was measured at 0/30/60 batches for 0/30/60 actors, with 42/72 under six active status projections.

Conditional representation/order/mask/particle/typewriter candidates remain unchanged where the required equivalent comparison is not established. They are recorded with measured barriers and triggers in [the backlog](../RefactorBacklog/UIBatchingStructuralCandidates.md). No whole-game FPS or isolated per-canvas CPU improvement is claimed. Source hashes/atlas GUID pairs and the three persistent profile hashes were restored/verified. [Final report and evidence](../SessionLogs/2026-10-07-ui-batching/README.md).
