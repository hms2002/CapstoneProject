---
status: legacy-review
authority: reference-only
category: review
last_reviewed: 2026-05-05
---

# Player Status Direction Review

> Legacy / Review  
> 이 문서는 플레이어 상태 시스템 방향을 검토하기 위해 작성된 리뷰 문서입니다.  
> 현재 표준은 상태/버프/디버프 아키텍처 문서를 우선하고, 이 문서는 구조 판단의 배경 기록으로 봅니다.

이 문서는 **플레이어 버프/디버프 상태를 어떤 계층에서 관리할지**를 검토하기 위한 정리 문서입니다.

현재 프로젝트는 이미:

- 무기 상태
- 유물 상태
- 환경 상태
- 상태 HUD / tooltip

까지는 구조가 상당히 정리되어 있습니다.

하지만 몬스터가 거는 디버프처럼 **플레이어에게 걸리는 상태**를 더 확장하려고 하면,

- `PlayerStatusRuntime`를 더 키울지
- 기존 `AbilitySystem / GameplayEffect / TagSystem` 경로를 그대로 중심으로 둘지
- `RestrictedVisionVisualController` 같은 특수 연출 컴포넌트를 어떻게 다룰지

를 먼저 분명히 해야 합니다.

이 문서는 그 판단을 위한 현재 상태와 선택지를 정리합니다.

## 현재 상황

### 이미 정리된 상태 계층

- 무기 상태
  - `WeaponRuntimeData / Processor / Coordinator`
  - owner가 명확하고, owner가 상태 수명을 거의 전부 관리합니다.
- 유물 상태
  - `MoveSpeedOnKillProc`
  - `MoveSpeedOnDamagedProc`
  - `MoveSpeedStackOnCriticalHitProc`
  - 유물 proc owner가 능력치 효과와 HUD 상태를 함께 관리합니다.
- 환경 상태
  - `SceneRestrictedVisionController`
  - 씬 owner가 플레이어 등록 시 상태를 신청하고, 씬 종료/교체 시 회수합니다.
- 상태 HUD
  - `StatusHudDefinition`
  - `PlayerStatusRuntime`
  - `PlayerStatusHudSource`
  - `StatusHudService / Presenter / Tooltip`

즉 현재 구조는 **owner가 상태를 신청하고, HUD는 projection만 한다**는 방향으로 잘 정리되어 있습니다.

### 현재 플레이어 상태 관련 핵심 컴포넌트

- `PlayerStatusRuntime`
  - 활성 상태 entry 목록
  - handle 기반 apply/update/release
  - HUD source가 읽는 현재 상태 허브
- `TagSystem`
  - 실제 gameplay tag 저장/추적/복원
- `AbilitySystem`
  - `GameplayEvent`
  - `GameplayEffect`
  - `DamagedTag / KillConfirmedTag`
- `GlobalVisionMaskController`
  - 시야 제한 연출 소비자
- `RestrictedVisionVisualController`
  - 현재는 시간 관리 + HUD 상태 등록 + darkness 연출 요청까지 함께 맡는 특수 컴포넌트

### 현재 드러난 문제

플레이어에게 걸리는 디버프는 무기/유물 상태와 완전히 같지 않습니다.

예:

- 몬스터가 디버프를 "건다"
- 실제 감속/약화/피해 증폭은 `GameplayEffect`나 `AttributeModifier`가 담당할 수 있다
- 존재 여부 판단은 `TagSystem`이 더 적합하다
- HUD 표시와 tooltip은 `PlayerStatusRuntime`가 더 잘한다
- 화면 연출은 `GlobalVisionMaskController` 같은 별도 소비자가 있다

즉 플레이어 상태는 **한 owner가 모든 걸 다 아는 구조**보다,
**적용 / 존재 여부 / 표시 / 연출이 분리된 종합 상태**에 더 가깝습니다.

## 겹치기 쉬운 책임

### `PlayerStatusRuntime` vs `TagSystem`

- `TagSystem`
  - 태그 저장, count, explicit snapshot, 복원
- `PlayerStatusRuntime`
  - 활성 상태 entry, handle, HUD projection용 상태 목록

정리:

- 태그 저장소를 `PlayerStatusRuntime`가 다시 만들면 안 됩니다.
- `PlayerStatusRuntime`는 필요 시 `TagSystem`에 태그를 부여/회수하도록 **조정만** 해야 합니다.

### `PlayerStatusRuntime` vs `PlayerStatusHudSource`

- `PlayerStatusHudSource`
  - `PlayerStatusRuntime`을 HUD 엔트리로 투영

정리:

