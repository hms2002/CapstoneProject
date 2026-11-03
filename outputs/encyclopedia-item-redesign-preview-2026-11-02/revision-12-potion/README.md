# 포션 도감 — 28번 계열 견본

작업 모드: 승인 전 견본 제작. 사용자 요청은 아이템 도감 적용 전에 포션 화면도 확인하는 것이며, Unity 적용은 포함하지 않는다.

## 구성

- 같은 크기의 두 페이지(각 460×576), 중앙 접힘선과 기존 책 외형을 유지한다.
- 왼쪽은 28번 수정안의 4열 아이콘 그리드다. 현재 ItemDatabase에 등록된 소모품은 회복 물약 1종이므로 한 칸만 표시한다. 빈 슬롯이나 미등록 물약을 추가하지 않았다.
- 오른쪽은 이름 아래에 포션 삽화와 효과를 나란히 배치한다. 무기·유물과 같은 밝은 종이, 얇은 다중 테두리, 머리말을 사용한다.
- 현재 포션 정보는 이름·아이콘·효과 설명이다. 무기 능력치, 유물 레벨, 무기 전용 자세히 설명 단축키를 넣지 않는다.
- 효과 원문: `● [[체력]] {pos:[+1]} 회복`. 체력 용어와 증가량의 색 구분을 유지한다.
- Galmuri9 픽셀 글꼴과 불투명 1픽셀 외곽선으로 표시했다. 한국어 글자는 GDI로 래스터화한 뒤 Aseprite의 독립 텍스트 픽셀 레이어에 합성했다.
- 현재 정보량에 맞춰 아래 공간을 비워 두었다. 실제 입력 키·쿨다운·가격·소지량처럼 현재 도감에 없는 정보는 만들지 않았다.

## 파일

- [포션 PNG](28-potion.png)
- [레이어가 있는 Aseprite 원본](28-potion.aseprite)
- [PNG·원본 ZIP](option28-potion.zip)
- [원문·출처·배치 정보](content-spec.json)
- [무기·유물 28번 수정안](../revision-11-option28-icon-grid/28-pair.png)

PNG/원본 캔버스는 1260×760이다. Aseprite 원본은 책 / 프레임 / 실제 아이콘 / 텍스트의 4개 레이어로 구성한다. 오른쪽 포션 삽화도 원래 32×32 시트의 첫 번째 스프라이트를 사용하며, 투명 여백을 제외한 이미지를 정수 배율로 확대했다.

## 확인과 제한

`ItemDatabase.asset`, `CD_HealPotion.asset`, `EncyclopediaItemRightPage.ShowConsumable`과 스프라이트 메타데이터를 읽어 표시 정보를 확인했다. Aseprite에서 합성·내보내기 후 PNG를 시각 검토했다. 게임 Assets, 프리팹, 씬, 코드, 설정을 변경하지 않았다.

자동 테스트·Unity PlayMode·컴파일·임포트 검증은 실행하지 않았다. 실제 TMP 표현, 다국어 줄바꿈과 게임 내 동작은 적용 후 확인이 남는다. 임시 제작 스크립트는 제거했다.

문서 영향: [SessionLog](../../../Docs/SessionLogs/2026-11-02.md). DecisionLog·ErrorLog·StructureMemory·Presentation HTML 변경 없음. Presentation HTML stale candidate 없음.
