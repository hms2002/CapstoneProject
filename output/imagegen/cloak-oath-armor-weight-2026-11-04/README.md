# 날렵한 망토·전사의 맹세·얇은 갑옷 조각·균형추 초안 — 2026-11-04

사용자가 직전 균열석·왕관·붕대·깃발 4종을 완벽한 작업으로 평가하고 같은 느낌을 요청했습니다. 해당 4종을 가장 최근 승인 화풍으로 우선 참조하고, 기존 8종과 팔레트를 함께 사용해 내장 이미지 생성 기능으로 이번 4종을 새로 그렸습니다. 프로젝트에는 적용하지 않았습니다.

![4종 미리보기](four-relics-preview.png)

왼쪽 위 날렵한 망토 → 오른쪽 위 전사의 맹세 → 왼쪽 아래 얇은 갑옷 조각 → 오른쪽 아래 균형추.

| 유물 | 원본 | 확대본 | 사용 색 수(외곽선 포함) |
| --- | --- | --- | --- |
| 날렵한 망토 | [32×32 PNG](01-nimble-cloak-32x32.png) | [8배 PNG](01-nimble-cloak-preview.png) | 5 |
| 전사의 맹세 | [32×32 PNG](02-warriors-oath-32x32.png) | [8배 PNG](02-warriors-oath-preview.png) | 6 |
| 얇은 갑옷 조각 | [32×32 PNG](03-thin-armor-shard-32x32.png) | [8배 PNG](03-thin-armor-shard-preview.png) | 4 |
| 균형추 | [32×32 PNG](04-balance-weight-32x32.png) | [8배 PNG](04-balance-weight-preview.png) | 5 |

- [2×2 원본 시트: 64×64](four-relics-64x64.png)
- [세로 원본 시트: 32×128](four-relics-32x128.png)
- [가로 원본 시트: 128×32](four-relics-128x32.png)
- [PNG 파일 묶음](four-relics-native.zip)
- [생성 프롬프트 4개와 색상 목록](prompts.json) · [망토 빈 부분 수정 프롬프트](cloak-correction-prompt.json)
- [우선 참조한 승인 4종](../rift-crown-bandage-flag-2026-11-04/README.md) · [승인 이력을 저장한 화풍 기준](../../style-review-2026-11-04/README.md)

망토는 초록 천·금색 여밈과 흐르는 큰 면, 맹세는 검 문양이 박힌 금빛/붉은 메달, 갑옷 조각은 휘어진 밝은 금속판과 간결한 이음선, 균형추는 회색 금속 덩어리와 짧고 두꺼운 구리 고리로 구분했습니다. 망토 생성본에 생긴 의도하지 않은 빈 얼룩은 이미지 생성 편집으로 제거했습니다. 균형추 고리의 작은 투명 구멍은 남겼습니다.

큰 생성 래스터를 최근접 샘플링·허용 팔레트 정리 후 실제 32×32로 내보내고 둥근 2픽셀 외곽선과 모서리를 정리했습니다. 최종 PNG는 사방 최소 2픽셀 투명 여백, 알파 0/255, 기존 팔레트 내 색만 사용합니다. 억지로 색 수를 채우지 않았습니다.

[크기·색상·여백·알파·시트/확대 픽셀 보존 확인](verification.json). 32×32 및 확대본 형태를 육안 확인했습니다. 새 파일은 이 출력 폴더에 저장했고, 사용자가 기억해 달라고 요청한 승인 화풍 이력은 output/style-review-2026-11-04의 README와 생성용 프롬프트에 추가했습니다. 프로젝트 Assets·기존 Docs/로그는 변경하지 않았습니다.

Doc Impact Check: no project doc update needed. SessionLogs/ErrorLog/DecisionLog/Presentation HTML 변경 및 새로운 HTML stale candidate 없음. Unity import/자동화 테스트는 실행하지 않았습니다.
