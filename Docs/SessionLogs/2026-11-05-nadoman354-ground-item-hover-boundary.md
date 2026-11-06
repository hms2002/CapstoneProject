# 바닥 아이템 설명 UI 경계 깜빡임

플레이어가 감지 범위 끝에 서 있을 때 설명 UI가 빠르게 여러 번 열린다는 보고가 있었다. 기존 코드에서는 마지막 트리거 접촉을 잃으면 선택을 바로 해제하고, 다시 닿으면 같은 아이템에도 `OnHighlight`를 다시 호출했다. 두 드롭이 함께 감지될 때는 두 중심까지의 거리가 조금만 역전되어도 선택이 바뀌었다.

## 변경

- [PlayerInteractionSensor2D](../../Assets/_Project/Runtime/Features/Player/Interaction/PlayerInteractionSensor2D.cs)가 플레이어 몸체 모양에 맞춰 설정한 실제 트리거를 보관하고, [PlayerInteractor2D](../../Assets/_Project/Runtime/Features/Player/Interaction/PlayerInteractor2D.cs)가 대상 선택 시 그 트리거를 전달한다.
- [PlayerInteractionTargetResolver2D](../../Assets/_Project/Runtime/Features/Player/Interaction/PlayerInteractionTargetResolver2D.cs)는 처음 선택할 때 센서 접촉을 요구한다. 이미 선택한 바닥 아이템과 무기는 콜라이더가 최대 0.08 월드 단위 떨어져도 선택을 유지한다. 두 드롭이 경쟁하면 다른 드롭이 0.08보다 더 가까워질 때 선택을 바꾼다. 획득 입력과 설명 UI는 같은 선택 대상을 사용한다.

## 검증

- 최초 진입, 경계 이탈과 재진입, 인접 드롭 전환, 상호작용 잠금, 획득 후 비활성화 및 파괴 경로를 소스에서 확인했다. 변경된 스크립트 3개는 모두 Gameplay 어셈블리에 포함된다. `Gameplay.csproj` MSBuild 결과는 오류 0개, 경고 147개이며 코드 diff의 공백 검사도 통과했다.
- Unity 플레이 모드와 자동 테스트는 실행하지 않았다. 수동 확인은 드롭 하나에 접근해 설명을 띄우고 경계에서 조금씩 앞뒤로 이동한 다음 완전히 물러나면 된다. 경계에서는 설명이 유지되고, 떠나면 닫혀야 한다. 드롭 두 개 사이에서는 다른 쪽으로 이동했을 때 설명과 실제 획득 대상이 함께 한 번 전환되는지도 확인한다.

## 감전·화상 추가 설명이 붙는 유물

- 재발 보고에서는 감전이나 화상 설명 카드가 붙은 유물이 지목됐다. [UI 프리팹](../../Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab)에서 기본 패널 716과 카드 400, 간격 16을 합하면 폭이 1,132다. 폭 1,920 캔버스에서 양쪽 후보가 모두 아이템을 가리는 구간이 생긴다. 기존 배치식에 월드 아이템 기준점의 중심 X를 137과 139로 넣으면, 겹침 폭 차이가 0에서 1로 바뀌며 기본 패널 중심이 178에서 -594로 이동한다. 같은 유물을 계속 선택해도 일어날 수 있는 위치 변화다.
- [HoverUIController](../../Assets/_Project/Runtime/UI/Common/HoverUIController.cs)는 추가 카드가 열려 있는 동안 직전 좌우 배치와 상하 정렬을 기억한다. 다른 배치가 가리는 면적을 충분히 줄일 때만 방향을 바꾸고, 새 설명을 열 때는 다시 계산한다. [ItemKeywordTooltipView](../../Assets/_Project/Runtime/UI/Inventory/Common/DetailUI/ItemKeywordTooltipView.cs)는 카드 문구나 표시 상태가 바뀐 경우에만 카드 레이아웃을 즉시 재계산한다.
- 변경된 두 스크립트가 포함된 `UI.csproj`를 MSBuild로 빌드해 오류 0개, 경고 314개를 확인했다. 프리팹 치수와 배치식을 대조했고 자동 테스트와 Unity 플레이 모드는 실행하지 않았다. 남은 수동 확인은 바닥의 감전·화상 유물에 설명을 띄운 채 조금씩 왕복해 같은 설명이 한쪽에 유지되는지, 크게 이동할 때는 카드까지 화면 안에 들어오도록 방향이 바뀌는지 보는 것이다.
