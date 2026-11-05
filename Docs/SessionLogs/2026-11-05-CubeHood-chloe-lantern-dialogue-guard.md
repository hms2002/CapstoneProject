# 클로에의 등불 보스 대화 중 공격 차단

- 작성일: 2026-11-05. 작성자 GitHub 로그인: `CubeHood` (동일 사용자 제공 ID 확인).
- Mode: Implementation. 사용자 보고는 보스 조우 대화 중 등불이 발사하여 보스 체력을 깎는 문제다. 목표는 대화 및 앞뒤 카메라 연출 중 신규 발사와 비행 중 탄막 피해를 막고, 정상 전투 때 기존 자동 공격을 재개하는 것이다.
- 원인: 등불의 독립 Update는 생존/표적/시야만 확인했다. 보스 연출이 일반 입력을 차단해도 등불은 대화 상태를 읽지 않았고, 공용 등불 탄막의 별도 Update/물리 콜백도 피해를 계속 적용할 수 있었다.

## 변경과 이유

- [ChloeLanternOrbitController.cs](../../Assets/_Project/Runtime/Features/Items/Relics/ChloeLanternOrbitController.cs): 기존 DialoguePlayback.IsPlaying 또는 소유 PlayerInteractor2D.CurrentState가 Talking이면 공격을 차단한다. BossEncounterDirector가 Talking을 카메라 이동 전부터 복귀 종료까지 유지하므로 대화창 외 연출 구간도 포함한다. 공전은 유지하며 소유 탄막을 비활성/파괴하고 발사 타이머를 0으로 초기화한다. 해제 후 정상 2초 간격부터 재개한다. 기존 탄막 정리 루프를 ClearProjectiles로 분리하여 disable/destroy/detach cleanup에서도 재사용한다. token/장착/공전 상태는 대화 때문에 해제하지 않는다.
- [ChloeLanternProjectile2D.cs](../../Assets/_Project/Runtime/Features/Items/Relics/ChloeLanternProjectile2D.cs): TickAttack과 OnTriggerEnter/Stay가 공유하는 ConsumeContact에서 동일한 대화 차단 상태를 확인한다. 차단 시 consumed를 먼저 세우고 즉시 비활성화한 뒤 파괴하며 TryApplyHit를 호출하지 않는다. 컨트롤러 Update보다 물리가 먼저 처리되는 경로와 이미 날아가던 탄막도 막는다. Tick의 차단 판정은 deltaTime 검사 전이어서 시간 정지 중에도 가능하다.
- 공유 탄막 타입을 사용하는 그림자 혼불의 비행 중 탄막도 같은 접촉 보호를 받는다. 혼불의 생성/처치 규칙, 다른 유물/전역 피해 처리/보스 대화 자체/전투 수치/사운드는 수정하지 않았다.
- [RelicRuntimeStructure.md](../StructureMemory/ScriptSystems/RelicRuntimeStructure.md), [ErrorLog.md](../ErrorLog.md): 기존 상태 판정, 소유 탄막 정리와 물리 콜백 방어 경계를 기록했다. 이 SessionLog는 변경 이유와 확인 범위를 보관한다.
- 새 MonoBehaviour/서비스/상태 소유자/직렬화 필드/씬/프리팹/ScriptableObject/meta/GUID/asmdef/저장 데이터 변경은 없다. 기존 같은 Gameplay 어셈블리의 PlayerInteractor2D와 DialoguePlayback을 읽기만 한다.

## 검증과 남은 확인

- 소스/참조/작업 전 diff로 BossEncounterDirector의 Talking 획득/유지/복원, DialoguePlayback backend 조회, 타이머 갱신/FindTarget/Fire 이전의 차단, 소환수 유지와 탄막 즉시 비활성화, Tick/Enter/Stay에서 피해 이전 조기 반환, 기존 token/disable/destroy cleanup 재사용을 확인했다. AttackBase는 동일 Gameplay 어셈블리에 있으며 두 변경 파일 모두 생성 Gameplay.csproj에 포함된다.
- Gameplay.csproj MSBuild exit 0 (기존 경고 존재). 기록/스냅샷: `Library/CodexLanternDialogue20261105/msbuild.log`, `before/`.
- 자동 테스트, Unity import/Editor 컴파일, Play Mode/보스 대화 실제 재현 및 Player build는 실행하지 않았다. 테스트 재시작 후 카메라 이동→인사/본대사→카메라 복귀 동안 보스 HP 유지, 대화 종료 후 2초 주기 재개, 대화 진입 직전 탄막 접촉, 일반 NPC 대화, 해제/씬 전환을 수동 확인해야 한다.
- Doc Impact Check: SessionLog + 기존 StructureMemory + ErrorLog. DecisionLog 추가 변경 없음. Architecture/Contracts/Presentation HTML 미수정, 새 HTML stale candidate 없음.
