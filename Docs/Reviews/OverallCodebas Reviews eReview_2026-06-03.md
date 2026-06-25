## 전체 코드베이스 정적 리뷰 결과

요청대로 **고치지 않고**, `Assets/` 아래의 C# 스크립트들을 중심으로 전체 코드베이스를 정적 탐색했습니다.
이번 답변은 “커밋 diff 리뷰”가 아니라, **현재 브랜치에 존재하는 Unity 런타임/에디터/테스트 코드 전반의 구조·위험·유지보수성 리뷰**입니다.

> 변경 사항은 없습니다.
> 따라서 커밋/PR은 만들지 않았습니다.

---

## 0. 리뷰 범위와 전제

### 읽은 범위

* `Assets/` 아래 C# 파일 수는 **1320개**였습니다.
* 전체 C# 라인 수는 약 **301,011줄**로 확인했습니다.
* 깊게 본 1차 영역은 다음입니다.
  * `Assets/LeeJunMo/Script/`
  * `Assets/HeoMinSeok/_Project/Scripts/`
  * `Assets/Script/Enemy/`
  * `Assets/Tests/`
  * `ProjectSettings/EditorBuildSettings.asset`
* `Ink`, 일부 플러그인/서드파티성 코드는 전체 스캔에는 포함했지만, 프로젝트 고유 구조 리뷰에서는 우선순위를 낮췄습니다.

### 리뷰 방식

* Unity 실행/컴파일 없이 **정적 코드 리뷰**로 진행했습니다.
* 따라서 실제 런타임 재현 여부는 “가능성이 높은 위험”과 “확정 버그 후보”를 구분해서 적었습니다.
* 현재 프로젝트의 에러 로그에는 Unity destroyed object에 대한 C# null-conditional 사용 금지 패턴이 이미 기록되어 있습니다. 특히 `UnityEngine.Object`가 파괴된 뒤에도 C# 참조는 남아 있을 수 있으므로 `?.` 호출이 `MissingReferenceException`으로 이어질 수 있다는 규칙이 명시되어 있습니다.

---

# 1. 최상위 아키텍처 리뷰

## 1.1 런타임 서비스 소유권이 아직 완전히 통일되지 않았습니다

### 관찰

프로젝트에는 런타임 서비스들을 `[RuntimeServices]` 루트 아래로 묶기 위한 공통 유틸리티가 있습니다. `RuntimeServiceOwnership.CreateServiceHost()`는 `[RuntimeServices]` 루트를 보장한 뒤 서비스 오브젝트를 그 아래 생성하고, `Adopt()`는 기존 서비스를 해당 루트 아래로 편입합니다.
또한 루트가 없으면 새 `GameObject("[RuntimeServices]")`를 만들고 `DontDestroyOnLoad`로 유지합니다.

이 방향 자체는 좋습니다. 문제는 모든 서비스가 이 체계를 쓰지는 않는다는 점입니다. 예를 들어 `SceneDomainAppScopeServices.Ensure()`는 씬 도메인 앱 스코프 서비스로 `SceneTransitionCoordinator`, `SceneFadeTransitionService`, `LoadingOverlayController`, `PresentationPreloadService`, `PortalRouteManager`, `GamePlayDataManager`, `MouseCursorService`를 보장합니다.
하지만 실제 코드베이스에는 별도의 `[RuntimeInitializeOnLoadMethod]`, 직접 `new GameObject`, 직접 `DontDestroyOnLoad`를 쓰는 서비스들이 여전히 많습니다.

### 위험

이 구조에서는 다음 문제가 생기기 쉽습니다.

* 부트스트랩 순서가 서비스마다 달라집니다.
* 씬에 작성된 authored/prefab 서비스와 코드가 만든 bootstrap 서비스가 서로 교체되는 시점이 불안정해집니다.
* `DontDestroyOnLoad` 오브젝트가 여러 루트로 흩어져 Title 복귀, Hub 복귀, Run 종료 시 정리 책임이 불명확해집니다.
* 이미 `SceneDomainTitleCleanupScope`가 특정 persistent 타입을 명시적으로 찾아 지우고 있는데, 이 리스트가 늘어날수록 “정리 대상 누락” 위험도 같이 늘어납니다. 현재도 Title cleanup은 `PauseMenuUI`, `SettingsPanelUI`, `KeyBindingPanelUI`, `UIManager`, `GlobalUIRoot`, `CameraBootstrap`만 직접 지웁니다.

### 권장 방향

* 새 기능은 무조건 `RuntimeServiceOwnership` 또는 `SceneDomain*Scope` 중 하나로 소유권을 명시하는 쪽이 좋습니다.
* 기존 서비스는 한 번에 전부 갈아엎기보다:
  1. App-scope 서비스
  2. Gameplay-session 서비스
  3. Scene-authored 서비스
  4. Emergency fallback 서비스
  로 분류표를 만들고, `RuntimeInitializeOnLoadMethod`와 `DontDestroyOnLoad` 사용처를 점진적으로 줄이는 게 안전합니다.

---

## 1.2 `GameSettingsService` → `MouseCursorService` 조기 생성 경로는 실제 위험도가 높습니다

### 관찰

