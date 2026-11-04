# 유물 6종 승인 이미지 적용 — 2026-11-04

첨부 순서로 적용했습니다. PNG는 색·여백·픽셀을 수정하지 않고 바이트 그대로 복사했습니다. 이름 변경 2종은 5개 언어의 기존 항목도 함께 갱신했습니다.

| 순서 | 유물 | 적용 이미지 | 정의 |
| --- | --- | --- | --- |
| 1 | 목각 주사위 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_WoodenDice.png) | [RD_BronzeDice.asset](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_BronzeDice.asset) |
| 2 | 질풍의 석판 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_WindTablet.png) | [RD_WindTablet.asset](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_WindTablet.asset) |
| 3 | 전투 장갑 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_CombatGloves.png) | [RD_CombatGloves.asset](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_CombatGloves.asset) |
| 4 | 낡은 숫돌 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_WornWhetstone.png) | [RD_WornWhetstone.asset](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_WornWhetstone.asset) |
| 5 | 장인의 숫돌 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_MasterWhetstone.png) | [RD_MasterWhetstone.asset](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_MasterWhetstone.asset) |
| 6 | 독한 한잔 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_DrunkenRush.png) | [RD_DrunkenRush.asset](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_DrunkenRush.asset) |

## 이름 번역

| 기존 정의 | KO | EN | JA | zh-Hans | zh-Hant |
| --- | --- | --- | --- | --- | --- |
| RD_BronzeDice | 목각 주사위 | Wooden Die | 木彫りのサイコロ | 木雕骰子 | 木雕骰子 |
| RD_DrunkenRush | 독한 한잔 | A Stiff Drink | 強い一杯 | 烈酒一杯 | 烈酒一杯 |

외국어는 검수 전 초안입니다. 정의 파일명·내부 이름·relicId·정의 GUID·효과·레벨·logic·번역 키·entry ID는 유지했습니다.

질풍의 석판과 독한 한잔은 기존 PNG만 교체해 메타데이터/GUID/연결을 보존했습니다. 나머지 4종은 새 PNG와 Point / 32 PPU / 단일 스프라이트 / 밉맵 없음 / 압축 없음 .meta를 추가하고 해당 정의의 icon만 변경했습니다. 목각 주사위와 독한 한잔의 displayName도 변경했습니다.

CSV의 기존 이름 2행과 다섯 StringTable의 값 2개씩만 변경했습니다. 모든 기존 변경 파일 13개의 결과 바이트가 작업 전 백업에 허용한 치환만 수행한 결과와 정확히 일치합니다. 원본 PNG 일치 6/6, 이름 번역 일치 10/10, 기타 보호 파일 382개 해시 보존과 정의 diff 공백 검사를 통과했습니다.

[전체 변경 파일·검증 기록](verification.json) · [SessionLog](../../../Docs/SessionLogs/2026-11-04.md)

Unity import·컴파일·Play Mode와 실제 화면/언어 전환은 실행하지 않았습니다. C# 변경은 없습니다. Doc Impact Check: SessionLog. 이 작업으로 ErrorLog/DecisionLog/StructureMemory/Presentation HTML은 변경하지 않았으며 새로운 HTML stale candidate도 없습니다.
