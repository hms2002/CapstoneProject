---
status: active
authority: project-log
category: decision-log
last_reviewed: 2026-05-17
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
- Boss chest base loot comes from `LootManager.GenerateBossChestLoot(...)`, which reads boss-specific count and rarity defaults from `StageLootTable`.
- Boss magic stone base count still comes from `LootManager.GetBossMagicStoneCount()`.
- Boss field heal base count comes from `StageLootTable`, while extra field heals remain additive modifiers.
- Boss-specific extra items, extra magic stones, extra field heals, and boss chest count deltas are additive modifiers.
- The former `BossDrop` adapter was temporary and has since been deleted; dedicated reward and portal components are the supported path.

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

## 2026-05-16 - RouteSets Reference Boss Special Reward Presets

Decision:
Let `CorridorBossRouteSetSO` reference only an optional `BossSpecialRewardPresetSO` for boss-specific special loot candidates. Authored chest/portal objects live in boss scenes, and reward amount changes remain runtime modifier overlays.

Reason:
Route sets already compose the corridor/boss scene, BGM, and loading context for a run stage, so linking a boss-specific special loot preset there is acceptable. Putting common chest, magic stone, portal prefabs, portal offsets, placement policy, magic stone bonuses, field-heal bonuses, or chest-count deltas on route data mixes content composition with shared prefab catalogs and runtime reward effects.

Implications:
- `CorridorBossRouteSetSO` may reference `BossSpecialRewardPresetSO`, but should not own common prefabs, portal objects, spawn positions, offsets, magic stone bonuses, field-heal bonuses, or chest-count deltas.
- `BossSpecialRewardPresetSO` owns only boss-specific special loot candidates.
- Upgrade, Affection, and future runtime effects should keep contributing `BossRewardModifierAggregate` overlays instead of mutating preset SO assets.
- Scene/prefab-owned `TreasureChest` and `ScenePortal` objects provide chest and portal positions through their own transforms.
- `BossBattleEndDefinitionSO`, `BossRewardProfileSO`, `BossBattleEndPrefabCatalogSO`, and `BossBattleEndAnchors` are deleted; route special reward preset and scene `BossBattleEndHandler` are the supported authoring path.

## 2026-05-16 - Remove BossDrop Legacy Adapter

Decision:
Delete the `BossDrop` legacy adapter instead of keeping it as a prefab-safe fallback.

Reason:
Boss battle-end ownership is now split across route-linked special reward presets, scene `BossBattleEndHandler`, and run-progress coordination. Keeping `BossDrop` would preserve a second reward/portal authoring path and hide missing RouteSet or handler wiring.

Implications:
- Serialized scene/prefab references to the old `BossDrop` script GUID must be removed during the migration.
- `BossRewardFallbackService` must not read `BossDrop` fields.
- Unity Editor verification must run the boss battle-end validator and play-check boss death reward/portal behavior because deleted `BossDrop` field values are no longer available as fallback data.

## 2026-05-16 - Remove BossRewardModifierSO Authoring Layer

Decision:
Do not keep a separate `BossRewardModifierSO` asset layer. Boss-specific special loot candidates can live in a RouteSet-linked `BossSpecialRewardPresetSO`, while Affection, upgrades, and future runtime effects expose their own fields and project them into `BossRewardModifierAggregate` at runtime.

Reason:
The modifier SO duplicated boss reward authoring responsibility and made runtime-changing effects look like mutable asset state. Runtime effects need additive overlays without mutating ScriptableObject reward presets.

Implications:
- `BossRewardModifierAggregate` remains the runtime value type for combining boss reward deltas.
- Affection and upgrade effects should author their own relevant values directly, then emit aggregates during run modifier rebuild.
- Existing assets that referenced `BossRewardModifierSO` need migration to direct effect fields or RouteSet special reward preset data.
- New boss-specific base special loot candidates should be configured through route-linked special reward presets, not modifier assets.

## 2026-05-16 - StageLootTable Owns Boss Base Reward Defaults

Decision:
Keep boss base reward defaults in `StageLootTable`: boss weapon count profile, boss relic count profile, boss Common/Rare/Epic relic rarity weights, boss magic stone count, and boss field heal base count.

Reason:
These values are stage progression defaults, not boss prefab wiring and not RouteSet composition. RouteSets should only identify the optional boss-specific special loot preset for the selected boss route, while runtime systems such as Affection and Upgrade should add overlays through `BossRewardModifierAggregate` without mutating ScriptableObjects.

Implications:
- Normal chest rewards use normal chest profiles; boss chests use the boss-specific generation path.
- RouteSet `BossSpecialRewardPresetSO` entries are only additional boss-specific special loot candidates.
- `BossRewardModifierAggregate` remains the only runtime overlay path for boss magic stone bonuses, field heal bonuses, boss chest count deltas, and runtime bonus loot.
- Boss reward base values should be reviewed in the StageLootTable Inspector, not on boss prefabs or RouteSet assets.

## 2026-05-16 - Keep Scene-Facing Compatibility Facades For P2 Closure

