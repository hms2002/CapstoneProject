---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-11-04
---

# Shop Structure

## Purpose

상점 후보 선택, 저장 재고, 구매와 표시의 현재 책임을 빠르게 찾는 구조 지도다. Architecture/Contracts를 대체하지 않는다.

## Current structure and ownership

- `ShopDefinitionSO`: 상점 사용 조건, 슬롯 수, 종류별 최대 개수, 가중치, 가격과 새로고침 설정.
- `MerchantShopPolicy`: 정의와 `RunModifierService.ShopModifiers`로 유효 정책 계산.
- `MerchantNPC`: 씬 소유 상점 조율자. 재고 바인딩, 플레이어/무기·유물 인벤토리/상자 상태 구독, 구매/새로고침 요청과 표시 갱신.
- `MerchantRunStateService`: `GamePlayData.merchantStates`의 상인별 재고·판매 여부·새로고침 횟수 관리. 현재 제외 무기, 제외 유물 또는 최대 레벨 유물인 미판매 슬롯만 보정.
- `ShopInventoryRoll`: 최초/새로고침/부분 보정/슬롯 확장의 공통 후보 선택. 유지 슬롯도 중복 제외와 종류별 한도에 포함.
- `MerchantOfferPricing`: 원가에서 전체 할인과 슬롯 특가를 곱해 최종 가격을 계산한다. 호감도 희귀도 보너스는 `MerchantNPC`가 최초/새로고침/부분 보정/슬롯 확장의 모든 추첨에 전달한다.
- `MerchantPurchaseService`: 실제 인벤토리 획득 가능 여부와 통화 지출/실패 환불. 인벤토리가 최종 획득 가능 여부를 결정한다.
- `ShopSlot`: 저장 재고를 화면과 상호작용으로 투영. 강조 중 상품 변경 시 상세 표시도 갱신하며 재고를 소유하지 않는다.
- `MerchantRefreshInteractable`: 새로고침 상호작용·남은 횟수 표시. 텍스트는 Core `TextPresentationBinding` 경유.

## Current selection rules

| 조건 | 현재 규칙 |
| --- | --- |
| 무기 기본 후보 | 해금된 무기 중 `LootPoolItemSelectionService.CanDropWeapon(id, treasureChest: true)` 통과 |
| 유물 기본 후보 | 해금된 유물 중 `CanDropRelic` 및 공통 `CanGainRelicLevels` 통과. 제외 유물 7종과 최대 레벨 제외 |
| 소모품 후보 | ItemDatabase의 소모품 목록 |
| 무기 제외 | `LootPoolContext.ShopStock`: 모든 보유 슬롯 + 월드 픽업 + 활성 WeaponDrop2D + 열린 상자 잔여 무기 |
| 재고 내 중복 | 같은 종류+ID 중복 금지. 보정/확장의 유지 슬롯도 제외 |
| 새로고침 | 직전 재고 전체(판매 완료 포함) 제외. 영구 획득 이력 제외는 아님 |
| 슬롯 제한 | anchor의 종류 필터, 정의의 가중치와 최대 개수. 유지 슬롯도 수량 계산에 포함 |
| 후보 소진 | 기존 유물/소모품 대체 규칙과 소모품 한도 유지. 모두 없으면 빈 슬롯 |
| 저장 재고 | 유효한 기존 상품/판매 완료/가격 유지. 현재 제외 무기/최대 레벨 유물인 미판매 슬롯만 교체 |
| 구매 | 최신 보정으로 클릭 대상이 바뀌면 결제하지 않음. 기존 중복 획득·용량·유물 레벨·통화 검사 유지 |

상점 제외 목록은 `MerchantStock` 소스를 포함하지 않으므로 자신의 저장 재고를 이유로 자기 상품을 다시 배제하지 않는다. 다른 상점 재고 전체를 금지하는 규칙도 아니다. 최대 레벨 유물은 후보 단계와 기존 미판매 재고 보정에서 제외되며 구매 단계의 최종 방어도 유지된다.

## Inventory lifecycle and purchase ordering

- `OnEnable`/`Start`에서 현재 인벤토리를 연결하고 `PlayerRegistered`/`PlayerUnregistered`를 따라 연결을 교체한다. `OnDisable`은 registry/무기·유물 인벤토리/상자 이벤트를 모두 해제한다.
- 무기 `OnInventoryChanged`, 유물 `OnChanged`, 상자 `WorldStateChanged`는 보정 필요 표시만 남기며 `Update`에서 처리한다. 인벤토리 shell 복원도 변경 이벤트를 발행한다.
- 구매 중 무기 획득 알림은 판매 완료 표시보다 먼저 온다. 이벤트 안에서 즉시 재고를 다시 뽑으면 구매 슬롯이 새 상품으로 바뀔 수 있으므로 조율자가 구매 완료 후 처리한다.
- 초기 저장 재고 바인딩과 구매 경계에서도 보정한다. 겹친 슬롯만 교체하며 새로고침 횟수는 사용하지 않는다.
- 부분 보정으로 유효한 대체 상품을 넣을 때 기존 슬롯의 `specialDiscountPercent`를 유지한다. 새 상품의 원가로 가격을 다시 계산하고, 기존 특가 배정 여부를 초기화하거나 할인율을 다시 추첨하지 않는다. 후보가 없어 빈 슬롯이 되면 표시할 특가도 없으며, 실제 재고 새로고침에서만 다시 배정한다.
- 런 골드의 원가/할인 저장 방식은 유지된다. 가격과 슬롯 수 설정을 바꾸는 곳은 `ShopDefinitionSO`와 정책 계층이다.

