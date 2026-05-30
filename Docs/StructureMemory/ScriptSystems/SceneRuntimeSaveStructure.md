---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-05-20
---

# Scene Runtime Save Structure

## Purpose

Map scene/run transition, title-to-game bootstrap, player runtime capture/restore, save data, run timer, map, and shortcut scripts.

## Current Structure

| Area | Count | Responsibility |
| --- | ---: | --- |
| Scene / Run Transition | 39 | Scene domain, player runtime capture/restore, portals, run progress, transition policies, route catalogs, title profile flow. |
| Save Data | 8 | Game/profile/run data managers, repositories, runtime state DTOs, merchant/shortcut progress. |
| Run Timer | 6 | Run timer HUD, timer policy/config, time limit system, time-over return flow. |
| Map / Shortcuts | 11 | Shortcut doors, construction site tilemap modules, door ID helpers, map gimmicks, legacy map interaction object. |

### Scene / Run Transition Breakdown

| Area | Count | Responsibility |
| --- | ---: | --- |
| Scene Transition / Fade | 11 | Scene transition services/policies, fade transition, scene domain/loader support, and transition flow helpers. |
| Player Runtime Snapshot / Restore | 10 | Player runtime state capture/restore, restore catalog/coordinator, spawn runtime policy, spawner, spawn point, and restore bootstrap. |
| Run Progress / Route | 7 | Run progress coordinator, portal route manager/catalog, route set plan, and route support data. |
| SceneManagement Other | 4 | Miscellaneous scene management helpers that do not yet justify a narrower responsibility area. |
| Title / Profile Entry | 3 | Title menu/controller and profile slot service flow. |
| Portal / Scene Entry Points | 3 | Scene portal and scene entry/exit point helpers. |
| Scene Runtime Services | 1 | Scene runtime service owner. |

### Save Data Breakdown

| Area | Count | Responsibility |
| --- | ---: | --- |
| Game Data Core | 4 | Game data model, manager, repository, and save coordinator. |
| Gameplay Data | 2 | Gameplay data model and manager. |
| Shortcut Progress | 1 | Shortcut progress service. |
| Merchant Runtime State | 1 | Merchant runtime state data. |

## Key Files

- `Assets/LeeJunMo/Script/SceneManagement/ScenePortalTravelService.cs`
- `Assets/LeeJunMo/Script/SceneManagement/SceneDomainCoordinator.cs`
- `Assets/LeeJunMo/Script/SceneManagement/TitleMenuController.cs`
- `Assets/LeeJunMo/Script/SceneManagement/TitleProfileSlotService.cs`
- `Assets/LeeJunMo/Script/SceneManagement/SceneTransitionCoordinator.cs`
- `Assets/LeeJunMo/Script/SceneManagement/PlayerSceneRestoreBootstrapper.cs`
- `Assets/LeeJunMo/Script/SceneManagement/PlayerRuntimeState.cs`
- `Assets/LeeJunMo/Script/SceneManagement/PlayerRuntimeRestoreCoordinator.cs`
- `Assets/LeeJunMo/Script/SceneManagement/RunProgressCoordinator.cs`
- `Assets/LeeJunMo/Script/SaveData/GamePlayDataManager.cs`
- `Assets/LeeJunMo/Script/SaveData/GameDataSaveCoordinator.cs`
- `Assets/LeeJunMo/Script/Map/Construction/ConstructionSiteTilemapModule.cs`

## Ownership And Lifecycle

