---
status: active
authority: refactor-backlog
category: refactor-item
last_reviewed: 2026-05-16
---

# BossDrop Responsibility Split

## Status

partially-refactored

## Current Problem

`BossDrop` historically held boss reward, portal, timer/progress, and boss-specific bonus loot responsibilities in one prefab-facing component.

The current code has split most runtime behavior into dedicated components, and `BossRewardSpawner` now delegates actual reward spawn execution to dedicated-file `BossRewardSpawnService` request/result helpers. `BossDrop` remains as a legacy adapter and reference holder for prefab compatibility.

## Why It Exists

- Existing boss prefabs/scenes may still reference `BossDrop` fields for chest prefab, portal object, spawn points, magic stone prefab, and boss unique loot.
- Removing the component immediately would risk broken serialized references.
- Boss reward and portal behavior needed to keep working while the new split components are introduced gradually.
- Reward-spawn helper boundaries are already in a dedicated file. The remaining blocker is scene/prefab reference migration, not same-file helper placement.

## Target Shape

- `RunProgressCoordinator` owns boss progress events, route-set dedupe, boss defeat handling, and timer pause behavior.
- `BossRewardSpawner` owns boss reward spawning from base `StageLootTable`/`LootManager` rewards plus additive boss reward modifiers.
- `BossRewardSpawnService` owns actual chest/currency/field-heal spawn execution behind request/result data.
- `BossExitPortalActivator` owns portal activation and portal visibility/interaction restoration.
- `BossDrop` is removed or reduced to a temporary migration-only component once scenes/prefabs are rewired.

## Risks

- Duplicate reward or portal handling if legacy and new components are both wired incorrectly.
- Prefab reference breakage if `BossDrop` is removed before serialized references are migrated.
- Boss-specific bonus loot may be confused with base stage reward rules if modifier ownership is not kept clear.
- Timer/progress behavior could diverge if boss death paths bypass `RunProgressCoordinator`.

## Refactor Trigger

- Boss scene or prefab wiring is already being edited.
- A new boss reward modifier or boss-specific reward rule is added.
- Boss portal activation behavior changes.
- The team is ready to remove legacy `BossDrop` serialized references from prefabs.

## Related Documents

- `Docs/DecisionLog.md` - `Boss Rewards Use Additive Modifier Aggregates`
- `Docs/SessionLogs/2026-05-14.md` - BossDrop split implementation notes
- `Assets/LeeJunMo/Script/Looting/BossDrop.cs`
- `Assets/LeeJunMo/Script/Looting/BossRewardSpawner.cs`
- `Assets/LeeJunMo/Script/SceneManagement/BossExitPortalActivator.cs`
- `Assets/LeeJunMo/Script/SceneManagement/RunProgressCoordinator.cs`

## Next Refactor Step

Current run-progress note: `RunProgressCoordinator` now keeps route-key, final-route, identity-key, and reward-context construction policy in `BossRunProgressPolicy.cs`, while still owning event dispatch, timer calls, and legacy reward/portal fallback execution.

Current reward-spawn note: `BossRewardSpawner` now owns event subscription, owner matching, legacy reference resolution, and reward-handled marking, while `BossRewardSpawnService.cs` owns chest, base loot, bonus loot, magic stone, field heal, scatter, and exception-logged spawn execution.

Current remaining debt:

- Boss prefabs/scenes may still rely on `BossDrop` public fields for chest prefab, portal object, spawn points, magic stone prefab, and boss unique loot.
- `BossRewardSpawner` and `BossExitPortalActivator` still support `BossDrop` fallback references so existing prefabs keep working.
- `RunProgressCoordinator` still has legacy fallback calls to `BossRewardSpawner.SpawnFromLegacyDrop(...)` and `BossExitPortalActivator.ActivateFromLegacyDrop(...)` when no dedicated handler marks the context handled.
- Removing or shrinking `BossDrop` now requires a planned scene/prefab reference pass and manual play verification, not another code-only helper split.

When boss prefabs are next touched, wire `BossRewardSpawner` and `BossExitPortalActivator` directly, confirm rewards/portal fire once, then remove fallback reliance on `BossDrop` references for that prefab.
