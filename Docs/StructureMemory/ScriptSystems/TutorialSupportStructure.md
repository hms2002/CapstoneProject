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

- `TutorialInfoPanel` is the scene/prefab-authored UI driver for a tutorial explanation panel.
- `TutorialInfoTrigger` opens a panel from a 2D trigger or from public methods such as `Fire`, `FireNow`, and `FireAfterDelay`.
- `TutorialProgressStore` reads and writes tutorial completion through `GameDataManager`.
- `TutorialSaveData` stores completed tutorial ids in `GameData.tutorialData.completedTutorialIds`.
- `Assets/Sprites/UI/Tutorial/TutorialInfoWindow.png` and `TutorialInfoTitleRibbon.png` are the imported explanation window/title sprite assets.

## Ownership And Lifecycle

- UI hierarchy, canvas, TMP text, Images, and sprite assignment should be authored in a scene or prefab, then referenced by `TutorialInfoPanel`.
- `TutorialInfoPanel` blocks normal gameplay input while open through `GameFlowInputBlocker` when `blockGameFlowWhileOpen` is enabled.
- Panel progress uses the existing `InputBindingService` `DialogueAdvance` binding.
- `TutorialInfoTrigger` can be placed on a trigger collider for player entry, or fired directly from code/UnityEvent for specific timing.
- Completion persistence is opt-in per request/trigger and should use stable non-empty tutorial ids.

## Extension Entry Points

- Add authored panel prefab/scene wiring by assigning `TutorialInfoPanel` references.
- Add tutorial timing by calling `TutorialInfoTrigger.FireNow` or `FireAfterDelay`.
- Add one-shot guidance by setting `usePersistentCompletion` and `markCompletedOnClose` with a stable `tutorialId`.

## Known Pitfalls

- Empty tutorial ids are never recorded as completed.
- Unity import/compile must refresh new script and sprite metadata before prefab wiring.
- The system currently provides only the common frame; actual tutorial steps, text, enemy/chest/gate hooks, and layout positions remain out of scope.

## Promotion Candidate

This can become a future contract if tutorial authoring rules become shared across multiple scenes.
