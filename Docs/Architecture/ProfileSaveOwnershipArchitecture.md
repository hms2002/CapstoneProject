---
status: active
authority: source-of-truth
category: architecture
last_reviewed: 2026-06-09
---

# Profile Save Ownership Architecture

This document defines durable profile save ownership. Runtime scene-transfer state still belongs to [Runtime Save Architecture](./RuntimeSaveArchitecture.md), and run/session lifecycle ownership still belongs to [Scene Domain Bootstrap Architecture](./SceneDomainBootstrapArchitecture.md).

## Core Rule

Durable save data must be written only from a ready source of truth.

If a manager exists but is not ready, `SaveData()` must preserve the existing DTO field instead of overwriting it with empty or partial runtime state.

## Commit Timing Model

| Data Kind | Commit Timing | Rule |
| --- | --- | --- |
| Hub meta progress | Immediate save. | The hub owner may write durable profile data when the action completes. |
| Run rewards and deltas | Run end commit. | Runtime pending state is accumulated during the run and committed through run-end policy. |
| Runtime transfer state | Scene transition capture/restore. | Not durable profile ownership by default. |
| Derived data | Recalculate from durable source. | Do not persist derived runtime snapshots as primary source unless explicitly designed. |

## Current Ownership Table

| Save Field | Source Of Truth | Commit Timing | Overwrite Guard | Notes |
| --- | --- | --- | --- | --- |
| `itemData.unlockedWeaponIDs` | `ItemManager` unlocked weapon set | Hub immediate / save request | `ItemManager.Instance != null && ItemManager.Instance.IsReady` | If `ItemManager` exists but is not ready, preserve existing `Data.itemData.unlockedWeaponIDs`. |
| `itemData.unlockedRelicIDs` | `ItemManager` unlocked relic set | Hub immediate / save request | `ItemManager.Instance != null && ItemManager.Instance.IsReady` | Same readiness guard as weapons. |
| `currencyData` / magic stone fields | `CurrencyManager` durable balance plus run pending commit | Hub immediate or run end | Manager/source must be initialized | Run deltas should commit at run end, not during active-run quit skip. |
| `affectionData` | `AffectionManager` and run affection pending commit | Hub immediate or run end | Owner must know current profile/run state | Do not mix active-run pending affection with durable hub state outside commit policy. |
| `mapData` / shortcuts | `ShortcutProgressService` and run commit policy | Run end for run shortcut unlocks | Commit policy must own mutation | `GamePlayDataManager` is not durable source of truth. |
| `upgradeData` | `UpgradeProgressService` / upgrade purchase path | Immediate after purchase | Purchase path must complete and rollback on failure | `RunModifierService` derives modifiers from upgrade purchase data. |
| `knownTotalUpgradeCount` | `UpgradeManager.GetAllUpgrades()` | Save request | `UpgradeManager.Instance != null` and list available | Diagnostic/progress metadata; do not let it drive upgrade ownership. |
| `bossDialogueData` | Boss dialogue progress store | Event completion / save request | DTO list normalized before save | Preserve records when dialogue service is absent. |
| `runSpecialNpcData` | Run-special NPC progress store | Run event / run end according to feature | DTO list normalized before save | Construction records must be initialized before write. |
| `tutorialData` | Tutorial progress store | Event completion / save request | DTO normalized before save | Normalize before repository save. |

## SaveData Guard Policy

`GameDataManager.SaveData()` is a collector and persistence boundary. It should not calculate gameplay state.

When collecting from runtime managers:

- Ensure the root `GameData` and nested DTOs exist.
- Pull from a manager only if that manager is ready for the specific data it owns.
- If a manager is absent or not ready, keep the existing DTO value.
- Do not use an empty manager collection as proof that durable save data should be cleared.
- Do not commit active-run pending state during quit or transition paths that explicitly skip active-run save.

## P0 Follow-Up

The current planned code follow-up is:

```text
GameDataManager.SaveData()
- ItemManager absent: preserve existing Data.itemData unlock lists.
- ItemManager present but !IsReady: preserve existing Data.itemData unlock lists.
- ItemManager ready: copy ItemManager unlock lists into Data.itemData.
```

This is intentionally not implemented by this documentation slice.

## Refactor Rules

- Do not rename save fields without an explicit migration plan.
- Do not delete save fields without compatibility handling.
- Do not change the meaning of existing save fields in a refactor-only slice.
- Additive fields are lower risk, but still need source-of-truth and commit timing.
- When a manager has `IsReady` or an equivalent readiness condition, save collection must respect it.

## Related Documents

- [Runtime Save Architecture](./RuntimeSaveArchitecture.md)
- [Scene Domain Bootstrap Architecture](./SceneDomainBootstrapArchitecture.md)
- [Scene Runtime Save Structure](../StructureMemory/ScriptSystems/SceneRuntimeSaveStructure.md)
- [Run Modifier Aggregation Boundary Split](../RefactorBacklog/RunModifierAggregationBoundarySplit.md)
