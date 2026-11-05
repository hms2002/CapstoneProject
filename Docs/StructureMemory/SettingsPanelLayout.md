# Settings panel responsive layout

> Tool retention update (2026-11-01): completed task-specific skin/layout/migration authoring and acceptance tools named below are historical and have been moved outside Assets. See [current retained tools and archive](TestAndEditorToolRetention.md). Gameplay/save regressions and reusable profiling remain. Automated tests run only when explicitly requested.


## Language choices (2026-10-03)

The existing language stepper now selects Korean, English, Japanese, Simplified Chinese and Traditional Chinese. Korean retains persisted value 0; Auto is not an enum or setting option. A missing/invalid saved choice is initialized once from the OS, with English fallback, then saved. Unity Localization owns runtime locale state; active value labels update on locale changes without resetting pending display choices. No scene/prefab layout or serialized bindings changed. See [LocalizationFlow](LocalizationFlow.md) for the current translation scope and remaining font/Play Mode checks.

Current map: 2026-11-05. This document is context, not an Architecture/Contracts authority.

## Purpose and key assets

- [GlobalUIRoot.prefab](../../Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab) authors the gameplay settings panel.
- [TitleScene.unity](../../Assets/_Project/Scenes/TitleScene.unity) contains a separate title-local settings panel with the same row layout.
- [SettingsPanelUI.cs](../../Assets/_Project/Runtime/UI/Settings/SettingsPanelUI.cs) binds values and handles opening/closing; it does not calculate row geometry.

## Layout ownership and extension

The main GlobalUIRoot and TitleScene use the approved clean settings skin. Settings (1240×764 reference units) and keys (1400×904) each occupy one page in the existing 1920×1080 Canvas space. The clean 320×76 title plaque overlaps the top center. The original two upper chains retain their presentation owner and endpoint references; lower chains are inactive and footer buttons are fixed inside the panel.

`Background/ContentsMask/Contents` now contains anchored LeftColumn/RightColumn groups. The old ScrollRect, layout groups, fitters and masks are disabled, and the scrollbar is inactive. Settings uses display/environment on the left and audio/controls on the right. Labels sit above their controls in 114-unit rows. Key mapping has 19 authored shared KeyMapRow instances, 9 left and 10 right, with 56-unit rows and 6-unit gaps. See [InputBindings](InputBindings.md) for action binding and migration limits.

The existing UiRenderLayerLayout slots/anchors continue to batch controls, icons and text. Render layers must follow both column roots in sibling order. Slider Fill Area is 12 units high; Handle Slide Area is 32 units high with zero handle height delta, because Slider drives its child anchors. Disabled lower-button UIChainDropPresentation components have their reach constraints cleared: disabled MonoBehaviours still receive Awake.

Pixel art comes from [SettingsSkin](../../Assets/_Project/Art/Sprites/UI/SettingsSkin/README.md), including editable Aseprite source. [SettingsSkinAuthoring](../../Assets/_Project/Editor/Tools/UI/SettingsSkinAuthoring.cs) applies native, scoped changes while preserving unrelated serialized blocks and existing font/material references. New section/footer labels use `ui.settings_skin.*` in all five GameText tables and the source CSV. Settings persistence and presenter ownership are unchanged.

Settings-to-keys navigation now replaces the page immediately. KeyBindingPanelUI opens with SnapOpen and closes through FinalizeClose in the same frame; initial settings opening/closing retains its chain animation. An authored CanvasGroup on the SettingsPanelUI root hides the complete page, including upper chains, while keeping its logical stack entry active. SettingsPanelUI stores/restores that group's alpha and input flags; returning does not reload bindings or reset unapplied display choices. UIManager retains global stack/lock ownership, while OpenFromSettings/FinalizeClose retain the existing title-local owner path. ApplyInPlaceNavigation in the authoring tool updates only the visibility group/reference and preserves geometry/art.

## Known pitfalls and verification limits

- Scene prefab overrides can retain positions previously written by layout groups. On 2026-11-02, user screenshots and source inspection found stale overrides still present in all 12 enabled scenes containing the main GlobalUIRoot: settings rows were bottom-anchored at Y=0, with misplaced footer/confirmation controls. Those conflicting geometry overrides now inherit the approved main prefab. This supersedes the earlier claim that scene overrides were already cleared. Inactive scrollbar geometry and unrelated overrides remain unchanged. See [repair evidence](../../outputs/ui-layout-normalization-2026-11-02/README.md); this repair was inspected at source level only, without tests or Unity visual execution.
- All 38 scene instances of the main GlobalUIRoot, including build-excluded scenes and recovery copies, now resolve the settings page to the same authored layout as TitleScene. The key mapping button sits 32 reference units above the footer divider and the title is centered in its plaque. After merging scenes, inspect the layout slot **and** the rendered button and label for old geometry overrides. The five alternate GlobalUIRoot prefabs have no scene instances in this inventory. See the [2026-11-05 scene inventory and repair](../SessionLogs/2026-11-05-nadoman354-settings-scene-layout.md).
- [SettingsSkinValidation](../../Assets/_Project/Editor/Tools/Validation/SettingsSkinValidation.cs) exercises actual presenters from both authored sources in disposable Play Mode fixtures, five locales, repeated opening, interaction unlock, hit targets, rebinding/conflict/discard and native captures. It does not apply display settings or save bindings. Full-scene manual acceptance, display transitions and player-build visuals remain separate checks. Historical pre-skin geometry measurements are superseded.
- This local authoring map is not currently an Architecture/Contracts promotion candidate.

