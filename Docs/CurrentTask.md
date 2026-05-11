---
status: active
authority: current-task
category: workflow
last_reviewed: 2026-05-08
---

# Current Task

## Goal

Implement the Lightning Spear V1 weapon runtime and authoring assets: mark creation, mark-targeted Q rush, unmarked Q sweep, E mark rain, core mark feedback hooks, reusable weapon prefabs, ScriptableObject wiring, and ItemDatabase registration.

## References

- `AGENTS.md`
- `Docs/README.md`
- `Docs/Architecture/GameplayAbilityWeaponArchitecture.md`
- `Docs/Architecture/CombatArchitecture.md`
- `Docs/Contracts/WeaponCleanupContract.md`
- `Docs/Contracts/PresentationAuthoringContract.md`

## In Scope

- Add Lightning Spear `WeaponAbilityLoadout`, selection strategy, runtime state, runtime data, mark actor, and ability logic scripts.
- Implement `Q/Skill1` as mark-targeted rush when a valid cursor-near mark exists, otherwise as a no-movement forward sweep.
- Implement `E/Skill2` as constrained random Lightning Spear mark generation with delayed activation and weak landing damage support.
- Enforce hard/soft blocker policy for mark rush target validation.
- Provide serialized prefab/reference hooks for marks, sweep/rush hitboxes, range indicators, and mark feedback.
- Register the new weapon type in runtime factories and editor validation where required.
- Create V1 placeholder ScriptableObject assets and prefabs for `WD_LightningSpear`, `WAL_LightningSpear`, `WAS_LightningSpear`, ability definitions/logics, mark prefab, and equipped weapon prefab.
- Register `WD_LightningSpear` in `ItemDatabase.asset` as an available/default unlocked weapon for prototype testing.

## Out of Scope

- Manually changing Unity scenes, animator controllers, or layer assignments.
- Creating the final authored VFX/SFX assets for the falling spear, cursor, range ring, or mark highlights.
- Implementing upgrade/relic effects such as 3-hit slam mark creation.
- Adding new Managers, Singletons, or `DontDestroyOnLoad` objects.
- Renaming serialized fields or changing existing weapon behavior outside required factory/editor registration.

## Done Criteria

- Lightning Spear loadout can expose Attack, Q/Skill1, and E/Skill2 abilities through the existing weapon ability selection path.
- Q selects the closest valid mark near the cursor, rushes to it, consumes it, and resets Q cooldown; invalid marks are ignored.
- Q falls back to a no-movement forward sweep when no valid mark exists.
- E creates up to the configured number of marks inside the current room area, or around the player when no room is found, without using invalid blocked positions.
- Marks expire by lifetime and are cleaned up on weapon swap, owner disable, or runtime state destruction.
- Combat damage flows through the existing combat payload / `CombatDamageAction` path.
- `WD_LightningSpear` has a weapon prefab, loadout, strategy, attack, Q, E, mark prefab, placeholder hitboxes, and feedback prefab references.
- `ItemDatabase.asset` includes `WD_LightningSpear` in both all weapons and default unlocked weapons.
- Verification result is reported, including Unity compile status if available.
