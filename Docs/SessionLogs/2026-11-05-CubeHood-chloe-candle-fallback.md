# 클로에 빈 촛대 탄막을 소등 패턴으로 대체

- 작성일: 2026-11-05. 작성자 GitHub 로그인: `CubeHood` ([동일 사용자 확인 기록](2026-11-05-CubeHood-relic-shot-audio.md)).
- Mode: Implementation. 사용자 요청: 꺼진 촛대 탄막이 선택됐지만 꺼진 촛대가 없으면 기존 촛대 끄기 패턴을 대신 실행한다.
- 조사 결과: Witch 프리팹의 탄막 엔트리는 `additionalConditions`가 비어 있고 실제 `HeoMinSeok_Boss_Shadow` 씬도 봉인 촛대 조건을 연결하지 않는다. 탄막 AL은 촛대 0개에서 종료하지만 GAS recovery 0.85초는 남는다.

## 변경과 이유

- [Witch.cs](../../Assets/_Project/Runtime/Features/Bosses/ShadowBoss/Witch.cs): `GetPatternState`에서 촛대 폭주 선택 및 봉인 촛대 0개를 확인하고, 현재 페이즈의 실행 가능한 기존 소등 엔트리로 예약을 교체하여 `WitchExtinguishPatternState`에 진입한다. 소등 AL은 설정만 제공하므로 AL 호출로 대체하지 않는다.
- 봉인 촛대가 1개 이상이면 기존 탄막 경로를 유지한다. 대체 소등 패턴은 기존 거리/HP/선택 잠금/사용 제한/GAS 조건 및 소등 대상 유무를 통과해야 한다. 대체할 수 없으면 예약을 비우고 대기하며 빈 탄막을 실행하지 않는다.
- 대체 시 기존 소등 엔트리를 사용하므로 소등 전용 경고/폭발/실제 봉인, 실행 이력, 종료 후 지연과 후속 연계 및 상태 이탈 시 정리 경로를 그대로 따른다. 선택 직후 상태 진입 전 분기하며 탄막 Ability 자체는 활성화하지 않는다.
- 씬/프리팹/직렬화 필드/스키마/Animator/asmdef/리소스 경로/공용 FSM 및 기존 소등 executor는 수정하지 않았다.

## 검증과 남은 확인

- 호출 경로를 소스로 대조했다: `BossPatternSelectState`의 예약 → Witch 상태 선택에서 소등 예약으로 교체 → 소등 state의 `TryStartPattern` 재평가 및 `BeginPattern` → 소등 executor 완료 → `FinishCurrentPattern`/`OnExit` 정리. 대체 후 강제 후속 예약 표시가 해제되고 일반 소등 조건으로 재평가됨을 확인했다.
- 기존 Gameplay.csproj에 Witch.cs가 포함되고 namespace/assembly 경계 및 Unity 생명주기/직렬화 선언 변경이 없음을 확인했다. `git diff --check` 통과.
- Gameplay MSBuild exit 0 (`BuildProjectReferences=false`). 로그: `Library/CodexChloeCandleFallback20261105/msbuild.log`. 기존 API obsolete/unused/member hiding 등의 경고가 있다. Unity Editor 컴파일/import 또는 Player build 성공을 의미하지 않는다.
- 자동 테스트/Unity import/Editor 컴파일/Play Mode/실제 플레이는 미실행. 수동 확인: 꺼진 촛대 0개에서 탄막 선택 시 소등 경고 후 실제 소등되는지, 1개 이상에서 기존 탄막이 나가는지, 촛대 자체가 없는 경우 빈 탄막 없이 대기하는지, 소등 중 그로기/사망 취소 시 경고가 정리되는지.
- Doc Impact Check: SessionLog. 기존 상태/실행 소유권을 유지하는 로컬 분기로 StructureMemory 추가 변경은 불필요하다. ErrorLog/DecisionLog 변경 없음. Architecture/Contracts 승격 및 Presentation HTML stale candidate 없음.
