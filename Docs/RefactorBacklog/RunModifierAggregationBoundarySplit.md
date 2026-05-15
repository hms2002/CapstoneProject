---
status: resolved
authority: refactor-backlog
category: refactor-candidate
last_reviewed: 2026-05-16
---

# Run Modifier Aggregation Boundary Split

## Current Problem

`RunModifierService` used to live under the Upgrade feature folder even though it acted as a project-wide run modifier aggregator.

It currently aggregates or exposes modifiers for:

- grave rewards
- chest rewards
- merchant shop availability, slots, discounts, and refresh count
- boss reward modifiers
- upgrade effect contributors
- affection reward contributors

The service and helper files now live under `Assets/LeeJunMo/Script/Progression/RunModifiers/`, matching the broader progression/run reward policy responsibility.

## Why It Exists

Upgrade effects were the first major source of run modifiers, so the modifier service was placed with the upgrade feature. Later, affection and reward systems also needed additive modifiers. Keeping one service avoided duplicate modifier calculation, but it made Upgrade look like the owner of systems it only contributes to.

## Target Shape

Treat run modifiers as a progression/run aggregation boundary:

- Upgrade effects contribute modifier deltas.
- Affection effects contribute modifier deltas.
- Loot, grave, chest, merchant, and boss reward systems consume resolved modifier snapshots.
- The aggregator owns rebuild/reload semantics and contributor ordering.
- Upgrade-specific code no longer appears to own boss reward, grave, chest, or merchant policy.

The public behavior remains source-compatible: `RunModifierService` is still the compatibility singleton facade, while aggregation/rebuild/provider helpers live in the progression run modifier layer.

## Risks

- `RunModifierService` is a `DontDestroyOnLoad` singleton.
- Consumers currently call `RunModifierService.Instance` directly.
- Moving files or changing class names can affect serialized references and scene/prefab wiring.
- Boss rewards depend on additive modifier behavior recorded in `Docs/DecisionLog.md`.
- Merchant stock must preserve existing run/session state when modifier values change.

## Refactor Trigger

Start this refactor when one of these becomes true:

- another system adds a new run modifier source
- loot, reward, grave, chest, merchant, or boss policy work needs to change modifier resolution
- boss reward modifiers expand beyond the current affection/upgrade sources
- the service needs to move out of the Upgrade folder
- direct `RunModifierService.Instance` consumers make modifier ownership hard to reason about

## Related Documents

- `Docs/StructureMemory/ScriptSystems/DialogueNpcAffectionStructure.md`
- `Docs/StructureMemory/ScriptSystems/LootRewardStructure.md`
- `Docs/StructureMemory/ScriptSystems/SceneRuntimeSaveStructure.md`
- `Docs/DecisionLog.md`
- `Docs/RefactorBacklog/UpgradeRuntimeBoundarySplit.md`

## Status

`resolved`

First implementation slice complete:

- Added same-file `RunRewardModifierSnapshot` in `RunModifierService.cs`.
- Added `RewardSnapshot` and `CurrentRewardSnapshot` paths while preserving `GraveModifiers`, `ChestModifiers`, `ShopModifiers`, `BossModifiers`, and `BossRewardModifiers` compatibility properties.
- Routed reward-facing consumers in chest loot, chest reward policy, merchant policy/activation, boss reward context construction, and runtime debug display through the snapshot path.

Second implementation slice complete:

- Added same-file `RunModifierAggregationRequest`, `RunModifierAggregationResult`, and `RunModifierAggregationService` helper types in `RunModifierService.cs`.
- Moved purchased-upgrade and affection reward traversal/calculation out of the `RunModifierService` MonoBehaviour methods and into the helper.
- Kept `RunModifierService` responsible for singleton lifecycle, `DontDestroyOnLoad`, lazy save loading, upgrade node caching, event notification, and public compatibility APIs.

Third implementation slice complete:

- Added same-file `RunModifierUpgradeNodeLoadRequest`, `RunModifierUpgradeNodeLoadResult`, and `RunModifierUpgradeNodeProvider` helper types in `RunModifierService.cs`.
- Moved `UpgradeManager.Instance.GetAllUpgrades()` plus `Resources.LoadAll<UpgradeNodeSO>(...)` merge policy out of `RunModifierService.LoadUpgradeNodes()` and into the provider helper.
- Preserved cached-node reuse, UpgradeManager-first node priority, resource fallback path, and public snapshot/rebuild behavior.

Fourth implementation slice complete:

- Added same-file `RunModifierRebuildRequest`, `RunModifierRebuildResult`, and `RunModifierRebuildService` helper types in `RunModifierService.cs`.
- Moved rebuild execution orchestration out of `RunModifierService.EnsureLoadedFromPurchases()` and into the helper:
  - upgrade-node load decision
  - cached node input/output
  - aggregation helper invocation
  - resolved modifier result return
- Kept `ReloadFromSave()` and `RebuildFromPurchasedUpgrades()` as public compatibility APIs while routing their shared invalidation/rebuild/event path through `RefreshFromSources()`.
- Preserved lazy load guard behavior, event notification timing, cached node reset behavior on explicit refresh, and reward snapshot compatibility.

Final ownership slice complete:

- Moved `RunModifierService.cs`, `RunModifierDeltas.cs`, `RunRewardModifierSnapshot.cs`, `RunModifierAggregationService.cs`, `RunModifierRebuildService.cs`, and `RunModifierUpgradeNodeProvider.cs` out of the Upgrade feature folder and into `Assets/LeeJunMo/Script/Progression/RunModifiers/`.
- Moved matching `.meta` files with the source files so Unity GUIDs are preserved.
- Kept the `RunModifierService` class name, singleton facade, `DontDestroyOnLoad` bootstrap, lazy-load guard, public refresh compatibility APIs, event notification, contributor ordering, and reward snapshot behavior unchanged.
- Treat the remaining singleton/public API surface as the current compatibility facade, not as Upgrade ownership. If a future task needs a different lifecycle contract or non-singleton injection boundary, scope it as a new focused backlog item.
