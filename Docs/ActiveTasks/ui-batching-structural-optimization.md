---
status: completed
authority: active-task
category: implementation
last_reviewed: 2026-10-08
---

# UI Batching Structural Optimization

- Mode: Implementation, explicitly authorized by `그거까지 해` after identifying key/settings render order, rectangular masks and per-monster Canvas boundaries.
- Intent: Reduce actual UI/render batches substantially while preserving authored visuals, five-language typography, input/navigation/rebinding/scroll behavior, motion timing, HP/trail/status projections and lifecycle ownership.
- Starting point: Completed first slice remains applied. Current dirty workspace is the baseline, including concurrent user/localization work; never reset to Git HEAD. External SHA256 snapshots and persistent profile hashes are captured before changes.
- Allowed: Existing key/settings view/controller code; minimal local presentation collaborators; scoped Unity Editor API authoring for KeyMapRow, GlobalUIRoot/Title-local settings viewport, shared MonsterWorldHud and the affected inherited monster HUD connections if necessary; HP renderer material/mesh assets and serialized references. Preserve existing serialized names, IDs/GUIDs, enum values and gameplay ownership. Add meaningful rendering/input/lifecycle regression and native measurements.
- Forbidden: New Managers/Singletons/DDOL roots, runtime-generated UI hierarchy, unrelated combat/balance/save/input-action/package/settings/bootstrap changes, hand-edited Unity YAML/meta, Architecture/Contracts rewrites and Presentation HTML updates. Existing scene-exit service-root assertion remains outside scope.
- Boundary: Rows/controllers still own their input and binding state; render ordering is presentation only. Monster owner/view retains HP/trail/status timing and cleanup. Do not move gameplay state into rendering code or replace required clipping/transparent order blindly.
- Assumptions and risk-bearing unknowns: rectangular clip replacements must preserve the invisible viewport raycast surface and transform/motion clipping; rendering layer separation must keep layout and raycasts equivalent when text/button/highlight overlaps; world HP geometry requires the same raster, alpha, sorting and transform behavior as authored UI and must still clean up with its actor.
- Success criteria: Lower native batches in representative key/settings screens and normal visible 0/30/60 world HUD cases; exact/static RGBA comparisons plus dynamic boundary/input/lifetime equivalence; no profile/state changes; Unity compile/import and applicable generated-project MSBuild/tests passed. Report each counter's real scope and availability; do not infer FPS from batch counts or global Canvas.BuildBatch.
- Verification path: Root exclusively launches Unity and authoring. Clone the minimum common authored subtree under inactive staging, assert all references stay inside the fixture before activation, reject blank rendering and compare protected asset hashes after tests. Wait for app/package/font readiness before locale captures. Verify masks at scroll endpoints/midpoint and motion scales/rotations; key row primary/secondary/reset/listening/navigation; HP decrease/trail hold/interpolation/heal/death/disable/re-enable and flips/scales/cameras/status compacting.
- Retention: Apply and measure one factor at a time. Keep changes whose equivalent results and actual cost reduction pass; correct regressions within this approved slice. No estimate is an achieved batch reduction.
- Documentation: Dated SessionLog/report, narrow StructureMemory/RefactorBacklog and relevant recurring-error/decision entries. Leave source-of-truth documents and HTML untouched.

Related starting evidence: [first slice report](../SessionLogs/2026-10-07-ui-batching/README.md), [structural candidates](../RefactorBacklog/UIBatchingStructuralCandidates.md), [UI rendering memory](../StructureMemory/UIBatchingOptimization.md), [world HUD map](../StructureMemory/MonsterWorldHudAndStatus.md).

## Completed outcome

- Native147/147 selected tests passed, zero skipped;76 five-locale/three-panel layer raster comparisons all measured maxRGBA0, plus combined layer/hybrid70 mask poses and HP/status/lifetime/sorting cases.
- Matched18-screen capture: Keybindings165→18 and Settings39→20 native batches; other controls unchanged, all locales/active Graphics/active TMP font multisets equal.
- Normal separated0/30/60 worldUI0/30/60→0/0/0; six-status cases42/72→12/12. Calibrated complete GameView total draws27→27,89→60,149→90. Dense60 actors use original Images/Canvas, nativeUI60 and total148→148. No isolated GPU time or Player FPS claim.
- Unity compile/import and UI/Editor/Procedural generated-project MSBuild passed. All1,216 protected starting files present, only9 approved existing files differ, existing meta/GUIDs and3 persistent profiles unchanged. Full semantic author audits preserve all original TMP/prefab-instance/Animator data outside approved hierarchy; Title SaveScene normalized trailing whitespace.
- Doc Impact: SessionLog/report, two StructureMemory maps, RefactorBacklog, ErrorLog and DecisionLog updated. Architecture/Contracts/Presentation HTML unchanged; no matching stale HTML candidate. [Final evidence/report](../SessionLogs/2026-10-08-ui-batching-structural/README.md).
