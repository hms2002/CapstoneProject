---
status: active
authority: project-log
category: decision-log
last_reviewed: 2026-05-23
---

# Decision Log

## 2026-05-25 - P2 Drain Completion Restores The Drain

Decision:
When a phase-two Slime Queen finishes the drain sink sequence, `DrainPipe` restores to its initial damageable cork state instead of becoming permanently blocked.

Reason:
The design intent is a temporary 4-second boss groggy/control-loss mechanic. The drain should be usable again after being re-opened by hits, not removed from future play.

Implications:
- `DrainPipe` resets `currentHitCount` to `0` after the P2 boss exits.
- The drain visual returns to the cork color/state.
- Existing captured Pawn slime targets are cleaned up during restore so disabled suction targets do not remain.
- Future P2 drain changes should avoid permanent disable/blocked flags unless a separate design request asks for one-time drains.

## 2026-05-25 - Boss HUD Reads Source Snapshots

Decision:
`BossHudController` reads `IBossHudSource` / `BossHudSnapshot` values instead of adding concrete boss-type branches for split or multi-body bosses.

Reason:
Slime Queen phase two needs two body channels, but the common HUD should remain a projection layer. Keeping Short/Long lookup and dual-channel rules in `SlimeQueenPhaseTwoHudSource` prevents the controller from accumulating boss-specific lifetime and display policy.

Implications:
- Normal bosses use `SingleBossHudSource`.
- Slime Queen phase two uses `SlimeQueenPhaseTwoHudSource`.
- Future split, multi-body, or shared-health bosses should add their own source/adapter instead of editing `BossHudController` with concrete type checks.
- Dedicated dual groggy UI references can be authored later; runtime fallback is only a migration path.

## 2026-05-23 - Run-Special Unavailable Responses Belong To Choices

Decision:
Run-special NPC unavailable response lines belong to the selected `RunSpecialNpcChoiceDefinition`, not to a top-level dialogue branch, when the failure is discovered at choice execution time.

Reason:
Different choices and future feature actions can fail for different reasons. Keeping the failure line with the choice lets authors control "what the NPC says after this selected option cannot execute" without forcing every feature to add another initial branch.

Implications:
- `RunSpecialNpcInteractor` must check `CanExecute(...)` before success response lines and before `Execute(...)`.
- Unavailable choices may remain visible only when the choice has `unavailableResponseLines` and the feature explicitly allows showing that unavailable state.
- Construction insufficient-funds feedback is a payment-choice unavailable response, while construction pending/completed remain feature branch states.

## 2026-05-23 - Same-Scene Teleport Splits Appearance And Landing Points

Decision:
Run-special same-scene teleport uses two authored anchors: `appearancePoint` for where the player becomes visible after fade-out, and `landingPoint` for the final playable position. The old `destination` serialized reference migrates to `landingPoint`.

Reason:
The teleport arrival presentation needs a separate visual start position and final control-restoration position. A single destination can move the player, but it cannot express "appear here, then land here" without overloading one transform.

Implications:
- `landingPoint` is required for teleport availability.
- `appearancePoint` is optional; if it is empty, the feature uses `landingPoint` as the appearance point and preserves the old one-anchor behavior.
- Player protection and targetability blocking release after the appearance-to-landing movement finishes.

## 2026-05-23 - Same-Scene Teleport Uses Player Targetability Gate

Decision:
During run-special same-scene teleport execution, the player is protected by both `PlayerCinematicProtection` and `PlayerTargetabilityBlocker`. Enemies keep their normal perception flow, but `Enemy.CanPerceiveTarget(...)` treats a blocked player target as not perceivable.

Reason:
Teleport arrival presentation should not make nearby enemies chase or attack while the player is still in a cinematic/control-locked state. Invulnerability already prevents damage, but it should not also become the meaning for enemy recognition.

Implications:
- `PlayerTargetabilityBlocker` is a player-owned owner-token gate and can be reused by future player-centered presentation flows.
- Enemy recognition resumes when the teleport feature releases the gate, currently after its fade/warp/arrival movement execution finishes.
- Boss combat activation and general enemy objects are not disabled by this gate; only player targetability is filtered.

## 2026-05-23 - Same-Scene Teleport Executes After Run-Special Presentation Close

Decision:
Only same-scene teleport run-special features execute after speech/letterbox/HUD presentation closes and gameplay time is restored. Construction and other default run-special features continue to execute inside the paused dialogue flow.

Reason:
`RunSameSceneTeleportNpcFeature` relies on `MovementMotor2D.WarpTo(...)`, which applies the warp in `FixedUpdate`, then waits for `WaitForFixedUpdate`. Running that while `RunSpecialNpcInteractor` holds `Time.timeScale = 0` can fade the screen without ever applying the warp.

Implications:
- `RunSpecialNpcFeatureBase.ExecuteAfterRunSpecialPresentationClose` defaults to `false`.
- `RunSameSceneTeleportNpcFeature` overrides that policy to `true`.
- `RunSpecialNpcInteractor` restores run timer pause and `Time.timeScale` before executing post-presentation features, while keeping input/player locks until the feature finishes.

## 2026-05-23 - Run-Special NPC Dialogue Data Lives In DialogueSetSO

