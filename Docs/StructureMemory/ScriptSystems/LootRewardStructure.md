---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-11-03
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
- `Assets/_Project/Runtime/Features/Bosses/Common/FSM/Core/BossEncounterEndDirector.cs`
- `Assets/_Project/Runtime/Features/Bosses/Common/Rewards/BossBattleEndHandler.cs`
- `Assets/_Project/Runtime/Features/Loot/BossRewardSpawnService.cs`
- `Assets/LeeJunMo/Script/Looting/BossSpecialRewardPresetSO.cs`
- `Assets/LeeJunMo/Script/Looting/BossRewardModifierAggregate.cs`
- `Assets/LeeJunMo/Script/Looting/StageLootTable.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Inventory/Chest/Runtime/TreasureChest.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/World/Runtime/DropItem/WorldItemPickup2D.cs`

## Ownership And Lifecycle

- Base boss chest rewards should come from the Boss Reward section of `StageLootTable` through `LootManager.GenerateBossChestLoot(...)`.
- Route sets may reference an optional boss special reward preset, but must not own common prefabs, portal objects, spawn positions, offsets, magic stone bonuses, field-heal bonuses, or chest-count deltas.
- Current boss scenes use `BossEncounterEndDirector` with a clear condition and authored inactive chest/exit objects. `BossBattleEndHandler` remains the event-driven compatibility path. Authored transforms own chest and portal positions.
- Affection, upgrades, and future modifiers should contribute additive reward modifiers rather than replace base tables.
- Run reward modifiers are aggregated in the progression/run modifier layer, not under the Upgrade feature folder.
- World drop visuals/pickups should present and deliver item state, not own progression reward policy.

## Runtime Boundary Review

The reviewed issue is not primarily the boss reward split. Boss chest rewards come from `LootManager` and additive modifiers are carried by `BossRewardModifierAggregate`. Current encounter rewards flow through `BossEncounterEndDirector` and `BossRewardSpawnService`; `BossBattleEndHandler` retains the event-driven path. The former `BossDrop` prefab-reference risk is tracked in `Docs/RefactorBacklog/BossDropResponsibilitySplit.md`.

The active boundary concern is the general loot/chest/world-pickup flow. Reward generation, current-world context, chest-specific modifiers, and final delivery are still coupled through runtime singletons and scene lookups.

| Boundary | Intended responsibility | Current pressure point |
| --- | --- | --- |
| Loot Roll Policy | Decide what reward categories and counts are rolled from loot tables. | `LootManager` remains the public facade. Chest loot generation, monster drop execution, and grave drop execution now delegate to helper files, while boss-facing helpers and public spawn wrappers remain on the manager. |
| Loot Pool Context | Describe which items should be excluded for this roll. | `LootPoolService` keeps the public facade, `LootPoolLiveWeaponExclusionSourceProvider` reads live player/world/scene/merchant sources into a snapshot, and `LootPoolWeaponExclusionProvider` combines that snapshot with `LootPoolContext`. Item selection reads go through `LootPoolItemSelectionService`. |
| Chest Reward Policy | Own chest-only modifiers such as refresh count, relic level bonus, and chest reward deltas. | `ChestRewardPolicy` is now a helper file for refresh eligibility, refresh guard snapshots/comparison, and relic level bonus calculation. It reads chest modifiers through `RunRewardModifierSnapshot`. |
| Reward Delivery | Deliver a chosen reward to an inventory, world object, currency store, or spawned pickup and return a success/failure result. | World pickup item delivery now goes through `WorldPickupDeliveryService`; overlapping inventory warning-code mapping is shared through `InventoryDeliveryWarningResolver`; currency is embedded in `CurrencyManager`/`MagicStonePickup`; spawned pickup delivery goes through `LootSpawnService`. |
| World Pickup Presentation | Present dropped items and forward interaction requests. | `WorldItemPickup2D` keeps item state, highlight/detail presentation, warning display, failed-pickup speech, and success destruction while delegating grant attempts/failure-code mapping to the delivery helper. Shared `WorldItemHoverPlayback` owns Skill1/Skill2 press priority while any world item detail request is active. `FieldHealPickup2D` owns field-heal drop travel, idle heartbeat/floating, travel-time interaction lock, healing, optional consume particle playback, and player-attached heal particle playback when HP actually increases. |
| Boss Reward Activation | Start normal rewards together after the defeat presentation; grant final-route magic stones before the ending. | `BossEncounterEndDirector` owns clear-condition completion and same-frame gold/EXP spawn, chest activation, magic stone/field-heal spawn, and portal reveal start. `BossRewardSpawnService` uses `StageLootTable` and additive modifiers for normal loot/drops and final-route direct magic stone delivery. Final routes omit gold, EXP, chest, field heals, and portal. `BossBattleEndHandler` keeps the same final reward policy on its event-driven path. |

