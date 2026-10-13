# UI batching structural optimization, 2026-10-08

**Completed: 147/147 selected native tests passed, zero skipped. Keybinding batches165→18 and Settings39→20; separated normal30/60-actor total draws89→60 /149→90. Dense overlap preserves original rendering cost and sorting.**

The user authorized the remaining structural candidates with appearance, input and ownership preservation as the boundary. This slice adds same-Canvas render layers for key/settings controls, conditional rectangular clipping and shared monster HP geometry. It continues the [first UI batching slice](../2026-10-07-ui-batching/README.md); results from that slice are not counted again here.

## Scope and baseline authority

The baseline is the current dirty workspace captured before this slice, including the first optimization slice and concurrent authored/localization changes. Git HEAD is not the behavioral baseline. The immutable external `baseline-manifest.json` contains path/SHA256 pairs; scene/settings/GUID/profile checks protect changes outside the approved presentation scope. No rollback to Git HEAD is permitted.

The scope is [UI batching structural optimization](../../ActiveTasks/ui-batching-structural-optimization.md). Existing controllers retain input, binding state and lifecycle. Existing monster views retain health/trail/status timing. New collaborators are view-owned presentation helpers, not new managers, singletons or DDOL objects. The new hierarchy and mesh/material references are authored through Unity APIs; runtime code does not create UI hierarchy. Serialized existing names, enum values, input actions, gameplay settings and save ownership stay unchanged.

The approved asset boundary is KeyMapRow, the two GlobalUIRoot menu viewports and render containers, Title-local Settings render containers, and the shared MonsterWorldHud geometry/references. Other scenes, ProjectSettings, packages, unrelated prefabs, original GUIDs and persistent profiles are protected. Final authoring audits verified 69 GlobalUIRoot and 67 Title semantic changes inside the approved hierarchy. Original font/style values were preserved across 110 Global texts, 87 Title texts and four row texts; original GUIDs remain intact. Title's only outside-scope text serialization difference is trailing-whitespace canonicalization. Final [protected audit](protected-audit.json): all 1,216 starting files are present, only9 approved existing files differ, no existing meta/GUID differs, and all 3 persistent GameSave slot SHA256 hashes match. Newly authored assets/scripts and their Unity-generated metadata are additional scoped files.

## Changed files and purpose

Paths below are under `Assets/_Project/`. They describe this slice relative to its dirty-workspace snapshot, rather than every unrelated change shown by Git status.

