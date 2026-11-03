# A 금테 확정 + 패널 없는 Bold 제목

후속 “적용해줘” 요청으로 [Unity 프리팹 적용을 완료](../../levelup-ui-integration-2026-11-03/README.md)했다. 아래 내용과 검증 범위는 이 Aseprite 시안 제작 당시의 기록이며, PNG는 실제 플레이 캡처가 아니다.

Mode: Implementation — 기존 Aseprite 시안 수정. 사용자 요청은 A 버튼 확정과 최상단 제목의 배경 패널 제거/Bold/외곽선 적용이다.

- [전체 화면 PNG](A-selected-screen.png)
- [편집 가능한 전체 화면 Aseprite](A-selected-screen.aseprite)
- [제목 2배 확대](title-detail-2x.png)
- [투명 제목 PNG](Level-UP-bold-outline.png) / [제목 Aseprite](Level-UP-bold-outline.aseprite)
- [선택한 버튼 Aseprite](A-gilt-buttons.aseprite)
- [전체 묶음](selected-a-title.zip)

## 수정한 디자인

- 하단 버튼은 승인한 **A 고전 금테**를 그대로 사용한다. 일반/포커스/눌림/비활성 4개 상태 원본도 포함했다.
- `Level UP` 뒤의 검은 직사각형을 제거하여 기존 게임 배경이 보이게 했다.
- 문구, Galmuri9 48px 기준 크기, 제목 중심 `(960,90)`을 유지했다.
- 흰색 글자를 1px씩 확장하여 Bold 무게감을 주고, 짙은 차콜 `#17131C` 외곽선을 4px로 둘렀다.
- 제목의 흰 글자와 외곽선을 별도 레이어로 분리했다. `Level-UP-bold-outline.png`는 배경이 투명하다.
- 카드, 설명, 하단 버튼 및 게임 배경의 나머지 부분은 기존 A 목업을 유지했다.

## 파일과 범위

- `A-selected-screen.*`: 선택 디자인의 전체 화면 원본/출력.
- `Level-UP-bold-outline.*`, `title-detail-2x.png`: 배경 없는 제목 원본/출력과 가독성 검토용 확대 이미지.
- `A-gilt-*`: 기존 A 버튼과 4개 상태의 동일 사본.
- `author-title.lua`, `title-spec.json`, `aseprite-export.log`, `source-hashes-before.json`: Aseprite 제작 소스, 구체적 제목 규격, 출력 로그와 입력 보존 기록.
- 프로젝트 Assets, 씬, 프리팹, C#, meta 및 설정은 수정하지 않았다. 최초 5개 시안도 보관했다.

## 검토와 남은 작업

- Aseprite batch 출력 성공, 전체 화면/제목 확대 이미지 시각 검토, 입력 보존과 이미지 변경 영역을 확인했다.
- 이 화면은 [이전 시안](../README.md)과 같은 프리팹 기반 재구성이다. 실제 플레이 캡처가 아니다.
- Bold는 Aseprite 픽셀 레이어에서 표현한 외형이다. 실제 적용 때 TMP `Bold`와 외곽선 재질을 맞춰야 하며, 래스터 제목으로 런타임 번역 문자열을 대체하지 않는다.
- Unity import/컴파일, 자동 테스트, Play Mode, Player build는 실행하지 않았다. 실제 TMP와 다른 언어의 제목 가독성은 적용 단계에서 확인해야 한다.

Doc Impact Check: **SessionLog + DecisionLog**. [세션 기록](../../../Docs/SessionLogs/2026-11-03.md)과 [선택 기록](../../../Docs/DecisionLog.md) 갱신. ErrorLog/StructureMemory/Architecture/Contracts/Presentation HTML 변경 없음. 이 수정으로 새 Presentation HTML stale candidate 없음.
