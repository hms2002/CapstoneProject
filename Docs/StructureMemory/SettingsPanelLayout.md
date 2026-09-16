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