Decision:
Run-special NPC authored lines and choices live in `RunSpecialNpcDialogueSetSO`. `RunSpecialNpcInteractor` reads that SO and asks its `primaryFeature` for the current `RunSpecialNpcDialogueBranchKey`; feature-specific branch selection and text formatting stay on the feature implementation.

Reason:
Construction and teleport NPCs need different branch states, but the authored line/choice data should be reusable content rather than extra scene components. Keeping SO data separate from scene feature references also avoids putting scene component references inside reusable assets.

Implications:
- `RunSpecialNpcInteractor` owns presentation flow and generic choice-action execution only.
- `RunConstructionNpcFeature` returns construction branch keys, including `ConstructionInsufficientFunds`, and resolves the `N일` token.
- `RunSameSceneTeleportNpcFeature` returns teleport branch keys.
- `RunSpecialNpcDialogueSetSO` uses feature-kind-specific branch groups, shown through a custom Inspector.
- Line breaks authored inside one `RunSpecialNpcLine` text field are interpreted as separate speech-bubble lines at playback time. Visual wrapping inside one line should rely on the speech bubble layout instead of hard line breaks.
- Choice execution is represented by action enum values such as `ExecutePrimaryFeature`, not direct scene component references in the SO.
- The earlier provider-component decision is superseded for runtime. Provider components are migration-only compatibility until scene/prefab data is moved to SO assets.

## 2026-05-23 - Run-Special NPC Dialogue Branching Lives In Providers

Decision:
Run-special NPC line and choice branching belongs to feature-specific `RunSpecialNpcDialogueProviderBase` components. `RunSpecialNpcInteractor` remains the presentation flow owner that executes provider-built branches, but it should not contain construction-specific, teleport-specific, or future feature-specific branch rules.

Reason:
Construction and teleport NPCs split dialogue on different state axes: construction progress, completion, remaining run count, affection lock, destination availability, and feature execution eligibility. Keeping those decisions inside the interactor made generic presentation code depend on one feature's serialized field names and token replacement rules.

Implications:
- Construction dialogue state lives in `RunConstructionNpcDialogueProvider`.
- Same-scene teleport dialogue state lives in `RunSameSceneTeleportNpcDialogueProvider`.
- `RunSpecialNpcInteractor` asks for a `RunSpecialNpcBranch`, plays lines, shows choices, then executes the selected feature.
- Feature-specific line text formatting, such as the construction `N일` remaining-run token, belongs to the provider that understands the feature.
- Moving old interactor-authored fields to provider components requires Unity Editor migration; `[FormerlySerializedAs]` is not enough because the data moves across components.

## 2026-05-23 - Encyclopedia Relic Body Uses Logic Tooltip

Decision:
The encyclopedia Relic RightPage displays `RelicLogic.BuildTooltip(...).effectText` as the relic body text. It does not display `RelicDefinition.description` in the current Item slice.

Reason:
The inventory hover `ItemDetailPanel` and the encyclopedia should expose the same relic information. Relic logic assets are the source of truth for effect text, formatting tokens, bullet lines, glossary links, and per-level preview output; the serialized relic description can drift from the actual drop tooltip.

Implications:
- `EncyclopediaItemRightPage` writes formatted relic effect text into the authored body slot used by `StoryText`.
- `RelicDefinition.description` remains available for item data, but is not used by the encyclopedia Relic tab body in this slice.
- A level `1 / 1` relic still shows Prev/Next preview guides as disabled authored controls instead of hiding the guide objects.

## 2026-05-22 - Encyclopedia Content Disappear Reuses ContentAppear Clip

Decision:
The encyclopedia book content transition uses the authored `ContentAppear` animation clip as the single content-cover source. Appear samples the clip forward. Disappear samples the same clip backward through `AnimationClip.SampleAnimation`; it does not require a separate `DisAppear` clip and does not use negative Animator speed.

Reason:
The content cover is a presentation overlay, not gameplay state. Keeping one authored clip reduces prefab wiring and avoids Animator reverse-play edge cases while still allowing a visible hide-before-swap and reveal-after-swap sequence.

Implications:
- `EncyclopediaBookPresentation` depends on `pageCoverImage` and `contentAppearClip`; `pageCoverAnimator` is optional.
- If `pageCoverAnimator` exists on the same object, it must not drive the cover while `SampleAnimation` is sampling, because its default state can replay `ContentAppear` forward and override the reverse disappear frames.
- Item sub-tab transitions should sequence content disappear, page turn, content swap, and content appear through the book presentation.
- Slot selection remains an immediate RightPage bind in this slice.

## 2026-05-22 - Encyclopedia Item Detail Uses Dedicated Presenter

Decision:
The encyclopedia Item tab uses `EncyclopediaItemRightPage` as the sole active RightPage item presenter. It does not use `ItemDetailPanel.Instance`, hover-canvas adoption, drag/drop, inventory tooltip behavior, or the older `EncyclopediaDetailPanel` component on the active Item layout.

Reason:
Runtime inventory item detail UI owns hover positioning and canvas migration behavior that conflicts with a scene-authored book page. The encyclopedia needs stable serialized RightPage references that project selected item data without owning gameplay state.

