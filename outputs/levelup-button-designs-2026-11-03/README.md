# 레벨업 카드 하단 버튼 시안 5종

## 현재 선택 — A 금테 + 패널 없는 Bold 제목

2026-11-03 사용자 선택으로 하단 버튼은 **A 고전 금테**로 확정했다. 상단 `Level UP`은 검은 패널을 없애고 흰색 Bold/짙은 외곽선으로 수정했다. 현재 시안은 [revision 2](revision-2-selected-a-title/README.md)의 [전체 화면](revision-2-selected-a-title/A-selected-screen.png), [Aseprite](revision-2-selected-a-title/A-selected-screen.aseprite), [다운로드](revision-2-selected-a-title/selected-a-title.zip)를 사용한다. 후속 승인으로 [실제 Unity 프리팹에 적용](../levelup-ui-integration-2026-11-03/README.md)했다. 아래 5안 비교와 ZIP 및 검증 기록은 최초 시안 제작 단계의 기록이다.

Mode: Implementation — 시안 제작만. Aseprite 1.3.18.6의 Lua API로 픽셀을 그리고 네이티브 원본과 PNG를 내보냈다.

## 바로 보기

- [버튼 확대 비교판](01-button-comparison.png)
- [기존 구성 + 전체 화면 목업 비교판](02-screen-comparison.png)
- [Aseprite·PNG 묶음](levelup-button-designs.zip)

| 시안 | 의도 | 1920×1080 적용 목업 | 버튼 Aseprite |
|---|---|---|---|
| A — 고전 금테 | 어두운 바탕, 절제된 금색 테두리. 카드에 시선을 남기는 방향 | [화면](A-gilt-screen.png) | [원본](A-gilt-buttons.aseprite) |
| B — 양피지 | 따뜻한 종이색과 갈색 음영. 어두운 배경에서 높은 시인성 | [화면](B-parchment-screen.png) | [원본](B-parchment-buttons.aseprite) |
| C — 철제 | 강한 명암과 작은 리벳. 묵직한 조작감 | [화면](C-iron-screen.png) | [원본](C-iron-buttons.aseprite) |
| D — 마력석 | 보라색 바탕과 리롤의 청록색 테두리 | [화면](D-arcane-screen.png) | [원본](D-arcane-buttons.aseprite) |
| E — 카드 탭 | 카드 설명란과 이어지는 종이색, 붉은 책갈피 모양 강조 | [화면](E-card-tab-screen.png) | [원본](E-card-tab-buttons.aseprite) |

## 범위와 성공 기준

- 현재 프리팹의 닫기/리롤 기능, 문구, 크기와 위치를 기준으로 5개 버튼 세트를 제작한다.
- 일반/포커스/눌림/비활성 시안을 함께 제공한다. 동작 코드나 새 인터랙션은 추가하지 않는다.
- `outputs/levelup-button-designs-2026-11-03/` 산출물과 세션 기록만 변경한다.
- 게임 Assets, 씬, 프리팹, `.meta`, GUID, C# 및 설정은 변경하지 않는다.

## 원본 구조

- `*-buttons.aseprite`: 345×40 픽셀, RGBA, 4프레임. `normal`, `focus`, `pressed`, `disabled` 태그는 **상태 견본**이며 재생용 애니메이션이 아니다.
- 각 원본의 레이어: 바탕/입체 테두리, 장식·키캡·횟수 배지, Galmuri9 글자.
- 왼쪽 닫기 영역 `(0,0,160,40)`, 오른쪽 리롤 영역 `(175,0,170,40)`. 적용 목업은 각각 2배, 320×80 / 340×80이다.
- `*-screen.aseprite`: 1920×1080, 배경·기존 카드 아트·카드 글자·버튼 3개 레이어를 분리했다.
- 비교판도 `.aseprite` 원본을 제공한다. 확대판은 원본 픽셀의 3배이다.
- 글자는 프로젝트 Galmuri9를 래스터화한 픽셀 레이어다. Unity 적용 시에는 현행 TMP로 키/문구/남은 횟수를 표시해야 하며 이미지에 구워 넣지 않는다.
- `render-typography.py`, `typography/`, `author-buttons.lua`: 글자 준비와 Aseprite 원본 제작을 재현하기 위한 아트 소스다. 테스트 도구가 아니다.

## 화면 목업의 근거와 한계

**실제 플레이 캡처가 아니라 프리팹 배치에 근거한 재구성이다.**

- 기준: [GlobalUIRoot.prefab](../../Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab)의 `RewardCanvas/LevelRewardSelectionRoot`.
- 화면 1920×1080, 카드 472×740의 3열, 닫기 중심 `(-180,80)`, 리롤 중심 `(180,80)`, 암전 alpha 0.78을 반영했다.
- 카드 그림/프레임: [LevelRewardCards](../../Assets/_Project/Art/Sprites/UI/LevelRewardCards/)의 실제 PNG와 `.meta`의 sprite rect를 사용했다.
- 배경: [기존 한국어 허브 캡처](../localization-screen-review-2026-10-04/ko-hub.png). 현재 실행 중인 게임의 캡처는 아니다.
- 보상 문구: 현재 데이터의 강철의 수련, 사냥의 흐름, 벌컥벌컥을 사용했다. 줄바꿈/긴 줄 축소/색 강조는 읽기 위한 목업 처리이며 TMP의 최종 렌더와 차이가 날 수 있다.
- 버튼은 동일한 위치/크기로만 비교했다. 비활성 목업의 `0/5`는 예시이며 실제 비활성 조건 전체를 뜻하지 않는다.

## 확인

- Aseprite에서 네이티브 원본·PNG를 내보내고 버튼 확대판과 전체 화면 6종 비교판을 시각 검토했다.
- Aseprite 헤더에서 버튼 5개가 모두 345×40, 4프레임, RGBA이고 화면 원본 5개가 1920×1080임을 확인했다.
- 참조한 프리팹/C# 2개/카드 아트 및 meta 등 33개 입력은 제작 전후 SHA256이 동일했다. [제작 전 해시](source-hashes-before.json).
- 자동 테스트, Unity import/컴파일, Play Mode, Player build는 실행하지 않았다. 이후 선택한 시안의 TMP 가독성, 다른 언어, 키보드 포커스/비활성 표현은 실제 게임에서 확인할 부분이다.

Doc Impact Check: **SessionLog**. [세션 기록](../../Docs/SessionLogs/2026-11-03.md) 갱신. ErrorLog/DecisionLog/StructureMemory/Architecture/Contracts/Presentation HTML은 변경하지 않았다. 이 시안 작업에서 새 Presentation HTML stale candidate는 없다.
