# 회전하는 철구 / 광전사의 보석 효과 교환

- 작성일: 2026-11-05. 작성자 GitHub 로그인: `CubeHood` (동일 사용자 제공 ID 확인).
- Mode: Implementation. 범위는 사용자가 지적한 처치 유물 두 종의 효과 역전을 교정하는 것이다. 일반 철구는 4초 공격력 +2, 희귀 보석은 4초 공격력 +2/이동속도 +10%로 배치한다. 기존 ID/아이콘/등급/최대 레벨/발동 조건/버프 갱신 정책을 보존한다.

## 변경과 이유

- [RD_RedContract.asset](../../Assets/_Project/Data/Items/Relics/Definitions/RD_RedContract.asset), [RD_RotatingIornBall.asset](../../Assets/_Project/Data/Items/Relics/Definitions/RD_RotatingIornBall.asset): 기존 logic 참조와 설명만 맞바꿨다. 희귀 광전사의 보석은 `Relic Logic_Move Speed On Kill_Managed`, 일반 철구는 `Relic Logic_Common_RedContract`를 사용한다. 두 전략은 이 두 정의에서만 참조되며 기존 전략 자체/공용 런타임 로직/HUD 상태 정의는 수정하지 않았다. 공격력 Flat +2, 이동속도 Percent +10%, 4초, 중첩 없이 갱신이라는 기존 동작을 재사용한다.
- [GameText.csv](../../DataSheets/Localization/GameText.csv), [AssetInventory.json](../../DataSheets/Localization/AssetInventory.json), `Assets/_Project/Data/Localization/GameText_{ko,en,ja,zh-Hans,zh-Hant}.asset`: 두 정의의 기존 설명 키 값만 교환했다. 이름과 지역화 키/entry ID는 유지한다. 실제 효과 툴팁은 연결된 전략의 기존 지역화 템플릿을 사용하므로 템플릿 키/내용은 바꾸지 않았다.
- [FixedAttackRelicAuthoring.cs](../../Assets/_Project/Editor/Tools/Data/FixedAttackRelicAuthoring.cs): 전략 경로/GUID 대응, 설명, 템플릿과 타입별 분기를 새 배치에 맞췄다. 기존 제작/읽기 진입점이 과거 연결을 요구하거나 잘못된 설명을 쓰지 않게 했다. 이 제작 도구를 실제 실행하지는 않았다.
- [RelicRuntimeStructure.md](../StructureMemory/ScriptSystems/RelicRuntimeStructure.md), [DecisionLog.md](../DecisionLog.md): 현재 유물별 효과/기존 내부 파일명과 실제 연결의 차이를 기록했다. 이 SessionLog는 전체 변경/검증 범위를 보관한다.

## 검증과 남은 확인

- 작업 직전 스냅샷 대비 두 정의에서 logic/description 이외 바이트가 보존됨을 확인했다. 각 locale에서 두 설명 scalar가 서로 교환되고 나머지 바이트가 보존됨을 확인했다. CSV와 Inventory의 다른 레코드, 두 전략의 기존 수치/킬 태그/갱신 동작, 템플릿 토큰, 참조 대상 meta GUID와 제작 도구 분기를 소스/참조/diff로 확인했다.
- 수정한 C#은 기존 Editor.csproj에 포함되어 있으며 Editor.csproj MSBuild exit 0. 기존 경고 존재. 기록과 스냅샷: `Library/CodexKillRelicSwap20261105/msbuild.log`, `before/`.
- 자동 테스트, Unity import/Editor 컴파일, 제작 도구 실행, Play Mode/실제 처치 후 스탯/HUD/툴팁 확인, Player build는 실행하지 않았다. 이미 장착되어 실행 중인 proc은 즉시 교체하는 별도 코드를 추가하지 않았으므로 테스트 재시작 후 확인한다. 기존 프로필/런 저장 데이터는 수정하지 않았다.
- Doc Impact Check: SessionLog + 기존 StructureMemory + DecisionLog. ErrorLog 추가 변경 없음. Architecture/Contracts/Presentation HTML 미수정, 새 HTML stale candidate 없음.
