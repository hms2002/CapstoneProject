---
status: active
authority: structure-memory
category: gameplay-placement-tools
last_reviewed: 2026-10-10
---

# Placement Tools

동기식 생성 위치 계획에 쓰는 공통 도구의 API와 요청 수명을 복원하기 위한 구조 지도다. Architecture/Contracts를 대신하지 않는다. 현재 범위는 [도구 조합 작업](../ActiveTasks/lightning-spear-tool-composition.md), 번개창 연결은 [Lightning Spear Placement](LightningSpearPlacement.md)에서 추적한다.

공통 코드의 production 소비자는 현재 번개창 연결 하나다. 다른 기능의 마이그레이션은 이번 범위에 포함되지 않는다. 좌표 목록과 수치 설정만 사용하는 별도 테스트가 같은 도구의 조합을 검증하도록 작성되어 있다.

## 공통 도구와 API

모든 파일은 기존 Gameplay assembly 아래 `Assets/_Project/Runtime/Features/Common/Placement/`에 있다. 공통 파일에는 Lightning/Spear, Enemy, TrainingDummy 참조가 없다. 대상 포함 규칙과 콘텐츠별 이름·레이어 해석은 호출자가 공급한다.

| 도구 | 책임과 상태 | 주요 API |
| --- | --- | --- |
| [CandidateSources](../../Assets/_Project/Runtime/Features/Common/Placement/CandidateSources.cs) | 요청의 범위 안 Tilemap 중심 좌표 수집·중복 제거, 대상 Transform 중복 제거·거리 정렬. 대상 정규화는 `PlacementTargetResolver`에 위임한다. | `Begin`, `End`, 지연 수집 `GetTileCandidates`, `FindTargets`, 값 cursor 생성 `RandomDisk` / `FixedThenRandom` |
| [NearestPositionSearch](../../Assets/_Project/Runtime/Features/Common/Placement/NearestPositionSearch.cs) | 정확한 목표점 우선 검사, 목표 좌표별 최근접 heap·진행·소진 상태, heap 재사용 pool. 기하 유효성 캐시는 소유하지 않는다. | `Begin`, `End`, `TryFind<TPositions>`, `IsActive`, 수집/방문/heap 통계 |
| [PlacementSession](../../Assets/_Project/Runtime/Features/Common/Placement/PlacementSession.cs) | 고정 `PlacementSettings`, live 위치 reader, pending 예약, 요청별 기하 판정 cache. | `Begin`, `End`, `CanPlace`, `Commit`, `Count`, `Pending`, `GeometryCacheHits` |
| [PlacementFlow](../../Assets/_Project/Runtime/Features/Common/Placement/PlacementFlow.cs) | 시도 한도 내 수집, 대상별 순환 수집, 최근접 실패 후 fallback. 별도 상태를 소유하지 않는 static 조합 함수다. | `FillWithinBudget`, `RoundRobinFill`, `FirstSuccess` |
| [MapQuery2D](../../Assets/_Project/Runtime/Features/Common/Placement/MapQuery2D.cs) | 호출자가 전달한 바닥/벽 레이어와 벽 Tilemap 이름으로 맵을 분류한다. 인스턴스별 프레임 캐시와 방 조회를 제공한다. | `ResolveGroundTilemaps`, `HasGroundTileAt`, `HasWallTileAt`, `FindRoomContaining`, `InvalidateTilemapCache` |
| [PhysicsQuery2D](../../Assets/_Project/Runtime/Features/Common/Placement/PhysicsQuery2D.cs) | 커지는 overlap/cast List를 재사용한다. 마스크와 시작 겹침에서 멀어지는 이동 허용 여부를 명시적으로 받는다. | `OverlapTargets`, `HasOverlap`, `HasBlocker`, `ClearPlacementResults`, 기존 배열 경로 `HasBlockerAll` |

`PlacementSettings`, `PlacementPoint`, `PositionCandidateSequence`는 값 타입이다. `PlacementSession<TPositions>`는 `struct, IPlacementPositions` 제약으로 reader의 `Count` / `TryGetPosition`을 호출한다. `PositionListReader`는 유지되는 좌표 List를 읽는다. 콘텐츠 reader는 같은 방법으로 actor의 현재 위치를 읽을 수 있으며 도구가 actor 목록을 소유하지 않는다.

상태를 가진 도구는 sealed 일반 C# 객체이며 MonoBehaviour가 아니다. 별도 Manager/Singleton/DDOL 오브젝트나 전역 공유 buffer는 없다. 대상 resolver와 정렬용 Comparison을 재사용하고, 후보마다 실행 객체나 capturing closure를 만들지 않는다. 후보 cursor는 난수를 미리 채운 배열이나 iterator 객체를 만들지 않는다.

