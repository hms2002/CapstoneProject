# 게임 흐름별 번역 커버리지 검토

2026-10-03 조사, 2026-10-04 구현 상태 보완. 현재 구현의 조사 지도이며 계약이 아니다. 범위: 첫 실행 → 타이틀/프로필 → 튜토리얼 → 허브 → 던전/전투 → 보상/성장 → 보스 → 사망/승리 → 엔딩 → 재실행. 숫자·밸런스·저장 ID·분기 ID·SOLD는 번역하지 않는다.

## 증거와 상태 구분

- **확인된 누락**: 실제 출력까지 이어지는 코드에서 원문을 그대로 전달하며 자산에 표시 문구가 있음. 모든 분기를 직접 플레이했다는 뜻은 아니다.
- **키 연결 확인**: 출력 시 번역 조회를 확인함. 실제 화면/시작 시점/빠른 언어 전환 완료를 뜻하지 않는다.
- **검증 필요**: 초기화·동적 생성·예외 분기·이미지 글자·레이아웃 등 실행 확인이 남음.
- `Tools/Localization/audit_flow_sources.py`: 런타임 1,377파일, 문자열 2,376개 분류, 후보 1,145개. 후보에는 개발 로그/식별자/메타데이터/이미 번역된 fallback이 포함되므로 누락 수가 아니다. 주석/문자 리터럴 제외, `\uXXXX` 해석, 영어 표시 후보 포함. 보간식/사용 경로는 사람이 검토해야 한다.
- [FlowSourceAudit](../../DataSheets/Localization/FlowSourceAudit.json), [FlowAssetAudit](../../DataSheets/Localization/FlowAssetAudit.json). 자산은 읽기 전용으로 조사했다. 코드 리터럴 검색만으로 직렬화된 문장을 찾을 수 없다.
- 이전 검증은 604자산 필드, 고정 UI와 글리프, 실제 ItemDatabase 유물 모든 레벨, NPC/무기/튜토리얼 표시를 검사한다. 이번 조사에서 제외됐던 출력 경로가 추가로 발견되어 전체 번역 완료 판정은 보류한다.

## 순서대로 확인할 흐름