### Boss Completion Timing

- Normal boss rewards start in one synchronous block after the death presentation, camera return, and any custom finale finish. The block does not wait for pickup collection, chest opening, or portal reveal completion.
- Slime Queen's two phase-two deaths are its encounter clear condition. Its completed encounter follows the normal reward timing.
- Final-route Demon King uses `GrantFinalMagicStoneReward` before awaiting the terminal ending. It adds `max(0, current table bossStoneCount + MagicStoneBonus)` through `CurrencyManager.AddMagicStone`, so an active run records the amount in pending run currency before the victory result reads it.
- Final magic stones are delivered directly, without a world pickup. Existing ending dialogue/outro/result behavior stays with `BossDeathPresentation` and `BossDefeatEndingSequence`.
- `hasCompleted` prevents repeating the director's completion routine; reward context flags and the compatibility handler's local flags preserve handled state. Missing loot/currency/save dependencies log a warning and do not mark the reward delivered.

### Field-Heal Pickup Presentation

- `Assets/Resources/PF_FieldHealPickup2D.prefab` wires `visualRoot`, drop arc values, idle float/heartbeat values, `collectParticlePrefab`, and the player-attached `healParticlePrefab`.
- Monster and boss field-heal drops enter through `LootSpawnService.SpawnFieldHealPickup(...)`, which chooses a bounded random landing offset before calling `FieldHealPickup2D.PlayDrop(...)`.
- `FieldHealPickup2D` locks interaction during drop travel, then resumes trigger collection after landing.
- `FieldHealPickup2D.heartbeatFrequency` owns only the heartbeat cadence, while `heartbeatScalePulseDuration` owns the grow/shrink pulse duration so beat period changes do not also change scale speed.
- `FieldHealPickup2D` keeps the collect particle at the pickup position, but spawns `HealParticle` as a child of the resolved `PlayerInteractor2D` root only when the `healthAttribute` current value increases.
- `FieldHealPickup2D` does not complete collection when the player is already at full HP; the pickup remains in the world until an actual HP increase can occur.

### Reviewed Responsibility Mix

- `LootManager` is the current public orchestration facade for monster loot, grave loot, field heal spawning, and boss base reward helpers. Chest generation has a `ChestLootRequest`/`ChestLootResult` path and delegates the chest roll loop to `ChestLootGenerationService`, while legacy List-returning APIs remain for compatibility.
- `MonsterLootDropService` owns current monster loot type roll execution and forwards chosen weapon/relic/consumable/field-heal drops to `LootSpawnService`.
- `GraveLootDropService` owns grave weapon/relic count rolls, relic rarity bonus rolls, duplicate weapon ban updates, and animated grave landing spawn execution.
- `BossBattleEndHandler` owns boss reward/portal event subscription, explicit boss matching, authored chest/portal references, and `BossRewardContext` handled flags. `BossRewardActivationRequest` and `BossRewardSpawnService` own boss chest loot generation, authored chest activation, bonus loot, StageLootTable/modifier-driven magic stone and field-heal physical drops, and exception-logged reward execution.
- `LootPoolService` builds weapon exclusion sets from a `LootPoolContext` that names source categories. Public compatibility APIs remain on the service. The default path uses `LootPoolLiveWeaponExclusionSourceProvider` to collect player inventory, world pickup, scene weapon drop, and merchant stock source snapshots, then `LootPoolWeaponExclusionProvider` performs pure context/source-set combination.
- `LootPoolItemSelectionService` owns the current `ItemManager.Instance` weapon/relic/consumable selection reads.
- `TreasureChest` owns world chest state, open presentation, inventory fill, refresh count state, and UI handoff. `ChestRewardPolicy` owns refresh eligibility, refresh guard comparison/snapshot, and relic level bonus calculations.
- `WorldItemPickup2D` owns world presentation, interaction forwarding, failure warning display, failed-pickup speech, and object destruction after pickup. `WeaponDrop2D` follows the same `WorldItemHoverPlayback` detail path for replaced weapons. The playback contract acquires both skill action-press blocks on show and releases them only for the matching anchor on hide, including disable cleanup. `WorldPickupDeliveryService` owns weapon/relic/consumable grant attempts and uses `InventoryDeliveryWarningResolver` for overlapping relic/consumable warning-code mapping.

### Refactor Candidate