| File | Change and reason |
| --- | --- |
| `Runtime/UI/Common/UiRenderLayerLayout.cs` | New local presentation helper projects authored render anchors into Controls/Icons/Text layers after the existing Canvas layout pass. Logical slots retain original layout participation. Before reparenting, it preserves the logical owner's inactive hierarchy state so hidden localization/presentation callbacks cannot transiently enable. It writes transforms/visibility only when changed, restores anchors on disable and releases detached local row/panel anchors on destruction. |
| `Runtime/UI/Settings/KeyBindingPanelUI.cs` | Wires its authored destination to each existing row and retains row sibling layout order. Existing row creation, action state, navigation and save/rebind ownership remain in the panel. |
| `Runtime/UI/Settings/KeyBindingRowUI.cs` | Binds the existing row-owned helper to the panel destination without replacing Button/TMP/icon references or listeners. |
| `Editor/Tools/UI/UiRenderLayerAuthoring.cs` | Explicit scoped authoring creates layout placeholders/render anchors and same-Canvas containers. Whole Slider/dropdown subtrees retain fill/handle/list geometry. Settings row-root Selectables retain their full Graphic subtree so child-label click routing keeps the original Button ancestry. |
| `Runtime/UI/Common/HybridRectClipView.cs` | Uses the authored RectMask2D only while the viewport's live corners are axis aligned in its root Canvas. Rotated, reflected, collapsed, skewed or tilted viewports retain the original stencil. Cached state avoids applying the same native clipping state each frame. |
| `Editor/Build/UiRectMaskAssetAuthoring.cs` | Validates and wires only the two approved GlobalUIRoot Settings/Keybinding hidden rectangular viewports. Retains the original Mask, Image, hierarchy and serialized references. |
| `Runtime/UI/Combat/StackStatus/MonsterHealthBarMeshView.cs` | Supplies one actor's width/height/health/trail through a local MaterialPropertyBlock to shared authored quad/material assets. Keeps the original six Images available and owns no gameplay timers or shared-asset lifetime. |
| `Runtime/UI/Combat/StackStatus/MonsterStackStatusWorldView.cs` | Continues the existing HP/trail/status projection. A private enabled-view registry resolves final HP/status bounds after LateUpdate. Disjoint actors use mesh HP; any overlap conservatively returns all registered visible actors to their original Canvas sorting path. |
| `Art/Shaders/MonsterHealthBarInstanced.shader` | Draws the original rectangular border/frame/track/trail/fill/highlight palette on shared geometry, with per-instance health data and compatible URP passes. Instancing support in source is not a measured native instancing-event count. |
| `Art/Meshes/MonsterHealthBarQuad.asset`, `Art/Materials/MonsterHealthBarInstanced.mat` | Unity-authored shared geometry and immutable palette/material. Actors borrow them and only own their property blocks. |
| `Editor/Tools/Monsters/MonsterHealthBarMeshAuthoring.cs` | Adds/wires the renderer on the existing shared HUD through Unity APIs, validates the plain default-UI rectangles, preserves original TMP font/material references and stores references to the six legacy Images. |
| `Editor/Tools/UI/UiPrefabPresentationPreservation.cs` | Captures current persistent presentation values before loading prefab contents, binds identities before reparenting, and restores original text/style, Scrollbar handle rects and unrelated nested-instance modifications before a scoped save. This preserves raw authored values through TMP validation and driven layout. |
| `Prefabs/UI/MonsterWorldHud.prefab` | Adds approved shared HP renderer references while retaining the original Images, status slots, timing and inherited ownership. |
| `Prefabs/UI/KeyMapRow.prefab`, `Prefabs/UI/GlobalUIRoot.prefab`, `Scenes/TitleScene.unity` | Approved Unity-API render-layer/viewport wiring. Semantic hierarchy, font/style and original-GUID audits passed. Title presentation changes are limited to its existing Settings subtree; outside-scope trailing whitespace is canonicalization only. |
| `Editor/Tools/UI/UiRenderLayerLegacyFixtureSaver.cs` and `Tests/Fixtures/UIBatchingLegacy/{KeyMapRow,SettingUI,KeyMappingUI,TitleSettingUI}.prefab` | Retain four minimal original authored fixtures before migration. Existing originals are never overwritten; already render-authored sources are rejected. Title is read in a preview scene and the production scene is not saved by the fixture saver. |
| `Tests/PlayMode/Procedural/UiRenderLayerLayoutPlayModeTests.cs` | Local projection, visibility, static-write and lifetime checks. These tests supplement rather than replace the real authored-control comparisons. |
| `Tests/PlayMode/Procedural/UiRenderLayerAuthoredPlayModeTests.cs` | Actual retained Settings/Title Settings/Key panel and row comparisons over five locales, scroll positions, primary/secondary/reset/listening buttons, navigation, pointer routing, enable/disable and stable projection. Completed all 76 native raster comparisons with actual max RGBA difference zero. |
| `Tests/PlayMode/Procedural/UiRectMaskRegressionPlayModeTests.cs` | Actual viewport clipping, five-locale raster, blank-viewport raycast, outside culling, rotation/skew/tilt/fade, lifecycle and stable-state comparisons. |
| `Tests/PlayMode/Procedural/MonsterHealthBarMeshPlayModeTests.cs` | HP/trail raster, normal health changes/timing, transforms, status pulse/overflow, cross-actor sorting and cleanup checks. |
| `Editor/Tools/Validation/{UiBatchingLiveScenarios,UiBatchingStructuralScenarios,MonsterHealthBarGpuValidation}.cs` | Measurement-only real screen/actor fixtures, normal-HP visibility/placement guards, projection counts and actual-render total-draw evidence. Setup, readback and inventory work stay outside timed marker windows; scenes/profiles are not saved. |
| `Editor/Tools/Testing/TestScenePresentationTraceIsolation.cs` | Adds the explicit `-uiBatchValidationTests` validation flag so disposable fixture VFX spawns stay out of project prewarm history even if the last open Editor scene was Title. Retains the existing temporary backend and callback restoration; production prewarm/gameplay behavior is not replaced. |

## Preserved behavior and fixture correction

