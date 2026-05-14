---
status: active
authority: refactor-backlog
category: refactor-index
last_reviewed: 2026-05-14
---

# Refactor Backlog

`RefactorBacklog` documents track intentional structural debt and future refactor candidates. They are not generic TODO notes.

Use this folder when the code works, but the current structure is known to be temporary, overloaded, legacy-compatible, or blocked by prefab/scene migration.

## When To Create Or Update

- A component keeps too many responsibilities because of a quick implementation.
- A legacy adapter or fallback path remains for prefab or scene compatibility.
- A cleaner target structure is known but out of current scope.
- Duplicate paths or temporary bridges exist and could create future bugs.
- A migration needs manual Unity scene/prefab work before code can be simplified.

## Required Sections

Each backlog document should include:

- Current Problem
- Why It Exists
- Target Shape
- Risks
- Refactor Trigger
- Related Documents
- Status

Allowed status values:

- `proposed`
- `active`
- `partially-refactored`
- `resolved`

## Boundaries

- Do not record vague ideas without a concrete risk or trigger.
- Do not duplicate `ErrorLog` entries unless the same issue also represents structural debt.
- Resolve or update entries when the debt is removed, not only when new work is added.

## Current Documents

- [BossDrop Responsibility Split](./BossDropResponsibilitySplit.md)
