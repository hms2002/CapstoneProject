---
status: proposed
authority: refactor-backlog
category: refactor-item
last_reviewed: 2026-05-16
---

# Scene Run State Lifecycle Ownership Split

## Current Problem

The P1 helper/file split is complete, but several scene/run/save owners still expose compatibility names and lifecycle surfaces that are too large to change safely as a code-only refactor.

- `ScenePortalTravelService` remains the static portal travel compatibility entry point and still owns transition lock checks, manager lookup, and high-level execution handoff.
- `GamePlayDataManager` is still named like gameplay save data while acting as the volatile run-session state holder for pending transition, pending player runtime state, run timer values, pending rewards, affection deltas, shortcuts, and merchant state.
- `RunProgressCoordinator` still owns boss battle-end subscriptions, run-scoped dedupe sets, reward-ready event dispatch, final boss timer pause calls, and legacy reward/portal fallback handoff.
- `PlayerSceneRestoreBootstrapper` still owns restore lifecycle, registry subscription, retry timing, runtime restorer rebinding, ordered restore handoff, confirmation coroutine, and pending-state consumption.

## Why It Exists

These classes are scene-facing or lifecycle-facing compatibility owners. Renaming or moving them can affect scenes, prefabs, runtime bootstrap order, static entry points, and manual play flows.

The P1 work intentionally extracted policy/execution helpers first without changing MonoBehaviour identity or scene-facing contracts.

## Target Shape

- Keep a narrow compatibility entry for portal travel while moving lifecycle decisions behind explicit scene/run services.
- Give volatile run-session state an explicit owner or facade name that cannot be mistaken for durable profile save data.
- Keep player runtime restore confirmation and pending-state consumption under a clearly named restore lifecycle owner.
- Keep boss battle-end progress, reward readiness, portal activation fallback, and timer pause as a visible boss/run bridge until `BossDrop` migration is complete.

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
- `BossDrop` prefab-reference migration or boss reward/portal behavior changes.
- A planned scene/prefab reference pass where Unity Editor import/compile and play verification are available.

## Related Documents

- `Docs/RefactorBacklog/SceneRunStateBoundarySplit.md`
- `Docs/StructureMemory/ScriptSystems/SceneRuntimeSaveStructure.md`
- `Docs/Architecture/RuntimeSaveArchitecture.md`
- `Docs/Architecture/SceneDomainBootstrapArchitecture.md`
- `Docs/RefactorBacklog/BossDropResponsibilitySplit.md`

## Status

`proposed`

This is a P2 follow-up. It should not block P1 closure because the remaining work is lifecycle, naming, and scene-facing contract design rather than helper/file boundary extraction.
