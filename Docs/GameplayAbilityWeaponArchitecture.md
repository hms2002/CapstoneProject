# Gameplay Ability Weapon Architecture

이 문서는 현재 프로젝트에서 `무기 <-> GAS` 경계를 어떻게 나누는지 설명하는 상위 구조 문서입니다.

## 핵심 원칙

- `WeaponDefinition`
  - 무기 정의 자산입니다.
  - 기본 정보와 optional `WeaponAbilityLoadout` 참조를 가집니다.

- `WeaponAbilityLoadout`
  - 그 무기가 사용할 공식 AD 참조 소켓을 가집니다.
  - 범용 후보 리스트보다 전용 참조 소켓 중심으로 설계합니다.
  - 필요하면 기대하는 `RuntimeData` 와 `RuntimeProcessor` 타입도 함께 선언해 factory/authoring validation의 기준점이 됩니다.

- `WeaponAbilitySelectionStrategy`
  - 현재 상태에서 어떤 AD를 실행할지 결정합니다.
  - selector 본체가 비대해지지 않도록 무기별 규칙을 분리합니다.

- `WeaponRuntimeData`
  - 인벤토리 슬롯이 소유하는 persistent runtime state입니다.
  - 장착 여부와 무관하게 스택, 자세, 누적 상태처럼 런 내내 이어져야 하는 정보를 진실한 저장소로 가집니다.

- `WeaponRuntimeProcessor`
  - `WeaponRuntimeData` 자체에 `Tick()`을 넣지 않고, 시간 경과/감쇠/만료 같은 상태 변화 규칙만 별도 계층에서 적용합니다.
  - 비활성 슬롯 무기도 같은 규칙으로 갱신되게 만들어 persistent state와 live behavior를 분리합니다.

- `WeaponRuntimeCoordinator`
  - 인벤토리가 소유한 슬롯별 `WeaponRuntimeData`와 `WeaponRuntimeProcessor`를 함께 관리합니다.
  - 전체 슬롯에 시간 경과를 공급하고, 현재 슬롯/반대 슬롯 문맥을 processor 쪽에 넘기는 coordinator 역할을 맡습니다.
  - 외부 상호작용 계층이 persistent state를 반영해야 할 때 공식 mutation 창구 역할도 맡습니다.

- `WeaponInteractionLayer`
  - 장착 중 runtime state가 올린 "교차 무기 상호작용 사실"을 pair rule로 해석하는 상위 계층입니다.
  - runtime state가 pair rule, command routing, 특정 조합 규칙 구현을 직접 알지 않게 만드는 추상 경계입니다.

- `WeaponPairInteractionRuleRegistry`
  - 프로젝트가 기본으로 쓰는 pair rule 집합을 한 곳에서 등록/생성하는 레지스트리입니다.
  - interaction layer가 조합 규칙 인스턴스 생성을 직접 하드코딩하지 않게 만들어 pair rule이 늘어날 때 비대해지는 속도를 줄입니다.

- `WeaponPairInteractionRule`
  - 두 무기 조합의 전투 문법을 해석하는 전용 규칙 객체입니다.
  - 현재 조합에서 어떤 슬롯 상태가 소비/개방/역반영되어야 하는지 결정하고, 실제 상태 변경은 coordinator에 요청합니다.

- `WeaponAbilityRuntimeState`
  - 장착 무기 프리팹 위에서 돌아가는 live adapter / live hook입니다.
  - 장기적으로는 상태를 직접 소유하기보다 `WeaponRuntimeData`를 읽고 갱신하는 thin adapter 역할을 맡습니다.

- `WeaponAbilityExecutor`
  - 시간이 흐르며 유지되는 액션의 시작, 대기, 외부 이벤트 반응, 취소, 완료, cleanup을 담당합니다.
  - selector가 선택을 끝낸 뒤의 "긴 실행 구간"을 공용 생명주기 틀로 운영합니다.
  - 종료는 `WeaponExecutorEndReason`을 동반한 단일 경로로 정리되며, cleanup은 베이스가 항상 강제 호출합니다.

- `WeaponExecutorRunner`
  - 현재 활성 executor 1개를 소유하고 시작/취소/강제 종료/종료 감시를 중앙에서 관리합니다.
  - relay가 전달한 gameplay event를 현재 executor에만 흘려 보내 executor가 ASC를 직접 구독하지 않게 만듭니다.
  - 무기 교체, owner disable 같은 전역 종료 사유도 runner가 명시적으로 전달합니다.

- `WeaponAbilitySelector`
  - 현재 무기, loadout, runtime state, runtime data를 읽고 실행할 AD를 고릅니다.
  - 필요하면 현재 슬롯과 반대 슬롯의 무기/runtime data를 함께 문맥으로 넘겨 쌍무기 상호참조 선택 규칙도 처리합니다.
  - 직접 검색기가 아니라, 준비된 정보를 바탕으로 결정만 합니다.

- `WeaponAbilityBridge`
  - 선택된 AD를 ASC/GAS에 실행 요청합니다.

- `AbilitySystem`
  - 선택된 AD의 시작/종료, 공통 태그, 쿨다운 같은 공용 실행만 담당합니다.

## 현재 우리가 지키는 경계

