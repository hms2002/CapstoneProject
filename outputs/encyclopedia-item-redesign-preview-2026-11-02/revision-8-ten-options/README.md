# 아이템 도감 — 새 견본 10안 (revision 8)

## 범위와 기준

- 작업 모드: Implementation — 사용자가 요청한 Aseprite 승인 견본 제작.
- 같은 크기의 좌우 두 페이지와 중앙 접힘선을 모든 안에서 유지한다. 원본 1920×1080 캔버스에서 페이지는 각각 460×576, 좌측 X=464 / 우측 X=996 / Y=286이며 중앙 72픽셀을 보존한다.
- 무기 능력치, 유물 레벨은 별도 소제목과 구분선을 가진 영역으로 분리한다. 기존 필수 정보, 픽셀 글꼴과 글자색 역할을 유지한다.
- 밝은 종이, 기존 책·아이템·스킬 이미지를 사용한다. 목록 배치, 선택 표시, 제목부, 테두리, 본문 구분 방식을 비교한다.
- Unity 적용은 이 견본 작업에 포함하지 않는다.

## 10안

| 번호 | 이름 | 구성 | 무기·유물 비교 | 개별 이미지 | Aseprite 원본 |
|---|---|---|---|---|---|
| 01 | 동판 장정 | 격자 목록 + 넓은 삽화 | [비교 PNG](01-pair.png) | [무기](01-weapon.png) · [유물](01-relic.png) | [무기](01-weapon.aseprite) · [유물](01-relic.aseprite) · [비교판](01-pair.aseprite) |
| 02 | 서가 색인 | 이름을 함께 보는 두 열 | [비교 PNG](02-pair.png) | [무기](02-weapon.png) · [유물](02-relic.png) | [무기](02-weapon.aseprite) · [유물](02-relic.aseprite) · [비교판](02-pair.aseprite) |
| 03 | 표본 기록 | 삽화·능력치와 설명 분리 | [비교 PNG](03-pair.png) | [무기](03-weapon.png) · [유물](03-relic.png) | [무기](03-weapon.aseprite) · [유물](03-relic.aseprite) · [비교판](03-pair.aseprite) |
| 04 | 보관함 | 세 열의 이름표 진열 | [비교 PNG](04-pair.png) | [무기](04-weapon.png) · [유물](04-relic.png) | [무기](04-weapon.aseprite) · [유물](04-relic.aseprite) · [비교판](04-pair.aseprite) |
| 05 | 등록 대장 | 줄 단위 목록 + 본문 색인 | [비교 PNG](05-pair.png) | [무기](05-weapon.png) · [유물](05-relic.png) | [무기](05-weapon.aseprite) · [유물](05-relic.aseprite) · [비교판](05-pair.aseprite) |
| 06 | 분류 서가 | 선택 머리말 + 열린 두 열 | [비교 PNG](06-pair.png) | [무기](06-weapon.png) · [유물](06-relic.png) | [무기](06-weapon.aseprite) · [유물](06-relic.aseprite) · [비교판](06-pair.aseprite) |
| 07 | 전시 도록 | 세로 썸네일 + 큰 삽화 | [비교 PNG](07-pair.png) | [무기](07-weapon.png) · [유물](07-relic.png) | [무기](07-weapon.aseprite) · [유물](07-relic.aseprite) · [비교판](07-pair.aseprite) |
| 08 | 공유 격자 | 하나의 목록 틀과 간결한 본문 | [비교 PNG](08-pair.png) | [무기](08-weapon.png) · [유물](08-relic.png) | [무기](08-weapon.aseprite) · [유물](08-relic.aseprite) · [비교판](08-pair.aseprite) |
| 09 | 삽화와 색인 | 페이지 내부를 삽화·목록으로 | [비교 PNG](09-pair.png) | [무기](09-weapon.png) · [유물](09-relic.png) | [무기](09-weapon.aseprite) · [유물](09-relic.aseprite) · [비교판](09-pair.aseprite) |
| 10 | 선택 강조 | 선택한 행을 넓힌 기록부 | [비교 PNG](10-pair.png) | [무기](10-weapon.png) · [유물](10-relic.png) | [무기](10-weapon.aseprite) · [유물](10-relic.aseprite) · [비교판](10-pair.aseprite) |

03·07·09는 능력치/레벨을 **왼쪽 페이지**에 두고 오른쪽을 설명에 사용한다. 나머지는 **오른쪽 페이지**에서 별도 영역으로 구분한다. 06·10의 무기는 스토리 다음에 능력치를 배치한다.

## 비교판과 자료

- [01–02](compare-01-02.png), [03–04](compare-03-04.png), [05–06](compare-05-06.png), [07–08](compare-07-08.png), [09–10](compare-09-10.png)
- [전체 PNG·Aseprite ZIP](encyclopedia-ten-designs.zip)
- [레이아웃 좌표](layout.json), [안별 구성·팔레트](options.json), [샘플 전체 문구](content-spec.json)
- [직전 능력치·레벨 분리 견본](../revision-7-stats-level-sections/README.md)

개별 PNG는 1260×760, 무기·유물 비교판은 2520×850, 두 안씩 묶은 비교판은 2520×1700이다. 원본 Aseprite는 책 / UI 도형 / 기존 게임 아이콘 / 글자 레이어를 분리했다. 글자는 프로젝트 Galmuri9 TTF의 픽셀 래스터이며 라이브 텍스트 객체가 아니다.

## 정보 보존

무기: 이름, 전기 속성 +5, 전체 스토리, 기본 스킬의 두 분기와 두 번째 스킬, 각 아이콘·이름·Mouse1/Mouse1/Q·3s/3s/10s·전체 간단 설명·자세히 설명 단축키.

유물: 실제 쇳덩이의 예비 탄창 아이콘/이름, Lv. 2/3 및 Q/E, 기존 여섯 효과 문단과 남은 장전 횟수 한 문단. 복잡한 효과를 별도 수치 카드로 축약하지 않았다.

목록 순서, 획득 상태, 페이지 수는 비교용 예시다. 현재 저장 데이터를 읽거나 변경하지 않았다. 미획득 항목의 검은 실루엣 및 상세 정보 숨김은 추후 적용에서도 유지해야 한다.

## 제작 및 검토

네이티브 Aseprite에서 UI 도형·아이콘·본문 레이어를 합성하고 PNG/aseprite로 내보냈다. 새 제목·목록·구분 문구는 Galmuri9 단색 픽셀 렌더링을 가져왔고, 본문은 앞선 견본의 전체 텍스트 레이어를 재배치했다.

10개 최종 무기·유물 비교판을 시각적으로 검토했다. 06·10의 하단 안내 여백과 09 무기 목록 아이콘의 수직 정렬을 조정했다. 임시 제작 스크립트는 내보내기 후 삭제하여 일회용 실행 도구를 저장소에 남기지 않았다.

자동 테스트, Unity PlayMode 확인, 컴파일, 임포트는 실행하지 않았다. 게임 Assets/씬/프리팹/ProjectSettings는 변경하지 않았다. 실제 TMP 렌더링, 더 긴 상세/다국어 문구의 스크롤과 최종 디자인 선택은 추후 적용 사항이다.

문서 영향: [SessionLog](../../../Docs/SessionLogs/2026-11-02.md). 기존 동일 페이지·영역 분리 결정에 추가 정책이 없으므로 DecisionLog, ErrorLog, StructureMemory는 변경하지 않았다. Presentation HTML 영향 없음.