The menu helpers keep render anchors inside the original Canvas and clipping ancestry. Layout slots remain at the original parent/sibling positions. Original controller objects, Button targetGraphics, listeners, selection state and navigation stay in place. Projection occurs after layout; a settled layout does not continually dirty transforms. Full controls whose internal parent-relative geometry matters move as one subtree.

Source review identified an input-ancestry risk: the Settings `KeyMapping` row-root Button was skipped while its child TMP text moved into a later render layer with raycasts suppressed. The authoring correction collects a row-root Selectable as its complete subtree. The reported `UISize/Stepper/Next` pointer mismatch was a separate fixture issue: retained UISize is authored inactive, while the test only checked its child's activeSelf. It probed a hidden button's stale geometry and hit an unrelated visible KeyMapping control in one clone. The corrected fixture captures activeInHierarchy independently while each side is actually active/rendered, asserts equal active states and skips only buttons inactive in both snapshots. Every visible Button keeps its strict route comparison; failures include point/rect/depth/cull/all-hit diagnostics. Final actual five-locale input/navigation/geometry comparisons passed; native menu batch capture remains separate evidence.

The stronger cold-ready/visible-button fixture exposed a real lifecycle gap: moving an initially activeSelf render anchor out of inactive UISize into an active layer briefly enabled its LocalizeStringEvent before Project hid it. The original hidden OptionText remained its authored Korean value; the candidate had received an English update. The runtime Attach correction applies `layoutSlot.activeInHierarchy` to the anchor before SetParent, so the original hidden callback lifetime is preserved. The strict inactive text equality was retained and passed in the final five-locale suite. Disable restoration returns anchors to their logical slots before restoring their local activeSelf, preserving inactive ancestors.

The clip helper preserves the invisible viewport's Image hit surface. When RectMask2D is active, it hides the Image color while retaining enabled/raycast behavior and disabling transparent-mesh culling when the Image is an authored raycast target. Settings originally has no viewport hit target; Keybinding does. During the authored random Z drop rotation, stencil clipping remains active; a static RectMask2D replacement would have changed the clip shape.

HP mesh mode is deliberately conditional. Any intersecting HP/status bounds switch every registered visible actor back to the original six Images and Canvas sorting, including distant actors in the same presentation set. Loose text/pulse bounds and ignored Z favor the original path. Separation restores mesh mode. Dense overlap can retain the previous draw cost, and the pairwise presentation-bound check has its own CPU cost. No isolated CPU or FPS benefit is claimed. The corrected fixed dense fixture below verifies this fallback and its retained draw cost. Actor health values, trail hold/interpolation, maximum-HP behavior, status ownership and destruction remain with the existing view.

## Completed selected validation

Unity 6000.4.2f1 native import/compilation and scoped authoring succeeded. Generated-project MSBuild passed for UI.csproj, Editor.csproj and ProceduralPlayModeTests.csproj; Editor was rebuilt after the validation-only trace flag was added. Code/docs whitespace checks passed. Whole-tree Git whitespace reporting still includes Unity-serialized empty-value spaces and prior unrelated changes; no YAML whitespace cleanup was performed. Unity ran exclusively in this chat; the final Editor is closed and the project can be reopened.

The final [selected native result](final-tests.xml) is **147/147 passed, zero failed, zero skipped**. It includes the original 119 relevant first-slice cases and 28 structural cases: HP mesh 9, existing world-HUD/status 9, hybrid clipping 5, local projection/lifetime 3 and real authored-control 2. The relevant three Chest methods remain selected; unrelated whole classes are not silently counted as passing.

- The actual five-locale Global Settings, Title Settings and Keybinding suite completed **76 native raster comparisons**, with **actual max RGBA difference 0** in every comparison. The declared allowed bound remains 1/255; the report records the smaller actual observed difference. Primary/secondary/reset/listening controls, navigation, strict visible pointer routes, scroll positions, reopen, inactive semantic text and static projection ownership also passed.
- The five clipping cases load the **current production GlobalUIRoot with render layers**, so the final **70 pose comparisons** cover combined layer+hybrid clipping, locale typography, fade, scroll and motion. Their native raster comparisons require exact RGBA. Blank viewport hit/no-hit surfaces, actual outside culling and stencil restoration passed.
- HP mesh tests cover normal ratios/trail timing, repeated damage/heal/max rescale, shared geometry and independent data, transforms, status overflow/pulse, disable/re-enable and cross-actor sorting. HP raster comparisons reject pixels beyond **2/255 per RGBA channel**; a pass does not establish that every HP sample has zero difference.
- Source/asset semantic audits kept changes within the approved hierarchy, preserved original font/style references and kept original GUIDs. The final protected/source/profile audit passed after all measurements.

