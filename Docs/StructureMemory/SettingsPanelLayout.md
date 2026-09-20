# Settings panel responsive layout

Current map: 2026-09-17. This document is context, not an Architecture/Contracts authority.

## Purpose and key assets

- [GlobalUIRoot.prefab](../../Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab) authors the gameplay settings panel.
- [TitleScene.unity](../../Assets/_Project/Scenes/TitleScene.unity) contains a separate title-local settings panel with the same row layout.
- [SettingsPanelUI.cs](../../Assets/_Project/Runtime/UI/Settings/SettingsPanelUI.cs) binds values and handles opening/closing; it does not calculate row geometry.

## Layout ownership and extension

`SettingUI/Background/ContentsMask/Contents` stretches horizontally inside the existing mask with zero horizontal offsets. Its VerticalLayoutGroup controls child widths and expands them to the available width; authored row heights and vertical scrolling are retained.

Rows use RectTransform anchors for their interior. Existing HorizontalLayoutGroups on rows, steppers and slider groups are disabled so they cannot override these anchors. Labels occupy the left 30 percent with a 30-unit left inset; controls run from 35 percent to the right edge with a 30-unit inset. KeyMapping has one full-width inset label on its existing button row.

Stepper arrows remain 60 units wide at opposite ends; the value spans between them with 20-unit gaps. Audio sliders stretch to reserve 100 units for the percentage and a 20-unit gap. Add new rows using the same authored structure. No runtime objects, new components or lifecycle ownership were introduced.

## Known pitfalls and verification limits

- Scene prefab overrides can retain positions previously written by layout groups. The enabled build scenes had the obsolete interior geometry overrides removed in this change so the prefab anchors propagate. Preserve unrelated overrides.
- Disabled/legacy scenes and alternate GlobalUIRoot prefabs were not migrated. If made active, inspect their settings layout and overrides first.
- Static geometry checks covered row widths 800, 1025.45, 1200, 1413.33 and 1800. Unity import, actual text rendering, display transitions and player-build visual acceptance were not executed. Very narrow widths still need visual validation.
- This local authoring map is not currently an Architecture/Contracts promotion candidate.

## UI size option removed (2026-09-17)

The UI size selector is retired. GameSettingsService no longer reads or writes `settings.ui.scale` or changes CanvasScaler reference resolutions/scale factors. Existing saved values are ignored; new runs use authored CanvasScaler settings with normal screen-size adaptation. GameUiScaleController and its meta were removed after confirming no serialized asset references.

SettingsPanelUI no longer contains the selector field, bindings or preset API. The UISize row is inactive in the main prefab, all five alternate prefabs and TitleScene, so vertical layout excludes it. The authored inactive row remains for asset reference stability. This supersedes the UI Small/Medium/Large acceptance checks above. Chain unlock logic was not changed; its 2.5-second delay still needs separate verification.

## Shared 16:9 presentation viewport (2026-09-21)

`GamePresentationController` applies its existing camera viewport adaptation to every Canvas directly beneath `GlobalUIRoot`, including inactive canvases. This includes `FadeInOutCanvas` (ending) and `LoadingCanvas`, which were omitted by the former gameplay-layer list. Nested canvases inherit their parent, world-space canvases retain world placement, and service-owned fullscreen overlays are outside this direct-child selection.

The controller refresh signature includes the GlobalUIRoot instance so a UI root appearing after settings initialization, or replacing a previous root, receives the viewport without requiring a resolution change. On returning to a full 16:9 viewport, each canvas restores its captured render mode, camera and plane distance. Existing sorting and authored anchors are retained.

Validation: UI.csproj MSBuild succeeded; main-prefab direct Canvas coverage and viewport/skip geometry were checked for 1280x800, 1920x1200, 2560x1600, 1920x1080, 2560x1440 and 2560x1080. Player-build visual verification remains pending. Editor Play intentionally bypasses display letterboxing, so it is not equivalent to this acceptance test. Test ending, loading and 16:10-to-16:9 transitions in a player build. This map is not an Architecture/Contracts promotion candidate.

## Chain integration timing (2026-09-21)

`SettingsPanelFakeChainPresentation` advances its existing Verlet solver through a time accumulator, with a constant tick of min(authored maxSimulationStep, 1/60 second), defaulting to 1/60 for a nonpositive maximum. Existing simulationSubsteps still subdivides that fixed tick. At most 0.1 second of elapsed time is accepted from a single rendered frame. Input/support motion is sampled when ticks are consumed so intervening-frame displacement is not discarded. ResetSimulation and lifecycle input-cache resets clear residual time.

`UIChainDropPresentation` still owns panel fall/settle behavior; no additional chain sleep mode was introduced. Authored link lengths, gravity, damping, anchors and serialized schemas remain unchanged. The change reduces frame-time-induced resting motion; it does not claim to solve all low-resolution rasterization artifacts.

`Tools/Validation/ChainFixedStepRegression.cs` runs with the production chain source in an isolated Unity project. Fixed and variable frame schedules, anchored/free ends, endpoint motion, fractional-time reset and long-frame backlog checks passed. The fixture reproduced old-driver resting variation of 0.1227722 local units, versus 0.00001525879 with fixed ticks. UI.csproj MSBuild passed. Actual small-resolution UI rendering and mouse interaction remain manual checks.
