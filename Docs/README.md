---
status: active
authority: docs-router
category: router
last_reviewed: 2026-05-05
---

# Project Docs Guide

이 문서는 "무엇을 하려는지" 기준으로 어디 문서를 먼저 읽어야 하는지 안내하는 빠른 라우터입니다.

## 작업 전 기본 읽기 순서

1. [`CurrentTask.md`](./CurrentTask.md)
2. [`ErrorLog.md`](./ErrorLog.md)
3. [`DecisionLog.md`](./DecisionLog.md)
4. 이 README에서 작업 목적에 맞는 문서

## 문서 권위 순서

문서 내용이 충돌하면 아래 순서를 우선합니다.

1. 현재 사용자 지시
2. [`CurrentTask.md`](./CurrentTask.md)
3. [`Contracts/`](./Contracts/)
4. [`Architecture/`](./Architecture/)
5. [`Guides/`](./Guides/)
6. [`Reviews/`](./Reviews/)
7. [`Notes/`](./Notes/)
8. [`Handoffs/`](./Handoffs/)

`Reviews`, `Notes`, `Handoffs`는 참고용입니다. 최신 구현 기준은 `Contracts`와 `Architecture`를 우선합니다.

## 아키텍처 핵심 원칙

- 무기 상태는 `RuntimeData`가 소유하고, GAS/ASC는 선택된 실행만 담당합니다.
- `PlayerStatusRuntime`은 상태 저장소가 아니라 적용 허브이며, 상태 owner가 씬 전환 뒤 다시 `Apply(...)` 합니다.
- 쌍무기 구조는 `RuntimeData / Processor / Coordinator / Interaction Layer` 경계를 기준으로 확장합니다.
- HUD와 tooltip은 상태를 소유하지 않고, 현재 활성 상태를 projection 해서 표시만 합니다.

## 문서를 고르는 법

### Codex / Obsidian 문서 기억 시스템을 관리하고 싶다
- [Document Inventory](./Overview/document-inventory.md)
- [Current Project Context](./Overview/current-project-context.md)
- [Decision Log](./DecisionLog.md)
- [Error Log](./ErrorLog.md)

### 게임오버, 보스 결과, 등장/처치 연출을 수정하고 싶다
- [Boss Encounter Architecture](./Architecture/BossEncounterArchitecture.md)
- [Presentation Authoring Contract](./Contracts/PresentationAuthoringContract.md)
- [Current Project Context](./Overview/current-project-context.md)

### 무기와 GAS가 어떻게 연결되는지 알고 싶다
- [Gameplay Ability Weapon Architecture](./Architecture/GameplayAbilityWeaponArchitecture.md)

### 지금 무기-GAS 구조가 어디까지 검증됐는지 보고 싶다
- [Weapon Cleanup Contract](./Contracts/WeaponCleanupContract.md)
- legacy 검토 문서는 아래 `Reviews` 섹션을 보세요.

### 무기 교체/강제 종료/씬 전환 때 cleanup 규칙을 보고 싶다
- [Weapon Cleanup Contract](./Contracts/WeaponCleanupContract.md)

### 무기 런타임 상태 저장/복원이 필요한지 판단하고 싶다
- [Runtime Save Architecture](./Architecture/RuntimeSaveArchitecture.md)
- legacy 검토 문서는 아래 `Reviews` 섹션을 보세요.

### 전투 피해/피격/속성 게이지를 수정하고 싶다
- [Combat Architecture](./Architecture/CombatArchitecture.md)
- 일반 몬스터 cleanup 기준까지 보고 싶으면
  - [Mob Cleanup Contract](./Contracts/MobCleanupContract.md)
- 전투 presentation authoring 기준까지 보고 싶으면
  - [Presentation Authoring Contract](./Contracts/PresentationAuthoringContract.md)

### 보스 FSM, 패턴, 등장/처치 연출을 수정하고 싶다
- [Boss Encounter Architecture](./Architecture/BossEncounterArchitecture.md)
- pattern-specific presentation이 AL에 있어야 하는지, 상태/연출 객체에 남아야 하는지 판단하려면
  - [Presentation Authoring Contract](./Contracts/PresentationAuthoringContract.md)
- legacy 연결/검토 문서는 아래 `Reviews` 섹션을 보세요.

