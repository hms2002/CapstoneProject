---
status: resolved
authority: refactor-backlog
category: refactor-item
last_reviewed: 2026-05-16
---

# BossDrop Responsibility Split

## Status

resolved

## Original Problem

`BossDrop` historically held boss reward, portal, timer/progress, and boss-specific bonus loot responsibilities in one prefab-facing component.

This debt is resolved in source. `BossDrop.cs` and its `.meta` were deleted, runtime/editor source dependencies on the `BossDrop` type were removed, and serialized scene/prefab references to the old script GUID were removed so the deleted script does not become a missing-script component.

The supported boss battle-end path is now split across route-linked special reward presets, a common prefab catalog, scene/prefab anchors, reward spawning, portal activation, and run-progress coordination. `Tools/Validation/Boss Battle-End Migration Validator` remains useful as an authoring validator for stale definition/profile data, missing catalog references, missing components, and missing anchors.

## Why It Existed

- Existing boss prefabs/scenes referenced `BossDrop` fields for chest prefab, portal object, spawn points, magic stone prefab, and boss unique loot.
- Boss reward and portal behavior needed to keep working while the split components, special reward preset path, common prefab catalog, and anchors were introduced.
- RouteSet special reward preset references, common reward/portal prefab catalog data, and anchors needed a migration/validation path before the legacy component could be deleted.
- Reward-spawn helper boundaries were already moved to dedicated files; the final blocker was prefab/scene serialized reference migration.

## Resolved Shape

- `RunProgressCoordinator` owns boss progress events, route-set dedupe, boss defeat handling, and timer pause behavior.
- `CorridorBossRouteSetSO` references an optional `BossSpecialRewardPresetSO`, but does not own common prefabs, portal objects, spawn positions, offsets, magic stone bonuses, field-heal bonuses, or chest-count deltas.
- `BossSpecialRewardPresetSO` owns boss-specific special loot candidates only.
- `BossBattleEndPrefabCatalogSO` owns common treasure chest, magic stone, and portal prefab references.
- `BossBattleEndAnchors` owns reward, scatter, and portal positions on the scene/prefab side.
- `BossRewardSpawner` owns boss reward spawning from `StageLootTable` boss defaults via `LootManager.GenerateBossChestLoot(...)`, route-linked special loot candidates, and additive runtime boss reward modifiers.
- `BossRewardSpawnService` owns actual chest/currency/field-heal spawn execution behind request/result data.
- `BossExitPortalActivator` owns portal activation/instantiation and portal visibility/interaction restoration.
- No runtime path reads `BossDrop` fields. Missing reward/portal authoring is surfaced through validators and editor/development fallback warnings instead of being hidden by the old adapter.

## Remaining Risks

- A boss that previously relied only on deleted `BossDrop` field values can lose reward/portal data until its special reward preset, common catalog reference, portal prefab/reference, and anchors are authored.
- Duplicate reward or portal handling is still possible if multiple scene components are wired to handle the same boss incorrectly.
- Boss-specific bonus loot may be confused with StageLootTable boss defaults if preset/modifier ownership is not kept clear.
- Timer/progress behavior could diverge if boss death paths bypass `RunProgressCoordinator`.
- Auto Fix can create placeholder anchors at the boss position. Those anchors preserve a safe default position but still need manual placement review.

## Reopen Trigger

- A new boss reward modifier, boss-specific reward rule, or boss base reward field is added.
- Boss portal activation behavior changes.
- The boss battle-end validator or Unity import reports missing components, missing anchors, stale definition/profile data, or catalog/preset gaps that require another structural change rather than normal Inspector authoring.

## Related Documents

- `Docs/DecisionLog.md` - `Boss Rewards Use Additive Modifier Aggregates`
- `Docs/SessionLogs/2026-05-14.md` - BossDrop split implementation notes
- `Assets/LeeJunMo/Script/Looting/BossDrop.cs` - deleted legacy adapter
- `Assets/LeeJunMo/Script/Looting/BossRewardSpawner.cs`
- `Assets/LeeJunMo/Script/Looting/BossSpecialRewardPresetSO.cs`
- `Assets/LeeJunMo/Script/SceneManagement/BossBattleEndPrefabCatalogSO.cs`
- `Assets/LeeJunMo/Script/SceneManagement/BossExitPortalActivator.cs`
- `Assets/LeeJunMo/Script/SceneManagement/RunProgressCoordinator.cs`

## Resolution Notes

- `RunProgressCoordinator` keeps route-key, final-route, identity-key, and reward-context construction policy in `BossRunProgressPolicy.cs`, while still owning event dispatch and timer calls.
- `BossRewardFallbackService` now only reports unhandled reward/portal authoring through editor/development warnings after reward-ready event dispatch. It does not dynamically spawn rewards or portals from a route definition.
- `BossRewardSpawner` owns event subscription, owner matching, catalog/preset/anchor resolution, and reward-handled marking, while `BossRewardSpawnService.cs` owns boss chest generation, bonus loot, magic stone, field heal, scatter, and exception-logged spawn execution.
- `BossBattleEndMigrationValidatorWindow` reports stale deleted battle-end/profile data, missing common catalog data, missing boss reward/portal components, optional RouteSet special reward preset state, and missing anchors. Its Auto Fix buttons can create the common catalog, add missing reward/portal/anchor components, assign catalog references, and create placeholder anchor children.
- Manual Unity Editor verification is still required: run the validator, review presets/catalog/anchors in the Inspector, then confirm boss rewards and portals fire once after boss death.
