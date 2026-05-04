# Codebase Review Guidelines

이 문서는 코드베이스 순회 점검 중 발견한 공통 구조 규칙, 리팩토링 기준, 새 기능 작성 시 주의사항을 누적하는 작업 노트입니다.

## Core Principles

- 상태의 주인은 하나만 둔다.
  - 특정 런타임 상태를 여러 싱글톤, 프리팹 bootstrapper, UI fallback이 동시에 추론하면 재현성 문제가 생긴다.
  - Player 관련 현재 인스턴스는 `PlayerRuntimeRegistry` 같은 명시적 registry를 통해 확인하고, UI나 저장 코드에서 scene-wide `Find*` fallback을 마지막 수단으로만 사용한다.

- 전환 계획과 복원 대상은 원자적으로 만든다.
  - 씬 전환은 `fromScene`, `toScene`, transition type, run directive, player runtime snapshot을 하나의 travel plan으로 묶는 편이 안전하다.
  - pending state를 소비하는 코드는 반드시 destination scene 또는 명시적 restore owner를 확인해야 한다.

- Shell restore와 runtime hook attach를 분리한다.
  - 인벤토리 배치, 슬롯, DTO 복원은 effect-free shell restore로 처리한다.
  - GAS grant, relic token, visual, stat hook 같은 런타임 효과는 shell restore 이후 명시적인 attach 단계에서 처리한다.

- Presentation, audio, cue는 코어 gameplay state와 분리한다.
  - Ability/Effect는 판정과 상태 변경의 owner여야 한다.
  - `Presentation`은 패턴별 연출, `Cue`는 재사용 가능한 완성 프리셋, runner/helper는 임시 handle과 cleanup만 맡는다.
  - 같은 `GameplayCueParams`/`SoundPlaybackContext` 생성 로직을 여러 router에 반복하지 않는다.

- 저장은 중간 파일 깨짐에 대비한다.
  - `File.WriteAllText` 직접 저장은 크래시/중단 시 save 파일 손상 가능성이 있다.
  - temp 파일 작성 후 replace/move 하는 atomic save 계층을 공통화한다.

- UI는 GameObject 수명과 tween 수명을 같이 관리한다.
  - `DOAnchorPos*`, `DOFade`, sequence는 `OnDisable`/`OnDestroy`에서 `DOKill` 또는 stored tween `Kill`이 있어야 한다.
  - destroy될 수 있는 UI 객체에 무한 루프 tween을 걸면 씬 전환 테스트에서 DOTween null target 경고가 재현된다.

## Area Rules

### Scene Transition / Player Lifecycle

- `PlayerSceneRestoreBootstrapper` 같은 Player prefab-local component가 global pending state를 직접 polling하는 구조는 위험하다.
- restore 호출 owner는 destination scene bootstrapper 또는 `PlayerSpawner` 쪽으로 모으는 것이 안전하다.
- `PlayerRuntimeRegistry`는 등록/해제/중복 감지만 맡고, component 생성은 prefab 구성 또는 명시적 bootstrap 단계에서 처리한다.
- `ScenePortalTravelService`는 player snapshot 저장, run start/end, transition context 준비를 하나의 `SceneTravelPlan` 수준으로 묶는 방향이 좋다.

### SaveData / Profile

- `GameDataManager.SaveData`가 `ItemManager`, `UpgradeManager`에서 직접 데이터를 당겨오면 저장 책임과 도메인 manager 책임이 섞인다.
- save contributor 또는 snapshot provider 인터페이스를 두고 저장 시점에는 provider들을 모아 DTO를 조립하는 편이 테스트하기 쉽다.
- `GameDataSaveCoordinator.RequestSave*`가 instance 부재 시 조용히 no-op 하지 않게 한다. 최소한 `EnsureInstance` 또는 static pending flag가 필요하다.
- `GamePlayDataManager`는 run session, pending transition/player state, pending reward delta, persistent commit 책임을 나눌 후보가 크다.

### Inventory / Item / Equipment

- item definition resolve는 `ItemManager.Instance.Get*Data` 직접 호출보다 `IItemDefinitionResolver` 같은 얇은 interface를 공유하는 편이 낫다.
- weapon/relic/consumable slot 비교 기준은 test, diagnostics, restore confirmation이 같은 comparer를 사용해야 한다.
- UI가 현재 player를 찾기 위해 `FindFirstObjectByType<PlayerConsumableInventory>`로 fallback하면 inactive/old player를 잡을 수 있다.
- `GetOrAdd`는 registry/UI에서 호출하지 않는다. 필요한 component는 prefab authoring 또는 player bootstrap에서 보장한다.

