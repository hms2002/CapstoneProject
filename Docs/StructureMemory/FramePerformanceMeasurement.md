---
status: active
authority: structure-memory
category: performance-measurement
last_verified: 2026-10-07
---

# Frame Performance Measurement

## Purpose

프레임 최적화의 이전/현재 구현을 다시 측정할 때 필요한 실행 경로, 임시 fixture 소유권, 결과 해석 경계를 빠르게 찾는 구조 지도다. 코드 경로와 cleanup을 읽어 확인한 문서이며 실행별 수치나 완료 판정은 담지 않는다. Architecture/Contracts를 대신하지 않는다.

## Source Of Truth

- 현재 범위: [Frame Performance Measurement ActiveTask](../ActiveTasks/frame-performance-measurement.md).
- 최적화 범위: [Frame Performance Optimization ActiveTask](../ActiveTasks/frame-performance-optimization.md).
- 실행 조건, 원본 hash, raw 결과와 최종 해석: [측정 artifacts README](../SessionLogs/2026-10-07-frame-performance-measurement/README.md).
- sandbox의 기존 소유권: [TestScene 구조](TestScene.md). 아래 구현 파일이 현재 실행 경로의 직접 근거다.

## Current Structure And Key Files

| 파일 | 진입점과 역할 |
| --- | --- |
| [FramePerformancePlayerBuild.cs](../../Assets/_Project/Editor/Tools/Validation/FramePerformancePlayerBuild.cs) | `Build()`와 `IProcessSceneWithReport`. 기존 TestScene만 Development Windows player로 빌드하고, 빌드 중인 씬 복사본의 기존 `TestSceneControlPanel` 오브젝트에 probe를 추가한다. |
| [FramePerformancePlayerProbe.cs](../../Assets/_Project/Runtime/UI/Testing/FramePerformancePlayerProbe.cs) | 빌드 복사본에서 `Start()`로 실행. 실제 player frame, 지원되는 native counters, SRP callbacks, warmup screenshot과 작업량을 기록한다. |
| [FramePerformanceSceneBenchmarks.cs](../../Assets/_Project/Editor/Tools/Validation/FramePerformanceSceneBenchmarks.cs) | `Run()`이 기존 TestScene을 열고 Play Mode의 coroutine stack을 직렬 진행한다. `-framePerfOpsOnly true`에서는 Editor FPS capture를 건너뛰고 CPU/UI/GPU operation 결과를 작성한다. |
| [FramePerformanceCpuBenchmarks.cs](../../Assets/_Project/Editor/Tools/Validation/FramePerformanceCpuBenchmarks.cs) | `Run()` / `RunWithOptions(...)`. 몹 감지/FSM busy tick/kill-lock/Flowering native overlap와 effect repository 조회, 총 16개 operation 비교를 수행한다. |
| [FramePerformanceUiBenchmarks.cs](../../Assets/_Project/Editor/Tools/Validation/FramePerformanceUiBenchmarks.cs) | `Run()`은 numeric/timer/status/popup/hover 비교 11개. `RunCoroutine(Action<Report>)`는 damage popup burst를 추가해 총 12개를 반환한다. |
| [FramePerformanceGpuBenchmarks.cs](../../Assets/_Project/Editor/Tools/Validation/FramePerformanceGpuBenchmarks.cs) | `Run(reportPath, renderSize, drawCount, measuredFrames)`. 별도 render target에 원본/현재 puddle shader를 그리며 GPU timestamp와 출력 픽셀을 검증한다. |

Player 경로는 전체 렌더링된 프레임 비교다. Editor operation 경로는 같은 입력으로 특정 코드/렌더 segment만 비교한다. 두 경로의 결과 단위가 다르다.

## Run Conditions And Reuse Entry Points

### Standalone player

