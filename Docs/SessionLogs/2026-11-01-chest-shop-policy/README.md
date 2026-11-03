# 상자·상점 무기 제외 및 상점 최대 레벨 유물 — 2026-11-01

## 범위

Mode: Implementation. 사용자가 상자에도 바닥 무기·이미 열린 다른 상자의 무기 제외를 적용하고, 최대 레벨 유물을 상점에서 제외하도록 요청했다. 직전에 합의한 상자↔상점의 미획득 무기 상호 제외도 연결했다.

기존 롤/재고/이벤트 소유자 안에서 처리한다. 새 Manager/Singleton/MonoBehaviour/파일은 만들지 않는다. 씬·프리팹·SO·세이브 DTO·GUID·asmdef·부트스트랩·가격과 가중치는 그대로 둔다. 자동 테스트는 요청되지 않아 실행하지 않는다.

## 적용된 규칙

| 항목 | 상자 | 상점 |
| --- | --- | --- |
| 현재 보유 무기 | 제외 | 제외 |
| 바닥 무기 | WorldItemRegistry 픽업 + 활성 WeaponDrop2D 제외 | 같은 제외 |
| 열린 상자에 남은 무기 | 다른 상자의 무기를 제외 | 제외 |
| 상점 무기 | 런 저장 재고 중 미판매 무기를 제외 | 자신의 재고를 자기 제외로 수집하지 않음 |
| 최대 레벨 유물 | 기존 공통 레벨 조건 유지 | 동일 조건으로 후보 제외 및 기존 재고 보정 |
| 정상 상품/보상 | 생성·리롤 정책대로 유지 | 정상 상품·판매 완료·기존 가격·새로고침 횟수 유지 |

- 열린 상자에서 아무것도 획득하지 않고 UI만 닫으면 남은 무기 제외가 유지된다.
- 획득 확정으로 상자가 소모되면 `CompleteLootSelection`이 남은 목록을 비운다. 획득 무기는 플레이어 보유로 계속 제외되고, 버린 보상은 이후 추첨에서 다시 후보가 될 수 있다.
- 상자 리롤은 자기 상자 전체를 ‘다른 열린 상자’로 취급하지 않는다. 선택해 유지한 무기는 기존 `RetainedItems`에서 제외·종류별 수량 계산에 포함한다. 유지하지 않은 이전 보상 전체를 금지하는 규칙은 추가하지 않았다.
- 보상이 아직 생성되지 않은 상자와 아직 열리지 않은 상자는 열린 상자 제외 출처가 아니다. 초기 보상 선확정이나 예약 관리자도 추가하지 않았다.
- 상자 간/상점 간 유물 중복은 이 무기 규칙에 포함하지 않는다. 유물은 강화 여지가 있으면 후보이며, `ParcelRelicDefinition`의 기존 비강화 슬롯 예외도 유지한다.
- 몬스터/무덤의 기존 제외 컨텍스트는 유지했다. 특히 기존 `MerchantStock`의 판매 완료 포함 의미를 바꾸지 않고 새 `UnsoldMerchantStock`을 상자에 사용했다.

## 변경 파일과 이유

| 파일 | 변경 이유 |
| --- | --- |
| [LootPoolContext.cs](../../../Assets/_Project/Runtime/Features/Loot/LootPoolContext.cs) | 열린 상자/미판매 상점 출처와 ChestStock/ForChest 추가. ShopStock에 열린 상자 추가. 리롤 요청의 원본 상자를 전달 |
| [LootPoolWeaponExclusionProvider.cs](../../../Assets/_Project/Runtime/Features/Loot/LootPoolWeaponExclusionProvider.cs) | 열린 상자의 잔여 무기와 미판매 상점 무기 수집. 현재 로드된 던전은 실물 상자, 떠난 던전은 기존 저장 잔여 목록 사용 |
| [ChestLootGenerationService.cs](../../../Assets/_Project/Runtime/Features/Loot/ChestLootGenerationService.cs) | 일반/보스 상자 요청의 기본 제외 컨텍스트를 ChestStock으로 연결 |
| [TreasureChest.cs](../../../Assets/_Project/Runtime/Features/Loot/Chests/TreasureChest.cs) | 자기 상자를 구분한 요청. 보스 추가 보상을 포함한 최종 삽입에도 제외 적용. 보상 초기화/리롤 완료 알림 |
| [LootPoolItemSelectionService.cs](../../../Assets/_Project/Runtime/Features/Loot/LootPoolItemSelectionService.cs) | 플레이어 유물 레벨 조회를 기존 CanGainRelicLevels와 결합한 공통 함수로 추출 |
| [ShopInventoryRoll.cs](../../../Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/ShopInventoryRoll.cs) | 상점 유물 후보가 공통 최대 레벨 조건을 통과하도록 변경 |
| [MerchantRunStateService.cs](../../../Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/MerchantRunStateService.cs) | 이전 무기 전용 보정을 ReconcileStock의 대상 판정 함수로 일반화. 미판매 슬롯만 교체하는 본체 유지 |
| [MerchantNPC.cs](../../../Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/MerchantNPC.cs) | 무기/유물 인벤토리와 상자 상태 알림 연결. 저장 재고·레벨 변경·구매 경계에서 무기와 최대 레벨 유물 함께 보정 |

