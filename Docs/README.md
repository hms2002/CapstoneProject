# Project Docs Guide

이 문서는 "무엇을 하려는지" 기준으로 어디 문서를 먼저 읽어야 하는지 안내하는 빠른 라우터입니다.

## 아키텍처 핵심 원칙

- 무기 상태는 `RuntimeData`가 소유하고, GAS/ASC는 선택된 실행만 담당합니다.
- `PlayerStatusRuntime`은 상태 저장소가 아니라 적용 허브이며, 상태 owner가 씬 전환 뒤 다시 `Apply(...)` 합니다.
- 쌍무기 구조는 `RuntimeData / Processor / Coordinator / Interaction Layer` 경계를 기준으로 확장합니다.
- HUD와 tooltip은 상태를 소유하지 않고, 현재 활성 상태를 projection 해서 표시만 합니다.

## 문서를 고르는 법

### 무기와 GAS가 어떻게 연결되는지 알고 싶다
- 먼저 [Gameplay Ability Weapon Architecture](./GameplayAbilityWeaponArchitecture.md) 로 가세요.

### 지금 무기-GAS 구조가 어디까지 검증됐는지 보고 싶다
- [Weapon GAS Assessment](./WeaponGASAssessment.md) 로 가세요.

### 무기 교체/강제 종료/씬 전환 때 cleanup 규칙을 보고 싶다
- [Weapon Cleanup Contract](./WeaponCleanupContract.md) 로 가세요.

### 무기 런타임 상태 저장/복원이 필요한지 판단하고 싶다
- [Weapon Runtime State Save Review](./WeaponRuntimeStateSaveReview.md) 로 가세요.

### 전투 피해/피격/속성 게이지를 수정하고 싶다
- [Combat Architecture](./CombatArchitecture.md) 로 가세요.

### 보스 FSM, 패턴, 등장/처치 연출을 수정하고 싶다
- [Boss Encounter Architecture](./BossEncounterArchitecture.md) 로 가세요.
- AI/FSM가 GAS/AbilitySystem과 현재 어떻게 연결되어 있는지, 어디가 direct call인지 먼저 보고 싶다면 [AI / FSM Ability Integration Review](./AIFSMAbilityIntegrationReview.md) 도 같이 보세요.

### 미연시/대화/NPC/호감도 시스템을 수정하고 싶다
- [Dialogue Architecture](./DialogueArchitecture.md) 로 가세요.

### 씬 이동 시 플레이어/장비/GAS 상태 저장 복원을 수정하고 싶다
- [Runtime Save Architecture](./RuntimeSaveArchitecture.md) 로 가세요.

### 플레이어 버프/디버프/환경 상태와 상태 HUD 구조를 수정하고 싶다
- [Gameplay Status Architecture](./GameplayStatusArchitecture.md) 로 가세요.
- [Gameplay Debuff Application Architecture](./GameplayDebuffApplicationArchitecture.md) 도 같이 보면, 플레이어 디버프 적용 경로 표준을 바로 볼 수 있습니다.
- [Gameplay Buff / Debuff Architecture](./GameplayBuffDebuffArchitecture.md) 를 보면, GE 중심 버프/디버프 구조와 무기/유물 상태와의 경계를 함께 볼 수 있습니다.

### 새 무기를 같은 구조로 만들고 싶다
- 먼저 [Gameplay Ability Weapon Architecture](./GameplayAbilityWeaponArchitecture.md) 를 읽고,
- 그다음 월식도 기준 사례인 [Eclipse Sword Pattern Guide](../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/EclipseSwordPatternGuide.md) 로 내려가세요.
- 현재 구조의 검증 범위를 빠르게 보고 싶으면 [Weapon GAS Assessment](./WeaponGASAssessment.md) 도 같이 보세요.

### 두 무기가 서로 상태를 읽고 능력이 바뀌는 구조를 만들고 싶다
- 먼저 [Gameplay Ability Weapon Architecture](./GameplayAbilityWeaponArchitecture.md) 에서 `WeaponRuntimeData / Processor / Coordinator / SelectionContext` 확장 부분을 보세요.
  - 구조 원칙만 확인하고 싶으면 여기서 멈춰도 됩니다.
- 현재 검증 범위는 [Weapon GAS Assessment](./WeaponGASAssessment.md) 에 반영되어 있습니다.
  - 지금 구조가 어디까지 버티는지 보고 싶으면 여기까지 보면 됩니다.
- 기준 사례는 [Dual Weapon Pattern Guide](./DualWeaponPatternGuide.md) 로 내려가면 됩니다.
  - 실제로 복사해서 시작할 기준 사례가 필요하면 이 문서까지 내려가세요.
  - 최소 성공 패턴은 `표식검 + 처형총`
  - 실제 제작형 기준 사례는 `태양도 + 월영도`

### 월식도가 왜 기준 사례인지 보고 싶다
- 바로 [Eclipse Sword Pattern Guide](../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/EclipseSwordPatternGuide.md) 로 가세요.

### 지금 구조에서 어떤 책임이 어디에 있는지 빠르게 확인하고 싶다
- [Gameplay Ability Weapon Architecture](./GameplayAbilityWeaponArchitecture.md) 를 보세요.
- 전투 쪽이면 [Combat Architecture](./CombatArchitecture.md),
- 보스 쪽이면 [Boss Encounter Architecture](./BossEncounterArchitecture.md),
- 대화/NPC 쪽이면 [Dialogue Architecture](./DialogueArchitecture.md),
- 저장/복원 쪽이면 [Runtime Save Architecture](./RuntimeSaveArchitecture.md) 를 보세요.

## 현재 기준 사례

- 월식도
  - 상태는 RuntimeState가 가진다.
  - AD 참조는 Loadout이 가진다.
  - 선택 규칙은 Strategy가 가진다.
  - ASC/GAS는 선택된 AD 실행만 맡는다.
- 표식검 + 처형총
  - 슬롯별 RuntimeData를 서로 읽는다.
  - 비활성 슬롯 상태도 선택 규칙에 참여한다.
  - 시간 경과 규칙은 Processor/Coordinator가 맡는다.
- 태양도 + 월영도
  - 실제 제작 무기 수준에서 반대 슬롯 스택이 일반 공격과 `Skill1`을 바꾼다.
  - pair rule이 양쪽 스택 동시 소비를 해석한다.
  - 비활성 슬롯 감쇠가 실제 제작 무기에서도 그대로 유지된다.
- 상태 HUD / 플레이어 상태
  - `PlayerStatusRuntime`은 상태 저장소가 아니라 적용 허브다.
  - 상태 owner가 씬 전환 뒤 다시 `Apply(...)`하는 재등록 모델을 기준으로 한다.
  - `시야 제한`은 환경 상태가 HUD/tooltip에 올라가는 기준 사례다.
  - `ShadowFog`는 `CombatBuffDebuffApplier`를 통해 GE와 HUD를 함께 동기화하는 첫 몬스터 디버프 구현 사례다.
  - 유물 상태는 proc owner가 직접 `Apply/UpdateStatus/Release`를 맡는다.
    - 시간형 기준 사례는 `Move Speed On Kill`, `Move Speed On Damaged`
    - 스택형 기준 사례는 `Move Speed Stack On Critical Hit`

## 문서 확장 원칙

- 최상위 문서는 짧게 유지합니다.
- 상위 문서는 "무슨 일을 하려면 어디로 가야 하는가"만 안내합니다.
- 실제 구조 설명은 중간 문서에 둡니다.
- 실제 구현 예시는 개별 사례 문서에 둡니다.
