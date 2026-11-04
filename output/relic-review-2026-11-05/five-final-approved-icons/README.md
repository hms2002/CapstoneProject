# 최종 승인 유물 이미지 5종 적용 — 2026-11-05

모드: Micro-fix. 사용자 첨부 순서대로 원본 PNG 바이트를 그대로 적용했습니다. 이름·효과·번역은 변경하지 않았습니다.

| 순서 | 사용자 지칭 | 유지한 기존 이름 | 새 이미지 | 정의 |
| --- | --- | --- | --- | --- |
| 1 | 회복 펜던트 | 회복의 팬턴트 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_RecoveryPendant.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_RecoveryPendant.asset) |
| 2 | 쇳덩이의 예비 탄약 | 쇳덩이의 예비 탄창 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_OddIronMagazine.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_OddIronMagazine.asset) |
| 3 | 뇌창의 세 번째 번개 | 뇌창의 세 번째 번개 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_LightningSpearThirdStrike.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_LightningSpearThirdStrike.asset) |
| 4 | 뇌창의 추적 낙뢰 | 뇌창의 추적 낙뢰 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_LightningSpearTargetedRain.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_LightningSpearTargetedRain.asset) |
| 5 | 견습 용사의 연계 증표 | 견습 용사의 연계 증표 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_ApprenticeChargeLink.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_ApprenticeChargeLink.asset) |

기존 포션/무기 공용 이미지를 보존하고 전용 PNG/.meta 5쌍을 추가하여 각 정의의 icon만 연결했습니다. 새 메타를 해당 PNG보다 먼저 배치했습니다. Point / 32 PPU / 단일 스프라이트 / 밉맵 없음 / 압축 없음 설정을 사용했습니다.

사용자 PNG의 픽셀·색·알파·여백은 수정하지 않았습니다. 특히 5번의 왼쪽 1픽셀 여백도 승인 원본 그대로 적용했습니다. 정의의 이름·설명·logic·relicId·정의 GUID·레벨·등급·DB 등록·저장/제외 정책과 모든 번역은 보존했습니다. C#·씬·프리팹·스키마·asmdef 변경 없음.

회복 펜던트와 예비 탄약은 현재 프로젝트에서 각각 `회복의 팬턴트`, `쇳덩이의 예비 탄창`으로 표기되어 있습니다. 이번 요청은 이미지 적용이므로 기존 이름과 번역은 유지했습니다.

원본 PNG 5/5 일치, 정의 icon 외 바이트 보존, 새 GUID 5개 유일성, 보호 파일 4054개 해시 보존, 대상 정의 git diff --check를 확인했습니다.

[전체 변경 파일·연결·확인 기록](verification.json) · [SessionLog](../../../Docs/SessionLogs/2026-11-05.md)

Unity import·컴파일·실제 화면·Play Mode 및 자동화 테스트는 실행하지 않았습니다. Doc Impact Check: SessionLog. ErrorLog/DecisionLog/StructureMemory/Presentation HTML 변경 및 새 HTML stale candidate 없음.
