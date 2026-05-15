---
status: active
authority: refactor-backlog
category: refactor-index
last_reviewed: 2026-05-16
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

## Priority Model

Priority is about execution order, not severity of current bugs.

| Priority | Meaning | Rule |
| --- | --- | --- |
| P1 | Prepare before broad reorganization or repeated content expansion. | Review before moving related files/folders, adding new systems in the area, or changing shared runtime policy. |
| P2 | Trigger-driven structural work. | Keep proposed until the listed trigger occurs; then scope a focused implementation. |
| P3 | Watch item / localized cleanup. | Do not start independently unless the exact component or feature is already being edited. |
| Blocked | Pending design question, not an executable backlog item. | Do not create or implement a refactor until the design rule is decided. |

## Priority Board

| Priority | Candidate | Backlog Status | Why It Sits Here | Start When |
| --- | --- | --- | --- | --- |
| P1 | [Inventory Transfer Responsibility Split](./InventoryTransferResponsibilitySplit.md) | resolved | Quick-move target selection, transfer execution, rollback, relic merge/level handling, minimal transfer result reporting, player/chest adapters, slot warning/refresh handoff, shared delivery warning mapping, and runtime adapter file ownership are now split from UI view bodies. | Reopen only for new inventory container/item category, chest/world/equipment transfer change, relic merge/level UX change, or transfer contract extraction that creates new shared policy debt. |
| P1 | [Loot Reward Policy Boundary Split](./LootRewardPolicyBoundarySplit.md) | resolved | Chest loot generation, chest reward policy, world pickup delivery, shared delivery warning mapping, loot pool context/provider/selection, live source provider snapshots, monster/grave drops, and boss reward spawn now have helper boundaries/files. BossDrop prefab-reference migration is tracked separately. | Reopen only for new reward source, loot exclusion rule change, chest upgrade/modifier work, world pickup UX change, or loot table expansion that creates new reward-policy debt. |
| P1 | [Run Modifier Aggregation Boundary Split](./RunModifierAggregationBoundarySplit.md) | resolved | Reward consumers have a `RunRewardModifierSnapshot` boundary, and service/delta/snapshot/aggregation/provider/rebuild files now live under the progression run modifier layer. `RunModifierService` remains the source-compatible singleton facade. | Reopen only for a new modifier source, boss/chest/grave/shop reward modifier expansion, or a lifecycle/API change that requires a new non-singleton boundary. |
| P1 | [Scene Run State Boundary Split](./SceneRunStateBoundarySplit.md) | resolved | `RunProgressCoordinator`, `ScenePortalTravelService`, `GamePlayDataManager`, and `PlayerSceneRestoreBootstrapper` now delegate policy/execution details to dedicated helper files; remaining lifecycle/naming debt is tracked as a P2 follow-up. | Reopen only if the helper/file boundary itself regresses. Use `SceneRunStateLifecycleOwnershipSplit` for lifecycle, naming, or scene-facing contract work. |
| P1 | [Scene Domain Bootstrap Boundary Split](./SceneDomainBootstrapBoundarySplit.md) | resolved | Title/game bootstrap rules are documented in Architecture, and the current code policy is split into helper files for title launch, scene-domain scope decisions, return-to-title execution, and camera title guards. | Reopen only for new title entry modes, continue-run semantics, return-to-title behavior changes, app/gameplay service bootstrap expansion, or camera title behavior changes. |
| P2 | [Upgrade Runtime Boundary Split](./UpgradeRuntimeBoundarySplit.md) | partially-refactored | `UpgradeManager` now delegates purchase transaction policy, purchase success ordering, runtime effect handoff, run-start eligibility/timing, UI open fade/input-blocker execution, lifecycle subscription/run-start guard state, and unlock-check save/notification execution to helpers, but still owns singleton/persistence setup, purchase callback wiring, public events, and public UI entry points. | New upgrade effect type, run-start effect bug, upgrade UI blocker/fade issue, or upgrade save timing work. |
| P2 | [Runtime Presentation Fallback Authoring Split](./RuntimePresentationFallbackAuthoringSplit.md) | proposed | Several production-facing UI/presentation paths still construct visual hierarchy at runtime. This is a build-facing authoring risk, not a gameplay blocker. | Loading/cursor/letterbox/status HUD/Boss HUD visual polish, Canvas/raycast/sorting conflict, or adding another runtime-created UI fallback. |
| P2 | [Scene Run State Lifecycle Ownership Split](./SceneRunStateLifecycleOwnershipSplit.md) | proposed | P1 helper/file split is complete, but portal travel static entry, run-session naming/lifecycle, restore confirmation/pending-state ownership, and boss run-progress bridge ownership still need scene-facing design before code movement. | Portal/run/save/boss battle-end flow change, new transition type, restore lifecycle edit, BossDrop migration, or planned scene/prefab reference pass. |
| P2 | [Combat Element Build-Up Source Unification](./CombatElementBuildUpSourceUnification.md) | resolved | Applied element build-up resolves from attacker `ElementOffenseSource`, ignored producer code and runtime payload APIs are removed; serialized legacy fields/assets are intentionally kept as compatibility data. | Reopen only for new elemental weapon tuning policy, legacy weapon asset/schema migration, or a regression in attacker-wide build-up application. |
| P2 | [BossDrop Responsibility Split](./BossDropResponsibilitySplit.md) | partially-refactored | Runtime split and reward-spawn helper file ownership are mostly done, but `BossDrop` remains a prefab-safe legacy adapter and fallback reference holder. | Boss prefab/scene wiring, boss reward modifier, portal activation, or removing legacy boss reward references. |
| P3 | [Boss HUD Special-Case Source Split](./BossHudSpecialCaseSourceSplit.md) | proposed | The Slime Queen split-health exception leaks into common Boss HUD, but it is localized and should not drive a standalone refactor unless HUD work resumes. | Another multi-body/shared-health boss, Slime Queen phase-two HUD edit, or Boss HUD health-channel rework. |

