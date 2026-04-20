# Gameplay Status Architecture

이 문서는 플레이어 상태 UI와 런타임 상태 적용 구조를 설명하는 상위 문서입니다.

현재 구조는 **상태를 영속 저장하는 전용 저장소**보다, **상태를 적용하고 회수하는 허브**에 가깝게 설계되어 있습니다.

## 어디서 시작하나

- 플레이어 상태 적용 허브
  - [PlayerStatusRuntime.cs](../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Status/Runtime/PlayerStatusRuntime.cs)
- 상태 적용 요청 값
  - [StatusApplyRequest.cs](../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Status/Runtime/StatusApplyRequest.cs)
- 해제 토큰
  - [StatusHandle.cs](../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Status/Runtime/StatusHandle.cs)
- HUD 표시 정의 SO
  - [StatusHudDefinition.cs](../Assets/HeoMinSeok/_Project/Scripts/UI/HUD/Status/StatusHudDefinition.cs)
- 플레이어 상태 HUD source
  - [PlayerStatusHudSource.cs](../Assets/HeoMinSeok/_Project/Scripts/UI/HUD/Status/PlayerStatusHudSource.cs)
- 씬 상시 시야 제한 사례
  - [SceneRestrictedVisionController.cs](../Assets/Script/Enemy/Mob/ShadowServant/SceneRestrictedVisionController.cs)
- 유물 상태 owner 사례
  - [MoveSpeedOnKillProc.cs](../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Relics/Runtime/Procs/MoveSpeedOnKillProc.cs)
  - [MoveSpeedOnDamagedProc.cs](../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Relics/Runtime/Procs/MoveSpeedOnDamagedProc.cs)
  - [RelicLogic_MoveSpeedStackOnCriticalHit_Managed.cs](../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Relics/Runtime/RelicLogic_MoveSpeedStackOnCriticalHit_Managed.cs)

## 핵심 원칙

- `PlayerStatusRuntime`
  - 플레이어에게 현재 적용 중인 상태를 등록/갱신/회수하는 **적용 허브**입니다.
  - 버프/디버프의 진짜 장기 저장소가 아닙니다.

- `StatusHudDefinition`
  - 상태의 아이콘, 이름, 스토리, 효과 설명, 그룹, 우선순위를 담는 **표시 정의 SO**입니다.
  - 실제 활성 여부, 남은 시간, 스택은 들고 있지 않습니다.

- `StatusApplyRequest`
  - 런타임 시점에 상태를 등록할 때 넘기는 값입니다.
  - 남은 시간, 스택, 강조 여부, 표시 override처럼 순간적으로 바뀌는 값은 이 struct가 가집니다.

- `StatusHandle`
  - 상태를 건 쪽이 들고 있는 해제 토큰입니다.
  - 상태를 등록한 owner는 자기 handle로만 상태를 회수합니다.

- `PlayerStatusHudSource`
  - `PlayerStatusRuntime`이 들고 있는 활성 상태를 HUD 엔트리로 투영합니다.
  - 상태 소유 로직과 HUD 표현을 분리합니다.

## 책임 분리

### `PlayerStatusRuntime`

이 계층은:

- 상태 등록
- 상태 갱신
- 상태 해제
- 현재 활성 상태 목록 유지

를 담당합니다.

이 계층이 하지 않는 일:

- 상태의 장기 영속 저장
- 지역/유물/런 시스템의 소유권 판단
- HUD 직접 렌더링

### 상태 owner

진짜 상태 소유자는 따로 있습니다.

예:

- 지역/씬 시스템
- 유물 시스템
- 런 진행 시스템
- 무기/상호작용 시스템

즉 `PlayerStatusRuntime`은 "누가 이 상태의 주인인가"를 몰라도 되고,  
owner가 `Apply(...)` / `Release(...)`로 자기 상태만 안전하게 붙였다 떼면 됩니다.

### HUD 계층

HUD는 다음처럼 분리됩니다.

- `StatusHudDefinition`
  - 표시 정의
- `PlayerStatusHudSource`
  - 현재 활성 상태 수집
- `StatusHudService`
  - source 집계
- `StatusHudPresenter`
  - 상태 슬롯 HUD 렌더링
- `StatusHudTooltipView`
  - hover 시 상세 정보 표시

즉 HUD는 상태를 소유하지 않고,  
현재 적용 중인 상태를 읽어서 표시만 합니다.

## 상태 수명 규칙

### 기본값은 저장보다 재등록

현재 구조의 기본 원칙은:

- 상태를 상태 시스템이 직접 저장/복원하지 않는다.
- 상태의 진짜 owner가 자기 데이터를 복구한 뒤 다시 등록한다.

이 방식이 더 좋은 이유:

- 상태 시스템이 과도하게 비대해지지 않습니다.
- 누가 이 상태의 주인인지 선명합니다.
- 지역 효과, 유물 버프, 런 전체 버프를 같은 패턴으로 다룰 수 있습니다.

### 씬 전환 규칙

- 이전 씬 owner는 `OnDisable`, `OnDestroy`, 해제 시점에 자기 handle을 회수합니다.
- 새 씬 owner는 플레이어 등록/씬 초기화 시점에 자기 조건을 다시 계산하고 `Apply(...)`를 다시 호출합니다.

즉 씬 전환은:

1. 이전 씬 회수
2. 새 씬 재등록

으로 이해하는 것이 맞습니다.

## 시야 제한 사례

