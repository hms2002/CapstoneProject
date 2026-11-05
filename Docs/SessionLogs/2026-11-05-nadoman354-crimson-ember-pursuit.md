# 홍련의 한계선 · 잔불 추격

‘한계선의 잔불 추격’을 보유하고 ‘점화!’를 차징하면 조준점 위쪽, 왼쪽 아래, 오른쪽 아래에서 같은 크기의 마법진 세 개가 함께 커진다. 원 중심 사이 거리는 현재 반지름과 같다. 발사하면 마지막으로 표시한 세 중심에 운석이 동시에 떨어진다. 적은 자신이 걸친 원마다 피해와 화상을 한 번씩 받는다.

## 변경 파일

- [AbilityLogic_CrimsonBoundaryCharge.cs](../../Assets/_Project/Runtime/Features/Items/Weapons/CrimsonBoundary/AbilityLogic_CrimsonBoundaryCharge.cs)는 시전 시작에 기존 유물 ID를 조회하고, 하나의 차징 시간과 쿨다운으로 각 원의 조준·비행·타격을 처리한다. [AbilityLogic_CrimsonBoundaryIgnite.cs](../../Assets/_Project/Runtime/Features/Items/Weapons/CrimsonBoundary/AbilityLogic_CrimsonBoundaryIgnite.cs)에서도 이전 처치 시 추적탄 발동을 제거했다.
- [CrimsonBoundaryRuntimeState.cs](../../Assets/_Project/Runtime/Features/Items/Weapons/CrimsonBoundary/CrimsonBoundaryRuntimeState.cs)는 시전 중인 마법진들을 함께 정리한다. [CrimsonBoundaryChargeData.cs](../../Assets/_Project/Runtime/Features/Items/Weapons/CrimsonBoundary/CrimsonBoundaryChargeData.cs)의 표시 인터페이스와 [CrimsonBoundaryChargePresentation.cs](../../Assets/_Project/Runtime/Presentation/VFX/Presentation/CrimsonBoundaryChargePresentation.cs)는 첫 마법진만 플레이어 게이지를 표시하도록 연결한다.
- [RD_CrimsonKillShot.asset](../../Assets/_Project/Data/Items/Relics/Definitions/RD_CrimsonKillShot.asset)의 한국어 설명, [GameText.csv](../../DataSheets/Localization/GameText.csv)와 [런타임 언어 테이블 5개](../../Assets/_Project/Data/Localization/), [AssetInventory.json](../../DataSheets/Localization/AssetInventory.json), [CommonTranslations.tsv](../../DataSheets/Localization/CommonTranslations.tsv)에 같은 효과를 반영했다.
- [무기 구조](../StructureMemory/ScriptSystems/BurnStatusAndCrimsonBoundaryWeapon.md#ember-pursuit-2026-11-05)와 [무기 유물 실행 경로](../StructureMemory/ScriptSystems/WeaponAndGASStructure.md)를 갱신했다.

## 검증과 남은 확인

- Gameplay와 Presentation을 포함한 MSBuild가 성공했다. 유물 유무에 따른 원 개수, 부분·최대 차징, 조기 해제, 착탄 전 취소, 무기 비활성화와 씬 전환 경로를 소스에서 확인했다.
- 다섯 언어의 유물 설명을 CSV와 런타임 테이블에서 대조했다. 유물 ID, 언어 키, 프리팹 참조는 기존 값을 사용한다.
- Unity 플레이 모드와 자동 테스트는 실행하지 않았다. 수동 확인 항목은 조준점을 움직일 때의 가독성, 화면 가장자리에서의 배치, 교집합에서의 타격 횟수다.

## 11월 5일 · 잔불 추격 전용 마법진 시안

- Aseprite 1.3.18.6에서 현재 세 착탄점 간격에 맞춰 [편집 원본](../../outputs/crimson-ember-pursuit-art-2026-11-05/ember-pursuit.aseprite)과 [투명 PNG](../../outputs/crimson-ember-pursuit-art-2026-11-05/ember-pursuit.png)를 만들었다. 320×320 캔버스의 중앙이 조준점이다. 각 원을 개별 그룹으로 나누고 원마다 범위선, 바깥 연꽃 장식, 착탄점 불씨를 별도 레이어에 그렸다.
- [기존 그림과 시안 비교](../../outputs/crimson-ember-pursuit-art-2026-11-05/comparison.png)는 같은 바닥 타일과 배율을 사용한다. [차징 미리보기](../../outputs/crimson-ember-pursuit-art-2026-11-05/charge-preview.gif)는 현재 스킬의 1.4초 차징, 발사 뒤 0.5초 위치 고정, 0.25초 페이드를 회전 없이 보여 준다.
- Aseprite로 편집 원본을 다시 열어 3개 그룹과 9개 레이어, PNG 내보내기의 픽셀 일치를 확인했다. GIF 44프레임에서 확대, 위치 고정, 소멸을 확인했다. 이 시안은 게임 적용 전 단계다.

### 테두리 장식과 독립 회전 시안

- [회전하는 Aseprite 원본](../../outputs/crimson-ember-pursuit-art-2026-11-05/rotating-border/ember-pursuit-rotating.aseprite)에 연꽃잎 문양, 가는 곡선, 금빛 마름모를 그렸다. 각 원은 자신의 착탄점을 중심으로 초당 24°씩 회전한다. 위와 오른쪽 아래는 시계 방향, 왼쪽 아래는 반시계 방향이다. 다른 원 안으로 들어가는 장식은 흐려지고 불꽃 주변은 비워 둔다.
- [차징·회전 GIF](../../outputs/crimson-ember-pursuit-art-2026-11-05/rotating-border/charge-and-rotation.gif)는 최대 차징 상태를 잠시 유지해 회전을 볼 수 있게 했다. [이전 시안과 비교](../../outputs/crimson-ember-pursuit-art-2026-11-05/rotating-border/border-comparison.png)와 [투명 PNG](../../outputs/crimson-ember-pursuit-art-2026-11-05/rotating-border/ember-pursuit.png)도 함께 내보냈다.
- Aseprite에서 100프레임 원본을 다시 열어 세 테두리의 회전 방향을 확인했다. 각 불꽃의 147픽셀은 모든 프레임에서 이전 원본과 위치·색상이 일치한다. 게임 적용은 보류한 상태다.
- 채팅에서 회전을 바로 볼 수 있도록 Aseprite 원본의 최대 크기 100프레임을 [회전 전용 GIF](../../outputs/crimson-ember-pursuit-art-2026-11-05/rotating-border/rotation-only.gif)로 내보냈다. 5초 무한 반복과 서로 다른 100프레임을 확인했고, 대화용 미리보기를 브라우저에서 재생해 테두리가 움직이는 것을 확인했다.

### 불꽃과 겹친 장식 비교

- [불꽃 없는 견본](../../outputs/crimson-ember-pursuit-art-2026-11-05/rim-variants/no-embers.gif)은 각 착탄점의 불꽃을 숨기고 그 자리에 끊겼던 범위선을 이었다. 겹친 곳에서는 기존처럼 장식이 흐려진다.
- [장식 불투명도 100% 견본](../../outputs/crimson-ember-pursuit-art-2026-11-05/rim-variants/full-opacity.gif)은 세 불꽃과 그 주변 여백을 유지한다. 겹친 곳을 지나는 장식도 바깥과 같은 불투명도로 표시한다. [두 변경을 합친 견본](../../outputs/crimson-ember-pursuit-art-2026-11-05/rim-variants/no-embers-full-opacity.gif)도 같은 회전 타이밍으로 내보냈다. 각 GIF 옆에 같은 이름의 편집용 Aseprite 원본이 있다.
- 세 원본을 다시 열어 100프레임의 불꽃 표시 여부와 회전 장식의 픽셀 알파를 확인했다. GIF 세 개는 각각 5초 반복이며 브라우저에서 같은 시점의 비교와 불꽃 전환을 확인했다. Unity 적용 전 미술 시안이다.

### 선택한 시안의 게임 적용

- 기본 ‘점화!’는 완전한 원 하나, 잔불 추격은 원 세 개를 표시한다. 두 경우 모두 착탄점의 불꽃은 고정하고 연꽃 장식만 초당 24° 회전한다. 유물의 왼쪽 아래 원만 반시계 방향이다. 장식이 교차하면 불투명도를 그대로 유지하며 이웃 불꽃 바로 주위에는 작은 여백이 있다.
- [Aseprite 원본과 게임용 PNG](../../Assets/_Project/Art/Sprites/VFX/CrimsonBoundary/MagicCircle/)를 갱신했다. `center`는 불꽃, `inner`는 회전 장식, `outer`는 기본 범위선이다. `outer.aseprite`의 유물 전용 레이어를 `relicContour.png`로 내보내고 [차징 프리팹](../../Assets/_Project/Prefabs/VFX/CrimsonBoundary/ChargeSixColor/CrimsonChargePresentation.prefab)에 연결했다.
- [차징 로직과 연출 인터페이스](../../Assets/_Project/Runtime/Features/Items/Weapons/CrimsonBoundary/)는 게임플레이가 사용하는 두 이웃 착탄점의 상대 위치를 [연출 컴포넌트](../../Assets/_Project/Runtime/Presentation/VFX/Presentation/CrimsonBoundaryChargePresentation.cs)에 전달한다. [마법진 셰이더](../../Assets/_Project/Art/Shaders/CrimsonMagicCircle.shader)는 범위 반경의 1/8 이내에서만 이웃 불꽃을 가로지르는 장식을 자른다. 네 스프라이트는 같은 중심과 192×192 캔버스를 쓰며 착탄 지름에 맞춰 커진다.
- 실제 PNG 네 장과 Aseprite 내보내기의 일치, 원본 불꽃의 불투명 147픽셀, 장식의 알파 0/255, 프리팹의 스프라이트 참조와 16 PPU 설정을 확인했다. Gameplay·Presentation MSBuild와 D3D11 셰이더의 정점·프래그먼트 4개 조합 컴파일이 통과했다. 자동 테스트와 Unity 플레이 모드는 실행하지 않았다. 실제 전투 화면에서의 회전 가독성은 수동 확인 항목이다.
- Unity 6000.4.2f1 편집 모드에서 새로고침했다. 에디터 로그에서 PNG 네 장, Aseprite 원본 세 개, 셰이더와 프리팹의 임포트 성공 및 스크립트 컴파일·어셈블리 재로드 완료를 확인했다. 에디터가 `center.aseprite.meta`의 잘라낸 불꽃 크기를 13×18로 갱신했고 원본 GUID는 유지했다.

### 중앙 불꽃 제거

- [기본·유물 공용 프리팹](../../Assets/_Project/Prefabs/VFX/CrimsonBoundary/ChargeSixColor/CrimsonChargePresentation.prefab)에서 중앙 불꽃을 표시하던 자식을 제거했다. [연출 컴포넌트](../../Assets/_Project/Runtime/Presentation/VFX/Presentation/CrimsonBoundaryChargePresentation.cs)는 범위선과 연꽃 장식만 차징·회전·페이드한다. [차징 로직과 인터페이스](../../Assets/_Project/Runtime/Features/Items/Weapons/CrimsonBoundary/)는 각 원의 방향만 연출에 전달한다.
- Aseprite에서 [범위선 원본](../../Assets/_Project/Art/Sprites/VFX/CrimsonBoundary/MagicCircle/outer.aseprite)의 유물 레이어에 두 곳의 끊긴 선을 이었다. [게임용 범위선](../../Assets/_Project/Art/Sprites/VFX/CrimsonBoundary/MagicCircle/relicContour.png)에 총 176픽셀을 복원하고 [셰이더](../../Assets/_Project/Art/Shaders/CrimsonMagicCircle.shader)에서 불꽃 여백을 만드는 마스크를 제거했다. [회전 GIF](../../outputs/crimson-ember-pursuit-art-2026-11-05/rim-variants/no-embers-full-opacity.gif)는 같은 디자인의 Aseprite 견본이다.
- 기본·유물의 두 Aseprite 레이어를 다시 내보내 각각의 게임용 PNG와 픽셀이 일치함을 확인했다. 프리팹에서 중앙 스프라이트 참조가 사라지고 테두리 참조와 회전 속도 24°/초가 유지됨을 확인했다. Gameplay를 포함한 Presentation MSBuild가 통과했다. 이번 수정의 Unity 에디터 재임포트·플레이 모드와 자동 테스트는 확인하지 않았다.

### 검은 외곽선 제거

- [범위선 Aseprite 원본](../../Assets/_Project/Art/Sprites/VFX/CrimsonBoundary/MagicCircle/outer.aseprite)의 기본·유물 레이어에서 검게 보이던 짙은 자주색 선을 지우고 [기본 범위선](../../Assets/_Project/Art/Sprites/VFX/CrimsonBoundary/MagicCircle/outer.png)과 [잔불 추격 범위선](../../Assets/_Project/Art/Sprites/VFX/CrimsonBoundary/MagicCircle/relicContour.png)을 다시 내보냈다. 픽셀 비교에서 기본 540개, 유물 766개가 투명해졌고 나머지 픽셀은 일치했다.
- [회전 견본 원본](../../outputs/crimson-ember-pursuit-art-2026-11-05/rim-variants/no-dark-outline.aseprite)과 [GIF](../../outputs/crimson-ember-pursuit-art-2026-11-05/rim-variants/no-dark-outline.gif)에도 같은 수정을 반영했다. Aseprite 원본을 다시 열어 두 게임용 PNG의 일치와 GIF의 100프레임·5초 반복을 확인했다. 이번 변경은 미술 에셋만 대상으로 했으며 Unity 플레이 모드와 자동 테스트는 실행하지 않았다.
