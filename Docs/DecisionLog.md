---
status: active
authority: project-log
category: decision-log
last_reviewed: 2026-05-15
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

## 2026-05-10 - Keep Lightning Spear Basic Attack Weapon-Specific

Decision:
Do not keep the temporary `WeaponComboAttack2D` shared layer. Lightning Spear basic attack owns its combo data and execution logic, while `SwordCombo2D` remains a legacy/sample ability.

Reason:
The shared layer only had one active consumer and made it unclear whether Sword combo behavior had become project-wide weapon policy.

Implications:
- Lightning Spear attack tuning stays in `LightningSpearAttackData`.
- New weapon combo logic should not depend on the removed shared runner by default.
- `SwordCombo2D` assets and logic remain untouched unless a separate task targets them.

## 2026-05-14 - Boss Rewards Use Additive Modifier Aggregates

Decision:
Keep `StageLootTable` and `LootManager` as the base boss reward source, and let Affection, upgrades, and future systems contribute only additive boss reward modifier aggregates.

Reason:
Affection should change extra boss rewards without bypassing stage progression reward rules. Keeping base rewards and modifiers separate prevents NPC Affection assets from becoming an alternate reward table system.

Implications:
- Boss chest base loot still comes from `LootManager.GenerateChestLoot(...)`.
- Boss magic stone base count still comes from `LootManager.GetBossMagicStoneCount()`.
- Boss-specific extra items, extra magic stones, extra field heals, and boss chest count deltas are additive modifiers.
- `BossDrop` remains only as a prefab-safe legacy adapter until boss scene/prefab references are migrated to dedicated reward and portal components.

## 2026-05-14 - Treat Markdown as Structure Memory

Decision:
Use project Markdown not only as task history, but as structure memory that helps future agents quickly understand previously changed systems before editing them again.

Reason:
Session logs that only say what was done are not enough when a later task starts in a different part of the codebase. Future work is faster and uses fewer tokens when the log also records ownership, lifecycle, key files, verification, and the next document or source file to read.

Implications:
- Material architecture, ownership, runtime state, lifecycle, shared service, interface, asmdef, MonoBehaviour, ScriptableObject, and prefab-facing changes should leave a concise task entry in `Docs/SessionLogs/YYYY-MM-DD.md`, and a feature-level `Docs/StructureMemory/` entry when future context reconstruction needs a stable system map.
- Durable decisions still belong in `Docs/DecisionLog.md`.
- `Docs/Architecture/` and `Docs/Contracts/` remain source-of-truth documents and should only be rewritten with explicit approval.
- Small implementation edits do not need new memory documents unless they change how future work should understand the system.

## 2026-05-14 - GameFlowInputBlocker Owns Flow Input Blocks

Decision:
Use `GameFlowInputBlocker` as the reusable lifecycle wrapper for temporary game-flow input blocks, while `UIManager` remains the central policy owner.

Reason:
Chest first-open sequences, dialogue playback, upgrade open fades, reward open presentations, and future authored flows all need the same temporary block behavior without each system directly manipulating `UIManager` internals.

Implications:
- New flow code should acquire/release `GameFlowInputBlocker` instead of calling `UIManager.SetExternalUiInputBlocked(...)` directly.
- `UIManager.TryPushUIForExternalBlockOwner(...)` is the owner exception path for a flow that must open its own stack UI while the block is active.
- The blocker is for stack-outside flow gaps and presentation windows; opened stack UI screens still express time freeze/control locks through `IStackableUI.GameplayLockProfile`.
- `GameFlowInputBlocker` must release from normal completion and from `OnDisable`/`OnDestroy` cleanup paths so interrupted flows do not leave controls locked.

## 2026-05-14 - Use Feature-Level StructureMemory and RefactorBacklog

Decision:
Add `Docs/StructureMemory/` for feature-level structure maps and `Docs/RefactorBacklog/` for feature-level structural debt tracking.

Reason:
Date-based session logs are useful for recent work, but they scatter system context over time. Future agents need a faster way to understand current structure and known refactor candidates before editing related systems.

Implications:
- `StructureMemory` is a fast context map, not a source-of-truth replacement for `Architecture` or `Contracts`.
- `RefactorBacklog` tracks intentional structural debt with target shape, risks, and refactor triggers; it is not a generic TODO list.
- Small edits usually update only `SessionLogs`; reusable structure, known debt, durable decisions, and recurring mistakes update the narrower matching memory document.
- Stable structure can be proposed for promotion from `StructureMemory` to `Architecture` or `Contracts`, but those official documents still require explicit approval before editing.

## 2026-05-15 - Prototype Runtime UI Creation Is Temporary

