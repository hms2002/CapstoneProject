# Localization Flow

2026-10-03. 공식 계약이 아닌 현재 구현 지도.

## 소유권과 입력

- 기존 GameSettingsService: 5개 언어/settings.language 저장, Korean=0 유지, Auto 없음. 저장값 우선; 최초 Steam → OS → 영어. Steam 수명은 기존 RuntimeServices의 SteamPlatformService 소유. [SteamIntegration](SteamIntegration.md).
- Unity Localization 1.5.13: SelectedLocale/테이블/로딩 소유. 새 Manager/Singleton 없음.
- DataSheets/Localization/GameText.csv: 1,803키×5언어. LocalizationSheetSetup 사전검증 후 공식 importer. TSV는 저작 마이그레이션 사전이며 런타임 문장 대조 없음.
- Assets/_Project/Data/Localization/: Locales, GameText, GameFonts, Shared Data, locale별 Galmuri9/Fusion Pixel 폰트 및 이전 Noto 자산. Addressables 그룹과 승인된 패키지 참조/Localization 설정 등록.

## 표시 경로

- Core GameText.Get/Format: 고정 키, 초기화 전/미등록 원문 fallback. Asset 조회는 generated GameTextAssetKeys.g.cs의 타입/이름/필드 대응표. 키는 GUID/필드 기반. Component 이름의 마지막 (Clone)만 제거. 이름/중첩 배열 순서 변경 시 재생성 필요.
- Weapon/Relic/Consumable/Ability/Attribute/Upgrade/LevelReward/NPC 표시 프로퍼티, RelicLogic 효과 템플릿, Status HUD/도감 nested entries에서 키 조회. raw ID/직렬화 스키마/수치 유지.
- 도감/RunSpecial nested entry는 nonserialized owner/field 경로 바인딩. Construction 남은 일수는 {remaining_runs}/units.days.value; 원래 N일 fallback 지원.
- Enemy.DisplayName 번역, EnemyName 원문 유지. 보스 HUD/사망 원인 표시가 DisplayName 사용.
- 고정 UI 468경로는 LocalizeStringEvent. LocalizeTmpFontEvent는 package Asset event TMP adapter. GameFonts로 KO/EN 원래 폰트, JA Galmuri9 Japanese SDF, Hans/Hant Fusion Pixel 12px proportional 지역별 폰트 선택. 동적 수치 TMP도 폰트 연결.
- 상점/상자/포탈/맵 이벤트/튜토리얼/GameOver/설정/프로필 문구는 명시적 키. 커스텀 상호작용 의미가 달라지면 추가 키 배정 필요.
- [[원래용어ID]] 유지, term.ID 표시명 조회. 수치 분류/게임플레이 식별은 raw ID.

## Ink와 생명주기

- 활성 16Ink/162대사·선택지의 loc 태그로 한 Ink에서 다섯 언어 조회. FullInkLocalizationCompile은 공식 컴파일러로 JSON 생성.
- DialogueController는 Continue 결과/태그 보존, DialogueView는 choice loc 조회. locale 변경 시 Continue/Choose 없음; 타이핑 완료 처리/선택 상태 유지.
- SettingsPanel/ItemDetailPanel 활성 동안 event 구독/해제. 기존 초기화 coroutine은 마지막 언어 요청 적용. 유물 프리뷰 레벨 유지. World prompt는 기존 LateUpdate 갱신.
- UI text/font 이벤트는 저작 자산 persistent listener. 프리팹 인스턴스 override는 RecordPrefabInstancePropertyModifications로 기록한다. 런타임 UI 계층 추가 없음.
- 폰트 listener는 LocalizeTmpFontEvent.ApplyLocalizedFont를 호출한다. authoredMaterial의 스타일을 TMP_MaterialManager.GetFallbackMaterial로 대상 atlas에 적용하고 Add/ReleaseFallbackMaterialReference로 수명을 관리한다. 교체 후 한 번 mesh padding/layout 갱신; disabled 동안 참조 유지, destroy 시 해제.
- DialogueController는 locale 변경 시 NPCParticipantRegistry의 현재 DisplayName을 DialogueView.RefreshSpeakerName에 반영한다. 대사 대기/전환 중에도 이름 갱신, Ink 진행 없음. 스탯 패널은 기존 LateUpdate에 갱신을 모으고 도감/상세는 선택·유물 프리뷰 레벨을 유지한다.
- SectionListView.Clear는 이전 섹션을 즉시 비활성화한 뒤 Destroy한다. 같은 프레임의 새 섹션과 이전 섹션이 함께 레이아웃에 계산되는 것을 방지한다. 문장 길이에 따른 정상 높이 변화는 유지한다.