Implications:
- Item details for `Weapon`, `Relic`, and `Consumable` read from `ItemDatabase` and bind through `EncyclopediaItemRightPage`.
- Weapon ability rows are pooled from `Panel_AbilityBlock_Encyclopedia.prefab` / `WeaponAbilityBlockView`, not from `WeaponDetailViewV2`.
- `ItemDetailPanel` under `RightPage` is a layout/ScrollRect/content host only. Presenter ownership should stay on `RightPage` through `EncyclopediaItemRightPage`.
- `Tools/Encyclopedia/Wire Existing GlobalUIRoot Encyclopedia` removes duplicate child `EncyclopediaItemRightPage` presenters and legacy `EncyclopediaDetailPanel` components under RightPage.
- Boss affection/reward detail remains out of this presenter and should use a future boss-specific detail component.
- Inspector wiring should target `EncyclopediaItemRightPage` authored header/detail roots and the encyclopedia ability-block prefab, not runtime hover detail components.

## 2026-05-22 - Encyclopedia Item Tab Owns Item Page State

Decision:
`EncyclopediaScreen` owns popup stack behavior, open/close presentation, and top-level tab entry. `EncyclopediaItemTab` owns Item sub-tab state, page index, selected entry, and item data binding. `EncyclopediaItemLeftPage` and `EncyclopediaItemRightPage` own the authored left/right page references for the Item tab.

Reason:
Keeping every Item control and selection field on `EncyclopediaScreen` made the Inspector hard to understand and would not scale when Monster and Boss get their own tab-specific layouts. Splitting the Item tab keeps screen-level presentation separate from item-specific content behavior.

Implications:
- `EncyclopediaScreen.OpenUI()` calls `EncyclopediaItemTab.ShowDefault()` after the book-open presentation completes.
- `OpenWeaponSubTab`, `OpenRelicSubTab`, `OpenConsumableSubTab`, and page-button methods remain on the screen only as button-friendly forwarding methods.
- `EncyclopediaLeftPageView` and `EncyclopediaDetailPanel` are obsolete/migration fallback components, not the preferred new authoring target.
- Monster/Boss work should add their own tab/presenter components instead of expanding Item tab state back into `EncyclopediaScreen`.

## 2026-05-22 - Encyclopedia Tome Open Uses Authored Animator Sequence

Decision:
Tome interaction opens the encyclopedia through a `DimPanel` fade, a separate book motion-root drop, and the child book Animator `BookOpen` state. Close uses `BookClose`, LeftPage sub-tabs use `BookLeftPage`, and future RightPage main-tab transitions use `BookRightPage`.

Reason:
The book asset already exposes authored animation states, and the layout should remain scene-authored. Moving the parent motion root keeps screen-level drop timing separate from the child book Animator's page/book clips.

Implications:
- `EncyclopediaBookPresentation` owns sequencing and content visibility around the Animator states.
- `EncyclopediaScreen` delays list/detail binding until the open presentation completes.
- Unity authoring must wire `DimPanel`, `BookMotionRoot`, the book Animator, and optional content roots instead of relying on runtime hierarchy creation.

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

## 2026-05-18 - Electric Trigger Splits Electrocute Status From Discharge Logic

Decision:
Electric gauge completion applies a dedicated instant trigger effect that refreshes the electrocute duration status, deals secondary electric damage, and then performs the discharge chain. The electrocute status effect itself remains status-only.

Reason:
Status refresh and chain execution have different responsibilities. Keeping discharge in the trigger effect lets each electric gauge completion and chain reapplication deal damage exactly once without making every generic status refresh recursively start another chain.

Implications:
- `GE_ElectricShockTrigger` is the Electric gauge trigger effect; `GE_ElectrocutedStatus` only grants `State.Status.Electrocuted`.
- Discharge is a single nearest-neighbor chain: every step scans around the current unit, chooses the nearest already-electrocuted unit, and visits each unit once per discharge event.
- Electric damage is applied through `GE_Damage_Spec` with `Data.Damage` SetByCaller so it stays inside GAS damage handling and does not re-enter element build-up.
- Electric chain visuals are driven from the final ordered chain point list; the current authored prefab renders SpriteRenderer segments with `ElectricParticleTrail` without changing the gameplay chain logic.

## 2026-05-19 - Closed Doors Block Mob Player Perception

Decision:
General mob player perception treats a closed `DoorObject` on the enemy-target sight line as an immediate perception blocker.

Reason:
Room doors should prevent monsters from detecting or continuing to act on a player beyond the closed door, even if the distance-based detection range still contains the player.

Implications:
- Shared `Enemy` perception checks line of sight for closed `DoorObject` colliders.
- Target acquisition, chase detection/movement, common mob attack continuation, and Dead's Skeleton self-destruct checks use the shared perception rule.
- Opening the door restores normal distance-based detection without clearing the cached player target.
- The rule depends on door colliders being authored so physics raycasts hit the closed `DoorObject`.

## 2026-05-19 - Run Special NPCs Use Speech Bubble Flow

Decision:
Run-internal special NPCs such as construction and same-scene teleport NPCs should use a separate speech-bubble interaction flow instead of extending the existing `DialogueController` / Ink / portrait dialogue stack.

Reason:
These NPCs need in-world `SpeechBubble` UI, local world choices, same-scene movement, construction progress, and shortcut unlock behavior. That interaction shape differs from the existing visual-novel dialogue UI and should not overload the Ink dialogue path.

