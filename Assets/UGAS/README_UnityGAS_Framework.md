# UnityGAS 프레임워크 사용 가이드 (GAS-like Architecture for Unity)

이 문서는 **Unity에서 Unreal GAS와 유사한 방식**으로 게임 로직을 구성하려는 사람을 위해 작성되었습니다.  
팀원이 프레임워크 코드를 수정하지 않고도 **Ability / Effect / Tag / Cue / Relic(패시브)** 콘텐츠를 안정적으로 추가할 수 있도록, **핵심 컴포넌트(ASC 역할)와 호출해야 하는 함수** 중심으로 설명합니다.

> ✅ 이 프레임워크는 “능력(Ability) 실행”과 “수치 변화(Effect)”를 분리하고,  
> `GameplayTag` + `GameplayEvent`로 상태/제약/타이밍을 통합하는 것을 목표로 합니다.

---

## 0) Unreal GAS와 매핑 (개념 대응표)

| Unreal GAS | UnityGAS(이 프로젝트) | 비고 |
|---|---|---|
| ASC (AbilitySystemComponent) | **AbilitySystem + TagSystem + AttributeSet + GameplayEffectRunner** | 사실상 ASC는 여러 컴포넌트로 분해됨 |
| GameplayAbility (GA) | **AbilityDefinition(SO) + AbilityLogic(SO) + AbilitySpec(런타임 상태)** | Definition=정책/연결, Logic=실행, Spec=상태 |
| GameplayEffect (GE) | **GameplayEffect(SO) / ISpecGameplayEffect** | Instant/Duration 모두 지원. SetByCaller는 Spec로 |
| GameplayTag | **GameplayTag(SO) + TagRegistry/TagMask/TagSystem** | Tag Editor로 생성/Resources로 로드 |
| GameplayEvent | **AbilitySystem.SendGameplayEvent(tag, data)** | 애니 이벤트/히트 이벤트/유물 트리거 표준화 |
| GameplayCue (GC) | **GameplayCueDefinition(SO) + GameplayCueManager** | Execute/Add/Remove + Persistent 지원 |

---

## 1) “ASC 역할”을 하는 핵심 컴포넌트

플레이어(또는 능력/효과를 받는 액터)는 보통 아래 4개를 **한 GameObject**에 가집니다.

### 1.1 AbilitySystem (핵심 오케스트레이터)
- Ability 실행/캐스팅/취소/쿨다운
- 이벤트 버스(게임플레이 이벤트 수신/브로드캐스트)
- 애니 트리거 라우팅(Player/Weapon 채널)
- EffectContainer 타이밍(OnActivate/OnEvent/OnEnd) 실행

### 1.2 TagSystem
- 상태/제약을 태그로 유지(예: `State.Move.Blocked`, `State.Attacking`)
- 태그 카운트/스택 기반 시스템(유물 스택, 1회 보호막 태그 등)에 사용

### 1.3 AttributeSet
- Health/MoveSpeed/CooldownReduction 같은 핵심 스탯 컨테이너
- GameplayEffect가 스탯을 변경할 때 최종 반영 지점

### 1.4 GameplayEffectRunner
- Instant/Duration 효과 적용 및 수명 관리(TimeRemaining)
- Duration 동안 태그 부여/회수, Cue WhileActive Add/Remove, OnRemove 실행
- “남은 시간 감소” 같은 유물 기능을 구현하기 위한 API 제공

---

## 2) 가장 중요한 API 목록 (팀원이 자주 호출하는 함수)

### 2.1 AbilitySystem API
- **능력 부여**
  - `GiveAbility(AbilityDefinition def)`
  - (보통 Inspector의 `initialAbilities`로 초기 부여)

- **능력 발동**
  - `TryActivateAbility(AbilityDefinition ability, GameObject target = null)`
  - `TryActivateAbility(AbilitySpec spec, GameObject target = null)`