Earlier diagnostics are retained as excluded verification attempts. A hidden-button activeSelf probe, a transient hidden-anchor OnEnable, and a cold locale preload overlap were corrected without weakening input/pixel/hidden-state checks. The package gate now waits for the current initialization and both database preloads to succeed with the same current handle/locale across three frames before the next locale write or restore. The final cold run passed it; no package/bootstrap edit, retry or log suppression was used.

A prior paired run completed 29 valid raster comparisons before its 180-second timeout. The fixture was repeatedly toggling roots and doing unused full texture readback per Button. The final helper prepares one real native render per side, then snapshots every visible Button's route/diagnostic while that side stays active. Geometry/navigation also avoids unused readback; actual raster tests retain ReadPixels/GetPixels32, nonblank baselines and their declared bounds. All 76 comparisons subsequently finished. This is a fixture-cost correction, not production FPS evidence.

The broader 187-case diagnostic run had **164 passed and 23 failed**, with 21 prewarm-history1224 errors in the Title-based fixture context and 2 out-of-scope Korean-literal/feedback assertions. The latter were not investigated against a before-slice baseline, so their attribution remains unconfirmed. It is not a clean full-suite result and is not replaced by the selected 147-case claim. The explicit validation flag keeps disposable VFX spawns out of prewarm history even when the last Editor scene was Title. That narrow Editor-only isolation does not change production prewarm behavior. Temporary missing-glyph warnings before locale/font callbacks settle were not suppressed. The existing scene-exit/service-root lifecycle assertion remains outside this presentation slice.

Final [18-screen comparison](ui-comparison.json) matches locale, active Graphic count and active TMP font-path/name multisets in every case. [Before](ui-before/live-scenario-evidence.json) and [after](ui-after/live-scenario-evidence.json) retain actual content/owner evidence.
## Actual rendering measurements

Normal-world evidence comes from [world-before](world-before/world-hud-60-hp-status.json) and [world-after](world-after/world-hud-60-hp-status.json). Requested/actual/visible actors match at 0/30/60, MaxHP stays normal, placement stays stable and the dynamic cases contain six real Electrocute status actors. Status cases also execute authored world VFX; native UI membership does not measure those ParticleSystems' GPU work.

| Normal visible world-HUD scenario | Native UI batches before → after | Interpretation |
| --- | --- | --- |
| 0 actors, steady | 0 → 0 | Empty world-HUD control, not no rendering for the whole game. |
| 30 actors, steady | 30 → 0 | HP leaves the UI path; all 30 use mesh projection. |
| 60 actors, steady | 60 → 0 | HP leaves the UI path; all 60 use mesh projection. |
| 30 actors, HP change + six statuses | 42 → 12 | Six status Canvases remain; HP projection uses mesh for all 30. |
| 60 actors, HP change + six statuses | 72 → 12 | Same six status Canvases remain; HP projection uses mesh for all 60. |

Zero native HP UI batches alone would only prove migration out of the UI path. The additional paired actual GameView total-draw evidence compares the same live actors, temporarily selecting original Images versus mesh mode:

| Actors | Completed-frame total native draws, Images → mesh | Difference |
| --- | --- | --- |
| 0 | 27 → 27 | 0 |
| 30 | 89 → 60 | 29 fewer total draws |
| 60 | 149 → 90 | 59 fewer total draws |

These are UnityStats **whole completed GameView frame draw counts**, including all rendered cameras/world/UI. Reports pass a camera disable/enable responsiveness canary and nonblank native rendering. The reports are `captured-total-only`: FrameDebugger event data is unavailable, HP-specific draws return -1 and instance counts are not available. Therefore the result is a verified total-draw reduction in matched normal scenarios, not a measured HP-only GPU duration or proof of an exact instanced-event count. GPU timing and whole-game FPS were not measured by this UI fixture.

The representative pre-render-layer menu run is `ui-before-2`: Keybinding **165**, Settings **39**, Korean HUD **38**, Korean inventory **17**, Korean three-card reward **9**, and traditional-Chinese pause **6** native UI batches. All had native UI data available. Final matched totals are recorded below. Only Settings/Keybinding changed; all 18 cases retain equal active Graphic counts and fonts.