Implications:
- `DialogueController`, `DialogueView`, Ink start paths, and portrait presentation remain the existing visual-novel/boss dialogue path.
- Run special NPCs should start from `Docs/StructureMemory/ScriptSystems/RunSpecialNpcStructure.md`.
- Teleport NPCs are planned as same-scene player movement, not `ScenePortal` scene transitions.
- Construction NPC path opening should prefer durable shortcut/map progress and scene-authored blocked/open objects before direct runtime tilemap mutation.

## 2026-05-20 - Run Special NPC Choice UI Stays Authored

Decision:
The run-special NPC source slice provides a `RunSpecialNpcChoicePresenter` that drives serialized `CanvasGroup`, `Button[]`, and `TMP_Text[]` references, but it does not create the choice UI hierarchy at runtime.

Reason:
The project UI contract prefers scene/prefab-authored UI. This keeps the new speech-bubble flow usable from code while leaving visual layout, world-space canvas placement, button styling, and references as explicit Unity authoring work.

Implications:
- `RunSpecialNpcInteractor` owns flow state; the presenter only projects labels and relays input.
- Missing choice presenter references are authoring errors, not a reason to instantiate buttons or canvases at runtime.
- Scene/prefab review is required before content can use the flow in play.

## 2026-05-20 - Run Special NPC Choices Use Screen-Space World Anchors

Decision:
Run-special NPC choices should use an authored screen-space presenter under `GlobalUIRoot > DialogueCanvas`, positioned from the player world coordinate through `RunSpecialNpcChoiceAnchorFollower`.

Reason:
The choices need to feel attached to the in-world conversation, but they still need shared UI raycast, scaling, input guard, and canvas ordering. Screen-space overlay with a world-anchor follower keeps the UI in the global canvas while avoiding per-NPC world-space Canvas sorting and camera setup.

Implications:
- `RunSpecialNpcInteractor` owns when the choice panel is shown and which player transform it follows.
- `RunSpecialNpcChoiceAnchorFollower` owns only coordinate projection and canvas clamping.
- The presenter remains authored UI; it does not create buttons at runtime.
- Scene references to prefab-owned presenters should be validated in Unity after prefab import.

## 2026-05-20 - Construction Progress Uses Run-Special Save Data

Decision:
Construction NPC payment/progress is stored in `GameData.runSpecialNpcData.constructionRecords`, with run-active starts staged in `GamePlayData.pendingRunSpecialNpcConstructionStarts` and committed through `RunSessionProgressCommitPolicy`.

Reason:
Construction progress is neither UI state nor a pure door unlock. It needs a durable start point so "required run completions after payment" can be computed before opening the permanent shortcut.

Implications:
- `CurrencyManager` still owns magic-stone spending.
- `DoorObject` / `ShortcutProgressService` still own the final permanent path unlock.
- Existing profiles rely on `GameDataManager` normalization to initialize the new save data safely.

## 2026-05-20 - Encyclopedia V1 Uses Public Catalog Before Discovery Save

Decision:
The first encyclopedia implementation uses `EncyclopediaCatalogSO` as authored display data and treats every registered weapon, monster, and boss entry as visible. Player-specific discovery IDs and save schema changes are deferred to a later release task.

Reason:
The feature does not yet have a built encyclopedia screen, content validation, or hub opening flow. Building and reviewing that visible v1 first reduces the risk of coupling UI/presentation work to unvalidated save events.

Implications:
- The catalog owns display content, not player discovery state.
- `GameData` and encounter/acquisition event hooks are intentionally unchanged in v1.
- Release discovery should add a save-owned discovered ID set and record from weapon acquisition, monster encounter, and boss encounter/defeat events.
- The encyclopedia screen and interactable should stay prefab/scene-authored; editor-only builders may generate temporary authoring assets, but runtime must not create the UI hierarchy.

## 2026-05-20 - Encyclopedia Book Presentation Is Optional Authored Sprite Playback

Decision:
The encyclopedia can experiment with UI book open/close and page-cover reveal presentation, but the runtime still only drives authored `Image`, `CanvasGroup`, `SpriteRenderer`, and sprite-array references. Missing book presentation references fall back to the existing immediate/pixel-reveal behavior instead of creating UI at runtime.

Reason:
The updated planning note prefers a simpler already-open UI, but the current requester explicitly asked to try the book unfolding and page-appearance feel. Keeping the animation optional and authored preserves the UI stack/input policy while allowing visual iteration.

Implications:
- UI book close is handled through `ICloseRequestHandler` so the screen remains on the popup stack until the close presentation finishes.
- Category/item data still updates immediately behind a visual cover; item selection remains unanimated.
- EarthTome field-book sheets are treated as 48x40 frame strips by editor import support.
- If the experiment is rejected, the screen can clear `EncyclopediaBookPresentation` references and continue using the prior reveal fallback.

## 2026-05-20 - Encyclopedia Book Presentation Uses Animator Clips

Decision:
The encyclopedia UI book, page content-appear cover, and EarthTome world book presentation should be driven by authored `AnimatorController` states and `AnimationClip` assets, not by runtime sprite-frame stepping coroutines.

