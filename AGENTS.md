# Codex Working Guide

이 문서는 Codex와 자동화 작업자가 이 프로젝트를 수정할 때 지켜야 하는 작업 규칙입니다.
게임 설계 자체를 설명하는 문서는 [Docs/README.md](./Docs/README.md)에서 찾습니다.

## 기본 원칙

- 현재 코드, 프리팹, 씬 설정을 가장 높은 권위로 둡니다.
- architecture / contract / authoring guide 문서는 현재 구조의 의도를 설명하는 기준 문서입니다.
- review, proposal, legacy notes는 설계 과정 기록입니다. 최종 규칙처럼 적용하기 전에 현재 코드와 상위 문서를 확인합니다.
- 코드 작성 시 대상 클래스, 인터페이스, 구조체가 가지는 책임을 주석으로 적습니다.

## 작업 전 확인

- 관련 README 또는 guide를 먼저 읽고, 수정 범위를 좁힌 뒤 작업합니다.
- 이미 열려 있는 사용자 변경을 되돌리지 않습니다.
- 프리팹, 씬, ScriptableObject, serialized field 변경은 코드 변경보다 영향 범위가 넓으므로 완료 보고에 반드시 언급합니다.
- public API, serialized field 이름, Addressables/asset reference, Animator parameter를 바꿀 때는 호환성 위험을 먼저 확인합니다.

## 코드 작성 규칙

- 새 클래스 / 인터페이스 / 구조체 상단에는 책임 주석을 둡니다.
- 상태, 패턴, ability logic은 실행 책임과 presentation 책임을 섞지 않습니다.
- BT/FSM이 ASC 또는 TagSystem을 직접 조작하지 않도록 bridge/helper 경계를 유지합니다.
- cleanup은 상태 전이, suppression, death, disable/unload, 실행 취소 경로를 함께 고려합니다.
- pattern-specific 데이터와 presentation hook은 가능한 한 AL/AD 자산 쪽에 둡니다.

## Unity Authoring 규칙

- 런타임에서 필요한 참조를 자동 생성하는 코드는 prefab asset을 오염시키지 않도록 주의합니다.
- `OnValidate`에서는 prefab asset의 transform 구조를 변경하지 않습니다.
- 공격 판정, 장판 판정, VFX 충돌체는 부모 검색으로 의도하지 않은 피해 source가 섞이지 않게 합니다.
- collider는 물리 충돌용과 hitbox/trigger용 책임을 분리합니다.
- 보스/몬스터 제작 시 death, stagger/groggy, cleanup, cinematic lock, reward/portal 연결을 체크합니다.

## 완료 기준

- 변경한 핵심 파일과 이유를 요약합니다.
- 실행한 검증 명령 또는 Unity 테스트 결과를 적습니다.
- 검증하지 못한 부분과 남은 리스크를 숨기지 않고 적습니다.
- 사용자가 프리팹/씬에서 직접 연결해야 하는 항목이 있으면 명확히 따로 적습니다.
