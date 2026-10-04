# 2026-10-04 실제 Play Mode 번역·폰트 검수

Unity 6000.4.2f1의 실제 Game View 캡처다. Editor Preview나 글자만 따로 그린 샘플이 아니다. 파일명은 `<locale>-<screen>.png`, 같은 이름 JSON에는 당시 활성 TMP의 문자열/폰트/overflow/크기가 기록되어 있다.

## 범위

| 실제 씬 | 화면 상태 | 언어별 수 |
| --- | --- | --- |
| TitleScene | title, profiles, settings | 3 |
| ProtoTypeHub | hub, inventory, encyclopedia, weapon-simple, weapon-detail, relic | 6 |
| TutorialCorridor | tutorial, tutorial-info | 2 |

언어는 ko/en/ja/zh-Hans/zh-Hant, 총 11상태×5언어=55장이다. 카메라와 CanvasScaler의 실제 결과를 캡처했으며 현재 Game View의 렌더 크기는 로그에 기록된다. `-screen-width` 옵션만으로 최종 Game View 해상도가 고정된다고 가정하지 않는다.

## 변경과 판단 기준

- JA는 공식 Galmuri9.ttf로 만든 Galmuri9 Japanese SDF를 사용한다. 현재 번역 고유 문자 1,062자 전부 지원하며 실제 제목/설명/튜토리얼에서 픽셀 형태를 확인한다. 중국어는 Fusion Pixel, KO/EN은 기존 저작 폰트다.
- 라벨별 한국어 Outline/색상 preset을 대상 atlas와 조합한다. 폰트 이름이 같다는 검사 외에 실제 PNG도 확인한다.
- 긴 스킬명·능력치 제목·저장 슬롯 제목은 한 줄 AutoSize(원래 크기~75%)로 맞춘다. 능력치 행은 부모가 폭을 정하고 라벨만 맞추며 실시간 값은 고정 크기를 유지한다. 설명 전환 안내도 기존 칸에 맞춘다.
- 오른쪽 위 타이머는 문자열 `15:00`이 정상인데 atlas 이미지 조각이 잘못 나타났다. TMP Outline setter가 CanvasRenderer에 이전 texture override를 남기는 경로를 확인했다. LocalizeTmpFontEvent에서 font/material 교체와 함께 renderer texture를 새 material atlas로 맞춘다. 타이머 시간 계산/색상/일시정지 pulse는 변경하지 않는다.
- 상세 설명 검수는 HoverUIController.ShowHover를 통해 기존 anchor/화면 경계 계산을 사용한다. 직접 ItemDetailPanel.ShowHover만 호출한 초기 캡처의 화면 밖 배치는 실제 hover 경로 통과로 판정하지 않았다.
- isTextOverflowing은 단독 합격/불합격 기준이 아니다. 투명도 0의 허수아비 초기 원문과 화면 밖 월드 라벨이 JSON에 포함될 수 있다. 실제 보이는 문구와 함께 판단한다.

## 재현 방법과 제한

FullLocalizationValidation의 ReviewTitleScreensBatch / ReviewHubScreensBatch / ReviewTutorialScreensBatch를 각각 새 batch Editor에서 실행한다. 정상 컨트롤러의 OpenUI/Show/hover를 호출하지만 사용자 입력 전체를 재현한 것은 아니다. 씬 사이의 실제 portal/저장 슬롯 선택 동선 검증과 구분한다.

- 검은 화면/누락 PNG는 실패로 판정한다. 초기 연속 씬 전환 검수는 camera/overlay 상태가 섞여 폐기하고, 씬별 독립 Play Mode로 대체했다.
- 기존 저장 6개와 언어 preference를 백업/복원한다. 리뷰 때문에 저장 진행도나 사용자의 언어 선택을 바꾸지 않는다.
- 무기/유물 상세 PNG는 등록 목록에서 대표 1개씩이다. 등록 데이터 전체 키/각 유물 레벨은 별도 자동 투영 검사를 통과했지만 전체 아이템의 실제 시각 검수와 동일하지 않다.
- 튜토리얼 정보 PNG는 실제 씬에 저작된 요청의 첫 페이지다. 모든 페이지/타이핑/홀드 입력 완료를 플레이한 것은 아니다.
- 전체 새 게임→인트로→튜토리얼 완료→던전/보스→사망/승리→엔딩, 구매 실패/재실행/Steam 환경/Player 빌드, 원어민 감수는 별도 확인이 남는다. 게임 로고 이미지의 한국어는 텍스트 번역 범위 밖이다. SOLD는 의도적으로 영어를 유지한다.
- 검수 로그에 기존 PrewarmTraceRuntime의 파일 쓰기 IOException(Win32 1224)이 있었다. 화면 캡처 종료 코드와 로컬라이제이션 검사 결과를 이 진단 기록기의 별도 문제와 구분한다.

관련 구조: [LocalizationFlow](../../Docs/StructureMemory/LocalizationFlow.md), [커버리지](../../Docs/StructureMemory/LocalizationCoverage.md), [세션 기록](../../Docs/SessionLogs/2026-10-04.md).

최종 캡처 세 배치는 exit0으로 종료했다. Hub는 InventoryUIManager.TryOpen으로 다시 캡처해 플레이어 값이 연결된 실제 인벤토리 경로를 확인했다. 55 PNG/55 JSON 모두 읽기 가능, 실제 크기2400×1359. 검수 후 저장 6개는 백업과 byte 일치했다.

남은 시각 문제: ja-encyclopedia.png에서 무기 제목과 상세 설명 전환 안내가 겹친다. 안내 글자 크기만으로 해결되지 않으며 도감 내 안내 위치 재배치가 필요하다. 모든 화면의 레이아웃 합격이나 전체 번역 완료로 판정하지 않는다.
