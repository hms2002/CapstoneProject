---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-05-16
---

# Dialogue NPC Affection Structure

## Purpose

Map dialogue, NPC features, affection, merchant, upgrade, and boss dialogue scripts.

## Current Structure

| Area | Count | Responsibility |
| --- | ---: | --- |
| Dialogue / NPC / Affection | 81 | Dialogue service/controller, Ink runtime references, dialogue UI, boss dialogue, NPC data/database/features, merchant/upgrade features, affection state/rewards/UI. |

### Breakdown

| Area | Count | Responsibility |
| --- | ---: | --- |
| Upgrade Feature | 17 | Upgrade manager, feature, progress service, database, node/effect base, tree UI/editor, tooltip/slot UI, runtime accumulator, and lake presentation. |
| Dialogue UI | 13 | Dialogue view/theme, portrait/cinematic/text animation, slide/fade, sequencer, choice input/glyph/highlight, and failure effect UI. |
| Dialogue Core | 12 | Dialogue service/controller, variables/session/tag handling, trigger, participant registry, runtime resolver, audio info, story segment, emotes, and knot selector. |
| Merchant Feature | 10 | Merchant NPC, activation cinematic, run state, refresh, purchase, shop definition/policy/roll/slot, and world item detail presenter. |
| Upgrade Effects | 9 | Upgrade effect ScriptableObjects for unlocks, shop/chest/grave modifiers, run-start rewards, attributes, and empty effects. |
| Affection Runtime / UI | 7 | Affection manager, progress/reward processing, UI, gradient border, gain screen effect, and base effect. |
| Boss Dialogue / Encounter Dialogue | 5 | Boss dialogue runner, progress store, encounter director/dialogue, and boss talk manager. |
| Affection Effects | 3 | Unlock item, info-only, and boss run modifier affection effects. |
| NPC Data / Manager | 3 | NPC database, data, and manager. |
| NPC Feature Core | 2 | NPC feature controller and feature interface. |

### Upgrade Feature Hotspots

| Parent path | Hotspot | Count | Responsibility |
| --- | --- | ---: | --- |
| Dialogue / NPC / Affection > Upgrade Feature | Upgrade Runtime Services | 5 | Upgrade manager, progress service, effect applier, runtime target accumulator, and run modifier service. |
| Dialogue / NPC / Affection > Upgrade Feature | Upgrade UI / Presentation | 5 | Upgrade tree UI, tooltip, slot UI, lake surface image, and ripple graphic. |
| Dialogue / NPC / Affection > Upgrade Feature | Upgrade Data / SO | 3 | Upgrade database, node ScriptableObject, and base upgrade effect ScriptableObject. |
| Dialogue / NPC / Affection > Upgrade Feature | Feature Entry / Requests | 2 | Upgrade feature entry and cinematic request. |
| Dialogue / NPC / Affection > Upgrade Feature | Upgrade Editor | 1 | Upgrade tree editor. |
| Dialogue / NPC / Affection > Upgrade Feature | Upgrade Other | 1 | Upgrade lake presentation support. |

## Key Files

- `Assets/LeeJunMo/Script/Dialogue/DialogueService.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/NPCFeature/Upgrade/UpgradeFeature.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/NPCFeature/Merchant/MerchantNPC.cs`
- `Assets/LeeJunMo/Script/Dialogue/Affection/AffectionManager.cs`

## Ownership And Lifecycle

- Dialogue playback and its input block should be owned by `DialogueService`.
- NPC features that open stack UI after dialogue should wait for dialogue blockers to release before opening their UI.
- Merchant stock and upgrade progress are run/save-policy sensitive; do not treat UI state as the source of truth.
- Affection should contribute modifiers/rewards without bypassing base progression reward rules.

## Boundary Review