Reason:
The book/page presentation is authored visual rhythm. Letting an `Animator` own sprite frame binding makes the state visible and replaceable in Unity, matches the project's presentation authoring direction, and avoids hiding clip structure inside runtime code.

Implications:
- `EncyclopediaBookPresentation` only requests states such as `Open`, `Close`, `Opened`, `Closed`, and `ContentAppear`; it uses clip lengths only to sequence callbacks.
- `BookWorldSpriteSequencePresentation` keeps its legacy class name for serialized reference compatibility, but it now requests EarthTome Animator states such as `ClosedIdle`, `OpenedIdle`, `Open`, and `Close`.
- `EncyclopediaV1AssetBuilder` is responsible for creating/wiring the temporary `.anim` and `.controller` assets through UnityEditor APIs.
- Manual prefab YAML patching is not an acceptable substitute for UnityEditor API generation when adding Animator components or serialized clip/controller references.

## 2026-05-20 - Encyclopedia Entry List Uses Prefab-Instantiated Slots

Decision:
The encyclopedia entry list uses an authored `EncyclopediaEntrySlot.prefab` instantiated and pooled under an authored `entryGridRoot`. Serialized `entrySlots` remain only as a migration fallback for old generated prefabs.

Reason:
The fixed 48-slot vertical list was hard to style, capped visible entries, and produced solid rectangular rows that competed with the book art. A prefab slot gives the UI a single editable holder/highlight/icon structure while keeping runtime creation limited to approved authored instances.

Implications:
- Runtime encyclopedia code may instantiate `EncyclopediaEntrySlot.prefab` but must not create raw `Button`, `Image`, `TMP_Text`, `Canvas`, or arbitrary UI hierarchy.
- `EncyclopediaScreen.prefab` must wire both `entryGridRoot` and `entrySlotPrefab` to use the new grid path.
- `EncyclopediaEntryButton` should resolve named `Icon`, `IndexText`, and `TitleText` children before fallback searches, so the holder background is not mistaken for entry art.
- Final visual layout can be tuned by editing the slot prefab and grid root in Unity without changing the runtime list population policy.

## 2026-05-22 - Encyclopedia Detail Uses Fixed Right-Page Presenter

Decision:
The encyclopedia right-page detail layout uses dedicated fixed presenters instead of reusing inventory `ItemDetailPanel`. This entry originally introduced `EncyclopediaDetailPanel`; the current Item tab presenter is now `EncyclopediaItemRightPage`, and `EncyclopediaDetailPanel` is retained only as a migration fallback.

Reason:
`ItemDetailPanel` is a hover UI singleton that adopts itself to the hover canvas and hides itself during `Awake()`. Reusing it inside the book page would fight the authored RightPage layout and inventory hover lifecycle.

Implications:
- `EncyclopediaItemRightPage` is the preferred Item RightPage wiring target.
- `EncyclopediaDetailPanel` may still exist on older layouts while serialized references migrate.
- RightPage presenters must not create arbitrary runtime UI hierarchy and must not invoke inventory hover controllers, drag/drop, or item detail popup behavior.

## 2026-05-22 - Encyclopedia Current Slice Is Item First

Decision:
The current encyclopedia implementation slice is limited to the Item tab, with `Weapon`, `Relic`, and `Consumable` sub-tabs active. Monster pages, boss pages, monster theme tabs, and boss-specific tabs/details are deferred.

Reason:
The full encyclopedia UI would take too long to finish in one pass, and the user is actively authoring the item-side layout first. Keeping the slice narrow prevents unfinished Monster/Boss UI from blocking item iteration.

Implications:
- `EncyclopediaScreen` keeps Monster/Boss code paths but exposes them as disabled serialized category toggles by default.
- Current scene/prefab authoring should wire and validate the Item tab, including weapon/relic/consumable layouts.
- Deferred category work is tracked in `Docs/RefactorBacklog/EncyclopediaDeferredCategories.md`.

## 2026-05-22 - Encyclopedia UI Uses Layout-Scoped View Components

Decision:
Split encyclopedia UI driving into layout-scoped tab/page components while keeping `EncyclopediaScreen` as the popup-stack and presentation owner.

Reason:
The item encyclopedia layout is now authored directly in Unity. Keeping every title, tab, page button, slot grid, and detail reference on `EncyclopediaScreen` makes the Inspector hard to reason about and couples layout editing to screen-level state.

Implications:
- `EncyclopediaScreen` owns open/close policy, main-tab entry, and presentation sequencing.
- `EncyclopediaItemTab` owns Item sub-tab, page, selection, and item data binding.
- `EncyclopediaItemLeftPage` owns authored Item LeftPage controls such as title, item sub-tab buttons, pagination labels, and list notices.
- `EncyclopediaItemRightPage` owns authored Item RightPage header/detail roots and weapon ability block pooling.
- `EncyclopediaEntryGridView` owns the slot pool and page binding under the authored grid root.
- `EncyclopediaLeftPageView` and `EncyclopediaDetailPanel` remain migration fallbacks only.
- View components relay user input upward and project state downward; they should not own catalog selection or gameplay state.

## 2026-05-22 - Encyclopedia Item Data Reads ItemDatabase Directly

Decision:
The item/weapon side of the encyclopedia reads `ItemDatabase.allWeapons` directly instead of requiring `EncyclopediaCatalogSO` as a copied weapon list.

