# 드랍 제외 아이템과 미사용 씬 정리 — 2026-11-01

## 결과와 범위

사용자의 폴더 정리 요청에 따라 `Assets/_Project/Unused/`에 무기 정의 9개, 유물 정의 7개, 씬 14개를 모았다. 기존 `Archive_NotInItemDatabase`의 10개 정의를 합치고, 현재 공통 드랍 정책이 제외하는 등록 정의 6개를 추가했다. 기존 자산과 `.meta`는 내용 변경 없이 함께 이동했다.

여기서 아이템의 기준은 **현재 무작위 획득 후보에서 제외되는 정의**다. 모든 참조가 사라진 자산이라는 뜻은 아니다. ItemDatabase/도감 등록, 세이브 ID, 해금 데이터, Addressables 주소 및 GUID 참조는 유지했다. 폴더 이동 자체로 빌드에서 제외되지 않는다. 효과 코드, 프리팹, 아이콘, 스킬 데이터와 전략 자산은 다른 참조를 보존하기 위해 기존 위치에 두었다.

- 드랍 판정: [LootPoolItemSelectionService.cs](../../../Assets/_Project/Runtime/Features/Loot/LootPoolItemSelectionService.cs).
- 씬 판정: Build Settings 전체, 실제 로딩 경로, 자산 GUID/경로/씬 이름 참조, 유지 중인 편집 도구와 테스트 의존성을 확인했다.
- 원본 경로·이동 경로·GUID·SHA-256: [archive-manifest.json](archive-manifest.json).
- 기존 상점 보유 무기 중복 조사와 별개인 정리 작업이며 상점 코드는 변경하지 않았다.

## 무기 9개

| 이름 | ID | 파일 | 분류 근거 |
| --- | --- | --- | --- |
| 사슬창 | `Weapon.ChainSpear` | [WD_ChainSpear.asset](<../../../Assets/_Project/Unused/Items/Weapons/WD_ChainSpear.asset>) | ItemDatabase 미등록 |
| 월식도 | `Weapon.EclipseSword` | [WD_EclipseSword.asset](<../../../Assets/_Project/Unused/Items/Weapons/WD_EclipseSword.asset>) | ItemDatabase 미등록 |
| 처형자의 대검 | `Weapon.ExecutionerGreatsword` | [WD_ExecutionerGreatsword.asset](<../../../Assets/_Project/Unused/Items/Weapons/WD_ExecutionerGreatsword.asset>) | ItemDatabase 미등록 |
| 처형총 | `Weapon.ExecutionGun` | [WD_ExecutionGun.asset](<../../../Assets/_Project/Unused/Items/Weapons/WD_ExecutionGun.asset>) | ItemDatabase 미등록 |
| 파편검 | `Weapon.FragmentBlade` | [WD_FragmentBlade.asset](<../../../Assets/_Project/Unused/Items/Weapons/WD_FragmentBlade.asset>) | ItemDatabase 미등록 |
| 표식검 | `Weapon.MarkSword` | [WD_MarkSword.asset](<../../../Assets/_Project/Unused/Items/Weapons/WD_MarkSword.asset>) | ItemDatabase 미등록 |
| 월영도 | `Weapon.MoonBlade` | [WD_MoonBlade.asset](<../../../Assets/_Project/Unused/Items/Weapons/WD_MoonBlade.asset>) | ItemDatabase 미등록 |
| 태양도 | `Weapon.SunBlade` | [WD_SunBlade.asset](<../../../Assets/_Project/Unused/Items/Weapons/WD_SunBlade.asset>) | ItemDatabase 미등록 |
| 잔영의 날개 | `Weapon.WindWeapon` | [WD_WindWeapon.asset](<../../../Assets/_Project/Unused/Items/Weapons/WD_WindWeapon.asset>) | 공통 무작위 획득 후보에서 제외 |

## 유물 7개

| 이름 | ID | 파일 | 분류 근거 |
| --- | --- | --- | --- |
| 피 부적 | `RD_BloodBonusRelic` | [RD_BloodBonusRelic.asset](<../../../Assets/_Project/Unused/Items/Relics/RD_BloodBonusRelic.asset>) | ItemDatabase 미등록 |
| 번개 부적 | `Relic.New2` | [RD_LightningRelic.asset](<../../../Assets/_Project/Unused/Items/Relics/RD_LightningRelic.asset>) | ItemDatabase 미등록 |
| 힘 부적 | `AttackBonusRelic` | [RD_AttackBonusRelic.asset](<../../../Assets/_Project/Unused/Items/Relics/RD_AttackBonusRelic.asset>) | 공통 무작위 획득 후보에서 제외 |
| 하얀 깃털 | `RD_FeatherOrbit` | [RD_FeatherOrbit.asset](<../../../Assets/_Project/Unused/Items/Relics/RD_FeatherOrbit.asset>) | 공통 무작위 획득 후보에서 제외 |
| 한방울의 신속 | `RD_OneDropOfSwiftness` | [RD_OneDropOfSwiftness.asset](<../../../Assets/_Project/Unused/Items/Relics/RD_OneDropOfSwiftness.asset>) | 공통 무작위 획득 후보에서 제외 |
| 달리는 장부 | `RD_RunningLedger` | [RD_RunningLedger.asset](<../../../Assets/_Project/Unused/Items/Relics/RD_RunningLedger.asset>) | 공통 무작위 획득 후보에서 제외 |
| 속기사 | `RD_Stenographer` | [RD_Stenographer.asset](<../../../Assets/_Project/Unused/Items/Relics/RD_Stenographer.asset>) | 공통 무작위 획득 후보에서 제외 |

