# 전역 서비스 계획

## 기준

- 씬이 바뀌어도 같은 의미를 유지하는 상태인가
- 허브, 복도, 보스 중 둘 이상에서 같은 규칙으로 반복 사용되는가
- 시스템의 주인이 특정 씬이 아니라 게임 전체인가
- 씬마다 달라지는 것은 입력 데이터나 앵커뿐이고, 본체 규칙은 공통인가

## 전역 서비스로 확정

- `GameDataManager`
- `GamePlayDataManager`
- `CurrencyManager`
- `ItemManager`
- `NPCManager`
- `AffectionManager`
- `PortalRouteManager`
- `RunTransitionResolver`
- `SceneTransitionPolicyResolver`
- `LootManager`
- `DialogueController`
- `UIManager`
- `RewardDisplayUI`
- `WorldInteractionPromptController`
- `DamagePopupService`

## 로컬로 유지

- `UpgradeManager`
- `ScenePortal`
- `MonsterSpawner`
- `CameraPresentationDirector`
- `DialogueTrigger`
- `NPCFeatureController`
- 각 씬의 spawn point, grave, chest, boss encounter 오브젝트
- 씬 연출용 카메라

## 구조 원칙

- 전역으로 올라가는 것은 `서비스 본체`다.
- 씬에 남는 것은 `입력자`, `앵커`, `배치물`, `연출기`다.
- 전역화가 필요한데 현재 씬 참조가 많다면, 먼저 `전역 본체`와 `씬 로컬 어댑터`로 분리한다.

## 작업 순서 체크리스트

- [x] `GameDataManager` 자동 부트스트랩 추가
- [x] `AffectionManager` 자동 부트스트랩 추가
- [x] `ItemManager` 자동 부트스트랩 1차 적용
  - 씬에 남아 있는 `ItemManager`가 있으면 `ItemDatabase`를 기존 전역 인스턴스에 넘기고 정리
  - 초기화 순서가 꼬여도 저장 데이터가 나중에 적용될 수 있게 보정
- [x] `NPCManager` 자동 부트스트랩 1차 적용
  - 씬에 남아 있는 `NPCManager`가 있으면 `NPCDatabase`를 기존 전역 인스턴스에 넘기고 정리
- [ ] 현재 자동 부트스트랩/전역화 상태 점검
- [x] `RunTransitionResolver`, `SceneTransitionPolicyResolver`의 전역 공급 방식 1차 정리
  - 룰 데이터를 어디서 주입할지 결정
- [ ] 전역 UI 루트 설계
  - `UIManager`
  - `RewardDisplayUI`
  - `WorldInteractionPromptController`
  - `DialogueController`
- [ ] `LootManager`를 전역 본체와 씬 로컬 입력자로 분리
  - 전역: 드랍 계산, 해금 풀, 희귀도 규칙
  - 로컬: 테이블 공급, 프리팹, 드랍 포인트
- [ ] `MonsterSpawner`는 전역화하지 않고 전역 난이도/규칙과 분리할지 검토
- [ ] `UpgradeManager`는 허브 로컬 유지 전제로 정리 지속

## 메모

- `전역 서비스`는 "모든 씬에서 항상 사용"을 뜻하지 않는다.
- 어떤 씬에서 잠시 사용하지 않더라도, 시스템의 소유가 게임 전체라면 전역 서비스로 본다.
- 허브는 예외 시스템이 일부 존재하지만, 그 외 다수 씬에서 반복되는 시스템은 전역화 우선순위가 높다.
> Legacy / Notes  
> 이 문서는 서비스 구조를 구상하던 시점의 계획 메모입니다.  
> 현재 표준 문서가 아니며, 과거 계획과 가정 확인 용도로만 유지합니다.
