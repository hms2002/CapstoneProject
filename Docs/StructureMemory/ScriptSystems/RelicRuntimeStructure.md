---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-09-14
---

# Relic Runtime Structure

## Purpose

Fast context map for runtime relic work. Source-of-truth rules still live in `Docs/Contracts/` and `Docs/Architecture/`; this file records the current implementation shape so future relic additions can start from the right extension point.

## Current Structure

- `RelicDefinition` assets hold player-facing relic identity: `relicId`, display name, icon, rarity, short description, level/drop settings, and a `RelicLogic` reference.
- `RelicLogic` assets own runtime behavior and tooltip body generation through `BuildTooltip(...)`.
- `ItemDatabase.asset` lists available relic definitions in both `allRelics` and `defaultUnlockedRelics` for default drop/unlock availability.
- Common always-on stat relics use `RelicLogic_StatModifiers`.
- Critical-hit movement stacking uses the existing `RelicLogic_MoveSpeedStackOnCriticalHit_Managed`.
- Event-timed stat buffs use `RelicLogic_TimedStatOnGameplayEvent_Managed`.
- `RelicLogic_MoveSpeedOnKill_Managed` registers separate token-owned temporary
  modifiers for its attack and movement attributes. `회전하는 철구` uses both
  at +10% for four seconds, while the movement proc alone owns its status HUD.
- Health-threshold stat buffs use `RelicLogic_StatWhileHealthRatio_Managed`.
- Timed event buffs can use `valueByLevel`; an empty list preserves the scalar
  `value` fallback for existing assets.
- Burn modifier assets may optionally register a deferred non-burning-target
  starter through `RelicProcManager`. `BurnSourceRuntime` owns the token-scoped
  minimum Fire value used only by burn ticks, so it does not increase native
  weapon Fire damage.
- `RelicLogic_FeatherOrbit_Managed.damageCoefByLevel` and
  `RelicLogic_CritFromBonusMoveSpeed_Managed.critPerStepByLevel` provide explicit
  level curves; when the lists are empty, their original scalar fields remain the
  compatibility fallback.

## Key Files

- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Relics/RelicDefinition.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Relics/Runtime/RelicLogic.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Relics/Runtime/RelicLogic_StatModifiers.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Relics/Runtime/RelicLogic_TimedStatOnGameplayEvent_Managed.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Relics/Runtime/RelicLogic_StatWhileHealthRatio_Managed.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Relics/Runtime/RelicLogic_MoveSpeedStackOnCriticalHit_Managed.cs`
- `Assets/HeoMinSeok/_Project/Data/Items/Relics/Definitions/`
- `Assets/HeoMinSeok/_Project/Data/Items/Relics/Logics/`
- `Assets/LeeJunMo/Datas/Looting/ItemDatabase.asset`

## Ownership And Lifecycle

- Equip and restore paths call the relic logic with a `RelicContext`; modifier sources should be scoped to `ctx.token` unless a temporary buff needs an independent runtime token.
- Unequip must remove all permanent modifiers sourced by `ctx.token` and unregister any proc objects registered through `RelicProcManager`.
- Event-driven relics should use `RelicProcManager` instead of adding new long-lived managers or scene objects.
- Burn starters queue hit targets and evaluate them on the manager tick. This lets
  a weapon's native post-hit burn application win first; a target that is already
  burning must not receive starter stacks.
- UI and tooltip views should project `RelicLogic.BuildTooltip(...)`; gameplay state stays in the logic/proc layer.

## Extension Points

- Use `RelicLogic_StatModifiers` for simple permanent attribute changes.
- Use `RelicLogic_TimedStatOnGameplayEvent_Managed` when an existing `GameplayTag` event should apply a temporary stat buff after validating instigator/target ownership.
- Use `RelicLogic_StatWhileHealthRatio_Managed` for stat modifiers that stay active while current health ratio is inside a serialized range.
- Create a new `RelicLogic` only when the behavior needs state, event payload data, or runtime ownership that the shared logic cannot express.

## Known Pitfalls

- Max-health modifiers change maximum health only; they do not heal current health unless a separate effect explicitly does so.
- `KillConfirmed` currently should not be used for critical-kill effects because the event payload does not carry critical-hit context.
- Boss-specific relics need a reliable boss identity and damage calculation path before they can be implemented safely.
- New `.cs` files may not be included in the generated `.csproj` until Unity refreshes project files; in that case command-line MSBuild does not cover them.
- New ScriptableObject logic and YAML assets require Unity import/compile validation before final gameplay confidence.
- A relic definition with `maxLevel > 1` must either consume `ctx.level` in runtime
  logic or provide an explicit level table; otherwise upgrades can silently have no
  gameplay effect even when the inventory level increases.
- Manually generated Unity YAML should serialize empty lists inline as `field: []`; a split `field:` then `[]` line can make later fields deserialize as defaults.

## Promotion Candidate

If more relics are added before demo lock, the reusable logic selection rules above may be worth promoting into a dedicated content-authoring guide or contract section.


### Weapon-exclusive relic gates (2026-09-16)

- Six independent RelicDefinition IDs live in WeaponExclusiveRelics: LightningSpear.ThirdStrike / TargetedRain, OddIron.Magazine, CrimsonBoundary.KillShot / LavaBall, ApprenticeHeroSword.ChargeLink. Each is independently registered in ItemDatabase allRelics/defaultUnlockedRelics. Numbered weapon effects are separate acquisitions.
- WeaponExclusiveRelics reads the owner's RelicInventory; weapon ability/runtime code owns execution. RelicLogic_WeaponExclusive supplies definition.description to the existing detail UI, plus the magazine's level as remaining reload count. It introduces no runtime hooks or modifiers.
- OddIron.Magazine has maxLevel 3, dropLevel 1. RelicInventory.TryConsumeLevel uses the existing level-change/removal lifecycle. WeaponAbilitySelector only chooses Shot/Barrage when a reload is possible; successful ability execution consumes a level and refills OddIronRuntimeData before firing. HUD/selection never reloads. Existing relic and ammo persistence remain the state owners.
- Other five definitions have maxLevel 1. Initial names describe effects and icons reuse their respective weapon artwork; these are authoring placeholders.
- Extension points: the six definitions, shared RL_WeaponExclusive strategy, and the corresponding weapon execution code. Avoid coupling the two spear or two Crimson gates. No Architecture/Contracts promotion requested.
