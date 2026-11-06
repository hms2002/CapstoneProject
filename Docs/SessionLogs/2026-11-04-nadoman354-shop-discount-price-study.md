# 사샤 상점 할인 가격표 견본

2026-11-04 · nadoman354 · 디자인 견본 제작

사용자는 앞선 비교에서 B안 종이 가격표를 선택했다. 이번 견본에서 NPC 머리 위의 상시 할인 패널을 제외하고 할인 정보는 각 상품 아래에 배치했다. 세 가지 세부 디자인은 선택 전이며 Unity에 적용하지 않았다. [이전 A/B 비교](../../outputs/shop-discount-preview-2026-11-04/sasha-price-study.png)

## 산출물

- [세 가지 진열 견본](../../outputs/shop-discount-preview-2026-11-04/paper-b/b-paper-comparison.png)과 [7레이어 Aseprite 원본](../../outputs/shop-discount-preview-2026-11-04/paper-b/b-paper-comparison.aseprite). 실제 사샤·카펫·상품·골드를 재사용하고 슬롯 간격을 동일하게 유지했다. 정가 950골드인 불타는 깃털이 전체 5%와 추가 특가 20%를 받아 722골드가 되는 예시를 비교한다. B1 접힌 양피지는 총 할인율 24%, B2 절취 쿠폰은 절약액 228골드, B3 남색 책갈피는 특가 여부를 표시한다.
- [상태별 견본](../../outputs/shop-discount-preview-2026-11-04/paper-b/b-paper-states.png)과 [4레이어 Aseprite 원본](../../outputs/shop-discount-preview-2026-11-04/paper-b/b-paper-states.aseprite). 동일 정가 1,200골드의 정상가·단골 할인·단독 특가·중복 할인·품절을 3가지 디자인으로 비교했다. 각 44×24픽셀 가격표에 이름 있는 슬라이스를 두고 종이와 글자를 분리했다.

## 확인

Aseprite 1.3.18.6에서 두 원본을 다시 열어 레이어와 15개 슬라이스를 확인했다. 원본에서 내보낸 464×348 비교판과 428×203 상태표를 최근접 2배 확대한 결과가 전달한 PNG와 픽셀 단위로 일치했다. 세 진열 견본의 상품 12개 아래에만 가격표를 두었으며, 머리 위 영역은 가격표 레이어의 알파가 비어 있음을 확인했다. 실제 게임 카메라 배율에서의 가독성은 미확인이다. Unity 테스트와 Play Mode는 실행하지 않았다.

문서 영향은 SessionLog다. 이번 견본에 따른 ErrorLog·DecisionLog 및 Presentation HTML 갱신 사항은 없다.

## B1 접힌 양피지 적용

- 사용자가 B1을 선택해 [런 골드 슬롯](../../Assets/_Project/Prefabs/UI/Dialogue/ShopSlot_RunGold.prefab)과 [허브 마정석 슬롯](../../Assets/_Project/Prefabs/UI/Dialogue/ShopSlot.prefab)에 적용했다. 상품 아래의 종이 한 장에 할인 전 정가, 실제 결제액과 총 할인율을 표시한다. 사샤의 머리 위에는 할인 패널을 배치하지 않았다.
- [가격표 스프라이트 시트](../../Assets/_Project/Art/Sprites/UI/Common/ShopPriceParchment.png)와 해당 임포트 메타는 B1 Aseprite 원본의 종이 레이어에서 가져온 44×24픽셀 기본·일반 할인·추가 특가 배경을 담는다. 숫자와 통화 아이콘은 [ShopSlot](../../Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/ShopSlot.cs)이 저장 재고에서 표시한다. 정가와 결제액이 같은 상품은 가격 한 번, 품절은 SOLD, 빈 슬롯은 종이까지 숨긴다.
- [보상 설정 메뉴](../../Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsAuthoring.cs)의 옛 가격표 자동 작성 단계를 제거해 재실행 시에도 B1 프리팹을 유지한다. [기존 검증 메뉴](../../Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsValidation.cs)의 표시 기대값도 전체 할인과 특가가 함께 적용된 가격에 맞췄다.
- 검증은 생성된 Editor.csproj 및 프로젝트 참조의 MSBuild 성공, 두 프리팹의 로컬 fileID·시트의 세 스프라이트 참조·상품 아래 배치 대조를 포함했다. 구매·환불·가격 계산 소스와 실제 허브 씬, 런 상인 그룹은 HEAD와 동일했다. 자동 테스트와 Play Mode는 실행하지 않았다.
- 격리된 Unity 6000.4.2f1 에디터에서 두 프리팹과 스프라이트를 임포트해 한국어 가격표를 렌더링했다. 첫 캡처에서 결제 숫자가 종이 아래 테두리에 닿아 글자 크기와 기준선을 조정했다. [런 상점](../../outputs/shop-discount-preview-2026-11-04/applied-b1/b1-merchant.png)에서 950→722와 −24%, [허브 마정석 슬롯](../../outputs/shop-discount-preview-2026-11-04/applied-b1/b1-hub-price.png)에서 정수 반올림 후 8→6과 −25%, [정상가·품절·빈 슬롯](../../outputs/shop-discount-preview-2026-11-04/applied-b1/b1-states-native.png) 표시를 확인했다. 허브 캡처는 비교를 위해 같은 진열대에 허브 프리팹을 배치한 것이다. 실제 플레이 카메라와 언어 전환은 미확인이다.
- 최종 렌더에 사용한 게임·에디터 파일 7개의 SHA-256이 작업 트리와 일치했다. 검토용 임시 프로젝트 `%TEMP%/b1-shop-2d430eec/Stage`는 재귀 삭제가 실행 정책에서 차단되어 남아 있다(약 2.3 GiB).
- 문서 영향은 SessionLog와 [RunGoldAndShop](../StructureMemory/RunGoldAndShop.md)이다. 이번 적용에 따른 ErrorLog·DecisionLog 및 Presentation HTML 갱신 사항은 없다.

