---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-09-17
---

# Inventory And Chest UI Structure

## Purpose

Map inventory, chest UI, HUD inventory entry points, item details, inventory runtime, world drops, and interaction scripts.

## Current Structure

| Area | Count | Responsibility |
| --- | ---: | --- |
| Inventory / Chest UI | 42 | Inventory screen/root stack UI, chest UI and reveal presentation, drag/drop, item slots, detail panels, player stat panel UI. |
| HUD | 26 | Weapon skill, health, consumable, status, boss HUD, HUD open request handlers, input glyph presenters. |
| World Drops | 17 | World item drop model, pickup/drop landing visuals, item display visual presenters/profiles. |
| Interaction | 9 | Shared interactable contracts/base, player interaction tracker/resolver/prompt/sensor/speech, world prompt controller. |
| Inventory Runtime | 7 | Runtime chest and inventory data structures, treasure chest interaction, chest interactable. |
| Consumables | 2 | Consumable definition and player consumable inventory. |

### Inventory / Chest UI Breakdown

| Area | Count | Responsibility |
| --- | ---: | --- |
| Common Detail UI | 16 | Item detail panel, section views, tooltip/glossary helpers, hover controller, formatter, and weapon detail v2 views. |
| Inventory Common UI | 8 | Inventory screen, stack manager, panel/backpack views, item slots, drag icon, drop zone, and slide/fade presentation. |
| Player Stats Panel | 8 | Player stat panel definitions, section/row views, value modes, and display formatting. |
| Chest UI / Reveal | 5 | Chest UI manager/screen and first-open reveal layout/motion/presentation helpers. |
| Weapon Detail UI | 3 | Weapon detail view and detail/tooltip provider interfaces. |
| Consumable Detail UI | 1 | Consumable detail view. |
| Relic Detail UI | 1 | Relic detail view. |

### Common Detail UI Hotspots

| Parent path | Hotspot | Count | Responsibility |
| --- | --- | ---: | --- |
| Inventory / Chest UI > Common Detail UI | Tooltip / Glossary / Text | 5 | Tooltip color palette, glossary popup/database, link handler, and detail text formatter. |
| Inventory / Chest UI > Common Detail UI | Detail Views / Sections | 5 | Detail section views, section list, weapon stat line, weapon ability block, and weapon detail v2 view. |
| Inventory / Chest UI > Common Detail UI | Detail Panel Core | 4 | Item detail panel, panel services, context, and detail view contract. |
| Inventory / Chest UI > Common Detail UI | Hover Controller | 1 | Item hover controller. |
| Inventory / Chest UI > Common Detail UI | Detail UI Other | 1 | Player detail context provider. |

### HUD Breakdown

| Area | Count | Responsibility |
| --- | ---: | --- |
| Status HUD | 13 | Status HUD service, presenter, bootstrap, sources, definition, entries, group, and tooltip view. |
| Boss HUD | 4 | Boss HUD controller, boss health/groggy bars, and split-health presentation interface. |
| Inventory Open HUD | 3 | HUD inventory open button and open request handlers. |
| Player Health HUD | 2 | Heart token and player health heart HUD. |
| HUD Debug | 1 | Debug player HP text. |
| Input Glyph HUD | 1 | Input action glyph presenter. |
| Weapon Skill HUD | 1 | Weapon skill HUD. |
| Consumable HUD | 1 | Player consumable HUD. |

## Key Files

- `Assets/HeoMinSeok/_Project/Scripts/UI/Inventory/Common/InventoryScreen.cs`
- `Assets/HeoMinSeok/_Project/Scripts/UI/Inventory/Chest/ChestScreen.cs`
- `Assets/HeoMinSeok/_Project/Scripts/UI/Inventory/Chest/ChestFirstOpenRevealPresentation.cs`
- `Assets/HeoMinSeok/_Project/Scripts/UI/Inventory/Common/ItemSlotUI.cs`
- `Assets/HeoMinSeok/_Project/Scripts/UI/Inventory/Common/DragIcon.cs`
- `Assets/HeoMinSeok/_Project/Scripts/UI/Inventory/Common/DetailUI/ItemDetailPanel.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Inventory/Chest/Runtime/TreasureChest.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Inventory/Chest/Runtime/ChestRewardPolicy.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Consumables/Runtime/PlayerConsumableInventory.cs`

