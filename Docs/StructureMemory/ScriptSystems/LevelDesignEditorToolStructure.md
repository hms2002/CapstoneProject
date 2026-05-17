---
status: active
authority: structure-memory
category: editor-tool-structure
last_reviewed: 2026-05-17
---

# Level Design Editor Tool Structure

## Purpose

Map the editor-only level-design tool used to inspect and author corridor, battle-room, shortcut, door, chest, monster-spawn, and portal scene wiring.

This document is project memory only. Runtime contracts remain in `Docs/Architecture/` and `Docs/Contracts/`.

## Current Structure

- `Assets/LeeJunMo/Script/Editor/LevelDesignEditorWindow.cs` owns the `Tools/Level Design/Level Design Editor` window and SceneView overlay.
- The tool works against active-scene or loaded-scene objects without introducing runtime scripts or serialized schema changes.
- It draws an editor grid, labels, link lines, selected-room bounds, and problem markers directly in `SceneView`.
- It groups workflows into Review, Link, Rooms, Place, and Options modes.
- SceneView marker dots are actionable buttons. Clicking them selects the object and navigates to the relevant workflow.
- Marker navigation falls back to Unity SceneView object picking, then resolves known parent level-design components.
- Validation scans existing runtime components:
  - `DoorObject`
  - `ShortcutBase`, `LeverShortcut`, `StatueShortcut`
  - `MonsterSpawnRoomGroup`, `MonsterRoomArea2D`, `MonsterSpawnContainer`
  - `TreasureChest`, `ChestMonsterKillLock`, `RoomDoorMonsterKillLock`
  - `ScenePortal`

## Authoring Flow

- Review mode reports duplicate/missing door IDs, missing shortcut targets, invalid shortcut-door type combinations, room/spawn/chest lock gaps, door kill-lock gaps, and portal ID/config issues.
- Link mode supports click-and-click Shortcut to Door wiring through `SerializedObject` and `Undo`.
- Marker linking is the expected fast path: click a Shortcut marker, then click a Door marker to wire `ShortcutBase.targetDoor`.
- Chest/Monster marker linking is supported in both directions:
  - `ChestMonsterKillLock` marker then `MonsterSpawnContainer` marker assigns `MonsterSpawnContainer.linkedChestKillLock`.
  - `MonsterSpawnContainer` marker then `ChestMonsterKillLock` marker assigns the same field.
- Room/Door marker linking is supported in both directions:
  - `MonsterSpawnRoomGroup` marker then `DoorObject` marker creates or updates `RoomDoorMonsterKillLock`.
  - `DoorObject` marker then `MonsterSpawnRoomGroup` marker resolves the same lock wiring.
- Completed marker links must clear all pending link-source fields. `Esc` also cancels the active link from both the editor window and SceneView.
- If the click lands on the rendered object instead of the marker, the same navigation path is attempted through `HandleUtility.PickGameObject`.
- Rooms mode can create a battle-room object with `MonsterSpawnRoomGroup`, `BoxCollider2D`, `MonsterRoomArea2D`, and `RoomEncounterEntryTrigger2D`, then assign nearby spawns and locks.
- Place mode uses an object placement palette for Door, Lever, Statue, Chest, KillLock Chest, Portal, and Monster Spawn.
- Object palette cards can be clicked to select a placement type or dragged into SceneView to place through the same Undo-backed placement path.
- The monster palette is grouped by prefab folder with foldouts.
- Options mode edits selected door, shortcut, statue, spawn, room-door lock, chest lock, and portal serialized fields from one panel.
- The top context panel mirrors the selected marker/object and exposes common next actions without requiring manual tab hunting.

## Key Ownership Rules

- The tool must remain editor-only under `Assets/LeeJunMo/Script/Editor/`.
- Scene changes must be explicit, Undo-backed, and use serialized APIs where possible.
- The tool may call existing editor sync helpers such as `DoorObject.EditorSyncConfigurationFromLinkedShortcuts()`.
- It must not rename serialized fields, add runtime managers, or change runtime ScriptableObject schemas.
- It should prefer active scene context and avoid mutating prefab assets unless a future task explicitly requests prefab editing.

## Extension Entry Points

- Add new validators through the Review-mode scan path before adding new placement behavior.
- Add new placement types by reusing the existing prefab field and `PlacePrefab`/Undo pattern.
- Add new palette placement types through the object placement palette item list and `CreatePlacementAt(...)`.
- Add new selected-object editors through Options mode with serialized property fields.
- Wall leak detection is intentionally not implemented yet; add it only if the tilemap/collider ownership model is clear enough to avoid noisy false positives.

## Known Pitfalls

- The generated Unity `.csproj` may not include new editor files until the Unity Editor refreshes project files. In that case, use direct source compilation/static checks and report that Unity import/compile was not observed.
- `ShortcutBase` components are often children of `DoorObject` hierarchies, so SceneView picking needs to prefer the current link target type after a link source is selected.
- Door kill-lock markers can overlap door markers. When a Shortcut link source is active, clicking a door-lock marker should resolve the lock's target Door and complete the link if possible.
- Door clicks now start a pending Door-to-Room link when no other link source is active; use the context panel's property action when the intent is only inspection.
- Spawn clicks now start a pending Spawn-to-ChestLock link when no chest link source is active; use the context panel's property action when the intent is only inspection.
- `DoorObject.GenerateID()` can repair duplicates, but changing existing door IDs may break saved shortcut data that references the old IDs.
- Room-based chest and door kill-lock linking is inferred from room bounds, so designers still need to inspect final scene wiring.

## Promotion Candidate

Not yet a promotion candidate. Keep this as StructureMemory until the workflow stabilizes through real scene-authoring use.