## 2026-11-05 정가 취소선과 상품 겹침

- [정가 전용 TMP 라벨](../../Assets/_Project/Runtime/UI/Shop/ShopOriginalPriceText.cs)과 Unity 메타를 추가했다. 기존 정가 텍스트의 첫 숫자 상단과 하단 사이 중앙에 취소선을 그린다. [허브 슬롯](../../Assets/_Project/Prefabs/UI/Dialogue/ShopSlot.prefab)과 [런 골드 슬롯](../../Assets/_Project/Prefabs/UI/Dialogue/ShopSlot_RunGold.prefab)의 정가에 연결하고, 두 슬롯 모두 기존 `ItemDisplay` 레이어에서 양피지를 순서 1, 정가·결제액·할인율·통화 아이콘을 순서 2에 두었다. 번개창의 전용 진열 그룹은 같은 레이어의 순서 0이다.
- 격리된 Unity 6000.4.2f1 편집 모드에서 두 프리팹을 임포트하고 한국어 글꼴로 렌더링했다. 기존 1280×720 캡처와 비교해 정가 950의 취소선이 화면 y=376에서 y=370으로 올라갔다. 숫자 위·아래는 y=359~380이다. [겹침 확인 이미지](../../outputs/shop-discount-preview-2026-11-04/applied-b1/b1-price-overlap.png)는 비교용으로 첫 번째 검만 내려서 양피지가 칼날을 가리는 모습을 보여 준다. 실제 슬롯의 상품 위치는 유지했다.
- 새 스크립트를 임시 MSBuild 대상으로 포함해 UI 어셈블리 빌드에 성공했다. 두 프리팹의 로컬 참조, 정가 필드 연결, 각 렌더러의 순서와 렌더에 사용한 네 파일의 일치를 대조했다. 자동 테스트와 실제 Play Mode는 실행하지 않았다.
- 문서 영향은 SessionLog와 [RunGoldAndShop](../StructureMemory/RunGoldAndShop.md)이다. ErrorLog·DecisionLog 및 Presentation HTML 갱신 사항은 없다.

## 2026-11-05 플레이어가 가격표 앞에 표시되도록 조정

- [공용 진열대 프리팹](../../Assets/_Project/Scenes/ShopSlotPositionGroup.prefab)의 루트에 `SortingGroup`을 추가해 진열대 전체를 `Entity` 순서 −10에 배치했다. 진열대 안에서는 일반 상품과 번개창보다 양피지·가격 숫자·통화 아이콘이 앞에 그려지고, 밖에서는 플레이어의 몸체(순서 0)와 그림자(−1)가 진열대보다 앞에 그려진다.
- 허브 씬과 런 상인 프리팹의 상품 앵커 각 네 개가 공용 진열대의 자식을 참조하는지 대조했다. 문서 영향은 SessionLog와 [RunGoldAndShop](../StructureMemory/RunGoldAndShop.md)이며, ErrorLog·DecisionLog 및 Presentation HTML 갱신 사항은 없다.
- [Unity 편집 모드 캡처](../../outputs/shop-discount-preview-2026-11-04/applied-b1/b1-player-in-front.png)에서 실제 플레이어 대기 스프라이트와 검을 첫 가격표에 겹쳤다. 같은 구도에서 정렬 그룹을 켜고 끈 화면을 비교해 양피지가 칼날을 가리고 플레이어가 양피지와 글자를 가리는 것을 확인했다. 실제 Play Mode와 자동 테스트는 실행하지 않았다.
