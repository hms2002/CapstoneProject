---
status: review
authority: reference-only
category: codebase-review
last_reviewed: 2026-06-03
scope: Assets C# scripts and project-level scene/test settings
---

# Overall Codebase Review - 2026-06-03

> Review / Reference Only
> 이 문서는 `Assets/` 아래 C# 스크립트 중심으로 현재 코드베이스를 정적 탐색한 전체 리뷰 기록입니다.
> 현재 표준 구조를 직접 정의하는 문서가 아니라, 위험 지점과 개선 우선순위를 Obsidian에서 다시 보기 위한 참고 문서입니다.

## 0. Review Scope

- `Assets/` 아래 C# 파일 전체를 인벤토리 기준으로 훑었습니다.
- 세부 리뷰는 다음 1차 프로젝트 코드 영역을 우선했습니다.
  - `Assets/LeeJunMo/Script/`
  - `Assets/HeoMinSeok/_Project/Scripts/`
  - `Assets/Script/Enemy/`
  - `Assets/Tests/`
  - `ProjectSettings/EditorBuildSettings.asset`
- `Ink`, 일부 plugin/third-party 성격의 코드는 전체 파일 수/라인 수 스캔에는 포함했지만, 프로젝트 고유 구조 판단에서는 우선순위를 낮췄습니다.
- Unity Editor 실행, PlayMode, player build, MSBuild 검증 없이 정적 코드 리뷰로만 판단했습니다.

## 1. Executive Summary

현재 코드베이스는 기능이 많이 쌓인 Unity 2D roguelike 프로젝트답게, 개별 시스템 안에는 lifecycle과 cleanup을 의식한 코드가 많이 있습니다. 특히 run/session data, ability cleanup, runtime fallback audit, scene domain 개념은 좋은 방향입니다.

다만 전체적으로 보면 가장 큰 위험은 다음 네 가지입니다.

1. 런타임 서비스 생성/소유권 경로가 아직 통일되지 않았습니다.
2. save data를 다른 manager runtime 상태로 덮어쓰는 시점에 readiness 검증이 부족한 곳이 있습니다.
3. Dialogue, Boss, Ability, Presentation 쪽에 대형 클래스/대형 파일이 많아 유지보수 비용이 큽니다.
4. 자동 테스트가 현재 코드 규모와 scene transition/save/ability complexity를 충분히 따라가지 못합니다.

## 2. Priority Findings

### P1 후보 - 데이터 손실 또는 실제 런타임 문제 가능성이 높은 항목

#### 2.1 `GameDataManager.SaveData()`가 `ItemManager.IsReady`를 확인하지 않음

`ItemManager`에는 singleton instance만 존재하고 database가 아직 adopt되지 않은 중간 상태를 구분하기 위한 `IsReady`가 있습니다.

하지만 `GameDataManager.SaveData()`는 `ItemManager.Instance != null`만 확인하고 unlocked weapon/relic ID 목록을 save DTO에 덮어씁니다.

위험:

- `ItemManager`가 bootstrap으로 먼저 생성됐지만 database가 아직 없을 수 있습니다.
- 이 상태에서 save가 발생하면 기존 unlock 목록이 빈 목록 또는 불완전한 목록으로 덮일 수 있습니다.
- save data 손실 가능성이 있으므로 우선순위가 높습니다.

권장:

- `ItemManager.Instance != null && ItemManager.Instance.IsReady`일 때만 item unlock 목록을 save DTO에 반영합니다.
- 준비되지 않았으면 기존 `Data.itemData`를 보존합니다.
- PlayMode 또는 EditMode 테스트로 `ItemManager` not-ready 상태에서 기존 unlock이 보존되는지 검증합니다.

#### 2.2 `GameSettingsService`가 `MouseCursorService`를 조기 생성할 수 있음

`GameSettingsService.ApplyDisplaySettings()`는 display setting 적용 후 `MouseCursorService.EnsureInstance().NotifyDisplayConfigurationChanged()`를 호출합니다.

`MouseCursorService.EnsureInstance()`는 instance가 없으면 runtime `GameObject`를 생성합니다. 이 생성은 bootstrap marker가 false인 경로로 발생할 수 있습니다.

위험:

- 이후 scene-authored cursor service가 있어도 기존 runtime-created service가 bootstrap instance로 인식되지 않아 교체가 기대대로 되지 않을 수 있습니다.
- authored cursor canvas/image가 무시되고 runtime fallback presentation으로 굴러갈 수 있습니다.
- display settings service가 cursor presentation 소유권을 간접적으로 결정하게 됩니다.

