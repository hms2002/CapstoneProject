---
status: active
authority: structure-memory
category: ui-encyclopedia
last_reviewed: 2026-11-01
---

# Encyclopedia Creature Tabs

> Tool retention update (2026-11-01): completed task-specific skin/layout/migration authoring and acceptance tools named below are historical and have been moved outside Assets. See [current retained tools and archive](TestAndEditorToolRetention.md). Gameplay/save regressions and reusable profiling remain. Automated tests run only when explicitly requested.


## Purpose

몬스터·보스·NPC 도감 작업을 다시 시작할 때 필요한 현재 데이터, UI 및 처치 저장 경계의 빠른 지도다. [현재 작업 범위](../ActiveTasks/encyclopedia-creature-tabs.md)와 [기존 Item 구조](ScriptSystems/EncyclopediaStructure.md)를 함께 참고한다. Architecture/Contracts를 대체하지 않는다.

## Current Structure

| 책임 | 현재 진입점 |
| --- | --- |
| 팝업 스택, main-tab 전환, 책 연출, 전체 보기 우선 닫기 | [EncyclopediaScreen.cs](../../Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaScreen.cs) |
| Monster/Boss/NPC 페이지·선택 상태와 데이터 바인딩 | [EncyclopediaCreatureTab.cs](../../Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaCreatureTab.cs) |
| 몬스터 preview/본문, 캐릭터 표정·호감도·보상·스토리·전체 보기 | [EncyclopediaCreatureDetailView.cs](../../Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaCreatureDetailView.cs) |
| authored 목록 카드와 보상 행 표시 | [EncyclopediaCharacterEntryView.cs](../../Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaCharacterEntryView.cs), [EncyclopediaAffectionRewardView.cs](../../Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaAffectionRewardView.cs) |
| 기본 표정 및 최대 5개 dot 창, 원본 비율 갱신 | [EncyclopediaPortraitUtility.cs](../../Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaPortraitUtility.cs) |
| 표시 콘텐츠, 기존 NPCData/원본 Sprite 참조, Idle 프레임 | [EncyclopediaCatalogSO.cs](../../Assets/_Project/Runtime/Features/Encyclopedia/EncyclopediaCatalogSO.cs), [Catalog](../../Assets/_Project/Data/Encyclopedia/EncyclopediaCatalog.asset) |
| 원본 자산 읽기, catalog/종 identity 연결 | [EncyclopediaCreatureDataAuthoring.cs](../../Assets/_Project/Editor/Tools/Encyclopedia/EncyclopediaCreatureDataAuthoring.cs) |
| creature subtree, 고정 슬롯·보상 행·전체 보기 authoring | [EncyclopediaCreatureUIAuthoring.cs](../../Assets/_Project/Editor/Tools/Encyclopedia/EncyclopediaCreatureUIAuthoring.cs), [EncyclopediaUI.prefab](../../Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab) |

Item의 Weapon/Relic/Consumable source, 설명 전환 및 페이지 presenter는 기존 경로를 유지한다. `EncyclopediaCreatureTab`은 별도 authored LeftPage/RightPage를 표시한다. 새 Manager/Singleton/DDOL이나 runtime fallback hierarchy는 없다.

Item 상세 표시 경계는 [EncyclopediaItemRightPage.cs](../../Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaItemRightPage.cs)의 serialized `pageContentRoot`이며 authored `ItemPageContent`를 가리킨다. 이 경계는 기존 `ItemDetailPanel` ScrollRect, 페이지 하단의 고정 `WeaponDescriptionHint`, 중앙 `UndiscoveredItem`을 함께 포함한다. 기존 `contentRoot`는 내부 능력/효과 projection 경계로 유지한다. presenter가 붙은 `RightPage`에는 주 탭 책갈피도 있어 전체 GameObject를 숨기지 않는다. `EncyclopediaItemTab`이 양쪽 presenter에 visibility와 interaction lock을 전달하고, `EncyclopediaScreen`이 주 탭 전환/닫기 때 이를 조정한다. 숨김은 scroll 이동·예약 reset·glossary를 정리하고 Update 입력을 차단한다. 숨김 중 locale 변경은 pending으로 기록해 Item 복귀 시 현재 선택의 상세를 갱신한다.

