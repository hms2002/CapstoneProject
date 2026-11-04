# 아이템 도감 배치·가독성 수정 — 2026-11-02

Mode: Implementation. 사용자가 무기 능력치를 아이콘 아래로 옮기고, 승인 견본과 다른 이름 누락·안내 정렬·가독성을 수정하도록 요청했다. [사용자 무기 화면](user-before-weapon.png), [사용자 유물 화면](user-before-relic.png)이 수정 전 재현 증거다. 게임 데이터와 발견/획득 정책은 변경하지 않는다.

## 확인한 원인

- `ProtoTypeHub.unity`에 아이템 도감의 옛 오버라이드 104개가 남아 있었다. 이름 TMP의 stretch 앵커를 고정 앵커로 바꾸면서 음수 높이가 남았고, 이전/다음 페이지 버튼은 같은 원점으로, 유물 Q/E 아이콘은 폭 0으로 덮어썼다. 레벨 수치의 중앙 배치도 이전 위치로 바뀌었다. [대상 속성](scene-override-evidence.json).
- 이전 일회성 정리에서 원시 UTF-8 문자열에 LF 전용 패턴을 적용해 CRLF 파일을 건너뛰었다. 현재 씬 전체의 줄바꿈을 정규화해서 읽자 허브 외 9개 같은 소비 씬에서도 잔여 오버라이드가 확인됐다. 저장은 각 파일의 원래 CRLF를 유지했다.
- 상세 아이콘에도 인벤토리 표시 프로필의 위치/확대가 적용됐다. 뇌창은 offset (-16,-16), scale 1.7, rotation -45°이므로 작은 상세 틀에서 아래/왼쪽으로 벗어났다. 기존 상단 pivot과 프로필의 중앙 pivot 차이도 위치를 바꿨다.
- 새 외곽선 재질의 alpha는 1이었으나 OutlineWidth 0.1로, 18px 본문에서 견본의 명확한 경계를 만들기에 너무 가늘었다. 공유 게임 폰트가 아닌 도감 전용 재질만 보정했다.

## 수정 파일과 목적

| 파일 | 변경 |
| --- | --- |
| [EncyclopediaUI.prefab](../../Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab) | 무기 능력치를 HeaderRow에서 빼서 아이콘 아래의 전체 너비 섹션으로 배치. 아이콘은 중앙 64×64. 기존 페이지 버튼에는 10×12 Arrow 자식을 저작하고 34×30 클릭 영역 유지 |
| [EncyclopediaItemRightPage.cs](../../Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaItemRightPage.cs) | 상세 아이콘에 인벤토리 위치·확대를 적용하지 않고, 표시 프로필의 스프라이트·방향은 유지 |
| [Galmuri9 SDF Opaque Outline.mat](../../Assets/_Project/Art/Sprites/UI/EncyclopediaItemSkin/Materials/Galmuri9%20SDF%20Opaque%20Outline.mat) | 불투명 갈색 외곽선 두께 0.1 → 0.5, sharpness 0 → 1. 글자색·외곽선 색·alpha·softness는 유지 |
| `Assets/_Project/Scenes/` 아래 10개 씬 | 아이템 도감에 속한 기존 오버라이드 1,001개만 제거하여 새 소스 배치 상속 |

씬은 `DragonCorridor`, `PixelLightTest`, `ProceduralDemonkingCorridor`, `ProceduralDungeonV0Test`, `ProtoTypeCorridor`, `ProtoTypeHub`, `SangHyup_Hallway`, `ShadowCorridor`, `SlimeCorridor`, `TutorialCorridor`다. [정확한 변경 파일](changed-files.json), [제거한 오버라이드](removed-overrides.json), [코드 변경](runtime.diff), [수정 직전 파일](before.zip).

무기는 이름 → 중앙 아이콘 → 능력치 → 스토리·스킬 순서다. 유물의 중앙 아이콘 → 제목 없는 레벨 → 효과, 소모품의 중앙 아이콘 → 효과 구성은 유지했다. 본문 길이에 따른 스크롤과 미획득 상세 숨김도 유지한다. 슬롯/스킬 공유 프리팹과 몬스터·보스·책 외형은 이번에 수정하지 않았다. 런타임 오브젝트 생성이나 새 컴포넌트 타입도 추가하지 않았다.

## 확인과 한계

- UI.csproj MSBuild 종료 코드 0. 기존 경고는 남아 있다. [컴파일 로그](msbuild-UI.log).
- [소스 검토](source-review.json): 기존 prefab object ID 삭제/누락 내부 참조 없음. 기존 13개 레코드 수정, 화살표 두 개의 저작 레코드 8개 추가. 씬 변경은 해당 override 제거뿐이며 줄바꿈을 유지했다.
- 사용자 요청에 따라 자동 테스트·Play Mode·실제 화면 검증은 실행하지 않았다. 사용 중인 Unity 인스턴스를 정지하거나 씬을 강제로 다시 열지 않았다. 이번 수정의 Editor 임포트/실제 재진입 결과는 사용자 확인이 남는다.
- 다음 Play에서 허브의 아이템 이름, 하단 중앙 페이지 안내, 유물 Q/E, 무기 아이콘·능력치 순서와 본문 외곽선을 확인한다. 현재 로드된 씬의 옛 상태가 디스크 수정을 덮어쓰지 않도록 변경된 씬을 다시 불러온 상태에서 확인해야 한다. 긴 스킬/다국어 설명은 스크롤을 포함해 확인한다.

## 문서 영향

[SessionLog](../../Docs/SessionLogs/2026-11-02.md), [StructureMemory](../../Docs/StructureMemory/ScriptSystems/EncyclopediaStructure.md), [DecisionLog](../../Docs/DecisionLog.md), [기존 ErrorLog의 중첩 오버라이드 항목](../../Docs/ErrorLog.md)을 갱신했다. Doc Impact: SessionLog + StructureMemory + DecisionLog + ErrorLog. Architecture/Contracts·Presentation HTML 변경 없음, HTML stale candidate 없음. 새 일회성 제작/테스트 도구는 남기지 않았다.