`GameSettingsService`는 `BeforeSceneLoad`에서 `EnsureInstance()`를 호출합니다.
`Awake()`에서는 자기 자신을 `DontDestroyOnLoad`로 유지하고 초기화를 수행합니다.
초기화 중 `ApplyDisplaySettings()`가 실행되고, 여기서 `MouseCursorService.EnsureInstance().NotifyDisplayConfigurationChanged()`를 호출합니다.

반면 `MouseCursorService.EnsureInstance()`는 기존 인스턴스가 없으면 `new GameObject(nameof(MouseCursorService))`를 만들고 컴포넌트를 붙입니다.

`MouseCursorService.Awake()`에는 bootstrap 인스턴스와 authored 인스턴스 교체 로직이 있습니다. 다만 기존 인스턴스가 `isBootstrapInstance`일 때만 새 authored 인스턴스가 기존 것을 파괴하고 교체할 수 있습니다.

### 문제

`GameSettingsService.ApplyDisplaySettings()`가 만든 `MouseCursorService`는 `EnsureInstance(markBootstrap: false)` 경로로 만들어질 수 있습니다. 그러면 `isBootstrapInstance`가 false인 런타임 생성 서비스가 먼저 살아남고, 이후 씬에 authored cursor service가 있어도 교체 로직이 기대대로 작동하지 않을 가능성이 있습니다.

### 영향

* authored cursor prefab/canvas/image가 무시될 수 있습니다.
* 런타임 fallback cursor presentation이 생길 수 있습니다.
* 설정 서비스가 커서 프레젠테이션의 소유권까지 간접적으로 결정하는 구조가 됩니다.

### 권장 방향

* `GameSettingsService`는 디스플레이 변경 “알림”만 발행하고, `MouseCursorService` 생성은 `SceneDomainAppScopeServices` 쪽에서만 담당하는 게 더 안전합니다.
* 또는 `MouseCursorService.EnsureInstance()`에 “설정 적용 중 호출된 조기 생성”도 bootstrap instance로 표시되도록 통일해야 합니다.

---

# 2. Save / Persistent Data 리뷰

## 2.1 `GameDataManager.SaveData()`가 `ItemManager` 준비 상태를 확인하지 않습니다

### 관찰

`ItemManager`에는 싱글톤이 존재하지만 아직 DB가 adopt되지 않은 상태를 구분하기 위한 `IsReady`가 있습니다. 설명상 “싱글톤 인스턴스만 존재하고 database가 아직 adopt되지 않은 중간 상태를 구분”하기 위한 프로퍼티입니다.

그런데 `GameDataManager.SaveData()`는 `ItemManager.Instance != null`만 확인하고, 곧바로 unlocked weapon/relic ID 목록을 저장 데이터에 덮어씁니다.

### 문제

`ItemManager`는 `BeforeSceneLoad`에서 빈 `GameObject(nameof(ItemManager))`를 만들 수 있습니다.
즉, `Instance`는 존재하지만 아직 `database`가 adopt되지 않았거나 초기화가 끝나지 않은 상태가 가능합니다.

이때 `GameDataManager.SaveData()`가 실행되면:

* 기존 save에 있던 unlock 목록을
* 아직 준비되지 않은 `ItemManager`의 빈/불완전 목록으로
* 덮어쓸 가능성이 있습니다.

### 심각도

**P1 후보**입니다. 실제 저장 데이터 손실 가능성이 있기 때문입니다.

### 권장 방향

* 저장 시 `ItemManager.Instance != null && ItemManager.Instance.IsReady`를 조건으로 삼아야 합니다.
* 준비되지 않았으면 기존 `Data.itemData`를 보존해야 합니다.
* 이 케이스는 PlayMode 테스트로 잡는 것이 좋습니다.
  * `ItemManager` 인스턴스만 있고 DB 미주입
  * 기존 save에는 unlock 데이터 있음
  * `GameDataManager.SaveData()`
  * unlock 데이터가 보존되는지 검증

---

## 2.2 Run data와 persistent data 분리는 좋습니다

### 관찰

`GamePlayDataManager`는 run 시작/종료를 담당하고, run 종료 시 pending progress를 commit한 뒤 `PortalRouteManager` plan을 clear합니다.
또한 pending run progress commit은 `GameDataManager.Instance.EnsureData()`를 통해 save data를 확보한 뒤 정책 객체에 위임합니다.

### 평가

이 방향은 좋습니다.

* run-session state와 persistent save state가 분리되어 있습니다.
* `CurrencyManager`처럼 run 중 획득분을 pending delta로 들고 있다가 commit하는 모델과 잘 맞습니다.
* 사망/중단/씬 전환에서 어떤 데이터가 영구 저장되는지 추적하기 쉽습니다.

### 남은 위험

다만 여러 매니저가 각자 `GameDataManager.Instance`를 직접 참조하는 구조라, 저장 타이밍이 늘어날수록 “어느 manager가 save DTO의 어느 필드를 최종 권한으로 덮어쓰는가”가 흐려질 수 있습니다.

---

## 2.3 `GameDataRepository`의 atomic write는 좋지만, corrupted save 보존 정책은 더 명확해야 합니다

### 관찰

