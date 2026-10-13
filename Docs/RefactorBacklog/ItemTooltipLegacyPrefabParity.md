---
status: proposed
authority: refactor-backlog
category: refactor-item
last_reviewed: 2026-10-10
---

# Item Tooltip Legacy Prefab Parity

## Current Problem

The main GlobalUIRoot authors the accepted description guide, stat/skill spacing and keyword-card style. Five independent roots (`GlobalUIRoot_Sub`, `_Water`, `_Salryojo`, `_Deafiso`, `_DialogueUpdate`) also reference the shared `Panel_AbilityBlock` template. They inherit the new skill frame but retain their own old local card spacing, so neighboring outlines may meet. These copies were outside the approved main-tooltip change and have not received visual acceptance checks.

## Why It Exists

The older roots duplicate the item panel hierarchy while sharing only the individual skill template. Updating the main panel does not propagate its guide placement, keyword cards or spacing to those independent copies.

## Target Shape And Risks

When a legacy copy is actually needed, choose an approved migration to the main authored panel or explicitly synchronize its presentation values and validate the result. Avoid a runtime border/layout repair path. Check scene/bootstrap references, nested prefab overrides, guide references, fonts, raycasts and hover bounds before changing a copy; migration may require separate scene/prefab approval.

## Refactor Trigger And Status

Status: `proposed`. Trigger: a maintained scene/build path references one of these copies, or the user explicitly requests tooltip parity. This entry does not authorize editing them during the current main-tooltip slice.

## Related Documents

- [Inventory and Chest UI structure](../StructureMemory/ScriptSystems/InventoryAndChestUIStructure.md)
- [Current implementation session](../SessionLogs/2026-10-10.md)
- [Presentation authoring contract](../Contracts/PresentationAuthoringContract.md)
