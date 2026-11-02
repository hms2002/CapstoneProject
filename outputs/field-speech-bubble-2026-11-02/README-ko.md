# 필드 대사 말풍선 편집 자료

호감도 대화 가능 표시가 아니라, 플레이어/보스 등이 필드에서 말할 때 사용하는 공용 SpeechBubblePrefab의 원본이다.

## 파일

- [SpeechBubble.png](SpeechBubble.png): 실제 교체용 원본. 원본과 바이트가 같은 19×16px RGBA 투명 PNG.
- [SpeechBubble-guide.png](SpeechBubble-guide.png): 픽셀 격자와 9분할 경계 안내. 게임에 넣는 이미지가 아니다.
- [확대 참고본](reference/SpeechBubble-32x.png): 원본 픽셀을 정확히 32배 확대한 608×512px 투명 PNG. 게임에 바로 넣지 않는다.
- [수입 설정](reference/sprite-import.json): 현재 Unity sprite/border/pivot 및 셰이더 설정.
- [색상 설정](reference/themes.csv): 기본/플레이어와 보스 4종의 현재 RGBA 색상. 8bit 환산값이다.

## 구조와 색상

여러 색의 PNG를 교체하는 방식이 아니다. 같은 흰색 모양 PNG 한 장을 UI Image의 Sliced(9분할) 방식으로 늘린다. 좌우 꼬리는 같은 배경을 좌우 반전해 표시한다. 글자는 다시 반전해 정상 방향을 유지한다.

원본은 불투명 흰색 160픽셀과 완전 투명 144픽셀로 이루어진 모양 마스크다. 검정/유색 외곽선은 원본에 들어 있지 않으며 UI/Alpha Outline 셰이더가 알파 경계에서 생성한다.

SpeechBubble.cs가 표시할 때마다 SpeechBubbleThemeSettings의 Border Color / Fill Color / Font Color를 적용한다. Use Custom Colors가 꺼져 있으면 프리팹 기본값(검정 테두리, 흰 배경 알파 0.52, 검정 글자)을 사용한다. 보스 SpeechData는 각각 사용자 지정 색을 사용한다. 런 특수 NPC 대사는 줄별 테마 설정도 가질 수 있다.

현재 SpeechBubblePrefab의 Background Texture Influence는 0이다. 따라서 PNG의 RGB 색은 무시하고 알파 모양을 사용한다. PNG에 색/문양/그라데이션을 그려도 현 설정에서는 색이 나타나지 않는다. 색만 바꾸려면 해당 캐릭터 SpeechData의 Bubble Theme에서 Use Custom Colors 및 Border/Fill/Font Color를 수정하는 것이 현재 구조에 맞는다. 일반 기본색은 SpeechBubblePrefab의 Default Theme에 있다. 머티리얼 원본의 색은 실행 중 이 테마에 의해 덮어써진다.

## 바로 교체 가능한 편집 기준

1. SpeechBubble.png를 19×16px 그대로 편집한다. 확대 참고본/안내도를 원본 대신 저장하지 않는다.
2. PNG 투명도를 유지한다. 불투명 흰색 영역이 말풍선의 채워진 모양이고, 바깥은 투명이다. 현재처럼 단단한 픽셀 경계를 유지하면 색상 테마와 셰이더 외곽선에 맞추기 쉽다.
3. 9분할 기준은 왼쪽 7 / 오른쪽 3 / 위 3 / 아래 6px이다. 이미지 편집기의 좌상단 기준 분할 경계는 x=7,16 / y=3,10이다. 가운데 9×7px은 양방향으로 늘어나고, 위아래 변은 가로로, 좌우 변은 세로로 늘어난다. 모서리 및 왼쪽 아래 꼬리가 들어가는 고정 영역을 기준으로 디자인한다.
4. 기존 파일 Assets/_Project/Art/Sprites/UI/Dialogue/SpeechBubble.png의 PNG 내용만 교체하고, 기존 SpeechBubble.png.meta는 유지한다. 삭제 후 재등록하거나 새 GUID를 만들지 않는다. 같은 경로/크기/분할 기준을 유지하면 기존 프리팹 참조와 수입 설정을 재사용할 수 있다.
5. 중앙에 그림/장식을 넣으면 늘어날 수 있다. 고정 영역 밖으로 꼬리나 장식을 옮기거나 이미지 크기/분할선을 바꾸려면 추가 Unity 설정/레이아웃 검토가 필요하다.
6. 모양이 바뀌면 공용 프리팹을 쓰는 플레이어·보스·NPC에 함께 적용된다. 교체 후 Unity에서 짧은/긴 문장과 좌우 꼬리를 확인한다.

원본 RGB 색을 그대로 살리는 디자인이 필요하면 별도 설정/표현 방식 검토가 필요하다. Texture Influence를 1로 바꾸는 것만으로도 Fill Color와 곱해지므로 원본 색이 그대로 보장되지는 않는다. 이번 작업은 원본 조사 및 내보내기만 수행했으며 프로젝트에 해당 변경을 적용하지 않았다.

## 조사 근거

- 원본: Assets/_Project/Art/Sprites/UI/Dialogue/SpeechBubble.png 및 기존 .meta
- 사용 프리팹: Assets/_Project/Prefabs/UI/SpeechBubble/SpeechBubblePrefab.prefab
- 테마 적용: Assets/_Project/Runtime/UI/Dialogue/SpeechBubble/SpeechBubble.cs, ApplyTheme
- 테마 데이터: Assets/_Project/Runtime/Core/Presentation/Speech/SpeechBubbleThemeSettings.cs
- 셰이더/머티리얼: Assets/_Project/Art/Shaders/UIAlphaOutline.shader, Assets/_Project/Art/Materials/SpeechBubbleOutLine.mat
- 색상 데이터: Assets/_Project/Data/Dialogue/SpeechData/*BossSpeechData.asset

## 확인 범위

원본 PNG 크기/팔레트/알파, 프리팹 참조, 실제 Single spriteBorder(과거 spriteSheet 잔여 정보와 구별), 테마 적용 코드 및 셰이더를 확인했다. 다운로드 PNG의 SHA-256을 원본과 대조했다. 확대본과 안내도는 원본 픽셀 및 실제 분할선을 기준으로 제작한 참고 자료이며 Unity 화면 캡처가 아니다. Unity import/compile/Play Mode는 실행하지 않았다.

프로젝트의 Assets 및 기존 Docs는 수정하지 않았다. SessionLogs/ErrorLog/DecisionLog는 갱신하지 않았다. Doc Impact Check: no doc update needed. 구조/문서 변경이 없어 Presentation HTML stale 후보 없음.
