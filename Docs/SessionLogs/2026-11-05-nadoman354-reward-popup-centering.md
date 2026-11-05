# 호감도 보상·업그레이드 완료 팝업 중앙 배치

호감도 보상과 업그레이드 완료 창은 [GlobalUIRoot](../../Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab)의 `RewardCanvas/RewardPanel/RewardUI`를 공유한다. 원본 프리팹은 전체 화면 패널의 중앙에 창을 배치하지만, 빌드 대상 게임 씬들이 X 위치를 960으로 밀고 Y 앵커를 상단으로 고정했다.

## 변경

아래 [게임 씬](../../Assets/_Project/Scenes)에서 각각 `RewardUI`의 `m_AnchorMin.y`, `m_AnchorMax.y`, `m_AnchoredPosition.x`, `m_AnchoredPosition.y`, `m_SizeDelta.x` 오버라이드를 제거했다. 총 60개 속성이 프리팹 값을 상속한다.

- 허브·튜토리얼 — `ProtoTypeHub`, `PrototypeTutorialUpgradeScene`, `DarkLord_Tutorial`
- 회랑 — `ProceduralShadowCorridor`, `ProceduralDragonCorridor`, `ProceduralSlimeCorridor`, `DemonkingCorridor`
- 보스·홀 — `HeoMinSeok_Boss_Shadow`, `HeoMinSeok_Boss_Dragon`, `SangHyup_Boss_SlimeQueen`, `LeeJunmo_Boss_DemonKing`, `Grand Hall`

## 확인

- [빌드 씬 목록](../../ProjectSettings/EditorBuildSettings.asset)과 인스턴스 참조를 대조했다. 변경된 12개 씬은 전체 화면 부모 패널 아래에서 중앙 앵커·피벗 `(0.5, 0.5)`과 위치 `(0, 0)`을 상속한다. 1920×1080, 1280×800, 2560×1080 기준으로 창의 중심과 화면 중심이 일치하는지 직렬화 값으로 계산했다.
- 각 씬의 변경 전후를 비교해 해당 속성 5개만 제거한 것을 확인했다. 자식 콘텐츠, 폰트, 버튼 바인딩 등 다른 씬 데이터는 일치했다.
- 후속 촬영에서 수정본과 바이트가 일치하는 허브 씬을 별도 저장 경로의 Unity 6000.4.2f1 Play Mode로 열었다. 실제 보상 표시 서비스로 [유물 슬롯 확장 완료](../../outputs/reward-popup-centering-2026-11-05/upgrade.png)와 [사샤의 호감도 할인 보상](../../outputs/reward-popup-centering-2026-11-05/affection.png)을 각각 표시해 Game 뷰 전체를 촬영했다. 두 창 모두 2400×1359 화면에서 중심이 (1200.00, 679.63)이었다. 실제 구매·호감도 획득 경로, 자동 테스트, Player 빌드는 실행하지 않았다.
- `Assets/_Project/Scenes` 아래 빌드 제외 씬 8개에도 과거 위치값이 있다. 이 씬들을 직접 실행 용도로 사용할 경우 별도로 검토한다.

## 후속 변경 — 보상 팝업 2배 확대

- 두 보상 창에 한해 [GlobalUIRoot 프리팹](../../Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab)의 `RewardUI` 가로·세로 배율을 1에서 2로 올렸다. 중앙 피벗을 기준으로 창 테두리, 글자, 아이콘, 닫기 버튼이 함께 확대된다.
- 빌드 대상 게임 씬 12개에서 해당 RectTransform의 배율과 위치를 덮어쓰는 값이 없는지 확인했다. 프리팹 diff는 배율 한 줄이며, 열기·닫기 연출은 위치와 투명도를 조절한다.
- 현재 허브 씬과 프리팹을 복사한 격리 환경에서 Unity Game 뷰를 다시 촬영했다. 2400×1359에서 [호감도 보상](../../outputs/reward-popup-centering-2026-11-05/affection-2x.png)과 [업그레이드 완료](../../outputs/reward-popup-centering-2026-11-05/upgrade-2x.png) 모두 중심이 (1200.00, 679.63)이며 설명과 닫기 버튼이 화면에 들어온다. 보상 표시 서비스를 직접 호출한 한국어 단일 보상 예시다. 다중 보상·다른 언어의 확대 화면, 실제 획득 흐름, 자동 테스트와 Player 빌드는 이번에 확인하지 않았다.
