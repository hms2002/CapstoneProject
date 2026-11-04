# 유물 9종 승인 이미지 및 이름 8종 변경 — 2026-11-04

모드: Micro-fix. 첨부 순서대로 사용자 수정 PNG 원본 바이트를 그대로 적용하고 요청한 이름 8개를 변경했습니다. 반격의 박차는 이름을 유지하며, 보약 곰방대는 이번 변경에서 제외했습니다. 효과는 유지했습니다.

| 순서 | 현재 이름 | 적용 PNG | 기존 정의 |
| --- | --- | --- | --- |
| 1 | 치명적인 도끼 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_DeadlyAxe.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_SharpAwl.asset) |
| 2 | 광란의 열매 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_FrenzyFruit.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_BerserkerCord.asset) |
| 3 | 전사의 완갑 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_WarriorBracer.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_BrokenClock.asset) |
| 4 | 주술사 가면 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_ShamanMask.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_BlackIncense.asset) |
| 5 | 순풍의 소라 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_FairwindConch.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_CalmHeart.asset) |
| 6 | 깨진 투구 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_BrokenHelm.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_LastBreath.asset) |
| 7 | 반격의 박차 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_CounterKick.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_CounterKick.asset) |
| 8 | 나뭇잎 펜던트 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_LeafPendant.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_SurvivorNecklace.asset) |
| 9 | 바람의 부채 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_WindFan.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_LightCord.asset) |

## 이름 번역

| KO | EN | JA | zh-Hans | zh-Hant |
| --- | --- | --- | --- | --- |
| 치명적인 도끼 | Deadly Axe | 致命の斧 | 致命之斧 | 致命之斧 |
| 광란의 열매 | Fruit of Frenzy | 狂乱の果実 | 狂乱之果 | 狂亂之果 |
| 전사의 완갑 | Warrior's Bracer | 戦士の腕甲 | 战士护臂 | 戰士護臂 |
| 주술사 가면 | Shaman's Mask | 呪術師の仮面 | 巫师面具 | 巫師面具 |
| 순풍의 소라 | Tailwind Conch | 追い風の巻き貝 | 顺风海螺 | 順風海螺 |
| 깨진 투구 | Broken Helm | 壊れた兜 | 破损头盔 | 破損頭盔 |
| 나뭇잎 펜던트 | Leaf Pendant | 木の葉のペンダント | 叶形吊坠 | 葉形吊墜 |
| 바람의 부채 | Wind Fan | 風の扇 | 风之扇 | 風之扇 |

외국어는 검수 전 초안입니다. 기존 displayName 키와 entry ID를 보존했습니다. CSV 이름 8행과 다섯 StringTable 이름 값 8개씩만 바꿨습니다. m_Metadata 직전까지 전체 scalar를 치환하고 대조하여 줄바꿈 잔여 문구를 방지했습니다. 설명/효과 번역은 바꾸지 않았습니다.

## 파일 변경과 보존

반격의 박차는 기존 Icon_CounterKick.png만 25×25에서 첨부 32×32로 교체하고 메타/GUID/Point·32 PPU 설정/정의의 icon 연결을 유지했습니다. 나머지 8개는 공용 시트를 보존하며 전용 PNG와 단일 스프라이트 Point / 32 PPU / 밉맵 없음 / 압축 없음 메타데이터를 추가하고 icon을 연결했습니다. 새 메타데이터를 PNG보다 먼저 배치했습니다.

8개 정의의 displayName도 변경했습니다. 파일명·m_Name·relicId·정의 GUID·효과·logic·레벨·저장/DB 등록·기존 메타/GUID·공용 시트 및 앞선 7종 제외 정책은 유지했습니다. 첨부 PNG의 색·픽셀·알파·여백은 수정하지 않았습니다. 보약 곰방대의 정의/이미지/이름/효과/번역도 유지했습니다. C#·씬·프리팹·스키마·asmdef 변경 없음.

기존 소스 파일 15개 수정, 새 PNG/메타 16개 추가. PNG 원본 9/9 일치, 이름 번역 40/40 일치, 허용한 결과 바이트 31개 일치, 새 GUID 8개 유일성 및 기타 보호 파일 4015개 해시 보존을 확인했습니다.

[전체 변경 파일과 검증 기록](verification.json) · [SessionLog](../../../Docs/SessionLogs/2026-11-04.md)

Unity import·컴파일·Play Mode·실제 화면/언어 전환 및 자동화 테스트는 실행하지 않았습니다. Doc Impact Check: SessionLog. ErrorLog/DecisionLog/StructureMemory/Presentation HTML 변경 및 새로운 HTML stale candidate 없음.
