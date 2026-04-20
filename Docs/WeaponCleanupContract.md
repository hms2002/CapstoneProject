# Weapon Cleanup Contract

이 문서는 무기 executor, runtime state, runtime data가 종료/교체/씬 전환 시 어떤 정리 규칙을 따라야 하는지 정리하는 운영 문서입니다.

## 왜 필요한가

구조가 커질수록 문제가 되는 건 "선택"보다 "정리"입니다.

- 무기 교체
- 씬 전환
- owner disable
- timeout
- target lost
- cancel
- force stop

같은 종료 경로에서 정리 규칙이 흔들리면 상태 꼬임이 가장 먼저 생깁니다.

지금 구조는 `WeaponAbilityExecutor` 베이스와 `WeaponExecutorRunner`가 cleanup 호출 자체는 강제합니다.  
이 문서는 **무엇을 정리해야 하는지**를 팀 규칙으로 고정합니다.

## 현재 코드 계약

- 종료 사유
  - `WeaponExecutorEndReason`
  - `Completed`, `Cancelled`, `Forced`, `Timeout`, `TargetLost`, `WeaponSwapped`, `SceneChanged`, `OwnerDisabled`

- 종료 경로
  - 외부는 `Begin / Cancel / ForceStop`
  - 내부 종료는 `FinalizeExecution(reason)` 한 경로로만 닫힙니다.

- cleanup 호출 책임
  - `WeaponAbilityExecutor` 베이스가 `Cleanup(reason)`을 항상 호출합니다.
  - 구현체는 cleanup을 "직접 불러야 하는 메서드"가 아니라 "override 해야 하는 종료 훅"으로 취급합니다.

- 전역 종료 책임
  - `WeaponExecutorRunner`가 활성 executor의 취소/강제 종료를 중앙에서 맡습니다.
  - 무기 교체, owner disable 같은 종료 사유는 runner가 reason과 함께 전달합니다.

## 공통 규칙

### 1. 상태 소유자는 data다

- 영속 상태는 `WeaponRuntimeData`
- 장착 중 live 훅은 `WeaponAbilityRuntimeState`
- 긴 실행 운영은 `WeaponAbilityExecutor`

cleanup은 live 쪽을 정리하는 것이지, 무조건 persistent state를 초기화하는 것이 아닙니다.

예:
- 표식 스택은 cleanup으로 바로 지우지 않습니다.
- 링크 대기 플래그, 임시 hitbox, 코루틴, 연출 핸들은 cleanup 대상입니다.

### 2. strategy는 cleanup을 하지 않는다

`WeaponAbilitySelectionStrategy`는 읽기만 담당합니다.  
정리는 `RuntimeState`, `Executor`, `Coordinator` 쪽에서만 합니다.

### 3. 종료 사유에 따라 정리 범위를 다르게 본다

같은 종료라도 의미가 다릅니다.

- `Completed`
  - 정상 종료
  - 후속 상태를 열어도 됨
- `Cancelled`
  - 플레이어 의도 취소
  - 일시 상태는 지우되 persistent reward는 보수적으로 유지
- `Forced`
  - 외부 강제 중단
  - 가능한 가장 안전한 정리
- `WeaponSwapped`
  - 무기 교체
  - 현재 무기 프리팹 local 상태와 실행만 정리
  - 슬롯 data는 보통 유지
- `SceneChanged`
  - 씬 이동
  - 저장 가능한 값만 남기고 scene object 참조는 정리
- `OwnerDisabled`
  - 플레이어 비활성화/죽음/컷신
  - 가장 보수적인 강제 정리

## 체크리스트

새 executor나 runtime state를 만들 때 아래를 확인합니다.

### 실행체 cleanup

- 코루틴이 남아 있지 않은가
- 임시 오브젝트를 제거했는가
- event relay를 직접 구독했다면 해제했는가
- 이동 제한/무적/태그 부여를 되돌렸는가
- hitbox/trigger를 껐는가
- 프리팹 로컬 참조를 null 또는 무효 상태로 정리했는가

### data cleanup

- 정말 persistent state를 지워야 하는가
- reason이 `WeaponSwapped`나 `SceneChanged`일 때도 지워야 하는가
- scene object 참조를 data에 남기지 않았는가

### 쌍무기 cleanup

- 다른 슬롯 상태를 소비하던 창을 닫아야 하는가
- 반대 슬롯 persistent state는 유지해야 하는가
- 한쪽 무기 cleanup이 반대 슬롯의 핵심 상태를 불필요하게 리셋하지 않는가

## 현재 기준 사례

### 월식도

- persistent state
  - stance 여부
  - 다음 공격 인덱스
  - bloom 가능 여부
- cleanup 원칙
  - 무기 교체나 씬 전환으로 자동 리셋하지 않음
  - slot-owned runtime data가 진실한 상태를 유지

### 사슬창

- cleanup 대상
  - 링크 대기 상태
  - throw executor의 시간축 실행
- `Timeout`
  - executor cleanup으로 링크 대기 해제
- `WeaponSwapped`
  - runner가 강제 종료
  - 프리팹 local 실행은 정리
  - persistent data 정책은 별도 판단

### 표식검 + 처형총

- 표식검
  - 표식 스택은 cleanup보다 processor와 data 규칙이 관리
- 처형총
  - 반격 창은 processor가 시간 경과로 닫음
  - 발동 성공 시 반대 슬롯 검의 상태를 소비하지만, runtime state가 직접 쓰지 않고 interaction layer/pair rule이 coordinator를 통해 반영함

## 팀 규칙 요약

1. cleanup은 반드시 executor base/runner 경로를 탄다.
2. strategy는 상태 정리 책임이 없다.
3. live component cleanup과 persistent data reset을 구분한다.
4. `WeaponSwapped`는 프리팹 실행 정리이지, 무조건 슬롯 상태 초기화가 아니다.
5. scene object 참조는 저장하지도, cleanup 이후 data에 남기지도 않는다.

## 다음에 강화할 부분

- 종료 reason별 디버그 로그 표준
- cleanup reason을 HUD/디버그 UI에 표시
- executor별 체크리스트를 editor 경고와 연결
