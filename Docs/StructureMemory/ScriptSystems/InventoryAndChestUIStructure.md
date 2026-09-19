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

### Death item presentation

- `PlayerDeathReturnToHub2D.CoDeathSequence` reads weapon, relic, consumable and backpack slots after control cleanup and starts all occupied-slot copies together. Normal defeat/time-over use this path. Tutorial boss defeat now invokes the same one-shot scatter from `PrepareForScriptedDeathPresentation`, while the normal death component remains disabled and the tutorial retains its dialogue/game-over/Hub routing. A repeated preparation call does not duplicate copies.
- Original containers and relic levels remain authoritative and unchanged. Existing GameOver inventory inspection reads them until the return action ends the run; no second inventory snapshot or save format is introduced.
- `LootManager.SpawnDeathItemCopy` and `LootSpawnService.SpawnPresentationCopy` reuse the authored world-item prefab, ground-position resolver and drop-animation backend. Copies carry item definitions/levels for visual display only, not weapon runtime ownership.
- `WorldItemPickup2D.MakePresentationOnly` permanently locks the copy and removes it from `WorldItemRegistry`, including after disable/re-enable. Direct interaction cannot deliver it, and animation completion cannot unlock it.
- The death component owns spawned copies, moves them into the player's scene and disables/destroys them on owner disable or destruction. The destruction path matters when tutorial defeat spawns copies after the component has already been disabled. Cleanup resets the one-shot guard; scene unload also destroys copies. Missing loot configuration skips visuals without removing original inventory.
- Extension entry point: death-scatter timing belongs to the death sequence; pickup safety belongs to the pickup. Actual scene Play Mode still needs visual review for crowded bags and wall/pit edges. This is a feature map, not an Architecture/Contracts promotion candidate.

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
- Up to two rewards can be selected; one is sufficient to confirm. `ChestSelectionTransferService`, in `Runtime/UI/Inventory/Common/InventoryQuickMoveService.cs`, reserves destinations for the entire selection, including cumulative duplicate-relic levels, before calling the existing transfer service. Insufficient consumable/relic space leaves both inventories and the selection unchanged; weapon capacity uses the batch replacement rule below. Completed transfers are rolled back in reverse order if a later transfer fails.
- `InventoryTransferService` rejects selection-only chest adapters before any target mutation, including relic merging. Chest slot drag/drop and right-click acquisition are disabled, and player quick-move into this chest is ignored. Player inventory rearrangement and world dropping remain available.
- `ChestScreen` owns only provisional slot choices and unscaled movement tweens. During movement it temporarily suspends the two grids' layout drivers and moves the travelling slot above the viewport mask. Completion or disable restores parenting, original ordering, and prior driver states. User close requests with pending choices are blocked; forced teardown still cancels choices; a successful reroll preserves chosen slots and rebuilds only unselected offers.
- `ChestUIManager.CompleteOpenedChest` delegates reward completion to `TreasureChest.CompleteLootSelection`, then the screen closes through the existing UI stack. Completion clears remaining rewards and deactivates the world chest. Preserved dungeon state uses the existing nonzero acquisition count plus empty loot to restore the completed chest as inactive, including a chest nested below a saved object root.
- Selected panel/grid, count label, and confirm button are serialized references authored in `ChestUI.prefab` and all six `GlobalUIRoot*.prefab` files. The panel ignores the chest frame's parent layout. Shared and embedded discard regions are tinted red, labelled in Korean, and remain faintly visible when idle. No runtime panel/button hierarchy is created.
- Extension points: selection presentation in `ChestScreen`; batch reservation/rollback in `ChestSelectionTransferService`; native relic validation in `PlayerRelicContainerAdapter.PreviewSelection`. Gameplay inventory effects continue to belong to the existing inventory owners.
- Known limitation: a custom inventory that rejects rollback cannot be made atomic by the UI service; that exceptional path retains the acquired item, logs an error, and leaves remaining rewards available. Legacy scene-local ChestScreen copies in `HeoMinSeokScene`, `LEeJunmo`, and `LEeJunmo 1` already have null slot-prefab references and were not migrated as part of this prefab-based flow.
- Verification and remaining visual checks: [session log](../../SessionLogs/2026-09-17.md#chest-selection-confirmation-and-discard-area). This remains a StructureMemory map, not a new Architecture/Contracts authority.

### Reroll and discard layout correction (2026-09-18)

- The existing reroll groups in `GlobalUIRoot` and `GlobalUIRoot_Salryojo` now sit inside the chest's 200-unit lower frame, from 16 to 166 units above its bottom. The prior top-pivot group extended 150 units below the chest and could leave the screen after the selection-panel layout moved the chest down. Hold controls, fill/count references and unlock rules are unchanged.
- Discard targets in `InventoryElementPannel` and the four embedded legacy root copies use bottom horizontal stretch anchors, zero added width, top pivot and a 12-unit gap beneath the inventory panel. Labels stretch inside the target. Authored `fitHeightBelowPanel` enables `DropZoneUI.LateUpdate` to fit the remaining space down to a 16-unit root-canvas bottom margin, converting canvas coordinates to panel-local coordinates for scaled layouts. This updates presentation geometry only and does not own item state or create UI objects.


## Selected chest slot background

`ChestUI.prefab` and the six independent `GlobalUIRoot*.prefab` copies author two permanent `SelectedItemSlot` cells as direct children of `SelectedItemsGrid`. The GridLayoutGroup owns their 72px size and 8px gap. `ChestScreen.ArrangeSelectedSlots` places each selected `ItemSlotUI` inside a cell, centered over its background; the temporary travelling item still uses the panel as a mask-free transit parent and returns to its cell on completion/cancellation. Empty cells remain authored and visible. Clearing choices destroys only generated item views, explicitly unbinding their container events before destruction.


## Chest confirmation blockers

ChestSelectionTransferService exposes TryCreatePlanWithFailure/TryCommitPlanWithFailure with the rejected source index; existing entry points retain their signatures and delegate to the same rules. ChestScreen reuses reservation previews every .15 unscaled seconds while a selection exists, removing each rejected candidate from a temporary validation list to identify remaining blockers. This is read-only; selection order determines capacity overflow. Commit-only failures show the failing slot for one second before revalidation. Existing warnings/transfer rollback remain unchanged.

ItemSlotUI.SetSelectionBlocked projects this state onto the existing hover highlight Graphic: red at action alpha even without hover, restoring the authored color when cleared or disabled. Clearing selection or resolving capacity/relic conditions removes the indication; no gameplay validity is owned by the slot. Scene/prefab hierarchy is unchanged.

- Authored-copy caveat: GlobalUIRoot.prefab and its Deafiso/DialogueUpdate/Salryojo/Sub/Water variants embed independent chest UI hierarchies; editing ChestUI.prefab alone does not update them. The two permanent selected-item frames and 8px spacing are present in all seven copies; ChestFixedFramesValidation.py checks them together.


## First Weapon Swap Hint (2026-09-19)

- `GamePlayData.weaponSwapHintUnlocked/Completed` store per-run progress across player recreation and scene transitions. `RunSessionLifecycleService` resets both on run start/end; development reset also clears them. They are not profile-wide tutorial flags.
- `WeaponInventory2D` records availability when two accessible slots are occupied, both on inventory notifications and its existing Update (covering run start with an already populated inventory). A successful `Swap()` marks completion; successful `OneSwordOathLevelRewardEffectSO.Apply` also completes the hint because swapping is no longer available; pickup auto-equip, direct Equip, inventory slot rearrangement and rejected swap attempts do not complete it. Current production Swap callers are PlayerCombatInput2D's mapped SwapWeapon input paths.
- `WeaponSwapHintPresenter` on GlobalUIRoot's LevelHUD reads state only. Its authored sibling prompt under GameplayHUDCanvas follows the registry player above the head, uses the same color/hover rhythm as the level-up prompt, and participates in the shared overhead layout: tutorial below Tab below level-up, with only visible rows occupying space. PlayerOverheadPromptLayout (in PrototypeTutorialPromptView.cs) uses screen-space rectangle heights across canvases; the legacy serialized levelUpPrompt reference is retained for asset compatibility but no longer controls ordering. OnDisable hides the view without resetting progress; missing player/camera, inactive run, missing or sealed weapon slots hide it temporarily.
- `InputBindingService` supplies the current primary SwapWeapon glyph (default Tab). The presenter checks for key changes while visible and on enable; missing artwork falls back to a key label alongside 무기 교체. No input interception or gameplay UI state ownership is added.
- The prefab owns all prompt objects and references; no runtime UI creation, new singleton or bootstrap change. Source/build and serialized reference checks passed; rendered overlap/position, actual remapping and real input/scene-transition playtests remain unexecuted.

- One Sword Oath dismisses the swap hint only after the slot seal, attack modifier and cooldown modifier all succeed. Rejected/rolled-back application does not complete it. Completion lasts until the next run reset.


## Pending Chest Selection Close Guard (2026-09-19)

- `InventoryScreen.TryHandleCloseRequest` delegates chest-mode close requests to `ChestScreen`. Shared UIManager Escape/PopUI, inventory-toggle and close-button paths therefore retain provisional selections instead of silently discarding them. First-open reveal keeps its existing close lock; empty selection and successful confirmation allow normal closing.
- `ChestScreen` sends the warning 아이템을 획득하거나 선택을 해제해 주십시오. through WarningPopupPlayback and enables its authored yellow confirm-button Outline. The border remains until all choices are deselected, confirmation succeeds, or selection binding is cleared/disabled/rebuilt. A failed acquisition keeps the border and selection.
- `ChestUI.prefab` and all six independent `GlobalUIRoot*.prefab` copies author the disabled Outline and reference it from ChestScreen. No runtime UI/component creation. Low-level CloseUI/forced cleanup remains usable for teardown; only user close requests are guarded.


## Item-type slot borders (2026-09-19)

- `ItemSlotUI.Refresh` projects the actual item type into an authored `ItemTypeBorderGraphic`; ClearIconAndLevel clears it for empty/locked/disabled slots. Weapon/relic appearance follows the item when moved between containers, rather than being tied to the container type.
- `ItemTypeBorderGraphic` draws a blue-gray rectangular frame with heavy corner brackets for weapons and a gold cut-corner frame with diamond ornaments for relics. It is a MaskableGraphic on the base ItemSlotUI prefab, inherited by weapon/consumable variants; colors are authored on the component, geometry scales with slot size. No runtime GameObjects, generated raster textures, manager or event subscriptions.
- Its non-raycast child sits above the icon but below the existing animated hover/red-error border and level label. Chest selected-panel background suppression does not hide this independent type border; potion/empty/locked slots draw no type border. Existing rarity/level/drag/selection rules are unchanged.
- MSBuild and source/prefab checks passed. Actual rendered frames, selected-panel overlap and mouse interaction need Unity visual confirmation.

### Chest weapon replacement and retained reroll (2026-09-20)

- The user's recovery tasks 3/4/5 supersede full-weapon-inventory pre-selection rejection. Selecting two weapons replaces both slots in selection order; selecting one fills an empty slot or replaces slot 0 when full, regardless of active slot. The final pair still obeys duplicate-weapon restrictions. Sealed slots may store weapons but cannot be equipped.
- `ChestSelectionTransferService` validates the whole batch, commits fallible consumable/relic transfers first, then delegates weapon acquisition through `PlayerWeaponContainerAdapter` to `WeaponInventory2D.TryAcquireChestSelection`. A failed companion transfer leaves weapons and world drops untouched. Invalid/repeated/selection-only commit requests fail before mutation.
- `WeaponInventory2D` previews the final pair, checks active weapon locks and required drop prefab, captures outgoing persistent payloads, uses existing unequip/ability cleanup, replaces the pair, equips an accessible slot, then emits the outgoing weapons via the existing WeaponDrop2D prefab and ground-position resolver. Unchanged slot runtime data is retained. Ordinary pickup/quick-move replacement behavior is unchanged.
- Reroll passes selected source indices through `ChestUIManager` to `TreasureChest.TryRefreshLoot(indices)`. `ChestInventory.ClearExcept` keeps their definitions and relic levels. ChestLootRequest carries retained definitions into generation: they consume their own weapon/relic/consumable quotas, retained weapon IDs seed the exclusion set, and override consumable filling includes all retained items. TreasureChest then fills only the newly generated remainder against the preserved inventory, so existing relic level-cap filtering includes retained relic levels. Successful reroll consumes one refresh use without acquiring retained items. Existing acquisition receipts/limits remain unchanged.
- `ChestScreen.RebuildUnselectedChestSlots` preserves the actual selected view objects/order and recreates only other offers. The selected panel and count stay visible throughout reroll while selection/confirmation is temporarily non-interactive. Rebuilt reveal particles target only fresh offers.
- `ChestFirstOpenRevealPresentation.CaptureInitialLayout` freezes the complete opened frame/grid metrics after `ChestScreen.BuildChestSlots`. Selection, deselection and reroll only reflow contents; reveal animation still uses 0..1 of the captured middle height. A grid ContentSizeFitter, if present, is held disabled and restored by ReleaseInitialLayout when binding is cleared. Reroll never recaptures; the next binding measures its own initial loot.
- The middle body height comes from the initial grid rows and layout padding. Left/right decorative frames follow that height; their authored or previous chest heights must not participate in the measurement (GlobalUIRoot decorations are authored at 415, while five items require a 225-high grid).
- Validation lives in `Tools/Validation/ChestSelectionRecoveryRegression.cs` and `ChestFixedFramesValidation.py`; native tests use freshly built assemblies in an isolated Unity project. Full gameplay visual inspection remains a separate manual check.

### Chest glyph click and inventory close

- `HoldActionButton.PointerClicked` is an optional short-click event. A completed pointer hold suppresses its subsequent click; existing hold-only consumers do not subscribe. `ChestScreen` subscribes alongside hold events and runs the same refresh/reveal completion path after validating availability. Keyboard Space retains hold behavior.
- `GlobalUIRoot/InventoryRoot/InventoryCloseButton` is authored with an Image, Button and label, wired to `InventoryScreen.closeButton`. It is shown in player-only mode; chest mode retains its existing close control and selection checks.
- Selected-item reroll and selection-panel visibility are handled by the recovery flow described above.

## Selected item stat projection (2026-09-20, task 6)

- `ChestScreen` sends a valid `ChestSelectionTransferService` plan to the existing standalone or shared `PlayerStatPanelView`; invalid batches clear the preview. Selection changes refresh immediately, and existing selection validation refreshes every 0.15 unscaled seconds. Teardown and confirmation clear the projection; reroll retains it for retained choices under the task 3-5 policy.
- `AttributeSet.CreatePreviewSnapshot` creates detached attribute/modifier state with snapshot-local max links. `RelicInventory.ProjectAcquisitions` applies ordered relic upgrades and linked HP compensation without executing equip/proc hooks, then uses `WeaponStatBinder.ProjectChange` and reevaluates health/movement-dependent relics. `WeaponInventory2D.PreviewEquippedAfterAcquisitions` follows the current batch destination/active-slot policy; stored inactive weapons do not add equipped stats.
- `AttributeStatProvider.Get` supports a supplied read function so preview uses actual composite/multiplier rules. `PlayerStatPanelView` formats current values plus signed green/red deltas; percent values use percentage-point changes, HP displays the delta only beside maximum HP (current HP remains its live number), and changes rounding to zero omit the suffix. Linked current-HP projection remains available internally for conditional relic calculations.
- `RelicLogic.AppendPreviewModifiers` remains the extension point for acquisition-time stats. Health ratio, current-health movement, Last Stand and movement-derived critical chance can read snapshot values through `RelicContext.ReadPreviewAttribute`. Future triggered hit/kill buffs are not activated speculatively. Newly authored persistent stat logics need a corresponding preview implementation.
- No UI hierarchy or serialized asset changes. Same-owner rebind/re-enable restores stat event subscriptions. Verification details: [task 6 session record](../../SessionLogs/2026-09-20.md#chest-selection-stat-preview-task-6). This is a structure map, not a new Architecture/Contracts authority.


### Game-over inventory entry point (2026-09-20)

- `GameOverPresentationController.gameOverInventoryButton` references a dedicated authored `InventoryOpenHudButton` in both `Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab` and `GlobalUIRoot_Salryojo.prefab`. `InfoGroup/GameOverInventoryButton` shares the return button style and sits at bottom-center Y=98, size 260x86, with a 16-unit gap below ReturnButton (Y=200).
- The dedicated button uses the existing serialized `InventoryUIOpenRequestHandler`; `InventoryUIManager` still routes game-over opens through inspection-only mode and the temporary Popup/Hover canvas lift. InfoGroup owns its fade and input gating. Button visibility follows `AllowInventoryDuringPresentation`; the text label has no HUD key glyph.
- The controller disables the original HUD presenter and hides its root without reparenting or changing its RectTransform. Reset, disable and destruction restore the captured enabled/active state. HUD lookup excludes presenters beneath game-over controllers so it cannot hide the dedicated entry point.
- Layout changes belong to the authored prefab button. No runtime hierarchy creation, inventory state ownership change, or Architecture/Contracts promotion is needed. Play Mode visual/click verification remains pending for this change.