## Ownership And Lifecycle

- Stack UI open/close policy should remain owned by `UIManager` and stack screens.
- Chest first-open presentation has separate world timing and UI reveal timing; do not conflate those lifetimes.
- `TreasureChest.OpenedUi` / `FirstOpenedUi` report successful chest UI opens after `ChestUIManager.OpenChest(...)` accepts the request. Tutorial bridge code can listen to these events without changing chest reward or UI ownership.
- Chest reroll presentation is owned by `ChestScreen`; hold input, interactable state, timing, and progress are delegated to the shared `HoldActionButton`/`HoldFillButtonView` pair, and reward mutation remains owned by `TreasureChest.TryRefreshLoot()` and guarded by `ChestRewardPolicy`.
- Reroll presentation reuses manual reveal progress on `ChestFirstOpenRevealPresentation`; successful refresh reopen may call FirstReveal open UI particles and high-rarity slot reveal particles, but must not call first-open side entry, impact particles, or post-reveal VFX. Open UI particles can start with the manual reopen, while high-rarity slot particles should play only after the reveal pose reaches the opened slot positions.
- Chest reroll passes a virtual resize pivot Y of `0` into manual reveal progress so close/open reads as bottom-anchored. The actual `RectTransform.pivot` is not changed; first-open reveal and edit preview keep the default center-sizing behavior.
- Chest reroll is considered unlocked only when the resolved chest refresh limit is greater than `0`. When reroll is not unlocked, `ChestScreen` hides the authored reroll group; when it is unlocked but exhausted or otherwise unavailable, the UI stays visible and disabled.
- `Assets/LeeJunMo/Prefab/UI/GlobalUIRoot.prefab` is the representative source for chest first-open reveal timing, shake, and layout scalar settings. UI root variants and scene overrides should stay aligned to that source while preserving their authored references and hierarchy.
- `GlobalUIRoot.prefab` currently carries the temporary chest `RerollButton` reference plus its `RerollHoldProgress` child `Image` fallback for code/UI wiring. The preferred hold-button path is an authored `HoldActionButton` driving `HoldFillButtonView`; Unity `Button` and `rerollHoldProgressImage` are legacy compatibility paths until prefab/scene authoring is migrated.
- Player relic inventory UI can render a fixed 24-slot visual grid while `RelicInventory.Capacity` owns the currently unlocked capacity. `ItemSlotUI` treats `index >= container.SlotCount` as a locked slot, shows the prefab-authored lock sprite, and blocks drag/drop/click/hover item interaction.
- Inventory slot prefabs use existing `Assets/Sprites/UI/Inventory/Inventory Sprites.png` sub-sprites for slot backgrounds: `InventorySlot`, `InventoryWeaponSlot`, `InventorySlotPotion`, and `InventorySlotLock`. Item icon images should start empty and be enabled by `ItemSlotUI.Refresh()` only when an item exists.
- `InventoryElementPannel.prefab` is authored as a vertical frame: `InventoryTop`, `InventoryPanelBody`, then `InventoryBottom`. `InventoryPanelBody` keeps the existing weapon/consumable/relic/drop references. `InventoryTop` and `InventoryBottom` use tiled Images so width follows the authored layout while height stays prefab-authored.
- Detail/tooltip UI should project item/runtime state rather than own gameplay state.
- Flow-owned reward popups can appear while Dialogue is still suppressing non-dialogue UI. `RewardDisplayUI` owns that exception boundary through `DialogueService`'s captured-layer temporary visibility API: it reopens the authored Reward canvas and the shared non-raycasting Hover canvas for reward slot item details, then hides both again if Dialogue is still active when the reward popup closes.
- HUD scripts should project player/combat/status state; they should not own the state they display.
- `PlayerConsumableInventory.TryUseAt(...)` owns successful consumable-use feedback. For heal potions it plays the player-attached `HealParticle` only after `ConsumableDefinition.TryUse(...)` confirms HP increased.

