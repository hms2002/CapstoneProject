# 상점·Loot CSV 견본

2026-09-28의 자산과 코드에서 가져온 기본 설정입니다. 게임에 연결하지 않았으며 기존 밸런스를 변경하지 않았습니다. 범위는 상점 정의 2개, 스테이지 Loot 표 3개, 유해 Loot 표 1개입니다. 몬스터 배치·맵 이벤트는 제외합니다.

| 파일 | 행 수 | 행의 의미 |
| --- | ---: | --- |
| [ShopSettings.csv](ShopSettings.csv) | 2 | 상점별 기본 슬롯·종류 가중치·제한 |
| [ShopPrices.csv](ShopPrices.csv) | 10 | 상점/상품 종류/희귀도별 기본 가격 규칙 |
| [LootWeights.csv](LootWeights.csv) | 36 | 표/추첨 그룹/결과별 가중치 |
| [RewardCounts.csv](RewardCounts.csv) | 25 | 표/보상 출처/아이템 종류별 개수 후보와 가중치 |
| [BossRewards.csv](BossRewards.csv) | 3 | 표별 보스 재화·필드 회복 기본 보상 수량 |
| [GraveWeaponCandidates.csv](GraveWeaponCandidates.csv) | 3 | 잡동사니 유해의 후보 무기 ID |

## 편집 기준

- 파일은 UTF-8 BOM CSV입니다. 식별자는 기존 자산 이름을 `shopId`/`tableId`로 사용하는 견본 규칙이며, 현재 게임에 이러한 ID 필드가 새로 생긴 것은 아닙니다. 무기 ID는 기존 `weaponId`입니다.
- `weight`는 같은 `tableId + rollGroup` 안의 상대 가중치입니다. 합계가 100일 필요는 없습니다. 보유/해금/재고 제외 및 실행 시 보정 규칙은 기존 코드에 남습니다.
- `RewardCounts`의 `minCount/maxCount`는 동일한 `tableId + rewardSource + itemType` 그룹에서 일치해야 합니다. `count`는 지급 개수 후보, `weight`는 그 후보의 가중치입니다. 가중치 0과 지급 개수 0은 서로 다른 의미입니다.
- `ShopPrices`의 `Fixed`는 고정 기본 가격입니다. `UniformIntegerThenRound`는 minPrice부터 maxPriceInclusive까지 정수를 균등 추첨한 뒤 roundTo 단위로 반올림합니다. 끝점 가격의 최종 확률까지 균등한 것은 아닙니다.
- 상점 가격은 호감도·업그레이드 할인표가 아닙니다. 보스 보상도 추가 modifier 적용 전 기본값입니다.

## 실제 출처와 주의할 차이

- 상점: `Assets/_Project/Data/Dialogue/Merchant/ShopDefinition_DefaultLocked.asset`, `ShopDefinition_RunGold.asset`.
- DefaultLocked 자산에 없는 `usesRunGold=0`, `maxWeaponSlots=1`, `maxConsumableSlots=1`은 `ShopDefinitionSO.cs`의 코드 기본값으로 보완했습니다. `valueSource=AssetAndCSharpDefaults`로 표시했습니다. Unity Editor에서 읽은 실효값 검증은 하지 않았습니다.
- RunGold 가격은 자산의 고정 가격이 아니라 `ShopDefinitionSO.RollGoldPrice()`의 현재 범위에서 가져왔습니다. `MerchantNPC`는 골드 상점 재고 생성 후 이 값으로 가격을 덮어씁니다. 무기 1150~1250, 일반 유물 400~500, 희귀 600~700, 영웅 900~1000, 소모품 850~950입니다. 모두 10단위 반올림입니다.
- 스테이지: `Assets/_Project/Data/Loot/Tables/Table_Stage1.asset`부터 `Table_Stage3.asset`까지입니다. 이름을 임의의 런 진행 번호로 재매핑하지 않았습니다. 현재 세 표의 추출값은 같습니다.
- 스테이지의 `legacyChest*` 필드는 기존 이행용 데이터이므로 현재 CountProfile과 중복해서 추출하지 않았습니다. `legendaryWeight`도 현재 ItemRarity와 LootRollService의 희귀도 추첨에 없어서 제외했습니다.
- 유해: `Assets/_Project/Data/Loot/Tables/GraveLootTable.asset`. 수량 설정이 이전 직렬화 이름으로 남아 있어 `LegacyAssetPendingEditorVerification`으로 표시했습니다. 현행 `GraveLootTable.OnValidate()`와 `CountRangeWeightProfile.TryInitializeFromLegacy()`가 이를 이전하는 구조입니다. Editor import를 실행하지 않았으므로 현재 런타임 실효값이라고 단정하지 않습니다.
- 유해 유물은 원본에 min=1, max=2와 함께 count=3/weight=10 후보도 존재합니다. 원본을 그대로 보존했습니다. 해당 범위가 적용된 기본 추첨에서는 3개 후보가 제외되고, 범위 보정 시 사용될 수 있습니다. 임의로 삭제하거나 max를 늘리지 않았습니다.
- GraveWeaponCandidates는 원본 GUID를 기존 무기 ID로 변환한 것입니다. 후보 참조를 바꾸지 않았습니다.

## 검증과 범위

6개 CSV 79행의 재읽기 및 값 일치, 키 중복, 가중치·수량, 후보 무기 ID, 원본 파일 해시 보존을 확인했습니다. Unity Import/컴파일/Play Mode는 실행하지 않았습니다. 파일을 편집해도 아직 게임에는 반영되지 않습니다. CSV 가져오기와 자산 반영은 다음 구현 범위입니다.
