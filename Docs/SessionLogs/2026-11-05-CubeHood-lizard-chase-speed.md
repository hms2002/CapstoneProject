# 리자드 전사 광폭 추격 속도 1.3배 조정

- 작성일: 2026-11-05. 작성자 GitHub 로그인: CubeHood. [기존 사용자 제공 확인 기록](2026-11-05-CubeHood-relic-shot-audio.md)을 근거로 사용했다.
- Mode: Micro-fix. 사용자의 좁은 공간 플레이 피드백에 따라 광폭 추격 속도를 플레이어 기본 이속의 1.4배에서 1.3배로 낮췄다.
- [AL_LizardWarriorCharge.asset](../../Assets/_Project/Data/Abilities/Strategies/Monsters/CommonCorridor/AL_LizardWarriorCharge.asset): 게임에 적용하는 chaseSpeedMultiplier를 1.3으로 변경.
- [AbilityLogic_LizardWarriorCharge.cs](../../Assets/_Project/Runtime/Features/Monsters/Common/Abilities/AbilityLogic_LizardWarriorCharge.cs): 새 에셋 생성 시 기본값도 1.3으로 정렬. 직렬화 필드/타입/namespace/assembly 및 생명주기는 유지.
- [BossAndMobEncounterStructure.md](../StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md): 현행 전사 추격 속도 설명을 1.3배로 정렬. 기존 문맥/다른 작업 내용 보존.
- 발동 거리 3.74와 광폭 시간 2.2초 등 다른 수치는 유지했다. 소스/에셋 diff는 속도 한 줄씩이며, 기본값/실제 적용값 일치와 공백을 확인했다.
- Gameplay.csproj MSBuild exit 0. 기존 obsolete/unused 경고가 있다. [컴파일 기록](../../outputs/lizard-combat-2026-11-05/msbuild-warrior-1p3.log).
- 자동 테스트, Unity import/Editor 컴파일, 수정 후 Play Mode/수동 전투 및 Player build는 미실행. 좁은 공간 회피 체감은 사용자 재확인이 필요하다.
- Doc Impact Check: SessionLog + 기존 StructureMemory 수치 정렬. ErrorLog/DecisionLog 변경 없음. Architecture/Contracts 승격 및 Presentation HTML 변경/새 stale candidate 없음.