### Weapon skill cooldown feedback and input buffering

- `WeaponSkillHudSlotPresenter` is the shared projection path for both the active and swap-weapon skill HUDs. A skill that cannot be used because of cooldown displays its existing icon at 60% RGB brightness while preserving alpha. Charge-count abilities stay bright while at least one charge remains and darken only at zero charges. Once actual cooldown starts, the base icon darkens immediately even while the ability is still executing; `activeOverlay` independently retains the active-cast feedback.
- `PlayerCombatInput2D` owns the player-only 0.08-second weapon Skill1/Skill2 cooldown input window. It stores the current weapon, slot and resolved ability, then retries once when cooldown reaches zero. Weapon changes, slot-resolution changes, pause/UI/flow/input blocking, invalid activation state, or a new busy state discard the request rather than producing a delayed surprise cast.
- `InputActionQuery` exposes owner-scoped action-press blocking. `WorldItemHoverPlayback` owns Skill1/Skill2 press blocks for the lifetime of a world item detail request, so tooltip Q/E preview input remains available to UI while gameplay and weapon-specific direct action queries cannot observe the same press. `PlayerCombatInput2D` sees that common block and clears pending apprentice/cooldown-buffered skill input. Basic attack, movement, dash, weapon swap and the skill HUD remain unchanged; this is separate from the tutorial-oriented full weapon-input block.
- Weapon runtime-state input hooks run before the common cooldown buffer, preserving specialized behavior such as Lightning Spear input handling and Crimson Boundary target consumption.
- Apprentice Hero Sword hold-charge input uses its existing dedicated pending path. Pressing inside the window waits without cancelling the current basic attack; releasing Skill1 before cooldown completion cancels the request. Charge time begins only after actual ability activation, so cooldown wait time never counts as free charge and a released key cannot produce an automatic minimum-charge attack.
- Key files: `Runtime/Features/Player/Input/PlayerCombatInput2D.cs`, `Runtime/UI/HUD/WeaponSkillHUD2D.cs`, `Runtime/UI/HUD/SwapWeaponSkillHUD2D.cs`, and `Runtime/UI/HUD/WeaponSkillHudSlotPresenter.cs`.

## Runtime Boundary Review

The current concern is not that the inventory UI lacks a strict MVP pattern. The concrete issue is that some view-facing UI scripts know and execute inventory transfer policy.

| Boundary | Intended responsibility | Current pressure point |
| --- | --- | --- |
| Visual | Icons, text, hover panels, drag image, highlight state, and reveal presentation. | Mostly acceptable; dynamic slot/detail views are expected, but core presentation component fallbacks still need authoring review. |
| UI Input | Pointer enter/exit, click, right-click, drag start/end, and drop request forwarding. | `ItemSlotUI` now forwards quick-move/drop post-action handling to helper services, but still starts/ends drag sessions and owns slot visual state. |
| Transfer Policy | Choose quick-move targets, validate destination, swap, merge relics, preserve relic levels, rollback on failure, and decide failure reason. | `InventoryQuickMoveService` owns quick-move target selection; `InventoryTransferService` owns drop, swap, relic merge, relic-level preservation, rollback execution, and minimal transfer failure details. |
| Runtime Data | Own actual item state for player, chest, world loot, equipment, consumables, weapons, and relics. | Player/chest container adapter implementations now live in the gameplay inventory runtime layer; UI views instantiate them but do not own their implementation files. |

### Reviewed Responsibility Mix

