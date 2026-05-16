---
status: active
authority: guide
category: content-authoring
last_reviewed: 2026-05-15
---

# Consumable Authoring Pipeline

## Purpose

새 소모품을 만들 때 `ConsumableDefinition`, 사용 효과, 소모품 인벤토리, 획득/loot/UI 연결을 어떤 순서로 확인할지 정리합니다.

관련 기준:

- [Inventory And Chest UI Structure](../../StructureMemory/ScriptSystems/InventoryAndChestUIStructure.md)
- [Loot Reward Structure](../../StructureMemory/ScriptSystems/LootRewardStructure.md)
- [Gameplay Status Architecture](../../Architecture/GameplayStatusArchitecture.md)

## Pipeline

| Step | Authoring Work | Main Files / Assets |
| --- | --- | --- |
| 1. 사용 규칙 정의 | 즉시 회복인지, 조건부 사용인지, 실패 조건이 있는지, 사용 후 소모되는지 정합니다. | 기획 노트. |
| 2. ConsumableDefinition 작성 | ID, 이름, 아이콘, 설명, 대상 attribute, 회복량을 설정합니다. | `ConsumableDefinition.cs`. |
| 3. Inventory 연결 | 획득, 슬롯 배치, swap, 사용, save/restore가 `PlayerConsumableInventory` 흐름과 맞는지 확인합니다. | `PlayerConsumableInventory.cs`. |
| 4. Use effect 확인 | 현재 구조에서는 definition의 `TryUse(GameObject owner)`가 AttributeSet을 수정합니다. 복잡한 효과가 늘면 별도 logic/service 분리 후보로 봅니다. | `ConsumableDefinition.cs`, `AttributeSet`. |
| 5. 획득/loot 연결 | database, chest, monster loot, world pickup, merchant/upgrade run-start 지급 경로를 확인합니다. | `ItemDatabase.cs`, `LootManager.cs`, `WorldItemPickup2D.cs`. |
| 6. UI projection 확인 | HUD, inventory slot, detail panel, tooltip이 definition과 inventory state를 표시만 하는지 확인합니다. | inventory/detail UI scripts. |

## Ownership Rules

| Concern | Owner |
| --- | --- |
| 소모품 정체성, 설명, 기본 효과 data | `ConsumableDefinition` |
| 슬롯 보관, 획득, 사용, save/restore | `PlayerConsumableInventory` |
| 실제 attribute 변경 | 현재 `ConsumableDefinition.TryUse` |
| loot roll과 world pickup | loot/reward pipeline |
| UI 표시 | HUD/detail/tooltip projection |

## Checklist

- `consumableId`가 유일하고 `ItemDatabase.allConsumables`에 등록되어 있는가.
- `targetAttribute`와 `restoreAmount`가 유효한가.
- 사용 실패 조건이 UI 경고나 입력 흐름에서 이상하게 보이지 않는가.
- 획득 source가 inventory capacity와 실패 결과를 제대로 처리하는가.
- save/restore 시 소모품 슬롯이 item ID로 복원되는가.
- chest, monster, grave, merchant, upgrade 지급 경로 중 필요한 곳에만 등장하는가.

## Pitfalls

- 현재 소모품은 단순 회복형 기준입니다. 조건부 효과, 복합 효과, 장시간 버프, target 선택이 들어오면 `ConsumableDefinition`에 계속 로직을 추가할지 별도 effect logic으로 나눌지 다시 결정해야 합니다.
- runtime `GetOrAdd` fallback은 편하지만 prefab/scene authoring이 흐려질 수 있으므로 production-facing 연결에서는 명시 참조를 우선합니다.
