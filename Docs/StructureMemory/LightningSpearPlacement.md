---
status: active
authority: structure-memory
category: weapon-placement
last_reviewed: 2026-10-10
---

# Lightning Spear Placement

번개창 생성 위치 계획의 현재 책임과 수명 경계를 빠르게 복원하기 위한 구조 지도다. Architecture/Contracts를 대신하지 않는다. 현재 조합 작업은 [ActiveTask](../ActiveTasks/lightning-spear-tool-composition.md), 공통 기능은 [PlacementTools](PlacementTools.md)를 따른다. 이전 무기 내부 분리 결과는 [완료 작업](../ActiveTasks/lightning-spear-placement-structure.md)에 있다.

## 구조와 소유권

| 소유자 | 책임과 상태 | 주요 진입점 |
| --- | --- | --- |
| [LightningSpearRuntimeState](../../Assets/_Project/Runtime/Features/Items/Weapons/Abilities/LightningSpearRuntimeState.cs) | live 창 목록, 입력·선택 피드백, Q/E 코루틴, GAS·생성·사운드·cleanup. 종료된 창 목록 정리와 실제 actor 생성은 이곳에 남는다. | `ExecuteSkill2`, `SpawnThirdStrikeMark`, `OnDisable`, `OnDestroy` |
| [LightningSpearMarkPlacementPlanner](../../Assets/_Project/Runtime/Features/Items/Weapons/Abilities/LightningSpearMarkPlacementPlanner.cs) | loadout/data를 공통 수치 설정으로 변환하고 대상 포함·live 창 좌표를 연결한다. FirstSuccess/FillWithinBudget/RoundRobinFill을 조합하며 actor 생성은 하지 않는다. | `GenerateRain`, `TryPlanThirdStrike`, `CreateSettings`, `TryResolveTarget` |
| [LightningSpearPlacementWorldQuery](../../Assets/_Project/Runtime/Features/Items/Weapons/Abilities/LightningSpearPlacementWorldQuery.cs) | Ground fallback/Wall 이름·레이어 입력, 착지 수치와 기존 돌진 API를 공통 MapQuery2D/PhysicsQuery2D에 연결하는 호환 경계. | `Maps`, `Physics`, `IsLandingValid`, `HasBlocker` |
| [공통 도구](PlacementTools.md) | CandidateSources의 후보/대상 버퍼, NearestPositionSearch의 heap/소진/진행, PlacementSession의 검증/기하 캐시/예약/결과, MapQuery2D/PhysicsQuery2D의 조회 버퍼. 콘텐츠 타입·유물 ID를 모른다. | `PlacementFlow.FirstSuccess`, `FillWithinBudget`, `RoundRobinFill` |

협력 객체는 기존 Gameplay assembly 안의 내부 일반 C# 객체다. 새 MonoBehaviour, singleton, manager, serialized field 또는 씬 연결을 추가하지 않는다. RuntimeState마다 WorldQuery 하나와 필요할 때 만드는 Planner/도구 묶음을 두며, Q도 같은 조회 도구를 사용한다. 공통 코드 재사용과 전역 mutable 버퍼 공유를 구별한다.

## 요청과 실행 수명

- Rain은 요청 시작 시 pending 예약과 Search의 요청 캐시를 초기화한다. 대상의 정확한 위치를 먼저 검사하고, 필요할 때 가장 가까운 타일 후보를 찾은 다음 기존 무작위 fallback을 수행한다.
- `GenerateRain`의 반환 List는 **다음 요청까지 빌려 쓰는 결과**다. `EndRequest`는 결과를 남겨 반환하되 Search의 요청 상태, 대상 참조와 WorldQuery의 물리 결과를 정리한다. 다음 `BeginRequest`가 pending을 비운다.
- E는 반환 결과를 새 List로 **첫 생성과 yield 전에 복사**한다. 표적 비의 창 사이 지연 중 ThirdStrike가 같은 Planner를 재사용해도 E의 생성 순서·위치·방 참조는 그 복사본에 남는다.
- ThirdStrike는 공통 `PlacementPoint` 값 하나를 반환하고 그 요청의 borrowed 결과를 비운다. FirstSuccess는 일반적으로 Commit하지만 단일 값 연결부가 결과 수명을 종료하여 기존 scratch 예약0을 보존한다. RuntimeState가 실제 창을 생성·등록한다.
- live 창의 소모·수명 만료·무기 교체·disable/destroy cleanup은 기존 RuntimeState와 MarkActor가 담당한다. 값 타입 `LightningSpearMarkPositions`가 owner List의 현재 좌표를 읽어 공통 generic Session에 제공한다. Actor snapshot·중복 registry·후보별 reader boxing은 도입하지 않는다.
- WorldQuery의 `ClearPlacementResults`는 대상/배치 cast 결과만 비운다. 맵 캐시는 기존처럼 프레임 단위로 유지한다. heap pool과 List 용량도 기존처럼 보존되므로 이전 큰 요청의 메모리가 남을 수 있다.