- Track the general loot boundary split in `Docs/RefactorBacklog/LootRewardPolicyBoundarySplit.md`.
- First implemented slice: chest loot generation now has `ChestLootRequest` and `ChestLootResult` helpers, and `TreasureChest` self-generation/refresh consumes the result-returning path without behavior changes.
- Second implemented slice introduced source-specific `LootPoolContext` presets. The current chest/shop presets are described in the 2026-11-01 update below; monster and grave contexts retain their earlier behavior.
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
- Final-route boss contexts grant only magic stones and mark the omitted portal handled. The grant precedes terminal ending/result capture; placing it after the presentation can miss it because the terminal branch exits the coroutine. Normal-boss pickup collection timing remains owned by the pickup components.
- Loot pool exclusion changes should check `LootPoolContext`, `LootPoolLiveWeaponExclusionSourceProvider`, `LootPoolWeaponExclusionProvider`, `LootPoolItemSelectionService`, and current callers in `LootManager`/merchant code so source-specific duplicate-prevention rules stay intentional.
- World pickup delivery changes should still be reviewed with inventory transfer policy because both paths decide inventory grant failures. Warning-code mapping for overlapping cases is now shared, but world pickup delivery is not a full shared delivery layer.

## Promotion Candidate

The additive boss reward decision is already recorded in `Docs/DecisionLog.md`. Keep detailed file topology here until a stable Architecture update is approved.

## Heart Wall Reflection (2026-09-12)

- FieldHealPickup2D sweeps its real pickup collider against non-trigger Wall colliders during planar drop motion. A hit reflects velocity at equal magnitude and consumes the remaining travel distance in that direction; initial/final overlap correction keeps the landing collider outside the wall.
- The parabolic arc and idle float belong to the authored visual child, not the pickup root. A root-only visual skips positional presentation so collision corrections cannot be overwritten. GroundPosition follows the actual reflected position.
- Ownership remains in the pickup drop coroutine; no Rigidbody or physics material migration is required. The bounded per-frame bounce/overlap passes prevent unbounded corner loops.

### Full-health heart contact (2026-09-18)

- `FieldHealPickup2D.TryCollect` checks the health attribute's runtime maximum (including its dynamic max getter). A full-health `PickupCollector2D` body pushes the landed heart out of overlap instead of attempting healing; no warning text, collect sound, or heal effect is emitted. Missing health data does not activate pushing. Injured players retain normal collection.
- Pushes are small, bounded displacement steps owned by the heart, with no new Rigidbody or prefab fields. Collider casts stop before solid `Wall` and `HoleTrap` colliders, including pit triggers; a saturated query stops movement. Ground tilemaps are cached on first contact and checked for active `Ground` tiles under the pickup footprint at each step. Unsupported ground stops movement, including maps without a verifiable floor. The footprint check is conservative near corners.
- Drop travel locks and pause prevent pushing. The existing airborne drop reflection and idle child visuals are unchanged. Successful displacement publishes `WorldStateChanged` so map content tracking observes the new position. Manual feel verification remains necessary in authored narrow corridors and corners.
- Contact refreshes a bounded push velocity (2.4 units/second); `FixedUpdate` advances it through the same safe movement checks and decelerates at 8 units/second squared. After contact ends the heart coasts for at most 0.3 seconds/about 0.36 units. Obstruction clears momentum immediately; disable/new drop clears it, and pause suspends it. No serialized tuning fields were added.

### Current playable loot pool (2026-09-16)

- Hub's weaponGravePrefab points to Prefabs/Loot/WeaponGrave.prefab (GraveType.JunkWeapon). Its starter candidate source is Data/Loot/Tables/GraveLootTable.asset.junkWeaponCandidates: ApprenticeHeroSword, LightningSpear, CrimsonBoundary only. Flowering and OddIron are special weapons and are not starter candidates.
- LootPoolItemSelectionService.CanDropWeapon permits those three for ordinary selection and grave candidates; Flowering/OddIron are eligible for treasure-chest and merchant selection. Other weapon IDs are excluded even if old saves unlock them.
- ItemDatabase.defaultUnlockedWeapons contains the three ordinary weapons plus both specials. WindWeapon is retained in allWeapons for ID lookup but is no longer a default unlock; ApprenticeHeroSword is now a default unlock.
- WindWeapon-exclusive relic IDs RD_Stenographer (속기사) and RD_OneDropOfSwiftness (한 방울의 신속) are excluded by loot rarity selection and merchant relic selection, and removed from defaultUnlockedRelics. allRelics retains both for existing references. Existing drop exclusions RD_RunningLedger/RD_FeatherOrbit also apply to merchant selection, matching treasure-chest eligibility.
- Count profiles, rarity weights, duplicate exclusions, item definitions and special-weapon acquisition eligibility are otherwise unchanged. These lists are the current release selection; expand CanDropWeapon when another weapon becomes playable.


### Weapon duplicate exclusions by source (2026-09-16)

ShopInventoryRoll uses CanDropWeapon(..., treasureChest: true) and CanDropRelic, matching treasure-chest candidate eligibility. Category counts/weights and per-source duplicate exclusions remain separately owned.