권장:

- `MouseCursorService` 생성 책임은 `SceneDomainAppScopeServices` 같은 app-scope bootstrap으로 제한합니다.
- `GameSettingsService`는 cursor service를 직접 생성하지 않고, 존재할 때만 display 변경 알림을 보내는 쪽이 안전합니다.
- 또는 `MouseCursorService.EnsureInstance()`의 early-created instance도 명확한 bootstrap instance로 표시되게 통일합니다.

#### 2.3 Unity destroyed object에 대한 null-conditional cleanup 재점검 필요

프로젝트 `Docs/ErrorLog.md`에는 이미 Unity destroyed object에 `?.`를 사용하면 `MissingReferenceException`이 날 수 있다는 재발 방지 규칙이 있습니다.

위험한 패턴:

- scene unload
- global UI replacement
- DDOL cleanup
- `OnDisable` / `OnDestroy`
- cleanup method에서 `UnityEngine.Object` 참조에 `?.` 사용

권장:

- 모든 `?.`를 없앨 필요는 없습니다.
- 다만 `MonoBehaviour`, `Component`, `GameObject`, `ScriptableObject` 참조이면서 scene unload/cleanup 경계에 있는 경우는 `if (obj != null) obj.Method();` 형태로 바꾸는 것이 안전합니다.

#### 2.4 `TagSystem.TagCount`가 내부 배열을 그대로 노출

`TagSystem`은 `_counts`, `_explicitCounts`, `_bits`로 tag invariant를 유지합니다. 그런데 `public int[] TagCount => _counts;` 형태로 내부 배열을 그대로 노출합니다.

위험:

- 외부 코드가 배열 값을 직접 수정할 수 있습니다.
- `_bits`와 `_counts`가 불일치할 수 있습니다.
- tag changed/added/removed event가 발행되지 않을 수 있습니다.

권장:

- 외부 사용처를 먼저 확인합니다.
- 가능한 경우 `GetTagCount(GameplayTag tag)` 또는 read-only API로 대체합니다.
- 성능상 copy가 부담되면 mutable array를 직접 주는 대신 명시적 read method를 제공합니다.

## 3. Runtime Service Ownership Review

### 3.1 좋은 방향

`RuntimeServiceOwnership`은 `[RuntimeServices]` root를 만들고 service host를 그 아래 생성하거나 기존 service를 adopt하는 공통 경계를 제공합니다.

이 구조는 다음 측면에서 좋습니다.

- DDOL service root를 시각적으로 한 곳에 모을 수 있습니다.
- runtime-created service가 scene hierarchy에 흩어지는 것을 줄일 수 있습니다.
- future cleanup/debugging이 쉬워집니다.

### 3.2 남은 문제

아직 모든 runtime service가 이 체계를 쓰지는 않습니다.

현재 코드베이스에는 다음이 혼재합니다.

- `RuntimeServiceOwnership.CreateServiceHost()`
- `SceneDomainAppScopeServices.Ensure()`
- 각 service의 `[RuntimeInitializeOnLoadMethod]`
- service별 직접 `new GameObject(...)`
- service별 직접 `DontDestroyOnLoad(...)`
- scene-authored service instance

위험:

- bootstrap 순서가 service마다 달라집니다.
- scene-authored instance와 runtime-created instance의 교체 정책이 일관되지 않습니다.
- Title 복귀, Hub 복귀, Run 종료 시 persistent object cleanup 누락이 발생하기 쉽습니다.

권장:

- service inventory를 다음 기준으로 분류합니다.
  - App-scope service
  - Gameplay-session service
  - Scene-authored service
  - Emergency fallback service
- 신규 service는 반드시 위 분류 중 하나로 소유권을 정합니다.
- 기존 service는 한 번에 전부 바꾸기보다 위험도가 높은 service부터 `[RuntimeServices]` 또는 scene domain scope로 이동합니다.

## 4. Save / Persistent Data Review

### 4.1 좋은 점

`GamePlayDataManager`는 run 시작/종료와 pending progress commit을 담당하고, persistent save data는 `GameDataManager` 쪽에 있습니다.

좋은 점:

- run-session state와 persistent save state가 분리되어 있습니다.
- run 종료 시 pending progress commit 흐름이 명확합니다.
- `CurrencyManager`의 pending run delta 모델과 방향이 맞습니다.

