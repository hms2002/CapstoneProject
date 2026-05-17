---
status: active
authority: guide
category: content-authoring
last_reviewed: 2026-05-15
---

# Loot Reward Integration Pipeline

## Purpose

새 무기, 유물, 소모품 또는 보상 source를 `ItemDatabase`, loot table, chest, boss reward, world pickup에 연결할 때 확인할 흐름을 정리합니다.

관련 기준:

- [Loot Reward Structure](../../StructureMemory/ScriptSystems/LootRewardStructure.md)
- [Loot Reward Policy Boundary Split](../../RefactorBacklog/LootRewardPolicyBoundarySplit.md)
- [Inventory Transfer Responsibility Split](../../RefactorBacklog/InventoryTransferResponsibilitySplit.md)
- [BossDrop Responsibility Split](../../RefactorBacklog/BossDropResponsibilitySplit.md)

## Pipeline

| Step | Authoring Work | Main Files / Assets |
| --- | --- | --- |
| 1. Reward source 정의 | chest, monster death, boss battle-end, grave, merchant, upgrade, affection, direct world drop 중 어디서 등장하는지 정합니다. | 기획 노트. |
| 2. ItemDatabase 등록 | 무기, 유물, 소모품을 각 `all*` 목록과 필요 시 default unlock 목록에 등록합니다. | `ItemDatabase.cs`, `ItemManager.cs`. |
| 3. Loot table 연결 | stage/chest/monster/grave/boss base reward에 필요한 count, rarity, 확률을 조정합니다. | `StageLootTable`, `GraveLootTable`, `LootManager.cs`. |
| 4. Exclusion context 확인 | 무기는 player inventory, world pickup, scene drop, merchant stock 제외 기준이 source별로 맞는지 확인합니다. | `LootPoolService`, `LootRewardPolicyBoundarySplit.md`. |
| 5. Chest reward 확인 | chest modifier, refresh, relic level bonus, first-open UI handoff를 `TreasureChest`와 loot policy 관점에서 확인합니다. | `TreasureChest.cs`, `LootManager.cs`. |
| 6. Monster death reward 확인 | 일반 몬스터 사망 보상이 `Mob.OnDeathStarted`와 `LootManager.SpawnMonsterLoot` 흐름을 타는지 확인합니다. | `Mob.cs`, `LootManager.cs`. |
| 7. Boss reward 확인 | base reward는 stage loot 기준을 유지하고 extra reward는 additive modifier 또는 boss-specific extra item으로 연결합니다. | `BossBattleEndHandler.cs`, `BossRewardSpawnService.cs`. |
| 8. World pickup delivery 확인 | pickup presentation, inventory 지급, 실패 warning, destroy timing이 의도와 맞는지 확인합니다. | `WorldItemPickup2D.cs`. |

## Ownership Rules

| Concern | Owner |
| --- | --- |
| item ID lookup와 unlock state | `ItemDatabase` / `ItemManager` |
| reward roll orchestration | `LootManager` |
| loot pool exclusion | `LootPoolService` |
| chest-specific refresh/modifier/level bonus | currently `TreasureChest`, future `ChestRewardPolicy` candidate |
| boss base reward와 modifier aggregation | `LootManager` / `BossBattleEndHandler` / `BossRewardSpawnService` |
| world object presentation과 pickup request | `WorldItemPickup2D` |
| final inventory transfer policy | inventory transfer boundary candidate |

## Checklist

- reward source가 명시되어 있고, 불필요한 source에 새 item이 등장하지 않는가.
- item ID가 유일하며 database cache lookup이 가능한가.
- unlock-only item과 loot-pool item의 구분이 맞는가.
- chest, monster, grave, boss reward 확률 또는 count 변경이 기존 기획 의도와 맞는가.
- world pickup 실패 사유가 inventory failure와 같은 의미로 표시되는가.
- relic level, merge, consumable capacity, weapon duplicate exclusion이 보존되는가.
- boss reward 변경이 `BossDropResponsibilitySplit`과 중복되는 새 legacy path를 만들지 않는가.

## Pitfalls

- 새 reward source가 늘어날수록 roll policy, exclusion context, delivery가 암묵적으로 연결되기 쉽습니다. 구체 문제가 확인되면 `LootRewardPolicyBoundarySplit`을 구현 후보로 다시 엽니다.
- world pickup 지급 규칙은 inventory transfer 문제와 겹치므로 별도 중복 backlog를 만들지 않습니다.
- 확률/보상 수치 변경은 문서 검증만으로 성공을 주장할 수 없고 플레이 검증이 필요합니다.
