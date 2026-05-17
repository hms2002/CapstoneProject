---
status: active
authority: guide
category: content-authoring
last_reviewed: 2026-05-15
---

# Mob Authoring Pipeline

## Purpose

새 일반 몬스터를 현재 FSM 표준 구조로 제작할 때 population/spawn, battle runtime, death result를 어떤 순서로 연결할지 정리합니다.

관련 기준:

- [General Mob FSM Authoring Guide](../GeneralMobFSMAuthoringGuide.md)
- [Mob Cleanup Contract](../../Contracts/MobCleanupContract.md)
- [Presentation Authoring Contract](../../Contracts/PresentationAuthoringContract.md)
- [Boss And Mob Encounter Structure](../../StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md)

## Pipeline

| Step | Authoring Work | Main Files / Assets |
| --- | --- | --- |
| 1. 몬스터 역할 정의 | 상시 배치형인지, 기믹 스폰인지, 추적 범위, 공격 리듬, death result, 분열/소환/변신 여부를 정합니다. | 기획 노트. |
| 2. 본체와 prefab 구성 | `Mob` 상속 본체 또는 기존 본체 확장, `EnemyChaseIntent2D`, `MobAbilityCoordinator`, `AbilitySystem`, `TagSystem`, `AttributeSet` 구성을 확인합니다. | `Mob.cs`, `EnemyChaseIntent2D.cs`, `MobAbilityCoordinator.cs`. |
| 3. 공격 판단 source 작성 | 어떤 공격을 지금 실행할지 `IMobAttackDecisionSource`로 제공합니다. 단순 몬스터는 본체 구현, 복잡한 몬스터는 helper 구현을 선택합니다. | `IMobAttackDecisionSource.cs`. |
| 4. Ability / runner 작성 | ASC로 실행할 ability와 긴 패턴을 수행할 runner를 연결합니다. runner는 runtime handle과 cleanup을 맡고, 고정 presentation data owner가 되지 않게 합니다. | `AbilityLogic_*`, `IMobPatternRunner.cs`. |
| 5. FSM lifecycle 확인 | `Idle`, `Chase`, `Attack`, `Recover`, `Stagger` 기본 흐름에 맞는지, 전용 상태가 필요한지 확인합니다. | `MobStateMachine.cs`, `MobAIContext.cs`. |
| 6. Presentation authoring | warning, hit, projectile presentation은 AL-owned를 우선합니다. 상태가 살아 있는 동안 유지되는 mask/overlay는 state-owned로 둡니다. | `PresentationAuthoringContract.md`. |
| 7. Population / Spawn 연결 | 기본 배치도 `MonsterSpawner`와 scene spawn profile/container 흐름을 통해 생성되는지 확인합니다. | `MonsterSpawner.cs`, `MonsterSpawnContainer`, `MonsterRoomSpawnProfileSO`. |
| 8. Death result 확인 | 기본 사망은 loot spawn으로 이어집니다. 분열, 소환몹, 변신, 지연 사망 연출은 lock overlay 기준이 별도 기획 결정 대상입니다. | `Mob.OnDeathStarted`, `LootManager.cs`, lock overlay scripts. |

## Ownership Rules

| Concern | Owner |
| --- | --- |
| 공통 FSM 실행 | `Mob` / `MobStateMachine` |
| 공격 선택 | `IMobAttackDecisionSource` 구현체 |
| ASC 연결, busy, suppression | `MobAbilityCoordinator` |
| 긴 패턴 runtime handle | `IMobPatternRunner` 구현체 |
| state-owned presentation cleanup | state exit와 fail-safe cleanup |
| population/spawn context | `MonsterSpawner` / scene spawn director |
| death loot | `Mob.OnDeathStarted`와 `LootManager` |
| room/chest lock overlay | lock overlay component; see Lock Count Authoring Rule below |

## Lock Count Authoring Rule

- Room/chest locks count only enemies that enter the lock through spawn registration, plus Slime split descendants that inherit a registered parent's lock context.
- General direct summons do not count toward room/chest clear. Do not rely on `Instantiate(...)` alone to add a summoned enemy to a lock.
- Transform or phase changes on the same GameObject stay one tracked enemy.
- Death presentation remains locked while the tracked GameObject still exists; lock release follows destruction/null compaction, not explicit death start.
- No-loot or gimmick enemies count only if they use the same spawn registration or Slime split inheritance path.

## Checklist

- prefab에 `MobAbilityCoordinator`와 `AbilitySystem`이 함께 있는가.
- `IMobAttackDecisionSource`가 하나 이상 해석되는가.
- chase intent가 붙어 있고 타겟 지연 생성 상황에서 회복 가능한가.
- runner가 `Cancel/finally` 또는 fail-safe cleanup 경로를 갖는가.
- AL-owned, state-owned, runner cleanup presentation 경계가 문서 기준과 맞는가.
- spawn profile/container가 scene-local authoring과 `MonsterSpawner` 흐름에 맞게 배치되었는가.
- 죽음 후 loot, split, summon, lock count 기준이 기획적으로 확정됐는가.

## Pitfalls

- 일반 몬스터는 Encounter 단위가 아니라 population 후 즉시 battle-ready 상태로 보는 편이 현재 구조에 맞습니다.
- 공통 FSM 엔진에 몬스터별 특수 패턴을 밀어 넣지 않습니다. 특수성은 decision source, runner, 전용 상태로 둡니다.
- 분열, 소환, 변신, 사망 지연은 lock 해제 기준을 꼬이게 만들 수 있으므로 리팩토링 확정 전에 기획 기준이 필요합니다.
