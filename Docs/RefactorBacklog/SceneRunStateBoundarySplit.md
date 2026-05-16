---
status: resolved
authority: refactor-backlog
category: refactor-item
last_reviewed: 2026-05-16
---

# Scene Run State Boundary Split

## Status

resolved

## Current Problem

Scene travel, run session state, runtime restore, and boss battle-end progress were close enough that future changes could blur ownership.

The P1 refactor resolved the behavior-preserving helper/file boundary work:

- `ScenePortalTravelService` is now a compatibility entry point that delegates route/run-policy/context decisions, execution, and player runtime capture details to dedicated helper files.
- `GamePlayDataManager` still acts as the volatile run-session state holder, but pending progress commit/clear policy lives in a dedicated helper file.
- `RunProgressCoordinator` still bridges boss battle-end to reward readiness, portal fallback, and timer pause, while route/final-boss/context policy lives in a dedicated helper file.
- `PlayerSceneRestoreBootstrapper` still owns restore lifecycle and pending-state consumption, while restore planning and execution validation live in dedicated helper files.

## Why It Existed

- Scene travel was the natural place to connect portal routes, run lifecycle, player capture, and scene loading.
- Runtime restore needs a temporary cross-scene store, and `GamePlayDataManager` became the central pending-state holder.
- Boss clear flow needs to bridge battle-end events to run progress, rewards, portals, and timer pause while route-linked battle-end authoring remains scene/prefab-sensitive.
- The original structure worked, but helper/file boundaries were not explicit enough for future scene/run/save refactors.

## Target Shape

- `Portal Travel Orchestration`
  - Owns portal interaction handoff, route resolution, transition context creation, and scene-load request.
  - Delegates run lifecycle decisions and player runtime capture to narrower collaborators.
- `Run Session State`
  - Owns volatile run-scoped data: pending transition, pending player runtime state, run timer values, pending run rewards, pending affection, shortcut unlocks, and merchant runtime state.
  - Is documented separately from persistent save data.
- `Persistent Save Data`
  - Keeps `GameDataManager`, `GameDataRepository`, and `GameDataSaveCoordinator` focused on durable profile data and save scheduling.
- `Player Runtime Capture / Restore`
  - Keeps capture/restore order and ownership under the runtime save architecture.
- `Boss BattleEnd / Run Progress Bridge`
  - Keeps boss defeat, reward readiness, portal activation, and final-boss timer pause visibly connected to boss battle-end structure.

## Risks

- Moving MonoBehaviours, scene-facing services, or serialized fields risks broken Unity references.
- Run start/end timing, pending player state consumption, and portal route advancement can regress if ownership is split without play verification.
- Boss reward and portal handling can duplicate behavior if it diverges from `BossDropResponsibilitySplit`.
- Save behavior can become inconsistent if run-session pending data is mistaken for durable profile data.

## Refactor Trigger

This P1 item should remain resolved. For future lifecycle/naming work, use `SceneRunStateLifecycleOwnershipSplit`.

Create a new focused implementation when one of these happens:

- Adding a new transition type that does not fit the current `ScenePortalTravelService` sequence.
- Changing run start/end behavior, time-over return, boss clear return, or portal route progression.
- Adding new run-scoped pending data to `GamePlayDataManager`.
- Editing player runtime capture/restore flow, pending-state consumption, or restore timing.
- Changing boss reward/portal/timer behavior or route-linked boss battle-end authoring.

## Related Documents

- `Docs/StructureMemory/ScriptSystems/SceneRuntimeSaveStructure.md`
- `Docs/StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md`
- `Docs/Architecture/RuntimeSaveArchitecture.md`
- `Docs/RefactorBacklog/BossDropResponsibilitySplit.md`
- `Docs/RefactorBacklog/SceneRunStateLifecycleOwnershipSplit.md`
- `Assets/LeeJunMo/Script/SceneManagement/ScenePortalTravelService.cs`
- `Assets/LeeJunMo/Script/SceneManagement/PlayerSceneRestoreBootstrapper.cs`
- `Assets/LeeJunMo/Script/SaveData/GamePlayDataManager.cs`
- `Assets/LeeJunMo/Script/SceneManagement/RunProgressCoordinator.cs`
- `Assets/LeeJunMo/Script/SceneManagement/PlayerRuntimeRestoreCoordinator.cs`
- `Assets/LeeJunMo/Script/SaveData/GameDataManager.cs`
- `Assets/LeeJunMo/Script/SaveData/GameDataSaveCoordinator.cs`

## Completed P1 Slices

- Added `BossRunProgressRequest`, `BossRunProgressResult`, and `BossRunProgressPolicy`.
- Moved boss route-key resolution, final-route detection, boss identity-key resolution, and boss reward context construction policy into the helper.
- Added `ScenePortalTravelRequest`, `ScenePortalTravelPlan`, and `ScenePortalTravelPlanner`.
- Moved portal route resolution, run transition directive selection, transition policy resolution, and transition context construction into the travel planner.
- Added `RunSessionProgressCommitRequest` and `RunSessionProgressCommitPolicy`.
- Moved pending magic stone, elapsed time, clear count, affection, shortcut unlock, and run-scoped runtime-state clearing policy into the commit helper.
- Added `PlayerRuntimeRestoreRequest`, `PlayerRuntimeRestoreResult`, and `PlayerSceneRestorePlanner`.
- Moved player target lookup, restore request/result construction, scene eligibility, item database readiness, pending equipment resolvability, player system context gathering, and post-restore equipment-state matching into the restore planner.
- Added `ScenePortalTravelExecutionRequest`, `ScenePortalTravelExecutionResult`, `ScenePortalTravelExecutor`, `ScenePortalPlayerRuntimeCaptureRequest`, and `ScenePortalPlayerRuntimeCaptureService`.
- Moved optional player runtime capture, scene-transition ability cleanup before capture, run start/end side-effect execution, pending transition storage, route consumption notification, and scene load handoff into travel execution/capture helpers.
- Added `PlayerSceneRestoreExecutionService`.
- Moved pending equipment resolvability checks, player component gathering, and restore result creation into the restore execution helper.
- Moved P1 helper types out of `RunProgressCoordinator.cs`, `ScenePortalTravelService.cs`, `GamePlayDataManager.cs`, and `PlayerSceneRestoreBootstrapper.cs` into dedicated helper files.

The scene run flow bundle was manually execution-tested after the earlier helper split. This P1 item is resolved because the behavior-preserving helper/file boundary work is complete. Future lifecycle, naming, and scene-facing ownership work should use the P2 follow-up instead of reopening this P1 item by default.
