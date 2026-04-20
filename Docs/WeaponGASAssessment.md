# Weapon GAS Assessment

이 문서는 월식도, 대검 처형자, 사슬창, 표식검+처형총, 태양도+월영도 검증을 바탕으로 현재 무기-GAS 구조를 항목별로 판정한 문서입니다.

## 합격

- `WeaponDefinition / Loadout / Strategy / RuntimeState / Bridge / ASC` 경계
  - 각 계층 책임이 분리되어 있고 기존 무기 fallback도 유지됩니다.
- 상태 기반 AD 선택
  - 월식도로 검증됐습니다.
- 실제 `HitConfirm` 기반 후속 분기
  - 대검 처형자로 검증됐습니다.
- 관계 상태 생성 후 소비
  - 사슬창 1차로 검증됐습니다.
- 공용 ASC 이벤트 relay
  - 무기 runtime state와 유물이 같은 이벤트 소비 경계를 탈 수 있습니다.
- executor 공용 틀
  - `WeaponAbilityExecutor` 와 `WeaponExecutorRunner` 가 들어가면서 긴 실행을 공통 생명주기로 운영할 기반이 생겼습니다.
- 월식도 custom runtime state 저장/복원
  - inventory-owned runtime data를 기준으로, 씬 전환 뒤에도 월식도의 기본 공격 상태 전환이 이어지는 것을 실제로 검증했습니다.
- 비활성 무기 persistent runtime state 보존
  - 월식도를 비활성 슬롯에 둔 상태에서도 스왑/씬 전환 후 상태가 이어지는 것을 검증했습니다.
- 쌍무기 상호참조 선택
  - 표식검+처형총으로 현재 슬롯과 반대 슬롯 runtime data를 함께 읽어 AD를 고르는 구조를 검증했습니다.
- 비활성 슬롯 시간 경과 상태 변화
  - `WeaponRuntimeProcessor` 와 `WeaponRuntimeCoordinator` 로 표식 감쇠, 반격 창 만료가 비활성 슬롯에서도 진행되는 것을 검증했습니다.
- 상태 생성 -> 소비 -> 역반영 왕복 구조
  - 검이 표식을 만들고, 총이 소비하고, 그 결과가 다시 검 스킬 개방으로 돌아오는 왕복 참조를 검증했습니다.
- 실제 제작형 쌍무기 상호참조
  - 태양도+월영도로 일반 공격 변경, `Skill1` 공명 피니시 변경, 양쪽 스택 동시 소비까지 실제 제작형 무기에서 검증했습니다.
- 비활성 슬롯 감쇠 + 실제 제작 무기 적용
  - 태양도/월영도의 열기/냉기 감쇠가 비활성 슬롯에서도 진행되고, 그 상태가 반대 슬롯 selector에 바로 반영되는 것을 검증했습니다.
- pair rule 등록 경계 분리
  - `WeaponInteractionLayer`에서 pair rule 인스턴스 하드코딩을 걷어내고, `WeaponPairInteractionRuleRegistry`로 등록 책임을 분리했습니다.
- runtime data / processor factory validation
  - loadout이 기대하는 `RuntimeData` / `RuntimeProcessor` 타입을 editor가 직접 검증하게 만들어 factory coverage 누락을 play 전에 더 빨리 잡을 수 있습니다.

## 보류

- runtime state 저장/복원
  - 2차 구현으로 inventory-owned runtime data 기준 저장/복원이 들어갔습니다.
  - 다만 아직 월식도 중심 검증이라, 다른 무기까지 같은 방식으로 옮길 범위와 우선순위는 더 판단이 필요합니다.
- cleanup 표준화
  - 지금은 executor 베이스가 cleanup 훅을 제공하지만, 무기 전반의 정리 규칙은 더 문서화할 필요가 있습니다.
- authoring scale
  - 전용 WAL과 전용 전략이 늘어날수록 editor/validation 투자가 계속 필요합니다.

## 다음 과제

- 어떤 무기부터 runtime data 기반으로 전환할지 판단
- 어떤 무기부터 processor/coordinator 기반 시간 경과 규칙이 필요한지 분류
- executor를 실제 무기 1~2개에 더 적용해 공용 규약이 충분한지 점검
- cleanup 규칙과 강제 종료 지점을 문서화
- editor가 executor/runtime state 누락을 더 잘 경고하도록 보강

## 한 줄 결론

현재 구조는 단순한 AD 선택을 넘어서, 상태 기반 분기, 적중 기반 후속 분기, 관계 상태 소비, 긴 실행 시작, **inventory-owned runtime data의 비활성 슬롯 보존과 씬 간 복원**, 그리고 **쌍무기 상호참조 + 비활성 슬롯 시간 경과 상태 변화 + 실제 제작형 쌍무기 적용**까지 감당할 수 있는 수준으로 발전했습니다. 다음 단계의 핵심은 다른 무기로의 data/processor 전환 범위와 cleanup 표준화입니다.
