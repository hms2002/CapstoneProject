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

The supported boss battle-end path is now centered on a scene-authored `BossBattleEndHandler` that references the boss and authored inactive chest/portal objects. Route-linked special reward presets and run-progress coordination stay separate. `Tools/Validation/Boss Battle-End Migration Validator` remains useful as an authoring validator for stale definition/profile data, missing handler coverage, missing authored chest/portal references, stale deleted component/catalog GUIDs, and boss exit portal semantic mistakes.

## Why It Existed

- Existing boss prefabs/scenes referenced `BossDrop` fields for chest prefab, portal object, spawn points, magic stone prefab, and boss unique loot.
- Boss reward and portal behavior needed to keep working while the scene handler and special reward preset path were introduced.
- RouteSet special reward preset references and authored chest/portal references needed a migration/validation path before legacy components could be deleted.
- Reward-spawn helper boundaries were already moved to dedicated files; the final blocker was prefab/scene serialized reference migration.

## Resolved Shape

- `RunProgressCoordinator` owns boss progress events, route-set dedupe, boss defeat handling, and timer pause behavior.
- `CorridorBossRouteSetSO` references an optional `BossSpecialRewardPresetSO`, but does not own common prefabs, portal objects, spawn positions, offsets, magic stone bonuses, field-heal bonuses, or chest-count deltas.
- `BossSpecialRewardPresetSO` owns boss-specific special loot candidates only.
- Authored inactive `TreasureChest` and `ScenePortal` objects own chest and portal positions through their transforms.
- Scene-authored `BossBattleEndHandler` owns boss matching, reward handling, portal handling, authored references, and handled marking.
- `BossRewardSpawnService` owns authored chest initialization/activation and variable-count physical magic stone/field-heal drops from the boss death position.
- `BossRewardSpawner`, `BossExitPortalActivator`, `BossBattleEndAnchors`, and `BossBattleEndPrefabCatalogSO` were deleted with their serialized scene/prefab references removed.
- No runtime path reads `BossDrop` fields. Missing reward/portal authoring is surfaced through validators and editor/development fallback warnings instead of being hidden by the old adapter.

## Remaining Risks

- A boss that previously relied only on deleted `BossDrop` or split-component field values can lose reward/portal data until its scene `BossBattleEndHandler`, special reward preset, authored chest reference, and authored portal reference are authored.
- Duplicate reward or portal handling is still possible if multiple scene components are wired to handle the same boss incorrectly.
- Boss-specific bonus loot may be confused with StageLootTable boss defaults if preset/modifier ownership is not kept clear.
- Timer/progress behavior could diverge if boss death paths bypass `RunProgressCoordinator`.
- Boss exit portal objects placed under moving boss roots can still move with the boss while inactive. Place the authored portal at the final scene position or under a stable arena parent.

## Reopen Trigger

- A new boss reward modifier, boss-specific reward rule, or boss base reward field is added.
- Boss portal activation behavior changes.
- The boss battle-end validator or Unity import reports missing components, stale deleted component/catalog GUIDs, stale definition/profile data, or preset gaps that require another structural change rather than normal Inspector authoring.

## Related Documents

- `Docs/DecisionLog.md` - `Boss Rewards Use Additive Modifier Aggregates`
- `Docs/SessionLogs/2026-05-14.md` - BossDrop split implementation notes
- `Assets/LeeJunMo/Script/Looting/BossDrop.cs` - deleted legacy adapter
- `Assets/LeeJunMo/Script/SceneManagement/BossBattleEndHandler.cs`
- `Assets/LeeJunMo/Script/Looting/BossRewardSpawnService.cs`
- `Assets/LeeJunMo/Script/Looting/BossSpecialRewardPresetSO.cs`
- `Assets/LeeJunMo/Script/SceneManagement/RunProgressCoordinator.cs`

## Resolution Notes

- `RunProgressCoordinator` keeps route-key, final-route, identity-key, and reward-context construction policy in `BossRunProgressPolicy.cs`, while still owning event dispatch and timer calls.
- `BossRewardFallbackService` now only reports unhandled reward/portal authoring through editor/development warnings after reward-ready event dispatch. It does not dynamically spawn rewards or portals from a route definition.
- `BossBattleEndHandler` owns event subscription, explicit boss matching, authored chest/portal resolution, and reward/portal handled marking, while `BossRewardSpawnService.cs` owns boss chest loot generation, authored chest activation, bonus loot, physical magic stone/field-heal drops, and exception-logged reward execution.
- `BossBattleEndMigrationValidatorWindow` reports stale deleted battle-end/profile data, stale deleted component/catalog GUIDs, hub portals without route catalogs, missing scene handler coverage, missing authored chest/portal references, optional RouteSet special reward preset state, and boss exit portal semantic mistakes. Scene Auto Fix can create first-pass handler/boss wiring, but final chest/portal placement and references remain Inspector authoring steps.
- Manual Unity Editor verification is still required: run the validator, review presets/authored chest/authored portal references in the Inspector, then confirm boss rewards and portals fire once after boss death.
