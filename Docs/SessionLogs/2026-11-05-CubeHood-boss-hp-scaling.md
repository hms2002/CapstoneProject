# 보스 단계별 HP 시작 배수 적용

- 작성일: 2026-11-05. 작성자 GitHub 로그인: CubeHood. [동일 사용자의 로그인 제공 확인 기록](2026-11-05-CubeHood-relic-shot-audio.md)을 확인하여, 앞서 작성자 확인 대기로 남겨 둔 보스 HP 작업의 SessionLog를 완료했다. 과거 공유 날짜 로그는 수정하지 않았다.
- Mode: Implementation. 사용자 승인 배수: 1단계 2배, 2단계 2.5배, 3단계 3배, 마왕 5배. 이번 로그 작성 시 보스 코드를 추가 수정하지 않았다.
- [BossControllerBase.cs](../../Assets/_Project/Runtime/Features/Bosses/Common/FSM/Core/BossControllerBase.cs): Start에서 현재 RunRoutePlayback 단계의 배수를 개체의 초기 MaxHealth/Health에 한 번 적용한다. 최대 HP를 먼저 올려 MaxLink clamp를 피하고, 같은 보스의 등장 순서에 따라 배수가 달라진다. 슬라임 P2 개체도 각자 초기 프로필에 같은 단계 배수를 적용한다. 런 문맥이 없는 직접 실행은 일반 보스 1단계 배수, DarkLord_Tutorial은 제외한다.
- [DemonKingController.cs](../../Assets/_Project/Runtime/Features/Bosses/DemonKing/DemonKingController.cs): 배수 5를 반환하여 현행 2800 기준 14000 HP로 초기화한다. 공격력/속도/그로기/패턴 임계값 및 원본 프로필/씬/프리팹/저장은 수정하지 않았다.
- [BossAndMobEncounterStructure.md](../StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md#boss-encounter-hp-multipliers-2026-11-05): 초기화 소유권, 단계 배수, 튜토리얼/직접 실행, 분열체 및 현재 baseline 수치를 정리했다. 오래된 profile HP 설명을 현재 연결값과 맞췄다.
- 검증: 기존 생성 Gameplay.csproj에 두 소스가 포함됨. 앞선 Gameplay MSBuild exit 0, 소스/참조/diff/Markdown 링크 점검. 프로필 참조 경로는 Slime 1000/400/400, Witch 1500, Dragon 2000, DemonKing 2800임을 확인했다. [증거와 변경 이유](../../outputs/boss-hp-scaling-2026-11-05/README.md), [컴파일 기록](../../outputs/boss-hp-scaling-2026-11-05/msbuild.log).
- 자동 테스트/Unity import/Editor 컴파일/실제 플레이/Player build는 미실행. 남은 확인: 실제 등장 순서별 HP, 분열체 생성과 마왕 HP 임계 패턴, 플레이어 빌드별 전투 시간.
- Doc Impact Check: SessionLog + 기존 StructureMemory. ErrorLog/DecisionLog 변경 없음. Architecture/Contracts 승격 및 Presentation HTML stale candidate 없음.

## 병합 후 중복 HP 2배 제거 (2026-11-05)

- Mode: Implementation. 사용자 요청에 따라 병합 커밋 `442020e12`에서 추가된 기본 HP 2배만 제거하고, 기존 단계별 2/2.5/3배 및 마왕 5배 초기화 코드는 유지했다.
- 변경 파일: `Assets/_Project/Data/Attributes/InitProfiles/Enemies/Bosses/`의 `SlimeQueenAttributeOverrideInitProfile.asset`, `SlimeQueenP2ShortAttributeOverrideInitProfile.asset`, `SlimeQueenP2LongAttributeOverrideInitProfile.asset`, `WitchAttributeOverrideInitProfile.asset`와 `Assets/_Project/Data/Attributes/InitProfiles/`의 `DragonBossAttributeOverrideInitProfile.asset`, `WitchBossAttributeOverrideInitProfile.asset`. 각 파일의 Health/MaxHealth 두 값만 각각 1000/400/400/1500/2000/2800으로 복원했다. 마지막 WitchBoss 이름의 프로필은 실제 마왕에 연결된다.
- 결과: 일반 보스 등장 단계별 HP는 기존 StructureMemory 표와 다시 일치하고, 마왕은 2800 × 5 = 14000이다. 리자드 전사 추격 1.3배와 룩 벽 충돌 50% 누적은 수정하지 않았다. 스키마, GUID, 참조, 씬/프리팹, 공격력 및 무력화 수치도 변경하지 않았다.
- 검증: 변경 전 씬/프리팹 참조와 Health/MaxHealth 초기화 경로 및 마왕의 HP 비율 기반 임계값을 소스로 확인했다. 변경 후 diff에서 프로필 여섯 개의 HP 12개 값만 변경되고 병합 전 기본값과 일치하는지 확인했다. `git diff --check`로 공백을 점검했다.
- 자동 테스트, 빌드, Unity import/Editor 컴파일 및 실제 플레이는 미실행. C# 변경이 없어 MSBuild는 실행하지 않았다. 새로 생성되는 보스에 적용되며, 이미 플레이 중인 개체의 HP 갱신은 보장하지 않는다. 프로필을 공유하는 직접 실행/튜토리얼의 기본 HP도 복원값을 따르며, 튜토리얼 배수 제외 규칙은 유지된다.
- Doc Impact Check: 기존 SessionLog 갱신. StructureMemory의 기존 baseline 및 배수 표와 일치하므로 추가 수정 없음. ErrorLog/DecisionLog 변경 없음. Architecture/Contracts 승격 및 Presentation HTML stale candidate 없음.