`GameDataRepository.Save()`는 JSON을 만들고 persistent save와 inspectable copy를 씁니다.
내부적으로 temp file, backup, `File.Replace` fallback을 사용하는 atomic write 흐름도 존재합니다.

### 평가

파일 쓰기 안정성은 좋은 편입니다.

### 위험

`LoadOrCreate()`는 로드에 실패하면 fresh data를 만들고 저장합니다. 이 자체는 일반적인 패턴이지만, JSON 파싱 실패나 손상 save가 있을 때 기존 파일을 즉시 덮어쓸 수 있습니다.

### 권장 방향

* corrupted save 감지 시:
  * 기존 파일을 `.corrupt-yyyyMMddHHmmss` 등으로 보존
  * fresh save 생성
  * 로그에 명확히 기록
  하는 정책이 더 안전합니다.

---

# 3. Scene / Build Settings / Test 구조 리뷰

## 3.1 PlayMode smoke test의 Boss scene 이름이 Build Settings와 맞지 않습니다

### 관찰

`SceneSmokePlayModeTests`는 boss scene 이름으로 `"ProtoTypeBoss 1"`을 사용합니다.

하지만 `EditorBuildSettings.asset`에는 boss scene으로 다음이 등록되어 있습니다.

* `Assets/Scenes/HeoMinSeok_Boss_Shadow.unity`
* `Assets/Scenes/HeoMinSeok_Boss_Dragon.unity`
* `Assets/Scenes/SangHyup_Boss_SlimeQueen.unity`
* `Assets/Scenes/LeeJunmo_Boss_DemonKing.unity`


반면 `"ProtoTypeBoss 1"`은 Build Settings에 없습니다. `SceneManager.LoadSceneAsync(sceneName)` 기반 테스트라면 빌드 설정에 없는 scene name은 실패할 가능성이 큽니다.

### 심각도

**P2**입니다. 테스트가 오래된 씬 이름에 묶여 있을 가능성이 높습니다.

### 권장 방향

* smoke test를 실제 build route scene으로 갱신해야 합니다.
* 최소한:
  * Hub
  * 각 corridor
  * 각 boss scene
  * Title 복귀
  * run start/end
  를 분리된 smoke suite로 구성하는 게 좋습니다.

---

## 3.2 Build Settings에 disabled `SampleScene`이 남아 있습니다

### 관찰

`ProjectSettings/EditorBuildSettings.asset`에는 disabled 상태의 `Assets/Scenes/SampleScene.unity`이 남아 있습니다.

실제 scene 파일 목록에서 `SampleScene.unity`는 확인되지 않았습니다.

### 영향

disabled라서 즉시 빌드 실패 원인은 아닐 수 있습니다. 하지만 다음 문제를 만듭니다.

* 프로젝트 구조 파악 시 노이즈
* scene migration/history 혼동
* 자동 검증 스크립트에서 “build settings path missing”으로 잡힐 수 있음

### 권장 방향

* disabled scene이라도 실제 파일이 없으면 제거하는 편이 좋습니다.
* 단, 이건 scene/build settings 수정이므로 별도 승인 하에 처리하는 게 맞습니다.

---

# 4. UI / Presentation 리뷰

## 4.1 Runtime UI fallback이 여러 곳에 존재합니다

### 관찰

`LoadingOverlayController`는 authored `LoadingOverlayView`가 없으면 런타임으로 `Canvas`, `CanvasScaler`, `GraphicRaycaster`, `Image`, TMP text, `LoadingOverlayView`를 생성합니다.

`MouseCursorService`도 authored cursor canvas/image를 찾지 못하면 런타임으로 `MouseCursorCanvas`, `Canvas`, `CursorImage`, `Image`를 만듭니다.

좋은 점은 두 코드 모두 `RuntimePresentationFallbackAudit.Record(...)`를 호출해 fallback 사용을 기록한다는 점입니다.

### 문제

프로젝트 규칙상 UI, HUD, 팝업, fade overlay, authored presentation object는 보통 scene/prefab에서 배치하고 serialized reference로 구동하는 방향입니다. 런타임 fallback은 개발 중 안전장치로는 좋지만, 실제 구조가 이 fallback에 의존하기 시작하면 다음 문제가 생깁니다.

* UI sorting order 충돌
* Canvas 중복
* scene/prefab authoring 누락이 조용히 숨겨짐
* QA에서 “왜 어떤 씬만 UI가 다르게 보이는지” 추적하기 어려움

### 권장 방향

* fallback은 유지하되, 빌드/런타임에서 경고를 더 강하게 내는 게 좋습니다.
* authored presentation 누락을 잡는 validation test를 추가하는 게 좋습니다.
* fallback audit 결과를 한 곳에서 볼 수 있는 editor window나 log summary가 있으면 좋습니다.

---

## 4.2 Unity object에 대한 `?.` 사용 패턴이 아직 일부 남아 있습니다

### 관찰

프로젝트 ErrorLog는 이미 destroyed `UnityEngine.Object`에 대해 C# null-conditional cleanup을 쓰면 `MissingReferenceException`이 날 수 있다고 기록합니다.

