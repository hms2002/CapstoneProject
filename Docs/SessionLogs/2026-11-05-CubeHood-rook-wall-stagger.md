# 슬라임 룩 벽 충돌 무력화 누적

- 작성일: 2026-11-05. 작성자 GitHub 로그인: CubeHood. [기존 로그의 사용자 제공 확인 기록](2026-11-05-CubeHood-relic-shot-audio.md)을 근거로 사용했다.
- Mode: Implementation. 사용자 승인 범위: 돌진 중 벽에 두 번 부딪히면 무력화되도록 최대 게이지 50% 누적, 중복/면역/시간 초과/취소/사망 방어. 자동 테스트는 미승인으로 실행하지 않았다.

## 변경 파일과 이유

- [RookChargeRunner.cs](../../Assets/_Project/Runtime/Features/Monsters/Slime/RookChargeRunner.cs): 실제 몸체 충돌과 벽 직전 정지 확인을 단일 HandleWallImpact로 연결. 콜백보다 먼저 hitWall을 세우고 StopDash로 이동/돌진용 면역을 해제한 뒤 기존 게이지에 최대치의 절반을 누적한다. 모서리/다중 collider/재진입 중복을 방지하고, 돌진 종료 시 실제 몸체의 전방 0.08 범위에 유효 차단물이 있는 경우에만 처리한다. 단순 제한 시간 만료는 더 이상 벽 충돌 연출/스태거를 발생시키지 않는다.
- 같은 파일: OnDisable을 Cancel로 연결하여 취소 상태를 명시. 종료 직전 마비는 해제될 때까지 terminal gameplay 작업을 기다린다. 죽음/취소/비활성/마비 중 누적을 차단한다. 기존 타겟/이속/플레이어 피해/분열/직렬화 필드는 유지했다.
- [BossAndMobEncounterStructure.md](../StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md#rook-wall-impact-stagger-2026-11-05): 현재 충돌 정책, 소유권, 게이지/면역 처리 순서, 제한 사항과 진입점 기록.
- [컴파일 로그](../../outputs/rook-wall-stagger-2026-11-05/msbuild.log): Gameplay.csproj MSBuild exit 0. 변경 C# 파일은 기존 생성 프로젝트에 포함되어 있다.

## 검증과 남은 확인

- 소스/참조/diff 점검: 직접 collision과 dash-end probe가 같은 단발 가드를 거치며, StopDash가 AddBuildUp보다 먼저 실행됨. 일반 StopDash/Cancel/finally/disable 자체는 누적을 호출하지 않는다. probe는 활성 비트리거 실제 몸체만 사용하고 기존 IsChargeBlocker로 벽/닫힌 문을 판정한다. 열린 문/구덩이/플레이어/다른 몬스터는 지형 판정을 새로 확장하지 않는다.
- Rook.prefab에서 StaggerGaugeSystem, 최대 HP의 1/3 게이지, resistancePercentAttribute 미연결과 기존 3초 GE_Groggy_Status 연결을 확인했다. CurStagger는 자동 재생이 없다. 최대 게이지의 50%를 두 번 누적하면 기존 overflow/그로기 흐름을 사용하며, UI는 1-current/max 투영이므로 표시상 50%씩 감소한다. 플레이어 공격으로 이미 누적했다면 더 일찍 그로기가 발생할 수 있다.
- 기존 메타데이터/GUID, namespace/assembly, 생명주기/면역 스택 처리와 공백 검토. 새 직렬화 필드/컴포넌트/런타임 hierarchy/프리팹/씬 변경 없음.
- 자동 테스트, Unity import/Editor 컴파일, Play Mode, Player build는 미실행. 남은 수동 확인: 서로 다른 두 돌진의 벽 충돌 후 그로기, 모서리 다중 접촉, 열린/닫힌 문, 벽 없는 안전 시간 초과, 취소/사망/비활성화 및 종료 직전 마비.
- Doc Impact Check: SessionLog + 기존 StructureMemory. ErrorLog/DecisionLog 변경 없음. Architecture/Contracts 승격 및 Presentation HTML stale candidate 없음.

- 최종 취소 점검: 실행 중 AbilitySpec을 보관하여 cancelRequested뿐 아니라 spec.Token.IsCancelled도 준비 종료/돌진 루프/종료 직전 대기/충돌 처리에서 확인한다. finally에서 spec 참조를 해제한다.
