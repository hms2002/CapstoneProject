# 28번 수정 — 왼쪽 아이콘 그리드

사용자는 28번의 오른쪽 정보 배치를 유지하고 왼쪽을 이전처럼 아이콘만 있는 그리드로 요청했다. 작업 모드는 견본 제작이며 Unity 적용은 포함하지 않는다.

- 왼쪽: 이름·번호가 없는 4열 아이콘 목록. 88×88 셀, 16픽셀 간격, 선택 테두리와 페이지 이동 유지.
- 오른쪽: [기존 28번](../revision-10-fixed-page-roles/28-pair.png)의 삽화·능력치/레벨·스토리·기술/효과 배치와 글자·아이콘을 그대로 유지.
- 같은 크기의 두 페이지와 중앙 접힘선을 유지.
- 예시 항목은 앞 견본과 같은 무기 5개/유물 8개다. 실제 저장 데이터·획득 상태와 연동하지 않았다.

## 파일

- [무기·유물 비교](28-pair.png) · [비교판 Aseprite](28-pair.aseprite)
- [무기 PNG](28-weapon.png) · [무기 Aseprite](28-weapon.aseprite)
- [유물 PNG](28-relic.png) · [유물 Aseprite](28-relic.aseprite)
- [PNG·Aseprite ZIP](option28-icon-grid.zip)
- [그리드 배치](grid-layout.json) · [유지한 샘플 문구](content-spec.json)

네이티브 Aseprite에서 기존 28번 파일을 열고 도형/아이콘 레이어의 왼쪽 페이지 영역만 교체했다. 글자 레이어에서는 왼쪽 목록의 이름·번호 영역만 지웠다. 제목·페이지 번호·책 외형·중앙 접힘선·오른쪽 영역은 원본 레이어에서 유지했다. 개별 PNG는 1260×760, 비교판은 2520×850이다.

최종 비교판을 시각적으로 확인했다. 자동 테스트, Unity PlayMode 확인, 컴파일·임포트는 실행하지 않았다. 게임 Assets/씬/프리팹/설정 변경 없음. 실제 TMP 표현, 긴 상세/다국어 문구와 도감 동작은 추후 적용 단계에 남는다. 임시 제작 스크립트는 삭제했다.

문서 영향: [SessionLog](../../../Docs/SessionLogs/2026-11-02.md) + [DecisionLog](../../../Docs/DecisionLog.md). ErrorLog·StructureMemory·Presentation HTML 변경 없음, HTML stale candidate 없음.

