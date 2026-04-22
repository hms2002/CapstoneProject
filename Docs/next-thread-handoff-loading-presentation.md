# Next Thread Handoff: Loading / Presentation / Prewarm

## 목적
이 문서는 다음 스레드가 `Presentation`, `Loading`, `Prewarm` 작업을 바로 이어갈 수 있도록 현재 구조, 결정사항, 최근 수정, 남은 작업을 정리한 handoff다.

## 현재 기준 원칙

### 1. Presentation과 Cue의 역할
- `Presentation`은 현장 작업용이다.
- 특히 `Witch` 보스 패턴은 전부 `AL 내부 Presentation` 기준으로 간다.
- `Cue`는 재사용 가능한 완성형 프리셋이다.
- `Cue`는 카탈로그형 공용 연출로 보고, `Presentation`은 패턴 로컬 연출로 본다.
- 실행기는 공통으로 `WorldPresentationRuntime`을 쓴다.
- 비주얼 인스턴스 관리는 `PresentationSpawnService`가 맡는다.

### 2. Loading 데이터의 source of truth
- 로딩 데이터 원본은 **무조건** [Assets/LeeJunMo/Datas/Loading](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Datas/Loading) 이다.
- `Resources/Loading`은 더 이상 로딩 데이터 원본으로 사용하지 않는다.
- `LoadingBootstrapConfig`는 원본도 [Assets/LeeJunMo/Datas/Loading/LoadingBootstrapConfig.asset](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Datas/Loading/LoadingBootstrapConfig.asset) 이다.
- 런타임 bootstrap 진입은 `Resources.Load`가 아니라 `preloadedAssets`를 사용한다.

### 3. Loading scope
- `Boot`
- `RunCommon`
- `RouteSet`
- RouteSet은 내부적으로 `Shared / Corridor / Boss`로 나뉜다.
- preload 기준은 `Boot + RunCommon + Current RouteSet + Next RouteSet`이다.

## 현재 핵심 파일

### Presentation
- [WorldPresentationHook.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Presentation/Runtime/WorldPresentationHook.cs)
- [WorldPresentationRuntime.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Presentation/Runtime/WorldPresentationRuntime.cs)
- [PresentationSpawnService.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Presentation/Runtime/PresentationSpawnService.cs)
- [CueCatalogSO.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Presentation/Runtime/CueCatalogSO.cs)
- [CueCatalogService.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Presentation/Runtime/CueCatalogService.cs)
- [PresentationCueSO.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Presentation/Runtime/PresentationCueSO.cs)

### Loading
- [LoadManifestSO.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Loading/Runtime/LoadManifestSO.cs)
- [RouteSetLoadManifestSO.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Loading/Runtime/RouteSetLoadManifestSO.cs)
- [LoadingBootstrapConfigSO.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Loading/Runtime/LoadingBootstrapConfigSO.cs)
- [PresentationAssetProvider.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Loading/Runtime/PresentationAssetProvider.cs)
- [PresentationPreloadService.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Loading/Runtime/PresentationPreloadService.cs)
- [LoadingDebugView.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Loading/Runtime/LoadingDebugView.cs)

### Editor tools
- [RouteSetLoadManifestBuilderWindow.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Editor/RouteSetLoadManifestBuilderWindow.cs)
- [LoadManifestInspectorWindow.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Editor/LoadManifestInspectorWindow.cs)
- [PrewarmRecommendationWindow.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/Editor/PrewarmRecommendationWindow.cs)

### Runtime trace
- [PrewarmTraceRuntime.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Loading/Runtime/PrewarmTraceRuntime.cs)
- trace output: [PrewarmTrace.json](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Datas/Loading/PrewarmTrace.json)

## 현재 실제 사용 중인 로딩 데이터
- [BootLoadManifest.asset](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Datas/Loading/BootLoadManifest.asset)
- [LoadingBootstrapConfig.asset](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Datas/Loading/LoadingBootstrapConfig.asset)
- [ShadowCorridorBossRouteSet_LoadManifest.asset](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Datas/Loading/ShadowCorridorBossRouteSet_LoadManifest.asset)

## 최근 중요한 수정 사항

### 1. Extinguish executor 복원
머지 과정에서 `WitchExtinguishPatternExecutor`가 raw prefab/sound/shake 실행으로 되돌아갔던 것을 복원했다.

현재 상태:
- executor 분리는 유지
- `AL -> presentation contract -> runtime` 흐름 복원
- 안개는 `PresentationSpawnService.SpawnPersistent(...)`
- 폭발은 `WorldPresentationRuntime.Play(...)`

관련 파일:
- [WitchExtinguishPatternExecutor.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/Script/Enemy/Boss/FSM/BossControllers/WitchBoss/WitchExtinguishPatternExecutor.cs)
- [Witch.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/Script/Enemy/Boss/FSM/BossControllers/WitchBoss/Witch.cs)
- [AbilityLogic_WitchExtinguishCandle.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/Script/Enemy/Boss/FSM/BossControllers/WitchBoss/Abilities/AbilityLogic_WitchExtinguishCandle.cs)

### 2. Boot `<none>` 문제
원인:
- bootstrap config 원본은 `Datas/Loading`에 있는데
- 런타임이 `Resources.Load`를 보던 시점이 있어서 `Boot`가 `<none>`으로 나왔다.