## Blocked / Not Backlog Yet

| Topic | Current Location | Why Blocked |
| --- | --- | --- |
| Room/chest lock overlay count semantics | [Boss And Mob Encounter Structure](../StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md) | Split mobs, summons, transforms, delayed death presentation, and gimmick/no-loot enemies need a design rule before a lock-tracking refactor can be scoped. |

## Recommended Next Work

- If the next goal is inventory/chest/world item work, start with P1 `InventoryTransferResponsibilitySplit`.
- If the next goal is content/reward expansion, use resolved P1 `LootRewardPolicyBoundarySplit` and resolved P1 `RunModifierAggregationBoundarySplit` as context.
- If the next goal is scene/save/run transition work, use resolved P1 `SceneRunStateBoundarySplit` for current helper boundaries and scope lifecycle work through P2 `SceneRunStateLifecycleOwnershipSplit`.
- If the next goal is title/profile/bootstrap or return-to-title work, use resolved P1 `SceneDomainBootstrapBoundarySplit` as context and create a new focused backlog item only if new debt appears.
- If the next goal is UI or presentation polish, use P2 `RuntimePresentationFallbackAuthoringSplit`.
- Do not start the blocked lock overlay topic until split/summon/transform/death-delay counting rules are decided.

## Current Documents

- [BossDrop Responsibility Split](./BossDropResponsibilitySplit.md)
- [Inventory Transfer Responsibility Split](./InventoryTransferResponsibilitySplit.md)
- [Loot Reward Policy Boundary Split](./LootRewardPolicyBoundarySplit.md)
- [Scene Domain Bootstrap Boundary Split](./SceneDomainBootstrapBoundarySplit.md)
- [Scene Run State Boundary Split](./SceneRunStateBoundarySplit.md)
- [Scene Run State Lifecycle Ownership Split](./SceneRunStateLifecycleOwnershipSplit.md)
- [Upgrade Runtime Boundary Split](./UpgradeRuntimeBoundarySplit.md)
- [Run Modifier Aggregation Boundary Split](./RunModifierAggregationBoundarySplit.md)
- [Runtime Presentation Fallback Authoring Split](./RuntimePresentationFallbackAuthoringSplit.md)
- [Combat Element Build-Up Source Unification](./CombatElementBuildUpSourceUnification.md)
- [Boss HUD Special-Case Source Split](./BossHudSpecialCaseSourceSplit.md)