그런데 예를 들어 `SceneDomainTitleCleanupScope.Cleanup()`에는 persistent cleanup 과정에서 `SoundManager.Instance?.StopMusic()`, `RunRouteBgmService.EnsureInstance()?.ForceRefreshActiveSceneBgm()`, `LoadingOverlayController.Instance?.ForceHidePresentation()`, `PortalRouteManager.Instance?.ClearPlan()` 같은 null-conditional 호출이 있습니다.

또한 `PlayerCombatInput2D`의 `OnEnable`/`OnDisable`에서도 `gameplayEventRelay?.Register(this)`와 `gameplayEventRelay?.Unregister(this)` 패턴이 있습니다.

### 구분 필요

모든 `?.`가 문제는 아닙니다.

* 순수 C# event/delegate에는 괜찮습니다.
* `UnityEngine.Object`가 아니거나 destroyed 가능성이 낮은 서비스에는 위험이 낮습니다.
* 하지만 scene unload, global UI replacement, DDOL cleanup 경계에서 `UnityEngine.Object` 참조에 쓰이면 위험합니다.

### 권장 방향

* 전역적으로 `?.`를 없애자는 게 아니라, 다음 조건에 해당하는 곳만 우선 정리하면 됩니다.
  * `MonoBehaviour`, `ScriptableObject`, `Component`, `GameObject` 참조
  * scene unload / OnDisable / OnDestroy / cleanup / title return 경계
  * persistent object 교체 경계
* 해당 경우는 `if (obj != null) obj.Method();` 형태가 안전합니다.

---

# 5. Dialogue 시스템 리뷰

## 5.1 `DialogueController`는 책임이 매우 많습니다

### 관찰

`DialogueController`는 singleton 등록, runtime reference resolve, `DialogueService` 등록, scene loaded hook, input 처리, Ink story 진행, tag handler, choice UI, affection/choice failure 처리 등을 한 클래스에서 관리합니다.

runtime reference는 `DialogueRuntimeReferenceResolver`로 어느 정도 분리되어 있습니다. `DialogueController`는 `ResolveRuntimeReferences()`에서 이를 호출합니다.

### 평가

좋은 점:

* dialogue scene reference resolution을 별도 resolver로 분리한 점은 좋습니다.
* `ContinueStory()` 흐름이 비교적 명시적입니다.
* view/director/tag handler validate 단계가 있습니다.

위험:

* controller가 Ink, UI, input, tags, affection, special effect를 모두 알고 있습니다.
* 새 dialogue tag나 effect가 추가될수록 controller가 계속 커질 가능성이 높습니다.
* scene unload 중 view/director가 파괴될 경우 방어가 균일하지 않을 수 있습니다.

---

## 5.2 `DialogueView`는 2500줄 이상으로, view 하나가 너무 많은 presentation 책임을 가집니다

### 관찰

`DialogueView`는 파일 길이가 약 2535줄입니다. 클래스 본문은 `DialogueView : MonoBehaviour`로 시작합니다.
`Awake()`에서 theme resolve, default cache, effect reset, presentation 초기화, choice/text clear를 모두 수행합니다.
`OnEnable`, `OnDisable`, `OnDestroy`에서도 motion/effect/tween/material cleanup을 담당합니다.
또한 typing, rich text reveal plan, camera shake, text effects, continue icon 처리까지 직접 담당합니다.

### 문제

이 클래스는 “View”라기보다 다음 역할을 모두 합니다.

* Dialogue text presenter
* Typing coroutine owner
* TMP rich text effect player
* Camera shake requester
* Choice UI presenter
* Dialogue theme/material manager
* Affection feedback presenter
* Animation/tween cleanup owner

### 심각도

**P2 유지보수 위험**입니다. 지금 당장 버그라고 하긴 어렵지만, 기능 추가와 QA 단계에서 버그 수정 비용이 크게 증가할 구조입니다.

### 권장 분리 방향

한 번에 갈라내기보다는 아래 순서가 안전합니다.

1. `DialogueTypingPresenter`
   * `TypeText`, `SkipTyping`, `TypeTextRoutine`, text reveal/effect 관련
2. `DialogueChoicePresenter`
   * choice button 표시, 선택 가능 상태, 선택 failure presentation
3. `DialogueThemePresenter`
   * theme/material/font/color 적용
4. `DialogueMotionPresenter`
   * camera shake, text shake, punch/scale/tween cleanup

이때 public serialized field 이름을 바꾸면 prefab reference migration 위험이 있으므로, 먼저 helper 클래스를 내부 위임 객체로 두고 serialized field 이동은 나중에 하는 게 안전합니다.

---

# 6. Combat / Ability 시스템 리뷰

## 6.1 `AbilitySystem`은 설계 흔적은 좋지만 중앙 책임이 여전히 큽니다

### 관찰

`AbilitySystem`은 ability activation, cooldown ticking, casting, buffered activation, gameplay event, scene transition cleanup, persistent runtime state capture/restore까지 폭넓게 담당합니다. `Awake()`에서 runtime services를 준비하고 초기 ability를 초기화합니다.
`Update()`에서는 cooldown tick, casting tick, buffered activation consume을 처리합니다.