| Representative screen | Native UI batches before → after |
| --- | --- |
| Keybinding, traditional Chinese | 165 →18 (89.1% lower) |
| Settings, traditional Chinese | 39 →20 (48.7% lower) |
| Pause, traditional Chinese | 6 →6 |
| HUD, ko/en/ja/zh-Hans/zh-Hant | 38/39/37/37/37 →same |
| Inventory, same locale order | 17/17/16/16/16 →same |
| Three reward cards, same locale order | 9/9/6/6/6 →same |

These are one final matched Editor pair on RTX 2060 Max-Q/Direct3D11, with native UI viewport 640×480. The settled menus use grouped submitted controls/icons/text plus rectangular culling. The18-screen comparison does not isolate the individual contribution of layers versus clipping. Five-language raster/input preservation is supplied by the native fixtures; no Settings/Keybinding profiler preview texture was available.

The final [fixed dense 60-actor case](world-after/world-hud-60-overlap-steady.json) has60 visible legacy projections, zero mesh projections, normal MaxHP and stable positions. Native worldUI batches remain60, and calibrated total GameView draws are[148 in original Image mode](world-after/gpu/world-hud-60-overlap-steady-legacy-images.json) and[148 in current fallback mode](world-after/gpu/world-hud-60-overlap-steady-instanced-mesh.json). The filename identifies the current renderer configuration; actual metadata verifies that overlap selected the retained Images. The fixture temporarily suspends only disposable actor collision simulation to hold the exact overlap pose, restores it in finally, and records that condition. It is sorting/draw evidence for a fixed pose, not a physical crowd or CPU benchmark.

Canvas.BuildBatch timing belongs to **all active Canvases**, not the captured fixture alone. Timed samples require the positive native marker canary, but an aggregate marker reduction is not isolated monster/menu CPU time. Native vertex fields returning zero remain unavailable rather than being treated as zero rendered geometry. Native UI preview availability and shader/material/Graphic inventories are not substitute draw counts or full-scene raster proof. These Editor measurements do not imply a Player FPS percentage.

## Authoring and fixture traps

Prefab loading/saving can invoke font validation and add unrelated nested overrides. Capture the current persistent font/material/style first, bind loaded identities before any wrapping, restore font/style and exact raw override arrays, clear the unrelated loaded Scrollbar tracker through public DrivenRectTransformTracker.Clear, restore original handle values, then compare the complete saved asset with the dirty-workspace snapshot. An original null TMP sharedMaterial must remain null; restoring only the font can select a default material and accidentally serialize it.

ScrollRect/Scrollbar layouts can drive rects during load/validation. Saved authored rect state must not be replaced with transient driven bounds. Keep the original layout, viewport, content and scrollbar references, and verify final raw saved rect fields against the approved baseline, allowing only intended renderer containers/references. The implemented preservation and final semantic/style audit checked this approved boundary.

Activate only minimum-common-ancestor clones after checking that localization, font, sword, scroll, controller and render-binding references stay inside each fixture. Same-name siblings need ordinal semantic paths; dropping duplicate entries would hide controls. Button bounds/navigation/pointer state must be snapshotted while each fixture is equivalently active and actually rendered: comparing an inactive baseline against an active candidate previously produced invalid geometry evidence. Rendering before raycast checks also establishes real Graphic depth/culling.

These selected UI/structural fixtures restore their borrowed state, retain normal HP and do not dismiss callback errors. Temporary actors, locale and test input state must be restored; final profile hashes are the acceptance evidence.

## Documentation impact

Documentation handoff covers the dated SessionLog/report and ActiveTask/TaskIndex, the two narrow reconstruction maps `UIBatchingOptimization` and `MonsterWorldHudAndStatus`, the structural RefactorBacklog, and focused ErrorLog/DecisionLog entries for preserved input/sorting and prefab roundtrip risks. SessionLogs, both StructureMemory maps, RefactorBacklog, ErrorLog, DecisionLog and ActiveTask/TaskIndex were updated. Architecture, Contracts and Presentation HTML remain unchanged; no automatic HTML update is part of this slice.

Doc Impact Check: SessionLog, StructureMemory, RefactorBacklog, ErrorLog and DecisionLog. No Architecture/Contracts promotion or matching stale Presentation HTML page is required; those files were not edited.
