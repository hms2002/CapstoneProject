---
status: active
authority: project-log
category: decision-log
last_reviewed: 2026-05-11
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

## 2026-05-06 - Update CurrentTask on Active Work Changes

Decision:
Update `Docs/CurrentTask.md` whenever the active implementation task changes.

Reason:
The file was being read but not updated, so it no longer represented the actual task in progress.

Implications:
- `CurrentTask.md` should hold the current goal, scope, and done criteria.
- Detailed implementation notes belong in `Docs/SessionLogs/`.
- Durable design choices still belong in `Docs/DecisionLog.md`.

## 2026-05-06 - Shop v2 Uses Definition-Driven Runtime Policy

Decision:
Use `ShopDefinitionSO` as the source of truth for merchant shop settings, and combine it with `RunModifierService.ShopModifiers` through a shop policy layer.

Reason:
Upgrade effects now modify shop availability, slot count, discounts, and refresh count. Keeping those policies inside `MerchantNPC` would make scene presentation, stock state, and upgrade logic too tightly coupled.

Implications:
- Merchant stock remains run/session scoped in `GamePlayData.merchantStates`.
- Existing stock is preserved when discounts change.
- Existing slots are preserved when slot count expands; only newly opened slots are rolled.
- Scene and prefab references must be manually wired to a `ShopDefinitionSO`.

## 2026-05-11 - Slime Queen Phase 2 Uses Separate Boss Prefabs

Decision:
Slime Queen phase 2 should be represented by two independent boss prefabs, `SlimeQueenP2Short` and `SlimeQueenP2Long`, spawned after the phase 1 `SlimeQueen` dies.

Reason:
The corridor slime gimmick is based on slime splitting after death. Splitting at 50% HP while the original queen remains alive contradicts that rule and makes shared-health UI misleading.

Implications:
- `SlimeQueen` no longer owns a 50% HP shared-health twin transition.
- Phase 2 queens should have their own HP and should die as normal enemies/bosses.
- The final HUD should show two independent full health bars after phase 2 starts, not a single shared ratio split visually.
- A later coordinator or HUD extension is still needed to resolve encounter completion when both phase 2 queens die.

## 2026-05-11 - Slime Queen Phase 2 HUD Splits Existing Bar Width

Decision:
Display `SlimeQueenP2Short` and `SlimeQueenP2Long` as independent health ratios inside the current boss health bar area, split into left and right halves with a small center gap.

Reason:
Two separate phase 2 bosses need separate remaining HP readability. Using one shared health bar hides which queen took damage, while two full-width bars would consume more HUD space than the current boss presentation allows.

Implications:
- The left phase 2 health bar represents `SlimeQueenP2Short`.
- The right phase 2 health bar represents `SlimeQueenP2Long`.
- The single-boss HUD path remains unchanged for phase 1 and other bosses.
- The current implementation supports authored dual slider references but can temporarily clone the existing sliders at runtime until the HUD prefab is explicitly wired.
