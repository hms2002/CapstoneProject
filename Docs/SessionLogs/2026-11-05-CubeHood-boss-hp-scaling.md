# 보스 단계별 HP 시작 배수 적용

- 작성일: 2026-11-05. 작성자 GitHub 로그인: CubeHood. [동일 사용자의 로그인 제공 확인 기록](2026-11-05-CubeHood-relic-shot-audio.md)을 확인하여, 앞서 작성자 확인 대기로 남겨 둔 보스 HP 작업의 SessionLog를 완료했다. 과거 공유 날짜 로그는 수정하지 않았다.
- Mode: Implementation. 사용자 승인 배수: 1단계 2배, 2단계 2.5배, 3단계 3배, 마왕 5배. 이번 로그 작성 시 보스 코드를 추가 수정하지 않았다.
- [BossControllerBase.cs](../../Assets/_Project/Runtime/Features/Bosses/Common/FSM/Core/BossControllerBase.cs): Start에서 현재 RunRoutePlayback 단계의 배수를 개체의 초기 MaxHealth/Health에 한 번 적용한다. 최대 HP를 먼저 올려 MaxLink clamp를 피하고, 같은 보스의 등장 순서에 따라 배수가 달라진다. 슬라임 P2 개체도 각자 초기 프로필에 같은 단계 배수를 적용한다. 런 문맥이 없는 직접 실행은 일반 보스 1단계 배수, DarkLord_Tutorial은 제외한다.
- [DemonKingController.cs](../../Assets/_Project/Runtime/Features/Bosses/DemonKing/DemonKingController.cs): 배수 5를 반환하여 현행 2800 기준 14000 HP로 초기화한다. 공격력/속도/그로기/패턴 임계값 및 원본 프로필/씬/프리팹/저장은 수정하지 않았다.
- [BossAndMobEncounterStructure.md](../StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md#boss-encounter-hp-multipliers-2026-11-05): 초기화 소유권, 단계 배수, 튜토리얼/직접 실행, 분열체 및 현재 baseline 수치를 정리했다. 오래된 profile HP 설명을 현재 연결값과 맞췄다.
- 검증: 기존 생성 Gameplay.csproj에 두 소스가 포함됨. 앞선 Gameplay MSBuild exit 0, 소스/참조/diff/Markdown 링크 점검. 프로필 참조 경로는 Slime 1000/400/400, Witch 1500, Dragon 2000, DemonKing 2800임을 확인했다. [증거와 변경 이유](../../outputs/boss-hp-scaling-2026-11-05/README.md), [컴파일 기록](../../outputs/boss-hp-scaling-2026-11-05/msbuild.log).
- 자동 테스트/Unity import/Editor 컴파일/실제 플레이/Player build는 미실행. 남은 확인: 실제 등장 순서별 HP, 분열체 생성과 마왕 HP 임계 패턴, 플레이어 빌드별 전투 시간.
- Doc Impact Check: SessionLog + 기존 StructureMemory. ErrorLog/DecisionLog 변경 없음. Architecture/Contracts 승격 및 Presentation HTML stale candidate 없음.
