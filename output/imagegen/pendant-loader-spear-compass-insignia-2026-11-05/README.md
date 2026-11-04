# 유물 5종 초안 — 2026-11-05

내장 image_gen으로 개별 생성한 초안입니다. 게임 에셋·이름·효과·번역·연결은 수정하지 않았습니다.

시트 순서: 윗줄 왼쪽부터 1–3, 아랫줄 왼쪽부터 4–5.

| 번호 | 유물 | 디자인 | 32×32 | 확대본 | 색 수 |
| --- | --- | --- | --- | --- | --- |
| 1 | 회복 펜던트 | 포션 문양이 들어간 하트 펜던트 | [PNG](01-recovery-pendant-32x32.png) | [8배](01-recovery-pendant-preview.png) | 6 |
| 2 | 쇳덩이의 예비 탄약 | 탄약을 묶어 장전하는 스피드 로더 | [PNG](02-reserve-speedloader-32x32.png) | [8배](02-reserve-speedloader-preview.png) | 6 |
| 3 | 뇌창의 세 번째 번개 | 뇌창의 갈고리형 창날과 푸른 전류 홈 | [PNG](03-third-lightning-spearhead-32x32.png) | [8배](03-third-lightning-spearhead-preview.png) | 5 |
| 4 | 뇌창의 추적 낙뢰 | 같은 창날이 바늘인 마법 나침반 | [PNG](04-tracking-thunder-compass-32x32.png) | [8배](04-tracking-thunder-compass-preview.png) | 6 |
| 5 | 견습 용사의 연계 증표 | 용사의 검 가드와 푸른 손잡이를 본뜬 휘장 | [PNG](05-hero-link-insignia-32x32.png) | [8배](05-hero-link-insignia-preview.png) | 6 |

[96×64 시트](five-relics-96x64.png) · [160×32 가로 시트](five-relics-160x32.png) · [32×160 세로 시트](five-relics-32x160.png) · [확대 미리보기](five-relics-preview.png) · [PNG 묶음](five-relics-native.zip)

## 제작 및 확인

- 승인받았던 균열석·왕관·붕대·깃발 시트와 원본 8종을 화풍 참고로 사용했습니다. 뇌창/용사의 검 원본은 무기 고유 형태 참고에 사용했습니다.
- 32×32, 이진 알파, 허용 팔레트, 외곽선을 포함한 사방 최소 2픽셀 여백을 파일에서 확인했습니다. 2픽셀 외곽선은 둥근 모서리 방식으로 구성했습니다.
- 생성 원본은 개별 `*-source.png`로 보관했습니다. 최근 작업과 같은 픽셀 중심 최근접 축소·팔레트 제한·둥근 외곽선 처리로 작은 PNG를 내보냈으며, 원본의 내부 투명 구멍은 유지했습니다.
- 확대본은 정확한 최근접 8배이며, 시트의 각 칸은 개별 PNG와 같습니다.
- [전체 생성 프롬프트 및 참고 경로](generation-jobs.json), [내보내기 설정](generation-sources.json), [파일 확인 기록](verification.json).

프로젝트 적용 전 디자인 검토용입니다. Unity import/실행 및 자동화 테스트는 실행하지 않았습니다. Doc Impact Check: 프로젝트 문서 갱신 불필요(초안 산출물만 추가). SessionLogs/ErrorLog/DecisionLog/Presentation HTML 변경 및 새 HTML stale candidate 없음.