- HUD용 문자열 조립, tooltip projection까지 `PlayerStatusRuntime`가 먹으면 안 됩니다.
- `PlayerStatusRuntime`는 진실한 상태 목록만 유지하고, HUD는 계속 source/presenter가 읽어야 합니다.

### `PlayerStatusRuntime` vs `GlobalVisionMaskController`

- `GlobalVisionMaskController`
  - dark overlay / player vision mask 연출 소비자

정리:

- 시야 제한 상태의 존재는 `PlayerStatusRuntime`나 `TagSystem`이 알고,
- 실제 연출은 `GlobalVisionMaskController`가 읽어 적용하는 쪽이 맞습니다.

### `PlayerStatusRuntime` vs `RestrictedVisionVisualController`

현재 가장 큰 중복 위험은 여기입니다.

`RestrictedVisionVisualController`는 현재:

- 시간 관리
- HUD 상태 등록/갱신/해제
- 어둠 연출 요청

까지 모두 들고 있습니다.

즉 `PlayerStatusRuntime`를 플레이어 디버프 중심으로 키우려면,
`RestrictedVisionVisualController`를 상태 owner처럼 키우는 방향은 적절하지 않습니다.

## 고려할 점

### 1. 이미 플레이어에게 버프/디버프를 부여하는 기존 경로가 있다

현재 프로젝트는 이미:

- `AbilitySystem`
- `GameplayEffect`
- `AttributeModifier`
- `TagSystem`

을 가지고 있습니다.

즉 `PlayerStatusRuntime`를 **새 버프 시스템**으로 만들면 안 됩니다.

`PlayerStatusRuntime`는:

- owner 기반 수명 관리
- HUD/tooltip projection용 활성 상태 목록
- 필요 시 tag 부여/회수 조정

정도로 제한하는 편이 안전합니다.

### 2. 플레이어 상태는 owner가 하나로 수렴되지 않는다

무기/유물 상태와 달리 플레이어 상태는 여러 owner가 같은 플레이어에게 걸 수 있습니다.

예:

- 씬/환경
- 몬스터
- 유물
- 축복/런 시스템
- 패시브

즉 플레이어 상태는 **owner별 신청**을 받고,
플레이어 쪽 허브가 이를 모아 다루는 구조가 더 자연스럽습니다.

### 3. 상태 존재 여부는 tag가 더 적합한 경우가 많다

많은 시스템은:

- "이 상태가 있는가"
- "이 debuff가 활성인가"

만 빠르게 알고 싶어합니다.

이건 `TagSystem`이 더 적합합니다.

반면 HUD는:

- remainingTime
- stackCount
- sourceKey
- ownerKey

를 알아야 합니다.

따라서 **tag + runtime state 병행**이 자연스럽습니다.

### 4. 씬 전환은 여전히 owner 재등록이 기본이다

플레이어 상태 시스템도 현재 원칙을 깨지 않는 편이 좋습니다.

- 상태 시스템이 영속 저장소가 된다기보다
- owner가 씬 전환 뒤 다시 `Apply(...)`

하는 구조가 여전히 유지보수와 확장에 더 유리합니다.

### 5. 몬스터 디버프는 "재등록"보다 "명시적 회수"가 먼저다

유물/환경 상태와 달리 몬스터 source 디버프는 owner가 씬과 함께 사라질 수 있습니다.

즉 몬스터 디버프는 기본적으로:

- 씬 전환 시 owner 재등록을 기대하지 않고
- owner 소멸, 대상 이탈, 씬 종료 시 명시적으로 회수되어야 합니다.

이 점이 유물/환경 상태와 가장 다른 축입니다.

### 6. 무기/유물 상태를 플레이어 상태 시스템으로 끌어올릴지 구분해야 한다

무기/유물 상태를 플레이어 상태 시스템에 전부 버프/디버프 형태로 올리는 건,
현재 구조 기준으로는 기본값이 아닙니다.

이유:

- 무기/유물 상태는 owner가 더 명확하다
- 상태 변화 규칙을 owner가 가장 잘 안다
- 저장/복원도 owner 문맥에서 더 자연스럽다
- 플레이어 전역 상태 목록에 넣으면 무기/유물 고유 문법이 흐려질 수 있다

즉:

- 무기/유물 고유 상태
  - owner가 직접 소유
  - owner가 직접 HUD에 투영
- 플레이어 전역 의미를 가지는 효과
  - `PlayerStatusRuntime`에 버프/디버프 형태로 등록

으로 나누는 편이 더 적절합니다.

## 디버프 진입점 표준

플레이어 디버프는 앞으로 다음 순서를 **표준 진입점**으로 삼는 편이 좋습니다.