- Normal/boss chest default context: all player weapon slots, plus previously picked weapons in that same chest roll. World drops, merchant stock and other chest contents are not inspected.
- Starter/JunkWeapon and Weapon grave: player weapon slots plus all run-session merchant weapon entries; same-roll picks added to the ban list. Merchant collection does not check isSold, so sold entries remain excluded until stock is replaced. World drops/other chest contents are not inspected.
- Shop new stock: player slots, registered world weapon pickups and active scene WeaponDrop2D objects. DrawFromPool removes each selected definition, preventing duplicates within that roll. Other merchant stock is not included in ShopStock context. Added slots exclude the current stock entries; a full refresh excludes all items in the immediately preceding list, including sold entries. Older offerings can return after being absent from the preceding list.
- Monster weapon drop default context: player slots only. Current authored stage tables have mobWeaponWeight zero.
- No global once-per-run weapon history exists in this exclusion path. These are source snapshots at roll time, not automatic removals from already-generated rewards.


### Merchant refresh fallback (2026-09-16)

MerchantRunStateService passes old stock before replacing the list. MerchantNPC uses slot offset zero for full refresh and existing count for appended slots. Exhausted slots fall back to remaining relics/consumables with positive weights and the existing consumable cap. Previous-stock exclusions and same-roll deduplication remain enforced. Empty slots remain possible if all permitted alternatives are exhausted.

### Chest reward selection completion (2026-09-17)

Chest UI now stages one or two rewards and transfers them only on confirmation. Closing before confirmation retains loot; successful reroll clears provisional UI choices. Successful confirmation clears unselected loot and deactivates the chest through `TreasureChest.CompleteLootSelection`. `RestoreOpenedStateForDungeon` treats nonzero acquired count with no remaining loot as completed and inactive, preserving nested-chest completion without changing save DTO fields. Loot-generation tables and reroll policy are unchanged. See [InventoryAndChestUIStructure](InventoryAndChestUIStructure.md#chest-selection-and-confirmation-2026-09-17) for UI ownership and transfer validation.

### Relic upgrade eligibility (2026-09-18)

- `LootPoolItemSelectionService.GetRandomRelicByRarity` excludes relics already at maximum level in the current player's `RelicInventory` before building exact/lower/any-rarity candidate lists. Normal/boss chest, monster and grave rolls share this path. Definitions and unlock data are unchanged; an unowned relic remains eligible, including maxLevel 1 relics. No player means no owned-level exclusion.
- `TreasureChest.TryAddLootItem` checks `ChestRewardPolicy.CanOfferRelic` against owned levels plus actual levels already placed in this chest, including chest level bonuses. An exhausted candidate is replaced using the existing rarity fallback with the same eligibility filter. If no eligible alternative exists, the slot is omitted. At owned 4/5, only one copy is offered; at 3/5, two +1 copies or one +2 copy exhaust the budget.
- Reservations are chest contents, not player upgrades or global history. Initialization and refresh clear contents before filling; separate chests have separate budgets. Existing generated/saved contents are not retroactively rewritten when player levels change, so confirmation-time validation remains necessary.
- `ParcelRelicDefinition` retains its separate carry-count behavior rather than being treated as a leveled duplicate. Merchant stock uses its own selection path and is unchanged.

## Chest/shop exclusion cooperation — 2026-11-01

- `ChestLootRequest` defaults to `ChestStock`; TreasureChest uses `ForChest(this)` to exclude current player weapons, world pickups, active WeaponDrop2D, remaining weapons in other opened chests and unsold merchant stock. The source chest is omitted so reroll only reserves retained choices through the existing RetainedItems path.
- `ShopStock` also excludes opened-chest leftovers. Loaded/inactive chest objects are inspected live; unloaded PreserveDuringRun dungeons use their existing saved chestLoot. Successfully generated dungeon IDs suppress their old snapshots so consumed loot and source reroll cannot be blocked by stale copies. No save schema or new registry is introduced.
- Final TreasureChest insertion also validates boss bonus weapon lists, replacing conflicts with an eligible weapon or omitting them if exhausted. Selection completion clears residual loot; cancel/close without acquisition preserves it. Initialization/reroll emit the existing WorldStateChanged signal.
- Shop relic rolls and stored-stock repair use the same CanGainRelicLevels rule as chests. The level helper preserves ParcelRelicDefinition's existing non-upgrade exception. Maximum-level offers are replaced after relic changes/restoration without replenishing SOLD slots or consuming refresh count.
- Existing MerchantStock includes sold entries for its older grave callers; the new UnsoldMerchantStock source is specific to the chest policy. Existing enum values, monster/grave contexts, rarity/count settings and currency behavior are unchanged.
- [Current shop map](ShopStructure.md), [changed files, evidence and unexecuted manual checks](../../SessionLogs/2026-11-01-chest-shop-policy/README.md). MSBuild Gameplay passed; automated tests/Editor import/Play Mode were not executed.
