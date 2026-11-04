# 유물 4종 승인 이미지 및 행운의 다트 이름 변경 — 2026-11-04

모드: Micro-fix. 첨부 순서대로 사용자 수정 PNG 원본 바이트를 그대로 적용했습니다. 보약 곰방대를 행운의 다트로 변경하고, 귀인의 검흔·회피의 깃·무거운 팔찌 이름은 유지했습니다. 효과는 유지했습니다.

| 순서 | 현재 이름 | 적용 PNG | 기존 정의 |
| --- | --- | --- | --- |
| 1 | 행운의 다트 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_TonicGombangdae.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_TonicGombangdae.asset) |
| 2 | 귀인의 검흔 | [PNG](../../../Assets/_Project/Art/Sprites/Items/Weapon/Flowering/Flowering_Artifact.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_FloweringBloomExtension.asset) |
| 3 | 회피의 깃 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_EvasionFeather.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_EvasionFeather.asset) |
| 4 | 무거운 팔찌 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_HeavyBracelet.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_HeavyBracelet.asset) |

## 이름 번역

| KO | EN | JA | zh-Hans | zh-Hant |
| --- | --- | --- | --- | --- |
| 행운의 다트 | Lucky Dart | 幸運のダーツ | 幸运飞镖 | 幸運飛鏢 |

외국어는 검수 전 초안입니다. 기존 displayName 키와 entry ID를 보존했습니다. CSV 이름 1행과 다섯 StringTable 이름 값 1개씩만 바꿨습니다. m_Metadata 직전까지 전체 scalar를 치환하고 대조하여 줄바꿈 잔여 문구를 방지했습니다. 설명/효과 번역은 바꾸지 않았습니다.

## 파일 변경과 보존

행운의 다트는 기존 Icon_TonicGombangdae.png, 귀인의 검흔은 기존 단일 스프라이트 Flowering_Artifact.png만 첨부 32×32로 교체했습니다. 기존 메타/GUID/Point·32 PPU 설정과 icon 연결을 유지하여 기존 픽업 프리팹·테스트 씬의 해당 스프라이트 참조도 새 그림을 유지합니다. 프리팹/씬 파일 자체는 변경하지 않았습니다.

회피의 깃과 무거운 팔찌는 공용 시트를 보존하며 전용 PNG와 단일 스프라이트 Point / 32 PPU / 밉맵 없음 / 압축 없음 메타데이터를 추가하고 해당 icon만 연결했습니다. 새 메타데이터를 PNG보다 먼저 배치했습니다.

행운의 다트 정의의 displayName도 변경했습니다. 기존 파일명·m_Name·relicId·정의 GUID·효과·logic·레벨·저장/DB 등록·기존 메타/GUID·공용 시트·앞선 7종 제외 정책은 유지했습니다. 사용자 PNG의 색·픽셀·알파·여백은 수정하지 않았습니다. C#·씬·프리팹·스키마·asmdef 변경 없음.

기존 소스 파일 11개 수정, 새 PNG/메타 4개 추가. PNG 원본 4/4 일치, 이름 번역 5/5 일치, 허용한 결과 바이트 15개 일치, 새 GUID 2개 유일성 및 기타 보호 파일 4039개 해시 보존을 확인했습니다.

[전체 변경 파일과 검증 기록](verification.json) · [SessionLog](../../../Docs/SessionLogs/2026-11-04.md)

Unity import·컴파일·Play Mode·실제 화면/픽업 배치/언어 전환 및 자동화 테스트는 실행하지 않았습니다. Doc Impact Check: SessionLog. ErrorLog/DecisionLog/StructureMemory/Presentation HTML 변경 및 새로운 HTML stale candidate 없음.
