# 술 장판·차지 게이지·스탯창·설정 입력 수정

2026-11-02 / Implementation. 현재 사용자 요청이 작업 범위이며, 기존 미커밋 작업 위에 필요한 변경만 적용했다. 수정 직전 파일은 `before/`에 보관했다.

## 적용 결과

| 항목 | 변경 |
| --- | --- |
| 술 장판 | 기본 충전 3초 → 2초. 현재 스피리 호감도 보상이 있으면 1초. 잔 표시도 같은 시간을 사용한다. |
| 효과 수명 | 잔이 가득 찬 뒤 다음 양의 시간 갱신에서 발동하는 기존 overflow 경계, 퇴장 후 1.5초 배출, 이로운 효과 5초 잔여 지속시간과 0.2초 갱신은 유지했다. |
| 차지 게이지 | 견습용사의 검·홍련 모두 광택, 음영, 테두리 장식과 완충의 밝은 끝점을 숨겼다. 24×4 외곽, 22×2 단색 채움, 대쉬 HUD와 같은 짙은 갈색 배경을 사용한다. 기존 위치와 진행 단계별 빨강/주황 색상은 유지한다. |
| 스탯창 | 공용 루트 XY 배율 1 → 1.15. 글자와 행을 포함해 15% 확대한다. 논리 크기 390×742, 표시 크기 448.5×853.3이며 상위 Canvas 배율은 별도로 적용된다. |
| 되돌리기 | 12×12 아이콘을 32×32 원형 화살표로 다시 그리고 실제 표시도 32×32로 변경했다. 기존 sprite GUID와 local ID를 유지했다. |
| 키 선택 ESC | 입력 대기 또는 키 충돌 확인 중 첫 ESC는 해당 선택만 취소한다. 같은 프레임의 다른 닫기 호출은 소비하고, 이후 ESC에 기존 변경사항 확인/설정 복귀 경로를 유지한다. |
| 기본 설정 클릭 | 타이틀·게임플레이 설정창의 8개 화살표 각각에서 실제 Button/TMP가 클릭을 받게 수정했다. 형제 배경은 색상 표시만 맡고 클릭을 받지 않는다. 3개 볼륨 슬라이더는 막대 클릭도 받는다. |

## 조사 결과

- ESC: `UIManager`가 `CanCloseOnEscape`보다 `TryHandleCloseRequest`를 먼저 호출한다. 기존 핸들러는 입력 대기를 지운 뒤 창을 닫았다. 반대로 키매핑의 `Update`가 먼저 대기를 취소해도, 뒤에 실행되는 닫기 핸들러가 같은 ESC로 창을 닫을 수 있었다. 두 순서를 모두 막도록 취소 상태와 취소 프레임을 처리했다. 타이틀의 직접 호출 경로도 같은 핸들러를 사용한다.
- 클릭: 프리팹과 타이틀 씬에서 화살표 Button/TMP와 `ButtonBackground`가 동일 크기의 형제였다. 배경만 raycast target이므로 부모를 따라 전달되는 포인터 이벤트가 형제 Button에 닿지 않았다. 실제 버튼의 클릭 영역을 활성화했다. 화면 설정의 적용 버튼은 변경값이 없을 때 비활성인 기존 동작을 유지한다.
- 위 원인은 소스/직렬화 경로로 확인했다. 사용자가 보고한 실제 플레이 상황을 재현하거나 수정 후 직접 플레이한 결과로 주장하지 않는다.

## 변경 파일과 이유

경로의 기준은 저장소 루트다. 전체 파일 목록은 [제품·검사 파일](changed-files.json), [문서 파일](documentation-files.json)에 있다.