### 4.2 주의점

여러 manager가 `GameDataManager.Instance.Data`를 직접 읽고 쓰는 구조가 늘어나면, save DTO의 각 필드에 대한 최종 권한자가 흐려질 수 있습니다.

권장:

- save DTO 필드별 owner를 정리합니다.
- manager가 save data를 덮어쓸 때 readiness 조건을 명시합니다.
- migration/load/normalize/save 순서에서 어떤 manager가 어떤 필드를 갱신할 수 있는지 문서화합니다.

### 4.3 Repository atomic write는 좋지만 corrupted save 정책은 더 명확해야 함

`GameDataRepository`는 temp write, replace, backup fallback을 쓰는 편이라 파일 쓰기 안정성은 좋은 편입니다.

다만 load 실패 시 fresh data를 만들고 저장하는 흐름은 corrupted save를 즉시 덮어쓸 수 있습니다.

권장:

- JSON parse/load 실패 시 기존 파일을 `.corrupt-yyyyMMddHHmmss` 같은 이름으로 보존합니다.
- fresh save 생성과 corrupted backup 생성을 log에 명확히 남깁니다.

## 5. Scene / Build Settings / Test Review

### 5.1 PlayMode smoke test scene name mismatch

`SceneSmokePlayModeTests`는 boss scene name으로 `ProtoTypeBoss 1`을 사용합니다.

하지만 build settings에는 현재 실제 route/boss scene들이 등록되어 있고, `ProtoTypeBoss 1`은 build settings에 없습니다.

위험:

- `SceneManager.LoadSceneAsync(sceneName)` 기반 테스트는 build settings에 없는 scene name에서 실패할 수 있습니다.
- smoke test가 현재 route 구조를 반영하지 못할 가능성이 큽니다.

권장:

- smoke test scene list를 `EditorBuildSettings.asset` 기준으로 갱신합니다.
- Hub, corridor, boss, Title return, run start/end를 나누어 smoke suite를 구성합니다.

### 5.2 Disabled missing `SampleScene` 정리 후보

Build Settings에 disabled `Assets/Scenes/SampleScene.unity`가 남아 있으나 실제 scene 파일 목록에서는 확인되지 않았습니다.

영향:

- disabled라 즉시 build 실패 원인은 아닐 수 있습니다.
- 하지만 scene inventory와 validation에서 노이즈가 됩니다.

권장:

- 별도 승인 하에 build settings에서 제거하는 것이 좋습니다.

## 6. UI / Presentation Review

### 6.1 Runtime UI fallback이 여러 곳에 존재

`LoadingOverlayController`는 authored view가 없으면 runtime canvas/text/image를 생성합니다.

`MouseCursorService`도 authored cursor canvas/image를 찾지 못하면 runtime cursor canvas/image를 생성합니다.

좋은 점:

- fallback 생성 시 `RuntimePresentationFallbackAudit.Record(...)`를 호출합니다.
- authoring 누락을 완전히 조용히 숨기지는 않으려는 방향입니다.

위험:

- project rule은 UI/presentation object를 scene/prefab authored reference로 두는 방향을 선호합니다.
- fallback에 의존하기 시작하면 canvas sorting, duplicate canvas, visual inconsistency를 추적하기 어려워집니다.

권장:

- fallback은 emergency/dev fallback으로 유지합니다.
- production validation에서는 fallback audit 발생 자체를 실패 또는 강한 warning으로 봅니다.
- authored presentation 누락을 검사하는 scene/prefab validator를 추가합니다.

### 6.2 UIManager / Dialogue / Cleanup 경계의 Unity null semantics

Unity destroyed object 관련 `?.` 위험은 UI cleanup과 dialogue cleanup에서 특히 자주 재발할 수 있습니다.

권장:

- scene unload, Title return, GlobalUIRoot replacement, popup stack cleanup 경계의 `?.`를 우선 점검합니다.
- pure C# event/delegate는 제외해도 됩니다.

## 7. Dialogue Review

### 7.1 `DialogueController` 책임이 큼

`DialogueController`는 singleton 등록, runtime reference resolve, scene loaded hook, input 처리, Ink story 진행, tag handling, choice UI, affection/choice failure 처리 등 많은 책임을 가집니다.

좋은 점:

- `DialogueRuntimeReferenceResolver`로 runtime reference resolution을 일부 분리했습니다.
- story continue, tag 처리, view 표시 흐름이 비교적 명시적입니다.

위험:

