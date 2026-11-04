# 빛 구슬 — 상하 움직임 없는 색상 순환

2026-11-03 후속 상태: 공용 경험치 픽업에 이 애니메이션을 연결했다. [게임 적용 기록](../../experience-pickup-integration-2026-11-03/README.md) 참고. 아래 검증 범위는 11월 2일 견본 제작 당시 기록이다.

2026-11-02. 사용자가 선택한 2번 빛 구슬의 유사 색상들을 애니메이션으로 연결하고 상하 움직임을 빼도록 요청했다. Implementation 범위는 애니메이션 원본과 전달 파일 제작이다.

- **16×16 캔버스, 실제 구슬 8×8, 2레이어, 40프레임, 100ms/프레임, 4초 무한 반복.**
- [이전 색상 견본](../revision-5-lumen-colors/README.md)의 A(연두) → B(라임) → C(레몬 라임) → E(따뜻한 연두) → D(민트) → A로 순환한다. 비슷한 색이 이어지도록 C 다음에 E를 배치했다.
- 각 팔레트 사이를 8프레임씩 부드럽게 보간한다. 각 프레임은 기존의 외곽·몸체·안쪽·중심 역할을 유지하는 불투명 4색이다.
- 모든 프레임에서 동일한 52픽셀을 사용하고, 투명 영역·위치·크기·모양이 같다. 상하/좌우 이동, 확대·축소, 알파 깜빡임이 없다.

## 파일

| 파일 | 용도 |
| --- | --- |
| [lumen-orb-color-cycle.aseprite](lumen-orb-color-cycle.aseprite) | 편집 가능한 native Aseprite 애니메이션 |
| [lumen-orb-color-cycle-sheet.png](lumen-orb-color-cycle-sheet.png) | 16×16 셀 40개를 왼쪽부터 배치한 640×16 투명 시트 |
| [lumen-orb-color-cycle.json](lumen-orb-color-cycle.json) | Aseprite가 내보낸 프레임 사각형·100ms 시간·레이어·태그 정보 |
| [lumen-orb-color-cycle.png](lumen-orb-color-cycle.png) | 첫 프레임 투명 PNG |
| [lumen-orb-color-cycle.gif](lumen-orb-color-cycle.gif) | 16×16 원래 크기의 투명 반복 GIF |
| [확대 미리보기 GIF](lumen-orb-color-cycle-preview.gif) | 고정된 큰 구슬과 어두운/돌색 배경 위 작은 구슬 비교 |
| [미리보기 Aseprite](lumen-orb-color-cycle-preview.aseprite), [첫 화면 PNG](lumen-orb-color-cycle-preview.png) | 편집 가능한 미리보기 구성과 정지 이미지 |
| [전체 ZIP](lumen-orb-stationary-color-cycle.zip) | 원본·시트·GIF·메타데이터·제작 자료 묶음 |
| [author-color-cycle.lua](author-color-cycle.lua) | Aseprite 1.3.18.6에서 사용한 제작 자료 |

미리보기 배경과 문구는 비교용이며 실제 투명 스프라이트에 포함되지 않는다. 미리보기는 Unity 캡처가 아니다.

## 확인 및 남은 작업

- Aseprite 원본을 다시 열어 시트/JSON/투명 GIF/확대 GIF 출력 완료, 모두 exit 0.
- JSON 기준 40프레임 ×100ms =4000ms, 2레이어. 시트는 640×16이며 각 프레임은 4색이다.
- 선택한 원본의 정지 위치와 비교한 40프레임 알파 차이 0픽셀: 구슬이 움직이지 않는 것을 확인했다.
- 투명 GIF와 native PNG 시트의 40프레임을 비교했을 때 보이는 RGBA/알파 차이 0픽셀이다. GIF 40프레임, loop count 0(무한 반복). 확대 GIF도 40프레임이다.
- 첫 화면을 시각 검토했고 로컬 Markdown 링크를 확인했다. Unity import·컴파일·Play Mode·자동 테스트는 실행하지 않았다.
- 게임 자산/코드는 변경하지 않았다. 실제 게임 적용, 표시 크기와 기존 SpriteRenderer tint 조정은 후속 작업이다.

변경 파일은 위의 새 애니메이션/전달 파일, 최신안을 연결한 상위 README, 결과를 기록한 [SessionLog](../../../Docs/SessionLogs/2026-11-02.md)다. Doc Impact는 **SessionLog**. ErrorLog·DecisionLog·StructureMemory 변경과 Presentation HTML stale 후보는 없다.
