# 창 파편·기사 휘장 유물 디자인 추가 4안 — 2026-11-05

내장 image_gen을 사용해 창 파편 2안과 실제 기사의 가슴에 다는 휘장 2안을 추가 제작했습니다. 이전 5종과 게임 에셋은 교체하지 않았습니다.

시트: 윗줄 창 파편 A/B, 아랫줄 기사 휘장 A/B.

| 위치 | 디자인 | 32×32 | 확대본 | 색 수 |
| --- | --- | --- | --- | --- |
| 왼쪽 위 | 창 파편 A — 부러진 창끝 | [PNG](01-spear-tip-fragment-32x32.png) | [8배](01-spear-tip-fragment-preview.png) | 5 |
| 오른쪽 위 | 창 파편 B — 갈라진 창날 밑동 | [PNG](02-spear-collar-fragment-32x32.png) | [8배](02-spear-collar-fragment-preview.png) | 5 |
| 왼쪽 아래 | 기사 휘장 A — 방패형 검 문장 | [PNG](03-knightly-shield-badge-32x32.png) | [8배](03-knightly-shield-badge-preview.png) | 6 |
| 오른쪽 아래 | 기사 휘장 B — 날개형 검 훈장 | [PNG](04-knightly-winged-badge-32x32.png) | [8배](04-knightly-winged-badge-preview.png) | 6 |

[64×64 시트](four-relics-64x64.png) · [가로 시트](four-relics-128x32.png) · [세로 시트](four-relics-32x128.png) · [확대 미리보기](four-relics-preview.png) · [PNG 묶음](four-designs-native.zip)

## 제작 기준과 확인

- 기존 승인 시트의 둥근 실루엣, 큰 색면과 단순한 음영을 유지했습니다. 이전에 호평받은 뇌창 창날은 파편의 색/형태 참고로, 용사의 검은 휘장의 검 문양 참고로 사용했습니다.
- 창 파편 A는 부러진 창끝과 푸른 단면, B는 중앙 창날이 사라진 밑동과 비대칭 갈고리 조각입니다. 휘장 A는 방패형 검 문장, B는 날개 장식 검 훈장입니다.
- 32×32, 2픽셀 둥근 외곽선, 사방 최소 2픽셀 투명 여백, 허용 팔레트, 이진 알파를 확인했습니다. 생성 원본을 최근접 축소/팔레트 정리하고 이전 작업의 외곽선 처리 방식으로 내보냈습니다.
- 시트와 개별 PNG가 일치하고 확대본은 정확한 최근접 8배입니다.
- [전체 생성 프롬프트와 참고 경로](generation-jobs.json), [내보내기 설정](generation-sources.json), [파일 확인 기록](verification.json).

파일 변경: 이 output 폴더의 초안/시트/미리보기/프롬프트/내보내기 자료만 추가. 게임 에셋·이름·효과·연결은 변경하지 않았습니다. 디자인 선택과 프로젝트 적용은 후속 결정입니다. Unity import/실행 및 자동화 테스트는 미실행. Doc Impact Check: 프로젝트 문서 갱신 불필요. SessionLogs/ErrorLog/DecisionLog/Presentation HTML 변경 및 새 HTML stale candidate 없음.
