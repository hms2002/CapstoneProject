---
status: active
authority: guide
category: guide
last_reviewed: 2026-05-05
---

# Dual Weapon Pattern Guide

이 문서는 **두 무기가 서로의 상태를 읽고, 소비하고, 다시 영향을 돌려주는 구조**를 만들 때 참고하는 기준 사례 문서입니다.

현재 기준 사례는 두 단계로 정리됩니다.

- **표식검 + 처형총**
  - 쌍무기 왕복 참조의 최소 성공 패턴
- **태양도 + 월영도**
  - 실제 제작형 쌍무기 패턴

## 왜 별도 문서가 필요한가

단일 무기 구조와 쌍무기 구조의 차이는 분명합니다.

- 현재 무기 상태만 보면 안 됩니다.
- 비활성 슬롯 상태도 공식 문맥에 들어와야 합니다.
- 한 무기의 결과가 다른 무기의 능력 선택을 바꿉니다.
- 시간 경과 규칙도 활성 슬롯에만 묶이면 안 됩니다.

즉 쌍무기부터는

- `WeaponRuntimeData`
- `WeaponRuntimeProcessor`
- `WeaponRuntimeCoordinator`
- 확장된 `WeaponSelectionContext`
- `WeaponInteractionLayer`
- `WeaponPairInteractionRule`

가 함께 움직여야 합니다.

## 기준 사례: 표식검 + 처형총

### 표식검

- 기본 공격 적중 시 `markStacks` 증가
- 시간 경과로 표식 감소
- 총이 `Execution Shot`을 쓰면 검의 표식이 소비됨
- 총이 연 `Rebound` 창이 살아 있으면 `Skill1`이 `Rebound Slash`로 바뀜

### 처형총

- 기본 공격은 검의 표식 수를 읽음
- 표식이 충분하면 `Execution Shot`
- `Execution Shot` 성공 시
  - 검의 표식을 소비
  - 총 쪽에 `Rebound` 창 개방
- 시간 경과로 `Rebound` 창 만료

즉 구조는 이렇게 됩니다.

1. 검이 상태 생성
2. 총이 그 상태를 읽어 다른 AD 선택
3. 총이 상태 소비
4. 총의 결과가 다시 검 스킬 선택에 영향

이게 쌍무기 왕복 참조의 최소 성공 패턴입니다.

## 실제 제작형 사례: 태양도 + 월영도

### 태양도

- 실제 공격 적중 시 `heatStacks` 증가
- 월영도의 `coldStacks`를 읽어 `Attack`이 `BaseAttack / HeatedAttack`으로 바뀜
- 양쪽 스택이 충분하면 `Skill1`이 `SolarFinishStarter`로 바뀜

### 월영도

- 실제 공격 적중 시 `coldStacks` 증가
- 태양도의 `heatStacks`를 읽어 `Attack`이 `BaseAttack / FrostedAttack`으로 바뀜
- 양쪽 스택이 충분하면 `Skill1`이 `LunarFinishStarter`로 바뀜

### 공명 피니시

- 둘 중 하나의 피니시 시동기가 성공하면
  - `SunMoonInteractionRule`
  - `WeaponRuntimeCoordinator`
를 통해 양쪽 스택이 함께 소모됩니다.

즉 태양도 + 월영도는 다음을 같이 검증한 사례입니다.

1. 비활성 슬롯 persistent state 읽기
2. 반대 슬롯 상태에 따른 일반 공격 변경
3. 반대 슬롯 상태에 따른 스킬 변경
4. processor 기반 비활성 슬롯 시간 경과 감쇠
5. pair rule 기반 양방향 상태 소비

## 현재 코드 위치

### persistent state

- [MarkSwordRuntimeData.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Data/MarkSwordRuntimeData.cs)
- [ExecutionGunRuntimeData.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Data/ExecutionGunRuntimeData.cs)

### 시간 경과 규칙