- **게임플레이 이벤트 발송(표준 트리거)**
  - `SendGameplayEvent(GameplayTag tag, AbilityEventData data = default)`
  - 예: 애니 타격 프레임, 대쉬 시작, 투사체 적중, 히트 확정 등

- **취소**
  - `CancelCasting(bool force=false)`
  - `CancelExecution(bool force=false)`
  - `interruptible=false`인 능력은 기본적으로 취소되지 않음. 시스템 상황(무기 교체 등)에서는 `force=true` 권장.

- **무기 애니메이터 등록(무기 교체 필수)**
  - `RegisterWeaponAnimator(Animator newWeaponAnimator)`
  - `OnWeaponEquipped()` : 무기 교체 시 호출(권장: 내부에서 무기 채널 실행 중이면 강제 취소)

- **EffectSpec 생성(=SetByCaller/Context 전달)**
  - `MakeSpec(GameplayEffect effect, GameObject causer, Object sourceObject)`

- **쿨다운 조회**
  - `GetCooldownRemaining(AbilityDefinition ability)`  
    (쿨다운이 GE 기반이면 Runner에서 RemainingTime을 조회)

### 2.2 GameplayEffectRunner API
- **Spec 기반 적용(추천: SetByCaller/Context 필요할 때)**
  - `ApplyEffectSpec(GameplayEffectSpec spec, GameObject target)`
  - Instant + Duration 모두 지원 (Duration은 activeEffects로 수명 관리)

- **Effect 기반 적용(간단/레거시)**
  - `ApplyEffect(GameplayEffect effect, GameObject target, GameObject instigator)`

- **쿨다운/버프 남은 시간 조작(유물 기능 핵심)**
  - `ReduceRemainingTimeByGrantedTag(target, tag, reduceSeconds)`
  - `MultiplyRemainingTimeByGrantedTag(target, tag, multiplier)`

---

## 3) Tag 생성/로딩 규칙 (중요)

- 태그 생성은 **GameplayTagEditor(EditorWindow)** 를 사용한다.
- 태그 에셋은 `Resources/Tags` 경로에 있어야 런타임 로드된다.
- 런타임에는 `TagRegistry`가 `Resources.LoadAll<GameplayTag>("Tags")`로 로드하고, 이름(path) 기반으로 id를 부여한다.

**절대 규칙**
- 같은 의미의 태그를 “다른 에셋”으로 중복 생성하지 말 것(혼란/검색/운영 문제).  
  (CueKey는 TagRegistry id 기반으로 보강되어 안전성이 올라갔지만, 운영상 중복은 금지 권장)

---

## 4) Ability 제작 표준 파이프라인 (팀원이 가장 많이 쓰는 절차)

아래 6단계를 따르면 “프레임워크 코드 수정 없이” 신규 스킬을 추가할 수 있습니다.

### Step 1) Tags 만들기
- 상태 태그: `State.*`
- 이벤트 태그: `Event.*`
- 연출 태그(Cue): `Cue.*`
- 쿨다운 태그: `Cooldown.*` (GE 쿨다운에서 grantedTags로 부여)

### Step 2) GameplayEffect 만들기
- 데미지: `GE_Damage_Spec` 사용(권장)  
  - `damageKey` = `Data.Damage`
- 버프/디버프(Duration): `GameplayEffect(SO)`로 만들고
  - `duration`, `grantedTags`, `cueOnExecute/WhileActive/OnRemove` 등을 설정

### Step 3) Typed Data SO 만들기 (능력 타입별 세부 데이터)
예: `SwordCombo2DData`, `BowChargeShotData` 등  
- 데미지/히트박스/트리거명/레이어/이벤트 태그/사용할 Effect 등을 Data에 모음

