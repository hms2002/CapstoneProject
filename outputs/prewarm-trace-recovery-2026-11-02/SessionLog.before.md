# 2026-11-02 작업 기록

## 서기관 카메라 표식 노출 및 blocking 대사 보존

- 범위: 서기관 카메라 연출과 문 개방 조건은 유지하면서 내부 `[camera cue]`가 표시되지 않도록 수정한다. 호감도/실패 연출 뒤 실제 대사가 누락되지 않는 기존 동작은 보존한다. 씬·프리팹·NPC 데이터·저장 스키마·카메라 소유권은 변경하지 않는다.
- `DialogueController.cs`: 현재 Ink 줄의 `presentation_only` 태그를 콜백에 전달한다. 표시 전용 효과만 있는 줄은 효과 종료 후 다음으로 진행하고, 태그가 없는 실제 대사는 기존처럼 같은 줄을 표시한다. blocking feature가 없는 표시 전용 줄도 내부 글자를 노출하지 않는다. 종료된 세션의 늦은 콜백 무시 조건은 보존했다.
- `GrandHallScribe.ink` 및 컴파일 JSON: 기존 `feature: scribe_cue` 6개 줄에만 `presentation_only`를 추가했다. 실제 대사·카메라 큐 순서·진행 조건은 그대로다. 서기관 카메라/입장 허용 소유자인 `GrandHallScribeSequence.cs`와 `Grand Hall.unity`의 SHA-256은 작업 전후 동일하다.
- 기존 `SceneSmokePlayModeTests.cs`에 별도 `DialogueBlockingPresentationPlayModeTests` fixture를 추가했다. 실제 Ink/TagHandler/DialogueController 경로에서 카메라 콜백 대기와 표식 미표시, 언어 변경, 실제 대사를 동반한 feature/호감도/실패 콜백, feature 없는 표시 전용 줄, 세션 종료 후 콜백, 서기관 intro/audience/repeat 큐 수를 검증하는 9개 사례다. 테스트용 inactive 객체를 사용하고 사용자 씬/저장은 로드하거나 저장하지 않는다.
- 실행한 검증: Ink 컴파일과 소스/JSON 일치. intro 대사 9/큐 4, audience 대사 2/큐 2, repeat 대사 1/큐 0. 모든 카메라 큐에 표시 전용 태그가 있고 실제 대사에는 없음을 확인했다. UI.csproj 및 PlayModeTests.csproj MSBuild 모두 종료 코드 0. 테스트 fixture의 컴파일 성공은 PlayMode 테스트 실행 성공과 구분한다.
- Unity PlayMode 테스트는 실행 완료하지 못했다. 사용자의 Editor가 이미 열린 상태라 별도 배치는 즉시 종료됐고, 열린 Editor는 JSON import 중 Win32 1224 파일 잠금 오류와 Importing assets 대기 창을 표시했다. Editor를 종료/재시작하거나 씬을 저장하지 않았다. 컴파일 결과와 실제 PlayMode 결과를 구분하며, 수동 서기관 연출/문 개방 및 기존 보스 호감도 연출 재확인은 남아 있다. 검증 로그: `Library/CodexScribeCue20261102/`.
- Doc Impact Check: SessionLog + ErrorLog + StructureMemory(GrandHallScribeSequence). DecisionLog/Architecture/Contracts/Guides/Presentation HTML 변경 및 HTML stale 후보 없음.

### 가져오기 대기 복구 확인

- 사용자가 약 10분 뒤 Importing assets 창이 해제됐다고 확인했다. 이어 열린 Unity의 정상 에디터 화면과 UI/PlayModeTests 컴파일 산출물 생성을 확인했다. 강제 종료나 재시작은 하지 않았다.
- Win32 1224 예외와 Ink 플러그인의 예외 시 StopAssetEditing 미실행 가능성은 확인했지만, 이번 대기가 영구 교착이거나 재시작이 필수라고 단정할 근거는 없었다. 앞선 사용자 안내의 재시작 필요 단정을 정정했다.
- 열린 에디터의 테스트 러너 접근을 시도했으나 사용자 입력이 감지돼 추가 UI 조작을 중단했다. PlayMode 회귀 테스트 실행 및 실제 서기관/보스 대화 검증은 여전히 미완료다.

## 한계선 전용 유물 2종 사용자 수정 이미지 적용

- 모드/범위: Micro-fix. 사용자의 요청에 따라 이름·효과는 유지하고 첨부 이미지 2개만 적용했습니다. 첨부 1번은 한계선의 잔불 추격, 2번은 한계선의 관통 용암에 연결했습니다.
- 새 파일: `Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_CrimsonKillShot.png`, `Icon_CrimsonLavaBall.png` 및 각각의 `.meta`. 원본 PNG 바이트를 그대로 복사하고 Point / 32 PPU / 단일 스프라이트 / 밉맵 없음 / 압축 없음 설정을 사용했습니다.
- 연결 변경: `Assets/_Project/Data/Items/Relics/Definitions/RD_CrimsonKillShot.asset`과 `RD_CrimsonLavaBall.asset`에서 icon 한 줄씩만 변경했습니다. 이름·설명·효과·logic·레벨·relicId·정의 GUID·번역은 그대로입니다.
- 검증: 첨부/복사본 SHA-256 일치 2/2, 실제 32×32 RGBA·각 6색·이진 알파, 신규 GUID 중복 없음(기존 13850개와 대조), 두 정의의 icon 외 바이트 보존, 공용 지팡이 이미지와 무기 정의 등 보호 파일 427개 해시 보존, 정의 diff 공백 검사를 통과했습니다.
- [변경 파일·검증 기록](../../output/relic-review-2026-11-02/crimson-exclusive-approved-application/README.md).
- 미실행/남은 확인: Unity Editor import와 실제 인벤토리·도감·보상 화면 표시. C# 변경이 없어 컴파일 테스트는 실행하지 않았습니다. 이름·효과 변경은 사용자의 추후 지시 대상입니다.
- Doc Impact Check: SessionLog. 이 작업으로 ErrorLog/DecisionLog/StructureMemory/Presentation HTML은 변경하지 않았으며 Presentation HTML stale candidate도 없습니다.

## 필드 유물·소모품 그림자 적용

- 모드/범위: Implementation. 사용자가 비교 자료에서 고른 작은 픽셀 타원(기존 `Shadow_0`), 폭 75%·높이 25%·진하기 34%, 보이는 윤곽 기준 간격 -4px(32px 미리보기 기준)을 필드 유물·소모품 공용 드롭에 적용했다. 사용자의 '-4%' 표현은 실제 선택값인 '-4px' 모습으로 해석한다고 작업 전에 명시했다.
- `PF_WorldItemPickup2D.prefab`: authored `Shadow` 자식과 `WorldItemShadowPresenter2D` 및 활성 정의 75개/서로 다른 아이콘 74종의 접점 정보를 추가했다. 기존 root/Render/Collider/pickup/presenter YAML은 필수 자식·컴포넌트 목록 추가를 제외하고 보존했다.
- 새 `WorldItemShadowPresenter2D.cs` 및 `.meta`: 원본 스프라이트와 투명 여백을 반영한 접점 정보로 위치·폭·높이·sorting을 투영한다. runtime 텍스처 읽기/표시 객체 생성 없이 동작하며, 기존 `IWorldItemDropLandingVisual`로 낙하 중 숨김·착지 후 표시를 연결했다. 실제 아이콘의 transform/outline/회전 중심과 획득 로직은 변경하지 않았다.
- 새 Editor `WorldItemShadowAuthoring.cs` 및 `.meta`: 프리팹 하나만 갱신하는 접점 bake 메뉴와 native 검증/캡처 메뉴를 제공한다. 이후 아이콘 추가/알파 윤곽 변경 시 bake가 필요하다. 구조 지도: [WorldItemShadows](../StructureMemory/WorldItemShadows.md).
- 실행 검증: 활성 정의 75/75 및 아이콘 74종 coverage, 독립 알파 충돌 판정 148개, prefab 원본 블록/로컬 참조 보존, 관련 보호 파일 SHA-256 1,941/1,941, 신규 GUID 중복 없음, 공백 검사. 기존 Presentation MSBuild는 통과했지만 생성 csproj에 신규 파일이 아직 없으므로 새 코드 coverage로 주장하지 않는다. 새 runtime/Editor 소스는 실제 Unity/project 참조를 이용한 별도 Roslyn 컴파일을 모두 통과했다.
- Unity 상태/미실행: 이미 열린 Editor가 `Importing assets` 대기 창을 표시했고(확인 당시 27분 이상), 별도 batch는 동일 프로젝트 중복 실행 거부로 종료 코드 1이었다. 사용자 Editor/씬을 종료·재시작·저장하지 않았다. 프리팹은 PNG/메타 기반 offline bake로 반영했다. Unity import/컴파일, native 검증/캡처, 실제 Play Mode 낙하·획득·조명 확인 및 Player build는 미실행이다. [검증 기록과 남은 확인](../../output/world-item-shadows-2026-11-02/README.md).
- Doc Impact Check: SessionLog + StructureMemory. ErrorLog/DecisionLog/Architecture/Contracts/Guides/Presentation HTML은 미변경이며 Presentation HTML stale candidate도 없다.

