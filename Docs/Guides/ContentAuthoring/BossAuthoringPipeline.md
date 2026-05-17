---
status: active
authority: guide
category: content-authoring
last_reviewed: 2026-05-15
---

# Boss Authoring Pipeline

## Purpose

새 보스 또는 보스 패턴을 `Encounter -> Battle -> BattleEnd` 흐름으로 제작할 때 필요한 owner와 연결 지점을 정리합니다.

관련 기준:

- [Boss Encounter Architecture](../../Architecture/BossEncounterArchitecture.md)
- [Presentation Authoring Contract](../../Contracts/PresentationAuthoringContract.md)
- [Boss And Mob Encounter Structure](../../StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md)
- [Loot Reward Structure](../../StructureMemory/ScriptSystems/LootRewardStructure.md)

## Pipeline

| Step | Authoring Work | Main Files / Assets |
| --- | --- | --- |
| 1. Boss flow 정의 | 등장/대화, 전투 시작 조건, 페이즈, 패턴 세트, 그로기/반응 상태, 처치 후 보상/포탈을 분리해서 정합니다. | 기획 노트. |
| 2. Encounter 작성 | 대화, 카메라, target 활성화, 전투 시작 handoff를 설정합니다. 이 단계는 battle logic을 직접 소유하지 않습니다. | `BossEncounterDirector.cs`, boss dialogue files. |
| 3. Battle controller 작성 | `BossControllerBase` 기반으로 phases, blackboard, state machine, target refresh, pattern select/execute를 구성합니다. | `BossControllerBase.cs`, boss-specific controller. |
| 4. Pattern data 작성 | `BossPatternEntry`, condition, executor, ability logic, boss-specific actor를 만듭니다. 보스 고유 로직은 말단 구현부에 둡니다. | `BossPatternCondition`, `AbilityLogic_*`, pattern actors. |
| 5. Presentation 작성 | pattern warning/hit/projectile은 AL-owned, state rhythm은 state/owner-owned, 임시 handle cleanup은 runner/helper-owned로 둡니다. | `PresentationAuthoringContract.md`. |
| 6. HUD 특수성 처리 | 분열 보스, 다중 body, phase-two bind 같은 특수성은 `BossHud` 공용 구조를 바꾸기보다 boss-local adapter/source에 둡니다. | [Boss HUD Special Case Source Split](../../RefactorBacklog/BossHudSpecialCaseSourceSplit.md) 후보. |
| 7. BattleEnd 작성 | death presentation, run progress, authored chest activation, portal activation을 전투 종료 흐름으로 연결합니다. | `BossDeathPresentation`, `RunProgressCoordinator`, `BossBattleEndHandler`. |
| 8. Loot / reward 연결 | base reward는 `LootManager` 기준을 따르고, 추가 보상은 additive modifier나 boss-specific extra reward로 둡니다. | `BossBattleEndHandler.cs`, `BossRewardSpawnService.cs`, `LootManager.cs`. |

## Ownership Rules

| Concern | Owner |
| --- | --- |
| 등장/대화/전투 시작 handoff | Boss Encounter |
| 전투 FSM, phase, pattern selection | Boss Battle controller/state |
| 보스별 패턴 구현 | boss-specific logic/actors |
| pattern execution presentation | AL / pattern data |
| state rhythm presentation | boss state / owner |
| 처치 연출, 보상, 포탈, 진행도 | Boss BattleEnd |
| legacy boss reward adapter | `BossDrop` until prefab migration |

## Checklist

- Encounter, Battle, BattleEnd 책임이 한 클래스에 다시 뭉치지 않았는가.
- 보스 고유 예외가 `BossControllerBase`나 공용 FSM에 새지 않았는가.
- phase와 pattern condition이 boss-specific data로 표현되는가.
- pattern-specific presentation이 공용 cue/tag 예외로 늘어나지 않았는가.
- death presentation 중 player protection과 input/camera 흐름이 유지되는가.
- reward와 portal 처리가 기존 [BossDrop Responsibility Split](../../RefactorBacklog/BossDropResponsibilitySplit.md) 후보와 충돌하지 않는가.

## Pitfalls

- 말단 boss-specific logic이 큰 것은 그 자체로 문제가 아닙니다. 문제는 공용 controller, FSM, HUD, reward policy로 예외가 새는 경우입니다.
- `BossDrop`은 prefab-safe legacy adapter로 남아 있으므로 중복 backlog를 만들지 않습니다.
- 보스별 특수 HUD 요구가 생기면 공용 HUD를 바로 확장하지 말고 boss-local source/adapter로 해결할 수 있는지 먼저 봅니다.
