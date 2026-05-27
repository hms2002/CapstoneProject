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
- `TutorialSceneSequenceDirector` is a scene-authored event coordinator for tutorial scene beats such as scene start, trigger entry, combat start, monster clear, chest open, portal entry, door open/close, player control lock, and player targetability blocking.
- `TutorialPlayerAutoMove` moves the authored player transform to a target point for tutorial presentation. It prefers existing `ExternalMovementController2D` / `MovementMotor2D` cleanup when present and falls back to Rigidbody2D or Transform movement.
- `TutorialBossEncounterSequence` coordinates the tutorial-only boss encounter flow: letterbox, camera focus, optional boss sprite scale, first dialogue, scripted lasers, second dialogue, collapse event, and fake game-over presentation.
- `TutorialBossLaserPresentation` plays authored Demon King laser steps without connecting to real boss AI or damage. Each step can reduce only the presentation HP view.
- `TutorialPresentationHpView` projects tutorial-only HP through authored text and slot roots. It does not read or write player `AttributeSet` or death state.
- `TutorialDefaultWeaponBootstrap` is a scene-authored tutorial loadout correction script. It forces the current player `WeaponInventory2D` to hold and equip the configured default weapon, optionally clearing other weapon slots.

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
- Tutorial scene flow scripts do not create scene content. Doors, HP UI, laser origins, boss visuals, camera focus targets, collapse effects, and scene portals remain authored in Unity and are connected through serialized references or UnityEvents.
- `TutorialBossEncounterSequence` may use `CameraPresentationDirector` when a boss-camera setup exists, but its default tutorial path focuses the existing gameplay camera through `CameraBootstrap` so a non-boss tutorial scene can still frame a placed boss sprite.
- Fake tutorial game-over calls `GameOverPresentationController.TryShow(...)` with `GameOverPresentationRequest.EndRunOnReturn = false`; real HP, real death components, and run end state are not changed by the tutorial sequence.
- Tutorial-only default weapon enforcement is scene-local. `TutorialDefaultWeaponBootstrap` uses `WeaponInventory2D.TrySetWeaponSlot(...)` and `Equip(...)` so existing weapon removal, stat binding, presentation binding, ability ownership, and equipped-change events remain owned by the weapon inventory path.
- `TutorialDefaultWeaponBootstrap.applyOnStart` waits authored startup frames before applying so player spawn and pending runtime restore have a chance to finish first. Use `ApplyNow()` from a scene director/UnityEvent when the tutorial needs an explicit timing point.

## Extension Entry Points

- Add authored panel prefab/scene wiring by assigning `TutorialInfoPanel` references.
- Add tutorial content by filling `TutorialInfoTrigger.pages`. Page data is the only tutorial content path; empty pages open a blank panel and should be treated as an authoring miss.
- Assign `previousPageButton` / `nextPageButton` and, when the clickable visual root differs from the Button GameObject, assign `previousPageRoot` / `nextPageRoot` so invalid page directions are hidden with `SetActive(false)`. Assign `pageNumberText` when the authored layout should show `current/total`.
- Assign `dimPanel` and `tutorialPanel` when open/close presentation is desired. Leave them empty for immediate legacy-style show/hide.
- Add a hold-confirm button by authoring a `HoldFillButtonView` and optional `HoldActionButton`, then assigning them to `TutorialInfoPanel`. Assign `advanceHoldButtonRoot` when the visual root differs from the `HoldActionButton` GameObject.
- Set per-layout hold timing on the authored `HoldActionButton` prefab/variant when a button is assigned; `TutorialInfoTrigger.holdSeconds` remains fallback-only for buttonless panels.
- Add tutorial timing by calling `TutorialInfoTrigger.FireNow` or `FireAfterDelay`.
- Add one-shot guidance by setting `usePersistentCompletion` and `markCompletedOnClose` with a stable `tutorialId`.
- Add scene flow by placing `TutorialSceneSequenceDirector` in the tutorial scene and wiring its UnityEvents from trigger colliders, chest open events, monster clear locks, portal entry, and authored door controls.
- Add a start walk-in by wiring `TutorialPlayerAutoMove.MoveToTarget` from the scene director or a trigger event. Assign the player, target point, and existing movement components when available.
- Add tutorial boss encounter by placing `TutorialBossEncounterSequence`, assigning a tutorial-only `NPCData`, first/second Ink TextAssets, optional `TutorialBossLaserPresentation`, optional `TutorialPresentationHpView`, boss focus target, boss visual root, and fake game-over return scene.
- Add laser beats by filling `TutorialBossLaserPresentation.steps` with origin transforms, direction, length, width, warning/attack timing, optional opposite ray, and an authored `DemonKingEgoLaserVfx` prefab.
- Use `TutorialBossEncounterSequence` UnityEvents for authored hit, collapse, sound, VFX, and animation hooks instead of connecting to real death/damage systems.
- Add tutorial-only default loadout by placing `TutorialDefaultWeaponBootstrap`, assigning the intended `defaultWeapon`, and leaving `clearOtherWeaponSlots` enabled when the scene must start with only that weapon.

## Known Pitfalls

- Empty tutorial ids are never recorded as completed.
- Unity import/compile must refresh new script and sprite metadata before prefab wiring.
- Do not expect `TutorialInfoTrigger.holdSeconds` to override an assigned `HoldActionButton`; change the button prefab/variant when a button-led tutorial needs a different hold duration.
- Page title text comes from `TutorialInfoPage.title`. Leave a page title empty only when the authored panel title should be blank for that page.
- `tutorialPanel` should point to the actual TutorialPanel/content root, not the full-screen root that contains `DimPanel`; otherwise the whole overlay can move.
- `dimPanel` should point to the DimPanel child/sibling `CanvasGroup`, not the same root that owns `TutorialInfoPanel`. The runtime avoids deactivating the panel root if misassigned, but the fade authoring is clearer with a dedicated DimPanel object.
- Presentation HP must stay separate from actual player HP. Do not call player damage, death, or `AttributeSet` mutation from tutorial boss laser UnityEvents unless the tutorial design explicitly changes to real damage.
- If tutorial HP should remain visible during letterbox, put it on a canvas/layer that is not included in `TutorialBossEncounterSequence.fadedLayers`, or disable `useCustomFadedLayers` only when default global UI fading is acceptable.
- New tutorial boss scripts add MonoBehaviours and serialized references, so Unity Editor import/compile and Inspector wiring are required before scene authoring can be considered complete.
- The system currently provides only the common frame and boss encounter support scripts; actual tutorial text, enemy/chest/gate placement, Ink content, and layout positions remain out of scope.
- `TutorialDefaultWeaponBootstrap` changes the live player inventory for the current runtime session. If a later transition should discard tutorial loadout state, that transition should reset or replace the run state deliberately instead of relying on this script to preserve the previous loadout.

## Promotion Candidate

This can become a future contract if tutorial authoring rules become shared across multiple scenes.