Decision:
Keep `UpgradeManager`, `GamePlayDataManager`, and `ScenePortalTravelService.TryTravel(...)` as the public/scene-facing compatibility surfaces while moving more ownership into helpers.

Reason:
These names and entry points are already referenced by scenes, prefabs, runtime bootstrap, portal interaction, upgrade UI flows, and save/run lifecycle code. Renaming them would create serialized reference and static call-site risk without improving the current player-facing behavior.

Implications:
- `UpgradeManager` remains the public upgrade facade, but data-change/save/UI-close notification ownership goes through `UpgradeNotificationService`.
- `GamePlayDataManager` remains the scene-facing run/session holder, but volatile timer, pending transition, pending player state, pending reward, affection, and shortcut mutations go through `RunSessionStateService`.
- `ScenePortalTravelService.TryTravel(...)` remains the static portal entry point, but route resolution, transition lock, manager resolution, and execution handoff live behind `ScenePortalTravelCoordinator` and existing planner/executor helpers.
- Future refactors should not reopen MonoBehaviour or static entry naming unless a scene/prefab migration pass is explicitly planned.

## 2026-05-16 - Treat Representative GlobalUIRoot As Build-Facing Presentation Root

Decision:
Use `Assets/LeeJunMo/Prefab/UI/GlobalUIRoot.prefab` as the representative build-facing runtime presentation root for loading, cursor, status HUD, and Boss HUD authored-reference validation. Keep the display letterbox runtime-generated by `GamePresentationController`.

Reason:
Runtime presentation fallbacks are useful as emergency/debug paths, but build-facing UI structure needs a single prefab target that can be inspected, auto-fixed, validated, and reused by scene setup.

Implications:
- Other GlobalUIRoot variants are warning/follow-up targets unless they are promoted to build-facing roots.
- `MouseCursorService` must prefer authored references before creating fallback presentation hierarchy.
- `GamePresentationController` owns display letterbox runtime hierarchy because the overlay is resolution/window-mode policy rather than prefab layout.
- `Tools/Validation/Scene Setup Validator` should report missing representative prefab references and offers an auto-fix path for cursor authoring.
- Unity Editor validation/play checks remain required because code validation cannot prove canvas order, input behavior, or visual layout.

## 2026-05-16 - Keep Shared ScenePortal Prefab Semantic-Neutral

Decision:
Keep the shared `ScenePortal.prefab` at `TransitionType.None` with no `RunRouteCatalogSO` reference. Hub start portals must carry `HubToRunStart` and the run route catalog on their scene instance, while boss exit portals rely on the active `PortalRouteManager` plan to resolve `BossToCorridor` or `ReturnToHubAfterRun`.

Reason:
The same portal prefab can be reused by hub, corridor, and authored boss exit portal instances. Letting the prefab carry `HubToRunStart` made boss exit portals behave like run-start portals and bypassed the intended active route semantics.

Implications:
- Boss battle-end no longer reads `BossBattleEndPrefabCatalogSO.portalPrefab`; authored boss exit portals should use semantic-neutral `ScenePortal` instances.
- Hub start portals are the only supported owners of `RunRouteCatalogSO` references.
- Boss exit portal placement is authored by the portal object's own transform.
- Runtime boss portal creation is not supported; missing `portalObj` is an authoring error.

## 2026-05-17 - Boss BattleEnd Chest And Portal Are Authored Activation Objects

Decision:
Boss battle-end `TreasureChest` and `ScenePortal` objects are authored in the scene and toggled inactive/active by a scene-authored `BossBattleEndHandler`. Runtime chest/portal prefab creation, anchor-driven portal movement, boss-position fallback, and boss-prefab-owned battle-end placement are not supported.

Reason:
The required behavior is simple: after boss death, a chest and a portal appear at predetermined positions and the portal routes through the active `RunRouteCatalogSO` plan. Authoring the objects directly makes their transforms the source of truth and avoids code-side spawn offsets, captured positions, or boss-child transform ambiguity. The handler belongs with the scene's battle-end placement setup, not with the reusable boss actor prefab.

Implications:
- `BossBattleEndHandler` references the `BossControllerBase`, authored `TreasureChest`, and authored exit portal.
- `BossBattleEndHandler` initializes the authored chest with generated boss loot and activates it.
- Boss magic stones and field heals remain physical runtime drops. Their counts come from `StageLootTable` plus `BossRewardModifierAggregate`, and their drop origin is the boss death position rather than the authored chest/portal position or a separate anchor.
- `BossBattleEndHandler` activates the authored exit portal without moving it.
- `BossRewardSpawner`, `BossExitPortalActivator`, `BossBattleEndAnchors`, and `BossBattleEndPrefabCatalogSO` are deleted; new authoring should not add replacement spawn/anchor components for this flow.
- Boss exit portals should not carry `RunRouteCatalogSO`; hub start portals remain the route catalog owner.

## 2026-05-17 - Keep Markdown As Source And Use Presentation HTML As Human Dashboard

Decision:
Keep Markdown documents as the project documentation source of truth, and use `Docs/Presentation/` HTML/CSS/JS only as human-readable navigation dashboards.

