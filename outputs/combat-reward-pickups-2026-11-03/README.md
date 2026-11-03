# 경험치·골드 전투 종료 일괄 획득

2026-11-03. 전투 중에는 두 픽업을 바닥에서 반투명으로 대기시키고, 종료 시 짧게 떠오른 다음 플레이어에게 함께 흡수되도록 변경했다.

## Task Brief

- Mode: Implementation. 현재 사용자 요청이 작업 범위이며 일치하는 ActiveTask는 없다.
- Goal/intent: 전투 중 자동 획득을 멈추고, 전투 종료 때 보상이 한꺼번에 모이는 연출을 제공한다.
- Allowed: 경험치·골드 픽업, 기존 공통 이동/포탈 정산 코드, 두 공용 프리팹의 해당 설정, 변경된 동작을 직접 참조하던 기존 재사용 회귀 테스트 한 곳, 관련 기억 문서.
- Forbidden: 보상량/드롭 정책, 마석/회복 아이템 동작, 전투 판정 자체, 씬/저장 스키마/매니저/부트스트랩 변경, 자동 테스트 및 Play Mode 실행.
- 가정과 성공 기준: 공용 `IsPlayerInCombat` 판정을 사용한다. 전투 중 45% alpha로 고정 대기하고, 마지막 드롭까지 같은 프레임의 해제 판정을 받아 함께 상승·흡수한다. 기존 지급 경로와 파티클, 중복 방지, 포탈 정산을 보존한다.
- 주요 위험: 웨이브 사이 조기 획득, 마지막 처치 드롭의 지연, 이동 중 포탈 사용 시 보상 유실, 일시정지/비활성화에서 alpha·이동 상태 잔류. 아래 소스 검토와 수동 확인 항목으로 구분했다.

## 적용 동작

| 구간 | 동작 |
| --- | --- |
| 전투 중 | 바닥 위치 유지, 원래 alpha ×0.45, 보상 지급 없음 |
| 전투 종료 | 0.15초 동안 위로 0.25 world unit 상승하면서 원래 불투명도로 복귀 |
| 흡수 | 약 0.3초 동안 현재 플레이어 위치로 가속 보간. 거리와 관계없이 같은 시간에 도착 |
| 전투 재개 | 진행 중 이동 취소 후 바닥 대기. 상승 중이면 기록한 바닥 위치로 복귀 |
| 일시정지/플레이어 없음 | 진행 정지. 플레이어를 다시 찾으면 이어서 처리 |
| 비활성화/재활성화 | 원래 alpha 복구 및 이동 취소, 재활성화 시 새 대기 상태 |
| 전투 종료 직후 포탈 | 아직 바닥/상승 중인 픽업도 기존 지급 메서드로 정산. 전투 중 강제 획득은 차단 |

모든 시간은 scaled time 기준이다. 전투 밖에서 새로 생성된 경험치·골드도 같은 짧은 상승/흡수를 사용한다. 기존 경험치의 색상 순환 Animator는 sprite만 바꾸며 이 흐름은 alpha와 위치만 다룬다.

## 변경 파일

