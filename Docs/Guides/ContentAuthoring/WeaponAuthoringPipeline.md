---
status: active
authority: guide
category: content-authoring
last_reviewed: 2026-05-15
---

# Weapon Authoring Pipeline

## Purpose

새 무기 또는 새 무기 스킬을 만들 때 `WeaponDefinition`, GAS, runtime state, cleanup, inventory/loot 연결을 어떤 순서로 확인할지 정리합니다.

관련 기준:

- [Gameplay Ability Weapon Architecture](../../Architecture/GameplayAbilityWeaponArchitecture.md)
- [Weapon Cleanup Contract](../../Contracts/WeaponCleanupContract.md)
- [Dual Weapon Pattern Guide](../DualWeaponPatternGuide.md)
- [Weapon And GAS Structure](../../StructureMemory/ScriptSystems/WeaponAndGASStructure.md)

## Pipeline

| Step | Authoring Work | Main Files / Assets |
| --- | --- | --- |
| 1. 무기 컨셉 고정 | `Attack`, `Skill1`, `Skill2`의 입력별 역할, persistent state 필요 여부, 반대 슬롯 참조 여부, 긴 실행 여부를 먼저 정합니다. | 기획 노트, 기존 기준 무기. |
| 2. 아이템 정의 작성 | 이름, ID, 아이콘, 장착 스탯, prefab, equipped tag, direct ability 또는 loadout 참조를 둡니다. | `WeaponDefinition.cs`, `ItemDatabase.cs`. |
| 3. Ability / Logic 작성 | 각 입력이 실행할 `AbilityDefinition`과 `AbilityLogic`을 구성합니다. 패턴별 경고, hit, VFX/SFX는 가능한 AL data가 소유합니다. | `AbilityDefinition.cs`, `AbilityLogic_*`. |
| 4. Loadout / Selection 작성 | 입력별 기본 ability와 선택 전략을 분리합니다. 상태를 읽어 ability를 바꾸는 규칙은 `WeaponAbilitySelectionStrategy`에 둡니다. | `WeaponAbilityLoadout.cs`, `WeaponAbilitySelectionStrategy.cs`. |
| 5. Runtime state 작성 | 슬롯이 기억해야 할 값은 `WeaponRuntimeData`, 시간 경과 규칙은 `WeaponRuntimeProcessor`, 장착 중 event hook은 runtime state adapter에 둡니다. | `WeaponRuntimeData`, `WeaponRuntimeProcessor`, `WeaponAbilityRuntimeState`. |
| 6. 긴 실행과 cleanup 작성 | 대기, 투사체 회수, 링크, 연속 입력처럼 실행 시간이 긴 동작은 executor와 runner cleanup 경로를 탑니다. | `WeaponAbilityExecutor`, `WeaponExecutorRunner`. |
| 7. 쌍무기 상호작용 연결 | 반대 슬롯 상태를 읽거나 소비해야 하면 selection context와 interaction layer를 사용합니다. 직접 cross-write는 피합니다. | `WeaponSelectionContext`, `WeaponInteractionLayer`, `WeaponPairInteractionRule`. |
| 8. 획득 경로 연결 | 무기가 보상으로 나와야 하면 database, unlock, loot pool, world pickup, inventory 경로를 확인합니다. | `ItemDatabase.cs`, `LootManager.cs`, `WorldItemPickup2D.cs`. |

## Ownership Rules

| Concern | Owner |
| --- | --- |
| 무기 정체성, 아이콘, 장착 스탯 | `WeaponDefinition` |
| 입력별 ability 후보와 default ability | `WeaponAbilityLoadout` 또는 `WeaponDefinition` fallback |
| 현재 상태에 따른 ability 선택 | `WeaponAbilitySelectionStrategy` |
| 슬롯이 오래 기억해야 하는 값 | `WeaponRuntimeData` |
| 비활성 슬롯 포함 시간 경과 | `WeaponRuntimeProcessor` / `WeaponRuntimeCoordinator` |
| 장착 중 event hook과 live adapter | `WeaponAbilityRuntimeState` |
| 긴 실행, 취소, 강제 종료 | `WeaponAbilityExecutor` / `WeaponExecutorRunner` |
| hit, damage, element 처리 | combat pipeline |
| HUD, tooltip | 현재 상태 projection |

## Checklist

- `weaponId`가 유일하고 `ItemDatabase.allWeapons` 또는 unlock 경로에 등록되어 있는가.
- `AbilityDefinition`이 실제로 grant 대상에 포함되는가.
- `WeaponAbilityLoadout.GetValidationErrors()`가 전략/필수 참조 누락을 드러낼 수 있는가.
- persistent state와 live state가 섞이지 않았는가.
- `WeaponSwapped`, `SceneChanged`, `OwnerDisabled`, `Timeout`에서 cleanup 대상이 분명한가.
- 반대 슬롯 상태를 직접 수정하지 않고 interaction layer나 coordinator를 거치는가.
- `ScriptableObject` data에 scene object reference를 저장하지 않는가.
- presentation timing이 pattern-specific이면 AL-owned data에, state rhythm이면 owner/state에 있는가.

## Pitfalls

- GAS/ASC는 선택된 실행을 담당하고, 무기별 persistent state owner가 되면 안 됩니다.
- strategy는 읽기 전용입니다. cleanup이나 상태 mutation은 runtime state, executor, coordinator 쪽에서 처리합니다.
- `WeaponRuntimeDataFactory`나 `WeaponRuntimeProcessorFactory`에 새 무기 예외가 늘어나는 경우에는 구조 부채 후보로 따로 기록합니다.
- prefab, scene, serialized field, ScriptableObject schema를 바꾸는 실제 구현은 reference risk 검토 후 진행합니다.