## Chest cooperation

상자 생성·리롤은 `LootPoolContext.ForChest(this)`를 사용하여 보유/바닥 무기, 다른 열린 상자의 잔여 무기와 미판매 상점 무기를 제외한다. 자기 상자 전체는 제외하지 않고, 유지한 선택은 `RetainedItems`로 전달한다. 획득 없이 UI만 닫으면 제외가 유지되고, 획득 확정으로 상자가 소모되면 버린 보상의 제외가 해제된다.

열린 상자 출처는 로드된 실물과 떠난 PreserveDuringRun 던전의 기존 잔여 저장 상태를 조회한다. 생성 완료된 던전은 실물만 사용해 오래된 스냅샷을 중복 적용하지 않는다. 보스 추가 보상도 최종 상자 삽입에서 같은 무기 제외를 통과한다. 일반 무덤의 기존 MerchantStock 컨텍스트는 유지되고 상자에는 UnsoldMerchantStock을 사용한다.

[상자·상점 통합 규칙과 검증 한계](../../SessionLogs/2026-11-01-chest-shop-policy/README.md).

## Prefab slot authoring

기존 `MerchantNPC.slotPrefab`과 순서 있는 `slotAnchors`가 재사용 `ShopSlot` 인스턴스를 소유한다. 각 anchor는 Any/Weapon/Relic/Consumable 필터를 가진다. 기존 scene-copied 슬롯 fallback도 남아 있다. 슬롯 순서가 저장 재고의 index이므로 재배치는 저장 슬롯 매핑에 영향을 준다. 이번 보정 작업은 씬/프리팹 구조를 변경하지 않았다.

- [ShopSlot.prefab](../../../Assets/_Project/Prefabs/UI/Dialogue/ShopSlot.prefab)
- [ShopSlot_RunGold.prefab](../../../Assets/_Project/Prefabs/UI/Dialogue/ShopSlot_RunGold.prefab)
- [ProtoTypeHub](../../../Assets/_Project/Scenes/ProtoTypeHub.unity)
- [SceneSetupValidatorWindow](../../../Assets/_Project/Editor/Tools/Validation/SceneSetupValidatorWindow.cs)

## Extension entry points and known pitfalls

- 제외 유물 7종은 `RelicDefinition.IsRetiredId`가 식별한다. 신규 추첨은 `CanDropRelic`, 저장된 미판매 재고 보정은 `MerchantNPC.ReconcileStock`에서 확인한다. 구매 직전 기존 재고 재검사도 유지되며 SOLD 슬롯과 보유 유물은 제거하지 않는다. [대상·검증 기록](../../../output/relic-retirement-2026-11-04/README.md).
- 후보 허용 정책: `LootPoolItemSelectionService`; 상점 종류·수량·가격: `ShopDefinitionSO`/`MerchantShopPolicy`; 보정할 슬롯 판정과 저장: `MerchantRunStateService`.
- 슬롯 일부를 다시 뽑을 때 실제 anchor index와 유지 슬롯을 넘긴다. 유지 상품을 빼고 개수 한도를 계산하면 소모품/무기 한도를 초과할 수 있다.
- 부분 보정의 유지 슬롯 정보와 호감도 `ShopRunModifierDelta`는 함께 전달한다. 어느 한쪽을 누락하면 중복/개수 제한 또는 희귀도 혜택이 빠진다. [병합 확인 기록](../../SessionLogs/2026-11-04.md).
- 기존 정상 상품과 SOLD 슬롯을 인벤토리 변경 때마다 전체 재생성하지 않는다. 상점 재방문은 새로고침이 아니다.
- 월드 드롭 생성만으로 모든 상점을 지속 재검사하는 전역 감시자는 없다. 최초/새로고침/인벤토리 변경/구매 경계에서 현재 제외 목록을 조회한다.
- 자동 보정 결과를 표시할 때 강조 중 상세 설명도 새 상품과 맞춘다. UI는 최신 상태를 보여 주고 판매/빈 슬롯에서는 설명을 숨긴다.

## Key files and verification

- [MerchantNPC](../../../Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/MerchantNPC.cs)
- [MerchantRunStateService](../../../Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/MerchantRunStateService.cs)
- [ShopInventoryRoll](../../../Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/ShopInventoryRoll.cs)
- [MerchantPurchaseService](../../../Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/MerchantPurchaseService.cs)
- [ShopSlot](../../../Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/ShopSlot.cs)
- [RunGoldAndShop](../RunGoldAndShop.md)
- [수정 근거·전체 규칙·검증 한계·수동 확인 절차](../../SessionLogs/2026-11-01-merchant-owned-weapons/README.md)

2026-11-01 보정은 소스/참조/diff와 MSBuild Gameplay 컴파일로 확인했다. 자동 테스트·Unity import/Play Mode·수동 플레이는 미실행이다. Architecture/Contracts 승격 후보는 아니다.
