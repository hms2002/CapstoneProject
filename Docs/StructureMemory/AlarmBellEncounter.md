# Alarm Bell Encounter

This is a reconstruction aid, not an Architecture or Contracts source of truth.

## Authoring And Runtime Flow

- `Assets/_Project/Data/Dungeon/MapEvents/AlarmBell/AlarmBellEncounterDefinition.asset` owns recognition tiers, waves, monster entries and completion EXP. Dragon, Shadow and Slime event rooms share it through `AlarmBellEventModule.prefab`.
- `AlarmBellMonsterEntry` resolves a stage-dependent monster set, falling back to its explicit prefab. Per-entry options control count, non-EXP drop suppression, additional HP, additional attack speed and sprite tint.
- `AlarmBellInteractable` owns interaction eligibility, encounter holds, wave progression, spawned-monster tracking and completion. It does not own pending level rewards or reward UI state.
- `MonsterSpawner` / `SceneMonsterSpawnDirector` apply normal difficulty and stage scaling first. The bell then calls `ApplySpawnedMonsterOptions` once for each returned instance. The direct-instantiation fallback uses the same options path.

## Completion Chest

- `AlarmBellEventModule.prefab` now contains one initially inactive nested instance of the existing `Items/Chests/TreasureChest.prefab`, at the bell's position. The prior box image was only a placeholder bell visual, not a loot container.
- The bell's `completionChest` reference selects this authored reward, and `bellVisualRoot` selects the old visual. Completing all waves disables the bell collider, hides the bell visual and enables the separate reward root/collider. Chest loot generation, opening, UI and remaining inventory stay owned by TreasureChest/ChestInteractable; the bell does not generate or reroll loot.
- A completed run event reveals the reward again on reconstruction. DungeonRoomBuilder's existing nested TreasureChest capture/restore preserves opened state and remaining loot under the module's stable object state ID. Re-enabling never creates another chest or resets its inventory.
- An unset completionChest preserves the old no-chest behavior for other custom modules. Use a separate child/root from the bell and keep its GameObject inactive in the prefab. The bell resolves its own animator from bellVisualRoot, not the nested chest animator.
- Minimap content tracking sees no chest before completion, then observes the newly enabled real chest. Its normal opened/disabled state notifications update the badge without bell-specific map coupling.

## Speed Adjustment

- `additionalAttackSpeedMultiplier` defaults to 1 for neutral behavior. Nonpositive, NaN and infinite values resolve to 1. Current authored entries use 1.5.
- The bell multiplies the instance's already-scaled `AttackSpeedBase` through `AttributeSet.TrySetBaseValue`. `DamageProfile` stat bindings identify the attribute, with the existing attribute-name fallback for legacy setups.
- `AbilityAttackSpeedResolver` / `CombatTimingService` consume the resulting stat for existing eligible warning, recovery, interval and cooldown slots. Global slot enablement and minimum-duration limits still apply.
- This does not rewrite shared prefabs, AD/AL timing, movement speed, damage or presentation lifetime. Missing attributes safely skip the adjustment.
- The post-spawn options path is a one-shot setup, not an idempotent refresh API. Reusing it would compound HP/speed multipliers. Explicitly reapplying normal difficulty later can overwrite these base-value options; that is not part of the normal bell spawn flow.

## Verification Entry Points

- `Assets/_Project/Tests/PlayMode/Procedural/AlarmBellMonsterScalingPlayModeTests.cs`: instance-only speed multiplication, unchanged other stats/shared defaults, neutral options and missing-component handling.
- `Assets/_Project/Tests/PlayMode/Procedural/AlarmBellRewardIndependencePlayModeTests.cs`: pending rewards do not prevent interaction, wave advancement, unlock or completion EXP.
- Current validation status and task-specific changes live in `Docs/SessionLogs/2026-09-11.md`. Unity Play Mode execution is separate from compilation.

No Architecture/Contracts promotion is proposed for this local extension.
