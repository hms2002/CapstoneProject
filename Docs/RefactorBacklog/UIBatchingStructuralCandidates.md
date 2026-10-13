---
status: partially-refactored
authority: refactor-backlog
category: ui-rendering-performance
last_reviewed: 2026-10-08
---

# UI Batching Structural Candidates

이 문서는 현재 UI의 구조적 batch 경계와 조건부 개선을 추적한다. [활성 작업](../ActiveTasks/ui-batching-optimization.md)의 사용자 승인 범위에 포함된 후보이며, 현재 검증하지 않은 표현·정렬·수명 변경은 기존 구현을 유지한다. 개선 채택 기준은 같은 조건에서의 비용 감소와 외형·입력·lifecycle 동등성이다.

## Evidence And Boundaries

- 아래 화면 수치는 2026-10-07 `pilot2-optimized`의 실제 native UI batch membership/breaking reason 관측이다. Keybind 164, Settings 39, Inventory 16은 해당 화면·프레임의 batch 수이며 GameObject/Graphic 수, 전체 게임 FPS 또는 최종 A/B 개선율이 아니다.
- `DifferentMaterialInstance` 발생 횟수는 고유 material 개수가 아니다. Settings의 masked TMP는 동일 submitted material `-21206`, masked Image는 `-21212`를 반복한다. Keybind도 동일 masked TMP `-23328`/Image `-23334` 전환이 주된 형태다. instance ID는 해당 실행 안에서만 의미가 있다.
- `Canvas.BuildBatch`는 모든 활성 Canvas의 CPU 합계다. 선택한 화면의 전용 CPU 시간으로 해석하지 않는다. 관측 native vertex 값은 모두 0이었으므로 geometry가 없거나 Outline 비용이 0이라는 근거로 쓰지 않는다.
- clone hierarchy path가 반복되므로 path만으로 native membership과 Graphic inventory를 연결하면 비활성 clone을 중복 포함할 수 있다. raw membership/reason과 실제 활성·submitted 상태를 구분한다.
- 초기 HUD 비교는 locale/font 적용 시점이 달라 최종 A/B 근거에서 제외한다. package 초기화뿐 아니라 app의 저장 언어 적용 coroutine 완료, active `LocalizeTmpFontEvent` handle 성공/result-font 일치, LateUpdate 안정화를 기다리는 경로로 다시 비교한다.
- 관련 측정 경로: [native runner](../../Assets/_Project/Editor/Tools/Validation/UiBatchingValidationRunner.cs), [실제 화면 fixture](../../Assets/_Project/Editor/Tools/Validation/UiBatchingLiveScenarios.cs), [world HUD fixture](../../Assets/_Project/Editor/Tools/Validation/UiBatchingStructuralScenarios.cs). 날짜별 최종 결과는 [UI 구조 메모리](../StructureMemory/UIBatchingOptimization.md)와 SessionLog에서 추적한다.

## 1. Repeated Image/Text Draw Order

- **Current problem:** Keybind 164 batches = Material 전환 107 + Texture 전환 56 + 첫 batch 1. 같은 row의 배경 Image → TMP label → button → keyboard icon → button → icon → reset TMP 순서가 반복된다. Settings 39 = Material 30 + Texture 8 + 첫 batch 1이며, WindowMode/Resolution/volume rows에서 동일 TMP와 Image가 교차한다. label마다 material 107개/30개가 따로 생성된 현상이 아니다.
- **Why it exists:** 각 행이 label, 입력 버튼, 배경을 소유하는 authored subtree다. 설정 내용·rebind·navigation·layout을 행 단위로 다루기 쉬우며, 현재 UI depth 순서가 이 subtree를 따라간다.
- **Target shape:** 실제 겹침과 native membership을 확인한 범위에서 같은 Image/TMP 상태를 연속 제출할 수 있는 authored 순서 또는 view 분리를 평가한다. font/shader/stencil 상태가 다른 TMP와 Image를 material 하나로 합치지 않는다.
- **Risks:** 배경과 글자 겹침, highlight/reset 표시, 스크롤, 선택·키보드 navigation, raycast, layout ownership, 다섯 언어 폭·fallback atlas. child Canvas 추가는 독립 batch 경계를 만들 수 있다.
- **Refactor trigger:** 같은 locale/font 완료 상태와 선택/scroll 위치에서 membership 및 pixel 비교를 통과하고, 실제 batch 또는 rebuild 비용 감소가 확인될 때. 행 수 증가도 재평가 계기다.
- **Related files/docs:** [KeyMapRow.prefab](../../Assets/_Project/Prefabs/UI/KeyMapRow.prefab), [KeyBindingPanelUI](../../Assets/_Project/Runtime/UI/Settings/KeyBindingPanelUI.cs) `EnsureRows`(383–398), [KeyBindingRowUI](../../Assets/_Project/Runtime/UI/Settings/KeyBindingRowUI.cs), [SettingsPanelUI](../../Assets/_Project/Runtime/UI/Settings/SettingsPanelUI.cs), [GlobalUIRoot.prefab](../../Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab); [UI flow](../StructureMemory/UIFlowInputBlocking.md).
- **Status:** resolved in the approved 2026-10-08 slice. Same-Canvas authored Controls/Icons/Text layers preserve logical slots, controller/Button refs and hidden-owner activation. Final native Keybinding165→18, Settings39→20; all5 locales/3 panels passed76 zero-difference RGBA comparisons and visible input/navigation/lifetime checks. Row-root Selectables remain whole controls. [Evidence](../SessionLogs/2026-10-08-ui-batching-structural/README.md).

