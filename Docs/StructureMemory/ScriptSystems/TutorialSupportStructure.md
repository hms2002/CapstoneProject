---
status: active
authority: structure-memory
category: tutorial-support
last_reviewed: 2026-05-20
---

# Tutorial Support Structure

## Purpose

The tutorial support layer provides reusable authoring pieces for future tutorials without defining tutorial steps or scene content.

## Current Structure

- `TutorialInfoPanel` is the scene/prefab-authored UI driver for a tutorial explanation panel, including optional shared hold-button progress projection.
- `TutorialInfoTrigger` opens a panel from a 2D trigger or from public methods such as `Fire`, `FireNow`, and `FireAfterDelay`.
- `TutorialProgressStore` reads and writes tutorial completion through `GameDataManager`.
- `TutorialSaveData` stores completed tutorial ids in `GameData.tutorialData.completedTutorialIds`.
- `Assets/Sprites/UI/Tutorial/TutorialInfoWindow.png` and `TutorialInfoTitleRibbon.png` are the imported tutorial panel/title sprite assets.
- `TutorialInfoPage` is the authored page data shape for repeated tutorial layouts: page title, body text, and content image.

## Ownership And Lifecycle

- UI hierarchy, canvas, TMP text, Images, and sprite assignment should be authored in a scene or prefab, then referenced by `TutorialInfoPanel`.
- `TutorialInfoPanel` blocks normal gameplay input while open through `GameFlowInputBlocker` when `blockGameFlowWhileOpen` is enabled.
- `TutorialInfoPanel` owns page index state. Prev/Next controls are scene-authored buttons whose roots are activated only when movement in that direction is valid. Optional `pageNumberText` displays the current page as `1/2`. A/D page keys use `InputKeyCompatibility` and do not require runtime-created UI.
- The hold-confirm button is only active/usable on the final page. Before the final page, `TutorialInfoPanel` resets hold progress, disables the authored `HoldActionButton`, and deactivates the authored hold-button root.
- Panel progress uses an authored `HoldActionButton` when assigned. In that mode the button's own hold time, input settings, and reset policy are the source of truth.
- If no `HoldActionButton` is assigned, `TutorialInfoPanel` falls back to the existing `InputBindingService` `DialogueAdvance` binding and uses request/panel hold seconds.
- `TutorialInfoTrigger` can be placed on a trigger collider for player entry, or fired directly from code/UnityEvent for specific timing.
- Completion persistence is opt-in per request/trigger and should use stable non-empty tutorial ids.
- Open presentation is owned by the panel: an optional authored `dimPanel` `CanvasGroup` fades in while an optional authored `tutorialPanel` root moves up from `hiddenPanelOffset` using an overshoot ease. Close plays the reverse and deactivates the root after the presentation finishes.

## Extension Entry Points

- Add authored panel prefab/scene wiring by assigning `TutorialInfoPanel` references.
- Add tutorial content by filling `TutorialInfoTrigger.pages`. Page data is the only tutorial content path; empty pages open a blank panel and should be treated as an authoring miss.
- Assign `previousPageButton` / `nextPageButton` and, when the clickable visual root differs from the Button GameObject, assign `previousPageRoot` / `nextPageRoot` so invalid page directions are hidden with `SetActive(false)`. Assign `pageNumberText` when the authored layout should show `current/total`.
- Assign `dimPanel` and `tutorialPanel` when open/close presentation is desired. Leave them empty for immediate legacy-style show/hide.
- Add a hold-confirm button by authoring a `HoldFillButtonView` and optional `HoldActionButton`, then assigning them to `TutorialInfoPanel`. Assign `advanceHoldButtonRoot` when the visual root differs from the `HoldActionButton` GameObject.
- Set per-layout hold timing on the authored `HoldActionButton` prefab/variant when a button is assigned; `TutorialInfoTrigger.holdSeconds` remains fallback-only for buttonless panels.
- Add tutorial timing by calling `TutorialInfoTrigger.FireNow` or `FireAfterDelay`.
- Add one-shot guidance by setting `usePersistentCompletion` and `markCompletedOnClose` with a stable `tutorialId`.

## Known Pitfalls

- Empty tutorial ids are never recorded as completed.
- Unity import/compile must refresh new script and sprite metadata before prefab wiring.
- Do not expect `TutorialInfoTrigger.holdSeconds` to override an assigned `HoldActionButton`; change the button prefab/variant when a button-led tutorial needs a different hold duration.
- Page title text comes from `TutorialInfoPage.title`. Leave a page title empty only when the authored panel title should be blank for that page.
- `tutorialPanel` should point to the actual TutorialPanel/content root, not the full-screen root that contains `DimPanel`; otherwise the whole overlay can move.
- `dimPanel` should point to the DimPanel child/sibling `CanvasGroup`, not the same root that owns `TutorialInfoPanel`. The runtime avoids deactivating the panel root if misassigned, but the fade authoring is clearer with a dedicated DimPanel object.
- The system currently provides only the common frame; actual tutorial steps, text, enemy/chest/gate hooks, and layout positions remain out of scope.

## Promotion Candidate

This can become a future contract if tutorial authoring rules become shared across multiple scenes.
