---
status: proposed
authority: refactor-backlog
category: elemental-status-migration
last_reviewed: 2026-08-19
---

# Element Gauge To Stack Status Migration

## Current Problem

Legacy Fire/Blood/Poison/Electric effects use threshold-and-decay gauges, while the new Burn validation path uses explicit integer stacks and timed consumption. Both models currently coexist by design.

## Why It Exists

The designer intends to retire the gauge model later, but the user explicitly restricted this implementation to a separate Burn path and requires separate approval before replacement work.

## Target Shape

After approval, define the shared stack-status rules for each element, migrate attacks and monster UI, then remove legacy buildup only after authored content and save/runtime compatibility are checked.

## Risks

- Existing attacks rely on attacker-wide `ElementOffenseSource` buildup.
- Monster prefabs and boss flows may carry gauge installers and serialized element definitions.
- Trigger/sustain VFX, damage formulas, UI projection, and persistent enum/GUID references can break if removed in one step.

## Refactor Trigger

Explicit user approval of the element-gauge replacement plan and its migration scope.

## Related Documents

- `Docs/StructureMemory/ScriptSystems/BurnStatusAndCrimsonBoundaryWeapon.md`
- `Docs/StructureMemory/ScriptSystems/WeaponAndGASStructure.md`
- `Docs/DecisionLog.md`

## Status

`proposed` — no legacy gauge code or assets were changed in the Burn/Crimson Boundary slice.