## 저작 도구/함정/후속

- LocalizationInventoryExport: batch 전용, 검토된 자산만 순회, ManagedReference 하위 제외, 자산별 진행 로그. 인터랙티브 자동 delayCall 제거. 광범위 BehaviorGraph 순회에서 정지 재현 이력.
- FullLocalizationAuthoring: 승인된 UI 프리팹/씬을 Unity API로 저장. YAML 수작업 없음. RunAndValidateBatch는 적용 후 readback 검증.
- Ink JSON memory-mapped 쓰기 실패는 ReleaseCachedFileHandles+임시 파일 교체로 대응.
- [번역 사용법](../../DataSheets/Localization/README.md), [실행 결과](../SessionLogs/2026-10-03.md). FullLocalizationValidation: 테이블/글리프/listener readback/기존 검증. audit_translation: 키/인자/용어 parity.
- 남은 확인: 외국어 표현 검수, 실제 화면 레이아웃/스타일, Play Mode 전환/저장 재실행, 전체 Player 빌드. 배치 검사와 구분한다.
- FullLocalizationValidation은 Outline/색상/FaceDilate와 대상 atlas를 비교하고, 1,500개 NPC/무기/등록 유물 모든 레벨/튜토리얼/상점 표시를 언어 왕복 전환으로 검사한다. 일반 스탯 로직만 검사하지 않고 ItemDatabase.allRelics의 실제 definition과 logic 조합을 사용한다. 표 값이 채워진 것만으로 동적 표시의 번역 완료를 판단하지 않는다. Editor 시작 직후 collection 조회 캐시가 비어 있으면 기존 collection 자산을 직접 로드한다.
- 폰트 검증 기준은 한국어 저작 머티리얼이어야 한다. Editor locale preview로 저장된 Noto 머티리얼은 authoredMaterial 기준으로 금지한다. export_original_font_styles.py가 HEAD의 원래 TMP 머티리얼을 읽고 RepairOriginalStylesBatch가 Unity API로 참조를 복구한다. 기존 별도 머티리얼 프리셋도 유지한다. ApplyKoreanFontDefaultsBatch는 CJK 기본 머티리얼에도 한국어 BlackOutline 셰이더/스타일을 복사하며 atlas/gradient/weight는 대상 것을 유지한다.
- RenderOutlineBatch는 독립 Editor 렌더 프로브로 5언어 Outline 켜짐/꺼짐 픽셀 차이를 비교한다. 실제 게임 UI 전체 렌더 검수와 구분한다. 임시 프로브 오브젝트는 finally에서 제거한다.
- 확인된 미완료 경로: NpcNameplatePresenter는 OnEnable에서만 표시명/폭을 계산하고, MerchantRefreshInteractable은 시작/활성화/재고 변경 때만 횟수를 갱신한다. 초기 Localization 준비 전 fallback 또는 이후 언어 변경이 화면에 남을 수 있다. DialogueView 이름 검사에는 이 월드 이름표와 횟수 라벨이 포함되지 않았다. 번역 갱신 수정/실제 씬 수명 검증 필요.
- ShopSlot.soldLabel의 SOLD는 사용자 요청으로 번역 대상에서 제외한다. 고정 영어 표기 유지. 한글 리터럴만 찾는 조사로는 다른 영어 원문 표시가 누락될 수 있다.
- ApplyFusionChineseBatch: FusionPixel 원본에서 동적 SDF 자산 생성, 한국어 머티리얼 스타일 복사, 중국어 Asset Table의 기존 4 font 키를 모두 교체. EnsureFont도 Fusion 자산이 있으면 이를 사용하므로 일반 authoring 재실행에서 Noto로 돌아가지 않는다. FullLocalizationValidation은 고정 Noto 경로 대신 현재 GameFonts 항목으로 글리프를 검사한다. source/지역별 ttf/OFL/upstream licenses와 해시를 보관한다.
- Architecture/Contracts 승격은 후속 후보. 이번 작업에서 공식 문서/Presentation HTML 수정 없음.

