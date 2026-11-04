# 경험치 알갱이 — Aseprite 견본

최신안: 선택한 2번 빛 구슬에 [revision 5의 색상 변형](revision-5-lumen-colors/README.md)을 연결한 **[제자리 색상 순환 애니메이션 — revision 6](revision-6-stationary-color-cycle/README.md)**. 상하 움직임을 제거했고 4초 주기로 부드럽게 색만 바뀐다. 2026-11-03 공용 경험치 프리팹에 연결했으며, 적용 범위와 검증은 [게임 적용 기록](../experience-pickup-integration-2026-11-03/README.md)에 있다. [revision 4의 5종](revision-4-researched/README.md), 아래 최초 초록색 원본, [revision 2](revision-2-lime/README.md), [revision 3](revision-3-green-yellow-flat/README.md)은 이전 비교안이다.

2026-11-02. 사용자 요청 범위는 초록색 계열 경험치 알갱이 견본 제작이다. Implementation 모드로 새 견본 파일만 작성했다. 일치하는 기존 ActiveTask는 없으며, 현재 요청을 범위로 사용했다.

## 견본

| 번호 | 디자인 | Aseprite 원본 | 투명 PNG |
| --- | --- | --- | --- |
| 1 | 비취 구슬: 둥글고 작은 경험치 알갱이 | [01-jade-orb.aseprite](01-jade-orb.aseprite) | [01-jade-orb.png](01-jade-orb.png) |
| 2 | 에메랄드 결정: 면이 나뉜 다이아몬드 | [02-emerald-shard.aseprite](02-emerald-shard.aseprite) | [02-emerald-shard.png](02-emerald-shard.png) |
| 3 | 마력 방울: 위로 꼬리가 뻗은 물방울 | [03-mana-drop.aseprite](03-mana-drop.aseprite) | [03-mana-drop.png](03-mana-drop.png) |
| 4 | 별빛 알갱이: 밝은 중심과 네 방향 빛살 | [04-star-mote.aseprite](04-star-mote.aseprite) | [04-star-mote.png](04-star-mote.png) |
| 5 | 정령 씨앗: 비스듬한 타원과 안쪽 능선 | [05-spirit-seed.aseprite](05-spirit-seed.aseprite) | [05-spirit-seed.png](05-spirit-seed.png) |

- 각 원본: 16×16 RGBA, 정지 1프레임, `Body and silhouette` / `Light and facets` 2레이어.
- 공통 6색: `#123F36`, `#16704C`, `#29AC59`, `#75DE68`, `#B5F48A`, `#EDFFD4`. 배경은 완전 투명, 그림은 완전 불투명 픽셀만 사용한다.
- [비교 이미지](five-green-pickups-preview.png): 확대 5종과 어두운/밝은 바닥 위 반복 배치. 아래 줄은 원본 픽셀 격자 기준 배치이며, 전체 게시 이미지는 3배 확대되어 있다. 실제 게임 화면이나 적용 크기를 보여주는 캡처는 아니다.
- [편집 가능한 비교판](five-green-pickups.aseprite), [비교판 원래 크기 PNG](five-green-pickups.png), 각 디자인의 `-12x.png` 확대본도 포함했다.
- [전체 ZIP](green-xp-five-samples.zip): 원본, PNG, 확대본, 비교판, 제작 스크립트와 이 설명.

## 제작과 확인

설치된 Aseprite 1.3.18.6의 Lua API로 정수 좌표에 직접 도트를 작성했다. [제작 스크립트](author-samples.lua)는 픽셀 배치와 레이어, 최근접 방식 확대를 재현한다. Aseprite의 `--batch --script-param output=<이 폴더의 절대 경로> --script <author-samples.lua의 절대 경로>`로 실행한다.

- 비교판을 시각적으로 확인했다. 작은 반복 배치와 밝고 어두운 바닥을 함께 확인했다.
- 5개 `.aseprite`를 Aseprite CLI로 다시 열고 PNG로 내보냈다. 모두 종료 코드 0이며 기존 PNG와 SHA-256이 같았다.
- PNG는 모두 16×16, 불투명 색상 6개, 반투명 픽셀 0개이다. 완전 투명 픽셀은 순서대로 146 / 171 / 160 / 183 / 150개이다.
- Unity import, 컴파일, Play Mode, 자동 테스트는 실행하지 않았다. 게임 코드, Assets, 프리팹, 씬, 메타데이터는 이번 작업에서 수정하지 않았다.

## 최초 견본 제작 당시 적용 전 남은 사항

디자인 선택과 실제 플레이 화면에서의 크기 확인이 남아 있다. 현재 `ExperiencePickup_Square.prefab`은 0.18 스케일과 초록색 SpriteRenderer tint를 사용한다. 향후 선택안을 연결할 때 새 PNG 색상이 중복 착색되지 않도록 tint와 PPU/표시 크기를 함께 결정해야 한다. 이번 견본에서는 그 값을 변경하지 않았다. 경험치 지급·추적·흡수 연출도 변경하지 않았다.

## 변경 파일과 문서 영향

- 이 폴더의 Aseprite/PNG: 요청된 5종 원본, 게임 적용 후보 이미지와 비교 미리보기.
- `author-samples.lua`: Aseprite 원본의 재현 가능한 픽셀 제작 자료.
- `README.md`, ZIP: 선택과 후속 편집을 위한 설명 및 전달 묶음.
- [SessionLog](../../Docs/SessionLogs/2026-11-02.md): 제작 결과와 확인 범위 기록.
- Doc Impact Check: **SessionLog**. ErrorLog / DecisionLog / StructureMemory / Architecture / Contracts 변경 없음. Presentation HTML stale 후보 없음.