- Scene transition should coordinate route/portal flow without owning player runtime state semantics.
- Title/game scene bootstrap rules should follow `Docs/Architecture/SceneDomainBootstrapArchitecture.md`: `TitleScene` is the app entry scene and gameplay session boundary.
- `SceneDomainCoordinator` is the current app-scope/gameplay-scope bootstrap lifecycle owner. It owns singleton lifecycle, Unity scene-loaded subscription, and editor direct-start orchestration, while helper files now own loaded-scene classification (`SceneDomainScenePolicy`), app-scope service ensure (`SceneDomainAppScopeServices`), gameplay session service ensure (`SceneDomainGameplaySessionScope`), title cleanup (`SceneDomainTitleCleanupScope`), and editor direct-start constants/eligibility (`SceneDomainEditorDirectStartPolicy`).
- `TitleMenuController` owns title-local menu flow and scene load request. `TitleProfileSlotService` resolves the launch request target and slot action: empty-slot `StartNewRun` uses `newProfileTargetSceneName` (`TutorialCorridor` by default), while existing/default launches use `targetSceneName`. `TitleProfileLaunchService` prepares the selected durable profile through `GameDataManager`.
- `UIManager.ReturnToTitleScreen()` remains the stack UI compatibility entry point for gameplay-to-title return. Same-file `TitleSceneNameResolver` and `TitleReturnService` now own title scene name resolution, UI prompt/popup cleanup handoff, run end, `SceneTransitionCoordinator` scene load request, and direct `SceneManager.LoadScene(...)` fallback.
- Player runtime capture/restore should follow `Docs/Architecture/RuntimeSaveArchitecture.md`.
- Save data managers/repositories own persistence; UI and scene objects should not become persistence owners.
- Run timer pause/complete behavior is progression-sensitive and should be coordinated through run progress/timer owners.
- Treat `ScenePortalTravelService.TryTravel(...)` as the portal travel compatibility wrapper. It delegates to `ScenePortalTravelCoordinator`, while route resolution, run transition directive selection, transition policy resolution, and transition context construction sit in `ScenePortalTravelPlanner`. Travel execution sits in `ScenePortalTravelExecutor`, and player runtime capture/transition cleanup sits in `ScenePortalPlayerRuntimeCaptureService`.
- `ScenePortal` owns only the pre-travel entrance presentation: it can pull, shrink, and rotate the player before calling `TryTravel(...)`, then the existing travel service still owns route resolution, run state, transition context, and runtime capture. Temporary `PlayerCinematicProtection` and `GameFlowInputBlocker` locks are released before `TryTravel(...)` so presentation/UI block tags are not captured. Portal interaction must stay disabled while `SceneTransitionCoordinator` is already active, because the delayed post-presentation travel request will be rejected by the travel service.
- Keep shared `ScenePortal.prefab` semantic-neutral. Hub start portals own `HubToRunStart` plus `RunRouteCatalogSO` on their scene instance, while corridor/boss/battle-end portals can use `TransitionType.None` so `PortalRouteManager` resolves the effective route from the active run plan and portal scene.
- Tutorial fixed scene travel uses `TutorialScenePortal`, not `ScenePortal`, when the target is an authored tutorial destination such as `DarkLord_Tutorial` and should not consume a run route plan.
- Boss battle-end portal placement comes from the authored inactive `ScenePortal` object referenced by the scene `BossBattleEndHandler.exitPortal`. The handler only toggles that object; it does not instantiate, detach, or move portals.
- Treat `GamePlayDataManager` as run-session state, not durable save ownership. Pending run progress commit/clear policy sits in `RunSessionProgressCommitPolicy`, run start/end mutation sits in `RunSessionLifecycleService`, and volatile timer/pending transition/player/reward/affection/shortcut mutation sits in `RunSessionStateService`. The manager still owns singleton lifecycle, public compatibility APIs, run events, and save flush orchestration.
- Treat `RunProgressCoordinator` as a boss battle-end/run-progress bridge when reviewing boss reward, portal, and timer behavior. Boss reward modifier data is read through `RunRewardModifierSnapshot`, route-key/final-boss/context policy sits in `BossRunProgressPolicy`, and unhandled reward/portal authoring warnings sit in `BossRewardFallbackService`.
- Treat `PlayerSceneRestoreBootstrapper` as the player runtime restore lifecycle and handoff owner. Player lookup, scene eligibility, item database readiness, and post-restore equipment matching sit in `PlayerSceneRestorePlanner`. Pending equipment resolvability, player component gathering, and restore result creation now sit behind `PlayerSceneRestoreExecutionService`; confirmation and pending-state consumption sit in `PlayerSceneRestoreConfirmationService`.
- Run-internal special NPCs cross this boundary when they unlock permanent shortcuts, track construction progress across runs, or move the player within the same scene. Construction progress now uses `GameData.runSpecialNpcData.constructionRecords` plus `GamePlayData.pendingRunSpecialNpcConstructionStarts`, and run-active starts commit through `RunSessionProgressCommitPolicy`. Completed construction toggles scene-authored `ConstructionSiteTilemapModule` blocked/open roots and keeps the durable shortcut state on `DoorObject` / `ShortcutProgressService`. Same-scene teleport remains separate from `ScenePortal` scene transitions.

## Refactor Candidates