## 2. Rectangular Stencil Masks

- **Current problem:** Settings/Keybind의 `Background/ContentsMask`는 sprite 없는 사각 Image + `Mask`, `showMaskGraphic=false` 구조다. native records에 stencil writer, masked Image reader, masked TMP reader가 구분된다. 현재 Keybind 행은 모두 활성화되어 생성된다.
- **Why it exists:** 기존 scroll viewport가 stencil로 내용을 자른다. 이 클리핑 기능은 필요하며, batch 감소를 위해 무조건 제거할 대상이 아니다.
- **Target shape:** 실제 clip 모양이 사각형이고 변환 조건이 맞는 viewport에서 `RectMask2D`를 비교한다. viewport 밖 요소의 실제 culling, stencil writer 제거 및 독립 clip 경계 변화까지 측정한다. 사각형이 아닌 alpha mask는 유지한다.
- **Risks:** 열기·닫기 중 회전/scale, parent Canvas 공간, 경계의 부분 glyph/outline, scroll 끝, raycast, 중첩 mask, shader clip 지원. stencil variant가 사라져도 서로 다른 TMP/Image shader와 texture 경계는 남는다.
- **Refactor trigger:** 원본 mask와 대체 clip의 초기/중간/완료 motion, 모든 scroll 위치, 다섯 언어 경계 pixels와 입력 결과가 같고 native 비용 감소가 확인될 때.
- **Related files/docs:** [GlobalUIRoot.prefab](../../Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab) Settings `ContentsMask`(34442–34517), Keybind `ContentsMask`(47596–47668), [UIChainDropPresentation](../../Assets/_Project/Runtime/UI/Settings/UIChainDropPresentation.cs), [KeyBindingPanelUI](../../Assets/_Project/Runtime/UI/Settings/KeyBindingPanelUI.cs); [UI 구조 메모리](../StructureMemory/UIBatchingOptimization.md).
- **Status:** resolved for the two approved GlobalUIRoot viewports. HybridRectClipView uses RectMask2D only at axis alignment and retains the original stencil during rotation/skew/tilt. Original Image hit/no-hit behavior, scroll boundaries, outside culling,70 five-locale pose RGBA comparisons and cleanup passed with current layers. Menu batch gains above combine ordering/culling; their separate contributions are not isolated. Native vertex counters remain unavailable. Title's original stencil stays. [Evidence](../SessionLogs/2026-10-08-ui-batching-structural/README.md).

## 3. Monster HP Representation And Canvas Ownership

