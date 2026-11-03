# 상자 유물 레어도 이중 테두리

사용자가 선택한 C안에서 상단 장식과 전용유물 하단 등급 표시를 제거한 형태를 적용했다.

## 이미지

- [상자 UI 미리보기](chest-ui.png): 실제 `GlobalUIRoot`의 상자 UI 사본을 Unity 에디터에서 렌더링한 이미지.
- [아이템 슬롯 확대](chest-slots-closeup.png): 같은 UI의 아이템 영역 확대.
- [승인된 디자인 견본](approved-concept.png): 이미지 생성 도구로 만든 이전 디자인 참고본. 실제 적용 결과는 위 에디터 렌더링 이미지가 기준이다.

각 줄은 일반 → 희귀 → 전설 → 전용유물 순서다. 미리보기에는 기존 유물 정의 8개를 사용한다. 전용유물의 `Lv 1`은 기존 강화 레벨 텍스트이며, 제거한 레어도 보석 표식과 별개다.

| 구분 | 외곽선 색 |
| --- | --- |
| 일반 | `#C8CDD6` |
| 희귀 | `#409CFF` |
| 전설 (`Epic`) | `#F2C14E` |
| 전용유물 | `#9BE34A` — 레어도보다 우선 |

## 변경 범위

- [ItemTypeBorderGraphic.cs](../../Assets/_Project/Runtime/UI/Inventory/Common/ItemTypeBorderGraphic.cs): 기존 authored Graphic에서 상자 유물용 이중 테두리와 색을 투영한다. 전용유물은 기존 `RelicLogic_WeaponExclusive`로 판별한다. 같은 종류 아이템끼리 교체해도 새 등급 색으로 갱신한다.
- [ItemSlotUI.cs](../../Assets/_Project/Runtime/UI/Inventory/Common/ItemSlotUI.cs): 바인딩마다 상자 테두리 사용 여부를 전달하고 해제 시 초기화한다.
- [ChestScreen.cs](../../Assets/_Project/Runtime/UI/Chest/ChestScreen.cs): 첫 표시, 재추첨, 획득 이동 사본과 마지막 재바인딩에서 상자 테두리를 사용한다.
- 상자 밖 인벤토리와 레어도 필드가 없는 무기는 기존 종류 테두리를 유지한다. 새 UI 계층/매니저/상태 소유자, 프리팹·씬 변경, 직렬화 필드 변경, 드롭·획득 규칙 변경은 없다.

## 이미지 제작과 확인 범위

- [캡처 소스](ChestBorderPreviewCapture.cs)는 요청된 이미지 제작용이며 자동 테스트가 아니다. 임시 Editor 사본으로 실행한 뒤 해당 사본과 생성된 메타파일을 제거한다. 소스는 재현을 위해 이 폴더에만 보관한다.
- 실제 프리팹 사본과 슬롯 표시 코드를 Edit Mode에서 사용한다. 미리보기 카메라가 상자 영역을 확대하며 플레이어 인벤토리는 임시 사본에서 숨긴다. Play Mode에 진입하거나 아이템을 실제 획득하지 않는다.
- 원본 에셋/씬 저장과 `AssetDatabase.SaveAssets` 호출은 없다. 런타임 상자 열기·재추첨·선택·획득 애니메이션의 수동 플레이 확인과 자동 테스트는 실행하지 않는다.
- `UI.csproj` MSBuild 성공. 로그: [MSBuild](msbuild.log), [Unity 렌더링](unity-capture.log). 실행 중 생성된 기존 경고는 기능 성공과 구분한다.
- 기존 파일의 작업 전 사본은 `before/`, 시작 상태는 `status-before.txt`, 주요 UI 프리팹 원본 해시는 `prefab-hashes-before.json`에 보관한다.

## 문서 영향

SessionLog 및 기존 InventoryAndChestUIStructure 지도 갱신. ErrorLog/DecisionLog/Architecture/Contracts/Presentation HTML 변경 없음. Presentation HTML stale candidate 없음.
