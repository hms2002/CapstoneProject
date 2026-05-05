---
status: active
authority: contract
category: contract
last_reviewed: 2026-05-05
---

# Mob Cleanup Contract

이 문서는 **일반 몬스터 FSM / runner / bridge가 cleanup을 언제, 누가, 무엇에 대해 수행해야 하는지**를 호출 경로별로 정리합니다.

이 문서의 목적은:

- 상태 전이 / 제압 / 사망 / 취소 / 비활성화 경로에서 cleanup 누락을 줄이고
- 새 상태나 새 패턴을 추가할 때
  - 무엇을 `Exit`에서 정리해야 하는지
  - 무엇을 전투 객체가 orchestration 해야 하는지
  - 무엇을 runner가 `Cancel/finally`에서 정리해야 하는지
  를 한눈에 보이게 하는 것입니다.

관련 코드:

- [Mob.cs](../../Assets/Script/Enemy/Mob/Mob.cs)
- [MobAIContext.cs](../../Assets/Script/Enemy/Mob/FSM/MobAIContext.cs)
- [IMobPatternRunner.cs](../../Assets/Script/Enemy/Mob/IMobPatternRunner.cs)
- [MobStateMachine.cs](../../Assets/Script/Enemy/Mob/FSM/MobStateMachine.cs)
- [MobAbilityCoordinator.cs](../../Assets/Script/Enemy/Mob/MobAbilityCoordinator.cs)
- [MobStaggerState.cs](../../Assets/Script/Enemy/Mob/FSM/MobStaggerState.cs)
- [Enemy.cs](../../Assets/Script/Enemy/Enemy.cs)

---

## 한 줄 원칙

> **상태 cleanup은 `Exit`, 패턴 cleanup은 `Cancel/finally`, 전역 종료(suppression / death / disable)는 전투 객체가 orchestration 한다.**

---

## cleanup 계층 역할

| 계층 | cleanup 의무 지점 | 책임 |
|---|---|---|
| 상태(`IMobState`) | `Exit` | 상태가 소유한 이동/임시 문맥/상태 로컬 리소스 정리 |
| 패턴 runner / executor | `Cancel()` / `finally` | 경고, armed 상태, 실행 중 presentation, runner 등록 정리 |
| presentation provider(`IMobPresentationCleanup`) | `CleanupPresentation()` | 전역 종료 경로에서 숨겨야 하는 warning / mask / overlay 정리 |
| 전투 객체(`Mob` / `MobAIContext` / `MobAbilityCoordinator`) | suppression / death / disable | 상태 밖 전역 종료 경로의 공통 cleanup orchestration |

즉 cleanup은 하나의 함수에 몰아넣지 않고,

- **상태**
- **패턴**
- **전투 객체**

세 층으로 나눠 책임집니다.

---

## 1. 상태 전이 기반 cleanup

예:

- `Idle -> Chase`
- `Chase -> Attack`
- `Attack -> Recover`
- `Recover -> Idle`
- `Any -> Stagger`

### 책임 주체

- **현재 상태의 `Exit`**

### 현재 규칙

- `MobStateMachine.ChangeState(...)`는 항상
  - 이전 상태 `Exit`
  - 다음 상태 `Enter`
  순서를 보장합니다.
- 따라서 **상태가 소유한 cleanup은 `Exit`에 둡니다.**

### 현재 적용 사례

- [MobChaseState.cs](../../Assets/Script/Enemy/Mob/FSM/MobChaseState.cs)
  - `Exit()` -> `StopChase()`
- [MobAttackState.cs](../../Assets/Script/Enemy/Mob/FSM/MobAttackState.cs)
  - `Exit()` -> `OnAttackStateExited(...)`