## 유지한 동작과 알려진 경계

- 정확한 적 위치 우선, 거리→X→Y 동률 순서, round-robin 배치, 간격 예약, 표본 시도 한도와 Unity RNG 소비를 유지한다.
- 기하 캐시는 동기식 요청 동안 고정된 월드 정보만 재사용한다. live/pending 창 간격은 매번 검사한다. 요청을 프레임에 걸쳐 이어 실행하는 구조로 바꾸면 이 전제를 다시 검토해야 한다.
- heap은 가까운 후보부터 검사하지만 최초 후보 수집은 반경 안 타일 전체를 순회한다. 이번 분리는 새로운 탐색 알고리즘이나 무할당 보장을 도입하지 않는다.
- `Wall` 레이어 또는 정확한 Tilemap 이름 `Wall` 판정, Ground fallback layer 7, 기존 트리거·초기 겹침 허용 규칙을 유지한다. 이름 의존성은 [Tilemap Classification backlog](../RefactorBacklog/LightningSpearTilemapClassification.md)에 남아 있다.
- 배치의 `StrictRushBlockMask`, 방 안 Q의 `HardBlockMask`, 방 밖 Q의 `StrictRushBlockMask`를 합치지 않는다. 돌진 `HasBlocker`의 기존 `CircleCastAll`도 유지한다. 가벽 통과·Hole 안전 정책 변경은 별도 작업이다.
- 콘텐츠 설정·대상 포함 변경은 Planner 연결부, actor 실행 변경은 RuntimeState, 후보·흐름·검증·조회 기능 변경은 해당 공통 도구에서 시작한다. 기존 이름 판정 정책은 WorldQuery의 입력 연결에 남는다.

## Editor 검증 경로

- [LightningSpearPlacementBeforeTools](../../Assets/_Project/Runtime/Features/Items/Weapons/Abilities/LightningSpearPlacementBeforeTools.cs)는 공통 도구화 직전 Planner/WorldQuery의 독립 Editor snapshot이다. `-lightningCompareStructure`는 이 기준과 새 조합을 비교한다. 이전 BeforeRefactor와 전체 정렬 legacy도 과거 비교 자료로 유지한다.
- [LightningSpearPlacementSearch](../../Assets/_Project/Runtime/Features/Items/Weapons/Abilities/LightningSpearPlacementSearch.cs)는 frozen 측정 경로만 사용하는 Editor 전용 원본이다. production은 공통 NearestPositionSearch를 사용한다.
- [RuntimeState.PlacementBenchmark](../../Assets/_Project/Runtime/Features/Items/Weapons/Abilities/LightningSpearRuntimeState.PlacementBenchmark.cs)의 legacy 경로는 이전 전체 정렬 방식과 원래 판정 순서의 비교·결과 검증용이다. 구조 분리 직전 비교 기준과 구별한다. 진단 카운터와 두 측정 경로는 `UNITY_EDITOR`로 Player build에서 제외된다.
- [LightningSpearPlacementBenchmark](../../Assets/_Project/Editor/Tools/Validation/LightningSpearPlacementBenchmark.cs)의 seeded fixture와 [TestScene](TestScene.md)을 기존 검증 진입점으로 사용한다. 위치/개수/순서/방/RNG와 작업 카운터를 확인하고 시간은 cold/warm을 구별한다. 생성 actor·렌더링·FPS는 동기식 계획 시간의 측정 범위에 포함되지 않는다.
- [PlacementStructurePlayModeTests](../../Assets/_Project/Tests/PlayMode/Procedural/LightningSpearPlacementStructurePlayModeTests.cs)에 계획 단계의 미생성·소유권, 독립 Planner 격리, 실패 후 재시도 정리, 실제 E 지연 중 ThirdStrike의 scratch 재사용과 복사 경계, E 취소 후 추가 생성 중단과 disable의 기존 actor 정리를 검사하는 테스트가 추가되어 있다.

구조 분리 후 seeded A/B와 새 PlayMode 테스트가 실행됐다. 반복 496쌍과 새 버퍼의 첫 요청 312쌍의 결과·RNG·작업량 일치, 최종 구조 검사 5/5와 기존 관련 검사 5개 통과를 확인했다. 별도 기존 표식 색상 정확 동등 단언 1개는 실패했으며 전체 확장 suite가 통과한 것으로 취급하지 않는다. 시간·할당·실패 검사와 실행 조건의 세부 근거는 [검증 보고서](../SessionLogs/2026-10-10-lightning-structure/README.md)에 있다. Editor CPU/DX11 계획 시간과 Player/FPS, byte allocation 결과는 구별한다.

## 문서 승격 여부

현재는 번개창 내부 구조 지도다. 여러 기능이 같은 위치 계획/월드 조회 경계를 공유하게 될 때 Architecture/Contracts 승격 후보를 다시 검토한다. 이번 작업에서는 source-of-truth 문서와 Presentation HTML을 바꾸지 않는다.
