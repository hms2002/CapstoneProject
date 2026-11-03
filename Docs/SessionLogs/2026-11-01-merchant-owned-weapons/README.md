# 보유 무기 상점 재등장 수정 — 2026-11-01

## 범위와 완료 기준

- Mode: Implementation. 사용자가 보유 무기 재등장 수정과 규칙 단순화를 요청했다.
- 목표: 현재 보유 무기가 최초 진열·저장 재고 복원·무기 획득 이후에 미판매 상품으로 남지 않도록 상점 재고를 보정한다.
- 허용 범위: 기존 상점 C# 흐름과 좁은 구조 문서. 무기 제외 판단은 기존 `LootPoolContext.ShopStock`을 재사용한다.
- 보존 경계: 씬/프리팹/ScriptableObject/세이브 스키마/ID/GUID/asmdef/가격 설정/드랍 대상/부트스트랩은 변경하지 않는다. 정상 재고·판매 완료·새로고침 사용 횟수는 유지한다.
- 검증: 소스·참조·diff·보호 파일 해시 확인과 MSBuild 컴파일. 자동 테스트 및 Play Mode는 요청되지 않아 실행하지 않는다.

## 원인과 증거 수준

기존 `MerchantNPC.RollStock`은 생성 당시 플레이어 보유 무기·월드 드롭을 제외했다. 하지만 `MerchantRunStateService.GetOrCreateState`는 이미 생성된 재고를 그대로 반환하고, `MerchantNPC`는 무기 획득/복원 이벤트를 관찰하지 않았다. 따라서 재고가 만들어진 뒤 그 무기를 획득하거나, 재고 생성보다 늦게 인벤토리가 복원되면 같은 무기가 계속 진열될 수 있었다.

이 누락은 소스로 확인했다. 사용자가 어느 시점에 무기를 획득했는지 기억하지 못해 실제 관찰 사례가 두 경로 중 어느 것인지는 확정하지 않았다. 이번 작업에서 Unity 플레이로 원래 현상을 재현하거나 수정 후 재검증한 것은 아니다.

구매 자체에는 이미 `WeaponInventory2D.PreviewAcquireWithoutReplacement`의 중복 거절이 있었다. 이번 문제는 주로 판매 후보와 저장 재고의 유효성 수명이 달랐던 것이다. 인벤토리/구매 서비스의 중복 획득 방어는 그대로 둔다.

## 현재 규칙을 한곳에서 보기

| 단계 | 규칙 | 소유 코드 |
| --- | --- | --- |
| 무기 후보 | 등록·해금된 무기 중 공통 보물상자 무기 허용 정책을 통과한 것. ApprenticeHeroSword, LightningSpear, CrimsonBoundary, Flowering, OddIron | `ItemManager`, `LootPoolItemSelectionService.CanDropWeapon(..., true)` |
| 유물 후보 | 등록·해금된 유물 중 공통 드랍 제외 5종을 제외 | `LootPoolItemSelectionService.CanDropRelic` |
| 소모품 후보 | ItemDatabase의 소모품 목록 | `ItemManager.GetAllConsumables` |
| 무기 중복 제외 | 현재 플레이어의 모든 무기 슬롯(비활성/봉인 슬롯 포함), WorldItemRegistry 픽업, 활성 WeaponDrop2D | `LootPoolContext.ShopStock`, `LootPoolService` |
| 한 재고의 중복 제외 | 같은 종류와 ID의 상품을 중복 선택하지 않음. 부분 교체 시 유지하는 상품도 제외 | `ShopInventoryRoll` |
| 명시적 새로고침 | 직전 재고 전체의 종류+ID를 제외. 판매 완료 상품도 포함. 영구적인 런 획득 이력은 아님 | `MerchantRunStateService.TryRefreshState` |
| 슬롯 배치 | 기존 anchor 필터·카테고리 가중치·무기/소모품 최대 개수 적용. 부분 보정/확장 때 유지 슬롯도 한도에 포함 | `ShopInventoryRoll` |
| 후보 부족 | 기존 규칙대로 남은 유물/소모품으로 대체하되 종류별 한도 준수. 후보가 모두 없으면 빈 슬롯 | `ShopInventoryRoll` |
| 저장 재고 보정 | 현재 제외 무기에 해당하는 미판매 슬롯만 다시 뽑음. 다른 상품·판매 완료·새로고침 사용 횟수 유지 | `MerchantRunStateService.ReconcileWeaponStock` |
| 구매 | 보정으로 클릭 대상이 바뀌었으면 그 클릭은 결제하지 않음. 실제 획득 전 인벤토리 제한 검사, 실패 시 기존 환불 경로 유지 | `MerchantNPC`, `MerchantPurchaseService` |

상점의 기본 후보/중복/슬롯 규칙은 `ShopInventoryRoll`을 공통으로 사용한다. 이번 단순화는 상점 범위다. 상자·무덤·몬스터 드랍의 서로 다른 제외 컨텍스트를 하나의 정책으로 합치지 않았다. 다른 상점의 재고와 영구적인 무기 획득 이력도 새 금지 목록에 추가하지 않았다.

## 바뀐 파일과 이유