- `ItemSlotUI` is a view/input component. It no longer decides quick-move destinations, directly displays quick-move warnings, or owns quick-move/drop refresh handoff, but it still owns drag start/end wiring and slot visual state.
- `InventoryQuickMoveService` currently resolves right-click quick-move targets for chest, world loot, consumable, weapon, and relic containers, then reuses `ItemDragContext.TryDrop(...)` as a compatibility entry point.
- `ItemDragContext` is now closer to a drag-session holder: it stores source/index/item/relic level, plays grab audio on begin, clears/cancels sessions, and delegates transfer execution to `InventoryTransferService`.
- `InventoryTransferService` is a pure helper in `InventoryTransferService.cs`; it executes same-container swaps, cross-container swaps, relic level preservation, same-relic merge index correction, target rollback on source-set failure, and returns `InventoryTransferResult` failure details.
- `InventoryTransferService` handles existing player relic merges as an absorb/clear-source transfer before swap validation. This keeps world-loot UI quick-move aligned with direct world pickup when the source container is read-only and cannot accept swapped items.
- `InventoryQuickMoveResult` now carries transfer failure details and warning codes. `InventorySlotTransferInteractionService` handles slot-level warning presentation and quick-move/drop refresh handoff.
- `InventoryDeliveryWarningResolver` shares overlapping warning-code mapping for quick-move full inventory, player relic adapter rejection, and world pickup relic/consumable rejection.
- `PlayerInventoryPanelView.cs` and `ChestScreen.cs` now instantiate adapter classes that live under `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Inventory/Runtime/`. This removes the adapters from MonoBehaviour class bodies, view files, and UI-adjacent folders while preserving constructor calls and runtime behavior.
- `HUD`, `ItemDetailPanel`, tooltip/glossary, and detail views are lower-priority refactor targets because they mostly project current state instead of owning gameplay state.
- Tooltip and HUD content is expected to be dynamic. The authoring risk is not runtime text/icon/row changes; it is full visual-tree fallback construction in code when prefab/template authoring should own the base layout.

### Refactor Candidate

- Track the concrete transfer-policy split in `Docs/RefactorBacklog/InventoryTransferResponsibilitySplit.md`.
- First implemented slice: quick-move target selection moved out of `ItemSlotUI` into `InventoryQuickMoveService`.
- Second implemented slice: transfer execution moved out of `ItemDragContext.TryDrop(...)` into `InventoryTransferService`, while keeping `TryDrop(...)` as the source-compatible wrapper.
- Third implemented slice: `InventoryTransferResult` now carries minimal failure reasons and warning code data, and `InventoryQuickMoveService` consumes the result-returning drop path without changing `ItemSlotUI` warning display.
- Fourth implemented slice: player and chest container adapters moved from nested MonoBehaviour classes into dedicated helper files without behavior changes.
- Fifth implemented slice: `InventorySlotTransferInteractionService` moved quick-move warning presentation and quick-move/drop refresh handoff out of `ItemSlotUI`.
- Sixth implemented slice: `InventoryDeliveryWarningResolver` shares quick-move, player relic adapter, and world pickup overlapping warning-code mapping.
- Seventh implemented slice: player and chest container adapter source/meta files moved to the gameplay inventory runtime layer.
- Current target shape: keep `ItemSlotUI` as view/input adapter, keep `ItemDragContext` as drag-session state, and keep inventory container adapter implementations outside individual UI view ownership.
- Track Status HUD and tooltip visual-template fallback under the broader `Docs/RefactorBacklog/RuntimePresentationFallbackAuthoringSplit.md` candidate instead of creating a duplicate HUD-only backlog.

## Extension Entry Points

- Add inventory screen behavior through Inventory Common UI and stack interfaces.
- Add chest reveal behavior through Chest UI / Reveal and documented input blocker flow.
- Add chest reroll UI by extending `ChestScreen` presentation/input/progress wiring and read-only chest refresh state APIs; keep refresh generation and consumption rules inside the runtime chest policy path.
- Add item explanation through Common Detail UI and item-specific detail providers.

## Known Pitfalls

