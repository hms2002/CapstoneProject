---
status: resolved
authority: refactor-backlog
category: refactor-item
last_reviewed: 2026-07-05
---

# Scene Domain Bootstrap Boundary Split

## Status

resolved

## Current Problem

Title-to-game bootstrap behavior works, and the current same-file boundary split is complete:

- `SceneDomainCoordinator` still owns bootstrap lifecycle and editor direct-start orchestration, while loaded-scene classification plus app/gameplay/title scope service execution sit behind same-file helpers.
- `TitleMenuController` owns title-local selection flow and scene transition request. Profile slot load/save has moved into same-file `TitleProfileLaunchService`.
- `UIManager.ReturnToTitleScreen()` remains the compatibility entry point, while title scene name resolution and return execution sit behind same-file `TitleSceneNameResolver` and `TitleReturnService`.
- `CameraBootstrap` keeps camera runtime ownership, while title-scene guard decisions route through same-file `CameraBootstrapScenePolicy` backed by `SceneDomainScenePolicy`.

## Why It Exists

The title screen was connected to the playable flow incrementally. The current implementation kept iteration moving by placing policy near the component that needed it: title UI owns launch, scene-domain owns runtime scope, UIManager owns pause/title return, and camera owns title-safe rig behavior.

## Target Shape

- Keep `SceneDomainCoordinator` as the bootstrap lifecycle owner, but move scene classification and scope decisions into same-file helper policy types before any physical file/layer move.
- Keep title menu UI scene-authored; `TitleProfileLaunchService` should remain the profile launch preparation boundary until a larger title/profile flow exists.
- Keep `UIManager.ReturnToTitleScreen()` compatible, with title-return policy narrowed into same-file helper types before adding more return sources.
- Keep `CameraBootstrap` title guard aligned with the scene-domain policy so title-local cameras are not overridden by gameplay runtime rigs.

Completed slice:

- Removed unused `TitleProfileLaunchContext`.
- Added same-file `TitleProfileLaunchResult` and `TitleProfileLaunchService` in `TitleProfileSlotService.cs`.
- Kept valid title launches loading their target scene even when `GameDataManager.Instance` is unavailable, matching previous behavior.
- Added same-file `SceneDomainLoadAction`, `SceneDomainSceneInfo`, `SceneDomainLoadDecision`, `SceneDomainScenePolicy`, `SceneDomainAppScopeServices`, `SceneDomainGameplaySessionScope`, `SceneDomainTitleCleanupScope`, and development direct-start policy helper now named `SceneDomainDevelopmentStartPolicy`.
- Kept `SceneDomainCoordinator` as the Unity lifecycle owner while moving loaded-scene action selection and runtime scope service execution behind helper boundaries.
- Added same-file `TitleReturnRequest`, `TitleReturnResult`, `TitleSceneNameResolver`, and `TitleReturnService` in `UIManager.cs`.
- Kept `UIManager.ReturnToTitleScreen()` as the compatibility wrapper while moving UI cleanup handoff, run end, scene transition, and direct load fallback into the title return helper.
- Added same-file `CameraBootstrapScenePolicy` in `CameraBootstrap.cs`.
- Removed `CameraBootstrap`'s duplicated title scene name constant and routed title-scene checks through `SceneDomainScenePolicy`.

Resolved scope:

- The title/bootstrap boundary split is resolved for the current same-file helper target.
- Physical movement of scene-domain, title-launch, title-return, and camera title-policy helpers into dedicated `.cs` files was completed during the P1 helper file split.
- `TitleProfileLaunchAction.ContinueRun` persisted active-run semantics remain a separate feature decision, not unresolved bootstrap refactor debt.

## Risks

- Bootstrap order regressions can leave title UI without required app services.
- Cleanup changes can destroy title-local objects or leave gameplay DDOL objects active on title.
- Profile launch changes can accidentally bypass slot loading, save initialization, or continue-run behavior.
- Return-to-title changes can skip run progress commit/clear behavior in `GamePlayDataManager.EndRun(...)`.
- Editor direct-start changes can break iteration-only route seeding or hub spawn presentation skips.

## Refactor Trigger

- Adding a new title entry mode, continue-run flow, profile slot behavior, or title quick-start behavior.
- Changing return-to-title, quit, run end, time-over return, or scene transition behavior.
- Adding more app-scope or gameplay-scope bootstrap services, especially if the helper boundaries need dedicated files.
- Editing `TitleMenuController`, `UIManager.ReturnToTitleScreen()`, `CameraBootstrap`, or `GamePlayDataManager` run lifecycle behavior.

## Related Documents

- `Docs/Architecture/SceneDomainBootstrapArchitecture.md`
- `Docs/Architecture/RuntimeSaveArchitecture.md`
- `Docs/Architecture/LoadingScopes.md`
- `Docs/StructureMemory/ScriptSystems/SceneRuntimeSaveStructure.md`
- `Docs/StructureMemory/ScriptSystems/LoadingPresentationStructure.md`
- `Docs/RefactorBacklog/SceneRunStateBoundarySplit.md`
- `Docs/RefactorBacklog/RuntimePresentationFallbackAuthoringSplit.md`
