# 유물 레벨 — 아이콘 아래 중앙 정렬

요청에 따라 견본의 `레벨` 제목을 제거하고, Q / Lv. 2 / 3 / E 묶음을 오른쪽 페이지의 가로 중앙에 배치했다. 순서는 이름 → 아이콘 → 레벨 조절 → 효과다. 이전 하단 레벨 바는 없앴다.

- [PNG 견본](28-relic.png) · [Aseprite 원본](28-relic.aseprite)
- [PNG·Aseprite ZIP](relic-level-centered.zip)
- [콘텐츠·배치](content-spec.json) · [글자 배치](text-layout.json)

레벨 영역은 페이지 내부 (120,166), 220×36이며 중심 x=230으로 아이콘과 일치한다. 효과 제목은 y=216, 본문은 y=258부터 시작한다. 기존 7개 효과 문단을 유지하며 마지막 문단은 y=503에서 끝난다. 왼쪽 그리드, 책 외형·동일 페이지 크기·중앙 접힘선·글자색은 유지했다. 소모품과 무기 견본은 변경하지 않았다.

네이티브 Aseprite로 이전 유물 원본을 수정하고 1260×760 PNG를 내보내 시각 검토했다. 텍스트는 기존 Galmuri9를 GDI로 래스터화한 픽셀 레이어다. 임시 제작 스크립트는 삭제했다. 자동 테스트·Unity PlayMode·컴파일·임포트 확인은 실행하지 않았다. 게임 코드·에셋·프리팹·씬은 변경하지 않았으며 실제 TMP·다국어·게임 동작은 적용 후 확인이 남는다.

변경 파일은 이 폴더의 견본·배치 기록·README, 프리뷰 목록 README, [SessionLog](../../../Docs/SessionLogs/2026-11-02.md), [DecisionLog](../../../Docs/DecisionLog.md)다. 문서 영향: SessionLog + DecisionLog. ErrorLog·StructureMemory·Presentation HTML 변경 없음. Presentation HTML stale candidate 없음.
