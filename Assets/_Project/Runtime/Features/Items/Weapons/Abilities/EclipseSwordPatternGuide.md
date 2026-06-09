# Eclipse Sword Pattern Guide

월식도는 현재 무기 `<-> GAS` 경계 구조의 기준 사례입니다.

## 책임 분리

- `EclipseSwordLoadout`
  - 월식도가 사용할 공식 AD 참조 소켓을 소유합니다.
  - selector가 검색 없이 필요한 AD를 직접 읽도록 합니다.

- `EclipseSwordRuntimeData`
  - 월식도가 슬롯 단위로 유지해야 하는 persistent state의 진실한 저장소입니다.
  - 자세 여부, 다음 공격 인덱스, 누적 공격 수, Bloom 가능 여부를 들고 있습니다.

- `EclipseSwordRuntimeState`
  - 장착 중 프리팹 live component입니다.
  - 실제 상태를 소유하지 않고 `EclipseSwordRuntimeData`를 읽고/갱신하는 thin adapter 역할을 합니다.

- `EclipseSwordSelectionStrategy`
  - 현재 runtime data를 읽고 입력 슬롯별로 실행할 AD를 결정합니다.
  - `Attack`은 기본 공격 또는 자세 A/B로, `Skill1`은 Enter / Exit / Bloom으로 분기합니다.

- `WeaponAbilitySelector`
  - 월식도 세부 규칙을 몰라도 됩니다.
  - `Loadout + Strategy + RuntimeState` 조합만 호출합니다.

- `AbilitySystem`
  - 선택된 AD를 시작/종료하는 공통 실행기 역할만 맡습니다.

## 현재 검증 범위

1. 기본 상태 `Attack -> Base Attack`
2. `Skill1 -> Enter Stance`
3. 자세 중 `Attack -> Stance A/B`
4. 자세 중 누적 후 `Skill1 -> Bloom Finish`
5. 자세 종료 후 `Attack -> Base Attack`
6. 비활성 슬롯에 둔 뒤 씬 전환해도 기본 공격 상태 전환 유지

## 다음 무기 설계 시 참고 규칙

- persistent 상태는 `RuntimeData`가 가진다.
- `RuntimeState` component는 가능하면 thin adapter로 둔다.
- AD 참조는 `Loadout`이 가진다.
- 선택 규칙은 `SelectionStrategy`가 가진다.
- ASC/GAS는 선택된 AD 실행만 맡는다.