Book 자식 표시 순서는 Item LeftPage/RightPage → CreatureLeftPage/CreatureRightPage → PageCover → CreatureFullIllustration이다. 실제 Reveal은 `EncyclopediaBookPresentation`의 기존 PageCover clip이 소유한다. `WireTabVisibilityAndReveal()`가 전체 Item root와 이 순서를 authoring하고 `AlignTextAndPadding()` 마지막에서도 적용한다. 기존 Content Appear/Style1의1~18 PNG는 평평한 책 상단에 맞게 원래 둥근 투명 틈만 메웠으며 reveal hole과 clip 시간은 유지한다.19번 마지막 sprite에는 원래15 opaque pixel이 남지만 routine 종료가 Cover 자체를 숨긴다. [승인 범위](../ActiveTasks/encyclopedia-tab-visibility-reveal.md).

## Current Content And Presentation

- 책 배경은 [Inventory Book SpriteSheet.png](<../../Assets/_Project/Art/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites/Inventory Book SpriteSheet.png>)와 같은 폴더의 Book.controller/Book*.anim이 소유한다. 816×720 slice8이 펼쳐진 기본 책이며 상단 외곽과 안쪽 테두리는 수평이다. 페이지 넘김의 고정 배경 부분도 같은 상단을 사용하고 움직이는 leaf의 원근/하단은 유지한다. 별도 Book Idle/1.png보다 실제 controller의 이 시트 참조가 현재 화면의 기준이다. 아트 단계에서는 PNG 픽셀만 바꾸고 기존 sprite IDs/rect/pivot/metadata와 UI geometry를 유지했다. [아트 검증 및 native 화면](../../outputs/encyclopedia-book-flat-top-2026-10-11/README.md).
- 페이지 제목/일러스트 geometry는 `EncyclopediaCreatureUIAuthoring.AlignBookContentInsets()`가 소유한다. 제목은600폭 페이지 안에25 UI unit inset을 사용하며 creature 왼쪽 제목과 MonsterName은550×52/중앙Y−51이다. 최신 캐릭터 Illustration은 책 갈색 선 바로 안쪽으로594×389/상단Y−7이며 하단396을 유지한다. native1920기준 책 선의 안쪽 여백은 계산상 좌1.51/우1.82/위1.64px이다. Monster preview의 폭550과 Item 왼쪽 TitleGroup 상단25는 유지한다. 이름 overlay, 목록, 호감도·보상·스토리/스크롤 geometry는 독립적이다. FitInParent로 원본 비율과 얼굴을 보존하며 그림을 늘여서 프레임을 채우는 변경은 아니다. 기존 AlignTextAndPadding 마지막에 적용하므로 Build/Polish 뒤에도 유지된다. [현재 프레임·검증 제한](../../outputs/encyclopedia-book-illustration-frame-2026-10-11/README.md), [이전25-unit 배치 캡처](../../outputs/encyclopedia-book-inner-padding-2026-10-11/README.md).
- 현재 authoring 검증은 catalog의 몬스터 14종과 보스 4명, 현재 사용 NPC 5명 및 원본 portrait 27장을 확인했다. 몬스터 목록은 12개 authored 슬롯으로 페이지를 나누고, 보스/NPC 목록은 2x2 슬롯이다.
- 보스 원본 표정: 멜타 7장, 클로에 3장, 스피리 6장, 마왕 6장. NPC 사샤·벨카·서기관·파셀·버피는 각각 1장이다. `NPCData.spriteLibraryAsset`의 `Face`를 읽고 같은 Sprite 별칭을 중복 제거한다. 스피리는 현재 `Spiri_` 표정만 가져오며 library에 남아 있는 이전 Dragon 이미지는 포함하지 않는다.
- 목록 portrait는 원본 Sprite를 `RectMask2D`와 bottom-pivot `AspectRatioFitter.EnvelopeParent`로 프레임 안에 채운다. 보스/NPC 우측 상세 portrait는 얼굴이 잘리지 않도록 `FitInParent`로 원본 비율을 프레임 안에 맞추고 하단 정렬을 유지한다. 원본 이미지 자체는 수정하지 않는다. 보스 목록/상세에만 검정→투명 gradient를 표시한다. 몬스터/NPC는 이를 숨기며 이름 패널을 추가하지 않는다. 미조우 카드의 이름은 `???`, 이미지는 검정 tint이며 호감도 하트도 숨긴다. 목록에는 dot이 없다.
- 기본 표정은 `Idle` 또는 `_Idle`, 그다음 `Normal`, 그다음 첫 유효 Sprite다. 실제 등록 표정 수 전체를 탐색하며 wrap하지 않는다. 이동 불가능한 방향의 화살표는 GameObject를 비활성화해 숨긴다. 입력 잠금 중에는 양쪽을 숨기고, 잠금 해제/표정 변경 시 가능한 방향만 다시 표시한다.
- Detail dot은 `min(5, portraitCount)`개다. 창 시작은 `clamp(selected - visibleCount / 2, 0, count - visibleCount)`이고 `(slot - (visibleCount - 1) / 2f) * spacing`으로 짝수 개도 중앙 정렬한다. 모든 원본 표정은 계속 탐색할 수 있다. 1장은 중앙 dot 하나이며 양쪽 화살표는 숨겨진다.
- 전체 보기는 `CreatureRightPage`와 같은 RectTransform 경계에서 책의 오른쪽 페이지 전체를 사용한다. 별도 종이 패널과 중복 제목은 표시하지 않고, `CharacterDetail`을 숨긴 뒤 현재 표정의 원본 비율을 유지한 uncropped Sprite를 확대한다. 그림 여백은 좌우/상단8, 하단72 UI unit이며 화살표·장수·닫기는 하단에 배치한다. 좌우 화살표와 장수는 상세의 단일 `portraitIndex`를 사용하며 목록/기존 상세 이미지와 동기화된다. 끝 방향과 입력 잠금 중 화살표는 숨긴다. 닫으면 마지막 표정과 기존 스크롤 위치를 유지해 상세로 복귀하고, 확대 중 locale/호감도 갱신은 상세를 다시 노출하지 않는다. Escape 첫 요청은 확대 보기를 닫고 다음 요청이 도감을 닫는다. 항목 변경/탭 숨김/disable도 확대 보기를 정리한다.
- `EncyclopediaCreatureUIAuthoring.PolishCreatureControls()`는 creature 목록의 종수 footer를 비활성화하고 전체 보기 버튼을 일러스트 프레임 우측 하단에 anchor/pivot `(1,0)`, 위치 `(-1,1)`로 배치한다. 여백은 기존 Canvas 기준 1 UI unit이며 화면 배율에 따라 실제 픽셀 수는 달라진다. `AlignTextAndPadding()`의 마지막에 적용하므로 Build/Polish 후에도 유지된다.
- 몬스터 미리보기는 기존 resting/Idle animation에서 editor가 추출한 Sprite 배열을 `Time.unscaledDeltaTime`으로 순환한다. 도감의 time freeze 동안도 움직이며 전투 프리팹·FSM·Animation Event를 실행하지 않는다.
- 호감도는 `AffectionManager.GetAffection(npcId)`에서 읽으며 manager가 없을 때 현재 프로필 record를 조회한다. UI는 변경 이벤트와 profile/locale 변경에 맞춰 다시 표시하고 gameplay 값을 변경하지 않는다.
- 보상은 `NPCData.affectionRewards`의 level, `AffectionEffect.RewardText`/icon과 현재 해금 여부만 표시한다. `Execute()`를 호출하거나 수령 버튼을 추가하지 않는다. 빈 보상 목록 및 빈 스토리는 명시적으로 미등록/설명 준비 상태를 표시한다. 새 보상이나 스토리 내용은 작성하지 않았다.
- 목록/일러스트 테두리, 버튼, 구분선, 보상 패널과 scrollbar는 승인된 `Art/Sprites/UI/Encyclopedia/PixelSkin` native 에셋을 사용하는 authored Image다. 보스 외곽 프레임은 gradient 영역과 일치하고 모서리 문양이 없다. 책 안쪽 테두리 모서리 문양도 원본 sheet의 고정 패턴에서 제거했다. 장식 Image는 raycast를 받지 않는다.
- 캐릭터 보상/스토리의 `ScrollRect`는 authored 세로 `Scrollbar`를 `AutoHide`로 연결한다. 기존 우측 여백에 놓아 표시 여부에 따라 viewport 폭이나 줄바꿈이 달라지지 않는다. 내용이 짧아지면 native ScrollRect가 스크롤바를 숨기며, 항목 변경은 기존 `SettleCharacterLayout`에서 최상단으로 초기화한다.
- 긴 보상 설명의 높이는 `EncyclopediaAffectionRewardView.ILayoutElement`가 TMP preferred height와 설명 RectTransform의 위/아래 여백을 합쳐 제공한다. authored `LayoutElement.minHeight`가 최소 행 높이를 소유한다. 설명 RectTransform은 행 높이에 stretch되어 본문과 다음 행이 겹치지 않으며, 외부 게임 데이터나 UI hierarchy를 런타임에 생성하지 않는다.
- 텍스트/여백 최종 authoring은 `EncyclopediaCreatureUIAuthoring.AlignTextAndPadding()`이다. 보상 제목과 본문은 공통 시작선, 해금 상태는 별도 우측 열을 사용하고 헤더에는 상단 여백이 있다. 콘텐츠 좌우 inset과 호감도/설명 제목 시작선을 맞추며, 호감도 글씨와 하트 및 페이지 footer를 세로 중앙에 정렬한다. 중복 `RewardsRule`은 숨기고 호감도 아래 한 구분선과 StoryRule을 유지한다. Build/Polish도 마지막에 이 authoring을 적용한다. 원본 이미지 크기, Fit/cover, 게임 데이터 및 Item 경로는 변경하지 않는다.

