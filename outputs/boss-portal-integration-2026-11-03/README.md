# 보스별 포탈 색상 게임 적용

2026-11-03 · Implementation · 사용자 요청: “게임에 적용해줘”.

클로에 C 회보라와 승인된 스피리 호박빛·멜타 민트빛·마왕 자수정빛을 게임 포탈에 연결했다. Aseprite 견본의 PNG를 그대로 임포트하고 기존 `PortalParticle`의 두 시작색을 함께 설정했다.

## 범위와 완료 조건

- Grand Hall의 보스별 포탈 4개, 일반 보스 처치 후 출구 3개, 고정 마왕 통로의 보스 입구 1개에 적용한다.
- 절차 생성되는 일반 보스 입구 3종은 기존 travel prefab을 상속한 테마별 variant로 연결한다.
- 석재, 알파, 크기·피벗, 머티리얼, 파티클 움직임, 충돌체, 이동 연결과 잠금/클리어 규칙을 유지한다.
- 기존 설치 도구를 다시 실행해도 테마별 보스 입구 프리팹을 사용하게 한다.
- Unity 네이티브 임포트/컴파일, C# 빌드, 참조·변경 전후 대조로 확인한다. 자동 테스트와 Play Mode는 요청 범위에 없다.

| 보스 / 테마 | 막 | 빛 테두리 | 파티클 min / max |
| --- | --- | --- | --- |
| 클로에 / Shadow — C | #79749B | #B7ACC9 | #998CB6 / #DAD0E8 |
| 스피리 / Dragon | #D99A3F | #FFCA68 | #FFAF4D / #FFE6A8 |
| 멜타 / Slime | #38C9A3 | #6CF3D0 | #43DBB1 / #B9FFE8 |
| 마왕 / DemonKing | #9C55DC | #C47CFF | #B66AFF / #EBC2FF |

막 알파는 129/255, 빛 테두리는 255/255다. 파티클 시작색은 TwoColors 모드, 두 색 모두 알파 1이며 기존 수명에 따른 투명도 설정은 유지한다.

## 변경 파일과 이유

| 파일 또는 폴더 | 변경 이유 |
| --- | --- |
| [BossPortals](../../Assets/_Project/Art/Sprites/Map/Environment/Level/BossPortals/) | `Portal_Shadow/Dragon/Slime/DemonKing.png` 4개와 Unity 생성 meta. `Source~`에 편집 가능한 동일 Aseprite 4개와 팔레트 안내를 보관한다. |
| [ProceduralSceneTravelPortal_Shadow.prefab](../../Assets/_Project/Prefabs/Map/Procedural/ProceduralSceneTravelPortal_Shadow.prefab), [Dragon](../../Assets/_Project/Prefabs/Map/Procedural/ProceduralSceneTravelPortal_Dragon.prefab), [Slime](../../Assets/_Project/Prefabs/Map/Procedural/ProceduralSceneTravelPortal_Slime.prefab) 및 meta | 공용 `ProceduralSceneTravelPortal.prefab`을 상속해 보스별 스프라이트와 파티클 색을 저작한다. |
| [Shadow_Boss.asset](../../Assets/_Project/Data/Dungeon/Rooms/BossThemes/Shadow/Shadow_Boss.asset), [Dragon_Boss.asset](../../Assets/_Project/Data/Dungeon/Rooms/BossThemes/Dragon/Dragon_Boss.asset), [Slime_Boss.asset](../../Assets/_Project/Data/Dungeon/Rooms/BossThemes/Slime/Slime_Boss.asset) | 각 `BossGate`의 `mediumPrefab` 참조 한 곳을 대응 variant로 바꾼다. |
| [Grand Hall.unity](../../Assets/_Project/Scenes/Grand%20Hall.unity) | `Portal_To_ShadowCorridor/DragonCorridor/SlimeCorridor/DemonkingCorridor` 4개의 sprite/particle RGB override. |
| [HeoMinSeok_Boss_Shadow.unity](../../Assets/_Project/Scenes/HeoMinSeok_Boss_Shadow.unity), [HeoMinSeok_Boss_Dragon.unity](../../Assets/_Project/Scenes/HeoMinSeok_Boss_Dragon.unity), [SangHyup_Boss_SlimeQueen.unity](../../Assets/_Project/Scenes/SangHyup_Boss_SlimeQueen.unity) | 각 기존 출구 `ScenePortal`의 sprite/particle RGB override. |
| [DemonkingCorridor.unity](../../Assets/_Project/Scenes/DemonkingCorridor.unity) | 고정 마왕 통로의 기존 `ScenePortal` 입구에 마왕 팔레트 override. |
| [ProceduralCorridorTravelInstaller.cs](../../Assets/_Project/Editor/Tools/Dungeon/ProceduralCorridorTravelInstaller.cs) | 기존 설치 흐름에서 테마별 variant를 불러와 BossGate 설정에 전달한다. 누락되면 에셋 경로와 함께 오류를 낸다. |
| 기존 StructureMemory 2개, SessionLog, 견본 README 2개 | 현재 게임 연결·소유권·선택 상태와 검증 결과를 기록한다. |

