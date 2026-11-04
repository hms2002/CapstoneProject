# 유물 6종 추가 승인 이미지 적용 — 2026-11-04

첨부 순서에 따라 PNG 원본 바이트 그대로 적용했습니다. 효과와 저장용 ID는 유지했습니다.

| 순서 | 유물 | 이미지 | 정의 |
| --- | --- | --- | --- |
| 1 | 회전하는 철구 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_RotatingIornBall.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_RotatingIornBall.asset) |
| 2 | 집중의 렌즈 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_FocusLens.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_FocusLens.asset) |
| 3 | 송골매의 증표 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_HawkEmblem.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_HawkEmblem.asset) |
| 4 | 소포 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_EventParcel.png) | [정의](../../../Assets/_Project/Data/Items/Relics/RD_EventParcel.asset) |
| 5 | 신속의 부츠 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_SpeedBoots.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_SpeedBoots.asset) |
| 6 | 전투 교본 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_CombatManual.png) | [정의](../../../Assets/_Project/Unused/Items/Relics/RD_AttackBonusRelic.asset) |

전투 교본 이름: KO 전투 교본 / EN Combat Manual / JA 戦闘教本 / zh-Hans 战斗教本 / zh-Hant 戰鬥教本. 외국어는 검수 전 초안입니다.

회전하는 철구·송골매의 증표·신속의 부츠는 기존 PNG만 교체했습니다. 집중의 렌즈·소포·전투 교본에는 개별 PNG와 Point / 32 PPU / 단일 스프라이트 메타데이터를 추가하고 해당 icon만 연결했습니다. 기존 메타데이터와 공용 시트는 보존했습니다.

힘 부적의 정의는 Unused 폴더에 있지만 현재 ItemDatabase와 튜토리얼 로딩 매니페스트에 등록되어 있습니다. 기존 경로·GUID·relicId를 유지하고 displayName과 icon만 수정했습니다. 데이터베이스나 로딩 등록은 변경하지 않았습니다.

CSV 이름 1행과 5개 StringTable 값 1개씩을 변경했습니다. 번역 키와 entry ID는 보존했습니다.

원본 PNG 6/6 일치, 이름 번역 5/5 일치, 허용한 결과 바이트 18개 파일 일치, 기타 보호 파일 326개 해시 보존을 확인했습니다.

[변경 파일·검증 기록](verification.json) · [SessionLog](../../../Docs/SessionLogs/2026-11-04.md)

Unity import·컴파일·Play Mode·실제 화면 확인 및 자동화 테스트는 실행하지 않았습니다. Doc Impact Check: SessionLog. ErrorLog/DecisionLog/StructureMemory/Presentation HTML 변경 및 새로운 HTML stale candidate 없음.