현재 상태:
- bootstrap config는 [LoadingBootstrapConfig.asset](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Datas/Loading/LoadingBootstrapConfig.asset) 하나가 원본이다.
- [PresentationPreloadService.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Loading/Runtime/PresentationPreloadService.cs)는 preloaded asset을 우선 잡고, 에디터에서만 source path fallback을 본다.
- `Resources/Loading` mirror 개념은 제거했다.

### 3. PrewarmRecommendationWindow 경로 문제
원인:
- [`Assets/LeeJunMo/Script/Editor`](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Editor) 아래 두었을 때 Unity가 해당 파일을 에디터 어셈블리에 올리지 않는 상황이 있었다.
- `Assembly-CSharp-Editor.csproj`에도 해당 파일이 안 잡혔다.

현재 상태:
- 파일을 [`Assets/Editor/PrewarmRecommendationWindow.cs`](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/Editor/PrewarmRecommendationWindow.cs) 로 옮겼다.
- 메뉴 경로:
  - `Tools > Loading > Prewarm Recommendations`
  - `Tools > Loading > Open Prewarm Recommendations`
- [LoadManifestInspectorWindow.cs](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Editor/LoadManifestInspectorWindow.cs) 에서 `Prewarm Recs` 버튼으로도 열 수 있다.

## 현재 툴 사용법

### 1. Boot manifest 생성
`Tools > Loading > RouteSet Manifest Builder`

순서:
- `Boot Seed Scene`에 `ProtoTypeHub` 기준 씬 지정
- `Build Boot Manifest From Seed Scene`

결과:
- [BootLoadManifest.asset](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Datas/Loading/BootLoadManifest.asset)
- [LoadingBootstrapConfig.asset](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Datas/Loading/LoadingBootstrapConfig.asset)

### 2. RouteSet manifest 생성
같은 창에서:
- `Build Selected RouteSet`
- 또는 `Build All RouteSets`

결과:
- 해당 RouteSet용 [RouteSetLoadManifestSO](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Script/Loading/Runtime/RouteSetLoadManifestSO.cs) 생성/갱신

### 3. Manifest 검사
`Tools > Loading > Load Manifest Inspector`

기능:
- `Boot / RunCommon / Shared / Corridor / Boss` scope 표시
- category별 분류
  - `Prefab`
  - `Audio`
  - `Cue`
  - `Data`
  - `Material`
  - `Font`
  - `Other`
- `Prefab` 항목에 대해 prewarm count 수동 편집

### 4. Prewarm 추천
전제:
- 플레이 세션을 한 번 이상 돌려서 [PrewarmTrace.json](/C:/Users/nadom/Desktop/졸업작품/CapstoneProject/Assets/LeeJunMo/Datas/Loading/PrewarmTrace.json)이 쌓여 있어야 한다.

창:
- `Tools > Loading > Prewarm Recommendations`
- 또는 inspector의 `Prewarm Recs`

기능:
- trace 분석
- `Boot / RunCommon / Shared / Corridor / Boss` manifest에 prefab 매핑
- `P1 / P2 / P3` 우선순위 추천
- `Apply P1`, `Apply All`, 개별 `Apply`

## 다음 스레드가 주의해야 할 점

### 1. mirror를 다시 만들지 말 것
- loading data는 `Datas/Loading` 단일 원본 유지
- `Resources/Loading` mirror 다시 도입하지 말 것

### 2. `Witch` 패턴 연출은 Cue로 억지로 빼지 말 것
- `Witch` 보스 패턴은 `AL 내부 Presentation` 기준으로 유지
- 공용 재사용 프리셋만 `Cue`로 다룰 것

### 3. `executor` 구조를 유지할 것
- `WitchExtinguishPatternExecutor`는 executor 구조를 유지한 채 presentation runtime을 타야 한다
- raw `Instantiate + SoundPlaybackUtility + CameraShakeHook.TryPlay`로 되돌리지 말 것

### 4. prewarm은 수동/반자동 기준 유지
- prewarm은 자동 전부 적용이 아니라 추천 후 승인 방식이 맞다
- manifest의 `prewarmPrefabs`는 opt-in 유지

## 다음 작업 후보

### 우선순위 1
- `LoadingDebugView`에 history/log 추가
- 보고 싶은 것:
  - preload/unload history
  - last transition event
  - loaded scene names
  - provider ref history

### 우선순위 2
- manifest builder 필터 정교화
- 현재 dependency 수집이 여전히 넓어서 과포함 가능성이 있다
- 폴더/타입 ignore 규칙이 더 필요할 수 있다

### 우선순위 3
- prewarm 추천 품질 개선
- 현재는 cold spawn, total spawn, first seen time 기반 점수
- 필요하면 scene/scope 기반 weighting 추가 가능

### 우선순위 4
- async provider 연구
- 현재는 direct reference + ref-count 구조
- 다음 단계에서 `IAssetProvider` 류 경계로 비동기 resolve나 Addressables 도입 검토 가능

## 빠른 체크리스트
- Hub 플레이 시 `F8` 디버그 뷰에서 `Boot`가 비지 않는가
- RouteSet manifest가 `Boot` 자산을 `Shared`로 다시 들고 오지 않는가
- `Prewarm Recommendations` 창이 메뉴나 inspector에서 열리는가
- `Witch Extinguish`가 다시 presentation runtime 경로를 타는가
> Legacy / Notes  
> 이 문서는 다음 작업 스레드로 넘기기 위한 핸드오프 메모입니다.  
> 현재 표준 문서가 아니며, 당시 작업 인수인계 기록으로만 봅니다.