| 구간 | 확인할 모든 표시/분기 | 현재 판정과 남은 검증 |
|---|---|---|
| 첫 실행/재실행 | 저장 언어 있음/없음, Steam 성공/실패, OS 5언어/미지원, 초기 테이블/폰트 준비 전, 저장 후 재실행 | 기존 Steam→OS→영어/저장 우선 매핑 있음. 실제 Steam App ID/새 저장/재실행 미검증. fallback이 나온 화면은 초기화 완료 후 재표시해야 함 |
| 타이틀/프로필 | 시작/설정/종료, 슬롯 1~3, 빈/대기/진행 슬롯, 시간/업그레이드/마정석/클리어, 계속/시작/삭제, 삭제 확인/취소/실패, 화면 재열기 | 4제목이 런타임 const로 덮어써지고 동적 값 27칸에 고정 String Event가 중복 연결되어 있으며 슬롯 제목 3개는 별도 고정 라벨인데 inventory 누락. 이번 수정 대상. 선택 칼은 실제 글자 왼쪽 기준. 디버그 슬롯 커스텀 값은 별도 원문 경로 |
| 설정/키 설정 | 언어/해상도/창 모드/음량/적용/복원, 키 변경/충돌/초기화, 패드/키보드 표기 | 기존 키 조회/고정 바인딩 있음. 언어 자체 이름은 한국어/English/日本語/简体中文/繁體中文 유지. 적용/취소 후 표시, 재매핑된 키와 설명 폭 확인 |
| 첫 시작 인트로 | IntroSequence_Default 슬라이드/타이핑/넘기기/홀드 스킵/완료 후 튜토리얼 | 4슬라이드 키 getter/표시 경로 수정 및 배치 확인. 실제 타이핑/홀드/완료 전환 Play Mode 필요 |
| 튜토리얼 | 이동/공격/대시/스킬/상자/게이트 4종류 8페이지, 길게 누르기/다음/닫기, 진행 퀘스트, 마왕 대사, 연출용 사망/추락, 허브로 이동 | 페이지와 유지된 page/hold 상태 배치 검증 있음. 연출용 게임오버 원문 default는 출력 때 키 조회하므로 그 자체는 누락 아님. 조작키 WASD/SPACE/F/Q는 저작 고정값이며 재매핑 안내 후속 필요 |
| 허브/NPC | 월드 이름/아이콘, 상호작용, 첫 대화/재방문/선택지, 호감도/공사 비용/남은 일수, 도감/업그레이드, 무기 없는 출발 실패, 첫 상점 영업 | NPCData/Ink/공사 nested getter/무기 출발 실패 키 확인. 첫 영업 말풍선 키 연결 완료. 이름표 원문 fallback·초기 폰트 준비·오브젝트 이름 변경은 확인 필요 |
| 던전/HUD/미니맵 | 메인/서브 퀘스트, 보스 토벌 0~3/완료, 소포 상태, 맵 위치/방 이름, 잠김/열림 문·포탈·레버·호감도/제물 실패 | 퀘스트/일반 안내 키 확인. RouteSet 8장소명 실제 getter/조회 배치 확인, 미니맵 텍스트 갱신 연결. 열린 상태 실제 전환 검수 필요 |
| 전투 | 일반/보스 이름, 체력/스탯/상태명/속성/스킬, 피해·DPS·누적, 보스 공격/피격/사망/페이즈/궁극기 말풍선, 플레이어 문 잠김/가방/마나 실패 | 스탯 getter/더미 표시 키 확인. BossSpeechData/PlayerSpeechData.GetLine 56후보의 실제 조회와 RNG 소비 배치 확인. 실제 말풍선/발생 분기 Play Mode 필요 |
| 상자/드롭/상점 | 상자 열기/남은 적/해제, 선택 0~2/확정/취소/반환, 공간 부족/최대 레벨/유물 변경 사망 경고, 드롭 이름/획득/버리기, 구매/재화 부족/가방 가득/새로고침/남은 횟수/SOLD | 상자 잔여 수/4폰트 연결 배치 확인. 상점 구매 실패 말풍선 2개 키 연결 완료; 실제 실패 조건 플레이 필요. 공간 부족 팝업은 기존 키 확인. SOLD 의도적 제외 |
| 인벤토리/도감/상세 | 이름/희귀도/레벨/스토리, 기본 공격/스킬 1/2, 간단↔자세한 설명, 무기 변형, 유물 모든 레벨/조건/전용 무기/수치, 소모품, 스탯 섹션/최대 체력, 용어 클릭, 빈/미해금/정보 없음, 가져오기/버리기 | 이전 누락 수정/유물 모든 레벨 배치 검증 있음. 도감 서브탭 제목 3개 preset 우회 수정/실제 prefab 배치 확인. 숨겨진 모드 전환 fallback은 잠재 경로. 상세/도감/상점/드롭 동일 아이템 교차 확인 필요 |
| 레벨업/업그레이드/호감도 | 카드 제목/효과/조건/남은 리롤, 선택 불가/이미 열림/전투 중 차단, 업그레이드 잠김/구매/최대/부족/선행 조건, 공사/호감도 보상 | 카드/리롤/업그레이드 표시 키 확인. 전투 중 레벨업 차단 사유/Reject 키 연결 완료. 실제 실패 팝업 분기 테스트 필요 |
| 맵 이벤트 | 종 사용/사용됨/구성 오류/전투 완료, 운동기구 3종/보상 실패/이미 사용, 소포 획득 횟수/공간 부족/배송/배송 지점 없음/보상 실패, 지름길/제물/보스 조건 | 현재 존재하는 화면은 보류된 CSV 데이터화와 무관하게 번역 대상. 검토한 상호작용/팝업은 GameText 키 확인. 실제 저작 커스텀 문구가 같은 타입의 공통 키에 덮이지 않는지 확인 |
| 사망/시간초과/승리 | 무작위 몬스터 사망 문구, 원인 이름, 함정/불 장판/시간 초과, 장소/남은 시간/보상, 인벤토리/허브 복귀, 승리 전용 제목/메시지 | 일반 형식은 키 연결. 정상 승리 override/장소/일부 원인 fallback 키 연결과 승리 표시/사망 문구 캐시 배치 확인. 커스텀 override와 실제 원인별 사망 플레이는 별도 |
| 엔딩/복귀 | 4슬라이드 텍스트, 타이핑/넘기기/홀드 스킵, 다시 타이틀/허브, 재시작 | 4슬라이드 키 getter/표시 경로 수정 및 배치 확인. Ink 경로가 아니며 실제 타이핑/홀드·엔딩 완료 Play Mode 필요 |
| 로딩/이미지 글자 | 씬 전환 Loading..., 팁, 타이틀 로고·표지·버튼 스프라이트 안 글자 | compact Loading 의미 키 연결 완료(SOLD 제외 승인과 별개). 팁/진단 줄은 현재 숨겨지는 경로로 활성화 정책 확인. 스프라이트 안 글자는 String Table 검색으로 잡히지 않으며 Asset Table/별도 그림 검토 필요 |