### GAS / Ability / Effect / Tag

- `AbilitySystem`은 현재 activation, cooldown, parallel execution, persistence, presentation cleanup을 모두 가진다. 장기적으로 controller 단위로 분리한다.
- `AbilitySystem`, `GameplayEffectRunner`, `GameplayPresentationRuntime`의 cue manager resolve는 scene-wide `Find*`보다 scene domain/service provider 주입이 안전하다.
- persistent state key는 reserved key와 user key를 명확히 유지하고, 새 ability state를 추가할 때 export/import 대상인지 먼저 결정한다.
- presentation context 생성(`WorldPresentationContext`, `SoundPlaybackContext`)은 Ability, Effect, Cue 경로에서 중복하지 않도록 공통 factory로 모은다.

### UI / EventSystem / DOTween

- runtime fallback service가 `EventSystem`을 생성할 수 있다면, 씬 전환 이후 중복 EventSystem 정리 정책도 같은 계층에서 가져야 한다.
- global UI service는 DDOL root로 이동할 때 root GameObject만 `DontDestroyOnLoad` 대상이 되도록 보장한다.
- 대화/초상화/인벤토리 UI tween은 객체 pool 재사용과 씬 unload를 모두 고려해서 `DOKill` 경로를 가진다.
- UI manager가 player inventory를 bind할 때는 `PlayerRuntimeRegistry.CurrentPlayer` 기준으로 실패를 명확히 보고하고, 자동 scene search fallback은 제한한다.
- `DialogueView`처럼 `Awake`에서 무한 loop tween을 시작하는 UI는 tween handle을 보관하고 `OnDisable` 또는 `OnDestroy`에서 반드시 kill한다.
- `SceneFadeTransitionService` 같은 fallback creator가 `EventSystem`을 만들 경우, persistent EventSystem owner와 scene-local EventSystem coexistence 정책을 명시한다.

### Enemy / Boss / AI

- 기존 문서 기준으로는 AL owns pattern execution presentation, FSM state owns state-rhythm presentation, runner owns temporary handles and cleanup 규칙을 유지한다.
- `IMobPresentationCleanup`, `MobCleanupContract`, `PresentationAuthoringContract`와 실제 mob/boss 구현의 일치 여부를 확인한다.
- 일반 mob cleanup은 `MobAIContext.PerformFailSafeCleanup`, `CancelPatternRunners`, `CleanupPresentation` 흐름을 유지한다. 새 mob도 이 계약을 우회하지 않는다.
- enemy target resolve는 `GameObject.FindWithTag("Player")`보다 `PlayerRuntimeRegistry` 또는 injected target provider 기준으로 통일한다.
- `MonsterSpawner`는 DDOL singleton과 scene-local spawn director 책임이 섞여 있다. 장기적으로 current difficulty/state 보관과 active scene spawn orchestration을 분리한다.
- boss encounter director, talk manager, camera presentation director resolve는 `FindAnyObjectByType` fallback을 줄이고 encounter composition root에서 주입한다.
- boss/mob prefab 구성 누락을 `AddComponent`로 조용히 보정하면 authoring 오류가 늦게 드러난다. 필수 executor/coordinator는 prefab authoring validator로 잡고, 런타임 추가는 선택 기능에만 둔다.

## Review Checklist

- [x] Scene transition / Player lifecycle
- [x] SaveData / Profile
- [x] Inventory / Item / Equipment
- [x] GAS / Ability / Effect / Tag
- [x] UI / EventSystem / DOTween
- [x] Enemy / Boss / AI
- [x] Refactoring priority roadmap

## Completed Fixes