- First-open chest input blocking has had regressions; check `Docs/ErrorLog.md` and `Docs/StructureMemory/UIFlowInputBlocking.md` before changing it.
- `OpenedUi`/`FirstOpenedUi` mean the chest UI open succeeded, not merely that the world chest open interaction was requested. Use those events for tutorial continuation that depends on the player seeing the chest UI.
- When synchronizing chest reveal presentation across scenes, change scalar motion/shake/layout values only unless a prefab/scene reference migration has been explicitly reviewed.
- Reroll pose control should start from the current reveal progress. The intended sequence is fast close, closed-state shake until the 1.5 second hold threshold, successful refresh, then fast open with open particles and opened-position slot reveal particles. Cancel reopen paths should not trigger VFX or consume rerolls outside `TryRefreshLoot()`.
- Reroll manual reveal uses bottom-anchor compensation only on the reroll path. Do not change the authored chest panel pivot to solve this visual unless the first-open reveal layout is reviewed separately.
- Reroll hold-progress UI should stay authored on the prefab/scene button. `HoldActionButton` should own hold availability, timing, and progress; `ChestScreen` should consume its events and must not runtime-create progress Images or make Unity `Button` the primary reroll state owner.
- Avoid runtime creation of full UI hierarchy unless explicitly approved; dynamic tooltip/HUD data is acceptable, but the base visual template should be prefab/scene authored when it is build-facing.
- Do not rename serialized UI fields without prefab/scene migration review.
- Relic locked-slot display depends on `ItemSlotUI.prefab` referencing the existing `InventorySlotLock` sub-sprite from `Assets/Sprites/UI/Inventory/Inventory Sprites.png`; do not replace that PNG or `.meta` as a side effect of slot unlock work.
- Slot/frame visual changes should only change prefab sprite references or authored layout components unless the user explicitly asks to replace `Inventory Sprites.png`. Confirm the `InventoryWeaponSlot` sub-sprite resolves correctly in Unity Inspector because this prefab references its sprite internal ID directly.
- `Find*` and `AddComponent` fallbacks in inventory/chest/HUD UI are authoring-risk markers. Treat them as prefab/scene wiring cleanup candidates, not as proof that UI should own runtime state.

## Promotion Candidate

Not yet. Some input-blocking behavior is documented in `UIFlowInputBlocking`; inventory/chest rules should stay here until stable enough for Architecture or Contract promotion.

## Chest Acquisition Budget and Sealed Weapons (2026-09-12)

- Current implementation paths are under Assets/_Project/Runtime. ChestInventory owns a lifetime budget of two acquired items; UI observes AcquiredCount and AcquisitionRejected. Successful outward transfers, including swaps and direct world drops, consume one. Failed transfers and rearrangement inside the same chest do not. Returning an item does not refund the budget.
- InventoryTransferService checks both chest sides of a swap before mutation and records only success. DropZoneUI applies the same policy for direct chest-to-world drops. ChestScreen projects the counter and owns its unscaled red/shake/white tween, releasing subscriptions and tweens when unbound.
- ChestInventory.Clear deliberately preserves the budget during reroll. DungeonObjectRuntimeStateData.chestAcquiredCount is captured/restored through DungeonRoomBuilder and copied by RunSessionStateService. Old snapshots default to zero.
- GlobalUIRoot.prefab and ChestUI.prefab author AcquisitionCount; the counter uses LayoutElement.ignoreLayout so it does not resize the chest slots.
- WeaponInventory2D keeps sealing as an equip restriction. Storage, inventory swaps, and removal remain available; swapping an equipped weapon into a sealed slot unequips it and selects an accessible occupied slot. Existing active-weapon cleanup and Flowering swap restrictions still apply.
- Known boundary: direct gameplay code mutating ChestInventory.Set is not a player acquisition operation. New player-facing extraction paths must use the acquisition checks.

### Refund receipts and BlackOutline (2026-09-12 follow-up)