## 2026-10-03 확인된 추가 누락과 수정 지점 (11그룹 수정 완료)

1. `Presentation/Loot/ChestMonsterKillLockView.RefreshText`: lockedFormat를 직접 string.Format. 명시적 키 + 현재 count로 조합하고 잠금 상태/파티클을 건드리지 않고 텍스트만 언어 갱신. Items/Chests 프리팹 4개에서 컴포넌트 존재 확인; 실제 드롭 선택률/씬 사용 여부는 별도.
2. `Features/Dialogue/NPC/Merchant/MerchantNPC.SpeakFailure`: 재화 부족/가방 가득 2개 원문을 Speak. 실패 타입마다 고정 키로 번역한 다음 전달. 공통 WarningPopup과 다른 표시 경로다.
3. `Presentation/Dialogue/NPC/Merchant/MerchantActivationCinematic`: merchantSpeechText를 직접 Speak. 첫 영업 연출용 키 배정. Unicode escape로 저장되어 육안 한글 검색에서 빠질 수 있음.
4. `UI/Encyclopedia/EncyclopediaItemTab.RefreshTitle`: 번역은 preset.text가 비어 있을 때만 실행. 실제 EncyclopediaUI 프리팹에는 무기/유물/소모품이 모두 채워져 있음. 세 제목의 출력에 항상 키 조회하고 preset는 fallback으로 유지.
5. `Features/Progression/Leveling/Rewards/LevelRewardSessionController.EvaluateOpenEligibility/Reject`: 전투 중 combatBlockedMessage 원문을 반환/팝업. 명시적 키를 거친 동일 표시 사유를 사용; 전투/세션 조건 유지.
6. `Core/Presentation/Speech/BossSpeechData.GetLine`, `PlayerSpeechData.GetLine`: entries/lines에서 무작위 원문 반환. 자산 5개/56개 저작 문장(보스 48/플레이어 8). 선택된 entry/line 인덱스를 유지한 GUID+필드 키 조회; 재번역 때문에 Random을 다시 호출하지 않음. BossSpeechController, PlayerSpeechController, SlimeQueenBossBase 직접 경로까지 함께 확인.
7. `Features/Map/Routes/CorridorBossRouteSetSO.TryResolveLocationName`: 실제 4 RouteSet의 corridorLocationName/bossLocationName 원문. Display getter를 키 조회하고 TryResolve에도 그 getter 사용. sceneName/themeId/entryPointId는 그대로 유지. 미니맵과 게임오버 공통 영향.
8. `Features/Player/Health/GameOverPresentationPlayback.GameOverPresentationRequest`: 승리 제목/메시지 Unicode const 및 장소 fallback 원문. 요청을 만들 때 표시 키를 배정하고 raw 식별/사망 분류와 분리. UI의 번역된 형식만으로 override가 번역되지 않음.
9. `Features/Progression/Ending/EndingOutroSequenceSO/EndingOutroPlayer`: 4슬라이드 중첩 text에 키 lookup 없음. sequence/slide index의 기존 필드 경로로 번역 조회. ScriptableObject 저장 스키마 변경 필요 없이 표시 getter 추가 가능.
10. `UI/Title/TitleIntroSequenceSO/TitleIntroPlayer`: 첫 시작 IntroSequence_Default의 슬라이드도 원문 Text getter. 엔딩과 같은 형태지만 별도 타입/데이터이므로 독립 키/표시 검증 필요.
11. `Infrastructure/Loading/LoadingOverlayController.UpdateCompactLoadingText`: Loading+점 애니메이션 원문. Loading 의미 키만 조회하고 점 개수/로딩 진행 유지. 숨긴 디버그 상태 줄/미사용 팁은 번역 누락과 구분.

