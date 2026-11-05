# 클로에 2단계 해금 보상 3종 확장

- 작성일: 2026-11-05. 작성자 GitHub 로그인: `CubeHood`(사용자 제공). 같은 날짜의 공유 로그는 수정하지 않았다. 같은 작성자의 후속 작업은 이 파일에 추가한다.
- Mode: Micro-fix / 승인된 기존 보상 데이터 확장. [ActiveTask](../ActiveTasks/melta-chloe-tier-two-unlocks.md)의 Chloe 후속 범위를 따른다.

## 변경과 이유

- [ChloeRelicUnlockReward.asset](../../Assets/_Project/Data/Dialogue/Affection/ChloeRelicUnlockReward.asset): 기존 클로에의 등불을 유지하고 그림자 잔향(`RD_ShadowEcho`), 그림자 혼불(`RD_ShadowWisp`)을 추가해 총 3개로 구성했다. 기존 효과 타입/GUID/호감도 2단계 연결은 그대로다.
- [GameText.csv](../../DataSheets/Localization/GameText.csv), [AssetInventory.json](../../DataSheets/Localization/AssetInventory.json), `Assets/_Project/Data/Localization/GameText_{ko,en,ja,zh-Hans,zh-Hant}.asset` 5개: 기존 rewardText 키와 entry ID를 유지하며 세 유물 이름 및 해금 안내를 5개 언어로 동기화했다. 한국어는 “클로에의 등불, 그림자 잔향, 그림자 혼불이 해금되었습니다. 이제 상자와 상점에서 발견할 수 있습니다.”다.
- [DialogueNpcAffectionStructure](../StructureMemory/ScriptSystems/DialogueNpcAffectionStructure.md), [RelicRuntimeStructure](../StructureMemory/ScriptSystems/RelicRuntimeStructure.md), [ActiveTask](../ActiveTasks/melta-chloe-tier-two-unlocks.md), [TaskIndex](../TaskIndex.md): 현재 보상이 멜타 1종/클로에 3종임을 맞추고 기존 프로필 복원 경로와 후속 확인 범위를 기록했다. [DecisionLog](../DecisionLog.md)에는 클로에 보상 수량 확정을 추가했다.
- 새 유물은 이미 allRelics에 등록되고 기본 해금에서 제외되어 있어 ItemDatabase/정의/효과/아이콘은 수정하지 않았다. 기존 UnlockItemAffectionEffect 및 ItemManager.RestoreEarnedAffectionUnlocks가 목록 전체를 읽으므로 런타임 C#/저장 스키마/새 키/Shared Data/메타/GUID/UI 프리팹 변경은 없다. 기존 호감도 2 이상 프로필도 빠진 두 유물 해금을 목록 조회 시 복원하며, 소급 팝업·현물 지급·재고 재추첨은 추가하지 않았다.

## 검증과 남은 확인

- 새 두 정의의 표시 이름/ID/meta GUID와 DB allRelics 1건씩/default 0건, 일반 드롭 제외 조건, 기존 복원/보상 슬롯 경로를 소스로 확인했다.
- 효과의 유물 GUID가 등불→잔향→혼불 순서로 3개임을 확인했다. 기존 reward GUID `98bab4e87b4e423483f4b55d17eee8b5`, 지역화 키 `asset.98bab4e87b4e423483f4b55d17eee8b5.rewardText` 및 entry ID `11841048591966208`을 유지했다.
- CSV/Inventory의 대상 키 각 1건, 5개 locale과 CSV 값 일치, 기존 locale의 다른 내용 및 다른 CSV/Inventory 레코드 보존, scoped diff/공백과 문서 로컬 링크를 확인했다. 적용 전 자료와 변경 파일 목록: `Library/CodexChloeThreeRelics20261105/`.
- C# 변경이 없어 MSBuild는 실행하지 않았다. 자동 테스트·Unity import/Editor 컴파일·실제 보상 창 3개 아이콘/툴팁/줄바꿈·획득 추첨·기존 고호감도 프로필 저장 재실행·Player build는 미실행이다. 실제 화면과 아이템 등장/저장 유지는 후속 수동 확인 대상이다.
- Doc Impact Check: SessionLog + 기존 StructureMemory + DecisionLog + ActiveTask/TaskIndex. ErrorLog 추가 변경 없음. Architecture/Contracts/Presentation HTML 변경 및 새 HTML stale candidate 없음.
