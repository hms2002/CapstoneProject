# 드랍 제외 아이템 정의 보관 구조

## 목적과 현재 위치

현재 무작위 획득 경로에서 제외한 무기·유물 정의를 `Assets/_Project/Unused/Items/Weapons`와 `Relics`에 모아 둔다. 2026-11-01 기준 무기 9개, 유물 7개다. 같은 `Unused` 아래 `Scenes`에는 별도로 판별한 미사용 프로젝트 씬 14개가 있다.

이 문서는 탐색용 구조 기억이며 Architecture/Contracts를 대체하지 않는다. 폴더 이동은 DB 등록 해제, 세이브 ID 폐기, Addressables 제거 또는 빌드 제외가 아니다.

## 판정과 데이터 소유권

- [LootPoolItemSelectionService](../../Assets/_Project/Runtime/Features/Loot/LootPoolItemSelectionService.cs)가 현재 무작위 획득 후보를 제한한다. 세 무기는 일반/보물 획득, Flowering/OddIron은 보물 획득에 허용되며 WindWeapon은 제외된다. 유물은 힘 부적·달리는 장부·하얀 깃털·속기사·한방울의 신속이 제외된다.
- 현재 ItemDatabase 미등록 정의 11개와 등록된 드랍 제외 유물 정의 5개를 보관한다. WindWeapon은 폴더 이동 후 사용자 요청으로 ItemDatabase 등록도 해제했다. 정의의 ID/GUID는 보존하며 나머지 아이템의 DB 등록은 변경하지 않았다.
- 현재 도감 Item 탭은 ItemDatabase를 직접 읽으므로 WindWeapon이 목록에서 제외된다. 기존 저장 데이터의 `Weapon.WindWeapon`은 DB를 통한 정의 조회/복원 대상에서 빠진다. 저장 데이터 자체는 수정하지 않았다.
- 효과 코드, 프리팹, 아이콘, Loadout, 스킬 데이터 및 Relic Strategies는 기존 위치다. 정의 위치만으로 종속 자산을 일괄 이동하거나 삭제하지 않았다.
- 배송 이벤트의 `Relic.Event.Parcel`은 실제 퀘스트에서 사용하므로 원위치에 남아 있다.

## 보관 무기

| 이름 | ID | 파일 | 분류 근거 |
| --- | --- | --- | --- |
| 사슬창 | `Weapon.ChainSpear` | [WD_ChainSpear.asset](<../../Assets/_Project/Unused/Items/Weapons/WD_ChainSpear.asset>) | ItemDatabase 미등록 |
| 월식도 | `Weapon.EclipseSword` | [WD_EclipseSword.asset](<../../Assets/_Project/Unused/Items/Weapons/WD_EclipseSword.asset>) | ItemDatabase 미등록 |
| 처형자의 대검 | `Weapon.ExecutionerGreatsword` | [WD_ExecutionerGreatsword.asset](<../../Assets/_Project/Unused/Items/Weapons/WD_ExecutionerGreatsword.asset>) | ItemDatabase 미등록 |
| 처형총 | `Weapon.ExecutionGun` | [WD_ExecutionGun.asset](<../../Assets/_Project/Unused/Items/Weapons/WD_ExecutionGun.asset>) | ItemDatabase 미등록 |
| 파편검 | `Weapon.FragmentBlade` | [WD_FragmentBlade.asset](<../../Assets/_Project/Unused/Items/Weapons/WD_FragmentBlade.asset>) | ItemDatabase 미등록 |
| 표식검 | `Weapon.MarkSword` | [WD_MarkSword.asset](<../../Assets/_Project/Unused/Items/Weapons/WD_MarkSword.asset>) | ItemDatabase 미등록 |
| 월영도 | `Weapon.MoonBlade` | [WD_MoonBlade.asset](<../../Assets/_Project/Unused/Items/Weapons/WD_MoonBlade.asset>) | ItemDatabase 미등록 |
| 태양도 | `Weapon.SunBlade` | [WD_SunBlade.asset](<../../Assets/_Project/Unused/Items/Weapons/WD_SunBlade.asset>) | ItemDatabase 미등록 |
| 잔영의 날개 | `Weapon.WindWeapon` | [WD_WindWeapon.asset](<../../Assets/_Project/Unused/Items/Weapons/WD_WindWeapon.asset>) | ItemDatabase 미등록; 공통 무작위 획득 후보에서도 제외 |

## 보관 유물

