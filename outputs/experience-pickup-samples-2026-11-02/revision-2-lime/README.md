# 경험치 알갱이 revision 2 — 노랑과 초록 사이의 라임색

2026-11-02. 사용자 요청에 따라 기존 5종의 모양을 유지하고 초록~노랑 중간 계열로 색상을 수정했다. Implementation 범위는 견본의 팔레트 수정이며 게임 적용은 포함하지 않는다.

| 번호 | 디자인 | Aseprite 원본 | 투명 PNG |
| --- | --- | --- | --- |
| 1 | 라임 구슬 | [원본](01-lime-orb.aseprite) | [PNG](01-lime-orb.png) |
| 2 | 라임 결정 | [원본](02-lime-shard.aseprite) | [PNG](02-lime-shard.png) |
| 3 | 마력 방울 | [원본](03-mana-drop.aseprite) | [PNG](03-mana-drop.png) |
| 4 | 별빛 알갱이 | [원본](04-star-mote.aseprite) | [PNG](04-star-mote.png) |
| 5 | 정령 씨앗 | [원본](05-spirit-seed.aseprite) | [PNG](05-spirit-seed.png) |

- [비교 미리보기](five-lime-pickups-preview.png), [편집 가능한 비교판](five-lime-pickups.aseprite), [전체 ZIP](lime-xp-five-samples.zip).
- 16×16, 2레이어, 정지 1프레임. 올리브색 외곽선에서 라임색 본체, 연노랑 반사광으로 이어지는 6색이다: `#3C491B`, `#76882B`, `#B6CF3A`, `#D9E860`, `#F0F699`, `#FFFBD8`.
- Aseprite 1.3.18.6에서 [제작 스크립트](author-samples.lua)를 실행하여 원본·PNG·12배 확대본·비교판을 만들었다. 최초 초록색 파일은 상위 폴더에 보존했다.
- 확인: 비교판 시각 검토, 5개 원본 재열기와 PNG 재출력 모두 exit 0 및 같은 SHA-256. 기존 5종과 투명도 차이 0픽셀, 모든 PNG 16×16/불투명 6색이다.
- 이번 변경 파일은 이 폴더의 색상 수정 견본과 전달 자료, 상위 README의 최신안 링크, SessionLog의 결과 기록이다.
- 게임에는 미적용. 선택과 실제 크기/tint 확인은 후속 작업이다. Unity import·컴파일·Play Mode·자동 테스트는 실행하지 않았다.
- Doc Impact: SessionLog. ErrorLog·DecisionLog·StructureMemory 변경 없음. Presentation HTML stale 후보 없음.