## Approved Skin And Discovery Ownership (2026-11-01)

- 현재 최종 스킨 authoring은 [EncyclopediaSkinAuthoring.cs](../../Assets/_Project/Editor/Tools/Encyclopedia/EncyclopediaSkinAuthoring.cs)다. 기존 creature builder를 다시 실행했다면 이 도구를 마지막에 적용한다. 이전 geometry 기록은 기본 layout의 배경이며 최신 스킨이 이를 보완한다.
- 보스/NPC `Illustration` 외곽 크기는 유지하고 내부 `PortraitInset`에 좌우10/상단60/하단12 UI unit 여백을 둔다. 원본 비율 FitInParent를 유지하고 제목24pt와 왕관/얼굴의 겹침을 피한다. 전체 보기 버튼은54×54, 우하단3-unit inset이며 네 방향 화살표 icon40×40을 사용한다.
- [EncyclopediaDiscoveryProgress.cs](../../Assets/_Project/Runtime/Core/Save/EncyclopediaDiscoveryProgress.cs)의 additive v1 DTO가 실제 획득 Weapon/Relic ID와 실제 조우 Monster ID를 저장한다. `GameData.encyclopediaDiscoveryData`는 profile 확정분, `GamePlayData.pendingEncyclopediaDiscoveryData`는 현재 run 대기분이다. UI는 둘의 합을 읽는다. itemData spawn-unlock ID는 획득 근거가 아니다.
- `WeaponInventory2D.NotifyInventoryChanged`와 `RelicInventory.NotifyInventoryChanged`는 성공한 슬롯 갱신/복원 뒤 현재 소유 아이템을 기록한다. 런 중에는 pending만 변경하며, 허브 획득은 profile을 바꾸고 기존 deferred-save 요청 경로를 사용한다. Test 씬 획득은 제외한다.
- `MonsterSpawnRoomGroup`은 플레이어가 방에 들어왔을 때 살아 있는 등록 종과 이후 실제 스폰을 기록한다. preplaced/보스 소환은 `MonsterEncyclopediaIdentity`의 최초 Mob 인식으로 보완한다. 단순 스폰만으로는 공개하지 않고 active run을 요구하며 Tutorial/Test 조우는 제외한다.
- `RunSessionProgressCommitPolicy`가 기존 런 종료 정책에서 pending을 병합/초기화한다. `GamePlayDataManager.HandleProfileLoaded`도 초기화한다. 새 Manager/DDOL은 없다. 새 프로필/구형 null DTO는 안전하게 정규화하고 유효한 미등록 ID도 보존한다.
- 기존 저장의 몬스터 처치 수는 조우 근거로 인정한다. 보스는 기존 `bossDialogueData`의 encounter/victory/defeat count를 사용한다. 기존 저장에 없던 아이템 획득 이력은 추정하지 않으며 실제 획득/인벤토리 복원으로 기록한다.
- 미발견 실루엣과 `???`는 왼쪽 목록에만 표시한다. 오른쪽 Creature `UndiscoveredDetail`과 Item `UndiscoveredItem`은 페이지 중앙에 종류별 미조우/미획득 문구 하나만 표시한다. 본문/제목/처치 수/호감도/보상/표정/전체 보기/단축키를 숨기고 이전 portrait 목록도 비운다. Item은 전체 ScrollRect를 숨겨 빈 제목 테두리나 내용 배경이 남지 않게 한다. `EncyclopediaLayoutAuthoring.Apply`가 기본 builder/skin 작업 뒤 이 배치와 참조를 authoring한다. [레이아웃 검증 자료](../../outputs/encyclopedia-layout-2026-11-01/README.md).
- discovery/profile/locale 변경 시 현재 선택을 다시 투영해 이전 공개 정보가 남지 않는다. NPC/Consumable의 공개 정책은 변경하지 않는다.
- 검증 자료: [통합 기록](../../outputs/encyclopedia-skin-discovery-2026-11-01/README.md), [승인 범위](../ActiveTasks/encyclopedia-skin-discovery.md). 저장 소유권/공개 정책은 Architecture 또는 Contracts 승격 후보이며 이 작업에서는 해당 문서를 수정하지 않는다.