일반 포탈 원본 2종과 원본 PNG/disabled PNG는 그대로다. `GrandHallClearedPortalView`가 활성 스프라이트를 캡처하므로 기존 잠금/클리어 때 공유 비활성 스프라이트를 사용하고 복구 때 해당 보스 색상으로 돌아간다. 새 런타임 컴포넌트, serialized field, enum, asmdef 또는 Manager는 추가하지 않았다.

## 검증

- Unity 6000.4.2f1에서 네이티브 authoring 성공: [연결 결과](native-authoring.txt), [제작 로그](unity-authoring.log). 4 sprite, 3 variant, 3 room binding, 8 scene portal을 읽고 색상과 크기·피벗을 확인했다.
- 임시 Editor 제작 파일을 제거한 뒤 최종 batch 임포트/스크립트 컴파일 **exit 0**: [최종 로그](unity-final-import.log), [종료 코드](unity-final-exit-code.txt). Tundra build success를 확인했다.
- 변경된 설치 도구를 포함한 `Editor.csproj` MSBuild **exit 0**: [빌드 로그](msbuild-editor.log), [종료 코드](msbuild-editor-exit-code.txt). 기존 API 사용 중단/미할당 필드 등의 경고는 남아 있다.
- [변경 전후 대조](source-inspection.txt): 승인 견본과 게임 PNG/Aseprite 4쌍의 SHA256이 모두 같다. 원본/공용 프리팹/포탈 로직/빌드 설정 27파일은 작업 전과 동일하다. 기존 수정 대상 meta도 그대로다.
- 씬 5개는 sprite와 `InitialModule.startColor` RGB override만 추가했다. 방 3개는 prefab 참조 한 줄씩만 바뀐다. Unity 저장 중 생긴 범위 밖 폰트/기본 필드 재직렬화는 작업 전 값으로 되돌렸다. 개별 `.diff`는 HEAD가 아닌 작업 직전 사본과 비교한 기록이다.
- [참조 검사](reference-inspection.txt): 새 GUID 8개가 유일하며 variant의 상속과 room 연결이 일치한다. 원본 50×49, PPU 16, pivot (0.5, 0.15), Point, 압축·mipmap 없음 설정을 유지했다.
- C#은 Editor assembly의 기존 설치 함수만 수정했다. namespace/assembly reference/lifecycle/직렬화 계약 변경은 없다. Markdown 상대 경로와 변경 범위 공백도 확인했다.

자동 테스트, Play Mode, 수동 플레이, Player build는 실행하지 않았다. 실제 씬 조명·정렬에서의 색감과 생성/잠금/클리어/출구 등장 전환은 수동 확인이 남아 있다. [견본 비교판](../boss-portal-color-samples-2026-11-03/boss-portal-comparison-2x.png)은 색상 참고용이며 게임 플레이 캡처가 아니다.

## 보관과 문서 영향

`before/`는 작업 직전 대상 파일과 보호 파일 해시다. [BossPortalPaletteImport.cs](BossPortalPaletteImport.cs)는 제작 재현용 소스를 `Assets` 밖에 보관한 것이며 게임 코드/테스트 도구로 남기지 않았다. 네이티브 씬 재저장은 다른 기존 컴포넌트의 직렬화를 동반할 수 있으므로 재사용 시 변경 전후 범위를 검토해야 한다.

Doc Impact Check: **SessionLog + StructureMemory**. [SessionLog](../../Docs/SessionLogs/2026-11-03.md), [Grand Hall 포탈 구조](../../Docs/StructureMemory/GrandHallOfficerQuestAndPortals.md#boss-palette-authoring-2026-11-03), [절차 던전 구조](../../Docs/StructureMemory/ProceduralDungeonRoomPipeline.md)를 갱신했다. 이 작업에서 ErrorLog/DecisionLog/RefactorBacklog/Architecture/Contracts는 변경하지 않았다. 이 색상 적용으로 새 Presentation HTML stale 후보는 없다.