1. **source가 대상 플레이어를 확정한다**
   - 예: `ShadowFog`, 몬스터 공격 proc, 디버프 장판
2. **기존 gameplay 효과 경로로 실제 효과를 적용한다**
   - `GameplayEffect`
   - `AttributeModifier`
   - 또는 기존 전투/상태 효과 적용 경로
3. **같은 owner가 상태 존재를 tag로 보장한다**
   - 가능하면 effect가 tag를 함께 부여
   - 필요 시 owner가 `TagSystem`에 직접 부여/회수
4. **같은 owner가 `PlayerStatusRuntime.Apply/Update/Release`를 호출한다**
   - HUD/tooltip 수명은 여기서 관리

즉:

- `PlayerStatusRuntime.Apply()`를 단독으로 먼저 호출하는 것이 아니라
- **기존 gameplay 효과 적용 경로와 같은 owner가 같은 시점에 HUD 상태를 함께 신청**하는 것이 기준입니다.

### 왜 이 순서가 중요한가

이 순서가 있어야:

- 실제 효과
- 존재 플래그
- HUD 상태

가 서로 다른 코드 경로로 흩어지지 않습니다.

이 문서 기준으로는:

- `AbilitySystem.TryActivateAbility()` 경유가 필요한 경우는 ability 기반 디버프일 때만
- 일반적인 몬스터 디버프는 **effect 적용 -> tag 보장 -> HUD 상태 등록** 순서를 권장합니다.

## 세 계층 동기화 보장 주체

플레이어 디버프는 아래 세 계층이 항상 같은 수명으로 움직여야 합니다.

1. `GameplayEffect / AttributeModifier`
   - 실제 감속, 약화, 피해 증폭 등
2. `TagSystem`
   - 상태 존재 플래그
3. `PlayerStatusRuntime`
   - HUD/tooltip 수명

이 세 가지를 **각 시스템이 따로따로 관리하면 안 됩니다.**

### 권장 원칙

하나의 디버프 owner가 아래를 함께 책임집니다.

- effect 적용
- tag 부여/회수
- `PlayerStatusRuntime` handle apply/update/release

즉 `PlayerStatusRuntime`가 나머지 둘을 추론해서 동기화하려고 하면 안 되고,
**디버프를 시작한 owner가 세 계층을 묶어서 관리**해야 합니다.

### 추천 owner 형태

- `ShadowFog`
- 몬스터 디버프 proc
- 장판/트랩 디버프 컴포넌트
- 또는 이후 필요하면 공용 `MonsterDebuffApplier`

중요한 건 이름보다도,
**세 계층 수명을 한 owner가 같이 들고 있어야 한다**는 점입니다.

### 중복 디버프 정책은 HUD가 아니라 상태 관리 계층이 결정한다

같은 디버프가 여러 source에서 동시에 들어오는 경우,

- 갱신(refresh)
- 스택 추가(stack)
- 더 강한 값으로 교체
- source별 독립 유지

같은 정책은 `PlayerStatusRuntime` 또는 그 위의 상태 관리 계층이 먼저 판단해야 합니다.

즉:

- HUD는 중복 정책을 해석하지 않는다
- HUD는 상태 관리 계층이 확정한 최종 결과를 그대로 보여준다

이 기준을 지켜야,

- 중복 규칙이 바뀌어도 HUD 구현이 흔들리지 않고
- 상태의 진실한 원천이 한 곳에 유지됩니다.

## RestrictedVisionVisualController에 대한 현재 판단

`RestrictedVisionVisualController`는 현재 구조상 과도기적 존재입니다.

현재 맡고 있는 책임:

- 시간 관리
- HUD 상태 등록/갱신/해제
- darkness 연출 요청

즉 Option B가 지향하는 "플레이어 상태 허브 + 기존 effect/tag/연출 분리"와는 어긋나는 부분이 있습니다.

### 정리 원칙

- 신규 플레이어 디버프 구현은 **`RestrictedVisionVisualController` 패턴을 복사하지 않습니다.**
- 신규 구현은
  - 기존 effect 경로
  - tag 존재 플래그
  - `PlayerStatusRuntime`
  - 별도 연출 소비자
  기준으로 만듭니다.
- `RestrictedVisionVisualController`는 기존 시야 제한 사례를 유지하기 위한 **레거시/과도기 컴포넌트**로 간주합니다.

### 정리 시점

- `ShadowFog` 기반 몬스터 디버프 구조를 새 기준으로 한 번 구현하고
- `GlobalVisionMaskController`가 플레이어 상태/tag를 읽는 형태가 안정되면
- 그다음 `RestrictedVisionVisualController`를 더 일반화된 연출 소비자 계층으로 흡수할지 판단하는 순서가 자연스럽습니다.

