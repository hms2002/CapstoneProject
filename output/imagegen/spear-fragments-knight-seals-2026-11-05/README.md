# 창 파편·기사 휘장 추가 시안 — 2026-11-05

내장 image_gen으로 디자인별 개별 생성했습니다. 기존 게임 에셋과 연결은 변경하지 않았습니다.

시트 윗줄: 창 파편 A / B. 아랫줄: 기사 휘장 A / B.

| 시안 | 디자인 | 원본 | 확대 | 색 수 |
| --- | --- | --- | --- | --- |
| 창 파편 A — 맞물린 조각 | 창날의 흐름이 이어지는 세 조각 | [32×32](01-spear-fragments-a-32x32.png) | [8배](01-spear-fragments-a-preview.png) | 4 |
| 창 파편 B — 흩어진 조각 | 창끝·갈고리·밑부분이 흩어진 세 조각 | [32×32](02-spear-fragments-b-32x32.png) | [8배](02-spear-fragments-b-preview.png) | 5 |
| 기사 휘장 A — 쌍띠 인장 | 붉은 인장 아래 길게 내려오는 두 줄 양피지 | [32×32](03-knight-seal-a-32x32.png) | [8배](03-knight-seal-a-preview.png) | 6 |
| 기사 휘장 B — 접힌 서약지 | 붉은 인장 뒤에서 접혀 흐르는 한 장의 서약지 | [32×32](04-knight-seal-b-32x32.png) | [8배](04-knight-seal-b-preview.png) | 6 |

[64×64 시트](four-relics-64x64.png) · [가로 시트](four-relics-128x32.png) · [세로 시트](four-relics-32x128.png) · [확대 미리보기](four-relics-preview.png) · [PNG 묶음](four-variants-native.zip)

팔레트는 기존 허용 색으로 제한하고 2픽셀 둥근 외곽선을 적용했습니다. 사방 최소 2픽셀 여백, 32×32, 이진 알파, 각 시트와 개별 PNG 일치, 정확한 최근접 8배 확대를 확인했습니다.

큰 생성 원본을 보관하고, 최근 작업과 같은 픽셀 중심 최근접 축소·팔레트 제한·둥근 외곽선 내보내기로 구성했습니다. 생성된 내부 투명 틈은 유지했습니다. [프롬프트·참고 경로](generation-jobs.json), [내보내기 설정](generation-sources.json), [확인 기록](verification.json).

초안 산출물만 추가했습니다. Unity import/실행 및 자동화 테스트 미실행. Doc Impact Check: 프로젝트 문서 갱신 불필요. SessionLogs/ErrorLog/DecisionLog/Presentation HTML 변경 및 새 HTML stale candidate 없음.
