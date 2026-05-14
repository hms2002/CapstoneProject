---
status: active
authority: current-task
category: workflow
last_reviewed: 2026-05-14
---

# Current Task

## Goal

Implement the Docs Memory System for feature-level structure memory and refactor backlog documents.

## References

- `AGENTS.md`
- `Docs/README.md`
- `Docs/Overview/document-inventory.md`
- `Docs/DecisionLog.md`
- `Docs/ErrorLog.md`

## In Scope

- Add `Docs/StructureMemory/` for feature-level, fast context documents.
- Add `Docs/RefactorBacklog/` for feature-level structural debt and refactor candidates.
- Update `AGENTS.md` so future agents know when and how to update these documents.
- Update `Docs/README.md` and `Docs/Overview/document-inventory.md` so the new document types are discoverable.
- Add initial documents for UI flow input blocking and BossDrop responsibility split.
- Record the implementation outcome in `Docs/SessionLogs/2026-05-14.md`.

## Out of Scope

- Rewriting `Docs/Architecture/` or `Docs/Contracts/`.
- Changing runtime code, scenes, prefabs, serialized fields, or ScriptableObject schemas.
- Running Unity batchmode.

## Done Criteria

- `Docs/StructureMemory/README.md` and `Docs/RefactorBacklog/README.md` exist and define document roles.
- `Docs/StructureMemory/UIFlowInputBlocking.md` captures the current UI flow blocker structure.
- `Docs/RefactorBacklog/BossDropResponsibilitySplit.md` captures the current BossDrop legacy/refactor state.
- `AGENTS.md` includes routing rules for `StructureMemory` and `RefactorBacklog`.
- Documentation links and folder paths are verified.