This follow-up supersedes the non-refundable budget above. ChestInventory records one item-definition receipt per acquisition. A matching return refunds one receipt; unrelated deposits do not. Copies of the same definition are interchangeable in the existing inventory model.

InventoryTransferService validates a simultaneous return before rejecting an at-limit exchange, and updates receipts only after a completed transaction. Player relic merges never return the existing target relic. DropZoneUI records the removed item's receipt as well.

DungeonObjectRuntimeStateData.chestOutstandingAcquisitions accompanies chestAcquiredCount through TreasureChest, DungeonRoomBuilder, and the run-state clone. Clear/reroll preserves receipts. Old count-only saves/hot-reloaded chests allow one deposit refund per unknown receipt, bounded by their old count; new receipts require the matching definition.

AcquisitionCount in GlobalUIRoot and ChestUI uses Galmuri9 SDF BlackOutline and its embedded material. Face-color warning tweens preserve the black outline.

### Chest return policy and presentation (2026-09-12, supersedes earlier fallback/slot guard)

ChestInventory outstanding receipts now strictly require matching item definitions. ChestContainerAdapter.CanPlace and InventoryTransferService reject foreign inbound deposits/swap replacements; raw setters remain available for transactional rollback. Count-only legacy state fails closed. Copies of the same definition are not uniquely identified by this inventory model.

ChestRewardPolicy uses zero outstanding acquisitions plus the existing generated/manager/reroll-budget conditions. The old slot-index refresh guard was removed so all-item returns to different slots and restored chest state work consistently.

ChestScreen projects the open chest's outstanding receipt quantities onto player ItemSlotUI instances (including the shared InventoryRoot panel). Per-definition marker counts cannot exceed outstanding acquisition counts: newly changed matching slots take priority, followed by existing markers and stable slot order for restored receipts. ItemSlotUI receives a presentation-only boolean; it no longer independently uses definition membership to highlight every duplicate. Same-definition return eligibility remains interchangeable; these markers are not persistent item provenance. Unbinding clears markers. Authored AcquisitionCount is centered under TopChestFrame with a CanvasGroup; it fades in after first-open/reroll reveal. ChestCloseHint is authored at screen top center. CombatFeelAndQuestInstaller preserves these placements and references.

Related verification: [2026-09-12 session](../../SessionLogs/2026-09-12.md).

### Lone first weapon exception (2026-09-12)

WeaponInventory2D.TrySwapWeaponSlots rejects moving a sole weapon from index 0 into empty index 1, including reverse-source drag of that empty slot. This applies with or without a seal. Two occupied slots still swap and a new second weapon can still be acquired. The storage/drop exception for sealed slots otherwise remains.

### Source-specific weapon availability (2026-09-12)

LootPoolItemSelectionService.CanDropWeapon rejects Weapon.WindWeapon globally and gates Weapon.Flowering / Weapon.OddIron behind explicit treasure-chest selection. LootPoolService.GetRandomTreasureChestWeapon is used by normal and override ChestLootGenerationService paths (including reroll/boss rewards). Other generic selection and candidate-based Grave selection exclude them. ShopInventoryRoll applies the same non-chest policy to new stock. Existing owned weapons, saved stock and world pickups are not removed; inventory dropping is not a random loot roll. Weapon definitions/unlock databases remain intact.


## Combat eligibility (2026-09-16)

InventoryUIManager gates CanOpen, TryOpen and direct Open with MonsterSpawnRoomGroup.IsPlayerInCombat. A completed room with no living/reserved/held encounter members permits inventory immediately; no damage grace timer remains. Game-over inventory inspection retains its existing exception. An already open inventory can still close. The same query is used by LevelRewardSessionController; see LevelProgressionStructure.md.


## Hub unarmed departure guidance

