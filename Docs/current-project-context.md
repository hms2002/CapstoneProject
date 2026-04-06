# Current Project Context

이 문서는 새 스레드에서 바로 작업을 이어갈 수 있도록, 현재 프로젝트의 **실제 작업 범위**, **최근 구조 변경**, **중요한 정책**을 짧게 정리한 문서다.

## 작업 범위

- 현재 기준으로 **`ProtoType*` 씬만 작업 대상**이다.
- 레거시 씬은 구조 판단이나 리팩토링 기준에서 제외한다.
- 플레이어/대화/UI/저장 정책 관련 검증도 `ProtoTypeHub`, `ProtoTypeCorridor*`, `ProtoTypeBoss*` 기준으로 본다.

## 전역 UI 구조

- 전역 UI는 `GlobalUIRoot` 기준으로 정리되어 있다.
- `GlobalUIRoot`는 프리팹으로 운용하는 방향이며, 각 플레이 가능 씬에 배치해서 테스트하기 쉽게 유지한다.
- 중복 전역 UI는 런타임/검증 툴에서 정리하는 구조다.

주요 캔버스 축:

- `GameplayHUDCanvas`
- `DialogueCanvas`
- `PopupCanvas`
- `HoverCanvas`
- `RewardCanvas`
- `DamagePopupCanvas`
- `BossHUDCanvas`

주요 관련 파일:

- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\LeeJunMo\Script\UIStructure\GlobalUIRoot.cs`
- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\LeeJunMo\Script\UIStructure\UIManager.cs`

## 대화 구조

### 핵심 원칙

- `DialogueView`는 전역 UI 뷰다.
- `DialogueController`는 오케스트레이터지만, 최근 일부 책임을 분리했다.
- 보스 대화 연출은 `DialogueEffect` + `BossEffectCnt.controller` 기반이다.

### 최근 분리된 컴포넌트

- `DialoguePresentationSequencer`
- `DialogueRuntimeReferenceResolver`

관련 파일:

- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\LeeJunMo\Script\Dialogue\DialogueController.cs`
- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\LeeJunMo\Script\Dialogue\DialogueRuntimeReferenceResolver.cs`
- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\LeeJunMo\Script\Dialogue\UI\DialoguePresentationSequencer.cs`
- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\LeeJunMo\Script\Dialogue\UI\DialogueView.cs`

### Dialogue Theme 정책

`NPCData.dialogueTheme` 기준으로 적용한다.

테마 요소:

- TextBox 외곽선 색
- SpeakerFrame 외곽선 색
- SpeakerFrame 내부색
- DialogueEffect animator override

주의:

- TextBox 내부 fill은 공통 검정이다.
- 화자가 바뀌면 `TextBox / SpeakerFrame / DisplayName` 색은 즉시 바뀐다.
- `DialogueEffect` 연출은 **메인 스피커 기준 1회 재생**이다.

관련 파일:

- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\LeeJunMo\Script\Dialogue\UI\DialogueThemeSO.cs`
- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\LeeJunMo\Script\Dialogue\NPC\NPCData.cs`

## 저장 정책

### 핵심 정책

- **허브 내부 메타 진행**: 즉시 저장
- **런 중 발생한 저장 가능 변화**: 런 종료 시 커밋

예시:

- 허브 업그레이드 구매: 즉시 저장
- 런 중 재화 변화: 런 종료 시 반영
- 런 중 호감도 변화: 런 종료 시 반영
- 런 중 shortcut 해금: 런 종료 시 반영

### 중앙 저장 경로

- 저장 요청은 `GameDataSaveCoordinator` 기준으로 중앙화 중이다.

관련 파일:

- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\LeeJunMo\Script\SaveData\GameDataSaveCoordinator.cs`
- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\LeeJunMo\Script\SaveData\GameDataManager.cs`
- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\LeeJunMo\Script\SaveData\GamePlayDataManager.cs`

### RunModifier 정책

- `RunModifier`는 저장 데이터의 원본이 아니라 **구매 이력 기반 재계산 파생 데이터**로 정리 중이다.
- `UpgradeProgressService`의 구매 결과를 기준으로 `RunModifierService`가 재구성하는 방향을 사용한다.

## 플레이어 구조

- `SampleTopDownPlayer`는 제거되고, 현재 기준 이름은 `PlayerInteractor2D`다.
- 상호작용 축은 별도 컴포넌트로 분리되어 있다.

주요 파일:

- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\HeoMinSeok\_Project\Scripts\Gameplay\Characters\Runtime\PlayerInteractor2D.cs`
- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\HeoMinSeok\_Project\Scripts\Gameplay\Interaction\PlayerInteractableTracker2D.cs`
- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\HeoMinSeok\_Project\Scripts\Gameplay\Interaction\PlayerInteractionTargetResolver2D.cs`
- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\HeoMinSeok\_Project\Scripts\Gameplay\Interaction\PlayerInteractionPromptPresenter.cs`
- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\HeoMinSeok\_Project\Scripts\Gameplay\Interaction\PlayerSpeechController.cs`

## 상자 구조

### 현재 동작

- 상자와 상호작용하면 첫 오픈 시 프렐류드 연출 후 UI를 연다.
- 프렐류드 동안 `Time.timeScale = 0`을 적용할 수 있다.
- UI를 닫아도 상자는 열린 상태를 유지한다.
- 이미 열린 상자는 재오픈 시 애니메이션 없이 바로 UI를 연다.

관련 파일:

- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\HeoMinSeok\_Project\Scripts\Gameplay\Inventory\Chest\Runtime\TreasureChest.cs`
- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\HeoMinSeok\_Project\Scripts\Gameplay\Inventory\Chest\Runtime\ChestInteractable.cs`
- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\HeoMinSeok\_Project\Scripts\UI\Inventory\Chest\ChestUIManager.cs`

### 상자 Animator 권장 상태

- `Idle`
- `Open`
- `Opened`

권장 전이:

- `Entry -> Idle`
- `Open -> Opened`

`Idle -> Open`은 코드에서 직접 `Play("Open")`로 들어가는 전제다.

### 상자 Loot

- 상자는 현재 `Weapon / Relic / Consumable` 루트를 생성할 수 있다.
- Consumable 슬롯과 플레이어 쪽 consumable 인벤토리/입력도 연결되어 있다.

관련 파일:

- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\LeeJunMo\Script\Looting\StageLootTable.cs`
- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\LeeJunMo\Script\Looting\LootManager.cs`

## 씬 검증 툴

- `SceneSetupValidatorWindow`가 존재한다.
- `Validate / Auto Fix / Cleanup` 흐름으로 전역 UI와 씬 설정 무결성을 보조한다.

관련 파일:

- `C:\Users\nadom\Desktop\졸업작품\CapstoneProject\Assets\LeeJunMo\Script\Editor\SceneSetupValidatorWindow.cs`

## 스프라이트 폴더 정리 상태

`Assets/Sprites`는 1차 정리가 끝났고, 현재 큰 구조는 아래 기준이다.

- `Characters`
- `Effects`
- `Environment`
- `Items`
- `ThirdParty`
- `UI`

대화용 UI 에셋은 `Assets/Sprites/UI/Dialogue`에 모였고, 공용 UI는 `Assets/Sprites/UI/Common`에 모였다.

## 새 스레드에서 먼저 확인할 것

1. 현재 작업이 `ProtoType*` 씬 기준인지
2. `GlobalUIRoot` 전역 UI 전제인지
3. 저장 정책이 허브 즉시 저장 / 런 종료 커밋 전제인지
4. 플레이어 타입은 `PlayerInteractor2D` 기준인지
5. 대화 테마는 `NPCData.dialogueTheme` 기준인지

## 현재 보류 중인 항목

- 몬스터 consumable 드롭 기획/구현
- `Sprites` 2차 정리 (`Anim`, `UI/HeartUIAsset`, `UI/Relics` 등)
- 저장 정책 플레이 테스트 전면 검증