좋은 점은 scene transition cleanup이 꽤 명확히 작성되어 있다는 점입니다. 현재 casting/executing/parallel ability에 cleanup 기회를 주고, granted tags, gameplay event waiter, coroutine, visual router, extra cleanup tag를 정리하는 흐름이 있습니다.

### 좋은 점

* 씬 전환 cleanup 책임이 명시되어 있습니다.
* ability runtime state persist/restore 흐름이 있습니다.
* cooldown/casting/visual/event router 같은 하위 컨트롤러로 일부 분리하려는 구조가 보입니다.

### 위험

`AbilitySystem`이 여전히 너무 많은 lifecycle을 직접 들고 있습니다.

특히 `GameplayEventRaised` event accessor는 `gameplayEventChannel`이 null이면 subscribe를 그냥 무시합니다.
대부분은 `Awake()` 이후 구독하므로 괜찮을 수 있지만, 실행 순서가 꼬이면 “구독 코드는 실행됐는데 실제로는 등록되지 않은” 상황이 생길 수 있습니다.

### 권장 방향

* event accessor에서 channel이 없으면 `EnsureRuntimeServicesReady()`를 호출하거나, pending subscriber를 저장하는 방식이 안전합니다.
* `AbilitySystem` 자체는 지금 당장 대수술보다:
  1. event channel 안정화
  2. scene transition cleanup 테스트
  3. persistent state capture/restore 테스트
  4. 이후 책임별 파일 분리
  순서가 좋습니다.

---

## 6.2 `TagSystem.TagCount`가 내부 배열을 그대로 노출합니다

### 관찰

`TagSystem`은 `_counts`, `_explicitCounts`, `_bits`로 tag 상태를 관리합니다. 그런데 `public int[] TagCount => _counts;`로 내부 배열을 그대로 반환합니다.

`AddTag()`는 explicit count와 closure count를 갱신하고, count 변경 이벤트와 added 이벤트를 발행합니다.

### 문제

외부 코드가 `TagCount` 배열을 직접 수정하면:

* `_bits`와 `_counts`가 불일치할 수 있습니다.
* `OnTagCountChanged`, `OnTagAdded`, `OnTagRemoved` 이벤트가 발행되지 않습니다.
* tag invariant가 깨집니다.

### 심각도

**P1/P2 사이**입니다. 실제 외부 mutation 사용처가 있다면 P1, 단순 조회만 한다면 P2입니다.

### 권장 방향

* `TagCount`를 제거하거나 `IReadOnlyList<int>`/copy 반환으로 바꾸는 게 이상적입니다.
* 성능상 copy가 싫다면 `GetTagCount(GameplayTag tag)` 같은 read API를 쓰는 게 안전합니다.
* 기존 외부 사용처를 먼저 확인한 뒤 migration해야 합니다.

---

## 6.3 Player input 쪽 runtime `AddComponent`는 prefab authoring 누락을 숨길 수 있습니다

### 관찰

`PlayerCombatInput2D.Awake()`는 필요한 컴포넌트를 찾고, 없으면 `AbilityGameplayEventRelay`와 `WeaponExecutorRunner`를 런타임에 추가합니다.

### 장점

* prefab이 불완전해도 런타임에서 동작할 가능성을 높입니다.
* 개발 중 빠른 복구에는 유용합니다.

### 위험

* prefab authoring 오류가 QA까지 숨어 있을 수 있습니다.
* 어떤 컴포넌트가 prefab-authored인지, runtime-created인지 추적이 어려워집니다.
* component lifecycle order가 prefab에 명시된 경우보다 덜 예측 가능합니다.

### 권장 방향

* 런타임 추가는 유지하더라도 warning/audit을 남기는 게 좋습니다.
* production prefab validator에서 “필수 컴포넌트 누락”을 잡는 것이 더 좋습니다.
* `RequireComponent`를 붙일 수 있는 경우는 일부 적용을 고려할 수 있습니다.

---

# 7. Boss / Enemy 코드 리뷰

## 7.1 DemonKing ability logic 파일은 분리 필요성이 큽니다

### 관찰

`DemonKingAbilityLogics.cs`는 약 3866줄이고, 한 파일 안에 여러 DemonKing ability logic class가 들어 있습니다. 예를 들어 다음 ability들이 같은 파일에 있습니다.

* `AbilityLogic_DemonKingPierceCombo`
* `AbilityLogic_DemonKingHeavySlash`
* `AbilityLogic_DemonKingThrowEgoSword`
* `AbilityLogic_DemonKingHomingMagic`
* `AbilityLogic_DemonKingBombardment`
* `AbilityLogic_DemonKingExplosionJump`
* `AbilityLogic_DemonKingRecallEgoSword`
* `AbilityLogic_DemonKingEgoSwordVerticalStrike`
* `AbilityLogic_DemonKingEgoSwordCrossLaser`
* `AbilityLogic_DemonKingWallBounceRush`
* `AbilityLogic_DemonKingGroggyRecoverCounter`
* `AbilityLogic_DemonKingFinalDesperation`

이 클래스들은 모두 하나의 파일에 선언되어 있습니다.

### 문제

* merge conflict 가능성이 큽니다.
* ability 하나를 수정하려 해도 전체 파일 컨텍스트가 커집니다.
* 능력별 책임/상태/cleanup 비교가 어렵습니다.
* boss phase 변경 시 영향 범위 추적이 어렵습니다.

