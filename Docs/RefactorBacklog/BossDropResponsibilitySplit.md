---
status: active
authority: refactor-backlog
category: refactor-item
last_reviewed: 2026-05-14
---

# BossDrop Responsibility Split

## Status

partially-refactored

## Current Problem

`BossDrop` historically held boss reward, portal, timer/progress, and boss-specific bonus loot responsibilities in one prefab-facing component.

The current code has split most runtime behavior into dedicated components, but `BossDrop` remains as a legacy adapter and reference holder for prefab compatibility.

## Why It Exists

- Existing boss prefabs/scenes may still reference `BossDrop` fields for chest prefab, portal object, spawn points, magic stone prefab, and boss unique loot.
- Removing the component immediately would risk broken serialized references.
- Boss reward and portal behavior needed to keep working while the new split components are introduced gradually.

## Target Shape

- `RunProgressCoordinator` owns boss progress events, route-set dedupe, boss defeat handling, and timer pause behavior.
- `BossRewardSpawner` owns boss reward spawning from base `StageLootTable`/`LootManager` rewards plus additive boss reward modifiers.
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

When boss prefabs are next touched, wire `BossRewardSpawner` and `BossExitPortalActivator` directly, confirm rewards/portal fire once, then remove fallback reliance on `BossDrop` references for that prefab.
