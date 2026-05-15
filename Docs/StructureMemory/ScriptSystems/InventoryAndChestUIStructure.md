---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-05-15
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
- `Assets/HeoMinSeok/_Project/Scripts/UI/Inventory/Common/ItemSlotUI.cs`
- `Assets/HeoMinSeok/_Project/Scripts/UI/Inventory/Common/DragIcon.cs`
- `Assets/HeoMinSeok/_Project/Scripts/UI/Inventory/Common/DetailUI/ItemDetailPanel.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Inventory/Chest/Runtime/TreasureChest.cs`

## Ownership And Lifecycle

- Stack UI open/close policy should remain owned by `UIManager` and stack screens.
- Chest first-open presentation has separate world timing and UI reveal timing; do not conflate those lifetimes.
- Detail/tooltip UI should project item/runtime state rather than own gameplay state.
- HUD scripts should project player/combat/status state; they should not own the state they display.

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
- Add item explanation through Common Detail UI and item-specific detail providers.

## Known Pitfalls

- First-open chest input blocking has had regressions; check `Docs/ErrorLog.md` and `Docs/StructureMemory/UIFlowInputBlocking.md` before changing it.
- Avoid runtime creation of full UI hierarchy unless explicitly approved; dynamic tooltip/HUD data is acceptable, but the base visual template should be prefab/scene authored when it is build-facing.
- Do not rename serialized UI fields without prefab/scene migration review.
- `Find*` and `AddComponent` fallbacks in inventory/chest/HUD UI are authoring-risk markers. Treat them as prefab/scene wiring cleanup candidates, not as proof that UI should own runtime state.

## Promotion Candidate

Not yet. Some input-blocking behavior is documented in `UIFlowInputBlocking`; inventory/chest rules should stay here until stable enough for Architecture or Contract promotion.
