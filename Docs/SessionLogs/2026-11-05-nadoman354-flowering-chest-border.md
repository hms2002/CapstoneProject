# 귀인의 검흔 상자 테두리

- 개화 전용유물 ‘귀인의 검흔’이 상자에서 전설 금색으로 표시되어, [상자 테두리 판별](../../Assets/_Project/Runtime/UI/Inventory/Common/ItemTypeBorderGraphic.cs)에 이 유물의 효과 타입 `RelicLogic_FloweringBloomExtension_Managed`를 추가했다. 전용유물 분기가 등급 조회에 앞서 연두색 `#9BE34A`를 선택한다.
- [인벤토리와 상자 UI 구조 지도](../StructureMemory/ScriptSystems/InventoryAndChestUIStructure.md)에 두 전용유물 효과 타입의 색상 판별 기준을 기록했다.
- 확인한 범위는 유물 정의 → 전략 에셋 → 효과 스크립트의 GUID 참조, 상자 첫 표시·재추첨·획득 이동 사본의 슬롯 바인딩, diff 공백 및 `UI.csproj` MSBuild 성공이다. 기존 UI 어셈블리 안에서 조건식만 수정했다.
- 자동 테스트와 Play Mode 시각 확인은 미실행이다. 실플레이 확인 지점은 상자에서 귀인의 검흔을 표시하고 선택할 때의 연두색 이중 테두리다.
- 문서 영향은 SessionLog와 StructureMemory다. ErrorLog·DecisionLog 갱신 및 Presentation HTML 후속 작업은 필요하지 않다.