## 소유권과 요청 수명

| 범위 | 현재 구조 |
| --- | --- |
| 콘텐츠 owner / actor | 호출자가 도구 인스턴스, live 위치 목록, 실제 생성·실행·취소·cleanup을 소유한다. 서로 다른 owner는 요청 scratch를 공유하지 않는다. |
| 도구 인스턴스 | List/Dictionary 용량과 heap pool은 요청 사이에 재사용된다. 맵 캐시는 같은 인스턴스의 현재 프레임에 한정된다. 큰 요청 이후 용량이 남을 수 있다. |
| 동기 요청 | Session의 설정·기하 cache·pending 예약, Search의 목표별 heap/소진 상태, Sources의 후보 수집 상태가 적용된다. |
| 반환 결과 | `PlacementPoint`는 위치·방 값이다. `Pending`은 도구가 소유하는 List이며 다음 예약 초기화까지 빌려 쓰는 결과다. |

현재 번개창 연결의 시작은 `session.Begin(settings)`와 `search.Begin(origin, range)`다. 후자는 Sources도 시작한다. 종료는 `session.End()`, `search.End()`, `physics.ClearPlacementResults()`를 함께 호출한다. End는 요청 기하·목표 참조·물리 결과를 정리하면서 컬렉션 용량을 보존한다. Session의 pending 결과는 기본적으로 남고, 다음 `Begin`의 `clearReservations=true`가 비운다. `clearReservations=false`는 기존 standalone nearest/spacing probe의 예약 유지 경로에서 사용된다.

`CanPlace`는 검사만 수행하고 예약하지 않는다. `Commit`은 위치·방을 pending에 추가하며 검사 자체를 반복하지 않는다. Flow는 성공한 후보에만 한 번 Commit한다. 3타처럼 단일 값을 owner에게 넘기는 연결은 종료 뒤 Pending을 비워 이전의 단일 계획 scratch 수명을 유지한다.

장기 코루틴이 borrowed List를 계속 읽으면 다른 요청이 초기화할 때 결과가 바뀐다. 번개창 E는 첫 생성과 yield 전에 결과를 복사하여, 창 사이 대기 중 3타가 같은 도구를 재사용해도 자기 계획을 유지한다. 도구는 실제 생성이나 취소 상태를 대신 관리하지 않는다.

## 세 구성의 조합

공통 Flow에는 3타/일반 비/표적 비 모드 분기가 없다. 콘텐츠 연결이 같은 source/search/session을 아래 순서로 조합한다.

| 구성 | 실행 순서와 예산 |
| --- | --- |
| 단일 근접 생성 | `FirstSuccess(preferred, nearest, FixedThenRandom(preferred, tip, tip, localRadius), B)`로 정확한 preferred와 최근접 타일 전체를 먼저 검사한다. 실패하면 preferred → tip → tip 주변 난수 순서로 검사한다. `B=max(1, CandidateSamples)`에 고정 두 점도 포함된다. |
| 여러 무작위 생성 | `RandomDisk(ownerOrigin, castRange)`를 `FillWithinBudget`에 전달한다. 원하는 총수 N까지, 시도 한도 `S=max(CandidateSamples, N*12)` 안에서 수집한다. |
| 대상 우선 여러 생성 | 대상 포함 규칙으로 수집·정렬한 목록에 `RoundRobinFill`을 적용한다. 대상당 한 번씩 성공 위치를 예약하고, 한 바퀴의 성공 수 증가가 없으면 멈춘다. 이어 같은 무작위 fill로 부족분을 채운다. fallback 예산은 남은 수 기준으로 다시 줄이지 않는다. |

난수는 `TryNext`가 요청된 순간에만 `Random.insideUnitCircle * Max(.01, radius)`로 소비된다. 첫 단계가 성공하거나 이미 N개를 채운 경우 fallback cursor를 만들더라도 난수를 소비하지 않는다. B가 1이면 첫 고정점만, 2이면 고정 두 점만 검사한다. 두 고정점이 같아도 중복 검사를 없애지 않는다. 최근접 검색에서 preferred가 실패했더라도 단일 구성의 fallback 첫 검사에서 다시 확인한다.

검증/타일 수집의 origin과 후보 공급의 중심은 독립 값이다. 현재 단일 구성은 owner origin과 cast range로 허용 영역·타일 후보를 정하고, preferred로 거리를 정렬하며, tip과 local radius로 마지막 난수를 만든다. 이 값들을 하나의 origin/radius로 합치면 검색 결과가 바뀐다.