### 권장 방향

가장 안전한 개선은 **클래스명과 namespace는 그대로 유지하고 파일만 분리**하는 것입니다.

예:

* `AbilityLogic_DemonKingPierceCombo.cs`
* `AbilityLogic_DemonKingHeavySlash.cs`
* `AbilityLogic_DemonKingThrowEgoSword.cs`
* ...

주의할 점:

* Unity serialization은 보통 class name/namespace 기준이라 파일 이동 자체는 안전한 편이지만, asmdef 포함 여부와 compile 결과는 반드시 확인해야 합니다.
* ScriptableObject나 serialized reference에서 type rename은 금지해야 합니다.
* 파일 분리는 behavior-preserving refactor로 진행하고, Unity compile/import 확인이 필요합니다.

---

## 7.2 Boss actor/controller 계열도 “대형 클래스” 위험이 큽니다

### 관찰

이번 전체 스캔에서 긴 파일 상위권에 다음이 포함됐습니다.

* `DemonKingAbilityLogics.cs` 약 3866줄
* `EgoSwordActor.cs` 약 2198줄
* `DemonKingController.cs` 약 1642줄
* `DemonKingPrimitiveVisual.cs` 약 1529줄
* SlimeQueen 계열 boss 파일들도 1000줄 이상 다수

### 평가

Boss는 상태, phase, animation, hitbox, telegraph, VFX, camera shake, projectile, cleanup이 복잡하므로 파일이 커지는 건 어느 정도 자연스럽습니다.
하지만 1500~3800줄 단위가 되면 “버그 수정 비용”이 급격히 늘어납니다.

### 권장 분리 기준

boss 코드는 다음 기준으로 쪼개는 게 좋습니다.

* Phase state
* Ability logic
* Telegraph/presentation
* Actor/projectile helper
* Cleanup/lifecycle
* Debug/editor visualization

특히 ability logic은 위에서 말한 대로 파일 분리만 해도 효과가 큽니다.

---

# 8. Input / Settings 리뷰

## 8.1 `InputBindingService`의 key scan은 캐싱 여지가 있습니다

### 관찰

`InputBindingService` 쪽 key compatibility helper는 key 입력 감지 시 `Enum.GetValues(typeof(KeyCode))`를 호출하고 전체 `KeyCode`를 순회합니다.
Input System 경로에서도 다시 `Enum.GetValues(typeof(KeyCode))`를 호출합니다.

### 평가

이 함수가 key rebinding 모드에서만 호출된다면 큰 문제는 아닐 수 있습니다.
하지만 매 프레임 호출되거나 UI가 열려 있는 동안 계속 호출된다면 불필요한 enum allocation/iteration 비용이 생깁니다.

### 권장 방향

* `private static readonly KeyCode[] CachedKeyCodes = ...` 형태로 캐싱하는 게 좋습니다.
* `KeyCode.None` 제외, mouse/button 제외 등 필요한 필터링도 한 번만 해두면 됩니다.

---

## 8.2 `InputBindingService`도 독립 GameObject 생성 경로를 가집니다

### 관찰

`InputBindingService.EnsureInstance()`는 기존 서비스가 없으면 `new GameObject(nameof(InputBindingService))`를 만들고 컴포넌트를 붙입니다.

### 평가

기능상으로는 간단하고 확실합니다.
하지만 앞서 언급한 런타임 서비스 소유권 관점에서는 `[RuntimeServices]` root 아래로 묶이지 않습니다.

### 권장 방향

* 다른 서비스들과 동일하게 `RuntimeServiceOwnership.CreateServiceHost()`를 사용하는 방향을 고려할 수 있습니다.
* 단, 이미 씬-authored `InputBindingService`가 있다면 교체/Adopt 정책을 먼저 정해야 합니다.

---

# 9. Loading / Addressables 리뷰

## 9.1 Addressables 실패 후 fallback asset을 쓰면서 operation은 error로 완료됩니다

### 관찰

`AddressableAssetProvider.CompleteLoadOperation()`은 Addressables handle이 실패하면 `errorMessage`를 채웁니다. 이후 `loadedAsset`이 null이면 `state.SourceAsset`으로 fallback합니다.
fallback을 썼다는 debug event도 기록합니다.
하지만 최종적으로 `operation.Complete(errorMessage)`를 호출합니다.

### 문제

실제 asset은 fallback으로 사용 가능하지만, operation 결과는 실패로 표시될 수 있습니다.

이 정책이 의도라면 괜찮습니다. 예를 들어 “Addressables 실패는 반드시 loading error로 취급하되, 에디터/개발 fallback만 제공한다”는 정책일 수 있습니다.
하지만 UX나 loading overlay 입장에서는 “asset은 표시되는데 loading operation은 실패”라는 애매한 상태가 됩니다.

### 권장 방향

정책을 명확히 해야 합니다.

선택지:

1. **Fallback success 정책**
   * fallback asset이 있으면 operation은 success
   * 별도 warning/debug event 기록
2. **Addressables strict failure 정책**
   * Addressables 실패는 operation failure
   * fallback은 editor/dev only
   * UI에서도 명확히 실패로 표시
