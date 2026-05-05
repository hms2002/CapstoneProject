---
status: note
authority: reference-only
category: note
last_reviewed: 2026-05-05
---

# 시스템 메모

## 포탈 및 런 시스템

### ScenePortal

- 역할: 상호작용 진입점
- 책임:
  - 프롬프트 표시
  - 하이라이트 처리
  - 상호작용 가능 여부 판단
  - 이동 요청 전달
- 허브 시작 포탈은 `RunRouteCatalogSO`를 가질 수 있다.

### PortalRouteManager

- 역할: 런 경로 계획 및 목적지 해석
- 책임:
  - 허브 시작 포탈별 pending run plan 생성
  - 선택된 허브 시작 포탈의 플랜을 active plan으로 전환
  - 각 포탈 전이에 대한 목적지 씬과 entry point 해석
  - 현재 active run의 stage index 관리

### RunTransitionResolver

- 역할: 특정 전이가 런을 시작하는지 끝내는지 결정
- 현재 실질 기본 규칙:
  - `HubToRunStart` -> 런 시작
  - `ReturnToHubAfterRun` -> 승리로 런 종료

### SceneTransitionPolicyResolver

- 역할: 씬 전이 시 회복, 쿨다운 초기화, effect 정리 같은 정책 결정
- 현재 상태:
  - 구조는 존재한다
  - 값은 `SceneTransitionContext`에 기록된다
  - 실제 downstream 소비는 아직 제한적이다

## 플레이어 스폰 및 복원

### PlayerSpawner

- `SceneTransitionContext.entryPointId`를 기준으로 스폰 포인트를 찾는다.
- 플레이어 생성 전에 spawn runtime policy를 적용한다.
- 현재 spawn runtime policy:
  - `RestorePendingState`
  - `ResetToSceneDefault`

### PlayerSceneRestoreBootstrapper

- 새로 생성된 플레이어에게 pending runtime state를 복원한다.
- 전투 씬 간 상태 이어받기에 사용된다.
- 허브 스폰 포인트는 스폰 전에 pending player state를 비움으로써 이어받기를 차단할 수 있다.

## 업그레이드 시스템

### 현재 구조

- `UpgradeManager`가 현재 다음 역할을 함께 맡고 있다.
  - 업그레이드 DB 조회
  - 해금 상태 계산
  - 구매 처리
  - 플레이어 재적용
  - 저장 호출
  - UI 열기/닫기

### 현재 리스크

- effect 계약이 아직 플레이어 중심이다.
- 플레이어 스탯 업그레이드나 아이템 해금에는 괜찮다.
- 하지만 아래와 같은 런 규칙형 업그레이드에는 점점 어색해질 가능성이 높다.
  - 유해 개수
  - 유해 드랍 수
  - 희귀도 보너스
  - 기타 비플레이어 수정자

### 이후 방향

- 업그레이드 effect를 적용 대상에 따라 분리한다.
  - 플레이어 effect
  - 아이템 해금 effect
  - 런 modifier effect

## 루팅 시스템

### 현재 구조

- `LootManager`가 현재 다음 역할을 함께 맡고 있다.
  - 스테이지 테이블 선택
  - 드랍 개수 굴림
  - 아이템 풀 필터링
  - 희귀도 굴림
  - 월드 드랍 생성
  - 유해 드랍 생성
  - 보스 마정석 수량 조회

### 최근 수정 사항

- `GetRandomRelicByRarity`가 실제로 유물 rarity를 반영하도록 수정했다.

### 아직 남은 부분

- 유해 업그레이드 보너스는 아직 연결되지 않았다.
- 기본 유해 드랍 자체는 `GraveLootTable` 기준으로 데이터 주도 구조다.
- 업그레이드 기반 유해 보너스를 읽는 별도 source 또는 modifier service가 필요할 수 있다.

## 권장 리팩토링 대상

### Loot

- `LootTableResolver`
- `LootPoolService`
- `LootRollService`
- `LootSpawnService`

### Upgrade

- `UpgradeProgressService`
- `UpgradePurchaseService`
- `UpgradeEffectApplier`
- `UpgradeUIController`

## 사용 규칙

- 포탈, 런, 업그레이드, 루팅 관련 코드를 수정하기 전에 이 문서와 `prototype-notes.md`를 먼저 읽는다.
> Legacy / Notes  
> 이 문서는 시스템 관련 임시 메모를 보관한 문서입니다.  
> 현재 표준 문서가 아니며, 과거 메모를 추적할 필요가 있을 때만 봅니다.
