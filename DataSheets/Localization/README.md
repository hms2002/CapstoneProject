# 키 기반 번역 입력

`GameText.csv`: key,ko,en,ja,zh-Hans,zh-Hant. 1,803개 고정 키, 한국어·영어·일본어·중국어 간체·번체. 외국어는 검수 전 초안이다. 수치, 저장 ID, 분기 식별자는 번역하지 않는다.

## 편집과 적용

- 문장/행 순서 변경 시에도 키를 유지한다. UTF-8 CSV 저장 후 Unity Tools > Localization > Import GameText CSV로 가져온다. 헤더·중복·빈 값·값 토큰을 먼저 검사한다. CSV에서 사라진 키는 기존 테이블에서 자동 삭제하지 않는다.
- 런타임은 Unity Localization String Tables를 조회한다. CSV/한국어 문장 검색 사전을 읽지 않는다. 초기화 전/미등록 키는 자산 원문을 표시한다.
- CommonTranslations.tsv와 Tools/Localization/expand_*.py는 원문→키 저작 마이그레이션용이다. 번역 수정의 기준은 CSV다. audit_translation.py로 키/인자/용어 링크를 검사한다.
- AssetInventory.json은 표시 자산 필드, StaticUiBindings.json은 고정 UI 경로, InkInventory.json은 대사/선택지 키 대응표다. Inventory.json은 동적 수치/임시 문구도 포함한 읽기 전용 조사 결과다.
- SourceAudit.json에는 개발 로그, 에디터 라벨, 구성 오류, 언어 자체 표기와 원문 fallback 조각이 남는다. 후보 개수와 플레이어 미번역 개수는 다르다. 새 콘텐츠에는 별도 키를 추가해야 한다.

## 언어와 폰트

기존 설정의 언어 선택을 사용한다. Korean=0, English=1, Japanese=2, SimplifiedChinese=3, TraditionalChinese=4. Auto 없음. 유효한 저장값 우선, 최초 선택은 Steam 게임 언어 → OS 언어 → 영어. 실제 App ID 없는 Editor는 Steam 초기화를 생략한다. [Steam 연결](../../Docs/StructureMemory/SteamIntegration.md).

Unity Localization SelectedLocale가 언어 상태를 소유한다. 설정·아이템 상세·Ink는 locale 변경 시 표시를 갱신하며 프리뷰 레벨과 대사 진행/선택 상태를 유지한다. 고정 UI는 LocalizeStringEvent에 연결했다.

GameFonts Asset Tables와 LocalizeTmpFontEvent로 KO/EN 원래 폰트, JA Galmuri9 Japanese SDF, 간체·번체 Fusion Pixel 12px proportional 지역별 폰트를 선택한다. Fusion Pixel 10px는 현재 간체 문자의 U+89D0가 없어 12px를 사용한다. 12px는 설계 격자 크기로 기존 TMP fontSize를 바꾸지 않는다. 동적 TMP atlas 사용. 원본 폰트/라이선스/출처는 Assets/_Project/Data/Localization/Fonts/ 및 Sources.json. 실제 줄바꿈·넘침·스타일은 플레이 검수가 필요하다.

폰트 이벤트는 TMP set_font 대신 LocalizeTmpFontEvent.ApplyLocalizedFont에 연결한다. 저작 머티리얼의 Outline·색상·FaceDilate를 TMP의 공식 fallback material API로 유지하며 대상 폰트 atlas를 사용한다. 설정을 바꾸면 저작 머티리얼 참조도 확인한다. NPC 이름, 스탯 패널, 도감과 아이템 상세는 언어 변경 시 현재 표시를 갱신한다.

저작 머티리얼은 한국어 원본 프리셋이어야 한다. 언어 미리보기에서 교체된 머티리얼을 기준으로 저장하면 Outline=0도 검사에 통과할 수 있다. CJK 기본 머티리얼도 한국어 BlackOutline 설정을 사용하며 각 언어 atlas는 유지한다. 대화창 NPC 이름 갱신과 월드 이름표 갱신은 별도이며, 월드 이름표/상점 횟수 표시의 초기화·언어 전환 누락은 후속 수정 대상으로 확인됐다. SOLD는 사용자 요청으로 번역하지 않는다.

## 자산·수치·Ink

