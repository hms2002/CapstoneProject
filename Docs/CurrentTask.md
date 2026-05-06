---
status: active
authority: current-task
category: workflow
last_reviewed: 2026-05-06
---

# Current Task

## Goal

Implement the upgrade-driven Shop v2 foundation.

## References

- `AGENTS.md`
- `Docs/README.md`
- `Docs/Architecture/DialogueArchitecture.md`
- `Docs/Architecture/RuntimeSaveArchitecture.md`
- `Docs/Contracts/PresentationAuthoringContract.md`

## In Scope

- Add `ShopDefinitionSO` as the source of truth for merchant shop settings.
- Split shop availability, slot count, discount, and refresh-limit resolution into a policy layer.
- Keep merchant stock state in `GamePlayData.merchantStates` for run/session lifetime.
- Preserve existing stock and sold state when discount changes.
- Preserve existing stock when slot count expands and roll only newly opened slots.
- Add an authored-scene refresh interactable that calls merchant refresh logic.
- Keep scene and prefab references for Unity manual connection; do not edit scenes or prefabs in this pass.

## Out of Scope

- Runtime creation of shop UI hierarchy, slots, buttons, or presentation objects.
- Persistent save migration for merchant stock.
- Direct scene or prefab wiring for `ShopDefinitionSO` or refresh interactables.
- Full economy rebalance.

## Done Criteria

- Shop code compiles with `ShopDefinitionSO`-driven settings.
- Missing shop definitions disable the shop and log a warning.
- Upgrade modifier changes can immediately refresh shop availability, visible slot count, and prices.
- Refresh interactable can call `MerchantNPC.TryRefreshStock()` without runtime UI creation.
- Documentation logs record the current task update and implementation outcome.
- Verification result is reported, including Unity compile status if available.
