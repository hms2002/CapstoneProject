---
status: active
authority: source-of-truth
category: architecture
last_reviewed: 2026-05-05
---

# Loading Scopes

이 프로젝트의 1차 로딩 기준은 세 단계로 나눈다.

## Boot

- 게임 시작 시 한 번 로드하고 종료 전까지 유지한다.
- 대상:
  - 글로벌 서비스
  - 글로벌 UI
  - 플레이어 공용 자산
  - 공용 Audio / Cue / 데이터

## RunCommon

- 런 시작 시 로드하고 허브 복귀 시 해제한다.
- 기준 자산:
  - 런 전용 HUD / 전투 공용 자산
  - 런 전체에서 반복 사용하는 공용 VFX / Cue / 데이터
- 데이터 진입점:
  - [RunRouteCatalogSO](../../Assets/LeeJunMo/Script/SceneManagement/RunRouteCatalogSO.cs)
  - `runCommonLoadManifest`

## RouteSet

- `Corridor + Boss`를 한 덩어리로 본다.
- 현재 RouteSet과 다음 RouteSet만 유지하는 것을 1차 목표로 잡는다.
- 데이터 진입점:
  - [CorridorBossRouteSetSO](../../Assets/LeeJunMo/Script/SceneManagement/CorridorBossRouteSetSO.cs)
  - `loadManifest`
- 세부 구성:
  - `sharedManifest`
  - `corridorManifest`
  - `bossManifest`

## Runtime Entry Points

- 현재 런 계획 / 현재 RouteSet / 다음 RouteSet:
  - [PortalRouteManager](../../Assets/LeeJunMo/Script/SceneManagement/PortalRouteManager.cs)
- 현재 로딩 윈도우 접근:
  - `TryGetActiveLoadWindow(...)`

## Current Goal

지금 단계에서는 실제 비동기 로딩보다 먼저:

1. 어떤 자산이 어느 scope에 속하는지 명확히 나누고
2. 그 scope를 코드에서 읽을 수 있게 만들고
3. 이후 `AssetProvider` / `PreloadService`가 이 manifest를 소비하도록 만드는 것

을 목표로 한다.