- 상태는 RuntimeState가 가진다.
- 지속 상태의 진실한 소유자는 RuntimeData가 가진다.
- 시간 경과 규칙은 RuntimeProcessor가 가진다.
- 전체 슬롯 갱신 책임은 RuntimeCoordinator가 가진다.
- AD 참조는 Loadout이 가진다.
- 선택은 Strategy가 한다.
- 긴 실행은 Executor가 가진다.
- Executor 생명주기는 Runner가 가진다.
- 쌍무기 상호작용 해석은 InteractionLayer / PairInteractionRule이 가진다.
- pair rule 등록 책임은 PairInteractionRuleRegistry가 가진다.
- 다른 무기 상태의 실제 반영은 Coordinator가 가진다.
- cleanup 호출 책임은 Executor 베이스가 가진다.
- 입력 계층은 Selector/Bridge를 경유한다.
- ASC는 무기 문맥을 직접 소유하지 않는다.

## 기준 사례

- [Eclipse Sword Pattern Guide](../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/EclipseSwordPatternGuide.md)
- [Dual Weapon Pattern Guide](./DualWeaponPatternGuide.md)
- [Weapon GAS Assessment](./WeaponGASAssessment.md)

월식도는 다음을 검증한 기준 사례입니다.

- 기본 상태와 자세 상태에서 같은 입력이 다른 AD를 선택한다.
- 런타임 상태가 누적과 분기를 가진다.
- `Skill1`이 Enter / Exit / Bloom Finish로 분기된다.

추가로 최근 검증한 구조는 다음을 확인했습니다.

- 대검 처형자
  - 실제 `HitConfirm` 이후에만 Finish 분기가 열린다.
- 사슬창
  - 관계 상태를 만든 뒤 후속 입력이 그 상태를 소비한다.
  - `Throw` 이후 링크 대기 구간은 executor가 시간축으로 운영한다.
- 월식도 저장/복원
  - inventory-owned runtime data가 비활성 슬롯에서도 유지되고, 씬 전환 뒤에도 이어진다.
- 표식검 + 처형총
  - 현재 슬롯과 반대 슬롯 runtime data를 함께 읽는 `WeaponSelectionContext` 확장이 실제 선택 규칙에 먹힌다.
  - 비활성 슬롯에 있는 다른 무기의 스택/창 상태도 selector가 자연스럽게 참조할 수 있다.
  - `WeaponRuntimeProcessor + WeaponRuntimeCoordinator` 조합으로 표식 감쇠와 반격 창 만료가 비활성 슬롯에서도 진행된다.
  - `WeaponInteractionLayer -> PairInteractionRule -> Coordinator` 경유로 runtime state의 direct cross-write를 제거했다.
- 태양도 + 월영도
  - 실제 제작형 쌍무기 사례로, 각 슬롯의 `heat/cold` 스택이 반대 슬롯의 일반 공격과 `Skill1` 선택을 바꾸는 흐름을 검증했다.
  - `SunBladeRuntimeProcessor / MoonBladeRuntimeProcessor`가 비활성 슬롯에서도 스택 감쇠를 유지해, 실제 제작 무기에서도 inventory-owned runtime data 구조가 자연스럽게 작동함을 확인했다.
  - `SunMoonInteractionRule`이 공명 피니시 사용 시 양쪽 스택을 함께 소비해, pair rule이 "조합 전투 문법 해석" 역할에 머무르는 구조를 실제 무기로 검증했다.

## 다음 무기 설계 시 체크리스트

### 무기별 상태가 필요한가
- 필요하면 슬롯이 소유할 `WeaponRuntimeData`를 먼저 정의합니다.
- 장착 중 live behavior가 필요할 때만 `WeaponAbilityRuntimeState` thin adapter를 둡니다.

### 비활성 무기 상태도 시간에 따라 변해야 하는가
- 그렇다면 `WeaponRuntimeData`에는 상태만 두고, 별도 `WeaponRuntimeProcessor`를 만들어 시간 경과 규칙을 처리합니다.
- 전체 슬롯 갱신은 `WeaponRuntimeCoordinator`가 맡게 두어 프리팹 생명주기와 분리합니다.

### 다른 무기가 내 상태를 읽거나 내가 다른 무기 상태를 읽어야 하는가
- `WeaponSelectionContext`에 현재 슬롯과 반대 슬롯의 무기/runtime data를 함께 담아 전략이 직접 참조하게 합니다.
- 상태 변경은 strategy가 아니라 실행 후 경로(`RuntimeState`, `HandleGameplayEvent`, `Executor`)에서 처리합니다.
- 다른 무기 상태를 직접 수정하지 말고, `WeaponInteractionLayer`에 사실을 알린 뒤 pair rule이 coordinator를 통해 반영하게 합니다.

### 입력별 AD가 고정이 아닌가
- 그렇다면 전용 `WeaponAbilityLoadout` 과 `WeaponAbilitySelectionStrategy` 를 만듭니다.

### 범용 리스트가 아니라 명시적 참조 소켓이 더 읽기 쉬운가
- 그렇다면 전용 WAL 타입을 만듭니다.

### 구조 검증용인지, 실제 무기 제작인지
- 구조 검증용이면 로그형 AL/AD부터 시작합니다.
- 실제 무기 제작이면 이후 executor/연출/판정까지 확장합니다.

### 시간에 걸친 대기/분기/취소가 필요한가
- 필요하면 `WeaponAbilityExecutor` 와 `WeaponExecutorRunner` 를 함께 설계합니다.
- selector/runtime state는 "무엇을 시작할지"까지만 맡고, 시작 후 운영은 executor로 넘깁니다.