- `Docs/RefactorBacklog/SceneRunStateBoundarySplit.md` records the resolved P1 helper/file split between portal travel orchestration, run-session state, persistent save data, player runtime capture/restore, and boss battle-end/run-progress bridging.
- `Docs/RefactorBacklog/SceneRunStateLifecycleOwnershipSplit.md` records the resolved P2 compatibility facade choice for scene-facing contracts. Reopen it only for a planned naming/scene-reference migration.
- `Docs/RefactorBacklog/SceneDomainBootstrapBoundarySplit.md` records the resolved title/game bootstrap boundary split. Reopen that area only when adding new title entry modes, continue-run semantics, return-to-title behavior, app/gameplay bootstrap services, or camera title behavior.
- `ScenePortalTravelService` remains the portal travel compatibility entry point. Coordinator, planner, executor, and capture helpers own the implementation details; static entry ownership is now intentionally retained as a compatibility facade.
- `GamePlayDataManager` is documented as volatile run-session state. It holds pending transition, pending player runtime state, run timer values, pending run rewards, affection deltas, shortcut unlocks, and merchant state before durable save commit. Pending progress commit/clear mutation is isolated in `RunSessionProgressCommitPolicy`, start/end mutation is isolated in `RunSessionLifecycleService`, and field-level volatile mutation is isolated in `RunSessionStateService`.
- `RunProgressCoordinator` remains cross-linked with boss battle-end structure because it handles boss defeat, reward readiness, final boss timer pause, and boss reward modifier snapshot consumption. It delegates route-key resolution, final-route detection, identity-key resolution, and reward-context construction to `BossRunProgressPolicy`, and unhandled reward/portal authoring warnings to `BossRewardFallbackService`.
- `PlayerSceneRestoreBootstrapper` remains the restore lifecycle owner around registry subscription, retry coroutine timing, warning/error reporting, runtime restorer rebinding, ordered restore handoff, and confirmation coroutine timing. Restore planning, execution validation, confirmation, and pending-state consumption live in dedicated helper files; remaining lifecycle debt is tracked in the P2 lifecycle backlog.

## Extension Entry Points

- Add scene transition behavior through transition/fade and portal/route buckets.
- Add title/game bootstrap behavior through the scene-domain architecture, not through ad hoc title UI or camera/audio services.
- Add runtime state restoration through player runtime snapshot/restore buckets.
- Add save data only through save coordinator/repository/data model patterns.
- For run special NPCs, use [Run Special NPC Structure](./RunSpecialNpcStructure.md) as the feature map and this document for shortcut/save/teleport persistence boundaries.
- For construction NPCs, durable progress belongs in `RunSpecialNpcSaveData`, pending run starts belong in `GamePlayData`, scene path presentation belongs in `ConstructionSiteTilemapModule`, and permanent path unlocks should still use `DoorObject` / `ShortcutProgressService`.

## Known Pitfalls

- Scene, portal, and spawn scripts are prefab/scene-facing; moving or renaming them needs Unity reference review.
- A shared portal prefab must not carry a start-run catalog or hub-start semantic. Boss battle-end now uses authored portal instances, and hub-start catalog ownership still needs validation after portal prefab or scene portal changes.
- Pre-travel portal presentations must not start while a previous scene transition/fade is still active. Otherwise the entrance animation can complete and then restore the player because `ScenePortalTravelService.TryTravel(...)` rejects overlapping scene transitions.
- Do not add tutorial-only fixed scene jumps to `TransitionType` unless they become real run route semantics. Use `TutorialScenePortal` for direct tutorial destinations.
- Runtime state restore can silently break if ownership shifts between player, weapon, relic, and save data.
- Title return can silently break if run end, route clear, gameplay UI cleanup, and title scene transition drift apart.
- Title launch currently prepares profile data directly before target scene load. New profiles enter the tutorial target, while existing/default launches keep the normal target; a future continue-run persistence model would need explicit runtime state and target-scene restore rules.
- Do not treat pending run data in `GamePlayDataManager` as already-persisted profile data.
- Scene/run/save helper boundaries have been moved to dedicated helper files. Generated project files now include the recent helper files and Visual Studio MSBuild errors-only verification passes, but Unity Editor import/compile confirmation remains the final verification path for scene-facing flows.
- Do not split boss progress/reward/portal behavior without checking the resolved `Docs/RefactorBacklog/BossDropResponsibilitySplit.md` and the current battle-end validator behavior.
- Do not claim compile/import safety without Unity verification after schema or MonoBehaviour changes.
- Construction NPC state now adds save-schema fields. Keep `GameDataManager.NormalizeLoadedData()` and save-time normalization updated for existing profiles, and do not claim Unity import safety without Editor verification.
- Construction site additive tilemaps require active-object filtering in tilemap scanners. If a consumer searches inactive tilemaps without filtering, unopened `OpenState` ground or closed `BlockedState` walls can affect safety/drop/path decisions.

## Promotion Candidate

Runtime save rules already belong to `Docs/Architecture/RuntimeSaveArchitecture.md`. Title/game bootstrap rules now belong to `Docs/Architecture/SceneDomainBootstrapArchitecture.md`. Keep this map as current script topology for the related scripts.
