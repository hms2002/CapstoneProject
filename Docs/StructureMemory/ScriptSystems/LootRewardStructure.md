---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-05-16
---

# Loot Reward Structure

## Purpose

Map loot, boss rewards, drops, pickups, currency, and reward presentation boundaries.

## Current Structure

| Area | Count | Responsibility |
| --- | ---: | --- |
| Loot / Rewards | 19 | Loot manager, stage/grave loot tables, roll/spawn/pool services, boss battle-end handler/modifiers, pickups and drops. |
| World Drops | 17 | World item drop model, pickup/drop landing visuals, item display visual presenters/profiles. |

### Loot / Rewards Breakdown

| Area | Count | Responsibility |
| --- | ---: | --- |
| Loot Services / Tables | 6 | Loot manager, resolver, roll/spawn/pool services, and stage loot table. |
| Boss Rewards | 4 | Scene battle-end handler, RouteSet special reward preset, authored chest/portal references, and runtime modifier aggregate. |
| Grave Loot | 3 | Grave spawner, interactable, and loot table. |
| Drop Position / Monster Drop | 2 | Ground tile drop position resolver and monster drop. |
| Item Database / Manager | 2 | Item database and manager. |
| Currency / Pickup | 2 | Currency manager and magic stone pickup. |
| Looting Other | 1 | Shared looting common definitions. |

## Key Files

- `Assets/LeeJunMo/Script/Looting/LootManager.cs`
- `Assets/LeeJunMo/Script/Looting/LootPoolService.cs`
- `Assets/LeeJunMo/Script/SceneManagement/BossBattleEndHandler.cs`
- `Assets/LeeJunMo/Script/Looting/BossRewardSpawnService.cs`
- `Assets/LeeJunMo/Script/Looting/BossSpecialRewardPresetSO.cs`
- `Assets/LeeJunMo/Script/Looting/BossRewardModifierAggregate.cs`
- `Assets/LeeJunMo/Script/Looting/StageLootTable.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Inventory/Chest/Runtime/TreasureChest.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/World/Runtime/DropItem/WorldItemPickup2D.cs`

## Ownership And Lifecycle

- Base boss chest rewards should come from the Boss Reward section of `StageLootTable` through `LootManager.GenerateBossChestLoot(...)`.
- Route sets may reference an optional boss special reward preset, but must not own common prefabs, portal objects, spawn positions, offsets, magic stone bonuses, field-heal bonuses, or chest-count deltas.
- A scene-authored `BossBattleEndHandler` references the boss plus authored inactive `TreasureChest` and `ScenePortal` objects; those objects own chest and portal positions through their transforms.
- Affection, upgrades, and future modifiers should contribute additive reward modifiers rather than replace base tables.
- Run reward modifiers are aggregated in the progression/run modifier layer, not under the Upgrade feature folder.
- World drop visuals/pickups should present and deliver item state, not own progression reward policy.

## Runtime Boundary Review

The reviewed issue is not primarily the boss reward split. Boss chest rewards come from `LootManager` and additive modifiers are carried by `BossRewardModifierAggregate`. Boss reward activation now flows through a scene `BossBattleEndHandler` and helper service, and the former `BossDrop` prefab-reference risk is resolved in `Docs/RefactorBacklog/BossDropResponsibilitySplit.md`.

The active boundary concern is the general loot/chest/world-pickup flow. Reward generation, current-world context, chest-specific modifiers, and final delivery are still coupled through runtime singletons and scene lookups.

| Boundary | Intended responsibility | Current pressure point |
| --- | --- | --- |
| Loot Roll Policy | Decide what reward categories and counts are rolled from loot tables. | `LootManager` remains the public facade. Chest loot generation, monster drop execution, and grave drop execution now delegate to helper files, while boss-facing helpers and public spawn wrappers remain on the manager. |
| Loot Pool Context | Describe which items should be excluded for this roll. | `LootPoolService` keeps the public facade, `LootPoolLiveWeaponExclusionSourceProvider` reads live player/world/scene/merchant sources into a snapshot, and `LootPoolWeaponExclusionProvider` combines that snapshot with `LootPoolContext`. Item selection reads go through `LootPoolItemSelectionService`. |
| Chest Reward Policy | Own chest-only modifiers such as refresh count, relic level bonus, and chest reward deltas. | `ChestRewardPolicy` is now a helper file for refresh eligibility, refresh guard snapshots/comparison, and relic level bonus calculation. It reads chest modifiers through `RunRewardModifierSnapshot`. |
| Reward Delivery | Deliver a chosen reward to an inventory, world object, currency store, or spawned pickup and return a success/failure result. | World pickup item delivery now goes through `WorldPickupDeliveryService`; overlapping inventory warning-code mapping is shared through `InventoryDeliveryWarningResolver`; currency is embedded in `CurrencyManager`/`MagicStonePickup`; spawned pickup delivery goes through `LootSpawnService`. |
| World Pickup Presentation | Present dropped items and forward interaction requests. | `WorldItemPickup2D` now keeps item state, highlight/detail presentation, warning display, failed-pickup speech, and success destruction while delegating grant attempts/failure-code mapping to the delivery helper. `FieldHealPickup2D` owns field-heal drop travel, idle heartbeat/floating, travel-time interaction lock, healing, and optional consume particle playback. |
| Boss Reward Activation | Initialize and activate an authored boss chest when applicable, spawn variable-count physical pickups, and activate an authored boss exit portal. | `CorridorBossRouteSetSO` can reference an optional `BossSpecialRewardPresetSO`; `StageLootTable` supplies boss weapon/relic count and boss relic rarity for chest loot plus magic stone/field-heal pickup counts; scene `BossBattleEndHandler` owns event subscription, boss matching, authored chest/portal references, final-route chest suppression, boss death-position drop origin, and handled marking; `BossRewardSpawnService` owns chest initialization/activation, bonus loot application, and boss physical pickup spawning from that origin. Final routes suppress only chest activation, not physical drops. |

