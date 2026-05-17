---
status: active
authority: current-task
category: workflow
last_reviewed: 2026-05-18
---

# Current Task

## Goal

Refactor the pit / hole trap runtime structure while preserving current scene and prefab references.

## References

- `AGENTS.md`
- `Docs/README.md`
- `Docs/Architecture/CombatArchitecture.md`
- `Docs/Architecture/GameplayDebuffApplicationArchitecture.md`
- `Docs/Contracts/PresentationAuthoringContract.md`

## In Scope

- Keep existing `HoleTrap` component name and serialized fields compatible with current scenes.
- Split pit trigger detection from fall execution responsibilities.
- Introduce a small fall context object/value and runtime execution path.
- Move pit fall target position resolution out of `GameplayCue_Falling` as the primary owner.
- Keep `GameplayCue_Falling` as a reusable cue that consumes target/causer context and restores runtime state.
- Preserve existing GAS falling effect, cue, hazard damage, respawn, and dash-ignore behavior.

## Out of Scope

- Changing Unity scenes, prefabs, layers, animator controllers, or authored UI references manually.
- Renaming serialized fields on existing scene-bound components.
- Replacing the current GAS effect/cue asset setup.
- Adding new Managers, Singletons, or `DontDestroyOnLoad` objects.
- Adding monster pit-fall behavior unless required to keep the new structure compiling.

## Done Criteria

- `HoleTrap` delegates fall execution instead of owning the full coroutine workflow directly.
- Pit fall execution has an explicit context containing target, trap, timing, damage, respawn, and fall-center data.
- `GameplayCue_Falling` prefers an explicit world fall position when one is provided, with legacy Tilemap fallback kept for compatibility.
- Hazard damage still goes through `HazardDamageAction`.
- Existing scene references to `HoleTrap` remain compatible.
- Verification result is reported, including Unity compile/build status if available.