## Profile Kill Ownership

- [MonsterEncyclopediaIdentity.cs](../../Assets/_Project/Runtime/Features/Encyclopedia/MonsterEncyclopediaIdentity.cs)의 serialized `monsterId`는 catalog ID와 동일한 영속 키다. 기존 실제 prefab에 붙으므로 그 prefab을 사용하는 일반 스폰·보스 소환·슬라임 분열 개체도 자신의 종 ID를 갖는다. UI 표시 이름으로 종을 구분하지 않는다.
- [MonsterEncyclopediaProgress.cs](../../Assets/_Project/Runtime/Features/Encyclopedia/MonsterEncyclopediaProgress.cs)는 `Enemy.AnyDeathStarted`를 한 번 등록하고 `Mob + identity + active run`만 기록한다. `Enemy.Die()`의 기존 `isDead` guard 이후 이벤트가 한 번 발행된다. 최종 피해 기여자를 전달하지 않아 환경사망도 포함한다.
- Tutorial/Test가 포함된 씬 이름은 기능-local 집계 제외 대상이다. Editor 직접 시작은 이들 씬에도 active run을 만들 수 있으므로 active flag만으로 제외되지 않는다. TestScene의 임시 backend는 `IMonsterEncyclopediaRunProgressBackend`를 구현하지 않아 추가로 기록이 차단된다.
- [GameData.cs](../../Assets/_Project/Runtime/Core/Save/GameData.cs)의 `monsterEncyclopediaData`가 커밋된 프로필 값을 소유한다. [GamePlayData.cs](../../Assets/_Project/Runtime/Core/Save/GamePlayData.cs)의 `pendingMonsterEncyclopediaData`는 현재 런 동안만 유지된다. [MonsterEncyclopediaRunProgress.cs](../../Assets/_Project/Runtime/Core/Save/MonsterEncyclopediaRunProgress.cs)가 누적·커밋·초기화를 처리한다.
- Gameplay는 [RunSessionStore.cs](../../Assets/_Project/Runtime/Core/Save/RunSessionStore.cs)와 `GameDataStore.Data`를 사용하며 Infrastructure manager에 직접 의존하지 않는다. `GetKillCount(id)`는 커밋된 값과 현재 런 대기분의 합을 projection한다.
- [RunSessionProgressCommitPolicy.cs](../../Assets/_Project/Runtime/Infrastructure/Save/RunSessionProgressCommitPolicy.cs)가 기존 active-run 종료 경계에서 대기분을 현재 프로필에 합친 뒤 비운다. 기존 active-run quit의 save skip은 유지된다. 시작/개발 reset은 대기분을 비우고 [GamePlayDataManager.cs](../../Assets/_Project/Runtime/Infrastructure/Save/GamePlayDataManager.cs)의 profile-loaded 이벤트도 이전 프로필 대기분을 폐기한다.
- [GameDataManager.cs](../../Assets/_Project/Runtime/Infrastructure/Save/GameDataManager.cs)는 기존 세이브의 누락 DTO/list를 보완하고 잘못된/중복 record를 정규화한다. 이전에 기록하지 않은 처치 수는 0이며 과거 기록을 추정하지 않는다. 종 ID별 count는 음수가 되지 않고 `int.MaxValue`에서 포화한다.

