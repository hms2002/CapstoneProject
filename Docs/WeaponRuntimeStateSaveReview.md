# Weapon Runtime State Save Review

> Legacy / Review  
> 이 문서는 무기 런타임 상태 저장/복원 범위를 검토하기 위해 작성된 리뷰 문서입니다.  
> 현재 표준 문서가 아니라, 저장 정책 판단과 전환 이력을 보관하는 용도로 봅니다.

이 문서는 `WeaponAbilityRuntimeState` 자체를 저장/복원해야 하는지 판단하고, 1차 구현 범위를 기록하기 위한 메모입니다.

## 현재 상태

- 저장되는 것
  - 무기가 소유한 ability spec의 persistent state
  - 쿨다운, charges 등 ability 쪽 상태
  - 인벤토리 슬롯이 소유한 `WeaponRuntimeData` 중 `IWeaponRuntimeStatePersistence`를 구현한 값 타입 중심 상태
- 저장되지 않는 것
  - 프리팹 live component에만 존재하는 임시 상태
  - `GameObject` 참조 기반 타깃 정보
  - 예: 최근 적중 대상, 링크 대상

## 1차 구현 상태

- `WeaponAbilityRuntimeStateBridge`
  - `abilityLoadout` 기준으로 무기 ability persistent state를 저장/복원합니다.
  - `WeaponRuntimeData`도 같은 `weaponRuntimeStates` 리스트에 `stateType`별 entry로 저장합니다.
- `IWeaponRuntimeStatePersistence`
  - 저장이 필요한 slot-owned runtime data만 opt-in 하도록 분리했습니다.
- `EclipseSwordRuntimeData`
  - 자세 여부, 다음 자세 공격 인덱스, 누적 공격 횟수, bloom 가능 여부를 저장/복원합니다.
  - 실제로 씬 전환 뒤에도 기본 공격 상태 전환이 이어지고, 비활성 슬롯에서도 상태가 유지되는 것을 검증했습니다.
- `EclipseSwordRuntimeState`
  - 상태 소유자가 아니라 inventory-owned runtime data를 읽고 쓰는 thin adapter로 축소됐습니다.

## 현재 제약

- 아직 월식도만 `WeaponRuntimeData` 기반으로 옮겨졌습니다.
- 대검/사슬창은 여전히 live component 쪽 상태 비중이 크므로, 같은 전환을 하려면 별도 설계가 더 필요합니다.
- 사슬창처럼 씬 참조를 들고 있는 상태는 지금도 저장 대상에서 제외하는 게 맞습니다.
- 대검처럼 장면 전환 뒤 유지 가치가 불분명한 대기 상태도 기본은 "저장 안 함"으로 두는 편이 안전합니다.

## 지금 당장 급하지 않은 이유

- 현재 검증 무기들은 장면 전환/저장 복원보다 구조 검증이 목적이었습니다.
- 월식도, 대검, 사슬창 모두 전투 중 즉시 상태만 다뤘고, 세션 간 복원을 아직 요구하지 않았습니다.

## 저장이 필요해지는 조건

- 장면 전환 후에도 자세/링크/차지 같은 무기 상태를 유지해야 할 때
- 허브나 던전 전환 사이에 무기 상태를 플레이 감각으로 이어가야 할 때
- 실제 제작 무기에서 runtime state가 단순 전투 중 임시값을 넘어서 메타 진행 정보가 될 때

## 추천 판단 기준

- 전투 중 임시 상태인가
  - 그렇다면 저장 없이 시작 시 리셋해도 됩니다.
- 장면 전환 후에도 이어져야 하는 상태인가
  - 그렇다면 저장/복원을 붙여야 합니다.
- 타깃 GameObject 참조처럼 세션 간 무의미한 값인가
  - 이런 값은 저장 대상에서 제외하는 편이 좋습니다.

## 다음 단계 제안

1. 월식도처럼 저장 가치가 분명한 무기부터 `WeaponRuntimeData` 기반으로 전환합니다.
2. 대검/사슬창처럼 저장 가치가 애매한 상태는 "저장 안 함" 또는 partial 저장을 기본으로 두고 다시 판단합니다.
3. live component는 상태 소유자가 아니라 thin adapter / executor host로 유지합니다.
4. 어떤 무기까지 data 기반 전환할지 실제 제작 우선순위에 맞춰 결정합니다.
