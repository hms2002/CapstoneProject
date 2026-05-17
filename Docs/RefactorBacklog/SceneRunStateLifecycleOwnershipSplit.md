---
status: resolved
authority: refactor-backlog
category: refactor-item
last_reviewed: 2026-05-16
---

# Scene Run State Lifecycle Ownership Split

## Current Problem

The P1 helper/file split is complete, and a later source-only P2 slice isolated several lifecycle details, but several scene/run/save owners still expose compatibility names and lifecycle surfaces that are too large to change safely as a code-only refactor.

- `ScenePortalTravelService` remains the static portal travel compatibility entry point, but it now delegates to `ScenePortalTravelCoordinator` before planner/executor helpers handle route, run directive, capture, and scene-load handoff.
- `GamePlayDataManager` is still named like gameplay save data while acting as the volatile run-session state holder for pending transition, pending player runtime state, run timer values, pending rewards, affection deltas, shortcuts, and merchant state. Run start/end state mutation delegates to `RunSessionLifecycleService`, and volatile session field mutation delegates to `RunSessionStateService`.
- `RunProgressCoordinator` still owns boss battle-end subscriptions, run-scoped dedupe sets, reward-ready event dispatch, and final boss timer pause calls. Unhandled reward/portal authoring warnings now delegate to `BossRewardFallbackService`.
- `PlayerSceneRestoreBootstrapper` still owns restore lifecycle, registry subscription, retry timing, runtime restorer rebinding, ordered restore handoff, and confirmation coroutine timing. Pending-state confirmation and consumption now delegate to `PlayerSceneRestoreConfirmationService`.

## Why It Exists

These classes are scene-facing or lifecycle-facing compatibility owners. Renaming or moving them can affect scenes, prefabs, runtime bootstrap order, static entry points, and manual play flows.

The P1 work intentionally extracted policy/execution helpers first without changing MonoBehaviour identity or scene-facing contracts.

## Target Shape

- Keep a narrow compatibility entry for portal travel while moving lifecycle decisions behind explicit scene/run services.
- Give volatile run-session state an explicit owner or facade name that cannot be mistaken for durable profile save data.
- Keep player runtime restore confirmation and pending-state consumption under a clearly named restore lifecycle owner.
- Keep boss battle-end progress, reward readiness, reward/portal handled-state reporting, and timer pause as a visible boss/run bridge while the route-linked special reward preset and scene-authored chest/portal activation path is validated in Unity.

## Risks

- Renaming or moving MonoBehaviours can break scene/prefab references.
- Changing static entry points can break portal, title return, time-over return, and boss clear flows.
- Changing restore pending-state consumption can duplicate or lose player runtime state.
- Changing boss bridge ownership can duplicate rewards or portal activation.
- Save behavior can regress if volatile run-session data is treated as durable profile data.

## Refactor Trigger

Start this only when one of these is already being edited or verified:

- Portal/run/save/boss battle-end flow changes.
- New transition type or continue-run semantics.
- New volatile run-session state.
- Player runtime restore lifecycle or confirmation timing changes.
- Boss reward/portal behavior changes or route-linked special reward preset / scene-authored chest/portal authoring migration issues.
- A planned scene/prefab reference pass where Unity Editor import/compile and play verification are available.

## Related Documents

- `Docs/RefactorBacklog/SceneRunStateBoundarySplit.md`
- `Docs/StructureMemory/ScriptSystems/SceneRuntimeSaveStructure.md`
- `Docs/Architecture/RuntimeSaveArchitecture.md`
- `Docs/Architecture/SceneDomainBootstrapArchitecture.md`
- `Docs/RefactorBacklog/BossDropResponsibilitySplit.md`

## Status

`resolved`

Source-only P2 slice complete:

- Added `RunSessionLifecycleService` for run start/end volatile-state mutation and route-plan cleanup handoff.
- Added `PlayerSceneRestoreConfirmationService` for restore confirmation checks and pending player-state consumption.
- Added `BossRewardFallbackService` for unhandled boss reward/portal authoring warnings after reward-ready event dispatch.
- Preserved current public entry points, MonoBehaviour identities, serialized fields, scene/prefab references, route behavior, save flush timing, and reward/portal handled-state semantics.
- The generated `Assembly-CSharp.csproj` now includes the new lifecycle and boss fallback helper files, and Visual Studio MSBuild errors-only verification passed for `Assembly-CSharp.csproj` on 2026-05-16. This is source/project-file verification only; Unity Editor import/play verification is still required for scene-facing flows.
- Boss reward/portal unhandled branches now emit editor/development warnings when no dedicated handler marks the context handled.

Final P2 compatibility facade slice complete:

- Added `RunSessionStateService` for run timer, pending transition, pending player state, pending magic stone, pending affection, and pending shortcut mutation.
- Changed `GamePlayDataManager` public methods to delegate volatile run/session state operations to `RunSessionStateService`.
- Added `ScenePortalTravelCoordinator` and changed `ScenePortalTravelService.TryTravel(...)` into a thin static compatibility facade.
- Chose the compatibility facade shape as the P2 target instead of renaming scene-facing MonoBehaviours or changing static portal call sites.

Residual follow-up:

- Unity Editor import/play verification is still required for portal movement, run start/end, player restore, and boss battle-end flows.
- A future naming migration for `GamePlayDataManager` should only happen during an explicit scene/prefab reference migration pass.