## Extension Entry Points And Pitfalls

- 추가 몬스터는 stable ID, 실제 prefab identity 및 preview 프레임을 함께 authoring한다. catalog 표시 등록만으로 사망 집계가 연결되지는 않는다. ID 변경은 기존 프로필 호환성을 고려해야 한다.
- 새로운 캐릭터 표정은 `NPCData.spriteLibraryAsset`의 원본 참조를 등록한 뒤 catalog authoring을 다시 실행한다. 5장처럼 고정 상한으로 자르지 않는다.
- Reward slot 수는 authored prefab 용량이다. 새 reward row UI를 런타임에 임의 생성하는 경로는 없다.
- Empty NPC story는 등록 누락 상태다. Ink 대사 전체를 도감 스토리로 자동 복사하거나 임의 내용을 만들어 보완하지 않는다.
- 도감 캐릭터 이미지는 실제 게임 자산이다. 검토 HTML에 쓰였던 생성 그림이나 샘플 수치를 production source로 사용하지 않는다.
- 원본 Idle이 1프레임인 일부 몬스터는 미리보기도 정적이다. 원본 resting animation의 재생이며 임의 움직임을 덧붙이지 않는다.
- identity는 표시/진행도 metadata이며 `RequireComponent(Mob)` 체인을 강제하지 않는다. 기존 legacy prefab에 metadata를 붙일 때 Unity가 누락된 전투 dependency까지 추가하는 부작용을 피하기 위한 경계다.