### Step 4) AbilityLogic SO 만들기 (실행만 담당)
- **중요 원칙:** Logic은 밸런스 값을 소유하지 않는다.
- `var data = spec.Definition.sourceObject as XXXData;` 로 Data를 가져온다.
- 런타임 상태(콤보 인덱스, 만료시간, RecoveryOverride 등)는 **AbilitySpec 변수**로 저장한다.

### Step 5) AbilityDefinition SO 만들기 (공통 정책/연결)
- `logic` 연결
- `sourceObject`에 Data SO 연결
- `grantedTagsWhileActive` 설정
- `cooldown / cooldownEffect / castTime / recoveryTime` 설정
- `canCastWhileMoving / interruptible` 설정
- `animationChannel` 설정 (Player/Weapon)
- (선택) Cue 태그 연결

### Step 6) AbilitySystem에 부여
- 테스트/샘플은 `AbilitySystem.initialAbilities`에 추가
- 런타임 부여는 `GiveAbility(def)` 사용

---

## 5) Ability 실행 흐름(라이프사이클) – 어디서 무엇이 호출되나

### 5.1 Activate(입력)
`TryActivateAbility(def)` 호출 → 조건 검사 → Busy면 버퍼 → 캐스팅 시작

### 5.2 Casting(캐스팅)
- `castTime > 0`이면 캐스팅 상태 유지
- Cue: `cueOnCastStart` (Execute), `cueWhileCasting` (Add/Remove)

### 5.3 Commit(캐스팅 완료)
`CompleteCast()`에서:
- 코스트 지불
- (옵션) Definition의 `animationTrigger` 자동 실행
- Cue: `cueOnCommit` (Execute)

### 5.4 Execution(실행)
`RunAbility()`에서:
- `grantedTagsWhileActive` 부여
- Cue: `cueWhileActive` Add
- **EffectContainers: OnActivate 실행**
- `AbilityLogic.Activate()` 코루틴 실행
- recoveryTime 처리(RecoveryOverride 지원)
- finally:
  - **EffectContainers: OnEnd 실행**
  - 태그 회수, Cue 제거/End/Cancelled 실행

### 5.5 Execution 중 이벤트 처리(OnEvent)
Ability 실행 중 `SendGameplayEvent(tag, data)`가 들어오면:
- 이 Ability의 `containers` 중 `OnEvent` 타이밍을 가진 컨테이너가 실행된다.
- 애니 타격 프레임/투사체 적중/히트 확정 등의 트리거를 여기에 연결하면 확장성이 매우 좋아진다.

---

## 6) 쿨다운: Spec 타이머 vs GE(Duration) (권장: GE)

### 6.1 기본(레거시): `AbilitySpec.CooldownRemaining`
- `cooldownEffect == null`일 때 사용
- 간단하지만, “타격 시 쿨감/특정 무기만 쿨감” 같은 GAS식 확장은 불리함

### 6.2 권장: `cooldownEffect`(Duration GE)로 쿨다운 관리
- AbilityDefinition에 `cooldownEffect`를 지정하면
  - `StartCooldown`에서 `cdSpec.SetDuration(def.cooldown)` 후 `ApplyEffectSpec`로 적용
  - `IsOnCooldown`/`GetCooldownRemaining`은 Runner에서 RemainingTime 조회

**유물 “타격 시 쿨감” 구현 예시**
- 쿨다운 GE의 `grantedTags`에 `Cooldown.Weapon.Sword.Basic` 같은 태그 부여
- 타격 이벤트 발생 시:
  - `effectRunner.ReduceRemainingTimeByGrantedTag(player, Cooldown.Weapon.Sword.Basic, 0.3f);`

---

## 7) GameplayCue(연출) 사용 규칙

- **Ability 연출**: AbilityDefinition의 cue 필드로 연결  
  - CastStart / WhileCasting / Commit / WhileActive / End / Cancelled
- **Effect 연출**: GameplayEffect의 cue 필드로 연결  
  - OnExecute / WhileActive / OnRemove

> 원칙: “상태(버프/디버프)의 연출은 Effect”, “스킬 생명주기의 연출은 Ability”.

