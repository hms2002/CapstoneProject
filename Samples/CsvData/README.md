# CSV 데이터 견본

추가 견본: [상점·Loot 설정 CSV 6개](ShopLoot/README.md).

2026-09-28 기준. **견본이며 게임에 연결되지 않았습니다.** 기존 ID와 자산 수치를 읽어서 작성했습니다. 영어는 검토 전 번역 초안입니다. UTF-8 BOM, 쉼표 구분, 큰따옴표 이스케이프를 사용합니다.

| 대상 | 파일 | 포함 범위 |
| --- | --- | --- |
| 기묘한 쇳덩이 | [OddIron.csv](Balance/Weapons/OddIron.csv) | 탄약 기본값과 사격/전탄난사 수치 7개 |
| 개화 | [Flowering.csv](Balance/Weapons/Flowering.csv) | 개화 강화와 대시 수치 10개 |
| 굳센 발걸음 | [FirmStep.csv](Balance/Relics/FirmStep.csv) | 1~3레벨, 중첩당 치명타 확률 및 최대 중첩 |
| 최후의 저항 | [LastStand.csv](Balance/Relics/LastStand.csv) | 1~3레벨, 효과 발동 체력 기준 |
| 무기 설명 1개 | [WeaponDescription.csv](Localization/WeaponDescription.csv) | 기묘한 쇳덩이 storyText 한국어/영어 |
| 유물 설명 1개 | [RelicDescription.csv](Localization/RelicDescription.csv) | 최후의 저항 효과 템플릿 한국어/영어 |
| NPC 1개 | [Merchant_1001.csv](Localization/Dialogue/Merchant_1001.csv) | 사샤(ID 1001)의 대사 6개와 선택지 2개 |

## 무기 형식

`weaponId,section,key,value,note`: 무기별 파일 안에서 한 행은 하나의 수치입니다. `(weaponId, section, key)`는 중복되지 않습니다. 공통 기능이나 동일한 키 구성을 강요하지 않습니다. 자료형과 허용 범위는 향후 무기별 가져오기 코드가 정의합니다.

이 견본은 선택한 기능의 실제 수치이며 무기의 모든 공격/스킬/VFX 설정을 전부 내보낸 것은 아닙니다. 상세한 필드 연결은 [source-map.json](source-map.json)에 있습니다. 원본 자산의 참조/런타임 상태 소유권은 바뀌지 않습니다.

## 유물 형식

`relicId,level,...전용 인자`: 한 행은 해당 레벨의 최종 설정입니다. `(relicId, level)`로 식별합니다. 이전 레벨과 합산하지 않습니다.

- 굳센 발걸음: `chancePerStack`은 0~1 확률이며 0.02는 2%p입니다. 게임 중 비치명타 적중으로 쌓이는 중첩당 값이고, 레벨 간 누적 증가량이 아닙니다. 최대 중첩은 모든 레벨에서 8입니다.
- 최후의 저항: `healthThreshold`는 HP 절댓값입니다. 각 레벨에서 HP가 1/2/3 이하일 때의 조건값입니다. 효과의 생존 조건과 치명타 보장 동작은 기존 코드 소유입니다.
- 현재 두 유물 모두 실제 자산에 레벨별 배열이 있으므로 계산값을 임의로 만든 것이 아닙니다. 향후 가져오기에서는 최대 레벨과 연속된 레벨 행의 일치 여부를 검증합니다.

## 설명과 Ink

무기 설명 원본은 `Assets/_Project/Data/Items/Weapons/Definitions/WD_OddIron.asset`의 `storyText`입니다. 색상 태그를 유지했습니다.

유물 설명은 `Assets/_Project/Data/Items/Relics/Strategies/RelicLogic_LastStand.asset`의 `effectTemplate`에서 가져왔습니다. `{threshold}`에 현재 레벨의 `healthThreshold`를 전달하는 연결이 필요합니다. CSV에 플레이어 상태를 저장하지 않습니다.

NPC 원본은 `Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink`입니다. [태그를 붙인 Ink 견본](Ink/Merchant_1001.sample.ink)은 원문과 선택지/분기/기존 태그를 유지하고 `loc:`만 추가한 사본입니다. 실제 Ink 자산과 NPC 연결은 수정하지 않았습니다.

`dialogue.merchant.001` 등의 ID는 견본에서 처음 부여한 고정 ID입니다. 향후 문장 삽입/재정렬 시 번호를 재생성하지 않습니다. CSV의 한국어는 Ink 원문의 추출 사본, 영어는 번역 편집 값이라는 구성입니다. 아직 추출기/번역 표시 코드가 구현된 것은 아닙니다.

## 확인 결과와 다음 구현 경계

- CSV 7개의 재읽기, 행/열 값 일치, 기존 등록 ID, 유물 레벨 연속성, 한국어 원문 및 변수 토큰을 확인했습니다.
- 게임 데이터/스키마/밸런스는 수정하지 않았습니다. Unity 컴파일, Ink 컴파일 및 Play Mode 검증은 실행하지 않았습니다.
- 형식을 확정하면 다음 작업은 CSV 가져오기와 검증, 설명 번역 연결, Ink 태그 번역 연결로 나눌 수 있습니다.
- 보관 결과는 [아이템 보관 구조](../../Docs/StructureMemory/ItemDefinitionArchive.md)를 참조하세요.