3. **Config 기반 정책**
   * Development build에서는 fallback success
   * Release build에서는 strict failure

현재 코드만 보면 1번과 2번이 섞여 있는 인상입니다.

---

# 10. Test Coverage 리뷰

## 10.1 현재 smoke test는 프로젝트 규모에 비해 좁고 오래된 가능성이 있습니다

### 관찰

`SceneSmokePlayModeTests`는 Hub와 Boss scene의 core object 존재 여부, camera persistence 등을 확인합니다.
하지만 Boss scene 이름은 build settings와 맞지 않는 `"ProtoTypeBoss 1"`입니다.

### 부족한 테스트 영역

현재 코드베이스 구조상 다음 테스트가 중요합니다.

* Save data migration / corrupted save 보존
* ItemManager not-ready 상태에서 SaveData 보존
* Run start/end pending progress commit
* Title → Hub → Corridor → Boss → Hub/Title scene transition
* UI fallback audit 발생 여부
* MouseCursorService authored instance 교체
* AbilitySystem scene transition cleanup
* TagSystem invariant
* Addressables fallback success/failure semantics
* Dialogue scene unload 중 cleanup

### 권장 우선순위

가장 먼저 추가하면 좋은 테스트는 다음 4개입니다.

1. `GameDataManager_SaveData_DoesNotOverwriteItemUnlocks_WhenItemManagerNotReady`
2. `MouseCursorService_AuthoredInstance_ReplacesBootstrapInstance`
3. `AbilitySystem_CancelAllForSceneTransition_RemovesGrantedTagsAndStopsExecutions`
4. `SceneSmoke_AllBuildSettingsGameplayScenes_LoadCoreServices`

---

# 11. 코드베이스 전체에서 보이는 구조적 패턴

## 11.1 장점

### 명시적 lifecycle 의식이 있습니다

`AbilitySystem.CancelAllForSceneTransition()`처럼 씬 전환 cleanup을 명시적으로 다루는 코드가 있습니다.

### runtime fallback audit를 남기려는 시도가 있습니다

`LoadingOverlayController`와 `MouseCursorService`는 fallback UI를 만들 때 audit를 기록합니다.

### scene domain이라는 개념을 도입했습니다

`SceneDomainAppScopeServices`, `SceneDomainGameplaySessionScope`, `SceneDomainTitleCleanupScope`가 있어 App-scope, gameplay-session, title cleanup을 분리하려는 방향이 보입니다.

### save repository는 파일 쓰기 안정성을 고려했습니다

temp write, replace, backup fallback을 쓰는 atomic write 흐름이 있습니다.

---

## 11.2 가장 큰 구조적 약점

### 서비스 생성 경로가 너무 많습니다

`RuntimeServiceOwnership`, `SceneDomain*Scope`, 각 서비스의 `RuntimeInitializeOnLoadMethod`, 각 서비스의 직접 `new GameObject`, scene-authored instance가 동시에 존재합니다.
이건 중장기적으로 가장 큰 구조 리스크입니다.

### 대형 클래스가 많습니다

Dialogue, Boss, Ability, Inventory presentation, Editor tooling 쪽에 1000~5000줄 파일이 다수 있습니다.
이 상태에서는 “작은 버그 수정”도 영향 범위 파악이 어렵습니다.

### presentation fallback과 authored presentation 경계가 아직 흐립니다

프로젝트 규칙은 authored UI를 선호하지만, 실제 코드에는 runtime fallback UI가 여럿 있습니다. fallback audit가 있으므로 방향은 좋지만, QA/production에서는 fallback 발생 자체를 검증 대상으로 삼아야 합니다.

### 테스트가 코드 규모를 따라가지 못합니다

현재 구조는 scene transition, save, ability cleanup, UI fallback, boss ability lifecycle 쪽에 버그가 생기기 쉬운데, 자동 테스트는 그만큼 촘촘하지 않습니다.

---

# 12. 우선순위별 액션 제안

## P1 — 실제 버그/데이터 손실 가능성이 있는 후보

1. **`GameDataManager.SaveData()`에서 `ItemManager.IsReady` 확인**
   * `Instance != null`만으로 unlock 목록을 덮어쓰면 위험합니다.

2. **`GameSettingsService`가 `MouseCursorService`를 조기 생성하는 경로 정리**
   * authored cursor service 교체 실패 가능성이 있습니다.

3. **Unity object cleanup 경계의 `?.` 재스캔**
   * 이미 ErrorLog에 재발 방지 규칙이 기록되어 있습니다.

4. **`TagSystem.TagCount` 내부 배열 노출 제거 또는 사용처 검증**
   * 외부 mutation이 가능해 invariant가 깨질 수 있습니다.

---

## P2 — 유지보수성/구조 안정성 개선

1. **`DemonKingAbilityLogics.cs` 파일 분리**
   * 여러 ability class가 한 파일에 있습니다.

2. **`DialogueView` presentation 책임 분리**
   * typing, effects, theme, cleanup, choice UI가 한 view에 과도하게 집중되어 있습니다.

3. **`AbilitySystem` event accessor 안정화**
   * channel null 시 구독이 조용히 버려질 수 있습니다.

