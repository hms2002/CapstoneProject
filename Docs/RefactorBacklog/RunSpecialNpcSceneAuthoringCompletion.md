---
status: partially-refactored
authority: refactor-backlog
category: runtime-gameplay
last_reviewed: 2026-05-20
---

# Run Special NPC Scene Authoring Completion

## Current Problem

`SlimeCorridor` has a placed `ConstructionNpc` and now has a minimal `ConstructionSiteTilemapModule` validation object, but the final construction shortcut authoring is still incomplete:

- `RunConstructionNpcFeature.constructionSiteModule` points at `ConstructionSite_Test_01`.
- `ConstructionSite_Test_01/BlockedState` owns the existing `BlockCollidor` object for the blocked state.
- `ConstructionSite_Test_01/OpenState` is currently an empty inactive root.
- The authored `RunSpecialNpcChoicePresenter` now exists under `GlobalUIRoot > DialogueCanvas` and is wired to the SlimeCorridor NPC, but it still needs Unity Editor visual/play validation.
- No final open-ground tilemap, Door/Shortcut anchor, or Chest is wired for this site yet.
- `targetDoor` is intentionally unset and Door/Shortcut saving is disabled on the test module, so the current validation only checks blocked/open root switching.

## Why It Exists

The NPC was already placed in the scene before the full construction-site object hierarchy existed. The current slice adds a minimal module around the existing blocker so completion behavior can be tested before the final map block, Door/Shortcut, and reward authoring are finalized.

## Target Shape

`SlimeCorridor` construction authoring should move to the documented block structure:

```txt
ConstructionSite_<id>
  ConstructionSiteTilemapModule
  BlockedState
    TemporaryWallTilemap / collider
  OpenState
    GroundTilemap
    optional WallTilemap
    DoorObject or Shortcut anchor
    optional Chest
```

The NPC should reference the module. The speech-bubble choice presenter is now authored on `GlobalUIRoot`, and the NPC now references the test module, so remaining scene work should focus on the final construction block/Door/Shortcut/Chest authoring and visual validation.

## Risks

- If `BlockCollidor` does not cover the final shortcut boundary, completion may open only collision and not the intended map path.
- Because the current `OpenState` is empty, completion can prove blocker removal but cannot prove final path visuals, pathfinding over new ground, Door logic, or chest activation.
- Until final open ground tilemaps are authored under `OpenState`, additional open ground tilemap registration with `TilemapPathfinder2D` is not exercised by this site.
- Without a `DoorObject` or Shortcut anchor, durable shortcut state is limited to the construction completion record.
- The prefab-owned choice presenter is referenced from `SlimeCorridor` through a prefab-instance stripped reference, so Unity import/Inspector validation is required.
- The current two-button panel is intentionally minimal and may need visual styling once the final speech-bubble UI direction is reviewed in play.

## Refactor Trigger

Start this when the SlimeCorridor shortcut layout is finalized or when adding the next construction site NPC.

## Related Documents

- `Docs/StructureMemory/ScriptSystems/RunSpecialNpcStructure.md`
- `Docs/StructureMemory/ScriptSystems/SceneRuntimeSaveStructure.md`
- `Docs/DecisionLog.md`

## Status

`partially-refactored`
