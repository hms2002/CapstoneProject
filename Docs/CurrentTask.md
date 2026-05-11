---
status: active
authority: current-task
category: workflow
last_reviewed: 2026-05-12
---

# Current Task

## Goal

Tune Slime Queen phase 2 boss prefabs after wiring P2Short pattern 2 toxic rush.

## References

- `AGENTS.md`
- `Docs/README.md`
- `Docs/Architecture/BossEncounterArchitecture.md`
- `Docs/Architecture/CombatArchitecture.md`
- `Docs/Contracts/PresentationAuthoringContract.md`
- Notion: `https://www.notion.so/1-357285ea36cd807caf47fbefa7be26c9`

## In Scope

- Read the P2Short pattern 2 toxic rush poison floor requirement from the Notion spec.
- Reuse existing toxic floor / hazard infrastructure if the project already has one.
- Use the existing reusable poison cloud object as the toxic rush trail payload.
- Add the GAS AbilityLogic and AD/AL assets for P2Short toxic rush.
- Register the toxic rush pattern on `SlimeQueenP2Short.prefab`.
- Tune `SlimeQueenP2Short.prefab` and `SlimeQueenP2Long.prefab` prefab scale.
- Keep sprite art and VFX polish swappable by the developer later.

## Out of Scope

- Final sprite, animation, and VFX polish.
- Reworking global hazard or combat damage architecture.
- Reworking phase 2 joint patterns.

## Done Criteria

- P2Short pattern 2 aims at the player, shows a rush warning, rushes repeatedly, and leaves poison cloud trail objects.
- The toxic rush values are designer-tunable from the P2Short prefab.
- `SlimeQueenP2Short.prefab` includes the new pattern entry.
- `SlimeQueenP2Short.prefab` and `SlimeQueenP2Long.prefab` use the requested phase 2 boss scale.
- Verification result is reported, including Unity compile status if available.