- [DeadsSkeleton.cs](../../Assets/Script/Enemy/Mob/Dead'sSkeleton/DeadsSkeleton.cs)
  - `DeadsSkeletonSelfDestructState.Exit()` -> armed chase 정리 + attack exit hook

### 상태가 반드시 정리해야 하는 것

- `ChaseIntent` 시작/중지 같은 **상태 소유 이동**
- 상태 내부에서 켠 임시 플래그
- 상태 전용 로컬 문맥

### 상태가 직접 소유하지 않는 것

- 전역 suppression 반응
- 사망 cleanup
- disable/unload fail-safe

이건 전투 객체 cleanup 경로가 맡습니다.

---

## 2. 제압(suppression) 진입 기반 cleanup

예:

- `Groggy`
- 앞으로의 crowd control / 행동 제압 계열

### 책임 주체

- **전투 객체**
  - [MobAIContext.cs](../../Assets/Script/Enemy/Mob/FSM/MobAIContext.cs)
  - [MobStaggerState.cs](../../Assets/Script/Enemy/Mob/FSM/MobStaggerState.cs)
  - [MobAbilityCoordinator.cs](../../Assets/Script/Enemy/Mob/MobAbilityCoordinator.cs)

### 현재 규칙

- suppression 의미 해석은 `IAIAbilityBridge.IsAbilityExecutionSuppressed`가 담당
- `StaggerState.Enter()`는 `context.PerformSuppressionCleanup()` 호출
- `PerformSuppressionCleanup()`은 최소한:
  - `StopChase()`
  - 오브젝트에 붙은 `IMobPatternRunner` 전체 `Cancel()`
  - `CancelActiveAbility(true)`
  - `CleanupPresentation()`
  를 보장

### 현재 적용 사례

- [MobAIContext.cs](../../Assets/Script/Enemy/Mob/FSM/MobAIContext.cs)
  - `PerformSuppressionCleanup()`
- [MobStaggerState.cs](../../Assets/Script/Enemy/Mob/FSM/MobStaggerState.cs)
  - `Enter()`에서 suppression cleanup 호출
- [MobAbilityCoordinator.cs](../../Assets/Script/Enemy/Mob/MobAbilityCoordinator.cs)
  - runner cancel + ASC cast/execution cancel
- [Mob.cs](../../Assets/Script/Enemy/Mob/Mob.cs)
  - `ResolvePatternRunnerTargets()`로 runner cleanup 대상을 수집

### suppression 경로가 반드시 정리해야 하는 것

- chase 중지
- active ability / runner 취소
- 이후 실행 시작 금지

### 아직 개별 구현에 남는 것

- 패턴 전용 armed flag 원복

즉 전역 종료 경로는 `IMobPresentationCleanup`으로 공통 시각 정리를 보장하고,
패턴 전용 gameplay 원복은 runner/executor의 `Cancel/finally` cleanup이 맡습니다.

---

## 3. 사망 기반 cleanup

예:

- 체력 0
- 광원 사망
- 자폭 완료 후 사망

### 책임 주체

- **전투 객체**
  - [Enemy.cs](../../Assets/Script/Enemy/Enemy.cs)
  - [Mob.cs](../../Assets/Script/Enemy/Mob/Mob.cs)

### 현재 규칙

- `Enemy.Die()`가 공통 사망 진입점
- `Mob.OnDeathStarted()`에서 먼저
  - `MobDeathState` 전이
- 이후 `Enemy.StopDeathGameplay()`가
  - 이동 정지
  - 충돌 정지
  - 물리 정지
  를 처리

### 현재 적용 사례

- [Mob.cs](../../Assets/Script/Enemy/Mob/Mob.cs)
  - `OnDeathStarted()`, `EnterDeathState()`
- [Enemy.cs](../../Assets/Script/Enemy/Enemy.cs)
  - `Die()`, `StopDeathGameplay()`
- [MobStaggerState.cs](../../Assets/Script/Enemy/Mob/FSM/MobStaggerState.cs)
  - `MobDeathState`
- [DeadsSkeleton.cs](../../Assets/Script/Enemy/Mob/Dead'sSkeleton/DeadsSkeleton.cs)
  - 전용 warning/detection cleanup 추가

### 사망 경로가 반드시 정리해야 하는 것

- FSM shutdown
- chase/runner/ability cancel
- 이동/충돌/물리 정지

### 현재 상태

- `DeathState`는 현재 터미널 상태로 들어와 fail-safe cleanup 이후 더 이상의 전투 판단이 일어나지 않게 한다.
- 실제 애니메이션 재생과 제거 시점은 계속 [Enemy.cs](../../Assets/Script/Enemy/Enemy.cs)의 공통 사망 루틴이 담당한다.

### 향후 개선 포인트

- 필요하면 `DeathState`가
  - 애니메이션
  - 사라지는 시점
  - presentation cleanup
  까지 더 적극적으로 소유하도록 확장할 수 있음

---

## 4. 실행 실패 / 시작 거부 기반 cleanup

예:

- `TryStartAbility(...)` 실패
- request invalid
- cooldown / busy / suppression 때문에 시작 못 함

### 책임 주체

- **공격 상태**
- **helper / decision source**

### 현재 규칙

이 경로는 두 종류를 구분합니다.

#### A. pre-start failure

- 아직 능력 실행이 시작되지 않음
- 임시 request/context 폐기만 필요

예:

- [MobAttackState.cs](../../Assets/Script/Enemy/Mob/FSM/MobAttackState.cs)
  - `TryStartAbility(...)` 실패 시 post-attack state로 복귀
- [TackleAttack.cs](../../Assets/Script/Enemy/Mob/TackleAttack.cs)
  - 시작 실패 시 `ClearContext()`

#### B. post-start interruption

- 이미 cast / telegraph / runner가 시작된 뒤 취소
- 이 경우는 suppression / death / explicit cancel 경로 cleanup을 따라감

### 현재 규칙

- **pre-start failure는 로컬 request/context 정리**
- **post-start interruption은 suppression/death/runner cancel 경로 정리**

---

## 5. 패턴 내부 취소 기반 cleanup

예:

- self-destruct cancel
- target invalidation
- executor cancel

### 책임 주체

- **runner / executor**

### 현재 규칙

- 패턴 실행기는 `Cancel()` 또는 `finally`에서
  - 전용 warning hide
  - armed/intro 같은 전용 플래그 정리
  - `EndRunner(...)`
  를 정리해야 합니다.

### 현재 적용 사례

- [ShadowServantAttackRunner.cs](../../Assets/Script/Enemy/Mob/ShadowServant/ShadowServantAttackRunner.cs)
  - `finally` -> `HideWarning()`, `EndRunner(...)`
- [StrangeCandlestickAttackRunner.cs](../../Assets/Script/Enemy/Mob/StrangeCandlestick/StrangeCandlestickAttackRunner.cs)
  - `finally` -> `HideWarning()`, `EndRunner(...)`
- [DeadsSkeletonSelfDestructPatternExecutor.cs](../../Assets/Script/Enemy/Mob/Dead'sSkeleton/DeadsSkeletonSelfDestructPatternExecutor.cs)
  - `Cancel()` / `finally`
- [DeadsSkeleton.cs](../../Assets/Script/Enemy/Mob/Dead'sSkeleton/DeadsSkeleton.cs)
  - `CancelSelfDestruct()`
  - warning/sight mask/speed range 원복
- [TackleAttack.cs](../../Assets/Script/Enemy/Mob/TackleAttack.cs)
  - `CleanupPresentation()` -> telegraph hide
- [ShadowServantAttackRunner.cs](../../Assets/Script/Enemy/Mob/ShadowServant/ShadowServantAttackRunner.cs)
  - `CleanupPresentation()` -> warning hide
- [StrangeCandlestickAttackRunner.cs](../../Assets/Script/Enemy/Mob/StrangeCandlestick/StrangeCandlestickAttackRunner.cs)
  - `CleanupPresentation()` -> warning hide
- [DeadsSkeleton.cs](../../Assets/Script/Enemy/Mob/Dead'sSkeleton/DeadsSkeleton.cs)
  - `CleanupPresentation()` -> warning hide + sight mask reset

### runner가 반드시 정리해야 하는 것

- 전용 경고/telegraph
- 전용 armed flag / intro flag
- runner registration

---

## 6. Disable / Unload 기반 cleanup

예:

- `OnDisable`
- 씬 전환
- 오브젝트 비활성

### 책임 주체

- **전투 객체 fail-safe cleanup**

### 현재 규칙

- 이 경로는 정상 전이용 cleanup이 아니라 **최후 방어선**입니다.
- `Mob.OnDisable()`는
  - `PerformFailSafeCleanup()`
  - `ShutdownStateMachine()`
  를 호출
- `MobAbilityCoordinator.OnDisable()`는
  - `CancelActiveAbility(true)`
  - `activeRunner = null`
  처리
- `PerformFailSafeCleanup()`은 최소한:
  - `StopChase()`
  - 오브젝트에 붙은 `IMobPatternRunner` 전체 `Cancel()`
  - `CancelActiveAbility(true)`
  - `CleanupPresentation()`
  를 보장

### 현재 적용 사례

- [Mob.cs](../../Assets/Script/Enemy/Mob/Mob.cs)
  - `OnDisable()`
- [MobAbilityCoordinator.cs](../../Assets/Script/Enemy/Mob/MobAbilityCoordinator.cs)
  - `OnDisable()`
- [TackleAttack.cs](../../Assets/Script/Enemy/Mob/TackleAttack.cs)
  - helper 로컬 `OnDisable()` cleanup

### 이 경로의 목표

- "예쁘게 정리"보다
- **무언가 남기고 죽지 않기**

입니다.

즉:

- active runner
- active ability
- chase intent
- 남은 상태 기계
- 남은 warning / mask / overlay

를 최소한 강제로 정리하는 fail-safe로 봅니다.

---

## 현재 기준 요약

| 호출 경로 | 공통 규칙 상태 | 메모 |
|---|---|---|
| 상태 전이 | 비교적 강함 | `Exit` 책임이 명확함 |
| suppression 진입 | 강함 | `PerformSuppressionCleanup()` 추가됨 |
| 사망 | 강함 | `Die() -> OnDeathStarted() -> fail-safe cleanup` |
| 실행 실패 | 중간 | pre-start / post-start 분리 필요 |
| 패턴 내부 취소 | 중간 | 각 runner는 잘 되어 있으나 공통 계약은 더 다듬을 수 있음 |
| disable / unload | 강함 | `Mob` / `Coordinator` fail-safe + runner 전체 취소 추가됨 |

---

## 남은 보완 포인트

1. `pre-start failure`와 `post-start interruption`의 실패 분류를 더 명시적으로 드러내기
2. 새 runner / executor가 `Cancel/finally` cleanup 계약을 빠뜨리지 않게 authoring/리뷰 기준 강화
3. 보스/특수 전투 오브젝트 쪽 cleanup 계약과의 연결 범위 점검

한 줄로 정리하면:

> **지금 cleanup 구조는 “상태 Exit + 전투 객체 fail-safe + runner cancel/finally” 삼각형으로 이해하면 됩니다.**
