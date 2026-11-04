# 선택한 2번 빛 구슬 — 비슷한 색상 변형 5종

2026-11-02. 사용자가 revision 4의 2번 Lumen Orb를 선택하고 비슷한 색의 바리에이션을 요청했다. Implementation 범위는 색상 견본 제작이다. 선택한 8×8 그림, 16×16 투명 캔버스, 중심광 배치와 기존 8프레임 idle을 유지했다. 게임 적용이나 최종 팔레트 확정은 포함하지 않는다.

[정지 비교 이미지](lumen-orb-color-variants-preview.png) · [움직이는 비교 이미지](lumen-orb-color-variants-preview.gif) · [비교판 Aseprite](lumen-orb-color-variants.aseprite) · [전체 ZIP](lumen-orb-five-color-variants.zip)

비교판 맨 왼쪽 REF는 선택한 원본이다. A~E가 새 색상 변형이다.

| 구분 | 색감 | 외곽 → 몸체 → 안쪽 → 중심 | Aseprite / PNG |
| --- | --- | --- | --- |
| REF | 선택한 원본 | `385D32 / 75B84A / C4E766 / F4FFBC` | [원본](00-original-reference.aseprite) / [PNG](00-original-reference.png) |
| A | 새싹 연두 — 초록 비중을 높임 | `315C36 / 65AD4C / A4DD67 / E9FFB8` | [원본](a-leaf-green.aseprite) / [PNG](a-leaf-green.png) |
| B | 밝은 라임 — 더 선명한 연두 | `3E622F / 80C147 / C2EE55 / F3FFAD` | [원본](b-bright-lime.aseprite) / [PNG](b-bright-lime.png) |
| C | 레몬 라임 — 노랑 비중을 높임 | `4F612E / A0BD46 / DDE866 / FFF9BB` | [원본](c-lemon-lime.aseprite) / [PNG](c-lemon-lime.png) |
| D | 민트 연두 — 시원하고 부드러운 색 | `305D42 / 68B96B / ACE49A / E9FFD0` | [원본](d-mint-green.aseprite) / [PNG](d-mint-green.png) |
| E | 따뜻한 연두 — 황록색·크림색에 가까움 | `5D5E30 / ADB349 / E6DA78 / FFF2C4` | [원본](e-warm-pear.aseprite) / [PNG](e-warm-pear.png) |

모두 불투명 4색/2레이어이며, 130ms×8프레임(1.04초)의 상하 1픽셀 idle이다. `*-idle-sheet.png`는 16×16 셀 8개가 나란히 놓인 128×16 시트다. 정지 PNG와 12배 확대본도 포함했다. 비교판은 전체 3배 확대이며 Unity 화면 캡처가 아니다.

변경 파일: 이 폴더의 Aseprite/PNG/GIF는 색상 비교 자료, [author-samples.lua](author-samples.lua)는 재현 가능한 Aseprite 제작 자료, README/ZIP은 설명과 전달 묶음이다. 상위 README는 최신 선택 경로, [SessionLog](../../../Docs/SessionLogs/2026-11-02.md)는 결과와 확인 범위를 기록한다.

확인: 정지 비교판 시각 검토. REF와 5종 모두 Aseprite 1.3.18.6에서 다시 열어 시트 출력 exit 0. 원본과 대조한 48프레임의 알파 차이는 0픽셀이며, REF의 보이는 색상 차이도 0픽셀이다. 각 변형은 4색이며 모든 불투명 픽셀만 다시 칠해졌다. 움직이는 비교판도 native Aseprite로 출력했다. README 로컬 링크를 확인했다.

게임 파일, Unity import·컴파일·Play Mode·자동 테스트는 이번 작업에서 수정/실행하지 않았다. 최종 색상 선택과 실제 게임 크기/tint 확인이 남는다. Doc Impact는 **SessionLog**. ErrorLog·DecisionLog·StructureMemory 변경과 Presentation HTML stale 후보는 없다.