- HubWeaponDepartureGuide owns guidance sessions until weapon equipment, departure/disable or target replacement. The authored HubWeaponGuidanceArrow prefab uses UI/DownArrow.png, pointing down with identity rotation.
- GraveInteractable and ChestInteractable merge ordinary interaction highlight with owner-scoped guidance requests through SetGuidanceHighlight. A hover exit does not clear active guidance; releasing one guide leaves other owners intact. Disable clears both states; looted graves suppress their outline. The guide releases its request before replacing/clearing its target. Existing OutlineMaterial supplies the outline.
- The shared WeaponHUDUI/Skill1UI cooldown TMP is fixed at 26pt (auto size disabled).


## Weapon and relic tooltip display sources

- `WeaponDetailViewV2` and `EncyclopediaItemRightPage.BuildWeaponAbilityBlocks` normally project Skill1/Skill2. `Weapon.CrimsonBoundary` additionally projects its Attack definition through the same authored ability-block prefab, before those skills. Its existing clear/rebuild lifecycle owns the extra display block; no gameplay state is stored in it.
- Weapon copy lives in `WeaponDefinition.storyText` and `AbilityDefinition.description`; LightningSpear Skill1 also supplies variant bodies from `LightningSpearSkill1Data`. Relic effects come from `RelicLogic.BuildTooltip`, with serialized `effectTemplate` overrides taking precedence over C# defaults. Weapon-exclusive relics use `RelicDefinition.description` through `RelicLogic_WeaponExclusive`.
- `DetailTextFormatter` resolves semantic color tokens and glossary links after relic value substitution. Review both authored overrides and generated bodies when changing copy. Exact input glyphs come from the authored binding service, so code fallback defaults and weapon fallback hint strings alone do not establish the current input mapping.
- This remains a UI projection map, not an Architecture/Contracts promotion candidate. See [the tooltip copy session](../../SessionLogs/2026-09-17.md#approved-weapon-and-relic-tooltip-copy-pass) for verification limits and the outstanding CrimsonBoundary damage-copy discrepancy.

## Chest selection and confirmation (2026-09-17)

This flow supersedes immediate chest acquisition/return interaction described in older sections above.

- `Runtime/UI/Chest/ChestScreen.cs` binds a selection-only `ChestContainerAdapter`. Only occupied source slots are instantiated. Left or right click moves the same `ItemSlotUI` into the authored selected-items grid; either click restores its original source order when already selected. Selection remains provisional and does not remove runtime loot.
- Tutorial guidance can optionally acquire `ChestScreen.AcquireGuidedSelection(owner)`: direct selection, confirmation and reroll are blocked until that owner forwards `TrySelectGuidedFirstSlot` and releases ownership. Normal callers have no guidance owner. `SelectionCommitted` fires only after a successful transfer; slot/confirm/inventory accessors expose current view state. The scene-owned overlay, pointer/navigation restriction and confirm pulse live in `PrototypeChestNavigation`, described in [TutorialSupportStructure](TutorialSupportStructure.md), not in shared prefab defaults.
- Up to two rewards can be selected; one is sufficient to confirm. `ChestSelectionTransferService`, in `Runtime/UI/Inventory/Common/InventoryQuickMoveService.cs`, reserves destinations for the entire selection, including cumulative duplicate-relic levels, before calling the existing transfer service. Insufficient space leaves both inventories and the selection unchanged and asks the player to drag inventory items to the discard region. Completed transfers are rolled back in reverse order if a later transfer fails.
- `InventoryTransferService` rejects selection-only chest adapters before any target mutation, including relic merging. Chest slot drag/drop and right-click acquisition are disabled, and player quick-move into this chest is ignored. Player inventory rearrangement and world dropping remain available.
- `ChestScreen` owns only provisional slot choices and unscaled movement tweens. During movement it temporarily suspends the two grids' layout drivers and moves the travelling slot above the viewport mask. Completion or disable restores parenting, original ordering, and prior driver states. Closing without confirmation cancels choices; a successful reroll rebuilds occupied slots and clears choices.
- `ChestUIManager.CompleteOpenedChest` delegates reward completion to `TreasureChest.CompleteLootSelection`, then the screen closes through the existing UI stack. Completion clears remaining rewards and deactivates the world chest. Preserved dungeon state uses the existing nonzero acquisition count plus empty loot to restore the completed chest as inactive, including a chest nested below a saved object root.
- Selected panel/grid, count label, and confirm button are serialized references authored in `ChestUI.prefab` and all six `GlobalUIRoot*.prefab` files. The panel ignores the chest frame's parent layout. Shared and embedded discard regions are tinted red, labelled in Korean, and remain faintly visible when idle. No runtime panel/button hierarchy is created.
- Extension points: selection presentation in `ChestScreen`; batch reservation/rollback in `ChestSelectionTransferService`; native relic validation in `PlayerRelicContainerAdapter.PreviewSelection`. Gameplay inventory effects continue to belong to the existing inventory owners.
- Known limitation: a custom inventory that rejects rollback cannot be made atomic by the UI service; that exceptional path retains the acquired item, logs an error, and leaves remaining rewards available. Legacy scene-local ChestScreen copies in `HeoMinSeokScene`, `LEeJunmo`, and `LEeJunmo 1` already have null slot-prefab references and were not migrated as part of this prefab-based flow.
- Verification and remaining visual checks: [session log](../../SessionLogs/2026-09-17.md#chest-selection-confirmation-and-discard-area). This remains a StructureMemory map, not a new Architecture/Contracts authority.

### Reroll and discard layout correction (2026-09-18)

- The existing reroll groups in `GlobalUIRoot` and `GlobalUIRoot_Salryojo` now sit inside the chest's 200-unit lower frame, from 16 to 166 units above its bottom. The prior top-pivot group extended 150 units below the chest and could leave the screen after the selection-panel layout moved the chest down. Hold controls, fill/count references and unlock rules are unchanged.
- Discard targets in `InventoryElementPannel` and the four embedded legacy root copies use bottom horizontal stretch anchors, zero added width, top pivot and a 12-unit gap beneath the inventory panel. Labels stretch inside the target. Authored `fitHeightBelowPanel` enables `DropZoneUI.LateUpdate` to fit the remaining space down to a 16-unit root-canvas bottom margin, converting canvas coordinates to panel-local coordinates for scaled layouts. This updates presentation geometry only and does not own item state or create UI objects.


## Selected chest slot background

ChestUI.prefab authors two permanent SelectedItemSlot frames (72px, 8px gap) as siblings behind SelectedItemsGrid. They remain visible when the selection is empty and never participate in the dynamic grid layout. ChestScreen.ToggleSelection moves existing item slots above the frames; ItemSlotUI.SetChestSelectionOverlay hides only the item's own background alpha, preserving the full-slot raycast surface and hover/error border. Deselecting restores the original background color. No empty slots are created at runtime; ClearChestSlots only destroys dynamic item slots.


## Chest confirmation blockers

ChestSelectionTransferService exposes TryCreatePlanWithFailure/TryCommitPlanWithFailure with the rejected source index; existing entry points retain their signatures and delegate to the same rules. ChestScreen reuses reservation previews every .15 unscaled seconds while a selection exists, removing each rejected candidate from a temporary validation list to identify remaining blockers. This is read-only; selection order determines capacity overflow. Commit-only failures show the failing slot for one second before revalidation. Existing warnings/transfer rollback remain unchanged.

ItemSlotUI.SetSelectionBlocked projects this state onto the existing hover highlight Graphic: red at action alpha even without hover, restoring the authored color when cleared or disabled. Clearing selection or resolving capacity/relic conditions removes the indication; no gameplay validity is owned by the slot. Scene/prefab hierarchy is unchanged.

- Authored-copy caveat: GlobalUIRoot.prefab and its Deafiso/DialogueUpdate/Salryojo/Sub/Water variants embed independent chest UI hierarchies; editing ChestUI.prefab alone does not update them. The two permanent selected-item frames and 8px spacing are present in all seven copies; ChestFixedFramesValidation.py checks them together.