Reason:
Weapon, relic, and consumable source data already exists in item definition/database assets. A separate catalog for those entries creates duplicate maintenance and makes the encyclopedia look like another source of truth.

Implications:
- `EncyclopediaItemTab` uses `ItemDatabase` for weapon/relic/consumable counts, names, icons, and detail binding.
- `EncyclopediaCatalogSO` remains only as a legacy fallback and temporary Monster/Boss display-data path until those pages get their own source/provider shape.
- `EncyclopediaInteractable` should no longer require a catalog for the item page to open.
- Relic/Consumable subtabs also read existing item data sources directly rather than adding copied catalog entries.

## 2026-05-20 - DemonKing EgoSword Subpatterns Stay In GAS

Decision:
EgoSword dropped VerticalStrike and CrossLaser are DemonKing-owned GAS abilities, but they run through `ParallelIndependent` AbilityDefinitions triggered by EgoSword's independent dropped-pattern runner instead of the main `BossPhaseConfig` pattern timer.

Reason:
The sword's dropped behavior is still part of DemonKing's pattern kit, so it should remain inspectable and cancellable through GAS. Its cadence is intentionally separate from the boss FSM's main pattern selection, so putting it in phase selection would couple two timers that should stay independent.

Implications:
- `DemonKingController` registers the two subpattern AbilityDefinitions separately from phase pattern entries.
- `EgoSwordActor` owns only the interval/toggle runner and visual state cleanup, then asks DemonKing's AbilitySystem to activate the subpattern ability.
- The subpattern AbilityDefinitions must remain `ParallelIndependent` and instant, otherwise they can be blocked by the boss's active main pattern execution.
- EgoSword should not grow its own runtime-created AbilitySystem for these patterns unless a future design explicitly separates sword ownership from DemonKing.

## 2026-05-20 - Hit Impact Cue Classification Lives On ALData

Decision:
Hit impact cue classification is authored on ALData at the actual hit unit, not on `AbilityDefinition`.

Reason:
Combo attacks, multi-hit patterns, and projectile/helper hit configs can mix slash, blow, or suppress-hit styles inside one AbilityDefinition. ALData already owns execution presentation intent and builds the `CombatHitPayload`, so it is the narrowest authoring layer that can describe each hit correctly.

Implications:
- Runtime transport is `ALData -> CombatHitPayload -> CombatDamageAction -> AbilityEventData -> AbilityHitCueRouter`.
- `AbilityDefinition` hit-confirmed cue lists remain for explicit reusable cues such as hit spark and camera shake.
- `HitImpactCueKind.Default`, `Slash`, and `Blow` currently resolve to `SlashHit` until a separate BlowHit cue exists; `None` suppresses the automatic impact cue.
- New combo or hit-config ALData should expose the classification at the same granularity as the hit payload it creates.

## 2026-05-20 - Runtime Split And Summoned Mobs Do Not Drop Monster Loot

Decision:
General mobs created by runtime split or boss/local summon paths suppress the normal `Mob.OnDeathStarted` monster loot request. Spawn-registered mobs from `MonsterSpawner` / `MonsterSpawnContainer` keep the existing monster drop behavior.

Reason:
Split and summon mobs are not authored baseline room population. Letting every temporary child roll the full monster loot table can multiply rewards, including field-heal pickups, beyond the intended room placement.

Implications:
- Slime split descendants can still inherit room/chest lock tracking, but they do not spawn monster loot on death.
- SlimeQueen call/drop summons and Witch retreat skeleton summons are marked no-loot immediately after instantiation.
- Boss rewards, boss physical drops, graves, chests, and direct world pickups are outside this policy.

## 2026-05-20 - Construction Sites Use Additive Tilemap State Modules

Decision:
Run construction shortcuts use scene-authored additive `ConstructionSiteTilemapModule` blocks with `BlockedState` and `OpenState` roots instead of editing the main Ground/Wall tilemaps at runtime.

Reason:
Construction completion needs a visible wall-before/path-after map change, but raw `Tilemap.SetTile` mutation would be harder to author, review, save, and validate. A block module keeps temporary wall tilemaps, open ground tilemaps, optional Door/Shortcut anchors, and optional Chest objects together as one scene-authored construction site.

Implications:
- `RunConstructionNpcFeature` should call the construction site module when assigned; its direct blocked/open root and target door fields are fallback authoring only.
- Save data stores semantic construction completion by `constructionId`, not individual tile cells.
- Tilemap consumers that scan the scene must ignore inactive tilemaps, and pathfinding must accept additive open ground tilemaps after completion.

## 2026-05-20 - Run-Special NPC Interaction Freezes Time With Unscaled Presentation

Decision:
Run-special NPC interaction pauses the run timer and `Time.timeScale`, while its speech-bubble typing, line skip, choice UI, and letterbox presentation run on unscaled time.

Reason:
These NPCs are an interaction flow rather than live combat dialogue. The player should not lose run time or be attacked through scaled gameplay while reading construction/teleport choices, but the presentation still needs to animate and accept Dialogue-style skip input.

