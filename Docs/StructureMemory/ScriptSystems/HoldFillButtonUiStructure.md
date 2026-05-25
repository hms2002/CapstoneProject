---
status: active
authority: structure-memory
category: ui-shared
last_reviewed: 2026-05-23
---

# Hold Fill Button UI Structure

## Purpose

Provide one authored UI pattern for hold-to-confirm buttons used by tutorial panels, chest reroll, title intro skip, and future UI flows.

## Current Structure

- `HoldFillButtonView` drives the visual state: base sprite, fill sprite, progress, filled clipping width, disabled RGB tint, and optional disabled alpha testing.
- `HoldActionButton` is the pointer/keyboard hold input and interactable-state owner for authored hold buttons. It owns hold timing, emits progress/completion events, and drives `HoldFillButtonView` when assigned.
- `TutorialInfoPanel`, `ChestScreen`, and `TitleIntroView` can project hold progress through `HoldFillButtonView` while keeping legacy `Image.fillAmount` fallback fields where needed.

## Authoring Rule

- Author the button hierarchy in a scene or prefab. Runtime code must not create the button, TMP text, Images, or mask hierarchy.
- Hold-only controls do not require a Unity `Button` component. Prefer an authored raycastable `Graphic` root with `HoldActionButton` and `HoldFillButtonView`; Unity `Button` is an optional legacy compatibility component.
- Recommended hierarchy: base button Image, normal text/icon, fill Image, filled `RectMask2D` clip root, and duplicated filled text/icon children under the clip root.
- Inverted text/icon color is authored through duplicated filled graphics, not through a runtime pixel-inversion shader.
- Fill appearance is authored on the fill sprite/Image itself; the shared component does not apply runtime fill tint.
- Disabled appearance defaults to RGB tint through `disabledColor`; active and disabled alpha stay at `1` unless `applyDisabledAlpha` is explicitly enabled for visual testing.
- Multi-color icon inversion is out of v1 scope. Use a separate filled-state sprite or tintable single-color graphic.

## Known Pitfalls

- `filledClipRoot` should be left-anchored with a left pivot for reliable left-to-right fill clipping.
- If the fill `Image` is `Image.Type.Filled` and uses the same `RectTransform` as `filledClipRoot`, do not also shrink that same rect by progress. That double-applies progress and can make the fill look missing until very late in the hold.
- Do not let two components own the same hold timing/progress. If `HoldActionButton` is present, gameplay screens should consume its events instead of recalculating pointer/keyboard hold progress.
- Gameplay screens should set hold availability through `HoldActionButton.SetInteractable(...)`; they should not treat a companion Unity `Button` as the primary state owner for hold-only controls.
- `HoldActionButton` availability is based on its own interactable state. Optional companion Unity `Button` components may mirror visuals for compatibility, but must not gate hold input through `Button.IsInteractable()`.
- If a companion Unity `Button` remains on the same object, check its transition colors if disabled visuals look different from `HoldFillButtonView`; the shared hold path expects the hold view to own disabled RGB/alpha projection.
- Keyboard hold must handle the key already being held when the button becomes usable. Stack UI reveal/interactable timing can otherwise miss the original key-down frame.
- For stack UIs like chest reroll, rely on the open stack UI lock instead of adding a new `GameFlowInputBlocker`.

## Promotion Candidate

Candidate for a future UI authoring contract if more hold-confirm buttons adopt the same structure.
