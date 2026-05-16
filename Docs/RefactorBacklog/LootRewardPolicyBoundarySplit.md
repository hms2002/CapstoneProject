---
status: resolved
authority: refactor-backlog
category: loot-reward
last_reviewed: 2026-05-15
---

# Loot Reward Policy Boundary Split

## Current Problem

General loot and reward behavior is split across runtime objects in a way that hides policy boundaries.

- `LootManager` remains the public facade for monster loot, grave loot, field heal spawning, and boss base reward helpers. Chest, monster, and grave execution now have same-file helper boundaries, but those helpers are still colocated with `LootManager`.
- `LootPoolService` now accepts an explicit weapon-exclusion context, delegates live source reads to `LootPoolLiveWeaponExclusionSourceProvider`, and delegates pure source/context combination to `LootPoolWeaponExclusionProvider`. Item selection reads from `ItemManager.Instance` are behind `LootPoolItemSelectionService`.
- `TreasureChest` delegates chest refresh eligibility, refresh guard snapshots/comparison, and relic level bonus calculation to a same-file `ChestRewardPolicy`, but that helper still reads `RunModifierService` directly and remains colocated with the world chest component.
- `WorldItemPickup2D` presents a world item, handles interaction, displays delivery warnings/speech, and destroys itself after successful pickup. `WorldPickupDeliveryService` now grants items to player inventories and uses shared inventory delivery warning mapping for relic/consumable failures.

The boss reward path is tracked separately: `BossRewardSpawner` follows the additive modifier decision and delegates chest/currency/field-heal spawn execution through a request/result helper. The former legacy adapter risk is resolved by `BossDropResponsibilitySplit`.

## Why It Exists

The current structure grew from practical feature ownership. `LootManager` became the easiest single entry point for rolling and spawning rewards, while world pickups and chests handled delivery details close to the objects the player interacts with.

As reward sources expand, the same coupling makes it hard to tell which rules apply to a given source. Chest, monster, grave, shop, boss, field drop, and future event rewards may need different exclusion, modifier, and delivery rules.

## Target Shape

- `LootManager` remains an orchestration facade and delegates source-specific policy to narrower services.
- `LootPoolContext` explicitly describes exclusion sources for a roll, such as player inventory, world pickups, scene drops, merchant stock, or no exclusions.
- Live loot pool source reads are isolated from pure source/context combination so source snapshots can be injected for tests.
- `ChestRewardPolicy` owns chest reward deltas, refresh count, refresh guard decisions, and relic level bonus behavior.
- `RewardDelivery` or `WorldPickupDelivery` owns final inventory/currency/world-object delivery and returns success/failure results.
- `WorldItemPickup2D` focuses on world presentation and interaction request forwarding.
- Inventory delivery warning mapping is shared between quick-move, player relic adapters, and world pickup delivery.
- Future boss battle-end authoring issues should continue through `BossDropResponsibilitySplit` instead of a duplicate backlog item.

## Risks

- Existing reward probabilities and count rolls must remain behavior-compatible.
- Weapon exclusion rules must preserve current player, world pickup, scene drop, and merchant stock behavior where that behavior is intended.
- Chest refresh count, refresh guard, and relic level bonus behavior must not regress.
- World pickup failure warning codes must still match inventory failure reasons.
- Prefab-spawn paths for world item pickups, magic stones, field heals, and boss chests must keep their required components.
- Future implementation may touch MonoBehaviours, ScriptableObjects, prefabs, and serialized references, so Unity reference risk must be reviewed first.

## Refactor Trigger

Start this split when one of the following happens:

- A new reward source is added.
- Chest upgrades or chest reward modifiers expand.
- Loot exclusion rules differ by reward source.
- World pickup UX changes, such as auto-pickup, temporary storage, comparison UI, or different failure handling.
- Loot table behavior is extended beyond the current stage/grave/boss base paths.

## Related Documents

- `Docs/StructureMemory/ScriptSystems/LootRewardStructure.md`
- `Docs/RefactorBacklog/BossDropResponsibilitySplit.md`
- `Docs/RefactorBacklog/InventoryTransferResponsibilitySplit.md`
- `Docs/DecisionLog.md` - `Boss Rewards Use Additive Modifier Aggregates`

## Partial Progress

2026-05-15:

- Added `ChestLootRequest` and `ChestLootResult` as same-file helper types in `LootManager.cs`.
- Kept existing `LootManager.GenerateChestLoot()` and `GenerateChestLoot(ChestRunModifierDelta)` List-returning compatibility APIs.
- Routed `TreasureChest` self-generation and refresh through `LootManager.GenerateChestLootResult(...)`.
- Preserved chest count rolls, player weapon exclusion behavior, chest modifier merging, refresh guard behavior, refresh count checks, relic level bonus behavior, and boss chest compatibility.
- Added `LootPoolContext` and `LootPoolExclusionSource` as same-file helper types in `LootPoolService.cs`.
- Added `LootPoolService.BuildWeaponExclusionSet(LootPoolContext)` and kept the existing `BuildPlayerWeaponExclusionSet()`, `BuildShopWeaponExclusionSet()`, and `BuildMerchantWeaponExclusionSet()` compatibility APIs.
- Routed chest/monster, shop, and grave weapon rolls through explicit context presets while preserving their existing exclusion sources.
- Added same-file `ChestLootGenerationService` in `LootManager.cs` and routed `GenerateChestLootResult(...)` through it while keeping the legacy List-returning APIs.
- Added same-file `ChestRewardPolicy` and file-scope `ChestLootSnapshot` in `TreasureChest.cs`, and routed refresh eligibility, refresh guard recording/comparison, and relic level bonus calculation through the helper.
- Added same-file `WorldPickupDeliveryRequest`, `WorldPickupDeliveryResult`, `WorldPickupDeliveryFailureReason`, and `WorldPickupDeliveryService` in `WorldItemPickup2D.cs`.
- Routed weapon, relic, and consumable world pickup grant attempts plus relic/consumable warning-code mapping through `WorldPickupDeliveryService`, while `WorldItemPickup2D` keeps warning display, failed-pickup speech, and success destruction.
- Added same-file `LootPoolWeaponExclusionRequest`, `LootPoolWeaponExclusionResult`, `LootPoolWeaponExclusionProvider`, and `LootPoolItemSelectionService` in `LootPoolService.cs`.
- Routed player inventory, world pickup, scene weapon drop, merchant stock, and `ItemManager.Instance` reads through those helpers while keeping the existing `LootPoolService` public compatibility APIs.
- Added same-file `MonsterLootDropRequest`, `MonsterLootDropResult`, `MonsterLootDropService`, `GraveLootDropRequest`, `GraveLootDropResult`, and `GraveLootDropService` in `LootManager.cs`.
- Routed monster loot type roll/drop execution and grave weapon/relic roll/drop execution through those helpers while keeping `LootManager` public spawn APIs intact.
- Added same-file `BossRewardSpawnRequest`, `BossRewardSpawnResult`, and `BossRewardSpawnService` in `BossRewardSpawner.cs`.
- Routed boss treasure chest, base loot, bonus loot, magic stone, field heal, scatter, and exception-logged reward spawn execution through `BossRewardSpawnService` while keeping `BossRewardSpawner` event handling intact. The later `BossDrop` deletion removed the old legacy spawn API.
- Moved `LootPoolContext`, loot pool provider/selection helpers, chest loot generation, monster/grave drop services, boss reward spawn service, chest reward policy, and world pickup delivery helpers into dedicated `.cs` files during the P1 helper file split.
- Added shared `InventoryDeliveryWarningResolver` in `InventoryTransferService.cs`.
- Routed quick-move full-inventory warning mapping, world pickup relic/consumable warning mapping, and player relic adapter warning mapping through the shared resolver.
- Added `LootPoolWeaponExclusionSourceSet` and `LootPoolLiveWeaponExclusionSourceProvider`.
- Routed `LootPoolService.BuildWeaponExclusionSet(...)` through an internal source snapshot provider seam while keeping the public loot pool APIs unchanged.
- Reduced `LootPoolWeaponExclusionProvider` to source snapshot plus `LootPoolContext` combination; live reads from `PlayerRuntimeRegistry`, `WorldItemRegistry`, `Object.FindObjectsByType<WeaponDrop2D>`, and `GamePlayDataManager` now live in the live source provider.
- Did not introduce dedicated namespaces, asmdefs, serialized-field changes, or prefab/scene changes.

## Status

`resolved`

The chest loot request/result slice, weapon-exclusion context slice, chest policy/generation helper slice, world pickup delivery helper slice, loot pool provider/selection helper slice, monster/grave drop execution helper slice, boss reward spawn execution helper slice, inventory delivery warning resolver slice, and live loot pool source provider split are implemented. World pickup and inventory transfer now share warning-code mapping for the current overlapping failure cases; delivery result types remain source-specific unless future UX needs a common result model. No prefab, scene, namespace, asmdef, serialized field, `MonoBehaviour` serialized contract, or `ScriptableObject` schema changes were made for this backlog item. The later BossDrop adapter deletion is tracked in `BossDropResponsibilitySplit`.