Implications:
- `RunSpecialNpcInteractor` owns the time-scale pause and restores the previous value during cleanup.
- `SpeechBubble` uses unscaled DOTween updates so bubble typing, auto-hide, fade, and scale presentation continue while `Time.timeScale` is `0`.
- Run-special line skip uses left click or `InputActionId.DialogueAdvance`; run-special choices remain click/number-key only so Space does not confirm choices.
- The existing `CinematicLetterboxOverlay` can be reused for letterbox plus HUD fade, but run-special NPCs pass an explicit faded layer list that excludes `GlobalCanvasLayer.Dialogue` so the authored `DialogueCanvas` choice panel stays visible and interactive.

## 2026-05-20 - Run-Special Speech Bubbles Pre-Size Only In Special NPC Flow

Decision:
Run-special NPC dialogue uses a dedicated pre-sized speech-bubble call that measures the full line, clamps maximum text width, enables wrapping, and then types inside the prepared bubble. General speech-bubble users keep the existing empty-text-then-grow `Speak(...)` path.

Reason:
Construction/teleport NPC lines are longer and should feel closer to dialogue UI, but globally changing `SpeechBubble` sizing would alter unrelated short world barks and cinematic bubbles.

Implications:
- `RunSpecialNpcInteractor` owns the opt-in sizing limits for this flow.
- `SpeechBubbleComponent.Speak(...)` remains the compatibility path.
- `SpeechBubbleComponent.SpeakWithPreSizedLayout(...)` is the explicit path for dialogue-style wrapping.

## 2026-05-22 - Authored GlobalUIRoot Encyclopedia Uses Scoped Wiring Tool

Decision:
The current authored `GlobalUIRoot.prefab` encyclopedia layout is repaired through `Tools/Encyclopedia/Wire Existing GlobalUIRoot Encyclopedia`, not through `EncyclopediaV1AssetBuilder` and not through direct prefab YAML string patches.

Reason:
The active encyclopedia layout is user-authored under `GlobalUIRoot/EncyclopediaUI/Book`. The V1 builder can rebuild generated shell assets and has previously been a risky path for preserving the current layout. A scoped UnityEditor API tool can wire existing components and report missing authored roots while keeping visual placement under Unity authoring control.

Implications:
- `EncyclopediaV1AssetBuilder` remains a generated-asset support tool, but it should not be used as the repair path for the current `GlobalUIRoot` layout.
- Prefab reference repairs should use `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` or direct Unity Inspector work.
- The wiring tool should not create replacement visual layout, item sub-tab buttons, or detail section roots for the current authored layout. Missing roots are authoring gaps to fix in Unity.
- After running the wiring menu, Inspector review is still required because duplicate generic names in authored UI can bind to the wrong candidate.

## 2026-05-22 - Encyclopedia Ability Switch Is Variant-Only

Decision:
`Panel_AbilityBlock_Encyclopedia` represents one displayed weapon skill. Skill1 and Skill2 are separate block instances. The switch guide and preview behavior are shown only when that specific skill has multiple tooltip variants through `IAbilityTooltipVariantProvider`.

Reason:
The switch UI represents replacement-mode skills such as LightningSpear, not the fact that a weapon has two different skill slots. Treating normal Skill1/Skill2 as one switchable panel collapses distinct abilities into a confusing single UI block.

Implications:
- Normal weapons should show two separate ability blocks when both Skill1 and Skill2 exist.
- Variant-capable skills may reserve an extra preview block for the switch animation, but that preview belongs to that one skill's variant cycle.
- `AbilityBlockContainer` must be authored or normalized as a vertical list that controls child height so pooled block instances do not overlap.

## 2026-05-22 - Encyclopedia Item RightPage Uses Type Sections

Decision:
The encyclopedia Item RightPage uses common `Icon` / `Name` / `Description` fields plus item-type section roots instead of one shared metadata/story presenter for Weapon, Relic, and Consumable.

Reason:
Weapon, Relic, and Consumable detail layouts have different information contracts. Automatically injecting ID, rarity, max level, restore amount, or target attribute into one shared text path made the UI harder to author and did not match the planned encyclopedia layout.

Implications:
- Weapon entries show story, stat text, and separate Skill1/Skill2 ability blocks. They do not show a relic-style level preview.
- Relic entries show description, preview level, and `RelicLogic.BuildTooltip(...)` effect text. They do not show weapon stats or weapon skill blocks.
- Consumable entries show only name and description in the current encyclopedia scope.
- Any additional metadata must be added as an explicit authored section decision, not as a shared fallback string.

## 2026-05-22 - EncyclopediaScreen Is Orchestration Only

Decision:
`EncyclopediaScreen` owns only stack participation, open/close requests, main-tab routing, and book/dim presentation. Item sub-tab state, page index, selection, grid binding, and detail binding belong to `EncyclopediaItemTab`, `EncyclopediaItemLeftPage`, and `EncyclopediaItemRightPage`.

Reason:
Putting Item data and page state directly on the screen made the Inspector hard to understand and created multiple competing places for button/page/detail references. The screen should be readable as the popup shell; Item behavior should be readable in the Item tab components.

Implications:
- Screen public button methods remain for Unity Button bindings, but Item-related calls forward to `EncyclopediaItemTab`.
- `EncyclopediaScreen` should not regain direct item grid/detail/page serialized fields.
- If the screen is authored active, `closeOnRuntimeAwake` snaps the presentation closed and deactivates the screen at Play startup so opening still happens only through Tome/UI stack flow.

