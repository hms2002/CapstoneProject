# 검투사의 완갑·마력의 성배·순풍의 소라·불굴의 심재 초안 — 2026-11-04

사용자가 선택한 완갑·성배·소라·심재 디자인을 내장 이미지 생성 기능으로 각각 제작했습니다. 승인 화풍인 균열석·왕관·붕대·깃발 4종과 기존 8종, 기존 팔레트를 참조했습니다. 제안 이름이며 프로젝트의 이름·효과·이미지는 변경하지 않았습니다.

![4종 미리보기](four-relics-preview.png)

왼쪽 위 검투사의 완갑 → 오른쪽 위 마력의 성배 → 왼쪽 아래 순풍의 소라 → 오른쪽 아래 불굴의 심재.

| 제안 디자인 | 기존 유물 | 원본 | 확대본 | 색 수(외곽선 포함) |
| --- | --- | --- | --- | --- |
| 검투사의 완갑 | 깨진 시계 | [32×32 PNG](01-gladiators-bracer-32x32.png) | [8배 PNG](01-gladiators-bracer-preview.png) | 5 |
| 마력의 성배 | 검은 향로 | [32×32 PNG](02-arcane-chalice-32x32.png) | [8배 PNG](02-arcane-chalice-preview.png) | 6 |
| 순풍의 소라 | 여유로운 심장 | [32×32 PNG](03-fair-wind-conch-32x32.png) | [8배 PNG](03-fair-wind-conch-preview.png) | 6 |
| 불굴의 심재 | 마지막 숨 | [32×32 PNG](04-unyielding-heartwood-32x32.png) | [8배 PNG](04-unyielding-heartwood-preview.png) | 5 |

- [2×2 원본 시트: 64×64](four-relics-64x64.png)
- [세로 원본 시트: 32×128](four-relics-32x128.png)
- [가로 원본 시트: 128×32](four-relics-128x32.png)
- [PNG 파일 묶음](four-relics-native.zip)
- [생성 프롬프트 4개와 색상 목록](prompts.json) · [승인 화풍 기준](../../style-review-2026-11-04/README.md)

## 디자인

- 검투사의 완갑: 겹쳐지는 두 금속 면과 붉은 안감으로 짧고 두툼한 팔 보호대 형태를 잡았습니다.
- 마력의 성배: 넓은 금빛 잔·짧고 굵은 줄기·넓은 받침과 보랏빛 액체로 구성했습니다.
- 순풍의 소라: 상아색 나선과 청록색 입구를 크게 남기고 복잡한 껍질 줄무늬는 생략했습니다.
- 불굴의 심재: 짙은 나무 겉면·큰 균열·밝은 단면과 굵은 나이테 하나로 부러진 심재를 표현했습니다.

## 형식과 확인

큰 생성 래스터를 최근접 샘플링·허용 팔레트 정리 후 실제 32×32로 내보내고 둥근 2픽셀 외곽선과 모서리를 정리했습니다. 사방 최소 2픽셀 투명 여백, 알파 0/255, 기존 팔레트 내 색만 사용합니다. 불필요하게 색 수를 채우지 않았습니다.

[크기·색상·여백·시트/확대본 픽셀·프로젝트 보존 확인](verification.json). 32×32 원본과 확대본에서 형태를 육안 확인했습니다. 이 출력 폴더에만 초안/생성 원본 사본/프롬프트/시트/스크립트/검증 기록을 저장했습니다. Assets·이름·효과·번역·기존 Docs/로그는 변경하지 않았습니다.

Doc Impact Check: no doc update needed. SessionLogs/ErrorLog/DecisionLog/Presentation HTML 변경 및 새로운 HTML stale candidate 없음. Unity import/자동화 테스트는 실행하지 않았습니다.
