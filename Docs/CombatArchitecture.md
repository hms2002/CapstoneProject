# Combat Architecture

이 문서는 현재 프로젝트의 전투 피해/피격 처리 흐름을 큰 그림으로 설명합니다.

## 어디서 시작하나

- 실제 피해 적용의 중심:
  - [CombatDamageAction.cs](../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Combat/Runtime/CombatDamageAction.cs)

- hazard 쪽 피해 흐름:
  - `HazardDamageAction`

- 무적/회피 판정 보조:
  - `CombatInvulnerabilityUtil`
  - `CombatEvasionUtil`

## 핵심 책임 분리

- `CombatDamageAction`
  - 피해, 넉백, 경직 게이지, 속성 게이지, hit confirmed 이벤트를 한 곳에서 조율합니다.
  - 전투 계층에서 GAS를 호출하는 가장 중요한 브리지입니다.

- `AbilitySystem / GameplayEffectRunner`
  - 실제 HP 감소와 효과 적용은 GAS 쪽에서 처리합니다.

- `StaggerGaugeSystem`
  - 경직 누적을 담당합니다.

- `ElementGaugeSystem`
  - 속성 누적과 반응 게이지를 담당합니다.

- `PlayerHitFeedback2D / MonsterHitFeedback2D`
  - 피격 시각/청각 피드백을 담당합니다.

## 현재 구조에서 기억할 점

- 무적 상태는 피해 자체뿐 아니라 `EVADE` 같은 텍스트 노출 규칙에도 영향을 줍니다.
- 피해 적용 규칙을 바꾸고 싶으면 먼저 `CombatDamageAction`을 보세요.
- 각 능력/투사체가 피해를 직접 처리하기보다, 공용 피해 경로를 타게 유지하는 게 중요합니다.

## 어떤 수정이 어디에 가까운가

### HP가 왜 줄었는지 보고 싶다
- `CombatDamageAction`
- `GE_Damage_Spec`

### 넉백 규칙을 바꾸고 싶다
- `CombatDamageAction`
- `GE_Knockback_Spec`

### 그로기/경직이 왜 쌓이는지 보고 싶다
- `CombatDamageAction`
- `StaggerGaugeSystem`

### 속성 게이지가 왜 쌓이는지 보고 싶다
- `CombatDamageAction`
- `ElementGaugeSystem`

## 주의할 점

- 전투 로직이 GAS를 우회해서 직접 HP를 깎는 방향으로 퍼지지 않게 해야 합니다.
- invulnerable / evade / hit feedback 규칙은 한쪽만 바꾸면 체감이 어긋날 수 있습니다.
