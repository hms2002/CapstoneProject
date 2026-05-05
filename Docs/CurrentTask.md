---
status: active
authority: current-task
category: workflow
last_reviewed: 2026-05-05
---

# Current Task

## Goal

Build the project memory system for Codex + Obsidian before continuing feature implementation.

## References

- `AGENTS.md`
- `Docs/README.md`
- `Docs/Overview/document-inventory.md`
- OpenAI Codex AGENTS.md guide: https://developers.openai.com/codex/guides/agents-md
- OpenAI Codex config reference: https://developers.openai.com/codex/config-reference

## In Scope

- Use `Docs/` as the Obsidian vault and official Markdown memory store.
- Organize existing Markdown documents by authority and purpose.
- Add Codex project instructions through root `AGENTS.md`.
- Add `DecisionLog`, `ErrorLog`, and `SessionLogs` for durable project context.
- Ignore Obsidian local settings.

## Out of Scope

- Unity gameplay, scene, prefab, or ScriptableObject changes.
- Obsidian MCP write integration.
- `.codex/config.toml` setup.
- Codex Skills or scheduled automations.

## Done Criteria

- `Docs/` has role-based folders.
- `Docs/README.md` routes to the new folder structure.
- `AGENTS.md` defines reading order, authority order, and conservative documentation rules.
- `Docs/.obsidian/` is ignored by Git.
- Markdown links are checked after the move.

## Next Feature Candidate

Game over presentation implementation should be planned after this memory system is stable.
