# 미믹 상자 — Aseprite 도트 에셋

2026-11-01. Mode: Implementation — 사용자 요청에 따른 에셋 제작만 수행.
현재 상자/UI의 픽셀 해상도와 단순한 명암을 유지한 편집 가능한 도트 원본이다.
Aseprite 1.3.18.6의 Lua 픽셀 드로잉으로 제작했다. 앞선 생성형 이미지의 축소/색상 양자화 결과가 아니다.

## 파일

| 파일 | 용도 |
| --- | --- |
| [MimicChest-16px.aseprite](MimicChest-16px.aseprite) | 16×16 월드 상자, 12프레임, 몸체/눈·입/이빨/혀 4레이어 |
| [MimicChestUI-80x122.aseprite](MimicChestUI-80x122.aseprite) | 80×122 UI, 원본 프레임/입/눈/이빨/혀 + 숨겨진 배치 예시 레이어 |
| [exports](exports/) | 투명 PNG, UI 분리 파츠, 네이티브 GIF |
| [MimicChest-comparison-3x.png](MimicChest-comparison-3x.png) | 원본 대비 확대 미리보기 |
| [MimicChest-comparison.aseprite](MimicChest-comparison.aseprite) | 비교판 편집 원본, 384×240 |
| [MimicChest-preview-8x.gif](MimicChest-preview-8x.gif) | 월드 애니메이션 정수 8배 확대 |
| [MimicChestUI-preview-6x.png](MimicChestUI-preview-6x.png) | UI 배치 예시 정수 6배 확대 |
| [asset-manifest.json](asset-manifest.json) | 프레임·소스 좌표·UI 파츠 크기·팔레트 정보 |
| [author-mimic.lua](author-mimic.lua) | 재제작 가능한 Aseprite 저작 스크립트 |

## 원본 기준

- 월드 원본: [All Chest Sheet.png](../../Assets/_Project/Art/Sprites/Map/Environment/Level/All%20Chest%20Sheet.png). 실제 TreasureChest가 쓰는 첫 행 8장의 16×16 프레임을 기반으로 했다. 닫힘 몸체는 원본 픽셀 그대로이며 열림 몸체도 원본 프레임을 별도 레이어로 보존한다.
- 월드 기준: 16 PPU, 기존 pivot `(0.5, 0.123)`. PNG/Aseprite가 Unity 임포트 설정을 자동 부여하는 것은 아니다.
- UI 원본: 실제 ChestUI가 참조하는 [examples.png](../../Assets/_Project/Art/Sprites/UI/Chest/examples.png)의 좌상단 기준 `(384,20,80,122)` 영역.
- `examples_0`~`examples_9`의 10개 파츠와 같은 크기/좌표를 유지한 `MimicFrame-0`~`MimicFrame-9`를 내보냈다. 눈·이빨·혀는 별도 80×122 투명 오버레이 PNG이므로 가로 늘리기에 의해 표정이 찌그러지지 않도록 나중에 별도로 배치할 수 있다.
- 색상은 위 두 원본과 [UI Chest inventory.png](../../Assets/_Project/Art/Sprites/UI/Chest/UI%20Chest%20inventory.png)의 기존 불투명 색만 사용했다. 월드 16색, UI 마스터 팔레트는 숨겨진 배치 예시를 포함해 21색이다.
- 기본 목재와 금속의 색/평면 명암을 유지했다. 신규 눈·이빨·혀도 1px 단위이며, 제작 아트의 반투명 픽셀과 원본 팔레트 밖 픽셀은 모두 0개다.

## 프레임과 UI 편집

- Aseprite 태그: `Closed` 1, `Awake` 2, `Open` 3~10, `Opened` 11~12. 프레임 번호는 Aseprite 화면의 1-based 기준이다.
- Open은 기존과 같은 8포즈/12fps 설계다. Aseprite의 정수 밀리초 저장값은 각 83ms다. 실제 Unity 연결 시 기존 12fps 타이밍을 사용하면 된다.
- 전체 시트: `exports/MimicChest-all-frames-192x16.png`. Open 전용 시트: `exports/MimicChest-open-128x16.png`.
- UI `06 PREVIEW ONLY` 레이어를 켜면 아이템 4칸·선택 2칸·확정 버튼의 간단한 배치 예시가 보인다. 슬롯/아이콘은 배치 검토용이며 게임 동작을 정의하지 않는다. 해당 레이어는 저장본에서 숨김이며 제작용 PNG에는 포함되지 않는다.
- `MimicChestUI-80x122.png`는 표현부까지 합친 그림, `MimicChestUI-frame.png`와 눈/이빨/혀 PNG는 분리 배치용이다. 기존 게임 UI의 텍스트·아이템 아이콘·선택 수는 계속 런타임 UI가 표시해야 한다.

## 확인 및 남은 범위

- Aseprite에서 두 마스터를 다시 읽고 PNG/시트를 내보냈다. 최초 PNG와 재출력 PNG의 RGBA 픽셀 차이는 월드/UI 모두 0개였다.
- 재출력 메타데이터에서 월드 16×16, 12프레임, 4레이어, 4태그와 저장 시간을 확인했다. 원본 대비 확대판 및 UI 6배 확대판을 육안 확인했다.
- 기존 상자 PNG/메타, ChestUI/월드 상자 프리팹은 수정하지 않았다. 출력은 `Assets/` 밖에만 저장했다.
- 자동 테스트, Unity import/컴파일, Play Mode, Player 빌드는 실행하지 않았다. 게임 내 실제 크기와 Reveal/재뽑기 연출은 아직 확인하지 않았다.
- 후속 결정: 디자인 수용 후 Unity 연결 범위를 정하고, UI 표정 오버레이를 authored 자식으로 배치하며 원본 레이아웃에 맞춰 배율/위치를 검토해야 한다. 이 파일들은 자동으로 연결된 교체본이 아니다.
- Doc Impact Check: SessionLog. StructureMemory/ErrorLog/DecisionLog 및 Architecture/Contracts 수정 불필요, Presentation HTML 영향 없음.

저작 API 참고: [Aseprite CLI](https://www.aseprite.org/docs/cli/), [Aseprite Image API](https://aseprite.org/api/image), [Aseprite Sprite API](https://aseprite.org/api/sprite).