- [MarkSwordRuntimeProcessor.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Data/MarkSwordRuntimeProcessor.cs)
- [ExecutionGunRuntimeProcessor.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Data/ExecutionGunRuntimeProcessor.cs)
- [WeaponRuntimeCoordinator.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Data/WeaponRuntimeCoordinator.cs)

### 선택 규칙

- [MarkSwordSelectionStrategy.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/MarkSwordSelectionStrategy.cs)
- [ExecutionGunSelectionStrategy.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/ExecutionGunSelectionStrategy.cs)
- [WeaponSelectionContext.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/WeaponSelectionContext.cs)

### 장착 중 live adapter

- [MarkSwordRuntimeState.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/MarkSwordRuntimeState.cs)
- [ExecutionGunRuntimeState.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/ExecutionGunRuntimeState.cs)

### 쌍무기 상호작용 계층

- [WeaponInteractionLayer.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Interactions/WeaponInteractionLayer.cs)
- [WeaponPairInteractionRule.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Interactions/WeaponPairInteractionRule.cs)
- [MarkSwordExecutionGunInteractionRule.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Interactions/MarkSwordExecutionGunInteractionRule.cs)
- [SunMoonInteractionRule.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Interactions/SunMoonInteractionRule.cs)

### 실제 제작형 runtime data / processor / strategy

- [SunBladeRuntimeData.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Data/SunBladeRuntimeData.cs)
- [MoonBladeRuntimeData.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Data/MoonBladeRuntimeData.cs)
- [SunBladeRuntimeProcessor.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Data/SunBladeRuntimeProcessor.cs)
- [MoonBladeRuntimeProcessor.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Data/MoonBladeRuntimeProcessor.cs)
- [SunBladeSelectionStrategy.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/SunBladeSelectionStrategy.cs)
- [MoonBladeSelectionStrategy.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/MoonBladeSelectionStrategy.cs)
- [SunBladeRuntimeState.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/SunBladeRuntimeState.cs)
- [MoonBladeRuntimeState.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/MoonBladeRuntimeState.cs)

## 책임 분리

### `WeaponRuntimeData`

상태만 가집니다.

예:
- 표식 스택 수
- 표식 감쇠 남은 시간
- 반격 창 개방 여부
- 반격 창 남은 시간

여기에 `Tick()`을 넣지 않습니다.

### `WeaponRuntimeProcessor`

시간 경과 규칙만 가집니다.

예:
- 표식이 5초 후 1개 감소
- 반격 창이 6초 후 닫힘

즉 data는 "기억", processor는 "시간 규칙"입니다.

### `WeaponRuntimeCoordinator`

전체 슬롯을 순회합니다.

- 현재 슬롯이든 비활성 슬롯이든 같은 규칙으로 processor를 호출
- 현재 슬롯/반대 슬롯 문맥을 processor에 제공
- interaction layer가 요청한 상태 변경을 올바른 슬롯 owner에게 반영

이렇게 해야 비활성 무기도 시간 경과 상태가 자연스럽게 유지됩니다.

### `WeaponSelectionContext`

이제 최소 문맥은 아래를 포함합니다.

- 현재 무기
- 현재 슬롯 index
- 현재 runtime data
- 현재 live runtime state
- 반대 슬롯 무기
- 반대 슬롯 runtime data

쌍무기부터는 이 정도가 기본 문맥입니다.

### `WeaponAbilitySelectionStrategy`

읽기만 담당합니다.

예:
- 총 전략이 검의 `markStacks`를 읽어 `Base Shot / Execution Shot` 선택
- 검 전략이 총의 `reboundSlashReady`를 읽어 `Default Skill / Rebound Slash` 선택

전략은 상태를 바꾸지 않습니다.

### `WeaponAbilityRuntimeState`

장착 중 프리팹 live adapter입니다.

예:
- 실제 `HitConfirm`을 받아 표식 증가
- `Execution Shot` 성공 사실을 interaction layer에 전달
- `Rebound Slash` 소비 사실을 interaction layer에 전달

즉 상태 변경은 실행 후 경로에서 일어납니다.

### `WeaponInteractionLayer`

쌍무기 상호작용의 진입점입니다.

