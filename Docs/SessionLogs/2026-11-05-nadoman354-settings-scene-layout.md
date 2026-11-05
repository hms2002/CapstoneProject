# 모든 씬의 설정 화면 배치 복구

허브 설정 화면에서 ‘키 매핑 설정’ 버튼이 하단 구분선을 침범하고 ‘설정’ 제목이 명패 아래로 내려갔다. 11월 4일 병합 `45d4b28a`에서 두 요소의 옛 배치 속성 10개가 다시 들어왔다. 빌드 제외 씬 중에는 이전 레이아웃에서 저장한 위치 때문에 표시·소리·환경 항목까지 겹치는 경우가 있었다.

## 변경한 씬

설정 페이지에서 프리팹과 배치가 달랐던 요소의 앵커·위치·크기 오버라이드를 제거했다. 허브에서는 제목과 키 매핑 버튼의 슬롯 두 곳을 복구했다. 나머지 씬에서는 각 씬에 남아 있던 항목, 라벨, 버튼 또는 하단 버튼 그룹의 옛 배치를 복구했다. 총 18개 씬이 해당한다. 배치 원본은 [GlobalUIRoot.prefab](../../Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab)이다.

| 위치 | 변경한 씬 |
| --- | --- |
| [프로젝트 씬](../../Assets/_Project/Scenes) | ProtoTypeHub, DragonCorridor, PixelLightTest, ProceduralDemonkingCorridor, ProceduralDungeonV0Test, ProtoTypeCorridor, SangHyup_Hallway, ShadowCorridor, SlimeCorridor, TutorialCorridor |
| [테스트 씬](../../Assets/_Project/Scenes/Tests) | TestScene |
| [보관 씬](../../Assets/_Project/Unused/Scenes) | DragonCorridor 1, LEeJunmo 1, ProtoTypeBossLagacy, ProtoTypeHubasf, SlimeCorridor 1 |
| [복구용 씬](../../Assets/_Recovery) | 0 (1), 0 (2) |

## 확인

- `Assets` 안의 씬 47개를 조사했다. 38개는 공통 프리팹을 인스턴스화하고, [타이틀 씬](../../Assets/_Project/Scenes/TitleScene.unity)은 설정 화면을 직접 보유한다. 나머지 8개에는 설정 화면이 없다.
- 수정 후 38개 인스턴스에서 설정 패널의 RectTransform 151곳을 원본과 대조하고 타이틀 씬의 같은 경로와도 비교했다. 비활성 UI 크기 항목·스크롤바 및 슬라이더가 값에 따라 움직이는 채움·핸들은 이 비교에서 제외했다.
- 허브의 버튼 하단은 구분선을 47 캔버스 단위 침범하던 위치에서 구분선 위로 32만큼 떨어졌다. 제목 중심과 명패 중심 사이의 세로 오차는 38에서 0이 됐다. 이 값은 씬에 저장된 좌표로 계산했다.
- 수정 직전 사본과 대조해 18개 씬 모두 해당 RectTransform 속성 2,419개를 제외한 나머지 바이트가 일치함을 확인했다. 이 중 원본과 값이 달랐던 속성은 995개이고, 나머지는 같은 요소에서 기존 수치를 고정하던 오버라이드였다.
- Unity 실행, 플레이 테스트, Player 빌드는 수행하지 않았다. 수동 확인은 허브에서 스크린샷과 같은 1360×768 창모드로 설정을 열고 제목과 하단 여백을 확인하면 된다.

씬 병합 후 같은 문제를 찾는 방법은 [ErrorLog](../ErrorLog.md)에, 설정 화면의 현재 배치 범위는 [SettingsPanelLayout](../StructureMemory/SettingsPanelLayout.md)에 기록했다.
