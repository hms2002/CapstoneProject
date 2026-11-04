# 유물 6종 이미지·이름 및 만드라고라 효과 변경 — 2026-11-04

모드: Implementation. 현재 사용자 요청대로 첨부 PNG를 원본 바이트 그대로 적용하고, 5개 이름과 만드라고라의 이동속도 효과를 변경했습니다. C#·스키마·ID·기존 GUID는 보존했습니다.

| 순서 | 적용 이름 | PNG | 정의 |
| --- | --- | --- | --- |
| 1 | 무딘 방패 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_BluntShield.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_BluntShield.asset) |
| 2 | 박동하는 심장 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_BeatingHeart.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_SmallHeart.asset) |
| 3 | 광전사의 보석 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_BerserkerGem.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_RedContract.asset) |
| 4 | 추격 명령 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_PursuitOrder.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_PursuerClaw.asset) |
| 5 | 만드라고라 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_SpeedMedal.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_SpeedMedal.asset) |
| 6 | 행운의 증표 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_LuckyToken.png) | [정의](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_FirmStep.asset) |

## 이름 번역

| KO | EN | JA | zh-Hans | zh-Hant |
| --- | --- | --- | --- | --- |
| 박동하는 심장 | Beating Heart | 鼓動する心臓 | 跳动的心脏 | 跳動的心臟 |
| 광전사의 보석 | Berserker's Gem | 狂戦士の宝石 | 狂战士宝石 | 狂戰士寶石 |
| 추격 명령 | Pursuit Order | 追撃命令 | 追击命令 | 追擊命令 |
| 만드라고라 | Mandragora | マンドラゴラ | 曼德拉草 | 曼德拉草 |
| 행운의 증표 | Token of Luck | 幸運の証 | 幸运信物 | 幸運信物 |

외국어는 검수 전 초안입니다. 무딘 방패 이름은 유지했습니다. 만드라고라는 기존 Icon_SpeedMedal PNG와 메타/GUID/연결을 유지하고 PNG만 교체했습니다. 다른 5개는 공용 시트를 보존하며 개별 PNG/메타를 추가하고 각 정의의 icon을 변경했습니다.

## 만드라고라 효과

- [기존 전략](<../../../Assets/_Project/Data/Items/Relics/Strategies/Relic Logic_SpeedMedalBonus.asset>)의 이동속도 항목(3/5/7/9/12%)만 제거했습니다. 다른 정의에서 이 전략을 사용하지 않음을 Items 데이터 참조에서 확인했습니다.
- 치명타 확률 항목은 바이트 그대로 유지: 2/4/6/8/10%, Flat, duration 0. 최대 레벨 5 유지.
- 공통 RelicLogic_StatModifiers의 장착·복원·프리뷰·툴팁은 남아 있는 한 항목을 순회합니다. 해제는 기존 token 기반 RemoveModifiersFromSource를 유지합니다. C# 수정 없음.
- 정의 설명과 5언어 설명을 치명타 전용으로 수정했습니다. 이동속도 항목 제거로 치명타 항목이 배열 0번이 되므로, 해당 기존 displayNameOverride 번역도 치명타 확률로 동기화했습니다. Shared Data, generated map, 키/entry ID는 유지했습니다. 이전 1번 키도 삭제하지 않았습니다.
- 다른 5개 유물의 효과는 그대로입니다. 기존 세이브/진행 중 런의 실제 재장착·불러오기는 실행하지 않았습니다.

## 검증 및 문서 영향

PNG 원본 일치 6/6, 번역 값 일치 35/35, 변경 파일 결과 바이트 일치 24개, 다른 보호 파일 해시 보존 578개. 정의 변경은 icon·승인 이름·만드라고라 description에만 한정되며 치명타 항목 원문과 수치 보존을 확인했습니다.

[변경 파일·검증 상세](verification.json) · [SessionLog](../../../Docs/SessionLogs/2026-11-04.md)

자동화 테스트·Unity import·컴파일·Play Mode·실제 화면/언어 전환은 실행하지 않았습니다. Doc Impact Check: SessionLog. ErrorLog/DecisionLog/StructureMemory/Presentation HTML 변경 및 새로운 HTML stale candidate 없음.
