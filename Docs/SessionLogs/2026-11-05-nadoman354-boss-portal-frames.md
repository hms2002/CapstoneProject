# 보스 포탈 테두리 시안

2026-11-05 · nadoman354 · Implementation (Aseprite 견본 제작)

게임에 적용된 네 색상을 기준으로 외곽 프레임을 새로 그렸다. 비교판 위쪽은 5배 확대, 아래쪽은 보스별로 현재 포탈과 시안을 1배 크기로 나란히 놓았다. [비교판 PNG](../../outputs/boss-portal-frames-2026-11-05/boss-portal-frames.png)

| 보스 | 디자인 | 편집 원본 | 투명 PNG |
| --- | --- | --- | --- |
| 클로에 | 회보라 후드 아래 작은 눈을 넣고 양쪽 천 주름에 그림자 정령을 매달았다. | [Aseprite](../../outputs/boss-portal-frames-2026-11-05/01-chloe.aseprite) | [PNG](../../outputs/boss-portal-frames-2026-11-05/01-chloe.png) |
| 스피리 | 한 쌍의 금빛 뿔 사이에 불꽃을 두고 두 기둥을 붉은 겹비늘로 감쌌다. | [Aseprite](../../outputs/boss-portal-frames-2026-11-05/02-spiri.aseprite) | [PNG](../../outputs/boss-portal-frames-2026-11-05/02-spiri.png) |
| 멜타 | 중앙에 작은 금관을 올리고 민트색 젤리가 기둥을 타고 바닥에 고이게 그렸다. | [Aseprite](../../outputs/boss-portal-frames-2026-11-05/03-melta.aseprite) | [PNG](../../outputs/boss-portal-frames-2026-11-05/03-melta.png) |
| 마왕 | 검의 눈을 상단 가로대에 새기고 흑철 기둥을 각진 칼날 형태로 깎았다. | [Aseprite](../../outputs/boss-portal-frames-2026-11-05/04-demon-king.aseprite) | [PNG](../../outputs/boss-portal-frames-2026-11-05/04-demon-king.png) |

각 원본은 50×49 RGBA이며 테두리 바탕, 재질, 보스 장식, 막, 안쪽 빛 테두리를 5개 레이어로 분리했다. Aseprite에서 네 원본을 다시 열어 내보낸 PNG와 납품 PNG의 픽셀 차이는 모두 0이다. 게임 PNG와 대조한 결과 보스마다 막 576픽셀과 안쪽 빛 테두리 88픽셀의 RGBA가 일치했다. 확대 및 1배 비교판을 눈으로 확인했다.

이번 범위는 디자인 검토용 견본이다. 다음 결정은 보스별 테두리 채택 여부다. Unity 임포트·Play Mode·자동 테스트는 미실행이다. 문서 영향은 SessionLog 한 건이며 ErrorLog·DecisionLog와 Presentation HTML에는 이번 시안으로 인한 변경이 없다.
