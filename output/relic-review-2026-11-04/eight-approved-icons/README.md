# 유물 8종 승인 이미지 및 두 이름 변경 — 2026-11-04

모드: Micro-fix. 첨부 순서대로 사용자 수정 PNG 원본 바이트를 그대로 적용했습니다. 프로젝트의 승리의 깃발을 사용자가 지칭한 붉은 깃발 대상으로 확인해 광전사의 깃발로 변경했습니다. 전사의 맹세는 전사의 메달로 변경했습니다. 효과는 유지했습니다.

| 순서 | 현재 이름 | 적용 PNG | 기존 정의 |
| --- | --- | --- | --- |
| 1 | 부서진 왕관 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_BrokenCrown.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_BrokenCrown.asset) |
| 2 | 균열석 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_RiftStone.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_RiftStone.asset) |
| 3 | 질긴 붕대 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_ToughBandage.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_ToughBandage.asset) |
| 4 | 광전사의 깃발 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_BerserkerFlag.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_VictoryFlag.asset) |
| 5 | 날렵한 망토 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_NimbleCloak.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_NimbleCloak.asset) |
| 6 | 전사의 메달 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_WarriorMedal.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_WarriorOath.asset) |
| 7 | 얇은 갑옷 조각 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_ThinArmorShard.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_ThinArmorShard.asset) |
| 8 | 균형추 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_BalanceWeight.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_BalanceWeight.asset) |

## 이름 번역

| KO | EN | JA | zh-Hans | zh-Hant |
| --- | --- | --- | --- | --- |
| 광전사의 깃발 | Berserker's Banner | 狂戦士の旗 | 狂战士旗帜 | 狂戰士旗幟 |
| 전사의 메달 | Warrior's Medal | 戦士のメダル | 战士勋章 | 戰士勳章 |

외국어는 검수 전 초안입니다. 기존 displayName 키와 entry ID는 보존했습니다. CSV 이름 2행과 다섯 StringTable의 이름 값 2개씩만 바꿨습니다. 줄바꿈된 YAML 값에 잔여 문구가 남지 않도록 m_Metadata 전까지 전체 scalar를 치환하고 대조했습니다. 설명/효과 번역은 바꾸지 않았습니다.

## 파일 변경과 보존

8종은 모두 기존 공용 이미지 시트를 사용했으므로, 각자 전용 PNG와 단일 스프라이트 Point / 32 PPU / 밉맵 없음 / 압축 없음 메타데이터를 추가하고 해당 정의의 icon만 연결했습니다. 두 정의의 displayName도 변경했습니다. 새 메타데이터를 PNG보다 먼저 배치했습니다.

원본 파일명·m_Name·relicId·정의 GUID·효과·logic·레벨·저장/DB 등록·기존 메타/GUID·공용 시트는 유지했습니다. 첨부 PNG의 색·픽셀·알파·여백은 수정하지 않았습니다. 별도 C#·씬·프리팹·스키마·asmdef 변경은 없습니다.

기존 파일 14개 수정, 새 PNG/메타 16개 추가. 원본 PNG 8/8 일치, 이름 번역 10/10 일치, 허용한 결과 바이트 30개 일치, 새 GUID 8개 유일성 및 기타 보호 파일 4000개 해시 보존을 확인했습니다.

[전체 변경 파일과 검증 기록](verification.json) · [SessionLog](../../../Docs/SessionLogs/2026-11-04.md)

Unity import·컴파일·Play Mode·실제 화면/언어 전환 및 자동화 테스트는 실행하지 않았습니다. Doc Impact Check: SessionLog. ErrorLog/DecisionLog/StructureMemory/Presentation HTML 변경 및 새로운 HTML stale candidate 없음.
