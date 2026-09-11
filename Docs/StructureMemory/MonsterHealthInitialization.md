---
status: active
authority: structure-memory
category: feature-map
last_reviewed: 2026-09-11
---

# Monster Health Initialization

## Ownership

- `AttributeSet` applies definition defaults, the base init profile, and override init profiles once.
- Initial HP values for the common-corridor and slime monsters live in `Data/Attributes/InitProfiles/Enemies/Mobs/`, not their AI classes.
- The seven common-corridor monster scripts no longer expose a separate serialized `maxHealth`.
- Slime subclasses apply their name and scale via `ApplyAppearance/SetAppearance`. Neither Awake nor `InitSplit` resets health.
- `MonsterDifficultyReceiver` caches original max HP and reapplies spawn modifiers from that baseline. Repeated difficulty application is not cumulative.
- It captures current HP before changing MaxHealth, so shrinking max HP (including tank x0.5) preserves the original health ratio rather than scaling an already-clamped value again.
- `MonsterSpawner` applies the defeated-boss progression multiplier. `StageMonsterSetSO` adds role-specific multipliers; alarm-bell elite scaling remains a later, separate operation.

## Full Health On Spawn

The seven common-corridor, five slime, and three Shadow/Servant/StrangeCandlestick prefabs enable the Health-to-MaxHealth link's `fillToMaxOnInitialize`.

This is important because profile entries can write Health before their larger MaxHealth. The intermediate clamp can otherwise leave an authored 450/450 monster at 100/450. The existing fill option runs after all profiles and restores full authored HP without changing global AttributeSet initialization semantics.

This fill is initialization-only. It does not run every frame or heal an already-initialized slime during split setup. The default and other prefabs are not globally changed; deliberately injured spawns can keep it disabled and require ordered/profile-aware initialization.

## Extension And Verification

- Change HP in the monster's override init profile. Keep the initial Health and MaxHealth values consistent for normal full-health spawns.
- New full-health monster prefabs need the Health/MaxHealth link with fill enabled.
- CommonMonsterAuthoringGenerator connects the matching override init profile when creating a new common prefab. Regenerating an existing prefab preserves its authored overrides and enables the existing full-health link; no legacy maxHealth callback remains.
- Do not reintroduce numeric health writes into AI Awake/Start or presentation/split setup.
- Split children still instantiate their own prefab/profile; this change does not introduce inheritance of parent stage or elite modifiers.
- `Tests/PlayMode/Procedural/MonsterHealthAndWeaponSkillBalancePlayModeTests.cs` checks 15 real prefabs, repeated stage/role scaling, preservation of injured HP during slime split setup, and new/existing authoring profile behavior.

## Key Files

- `Runtime/Core/Attributes/AttributeSet.cs`
- `Runtime/Features/Monsters/Spawning/MonsterDifficultyReceiver.cs`
- `Runtime/Features/Monsters/Spawning/SceneMonsterSpawnDirector.cs`
- `Runtime/Features/Monsters/Common/CommonCorridor/`
- `Runtime/Features/Monsters/Slime/Slime.cs`

This is a feature map, not a new Architecture or Contracts policy.