## 잠재 누락과 공통 회귀 조건

- WeaponAbilityBlockView.SetVariantSwitchGuide의 빈 label fallback은 한국어. 현재 발견한 호출은 모두 visible=false이므로 사용자 화면 확정 누락으로 세지 않는다. 활성 호출 추가 시 고정 키 필요.
- TitleProfileSlotDebugState에서 hasProfile=true이고 커스텀 값이 비어 있지 않으면 원문 override 유지. 현재 정상 저장 흐름과 구분하며 디버그 데이터 사용 시 검수.
- GameText.Asset는 타입/이름/필드 대응이 없으면 원문 반환. 새 자산, 이름 변경, Clone, 중첩 배열 순서 변경, 새 ManagedReference 하위 필드가 inventory에 포함됐는지 검사. 빈 번역표만 검사해서는 잡히지 않는다.
- 초기화 전 GameText.Get은 한국어 fallback. OnEnable/Start에만 한 번 설정되는 라벨은 준비 후 갱신 필요. 테이블 로드 완료와 폰트 이벤트 완료 순서가 다를 수 있어 일시적 missing glyph 경고와 영구 연결 누락을 구별해야 함.
- 언어 전환은 대사 Continue/Choose/Random/아이템 선택/유물 레벨/튜토리얼 홀드/삭제 확인/재구매를 다시 실행하지 않고 표시만 갱신해야 한다.
- 가변 글자 수: 5언어×주요 화면에서 줄바꿈/잘림/AutoSize 최소 크기/Outline/FaceDilate/색상/링크 범위/선택 아이콘/말풍선/스크롤/버튼 폭을 확인. 일본어는 현재 Galmuri9가 아니라 Noto Sans CJK JP; 중국어는 Fusion Pixel 12px 지역별. 폰트 이름과 pixel/SDF 샘플링은 별개.
- 아이템은 동일 항목을 상점/상자/인벤토리/도감/드롭에서 각각 검수. 단순 설명/자세한 설명/변형/레벨 증가/최대 레벨/빈 템플릿/전용 무기 제한을 나눠 확인.
- 모든 무작위 사망 문구/상황별 대사는 각 후보를 결정적으로 전부 검사하고 플레이 검증도 보완. 한 번 실행해 나온 문장만 보는 방식은 부족하다.
- 배포에서 열 수 있는 DemoCheat/LoadingDebug 메뉴는 개발 화면 제외 정책 재확인 필요. 개발 로그/Inspector Header/Tooltip/씬 ID/포트레이트 감정 Normal은 일반 번역 대상이 아님.
- SOLD, 숫자/%, Lv/DPS, 실제 입력 키 표기와 용어 내부 ID는 별도로 판단. 영어가 있다는 이유로 전부 번역하거나 전부 제외하지 않는다.

## 다음 수정과 완료 판정

순차 작업: (1) 확인된 원문 passthrough에 키 연결 (2) Speech/Route/Ending nested 필드 inventory/번역 보충 (3) 초기화/언어 전환 표시 갱신 (4) 실제 사용 프리팹 폰트/스타일 readback (5) 모든 데이터/후보 결정적 표시 검사 (6) 위 흐름의 정상/실패 분기를 5언어 Play Mode로 검수 (7) 저장 후 재실행/Player Addressables 포함 검증.

2026-10-04 사용자의 구현 승인에 따라 추가 발견 11그룹을 수정하고 배치 검증했다. 위 게임 흐름 표는 현재 구현 상태와 남은 검증을 반영한다. 번호 목록은 2026-10-03 발견 원인/수정 방향 기록이며 아래 결과와 함께 본다. 실제 게임 전체 플레이 완료로 오인하지 않는다. [현재 구현](LocalizationFlow.md), [작업 범위](../ActiveTasks/full-game-localization.md), [세션 결과](../SessionLogs/2026-10-03.md).

Doc Impact: StructureMemory + SessionLog + ErrorLog. Architecture/Contracts/Presentation HTML 변경 없음; 이번 지도 추가로 HTML stale 후보를 만들지 않음.

