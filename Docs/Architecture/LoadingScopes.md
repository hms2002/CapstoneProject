---
status: active
authority: source-of-truth
category: architecture
last_reviewed: 2026-06-25
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

## Manifest-First Addressables

Addressables를 도입하더라도 `LoadManifestSO / RouteSetLoadManifestSO`가 로딩 기준이다. 직접 참조는 제거하지 않고 authoring source, fallback, registry lookup key로 유지한다.

1차 적용 범위는 VFX / Presentation 에셋이다. 몬스터 본체, 보스 본체, 무기 actor, projectile / hitbox 같은 gameplay actor는 후순위로 둔다.

### Runtime Policy

- `LoadingBootstrapConfigSO.assetProviderMode`가 Addressables면 `AddressableAssetProvider`가 `PresentationAssetProvider` override로 설치된다.
- Addressables load 성공 시 loaded asset을 사용한다.
- address 누락, Addressables location 누락, load 시작 실패, load 실패는 direct reference fallback을 사용한다.
- fallback은 operation success로 완료하되 에디터 / 개발 빌드에서 warning과 loading debug history를 남긴다.

### Editor Workflow

새 route / scene 작업 후 권장 순서는 다음과 같다.

1. `Tools/Loading/RouteSet Manifest Builder`
2. `Tools/Loading/Addressable Bundle Planner`
3. `Tools/Loading/Build Addressable Registry`
4. Addressables Content Build
5. Play test + Loading Debug 확인

### Group Policy

- `BootCommon`: 로딩 / 기본 UI / 부팅 필수 에셋
- `RunCommon`: 런 전체 공통 에셋
- `CombatCommon`: telegraph, damage popup, 공통 전투 VFX
- `RouteShared_{RouteName}`: 같은 route의 corridor / boss 공유 에셋
- `RouteCorridor_{RouteName}`: corridor 전용 에셋
- `Boss_{BossName}`: 보스 전용 대형 VFX / 대사 / 패턴 에셋
- `ReviewNeeded`: 자동 분류가 애매한 에셋

초기 packing은 `Pack Together`만 사용한다. `Pack Separately`는 큰 에셋이 같은 group 안에서 독립적으로 사용되는 문제가 확인될 때 후속으로 검토한다.