1. Editor 측 `FramePerformancePlayerBuild.Build`에 `-framePerfBuildOutput`으로 별도 executable 경로를 전달한다.
2. player 실행 시 `-framePerfVariant`와 `-framePerfOutput`으로 구현 label과 보고서 경로를 지정한다.
3. 현재 fixture는 1280×720 windowed, VSync 0, targetFrameRate -1, 고정 seed와 이동/공격 입력 없는 조건이다. HP/resource와 HUD 개수는 정상 상태로 유지하며 기존 `State.Invulnerable` tag를 측정 owner의 count 1만큼 추가한다. 전체 `HeartTokenUI` 개수가 128을 넘으면 workload 검증이 실패한다.
4. 0/30/60 GoblinWarrior와 초당 120회 popup 요청을 각각 5초 warmup, 8초 capture로 진행한다. 매 scenario 전에 player를 초기 위치로 warp한다. 일반 AI, animation과 physics는 진행하지만 보호 tag로 피해와 피격 feedback이 차단되는 조건이다. 자연 전투의 피격/knockback 비용까지 포함한 결과나 deterministic replay는 아니다.
5. 전경 렌더링 조건을 맞춘 원본/현재 ABBA 실행은 이 측정 작업에서 승인된 비교 방식이다. 해상도, focus, 실제 몹/요청 수, 이동한 위치와 wait markers를 raw 결과와 함께 검토한다.

### Editor operations

- `FramePerformanceSceneBenchmarks.Run`에 `-framePerfVariant optimized`, `-framePerfOpsOnly true`, `-framePerfOutput`을 전달하면 CPU → UI coroutine → GPU 순서로 실행한다.
- CPU와 UI는 warmed AB/BA 순서로 비교하며 CPU stopwatch와 GC recording은 별도 pass다. UI는 각 pass 21 paired rounds를 사용한다.
- Scene runner의 GPU 호출은 segment당 warmup 20 frames + capture 30 frames다. single/many draw calibration 다음 ground/legacy-control 상태를 각각 ABBA로 비교한다. GPU helper를 직접 호출할 때 기본 capture는 60 frames이므로 호출 인수를 확인한다.
- 이미 준비된 Play Mode에서는 CPU/UI `Run()`과 GPU `Run(...)`을 별도로 재사용할 수 있다. UI의 실제 deferred Destroy 완료 확인이 필요하면 synchronous `Run()` 대신 `RunCoroutine(...)`을 사용한다.
- Unity 실행은 직렬로 진행한다. source 교체/import/build 중인 Editor와 다른 측정 실행을 겹치면 대상 DLL과 fixture 상태를 식별하기 어려워진다.

## Source Fixtures And Provenance

- [Source manifest](../SessionLogs/2026-10-07-frame-performance-measurement/source-manifest.json)은 비교 대상 20개 파일의 원본/현재 hash를 기록한다.
- [Original operation sources](../SessionLogs/2026-10-07-frame-performance-measurement/original-operation-sources/)에는 19개 원본 코드/셰이더를 기존 `Assets/...` 경로에 `.txt`를 붙여 보관한다. 실행 가능한 `.cs`로 두지 않아 컴파일 중복을 피한다.
- 큰 `GlobalUIRoot.prefab` 원본은 별도 임시 backup에 있다. 비교 차이는 세 텍스트의 raycastTarget 값이며 prefab 전체를 다른 상태로 되돌리는 절차가 아니다.
- UI helper는 원본 UI 본문을 Editor-only frozen namespace에 보존하며 현재와 같은 authored hierarchy/reference로 fixture를 만든다. 원본 tooltip의 singleton fallback만 fixture 전용 경로로 제한한다.
- source baseline player build는 Editor를 닫은 상태에서 승인된 최적화 부분만 임시 되돌린다. 이전 사용자 변경은 유지한다. 새 API를 참조하는 measurement-only UI helper의 임시 stub은 baseline player에서 실행되지 않으며, 이후 정확한 원래 bytes로 복원한다.

## Ownership And Lifecycle