기존 LootPoolExclusionSource의 값/순서는 유지하고 새 런타임 비트 2개만 추가했다. 컨텍스트는 저장 DTO가 아니며 직렬화 필드를 변경하지 않았다. 이번 작업은 앞선 보유 무기 상점 수정 위에 이어지므로 git diff에는 그 작업의 변경도 함께 보인다.

## 중요한 수명·순서 처리

- 열린 상자는 비활성 상태라도 로드되어 있으면 잔여 재고를 조회한다. 소모된 상자는 제외하지 않는다.
- `PreserveDuringRun`으로 떠난 던전의 열린 상자 목록은 기존 `dungeonRunStates.objectStates.chestLoot`에서 읽는다. 현재 생성 완료된 던전의 ID는 저장 스냅샷 조회에서 빼고 실물 상자를 우선한다. 오래된 저장 목록이 소모된 무기를 계속 막거나 자기 리롤을 막지 않게 한다. 저장 형식은 그대로다.
- 상점은 무기/유물 변경 및 상자 개봉·복원·리롤·소모 신호를 받으면 다음 Update에 보정한다. 구매 중 획득 이벤트보다 SOLD 기록이 늦다는 기존 순서를 보존한다. 구매 직전에는 클릭했던 상품이 교체됐는지 확인하고 바뀌었으면 결제하지 않는다.
- 모든 구독은 MerchantNPC.OnDisable에서 해제한다. 매 프레임 상자/드랍 전체 검색을 추가하지 않았다. 검색은 추첨과 보정 경계에서 실행된다.
- 보스 보너스 목록은 기본 추첨 뒤 직접 추가되므로 `FillInventoryWithLoot`에서도 같은 무기 제외를 검사한다. 충돌하면 허용된 무기로 대체하고, 후보가 없으면 그 무기 항목을 생략한다. 유지 선택과 이번 삽입 무기도 제외 목록에 추가해 중복을 막는다.
- 상점의 최대 레벨 보정은 기존 부분 교체를 사용해 정상 상품, SOLD 슬롯, 슬롯 필터·수량 한도와 refreshCountUsed를 보존한다. 후보가 모두 소진되면 기존 빈 슬롯 처리로 끝나며 금지 항목을 다시 넣지 않는다.

## 검증

- 최종 MSBuild `Gameplay.csproj /t:Build /p:Configuration=Debug`: exit 0. [컴파일 로그](msbuild-gameplay.txt). 기존 코드 경고는 남아 있다.
- 수정한 8개 파일 모두 생성된 Gameplay.csproj에 포함됨. 새 소스/asmdef가 없어 import 전 프로젝트 누락은 없다.
- [소스·범위 확인](source-verification.json): 보호 파일 48개 해시 유지, 기존 enum 값 유지, 변경 파일의 프로젝트 포함 확인. [작업 전 기준](scope-baseline.json).
- 소스/호출부/diff/공백 확인. 구독-해제, 정상 구매 후 SOLD, 자기 상자 리롤 제외, 선택 유지, 저장 상태와 실물의 우선순위, 유물 레벨 조회의 공통 사용을 검토했다.
- 자동 테스트·Unity Editor import/컴파일·Play Mode·수동 플레이·Player 빌드는 미실행. 소스 및 MSBuild 결과를 런타임 재현 통과로 해석하지 않는다.

## 수동 확인 절차 — 미실행

1. 보유 무기 A, 바닥 무기 B, 열린 다른 상자의 무기 C, 미판매 상점 무기 D를 둔다. 새 상자와 상자 리롤에서 A/B/C/D가 제외되는지 확인한다.
2. 상자에서 무기를 선택 유지하고 리롤한다. 유지 무기 중복은 없고, 자기 상자의 미유지 보상은 다른 제외 사유가 없으면 다시 나올 수 있는지 확인한다.
3. 열린 상자를 획득 없이 닫고 상점을 생성/갱신한다. 그 상자에 남은 무기는 상점에서 제외되는지 확인한다.
4. 상자 획득을 확정한다. 획득 무기는 보유로 계속 제외되고, 버린 보상은 이후 추첨에서 다시 가능해지는지 확인한다.
5. 열린 상자를 남겨 두고 다른 씬으로 이동한다. 저장된 잔여 무기가 제외되는지, 복귀 후 소모했을 때 옛 스냅샷 때문에 계속 막히지 않는지 확인한다.
6. 최대 레벨 유물을 보유하고 새 상점을 방문한다. 후보에 없으며, 기존 상점 상품이 나중에 최대 레벨에 도달해도 해당 미판매 슬롯만 바뀌는지 확인한다.
7. 상점에서 마지막 유물 레벨을 정상 구매한다. 구매 슬롯은 SOLD로 남고 자동 재충전되지 않으며, 다른 정상 상품/가격과 리롤 횟수도 보존되는지 확인한다.
8. 보스 추가 무기가 바닥/상점/열린 상자와 충돌할 때 대체되고, 허용 무기가 모두 소진되면 중복 없이 생략되는지 확인한다.

Doc Impact Check: SessionLog + 기존 StructureMemory + DecisionLog + ErrorLog. Architecture/Contracts·Presentation HTML 변경 없음. 기존 HTML의 제작/소유권 요약과 충돌하지 않아 stale 후보 없음. 실제 플레이와 씬 재진입 확인은 남아 있다.
