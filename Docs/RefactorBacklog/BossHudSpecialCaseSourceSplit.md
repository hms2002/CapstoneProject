---
status: superseded
authority: refactor-backlog
category: refactor-item
last_reviewed: 2026-05-15
---

# Boss HUD Special-Case Source Split

## Status

superseded by one-slot-per-boss HUD registration

## Current Problem

`BossHudController` previously contained common HUD orchestration and a concrete Slime Queen phase-two exception.

The original pressure points were:

- `BossHudController` stores `SlimeQueenP2Short` and `SlimeQueenP2Long` references.
- It finds those concrete types directly when refreshing the HUD.
- It owns the Slime Queen dual boss display name and phase-two dual health behavior.

Slime Queen phase two is still a valid special case because the boss is split into separate runtime bodies. The structural issue was that the common HUD controller knew the endpoint boss types.

Current shape:

- `BossHudController` owns explicit boss registrations.
- Each registered boss receives one `BossHudSlotView`.
- `BossHealthBarUI` and `BossGroggyBarUI` render one boss channel only.
- Slime Queen phase two Short/Long bodies appear as separate HUD slots instead of one dual-channel HUD.

## Why It Exists

- Slime Queen phase two needed a quick way to display two body health ratios as one boss HUD.
- `BossHudController` already had scene-load binding and health/groggy view orchestration, so adding the special case there was the shortest path.
- Existing `IBossSplitHealthPresentation` helps with split labels on a single bound boss, but it does not model a phase made from multiple boss objects.

## Superseded Target Shape

The previous target was a generic source/snapshot adapter layer that projected normal bosses and Slime Queen phase-two bodies into one HUD view. That target was replaced by the simpler slot registration model:

```text
BossHudController
-> BossControllerBase
-> BossHudSlotView
-> BossHealthBarUI / BossGroggyBarUI
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

No dual groggy authoring follow-up remains. Future HUD work should improve `BossHudSlotView` and the slot container layout rather than reintroducing dual health/groggy bar rendering.
