# 레벨업 A 금테 UI 실제 적용

Mode: Implementation. 사용자 "적용해줘"에 따라 [승인 시안](../levelup-button-designs-2026-11-03/revision-2-selected-a-title/README.md)을 실제 프로젝트에 연결한다.

## 범위와 성공 기준

- `GlobalUIRoot/RewardCanvas/LevelRewardSelectionRoot`의 하단 A 버튼과 패널 없는 흰색 Bold/외곽선 제목.
- 승인 아트 PNG/Aseprite/Unity import metadata, 제목 전용 TMP 재질, 해당 프리팹 및 필요한 로컬라이징 항목만 수정한다.
- 기존 프리팹 버튼 크기/위치, 클릭/ESC/R, 카드 선택과 애니메이션, 세션 pause/input/리롤 횟수 소유권은 유지한다.
- 문구/단축키/횟수는 TMP로 표시하며 런타임 UI 계층을 생성하지 않는다. 기존 `rerollCountText`는 분리된 횟수 배지에 투영하도록 최소 수정한다.
- 기존 사용자 변경을 보존하며 씬/저장/게임플레이/공유 글꼴/패키지/설정은 변경하지 않는다.
- 확인은 source/reference/diff와 Unity import/컴파일이다. 자동 테스트·Play Mode 실행은 요청되지 않았으므로 실행하지 않는다.

## 적용 결과와 변경 파일

- [GlobalUIRoot.prefab](../../Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab): 두 버튼의 기본 배경과 SpriteSwap 4상태, 분리된 TMP 문구/단축키/횟수, 패널 없는 Bold 제목을 연결했다. 제목 Mount의 Image만 비활성화하고 기존 위치/크기와 카드/입력/Presenter 참조를 유지했다.
- [LevelRewardControls](../../Assets/_Project/Art/Sprites/UI/LevelRewardControls/): Close/Reroll 각각 Normal, Focus, Pressed, Disabled PNG 8개와 Unity meta, 편집 가능한 `LevelRewardControls.aseprite`를 추가했다. Aseprite에서 원본 글자 레이어를 숨겨 출력했으며, PNG는 160×40 / 170×40, Point, PPU100, mipmap 없음, 기본 플랫폼 무압축이다. 기존 320×80 / 340×80 버튼에 2배 크기로 표시한다.
- [LevelRewardTitleOutline.mat](../../Assets/_Project/Art/Font/LevelRewardTitleOutline.mat) 및 meta: 흰색 face, #17131C outline, OutlineWidth 0.314, OutlineSoftness 0의 제목 전용 TMP 재질이다. TitleText는 Bold/48px 기준이며 `LocalizeTmpFontEvent.authoredMaterial`도 이 재질을 사용한다. 공유 Galmuri 글꼴은 수정하지 않았다.
- [LevelRewardSelectionPresenter.cs](../../Assets/_Project/Runtime/UI/Progression/Leveling/LevelRewardSelectionPresenter.cs): 기존 `rerollCountText`에 `remaining/max`만 쓰도록 한 줄 변경했다. 리롤 문구는 별도의 authored `RerollActionLabel`, 닫기 문구는 기존 `CloseLabel`의 LocalizeStringEvent가 담당한다. 횟수/사용 가능 여부/ESC/R 처리는 기존 로직을 유지한다.
- [GameText.csv](../../DataSheets/Localization/GameText.csv), `Assets/_Project/Data/Localization/GameText Shared Data.asset` 및 `GameText_ko/en/ja/zh-Hans/zh-Hant.asset`: `ui.level_reward.close`, `ui.level_reward.reroll` 두 항목을 추가했다. 기존 키/번역은 보존했다.
- 제작 자료: [Aseprite 출력 Lua](export-controls.lua), [Unity 네이티브 제작 소스](LevelRewardSkinAuthoring.cs.txt), 실행 로그, 작업 전 사본과 보존 보고서를 이 폴더에 보관한다. 임시 Editor 제작 코드는 Assets에서 제거했다.

## 검증

- Aseprite CLI 출력 성공. PNG에는 배경·장식만 있으며 문구/키/횟수는 라이브 TMP로 표시한다.
- Unity 6000.4.2f1 네이티브 API로 sprite importer, 재질, StringTable, 프리팹을 저장했다. [작성 재실행 로그](unity-authoring-retry.log)는 exit 0이다. 첫 실행에서 열려 있는 CSV를 전체 덮어쓰려다 Windows 1224가 발생했으나, 원본을 유지한 두 행 추가로 해결했다.
- 임시 Editor 코드 제거 후 [최종 Unity batch import/스크립트 컴파일](unity-final-import.log) exit 0, C# 컴파일 오류 0. [UI.csproj MSBuild](msbuild-ui.log) exit 0. 기존 경고는 남아 있다. Player build는 실행하지 않았다.
- 프리팹 저장 시 대상 외 TMP 재질/중첩 override가 바뀌는 기존 함정을 확인했다. 작업 직전 사본의 범위 밖 serialized document 127개를 그대로 복구했다. 최종 변경은 허용된 기존 document 14개와 버튼 하위 신규 document 17개뿐이며, 기존 대상 외 document와 버튼 루트 배치/클릭 이벤트는 동일하다. [보존 보고서](prefab-preservation.json). 최종 Unity import 후 프리팹 SHA256도 동일하다.
- 신규 TMP/로컬라이징 참조, SpriteSwap GUID 연결, 제목 Bold/패널 비활성화, 기존 rerollCountText 참조를 소스에서 확인했다. 이전 공유 글꼴 파일 해시가 모두 동일하며 CSV는 이전 바이트를 그대로 prefix로 보존한다.
- 문서 상대 링크를 확인했다. 기본 `git diff --check`는 Unity가 쓰는 빈 YAML 필드 뒤 공백을 보고했으며, serializer 공백을 제외한 diff check는 통과했다. 원래 serializer 형식은 유지했다.
- 자동 테스트, Play Mode, 수동 플레이는 실행하지 않았다. 이 확인은 파일/참조/컴파일 검사이며 실제 화면의 렌더 검증이 아니다.

## 남은 확인

- 실제 게임에서 제목 외곽선/Bold, 각 언어의 긴 문구 축소, 키보드 포커스·눌림·비활성 상태를 확인해야 한다. SpriteSwap은 배경 상태를 바꾸며 TMP의 위치는 고정이다.
- [승인 전체 화면 목업](../levelup-button-designs-2026-11-03/revision-2-selected-a-title/A-selected-screen.png)은 디자인 참고이며 이번 적용 후의 플레이 캡처가 아니다.

Doc Impact Check: **SessionLog + StructureMemory + DecisionLog + ErrorLog**. [세션 기록](../../Docs/SessionLogs/2026-11-03.md), [레벨 진행 구조](../../Docs/StructureMemory/ScriptSystems/LevelProgressionStructure.md), [디자인 선택](../../Docs/DecisionLog.md), [기존 프리팹 저장 함정](../../Docs/ErrorLog.md)을 갱신했다. Architecture/Contracts/Presentation HTML 변경 없음. 이번 UI 변경으로 새 Presentation HTML stale candidate 없음.
