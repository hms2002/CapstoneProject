# 술장판 머리 위 상태 표시

전투 중 혼란으로 걷기와 방향 대시가 반전되는 순간, 그리고 정상으로 돌아오는 순간을 플레이어 머리 위에서 읽을 수 있도록 했다. 사용자가 맥주 몬스터 잔의 표정으로 두 효과를 구분하는 안을 선택했다.

- [전용 픽셀 시트](../../Assets/_Project/Art/Sprites/UI/StatusIcons/AlcoholMonsterMug.png)와 [임포터](../../Assets/_Project/Art/Sprites/UI/StatusIcons/AlcoholMonsterMug.png.meta)에 충전·혼란·버프 3개 포즈를 담았다. [셰이더](../../Assets/_Project/Art/Shaders/AlcoholMugFill.shader)는 충전 중 빈 부분을 흐리게 표시하고 활성 표정 아래 작은 창에 남은 노출량을 표시한다.
- [노출 상태](../../Assets/_Project/Runtime/Features/Player/Status/PlayerAlcoholExposure.cs)에서 이 플레이어의 술장판이 건 실제 효과를 조회한다. [머리 위 연출](../../Assets/_Project/Runtime/Presentation/Map/Puddles/AlcoholExposurePlayerVisual.cs)은 상태가 바뀔 때 한 번만 잔을 확대한다. 혼란은 잔이 비워지는 동안에도 흔들리고, 버프는 잔이 빈 뒤에도 실제 효과 종료까지 표시된다.
- [플레이어 프리팹](../../Assets/_Project/Prefabs/Player/PF%20Player.prefab)에 세 포즈를 연결했다. [기존 제작 메뉴](../../Assets/_Project/Editor/Tools/Data/SpiriAlcoholRewardAuthoring.cs)에서도 같은 포즈를 연결한다.
- 검증은 Unity 6000.4.2f1 스크립트 컴파일, 도메인 리로드, 시트·셰이더·프리팹 임포트와 생성된 Editor 프로젝트 MSBuild로 진행했다. Unity가 임포트한 Sprite 세 개의 ID·피벗·PPU를 프리팹 참조와 대조했다. 실제 전투에서 걷거나 대시하면서 느끼는 가독성은 수동 확인 대상으로 남는다. Play Mode, 자동 테스트, Player 빌드는 실행하지 않았다.

[구조](../StructureMemory/AlcoholExposure.md) · [결정 기록](../DecisionLog.md)

## 유리 맥주잔으로 수정

사용자 피드백에 따라 나무통과 얼굴판을 곧은 유리 벽, 두꺼운 잔 바닥, 안쪽이 비치는 손잡이로 교체했다. 눈을 유리 위에 직접 그려 혼란은 보라색 소용돌이, 버프는 웃는 눈과 민트색 상승 화살표로 읽힌다.

- 기존 픽셀 시트를 같은 96×32 규격으로 교체했다. 생성한 원화를 세 칸의 바닥과 손잡이 위치에 맞춰 축소하고, 작은 크기에서 뭉치던 눈과 화살표 픽셀을 정리했다.
- 술잔 셰이더는 맥주의 수면을 아래위로 이동시켜 충전과 배출을 보여 준다. 효과가 지속되는 동안에는 유리와 눈이 계속 보인다.
- Unity에서 스프라이트와 셰이더의 임포트를 확인했다. 채팅 미리보기에서 충전 중, 두 활성 표정, 장판 이탈 후 잔류 버프와 만료를 확인했다. 이번 아트 수정에서 Play Mode와 자동 테스트는 실행하지 않았다. 실전 배경에서의 가독성은 수동 확인 대상으로 남는다.

<details>
<summary>원화 제작 프롬프트 (내장 ImageGen)</summary>

Edit target is the existing three-cell pixel sprite sheet for an overhead Unity status indicator. Completely redraw it as a handsome, straight-sided TRANSPARENT GLASS BEER STEIN. Deliver ONE wide transparent sprite strip, three equal square cells in one horizontal row, one mug centered in each cell. All silhouettes, size, handle, and baselines must align. Use pixel art actually designed on a tiny 32 by 32 grid per mug, enlarged with crisp square nearest-neighbor pixels. Draw a tall, upright straight clear glass with parallel sides, thick pale blue transparent glass base, a rectangular clear glass C handle on the right with a see-through hole, a thin dark blue-gray contour, a tall vertical white reflection, clean luminous golden lager. Classic bright and elegant game inventory icon; glass and golden beer should be instantly recognizable, no wood, metal bands, plaques, goggles, mouth, faceplates, accessories, tints across the face or character bodies. Face detail should be only the eyes directly on the beer behind glass. Cell 1: calm plain full golden beer, a thin rim, no eyes and no foam crown (game reveals liquid height dynamically). Cell 2: same glass with a small creamy white head of foam, two clear bold dark-violet spiral eyes drawn directly on the amber, a tiny lavender dizzy glint next to the foam if needed. Cell 3: same full gold glass with small white foam head and two happy upward-curved crescent eyes directly on the amber. A single tiny mint up arrow in the gap above the handle for the buff. Black/dark outline detail only where it separates the glass or eyes; favor spacious amber field, a few large readable clusters over noisy detail. Transparent outside all mugs and through handle holes. Strict front view, no perspective tilt. No shadows, background, text, labels, grid lines or UI.

