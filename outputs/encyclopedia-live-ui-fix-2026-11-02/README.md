# 아이템 도감 실제 화면 보정 — 2026-11-02

Mode: Implementation. 사용자가 승인 견본과 실제 화면을 대조하며 수정하도록 요청했고, 아이템 슬롯의 마우스 클릭 불가를 추가했다. 기존 무기 능력치의 아이콘 아래 배치, 유물의 아이콘 → 제목 없는 레벨 → 효과, 소모품의 아이콘 → 효과 구조를 유지했다.

## 원인과 변경 파일

| 파일 | 변경 이유와 내용 |
| --- | --- |
| [EncyclopediaEntrySlot.prefab](../../Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaEntrySlot.prefab) | Button이 참조하는 루트 Image까지 Raycast Target이 꺼져 클릭 경로가 없었다. 루트의 Raycast Target만 켰다. 장식/아이콘은 비활성 유지 |
| [Galmuri9 SDF Opaque Outline.mat](../../Assets/_Project/Art/Sprites/UI/EncyclopediaItemSkin/Materials/Galmuri9%20SDF%20Opaque%20Outline.mat) | 직전 OutlineWidth 0.5는 실제 화면에서 흰색·강조색 내부를 덮었다. OutlineWidth 0.18, FaceDilate 0.18로 글자 내부와 불투명 갈색 테두리를 함께 확보. Unity가 파생 ScaleRatio B/C를 재계산 |
| [Galmuri9 SDF Plain.mat](../../Assets/_Project/Art/Sprites/UI/EncyclopediaItemSkin/Materials/Galmuri9%20SDF%20Plain.mat) | 도감 전용 평문 재질의 Sharpness를 1로 설정하여 제목 경계를 명확히 함 |
| [EncyclopediaUI.prefab](../../Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab) | 카테고리와 아이템 이름의 이전 Bold 스타일을 Regular로 바꿔 견본의 가는 픽셀 획과 맞춤 |
| [EncyclopediaItemLeftPage.cs](../../Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaItemLeftPage.cs) | 페이지 번호를 `1/5`에서 `1 / 5`로 표기하여 숫자와 구분자 간격 확보 |

[수정 직전 5개 파일](before.zip), [이번 작업만의 소스 차이](source.diff), [변경 파일 목록](changed-files.json). 새 타입/직렬화 필드/GUID/씬 변경, 저장·발견 정책 변경, 런타임 UI 생성은 없다.

## 실제 Unity 화면

아래는 목업이 아니라 Unity 6000.4.2f1의 ProtoTypeHub, 1920×1080 Game 뷰에서 Computer Use로 관찰하고 보관한 화면이다.

- [무기 최종 화면](weapon-final.jpg): 홍련의 한계선 선택, 중앙 아이콘 아래 능력치, 스토리/스킬, 고정 하단 설명 안내.
- [유물 최종 화면](relic-final.jpg): 스피드 메달 선택, 중앙 아이콘과 Q/E·Lv. 2 / 5, 효과 제목과 강조색.
- [소모품 최종 화면](consumable-final.jpg): 회복 물약 선택, 중앙 아이콘 아래 효과.

슬롯 클릭으로 선택 테두리와 이름/설명이 바뀌는 것을 확인했다. 세 하위 탭 전환, 미획득 실루엣·상세 숨김, 유물 E 입력의 레벨/효과 갱신, 다음 페이지 클릭으로 1 / 5 → 2 / 5, 긴 무기 설명의 휠 스크롤을 화면에서 확인했다. 무기 능력치의 아이콘 아래 배치와 이름/페이지 표시도 실제 화면에서 확인했다.

견본의 예비 탄창은 현재 플레이 상태에서 미획득이므로 해당 정보를 강제로 노출하거나 저장을 수정하지 않았다. 실제 획득 유물로 동일 레이아웃을 확인했다. 허브 재진입 시 화면 확인을 위해 실행 중 플레이어 위치만 도감 앞 (-18.5, 4)으로 이동했으며, 씬/스폰/저장 데이터는 편집하지 않았다. 확인 뒤 Play를 종료했다.

## 컴파일·한계

- UI.csproj에 변경 소스가 포함된 것을 확인하고 MSBuild 실행, 종료 코드 0. [로그](msbuild-UI.log). 이후 Unity 재임포트/스크립트 재로딩 뒤 다시 Play에 진입해 최종 화면을 확인했다.
- 자동 테스트 스위트와 Player 빌드는 실행하지 않았다. 모든 언어·해상도·유물 설명 조합을 검수한 것은 아니다.
- 기존 PrewarmTraceRuntime의 Win32 IO 1224와 비활성 Animator 경고가 관찰됐다. 이번 도감 클릭/글자 수정 범위 밖이므로 변경하지 않았다.
- 소스 비교상 기존 프리팹 ID/참조/배치 구조를 유지한다. 새 일회성 테스트/제작 도구를 남기지 않았다.

Doc Impact: SessionLog + 기존 StructureMemory + ErrorLog. DecisionLog, Architecture/Contracts, Presentation HTML 변경 없음. 이번 수정으로 인한 Presentation HTML stale candidate 없음.