| Boundary | Current Shape | Review Outcome |
| --- | --- | --- |
| Dialogue playback and flow block | `DialogueService` owns dialogue playback state, `GameFlowInputBlocker`, and run timer pause while dialogue is active. | Good current owner. Feature UI should continue to wait for dialogue playback and external UI blocks to release before opening. |
| NPC feature handoff | `INPCFeature.Execute(onComplete)` supports both blocking dialogue tags and features that exit dialogue before opening UI. | Keep this visible. Future features should distinguish dialogue-continuing behavior from dialogue-exit-then-open-UI behavior. |
| Upgrade runtime | `UpgradeManager` owns upgrade public entry points, singleton/persistence setup, purchase callback wiring, and public UI/data-change events. Purchase transaction policy delegates to `UpgradePurchaseService`, purchase success ordering delegates to `UpgradePurchaseCompletionService`, runtime effect reapply/run-start/hub-target handoff delegates to `UpgradeRuntimeEffectService`, run-start eligibility/timing delegates to `UpgradeRunStartEffectPolicy`, UI open fade/input-blocker execution delegates to `UpgradeUiOpenFlow`, scene/run/player lifecycle subscription plus run-start guard state delegates to `UpgradeRuntimeLifecycleService`, and unlock-check save/notification execution delegates to `UpgradeProgressSaveService`. The lifecycle and progress-save helpers now live in dedicated source files instead of below the MonoBehaviour. | Still overloaded after the purchase, purchase-completion, runtime-effect, run-start, UI-open flow, lifecycle, and progress-save helper splits. Track remaining persistence/public-entry debt in `Docs/RefactorBacklog/UpgradeRuntimeBoundarySplit.md`. |
| Run modifiers | `RunModifierService` and its delta/snapshot/aggregation/rebuild/provider helpers live under `Assets/LeeJunMo/Script/Progression/RunModifiers/`. It aggregates upgrade and affection contributors for grave, chest, shop, and boss reward modifiers. Reward-facing consumers use `RunRewardModifierSnapshot`, aggregation traversal/calculation runs through `RunModifierAggregationService`, upgrade node loading/merge policy runs through `RunModifierUpgradeNodeProvider`, and rebuild execution orchestration runs through `RunModifierRebuildService`. | System-wide progression/run modifier owner, not upgrade-only. The current singleton/public API surface remains as the compatibility facade. |
| Merchant policy | `MerchantNPC` combines `ShopDefinitionSO`, `RunModifierService.ShopModifiers`, `MerchantShopPolicy`, and `MerchantRunStateService`. | Boundary is acceptable. Continue treating stock as run/session state and shop definition as authored policy. |
| Boss dialogue sequence | `BossEncounterDirector` is the current encounter dialogue/camera/combat-start path; `BossTalkManager` remains a legacy bridge with similar sequence responsibilities. | Watch as legacy/current bridge. Do not create a separate backlog entry until prefab migration or duplicate sequence behavior becomes an active task. |

## Extension Entry Points

- Add dialogue behavior through Dialogue Core and Dialogue UI buckets.
- Add NPC actions through NPC Feature Core and feature-specific folders.
- Add upgrade behavior through Upgrade Feature and Upgrade Effects, keeping authored data in ScriptableObjects.
- Add affection reward behavior through Affection Effects and additive modifier patterns.

## Known Pitfalls

- Upgrade UI previously failed when opened before dialogue blocker release; check `Docs/ErrorLog.md`.
- Merchant stock is session/run scoped and should preserve existing state when modifiers change.
- `UpgradeManager` is still a refactor hotspot because singleton lifecycle, persistence root adoption, purchase callback wiring, public data-change/UI events, and public UI entry points meet in one MonoBehaviour. Purchase transaction policy has been split to `UpgradePurchaseService`, purchase success ordering has been split to `UpgradePurchaseCompletionService`, runtime effect handoff has been split to `UpgradeRuntimeEffectService`, run-start eligibility/timing has been split to `UpgradeRunStartEffectPolicy`, upgrade UI open fade/input-blocker execution has been split to `UpgradeUiOpenFlow`, lifecycle subscription/run-start guard state has been split to `UpgradeRuntimeLifecycleService`, and unlock-check save/notification execution has been split to `UpgradeProgressSaveService`. The current remaining debt is public/persistent owner shape, not same-file helper placement.
- `RunModifierService` should not be treated as an upgrade-only service when reviewing loot, boss rewards, graves, chests, or merchant policy; reward consumers should prefer `RunRewardModifierSnapshot` over direct individual modifier property reads.
- `RunModifierService`, `RunModifierDeltas`, `RunRewardModifierSnapshot`, `RunModifierAggregationService`, `RunModifierUpgradeNodeProvider`, and `RunModifierRebuildService` live in the dedicated progression/run modifier layer.
- Boss dialogue has both current and legacy sequence drivers; check scene/prefab references before removing either path.
- ScriptableObject changes need asset migration/reference review.

## Refactor Candidates

- `Docs/RefactorBacklog/UpgradeRuntimeBoundarySplit.md` records the partial split of upgrade purchase transaction policy, purchase success ordering, runtime effect handoff, run-start eligibility/timing, UI open flow execution, lifecycle subscription state, and progress save/notification execution, plus the remaining persistence/public-entry debt.
- `Docs/RefactorBacklog/RunModifierAggregationBoundarySplit.md` records the resolved move from Upgrade-owned service semantics to a progression/run modifier aggregator with Upgrade and Affection as contributors.

## Promotion Candidate

Stable dialogue rules already live in `Docs/Architecture/DialogueArchitecture.md`. Keep feature-specific structure here until a rule is stable enough to promote.