---

## 8) 애니 이벤트 / 이벤트 버스 연결(가장 중요한 실무 규칙)

### 8.1 표준 규칙
- 애니메이션 타이밍(타격 프레임 등)은 **Animation Event → AbilityAnimationEventRelay → AbilitySystem.SendGameplayEvent** 로 전달한다.
- AbilityLogic은 `AbilityTasks.WaitGameplayEvent(tag, predicate: d.Spec == spec)` 패턴으로 동기화한다.

### 8.2 Weapon/Player Animator 분리 구조
- **Player Animator**: 이동/대쉬/피격/사망 등 기본 동작
- **Weapon Animator**: 공격/스킬 등 무기 액션

필수:
- 무기 프리팹에는 `Animator + AbilityAnimationEventRelay`가 있어야 한다.
- 무기 장착 시:
  - `abilitySystem.RegisterWeaponAnimator(weaponAnimator)`
  - 무기 프리팹 내부 Relay들에 `Bind(abilitySystem)` (또는 Inspector 연결)

> 무기 교체 시 실행 중인 무기 채널 능력은 정책적으로 취소하는 것을 강력 권장합니다.  
> (`CancelExecution(force:true)` 권장)

---

## 9) 이동 중 시전 제한(canCastWhileMoving)
- AbilityDefinition에서 `canCastWhileMoving=false`면, AbilitySystem이 `IMovementStateProvider.IsMoving`을 확인한다.
- 이동 컴포넌트가 `IMovementStateProvider`를 구현해야 이 기능이 동작한다.

---

## 10) 디버깅 체크리스트(안 될 때 순서대로)

1) 태그가 실제로 로드됐는가? (`Resources/Tags` 위치 확인)
2) AbilityDefinition이 AbilitySystem에 부여됐는가? (`initialAbilities` 또는 `GiveAbility`)
3) ability의 `logic/sourceObject`가 올바르게 연결됐는가?
4) 쿨다운이 GE 기반이면 `cooldownEffect`의 duration/grantedTags가 맞는가?
5) 이벤트가 들어오는가?
   - Animation Event 함수명/파라미터가 Relay와 일치하는가?
   - `AbilityEventData.Spec`가 현재 실행 spec로 들어오는가?
6) 데미지가 Spec 경로로 적용되는가?
   - `GE_Damage_Spec.damageKey`에 SetByCaller가 들어가는가?
7) Weapon Animator 라우팅이 맞는가?
   - ability.animationChannel = Weapon/Player 설정
   - 무기 장착 시 RegisterWeaponAnimator 호출

---

## 11) 팀 운영 규칙(권장)

- Health 직접 변경 금지 → **GE_Damage_Spec**로만 적용
- 새 Ability 추가 시:
  - **AbilityDefinition + Typed Data SO + AbilityLogic SO**를 기본 단위로 사용
- 이벤트는 반드시 AbilitySystem 이벤트 버스로 통합:
  - `SendGameplayEvent(tag, data)`
- 쿨다운/버프/디버프는 가능하면 GE(Duration)로 표현:
  - 유물/패시브 확장성이 올라간다.

---

### Appendix: “최소 샘플” 제작 순서(검 콤보)
1) Tags: `State.Attacking`, `State.Move.Blocked`, `Event.Anim.SwordCombo.Hit`, `Data.Damage`
2) `GE_Damage_Spec` 생성 + healthAttribute/damageKey 설정
3) `SwordCombo2DData` 생성(데미지/트리거/히트박스/레이어/히트태그/GE)
4) `AL_SwordCombo2D` 생성
5) `AD_SwordCombo2D` 생성 + 연결
6) Weapon Animator에 Attack 애니 + 타격 프레임 이벤트(Relay.SendEvent)
7) 무기 장착 시 AbilitySystem에 WeaponAnimator 등록 후 테스트
