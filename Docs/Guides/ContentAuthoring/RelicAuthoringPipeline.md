---
status: active
authority: guide
category: content-authoring
last_reviewed: 2026-05-15
---

# Relic Authoring Pipeline

## Purpose

새 유물을 만들 때 `RelicDefinition`, `RelicLogic`, proc, tooltip, inventory level/merge, loot 연결을 어떤 순서로 확인할지 정리합니다.

관련 기준:

- [Inventory And Chest UI Structure](../../StructureMemory/ScriptSystems/InventoryAndChestUIStructure.md)
- [Loot Reward Structure](../../StructureMemory/ScriptSystems/LootRewardStructure.md)
- [Runtime Save Architecture](../../Architecture/RuntimeSaveArchitecture.md)

## Pipeline

| Step | Authoring Work | Main Files / Assets |
| --- | --- | --- |
| 1. 효과 타입 정의 | 장착 즉시 modifier인지, gameplay event proc인지, runtime state 저장이 필요한지, 레벨당 수치가 바뀌는지 정합니다. | 기획 노트. |
| 2. RelicDefinition 작성 | ID, 이름, 아이콘, rarity, description, `maxLevel`, `dropLevel`, `logic`, 선택 `param`을 설정합니다. | `RelicDefinition.cs`. |
| 3. RelicLogic 작성 | `OnEquipped`, `OnUnequipped`, 필요 시 `OnRestoreAttached`, `OnRestoreDetached`, tooltip builder를 구현합니다. | `RelicLogic.cs`, `RelicLogic_*`. |
| 4. Proc 연결 | hit, kill, damaged 같은 gameplay event를 듣는 유물은 `RelicProcManager`와 proc 객체로 분리합니다. | `RelicProcManager.cs`, `IRelicProc.cs`, `Procs/*`. |
| 5. Runtime state 저장 판단 | 유물별 누적 스택이나 지속 상태가 저장/복원되어야 하면 serializer 경계를 검토합니다. | `RelicRuntimeStateHub`, `IRelicRuntimeStateSerializer`. |
| 6. Inventory level/merge 확인 | 같은 `relicId`는 기본적으로 하나로 합쳐지고 `dropLevel`만큼 강화됩니다. max level 도달 실패 처리를 확인합니다. | `RelicInventory.cs`. |
| 7. 획득/loot 연결 | 보상으로 등장해야 하면 database, unlock, loot table, chest, world pickup 경로에 등록합니다. | `ItemDatabase.cs`, `LootManager.cs`, `WorldItemPickup2D.cs`. |

## Ownership Rules

| Concern | Owner |
| --- | --- |
| 유물 정체성, rarity, level policy | `RelicDefinition` |
| 장착/해제 효과 | `RelicLogic` |
| gameplay event 반응 | `RelicProcManager`와 proc |
| 장착 슬롯, 중복 merge, level reapply | `RelicInventory` |
| tooltip 문장 완성 | `RelicLogic.BuildTooltip` |
| 획득 source와 reward roll | loot/reward pipeline |
| UI display | detail panel / tooltip projection |

## Checklist

- `relicId`가 유일하고 `ItemDatabase.allRelics` 또는 unlock 경로에 등록되어 있는가.
- `logic`이 null일 때 gameplay 효과가 없는 것이 의도인가.
- `OnUnequipped`가 `OnEquipped`에서 건 modifier, tag, proc, runtime hook을 되돌리는가.
- restore path에서 중복 효과 적용 없이 hook만 복원되는가.
- level merge 후 기존 효과가 올바른 level로 reapply 되는가.
- tooltip 수치가 현재 preview level과 같은 계산 기준을 쓰는가.
- loot/chest/world pickup에서 relic level override가 보존되어야 하는지 확인했는가.

## Pitfalls

- `RelicInventory`가 큰 aggregate인 것은 현재 도메인상 허용 가능한 구조입니다. 다만 acquisition/delivery source가 더 늘어나면 loot/reward 또는 inventory transfer backlog와 함께 다시 봅니다.
- UI는 유물 상태를 소유하지 않고 `RelicInventory`와 `RelicLogic`이 만든 현재 상태를 표시해야 합니다.
- `ScriptableObject` schema 변경은 기존 asset migration risk를 먼저 봅니다.
