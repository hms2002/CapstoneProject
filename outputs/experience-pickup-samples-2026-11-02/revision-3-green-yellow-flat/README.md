# 경험치 알갱이 revision 3 — 초록·노랑 혼합, 평면 도트

2026-11-02. 사용자는 중간색으로 바꾸는 것이 아니라 색을 섞으려는 뜻이라고 정정했고, 반사광 제거와 음영 생략을 요청했다. 이번 수정은 기존 다섯 모양에 초록·노랑 두 색을 섞는 Implementation 범위다.

- 초록 `#70D64B`와 노랑 `#F1DF4D`을 불규칙한 작은 도트 덩어리로 함께 배치했다. 광원 방향, 반사광, 음영, 어두운 외곽선을 제거했다.
- 16×16, 정지 1프레임, 초록/노랑 픽셀별 2레이어. 기존 투명 영역과 모양은 그대로다. 배경은 완전 투명이다.
- [비교 이미지](five-mixed-pickups-preview.png) · [비교판 Aseprite](five-mixed-pickups.aseprite) · [전체 ZIP](mixed-flat-xp-five-samples.zip).

| 번호 | 디자인 | Aseprite | PNG |
| --- | --- | --- | --- |
| 1 | 구슬 | [원본](01-mixed-orb.aseprite) | [PNG](01-mixed-orb.png) |
| 2 | 결정 | [원본](02-mixed-shard.aseprite) | [PNG](02-mixed-shard.png) |
| 3 | 방울 | [원본](03-mana-drop.aseprite) | [PNG](03-mana-drop.png) |
| 4 | 별 | [원본](04-star-mote.aseprite) | [PNG](04-star-mote.png) |
| 5 | 씨앗 | [원본](05-spirit-seed.aseprite) | [PNG](05-spirit-seed.png) |

변경 파일: 이 폴더의 Aseprite/PNG/12배 확대본/비교판은 수정된 견본, [Lua 파일](author-samples.lua)은 Aseprite 제작 자료, README/ZIP은 전달 자료다. 상위 README에는 최신안 링크를 갱신했고 SessionLog에는 수정 의도와 확인 결과를 기록했다. 이전 견본은 보존했다.

확인: 비교판 시각 검토. 5종 모두 native Aseprite 재열기/재출력 exit 0 및 동일 PNG SHA-256. 실제 픽셀 색은 요청한 초록·노랑 2개뿐이며 기존 대비 투명도 차이는 0픽셀이다. README 링크도 확인했다.

게임 적용·Unity import·컴파일·Play Mode·자동 테스트는 실행하지 않았다. 선택과 게임 내 표시 크기 확인이 남아 있다. Doc Impact는 **SessionLog**이며 ErrorLog·DecisionLog·StructureMemory 변경과 Presentation HTML stale 후보는 없다.
