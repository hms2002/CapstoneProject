---
status: completed
authority: current-task
category: documentation
last_reviewed: 2026-05-16
---

# Current Task

## Goal

Synchronize the P2 BossDrop Responsibility Split backlog with the current source state, where boss reward spawn execution already lives in a dedicated helper file and the remaining debt is prefab-reference migration.

## References

- `AGENTS.md`
- `Docs/README.md`
- `Docs/ErrorLog.md`
- `Docs/DecisionLog.md`
- `Docs/RefactorBacklog/BossDropResponsibilitySplit.md`
- `Docs/StructureMemory/ScriptSystems/LootRewardStructure.md`
- `Docs/StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md`

## In Scope

- Verify current `BossDrop`, `BossRewardSpawner`, `BossRewardSpawnService`, `BossExitPortalActivator`, and `RunProgressCoordinator` source state.
- Update `BossDropResponsibilitySplit.md` so it no longer claims the reward spawn helper is same-file.
- Update the Refactor Backlog priority board wording if needed.
- Record that no runtime behavior, scenes, prefabs, serialized fields, MonoBehaviours, asmdefs, or ScriptableObject schemas changed.

## Out of Scope

- Removing `BossDrop`.
- Rewiring boss scenes or prefabs.
- Changing boss reward, portal, timer, route-progress, additive modifier, or legacy fallback behavior.
- Running Unity batchmode while Unity Editor is open.

## Done Criteria

- Backlog reflects `BossRewardSpawnService.cs` as the current reward spawn execution owner.
- Remaining BossDrop debt is narrowed to prefab/scene reference migration and legacy adapter removal.
- Documentation-only verification is recorded.

## Outcome

- Confirmed `BossRewardSpawnService.cs` is the current dedicated reward spawn execution helper file.
- Updated `BossDropResponsibilitySplit.md` and the Refactor Backlog priority board so the remaining debt is prefab/scene reference migration and legacy fallback reliance.
- No runtime code, scenes, prefabs, serialized fields, MonoBehaviours, asmdefs, or ScriptableObject schemas changed.
- Documentation-only change; no MSBuild, Unity compile, or Unity batchmode was run.
