# 아이템 도감 승인 디자인 적용 — 2026-11-02

후속 사용자 화면에서 허브 씬의 잔여 배치 오버라이드와 아이콘 위치·글씨 문제가 확인됐다. 현재 상태는 [배치·가독성 수정 기록](../encyclopedia-item-layout-fix-2026-11-02/README.md)을 따른다. 무기 능력치는 사용자 요청으로 중앙 아이콘 아래로 이동했다. 아래 내용은 최초 적용 기록이다.

사용자의 마지막 견본 적용 요청에 따른 Implementation. 무기는 [revision 11](../encyclopedia-item-redesign-preview-2026-11-02/revision-11-option28-icon-grid/README.md), 소모품은 [revision 13](../encyclopedia-item-redesign-preview-2026-11-02/revision-13-centered-icons/README.md), 유물은 [revision 14](../encyclopedia-item-redesign-preview-2026-11-02/revision-14-relic-level-below-icon/README.md)를 적용했다.

## 적용 내용

- 같은 크기의 양쪽 페이지, 왼쪽 4열 아이콘 그리드와 오른쪽 정보 구성을 유지했다. 밝은 종이, 픽셀 테두리, 선택 표시를 Aseprite 원본과 스프라이트 시트로 제작해 연결했다.
- 무기: 이름 → 아이콘·별도 능력치 영역 → 스토리 → 스킬 아이콘/이름/입력/재사용 대기시간/설명. 기존 상세 설명 전환을 유지했다.
- 유물: 이름 → 중앙 아이콘 → 제목 없는 중앙 `Q · Lv. 현재 / 최대 · E` → 효과. 효과는 기존 레벨별 툴팁에서 가져온다.
- 소모품: 이름 → 중앙 아이콘 → 효과. 긴 설명은 오른쪽 페이지 안에서 스크롤한다.
- Galmuri9와 기존 글자색 역할을 유지하면서 불투명 갈색 외곽선 및 일반 글자 재질을 분리했다. 언어별 폰트 이벤트에는 같은 저작 스타일을 연결했다. 새 능력치 제목은 5개 언어 테이블과 CSV에 추가했다.
- 아이콘 기본 변환은 한 번만 저장한다. 무기별 아이콘 보정값이 다음 항목으로 누적되지 않는다. 그리드 아이콘의 여백은 부모 마스크에 두어 기존 런타임 Stretch 처리와 충돌하지 않게 했다.
- 미획득 무기·유물 실루엣/정보 숨김, 도감 데이터·레벨·입력·선택 소유권, 기존 슬롯/스킬 프리팹 풀을 유지했다. 새 런타임 UI 생성 경로를 추가하지 않았다.

## 변경 파일과 이유

| 파일 | 변경 이유 |
| --- | --- |
| [EncyclopediaUI.prefab](../../Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab) | 양면 종이, 목록, 종류별 정보 영역, 스크롤과 입력 안내를 승인 배치로 저작 |
| [EncyclopediaEntrySlot.prefab](../../Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaEntrySlot.prefab) | 88×88 아이콘 전용 셀과 선택/호버 테두리 |
| [Panel_AbilityBlock_Encyclopedia.prefab](../../Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/Panel_AbilityBlock_Encyclopedia.prefab) | 연속된 스킬 제목 행과 본문, 기존 데이터 참조 유지 |
| [EncyclopediaItemSkin](../../Assets/_Project/Art/Sprites/UI/EncyclopediaItemSkin/) | PNG 12개 스프라이트, 편집 가능한 Aseprite, 일반/외곽선 TMP 재질 및 Unity 메타데이터 |
| [EncyclopediaItemRightPage.cs](../../Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaItemRightPage.cs) | 소모품 효과 전용 영역 연결, 레벨 표기, 아이콘 기본값 보존 |
| [GlobalUIRoot.prefab](../../Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab), 아래 12개 씬 | 새 소스 프리팹을 덮어쓰던 아이템 도감의 기존 배치·스타일 오버라이드만 제거 |
| [GameText CSV](../../DataSheets/Localization/GameText.csv), GameText Shared Data 및 ko/en/ja/zh-Hans/zh-Hant 테이블 | `encyclopedia.item_stats` 추가 |

씬: `DarkLord_Tutorial`, `DemonkingCorridor`, `Grand Hall`, `HeoMinSeok_Boss_Dragon`, `HeoMinSeok_Boss_Shadow`, `LeeJunmo_Boss_DemonKing`, `ProceduralDragonCorridor`, `ProceduralShadowCorridor`, `ProceduralSlimeCorridor`, `PrototypeTutorialUpgradeScene`, `SangHyup_Boss_SlimeQueen`, `Tests/TestScene`. 모든 씬 경로는 `Assets/_Project/Scenes/` 아래다.

이번 작업 직전 파일은 [before.zip](before.zip)에 보존했다. 저장소에는 이전 작업의 변경이 이미 많으므로 이번 비교는 Git HEAD 대신 이 복사본을 기준으로 한다. [상속 변경 내역](inheritance-changes.json), [런타임 코드 변경](runtime-change.diff), [소스 참조 검토](source-reference-review.json)를 함께 남겼다. 작업용 제작 스크립트는 삭제했다.

## 확인 범위와 남은 확인

- 네이티브 Aseprite 저장/내보내기, Unity 6000.4.2f1 프리팹 저장을 수행했다.
- 수정된 소스를 포함하는 `UI.csproj` MSBuild가 종료 코드 0으로 완료됐다. 기존 obsolete API/미사용 필드 경고는 남아 있다. [MSBuild 로그](msbuild-UI-final.log).
- 세 프리팹의 기존 object ID 삭제와 누락된 내부 참조가 없음을 소스에서 확인했다. `EncyclopediaUI.prefab`의 아이템 범위 밖 기존 레코드는 작업 직전과 동일하다. GlobalUIRoot와 12개 씬의 변경은 해당 아이템 오버라이드 제거뿐이다.
- [최종 Unity 임포트/컴파일](final-import-unity.log)은 종료 코드 0으로 완료됐다. 세 프리팹과 최종 재질의 임포트를 확인했으며 Play Mode 진입이나 테스트 실행은 하지 않았다.
- **자동 테스트·Play Mode·실제 화면 검수는 실행하지 않았다.** 실제 게임의 스크롤, 페이지 전환, 상세 설명, 언어별 긴 문장과 화면 크기별 배치는 사용자 확인이 남는다. 견본의 PNG 텍스트와 런타임 TMP 줄바꿈은 완전히 동일하다고 보장하지 않는다.

## 문서 영향

[SessionLog](../../Docs/SessionLogs/2026-11-02.md), [EncyclopediaStructure](../../Docs/StructureMemory/ScriptSystems/EncyclopediaStructure.md), [DecisionLog](../../Docs/DecisionLog.md), [견본 목록](../encyclopedia-item-redesign-preview-2026-11-02/README.md)을 갱신했다. ErrorLog는 기존 상속 오버라이드 기록을 재사용하며 수정하지 않았다. Doc Impact: SessionLog + StructureMemory + DecisionLog. Architecture/Contracts 및 Presentation HTML 변경 없음, HTML stale candidate 없음.
