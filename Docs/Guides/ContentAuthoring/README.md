---
status: active
authority: guide
category: content-authoring
last_reviewed: 2026-05-15
---

# Content Authoring Pipelines

## Purpose

반복적으로 추가될 전투 콘텐츠의 제작 순서를 한 곳에서 찾기 위한 가이드 허브입니다.

이 폴더는 source-of-truth가 아니라 제작 진입점입니다. 세부 규칙이 충돌하면 `Docs/Contracts/`, `Docs/Architecture/`, 기존 전용 가이드를 우선합니다.

## Common Pipeline

| Step | Question | Output |
| --- | --- | --- |
| 1. 기획 정의 | 새 콘텐츠가 어떤 상황에서 등장하고 어떤 규칙을 가지는가? | 기능 요약, 입력/트리거, 보상/실패 조건, 필요한 상태. |
| 2. 데이터 / SO | 어떤 authored asset이 진실한 설정인가? | `ScriptableObject`, prefab serialized reference, loot/database 등록 위치. |
| 3. 런타임 owner | 누가 상태와 생명주기를 소유하는가? | runtime data, MonoBehaviour owner, inventory/save owner, cleanup owner. |
| 4. 전투 / 효과 흐름 | 공격, 효과, 태그, 피해, 상태 변화가 어디서 실행되는가? | Ability, logic, runner, proc, damage/effect path. |
| 5. Presentation | 경고, 투사체, VFX, SFX, HUD 표시가 누가 소유한 타이밍인가? | AL-owned, state-owned, runner cleanup, authored prefab/reference. |
| 6. Inventory / Loot / Save 연결 | 획득, 지급, 해금, 저장, 복원이 필요한가? | item database, loot table, pickup, inventory, runtime restore 연결. |
| 7. 검증 | 제작자가 무엇을 확인해야 하는가? | inspector reference, cleanup, play mode smoke, 문서 링크 확인. |

## Pipelines

| Document | Use When |
| --- | --- |
| [Weapon Authoring Pipeline](./WeaponAuthoringPipeline.md) | 새 무기, 새 무기 스킬, 쌍무기 상호작용, 무기 runtime state를 만든다. |
| [Mob Authoring Pipeline](./MobAuthoringPipeline.md) | 새 일반 몬스터, 몬스터 FSM 공격, spawn population, death result를 만든다. |
| [Boss Authoring Pipeline](./BossAuthoringPipeline.md) | 새 보스 또는 보스 패턴을 `Encounter -> Battle -> BattleEnd` 흐름으로 만든다. |
| [Relic Authoring Pipeline](./RelicAuthoringPipeline.md) | 새 유물, 유물 proc, 유물 레벨/툴팁/복원 동작을 만든다. |
| [Consumable Authoring Pipeline](./ConsumableAuthoringPipeline.md) | 새 소모품, 사용 효과, 소모품 인벤토리 연결을 만든다. |
| [Loot Reward Integration Pipeline](./LootRewardIntegrationPipeline.md) | 새 콘텐츠를 loot table, item database, chest, boss reward, world pickup에 연결한다. |

| [Gemini Ink Dialogue Authoring Guide](./GeminiInkDialogueAuthoringGuide.md) | Gemini/Gem으로 Ink 대사 초안, animated variant, NPC/boss 대사 branch를 작성할 때 사용한다. |

## Related Maps

- [Script System Map](../../StructureMemory/ScriptSystemMap.md)
- [Weapon And GAS Structure](../../StructureMemory/ScriptSystems/WeaponAndGASStructure.md)
- [Boss And Mob Encounter Structure](../../StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md)
- [Inventory And Chest UI Structure](../../StructureMemory/ScriptSystems/InventoryAndChestUIStructure.md)
- [Dialogue NPC Affection Structure](../../StructureMemory/ScriptSystems/DialogueNpcAffectionStructure.md)
- [Scene Runtime Save Structure](../../StructureMemory/ScriptSystems/SceneRuntimeSaveStructure.md)
- [Loot Reward Structure](../../StructureMemory/ScriptSystems/LootRewardStructure.md)

## Use Rules

- 새 콘텐츠 제작 전에는 해당 파이프라인과 관련 `Architecture` / `Contracts` 문서를 먼저 읽습니다.
- `MonoBehaviour`, prefab, scene reference, serialized field, `ScriptableObject` schema 변경이 있으면 Unity reference risk를 먼저 확인합니다.
- runtime-created UI 또는 presentation fallback을 production-facing 구조로 고정하지 않습니다.
- 제작 중 구조 부채가 명확해지면 구현 TODO가 아니라 `Docs/RefactorBacklog/` 후보로 분리합니다.