`RD_BloodBonusRelic`과 `Relic.New2`는 DB 미등록 상태지만 BootCommon/LoadingAddressableRegistry 참조가 남아 있고, `Relic.New2`는 호감도 해금 효과에서도 참조된다. 등록된 제외 아이템 6개도 DB에서 제거하지 않았다. 기존 Addressables `m_Address`/`addressKey`의 옛 경로 모양 문자열은 실제 파일 위치가 아닌 안정적인 주소 키이므로 변경하지 않았다.

`Relic.Event.Parcel`은 배송 이벤트가 사용하는 퀘스트 아이템이므로 남겼다. 보물상자 전용 무기 `Weapon.Flowering`과 `Weapon.OddIron`도 실제 획득 경로가 있으므로 남겼다.

## 이동한 씬 14개

| 씬 | 이동 전 폴더 | 이동 후 파일 |
| --- | --- | --- |
| DragonCorridor 1 | `Assets/_Project/Scenes` | [DragonCorridor 1.unity](<../../../Assets/_Project/Unused/Scenes/DragonCorridor 1.unity>) |
| HeoMinSeokScene | `Assets/_Project/Scenes` | [HeoMinSeokScene.unity](<../../../Assets/_Project/Unused/Scenes/HeoMinSeokScene.unity>) |
| LEeJunmo 1 | `Assets/_Project/Scenes` | [LEeJunmo 1.unity](<../../../Assets/_Project/Unused/Scenes/LEeJunmo 1.unity>) |
| LEeJunmo | `Assets/_Project/Scenes` | [LEeJunmo.unity](<../../../Assets/_Project/Unused/Scenes/LEeJunmo.unity>) |
| ProtoTypeBoss 2 | `Assets/_Project/Scenes` | [ProtoTypeBoss 2.unity](<../../../Assets/_Project/Unused/Scenes/ProtoTypeBoss 2.unity>) |
| ProtoTypeBoss 3 | `Assets/_Project/Scenes` | [ProtoTypeBoss 3.unity](<../../../Assets/_Project/Unused/Scenes/ProtoTypeBoss 3.unity>) |
| ProtoTypeBoss 4 | `Assets/_Project/Scenes` | [ProtoTypeBoss 4.unity](<../../../Assets/_Project/Unused/Scenes/ProtoTypeBoss 4.unity>) |
| ProtoTypeBossLagacy | `Assets/_Project/Scenes` | [ProtoTypeBossLagacy.unity](<../../../Assets/_Project/Unused/Scenes/ProtoTypeBossLagacy.unity>) |
| ProtoTypeCorridor 1 | `Assets/_Project/Scenes` | [ProtoTypeCorridor 1.unity](<../../../Assets/_Project/Unused/Scenes/ProtoTypeCorridor 1.unity>) |
| ProtoTypeCorridor 2 | `Assets/_Project/Scenes` | [ProtoTypeCorridor 2.unity](<../../../Assets/_Project/Unused/Scenes/ProtoTypeCorridor 2.unity>) |
| ProtoTypeCorridor 3 | `Assets/_Project/Scenes` | [ProtoTypeCorridor 3.unity](<../../../Assets/_Project/Unused/Scenes/ProtoTypeCorridor 3.unity>) |
| ProtoTypeHubasf | `Assets/_Project/Scenes` | [ProtoTypeHubasf.unity](<../../../Assets/_Project/Unused/Scenes/ProtoTypeHubasf.unity>) |
| SlimeCorridor 1 | `Assets/_Project/Scenes` | [SlimeCorridor 1.unity](<../../../Assets/_Project/Unused/Scenes/SlimeCorridor 1.unity>) |
| MainMenu | `Assets/Scenes` | [MainMenu.unity](<../../../Assets/_Project/Unused/Scenes/MainMenu.unity>) |

위 씬은 빌드 목록과 현재 씬 진입 경로에 없으며, 유지 중인 도구나 테스트에서 씬을 여는 의존성도 발견되지 않았다. 번호가 붙은 ProtoType 씬 6개는 `GameOverPresentationPlayback`의 씬 이름별 연출 분기에만 남아 있다. 이 분기는 씬을 로드하지 않으며 이동 후에도 파일 이름이 같아 동작 조건은 유지된다.

## 남긴 씬과 이유

