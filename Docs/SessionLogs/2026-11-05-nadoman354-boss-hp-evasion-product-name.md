# 플레이어 기본 회피율, 보스 HP, 게임 제품명 변경

- [플레이어 초기 능력치](../../Assets/_Project/Data/Attributes/InitProfiles/PlayerAttributeOverrideProfile.asset)의 기본 회피율을 20%에서 2%로 낮췄다. 플레이어 프리팹이 쓰는 값만 바뀌며 유물·레벨 보상의 가산치는 그대로다.
- [Player Settings](../../ProjectSettings/ProjectSettings.asset)에서 제품명을 `Capstone_Project`에서 `Demon lord's Castle`로 바꿨다. Windows 저장 코드는 `Application.persistentDataPath`를 사용하므로 이전 제품명 폴더의 슬롯 파일은 새 이름에서 자동으로 발견되지 않을 수 있다. 저장 파일 이전은 수행하지 않았다.
- 각 보스가 사용하는 프로필의 시작 HP와 최대 HP를 함께 변경했다. 스테이지 배율과 그로기 수치는 그대로다.

| 보스 프로필 | 이전 HP | 변경 HP |
| --- | ---: | ---: |
| [슬라임 여왕](../../Assets/_Project/Data/Attributes/InitProfiles/Enemies/Bosses/SlimeQueenAttributeOverrideInitProfile.asset) | 1,000 | 2,000 |
| [슬라임 여왕 2페이즈 근거리](../../Assets/_Project/Data/Attributes/InitProfiles/Enemies/Bosses/SlimeQueenP2ShortAttributeOverrideInitProfile.asset) | 400 | 800 |
| [슬라임 여왕 2페이즈 원거리](../../Assets/_Project/Data/Attributes/InitProfiles/Enemies/Bosses/SlimeQueenP2LongAttributeOverrideInitProfile.asset) | 400 | 800 |
| [마녀](../../Assets/_Project/Data/Attributes/InitProfiles/Enemies/Bosses/WitchAttributeOverrideInitProfile.asset) | 1,500 | 3,000 |
| [드래곤](../../Assets/_Project/Data/Attributes/InitProfiles/DragonBossAttributeOverrideInitProfile.asset) | 2,000 | 4,000 |
| [마왕](../../Assets/_Project/Data/Attributes/InitProfiles/WitchBossAttributeOverrideInitProfile.asset) | 2,800 | 5,600 |

보스 프리팹 참조와 체력 속성 GUID를 대조했다. 마왕은 파일명에 `WitchBoss`가 들어 있는 프로필을 사용하며, 튜토리얼 마왕도 같은 프로필을 참조한다. [기존 보스 HP 검증값](../../Assets/_Project/Tests/PlayMode/Procedural/MonsterHealthAndWeaponSkillBalancePlayModeTests.cs)과 [회피율 누적 보상 검증값](../../Assets/_Project/Tests/PlayMode/Procedural/LevelRewardStatPlayModeTests.cs)을 새 밸런스에 맞췄다. 3%p 보상을 40번 받은 테스트의 회피율은 기본 2%를 더한 122%이며, 실제 회피 판정은 기존처럼 100%에서 제한된다.

변경 파일의 diff와 프리팹 연결을 확인했고, `ProceduralPlayModeTests.csproj` MSBuild가 성공했다. 자동 테스트, Unity 플레이 모드 및 플레이어 빌드는 실행하지 않았다. 문서 영향은 SessionLog이며 ErrorLog, DecisionLog, Presentation HTML 변경은 필요 없다.