## 상인 NPC 대사·호감도 보상 Notion 정리

- 모드/범위: Implementation, 문서 정리. 사용자가 지정한 [상인 NPC 페이지](https://app.notion.com/p/40c3291840a480fd824afe15aa40867d)에 현재 구현된 한국어 대사와 단계별 영구 보상을 정리했다. 기존 구분선·빈 문단과 페이지 속성은 유지하고 내용을 추가했다.
- Notion 추가 내용: 호감도 대화 원문 6종의 선택지 12개와 각 응답/호감도 변화, 일반 안부 3종, 가방 가득 참 안부 2종, 구매 실패 반응 2종, 런당 대화 운영 규칙, 영구 보상 4단계 및 할인 중첩/금화 이월/등급 가중치 조건. 현재 호감도 3에서 2-A/2-B를 재사용하는 후보 연결도 표시했다.
- 근거/검증: 상인 Ink와 한국어 GameText CSV, NPCData의 8개 후보/4개 보상 연결, 보상 에셋 수치(특가 10~20%, 보존 100, 희귀/영웅 가중치 0.10/0.15, 전체 할인 0.05), 실제 구매 실패 대사 조회 경로를 대조했다. 저장된 Notion 내용을 읽어 대사·선택지·응답·구매 반응의 원문 텍스트 40개가 모두 포함됨을 확인했다. 검토 문장 한 곳의 Markdown 굵게 표현이 Notion의 `<b>`로 정규화되어 부분 수정 경고가 발생했으며, 저장 내용을 읽고 일반 본문을 대상으로 재수정해 경고 없이 완료했다.
- Suggested Later: 첫 호감도 대사와 통화 부족 공통 한국어 번역에는 마정석 표현이 남아 있고, 런 상인 기본 통화 부족 문구는 골드 표현이므로 검토 사항으로 기록했다. 호감도 3→4 전용 대사 추가와 보상 수치 후속 기획도 구분했다. 이번 작업은 해당 대사나 게임 동작을 수정하지 않았다.
- 로컬 변경 파일: 이 SessionLog에 Notion 변경 목적·범위·검증·남은 검토 사항을 기록했다. 문서 작업이므로 Unity import/컴파일/Play Mode/Player build는 실행하지 않았다.
- Doc Impact Check: SessionLog. ErrorLog/DecisionLog/StructureMemory/Architecture/Contracts/Guides/Presentation HTML은 이 작업으로 변경하지 않았으며 Presentation HTML stale candidate도 없다.

## 일반 NPC 대화 누적 해금·최대 호감도 대화 유지

- 모드/범위: Implementation. 사용자가 승인한 [범위](../ActiveTasks/npc-dialogue-cumulative-pool.md)에 따라 이전 대화를 계속 무작위 후보로 유지하고, 최대 호감도에서도 런당 선택지 대화 1회를 제공한다. 그 뒤에는 기존 안부 대사를 사용한다. 호감도 상승·보상 재지급과 상승 표시 말풍선은 최대 상태에서 제공하지 않는다. 허브·보스·미설정 NPC 흐름, 보상 수치와 말풍선 위치는 보존했다.

### 변경 파일과 이유

- `Assets/_Project/Runtime/Features/Dialogue/NpcAffectionDialogue.cs`: 기존 `affectionLevel` 필드를 최소 해금 단계로 해석하며 Inspector Tooltip으로 설명한다. `CanStartConversation`은 최대 상태를 포함한 선택지 대화 가능 여부를, 기존 `CanOffer`는 상승/말풍선 가능 여부를 판단한다. 선택 시 해금된 서로 다른 path만 한 번씩 후보에 넣어 균등 추첨한다. 기존 serialized 필드명·저장 스키마·namespace/assembly는 유지한다.
- `Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsAuthoring.cs`: 별도 `ConfigureConversations`에 0단계 3개, 1·2단계 각 2개를 등록하고 중복된 3단계 path 두 개를 제거했다. 이전 설정 메뉴도 이 후보 구성을 유지한다. 이번 실제 migration에서는 보상 재설정 경로를 호출하지 않았다.
- `Assets/_Project/Editor/Tools/Dialogue/NpcAffectionDialogueAuthoring.cs`: 기존 도구의 설명 개수를 갱신하고, 별도 배치 경로에서 Ink 컴파일·NPC 후보·상인 번역만 반영한다. 스프라이트/프리팹/보상 저작은 실행하지 않는다.
- `Assets/_Project/Editor/Tools/Dialogue/NpcAffectionDialogueValidation.cs`: 기존 실제 라우팅/런/호감도 fixture를 누적 후보와 최대 상태 규칙에 맞게 확장했다. 5개 호감도 값에서 각 256개 seed로 전체 후보 도달·미해금 제외·중복/null/공백 중립성을 확인하고, 최대 양성 선택의 콜백·무증가·보상 불변, 중단/재시도·완료/재조우·새 런·실패·말풍선 숨김을 검증한다. 전체 활성 상인 loc 43개의 언어별 값도 비교한다.
- `Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsValidation.cs`: 별도 보상 검증 도구의 기존 3단계 중복 후보 가정을 7개 누적 후보로 갱신했다. 이 별도 도구 전체를 이번에 재실행했다고 주장하지 않는다.
- `Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink`와 컴파일 JSON: 기본 후보 `affection_0_c`에 일방적인 동업 선언을 추가했다. 사샤가 정문/뒷문 분업을 선언하고, 안전을 챙기는 답변에 +1, 구매하지 않겠다는 답변에 변화 없음으로 반응한다. 한 분기는 첫마디 1창 + 응답 1창이다. 기존 원문은 새 knot 밖에서 그대로다.
- `Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset`: 7개 서로 다른 path와 최소 해금 단계만 반영했다. 실제 후보 수는 호감도 0/1/2/3/4에서 3/5/7/7/7이다. 보상·Primary Ink GUID·기본 NPC/인사/초상 설정은 보존했다.
- `DataSheets/Localization/GameText.csv`, `InkInventory.json`, `Assets/_Project/Data/Localization/GameText Shared Data.asset` 및 5개 언어 테이블: 5개 신규 키/25개 번역을 등록하고 상인 원문 inventory의 줄 번호를 맞췄다. 기존 모든 CSV 값과 locale ID/record, 다른 Ink inventory는 보존했다. 다른 유물명에 생긴 Unity의 의미 동일한 YAML 따옴표/escape 대소문자 변화는 원래 표기로 복원했다.
- [Notion 상인 NPC 페이지](https://app.notion.com/p/40c3291840a480fd824afe15aa40867d): 누적 후보 표, 최대 상태 규칙, 해금 단계 기준 제목과 동업 선언을 갱신했다. 다른 내용·보상 수치·사용자가 조정한 표 너비는 유지했고, targeted update는 경고 없이 완료됐다.
- ActiveTask/TaskIndex, DialogueNpcAffectionStructure·SceneRuntimeSaveStructure, DecisionLog, ErrorLog, 이 SessionLog: 확정한 규칙, gain/choice 구분과 기존 session 소유권, 검증·정규화 함정을 기록했다.

### 검증과 남은 확인

- Unity 6000.4.2f1 실제 import/compile 및 생산 코드/메모리 프로필 기반 **337/337 통과, 종료 코드 0**. [검증 결과](../../outputs/npc-dialogue-pool-2026-11-02/validation.txt), [Unity 로그](../../outputs/npc-dialogue-pool-2026-11-02/unity-validation.log). 호감도 4에서도 실제 indicator renderer가 대화 시작 전 꺼져 있고, 양성 선택은 콜백을 정확히 한 번 호출하면서 호감도·보상 개수를 유지함을 확인했다.
- 보존/복사 검증 **216개 통과**. [검증 기록](../../outputs/npc-dialogue-pool-2026-11-02/static-checks.txt). 기존 원문/번역, 후보 목록 외 NPC 설정, 다른 inventory, 보호한 씬·보상·설정·수동 말풍선 위치를 비교했다. 검증된 NPC/JSON/6개 번역 자산만 복사했고 기존 `.meta`·프리팹·폰트·ProjectSettings는 복사하지 않았다.
- 생성된 `Editor.csproj`가 수정된 C# 5개를 포함하는 상태에서 MSBuild를 실행했다. [첫 빌드](../../outputs/npc-dialogue-pool-2026-11-02/msbuild.log)와 마지막 fixture 변경까지 포함한 [최종 빌드](../../outputs/npc-dialogue-pool-2026-11-02/msbuild-final.log)는 모두 종료 코드 0으로 통과했다. 생성 프로젝트를 직접 수정하거나 새 C# 파일/asmdef를 추가하지 않았다.
- 최종 파일 해시, 보호 파일, 생성 프로젝트의 변경 소스 coverage, C# 공백 및 Markdown 로컬 경로 검사도 통과했다. [최종 점검](../../outputs/npc-dialogue-pool-2026-11-02/final-checks.txt).
- 사용자의 열린 Unity와 실제 저장 슬롯을 건드리지 않고 별도 `.ncv` 프로젝트에서 검증했다. 증거 보존 뒤 해당 임시 프로젝트를 정리한다. 전체 수동 Play Mode, 실제 대화/구매 입력 감각 및 Player build는 실행하지 않았다. 신규 외국어 문장은 초안 번역이며 언어 감수는 남아 있다.
- Doc Impact Check: SessionLog + StructureMemory + DecisionLog + ErrorLog. Architecture/Contracts/Guides/Presentation HTML은 변경하지 않았고 기존 Presentation 요약에 명시된 소유권은 유지되어 HTML stale 후보가 없다.

## 사샤 기본·당황 초상 및 할인 명분 대화

- 모드/범위: Implementation. [작업 범위](../ActiveTasks/sasha-discount-portraits.md). 첨부 1을 기본 초상으로 교체하고 첨부 2를 기존 호감도 유지 선택지의 당황 표정으로 연결했다. 앞서 합의한 1단계 할인 명분 대사도 두 번 넘기는 시작 구성으로 추가했다. 런/호감도/보상 소유권과 기존 수동 말풍선 위치는 유지했다.

### 변경 파일과 이유

- `Assets/_Project/Art/Sprites/NPCs/merchant/merchant.png`: 첨부 1의 원본 바이트로 교체했다. 기존 `.meta`, GUID와 sprite fileID를 보존했다. 새 `merchant_surprised.png` 및 Unity 생성 `.meta`: 첨부 2를 별도 초상으로 등록했다. 두 이미지 모두 1254×1254이며 픽셀 수정이나 리사이즈는 하지 않았다.
- `Assets/_Project/Data/Dialogue/NPC/SpriteLibrary/SpriteLibraryAsset_Npc_Merchant.spriteLib`: 공식 SpriteLibrary source factory로 Face/Normal을 유지하고 Face/Surprised를 추가했다. 기존 라이브러리 GUID/localID와 `.meta`는 그대로다. 실제 `PortraitActor`, 태그 처리기, `Portraitlmage.prefab`의 크기·위치·참조는 바꾸지 않았다.
- `Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink` 및 컴파일 JSON: 기존 7개 `choice_fail` 응답에 Surprised를 연결하고 새 `affection_1_c` 거절 응답에도 같은 표정을 사용했다. 시작/인사/긍정 응답은 Normal이다. 최대 호감도라 수치가 오르지 않는 긍정 응답도 Normal을 유지한다. 기존 대사의 문구·선택 결과는 유지했다.
- `Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset`: 1단계 최소 해금 후보 `affection_1_c`만 추가했다. 누적 후보는 호감도 0/1/2/3/4에서 3/6/8/8/8종이다. 보상과 다른 NPC 설정은 그대로다.
- 새 대사: “어서 와, 용사님! 왜 이렇게 싸냐고? 헤헤, 내 장사엔 다 계산이 있지!”를 첫 창에, 사용자가 지정한 매입비/시선/재방문 설명을 둘째 창에 출력한다. 둘째 창 이후 두 선택지가 나온다. “그걸 나한테 설명해도 돼?”는 최대 미만 +1/기본 응답, “그래도 안 살 건데.”는 변화 없음/당황 응답이다. 각 응답은 한 창이다.
- `DataSheets/Localization/GameText.csv`, `InkInventory.json`, `Assets/_Project/Data/Localization/GameText Shared Data.asset` 및 다섯 언어 테이블: 신규 6키/30개 번역을 등록하고 상인 inventory만 갱신했다. 이전 CSV/locale ID·값·raw record 및 다른 Ink inventory를 보존했다. Unity가 의미 변화 없이 바꾼 기존 유물명 YAML 따옴표/Unicode escape 대소문자도 preimage 표기로 복원했다.
- `Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsAuthoring.cs`: 공용 후보 저작 경로를 8종으로 갱신했다. `MerchantAffectionRewardsValidation.cs`: 별도 보상 검증 도구의 예상 후보 수도 8종으로 맞췄다. 별도 전체 301-case 보상 도구는 이번에 재실행하지 않았다.
- `Assets/_Project/Editor/Tools/Dialogue/NpcAffectionDialogueAuthoring.cs`: 격리 batch에서 새 초상 import/기존 라이브러리 수정 후 대사·번역만 저작하는 진입점을 추가했다. 보상/월드 프리팹 재저작 메뉴는 실행하지 않았다.
- `Assets/_Project/Editor/Tools/Dialogue/NpcAffectionDialogueValidation.cs`: 두 대사창, 8종 후보, 49개 활성 대사 키/245개 번역, 실제 초상 프리팹·태그 처리기의 Normal/Surprised 분기와 다음 대화 reset을 검증한다. 검증에서만 소유하고 정리하는 Canvas/camera로 두 표정을 촬영했다. 런타임 UI 객체 생성이나 프로필 저장 변경은 없다.
- [Notion 상인 페이지](https://app.notion.com/p/40c3291840a480fd824afe15aa40867d): 8종 후보와 할인 대사, 두 창 구성, 표정 적용 범위를 targeted text update로 정리했다. 경고 없이 완료했고, 기존 원문/보상/사용자가 조정한 표 너비를 보존했다. 원본 이미지 첨부 시도는 외부 공유에 대한 명시적 승인이 없다는 자동 승인 검토 사유로 거절됐으며 이미지는 업로드하지 않았다. 게임 반영이나 문서 텍스트 갱신에는 영향이 없다.
- ActiveTask/TaskIndex와 이전 scope의 후속 링크, 기존 DialogueNpcAffectionStructure를 현재 대사/표정에 맞췄다. ErrorLog에는 열린 Windows Editor의 파일 매핑으로 in-place copy가 실패했을 때 preimage 검증과 원자 교체로 보존하는 함정을 기록했다. DecisionLog는 이번 slice에서 추가 변경하지 않았다.

### 검증과 남은 확인

- Unity 6000.4.2f1 실제 import/compile 및 production assembly/메모리 프로필 기반 **430/430 통과, 종료 코드 0**. [결과](../../outputs/sasha-portraits-2026-11-02/validation.txt), [Unity 로그](../../outputs/sasha-portraits-2026-11-02/unity-validation.log).
- 실제 authored portrait prefab에서 기본/당황 두 표정, 모든 대사 분기와 안부 reset, 이미지 전체 rect/투명도/Point/100 PPU 및 기존 참조를 확인했다. [네이티브 촬영](../../outputs/sasha-portraits-2026-11-02/portraits-native.png)을 직접 검토해 크기·위치가 같고 두 표정이 정상 출력됨을 확인했다. 누적 선택/런 제한/최대 호감도/보상 불변/말풍선의 기존 회귀 검사도 포함했다.
- 보존/복사 검증 **232개 통과**. [기록](../../outputs/sasha-portraits-2026-11-02/static-checks.txt). 133개 보호 파일과 기존 메타/번역/대사/NPC 설정을 보존했다. 15개 승인 asset/data만 원자 교체했고 복사 후 해시를 확인했다. 기존 UI/월드 프리팹·씬·보상·설정은 복사하지 않았다.
- 변경된 네 C#가 생성 `Editor.csproj`에 포함된 상태에서 **MSBuild 종료 코드 0**을 확인했다. [로그](../../outputs/sasha-portraits-2026-11-02/msbuild.log). 새 C#나 asmdef를 추가하거나 생성 프로젝트를 직접 수정하지 않았다. 관련 없는 기존 경고는 남아 있다.
- [최종 검사](../../outputs/sasha-portraits-2026-11-02/final-checks.txt)는 복사 자산/소스/보호 파일 해시, 기존 GUID 메타, C# 프로젝트 포함/공백, Markdown 로컬 링크와 네이티브 통과 마커를 확인한다. 검증 증거를 보존한 뒤 `.spv` 임시 프로젝트를 정리했다.
- 사용자의 열린 Unity와 실제 저장 슬롯은 조작하지 않았다. 전체 수동 Play Mode와 Player build는 실행하지 않았다. 신규 외국어 네 종류는 초안 번역이므로 감수는 남아 있다. 선택적 Notion 이미지 첨부에는 원본 두 파일의 외부 공유 승인이 필요하다.
- Doc Impact Check: SessionLog + StructureMemory + ErrorLog. TaskIndex/ActiveTasks는 라우팅/결과로 갱신했다. DecisionLog·Architecture·Contracts·Guides·Presentation HTML은 변경하지 않았고 소유권 구조가 유지되어 HTML stale 후보가 없다.

## 사샤 웃는 표정 — 지정한 긍정 응답 두 곳

- 모드/범위: Implementation, 현재 사용자의 좁은 후속 요청. 새 1254×1254 첨부를 Face/Happy로 등록하여 `affection_0_a`, `affection_1_c`의 호감도 상승 선택지 응답에만 적용했다. 최대 호감도에서도 해당 긍정 분기의 표정은 Happy이며 수치/보상 규칙은 그대로다.
- `Assets/_Project/Art/Sprites/NPCs/merchant/merchant_happy.png` 및 Unity 생성 `.meta`: 첨부 원본 바이트 그대로 신규 등록했다. 기존 PNG/메타는 보존했으며, 전체 canvas/pivot/100 PPU/Point/투명도/no mipmap을 확인했다.
- `Assets/_Project/Data/Dialogue/NPC/SpriteLibrary/SpriteLibraryAsset_Npc_Merchant.spriteLib`: 기존 source factory로 Face/Happy만 추가했다. Normal/Surprised 매핑, 기존 라이브러리 GUID/localID 및 `.meta`는 유지했다.
- `Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink`와 컴파일 JSON: 지정한 두 긍정 응답의 face 태그만 Normal에서 Happy로 바꿨다. JSON 구조 비교에서도 같은 두 태그 외 차이가 없다. 대사/선택지/loc/호감도 태그와 다른 분기는 보존했다. 대화 시작은 Normal, 유지 응답은 Surprised, 다른 긍정 응답은 Normal이다. CSV/번역/InkInventory/NPC 후보/프리팹/보상은 변경하지 않았다.
- `Assets/_Project/Editor/Tools/Dialogue/NpcAffectionDialogueValidation.cs`: 기존 초상 회귀 도구에서 두 긍정 분기의 기대 표정을 Happy로 맞추고 새 표정의 canvas/pivot/PPU를 확인하도록 했다. 새 runtime C#나 새 검증 프레임워크는 추가하지 않았다.
- 기존 ActiveTask에 후속 승인/결과를 덧붙이고 DialogueNpcAffectionStructure의 현재 표정 매핑만 갱신했다. 이 SessionLog는 변경 파일/검증/남은 확인을 기록한다.
- 검증: 최소 portrait/Ink 전용 임시 Unity 프로젝트에서 **39/39 네이티브 검사 통과, 종료 코드 0**. 새 asset import·라이브러리 참조·실제 Ink 실행·8개 대화 시작/reset과 16개 응답의 표정 매핑을 확인했다. 전체 런/보상 회귀는 재실행하지 않았다. [결과](../../outputs/sasha-happy-2026-11-02/validation.txt), [Unity 로그](../../outputs/sasha-happy-2026-11-02/unity-native.log).
- 보존/복사 **48/48 통과**: 기존 파일의 작업 전 해시와 정확한 두 태그 차이를 확인하고 검증된 5개 asset/data만 원자 교체했다. [보존 결과](../../outputs/sasha-happy-2026-11-02/preservation.txt). 기존 Editor fixture 1개도 수정 후 생성 `Editor.csproj` 포함 상태에서 **MSBuild 종료 코드 0**을 확인했다. [빌드](../../outputs/sasha-happy-2026-11-02/msbuild.log).
- 첫 샌드박스 실행은 Windows IPC/라이선스 연결 제약으로 helper 실행 전에 종료됐다. 같은 최소 프로젝트를 승인된 제한 밖 실행으로 검증했다. 사용자의 열린 Editor/씬/실제 저장 슬롯은 조작하지 않았다. 임시 `.shp`는 증거 보존 후 정리했다.
- 남은 확인: 전체 수동 Play Mode와 Player build는 실행하지 않았다. 이미지·대사 외부 업로드와 Notion 변경은 이번 요청 범위에 포함하지 않았다.
- Doc Impact Check: SessionLog + 기존 StructureMemory의 현재 표정 매핑 및 ActiveTask 후속 기록. ErrorLog/DecisionLog 추가 변경 없음. 구조/소유권 변경이 없어 Architecture/Contracts 승격 또는 Presentation HTML stale 후보 없음.

## 사샤 스피리 영역 물건 대화 및 영역별 후보 조건

- 모드/범위: Implementation. [승인 범위](../ActiveTasks/sasha-spiri-dialogue.md). `affection_0_b`를 합의한 초안으로 교체하고 스피리 영역에서만 무작위 후보로 제공한다. 런 중 NPC별 대화 1회와 기존 최대 호감도/보상 규칙은 유지한다.
- `Assets/_Project/Runtime/Features/Dialogue/NpcAffectionDialogue.cs`: 기존 entry에 선택적 `requiredThemeId`를 추가했다. 빈 값은 종전처럼 모든 영역에서 허용하고, 지정한 값은 기존 `RunRoutePlayback`의 활성·확정된 route `StableThemeId`와 비교한다. 활성 계획/현재 영역이 없으면 제한된 후보만 제외한다. 동일한 predicate를 CanStartConversation과 TrySelect가 사용해 말풍선/대화 가능 여부와 실제 선택이 일치한다. 필터를 통과한 서로 다른 path에서 기존 균등 선택을 유지하며 별도 확률 판정/상태 저장은 추가하지 않았다.
- `Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsAuthoring.cs` 및 `Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset`: 기존 8개 후보 중 `affection_0_b`에만 `requiredThemeId: dragon`을 지정했다. 다른 후보는 빈 값이다. 호감도 0/1/2/3/4의 후보 수는 스피리 영역에서 3/6/8/8/8, 다른 영역 또는 route 정보가 없을 때 2/5/7/7/7이다. 다른 NPC의 기존 미지정 필드는 기본적으로 제한 없이 동작한다.
- `Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink` 및 JSON: 술주정뱅이 용 옆에서 반짝이는 물건을 골라 왔다는 소개 → 안 깨우면 된다는 설명의 두 대사창 뒤에 선택지를 출력한다. “너, 들키면 어쩌려고?”는 Happy/+1(최대 미만), “정말 눈치 못 챈 걸까?”는 Surprised/변화 없음이다. 응답은 각각 한 창이며 시작은 Normal이다. 다른 knot의 대사·선택·결과·표정은 그대로다. 초상 PNG/라이브러리/메타는 변경하지 않았다.
- `DataSheets/Localization/GameText.csv`, `InkInventory.json`, `Assets/_Project/Data/Localization/GameText Shared Data.asset` 및 5개 언어 테이블: 기존 `.0`–`.4`의 ID를 유지해 문구를 교체하고 둘째 대사창 `.5`만 추가했다. 6개 대사의 30개 언어 값을 반영하고 상인 inventory의 줄 번호/선택 여부를 맞췄다. 활성 merchant 키는 50개다. 다른 CSV/locale ID·값·metadata/raw record와 다른 Ink inventory는 보존했다.
- `Assets/_Project/Editor/Tools/Dialogue/NpcAffectionDialogueAuthoring.cs`: 기존 대화/후보/번역 반영 본문을 공용 private 함수로 추출하고, 좁은 `UpdateSpiriDialogueBatch` 진입점으로 재사용했다. 보상·월드 배치·초상 저작은 실행하지 않았다.
- `Assets/_Project/Editor/Tools/Dialogue/NpcAffectionDialogueValidation.cs`: 영역별 후보·가용성·사용 기회 검증, 정확한 두 대사창/선택지/응답과 실제 표정 태그 처리, 50개 활성 키/250개 언어 값을 확인하는 scoped runner를 추가했다. 기존 전체 도구의 예상 후보/Happy 분기/키 개수도 현재 콘텐츠에 맞췄다. 새 검증 프레임워크나 runtime owner는 추가하지 않았다.
- ActiveTask/TaskIndex, 기존 DialogueNpcAffectionStructure와 DecisionLog에 현재 영역 조건과 확률/런 기회 의미를 기록했다. ErrorLog는 기존 직렬화/Windows 파일 교체 함정을 따랐으며 새 항목을 추가하지 않았다.

### 검증 및 남은 확인

- Unity 6000.4.2f1 import/compile 후 실제 production selector/session/Ink/tag parser와 메모리 프로필로 **359/359 통과, 종료 코드 0**. [결과](../../outputs/sasha-spiri-2026-11-02/validation.txt), [Unity 로그](../../outputs/sasha-spiri-2026-11-02/unity-native.log). Dragon/다른 영역/route 없음 각각에서 호감도 0–4, 256개 seed로 도달/제외/중복 중립성과 후보 수를 확인했다. 조건부 후보만 있는 경우의 indicator/안부, 영역 변경 후 기회 유지, 새 런 초기화, 비활성 계획/현재 영역 없음도 확인했다.
- 생성 Editor/Gameplay 프로젝트가 변경된 기존 C# 4개를 포함한 상태에서 **MSBuild 종료 코드 0**. [로그](../../outputs/sasha-spiri-2026-11-02/msbuild.log). 새 C#나 asmdef/생성 프로젝트 수정은 없다. 관련 없는 기존 빌드 경고는 유지했다.
- 보존/복사 **989개 통과**. [기록](../../outputs/sasha-spiri-2026-11-02/preservation.txt). 보호 파일 860개와 대사/번역/기존 ID 및 metadata를 대조한 뒤 검증된 11개 asset/data만 원자 교체했다. Unity의 의미 동일한 기존 YAML 따옴표/Unicode escape 대소문자 변화는 preimage 표기로 복원했다. 교체한 긴 번역의 YAML 여러 줄은 값 전체와 metadata를 구분해 검사했으며 실제 값은 native StringTable 조회로 확인했다.
- [최종 검사](../../outputs/sasha-spiri-2026-11-02/final-checks.txt)는 반영 파일/소스/보호 파일 해시, 생성 프로젝트 포함/공백, Markdown 로컬 경로 및 native pass marker를 확인한다. 증거 보존 후 경로를 검증하여 `.ssv` 임시 프로젝트만 정리했다.
- 사용자의 열린 Editor/씬/실제 저장 슬롯은 조작하지 않았다. 전체 수동 Play Mode와 Player build는 실행하지 않았다. 외국어 4종은 초안 번역이며 언어 감수가 남아 있다. 이번에는 영역 조건과 대사 관련 검사만 실행했으며 전체 430-case 런/보상 회귀 도구는 재실행하지 않았다.
- Doc Impact Check: SessionLog + StructureMemory + DecisionLog, ActiveTask/TaskIndex 갱신. ErrorLog 추가 변경 없음. Architecture/Contracts/Guides/Presentation HTML은 변경하지 않았고 기존 소유권이 유지되어 HTML stale 후보가 없다.

## 사샤 일방적 동업 선언 개그 대사 교체

- 모드/범위: Implementation, 현재 사용자가 승인한 좁은 콘텐츠 수정. `affection_0_c`만 “용사님 몫도 챙겨 왔다 → 용사님에게 팔 몫이었다”는 초안으로 교체했다. 런/영역 조건·호감도/보상 처리와 기존 UI·초상 자산은 유지했다.
- `Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink` 및 JSON: 정문/뒷문 분업 인사와 용사 몫 소개의 두 창 뒤에 선택지를 출력한다. “그럼 내 몫 좀 보여 줘.”는 Happy와 기존 +1 요청, “동업자 몫이면 공짜 아니야?”는 Surprised와 기존 choice_fail을 사용한다. 응답은 각각 한 창이다. 다른 knot 및 컴파일 container는 그대로다.
- `DataSheets/Localization/GameText.csv`, `InkInventory.json`, `Assets/_Project/Data/Localization/GameText Shared Data.asset` 및 다섯 언어 테이블: 기존 5키의 ID를 보존해 교체하고 둘째 창 `.5` 1키를 추가했다. 6개 대사/선택지의 30개 언어 값을 반영했으며 활성 merchant 키는 51개다. 새 ID는 설치된 Localization DistributedUIDGenerator의 기존 epoch/time/machine/sequence 형식으로 만들고 충돌을 검사했다. 키 생성기 설정과 모든 기존 ID는 유지했다. YAML의 수정 값만 JSON 호환 인용 scalar로 기록하여 다른 entry의 raw record까지 보존했다.
- `Assets/_Project/Editor/Tools/Dialogue/NpcAffectionDialogueValidation.cs`: 기존 도구에서 동업 선언의 시작 창 수 2개, 활성 키 51개와 `affection_0_c` 긍정 Happy 표정의 기대값만 맞췄다. runtime C#나 새 검사 프레임워크는 추가하지 않았다.
- 이 SessionLog와 기존 DialogueNpcAffectionStructure는 현재 창 구성/표정/키 개수를 기록한다. 콘텐츠 수정으로 소유권·수명주기·스키마 변경은 없다.
- 검증: 기존 production `Ink-Libraries.dll`을 직접 실행해 전체 상인 Ink를 컴파일하고, **21개 분기 검사 통과**. 정확한 시작 2창/선택 문구/응답 1창, Happy/+1 및 Surprised/no gain 태그, 재시작 시 Normal 복귀를 확인했다. [Ink 결과](../../outputs/sasha-partnership-2026-11-02/ink-checks.txt). 새 Unity 프로젝트나 패키지 임포트는 실행하지 않았다.
- 데이터/보존 **169개 검사 통과**. [결과](../../outputs/sasha-partnership-2026-11-02/data-checks.txt). 기존 공유 ID·locale metadata·다른 locale/CSV record, 다른 Ink container/inventory와 PNG/기존 메타/영역 selector/NPC 설정/월드·초상 prefab을 보존했다. 실제 CSV/원문과 새 YAML 값 30개를 대조하고, 충돌이 없는 10개 text/data만 원자 교체했다.
- 변경된 기존 Editor fixture가 생성 프로젝트에 포함된 상태에서 **MSBuild 종료 코드 0**. [로그](../../outputs/sasha-partnership-2026-11-02/msbuild.log). [최종 검사](../../outputs/sasha-partnership-2026-11-02/final-checks.txt)는 반영 해시·보호 파일·C# 포함/공백과 Markdown 경로를 확인한다.
- 남은 확인: Unity Editor import·수동 Play Mode·Player build는 이번에 실행하지 않았다. 네 외국어 번역은 초안이며 감수가 남아 있다. 기존 초상 표시/영역·런 규칙에 대한 전체 회귀는 재실행하지 않았다. 실제 저장 슬롯과 열린 Editor/씬은 조작하지 않았다.
- Doc Impact Check: SessionLog + 기존 StructureMemory의 콘텐츠 매핑 갱신. ErrorLog/DecisionLog 추가 변경 없음. Architecture/Contracts/Guides/Presentation HTML 변경 및 HTML stale 후보 없음.

## 사샤 검토 완료 호감도 대화 네 개만 활성화

- 모드/범위: Implementation, 현재 사용자의 명확한 후보 비활성 요청. `affection_0_a`, `affection_0_b`, `affection_0_c`, `affection_1_c`만 호감도 대화 후보로 유지했다. 안부/인벤 가득 참 대사는 유지했다. 별도 enabled 필드나 runtime 선택기 변경 없이 후보 목록에서만 제외하는 작은 변경을 택했다.
- `Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset`: 미완성 `affection_1_a`, `affection_1_b`, `affection_2_a`, `affection_2_b` 네 후보 entry만 제거했다. 제거 전 원본은 작업 evidence baseline에도 보존했다. 나머지 entry의 최소 호감도/스피리 조건, rewards·기타 NPC 필드는 그대로다. 네 대사의 Ink/JSON/번역 원문은 삭제하지 않았다.
- `Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsAuthoring.cs`: 공용 ConfigureConversations 기본 목록을 같은 네 후보로 맞춰, setup 또는 대화 반영 도구 재실행이 비활성 대사를 복원하지 않도록 했다.
- `Assets/_Project/Editor/Tools/Dialogue/NpcAffectionDialogueAuthoring.cs`: setup 결과 메시지의 활성 후보 수를 4로 맞췄다.
- `Assets/_Project/Editor/Tools/Dialogue/NpcAffectionDialogueValidation.cs`, `MerchantAffectionRewardsValidation.cs`: 현재 네 path 집합, 최소 단계와 영역별 후보 수에 맞춰 기존 기대값을 갱신했다. 중복 중립성 fixture의 중복 path도 활성 후보 `affection_1_c`로 바꿨다. 새 검사 프레임워크/상태/스키마는 추가하지 않았다.
- 진행 확인: 기존 누적 후보 선택과 +1 효과는 대사 종류 수에 의존하지 않고, 최대 호감도는 변경하지 않은 reward threshold 4에서 계산한다. 스피리 영역의 호감도 0/1/2/3/4 후보 수는 3/4/4/4/4, 그 밖/route 없음에서는 2/3/3/3/3이다. 모든 단계·영역에서 제한 없는 `affection_0_a`와 `affection_0_c`가 계속 남으므로 네 단계 보상까지 올릴 경로가 유지된다. 런당 한 번 소비/초기화와 최대 호감도 동작도 같은 runtime 코드를 사용한다. 당분간 같은 대화의 반복 빈도는 늘어난다.
- 검증: 실제 production Ink runtime으로 활성 4개 path × 양쪽 선택지 **8개 분기 통과**. Normal 시작, 두 선택지, Happy/+1 및 Surprised/no gain 응답을 확인했다. [Ink 결과](../../outputs/sasha-active-dialogues-2026-11-02/ink-checks.txt).
- 데이터/진행 가능 경로/보존 **272개 검사 통과**. 후보 제거 외 NPC bytes, 원문·번역·기존 메타·초상·보상·런 선택기·호감도 처리 보존과 호감도 0–4/세 영역 조건의 비어 있지 않은 후보를 확인했다. 이는 데이터 기반 도달성 확인이며 실제 전체 플레이 완료를 의미하지 않는다. [결과](../../outputs/sasha-active-dialogues-2026-11-02/data-checks.txt).
- 변경된 기존 Editor C# 4개가 생성 프로젝트에 포함된 상태에서 **MSBuild 종료 코드 0**. [로그](../../outputs/sasha-active-dialogues-2026-11-02/msbuild.log). Unity Editor 실행 검사·전체 회귀·수동 Play Mode·Player build는 실행하지 않았다. 사용자의 열린 Editor/씬과 실제 저장 슬롯은 조작하지 않았다.
- Doc Impact Check: SessionLog + 기존 DialogueNpcAffectionStructure의 활성/보존 후보 및 재활성 진입점 갱신. ErrorLog/DecisionLog 추가 변경 없음. Architecture/Contracts/Guides/Presentation HTML 변경 및 HTML stale 후보 없음. 이후 대사를 개편해 다시 켤 때는 NPC 후보와 공용 authoring 목록을 함께 갱신하면 된다.

## 필드 대사 말풍선 — 첨부 이미지와 색상 읽기 ON 적용

- 모드/범위: Implementation. 사용자가 기존/첨부 색상 읽기 ON 비교 자료를 검토한 뒤 해당 방식의 게임 반영을 승인했다. 호감도 대화 가능 표시가 아닌 공용 필드 대사 말풍선에만 적용한다. 직접 그린 외곽선·아래 음영과 기존 자동 외곽선을 함께 사용하는 승인된 모습을 유지한다.
- `Assets/_Project/Art/Sprites/UI/Dialogue/SpeechBubble.png`: 승인된 첨부 19×16px RGBA PNG의 바이트를 그대로 반영했다. 기존 PNG와 메타를 먼저 백업하고 PNG만 원자 교체했다. 기존 `.meta`의 GUID, Single, Point, 32 PPU, pivot, border L7/B6/R3/T3 및 투명도 수입 설정은 유지했다.
- `Assets/_Project/Prefabs/UI/SpeechBubble/SpeechBubblePrefab.prefab`: `backgroundTextureInfluence`만 0에서 1로 변경했다. 작업 전 파일과 바이트 비교에서도 이 값 하나만 다르다. 기존 sprite/material 참조, 기본 테마, UI 계층·레이아웃·글자 설정은 유지했다. 같은 프리팹을 사용하는 플레이어·보스·NPC에 공통 적용되며, 씬의 해당 필드 override는 발견되지 않았다.
- 셰이더·머티리얼·runtime C#·캐릭터별 SpeechData·씬·메타 파일은 수정하지 않았다. 기존 `SpeechBubble.ApplyTheme`가 프리팹의 영향값 1과 캐릭터별 색을 runtime 머티리얼에 적용한다. PNG의 어두운 선·음영에도 배경 테마색과 알파가 곱해지는 모습은 사전에 제시한 비교와 같다. 미리보기의 다른 권장 조합이나 자동 외곽선 OFF 방식은 적용하지 않았다.
- 검증: 반영한 PNG와 **수정하지 않은 실제 `.meta`**, 기존 셰이더·머티리얼 및 프리팹 설정을 격리 Unity 6000.4.2f1에서 import하고 검증용 C#를 컴파일했다. **23/23 검사 통과, 종료 코드 0**. 실제 Single sprite의 GUID/rect/border/pivot/PPU/Point/투명도 및 자동 외곽선 0.52를 확인했다. [네이티브 결과](../../outputs/field-speech-bubble-apply-2026-11-02/native-validation.txt), [Unity 로그](../../outputs/field-speech-bubble-apply-2026-11-02/unity-native.log).
- 기본·슬라임·드래곤·마녀·마왕 5종 × 밝고 어두운 배경의 네이티브 렌더 **10장 모두 승인한 색상 읽기 ON 미리보기와 픽셀 차이 0**이다. [비교 결과](../../outputs/field-speech-bubble-apply-2026-11-02/approved-render-comparison.json). 검증용 소스·렌더·백업과 보존 결과는 `outputs/field-speech-bubble-apply-2026-11-02/`에 보관하고, 결과 보존 뒤 경로를 확인해 격리 `preview-project`만 정리했다.
- 보존 확인: 작업 전 스냅샷과 비교해 승인한 게임 자산 두 개 및 이 SessionLog만 변경했으며, 관련 17개 보호 파일의 해시를 유지했다. 프리팹 diff/공백과 이 항목의 Markdown 로컬 경로를 확인했다. [최종 결과](../../outputs/field-speech-bubble-apply-2026-11-02/final-checks.json).
- 남은 확인: 사용자의 열린 게임 Editor·씬·실제 저장 슬롯은 조작하지 않았다. 전체 게임 프로젝트 compile, 실제 SpeechBubble/TMP/ContentSizeFitter 및 카메라·URP·아틀라스 조건의 전체 수동 Play Mode, Player build는 이번에 실행하지 않았다. 이번 네이티브 검증은 반영 이미지의 import와 승인된 배경 셰이더 출력 일치에 한정한다. 게임 C# 변경이 없어 MSBuild는 실행하지 않았다.
- Doc Impact Check: SessionLog. 구조·소유권·직렬화 스키마 변경이 없어 StructureMemory/RefactorBacklog/ErrorLog/DecisionLog 및 Architecture/Contracts 갱신은 필요하지 않다. Presentation HTML 변경 및 HTML stale 후보 없음.


## 노션 상인 대사 — 활성/보류 분리 및 최신본 동기화

- 모드/범위: Implementation (문서 정리). 사용자가 지정한 기존 [상인 NPC 페이지](https://app.notion.com/p/40c3291840a480fd824afe15aa40867d)를 현재 프로젝트의 Ink 원문과 NPC 후보 설정에 맞춰 갱신했다.
- 현재 활성 4종 `affection_0_a`, `affection_0_b`, `affection_0_c`, `affection_1_c`를 앞에 모았다. 스피리 물건/동업 선언의 최신 대사와 선택지, 대사창 분리, Happy/Surprised 응답과 Normal 시작을 기록했다. 0-A의 응답은 각 2창, 나머지 활성 응답은 각 1창임을 구분했다.
- 기존 후보 `affection_1_a`, `affection_1_b`, `affection_2_a`, `affection_2_b`는 별도 보류·비활성 구역으로 이동하고 원문·선택지·기존 효과와 사용자가 조정한 표 너비를 보존했다. 현재 추첨에 포함되지 않는다는 안내를 붙였다.
- 운영 규칙과 후보 표에 스피리 영역 조건/균등 추첨, 0단계 3/2종과 1단계 이상 4/3종, 최대 호감도와 런당 사용 기회 유지, 네 대화로 4단계 보상까지 진행 가능함을 반영했다. 보상 전체, 안부·인벤 가득 참 대사, 구매 실패 반응은 원래 내용을 유지했다.
- 검증: 실제 Ink의 한국어 대사·선택지 51개가 정리본에 모두 포함됨을 확인하고 NPC 활성 목록 4개와 대조했다. 보류 원문과 관련 없는 보상/안부/구매 반응 구역의 보존을 확인했다. 5개 targeted update는 Notion API에서 오류·경고 없이 완료됐다. 도구 지침에 따라 성공 후 불필요한 재조회는 하지 않았다. 원본 이미지 업로드나 게임 파일 수정은 수행하지 않았다.
- 로컬 변경 파일은 이 SessionLog이며 목적은 외부 문서 갱신의 범위·검증 기록이다. Doc Impact Check: SessionLog. ErrorLog/DecisionLog 및 구조·아키텍처·계약 변경 없음. Presentation HTML 갱신 또는 stale 후보 없음. 문서 작업이므로 Unity 실행/빌드는 필요하지 않아 실행하지 않았다.


## Ink JSON 파일 잠금 후 Importing assets 대기 복구

- 모드/범위: Implementation. 사용자가 Unity 종료를 확인한 뒤, 상인 대사 JSON 저장 중 Windows 1224가 발생하면 임포트가 중단 상태에 남는 문제를 수정했다. 게임 씬·프리팹·대사 원문/JSON·설정·메타·asmdef는 변경하지 않았다.
- [InkCompiler.cs](../../Assets/Ink/Editor/Core/Compiler/InkCompiler.cs): `StartAssetEditing` 이후 구간을 `try/finally`로 감싸 `StopAssetEditing` 호출을 보장한다. JSON 저장의 `IOException`은 해당 파일의 기존 컴파일 오류 경로로 전달하여, 오류를 콘솔에 보고하면서 진행창 제거·자동 새로고침 재개·빌드/Play Mode 대기 상태 정리가 끝까지 실행되게 했다. 잠금 해제 후 성공적으로 재컴파일하면 이전 저장 오류도 지운다. 정상 파일의 출력 형식과 대사 컴파일 순서는 유지한다.
- [InkCompilerImportRecoveryRegression.cs](../../Tools/Validation/InkCompilerImportRecoveryRegression.cs): 게임 프로젝트 밖의 격리 Unity 프로젝트에서 실행하는 native 회귀 검사다. 정상 컴파일, 실제 memory-mapped JSON으로 Windows 1224 재현, 기존 JSON 보존, 실패 경로/오류 콘솔 보고, 실패 후 임포트·자동 새로고침·진행창·빌드·어셈블리 reload 상태 복구, 잠금 해제 후 UTF-8 대사 재컴파일, 기존 문법 오류 경로를 검증한다.
- 검증: Unity 6000.4.2f1 native **39/39 검사 통과, 종료 코드 0**. [결과](../../outputs/ink-import-recovery-2026-11-02/native-validation.txt), [Unity 로그](../../outputs/ink-import-recovery-2026-11-02/unity-native.log). 생성 `InkEditor.csproj`가 수정 소스를 포함하며 MSBuild 통과(오류 0, 기존 CS0618 경고 2). [빌드 로그](../../outputs/ink-import-recovery-2026-11-02/msbuild.log). 관련 보호 파일 10/10 SHA-256 유지, diff 공백 및 추가 Markdown 링크/경로 검사. 격리 테스트 첫 실행은 샌드박스의 Mono 네트워크 초기화 크래시로 테스트에 도달하지 못했고, 이어 검증 코드의 Path 이름 충돌과 상태 조회를 바로잡은 뒤 위 최종 실행을 통과했다.
- 남은 확인/한계: 사용자의 실제 게임 프로젝트 재실행·전체 게임 compile/Play Mode/Player build는 실행하지 않았다. 파일을 매핑한 주체는 확인하지 못했으며, 잠금 자체를 강제로 해제하거나 저장 재시도/원자적 교체를 추가하지 않았다. 잠금이 재발하면 컴파일 실패를 보고하므로 해당 잠금이 해제된 후 대사를 다시 컴파일해야 한다. Ink vendor 업데이트 시 로컬 오류 처리 패치를 보존하거나 동등한 upstream 수정을 확인한다.
- Doc Impact Check: SessionLog + ErrorLog. [오류 기억](../ErrorLog.md)에 원인과 회귀 검증을 기록했다. 구조·직렬화·게임플레이 계약 변경이 없어 StructureMemory/RefactorBacklog/DecisionLog/Architecture/Contracts 갱신은 필요하지 않다. Presentation HTML 변경 및 stale 후보 없음.


## 로비 금화 HUD에 다음 런 보존 금화 표시

- 모드/범위: Micro-fix, 사용자의 명확한 표시 변경 요청. 사망 후 로비에서도 보존 중인 금화가 보이도록 기존 금화 HUD의 projection만 바꿨다. 예: 보존값 100이면 로비에서 100, 다음 런 시작 후에는 실제 지급된 런 금화 100을 표시한다. 고정 100을 표시하는 방식은 아니며 실제 보존값이 없으면 0이다.
- `Assets/_Project/Runtime/UI/HUD/CurrencyUI.cs`: 금화 행은 런 밖에서 프로필 retainedRunGold를 읽고, 활성 런에서는 기존 currency event의 실제 잔액을 표시한다. 초기 표시/런 종료에도 같은 UpdateUI가 적용된다. 금화 행에 프로필 로드 이벤트 구독과 OnDestroy 해제를 추가해 슬롯 전환·재로드 후 다른 프로필의 숫자가 남지 않도록 했다. 마정석 행과 N0 숫자 형식은 유지했다.
- `Docs/StructureMemory/RunGoldAndShop.md`: 소비 가능한 런 잔액과 로비 보존 금화 표시를 구분하고, 기존 지급 owner와 새 profile-refresh 경로를 기록했다. DecisionLog는 사용자가 승인한 읽기 전용 로비 표시 규칙을 짧게 남겼고, 이 SessionLog는 실제 변경/검증/한계를 기록한다.
- 금화 지급·차감·보존량 계산, GameData/런 저장 DTO, GamePlayDataManager/RunSessionLifecycleService/RunSessionProgressCommitPolicy, CurrencyManager.GetGold의 비활성 런 0 반환은 변경하지 않았다. 따라서 HUD가 보존값을 소모하거나 중복 지급하지 않는다. 새 manager/UI hierarchy, scene/prefab, serialized field/GUID/asmdef 변경은 없다.
- 검증: 생성 UI.csproj가 변경 source를 포함하는 상태에서 MSBuild 종료 코드 0. [빌드](../../outputs/retained-gold-hud-2026-11-02/msbuild.log). namespace/assembly 접근, 초기·종료·시작·프로필 로드 호출 순서와 이벤트 해제를 검토했다. 저장/수명주기/통화 코드와 기존 UI meta의 작업 전 해시를 대조했다. [최종 검사](../../outputs/retained-gold-hud-2026-11-02/final-checks.txt).
- 남은 확인: 실제 사망→로비→다음 런의 Unity 화면/수동 Play Mode와 Player build는 이번에 실행하지 않았다. 실제 저장 슬롯과 열린 Editor/씬은 조작하지 않았다. 현재 데이터에 보존 금화가 저장되지 않은 별도 문제가 있다면 표시값은 그대로 0이므로, 이번 작업은 금화 이월 자체를 성공했다고 검증한 것은 아니다.
- Doc Impact Check: SessionLog + 기존 StructureMemory + DecisionLog. ErrorLog 추가 변경 없음. Architecture/Contracts/Guides/Presentation HTML 변경 및 HTML stale 후보 없음.


## 사샤 금화 보존 상한 500 상향

- 모드/범위: Micro-fix (보상 밸런스). 사용자가 다음 런의 이익을 늘리기 위해 2단계 금화 보존 상한을 100에서 500으로 올리도록 요청했다. 사망/시간 초과 시 보유 금화와 500 중 작은 금액을 보존한다. 이미 저장된 이월금은 실제 보존액 그대로 유지하며 실제 저장 슬롯을 수정하지 않았다.
- `Assets/_Project/Data/Dialogue/Affection/SashaGoldRetention.asset`: 기존 goldRetentionLimit을 500으로 바꾸고 fallback 보상 설명을 맞췄다. 기존 GUID/meta/아이콘/다른 필드는 유지했다.
- `Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsAuthoring.cs`: 공용 setup의 기본값과 5개 언어 설명도 500으로 맞춰 재실행이 100으로 되돌리지 않도록 했다.
- `Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsValidation.cs`: 해금 단계별 기대 상한과 기존 commit/one-shot 검증 입력을 500으로 맞추고 300/499/500/501/800 보유량을 추가했다. 이 검증 도구 전체를 이번에 실행한 것은 아니며 컴파일을 확인했다. 가격 계산/확률 검증의 관련 없는 100 상수는 유지했다.
- `DataSheets/Localization/GameText.csv`, `AssetInventory.json`, `Assets/_Project/Data/Localization/GameText_ko.asset`, `GameText_en.asset`, `GameText_ja.asset`, `GameText_zh-Hans.asset`, `GameText_zh-Hant.asset`: 기존 rewardText 한 entry의 숫자만 500으로 맞췄다. 기존 key/ID/metadata 및 다른 row의 바이트를 유지했다. Shared Data와 generated key map은 변경하지 않았다.
- [Notion 상인 페이지](https://app.notion.com/p/40c3291840a480fd824afe15aa40867d): 2단계 보상과 적용 메모의 세 구역을 500으로 갱신했다. 300 보유 시 300, 700 보유 시 500 예시와 로비 표시/기존 이월금 유지도 기록했다. Targeted update는 오류·경고 없이 완료됐다. 다른 대사/보상 구역은 편집하지 않았다.
- RunGoldAndShop와 DialogueNpcAffectionStructure의 현재 수치를 500으로 맞추고, 기존 ActiveTask에 후속 튜닝을 덧붙였다. DecisionLog는 100에서 500으로 바뀐 승인 수치를 기록하고, 이 SessionLog는 실제 변경/검증/한계를 기록한다. 과거 날짜의 100 보존 테스트 영수증은 역사적 기록으로 보존했다.
- 검증: 설정/5개 언어/보존 검사 52개 통과. [결과](../../outputs/retained-gold-500-2026-11-02/data-checks.txt). 변경된 Editor source 두 개가 생성 프로젝트에 포함된 상태에서 MSBuild 종료 코드 0. [빌드](../../outputs/retained-gold-500-2026-11-02/msbuild.log). 런 commit/min 계산/단발 지급/소비 가능 잔액/HUD projection과 기존 metadata의 해시가 유지됨을 확인했다. 새 runtime 코드/저장 스키마/씬/프리팹/Manager를 추가하지 않았다.
- 남은 확인: 실제 사망→로비→다음 런의 Unity 실행, 전체 보상 회귀와 Player build는 실행하지 않았다. RunModifierService는 집계 결과를 캐시하므로 수정 당시 플레이 중이었다면 Play Mode를 재시작해 새 설정을 로드한 뒤 확인한다. 이미 보존한 100이 자동으로 500이 되는 동작은 아니다.
- Doc Impact Check: SessionLog + 기존 StructureMemory + DecisionLog 및 기존 ActiveTask 후속 메모. ErrorLog 추가 변경 없음. Architecture/Contracts/Guides/Presentation HTML 변경 및 HTML stale 후보 없음.
