# 날렵한 망토·전사의 맹세·얇은 갑옷 조각·균형추 초안 — 2026-11-04

내장 이미지 생성 기능으로 각각 새로 그렸습니다. 기존 8개 참고 아이콘과 사용자가 가장 만족한 균열석·부서진 왕관·질긴 붕대·승리의 깃발 4종을 함께 화풍 기준으로 사용했습니다. 프로젝트에는 적용하지 않았습니다.

![4종 미리보기](four-relics-preview.png)

순서: 왼쪽 위 날렵한 망토 → 오른쪽 위 전사의 맹세 → 왼쪽 아래 얇은 갑옷 조각 → 오른쪽 아래 균형추.

| 유물 | 원본 | 확대본 | 색 수(외곽선 포함) |
| --- | --- | --- | --- |
| 날렵한 망토 | [32×32 PNG](01-agile-cloak-32x32.png) | [8배 PNG](01-agile-cloak-preview.png) | 5 |
| 전사의 맹세 | [32×32 PNG](02-warriors-oath-32x32.png) | [8배 PNG](02-warriors-oath-preview.png) | 5 |
| 얇은 갑옷 조각 | [32×32 PNG](03-thin-armor-shard-32x32.png) | [8배 PNG](03-thin-armor-shard-preview.png) | 4 |
| 균형추 | [32×32 PNG](04-counterweight-32x32.png) | [8배 PNG](04-counterweight-preview.png) | 4 |

- [2×2 원본 시트: 64×64](four-relics-64x64.png)
- [세로 원본 시트: 32×128](four-relics-32x128.png)
- [가로 원본 시트: 128×32](four-relics-128x32.png)
- [PNG 파일 묶음](four-relics-native.zip)
- [생성 프롬프트와 색상 목록](prompts.json)
- [이번에 기록한 승인 화풍 기준](../../style-review-2026-11-04/approved-followup-style.md) · [화풍 분석 기준](../../style-review-2026-11-04/README.md)

날렵한 망토는 짧은 후드와 바람에 흐르는 녹색 천, 전사의 맹세는 검을 새긴 두꺼운 동제 증표, 얇은 갑옷 조각은 뜯겨 나온 곡면 금속판과 리벳, 균형추는 두툼한 고리 손잡이와 무거운 금속 몸통을 표현했습니다. 갑옷 조각의 첫 생성 시도가 도구에서 거절되어, 단순한 금속판 조각으로 명확히 한 재시도 결과를 사용했습니다. 두 프롬프트 모두 보존했습니다.

큰 생성 원본에서 최근접 샘플링과 지정 팔레트 정리 후 실제 32×32로 내보냈습니다. 둥근 대각 모서리의 2픽셀 외곽선을 정리하고, 균형추 손잡이의 투명 구멍 13픽셀을 보존했습니다. 모든 프레임은 사방 최소 2픽셀 투명 여백과 알파 0/255를 유지합니다.

[크기·색상·여백·시트 픽셀·프로젝트 보존 확인](verification.json). 32×32 원본과 확대본을 육안 확인했습니다. 초안·시트·미리보기·생성 원본 사본·프롬프트·내보내기 스크립트·확인 자료는 이 폴더에만 생성했습니다. 사용자의 요청에 따라 기존 output 화풍 README에 승인 기준 링크를 추가하고 승인 기준 파일을 기록했습니다. 프로젝트 Assets·Docs·기존 로그는 변경하지 않았습니다.

Doc Impact Check: no project doc update needed. SessionLogs/ErrorLog/DecisionLog/Presentation HTML 변경 및 새로운 HTML stale candidate 없음. Unity import/자동화 테스트는 실행하지 않았습니다.
