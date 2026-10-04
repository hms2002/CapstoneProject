# ItemDatabase 미등록 정의 자산 보관

- 기준: 사용자가 명시한 ItemDatabase 등록 목록. 2026-09-28 이동 시 등록 무기 6개, 유물 66개.
- 위치: `Assets/_Project/Data/Items/Archive_NotInItemDatabase/Weapons` 및 `Relics`.
- 범위: 미등록 WeaponDefinition 8개, RelicDefinition 2개와 각각의 원본 .meta. 코드/프리팹/효과/Loadout 등 종속 자산은 기존 위치 유지.
- Assets 내부의 정리 폴더이며 빌드 제외나 사용 중단을 뜻하지 않는다. 보관만으로 Addressables/해금 등록을 제거하지 않는다.
- 원본 자산 및 .meta는 SHA-256이 이동 전후 동일하다. ID/GUID/필드/참조와 ItemDatabase는 수정하지 않았다.

## 현재 목록

| ID | 보관 파일 |
| --- | --- |
| RD_BloodBonusRelic | [RD_BloodBonusRelic.asset](../../Assets/_Project/Data/Items/Archive_NotInItemDatabase/Relics/RD_BloodBonusRelic.asset) |
| Relic.New2 | [RD_LightningRelic.asset](../../Assets/_Project/Data/Items/Archive_NotInItemDatabase/Relics/RD_LightningRelic.asset) |
| Weapon.SunBlade | [WD_SunBlade.asset](../../Assets/_Project/Data/Items/Archive_NotInItemDatabase/Weapons/WD_SunBlade.asset) |
| Weapon.MoonBlade | [WD_MoonBlade.asset](../../Assets/_Project/Data/Items/Archive_NotInItemDatabase/Weapons/WD_MoonBlade.asset) |
| Weapon.MarkSword | [WD_MarkSword.asset](../../Assets/_Project/Data/Items/Archive_NotInItemDatabase/Weapons/WD_MarkSword.asset) |
| Weapon.FragmentBlade | [WD_FragmentBlade.asset](../../Assets/_Project/Data/Items/Archive_NotInItemDatabase/Weapons/WD_FragmentBlade.asset) |
| Weapon.ExecutionGun | [WD_ExecutionGun.asset](../../Assets/_Project/Data/Items/Archive_NotInItemDatabase/Weapons/WD_ExecutionGun.asset) |
| Weapon.ExecutionerGreatsword | [WD_ExecutionerGreatsword.asset](../../Assets/_Project/Data/Items/Archive_NotInItemDatabase/Weapons/WD_ExecutionerGreatsword.asset) |
| Weapon.EclipseSword | [WD_EclipseSword.asset](../../Assets/_Project/Data/Items/Archive_NotInItemDatabase/Weapons/WD_EclipseSword.asset) |
| Weapon.ChainSpear | [WD_ChainSpear.asset](../../Assets/_Project/Data/Items/Archive_NotInItemDatabase/Weapons/WD_ChainSpear.asset) |

## 참조와 복원

- `RD_BloodBonusRelic`과 `Relic.New2`는 LoadingAddressableRegistry와 BootCommon Addressables 목록에 남아 있다.
- `Relic.New2`는 Effect_Affection_Unlock에서도 참조된다. 따라서 두 유물을 완전히 미사용이라고 단정하지 않는다. 등록 정리는 별도 결정 대상이다.
- 보관 위치에서도 GUID 참조는 유지된다. 복원 시 자산과 .meta를 함께 원래 위치로 이동하고, 필요할 때 ItemDatabase 등록을 별도로 검토한다.
- 원래 경로, GUID 및 해시: [archive-manifest.json](../../Samples/CsvData/archive-manifest.json).
- CSV 형식 견본: [README](../../Samples/CsvData/README.md).
- Unity import/Play Mode 확인은 미실행. 정적 참조/해시 검사만 수행.
- 이 문서는 탐색용 구조 기억이며 Architecture/Contracts 승격 대상은 아니다.
