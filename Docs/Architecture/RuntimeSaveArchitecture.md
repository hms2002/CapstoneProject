---
status: active
authority: source-of-truth
category: architecture
last_reviewed: 2026-05-05
---

# Runtime Save Architecture

이 문서는 씬 이동 시 플레이어, 장비, GAS 런타임 상태가 어떻게 저장/복원되는지 큰 그림을 설명합니다.

## 어디서 시작하나

- 루트 DTO:
  - [PlayerRuntimeState.cs](../../Assets/LeeJunMo/Script/SceneManagement/PlayerRuntimeState.cs)

- 무기 인벤토리 복원:
  - [WeaponInventory2D.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Inventory/WeaponInventory2D.cs)

- 무기 ability 영속 상태 브리지:
  - [WeaponAbilityRuntimeStateBridge.cs](../../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/WeaponAbilityRuntimeStateBridge.cs)

## 핵심 책임 분리

- `PlayerRuntimeState`
  - 플레이어 루트 저장 DTO입니다.
  - 장비 배치, GAS 런타임 상태, 장비별 runtime state 저장 슬롯을 함께 가집니다.

- `WeaponInventory2D`
  - 무기 슬롯 배치(shell state) 복원과 장착 상태 복원을 담당합니다.

- `WeaponAbilityRuntimeStateBridge`
  - 무기 소유 ability들의 persistent state를 저장/복원합니다.

- `PlayerStatusRuntime`
  - 플레이어 상태를 적용/갱신/회수하는 허브입니다.
  - 현재 구조에선 버프/디버프의 장기 영속 저장소가 아니라, owner가 재등록할 실행 허브에 가깝습니다.

## 현재 구조에서 기억할 점

- 지금 저장되는 건 주로 **무기 소유 ability persistent state**입니다.
- `EclipseSwordRuntimeState` 같은 **무기 전용 MonoBehaviour 상태값은 아직 별도 저장되지 않습니다.**
- 플레이어 상태 HUD에 올라가는 버프/디버프도 현재는 **상태 시스템 자체가 직접 저장하지 않습니다.**
- 대신 상태의 진짜 owner가 씬 전환 뒤 다시 `Apply(...)`를 호출하는 **재등록 모델**을 기본으로 삼습니다.

즉 현재는:
- `AD/AL` 실행 상태 일부는 저장 가능
- 무기 커스텀 전투 상태는 아직 저장 구조가 별도로 필요할 수 있음

## 어떤 수정이 어디에 가까운가

### 씬 이동 후 무기 슬롯 배치가 왜 바뀌는지 보고 싶다
- `PlayerRuntimeState`
- `WeaponInventory2D.RestoreShellState(...)`

### 무기 ability 쿨다운/차지가 왜 복원되는지 보고 싶다
- `WeaponAbilityRuntimeStateBridge`

### 월식도 같은 무기의 자세 상태까지 저장하고 싶다
- 현재는 미지원에 가깝습니다
- `WeaponAbilityRuntimeState` 저장 경로를 별도로 설계해야 합니다

### 씬 전환 뒤에도 유지되는 버프/디버프를 붙이고 싶다
- 먼저 "이 상태의 진짜 owner가 누구인가"를 판단해야 합니다.
- 현재 기본 구조는 `PlayerStatusRuntime`이 상태를 직접 저장/복원하기보다, owner가 자기 데이터를 복구한 뒤 다시 상태를 등록하는 방식입니다.
- 예:
  - 씬 상시 시야 제한
    - 새 씬의 `SceneRestrictedVisionController`가 플레이어 등록 시 다시 `Apply(...)`
  - 유물/런 전체 버프
    - 유물 시스템 또는 런 시스템이 복구 뒤 다시 `Apply(...)`
  - 유물 proc owner 버프
    - 유물 시스템이 proc를 다시 만들고, proc가 필요 시 `Apply(...)`를 다시 호출

즉 유물 상태도 저장 DTO를 상태 시스템이 직접 들고 가기보다,
**유물 owner가 자기 수명을 복구한 뒤 다시 등록하는 모델**을 기본으로 봅니다.

## 주의할 점

- shell restore와 runtime restore는 분리해서 생각해야 합니다.
- 무기 커스텀 runtime state 저장을 붙일 때는 ability persistent state와 중복 책임이 생기지 않게 해야 합니다.
- 상태 HUD 시스템은 현재 "저장소"보다 "적용 허브"에 가깝습니다.
- 따라서 상태를 새로 만들 때는 "이걸 저장할까?"보다 "누가 복구 후 다시 등록할까?"를 먼저 판단하는 편이 더 자연스럽습니다.