- 측정 probe는 기존 test controller의 **빌드 복사본**에만 부착된다. 새 Manager/Singleton 설계, 저장된 DDOL 오브젝트, scene/prefab/ProjectSettings 변경을 추가하지 않는다.
- PlayerProbe는 임시 입력 backend, frame-rate/VSync, settings component 활성 상태와 rendering hooks를 관리한다. 기존 보호 tag에 owner count 1을 추가하고 종료/파괴 cleanup에서 같은 count만 제거해 기존 tag 소유권을 유지한다. 정상 종료 시 backend/settings/hooks를 정리하고 JSON을 쓴 다음 player를 종료한다. HP/resource를 키우는 override는 없으며 warp는 sandbox player의 상태다. 실제 profile을 갱신하는 경로를 추가하지 않는다.
- Scene runner는 자신이 생성한 actor와 coroutine stack을 정리하고 입력/rate 설정을 복구한다. 완료/실패 상태를 기록한 뒤 Play Mode를 종료한다.
- CPU/UI fixture는 생성한 GameObject, delegate/cache와 native recorder를 소유하고 dispose한다. UI singleton lease는 fixture `Awake` 동안만 적용하고 기존 참조를 즉시 복구한다. damage pool 비교는 기존 서비스 구현을 임시 fixture에서 사용하며 실제 게임의 활성 pool을 옮기지 않는다.
- UI damage burst는 원본 Destroy 요청 뒤 frame boundary를 기다려 해당 객체의 파괴를 확인한다. 이는 기존 수명 흐름 확인이며 native 파괴 비용 전체를 operation stopwatch에 포함했다는 뜻은 아니다.
- GPU helper는 생성 shader/material/render target/command buffer/recorder를 정리하고 `_Time`, shader compilation 및 profiler area 설정을 `finally`에서 복구한다.

## Known Pitfalls And Limits

- 초기 probe의 100000 HP/resource override는 `healthPerHeart=1` HUD에서 100000개의 `HeartTokenUI` 생성을 유발했다. 이는 측정 fixture가 만든 workload 오류이며 해당 조건의 초기 player ABBA 결과는 비교에서 제외한다. 게임 FPS나 최적화의 회귀 근거로 쓰지 않는다. 현재 경로는 정상 HP/resource, owner가 정리하는 보호 tag와 HUD 개수 guard로 이 오류를 피한다.
- CPU/UI median/p95는 batch-average operation 비용의 분포다. 전체 게임 FPS나 실제 frame-tail p95로 환산하지 않는다. frame 변화율은 같은 조건의 standalone frame 결과에서 판단한다.
- GC.Alloc operation 수치는 Bytes metadata, 무할당 control, 알려진 payload 크기/증가량과 event count calibration을 통과할 때만 보고한다. Player frame GC는 별도 0/4096/8192-byte payload step 검증을 사용한다. 실패/미지원은 unavailable이며 heap 증가량으로 대체하지 않는다.
- `Main Thread`는 대기를 포함할 수 있다. wait markers는 겹칠 수 있으므로 임의로 합하거나 빼서 순수 gameplay CPU를 만들지 않는다.
- draw/set-pass/vertices counter 미지원은 렌더링이 없다는 증거가 아니다. 완료된 SRP callbacks와 nonuniform warmup screenshot이 별도 렌더링 근거다. readback은 capture window 밖에 둔다.
- GPU 결과는 지연된 timestamp라 warmup과 positive sample/block-count/draw-scaling 검증이 필요하다. CPU command submission 시간을 GPU 시간으로 보고하지 않는다.
- 숨김/가림/비활성 window의 frame rate는 전경 플레이 결과와 혼합하지 않는다. 같은 scene과 actor 수만으로 focus/present 상태까지 같아지는 것은 아니다.
- source/frame/profile 설정이 바뀌면 기존 artifact와 직접 비교하기 전에 manifest, 실행 조건과 correctness를 다시 확인한다.

## Recent Structural Changes

operation fixtures와 standalone build-copy probe가 분리되었다. measurement code는 기존 TestScene 및 UI/몹/puddle 자산을 소비하며 production gameplay state의 소유권을 가져가지 않는다. 날짜별 raw 수치와 시행 과정은 artifacts README/SessionLog에서 추적한다.

## Recovery Notes

- 원본 교체 중 중단되면 먼저 해당 실행의 Editor/player 상태와 manifest hash를 확인한다. 다른 작업의 변경을 덮어쓰며 baseline/current를 복원하지 않는다.
- helper/원본 archive가 없거나 counter calibration이 실패하면 해당 측정은 증거가 부족한 상태다. 원본/조건을 복구한 뒤 대응 fixture를 다시 실행한다.
- saved scene/asset의 측정용 변경을 전제로 하지 않으므로 임시 fixture 제거와 소유한 실행 종료가 cleanup 경로다. 관련 source 복원과 generated report 경로는 artifacts README를 따른다.

## Promotion Candidate

현재는 특정 최적화 slice의 재측정 지도다. Architecture/Contracts 승격 후보는 아니며, 여러 기능에서 반복 사용되는 표준 측정 pipeline이 되면 별도 검토할 수 있다. Presentation HTML 변경은 필요하지 않다.
