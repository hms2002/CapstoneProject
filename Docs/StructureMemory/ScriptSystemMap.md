---
status: active
authority: structure-memory
category: system-map
last_reviewed: 2026-05-14
---

# Script System Map

## Purpose

Provide the top-level index for project-owned C# scripts before any code, folder, namespace, asmdef, scene, prefab, or serialized-field reorganization.

This is a fast context map. It is not a source-of-truth architecture document, and it should not override `Docs/Architecture/` or `Docs/Contracts/`.

## Current Inventory

The current inventory was checked with `rg --files Assets -g '*.cs'`.

| Boundary | Count | Notes |
| --- | ---: | --- |
| Total C# files under `Assets/` | 1070 | Includes project, editor, test, and vendor scripts. |
| Project-owned scripts excluding `Assets/Ink` and `Assets/Plugins` | 936 | Main review surface for future structure work. |
| External/vendor scripts | 134 | `Assets/Ink` and `Assets/Plugins`; keep out of project refactor plans unless upgrading the dependency. |
| Editor scripts | 60 | Includes project editor tooling and vendor editor scripts. |
| Test scripts | 2 | PlayMode smoke/diagnostic tests. |

## First-Pass Clusters

| Cluster | Count | Detailed map |
| --- | ---: | --- |
| Gameplay Core | 301 | [Weapon And GAS Structure](./ScriptSystems/WeaponAndGASStructure.md), [Loading Presentation Structure](./ScriptSystems/LoadingPresentationStructure.md) for presentation-adjacent runtime. |
| Progression / Content | 228 | [Dialogue NPC Affection Structure](./ScriptSystems/DialogueNpcAffectionStructure.md), [Scene Runtime Save Structure](./ScriptSystems/SceneRuntimeSaveStructure.md), [Loot Reward Structure](./ScriptSystems/LootRewardStructure.md), [Inventory And Chest UI Structure](./ScriptSystems/InventoryAndChestUIStructure.md). |
| Enemy / Encounter | 185 | [Boss And Mob Encounter Structure](./ScriptSystems/BossAndMobEncounterStructure.md); separates boss encounter/battle/battle-end from mob population/runtime/death and lock overlays. |
| UI / Presentation | 179 | [Inventory And Chest UI Structure](./ScriptSystems/InventoryAndChestUIStructure.md), [Loading Presentation Structure](./ScriptSystems/LoadingPresentationStructure.md). |
| Editor Tools | 40 | Boundary group; do not treat as runtime structure unless editor tooling is the task. |
| Tests | 2 | Boundary group for PlayMode smoke and diagnostic tests. |
| Prototype / Legacy | 1 | Boundary group for legacy/prototype scripts outside the main ownership roots. |

## Detailed Structure Documents

Use these documents instead of expanding this index further.

| Document | Covers | Start here when |
| --- | --- | --- |
| [ScriptSystems/README.md](./ScriptSystems/README.md) | Index for focused script-system maps. | You need to choose the right detailed map. |
| [WeaponAndGASStructure.md](./ScriptSystems/WeaponAndGASStructure.md) | Weapons, GAS/Abilities, combat, status, movement/player-adjacent runtime. | Work touches weapon execution, runtime data, ability effects/cues/tags, damage flow, or weapon UI projection. |
| [BossAndMobEncounterStructure.md](./ScriptSystems/BossAndMobEncounterStructure.md) | Boss encounter/battle/battle-end flow, mob population/runtime/death flow, room/chest lock overlays, hazards/puddles, enemy shared cleanup. | Work touches bosses, mobs, FSM/pattern runners, spawn population, lock overlays, hazards, or enemy cleanup. |
| [InventoryAndChestUIStructure.md](./ScriptSystems/InventoryAndChestUIStructure.md) | Inventory/chest UI, HUD, inventory runtime, world drops, interaction, item details. | Work touches inventory screens, chest reveal, item detail/tooltip, HUD inventory entry points, or world item pickup/drop. |
| [DialogueNpcAffectionStructure.md](./ScriptSystems/DialogueNpcAffectionStructure.md) | Dialogue, NPC features, affection, merchant, upgrade, boss dialogue. | Work touches Ink dialogue, NPC feature popups, affection rewards, merchant/upgrade policies, or dialogue blockers. |
| [SceneRuntimeSaveStructure.md](./ScriptSystems/SceneRuntimeSaveStructure.md) | Scene/run transition, player runtime capture/restore, save data, run timer, map/shortcuts. | Work touches scene transitions, portals, runtime persistence, run progress, save data, or shortcut/map progression. |
| [LootRewardStructure.md](./ScriptSystems/LootRewardStructure.md) | Loot manager, boss rewards, grave/stage loot, pickups, reward presentation boundary. | Work touches reward generation, loot tables, boss reward modifiers, currency, or pickup spawning. |
| [LoadingPresentationStructure.md](./ScriptSystems/LoadingPresentationStructure.md) | Loading, presentation runtime, global UI, camera/audio/input/settings/speech bubbles. | Work touches loading scopes, asset providers, presentation services, global UI, camera/audio presentation, or settings/input binding UI. |
| [LevelDesignEditorToolStructure.md](./ScriptSystems/LevelDesignEditorToolStructure.md) | Editor-only level-design validation, SceneView overlay, door/shortcut linking, battle-room authoring, placement helpers. | Work touches level-design authoring tools, map object validation, room/spawn/lock wiring helpers, or editor-only scene placement workflows. |

## Boundaries

| Boundary | Count | Rule |
| --- | ---: | --- |
| External / Ink | 118 | Vendor dependency. Exclude from project runtime reorganization unless upgrading/integrating Ink. |
| External / Plugins | 16 | DOTween plugin. Exclude from project runtime reorganization unless upgrading/integrating the plugin. |
| Editor Tools | 40 | Keep separate from runtime maps unless the task targets authoring tools. |
| Tests | 2 | Keep separate from runtime maps unless the task targets test coverage. |
| Prototype / Legacy | 1 | Treat as reference/legacy until a task explicitly migrates it. |

## Ownership And Risk

- `MonoBehaviour` scripts are scene, prefab, or runtime component owned; do not move or rename them without serialized reference review.
- `ScriptableObject` scripts define authored data and schemas; schema changes need asset migration risk review.
- Interfaces and asmdef boundaries are refactor-sensitive because they can affect compile dependencies and implementation contracts.
- UI and presentation scripts should project runtime state through serialized references where possible, following `Docs/Contracts/PresentationAuthoringContract.md`.
- Runtime state ownership should stay explicit: player runtime state, weapon runtime data, relic runtime state, save/run data, and UI stack state should not be merged casually.

## Extension Entry Points

- Add or update focused maps under `Docs/StructureMemory/ScriptSystems/` when a cluster needs repeated work or a multi-file flow map.
- Promote stable rules to `Docs/Architecture/` or `Docs/Contracts/` only after explicit approval.
- Create `RefactorBacklog` entries only when a concrete structural debt has a current problem, target shape, risk, and trigger.
- Before a physical folder reorganization, split the plan by Unity risk class: plain C#, interface, MonoBehaviour, ScriptableObject, asmdef, prefab/scene-facing asset.

## Promotion Candidate

Not yet a promotion candidate. This index and the focused maps should remain in `StructureMemory` until stable boundaries prove which rules should be promoted to Architecture or Contracts.
