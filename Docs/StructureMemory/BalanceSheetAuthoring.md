# Balance Sheet Authoring

2026-10-03. 데이터 시트에서 설정을 편집해 기존 게임 데이터에 반영하는 편집기 흐름. 공식 Architecture/Contracts가 아닌 구현 구조 지도.

## 현재 구조

- 입력 원본: `DataSheets/Balance/StageHealth.csv`, `LevelExperience.csv`, `GoldEconomy.csv`.
- XLSX 편집/분석: `outputs/balance-authoring-2026-10-03/BalanceAuthoring.xlsx`. 입력 탭을 CSV로 내보낸 뒤 Unity 메뉴로 적용한다. 자동 동기화 없음.
- `Assets/_Project/Editor/Tools/Data/BalanceCsvDocument.cs`: 순수 C# 숫자 CSV 검증, 레벨 키 복원, 골드 코드 생성.
- `BalanceSheetImportTool.cs`: 메뉴, 전체 사전검증/변경 내역, 기존 자산의 SerializedObject 적용, 개별 저장/Undo, 실패 시 파일 복원.
- `Assets/_Project/Runtime/Core/Scaling/BalanceGoldValues.g.cs`: 상태 없는 생성 상수. Core → Gameplay 기존 참조 방향 사용.
- `MonsterSpawnRoomGroup.cs`: 방 골드 상수를 생성 상수에 연결. `ShopDefinitionSO.cs`: 런 골드 가격 범위를 생성 상수에 연결; 무작위 범위/반올림 유지.

## 소유권 / 주의점

- 게임 실행은 기존 체력/경험치 자산과 생성 코드 사용. CSV는 런타임 저장 데이터가 아니다.
- 새 Manager, bootstrap, Resources 경로, 직렬화 스키마, prefab/scene 변경 없음.
- 체력 설정 enabled는 공격 템포를 포함하는 기존 활성 플래그. 3스테이지 추가 2배, 기본 몬스터 HP는 CSV 적용 범위 밖.
- EXP 행 수가 최대 레벨을 결정하므로 길이 변경은 게임 디자인 검토 필요.
- 골드 변경은 재컴파일이 필요하다. 자산은 Undo, 생성 코드는 Git으로 복원. CSV/생성 결과를 함께 관리한다.
- 파일 문법 및 사용 절차: `DataSheets/Balance/README.md`.
- 검증 실행: `Tools/Validation/BalanceCsv/BalanceCsvValidation.csproj` (Unity 엔진 외부).

확장 시 기존 importer의 명시적 필드 연결과 검증을 추가한다. 임의 자산 경로/필드명을 CSV에서 받아 쓰는 범용 importer는 없다. Unity 메뉴 적용/Undo/Play Mode는 아직 실행되지 않았다.

현재 편집기 적용 흐름은 이후 authoring guide 승격 후보. 공식 문서/Presentation 수정은 이번 범위 아님.