## 2026-10-04 구현과 확인 결과

11그룹 모두 키 조회를 연결했다. 추가 90키(72자산 + 18안내), 총 1,803키×5언어/676자산 필드. Speech 56문장, Intro/Outro 8슬라이드, RouteSet 8장소명을 실제 getter로 확인했다. 상자 4프리팹에 폰트 연결을 추가했고, 도감 제목/상자/미니맵/슬라이드/정상 사망·승리 표시의 언어 갱신을 연결했다. 사망 원인/문구 키는 요청 및 UI의 일시적 표시 문맥이며 저장 스키마에 추가하지 않는다.

Unity 컴파일, 9,015번역 값/CJK 글리프, 468고정 라벨/1,901폰트 readback, 기존 1,500동적 표시와 684타이틀 검사, 추가 504흐름 투영, 9,076기반 검사 통과. MSBuild exit0. 개별 대사의 1회 RNG 소비와 사망 문구 재추첨 방지도 확인했다. Ink 16소스 원문/분기/효과 보존과 값 토큰 parity 통과.

미실행: 실제 새 저장 시작→인트로→튜토리얼→허브→던전/보스→사망/승리→엔딩의 정상/실패 분기 Play Mode, 빠른 언어 전환과 재실행, 전체 Player 빌드/실제 Steam ID. 슬라이드 타이핑/홀드·미니맵을 열어 둔 전환은 코드 검토 완료이며 실제 프레임 검사와 다르다. 자유 문자열 커스텀 game-over override, 숨긴 디버그 경로, 스프라이트 안 글자는 기존 별도 검토 항목으로 남는다. 외국어 표현도 원어민 검수 전 초안이다. [구현 기록](../SessionLogs/2026-10-04.md).
## 2026-10-04 실제 화면 검수 추가

일본어 JA font table은 Noto에서 공식 Galmuri9 Japanese SDF로 교체했다. 현재 일본어 1,062고유 문자의 glyph 검사가 통과했으며 KO material preset/중국어 Fusion Pixel은 유지한다. 실제 씬별 Play Mode에서 11대표 화면 상태×5언어=55 PNG를 캡처했다. [검수 범위·화면](../../outputs/localization-screen-review-2026-10-04/README.md)과 [세션 기록](../SessionLogs/2026-10-04.md)을 확인한다.

- TitleScene: 타이틀/저장 슬롯/설정. 일본어 픽셀 글꼴, 슬롯 제목 fit, 선택 칼이 locale별 실제 글자 앞에 오는 것 확인.
- ProtoTypeHub: 허브/인벤토리/도감/대표 무기 간단·상세/대표 유물. 스킬명의 마지막 한 글자 줄바꿈, stat title 줄바꿈/행 폭 확장, 영어 설명 전환 안내와 키 아이콘 간격을 수정 후 확인. 본문/실시간 숫자는 자동 크기 조정 대상 밖이다.
- TutorialCorridor: 시작 화면/실제 authored info 요청 첫 페이지. 일본어/중국어/영어 문구/픽셀 글꼴 확인. 모든 tutorial 페이지/홀드 입력/클리어를 플레이한 것은 아니다.
- 오른쪽 위 타이머의 `15:00` 문자열은 정상이나 TMP Outline이 남긴 이전 CanvasRenderer atlas override가 숫자를 깨뜨렸다. LocalizeTmpFontEvent가 renderer atlas도 바꾸도록 수정하여 실제 정상/정지 타이머를 확인했다.

캡처는 기존 컨트롤러를 통한 실제 렌더링이며 전체 입력·정상 씬 전환·등록 데이터 전체의 시각 검수와 구분한다. 아래 전체 게임 흐름의 미검증 항목은 대표 화면 캡처만으로 완료 처리하지 않는다. 외국어는 여전히 원어민 감수 전 초안이다.

Suggested Later: 실제 ja-encyclopedia.png에서 무기 제목과 상세 설명 전환 안내의 위치 겹침을 확인했다. 안내 label fit만으로 해결되지 않는 authored 위치 문제이며, 도감 헤더/안내 RectTransform 배치를 별도 수정·재검수해야 한다. 이 화면을 레이아웃 합격으로 판정하지 않는다.

