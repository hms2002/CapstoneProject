---
status: active
authority: structure-memory
category: ui-rendering
last_reviewed: 2026-10-08
---

# UI Batching And Refresh Ownership

> Tool retention update (2026-11-01): completed task-specific skin/layout/migration authoring and acceptance tools named below are historical and have been moved outside Assets. See [current retained tools and archive](TestAndEditorToolRetention.md). Gameplay/save regressions and reusable profiling remain. Automated tests run only when explicitly requested.


This is a reconstruction map, not an Architecture/Contracts replacement. The structural implementation scope is [UI batching structural optimization](../ActiveTasks/ui-batching-structural-optimization.md); the [first slice](../ActiveTasks/ui-batching-optimization.md) remains applied.

## Runtime paths

- `Runtime/UI/HUD/WeaponSkillHudSlotPresenter.cs`: the existing presenter privately retains immutable idle materials by ready shader and setup texture. Each slot leases one idle material; an active flash owns a separate reusable instance. Disable restores the borrowed original without allocating; destroy releases the slot instance and idle lease. Both HUD owners use these boundaries. The zero-flash shader is deliberately retained: UI/Default produces different alpha for translucent pixels. External writers must replace a borrowed material rather than mutate the shared idle material in place.
- `Runtime/UI/Dialogue/SimpleTextAnimator.cs`: classify wave/shake links on text/style/property invalidation. Ordinary stable text uses TMP's own lifecycle. Real effects retain regeneration, link/character order, offsets and random calls. Do not cache already animated vertices as an unmodified baseline.
- `Runtime/UI/Common/MenuButtonHighlightPresentation.cs`: cache local text bounds, invalidate on TMP geometry/text/font/material/rect and locale changes. World transforms, sword corners, anchoring and motion remain live. The existing sibling sword reference must be cloned with its common ancestor in fixtures.
- `Runtime/UI/HUD/Status/StatusHudEntryView.cs`: the authored progress preset avoids per-entry outline setter clones. `LocalizeTmpFontEvent.ApplyAuthoredMaterialStyle` delegates font/style matching and reference counts to TMP's existing material manager. The package still owns locale loading. Preserve the CanvasRenderer atlas override when changing fonts; restore the normal authored style on progress exit. An inactive pooled entry retains its material lease until destruction.
- `Runtime/UI/Upgrade/UpgradeLakePresentation.cs`: source identity plus native material CRC gates redundant preset copies. Dynamic uniforms/content still update. Runtime preset mutations must synchronize on the next update; CRC coverage is verified against the current Unity version.
- `Runtime/UI/Inventory/Common/PlayerInventoryPanelView.cs`: three local slot groups retain exact root/prefab/count/parent matches. Explicit slot binding cleanup runs before adapter disposal. See [inventory ownership](ScriptSystems/InventoryAndChestUIStructure.md).

Paths above are under `Assets/_Project/`. No gameplay state moved into UI and no new runtime manager, singleton or DDOL owner was added.

## Structural presentation paths

- `Runtime/UI/Common/UiRenderLayerLayout.cs` projects existing authored anchors into Controls/Icons/Text containers on the same Canvas after layout. Original slots remain under the row layout; controllers, target Graphics, Button references and binding state remain the existing objects. KeyBindingPanelUI supplies its authored destination to each retained row. Disable returns anchors to their slots; row destruction releases detached anchors. Stable poses avoid identical transform writes. Whole Slider/dropdown/control subtrees retain their local geometry and input ancestry.
- `Runtime/UI/Common/HybridRectClipView.cs` retains the original Mask and invisible Image hit surface, using an authored RectMask2D only when the viewport is axis aligned in its root Canvas. Rotation returns to stencil clipping; disable restores the original Image/Mask state. The state cache still observes live transform changes. Only the approved GlobalUIRoot Settings and Keybinding viewports use this path.
- `Runtime/UI/Combat/StackStatus/MonsterHealthBarMeshView.cs` owns an actor-local property block for the shared authored HP quad/material. Since2026-10-30, the shader and retained Image fallback both sample the approved Aseprite sheet; four visible parts share one texture. The original six Image references remain available, with redundant rim/highlight Graphics transparent. The existing MonsterStackStatusWorldView owns HP/trail timing, actor transform compensation, status subscription and cleanup. See [world HUD ownership](MonsterWorldHudAndStatus.md).
- `Editor/Tools/UI/UiRenderLayerAuthoring.cs`, `Editor/Build/UiRectMaskAssetAuthoring.cs` and `Editor/Tools/Monsters/MonsterHealthBarMeshAuthoring.cs` are explicit scoped authoring entry points. They add/wire authored presentation through Unity APIs; runtime code creates no hierarchy. `UiPrefabPresentationPreservation.cs` protects original font/material/size values and unrelated nested overrides while loading/saving a prefab. Review the complete persistent diff against the current-workspace snapshot after authoring; API save success alone does not prove preservation.

