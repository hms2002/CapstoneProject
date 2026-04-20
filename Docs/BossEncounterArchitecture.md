# Boss Encounter Architecture

이 문서는 보스 FSM, 패턴 실행, 등장/처치 연출의 큰 구조를 설명합니다.

## 어디서 시작하나

- 보스 전투/FSM 중심:
  - [BossControllerBase.cs](../Assets/Script/Enemy/Boss/FSM/Core/BossControllerBase.cs)

- 보스 등장 연출:
  - [BossEncounterDirector.cs](../Assets/LeeJunMo/Script/Dialogue/BossEncounterDirector.cs)

- 보스 처치 연출:
  - `BossDeathPresentation`

- 연출 중 플레이어 보호:
  - [PlayerCinematicProtection.cs](../Assets/HeoMinSeok/_Project/Scripts/Gameplay/Characters/Runtime/PlayerCinematicProtection.cs)

## 핵심 책임 분리

- `BossControllerBase`
  - 보스 FSM, 페이즈 변화, 패턴 시작/종료, 반응 상태 전이를 조율합니다.

- 개별 보스 컨트롤러
  - 예: `Witch`
  - 보스 고유 패턴 호스트, runtime 구성, 월드 행위를 담당합니다.

- pattern executor
  - 복잡한 패턴의 실제 긴 실행을 담당합니다.

- `BossEncounterDirector`
  - 보스 등장 시퀀스, 대화, 카메라, 전투 시작 타이밍을 조율합니다.

- `BossDeathPresentation`
  - 보스 처치 연출, 카메라, 오버레이, 종료 타이밍을 조율합니다.

- `PlayerCinematicProtection`
  - 연출 중 플레이어 입력 잠금과 무적 상태를 공용 규칙으로 보장합니다.

## 현재 구조에서 기억할 점

- 패턴 데이터는 가능한 한 AL/AD 자산이 들고,
- 보스 본체는 월드 행위 host 쪽으로 남기는 방향이 좋습니다.

- 사망/그로기/연출 진입 시 남는 패턴 오브젝트 정리는 보스 쪽 cleanup 규칙으로 봐야 합니다.

## 어떤 수정이 어디에 가까운가

### 보스가 어떤 상태로 전환되는지 보고 싶다
- `BossControllerBase`
- 각 `BossState`

### 패턴이 왜 시작/취소/종료되는지 보고 싶다
- `BossControllerBase`
- 각 보스의 state bridge
- pattern executor

### 등장 연출/대화/전투 시작 타이밍을 바꾸고 싶다
- `BossEncounterDirector`

### 처치 연출 중 플레이어 보호를 바꾸고 싶다
- `BossDeathPresentation`
- `PlayerCinematicProtection`

## 주의할 점

- 보스 패턴 정리와 연출 보호 상태는 같이 봐야 합니다.
- 보스별 특수 패턴 로직을 `BossControllerBase`에 다시 밀어 넣지 않게 조심해야 합니다.