## 동적 표시·튜토리얼 누락 수정

- WeaponExclusive는 definition.description도 GameText.Asset로 조회한다. TimedStat의 triggerLabel 7필드를 GUID 키/604필드 대응표에 추가했다. 9개 로직의 중복 한글 fallback을 기존 DefaultEffectTemplate로 통일하고, 기본 템플릿 프로퍼티가 없는 3개 로직은 명시적 키를 조회한다. 장착/수치 계산/용어 ID는 유지한다.
- ItemDetailContext의 가져오기/버리기는 명시적 키이며 ItemDetailPanel이 locale 변경과 초기 로딩 완료 시 액션 힌트를 갱신한다. LevelRewardSelectionPresenter의 리롤 수와 TrainingDummyDamageReadout2D의 피해/DPS/누적 형식도 명시적 키로 표시한다.
- NpcNameplatePresenter는 LateUpdate에서 이름/폰트 변경을 감지하고 기존 폭/아이콘 위치 계산을 다시 적용한다. NPCData 없는 도감은 별도 키, 슬라임 두 이름은 interactionAnchor 부모 이름 기반 키로 연결한다. 이 저작 이름 변경 시 CSV 키도 갱신한다. MerchantRefreshInteractable은 보이는 횟수 문구를 LateUpdate에서 갱신하되 같은 텍스트의 재할당을 피한다.
- TutorialInfoPanel은 기존 tutorialId/페이지 번호로 title/body를 조회한다. 언어 갱신은 텍스트만 변경하여 페이지 이동/홀드 초기화 경로를 호출하지 않는다. 실제 TutorialCorridor의 4종류/8페이지를 검증한다. 기존 WASD/SPACE/F/Q 저작 안내는 유지하며 재매핑 대응 개선은 별도 후속이다.
- PrototypeTutorialUpgrade는 일시정지 중에도 진행 문구만 갱신하고, PrototypeTutorialPromptView의 한글 재삽입을 제거했다. 길게 누르기의 색상 태그는 각 언어의 번역 템플릿에 들어간다.
- FirePuddle 사망 원인은 표시 요청에서만 번역한다. 원인 분류용 원문은 유지한다. SOLD는 영어 표기를 유지한다.
- Preview Scene/프리팹 검증은 저장하지 않는다. 실제 Merchant 프리팹 존재를 확인한 뒤 카운트 LateUpdate를 실행하여 번역 및 초기 갱신을 확인한다. 이 검사는 실제 Play Mode 화면/수동 입력 검증을 대신하지 않는다.

## 타이틀과 전체 흐름 조사 후속

