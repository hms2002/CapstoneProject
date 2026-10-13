---
status: resolved
authority: refactor-backlog
category: ui-encyclopedia
last_reviewed: 2026-10-11
---

# Encyclopedia Deferred Categories

## Current Problem

Resolved by the approved 2026-10-11 Monster/Boss/NPC extension. Dedicated non-item tab/detail presenters and a separate prefab-authoring path now exist. Final Unity authoring, 8/8 save EditMode tests, 19/19 creature PlayMode tests and native UI captures passed; full hub manual gameplay and Player build remain unexecuted validation limits.

## Why It Exists

The earlier item-first slice intentionally deferred Monster/Boss layout to protect the Item presenters. The accepted extension keeps that boundary and adds NPC to the non-item scope:

- Top-level tabs: `Item`, `Monster`, `Boss`, `NPC`
- Item sub-tabs: `Weapon`, `Relic`, `Consumable`
- Monster pages: registered species, Idle preview, description and profile cumulative kills
- Boss/NPC pages: 2x2 character selection, actual portrait/expression controls, affection, registered rewards and story

Theme filtering and individual-boss sub-tabs from the early proposal are not requirements of the approved extension.

## Target Shape

- Preserve Item behavior and authored layout across `Weapon`, `Relic`, and `Consumable`.
- Route Monster/Boss/NPC to `EncyclopediaCreatureTab` and `EncyclopediaCreatureDetailView`, without reactivating the legacy `EncyclopediaDetailPanel`.
- Read existing portrait, affection, reward and story data; missing content remains unregistered.
- Keep page index zero-based internally and display pages as one-based text.

## Risks

- Automated authored-reference, rendering/input and Item-preservation checks passed. Full hub manual gameplay and Player build remain outside the executed validation.
- Species IDs are persistent profile keys; future renames need an explicit migration.
- New reward/story content and further filtering require separate scope.

## Refactor Trigger

The user approved the iterated Monster/Boss/NPC layout for in-game implementation on 2026-10-11, explicitly excluding Item work.

## Related Documents

- [Active scope](../ActiveTasks/encyclopedia-creature-tabs.md)
- `Docs/DecisionLog.md` - `Encyclopedia Current Slice Is Item First`
- `Docs/DecisionLog.md` - `Encyclopedia Item Detail Uses Dedicated Presenter`
- `Docs/StructureMemory/ScriptSystems/EncyclopediaStructure.md`
- `Docs/SessionLogs/2026-05-22.md`
- [Creature structure](../StructureMemory/EncyclopediaCreatureTabs.md)
- [2026-10-11 session](../SessionLogs/2026-10-11.md)

## Status

`resolved` — dedicated creature extension implemented and automatically verified; remaining manual/build limits are recorded in the completed task.
