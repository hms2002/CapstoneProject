---
status: resolved
authority: refactor-backlog
category: refactor-item
last_reviewed: 2026-05-15
---

# Boss HUD Special-Case Source Split

## Status

resolved

## Current Problem

`BossHudController` previously contained common HUD orchestration and a concrete Slime Queen phase-two exception.

The original pressure points were:

- `BossHudController` stores `SlimeQueenP2Short` and `SlimeQueenP2Long` references.
- It finds those concrete types directly when refreshing the HUD.
- It owns the Slime Queen dual boss display name and phase-two dual health behavior.

Slime Queen phase two is still a valid special case because the boss is split into separate runtime bodies. The structural issue was that the common HUD controller knew the endpoint boss types.

Resolved shape:

- `BossHudController` reads `IBossHudSource` / `BossHudSnapshot`.
- `SingleBossHudSource` adapts a normal `BossControllerBase`.
- `SlimeQueenPhaseTwoHudSource` owns the Short/Long lookup and projects two phase-two channels.
- `BossGroggyBarUI` now supports dual groggy channels, with a runtime fallback that clones the existing single groggy slider until authored dual references are wired.

## Why It Exists

- Slime Queen phase two needed a quick way to display two body health ratios as one boss HUD.
- `BossHudController` already had scene-load binding and health/groggy view orchestration, so adding the special case there was the shortest path.
- Existing `IBossSplitHealthPresentation` helps with split labels on a single bound boss, but it does not model a phase made from multiple boss objects.

## Target Shape

- `BossHudController` reads a generic HUD source or snapshot, not concrete boss types. Done.
- A normal boss adapter/source translates one `BossControllerBase` into the common HUD snapshot. Done.
- A Slime Queen phase-two adapter/source owns references to `SlimeQueenP2Short` and `SlimeQueenP2Long` and translates them into the same snapshot. Done.
- The snapshot carries display name, visibility, groggy display state, and one or more health/groggy channels. Done.
- `BossHealthBarUI` and `BossGroggyBarUI` render supplied channels without knowing which boss produced them. Done.

Example target boundary:

```text
BossHudController
-> IBossHudSource / BossHudSnapshot
-> BossHealthBarUI

SingleBossHudSource
-> BossControllerBase

SlimeQueenPhaseTwoHudSource
-> SlimeQueenP2Short + SlimeQueenP2Long
```

## Risks

- Boss HUD references are scene/prefab-facing serialized fields.
- Slime Queen phase-two object lifetime, one-body death, both-body death, and scene-load binding must keep current behavior.
- Health bar delayed animation, dual-health mode, split labels, and groggy hiding need manual visual review.
- `BossControllerBase.BindBoss/UnbindBoss` interactions with the HUD must be preserved or replaced deliberately.

## Refactor Trigger

- Adding another split, multi-body, shared-health, or phase-replaced boss.
- Editing `BossHudController` for Slime Queen phase two.
- Reworking Boss HUD health bar presentation.
- Needing Boss HUD support for more than one health channel without adding another concrete boss branch.

## Related Documents

- `Docs/StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md`
- `Docs/StructureMemory/ScriptSystems/InventoryAndChestUIStructure.md`
- `Docs/Architecture/BossEncounterArchitecture.md`
- `Assets/HeoMinSeok/_Project/Scripts/UI/HUD/BossHudController.cs`
- `Assets/HeoMinSeok/_Project/Scripts/UI/HUD/BossHealthBarUI.cs`
- `Assets/HeoMinSeok/_Project/Scripts/UI/HUD/IBossSplitHealthPresentation.cs`
- `Assets/Script/Enemy/Boss/FSM/BossControllers/SlimeQueenBoss/SlimeQueenP2Short.cs`
- `Assets/Script/Enemy/Boss/FSM/BossControllers/SlimeQueenBoss/SlimeQueenP2Long.cs`

## Remaining Follow-up

Author dedicated dual groggy references on the active `GlobalUIRoot` / boss HUD prefab if the runtime fallback layout is not acceptable for final UI. That is an authoring follow-up, not a remaining source split blocker.