| 파일 | 이유 |
| --- | --- |
| [ExperiencePickup2D.cs](../../Assets/_Project/Runtime/Features/Progression/Leveling/ExperiencePickup2D.cs), [GoldPickup2D.cs](../../Assets/_Project/Runtime/Features/Loot/GoldPickup2D.cs) | 기존 지연 추적을 전투 대기·상승·흡수로 연결하고, disable/enable 초기화 및 전투 종료 후 포탈 정산 대상을 확장한다. 보상 지급/consumed guard/획득 파티클은 기존 경로다. |
| [HomingPickupTravel.cs](../../Assets/_Project/Runtime/Features/Loot/HomingPickupTravel.cs) | LateUpdate에서 한 프레임에 한 번 공용 전투 판정을 읽는다. 같은 파일의 `CombatRewardPickupMotion`은 픽업별 상태와 alpha/위치만 소유한다. 마석용 기존 가속 함수와 정산 진입점은 유지한다. |
| [ExperiencePickup_Square.prefab](../../Assets/_Project/Prefabs/Loot/ExperiencePickup_Square.prefab), [GoldPickup.prefab](../../Assets/_Project/Prefabs/Loot/GoldPickup.prefab) | 기존 renderer를 연결하고 대기 alpha/상승 높이/시간/흡수 진행률을 함께 설정한다. GUID, 스프라이트, Animator, 크기, collider, 보상/파티클 참조 유지. |
| [CombatFeelLootQuestPlayModeTests.cs](../../Assets/_Project/Tests/PlayMode/Procedural/CombatFeelLootQuestPlayModeTests.cs) | 삭제된 private 초기 추적 타이머와 Update를 호출하던 기존 골드 회귀 한 곳을 전투 대기·상승·거리별 동시 도착 검사로 갱신했다. 실행하지 않았다. |
| [RunGoldAndShop](../../Docs/StructureMemory/RunGoldAndShop.md), [LevelProgressionStructure](../../Docs/StructureMemory/ScriptSystems/LevelProgressionStructure.md) | 현재 책임·수명·튜닝 위치·포탈 정산 조건을 갱신했다. |
| [SessionLog](../../Docs/SessionLogs/2026-11-03.md), [DecisionLog](../../Docs/DecisionLog.md) | 적용 결과와 전투 종료 획득 정책을 기록했다. |

프리팹의 직렬화 필드 이름은 유지했다. `homingDelay`는 이제 전투 종료 후 상승 시간이고, `homingSpeed`는 월드 속도가 아닌 초당 정규화 진행률이다. 양쪽 기본값 3.333333은 약 0.3초를 의미한다. 튜닝은 두 프리팹 값을 함께 바꾸면 된다. 새로운 `.cs`/`.meta`, 컴포넌트, 전역 Manager, 이벤트 구독, 코루틴은 추가하지 않았다.

## 검증 결과

- `Gameplay.csproj` MSBuild **종료 코드 0, 오류 0**. [컴파일 로그](msbuild-gameplay.log).
- `ProceduralPlayModeTests.csproj` MSBuild **종료 코드 0**. 기존 회귀 코드의 컴파일만 수행했으며 테스트 실행이 아니다. [로그](msbuild-regression-compile.log).
- Unity **6000.4.2f1** batch import/스크립트 컴파일 **종료 코드 0**. [Unity 로그](unity-import.log). Play Mode 진입, 테스트 실행, Player build는 하지 않았다.
- 소스·참조 검토: 변경 스크립트는 모두 기존 Gameplay 어셈블리/생성 csproj에 포함된다. 두 스크립트의 authored 사용처는 해당 프리팹 두 개이며 renderer fileID가 유효하다. 프리팹 변경은 지정한 설정 5개뿐이다. [참조·범위 대조](source-reference-inspection.txt).
- 전투 판단은 기존 active room의 웨이브/등록·예약 적/encounter hold와 방 밖 적 인식 규칙을 그대로 사용한다. 모든 픽업이 같은 LateUpdate 판정을 받아 마지막 처치 드롭도 추가 1초 지연 없이 출발한다.
- 지급 실패 시 consumed를 복구/유지해 재시도하며, 성공 시 기존 consumed guard로 중복 지급을 막는다. 포탈에서는 새 판정을 직접 읽어 전투 중 정산을 차단한다. 개별 지급 실패가 있으면 기존 이동 차단 경고가 유지된다.
- `git diff --check` 및 로컬 Markdown 링크 확인. 기존 미커밋 변경과 이전 경험치 아트 작업을 보존했다.

## 수동 확인이 남은 항목

자동 테스트와 수동 플레이는 미실행이다. 여러 거리의 픽업이 있는 방, 웨이브 사이 빈 시간/경보 종 hold, 마지막 처치와 동시에 생긴 드롭, 상승/흡수 중 pause·재전투·disable/enable, 전투 종료 직후 포탈을 실제 게임에서 확인해야 한다. 씬 조명에서의 45% alpha 시인성과 0.25 높이·0.45초 전체 연출의 체감도 미확인이다.

Doc Impact Check: **SessionLog + StructureMemory + DecisionLog**. ErrorLog/RefactorBacklog 변경 없음. Architecture/Contracts 승격이나 Presentation HTML stale 후보 없음.