| 이름 | ID | 파일 | 분류 근거 |
| --- | --- | --- | --- |
| 피 부적 | `RD_BloodBonusRelic` | [RD_BloodBonusRelic.asset](<../../Assets/_Project/Unused/Items/Relics/RD_BloodBonusRelic.asset>) | ItemDatabase 미등록 |
| 번개 부적 | `Relic.New2` | [RD_LightningRelic.asset](<../../Assets/_Project/Unused/Items/Relics/RD_LightningRelic.asset>) | ItemDatabase 미등록 |
| 힘 부적 | `AttackBonusRelic` | [RD_AttackBonusRelic.asset](<../../Assets/_Project/Unused/Items/Relics/RD_AttackBonusRelic.asset>) | 공통 무작위 획득 후보에서 제외 |
| 하얀 깃털 | `RD_FeatherOrbit` | [RD_FeatherOrbit.asset](<../../Assets/_Project/Unused/Items/Relics/RD_FeatherOrbit.asset>) | 공통 무작위 획득 후보에서 제외 |
| 한방울의 신속 | `RD_OneDropOfSwiftness` | [RD_OneDropOfSwiftness.asset](<../../Assets/_Project/Unused/Items/Relics/RD_OneDropOfSwiftness.asset>) | 공통 무작위 획득 후보에서 제외 |
| 달리는 장부 | `RD_RunningLedger` | [RD_RunningLedger.asset](<../../Assets/_Project/Unused/Items/Relics/RD_RunningLedger.asset>) | 공통 무작위 획득 후보에서 제외 |
| 속기사 | `RD_Stenographer` | [RD_Stenographer.asset](<../../Assets/_Project/Unused/Items/Relics/RD_Stenographer.asset>) | 공통 무작위 획득 후보에서 제외 |

## 경로와 참조 주의점

- `RD_BloodBonusRelic`과 `Relic.New2`는 LoadingAddressableRegistry/BootCommon에 남아 있고 `Relic.New2`는 호감도 해금 효과 참조도 있다. 완전히 미참조인 유물로 해석하지 않는다.
- Addressables의 경로처럼 생긴 주소 키는 안정적인 로딩 키다. 자산 파일의 위치 변경만을 이유로 키를 함께 바꾸지 않았다.
- `FixedAttackRelicPlayModeTests`, `MonsterHealthAndWeaponSkillBalancePlayModeTests`, `FixedAttackRelicAuthoring`의 파일 경로는 새 위치를 사용한다.
- `RouteSetLoadManifestBuilderWindow`는 이동 당시 DB에 등록돼 있던 정의 6개의 새 경로도 Boot 생성 제외 목록에 포함하여 기존 정책을 유지한다.
- WindWeapon의 기존 EncyclopediaCatalog 항목과 LoadingManifest/Addressables 참조는 남아 있다. 현재 도감 Item 탭은 이 Catalog 항목을 사용하지 않으며, DB 등록 해제는 빌드에서 종속 자산을 제거하는 작업이 아니다.
- `CombatBalanceEditorWindow`는 활성 Definitions 폴더만 검색하므로 보관 무기는 목록 밖에 있다. 보관 자산을 다시 개발할 때 파일 위치·조회 코드·드랍 정책·DB 등록을 함께 확인한다.
- 원본 자산과 메타 60개 및 기존 폴더 메타 3개는 이동 전후 해시를 보존했다. 새 폴더 두 개만 새 메타를 가진다.

## 검증과 복원 자료

- [전체 목록·유지한 씬·코드 변경·검증 결과](../SessionLogs/2026-11-01-unused-assets/README.md).
- [현재 이동 manifest](../SessionLogs/2026-11-01-unused-assets/archive-manifest.json): 원래/새 경로, GUID, 해시.
- [과거 DB 미등록 정의 이동 기록](../../Samples/CsvData/archive-manifest.json): 과거 경로를 기록한 이력이며 현재 위치는 위 manifest를 따른다.
- 최초 폴더 이동에서는 Unity Refresh 및 MSBuild Editor/테스트 어셈블리 컴파일을 확인했다. 이후 WindWeapon DB 등록 해제는 DB diff·참조·자산/메타 해시를 확인했으며 Unity import/컴파일은 미실행. 두 작업 모두 자동 테스트·Play Mode·Player 빌드는 미실행.
- 복원할 때 자산과 원본 `.meta`를 함께 되돌린다. DB/Addressables 정리 여부는 별도 결정 대상이다.
- Architecture/Contracts 승격 후보는 아니다.