| 파일 | 변경 이유 |
| --- | --- |
| [MerchantNPC.cs](../../../Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/MerchantNPC.cs) | 플레이어 등록/해제와 인벤토리 변경을 구독하고 저장 재고를 보정. 구매 직전 오래된 슬롯 클릭을 차단. 최초/부분/새로고침에 같은 롤 경로 사용 |
| [MerchantRunStateService.cs](../../../Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/MerchantRunStateService.cs) | 제외 무기에 해당하는 미판매 슬롯만 교체하는 상태 연산 추가. 나머지 슬롯 객체와 refreshCountUsed 유지 |
| [ShopInventoryRoll.cs](../../../Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/ShopInventoryRoll.cs) | 부분 교체/확장의 유지 슬롯을 중복 제외와 종류별 개수 한도에 반영 |
| [ShopSlot.cs](../../../Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/ShopSlot.cs) | 강조 중 상품이 바뀌면 기존 상세 표시 경로로 새 설명을 표시하고, 판매/빈 슬롯이면 숨김 |
| [ShopStructure.md](../../StructureMemory/ScriptSystems/ShopStructure.md) | 현재 재고 소유권·구독 수명·규칙·주의점 정리 |
| [RunGoldAndShop.md](../../StructureMemory/RunGoldAndShop.md) | 런 상점에 적용되는 보정 흐름 연결 |
| [SessionLog](../2026-11-01.md), [DecisionLog](../../DecisionLog.md), [ErrorLog](../../ErrorLog.md) | 결과/검증 한계, 정상 재고 보존 원칙, 오래된 재고 및 구매 이벤트 재진입 위험 기록 |

## 상태 변경 순서

1. `OnEnable`/`Start`에서 현재 플레이어 인벤토리를 연결한다. 늦게 생성/등록된 플레이어와 이후 인벤토리 복원도 기존 이벤트로 따라간다.
2. 무기 변경 이벤트는 보정 필요 표시만 남긴다. 실제 보정은 `Update`에서 수행한다.
3. 기존 구매는 획득 → 성공 반환 → 판매 완료 표시 순서다. 따라서 획득 이벤트 안에서 즉시 재고를 바꾸지 않아 구매한 자리를 새 상품으로 다시 채우지 않는다.
4. 저장 재고 바인딩과 구매 직전에도 현재 제외 목록을 검사한다. 슬롯 참조가 교체된 클릭은 결제 없이 끝낸다.
5. 교체 상품만 새로 뽑고, 런 골드의 기존 원가/할인 계산을 그대로 적용한다. 자동 보정은 새로고침 횟수를 증가시키지 않는다.
6. `OnDisable`에서 플레이어 registry와 인벤토리 구독을 모두 해제한다. 별도 관리자/싱글턴/런 영속 필드는 추가하지 않았다.

매 프레임 전체 드랍 검색을 추가하지 않았다. 인벤토리 변경 신호가 있을 때와 초기화/구매 경계에서만 검사한다. 월드 드롭만 별도로 생성되는 상황에 대한 전역 실시간 보정은 추가하지 않았다.

## 검증 결과

- 최종 `Gameplay.csproj /t:Build /p:Configuration=Debug` MSBuild exit 0. 네 수정 파일이 모두 프로젝트에 포함된다. [컴파일 로그](msbuild-gameplay.txt). 기존 코드 경고는 남아 있다.
- [소스 검증 결과](source-verification.json): 보호 대상 39개 파일의 작업 전 SHA-256 유지. DB·드랍 정책·인벤토리·구매 서비스·세이브 DTO·빌드 설정·Addressables·유지 씬 등 포함. [시작 기준](scope-baseline.json).
- diff/공백 검사, 기존 호출부 호환성, 네 파일의 Gameplay 어셈블리 경계, 직렬화 필드 유지와 이벤트 구독/해제 및 구매 완료 순서를 확인했다.
- 유지 슬롯의 중복/종류별 한도, 판매 완료 건너뛰기, 후보 소진 시 빈 슬롯, 저장 재고 보정 경로를 소스로 확인했다.
- 자동 테스트·Unity Editor import/컴파일·Play Mode·수동 플레이·Player 빌드는 미실행. 컴파일 성공을 실제 게임 동작 검증으로 대체하지 않는다.

## 수동 확인 절차 — 미실행

1. 무기 두 개를 보유한 채 런 상점을 처음 방문한다. 두 무기 모두 진열되지 않는지 확인한다.
2. 상점 재고가 생성된 뒤, 진열된 무기를 상자/드랍에서 획득한다. 겹친 미판매 슬롯만 바뀌고 다른 상품/가격은 그대로인지 확인한다.
3. 보유 무기가 포함된 기존 저장 재고로 씬에 돌아오거나 인벤토리를 늦게 복원한다. 같은 보정이 적용되는지 확인한다.
4. 상점에서 무기를 정상 구매한다. 해당 슬롯은 SOLD로 남고 자동으로 새 상품이 채워지지 않는지 확인한다. 다른 상점의 겹치는 미판매 슬롯은 보정 대상이다.
5. 후보를 소진한 상태에서 보정/허브 새로고침을 한다. 보유 무기·직전 재고가 재등장하지 않고, 소모품 한도를 넘지 않는지 확인한다.
6. 설명이 열린 슬롯의 상품이 바뀌거나 팔리면 설명도 갱신/숨김 되는지 확인한다. 동일 프레임의 오래된 클릭으로 새 상품이 결제되지 않는지도 확인한다.

## 남은 범위

- 실제 사용자가 본 재현 경로와 수정 후 플레이 확인은 남아 있다.
- Suggested Later: 유물은 최대 레벨 여부를 구매 시 검사하며, 후보 단계에서 최대 레벨 유물을 걸러내는 변경은 하지 않았다. 드랍/상점/구매 전체를 같은 규칙으로 합치려면 유물 업그레이드와 슬롯 교체 등 서로 다른 허용 조건을 먼저 정해야 한다.
- Doc Impact Check: SessionLog + StructureMemory + DecisionLog + ErrorLog. Architecture/Contracts 변경 없음. 기존 Presentation HTML의 상점 제작/소유권 요약과 충돌하지 않아 stale 후보 없음.