## Verification And Related Sources

- 실제 Item/Reveal 통합 후속 검증: Unity6000.4.2f1 authoring/import, Editor 및 PlayMode 테스트 프로젝트 MSBuild exit0, [최종 PlayMode26/26](../../outputs/encyclopedia-tab-visibility-reveal-2026-10-12/playmode-final-results.xml), 보존23/23. 실제 Item presenter를 유지한 Item→Boss/Monster/NPC→Item, 같은 탭 재선택, 연출 중 닫기/다시 열기, 숨김 단축키/스크롤/언어 갱신을 검증했다. native early/mid/final sample과 실제 post-reveal 캡처는 authored font binding 로딩/적용을 기다렸고 마지막 실제 종료에서 Cover 비활성을 확인했다. 현재 활성 언어는 zh-Hans이며 캡처도 해당 언어를 표시한다. [보존·캡처·검증 제한](../../outputs/encyclopedia-tab-visibility-reveal-2026-10-12/README.md). 이전 isolated fixture의 Item 제거/사전 숨김 경로는 실제 Item 내용 제거를 검증한 것으로 해석하지 않는다.
- 컨트롤 후속 검증: Unity6000.4.2f1 authoring/import, Editor와 최종 PlayMode 테스트 프로젝트 MSBuild exit0, [PlayMode21/21](../../outputs/encyclopedia-controls-polish-2026-10-11/playmode-results.xml). 첫/마지막 표정·입력 잠금/복구·한 장짜리 NPC·빈 portrait의 화살표 비활성 및 복원을 포함한다. Count/FullView 두 레코드만 변경하고 2583개 ID/hierarchy·폰트/원본 이미지/Fit·보호 파일6개를 유지했다. 몬스터/보스/NPC 실제 캡처3장과 저장10개 hash 보존은 [후속 자료](../../outputs/encyclopedia-controls-polish-2026-10-11/README.md)에 기록한다.
- 글씨 정렬/여백 후속 검증: persistent prefab의 2583개 record/ID/hierarchy 중 typography/layout 85개만 변경하고 나머지 2498개 record와 원본 sprite/font 참조/Fit을 보존했다. 최종 Unity6000.4.2f1 authoring/import, Editor MSBuild exit0 및 [PlayMode21/21](../../outputs/encyclopedia-text-spacing-2026-10-11/playmode-final-results.xml). [실제 3개 탭 캡처·보존 기록](../../outputs/encyclopedia-text-spacing-2026-10-11/)을 확인했다. 여백 값을 복제하는 신규 테스트를 만들지 않고 기존 긴/짧은 내용 및 입력 회귀를 사용했다.
- 테두리/스크롤바 후속 검증: Unity6000.4.2f1 authoring/import, Editor 및 PlayMode 테스트 프로젝트 MSBuild exit0, [PlayMode21/21](../../outputs/encyclopedia-lines-scrollbar-2026-10-11/playmode-results.xml). 긴/짧은 스토리와 보상 전환의 AutoHide, 고정 viewport 폭, 실제 스크롤 입력과 동적 행 높이를 포함한다. [후속 검증 자료](../../outputs/encyclopedia-lines-scrollbar-2026-10-11/)에 실제 기본 3개 탭 native 캡처와 serialized/저장 보존을 기록한다.
- 검증 상태: Unity 6000.4.2f1 compile/import·final authoring/polish, MSBuild exit0, [EditMode8/8](../../outputs/encyclopedia-implementation-2026-10-11/editmode-results.xml), [PlayMode19/19](../../outputs/encyclopedia-implementation-2026-10-11/playmode-final-results.xml) 통과. 실제 일시정지 book Animator 탭 전환과 Item page 비활성/입력 복구를 포함한다. 1920x1080 Monster/Boss/NPC native PNG 3장을 시각 확인했다. 정확한 결과와 보존 범위는 [2026-10-11 SessionLog](../SessionLogs/2026-10-11.md)와 [검증 자료 폴더](../../outputs/encyclopedia-implementation-2026-10-11/)에 기록한다.
- [EncyclopediaProgressEditModeTests.cs](../../Assets/_Project/Tests/EditMode/EncyclopediaProgressEditModeTests.cs): 구형 JSON, round-trip, 종별 분리, normalization, run pending/commit, inactive guard, discard, null 필드, overflow.
- [EncyclopediaCreaturePlayModeTests.cs](../../Assets/_Project/Tests/PlayMode/Procedural/EncyclopediaCreaturePlayModeTests.cs): 원본 catalog/identity, cropped/uncropped portrait, dot 범위, 정지 중 preview, affection projection, 전체 보기 닫기와 실제 book transition.
- [Item source/GlobalUIRoot 보존](../../outputs/encyclopedia-implementation-2026-10-11/item-preservation-report.json), [prefab serialized 보존](../../outputs/encyclopedia-implementation-2026-10-11/serialized-preservation-report.json), [65개 localized label/placeholder 검사](../../outputs/encyclopedia-implementation-2026-10-11/creature-localization-report.txt)를 수행했다. 전체 허브 수동 플레이/Player build는 미실행이다. native fixture는 base font로 UI projection을 검사했으며 localized font event를 제거한 fixture의 missing-CJK-glyph warning은 production CJK rendering 검증으로 해석하지 않는다.
- [Profile Save Ownership](../Architecture/ProfileSaveOwnershipArchitecture.md), [Scene Domain Bootstrap](../Architecture/SceneDomainBootstrapArchitecture.md), [Presentation Authoring Contract](../Contracts/PresentationAuthoringContract.md).
- [Deferred Categories](../RefactorBacklog/EncyclopediaDeferredCategories.md)는 이 승인된 확장으로 deferral을 닫는다. Discovery gating은 아래 저장 경계로 구현되어 있고 추가 theme filtering은 포함하지 않는다.
- Architecture/Contracts 승격 후보: 실제 검증 뒤 종 ID·집계 범위·run commit이 다른 progression 소비자에게 공용 규칙이 될 때 검토할 수 있다. 현재 slice는 해당 source-of-truth 문서를 편집하지 않는다.