- 새 dialogue tag/effect가 추가될수록 controller가 계속 커질 가능성이 큽니다.
- scene unload 중 view/director reference 파괴에 대한 방어가 균일하지 않을 수 있습니다.

권장:

- tag handling, choice handling, affection/effect routing을 작은 helper로 분리합니다.
- serialized reference migration 위험이 있으므로 field rename 없이 내부 위임부터 시작합니다.

### 7.2 `DialogueView`는 너무 큼

`DialogueView`는 약 2500줄 규모입니다.

현재 한 view가 다음 책임을 모두 가집니다.

- speaker/text UI
- typing coroutine
- TMP rich text reveal/effect
- continue icon
- choice UI
- dialogue theme/material/font/color
- camera shake
- text motion/tween cleanup
- affection feedback presentation

권장 분리 순서:

1. `DialogueTypingPresenter`
2. `DialogueChoicePresenter`
3. `DialogueThemePresenter`
4. `DialogueMotionPresenter`

주의:

- serialized field 이름 변경은 prefab migration 위험이 있으므로 피합니다.
- 첫 단계는 helper class 위임 또는 partial split 정도가 안전합니다.

## 8. Combat / Ability Review

### 8.1 `AbilitySystem`은 설계 흔적은 좋지만 중앙 책임이 큼

좋은 점:

- ability activation, cooldown, casting, buffered activation, scene transition cleanup, persistent state capture/restore가 명시적으로 구현되어 있습니다.
- `CancelAllForSceneTransition()`은 casting/executing/parallel ability cleanup, granted tag 회수, event waiter cancel, coroutine stop, visual router release 등을 고려합니다.

위험:

- `AbilitySystem`이 여전히 매우 많은 lifecycle 책임을 직접 가집니다.
- `GameplayEventRaised` event accessor는 `gameplayEventChannel`이 null이면 subscription을 조용히 버립니다.

권장:

- event accessor에서 channel이 없으면 `EnsureRuntimeServicesReady()`를 호출하거나 pending subscriber를 보존합니다.
- refactor는 한 번에 하지 말고 event channel 안정화, scene cleanup test, persistent state test 순서로 진행합니다.

### 8.2 Player input runtime `AddComponent`는 prefab authoring 누락을 숨길 수 있음

`PlayerCombatInput2D`는 `AbilityGameplayEventRelay`, `WeaponExecutorRunner`가 없으면 runtime으로 붙입니다.

장점:

- 불완전한 prefab에서도 실행 가능성이 높아집니다.

위험:

- prefab authoring 오류가 QA까지 숨어 있을 수 있습니다.
- component lifecycle order가 prefab-authored 구조보다 덜 명시적입니다.

권장:

- runtime add는 유지하더라도 warning/audit를 남깁니다.
- player prefab validator에서 필수 컴포넌트 누락을 잡습니다.
- 가능한 경우 `RequireComponent`를 검토합니다.

## 9. Boss / Enemy Review

### 9.1 `DemonKingAbilityLogics.cs` 파일 분리 필요

`DemonKingAbilityLogics.cs`는 약 3800줄 이상이며 여러 DemonKing ability logic class가 한 파일에 있습니다.

대표 class:

- `AbilityLogic_DemonKingPierceCombo`
- `AbilityLogic_DemonKingHeavySlash`
- `AbilityLogic_DemonKingThrowEgoSword`
- `AbilityLogic_DemonKingHomingMagic`
- `AbilityLogic_DemonKingBombardment`
- `AbilityLogic_DemonKingExplosionJump`
- `AbilityLogic_DemonKingRecallEgoSword`
- `AbilityLogic_DemonKingEgoSwordVerticalStrike`
- `AbilityLogic_DemonKingEgoSwordCrossLaser`
- `AbilityLogic_DemonKingWallBounceRush`
- `AbilityLogic_DemonKingGroggyRecoverCounter`
- `AbilityLogic_DemonKingFinalDesperation`

위험:

- merge conflict 가능성이 큽니다.
- ability 하나만 수정해도 전체 파일 context가 커집니다.
- boss phase/ability 영향 범위 추적이 어렵습니다.

권장:

- class name과 namespace를 유지하고 파일만 ability별로 분리합니다.
- ScriptableObject serialized type rename은 하지 않습니다.
- Unity import/compile 확인을 반드시 거칩니다.

### 9.2 Boss actor/controller 대형 클래스도 분리 후보

긴 파일 상위권에 다음이 포함됩니다.

