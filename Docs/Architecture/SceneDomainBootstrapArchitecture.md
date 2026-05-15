---
status: active
authority: source-of-truth
category: architecture
last_reviewed: 2026-05-15
---

# Scene Domain Bootstrap Architecture

This document defines how the project connects `TitleScene` to gameplay scenes. It is the source-of-truth layer for app/session bootstrap ownership; runtime save rules still belong to [`RuntimeSaveArchitecture.md`](./RuntimeSaveArchitecture.md), and loading asset scope rules still belong to [`LoadingScopes.md`](./LoadingScopes.md).

## Core Rule

`TitleScene` is both the app entry scene and the gameplay session boundary.

- App-scope services may exist before, during, and after `TitleScene`.
- Gameplay-scope runtime presentation and gameplay session objects must not treat `TitleScene` as active gameplay.
- Returning to `TitleScene` must end or clear the active run/session path before gameplay runtime presentation is recreated.
- Title-local UI, cameras, and menu presentation should remain scene-authored unless a fallback/prototype path is explicitly documented.

## Current Scope Model

| Scope | Current owner or entry | Responsibility |
| --- | --- | --- |
| App scope | `SceneDomainCoordinator` lifecycle plus `SceneDomainAppScopeServices` | Keep transition, fade, loading, route, run-session store, cursor, save, settings, audio, and similar app services available across title and gameplay. |
| Title scene scope | `TitleMenuController`, `TitleProfileSlotService`, title-local panels/canvas/camera | Own title menu input, profile slot selection, scene-authored title UI, and the initial request to enter a game scene. |
| Gameplay session scope | `SceneDomainCoordinator` lifecycle plus `SceneDomainGameplaySessionScope` | Prepare gameplay camera rig, camera shake, route BGM, and gameplay presentation effects after a non-title scene loads. |
| Title cleanup scope | `SceneDomainCoordinator` lifecycle plus `SceneDomainTitleCleanupScope` | Stop gameplay music/loading presentation, clear route plans, and remove persistent gameplay UI/camera services when `TitleScene` loads. |
| Run-session state | `GamePlayDataManager` | Hold volatile run state such as active-run flag, pending transition, pending player state, timer values, rewards, affection deltas, shortcut unlocks, and merchant state. |
| Durable profile state | `GameDataManager` | Load/save the selected profile slot and durable profile data. |

## Runtime Flows

### Cold Boot Into Title

Before the first scene load, `SceneDomainCoordinator.AutoBootstrap()` creates the coordinator and app-scope services. When `TitleScene` is the active scene, scene-domain handling runs title cleanup and does not create gameplay session scope. `CameraBootstrap` guards the title scene through `CameraBootstrapScenePolicy`, which delegates the title-scene name check to `SceneDomainScenePolicy`; it skips first-load creation on title and disables any runtime camera rig in favor of scene cameras if it is asked to ensure a rig while title is active.

### Title Profile Launch

`TitleMenuController` prepares title UI and ensures `SceneDomainCoordinator`. When a slot is selected, `TitleProfileSlotService` creates a `TitleProfileLaunchRequest`, then `TitleProfileLaunchService` prepares the selected profile by loading the slot, marking it initialized, and saving through `GameDataManager` when available. `TitleMenuController` then asks `SceneTransitionCoordinator` to load the request target scene. The current default target is `ProtoTypeHub`.

There is no pending static title launch context. The reliable handoff is the loaded durable profile slot plus the target scene transition request.

### Title Quick Start

`TitleSceneStartInput` is an optional shortcut path. It loads its configured target scene through `SceneTransitionCoordinator` when enabled, but it bypasses profile slot selection and `TitleProfileLaunchService`. Treat it as a debug/prototype entry path unless a future task explicitly promotes it into the profile launch flow.

### Gameplay Scene Load

For every non-title scene load, `SceneDomainCoordinator` ensures app-scope services, classifies the loaded scene through `SceneDomainScenePolicy`, then delegates gameplay session preparation to `SceneDomainGameplaySessionScope`. This currently prepares the camera runtime rig, camera shake service, run route BGM service, and scene presentation effects. Scene portal travel and run start/end policy remain owned by the scene/run transition layer, not by title UI.

### Return To Title

`UIManager.ReturnToTitleScreen()` is the compatibility entry point from stack UI. It builds a `TitleReturnRequest`, resolves the title scene through `TitleSceneNameResolver`, then delegates popup/prompt cleanup, `GamePlayDataManager.EndRun(RunEndReason.None)`, and scene transition fallback to `TitleReturnService`. Once `TitleScene` loads, `SceneDomainCoordinator` delegates title-side cleanup to `SceneDomainTitleCleanupScope`.

### Editor Direct Scene Start

In editor-only direct starts outside `TitleScene`, `SceneDomainCoordinator` loads development slot `0`, marks the profile initialized, resets gameplay session data, clears route plans, and either skips hub spawn presentation for `ProtoTypeHub` or starts a run and seeds route context for gameplay scenes. This is editor iteration support, not player-facing title flow.

## Boundary Rules

- `TitleScene` must not require gameplay camera rig, gameplay BGM route state, gameplay global UI root, or active run state to render its authored menu.
- Gameplay scenes should not rely on title-local UI objects after transition.
- Persistent app services must be safe to create before title UI awakens and safe to reuse after returning to title.
- `GamePlayDataManager` is volatile run-session state; do not treat pending transition/player state as durable profile data.
- `GameDataManager` owns durable profile slot data; title menu code may choose/load a slot but should not become the general persistence owner.
- `SceneTransitionCoordinator` owns fade/loading scene transitions. It does not own run lifecycle decisions.
- `CameraBootstrap` and route BGM are presentation support. New progression rules should live in scene/run policy, not camera or audio services.

## Refactor Notes

The current structure works and now keeps title/bootstrap policy behind same-file helper boundaries: scene classification and scope services in `SceneDomainCoordinator.cs`, title return execution in `UIManager.cs`, and camera title guards in `CameraBootstrap.cs`. The implementation remains behavior-preserving; future physical file moves should wait until Unity project-file regeneration/static validation can reliably include new `.cs` files.

Related current maps:

- [`SceneRuntimeSaveStructure.md`](../StructureMemory/ScriptSystems/SceneRuntimeSaveStructure.md)
- [`LoadingPresentationStructure.md`](../StructureMemory/ScriptSystems/LoadingPresentationStructure.md)
- [`SceneRunStateBoundarySplit.md`](../RefactorBacklog/SceneRunStateBoundarySplit.md)