### Field-Heal Pickup Presentation

- `Assets/Resources/PF_FieldHealPickup2D.prefab` wires `visualRoot`, drop arc values, idle float/heartbeat values, and `collectParticlePrefab`.
- Monster and boss field-heal drops enter through `LootSpawnService.SpawnFieldHealPickup(...)`, which chooses a bounded random landing offset before calling `FieldHealPickup2D.PlayDrop(...)`.
- `FieldHealPickup2D` locks interaction during drop travel, then resumes trigger collection after landing.

### Reviewed Responsibility Mix

- `LootManager` is the current public orchestration facade for monster loot, grave loot, field heal spawning, and boss base reward helpers. Chest generation has a `ChestLootRequest`/`ChestLootResult` path and delegates the chest roll loop to `ChestLootGenerationService`, while legacy List-returning APIs remain for compatibility.
- `MonsterLootDropService` owns current monster loot type roll execution and forwards chosen weapon/relic/consumable/field-heal drops to `LootSpawnService`.
- `GraveLootDropService` owns grave weapon/relic count rolls, relic rarity bonus rolls, duplicate weapon ban updates, and animated grave landing spawn execution.
- `BossBattleEndHandler` owns boss reward/portal event subscription, explicit boss matching, authored chest/portal references, and `BossRewardContext` handled flags. `BossRewardActivationRequest` and `BossRewardSpawnService` own boss chest loot generation, authored chest activation, bonus loot, StageLootTable/modifier-driven magic stone and field-heal physical drops, and exception-logged reward execution.
- `LootPoolService` builds weapon exclusion sets from a `LootPoolContext` that names source categories. Public compatibility APIs remain on the service. The default path uses `LootPoolLiveWeaponExclusionSourceProvider` to collect player inventory, world pickup, scene weapon drop, and merchant stock source snapshots, then `LootPoolWeaponExclusionProvider` performs pure context/source-set combination.
- `LootPoolItemSelectionService` owns the current `ItemManager.Instance` weapon/relic/consumable selection reads.
- `TreasureChest` owns world chest state, open presentation, inventory fill, refresh count state, and UI handoff. `ChestRewardPolicy` owns refresh eligibility, refresh guard comparison/snapshot, and relic level bonus calculations.
- `WorldItemPickup2D` owns world presentation, interaction forwarding, failure warning display, failed-pickup speech, and object destruction after pickup. `WorldPickupDeliveryService` owns weapon/relic/consumable grant attempts and uses `InventoryDeliveryWarningResolver` for overlapping relic/consumable warning-code mapping.

### Refactor Candidate

