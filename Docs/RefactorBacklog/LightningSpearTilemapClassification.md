# Lightning Spear Tilemap Classification

- Status: proposed
- Problem: mark placement recognizes the exact authored Tilemap name `Wall` as well as the Wall physics layer because prototype combat scenes put Wall tilemaps on Ground. Renaming those objects can break classification.
- Why retained: correcting the reported skill bug without changing scene layers, collision behavior or serialized authoring contracts.
- Target: resolve floor/wall roles from explicit map ownership references for authored and generated maps.
- Risks: broad layer migration affects physics; arbitrary sorting layers or name heuristics are not a substitute for map roles.
- Trigger: tilemap naming changes, map authoring unification, or further classification failures.
- Related: [SessionLog](../SessionLogs/2026-09-11.md), [ErrorLog](../ErrorLog.md), [runtime](../../Assets/_Project/Runtime/Features/Items/Weapons/Abilities/LightningSpearRuntimeState.cs).
- Current owner: [PlacementWorldQuery](../../Assets/_Project/Runtime/Features/Items/Weapons/Abilities/LightningSpearPlacementWorldQuery.cs) supplies the unchanged layer/name convention to common MapQuery2D; [structure map](../StructureMemory/LightningSpearPlacement.md). Functional tool composition does not resolve this debt or impose that convention on other consumers.
