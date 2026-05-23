---
status: proposed
authority: refactor-backlog
category: ui-encyclopedia
last_reviewed: 2026-05-22
---

# Encyclopedia Deferred Categories

## Current Problem

The encyclopedia originally targeted Item, Monster, and Boss pages at once, but the item-side layout is the only authored area currently being worked on. Weapon, Relic, and Consumable item sub-tabs now have a code path, while Monster and Boss UI are still deferred. Finishing Monster and Boss UI in the same slice would slow down the item page and leave too many partially wired detail layouts.

## Why It Exists

The current encyclopedia flow has an ItemTab path for `Weapon`, `Relic`, and `Consumable`, while earlier Monster/Boss catalog scaffolding still exists for later work. The layout work has moved toward a tabbed structure:

- Top-level tabs: `Item`, `Monster`, `Boss`
- Item sub-tabs: `Weapon`, `Relic`, `Consumable`
- Monster sub-tabs: boss-theme groups
- Boss sub-tabs: individual bosses

Only the Item tab has a concrete layout and detail presenter right now.

## Target Shape

- Keep the current Item tab playable first across `Weapon`, `Relic`, and `Consumable`.
- Add Monster theme filtering under the Monster tab.
- Add Boss-specific tabs and a dedicated boss detail presenter rather than using `EncyclopediaDetailPanel` for affection/reward UI.
- Keep page index zero-based internally and display pages as one-based text.

## Risks

- Enabling Monster/Boss before their detail presenters exist can expose unfinished UI.
- Mixing Monster and Boss work with remaining Item detail polishing can expand the serialized contract too quickly.
- Broad scene/prefab YAML edits are risky while the user is actively authoring the layout in Unity.

## Refactor Trigger

- User resumes Monster page layout work.
- User resumes Boss page layout work.
- The Item tab is accepted and ready to generalize the same presentation flow into Monster/Boss.

## Related Documents

- `Docs/CurrentTask.md`
- `Docs/DecisionLog.md` - `Encyclopedia Current Slice Is Item First`
- `Docs/DecisionLog.md` - `Encyclopedia Item Detail Uses Dedicated Presenter`
- `Docs/StructureMemory/ScriptSystems/EncyclopediaStructure.md`
- `Docs/SessionLogs/2026-05-22.md`

## Status

`proposed`