Reason:
Markdown is cheaper for Codex context, easier to diff, and better for source edits. HTML is better for human scanning but adds presentation markup and should not duplicate full source document content.

Implications:
- Codex should continue reading Markdown documents first.
- Presentation HTML should stay thin and source-linked rather than copying full Markdown bodies.
- Routine dashboard updates should usually touch `_shared/docs-data.js`; HTML shells, shared CSS, and render JS should change rarely.
- If duplicated content drifts, update the Markdown source first and regenerate or adjust the presentation view from that source.
- Presentation HTML may be updated only when requested or approved by the authorized HTML maintainer `nadoman354`.
- When Markdown changes may make Presentation HTML stale, Codex should report the affected HTML page and required summary/diagram update instead of editing it automatically.
- `refactor-board.html` may exist as an approved thin overview page, but it must remain a risk/trigger board that links back to `RefactorBacklog`.
- `session-summary.html` is not kept as a Presentation entry by default because session logs remain Markdown-first history rather than a regular human decision surface.
- `authoring-guide.html` may be more detailed than the other Presentation pages when it works as a production handbook, but it should still link back to Markdown source documents instead of copying full pipeline bodies.
- `architecture-overview.html` should prefer focused UML-like ownership and runtime-flow diagrams over exhaustive component listings.
- Mermaid diagrams may be rendered from CDN for human-readable pages; if CDN rendering is unavailable, the Mermaid source text should remain readable rather than blocking the document.

## 2026-05-17 - Shop Slots Are Prefab-Instantiated From Typed Anchors

Decision:
Prefer authored slot anchor transforms plus a `ShopSlot` prefab reference for merchant shop layouts. Each anchor can specify the stock type filter (`Any`, `Weapon`, `Relic`, or `Consumable`) used by shop stock rolling.

Reason:
Scene-copied shop slot objects made layout maintenance repetitive and mixed visual placement with live slot behavior. Anchors keep scene authoring focused on position, while the prefab keeps slot presentation/interaction changes centralized.

Implications:
- `MerchantNPC` may instantiate or reuse `ShopSlot` instances under configured anchors during play.
- Existing child `ShopSlot` objects remain a compatibility fallback when prefab-slot authoring is not configured.
- `ShopDefinitionSO` still owns visible slot count, stock weights, and max weapon/consumable caps.
- Typed slot layouts must keep `ShopDefinitionSO.MaxWeaponSlots` and `MaxConsumableSlots` aligned with the intended display.

## 2026-05-17 - Enemy Player Targeting Uses Canonical Player Root

Decision:
For enemies targeting the `Player` tag, resolve the target transform through `PlayerRuntimeRegistry`/`PlayerInteractor2D` and map player-owned child colliders or directly assigned child transforms back to the player root.

Reason:
Player-attached relic/effect objects such as orbiting feathers can have colliders near the player. If boss encounter or combat movement targets those child objects, the boss appears to move even when the actual player is stationary.

Implications:
- Shared `Enemy` target acquisition should not use a player-owned child collider transform as the movement target.
- Explicit boss/enemy target assignment should normalize player-owned child transforms before the blackboard or movement code reads target position.
- Future player-attached objects should still avoid unintended `Player` tags, hurtboxes, or body-blocking colliders.
- Collision and damage target authoring remain separate from movement targeting and should be reviewed independently when new attached objects are added.

## 2026-05-17 - Final Boss Route Skips Chest Activation

Decision:
When `BossRewardContext.IsFinalRouteSet` is true, `BossBattleEndHandler` keeps the authored `TreasureChest` inactive, still spawns boss physical drops, marks rewards handled, and lets portal activation continue through the existing path.

Reason:
The final route ends the run sequence, so a post-boss reward chest is not needed. Physical drops are not chest presentation and should keep behaving like other boss death drops. Keeping portal handling unchanged preserves the existing final-route exit flow.

Implications:
- Final boss scenes can keep the common `BossBattleEndHandler` wiring without spawning/showing a chest.
- Final boss physical drops such as magic stones and field-heal pickups still spawn from the boss death position.
- Do not add a final-boss chest fallback unless final-route reward policy changes.
- Non-final boss reward behavior remains unchanged.

## 2026-05-17 - Room And Chest Locks Count Registered Combatants Plus Slime Splits

Decision:
Room and chest monster-kill locks count spawn-registered enemy roots and Slime split descendants that inherit a registered parent's lock context. General direct summons are excluded from lock conditions. Transform or phase changes on the same tracked GameObject remain the same enemy. Death presentation remains lock-active until the tracked root GameObject is destroyed.

Reason:
Lock state should follow explicit room/chest participation instead of every enemy instantiation. Slime splitting is the intended exception because the split children replace the parent's combat presence and must keep doors/chests locked while alive.

Implications:
- `RoomDoorMonsterKillLock` and `ChestMonsterKillLock` continue using tracked root GameObject lifetime.
- Slime split code must register valid split children into the same lock context as the parent.
- Boss/local summons and other direct `Instantiate(...)` enemies do not affect room/chest clear unless a future design explicitly registers them.
- No-loot or gimmick enemies count only when they enter through the same spawn registration or Slime split inheritance path.