</details>

## 64×64 나무 맥주잔으로 수정

사용자가 나무잔으로 돌아가 각 상태를 64×64픽셀로 그려 달라고 요청했다. 꿀빛 참나무 판재, 가는 황동 테와 둥근 나무 손잡이를 사용했다. 혼란은 연보라 소용돌이 눈, 버프는 웃는 눈과 민트색 상승 화살표로 구분한다.

- [픽셀 시트](../../Assets/_Project/Art/Sprites/UI/StatusIcons/AlcoholMonsterMug.png)를 192×64로 교체하고 [임포터](../../Assets/_Project/Art/Sprites/UI/StatusIcons/AlcoholMonsterMug.png.meta)를 64×64 세 칸, 64 PPU로 맞췄다. 프리팹에서 사용하던 표시 배율, 하단 중앙 피벗과 세 Sprite ID를 유지했다.
- [셰이더](../../Assets/_Project/Art/Shaders/AlcoholMugFill.shader)는 위아래 황동 테 사이의 나무결에 차오르는 높이를 표시한다. 활성 효과의 표정은 별도로 읽히며 기존 물방울은 32 PPU 좌표를 사용한다.
- Unity 6000.4.2f1의 임포트 결과에서 세 Sprite의 64×64 사각형·64 PPU·피벗·ID를 읽고 플레이어 프리팹 참조와 대조했다. 미리보기에서 충전, 혼란, 버프, 장판 이탈 후 잔류 버프와 만료를 확인했다. 실제 전투 Play Mode와 자동 테스트는 실행하지 않았다.

<details>
<summary>나무잔 원화 프롬프트 (내장 ImageGen)</summary>

Redraw the provided three-state sprite strip as a beautiful polished wooden tavern tankard for a cozy fantasy pixel RPG. Single horizontal sheet, THREE equally spaced and perfectly aligned front-facing sprites of exactly the SAME iconic tankard, each designed at a true 64x64 native pixel-art resolution enlarged with nearest neighbor. Transparent background and transparent handle holes. Wide 3:1 composition, regular cell centers, same body, handle, bands, baseline and scale in all three cells. A handsome, gently rounded honey-oak tankard with a slightly tapered foot, three substantial warm wooden staves, broad areas of unbroken amber wood in the middle to display eyes; buttery edge-light, understated carved grain, two SLENDER aged-brass hoops set only near lip and foot with one or two tiny rivets; rounded carved walnut D-handle on right. Small, dark brown pixel outline, cozy golden bounce light, rich warm colors, premium SNES inventory icon craft, selective clusters with crisp un-antialiased stair-step edges. The wood must feel hand-carved and tactile yet instantly read as a beer mug. Small top ellipse reveals golden beer. No glass, label, face plate, goggles, scarf, red ribbon, arms, feet, teeth, text, floor or ground shadow. The three frames from left to right: (1) calm charge-state mug, plain broad oak front WITHOUT eyes, visible dark inner rim with a narrow golden beer surface, very little foam. (2) identical oak mug now topped with a charming low scalloped ivory beer head and a single short foam drip on the far left lip. Two strongly readable vivid pale lilac spiral eyes inked directly onto the center wood with dark-plum outlines, implying dizziness; a tiny lavender four-point glint above the handle. (3) same wood shape and creamy foam as the second, with two happy arched eyes (closed ^ ^) directly on the center wood with a warm almost-black outline, one tiny mint-green upward arrow above its handle for a speed buff. No mouth. Faces should feel endearing, spacious, and clean. Do not color the entire mug green or purple. All three are versions of the same finished asset, no borders/dividers.

</details>

## Aseprite 64×64 원본으로 다시 제작

큰 원화를 축소한 이전 시트 대신, Aseprite 1.3.18.6에서 빈 64×64 인덱스 컬러 캔버스에 1픽셀 단위로 그렸다. [편집 원본](../../Assets/_Project/Art/Sprites/UI/StatusIcons/Source~/AlcoholMonsterMug.aseprite)은 충전·혼란·버프 태그가 붙은 3프레임과 손잡이, 나무 몸체, 황동 테, 잔 입구와 맥주, 거품, 눈, 상태 마크의 7레이어를 담고 있다.

