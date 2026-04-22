# Pattern Data Ownership Review

이 문서는 **일반 몬스터 패턴 실행에 필요한 데이터가 현재 어디에 놓여 있는지**와,  
**어떤 값이 이미 AL/패턴 데이터의 공식 소유가 되었고 무엇이 상태/owner 쪽에 남는 게 맞는지**를 정리합니다.

현재 기준으로는:

- `ShadowServant`
- `StrangeCandlestick`
- `DeadsSkeleton`

세 몬스터 모두 **명백한 패턴 실행 데이터 이전과 fallback 제거가 완료된 상태**입니다.

즉 지금 이 문서는:

- 어떤 값이 이미 AL 자산의 공식 소유가 되었는지
- 어떤 값은 상태 리듬/센서 규칙이라 owner나 상태 쪽에 남는 게 맞는지
- 다음에 무엇을 더 정리하면 좋은지

를 빠르게 점검하는 문서입니다.

관련 코드:

- [ShadowServant.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/ShadowServant/ShadowServant.cs)
- [ShadowServantAttackRunner.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/ShadowServant/ShadowServantAttackRunner.cs)
- [AbilityLogic_ShadowServantAttack.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AbilityLogic_ShadowServantAttack.cs)
- [AL_ShadowServantAttack.asset](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AL_ShadowServantAttack.asset)
- [StrangeCandlestick.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/StrangeCandlestick/StrangeCandlestick.cs)
- [StrangeCandlestickAttackRunner.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/StrangeCandlestick/StrangeCandlestickAttackRunner.cs)
- [AbilityLogic_StrangeCandlestickAttack.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AbilityLogic_StrangeCandlestickAttack.cs)
- [AL_StrangeCandlestickAttack.asset](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AL_StrangeCandlestickAttack.asset)
- [DeadsSkeleton.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Dead'sSkeleton/DeadsSkeleton.cs)
- [DeadsSkeletonSelfDestructPatternExecutor.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Dead'sSkeleton/DeadsSkeletonSelfDestructPatternExecutor.cs)
- [AbilityLogic_DeadsSkeletonSelfDestruct.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AbilityLogic_DeadsSkeletonSelfDestruct.cs)
- [AL_DeadsSkeletonSelfDestruct.asset](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AL_DeadsSkeletonSelfDestruct.asset)

---

## 한 줄 기준

패턴 데이터 소유권은 아래 기준으로 나눕니다.

- **FSM 상태**
  - 전이 규칙
  - 상태 생명주기
  - 전투 리듬
- **helper / owner**
  - 실행 가능 조건
  - 타깃/문맥 생성
  - 몬스터 정체성 전반에 걸친 값
- **runner / executor**
  - 실행 중 일시적인 런타임 상태
  - `Cancel/finally` cleanup
- **AL / 패턴 데이터**
  - 한 패턴이 실제로 어떻게 실행되는지에 필요한 수치와 참조
  - 경고 시간, 커밋 타이밍, 범위, 프리팹, SFX/VFX, 피해 effect 등

즉:

> **결정은 상태가, 가능 여부는 helper가, 실행 수치와 자산은 AL/패턴 데이터가, 일시적인 실행 상태는 runner가 가진다.**

---

## 1. ShadowServant

### 현재 분포

| 데이터 | 현재 위치 | 추천 소유자 | 메모 |
|---|---|---|---|
| `postAttackRecoverSeconds` | [ShadowServant.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/ShadowServant/ShadowServant.cs) | FSM 상태 / 상태 전이 데이터 | 공격 자체보다 전투 리듬에 가깝다. |
| `warningDuration`, warning blink 값 | [AbilityLogic_ShadowServantAttack.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AbilityLogic_ShadowServantAttack.cs), [AL_ShadowServantAttack.asset](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AL_ShadowServantAttack.asset) | AL / 패턴 데이터 | 1차 이전 완료. runner는 owner를 통해 소비만 한다. |
| 안개/피해/폭발 연출/사운드/카메라 셰이크 | [AbilityLogic_ShadowServantAttack.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AbilityLogic_ShadowServantAttack.cs), [AL_ShadowServantAttack.asset](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AL_ShadowServantAttack.asset) | AL / 패턴 데이터 | 1차 이전 완료. |
| `isRunning`, `cancelRequested`, `damagedTargets` | [ShadowServantAttackRunner.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/ShadowServant/ShadowServantAttackRunner.cs) | runner | 런타임 실행 상태다. |

### 현재 해석

`ShadowServant`는 원래 owner와 runner에 퍼져 있던 경고/피해/연출 데이터를  
이제 **AL 패턴 데이터가 공식적으로 소유**하는 상태가 됐습니다.

남은 건 `Recover` 시간을 상태 데이터로 더 명시적으로 드러낼지 정도입니다.

---

## 2. StrangeCandlestick

### 현재 분포

| 데이터 | 현재 위치 | 추천 소유자 | 메모 |
|---|---|---|---|
| `attackIntervalSeconds` | [AbilityLogic_StrangeCandlestickAttack.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AbilityLogic_StrangeCandlestickAttack.cs), [AL_StrangeCandlestickAttack.asset](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AL_StrangeCandlestickAttack.asset) | AL / 패턴 데이터 | 1차 이전 완료. |
| 발사체 prefab / 속도 / 피해 effect / 피해량 | [AbilityLogic_StrangeCandlestickAttack.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AbilityLogic_StrangeCandlestickAttack.cs), [AL_StrangeCandlestickAttack.asset](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AL_StrangeCandlestickAttack.asset) | AL / 패턴 데이터 | 1차 이전 완료. |
| 락온 시간 / 선 굵기 / 색 / style asset | [AbilityLogic_StrangeCandlestickAttack.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AbilityLogic_StrangeCandlestickAttack.cs), [AL_StrangeCandlestickAttack.asset](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AL_StrangeCandlestickAttack.asset) | AL / 패턴 데이터 | 1차 이전 완료. |
| `nextProjectileFireTime` | [StrangeCandlestick.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/StrangeCandlestick/StrangeCandlestick.cs) | helper / owner 런타임 상태 | 실행 중 상태이며 데이터 자산이 아니다. |
| `isRunning`, `cancelRequested` | [StrangeCandlestickAttackRunner.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/StrangeCandlestick/StrangeCandlestickAttackRunner.cs) | runner | 런타임 실행 상태다. |

### 현재 해석

`StrangeCandlestick`는 원래 owner가 거의 모든 패턴 데이터를 직접 들고 있던 사례였고,  
지금은 **발사/락온 패턴 데이터가 AL로 옮겨진 가장 깔끔한 사례 중 하나**가 됐습니다.

남은 건 락온 style을 asset-only로 더 강하게 고정할지 정도입니다.

---

## 3. DeadsSkeleton

### 현재 분포

| 데이터 | 현재 위치 | 추천 소유자 | 메모 |
|---|---|---|---|
| `selfDestructTriggerDiameter` | [DeadsSkeleton.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Dead'sSkeleton/DeadsSkeleton.cs) | helper / owner 또는 상태 데이터 | 자폭 상태 진입 규칙에 가깝다. |
| `selfDestructChaseSpeedScale`, `normalChaseSpeedScale`, `normalDetectionRange`, `selfDestructDetectionRange` | [DeadsSkeleton.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Dead'sSkeleton/DeadsSkeleton.cs) | 상태 데이터 / owner | 전용 상태의 이동 리듬 데이터다. |
| `explosionDiameter`, 폭발 피해, 폭발 연출/사운드/셰이크 | [AbilityLogic_DeadsSkeletonSelfDestruct.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AbilityLogic_DeadsSkeletonSelfDestruct.cs), [AL_DeadsSkeletonSelfDestruct.asset](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Abilities/AL_DeadsSkeletonSelfDestruct.asset) | AL / 패턴 데이터 | 1차 이전 완료. |
| `runtimeExplosionDiameterOverride` | [DeadsSkeleton.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Dead'sSkeleton/DeadsSkeleton.cs) | owner 런타임 상태 | 기본 패턴 데이터 위에 전투 중 강화값을 얹는 오버라이드다. |
| intro/armed warning style, sight mask tween | [DeadsSkeleton.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Dead'sSkeleton/DeadsSkeleton.cs) | 혼합 | 스타일 정의 일부는 패턴 데이터 후보지만, 현재는 전용 상태 연출과 더 강하게 묶여 있다. |
| `isSelfDestruct`, `hasEnteredArmedPhase`, `selfDestructIntroEndTime`, `canCancelSelfDestruct` | [DeadsSkeleton.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Dead'sSkeleton/DeadsSkeleton.cs) | 상태 / owner 런타임 상태 | 자폭 상태 진행 자체를 표현한다. |
| `isRunning`, `cancelRequested` | [DeadsSkeletonSelfDestructPatternExecutor.cs](C:/HMS/AboutCapstoneProject/CapstoneProject/CapstoneProject/Assets/Script/Enemy/Mob/Dead'sSkeleton/DeadsSkeletonSelfDestructPatternExecutor.cs) | executor | 런타임 실행 상태다. |

### 현재 해석

`DeadsSkeleton`은 여전히 가장 복잡한 사례입니다.

다만 이번 1차 이전으로:

- **폭발 반경**
- **폭발 피해**
- **폭발 연출/사운드/카메라 셰이크**

는 AL 쪽으로 이동했고,

- **자폭 진입 조건**
- **armed chase 속도/감지 범위**
- **전용 상태 진행 플래그**

는 상태/owner 쪽에 남는 구조가 더 자연스럽다는 것도 분명해졌습니다.

즉 `DeadsSkeleton`은 “AL로 다 옮긴다”가 아니라,  
**상태 데이터와 패턴 데이터의 경계를 가장 잘 보여주는 사례**가 됐습니다.

---

## 공통 패턴

세 몬스터를 같이 보면 지금 공통적으로 정리된 방향은 이렇습니다.

### 1. 명백한 패턴 실행 데이터는 AL이 가진다

- 피해량
- 범위
- 경고 시간
- 경고 스타일
- VFX / SFX / camera shake
- 투사체 속도

이런 값은 이제 AL 자산이 공식 소유 표면이 됩니다.

### 2. runner는 실행 상태만 가진다

runner / executor는:

- `isRunning`
- `cancelRequested`
- 진행 중 telegraph / handle
- `finally` cleanup

같은 **런타임 실행 상태**만 들고,
고정 패턴 값은 AL을 소비하는 방향으로 정리됩니다.

### 3. 상태 리듬과 센서 규칙은 상태/owner 쪽에 남는다

- `Recover` 시간
- 자폭 진입 범위
- armed chase 속도
- 감지 범위

같은 값은 패턴 “실행”보다 **전이와 리듬**에 가깝기 때문에  
FSM 상태나 owner/helper 쪽에 남는 것이 더 자연스럽습니다.

---

## 다음 이전 우선순위

지금 이후의 우선순위는 이렇게 보는 게 좋습니다.

### 1순위: `DeadsSkeleton` warning style 경계 정리

`introWarningStyle`, `armedWarningStyle`는 현재 상태 리듬과 presentation이 강하게 묶여 있어서  
즉시 옮기기보다 한 번 더 경계를 보는 게 안전합니다.

### 2순위: 상태 데이터 표면 명확화

예:

- `postAttackRecoverSeconds`
- `selfDestructTriggerDiameter`
- `selfDestructDetectionRange`

같은 값이 “FSM 상태 데이터”임을 더 명확히 드러내는 별도 정리가 가능합니다.

---

## 한 줄 결론

- `ShadowServant`, `StrangeCandlestick`, `DeadsSkeleton`의 **명백한 패턴 실행 데이터 이전과 fallback 제거는 완료**된 상태입니다.
- 지금부터의 핵심은  
  **전이/리듬 데이터는 FSM 쪽에 남기고, 패턴 실행 데이터는 AL/패턴 데이터 쪽으로 두는 규칙을 계속 유지하는 것**입니다.
- 다음 정리 대상은  
  **`DeadsSkeleton` warning style 경계, 상태 데이터 표면 명확화**  
  쪽이 가장 자연스럽습니다.