## 캐시와 순서의 전제

- Session의 검사 순서는 유한 좌표 → owner 범위 → 지정 방 → 최소 owner 거리 → live/pending 간격 → 기하 cache → 벽 타일 → 경로 blocker → 착지 blocker → 바닥/필수 ground overlap이다. 간격은 캐시된 기하 true보다 먼저 검사하므로 새 예약을 관찰한다.
- 기하 cache의 키는 좌표다. 설정, 원점, 마스크, probe 크기와 월드 기하가 같은 동기 요청에서 고정된다는 전제이며 서로 다른 설정이나 프레임의 안전 정보를 보관하는 구조가 아니다.
- Search의 Reject/소진 기록은 거절된 후보가 그 요청 안에서 다시 허용되지 않는다는 전제를 사용한다. pending 예약은 증가한다. reader가 live 변화를 읽는 기능과 Search의 재탐색 지원은 별개다. 기존 위치가 사라지거나 이동해서 이전 거절이 유효해질 수 있는 사용법은 새 요청에서 진행/cache를 초기화하는 경계를 검토한다.
- 성공한 heap 점은 바로 Pop하지 않는다. 다음 검사에서 예약 때문에 거절될 때만 Pop한다. 따라서 간격 0에서는 같은 성공 점을 다시 선택할 수 있다. 타일 중복 제거는 생성 성공 위치의 중복 금지 규칙이 아니다.
- 타일 순위는 거리 → X → Y이며, 대상 정렬은 기존 거리만 비교한다. 대상 동률에 새 XY 순서를 붙이지 않는다. round robin의 대상 순서·중간 성공 즉시 종료·무진행 종료도 별도 규칙이다.
- MapQuery는 전역 맵 역할 정책을 만들지 않는다. 번개창 연결은 기존 Ground fallback과 레이어/`Wall` 이름 판정을 인자로 전달한다. 이름에 의존하는 기존 호환 경계는 [분류 backlog](../RefactorBacklog/LightningSpearTilemapClassification.md)에 남아 있다.
- 배열 cast 경로와 reusable List cast 경로는 각각 기존 호출자를 위해 남는다. 마스크, trigger 설정, 시작 겹침 정책과 물리 조회 순서를 공통화의 부작용으로 바꾸지 않는다.

## 확장과 검증 경로

다른 콘텐츠는 수치 `PlacementSettings`, 유지되는 좌표 List의 `PositionListReader` 또는 자기 값 reader, 대상 resolver를 공급하고 같은 Flow를 조합할 수 있다. 점 계획의 소비·생성·owner cleanup은 콘텐츠에 남는다. 최소 거리 검사나 getter마다 새 클래스를 만들거나 별도 명령 DSL을 작성할 필요는 없다.

[PlacementToolCompositionPlayModeTests](../../Assets/_Project/Tests/PlayMode/Procedural/PlacementToolCompositionPlayModeTests.cs)는 무기 데이터/actor 없이 좌표와 설정만으로 작성된 7개 사례다. 같은 도구의 bounded fill/round robin/first-success 조합과 생성 없음, 고정 prefix 예산과 지연 RNG, 간격 0 반복, 예약 이후 캐시 성공 거절과 XY 동률, 요청 재사용 및 live 좌표 변화를 확인하도록 구성되어 있다. 공개 무기 타입은 기존 테스트 assembly가 내부 도구를 찾기 위한 assembly anchor로만 사용한다.

번개창 통합과 기존 결과·작업량·RNG 비교는 [콘텐츠 구조 지도](LightningSpearPlacement.md)와 [작업 범위](../ActiveTasks/lightning-spear-tool-composition.md)의 검증 경로에서 추적한다. 실제 Unity 공통 구성7개 및 관련10개 검사17/17, 반복736쌍+새 버퍼312쌍의 결과/RNG/작업량 일치를 확인했다. 측정 비용 증가·편차와 할당/Player 한계는 [검증 보고서](../SessionLogs/2026-10-10-lightning-tools/README.md)에 보존한다. 공통화 자체를 FPS/무할당/지속 속도 향상으로 해석하지 않는다.

현재는 feature-level 구조 지도다. 여러 production 기능에서 같은 요청·소유권 경계가 실제로 재사용되고 검증되면 공통 Architecture/Contracts 승격을 검토할 수 있다. 다른 소비자 마이그레이션과 source-of-truth 문서 변경은 별도 결정이며 이 문서가 대신 승인하지 않는다.
