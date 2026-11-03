# 소모품·유물 — 중앙 아이콘 아래 효과

작업 모드: 명시적으로 요청한 Aseprite 견본 수정. Unity 적용은 포함하지 않는다.

## 이번 변경

- 소모품과 유물의 오른쪽 페이지를 이름 → 가로 중앙의 아이콘 → 전체 폭 효과 섹션 순서로 통일했다.
- 아이콘 프레임은 오른쪽 페이지 내부 좌표 (185, 70), 크기 90×82다. 효과 제목은 y=171, 본문은 y=216부터 시작한다.
- 유물 레벨 미리보기와 Q/E 안내는 별도 하단 영역(y=526, 높이 38)으로 옮겼다. 효과와 레벨을 구분하는 기존 요구를 유지한다.
- 유물 효과는 앞 견본의 7개 문단을 전부 유지했다. 본문은 y=461에서 끝나므로 레벨 영역과 겹치지 않는다.
- 소모품은 실제 등록된 회복 물약 1종과 기존 효과 문구를 유지했다.
- 왼쪽 아이콘 그리드, 두 페이지의 동일 크기, 중앙 접힘선, 책 외형은 이전 원본에서 유지했다. 무기 견본은 변경하지 않았다.
- Galmuri9, 1픽셀 불투명 외곽선과 의미별 글자색을 유지했다.

## 산출물

- [소모품 PNG](28-potion.png) · [소모품 Aseprite](28-potion.aseprite)
- [유물 PNG](28-relic.png) · [유물 Aseprite](28-relic.aseprite)
- [비교판 PNG](28-centered-pair.png) · [비교판 Aseprite](28-centered-pair.aseprite)
- [PNG·Aseprite ZIP](centered-items.zip)
- [콘텐츠·배치 기록](content-spec.json) · [글자 배치 기록](text-layout.json)

개별 원본/PNG는 1260×760, 비교판은 2520×850이다. 네이티브 Aseprite에서 이전 소모품·유물 원본을 열어 도형·아이콘·텍스트 레이어의 오른쪽 페이지 영역만 교체했다. 텍스트는 Galmuri9를 GDI로 래스터화한 픽셀 레이어이며 라이브 텍스트가 아니다. 책 / UI 프레임 / 실제 게임 아이콘 / 글자 레이어를 분리했다.

## 검토와 남은 확인

두 개별 PNG를 시각 검토해 아이콘 중앙 정렬, 효과 배치, 유물 문구와 하단 레벨 영역을 확인했다. 자동 테스트, Unity PlayMode, 컴파일·임포트 확인은 실행하지 않았다. 실제 TMP 표현·다국어 줄바꿈·게임 동작은 적용 이후 확인이 필요하다. 게임 코드·Assets·프리팹·씬·설정은 변경하지 않았다. 임시 제작 스크립트는 삭제했다.

문서 영향은 [SessionLog](../../../Docs/SessionLogs/2026-11-02.md)와 [DecisionLog](../../../Docs/DecisionLog.md)다. 프리뷰 목록 README도 최신 방향으로 갱신했다. ErrorLog·StructureMemory·Presentation HTML 변경 없음. Presentation HTML stale candidate 없음.