4. **runtime UI fallback을 validation 대상으로 전환**
   * fallback 자체는 유지하되, authored 누락을 QA에서 확실히 잡아야 합니다.

5. **`InputBindingService` key scan 캐싱**
   * `Enum.GetValues(typeof(KeyCode))` 반복 호출을 캐싱할 수 있습니다.

---

## P3 — 정리/품질 개선

1. **Build Settings의 missing/legacy scene 정리**
   * disabled `SampleScene`이 남아 있습니다.

2. **Scene smoke test scene 이름 갱신**
   * 테스트는 `"ProtoTypeBoss 1"`을 쓰지만 build settings와 맞지 않습니다.

3. **runtime AddComponent audit**
   * `PlayerCombatInput2D`처럼 필수 gameplay component를 runtime으로 붙이는 곳은 warning/validator를 붙이는 게 좋습니다.

---

# 13. 이번 리뷰의 결론

현재 코드베이스는 기능이 꽤 많이 쌓인 Unity 프로젝트답게, **개별 시스템은 나름의 안전장치와 lifecycle 의식이 있지만, 전역 서비스 소유권과 대형 클래스 관리가 가장 큰 리스크**입니다.

가장 먼저 봐야 할 것은 다음 순서라고 봅니다.

1. **Save 데이터 손실 가능성**
   * `GameDataManager` ↔ `ItemManager.IsReady`

2. **런타임 서비스 생성 경로 통일**
   * 특히 `GameSettingsService` → `MouseCursorService`

3. **scene transition cleanup 안정화**
   * Unity object `?.` 사용 재점검

4. **테스트 갱신**
   * Build Settings 기준 scene smoke test
   * Save/Run/Ability cleanup 테스트

5. **대형 파일 분리**
   * `DemonKingAbilityLogics.cs`
   * `DialogueView.cs`
   * `AbilitySystem.cs` 일부 책임

---

## 검토에 사용한 명령 / 체크

* ✅ `rg --files Assets -g '*.cs' | wc -l`
* ✅ `rg --files Assets -g '*.unity' | rg 'SampleScene|ProtoTypeBoss|Boss' || true`
* ✅ `nl -ba ProjectSettings/EditorBuildSettings.asset | sed -n '1,70p'`
* ✅ `rg --files Assets -g 'GameSettingsService.cs'`
* ✅ `nl -ba Assets/LeeJunMo/Script/Settings/GameSettingsService.cs | sed -n '80,100p;320,385p;525,555p'`
* ✅ `nl -ba Assets/LeeJunMo/Script/UIStructure/MouseCursorService.cs | sed -n '150,205p;300,355p'`
* ✅ `nl -ba Assets/LeeJunMo/Script/SaveData/GameDataManager.cs | sed -n '95,125p'`
* ✅ `nl -ba Assets/LeeJunMo/Script/Looting/ItemManager.cs | sed -n '15,35p'`
* ✅ `nl -ba Assets/Tests/PlayMode/SceneSmokePlayModeTests.cs | sed -n '1,25p'`
* ✅ `nl -ba Docs/ErrorLog.md | sed -n '50,72p'`
* ✅ `nl -ba Assets/LeeJunMo/Script/Loading/Runtime/LoadingOverlayController.cs | sed -n '707,765p'`
* ✅ `nl -ba Assets/LeeJunMo/Script/Loading/Runtime/AddressableAssetProvider.cs | sed -n '422,465p'`
* ✅ `rg --files Assets -g 'TagSystem.cs'`
* ✅ `nl -ba Assets/HeoMinSeok/_Project/Scripts/Gameplay/Abilities/Tags/Runtime/TagSystem.cs | sed -n '1,30p;48,75p'`
* ✅ `nl -ba Assets/HeoMinSeok/_Project/Scripts/Gameplay/Abilities/Runtime/AbilitySystem.cs | sed -n '250,320p;450,545p;1310,1435p'`
* ✅ `nl -ba Assets/LeeJunMo/Script/Dialogue/DialogueController.cs | sed -n '37,70p;285,323p;620,665p;1050,1065p'`
* ✅ `nl -ba Assets/LeeJunMo/Script/Dialogue/UI/DialogueView.cs | sed -n '860,875p;1068,1125p;1272,1426p'`
* ✅ `nl -ba Assets/LeeJunMo/Script/Input/InputBindingService.cs | sed -n '60,110p;270,315p'`
* ✅ `rg -n '^\s*public\s+(sealed\s+)?class\s+AbilityLogic_|^\s*public\s+class\s+AbilityLogic_' Assets/Script/Enemy/Boss/FSM/BossControllers/DemonKingBoss/Abilities/DemonKingAbilityLogics.cs`
* ⚠️ `dotnet build Assembly-CSharp.csproj --no-restore` — 현재 환경에서 Unity/MSBuild 검증을 완료하지 못했으므로 컴파일 성공은 주장하지 않습니다.
* ❌ `git diff --check HEAD^ HEAD` — 현재 브랜치 diff 기준으로 prefab trailing whitespace가 감지되었습니다. 이 리뷰의 핵심은 전체 코드베이스 정적 리뷰이므로 별도 수정은 하지 않았습니다.