- 2026-04-30: `DialogueView`의 continue icon 무한 tween을 handle로 보관하고 `OnDisable`/`OnDestroy`에서 kill하도록 정리했다. 씬 전환 중 destroyed `RectTransform`을 DOTween이 계속 참조하는 경고를 막기 위한 조치다.
- 2026-04-30: `PlayerRuntimeRegistry.Register`에서 player runtime component 생성 책임을 제거하고 검증만 수행하도록 변경했다. `PF Player` 프리팹에는 `PlayerConsumableInventory`와 `PlayerConsumableInput2D`를 명시적으로 추가했다.
- 2026-04-30: `GameDataRepository` 저장을 temp file 작성 후 replace/move하는 atomic write 경로로 변경했다. persistent save와 editor inspectable copy 양쪽에 같은 경로를 사용한다.
- 2026-04-30: `InventoryUIManager.Open`에서 `FindFirstObjectByType`/`PlayerInteractor2D.Instance` fallback을 제거하고 `PlayerRuntimeRegistry.CurrentPlayer` 기준으로만 inventory를 bind하도록 변경했다.
- 2026-04-30: `MonsterSpawner`의 DDOL singleton/난이도 보관 책임과 active scene spawn 실행 책임을 분리했다. 기존 컴포넌트 설정은 유지하고 `SceneMonsterSpawnDirector` helper가 spawn point 수집, scene service resolve, 생성/정리, spawn context 주입을 담당한다.

## High-Value Refactoring Candidates

- `SceneTravelPlan`: transition context와 player runtime snapshot을 함께 준비하는 DTO/service.
- `PlayerRuntimeStateComparer`: production restore confirmation, diagnostics, PlayMode tests가 공유하는 비교 기준.
- `IItemDefinitionResolver`: weapon/relic/consumable resolve를 `ItemManager.Instance` 직접 의존에서 분리.
- `SaveContributor` or `SaveSnapshotProvider`: save data 조립을 manager별 provider로 분리.
- `AtomicGameDataRepository`: temp write, replace/move, backup policy를 가진 저장 계층.
- `GameplayPresentationContextFactory`: Ability/Effect/Cue audio and world presentation context 생성 공통화.
- `TweenLifecycleGuard`: UI component가 등록한 tween/sequence를 disable/destroy에서 일괄 kill하는 작은 helper.
- `RuntimeEventSystemOwner`: persistent EventSystem 생성, scene-local EventSystem prune, input module policy를 한 곳에서 관리.
- `EnemyTargetProvider`: enemy, boss, mob FSM이 현재 player target을 동일한 기준으로 받는 adapter.
- `SceneMonsterSpawnDirector`: active scene spawn point collection, installer/pathfinder resolve, SpawnAll timing을 scene-local 책임으로 분리.

## Refactoring Priority Roadmap

### Immediate

- `DialogueView` 무한 loop tween cleanup을 먼저 보강한다. 현재 PlayMode 로그의 DOTween null target 경고와 직접 연결된다.
- `PlayerSceneRestoreBootstrapper`의 active scene guard는 유지하되, 다음 구조 변경에서는 restore 호출 owner를 Player prefab에서 destination scene bootstrapper 또는 `PlayerSpawner`로 옮긴다.
- `PlayerRuntimeRegistry.Register`에서 component 생성 책임을 제거하기 전, Player prefab에 필요한 inventory/input/animator component가 모두 authoring되어 있는지 검증한다.

### Short Term

- `SceneTravelPlan`을 도입해 player snapshot, run directive, transition context 준비를 한 단위로 만든다.
- `PlayerRuntimeStateComparer`를 만들어 bootstrapper, diagnostics test, future restore verification이 같은 비교 기준을 쓰게 한다.
- `GameDataRepository` 저장 방식을 temp write + replace/move 기반 atomic save로 바꾼다.
- UI inventory binding에서 scene-wide `Find*` fallback을 줄이고 `PlayerRuntimeRegistry.CurrentPlayer` 실패를 명시적으로 다룬다.

### Mid Term

- `ItemManager`에서 item catalog resolve와 unlock/progression state를 분리하거나 `IItemDefinitionResolver`를 먼저 도입한다.
- `AbilitySystem`의 activation/cooldown/persistence/presentation cleanup 책임을 controller 단위로 나눌 준비를 한다.
- cue manager resolve와 presentation context 생성을 scene service/provider와 factory로 모아 중복을 줄인다.
- `MonsterSpawner`를 persistent spawn state와 scene-local spawn director로 분리한다.

### Long Term

- boss encounter, camera/talk manager, enemy target resolve를 composition root 주입 기준으로 정리한다.
- prefab 구성 누락을 런타임 `AddComponent`로 보정하는 패턴을 줄이고 authoring validator 또는 `RequireComponent` 기준으로 옮긴다.
- save contributor/snapshot provider 구조를 도입해 저장 계층이 개별 manager 내부 구조에 덜 의존하게 한다.
