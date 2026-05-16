---
status: resolved
authority: refactor-backlog
category: inventory-transfer
last_reviewed: 2026-05-15
---

# Inventory Transfer Responsibility Split

## Current Problem

Inventory/chest UI scripts currently mix view/input responsibilities with inventory transfer policy.

- `ItemSlotUI` displays a slot and handles pointer/drag input. Quick-move target selection, transfer execution, slot-level warning presentation, and post-transfer refresh handoff are extracted, but the slot still begins/ends drag sessions and owns slot presentation state.
- `InventoryQuickMoveService` now decides quick-move targets for chest, world loot, consumable, weapon, and relic containers, and can consume detailed transfer results while preserving the existing `ItemDragContext.TryDrop(...)` compatibility entry point.
- `ItemDragContext` now holds drag state, begins grab audio, clears/cancels drag sessions, and delegates drop execution to `InventoryTransferService`.
- `InventoryTransferService` executes drop, swap, relic-level preservation, same-relic merge index correction, rollback behavior, and minimal failure reason reporting, but remains colocated in `DragIcon.cs` as a temporary project-file-safe helper.
- Player and chest container adapter implementations now live under the gameplay inventory runtime layer instead of UI-adjacent files. `PlayerInventoryPanelView.cs` and `ChestScreen.cs` still instantiate the same adapter types through source-compatible constructor calls.

The issue is not that the UI does not follow strict MVP. The problem is that view-facing components know rules for where items should move and how transfer failures should be handled.

## Why It Exists

The current structure is practical for a small inventory UI because slot input, drag visuals, and item movement were implemented together. As chest, world drop, equipment, consumable, weapon, and relic behavior grew, the same UI layer became the easiest place to add transfer-specific branches.

## Target Shape

- `ItemSlotUI` owns slot visuals and converts pointer events into requests.
- `InventoryQuickMoveService` or an equivalent `QuickMoveResolver` owns right-click target selection for chest, world loot, equipment, and player inventory containers, then returns transfer/warning results to UI.
- `ItemDragContext` stores only drag-session state such as source container, source index, dragged item, and relic level.
- `InventoryTransferService` or an equivalent coordinator owns drop, swap, relic merge, relic-level preservation, rollback, and result/failure reason generation.
- Container adapters for player/chest/world/equipment are moved toward a runtime or adapter layer that is not owned by individual UI views.
- UI receives transfer results and displays warnings through a narrow interaction helper; it does not decide gameplay failure reasons itself.

## Risks

- Serialized UI references and prefab-authored slot roots must be preserved.
- Chest, world drop, equipment, consumable, weapon, and relic transfer rules must remain behavior-compatible.
- Relic level preservation and same-relic merge behavior must be covered before changing transfer execution.
- Warning popup codes must still map to the correct failure reasons.
- Any future code change may touch MonoBehaviours and prefab-facing UI scripts, so scene/prefab reference risk must be reviewed before implementation.

## Refactor Trigger

Start this split when one of the following happens:

- A new inventory container or item category is added.
- Chest, world drop, or equipment transfer rules need behavior changes.
- Inventory/chest UI is being reorganized or moved physically.
- Relic merge, relic level preservation, or transfer failure UX changes.

## Related Documents

- `Docs/StructureMemory/ScriptSystems/InventoryAndChestUIStructure.md`
- `Docs/StructureMemory/UIFlowInputBlocking.md`
- `Docs/Contracts/display-presentation-rules.md`
- `Assets/HeoMinSeok/_Project/Scripts/UI/Inventory/Common/ItemSlotUI.cs`
- `Assets/HeoMinSeok/_Project/Scripts/UI/Inventory/Common/DragIcon.cs`

## Partial Progress

2026-05-15:

- Moved right-click quick-move target resolution out of `ItemSlotUI` into `InventoryQuickMoveService`.
- Kept quick-move behavior source-compatible by reusing `ItemContainerGroupRegistry` and `ItemDragContext.TryDrop(...)`.
- Moved drag/drop/swap/relic merge/relic-level preservation/rollback execution out of `ItemDragContext` into `InventoryTransferService`.
- Kept `ItemDragContext.TryDrop(...)` as the source-compatible wrapper used by `ItemSlotUI.OnDrop(...)` and `InventoryQuickMoveService`.
- Added minimal `InventoryTransferFailureReason` and warning-code fields to `InventoryTransferResult`.
- Added a result-returning `ItemDragContext.TryDropWithResult(...)` path so `InventoryQuickMoveService` can preserve existing warning behavior while carrying transfer failure details.
- Kept `InventoryQuickMoveService` and `InventoryTransferService` in `DragIcon.cs` to avoid adding a C# file that static MSBuild would miss before Unity regenerates `Assembly-CSharp.csproj`.
- Prepared a manual verification checklist for the current transfer refactor; runtime play verification is still pending.
- Moved player/chest container adapters out of `PlayerInventoryPanelView` and `ChestScreen` nested MonoBehaviour scopes into same-file, file-scope `internal sealed` helper types.
- Moved `InventoryTransferService`, `InventoryQuickMoveService`, and player/chest container adapters into dedicated `.cs` helper files during the P1 helper file split.
- Added `InventorySlotTransferInteractionService` in `InventoryQuickMoveService.cs`.
- Moved quick-move warning presentation and quick-move/drop refresh handoff out of `ItemSlotUI`.
- Added shared `InventoryDeliveryWarningResolver` in `InventoryTransferService.cs` and routed quick-move, player relic adapter, and world pickup overlapping warning-code mapping through it.
- Moved `PlayerInventoryContainerAdapters.cs`, `ChestContainerAdapter.cs`, and their `.meta` files into `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Inventory/Runtime/`.
- Fixed the post-review world-loot relic quick-move edge case where an existing player relic merge could be blocked by source swap validation when the world source container could only accept `null`.
- Did not introduce dedicated namespaces, asmdefs, serialized-field changes, or prefab/scene changes.

## Status

`resolved`

Quick-move policy extraction, transfer execution extraction, minimal transfer result reporting, container-adapter boundary split, physical helper file split, slot-level warning/refresh handoff extraction, shared delivery warning-code mapping, and gameplay-runtime adapter file ownership are implemented. The current player, chest, world loot, and equipment-facing adapters are no longer owned by individual UI view files. No prefab, scene, namespace, asmdef, serialized field, `MonoBehaviour` serialized contract, or `ScriptableObject` schema changes have been made for this backlog item. Future transfer contract extraction, if needed, should be scoped as a new focused backlog item rather than reopening this completed P1 slice by default.
