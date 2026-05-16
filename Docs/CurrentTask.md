---
status: completed
authority: current-task
category: refactor
last_reviewed: 2026-05-16
---

# Current Task

## Goal

Split boss base reward defaults into `StageLootTable` while keeping RouteSets limited to optional boss special reward presets and keeping Affection/Upgrade reward changes as runtime modifier overlays.

## References

- `AGENTS.md`
- `Docs/README.md`
- `Docs/ErrorLog.md`
- `Docs/DecisionLog.md`
- `Docs/StructureMemory/ScriptSystems/LootRewardStructure.md`
- `Docs/RefactorBacklog/BossDropResponsibilitySplit.md`
- `Docs/Guides/ContentAuthoring/LootRewardIntegrationPipeline.md`

## In Scope

- Add a Boss Reward section to `StageLootTable` for boss weapon count, relic count, relic rarity weights, magic stone count, and field heal base count.
- Preserve current StageLootTable asset behavior by copying existing chest weapon/relic count profiles and relic rarity weights into the new boss fields.
- Add a boss-specific chest generation path so boss rewards do not use the normal chest profile directly.
- Keep RouteSet data limited to optional `BossSpecialRewardPresetSO` special-loot candidates.
- Keep Affection/Upgrade boss reward increases on `BossRewardModifierAggregate`.
- Update project memory documents for the new ownership boundary.

## Out of Scope

- Adding boss consumable base rewards.
- Adding Legendary boss relic rarity.
- Authoring final boss special reward preset contents.
- Running Unity batchmode while Unity Editor processes are open.

## Done Criteria

- `StageLootTable` exposes boss weapon/relic count profiles, boss Common/Rare/Epic relic rarity weights, boss magic stone count, and boss field heal base count.
- Boss chest reward spawning uses the boss chest generation path.
- Normal chest generation continues to use the normal chest profiles.
- RouteSet special reward presets and runtime boss reward modifiers are still applied on top of boss base rewards.
- StageLootTable assets include serialized boss reward defaults.
- Verification reports distinguish source/static checks from Unity Editor import/compile/play checks.

## Outcome

- Added boss weapon count, boss relic count, boss relic rarity, and boss field heal base fields to `StageLootTable`.
- Migrated `Table_Stage1`, `Table_Stage2`, and `Table_Stage3` to copy current chest weapon/relic count and rarity defaults into the new boss fields, with boss field heal base count set to `0`.
- Added `LootRollService.RollBossRelicRarity(...)`.
- Added `LootManager.GenerateBossChestLoot(...)`, `GenerateBossChestLootResult(...)`, and `GetBossFieldHealBaseCount()`.
- Updated `BossRewardSpawnService` so boss chests use the boss-specific generation path and boss field heal count is `StageLootTable` base plus runtime modifier bonus.
- Updated `DecisionLog`, `LootRewardStructure`, `BossDropResponsibilitySplit`, and the session log.
- Static source searches confirmed the boss reward spawner no longer calls normal `GenerateChestLoot(...)` directly.
- Unity batchmode was not run because Unity Editor processes were open.