Decision:
Runtime creation of UI or presentation objects is acceptable for first-pass prototyping, debug fallback, or emergency fallback, but production-facing UI and presentation should be promoted to scene- or prefab-authored objects before build-facing use.

Reason:
Runtime-created UI is useful for quickly checking feel and avoiding blocked iteration. Keeping it as the final structure hides sorting, raycast, scaling, animation, and serialized-reference choices in code, making Unity authoring, inspection, and prefab/scene verification harder.

Implications:
- Gameplay-facing UI should normally live under scene/prefab-authored objects, often through `GlobalUIRoot` canvas layers or explicit serialized references.
- Runtime-created `Canvas`, `EventSystem`, button, TMP, image, overlay, and hierarchy fallbacks must be clearly treated as prototype/debug/fallback paths, not silent production defaults.
- Any runtime-created UI that remains beyond prototype should have explicit owner, cleanup, and a prefab/scene migration follow-up.
- Pooled gameplay presentation objects may still be instantiated from authored prefabs; the prefab is the authored unit, while the runtime service owns spawning and cleanup.
- Existing runtime fallback presentation debt is tracked through `Docs/RefactorBacklog/RuntimePresentationFallbackAuthoringSplit.md`.

## 2026-05-15 - Use ContentAuthoring Guides for Repeatable Content Pipelines

Decision:
Manage repeatable combat-content production through a `Docs/Guides/ContentAuthoring/` hub and focused type-level pipeline guides.

Reason:
Weapons, mobs, bosses, relics, consumables, and reward links will keep growing. A single structure map is useful for code review, but content creators need a production-order guide that starts from authored data and ends at loot, inventory, presentation, save, and verification.

Implications:
- `ContentAuthoring` guides are production entry points, not replacements for `Contracts` or `Architecture`.
- Stable rules still belong in `Docs/Contracts/` or `Docs/Architecture/` after explicit approval.
- Structure-specific context remains in `Docs/StructureMemory/`, and concrete structural debt remains in `Docs/RefactorBacklog/`.
- New content pipelines should link to source-of-truth documents instead of copying their full rules.

## 2026-05-15 - Treat TitleScene as App And Gameplay Session Boundary

Decision:
Use `Docs/Architecture/SceneDomainBootstrapArchitecture.md` as the source-of-truth document for title-to-game scene bootstrap ownership. `TitleScene` is both the app entry scene and the gameplay session boundary.

Reason:
Title, profile selection, return-to-title, gameplay runtime presentation, camera rig behavior, and run-session cleanup were implemented across several services without a single documented boundary.

Implications:
- App-scope services may exist on title, but gameplay-scope runtime presentation should not treat title as active gameplay.
- Title-local UI/camera presentation stays scene-authored unless an explicit fallback/prototype path is documented.
- Return-to-title changes must preserve run/session cleanup before or during the title scene transition.
- Code refactors in this area should start from `Docs/RefactorBacklog/SceneDomainBootstrapBoundarySplit.md`.

## 2026-05-15 - Use Source-Structure Verification For Unity Script File Splits

Decision:
For source-only Unity script file splits, proceed with helper `.cs` file extraction when behavior and APIs are unchanged, even if Unity-generated `.csproj` files have not refreshed yet.

Reason:
Unity Editor import/compile is the reliable final check for new `Assets/` scripts, while generated `.csproj` files can lag behind the filesystem and block otherwise safe source-only refactors.

Implications:
- Do not manually edit generated `.csproj` files.
- Verify moved helpers by source structure: duplicate definitions, original helper removal, call-site references, namespace/global-scope compatibility, and whitespace.
- Run MSBuild only when generated project files include the relevant source files.
- Do not claim Unity compile or MSBuild success for new files unless that verification actually ran.

## 2026-05-15 - Element Build-Up Applies From ElementOffenseSource

Decision:
Applied elemental build-up is resolved from the attacker's `ElementOffenseSource` through `ElementBuildUpResolver`, not from per-hit `DamagePayloadConfig.elementFormulas` or `elementBuildUps` payloads.

Reason:
The combat application path already used attacker-wide elemental build-up, while legacy per-hit fields made weapon tuning intent ambiguous.

Implications:
- `DamagePayloadConfig.elementFormulas`, `CombatDamageSnapshot.ElementBuildUps`, and `elementBuildUps` / `elementInputs` API parameters are compatibility debt until serialized data and call sites are migrated.
- Future per-hit elemental tuning must be introduced as a named merge/override policy instead of reviving the old implicit payload path.
- Elemental weapon tuning should verify attacker `ElementOffenseSource`, stat provider wiring, and `ElementBuildUpFormulaProfile` rather than per-hit payload formulas.