즉:

- 지금 당장 제거 대상은 아니지만
- **새 코드는 따라 만들지 않는 대상**으로 명확히 선을 긋는 것이 좋습니다.

## 씬 전환 시 몬스터 디버프 규칙

몬스터 source 디버프는 씬 전환 시 아래를 기본 규칙으로 삼습니다.

1. source owner가 씬과 함께 사라지면
2. source owner가 들고 있는
   - effect 정리
   - tag 회수
   - `PlayerStatusRuntime` handle release
   를 같이 수행한다

즉 유물/환경 상태처럼 "다음 씬에서 재등록"이 기본이 아니라,
**씬 전환 시 명시적 회수**가 기본입니다.

### 왜 이렇게 보나

- 몬스터 owner는 씬과 강하게 결합돼 있다
- 다음 씬에 같은 owner가 다시 존재한다는 보장이 없다
- 따라서 상태 허브가 몬스터 디버프를 들고 다음 씬으로 가져가는 것은 위험하다

정리하면:

- 유물/환경 상태
  - 재등록 모델
- 몬스터 source 디버프
  - 회수 모델

로 보는 게 더 정확합니다.

## 선택지

### 옵션 A. `PlayerStatusRuntime`를 플레이어 버프/디버프의 중심 저장소로 키운다

설명:

- 플레이어에게 걸리는 버프/디버프의 진실한 목록을 `PlayerStatusRuntime`가 전부 소유
- 남은 시간, 스택, tag, HUD 상태를 모두 여기서 관리

장점:

- 플레이어 상태를 한 곳에서 보기 쉽다
- owner가 단순해진다

단점:

- 기존 `AbilitySystem / GameplayEffect / TagSystem`과 크게 겹칠 수 있다
- 실제 효과 적용과 HUD 상태가 분리되지 않으면 중복 시스템이 된다
- `RestrictedVisionVisualController` 같은 특수 컴포넌트와 충돌하기 쉽다

판단:

- 현재 프로젝트엔 너무 무겁고 위험한 선택지다

### 옵션 B. `PlayerStatusRuntime`는 표시/수명 허브만 맡고, 실제 효과는 기존 경로에 맡긴다

설명:

- 실제 버프/디버프 효과
  - `GameplayEffect`
  - `AttributeModifier`
  - `TagSystem`
- 상태 owner 수명 및 HUD 표시
  - `PlayerStatusRuntime`

장점:

- 기존 시스템을 살린다
- 중복을 줄인다
- owner 재등록 모델과 잘 맞는다
- HUD 확장성이 좋다
- 구현 기준을 잘 정하면 세 계층(effect/tag/HUD)의 책임을 분리하면서도 owner 단위 동기화를 유지할 수 있다

단점:

- 세 계층(effect/tag/HUD)이 엇박자 나면 실제 효과와 HUD가 분리될 수 있다
- 따라서 owner가 세 계층을 함께 관리하는 구현 규칙이 반드시 필요하다

판단:

- 현재 구조와 가장 잘 맞는 기본 선택지

### 옵션 C. 플레이어 상태도 owner 개별 컴포넌트가 직접 관리하고 `PlayerStatusRuntime`는 최소화한다

설명:

- `RestrictedVisionVisualController` 같은 보조 브리지가 과도한 상태 책임까지 들지 않도록 주의
- `PlayerStatusRuntime`는 거의 빈 허브로 유지

장점:

- 초기엔 빠르다
- 특수 사례 구현이 쉽다

단점:

- 디버프가 늘어날수록 중복이 급격히 증가한다
- 플레이어 상태가 한곳에 모이지 않는다
- HUD 패턴이 흔들린다

판단:

- 특수 사례 1개 정도엔 가능하지만, 앞으로 확장할 방향으로는 좋지 않다

## 추천 방향

현재 프로젝트엔 **옵션 B**가 가장 적합합니다.

즉:

- 기존 gameplay 효과 적용은 기존 경로를 유지
- `PlayerStatusRuntime`는 상태 owner가 신청한 활성 상태와 handle 수명을 관리
- `TagSystem`은 존재 여부 판정용 태그를 실제로 저장
- HUD는 `PlayerStatusHudSource`가 projection
- 연출은 `GlobalVisionMaskController` 같은 소비자가 상태/tag를 읽어 처리

### 이 방향의 구체 의미