현재 시야 제한은 두 층으로 나뉩니다.

- [SceneRestrictedVisionController.cs](../Assets/Script/Enemy/Mob/ShadowServant/SceneRestrictedVisionController.cs)
  - 씬이 플레이어에게 상시 `시야 제한` 상태를 등록합니다.
  - HUD 상태를 붙이고, 플레이어 비전 마스크 추적만 연결합니다.
  - dark overlay 알파는 직접 건드리지 않습니다.

- [GlobalVisionMaskController.cs](../Assets/Script/Enemy/Mob/ShadowServant/GlobalVisionMaskController.cs)
  - 전역 어둠 오버레이와 비전 마스크 연출을 담당합니다.
  - `AcquireDarkness/ReleaseDarkness`는 실제 안개/디버프처럼 화면을 더 어둡게 만들어야 할 때만 사용합니다.

- [RestrictedVisionVisualController.cs](../Assets/Script/Enemy/Mob/ShadowServant/RestrictedVisionVisualController.cs)
  - 접촉형/일시형 시야 제한처럼 실제 darkness 요청이 필요한 경우에만 overlay 알파를 진하게 만듭니다.
  - 상태/HUD는 다루지 않고 시야 차단 연출 브리지 역할만 맡습니다.

즉:

- 씬 상시 시야 제한
  - HUD 상태 등록
  - 마스크 추적만 유지
- 접촉형 안개 디버프
  - darkness 연출 + HUD 상태

로 역할을 나눕니다.

## HUD/Tooltip 구조

상태 HUD와 툴팁은 같은 계층이 아닙니다.

- HUD
  - `GameplayHUDCanvas`
  - 항상성 있는 상태 아이콘/스택/지속시간
- Tooltip
  - `HoverCanvas`
  - 커서를 올렸을 때만 보이는 상세 설명

툴팁의 정보 구조는 현재 다음 네 줄입니다.

1. `Icon`
2. `Name`
3. `Story`
4. `Effect`

즉 플레이 정보와 세계관 flavor text를 분리해서 읽기 쉽게 만드는 쪽을 기준으로 합니다.

## 현재 기준 사례

- `Heat / Cold`
  - 무기 런타임 상태를 SO 기반 표시 정의로 HUD에 올리는 사례
- `Restricted Vision`
  - 씬 owner가 상태를 재등록하고, 플레이어 상태 허브가 HUD에 표시하는 환경 디버프 사례
- `Move Speed On Kill`
  - 유물 proc가 시간제 버프의 owner가 되어 실제 능력치 버프와 HUD 지속시간을 함께 관리하는 사례
- `Move Speed On Damaged`
  - 피격 이벤트로 시작되는 시간제 버프를 유물 proc가 직접 신청/회수하는 사례
- `Move Speed Stack On Critical Hit`
  - 유물 proc가 스택형 상태를 owner로 들고 있으며, 피격 시 초기화와 HUD 스택 표시를 함께 갱신하는 사례

## 유물 상태 owner 패턴

유물 상태는 현재 다음 패턴을 기준으로 삼습니다.

- 상태 owner는 `RelicLogic` 자체보다 **실제 proc 인스턴스**가 맡습니다.
- proc는
  - gameplay event 반응
  - 실제 능력치 modifier 적용
  - `PlayerStatusRuntime.Apply/UpdateStatus/Release`
  를 한 owner 문맥에서 같이 처리합니다.

이렇게 두는 이유:

- 버프 시작/갱신/만료 시점을 proc가 가장 정확히 알고 있습니다.
- 씬 전환 후 유물 시스템이 proc를 다시 만들면, owner도 자연스럽게 다시 만들어집니다.
- `PlayerStatusRuntime`은 계속 적용 허브 역할만 유지하고, 유물 상태의 진짜 수명은 proc가 책임집니다.

### 시간형 유물 버프

예:

- `MoveSpeedOnKillProc`
- `MoveSpeedOnDamagedProc`

공통 규칙:

- 버프 시작 시 실제 modifier 적용
- 동시에 상태 HUD `Apply(...)`
- 남은 시간 갱신 중엔 `UpdateStatus(...)`
- 버프 종료/해제 시 `Release(...)`

### 스택형 유물 버프

예:

- `MoveSpeedStackOnCriticalHitProc`

공통 규칙:

- 스택 증가/초기화가 일어날 때마다 owner가 HUD 상태를 다시 투영
- 지속시간보다 `stackCount`가 진실한 핵심 값
- 피격 같은 reset 조건도 proc owner가 직접 처리

## 다음 상태를 추가할 때 체크리스트

1. 이 상태의 진짜 소유자는 누구인가
2. 상태를 직접 저장해야 하나, 아니면 owner가 복구 후 재등록하면 되는가
3. 표시 정의는 `StatusHudDefinition`으로 충분한가
4. owner가 실제 능력치/효과와 HUD 상태를 같은 수명으로 묶어 관리하는가
5. HUD와 tooltip은 상태를 소유하지 않고 projection만 하는가

## 한 줄 요약

현재 상태 구조의 핵심은 **`PlayerStatusRuntime`을 영속 저장소가 아니라 적용 허브로 두고, 상태의 진짜 소유자가 씬 전환 뒤에도 다시 `Apply(...)`를 호출하는 재등록 모델**에 있습니다. HUD는 그 상태를 읽어 보여줄 뿐이며, 시야 제한 같은 환경 상태도 같은 규칙으로 확장할 수 있습니다.
