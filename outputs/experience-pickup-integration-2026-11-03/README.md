# 경험치 구슬 게임 적용 — 2026-11-03

사용자가 선택한 2번 빛 구슬의 [제자리 색상 순환안](../experience-pickup-samples-2026-11-02/revision-6-stationary-color-cycle/README.md)을 기존 공용 경험치 픽업에 적용했다. 40프레임, 10 FPS, 4초 무한 반복이며 색만 바뀐다.

## 범위와 결과

- Mode: Implementation. 현재 요청 “게임에 적용해줘”를 범위로 삼았다. 일치하는 ActiveTask는 없으며 기존 픽업 프리팹과 필요한 아트/애니메이션 자산 연결을 수행했다.
- 성공 기준: 승인된 도트·팔레트 그대로 사용, 상하 이동 없는 색상 순환, 기존 표시 크기·추적·경험치 지급·획득 파티클 유지.
- 표시 크기는 기존 값 유지로 판단했다. 16×16 셀마다 중앙 8×8을 8 PPU로 잘라 root scale 0.18에서 기존과 같은 0.18×0.18 world bounds를 유지한다. 기존 초록 tint는 원본 색을 보존하도록 흰색으로 바꿨다.
- 실제 씬의 색감은 Play Mode 수동 확인 대상으로 남겼다. 기존 Sprite-Lit-Default 재질과 정렬 설정을 유지했다.

## 변경 파일과 이유

| 파일 | 변경 이유 |
| --- | --- |
| [ExperiencePickup_Square.prefab](../../Assets/_Project/Prefabs/Loot/ExperiencePickup_Square.prefab) | 스프라이트 교체, 흰색 tint, prefab-owned Animator 연결. 기존 이름과 GUID를 유지해 드롭 참조를 보존한다. |
| [ExperiencePickupColorCycle.png](../../Assets/_Project/Art/Sprites/Items/ExperiencePickup/ExperiencePickupColorCycle.png) 및 `.meta` | 승인 시트 원본 그대로, 40개 중앙 정렬 스프라이트, Point/압축 없음/mipmap 없음으로 가져온다. |
| [ExperiencePickupColorCycle.anim](../../Assets/_Project/Art/Animations/VFX/ExperiencePickup/ExperiencePickupColorCycle.anim), [controller](../../Assets/_Project/Art/Animations/VFX/ExperiencePickup/ExperiencePickupColorCycle.controller) 및 `.meta` | 단일 기본 상태로 `SpriteRenderer.m_Sprite`만 4초 반복한다. Transform 곡선, 이벤트, 파라미터, root motion은 없다. |
| [Source~](../../Assets/_Project/Art/Sprites/Items/ExperiencePickup/Source~/README.md) | 편집 가능한 Aseprite, 원본 Lua, export JSON과 편집 안내를 보관한다. Unity 가져오기 대상에서는 제외된다. |
| 신규 아트·애니메이션 폴더 `.meta` | Unity가 생성한 안정적인 자산 식별자. |
| [StructureMemory](../../Docs/StructureMemory/ScriptSystems/LevelProgressionStructure.md), [SessionLog](../../Docs/SessionLogs/2026-11-03.md), 견본 README 2개 | 현재 적용 상태, 자산 위치, 표현/게임플레이 책임과 검증 범위를 남긴다. |

Animator는 scaled time을 사용하고 화면 밖에서는 평가를 중지한다. 기존 `ExperiencePickup2D`가 계속 추적·경험치 지급·획득 파티클·루트 삭제를 담당한다. 신규 런타임 C#이나 매니저는 없다. Unity native API로 제작한 뒤 임시 Editor 스크립트는 제거했다.

## 검증

- Unity **6000.4.2f1** native authoring 종료 코드 **0**. [최종 제작 보고서](native-authoring.json): sprite 40, length 4.0, loop true, object-reference curve 1, float curve 0, event 0. [제작 로그](unity-authoring-fixed.log).
- 임시 제작 스크립트를 제거한 상태에서 Unity batch import/스크립트 컴파일 **종료 코드 0**. [최종 로그](unity-final-import.log)에 `Tundra build success`와 정상 batch 종료 기록이 있다. Player build는 실행하지 않았다. 이후 변경은 프리팹 빈 값 뒤 공백 정리와 문서뿐이다.
- 승인 PNG와 적용 PNG의 SHA-256 일치. 프리팹 `.meta`와 기존 Leveling 폴더 최상위 C# 5개 파일 해시가 작업 전과 동일하다.
- 프리팹 diff에서 Transform, BoxCollider2D, ExperiencePickup2D 설정 유지 확인. Animator 추가와 SpriteRenderer의 sprite/tint만 기능 변경이다.
- 소스/참조 검사: sprite key 40개(0.0~3.9초), 단일 상태와 파라미터 없음, 40개 sprite rect·중앙 pivot, clip/controller/prefab 참조 일치. 애니메이션에 위치·스케일 곡선이 없다.
- 자동 테스트, 자동 Play Mode 검사와 수동 플레이는 **미실행**. 실제 씬 조명·화면 크기에서 시인성 확인이 남아 있다.

기존 작업 내용은 작업 전 diff와 비교해 보존했다. 동시 진행 중인 별도 보스 포탈 견본 작업은 수정하지 않았다. `before/`에는 적용 전 프리팹·메타와 작업 상태 기록이 있다. `native-authoring-initial.json`과 최초 로그는 중간 제작 기록이며 최종 상태는 `native-authoring.json`을 따른다.

Doc Impact Check: **SessionLog + StructureMemory**. ErrorLog/DecisionLog는 수정하지 않았다. Architecture/Contracts 변경이나 Presentation HTML stale 후보는 없다.
