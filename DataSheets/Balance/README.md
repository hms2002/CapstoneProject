# 밸런스 시트 편집과 적용

현재 적용 입력은 이 폴더의 CSV 3개입니다. 게임에서는 기존 자산과 생성된 골드 상수 코드를 읽습니다. 실행 중 CSV를 읽지 않습니다.

## 편집 절차

1. `outputs/balance-authoring-2026-10-03/BalanceAuthoring.xlsx`의 입력 탭을 편집합니다. `Analysis`는 입력 연동 계산과 그래프이며 내보내지 않습니다.
2. 수정한 입력 탭을 **CSV UTF-8(쉼표 구분)** 으로 각각 저장합니다. 이 폴더의 동일한 파일을 덮어씁니다. XLSX 수정만으로 게임이나 CSV가 자동 갱신되지는 않습니다.
3. Unity에서 `Tools > Balance Sheets > Validate CSV`로 검증/변경 내역을 확인합니다.
4. `Apply CSV`를 실행합니다. 변경이 있으면 내역을 보여준 뒤 적용합니다. 값이 같으면 저장하지 않습니다.
5. 골드 변경이 있으면 컴파일이 완료된 뒤 플레이 확인합니다. Play Mode/컴파일 중 적용은 거부합니다.

## 입력과 현재 연결

| CSV | 키/의미 | 적용 위치 |
| --- | --- | --- |
| StageHealth.csv | enabled(0/1), hpMultiplierPerClearedStage | 기존 MonsterStageHpScalingSettings.asset의 해당 두 필드 |
| LevelExperience.csv | fromLevel, requiredExperience | 기존 LevelProgressionConfig.asset의 nextLevelRequirements |
| GoldEconomy.csv | 일반/대형 방 예산, 상품별 가격 최소/최대 | BalanceGoldValues.g.cs 생성; MonsterSpawnRoomGroup/ShopDefinitionSO 참조 |

원본 경로:
- `Assets/_Project/Resources/MonsterStageHpScalingSettings.asset`
- `Assets/_Project/Runtime/Core/Scaling/MonsterStageHpScalingSettings.cs`
- `Assets/_Project/Data/Progression/Leveling/LevelProgressionConfig.asset`
- `Assets/_Project/Runtime/Features/Monsters/Spawning/MonsterSpawnRoomGroup.cs`
- `Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/ShopDefinitionSO.cs`

스테이지 표시 1/2/3은 코드의 index 0/1/2입니다. 현재 3스테이지 추가 2배 규칙은 코드에 유지되어 시트 입력 대상이 아닙니다. enabled는 기존 설정 전체의 활성 필드이므로 끄면 공격 템포 보정도 함께 꺼집니다. 몬스터 기본 HP, 엘리트/난이도 배율, 배치 정보는 이번 적용 범위가 아닙니다.

fromLevel은 1부터 중복 없이 연속이어야 합니다. 행 순서는 키로 복원합니다. EXP는 양의 정수이고, 행 수가 바뀌면 최대 레벨도 바뀝니다. 경험치 시트는 설정값이며 진행 중 레벨/EXP를 저장하지 않습니다.

골드 가격의 최대값은 포함입니다. 기존 `Random.Range(min, max + 1)`과 10 단위 반올림을 유지합니다. 따라서 10 단위가 아닌 입력값은 표시 가격에서 반올림될 수 있습니다. 방 예산은 기본 EXP 가중치로 몬스터들에게 분배되며 개별 몬스터의 고정 드롭량이 아닙니다.

## 검증과 복구

- UTF-8 BOM, Excel의 숫자 필드 따옴표, LF/CRLF 지원. 두 열 이외의 메모/추가 열, 중복/누락 키, 잘못된 헤더, NaN/음수, 정수 범위 초과, 가격 min > max는 거부합니다.
- 모든 입력을 검증하고 대상 필드 존재를 확인한 뒤 적용합니다. 변경 대상에 저장하지 않은 Inspector 편집이 있으면 먼저 저장하도록 안내합니다.
- 자산 변경은 Undo를 지원합니다. Undo 이후 저장 상태와 CSV 불일치를 확인하세요. 생성된 골드 코드의 복구는 버전 관리로 합니다. 적용 중 저장 실패 시 세 대상 파일을 적용 직전 내용으로 복원합니다.
- .g.cs는 직접 편집하지 않습니다. 생성된 결과와 CSV를 함께 버전 관리합니다.
- 현재 값으로 실행해도 밸런스는 동일합니다. Unity Import/Undo/Play Mode 실검증은 별도입니다.

자동 검증: 프로젝트 루트에서 `dotnet run --project Tools/Validation/BalanceCsv/BalanceCsvValidation.csproj -- .`

이전 `Samples/CsvData/`와 `outputs/data-localization-sample-2026-10-02/`는 연결되지 않은 견본입니다. 실제 적용 입력과 혼용하지 않습니다.
