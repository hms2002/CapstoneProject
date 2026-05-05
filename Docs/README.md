# Project Docs Guide

이 문서는 "무엇을 하려는지" 기준으로 먼저 읽을 문서를 고르는 빠른 라우터입니다.
세부 규칙은 각 문서에 두고, 이 문서는 짧게 유지합니다.

## Quick Start

- 새 무기 / GAS 구조: [Gameplay Ability Weapon Architecture](./GameplayAbilityWeaponArchitecture.md)
- 무기 cleanup / 취소 / 씬 전환: [Weapon Cleanup Contract](./WeaponCleanupContract.md)
- 전투 피해 / 피격 / 속성 게이지: [Combat Architecture](./CombatArchitecture.md)
- 보스 FSM / 패턴 / 등장 / 처치 연출: [Boss Encounter Architecture](./BossEncounterArchitecture.md)
- 일반 몬스터 FSM 제작: [General Mob FSM Authoring Guide](./GeneralMobFSMAuthoringGuide.md)
- 일반 몬스터 cleanup: [Mob Cleanup Contract](./MobCleanupContract.md)
- 패턴 데이터 소유 위치: [Pattern Data Ownership Review](./PatternDataOwnershipReview.md)
- 연출 authoring 기준: [Presentation Authoring Contract](./PresentationAuthoringContract.md)
- 런타임 저장 / 복원: [Runtime Save Architecture](./RuntimeSaveArchitecture.md)
- 미연시 / 대화 / NPC / 호감도: [Dialogue Architecture](./DialogueArchitecture.md)
- 상태 HUD / 버프 / 디버프: [Gameplay Status Architecture](./GameplayStatusArchitecture.md)

## Work Guide

- Codex나 다른 작업자가 코드를 수정할 때는 루트 [AGENTS.md](../AGENTS.md)를 먼저 봅니다.
- 새 구조를 만들 때는 이 README보다 각 architecture / contract 문서를 우선합니다.
- 구현이 문서와 달라졌다면, 코드 수정과 같은 PR/작업 단위에서 문서도 같이 갱신합니다.

## Document Authority

1. 현재 코드와 프리팹/씬 설정
2. `*Architecture.md`, `*Contract.md`, `*AuthoringGuide.md`
3. 현재 README
4. `*Review.md`, proposal, legacy notes

`Review`, proposal, notes 문서는 설계 과정의 기록입니다. 최종 규칙처럼 쓰기 전에 현재 architecture / contract 문서와 코드 상태를 확인합니다.

## Core References

- [Gameplay Ability Weapon Architecture](./GameplayAbilityWeaponArchitecture.md)
- [Combat Architecture](./CombatArchitecture.md)
- [Boss Encounter Architecture](./BossEncounterArchitecture.md)
- [General Mob FSM Authoring Guide](./GeneralMobFSMAuthoringGuide.md)
- [Presentation Authoring Contract](./PresentationAuthoringContract.md)
- [Runtime Save Architecture](./RuntimeSaveArchitecture.md)
- [Dialogue Architecture](./DialogueArchitecture.md)

## Case Guides

- [Dual Weapon Pattern Guide](./DualWeaponPatternGuide.md)
- [Eclipse Sword Pattern Guide](../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/EclipseSwordPatternGuide.md)
- [Pattern Data Ownership Review](./PatternDataOwnershipReview.md)

## Review / Legacy Notes

- [AI / FSM Ability Integration Review](./AIFSMAbilityIntegrationReview.md)
- [Mob AI Architecture Direction Review](./MobAIArchitectureDirectionReview.md)
- [Personalized BT + Ability Structure Proposal](./PersonalizedBTAbilityStructureProposal.md)
- [Player Status Direction Review](./PlayerStatusDirectionReview.md)
- [Weapon GAS Assessment](./WeaponGASAssessment.md)
- [Weapon Runtime State Save Review](./WeaponRuntimeStateSaveReview.md)
- [current-project-context](./current-project-context.md)
- [global-services-plan](./global-services-plan.md)
- [next-thread-handoff-loading-presentation](./next-thread-handoff-loading-presentation.md)
- [prototype-notes](./prototype-notes.md)
- [system-notes](./system-notes.md)

## Extension Rule

- 상위 README에는 라우팅만 둡니다.
- 책임 경계와 금지 규칙은 architecture / contract 문서에 둡니다.
- 구현 예시는 case guide 또는 코드 가까운 문서에 둡니다.
