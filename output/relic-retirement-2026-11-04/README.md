# 유물 7종 획득·도감 제외 — 2026-11-04

모드: Implementation. 요청한 7종의 원본과 저장용 ID를 보관하면서 일반 획득 후보와 도감에서 제외했습니다. 전체 DB 등록 75종은 유지하며, 도감 표시 수는 기존 소포 제외에 더해 74종→67종이 되는 구성입니다(정적 대조 기준, 실제 UI 실행 아님).

| 이름 | 보존한 ID | 보존한 정의 |
| --- | --- | --- |
| 하얀 깃털 | `RD_FeatherOrbit` | [정의](../../Assets/_Project/Unused/Items/Relics/RD_FeatherOrbit.asset) |
| 속기사 | `RD_Stenographer` | [정의](../../Assets/_Project/Unused/Items/Relics/RD_Stenographer.asset) |
| 한방울의 신속 | `RD_OneDropOfSwiftness` | [정의](../../Assets/_Project/Unused/Items/Relics/RD_OneDropOfSwiftness.asset) |
| 사냥꾼의 표식 | `RD_HunterMark` | [정의](../../Assets/_Project/Data/Items/Relics/Definitions/RD_HunterMark.asset) |
| 도망자의 신발 | `RD_RunawayShoes` | [정의](../../Assets/_Project/Data/Items/Relics/Definitions/RD_RunawayShoes.asset) |
| 연쇄의 부적 | `RD_ChainCharm` | [정의](../../Assets/_Project/Data/Items/Relics/Definitions/RD_ChainCharm.asset) |
| 달리는 장부 | `RD_RunningLedger` | [정의](../../Assets/_Project/Unused/Items/Relics/RD_RunningLedger.asset) |

## 동작 및 보존 범위

- 새 상자·몬스터·무덤·상점 추첨은 기존 CanDropRelic 경로에서 제외합니다. 오래된 세이브에 해금 ID가 남아 있어도 후보에 들어가지 않습니다.
- 고정/보스 보상도 최종 상자 삽입에서 제외하며, 대체 후보가 있으면 기존 교체 경로를 사용합니다. 저장된 열린 상자의 미획득 보상은 복원 시 해당 항목만 생략합니다. 다른 슬롯·레벨·획득 횟수는 유지합니다.
- 저장된 상인의 미판매 재고는 기존 보정 경로에서 교체합니다. SOLD 슬롯·새로고침 횟수·정상 재고·슬롯 특가 보존 규칙은 그대로입니다.
- 도감은 count와 indexed lookup 양쪽에서 제외하므로 미발견 실루엣과 총개수에도 포함되지 않습니다.
- 기존 보유 유물·레벨·효과·인벤토리 이동/복원 및 보유 유물 강화 동작은 유지합니다. 전체 DB 등록, 원본 PNG/정의/전략, 번역, 기존 GUID/meta, 세이브 스키마, 씬/프리팹, asmdef는 수정하지 않았습니다.
- 이미 배치된 바닥 픽업을 일괄 삭제하지 않습니다. 현재 7종의 저작된 보상/씬 참조는 DB·로딩 매니페스트·번역 외에는 확인되지 않았습니다. 개발용 강제 지급이나 향후 직접 지급 코드를 인벤토리 입구에서 전역 차단하지는 않습니다.

## 변경 파일과 이유

| 파일 | 변경 목적 |
| --- | --- |
| [파일](../../Assets/_Project/Runtime/Features/Items/Relics/RelicDefinition.cs) | 세이브 조회와 분리된 공통 7종 제외 ID 판정 |
| [파일](../../Assets/_Project/Runtime/Features/Loot/LootPoolItemSelectionService.cs) | 기존 일반 획득·상점 후보 필터에 제외 판정 추가 |
| [파일](../../Assets/_Project/Runtime/Features/Loot/Chests/ChestRewardPolicy.cs) | 고정/보스 보상 최종 삽입 차단 |
| [파일](../../Assets/_Project/Runtime/Features/Loot/Chests/TreasureChest.cs) | 저장된 열린 상자의 제외 유물 보상 생략 |
| [파일](../../Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/MerchantNPC.cs) | 저장된 미판매 상점 재고 보정 |
| [파일](../../Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaItemTab.cs) | 도감 표시 개수와 indexed lookup에서 동일하게 제외 |
| [LootRewardStructure](../../Docs/StructureMemory/ScriptSystems/LootRewardStructure.md) | 제외 목록과 획득·도감·복원 경계 지도 |
| [RelicRuntimeStructure](../../Docs/StructureMemory/ScriptSystems/RelicRuntimeStructure.md) | 전체 등록/해금과 실제 제외 정책 구분 |
| [ShopStructure](../../Docs/StructureMemory/ScriptSystems/ShopStructure.md) | 미판매 제외 유물 보정 규칙 |
| [DecisionLog](../../Docs/DecisionLog.md) | 사용자 지정 7종 제외와 ID/기존 소유권 보존 결정 |
| [SessionLog](../../Docs/SessionLogs/2026-11-04.md) | 실제 변경·검증·남은 한계 |

## 확인 결과와 남은 확인

- 정적 소스/참조/diff 대조: 7종 원본 ID와 DB 등록, 공통 제외 목록, count/lookup 일관성, 기존 소유/복원 경계 확인. 보호 파일 2637개 해시 보존. 변경 C# 6개 모두 해당 csproj에 포함됨을 확인했습니다.
- MSBuild는 시도했지만 통과하지 못했습니다. 전체 UI 빌드는 기존 Core.csproj에서 EncyclopediaDiscoveryProgress.cs가 빠져 EncyclopediaDiscoverySaveData를 찾지 못했습니다. 현재 Unity DLL을 참조한 분리 Gameplay 빌드도 기존 IChargeGaugePresentation.cs가 생성 Gameplay.csproj에서 빠져 중단됐습니다. 생성 csproj·공통 소스는 수정하지 않았습니다. 이번 코드의 컴파일 성공으로 보고하지 않습니다.
- 자동화 테스트는 요청되지 않아 실행하지 않았습니다. Unity import·Play Mode·도감/상점/상자 화면과 기존 저장 복원 검증도 미실행입니다.
- 수동 확인: 도감 7종/실루엣/총개수 제외, 새 추첨 7종 제외, 미판매 기존 재고 교체와 SOLD/새로고침 보존, 저장 상자 해당 항목 생략, 기존 보유 7종의 ID·레벨·효과 복원을 확인해야 합니다.

[전체 검증 기록](verification.json) · [전체 빌드 로그](msbuild-final-console.log) · [분리 빌드 로그](msbuild-isolated-gameplay.log)

Doc Impact Check: SessionLog / StructureMemory / DecisionLog. ErrorLog·Architecture/Contracts·Presentation HTML 변경 없음. 새로운 HTML stale candidate 없음.