### 새 일반 몬스터를 현재 FSM 표준 구조로 만들고 싶다
- [General Mob FSM Authoring Guide](./Guides/GeneralMobFSMAuthoringGuide.md)
  - 인스펙터 validator와 제작 체크리스트 포함
- 패턴 실행 데이터가 어디에 있어야 하는지 점검하고 싶으면
  - [Pattern Data Ownership Review](./Reviews/PatternDataOwnershipReview.md)
    - `ShadowServant / StrangeCandlestick / DeadsSkeleton` AL 이전 및 fallback 제거 반영
- warning / telegraph / hit presentation이 AL 소유인지 상태 소유인지 판단하려면
  - [Presentation Authoring Contract](./Contracts/PresentationAuthoringContract.md)

### 미연시/대화/NPC/호감도 시스템을 수정하고 싶다
- [Dialogue Architecture](./Architecture/DialogueArchitecture.md)

### 씬 이동 시 플레이어/장비/GAS 상태 저장 복원을 수정하고 싶다
- [Runtime Save Architecture](./Architecture/RuntimeSaveArchitecture.md)

### 플레이어 버프/디버프/환경 상태와 상태 HUD 구조를 수정하고 싶다
- [Gameplay Status Architecture](./Architecture/GameplayStatusArchitecture.md)
- [Gameplay Debuff Application Architecture](./Architecture/GameplayDebuffApplicationArchitecture.md)
- [Gameplay Buff / Debuff Architecture](./Architecture/GameplayBuffDebuffArchitecture.md)
- legacy 검토 문서는 아래 `Reviews` 섹션을 보세요.

### 새 무기를 같은 구조로 만들고 싶다
- [Gameplay Ability Weapon Architecture](./Architecture/GameplayAbilityWeaponArchitecture.md)
- [Eclipse Sword Pattern Guide](../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/EclipseSwordPatternGuide.md)

### 두 무기가 서로 상태를 읽고 능력이 바뀌는 구조를 만들고 싶다
- [Gameplay Ability Weapon Architecture](./Architecture/GameplayAbilityWeaponArchitecture.md)
- [Dual Weapon Pattern Guide](./Guides/DualWeaponPatternGuide.md)
- legacy 검토 문서는 아래 `Reviews` 섹션을 보세요.

### 월식도가 왜 기준 사례인지 보고 싶다
- [Eclipse Sword Pattern Guide](../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/EclipseSwordPatternGuide.md)

### 지금 구조에서 어떤 책임이 어디에 있는지 빠르게 확인하고 싶다
- [Gameplay Ability Weapon Architecture](./Architecture/GameplayAbilityWeaponArchitecture.md)
- [Combat Architecture](./Architecture/CombatArchitecture.md)
- [Boss Encounter Architecture](./Architecture/BossEncounterArchitecture.md)
- [Dialogue Architecture](./Architecture/DialogueArchitecture.md)
- [Runtime Save Architecture](./Architecture/RuntimeSaveArchitecture.md)

## Reviews

- [AI / FSM Ability Integration Review](./Reviews/AIFSMAbilityIntegrationReview.md)
- [Mob AI Architecture Direction Review](./Reviews/MobAIArchitectureDirectionReview.md)
- [Pattern Data Ownership Review](./Reviews/PatternDataOwnershipReview.md)
- [Personalized BT + Ability Structure Proposal](./Reviews/PersonalizedBTAbilityStructureProposal.md)
- [Player Status Direction Review](./Reviews/PlayerStatusDirectionReview.md)
- [Weapon GAS Assessment](./Reviews/WeaponGASAssessment.md)
- [Weapon Runtime State Save Review](./Reviews/WeaponRuntimeStateSaveReview.md)

## Notes

- [global-services-plan](./Notes/global-services-plan.md)
- [prototype-notes](./Notes/prototype-notes.md)
- [system-notes](./Notes/system-notes.md)

## Handoffs

- [next-thread-handoff-loading-presentation](./Handoffs/next-thread-handoff-loading-presentation.md)

## 문서 확장 원칙

- 최상위 문서는 짧게 유지합니다.
- 상위 문서는 "무슨 일을 하려면 어디로 가야 하는가"만 안내합니다.
- 실제 구조 설명은 중간 문서에 둡니다.
- 실제 구현 예시는 개별 사례 문서에 둡니다.
- `Architecture`와 `Contracts` 변경은 먼저 제안하고 승인 후 반영합니다.