- TitleProfileSlotCardUI의 Unicode const 제목 4개를 키 기반 프로퍼티로 변경. RefreshLocalizedText는 텍스트만 변경하며 선택/삭제 버튼의 인터랙션·활성 상태와 callback을 재바인딩하지 않는다.
- TitleProfileSlotPanelUI는 활성 동안 SelectedLocaleChanged 구독/해제, 초기 Localization 준비 완료 후 텍스트 갱신. 현재 슬롯 summary를 다시 읽고 삭제 확인 경고도 현재 index로 재표시한다. 저장/삭제/게임 진행을 실행하지 않는다.
- TitleScene 슬롯 제목 3개는 카드 slotLabelText와 별도 고정 라벨이라 독립 키를 바인딩했다. 동적 값/제목/버튼 27개 기존 고정 String Event를 Unity API로 제거하고 StaticUiBindings에서 제외하여 늦게 오는 이벤트의 덮어쓰기를 막았다. authored 폰트 스타일/레이아웃 유지.
- MenuButtonHighlightPresentation은 기존 TMP의 실제 textBounds 왼쪽에서 기존 칼 RectTransform 오른쪽을 12 부모 단위 앞에 둔다. 부모 좌표/앵커/피벗 변환을 사용하고 표시/숨김 애니메이션은 상대 offset 유지. 첫 CanvasScaler 갱신 전 zero scale에서는 계산 보류. UI 계층/serialized field 추가 없음.
- ValidateTitleDisplays는 실제 TitleScene의 카드 3개/선택 칼 3개/기존 폰트 연결을 5언어+영어 재전환으로 684회 확인. Preview Scene의 CanvasScaler가 실행되지 않으므로 검증에서만 zero Canvas scale을 1로 두어 표시 상태를 모사한다. 실제 입력/애니메이션/Play Mode 검증과 구분한다.
- [전체 흐름 커버리지](LocalizationCoverage.md): 추가 원문 표시 경로 11그룹을 조사했다. SpeechData 무작위 56문장, Intro/Outro 각 4슬라이드, RouteSet 장소, 상점 실패/영업, 상자 남은 적, 도감 제목, 레벨업 차단, 게임오버 override, Loading. 11그룹은 2026-10-04 구현/배치 검증 완료. 실제 전체 번역 완료 판정은 보류. 고정/동적 표시 소유권 충돌과 초기화 fallback을 모든 화면에서 확인해야 한다.

## 추가 흐름 구현 (2026-10-04)

- SpeechData의 entry/line, Intro/Outro slide.text, RouteSet 장소명 72필드를 GUID+field 키 목록에 추가했다. 대응표 676필드. ScriptableObject 원문/스키마, route 식별자, Ink 분기는 그대로다.
- MerchantNPC 실패 두 종류, 첫 영업 연출, 상자 잔여 적, 레벨업 전투 차단, compact Loading은 명시적 표시 키로 조회한다. EncyclopediaItemTab은 비어 있지 않은 preset에도 조회하고 초기 준비/locale 변경 때 제목만 갱신한다.
- Intro/Outro player는 현재 slide index로 텍스트를 조회한다. 타이핑 중 번역 변경은 이미 표시한 문자 수를 유지·제한하고, 완료 대기 중에는 전체 표시를 갱신한다. 홀드/스킵/페이드 누적값을 초기화하지 않는다. 대기 길이 분류는 기존 저작 텍스트를 사용한다. 실제 입력/프레임 연출은 Play Mode 검수가 남는다.
- GameOverPresentationRequest의 CauseNameKey/LocationSceneName은 비직렬화 런타임 요청 문맥이다. PlayerDeathReturnToHub2D는 적이 사라지기 전에 표시 키를 확보한다. 사망 분류에는 원래 식별자를 사용하고 표시만 번역한다. UI는 선택된 사망 문구 키를 Begin마다 초기화하고 locale 갱신 시 재사용한다. 정상 승리 override와 장소명도 다시 조회한다. 커스텀 요청의 자유 문자열 override는 자동 키 추정하지 않는다.
- DungeonMinimapPresenter/ChestMonsterKillLockView는 보이는 현재 상태의 텍스트만 갱신한다. 파티클/잠금 상태/맵 그래프를 재실행하지 않는다.
- Items/Chests 4프리팹 기존 TMP에 LocalizeTmpFontEvent를 추가했다. authored 한국어 머티리얼 스타일/대상 locale atlas를 검증하며 UI 계층을 새로 만들지 않는다. 일본어 Noto/CJK 중국어 Fusion 선택은 유지했다.
- ValidateFlowDisplays는 5언어+영어 왕복으로 72필드의 실제 getter, 모든 speech line 선택과 1회 RNG 소비, 도감 preset/상자 잔여 수/승리/미지 장소/사망 문구 캐시를 504회 검사한다. Preview 객체는 저장하지 않고 locale/RNG 상태를 복원한다.
- [현재 범위 상태](LocalizationCoverage.md), [작업 기록](../SessionLogs/2026-10-04.md). 실제 전체 게임 흐름 플레이/레이아웃/저장 재실행/Player 빌드 완료 판정과 구분한다.