- runtime state가 올린 사실을 받음
- 현재 조합에 맞는 pair rule 호출
- runtime state가 pair rule 구체 타입을 모르도록 경계 제공

### `WeaponPairInteractionRule`

조합별 전투 문법을 해석합니다.

예:
- 총의 `Execution Shot` 성공
  - 검 표식 소비
  - 총 반격 창 개방
- 검의 `Rebound Slash` 소비
  - 총 반격 창 닫기

중요한 점은, rule이 직접 슬롯 배열을 만지지 않고 coordinator를 통해 반영을 요청한다는 것입니다.

## 설계 규칙

### 1. 각 무기는 자기 상태를 계속 소유한다

공유 상태 구조를 처음부터 만들지 않습니다.

- 검 데이터는 검이 소유
- 총 데이터는 총이 소유

처음엔 서로 읽기만 하게 두는 게 훨씬 단순합니다.

### 2. 반대 슬롯에 공개할 값은 최소화한다

좋은 공개 값:
- `markStacks`
- `reboundSlashReady`

나쁜 공개 값:
- 내부 타이머 전부
- 구현 세부 플래그 다수

다른 무기가 꼭 알아야 하는 의미 있는 값만 읽게 합니다.

### 3. 상태 변경은 실행 후 경로에서

전략은 읽기만.

상태 변경은:
- `HandleAbilityActivated`
- `HandleGameplayEvent`
- `Executor`
- `Processor`
- `InteractionLayer / PairRule`

에서만 합니다.

### 3-1. 다른 무기 상태는 직접 쓰지 않는다

runtime state나 strategy가 반대 슬롯 data를 직접 수정하지 않습니다.

- 읽기
  - 허용
- 쓰기
  - 금지
- 교차 상태 변경
  - `InteractionLayer -> PairRule -> Coordinator` 경유

즉 `A reads B`는 허용되지만, `A writes B directly`는 하지 않습니다.

### 4. 시간 경과는 data가 아니라 processor가

`RuntimeData`에 `Tick()`을 넣지 않습니다.

이건 쌍무기뿐 아니라 앞으로 더 복잡한 무기에서도 중요한 규칙입니다.

## 현재 검증 범위

1. 반대 슬롯 runtime data를 읽어 AD 선택
2. 비활성 슬롯 상태도 선택 문맥에 참여
3. 한 무기가 만든 상태를 다른 무기가 소비
4. 소비 결과가 다시 원래 무기 선택에 영향
5. 시간 경과 감쇠/만료가 비활성 슬롯에서도 진행
6. direct cross-write 없이 interaction layer 경유로 교차 상태 반영
7. 실제 제작형 쌍무기에서도 일반 공격과 스킬 변경이 같은 틀로 동작
8. pair rule이 실제 제작 무기의 "양쪽 스택 동시 소비"를 자연스럽게 처리

## 다음 쌍무기 설계 시 체크리스트

1. 각 슬롯이 소유할 `RuntimeData`는 무엇인가
2. 반대 슬롯이 읽어야 하는 최소 공개 값은 무엇인가
3. 시간 경과 규칙이 필요한가
4. 그 규칙은 `Processor`로 뺄 수 있는가
5. 상태 소비는 어떤 실행 후 경로에서 일어나는가
6. cleanup이 다른 슬롯 상태를 불필요하게 지우지 않는가

## 한 줄 요약

쌍무기 구조의 핵심은 **공유 상태를 억지로 만들기보다, 각 슬롯의 `WeaponRuntimeData`를 유지한 채 `SelectionContext`와 실행 후 경로에서 서로 참조하게 하고, 교차 상태 변경은 `InteractionLayer -> PairRule -> Coordinator` 경유로만 반영하는 것**입니다.

태양도 + 월영도 사례까지 포함하면, 이 구조는 이제 실험용 샘플을 넘어 **실제 제작 무기 수준의 상호참조와 비활성 슬롯 감쇠 규칙도 감당할 수 있는 패턴**으로 볼 수 있습니다.
