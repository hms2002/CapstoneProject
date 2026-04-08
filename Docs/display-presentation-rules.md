# Display Presentation Rules

## Goal

화면 모드, 해상도, 창 최대화/복원, 모니터 비율과 관계없이 아래 규칙을 항상 유지한다.

- 실제 게임 플레이 영역은 항상 `16:9`
- 선택한 해상도와 화면 모드는 `게임 영역 비율`이 아니라 `출력 컨테이너 크기`를 정한다
- 레터박스는 항상 `16:9 플레이 영역`을 유지하기 위한 남는 공간만 채운다
- UI는 항상 `최종 16:9 플레이 영역` 안에만 표시된다

즉, 게임은 항상 `16:9`이고, 바깥 상자만 달라진다.

## Terms

- `play area`
  실제 게임과 UI가 보여야 하는 논리 화면. 항상 `16:9`.
- `container`
  실제 출력이 들어가는 바깥 영역.
- `viewport`
  `container` 안에서 `play area`가 차지하는 정규화 사각형.
- `letterbox`
  `container - viewport`의 남는 영역을 검정 바로 채운 것.

## Source Of Truth

### 1. Play Area Aspect

- 게임의 기준 비율은 항상 `16:9`
- 어떤 화면 모드에서도 `targetAspect = 16f / 9f`를 사용한다
- 선택한 해상도 `3440x1440`, `1280x800`, `1024x768` 같은 값은 비율 기준이 아니다

### 2. Container By Window Mode

- `Windowed`
  창의 클라이언트 영역 크기 = 선택한 해상도
- `Borderless`
  현재 표시 중인 모니터의 실제 클라이언트 영역 크기
- `Fullscreen`
  실제 전체화면 출력 해상도 크기

즉 해상도/모드는 `container`를 정하고, `play area`는 별도로 `16:9`를 유지한다.

## Viewport Rules

항상 같은 규칙으로 계산한다.

- `containerAspect == 16:9`
  레터박스 없음
- `containerAspect > 16:9`
  좌우 pillarbox
- `containerAspect < 16:9`
  상하 letterbox

예시:

- `3440x1440` 컨테이너
  `21:9`에 가까움
  `16:9`보다 가로로 넓음
  `16:9 play area`를 꽉 차게 넣으면 좌우가 남으므로 `left/right pillarbox`
- `1280x800` 컨테이너
  `16:10`
  `16:9`보다 세로가 더 길므로 `top/bottom letterbox`
- `1920x1080` 컨테이너
  정확히 `16:9`
  레터박스 없음

계산 공식은 다음 하나만 쓴다.

```text
currentAspect = containerWidth / containerHeight
targetAspect = 16 / 9

if currentAspect > targetAspect:
    viewportWidth = targetAspect / currentAspect
    viewportHeight = 1
    insetX = (1 - viewportWidth) / 2
    => left/right pillarbox
else if currentAspect < targetAspect:
    viewportWidth = 1
    viewportHeight = currentAspect / targetAspect
    insetY = (1 - viewportHeight) / 2
    => top/bottom letterbox
else:
    full viewport
```

이 공식을 모든 모드에 동일하게 적용한다.

## UI Rules

### 1. UI Anchor Space

- HUD, Dialogue, Popup, Hover, Prompt, Reward, DamagePopup, BossHUD는 항상 `play area viewport` 기준으로만 그려야 한다
- 창 전체를 기준으로 그리면 안 된다
- 레터박스 영역 위에 UI가 침범하면 안 된다

### 2. Canvas Scaling

- UI 기준 해상도는 고정 `1280x720`
- UI scale preset은 이 기준 해상도에 대한 배율만 조정한다
- UI scale 로직은 현재 창 크기나 현재 컨테이너 비율을 기준으로 새 규칙을 만들면 안 된다

### 3. Render Mode Stability

- 화면 모드나 창 크기 변경 시, 가능하면 캔버스 `renderMode`를 반복 전환하지 않는다
- 전환이 꼭 필요하면 전환 조건과 복귀 조건이 완전히 대칭이어야 한다
- `Camera.main` 교체 타이밍과 캔버스 적용 타이밍이 어긋나면 최대화/복원 반복 시 UI가 깨진다