## 일본어 픽셀 폰트 및 화면 검수 (2026-10-04)

- 현재 JA GameFonts의 4개 엔트리는 `Fonts/Galmuri/Galmuri9 Japanese SDF.asset`을 사용한다. 이전 Noto 자산은 보존하지만 JA 테이블에서는 사용하지 않는다. 공식 Galmuri9.ttf/라이선스 출처·해시는 Fonts/Sources.json에 기록했다. 현재 JA 테이블의 고유 표시 문자 1,062자 전부 지원한다.
- 일본어 TMP는 10px 디자인 그리드의 정수배인 40pt, padding5, SDFAA, 1024 다중 동적 atlas, ClearDynamicDataOnBuild를 사용한다. 실제 라벨의 한국어 authored material preset은 LocalizeTmpFontEvent가 대상 atlas와 조합하므로 Outline/색상은 라벨별로 유지된다. 중국어 Fusion Pixel과 KO/EN 저작 폰트는 유지한다.
- 무기/도감 AbilityBlock의 스킬 제목과 PlayerStatSectionView 제목, TitleScene의 슬롯 제목 3개만 한 줄 AutoSize를 사용한다. 최대 크기는 원래 크기, 최소는 75%다. 본문과 실시간 수치는 변경하지 않는다. Unity API authoring 진입점은 FullLocalizationAuthoring.ApplyAbilityTitleFitBatch다.
- FullLocalizationValidation.ReviewTitleScreensBatch/ReviewHubScreensBatch/ReviewTutorialScreensBatch는 각각 실제 씬을 Edit Mode에서 연 뒤 Play Mode로 실행한다. 기존 화면 controller/hover 위치 계산 경로를 사용하며 결과 PNG/표시 문자열 JSON을 outputs/localization-screen-review-2026-10-04에 저장한다. 저장 6개와 언어 preference를 백업/복원한다. 새 runtime UI/서비스를 생성하지 않는다.
- PlayerStatSectionView의 기존 VerticalLayoutGroup이 행 폭을 제어하고, PlayerStatRowView의 horizontal ContentSizeFitter는 Unconstrained로 둔다. labelText만 한 줄 AutoSize를 사용하여 번역의 preferredWidth 때문에 행이 패널 밖으로 늘어나지 않게 한다. 값 필드 크기/계산은 그대로다. hover/도감의 WeaponDescriptionHint 라벨도 기존 칸에 맞춘다.
- LocalizeTmpFontEvent는 font/fontSharedMaterial에 더해 TextMeshProUGUI CanvasRenderer의 texture override도 대상 material atlas로 갱신한다. RunTimerHUD의 Outline 설정으로 남은 이전 texture override가 정상 숫자를 엉뚱한 atlas 조각으로 그리는 문제를 막는다. 추가 fontMaterial 인스턴스/매 프레임 갱신은 도입하지 않는다.
- 이 검수는 대표 화면 상태의 실제 렌더링 검사다. 마우스·패드 입력, 전체 무기/유물, 구매 실패, 전투/보스/사망/엔딩의 정상 플레이 통과와 동일하지 않다. 구체적인 결과/제한은 LocalizationCoverage와 결과 폴더 README를 확인한다.
