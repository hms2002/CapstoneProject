# 아이템 도감 — 좌측 목록 / 우측 정보 고정 (revision 10)

## 고정 조건

사용자 지시로 **왼쪽 페이지는 아이템 목록, 오른쪽 페이지는 선택한 아이템의 정보**로 고정한다. 이름·상세 삽화·능력치·레벨·스토리·스킬·효과는 모두 오른쪽에 둔다. 왼쪽 목록 행/타일의 항목명과 아이콘은 목록 구성 요소다.

두 페이지는 원본 1920×1080 캔버스에서 각각 460×576(X=464 / 996, Y=286)이며 중앙 72픽셀 접힘선을 보존한다. 무기 능력치와 유물 레벨은 오른쪽 안의 별도 영역이다. 정보·목록을 양쪽으로 분산했던 [11–20안](../revision-9-experimental-layouts/README.md)은 후보에서 제외했다.

작업 모드: Implementation — 승인용 견본. 실험 범위는 왼쪽 목록의 배열과 오른쪽 정보의 단수·순서·묶음으로 제한했다. 게임 적용은 포함하지 않는다.

## 수정 견본 21–30

| 번호 | 이름 | 오른쪽 정보 배치 | 비교 | 개별 PNG | Aseprite |
|---|---|---|---|---|---|
| 21 | 연속 기록 | 이름·능력치·스토리 아래 세로 기술 | [무기·유물](21-pair.png) | [무기](21-weapon.png) · [유물](21-relic.png) | [무기](21-weapon.aseprite) · [유물](21-relic.aseprite) · [비교](21-pair.aseprite) |
| 22 | 분기 병렬 | 앞의 두 기술을 나란히 · 마지막 기술은 넓게 | [무기·유물](22-pair.png) | [무기](22-weapon.png) · [유물](22-relic.png) | [무기](22-weapon.aseprite) · [유물](22-relic.aseprite) · [비교](22-pair.aseprite) |
| 23 | 삼단 기술 | 세 기술을 동일 너비의 세 열로 | [무기·유물](23-pair.png) | [무기](23-weapon.png) · [유물](23-relic.png) | [무기](23-weapon.aseprite) · [유물](23-relic.aseprite) · [비교](23-pair.aseprite) |
| 24 | 사분면 기록 | 요약 한 칸 · 기술 세 칸 | [무기·유물](24-pair.png) | [무기](24-weapon.png) · [유물](24-relic.png) | [무기](24-weapon.aseprite) · [유물](24-relic.aseprite) · [비교](24-pair.aseprite) |
| 25 | 능력치 옆기둥 | 좁은 능력치 열 · 넓은 설명 열 | [무기·유물](25-pair.png) | [무기](25-weapon.png) · [유물](25-relic.png) | [무기](25-weapon.aseprite) · [유물](25-relic.aseprite) · [비교](25-pair.aseprite) |
| 26 | 입력 세로열 | 아이콘·입력과 본문을 행 안에서 분리 | [무기·유물](26-pair.png) | [무기](26-weapon.png) · [유물](26-relic.png) | [무기](26-weapon.aseprite) · [유물](26-relic.aseprite) · [비교](26-pair.aseprite) |
| 27 | 기술 우선 | 기술을 먼저 · 능력치와 스토리는 아래 | [무기·유물](27-pair.png) | [무기](27-weapon.png) · [유물](27-relic.png) | [무기](27-weapon.aseprite) · [유물](27-relic.aseprite) · [비교](27-pair.aseprite) |
| 28 | 삽화 머리말 | 삽화·능력치를 위에 · 기술은 촘촘한 행으로 | [무기·유물](28-pair.png) | [무기](28-weapon.png) · [유물](28-relic.png) | [무기](28-weapon.aseprite) · [유물](28-relic.aseprite) · [비교](28-pair.aseprite) |
| 29 | 오른쪽 대조표 | 오른쪽 페이지 안에서 이름·입력과 설명 대조 | [무기·유물](29-pair.png) | [무기](29-weapon.png) · [유물](29-relic.png) | [무기](29-weapon.aseprite) · [유물](29-relic.aseprite) · [비교](29-pair.aseprite) |
| 30 | 본문 우선 | 이름·설명 왼쪽 · 아이콘·입력 오른쪽 | [무기·유물](30-pair.png) | [무기](30-weapon.png) · [유물](30-relic.png) | [무기](30-weapon.aseprite) · [유물](30-relic.aseprite) · [비교](30-pair.aseprite) |

21/27은 정보 순서를 비교한다. 22/23/24는 오른쪽 페이지를 2열·3열·사분면으로 나눈다. 25는 능력치/레벨을 오른쪽의 좁은 옆기둥으로 분리한다. 26/29/30은 기술 아이콘·입력·본문의 관계를 각 행 안에서 바꾼다. 28은 상세 삽화를 오른쪽 머리말에 포함한다.

## 자료

- [21–22](compare-21-22.png), [23–24](compare-23-24.png), [25–26](compare-25-26.png), [27–28](compare-27-28.png), [29–30](compare-29-30.png)
- [전체 PNG·Aseprite ZIP](encyclopedia-fixed-page-roles.zip)
- [배치](layout.json), [글자 위치](text-layout.json), [안별 구성](options.json), [전체 문구](content-spec.json)

개별 1260×760, 무기·유물 비교 2520×850, 두 안 비교 2520×1700. Aseprite 원본은 책, UI 도형, 기존 게임 아이콘, 픽셀 글자를 분리한 레이어다.

## 보존 및 제작

기존 무기 이름/전기 +5/스토리/기본 스킬 두 분기/낙뢰 표식/아이콘/Mouse1·Mouse1·Q/3s·3s·10s/전체 간단 설명/상세 단축키를 유지했다. 유물 이름/아이콘/Lv. 2/3/Q·E/여섯 효과 문단과 남은 장전 횟수 문단도 유지했다. 옆기둥 안에서는 능력치와 레벨을 여러 행으로 표현한다.

Galmuri9와 기존 글자색 역할, 얇은 불투명 외곽선을 사용한다. 일반 기술명은 27px, 좁은 열의 기술명은 18px, 본문은 18px/23px 행간이다. 글자는 같은 프로젝트 TTF에서 픽셀로 래스터화했으며 라이브 텍스트가 아니다. 합성·레이어 저장·PNG 출력은 네이티브 Aseprite에서 수행했다.

목록 순서·획득 상태·페이지 수는 예시이며 실제 저장 데이터는 변경하지 않았다. 추후 적용 시 미획득 항목의 실루엣과 정보 숨김은 계속 유지한다.

## 검토 및 남은 사항

10개 무기·유물 비교판과 일부 개별 원본을 시각적으로 확인했다. 목록/정보 소유 페이지와 본문 경계를 소스 배치에서도 확인했다. 일반 제목 크기, 카드 이름의 아래 여백, 기술 행 사이 간격을 정리했다. 임시 제작 스크립트는 삭제했다.

자동 테스트, Unity PlayMode 확인, 컴파일·임포트는 실행하지 않았다. Assets/씬/프리팹/ProjectSettings 변경 없음. 디자인 선택, 실제 TMP 표현, 더 긴 상세·다국어 문구 및 스크롤은 적용 단계에 남는다. 특히 3열은 줄바꿈이 많아 가독성에 대한 사용자 선택이 필요하다.

문서 영향: [SessionLog](../../../Docs/SessionLogs/2026-11-02.md) + [DecisionLog](../../../Docs/DecisionLog.md). ErrorLog, StructureMemory, Presentation HTML 변경 없음. Presentation HTML stale candidate 없음.

