# General Mob FSM Authoring Guide

이 문서는 **새 일반 몬스터를 현재 표준 FSM 구조로 제작할 때** 따라갈 최소 기준을 정리합니다.

이 문서의 목적은 두 가지입니다.

- 새 일반 몬스터를 만들 때 어떤 컴포넌트와 책임 구성이 필요한지 빠르게 안내한다.
- 일반 몬스터가 `MobStateMachine + decision source + bridge` 구조를 타도록 authoring 기준을 고정한다.

관련 문서:

- [Combat Architecture](./CombatArchitecture.md)
- [Boss Encounter Architecture](./BossEncounterArchitecture.md)
- [Gameplay Buff / Debuff Architecture](./GameplayBuffDebuffArchitecture.md)

---

## 한 줄 구조

현재 일반 몬스터의 표준 구조는 다음처럼 읽습니다.

```text
Mob
-> MobStateMachine
-> IMobAttackDecisionSource
-> IMobAbilityBridge
-> AbilitySystem
-> AbilityLogic / Runner
```

즉:

- 상위 상태 관리는 `Mob`의 공통 FSM
- 몬스터 고유 공격 판단은 `IMobAttackDecisionSource`
- ASC 연결은 `IMobAbilityBridge`
- 실제 공격 시퀀스는 `AbilityLogic / Runner`

가 맡습니다.

중요:

- 공통화되는 것은 **FSM 엔진과 bridge 계약**입니다.
- 상태 구현 전체를 모든 몬스터가 공유해야 하는 것은 아닙니다.

즉 새 일반 몬스터를 만들 때 기준은:

- **공통 엔진은 재사용**
- **상태 집합은 몬스터가 소유**

입니다.

---

## 현재 표준 상태

일반 몬스터 FSM의 현재 기본 표준 상태는 다음 다섯 가지입니다.

- `Idle`
- `Chase`
- `Attack`
- `Recover`
- `Stagger`

상태 의미는 다음과 같습니다.

- `Idle`
  - 감지 전 대기 상태
- `Chase`
  - 감지된 타깃을 추적하는 상태
- `Attack`
  - helper가 만든 공격 요청을 bridge로 실행하는 상태
- `Recover`
  - 공격 직후 짧은 후딜/템포 조절 상태
- `Stagger`
  - `Groggy` 같은 전역 제압 상태를 소비하는 상태

이 다섯 가지는 **모든 몬스터가 반드시 같은 구현체를 공유해야 한다는 뜻이 아니라, 현재 공통 엔진이 제공하는 기본 상태 집합**으로 이해하는 편이 맞습니다.

즉:

- 단순 몬스터는 이 기본 상태 집합만 써도 되고
- 특수 몬스터는 자기 전용 상태를 추가로 가질 수 있습니다

대표 사례:

- [DeadsSkeleton.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Dead'sSkeleton/DeadsSkeleton.cs)
  - 자폭 리듬을 위해 공통 공격 상태 대신 몬스터 전용 공격 상태를 선택할 수 있게 확장됨

---

## 필수 구성 요소

새 일반 몬스터를 현재 표준 구조로 제작하려면, 기본적으로 아래 구성 요소를 갖추는 편이 좋습니다.

### 1. 몬스터 본체

- [Mob.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Mob.cs) 상속

역할:

- 공통 `MobStateMachine` 초기화
- `IMobAbilityBridge` 해석
- `IMobAttackDecisionSource` 해석
- 공통 `Idle / Chase / Attack / Recover / Stagger` 상태 생명주기 실행

### 2. ASC 연결 창구

- [MobAbilityCoordinator.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/MobAbilityCoordinator.cs)

역할:

- `AbilitySystem` 실행/취소 연결
- runner busy와 ASC busy를 합친 공통 busy 상태 제공
- `Groggy` 같은 실행 금지 상태를 공통 규칙으로 해석

즉 이 컴포넌트는 일반 몬스터용 `IMobAbilityBridge`의 대표 구현입니다.

### 3. 공격 판단 source

- [IMobAttackDecisionSource.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/FSM/IMobAttackDecisionSource.cs) 구현체

역할:

- 지금 실행할 공격 요청을 만든다
- `MobAttackState` 진입/종료 훅을 받는다

대표 사례:

- [ShadowServant.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/ShadowServant/ShadowServant.cs)
- [StrangeCandlestick.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/StrangeCandlestick/StrangeCandlestick.cs)
- [TackleAttack.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/TackleAttack.cs)
- [DeadsSkeleton.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Dead'sSkeleton/DeadsSkeleton.cs)

### 4. 추적 intent

- `IEnemyChaseIntent` 구현체
- 기본 구현: [EnemyChaseIntent2D.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/EnemyChaseIntent2D.cs)

역할:

- 추적 시작/중지 생명주기 제공
- 감지 범위 판단 제공
- 실제 이동 intent 생성

중요:

- FSM은 이제 구체 타입이 아니라 `IEnemyChaseIntent`를 봅니다.
- 즉 몬스터별로 다른 chase 구현을 붙일 여지가 열려 있습니다.

### 5. 필요 시 pattern runner

예:

- [ShadowServantAttackRunner.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/ShadowServant/ShadowServantAttackRunner.cs)
- [StrangeCandlestickAttackRunner.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/StrangeCandlestick/StrangeCandlestickAttackRunner.cs)

역할:

- 공격 연출/딜레이/투사체/안개 생성 같은 긴 실행 시퀀스 수행

---

## 제작 순서

새 일반 몬스터를 만들 때 권장 순서는 다음과 같습니다.

### 1. `Mob` 상속 본체를 만든다

예:

- `MyNewMob : Mob`

여기서는:

- 기본 속성
- 애니메이션
- 필요 시 기즈모

정도만 담당하게 두는 편이 좋습니다.

### 2. 공격 판단 source를 정한다

둘 중 하나로 갑니다.

- 몬스터 본체가 직접 `IMobAttackDecisionSource` 구현
- 별도 helper 컴포넌트가 `IMobAttackDecisionSource` 구현

기준:

- 공격 문법이 몬스터 본체와 강하게 붙어 있으면 본체 구현
- 태클처럼 별도 helper가 더 자연스러우면 helper 구현

### 3. `MobAbilityCoordinator`를 붙인다

이 컴포넌트가 있어야:

- FSM이 공통 `IMobAbilityBridge`를 찾고
- `AttackState`가 ASC 실행을 시작할 수 있습니다

### 4. chase intent를 붙인다

기본값은 [EnemyChaseIntent2D.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/EnemyChaseIntent2D.cs)입니다.

이 컴포넌트는:

- `ChaseState.Enter()`에서 시작
- `ChaseState.Exit()`에서 중지

되므로, 추적 이동이 FSM 상태 생명주기에 맞춰 움직입니다.

### 5. 필요한 ability와 runner를 연결한다

공격 방식에 따라:

- `AbilityDefinition`을 준비하고 ASC에 등록
- 긴 실행이 필요하면 runner를 연결

합니다.

---

## `IMobAttackDecisionSource` 작성 기준

현재 표준에서 가장 중요한 규칙은 이겁니다.

- 공통 FSM 엔진은 상태 생명주기만 알아야 한다
- `MobAttackState`는 공격 종류를 몰라야 한다
- `IMobAttackDecisionSource`가 공격 선택과 문맥 구성을 맡아야 한다

즉 `TryBuildAttackRequest(...)` 안에서:

- 어떤 `AbilityDefinition`을 쓸지
- 누구를 명시 타깃으로 줄지
- 공격 후 `RecoverSeconds`를 얼마 줄지

를 정합니다.

예:

```text
TryBuildAttackRequest(out request)
-> 현재 조건에 맞는 AbilityDefinition 선택
-> explicitTarget 설정
-> recoverSeconds 설정
-> request 반환
```

### `MobAttackRequest`에 담는 것

- [MobAttackRequest.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/FSM/MobAttackRequest.cs)

현재 최소 구성:

- `Ability`
- `ExplicitTarget`
- `RecoverSeconds`

즉 이 요청은 **FSM이 세부 문맥을 해석하지 않고 bridge에 전달할 최소 실행 패키지**입니다.

---

## helper / source가 맡아야 하는 것

공격 판단 source나 helper는 아래를 맡는 편이 좋습니다.

- 공격 가능 여부 판단
- 공격 우선순위 판단
- 여러 AD 중 지금 실행할 AD 선택
- `MobAttackRequest` 생성
- 필요 시 `OnAttackStateEntered / Exited` 훅 대응

### helper / source가 직접 맡지 않는 것

- ASC 직접 실행 구현 세부
- `AbilitySystem` 직접 호출
- `TagSystem` 직접 해석
- 긴 실행 시퀀스 자체

이것들은 각각:

- bridge
- runner / logic

으로 분리하는 편이 좋습니다.

---

## `Chase` authoring 기준

현재 표준에서는 `Chase`가 단순 상태 이름이 아니라,
**추적 이동의 생명주기를 실제로 관리하는 상태**입니다.

즉:

- `MobChaseState.Enter()` -> `StartChase()`
- `MobChaseState.Exit()` -> `StopChase()`

따라서 새 chase 구현체는 최소한 아래 계약을 제공해야 합니다.

- `StartChase()`
- `StopChase()`
- `IsTargetWithinDetectionRange()`

중요:

- 추적 이동은 `ChaseState`일 때만 살아 있어야 합니다
- `Attack`, `Recover`, `Stagger`, `Idle`로 나가면 자동으로 멈춰야 합니다

다만 chase의 **구현 방식 자체는 몬스터마다 달라질 수 있습니다.**

즉:

- 엔진은 `IEnemyChaseIntent`만 알고
- 실제 추적 방식은 몬스터별 구현체가 소유

하는 방향이 현재 표준입니다.

---

## `Stagger` authoring 기준

현재 FSM은 `Groggy` 같은 제압 상태를 **소비만** 합니다.

즉:

- 실제 스태거 게이지 누적
- 그로기 효과 적용

은 기존 전투 시스템이 담당하고,
FSM은 `IAIAbilityBridge.IsAbilityExecutionSuppressed`를 통해 결과를 읽습니다.

따라서 새 일반 몬스터가 `Stagger`를 실제로 가지려면:

- `StaggerGaugeSystem`
- `staggeredEffect`
- 필요 시 `StaggerResistanceAttribute`

authoring이 같이 필요합니다.

중요:

- FSM / helper / runner는 `Groggy` 태그를 직접 해석하지 않습니다
- 공통 `bridge`가 해석한 suppression 결과만 소비합니다

---

## 첫 제작 체크리스트

새 일반 몬스터를 만들 때 최소 체크리스트는 이렇습니다.

1. `Mob` 상속 본체가 있는가
2. 같은 오브젝트에 `MobAbilityCoordinator`가 있는가
3. `IMobAttackDecisionSource` 구현체가 있는가
4. chase intent가 있는가
5. ability 등록 경로가 있는가
6. 긴 실행이면 runner가 있는가
7. `RecoverSeconds`가 적절한가
8. `Groggy`를 쓸 몬스터면 `StaggerGaugeSystem` authoring이 되어 있는가

## 에디터 validator

현재 프로젝트에는 일반 몬스터 authoring 누락을 빨리 찾기 위한 1차 에디터 validator가 들어가 있습니다.

관련 코드:

- [CombatAuthoringValidatorEditors.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Editor/CombatAuthoringValidatorEditors.cs)

사용 방법:

- `Enemy` / `Mob` 인스펙터를 열면 하단에
  - `Combat Object Validation`
  - `Mob FSM Validation`
  패널이 표시됩니다.
- 메뉴에서
  - `Tools/Validation/Validate Selected Combat Object`
  를 실행하면 현재 선택한 오브젝트의 검사 결과를 콘솔로 볼 수 있습니다.

현재 1차 validator가 보는 것:

- 공통 전투 오브젝트 필수 구성
  - `AbilitySystem`
  - `TagSystem`
  - `AttributeSet`
  - `GameplayEffectRunner`
- 일반 몬스터 FSM 필수/선택 구성
  - `MobAbilityCoordinator`
  - `IMobAttackDecisionSource`
  - `IEnemyChaseIntent`
  - `StaggerGaugeSystem`
  - `staggeredEffect`
  - `StaggerResistanceAttribute`

즉 문서 체크리스트만 보는 대신, 이제는 에디터가 기본 누락을 바로 알려주는 상태입니다.

--- 

## 현재 기준 사례

현재 일반 몬스터 제작 기준 사례는 다음과 같습니다.

- [ShadowServant.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/ShadowServant/ShadowServant.cs)
  - 본체가 `IMobAttackDecisionSource` 구현
  - 별도 runner 사용
- [StrangeCandlestick.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/StrangeCandlestick/StrangeCandlestick.cs)
  - 본체가 `IMobAttackDecisionSource` 구현
  - 투사체/발사 runner 사용
- [TackleAttack.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/TackleAttack.cs)
  - 별도 helper가 `IMobAttackDecisionSource` 구현
  - 태클 특수 문법 사례
- [ShadowMonster.prefab](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Prefabs/Enemies/Mobs/ShadowMonster.prefab)
  - `Mob` 본체 + `TackleAttack` + `MobAbilityCoordinator` + `EnemyChaseIntent2D` 조합 사례

현재 문서 기준으로 이 사례들을 읽는 방법은 다음과 같습니다.

- `ShadowServant`, `StrangeCandlestick`
  - 기본 상태 집합을 사용하는 몬스터 사례
- `Dead'sSkeleton`
  - 공통 엔진 위에 몬스터 전용 상태를 얹는 확장 사례
- `ShadowMonster`
  - 본체는 얇고 helper/prefab 조합으로 동작하는 사례

---

## 한 줄 결론

새 일반 몬스터를 현재 표준 구조로 만들 때 기준은 다음과 같습니다.

> **`Mob`가 공통 FSM 엔진을 돌리고, 몬스터별 상태/source/helper가 전투 문법을 만들고, `MobAbilityCoordinator`가 ASC에 연결하고, runner/logic이 실제 실행을 맡는다.**

더 짧게 쓰면:

> **상위는 `MobStateMachine`, 중간은 `IMobAttackDecisionSource`, 하위는 `IMobAbilityBridge + AbilityLogic / Runner`**