| 대상 | 유지 근거 |
| --- | --- |
| 활성 Build Settings의 13개 씬 | 타이틀·튜토리얼·허브·보스·절차 복도·Grand Hall의 현재 진입 경로 |
| ProtoTypeBoss 1 | `SceneSmokePlayModeTests` |
| ProtoTypeCorridor | `TestSceneBuilder` |
| Tests/TestScene | 유지 중인 게임플레이 회귀·성능 검사와 테스트 UI |
| TutorialCorridor | `FullLocalizationValidation` 등 편집 도구 |
| PixelLightTest | 조명·카메라 기준 편집 도구 |
| ProceduralDungeonV0Test | `ProceduralDungeonSceneInstaller` |
| ShadowCorridor, DragonCorridor, SlimeCorridor | 절차 씬 생성·장식 도구의 원본 |
| ProceduralDemonkingCorridor | 현재 런에서는 제외되지만 `DemonKingHubPortalPlayModeTests` 및 씬/BGM 도구가 사용 |
| SangHyup_Hallway | 비활성 Build Settings 항목이며 `SceneBgmAuthoringUtility`가 파일 이름으로 사용 |
| 외부 패키지 예제, 복구 씬, URP 템플릿 | 프로젝트 게임 씬 정리 범위 밖 |

## 함께 수정한 코드

| 파일 | 이유 |
| --- | --- |
| [FixedAttackRelicPlayModeTests.cs](../../../Assets/_Project/Tests/PlayMode/Procedural/FixedAttackRelicPlayModeTests.cs) | 하얀 깃털 정의의 경로 조합을 새 위치로 변경 |
| [MonsterHealthAndWeaponSkillBalancePlayModeTests.cs](../../../Assets/_Project/Tests/PlayMode/Procedural/MonsterHealthAndWeaponSkillBalancePlayModeTests.cs) | 힘 부적·하얀 깃털 파일 조회 경로 변경 |
| [FixedAttackRelicAuthoring.cs](../../../Assets/_Project/Editor/Tools/Data/FixedAttackRelicAuthoring.cs) | 기존 유물 편집 도구가 하얀 깃털을 계속 찾도록 경로 변경 |
| [RouteSetLoadManifestBuilderWindow.cs](../../../Assets/_Project/Editor/Build/Loading/RouteSetLoadManifestBuilderWindow.cs) | 기존 정의 폴더에서 옮긴 6개에 대해 Boot 목록 생성 시 기존 제외 정책 유지 |

런타임 로직·직렬화 필드·enum·asmdef·씬 내용·프리팹·기존 GUID는 변경하지 않았다. Boot 생성기의 새 제외 경로는 이번에 옮긴 6개만 지정하여, 기존 별도 보관 정의 10개의 정책까지 바꾸지 않는다. 새 폴더 `Unused`와 `Unused/Scenes`에는 폴더 메타를 만들었고, 기존 보관 폴더와 Weapons/Relics 하위 폴더의 메타 3개는 보존했다.

## 검증과 한계

- [무결성 결과](integrity-check.json): 자산 30개와 메타 30개, 기존 폴더 메타 3개, DB/BuildSettings/Addressables/드랍/상점/유지 씬 등 보호 파일 38개의 이동 전 해시를 비교했다. 기존 위치에 자산·메타가 남지 않는지와 이동 GUID의 유일성도 확인했다.
- MSBuild `ProceduralPlayModeTests.csproj /t:Build /p:Configuration=Debug`: exit 0. [컴파일 로그](msbuild-tests-assembly.txt).
- MSBuild `Editor.csproj /t:Build /p:Configuration=Debug`: exit 0. [컴파일 로그](msbuild-editor.txt). 두 컴파일에는 기존 코드의 경고가 있으며 경고 없는 빌드를 뜻하지 않는다.
- Unity에서 에셋 Refresh를 요청했고 도메인 리로드와 Unused 폴더 import를 확인했다. Auto Refresh는 작업 전 값인 `Enabled Outside Playmode`로 복원했다. [Refresh 로그 발췌](unity-refresh-summary.txt).
- Unity의 기존 ChestPanel OnValidate 경고와 assembly skipped 메시지가 있어 깨끗한 Console을 주장하지 않는다.
- 자동 테스트, Play Mode 진입, 수동 게임 플레이와 Player 빌드는 실행하지 않았다. 기존 테스트 파일은 경로만 수정했으며 테스트 실행 결과를 주장하지 않는다.

## 후속 판단과 복원

- `CombatBalanceEditorWindow`는 활성 무기 Definitions 폴더를 검색하므로 보관한 잔영의 날개는 그 목록에서 빠진다. 보관 정의는 새 폴더의 Inspector에서 확인할 수 있다.
- Suggested Later: 도감에서도 숨기거나 빌드 포함을 제거하려면 DB/해금/Addressables/세이브 호환성까지 별도 범위로 검토해야 한다. 이번 작업에서는 등록과 실제 로딩 참조를 유지했다.
- 복원은 manifest의 `destination`을 `source`로 자산과 `.meta`를 함께 이동한다. 기존 보관 폴더 메타도 원위치로 돌리고, 위 4개 코드의 경로 보정만 함께 되돌린다. 다른 작업의 변경 사항은 되돌리지 않는다.

Doc Impact Check: SessionLog + 기존 StructureMemory 갱신. 이번 작업에서 ErrorLog/DecisionLog는 변경하지 않았고 Architecture/Contracts 및 Presentation HTML 영향은 없다.