- **Current problem:** 각 몬스터의 nested `MonsterWorldHud`가 자체 World Space Canvas를 소유한다. HP border/track/red fill/white damage trail은 UI geometry이고, 상태 숫자/icon도 같은 HUD에서 projection한다. 정상 HP fixture의 실제 native 결과는 0/30/60마리에 0/30/60배치, HP 변경과 6개 실제 Electrocute 상태를 더한 30/60마리에 42/72배치다. 전부 화면 안에 있었고 MaxHP와 배치 위치를 보존했다. aggregate Canvas.BuildBatch는 약 0.12–0.14 ms였으며 몬스터 HUD만의 CPU나 GPU 개선을 뜻하지 않는다. native vertex 값 0은 unavailable로 처리했다.
- **Why it exists:** actor의 authored anchor, parent transform, disable/death/destroy 수명과 HUD를 함께 관리한다. `MonsterStackStatusWorldView`가 parent scale/rotation을 보정하고, HP 감소·hold·trail interpolation 및 상태 pulse를 표시한다.
- **Target shape:** 기존 actor owner 아래 shared quad/material HP 경로를 유지하며 status는 기존 Canvas에 남긴다. 교차 HUD bounds가 겹치면 전체 visible HUD가 retained Images/Canvas로 돌아가 기존 투명 정렬을 보존한다. 겹친 상태도 줄이려면 같은 cross-actor draw order/raster/lifecycle을 보존하는 별도 설계와 측정이 필요하다. 새 Manager/Singleton/DDOL은 전제로 하지 않는다.
- **Risks:** Sprite/UI raster 및 pixel snap, parent flip/scale, sorting layer/order와 투명 겹침, 카메라 culling, HP width/ratio, damage trail의 0.3초 hold/0.5초 interpolation, 상태 compact/pulse, actor disable/re-enable/death/scene unload. shared Canvas는 actor-child 수명과 정렬을 바꿀 수 있다.
- **Refactor trigger:** 정상 HP의 0/30/60 fixture에서 실제 visible HUD/분산 위치/같은 상태 갱신을 검증한 뒤, 비용과 raster/sorting/lifecycle 동등성 비교가 완료될 때. 동일 표현을 확보하지 못하면 기존 Canvas를 유지한다.
- **Related files/docs:** [MonsterWorldHud.prefab](../../Assets/_Project/Prefabs/UI/MonsterWorldHud.prefab) World Space Canvas/sorting(1036–1049), [MonsterStackStatusWorldView](../../Assets/_Project/Runtime/UI/Combat/StackStatus/MonsterStackStatusWorldView.cs), [MonsterSizeProfile](../../Assets/_Project/Runtime/Features/Monsters/Common/MonsterSizeProfile.cs), [MonsterStatusRuntime](../../Assets/_Project/Runtime/Features/Combat/Status/MonsterStatusRuntime.cs); [world HUD 구조](../StructureMemory/MonsterWorldHudAndStatus.md), [Mob cleanup 계약](../Contracts/MobCleanupContract.md).
- **Status:** partially-refactored. 2026-10-08 shared HP mesh와 전체 actor overlap fallback을 적용했다. 분산된 정상 actor 30/60의 native world UI batches는30/60→0/0, six-status cases는42/72→12/12; 동일 live actors의 calibrated total GameView draws는89→60 /149→90이었다. Total은 모든 camera/UI/world의 값이며 isolated HP GPU 시간이나 FPS가 아니다. 기존 Images/Canvas는 dense overlap 정렬을 위해 유지한다. [최종 조건/검증](../SessionLogs/2026-10-08-ui-batching-structural/README.md).

## 4. Idle Skill Shader Boundary

- **Current problem:** 대표 HUD의 스킬 icon 4개는 이미 같은 immutable `UI_ReadyFlash_SharedIdle` material을 쓴다. 그래도 `UI/Icon Ready Flash`와 주변 Default UI frame/key/cooldown의 순서 및 icon texture 차이로 native batch가 분리된다. material 공유만으로 서로 떨어진 icon draw가 하나가 되지는 않는다.
- **Why it exists:** zero-flash shader의 반투명 alpha 출력이 UI/Default와 다르다. 현재 shared idle shader 유지가 외형 보존 경계이며, active flash는 slot-owned material을 재사용한다.
- **Target shape:** 같은 RGBA/clip/stencil 출력을 유지하는 경로 안에서 icon/주변 graphic 순서와 compatible sprite texture를 평가한다. UI/Default로 단순 교체해 alpha 의미를 바꾸지 않는다. 외부 writer가 shared material을 변형하지 않도록 기존 lease ownership을 유지한다.
- **Risks:** 반투명 pixels, vertex tint, mask/alpha clip, 활성 flash 시작·종료, Sprite sampling/atlas UV, disable/re-enable와 원래 material 복원, 두 HUD owner의 공유/정리.
- **Refactor trigger:** idle 및 active flash RGBA/stencil/lifecycle 비교가 통과하고 실제 native 경계 또는 CPU/material 비용의 추가 감소가 확인될 때.
- **Related files/docs:** [WeaponSkillHudSlotPresenter](../../Assets/_Project/Runtime/UI/HUD/WeaponSkillHudSlotPresenter.cs), [UIIconReadyFlash.shader](../../Assets/_Project/Art/Shaders/UIIconReadyFlash.shader) alpha/clip(93–108), [Skill1UI.prefab](../../Assets/_Project/Prefabs/UI/WeaponHUDUI/Skill1UI.prefab); [UI 구조 메모리](../StructureMemory/UIBatchingOptimization.md).
- **Status:** proposed. 검증된 shared idle/active-instance 구조와 ready shader를 유지한다.

## 5. Inventory Chrome And Builtin Slider Textures