- 자산 키는 GUID/직렬화 필드 경로 기반이다. GameTextAssetKeys.g.cs의 런타임 대응표는 타입/자산 이름/필드로 조회한다. 이름이나 중첩 배열 순서를 변경하면 inventory/대응표를 갱신한다. 중복 식별자는 생성 시 오류다.
- 상호작용 안내는 interaction.<타입>.<필드> 키. 직렬화 필드는 fallback으로 유지한다. 같은 타입에 의미가 다른 커스텀 안내를 저작할 때는 추가 키를 연결해야 한다.
- {threshold}, {0:0.#}, {remaining_runs}, {val:...} 등 수치/강조 토큰 유지. [[용어ID]]도 유지하고 term.<ID>로 표시명만 번역한다. 게임플레이 식별/수치 분류에는 원래 ID를 사용한다.
- 동적으로 조합되는 스탯 용어도 별도 term 키가 필요하다. repair_display_coverage.py로 자산 인벤토리 기반 58개 용어 표시 키와 번개 창 변형 설명 4개 키를 추가했다. 새 콘텐츠는 실제 조합 결과까지 확인한다.
- add_display_omission_keys.py는 발동 조건 7필드와 안내/유물 fallback/튜토리얼 페이지 키를 보충한다. expand_asset_keys.py로 604필드 대응표를 재생성한다. 튜토리얼 키는 `tutorial.info.<기존 tutorialId>.page.<0부터 시작하는 페이지 번호>.title/body`다. 페이지 추가·순서 변경 시 번역 키도 함께 갱신한다.
- NPCData가 없는 이름표는 도감의 명시적 키 또는 `world.nameplate.<interactionAnchor 부모 오브젝트 이름>`을 사용한다. 현재 TeleportNPC/ConstructionNpc 두 이름을 등록했다. 저작 오브젝트 이름이 바뀌면 키도 갱신한다. SOLD는 사용자 요청에 따라 번역하지 않는다.
- 길게 누르기 강조는 각 언어의 튜토리얼 번역 템플릿에 TMP color 태그로 저작한다. 번역문 일부를 런타임에 한국어로 재삽입하지 않는다.
- 활성 Ink 16파일의 162대사/선택지에 loc 태그. 언어별 Ink 복사본 없음. 원문/분기/효과 태그를 유지하고 공식 Ink 컴파일러로 JSON 갱신.
- FullInkLocalizationCompile.RunBatch는 캐시 핸들을 해제하고 임시 파일 교체로 Windows memory-mapped JSON 덮어쓰기 오류를 피한다.

## 검증

FullLocalizationValidation.RunBatch: CSV import, 모든 테이블 값, CJK 글리프, 실제 씬/프리팹 text/font listener와 다섯 언어 setter, 언어 매핑·저장 enum·fallback·유물 레벨 값·Ink. BuildAddressablesBatch는 콘텐츠 빌드 검사다. [실제 실행 결과](../../Docs/SessionLogs/2026-10-03.md).

Play Mode, 저장 후 재실행, 빠른 언어 전환, 실제 화면 넘침, 전체 Player 빌드는 별도 확인이 필요하다. 배치 문자열/글리프 검사만으로 보장되지 않는다.

## 게임 흐름 검토와 타이틀 소유권

[게임 흐름별 커버리지](../../Docs/StructureMemory/LocalizationCoverage.md)에 정상/실패/초기화/언어 전환/재실행 검토를 정리했다. FlowSourceAudit.json은 Unicode escape/const/영어를 포함한 원문 후보이고, FlowAssetAudit.json은 별도 Speech/Route/Intro/Outro 저작 데이터 조사다. 후보 수는 누락 수가 아니다. 추가 확인된 11경로는 아직 수정 완료가 아니다.

타이틀 슬롯의 고정 헤더 3개는 String Event, 카드의 제목/수치/상태/시작·계속 버튼은 카드와 패널 코드가 표시를 소유한다. 동적 칸의 고정 String Event 27개를 제거하여 언어 이벤트가 수치를 저작 캡션으로 덮지 않도록 했다. StaticUiBindings 현재 468경로. inventory 재생성 시 이 런타임 소유 칸을 고정 번역으로 재등록하지 않는다. 빈 값이 아니고 한글이라는 이유만으로 고정 UI로 분류하면 안 된다.

## 게임 흐름 누락 보완 (2026-10-04)

- `FlowNewAssetFields.json`은 Speech 56문장, Intro/Outro 8슬라이드, Route 8장소명에 대한 Unity API 읽기 결과다. `FlowTranslations.json`/`FlowFixedTranslations.json`은 이번 최초 추가의 번역 초안이며, 이후 수정 기준은 `GameText.csv`다.
- `add_flow_omission_keys.py`는 72자산 키와 18안내 키를 최초 보충한다. 기존 CSV 키의 번역을 덮어쓰지 않는다. `expand_asset_keys.py` 대응표는 현재 676필드다. 배열 순서 변경 시 기존 키/인덱스 대응을 함께 검토한다.
- Speech는 기존 무작위 선택 결과의 필드 키를 조회한다. 사망 화면은 선택된 문구 키와 원인 키를 일시적으로 유지하여 언어 전환 시 재추첨하지 않는다. Intro/Outro는 표시 getter만 번역하며 저작 슬라이드 데이터/대기 설정은 유지한다.
- `FullLocalizationAuthoring.ApplyFlowBindingsBatch`는 Items/Chests 4프리팹의 기존 TMP에 폰트 이벤트만 연결한다. 잔여 적 텍스트는 ChestMonsterKillLockView가 소유한다.
- 배치 확인: 9,015테이블 값, 468고정 라벨/1,901폰트 연결, 기존 1,500동적 표시/684타이틀 검사와 추가 504흐름 표시 검사, 9,076기반 검사. 실제 Play Mode 전체 정상/실패 분기 및 시각 검수는 별도다.