## 2026-05-22 - Encyclopedia Item RightPage Shares Story And Preview Roots

Decision:
The Item RightPage uses one shared `StoryText` body for Weapon, Relic, and Consumable descriptions, and Weapon stat output may share the same authored root as Relic level preview.

Reason:
The current authored layout is one detail panel whose internal elements are switched on/off per item type. Splitting description fields or forcing separate stat/preview roots added confusing fallback paths and did not match the layout the user is wiring.

Implications:
- Weapon mode writes story to `StoryText`, shows stat text, and hides Relic preview guide icons even when the stat root is the same object as the Relic preview root.
- Relic mode writes description to `StoryText`, shows preview level/effect, and enables preview guides only when the relic has multiple levels.
- Consumable mode writes description to `StoryText` and keeps weapon/relic-only elements inactive.
- Legacy metadata or type-specific description text fields should remain migration-only and should not be touched by the primary binding path.

## 2026-05-22 - Encyclopedia Book Presentation Is A Required Screen Dependency

Decision:
The authored encyclopedia screen must have an `EncyclopediaBookPresentation` on the Book/screen root when Tome open/close and tab page-turn animations are expected.

Reason:
Without that component, `EncyclopediaScreen` and `EncyclopediaItemTab` can only perform immediate content binding. That makes Tome interaction and tab changes look like no animation is wired even when Animator clips exist in the child Book asset.

Implications:
- `EncyclopediaScreen` does not add the missing component at runtime. Missing presentation references produce warnings and immediate fallback behavior until the prefab is wired.
- `Tools/Encyclopedia/Wire Existing GlobalUIRoot Encyclopedia` also adds/wires the component on `GlobalUIRoot/EncyclopediaUI/Book`.
- Direct prefab YAML string patching remains avoided; persistent prefab repair should happen through Unity serialization or the scoped wire tool.

## 2026-05-22 - Encyclopedia Runtime Does Not Self-Wire Missing UI References

Decision:
The active encyclopedia runtime path does not create presenter components, canvas/button/TMP/image objects, or missing UI references during `Awake`, `OpenUI`, or item binding. Runtime code validates required serialized references, logs explicit warnings, and disables or falls back only at the feature level.

Reason:
Silent runtime self-repair made the Inspector unclear and hid the difference between the intended authored `GlobalUIRoot` layout and accidental fallback behavior. The current goal is a stable authoring contract where `EncyclopediaScreen -> EncyclopediaItemTab -> EncyclopediaItemLeftPage / EncyclopediaItemRightPage` is visible in the prefab.

Implications:
- `Reset()` and explicit `Auto Wire References` context menus remain authoring conveniences; normal runtime binding should use serialized references.
- `Tools/Encyclopedia/Wire Existing GlobalUIRoot Encyclopedia` is the persistent repair path for `GlobalUIRoot.prefab`.
- `EncyclopediaEntryGridView` may instantiate the authored slot prefab pool, and `EncyclopediaItemRightPage` may instantiate the authored ability-block prefab pool. These are approved content pools, not fallback hierarchy construction.
- Missing `screenActiveRoot`, `itemTab`, `EntryGridView`, `StoryText`, `ScrollRect`, or ability-block references should be treated as prefab/scene wiring issues instead of being hidden by broad child searches.

## 2026-05-22 - EncyclopediaUI Is The Screen Active Boundary

Decision:
The encyclopedia popup active boundary is `EncyclopediaUI`, not the child `Book` object.

Reason:
The authored layout can place `DimPanel`, `Book`, and page/presentation objects as siblings under `EncyclopediaUI`. Closing only `Book` can leave `DimPanel` active and black out the screen even though the encyclopedia content appears closed.

Implications:
- `EncyclopediaScreen.screenActiveRoot` should resolve to or be wired to `EncyclopediaUI`.
- `OpenUI()` activates `screenActiveRoot` before resolving/presenting content.
- Runtime startup close and `CloseUI()` deactivate `screenActiveRoot`.
- `Book` remains the animation target for open/close/page-turns, not the popup lifetime root.

## 2026-05-23 - Run Special NPC Camera Focus Reuses Gameplay Camera State

Decision:
Run-special NPC dialogue focuses the existing `CameraBootstrap` gameplay `CinemachineCamera` on the NPC target and restores the cached gameplay camera state after the interaction. It does not create a new manager or runtime UI/camera hierarchy.

Reason:
The requested dialogue-start camera move matches the Merchant activation cinematic pattern. Reusing the gameplay camera target swap keeps the feature source-only and avoids adding another camera ownership layer for a local NPC conversation.

Implications:
- `RunSpecialNpcInteractor` caches Follow/LookAt/Priority, legacy `CameraFollow` enabled state, and `CinemachineBrain.IgnoreTimeScale` before focusing.
- Camera blending runs with unscaled time because run-special dialogue pauses `Time.timeScale`.
- Scene authoring may assign a specific `cameraFocusTarget`; otherwise the speech-bubble transform is used, then the NPC transform.
- User choices are framed on the player: the flow returns the camera to the player before showing the authored choice panel, then refocuses the NPC only when the selected choice has NPC response lines.
- Manual play validation is required for scene-specific framing and damping.