- [게임용 PNG](../../Assets/_Project/Art/Sprites/UI/StatusIcons/AlcoholMonsterMug.png)는 이 원본을 1× 배율의 가로 시트로 내보낸 결과다. 프레임마다 64×64이고 전체는 192×64다.
- 저장한 원본을 Aseprite로 다시 열어 캔버스, 레이어와 태그를 읽고 재수출했다. Aseprite 출력과 적용된 PNG의 SHA-256이 일치했다. Unity에서 시트의 재임포트를 확인했다. 실제 전투에서의 시인성은 수동 확인 대상으로 남는다. Play Mode와 자동 테스트는 실행하지 않았다.

## 32×32 최종 규격

- [Aseprite 원본](../../Assets/_Project/Art/Sprites/UI/StatusIcons/Source~/AlcoholMonsterMug.aseprite)을 32×32로 줄이고, 눈과 상태 마크를 새 픽셀 격자에 맞춰 다시 그렸다. 세 상태 태그와 편집 레이어 7개를 유지했다.
- [게임용 PNG](../../Assets/_Project/Art/Sprites/UI/StatusIcons/AlcoholMonsterMug.png)는 1× 가로 수출본인 96×32다. [임포터](../../Assets/_Project/Art/Sprites/UI/StatusIcons/AlcoholMonsterMug.png.meta)는 32×32 세 칸과 32 PPU로 설정해 머리 위 월드 표시 배율을 유지한다. [셰이더](../../Assets/_Project/Art/Shaders/AlcoholMugFill.shader)의 충전 높이와 나무 영역을 새 격자에 맞췄다.
- Aseprite에서 원본을 다시 열어 내보낸 PNG가 적용된 파일과 바이트 단위로 일치했다. Unity 임포트 결과에서 세 Sprite의 32×32·32 PPU·하단 중앙 피벗과 플레이어 프리팹의 참조 ID를 대조했다. 채팅 미리보기에서 1×·3× 표시 및 잔류 버프를 확인했다. 실전 전투에서의 가독성은 수동 확인 대상으로 남는다. Play Mode와 자동 테스트는 실행하지 않았다.

## 32×32 술잔 테두리와 흔들림 정리

- Aseprite에서 몸통, 손잡이, 황동 띠, 잔 입구를 세 프레임의 연결 셀로 묶었다. 손잡이 구멍과 거품을 포함한 잔 가장자리에 같은 색을 사용하고, 두 황동 띠의 반사광은 윤곽 안쪽에 두었다. 혼란의 양쪽 소용돌이 눈은 같은 높이에 놓고 버프 화살표에는 촉과 줄기를 그렸다.
- AlcoholExposurePlayerVisual.cs에서 혼란·넘침의 좌우 흔들림과 효과 시작의 짧은 들썩임을 원본 픽셀 단위 이동으로 바꿨다. AlcoholExposurePlayerVisualPlayModeTests.cs의 기존 흔들림 검사는 이동 간격, 고정된 회전, 일시정지와 원위치 복귀를 확인하도록 맞췄다.
- Aseprite 재수출본과 게임용 PNG가 일치했다. 세 상태에서 손잡이 구멍은 각각 하나였고 잔의 경계 픽셀은 모두 동일한 외곽색이었다. Unity 텍스처 임포트와 Presentation 및 ProceduralPlayModeTests 프로젝트의 MSBuild를 확인했다. Play Mode와 자동 테스트는 실행하지 않았다. 실제 전투 배율에서의 가독성은 수동 확인 대상으로 남는다.

## 참고 그림에 맞춘 혼란 눈

- Aseprite 원본의 혼란 프레임 Eyes 셀에 짙은 보라색 한 획이 같은 방향으로 감기는 두 눈을 그렸다. 각 눈은 8×9픽셀이고 눈 사이는 2픽셀이다. 32×32 세 상태를 게임용 PNG로 다시 내보냈다.
- 원본 셀을 대조한 결과 혼란 눈 셀 하나만 바뀌었다. PNG에서도 바뀐 116픽셀은 모두 혼란 눈 영역에 있었고, 저장된 원본의 1× 재수출본과 게임용 PNG가 일치했다. Unity Play Mode와 자동 테스트는 실행하지 않았다.
- 게임 적용 확인. 플레이어 프리팹의 confusedMug 참조와 Unity가 임포트한 Mug_Confused의 ID(21300004)가 일치하고, 표시 컴포넌트는 실제 혼란 효과가 활성화되면 이 스프라이트를 선택한다. 허브와 절차형 전투 씬에서 같은 플레이어 프리팹을 참조하는 것도 확인했다.