- **Current problem:** Inventory native 16 중 Texture 전환이 10이다. 기존 Inventory Sprites sheet, DropZone, TrashCanIcon, InventoryBox 9-sliced 및 실제 weapon icon이 섞인다. Settings Texture 전환 8 중 6은 세 slider의 builtin Background → UISprite fill → Knob 전환이며, menu 3-Sprite atlas만으로 해결되지 않는다.
- **Why it exists:** UI chrome과 입력 control이 각각의 Sprite/sliced 표현을 사용한다. inventory item 종류는 계속 바뀌며, built-in control texture는 일반 프로젝트 Sprite atlas packable과 별개다.
- **Target shape:** 기본 UI shader를 사용하는 inventory chrome만 명시적 Sprite whitelist로 묶어 실제 membership/page 수·메모리를 비교한다. builtin slider는 현재 표현을 보존한 authored equivalent가 확인되는 범위에서 별도 평가한다. item 전체 폴더나 UV-dependent shader texture를 일괄 패킹하지 않는다.
- **Risks:** sliced border/PPU/pivot/filter/alpha, drag/drop/hover/highlight, 다섯 언어 layout, atlas page 수와 메모리, world와 Sprite를 공유하는 item의 shader UV, Addressables 중복 texture 포함.
- **Refactor trigger:** 같은 inventory 내용과 state에서 실제 texture boundary를 줄이고 geometry/pixel/input 검증 및 atlas memory/build 검증이 통과할 때.
- **Related files/docs:** [GlobalUIRoot.prefab](../../Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab), [InventoryScreen](../../Assets/_Project/Runtime/UI/Inventory/Common/InventoryScreen.cs), [ItemSlotUI](../../Assets/_Project/Runtime/UI/Inventory/Common/ItemSlotUI.cs), [SettingsPanelUI](../../Assets/_Project/Runtime/UI/Settings/SettingsPanelUI.cs); [inventory/chest 구조](../StructureMemory/ScriptSystems/InventoryAndChestUIStructure.md), [UI 구조 메모리](../StructureMemory/UIBatchingOptimization.md).
- **Status:** proposed. 현재 item texture/control 표현을 유지한다. 카드/메뉴 atlas의 검증 결과를 이 후보의 성능 증거로 대체하지 않는다.

## 6. UI Particle Aggregation

- **Current problem:** emitter가 particle/trail별 RawImage pool을 사용한다. 이번 steady screen snapshots에는 활성 UI particle batch membership이 없었다. pool의 capacity/비활성 객체 수를 현재 draw 수로 계산할 수 없다.
- **Why it exists:** 각 particle의 위치·회전·색·alpha·trail·lifetime과 Local/World 공간을 개별 Graphic으로 표현한다. 짧은 one-shot은 warmup 뒤 이미 종료될 수 있다.
- **Target shape:** 실제 authored emitter의 고정 seed, 발생 시점과 같은 particle age를 맞춘 burst/sustain/trail fixture에서 하나의 mesh 또는 호환되는 aggregate Graphic을 비교한다. 기존 emitter가 timing/ownership/cleanup을 계속 소유한다.
- **Risks:** 투명 draw 순서와 겹침, texture/material/clip 차이, trail 배열 순서, Random 호출/phase, Local/World 좌표, play/stop/clear, 비활성화·destroy·scene unload 및 pool 재사용. 화면 밖 tick 중단은 재등장 phase를 바꿀 수 있다.
- **Refactor trigger:** 활성 burst/trail의 실제 native batch·mesh/layout 비용이 병목으로 확인되고 같은 age의 pixel 및 전체 lifecycle 비교가 통과할 때.
- **Related files/docs:** [UIParticleEmitter](../../Assets/_Project/Runtime/UI/Common/UIParticleEmitter.cs) pool/RawImage 생성(625–704), [native runner](../../Assets/_Project/Editor/Tools/Validation/UiBatchingValidationRunner.cs); [Presentation authoring 계약](../Contracts/PresentationAuthoringContract.md), [측정 구조](../StructureMemory/FramePerformanceMeasurement.md).
- **Status:** proposed. 현재 emitter와 pool 수명을 유지한다. steady 활성 particle 관측 0을 효과 전체의 rendering 비용 0으로 일반화하지 않는다.

## Related Documents

- [Structural UI batching scope](../ActiveTasks/ui-batching-structural-optimization.md)
- [UI batching ownership/map](../StructureMemory/UIBatchingOptimization.md)
- [Frame measurement scope/limitations](../StructureMemory/FramePerformanceMeasurement.md)
- [Localization flow](../StructureMemory/LocalizationFlow.md)
- [Presentation authoring contract](../Contracts/PresentationAuthoringContract.md)
- [Runtime service ownership architecture](../Architecture/RuntimeServiceOwnershipArchitecture.md)

Overall status: **partially-refactored**. Render ordering and the approved rectangular viewports are resolved; shared HP is conditional, with dense overlap retaining the original Images/Canvas sorting and cost (fixed60-actor nativeUI60, total148→148). Shader, inventory texture and particle candidates remain proposed. Revisit overlap rendering only with a verified ordered/raster-equivalent design. Architecture/Contracts and Presentation HTML are unchanged.