- `PlayerStatusRuntime`가 "버프 시스템 2호기"가 되면 안 된다
- 대신 "플레이어 상태 표시/수명 허브"가 된다
- 태그 저장/카운트는 `TagSystem`
- 실제 감속/피해 증폭/약화는 `GameplayEffect` 또는 기존 능력치 경로

## `ShadowFog` 사례에 대한 정리

`ShadowFog`를 몬스터 source 디버프 기준 사례로 볼 때,
가장 자연스러운 형태는 다음과 같습니다.

- `ShadowFog`
  - 디버프 source
  - 플레이어 대상 확정
  - effect 적용 / tag 보장 / HUD 상태 신청을 같은 owner 문맥에서 시작
- `PlayerStatusRuntime`
  - `restricted_vision` 활성 상태 entry 관리
  - HUD 표시용 데이터 제공
  - 직접 effect나 연출을 소유하지 않음
- `GlobalVisionMaskController`
  - `restricted_vision` 상태 또는 tag를 읽어 연출 적용
- `RestrictedVisionVisualController`
  - 장기적으로는 축소/정리 대상
  - 남긴다면 연출 보조 bridge 정도가 적절

즉 `RestrictedVisionVisualController`가 현재처럼

- 시간
- HUD
- 어둠 연출

을 모두 직접 관리하는 구조는, 앞으로는 권장 방향과 어긋날 가능성이 큽니다.

## 무기/유물 상태와 플레이어 상태 시스템의 경계

이 문서에서 중요하게 보는 쟁점 중 하나는:

> 무기/유물의 상태를 플레이어 상태 시스템으로 올리는 것이 더 좋은가?

에 대한 판단입니다.

현재 프로젝트 기준 추천은 다음과 같습니다.

### 기본 원칙

- 무기/유물의 **고유 상태**는 owner가 직접 소유한다
- 플레이어 전역 의미를 가지는 **효과**만 `PlayerStatusRuntime`에 올린다

### owner가 직접 소유하는 것이 더 좋은 상태

예:

- 무기 자세
- 무기 스택
- 무기 pair interaction 상태
- 유물 proc 내부 스택/시간 상태

이런 상태는:

- owner가 시작/갱신/종료를 가장 잘 알고
- 플레이어 전역 상태로 올릴수록 의미가 흐려지며
- owner가 직접 HUD에 투영하는 구조가 더 자연스럽습니다.

### 플레이어 상태 시스템에 올릴 가치가 있는 효과

예:

- 몬스터 디버프
- 환경 디버프
- 플레이어 전역 버프
- 여러 시스템이 공통으로 읽어야 하는 상태

이런 상태는:

- 플레이어에게 걸린 것이 본질이고
- HUD/tooltip, 존재 플래그, 연출 소비가 플레이어 중심으로 움직이는 편이 자연스럽습니다.

### 한 줄 기준

무기/유물 상태는:

- **그 상태 자체가 owner의 전투 문법이면 owner가 직접 소유**
- **그 결과가 플레이어 전역 효과로 해석될 때만 `PlayerStatusRuntime`에 올린다**

로 이해하는 것이 현재 구조와 가장 잘 맞습니다.

## 바로 구현하기 전에 점검할 질문

1. 이 상태의 실제 효과는 기존 `GameplayEffect / AttributeModifier`로 충분한가
2. 상태 존재 여부를 다른 시스템이 tag로 빠르게 봐야 하는가
3. HUD에 필요한 값은 `remainingTime / stackCount / sourceKey` 정도인가
4. owner 재등록 모델이 자연스러운가
5. 현재 특수 컴포넌트가 너무 많은 책임을 들고 있지는 않은가

## 한 줄 결론

현재 방향성은 **`PlayerStatusRuntime`를 플레이어 상태의 표시/수명 허브로 두고, 실제 gameplay 효과와 태그 저장은 기존 시스템을 재사용하는 쪽**이 가장 적절합니다.

즉:

- `PlayerStatusRuntime` = 상태 허브
- `TagSystem` = 상태 존재 플래그 저장
- `GameplayEffect / AttributeModifier` = 실제 효과
- `PlayerStatusHudSource` = HUD projection
- `GlobalVisionMaskController` = 연출 소비

그리고 구현 기준으로는:

- **디버프 진입점은 "effect 적용 -> tag 보장 -> HUD 상태 등록"**
- **세 계층(effect/tag/HUD)은 같은 owner가 함께 수명을 관리**
- **몬스터 source 디버프는 씬 전환 시 재등록보다 회수가 기본**

으로 보는 것이 현재 프로젝트에 가장 잘 맞습니다.

이 책임선이 현재 프로젝트 구조와 가장 잘 맞습니다.