## Update Triggers

아래 상황에서는 presentation 재적용이 필요하다.

- 설정 UI에서 화면 모드 변경
- 설정 UI에서 해상도 변경
- 씬 로드 완료
- 창 최대화 / 복원
- OS 레벨 창 크기 변경
- 전체화면 / 테두리 없음 전환
- 모니터 변경

단순히 `Screen.width/height`만 바뀌었는지만 보면 부족하다.

재적용 트리거는 최소한 아래를 포함해야 한다.

- actual container width
- actual container height
- current window mode
- selected display resolution
- active presentation camera

## Current Code Mismatches

### 1. Mode-specific aspect switching

[GamePresentationController.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Settings/GamePresentationController.cs)

- `GetPresentationAspectRatio(...)`가 `Windowed`와 나머지 모드에서 서로 다른 기준을 쓴다
- 이 구조는 지금 사용자 기대와 충돌한다
- 기준은 항상 `16:9` 하나여야 한다

### 2. Container source inconsistency

[GamePresentationController.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Settings/GamePresentationController.cs)

- `Windowed`는 `Screen.width/height`
- `Borderless/Fullscreen`는 `Display.main.systemWidth/systemHeight`

이 방식은 최대화/복원, 해상도 변경, 모니터 변경 시 실제 렌더 컨테이너와 읽는 값이 어긋날 수 있다.

### 3. Canvas presentation instability

[GamePresentationController.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Settings/GamePresentationController.cs)

- `ApplyUiCanvasPresentation(...)`에서 viewport full 여부에 따라 `ScreenSpaceOverlay <-> ScreenSpaceCamera`처럼 동작 기준이 흔들릴 수 있다
- 반복 전환 시 UI 비율과 위치가 꼬일 가능성이 높다

### 4. Refresh condition is too weak

[GamePresentationController.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Settings/GamePresentationController.cs)

- `RefreshIfNeeded(...)`가 `Screen.width/height`만 캐시한다
- 화면 모드, 선택 해상도, 실제 display source 변경, canvas mode 재적용 필요성은 반영하지 못한다

### 5. UI scaling does not know viewport

[GameUiScaleController.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Settings/GameUiScaleController.cs)

- `CanvasScaler.referenceResolution`만 바꾸고 있다
- 최종 viewport 기반 보정인지, 창 전체 기준인지가 명확하지 않다

## Implementation Order

### Step 1. Freeze The Rule

- `targetAspect = 16:9` 고정
- 모든 모드에서 같은 viewport 계산식을 쓴다
- 선택 해상도는 `container size`로만 사용한다

### Step 2. Split Container Resolution From Play Area

- `GameSettingsService`는 창/출력 컨테이너 크기만 적용
- `GamePresentationController`는 `container -> viewport`만 책임진다

### Step 3. Stabilize UI Presentation

- UI 캔버스는 최종 viewport만 따라가게 고정
- 가능하면 캔버스 render mode 전환을 줄인다
- viewport 적용 시점과 UI scale 적용 시점을 정한다

### Step 4. Strengthen Refresh Conditions

- `width/height`만이 아니라 모드, 선택 해상도, display source 변경도 감지
- 최대화/복원 반복에서도 같은 계산 결과가 나오게 한다

### Step 5. Add Regression Checks

- `Windowed + 1920x1080`
  레터박스 없음
- `Windowed + 3440x1440`
  `16:9` play area 유지, letterbox 방향 확인
- `Windowed + 1280x800`
  방향 확인
- `Borderless + 21:9 monitor`
  `16:9` play area 유지
- `Fullscreen + 4:3`
  `16:9` play area 유지
- 최대화/복원 10회 반복 후 UI 비율 유지

## Practical Decision For Next Patch

다음 구현 패치는 아래만 목표로 한다.

- `GamePresentationController`의 aspect 기준을 `16:9` 하나로 통일
- container 해석을 모드별로만 분리
- UI 캔버스를 viewport 기준으로 고정
- 최대화/복원 반복에도 같은 결과가 나오게 refresh key 강화

이 문서 기준으로 구현을 진행하면 “창모드는 되고, Borderless/Fullscreen이나 최대화 반복에서만 깨지는” 종류의 임시 수정을 줄일 수 있다.
