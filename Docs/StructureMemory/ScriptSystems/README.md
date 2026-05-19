---
status: active
authority: structure-memory
category: system-map-index
last_reviewed: 2026-05-19
---

# Script Systems

## Purpose

Index focused structure maps for project-owned C# script areas.

These documents are working memory for context reconstruction. They are not source-of-truth architecture or contract documents.

## Documents

| Document | Covers |
| --- | --- |
| [Weapon And GAS Structure](./WeaponAndGASStructure.md) | Weapons, GAS/Abilities, combat, status, movement/player-adjacent runtime. |
| [Boss And Mob Encounter Structure](./BossAndMobEncounterStructure.md) | Boss encounter/battle/battle-end flow, mob population/runtime/death flow, room/chest lock overlays, hazards/puddles, enemy shared cleanup. |
| [Inventory And Chest UI Structure](./InventoryAndChestUIStructure.md) | Inventory/chest UI, HUD, inventory runtime, world drops, interaction, item details. |
| [Dialogue NPC Affection Structure](./DialogueNpcAffectionStructure.md) | Dialogue, NPC features, affection, merchant, upgrade, boss dialogue. |
| [Run Special NPC Structure](./RunSpecialNpcStructure.md) | Run-internal speech-bubble NPC flows, construction/permanent shortcut NPCs, same-scene teleport NPCs. |
| [Scene Runtime Save Structure](./SceneRuntimeSaveStructure.md) | Scene/run transition, player runtime capture/restore, save data, run timer, map/shortcuts. |
| [Loot Reward Structure](./LootRewardStructure.md) | Loot manager, boss rewards, grave/stage loot, pickups, reward presentation boundary. |
| [Loading Presentation Structure](./LoadingPresentationStructure.md) | Loading, presentation runtime, global UI, camera/audio/input/settings/speech bubbles. |
| [Level Design Editor Tool Structure](./LevelDesignEditorToolStructure.md) | Editor-only level-design validation, SceneView overlay, door/shortcut linking, battle-room authoring, placement helpers. |

## Use Rules

- Read [Script System Map](../ScriptSystemMap.md) first when choosing a system.
- Use these documents to identify likely files, ownership boundaries, lifecycle risks, and follow-up documents.
- Do not treat these maps as permission to move files or rename symbols.
- Promote only stable rules to `Docs/Architecture/` or `Docs/Contracts/` after explicit approval.
