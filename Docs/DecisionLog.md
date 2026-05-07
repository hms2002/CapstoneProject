---
status: active
authority: project-log
category: decision-log
last_reviewed: 2026-05-08
---

# Decision Log

## 2026-05-05 - Use Docs as Project Memory Vault

Decision:
Use `Docs/` as both the Obsidian vault and the official project Markdown memory store.

Reason:
Codex and the developer should read the same source-of-truth documents. Keeping a separate vault would create drift between human notes and agent context.

Implications:
- `Docs/.obsidian/` remains local and ignored.
- Markdown documents in `Docs/` are project assets.
- Obsidian is the editing/navigation UI, not a separate authority layer.

## 2026-05-05 - Use AGENTS.md for Codex Project Instructions

Decision:
Place project-specific Codex instructions in the repository root `AGENTS.md`.

Reason:
Codex uses `AGENTS.md` as durable project guidance. This keeps execution rules close to the code and separate from runtime configuration.

Implications:
- `.codex/config.toml` is reserved for future MCP, sandbox, profile, and approval settings.
- Project behavior rules live in `AGENTS.md`.

## 2026-05-05 - Treat Reviews, Notes, and Handoffs as Reference-Only

Decision:
`Docs/Reviews/`, `Docs/Notes/`, and `Docs/Handoffs/` are reference-only unless promoted into `Contracts` or `Architecture`.

Reason:
Older reviews and handoff notes are valuable context, but they can conflict with current architecture and contracts.

Implications:
- Current implementation decisions should prefer `Contracts` and `Architecture`.
- Review documents can explain why a decision exists, but should not override active rules.

## 2026-05-08 - Slime Queen Phase 2 Twin Shares Boss HP and HUD Ownership

Decision:
Slime Queen phase 2 uses a spawned twin for independent phase 2 pattern execution, but the original boss remains the owner of HP, HUD binding, rewards, and death cleanup.

Reason:
The two phase 2 bodies should behave independently in combat, but the encounter is still one boss fight. Keeping one authoritative HP owner prevents duplicated rewards, duplicated death handling, and HUD ownership drift.

Implications:
- Damage received by the twin is redirected to the original boss HP.
- The original Slime Queen exposes split health presentation data through `IBossSplitHealthPresentation`.
- The current split health bar divider is a fallback until the authored UI is added to `GlobalUIRoot` and wired through serialized references.