- `DemonKingAbilityLogics.cs`
- `EgoSwordActor.cs`
- `DemonKingController.cs`
- `DemonKingPrimitiveVisual.cs`
- SlimeQueen 계열 boss 파일들

권장 분리 기준:

- phase state
- ability logic
- telegraph/presentation
- actor/projectile helper
- cleanup/lifecycle
- debug/editor visualization

## 10. Input / Settings Review

### 10.1 Key scan 캐싱 여지

`InputBindingService` 쪽 key read helper는 `Enum.GetValues(typeof(KeyCode))`를 호출하고 전체 key를 순회합니다.

위험:

- rebinding mode에서만 호출되면 문제는 작습니다.
- 매 프레임 호출되면 불필요한 allocation/iteration 비용이 생길 수 있습니다.

권장:

- `static readonly KeyCode[]`로 캐싱합니다.
- `KeyCode.None` 등 제외 대상은 한 번만 필터링합니다.

### 10.2 InputBindingService도 독립 service 생성 경로를 가짐

`InputBindingService.EnsureInstance()`는 기존 instance가 없으면 직접 `new GameObject(nameof(InputBindingService))`를 만듭니다.

권장:

- `[RuntimeServices]` root 아래로 소유권을 맞추는 것을 고려합니다.
- 다만 scene-authored input service가 있다면 교체/adopt 정책을 먼저 정해야 합니다.

## 11. Loading / Addressables Review

### 11.1 Addressables 실패 후 fallback asset을 쓰면서 operation은 error로 완료됨

`AddressableAssetProvider`는 addressables load 실패 시 `state.SourceAsset` fallback을 사용합니다. 하지만 최종 operation completion에는 error message를 넘깁니다.

결과:

- 실제 asset은 fallback으로 사용 가능할 수 있습니다.
- 하지만 operation은 실패로 표시될 수 있습니다.

정책 선택이 필요합니다.

1. Fallback success policy
   - fallback asset이 있으면 operation success
   - warning/debug event만 기록
2. Addressables strict failure policy
   - addressables 실패는 failure
   - fallback은 editor/dev-only 보조
3. Build config policy
   - development build와 release build에서 다르게 처리

현재 코드는 fallback success와 strict failure가 섞여 보입니다.

## 12. Test Coverage Review

현재 smoke test는 프로젝트 규모에 비해 좁고, 일부 scene name이 오래된 구조를 참조할 가능성이 있습니다.

우선 추가하면 좋은 테스트:

1. `GameDataManager_SaveData_DoesNotOverwriteItemUnlocks_WhenItemManagerNotReady`
2. `MouseCursorService_AuthoredInstance_ReplacesBootstrapInstance`
3. `AbilitySystem_CancelAllForSceneTransition_RemovesGrantedTagsAndStopsExecutions`
4. `SceneSmoke_AllBuildSettingsGameplayScenes_LoadCoreServices`
5. `AddressableAssetProvider_FallbackPolicy_IsConsistent`
6. `TagSystem_DoesNotExposeMutableInternalCounts`

## 13. Suggested Refactor Order

### Step 1 - Safety fixes first

- `GameDataManager.SaveData()` item unlock preservation.
- `MouseCursorService` early creation path.
- Unity object cleanup `?.` high-risk scan.
- `TagSystem.TagCount` external mutation check.

### Step 2 - Test before large refactor

- Save not-ready test.
- Mouse cursor authored replacement test.
- Build settings scene smoke test.
- Ability cleanup test.

### Step 3 - Behavior-preserving file splits

- Split `DemonKingAbilityLogics.cs` by ability class.
- Split `DialogueView` presentation responsibilities through helper classes or partial classes.
- Stabilize `AbilitySystem` event channel and persistent state coverage before deeper split.

### Step 4 - Service ownership cleanup

- Create service inventory.
- Classify app-scope, gameplay-session, scene-authored, fallback services.
- Move high-risk services to common ownership path first.

## 14. One-line Conclusion

현재 코드베이스의 핵심 리스크는 “기능 부족”이 아니라, 이미 많이 쌓인 기능을 지탱하는 service ownership, save readiness, scene cleanup, presentation fallback, 대형 클래스 구조를 얼마나 명확하게 정리하느냐입니다. 가장 먼저 save data 보존과 runtime service 생성 경로를 안정화하고, 그 다음 Boss/Dialogue/Ability 대형 파일을 behavior-preserving 방식으로 나누는 것이 안전합니다.