## UI size option removed (2026-09-17)

The UI size selector is retired. GameSettingsService no longer reads or writes `settings.ui.scale` or changes CanvasScaler reference resolutions/scale factors. Existing saved values are ignored; new runs use authored CanvasScaler settings with normal screen-size adaptation. GameUiScaleController and its meta were removed after confirming no serialized asset references.

SettingsPanelUI no longer contains the selector field, bindings or preset API. The UISize row is inactive in the main prefab, all five alternate prefabs and TitleScene, so vertical layout excludes it. The authored inactive row remains for asset reference stability. This supersedes the UI Small/Medium/Large acceptance checks above. Chain unlock logic was not changed; its 2.5-second delay still needs separate verification.

## Shared 16:9 presentation viewport (2026-09-21)

`GamePresentationController` applies its existing camera viewport adaptation to every Canvas directly beneath `GlobalUIRoot`, including inactive canvases. This includes `FadeInOutCanvas` (ending) and `LoadingCanvas`, which were omitted by the former gameplay-layer list. Nested canvases inherit their parent, world-space canvases retain world placement, and service-owned fullscreen overlays are outside this direct-child selection.

The controller refresh signature includes the GlobalUIRoot instance so a UI root appearing after settings initialization, or replacing a previous root, receives the viewport without requiring a resolution change. On returning to a full 16:9 viewport, each canvas restores its captured render mode, camera and plane distance. Existing sorting and authored anchors are retained.

Validation: UI.csproj MSBuild succeeded; main-prefab direct Canvas coverage and viewport/skip geometry were checked for 1280x800, 1920x1200, 2560x1600, 1920x1080, 2560x1440 and 2560x1080. Player-build visual verification remains pending. Editor Play intentionally bypasses display letterboxing, so it is not equivalent to this acceptance test. Test ending, loading and 16:10-to-16:9 transitions in a player build. This map is not an Architecture/Contracts promotion candidate.

## Chain integration timing (2026-09-21)

### Free-hanging swing limit trial

`SettingsPanelFakeChainPresentation` limits the top-to-tip direction of FreeHanging chains to 30 degrees either side of chain-local down. From 25 degrees, quadratic influence gradually damps outward tangential motion and restores the pose inward. After distance solving, a rigid rotation around the top preserves existing segment lengths and bends; previous positions are updated with the corrected velocity to avoid injecting a kick. Individual links are not angle-clamped. Anchored chains retain their two endpoint constraints and do not use this limit. Trial values are private constants; no prefab/serialized schema changes.

The isolated Unity regression checks both directions, the soft zone, unchanged small swings, length preservation, removal of outward boundary velocity, repeated support impulses, and the existing fixed-step settling cases. These pass; rendered motion and feel still require a player playtest. This is a trial tuning choice, not a durable architecture decision.

`SettingsPanelFakeChainPresentation` advances its existing Verlet solver through a time accumulator, with a constant tick of min(authored maxSimulationStep, 1/60 second), defaulting to 1/60 for a nonpositive maximum. Existing simulationSubsteps still subdivides that fixed tick. At most 0.1 second of elapsed time is accepted from a single rendered frame. Input/support motion is sampled when ticks are consumed so intervening-frame displacement is not discarded. ResetSimulation and lifecycle input-cache resets clear residual time.

`UIChainDropPresentation` still owns panel fall/settle behavior; no additional chain sleep mode was introduced. Authored link lengths, gravity, damping, anchors and serialized schemas remain unchanged. The change reduces frame-time-induced resting motion; it does not claim to solve all low-resolution rasterization artifacts.

`Tools/Validation/ChainFixedStepRegression.cs` runs with the production chain source in an isolated Unity project. Fixed and variable frame schedules, anchored/free ends, endpoint motion, fractional-time reset and long-frame backlog checks passed. The fixture reproduced old-driver resting variation of 0.1227722 local units, versus 0.00001525879 with fixed ticks. UI.csproj MSBuild passed. Actual small-resolution UI rendering and mouse interaction remain manual checks.

## Settings control hit targets (2026-11-02)

Main GlobalUIRoot and TitleScene each have eight stepper Buttons on full-rect TMP
arrow objects. Their ButtonBackground Images are siblings under the render
anchor, not children of the Button. The TMP arrow is now the raycast target;
the background remains the Button targetGraphic for tinting but does not catch
clicks. Three Slider Background Images accept track clicks under their existing
Slider ancestor. Labels and decorative frames remain non-raycasting. Native
interaction is still a manual acceptance item; source references and compilation
are reviewed in the [change record](../../outputs/ui-gameplay-fixes-2026-11-02/README.md).