- `Assets/_Project/Runtime/Features/Player/Status/PlayerAlcoholExposure.cs`: 2초/1초 기준과 잔 진행률을 연결.
- `Assets/_Project/Runtime/UI/Settings/KeyBindingPanelUI.cs`: 입력 취소 우선 처리와 같은 프레임의 중복 닫기 방지.
- `Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab`, `Assets/_Project/Scenes/TitleScene.unity`: 설정 화살표·슬라이더 클릭 대상 수정.
- `Assets/_Project/Prefabs/VFX/ChargeGauge.prefab`, `Assets/_Project/Prefabs/VFX/CrimsonBoundary/ChargeSixColor/CrimsonChargePresentation.prefab`: 양쪽 차지 게이지를 평면 스타일로 변경.
- `Assets/_Project/Prefabs/UI/PopupUI/PlayerStatUI/PlayerStatUI.prefab`: 전체 표시 배율 15% 확대.
- `Assets/_Project/Prefabs/UI/KeyMapRow.prefab`: ResetIcon 표시를 32×32로 변경.
- `Assets/_Project/Art/Sprites/UI/SettingsSkin/SettingsSkin.png`, 해당 `.meta`, `Source/SettingsSkin.aseprite`, `README.md`: 새 원형 화살표, 스프라이트 영역과 원본 문서 동기화.
- `Assets/_Project/Data/Dialogue/Affection/SpiriAlcoholPuddleReward.asset`, `Assets/_Project/Editor/Tools/Data/SpiriAlcoholRewardAuthoring.cs`: 보상 설명과 재작성 원본을 1초로 변경.
- `Assets/_Project/Data/Localization/GameText_ko.asset`, `GameText_en.asset`, `GameText_ja.asset`, `GameText_zh-Hans.asset`, `GameText_zh-Hant.asset`, `DataSheets/Localization/GameText.csv`: 동일한 보상 설명 한 항목씩 수정.
- `Assets/_Project/Tests/PlayMode/Procedural/AlcoholExposurePlayModeTests.cs`, `AlcoholExposurePlayerVisualPlayModeTests.cs`: 기존 충전/배출/재진입/잔 표시 기대 시간을 새 기준으로 갱신. 테스트 실행은 하지 않았다.
- `Docs/StructureMemory/AlcoholExposure.md`, `SettingsPanelLayout.md`, `InputBindings.md`, `ScriptSystems/InventoryAndChestUIStructure.md`, `ScriptSystems/WeaponAndGASStructure.md`, `ScriptSystems/BurnStatusAndCrimsonBoundaryWeapon.md`: 현재 시간, 클릭 구조, ESC 처리, 크기 및 게이지 외형 지도 갱신.
- `Docs/SessionLogs/2026-11-02.md`, `Docs/ErrorLog.md`, `Docs/DecisionLog.md`: 실제 작업·검증, 클릭/ESC 함정, 이전 게이지 외형 결정을 대체하는 사용자 요청 기록.

## 검증과 한계

- MSBuild `UI.csproj` 및 후속 `Editor.csproj` 빌드 종료 코드 0. 관련 런타임/Editor 소스가 생성 프로젝트에 포함됨을 확인했다. 기존 경고가 남아 있다.
- Unity 6000.4.2f1 `-batchmode -quit` 임포트·컴파일 종료 코드 0. [Unity 로그](unity-import.log), [UI 빌드 로그](msbuild-ui.log), [Editor 빌드 로그](msbuild-editor.log).
- [수정 직전과의 소스 대조](source-review.txt): 기존 프리팹/씬 fileID 집합이 같고 새로 누락된 로컬 참조가 없다. 설정 소스 각각 19개 클릭 속성, 두 게이지 각각 8개 블록, 스탯/행 각각 1개 블록만 바뀌었다. 기존 scene 인스턴스에서 해당 클릭 속성 또는 공유 스탯 루트 배율을 덮는 override는 발견되지 않았다.
- Aseprite에서 편집·저장·PNG 내보내기를 완료하고 [아이콘](reset-icon.png)을 시각적으로 확인했다. 원본 시트와 달라진 303개 픽셀은 기존/새 Reset 영역에만 있다. 다른 8개 스프라이트는 유지됐다.
- 자동 테스트, Play Mode, 실제 마우스/키보드 입력과 전체 Player 빌드는 실행하지 않았다. 테스트 파일 수정은 실행/통과 증거가 아니다.
- 수정 직전 상태를 기준으로 공백과 문서 링크를 확인했다. 저장소 전체 diff에는 이전 작업에서 생성된 YAML 공백 경고가 있으므로 전체 diff가 깨끗하다고 주장하지 않는다.

수동 확인: 호감도 보상 없음/있음에서 약 2초/1초 발동과 잔 표시, 두 무기의 부분/완충 및 해제·취소, 인벤토리/상자 스탯창 크기, 타이틀/일시정지 설정의 좌우 화살표·볼륨·언어·적용/닫기, 키 입력 대기에서 ESC 한 번 후 같은 화면 유지와 다음 ESC의 정상 복귀를 확인하면 된다. 긴 번역과 작은 해상도에서의 스탯창 배치도 남아 있다.

Doc Impact: SessionLog + StructureMemory + ErrorLog + DecisionLog. Architecture/Contracts와 Presentation HTML은 변경하지 않았으며 HTML stale candidate는 없다.