The world HP mesh path applies only while published HP/status bounds do not intersect. A private enabled-view registry in the existing view resolves routing in Canvas.preWillRenderCanvases after every actor's LateUpdate. Any intersecting pair restores all current HUDs to the original Canvas path for that frame, preserving cross-actor transparent HP/status order. Registration/unregistration follows enable/disable/destroy; there is no separate runtime owner. Conservative timer/count overflow and SDF bounds can produce false positives, and dense encounters retain the previous rendering cost.

Attachment sets each anchor's initial active state from its logical slot before reparenting. This keeps hidden rows from briefly running locale/presentation OnEnable callbacks when moved into an active layer. Authoring clears the existing unrelated Scrollbar's DrivenRectTransformTracker through Unity's public Clear API before restoring original handles; restoring native anchor values alone did not preserve serialized inheritance values.

## Authored assets and extensions

`Editor/Build/UiBatchingAssetAuthoring.cs` owns the idempotent SpriteAtlas V2 prebuild path and approved progress preset reference. Card art uses four explicit Sprite subassets; ordinary menu chrome uses a separate three-texture whitelist when validation retains it. Original Sprite IDs, pivots, borders and references stay authored at their source. Rotation/tight packing are disabled and Point/no-mip/sRGB sampling is preserved.

Do not add whole sprite folders blindly. Broken-heart, alpha-outline and book-reveal shaders use UV-sensitive sampling; TMP atlases and lake render targets have independent ownership. A minimap Sprite that is also used in the world needs a world-material check before auto-binding it to an atlas.

## Verification entry points

- `Editor/Tools/Validation/UiBatchingLiveScenarios.cs`: real TestScene UI controllers, temporary normal weapons in initially empty sandbox slots, five locales and three deterministic reward candidates. No reward is selected; temporary state is restored.
- `UiBatchingValidationRunner.cs`: Editor ProfilerDriver recording plus UI/UIDetails areas, fresh completed native frame membership and recorded UI previews. Runtime Profiler.enabled alone does not create Editor frame history. Graphic counts and assigned materials are inventories, never substitutes for native batches.
- `UiBatchingStructuralScenarios.cs`: panel-owned normal-HP monsters at 0/30/60 counts with viewport/position guards. Canvas.BuildBatch is the aggregate CPU marker across all active canvases, not the selected fixture's isolated CPU cost. Real electrocution also creates world VFX, outside UI batch membership.
- `MonsterHealthBarGpuValidation.cs`: same live steady actors projected through original Images and then the current renderer, with native completed GameView UnityStats totals calibrated by camera disable/enable. FrameDebugger HP detail availability is reported separately. These totals include all rendered cameras/UI/world objects; they are not isolated HP draw counts or Player FPS.
- Retained `Tests/Fixtures/UIBatchingLegacy/` panels/row support actual authored layout, input and five-locale raster comparisons after production authoring. New structural tests cover clipping during motion/scroll, layer projection/input/cleanup, HP/trail raster and overlapping actor sorting. Fixtures guard internal ownership while inactive before any Awake/OnEnable callback.
- Play Mode regression fixtures cover ready-shader RGBA/stencil/lifetime, text geometry/RNG/menu placement, shared TMP style/locale/render cleanup, material CRC synchronization, slot ownership/transfers, and imported-versus-packed Sprite RGBA.

Native UI preview availability varies across captures. A null/blank preview is unavailable, never pixel-equivalence evidence. Locale tests must await global preload and restoration completion before starting another locale; rapid overlapping global preload requests are a separate package lifecycle risk.

The app's saved-language writer can finish before the package's newly restarted preload. Cold validation now waits for current initialization and both database preload handles to succeed on the same locale for three frames. The explicit `-uiBatchValidationTests` argument extends the existing Editor trace isolation to disposable UI fixtures even when the last opened scene is Title; it does not change normal gameplay tracing. Native pointer tests render each side once and snapshot all visible controls without unnecessary pixel readback or repeated locale-owner activation.

## Remaining boundaries

Separate canvases, font atlases, stencil states and overlapping image/text draw order can be necessary. HP representation changes, Canvas consolidation, mask replacement, particle aggregation and outline changes require measured benefit plus equivalent rendering/input/lifecycle checks. Typewriter character visibility must preserve progressive wrapping, rich text and UTF-16 behavior.

Source-of-truth promotion is not requested. Results and validation limits belong in the dated SessionLog/report; this map describes ownership and verification routes.
