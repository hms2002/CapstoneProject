# 경험치 픽업 — 인터넷 사례 조사 후 새로 만든 5안

2026-11-02. 사용자가 이전 조건을 모두 내려놓고 ‘경험치 알갱이’만을 기준으로 인터넷 사례를 조사하여 견본을 만들도록 요청했다. Implementation 범위는 참고 조사와 새로운 원본/미리보기 제작이다. 게임 적용은 포함하지 않는다.

## 조사에서 가져온 점

| 참고 자료 | 확인한 정보 | 이번 제작에 적용한 판단 |
| --- | --- | --- |
| [Vampire Survivors Experience Gem](https://vampire.survivors.wiki/w/Experience_Gem), [보석 이미지가 있는 설명 페이지](https://teemo.dev/game-design/vampire-survivors/systems/economy-and-resource-design/) | 적에게서 떨어진 경험치를 작은 색상별 보석으로 표시한다. 설명 페이지의 파랑·초록·빨강 보석도 직접 확인했다. | 가늘고 작은 파란 결정안을 만들고, 한눈에 보이는 실루엣과 밝은 면을 사용했다. |
| [Minecraft Experience](https://minecraft.wiki/w/Experience) | 검색에서 확인한 위키 설명은 경험치 구체가 초록과 노랑 사이로 변하고 플레이어 쪽으로 이동한다고 설명한다. | 물리적인 유리구슬 대신 빛이 모인 작은 에너지 구체라는 방향을 만들었다. 이 페이지 본문 직접 열기는 제한되어 검색에 노출된 설명만 참고했다. |
| [Brotato Materials](https://brotato.wiki.spellsandguns.com/Materials) | 작은 초록색 드롭을 얻으면 경험치와 재화를 함께 얻는다. | 수많은 작은 드롭을 반복 배치했을 때의 인식성을 살피고, 가장 작은 6×6 알갱이안을 포함했다. |
| [Willosuna Pixel Pickups & Rewards VFX](https://willosuna.itch.io/pixel-pickups-vfx) | 제작자가 16/32px 픽업, XP/마나/영혼 구체, idle 등의 애니메이션을 소개한다. 브라우저로 공개 미리보기의 실제 도트도 확인했다. | 16px 공통 캔버스, 적은 색, 편집 가능한 원본, 작은 idle 움직임으로 견본을 구성했다. |

오른쪽 열은 자료로부터 도출한 **이번 작업의 디자인 판단**이다. 특정 게임의 정답이나 공식 제작 규칙이라는 뜻은 아니다. 웹 이미지는 관찰용으로만 사용했으며 원본 파일/픽셀을 결과물에 넣거나 트레이싱하지 않았다.

## 새 견본

| 번호 | 이름 | 실제 그림 범위 | 방향 | 원본 / PNG |
| --- | --- | --- | --- | --- |
| 1 | 파란 경험치 결정 | 7×10 | 작은 세로 결정. 기본 경험치 드롭 후보 | [Aseprite](01-azure-shard.aseprite) / [PNG](01-azure-shard.png) |
| 2 | 빛 구슬 | 8×8 | 중심이 밝은 에너지 입자. 많은 수를 흩뿌리는 후보 | [Aseprite](02-lumen-orb.aseprite) / [PNG](02-lumen-orb.png) |
| 3 | 영혼 조각 | 8×10 | 꼬리가 흐르는 청록색 정수 | [Aseprite](03-soul-mote.aseprite) / [PNG](03-soul-mote.png) |
| 4 | 호박색 알갱이 | 6×6 | 가장 작은 따뜻한 색의 픽업 | [Aseprite](04-amber-grain.aseprite) / [PNG](04-amber-grain.png) |
| 5 | 마력 입자 묶음 | 11×9 | 작은 보라색 조각 세 개. 더 큰 경험치 묶음의 시각 후보 | [Aseprite](05-arcane-cluster.aseprite) / [PNG](05-arcane-cluster.png) |

실제 경험치 값이나 등급은 설정하지 않았다. 1번을 범용 보석 방향, 2번을 작은 에너지 입자 방향의 우선 후보로 제안한다.

- [정지 비교판](xp-new-directions-preview.png) · [움직이는 비교판 GIF](xp-new-directions-preview.gif) · [비교판 Aseprite](xp-new-directions.aseprite).
- [전체 ZIP](xp-researched-five-samples.zip): 5개 Aseprite, 투명 PNG, 12배 확대본, 가로 idle 시트, 비교판, 제작 Lua, 이 기록.
- 전부 16×16 투명 캔버스. 각 디자인은 4색, 2레이어, 8프레임이며 130ms/프레임으로 총 1.04초에 상하 1픽셀씩 움직인다. 색상이 깜빡이는 애니메이션은 아니다.
- `*-idle-sheet.png`는 16×16 셀 8개가 왼쪽부터 이어지는 128×16 시트다. `*.png`는 첫 프레임이다.
- 비교판 아래 줄은 각 그림을 원본 픽셀 격자로 배치한 어두운/돌색 바닥이다. 비교판 전체는 3배 확대되어 있으며 Unity 화면 캡처가 아니다.

## 변경 및 확인

- [author-samples.lua](author-samples.lua): Aseprite 1.3.18.6에서 실행한 새 도트/애니메이션 제작 자료. 이전 5종의 픽셀맵을 재사용하지 않았다.
- 이 폴더의 Aseprite/PNG/GIF/ZIP: 새로운 5종 원본과 전달 자료. 상위 README: 최신안 경로. [SessionLog](../../../Docs/SessionLogs/2026-11-02.md): 조사·제작 결과와 확인 범위.
- 정지 비교판을 시각 검토했고, 5개 native Aseprite를 다시 열어 8프레임 PNG 시트로 내보냈다. 모두 exit 0, 128×16, 불투명 4색, 반투명 픽셀 0개이다.
- 재출력한 시트 40프레임과 정지 PNG를 기준으로 한 의도된 위치 이동을 대조했다. 보이는 색상/알파 차이 0픽셀이다. PNG 파일 인코딩은 직접 작성본과 CLI 내보내기에서 달라질 수 있어 바이트 해시 동등성을 주장하지 않는다.
- GIF를 native Aseprite로 출력했고 1380×792/8프레임임을 확인했다. README 링크 경로를 확인했다.
- 게임 파일 수정, Unity import·컴파일·Play Mode·자동 테스트는 실행하지 않았다. 선택 후 실제 게임 크기, 다른 드롭과의 구별, 게임 내 idle 적용 여부를 정하는 작업이 남는다.
- Doc Impact: **SessionLog**. ErrorLog·DecisionLog·StructureMemory 변경 없음. Architecture/Contracts 수정과 Presentation HTML stale 후보 없음.