- Track the general loot boundary split in `Docs/RefactorBacklog/LootRewardPolicyBoundarySplit.md`.
- First implemented slice: chest loot generation now has `ChestLootRequest` and `ChestLootResult` helpers, and `TreasureChest` self-generation/refresh consumes the result-returning path without behavior changes.
- Second implemented slice: weapon exclusion source choices now flow through `LootPoolContext` presets; chest/monster rolls use player inventory, shop stock uses player/world/scene drops, and grave weapon rolls use player plus merchant stock.
- Third implemented slice: chest roll execution now lives in `ChestLootGenerationService`, and chest refresh/relic-level behavior now lives in `ChestRewardPolicy`/`ChestLootSnapshot` helpers.
- Fourth implemented slice: world pickup grant attempts and failure-code mapping now live in `WorldPickupDeliveryService`/request/result helpers while `WorldItemPickup2D` remains the presentation and interaction owner.
- Fifth implemented slice: chest, merchant, boss reward, loot fallback, and runtime debug consumers now read resolved run reward modifiers through `RunRewardModifierSnapshot` instead of directly reading individual `RunModifierService.Instance` modifier properties.
- Sixth implemented slice: `LootPoolService` now delegates source collection to `LootPoolWeaponExclusionProvider` and `ItemManager.Instance` reads to `LootPoolItemSelectionService`, while preserving the existing public loot pool APIs.
- Seventh implemented slice: `LootManager.SpawnMonsterLoot(...)` and `SpawnGraveLoot(...)` now delegate roll/drop execution to `MonsterLootDropService` and `GraveLootDropService`, while preserving current public spawn APIs and reward behavior.
- Eighth implemented slice: the former `BossRewardSpawner` path was replaced by scene `BossBattleEndHandler`; boss chest generation and exception-logged activation now live in `BossRewardSpawnService`.
- Ninth implemented slice: `InventoryDeliveryWarningResolver` now shares quick-move, player relic adapter, and world pickup relic/consumable warning-code mapping without changing delivery behavior.
- Tenth implemented slice: `LootPoolLiveWeaponExclusionSourceProvider` now owns live singleton/scene reads for weapon exclusion sources, while `LootPoolWeaponExclusionProvider` combines injected source snapshots with `LootPoolContext`.
- Eleventh implemented slice: route-linked `BossSpecialRewardPresetSO` remains the supported authoring path for boss-specific special loot candidates after `BossDrop` deletion. Chest/portal placement moved to authored inactive scene objects.
- Twelfth implemented slice: `StageLootTable` now owns boss base weapon count, relic count, Common/Rare/Epic relic rarity weights, magic stone count, and field heal count; `LootManager.GenerateBossChestLoot(...)` uses that boss-specific path while normal chests keep the normal chest profiles.
- Thirteenth implemented slice: boss battle-end `TreasureChest` and `ScenePortal` objects are no longer instantiated at runtime. Scene `BossBattleEndHandler` initializes/activates an authored chest and activates an authored portal object. Boss magic stones and field heals remain runtime physical pickups spawned from the boss death position. `BossRewardSpawner`, `BossExitPortalActivator`, `BossBattleEndAnchors`, and `BossBattleEndPrefabCatalogSO` were deleted.
- Keep future boss battle-end migration notes under the resolved `Docs/RefactorBacklog/BossDropResponsibilitySplit.md`.
- Connect `WorldItemPickup2D` delivery cleanup to `Docs/RefactorBacklog/InventoryTransferResponsibilitySplit.md` when item transfer and pickup delivery are implemented.

## Extension Entry Points

- Add reward table changes through Loot Services / Tables.
- Add boss base reward changes through the StageLootTable Boss Reward section.
- Add boss-specific special loot candidates through RouteSet-linked `BossSpecialRewardPresetSO`.
- Add runtime boss reward increases through `BossRewardModifierAggregate`.
- Add pickup/drop presentation through World Drops rather than loot policy services.

## Known Pitfalls

- RouteSet special reward presets, scene `BossBattleEndHandler`, and authored chest/portal references must be authored and manually verified because the former `BossDrop` field fallback and split components are deleted. Final-route boss scenes may intentionally omit `BossBattleEndHandler.treasureChest`; non-final boss scenes still require an authored inactive chest.
- Reward presentation and reward generation are separate responsibilities.
- Boss base weapon count, relic count, relic rarity, magic stone count, and field-heal pickup count belong in `StageLootTable`; base boss-specific special loot candidates belong in `BossSpecialRewardPresetSO`; runtime Affection, upgrade, and future effects should project direct effect fields into `BossRewardModifierAggregate` instead of mutating assets.
- New reward sources should define whether their loot pool context excludes player inventory, world pickups, scene weapon drops, merchant stock, or none of these.
- Chest reward changes should check `LootManager`, `ChestLootGenerationService`, `TreasureChest`, `ChestRewardPolicy`, and `RunRewardModifierSnapshot` from the progression/run modifier layer.
- Monster and grave drop changes should check `MonsterLootDropService`, `GraveLootDropService`, `LootRollService`, `LootSpawnService`, and `LootPoolService` together because roll choice, duplicate prevention, and world spawn execution are split across those helpers.
- Boss reward activation changes should check `CorridorBossRouteSetSO`, `BossSpecialRewardPresetSO`, scene `BossBattleEndHandler`, authored `TreasureChest`/`ScenePortal` wiring, `BossRewardSpawnService`, `RunProgressCoordinator`, `BossRewardFallbackService`, and `RunRewardModifierSnapshot` so preset lookup, reward-handled dedupe, fallback warnings, and additive modifier semantics stay aligned.
- Final-route boss contexts intentionally mark rewards handled without chest activation, but boss physical drops still spawn; do not reintroduce a final-boss chest fallback unless the route reward policy changes.
- Loot pool exclusion changes should check `LootPoolContext`, `LootPoolLiveWeaponExclusionSourceProvider`, `LootPoolWeaponExclusionProvider`, `LootPoolItemSelectionService`, and current callers in `LootManager`/merchant code so source-specific duplicate-prevention rules stay intentional.
- World pickup delivery changes should still be reviewed with inventory transfer policy because both paths decide inventory grant failures. Warning-code mapping for overlapping cases is now shared, but world pickup delivery is not a full shared delivery layer.

## Promotion Candidate

The additive boss reward decision is already recorded in `Docs/DecisionLog.md`. Keep detailed file topology here until a stable Architecture update is approved.
