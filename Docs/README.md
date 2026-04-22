# Project Docs Guide

이 문서는 "무엇을 하려는지" 기준으로 어디 문서를 먼저 읽어야 하는지 안내하는 빠른 라우터입니다.

## 아키텍처 핵심 원칙

- 무기 상태는 `RuntimeData`가 소유하고, GAS/ASC는 선택된 실행만 담당합니다.
- `PlayerStatusRuntime`은 상태 저장소가 아니라 적용 허브이며, 상태 owner가 씬 전환 뒤 다시 `Apply(...)` 합니다.
- 쌍무기 구조는 `RuntimeData / Processor / Coordinator / Interaction Layer` 경계를 기준으로 확장합니다.
- HUD와 tooltip은 상태를 소유하지 않고, 현재 활성 상태를 projection 해서 표시만 합니다.

## 문서를 고르는 법

### 무기와 GAS가 어떻게 연결되는지 알고 싶다
- [Gameplay Ability Weapon Architecture](./GameplayAbilityWeaponArchitecture.md)

### 지금 무기-GAS 구조가 어디까지 검증됐는지 보고 싶다
- [Weapon Cleanup Contract](./WeaponCleanupContract.md)
- legacy 검토 문서는 아래 `Legacy / Review` 섹션을 보세요.

### 무기 교체/강제 종료/씬 전환 때 cleanup 규칙을 보고 싶다
- [Weapon Cleanup Contract](./WeaponCleanupContract.md)

### 무기 런타임 상태 저장/복원이 필요한지 판단하고 싶다
- [Runtime Save Architecture](./RuntimeSaveArchitecture.md)
- legacy 검토 문서는 아래 `Legacy / Review` 섹션을 보세요.

### 전투 피해/피격/속성 게이지를 수정하고 싶다
- [Combat Architecture](./CombatArchitecture.md)
- 일반 몬스터 cleanup 기준까지 보고 싶으면
  - [Mob Cleanup Contract](./MobCleanupContract.md)
- 전투 presentation authoring 기준까지 보고 싶으면
  - [Presentation Authoring Contract](./PresentationAuthoringContract.md)

### 보스 FSM, 패턴, 등장/처치 연출을 수정하고 싶다
- [Boss Encounter Architecture](./BossEncounterArchitecture.md)
- pattern-specific presentation이 AL에 있어야 하는지, 상태/연출 객체에 남아야 하는지 판단하려면
  - [Presentation Authoring Contract](./PresentationAuthoringContract.md)
- legacy 연결/검토 문서는 아래 `Legacy / Review` 섹션을 보세요.

### 새 일반 몬스터를 현재 FSM 표준 구조로 만들고 싶다
- [General Mob FSM Authoring Guide](./GeneralMobFSMAuthoringGuide.md)
  - 인스펙터 validator와 제작 체크리스트 포함
- 패턴 실행 데이터가 어디에 있어야 하는지 점검하고 싶으면
  - [Pattern Data Ownership Review](./PatternDataOwnershipReview.md)
    - `ShadowServant / StrangeCandlestick / DeadsSkeleton` AL 이전 및 fallback 제거 반영
- warning / telegraph / hit presentation이 AL 소유인지 상태 소유인지 판단하려면
  - [Presentation Authoring Contract](./PresentationAuthoringContract.md)

### 미연시/대화/NPC/호감도 시스템을 수정하고 싶다
- [Dialogue Architecture](./DialogueArchitecture.md)

### 씬 이동 시 플레이어/장비/GAS 상태 저장 복원을 수정하고 싶다
- [Runtime Save Architecture](./RuntimeSaveArchitecture.md)

### 플레이어 버프/디버프/환경 상태와 상태 HUD 구조를 수정하고 싶다
- [Gameplay Status Architecture](./GameplayStatusArchitecture.md)
- [Gameplay Debuff Application Architecture](./GameplayDebuffApplicationArchitecture.md)
- [Gameplay Buff / Debuff Architecture](./GameplayBuffDebuffArchitecture.md)
- legacy 검토 문서는 아래 `Legacy / Review` 섹션을 보세요.

### 새 무기를 같은 구조로 만들고 싶다
- [Gameplay Ability Weapon Architecture](./GameplayAbilityWeaponArchitecture.md)
- [Eclipse Sword Pattern Guide](../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/EclipseSwordPatternGuide.md)

### 두 무기가 서로 상태를 읽고 능력이 바뀌는 구조를 만들고 싶다
- [Gameplay Ability Weapon Architecture](./GameplayAbilityWeaponArchitecture.md)
- [Dual Weapon Pattern Guide](./DualWeaponPatternGuide.md)
- legacy 검토 문서는 아래 `Legacy / Review` 섹션을 보세요.

### 월식도가 왜 기준 사례인지 보고 싶다
- [Eclipse Sword Pattern Guide](../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/EclipseSwordPatternGuide.md)

### 지금 구조에서 어떤 책임이 어디에 있는지 빠르게 확인하고 싶다
- [Gameplay Ability Weapon Architecture](./GameplayAbilityWeaponArchitecture.md)
- [Combat Architecture](./CombatArchitecture.md)
- [Boss Encounter Architecture](./BossEncounterArchitecture.md)
- [Dialogue Architecture](./DialogueArchitecture.md)
- [Runtime Save Architecture](./RuntimeSaveArchitecture.md)

## Legacy / Review

- [AI / FSM Ability Integration Review](./AIFSMAbilityIntegrationReview.md)
- [Mob AI Architecture Direction Review](./MobAIArchitectureDirectionReview.md)
- [Pattern Data Ownership Review](./PatternDataOwnershipReview.md)
- [Personalized BT + Ability Structure Proposal](./PersonalizedBTAbilityStructureProposal.md)
- [Player Status Direction Review](./PlayerStatusDirectionReview.md)
- [Weapon GAS Assessment](./WeaponGASAssessment.md)
- [Weapon Runtime State Save Review](./WeaponRuntimeStateSaveReview.md)

## Legacy / Notes

- [current-project-context](./current-project-context.md)
- [global-services-plan](./global-services-plan.md)
- [next-thread-handoff-loading-presentation](./next-thread-handoff-loading-presentation.md)
- [prototype-notes](./prototype-notes.md)
- [system-notes](./system-notes.md)

## 문서 확장 원칙

- 최상위 문서는 짧게 유지합니다.
- 상위 문서는 "무슨 일을 하려면 어디로 가야 하는가"만 안내합니다.
- 실제 구조 설명은 중간 문서에 둡니다.
- 실제 구현 예시는 개별 사례 문서에 둡니다.
