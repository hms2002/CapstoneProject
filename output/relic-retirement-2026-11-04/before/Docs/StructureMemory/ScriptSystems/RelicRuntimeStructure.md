---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-10-12
---

# Relic Runtime Structure

## Purpose

Fast context map for runtime relic work. Source-of-truth rules still live in `Docs/Contracts/` and `Docs/Architecture/`; this file records the current implementation shape so future relic additions can start from the right extension point.

## Current Structure

- `RelicDefinition` assets hold player-facing relic identity: `relicId`, display name, icon, rarity, short description, level/drop settings, and a `RelicLogic` reference.
- `RelicLogic` assets own runtime behavior and tooltip body generation through `BuildTooltip(...)`.
- `ItemDatabase.asset` lists available relic definitions in both `allRelics` and `defaultUnlockedRelics` for default drop/unlock availability.
- Common always-on stat relics use `RelicLogic_StatModifiers`.
- Authored Attack bonuses use `ModifierType.Flat` on `AttackAddAttribute`, which
  enters `(AttackBase + AttackAdd) * AttackMul`. Worn/Master Whetstone use 1/2/3;
  Heavy Bracelet uses 2/3/4; Last Breath grants 3 at Health <= 50%; Red Contract
  grants 2 for four seconds after a kill. Master's basic-attack damage percentage
  and Heavy Bracelet's movement-speed penalty keep their existing separate entries.
- Critical-hit movement stacking uses the existing `RelicLogic_MoveSpeedStackOnCriticalHit_Managed`.
- Event-timed stat buffs use `RelicLogic_TimedStatOnGameplayEvent_Managed`.
- `RelicLogic_MoveSpeedOnKill_Managed` registers separate token-owned temporary
  modifiers for its attack and movement attributes. `회전하는 철구` grants AttackAdd
  +2 and movement speed +10% for four seconds, refreshing without stacking.
  The serialized `attackPercentBonus` name remains for compatibility but stores
  a flat value. `MoveSpeedOnKillProc` receives Flat only for the attack branch;
  its optional modifier type defaults to Percent for existing movement callers.
  The movement proc alone owns its status HUD; disposal removes each buff source.
- Health-threshold stat buffs use `RelicLogic_StatWhileHealthRatio_Managed`.
- Timed event buffs can use `valueByLevel`; an empty list preserves the scalar
  `value` fallback for existing assets.
- Burn modifier assets may optionally register a deferred non-burning-target
  starter through `RelicProcManager`. `BurnSourceRuntime` owns the token-scoped
  minimum Fire value used only by burn ticks, so it does not increase native
  weapon Fire damage.
- Feather Orbit uses fixed base damage 3/4/5/6/7 across its five levels.
  `RelicLogic_FeatherOrbit_Managed` keeps serialized `damageCoef` and
  `damageCoefByLevel` names but now stores fixed damage; `attackStatId` remains
  as a compatibility field and no longer influences `ComputeHpDamage()`.
  Orbit speed, per-target cooldown, prefab/effect references and token cleanup
  retain their existing paths. The `{damage_coef}` tooltip token now formats a
  number, while its existing code key remains
  `code.reliclogic_featherorbit_managed.cef8ff8aec`.
- Conditional/kill/feather tooltip templates retain `effectTemplate` and their
  `asset.<strategy GUID>.effectTemplate` identities. Last Breath and Red Contract
  keep their existing bindings; iron ball and feather templates use the same
  asset-key route through `GameTextAssetKeys.g.cs`, with values from runtime logic.
- Firm Step, Tonic Gombangdae, and Portable Brazier use dedicated hit-confirm
  procs. Firm Step accumulates critical chance after non-critical hits and clears
  it on a critical hit; Tonic Gombangdae refreshes a three-second critical-chance
  modifier on critical hits; Portable Brazier adds Burn only when a critical hit
  lands on an already-burning target and owns a global proc cooldown.
- Gold Vein and Growth Sprout share `RelicLogic_KillRewardMultiplier` and the
  player-owned `RelicKillRewardMultiplierRuntime`. Their exact 10/20/30/40/50%
  curves apply only at ordinary-enemy and normal boss reward sources. Fractional
  bonus remainder is token-owned and serialized through the existing relic runtime
  state bridge so small EXP drops preserve the authored rate across scene travel.
- Flying Boots uses `RelicLogic_DashCooldownMultiplier` to register an
  AbilitySystem scoped-duration multiplier that matches only the global
  `AD_Dash`. Its 10/20/30% reductions become 0.9/0.8/0.7 duration multipliers;
  the token-owned proc handle restores on scene attach and disposes on unequip.
- Last Stand uses `RelicLogic_LastStandCritical` to watch current Health against
  the level-specific absolute thresholds 1/2/3. While active it adds 1.0 to
  `CritChanceAdd`, which resolves to 100% through the current shared critical
  formula, and owns the matching relic-status HUD handle. Healing above the
  threshold, death, unequip, and proc disposal remove both modifier and status.

## Key Files

### Electric conditional relics (2026-10-12)

- `RelicLogic_ElectricAttributeBonus_Managed.cs` handles uncapped scene-wide shocked-enemy count, uncapped maximum-health cells and accepted movement-ability stacks. Persistent mode values remain0/1/2/3; legacy movement fields, triggerDashAbility and unused nearbyRadius are retained for compatibility. maxUnits now limits only the legacy movement conversion.
- `RD_ThunderHeart` grants ElectricAdd1/1/2/2/3 per living, active electrocuted enemy across five levels, without an enemy-count cap. The uncapped `Enemy.CountLivingWithTagInScene(scene, tag)` overload reuses the existing registry, includes distant enemies in that scene, and excludes dead/inactive/disabled enemies and enemies in other additive or persistent scenes. The proc polls at0.1 seconds and changes its modifier only when the bonus changes; radius/maxUnits no longer affects this mode.
- `RD_StormStep` retains its filename, GUID, and `Relic.Electric.StormStep` identity and displays as `생명의 전류`. It counts every full maximum-health cell at healthStep1 without a gameplay cap, granting1/1/2/2/3 ElectricAdd across five levels. Maximum-health changes include current modifiers; current HP and ElectricAdd changes are irrelevant. Runtime and detached acquisition/upgrade previews share this calculation. maxUnits does not limit this mode.
- `RD_DashCoil` grants ElectricAdd1/1/2 per accepted movement-ability use for three seconds, stacking up to four. Every use replaces one timed total modifier and refreshes all stacks, even at cap. `AttributeModifier.TimeRemaining` is the authoritative lifetime; an expired reference resets the next use to one stack even when the proc manager was disabled. The nine new electric relics are Rare/dropLevel1: ThunderHeart, StormStep (`생명의 전류`), DashCoil, ThunderKnot, Bell, Shackles, Jar, Echo and Conductor. ThunderHeart and StormStep have five levels; Shackles/Jar/Echo/Conductor one; DashCoil/ThunderKnot/Bell three. Existing Common `RD_CurrentShard` now has six levels and flat ElectricAdd1/2/3/4/5/6. Definitions/strategies and stable IDs/GUIDs remain in their existing routes; database lists contain each once.
- Coil subscribes to instance `AbilityExecutionStarted`, emitted after accepted activation enters execution. Failed/blocked casts, pending queues and cancelled windup do not start a buff; interruption after accepted start does not undo the granted use. Existing `AbilityLogic_Dash2D.CompletedKey` remains unrelated to this revised proc. `AbilityDefinition.HasAbilityTag(category)` matches identity tags and parent ancestry without adding serialized schema. `Ability.Movement` is classification only, not a caster state grant. The initial four authored members are AD_Dash, AD_ApprenticeHeroSwordSkill2_DashStab, AD_LightningSpearSkill1 and AD_RW_Skill1_Rush. Basic-attack lunges and Bloom enhancement are not initial members; future reposition skills opt in through abilityTags.
- Equipment token owns the proc. Dash modifier uses a separate proc-owned temporary token so ordinary acquisition previews preserve an active buff. Upgrading that relic removes its temporary source only from detached preview attributes, matching actual reapply cleanup. Count/stat conditions recompute on attach; temporary dash bonus is not restored across scenes.
- `RelicProcManager` destroys all registered procs on owner/manager destruction. Each proc removes modifiers/subscriptions and destroys its temporary token. Disable/reenable retains the existing equipment state.
- `RelicInventory.ProjectAcquisitions` recomputes `MaxHealth` and legacy movement-derived electric bonuses in its second conditional pass, after health-based conditions. The health relic reads detached attributes with the same cell calculation as runtime. The UI remains a projection and never starts a dash buff.
- `RD_ThunderKnot` uses `RelicLogic_ChainShockExtension_Managed` to register an owner-filtered, disposable `ElectricChainDurationBonuses` hook in Core. Each follower's chain hit adds 0.5/1/1.5 seconds to existing electrocution, capped at the status effect's authored duration (currently four seconds). Initial targets receive no relic extension; expired, missing, or death-cleared effects are not recreated, and a remaining duration already above the cap is left intact. Its `electrocutedStatusEffect` reference keeps the tooltip cap aligned with the status data. Unequip, level reapply, and manager destruction dispose the registration.
- `공명의 종` uses `RelicLogic_ElectricChainTargets_Managed` and owner-filtered `ElectricChainTargetBonuses` registration to add 1/2/3 targets. The authored baseline remains three total targets including the initial target; that relic's three levels permit four/five/six total targets. The existing visited-root rule still prevents hitting the same target twice within one discharge.
- `저릿한 족쇄` uses `RelicLogic_ShockParalysis_Managed` and `ElectricShockApplications` registration. After its owner's direct initial discharge successfully applies or renews shock, it requests a fixed 0.1-second local paralysis on a living, active normal enemy. Bosses retain `ICombatHitPauseImmune`. Follower damage, remaining-time extension, and saved-status restoration do not emit this application hook. The equipment token owns future-trigger registration; the target's `CombatHitPause2D` owns the brief pause already applied.
- Target paralysis preserves its current FSM state, AbilitySpec/token, coroutine position, movement progress, and hit records. Its actor clock excludes only paralysis intervals; repeat requests refresh the expiration without adding durations, full UI pause retains the remaining game-time pause, and older overlapping hit stun still governs final animation restoration. Owner-bound timers/telegraphs/attack windows resume from remaining progress; existing detached effects and launched projectiles continue independently. Death clears paralysis before existing death cleanup, and disable ends the owner-bound execution rather than resuming it later. The detailed actor boundary is mapped in [Boss And Mob Encounter Structure](BossAndMobEncounterStructure.md#electric-paralysis-and-local-monster-action-time-2026-10-11).
- `전류 항아리` uses `RelicLogic_ElectricExpiryDamage_Managed`. Its token-owned listener filters `GameplayEffectRunner.NaturallyExpired` by exact shock asset and applying owner. It reads that owner's current ElectricFinal and sends E×0.4 through the configured electric damage effect, with no flat addition or discharge modifiers. Explicit remove/cleanse/death/scene cleanup and attach/detach do not emit natural expiry. Dead/inactive/missing actors are skipped. Restore only reconnects the listener; a restored shock can expire normally later if its instigator is supplied.
- `GameplayEffectRunner.TickEffects` queues natural expirations through existing end cleanup, finishes repository iteration, then clears the pending list before notifying listeners. Expiry damage may kill a target and clear other effects without invalidating the tick collection. The runner owns effect lifetime; the relic owns its event subscription and disposes it on unequip/reapply/manager destruction.
- `도체 고리` and `메아리 코일` use their existing managed logic and owner-filtered `ElectricDischargeRelicBonuses`. Conductor multiplies resolved discharge damage by1/1.2/1.4/1.6/1.6… in each burst. Echo records only positive follower submissions and repeats them once at50%, skipping dead/inactive/missing targets; the initial target never receives Echo damage. It keeps the saved initial origin and follower points for replay VFX. `IChainReplayPresentation.PlayReplay` supplements the original interface; ElectricChainRibbonVfx renders the same links/scale/rhythm but starts hit snaps at follower index1. No-chain bursts have no Echo damage/VFX. No new search/stat calculation/status renewal/duration extension/paralysis occurs. `ApplySecondaryElectricDamage` remains the shared damage-only route.
- `ElectricRelicAuthoring.ApplyBatch` authors data/coefficient references; `LocalizeBatch` imports the reviewed30-key electric content subset. `Tools/Localization/expand_electric_relics.py` maintains their CSV, inventory and additive asset-name lookup entries. Authored runtime tooltip templates use `relic.electric.scene/health/dash/extension/targets/paralysis/expiry/echo/conductor.template`; the movement template remains for the compatibility mode.
- Player-facing text omits scene/activation terminology. Jar's death/cleanse exclusion sentence is removed from UI only; its filters remain. `ElectricRelicDescriptionReadback.ExportBatch` loads the actual Korean table and uses DisplayName/GameText.Asset/BuildTooltip/DetailTextFormatter to export ten relics across29 authored levels. The native readback writes only to Library; the reviewed source subset remains30 keys and all lookup identities stay stable.
- The corresponding `GE_ElectricShockTrigger` snapshots ElectricFinal, duration bonuses, target-count bonuses and Conductor/Echo rules before initial status callbacks, then calculates base 5 + E×1 for initial / base 5 + E×0.5 for followers before existing discharge modifiers and Conductor's multiplier. Baseline chains preserve remaining shock time, with `ThunderKnot` adding bounded remaining time after follower damage without reapplying the status. The source-owned registration handles are disposed on unequip/reapply and manager destruction; restoration reconnects future hooks without replaying paralysis or damage.
- Known pitfall: the game consumes maximum health as complete cells, while `AttributeValue` suppresses change notifications for absolute deltas at or below 0.001 except depletion. A sub-threshold fractional change can cross a cell boundary without immediately refreshing an attribute-watching relic; the shared notification threshold is unchanged.
- Verification boundary: this structure map describes source and authoring responsibilities. Native compile/test results belong in the current task's verification record; full authored-monster combat, resumed-hit timing, and six-relic tooltip/visual review still require their own evidence.

### Existing relic file routes

- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Relics/RelicDefinition.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Relics/Runtime/RelicLogic.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Relics/Runtime/RelicLogic_StatModifiers.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Relics/Runtime/RelicLogic_TimedStatOnGameplayEvent_Managed.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Relics/Runtime/RelicLogic_StatWhileHealthRatio_Managed.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Relics/Runtime/RelicLogic_MoveSpeedStackOnCriticalHit_Managed.cs`
- `Assets/_Project/Runtime/Features/Items/Relics/RelicLogic_CritChanceOnNonCriticalHit_Managed.cs`
- `Assets/_Project/Runtime/Features/Items/Relics/RelicLogic_CritChanceAfterCriticalHit_Managed.cs`
- `Assets/_Project/Runtime/Features/Items/Relics/RelicLogic_BurnOnCriticalHit_Managed.cs`
- `Assets/_Project/Runtime/Features/Items/Relics/RelicLogic_KillRewardMultiplier.cs`
- `Assets/_Project/Runtime/Features/Items/Relics/RelicKillRewardMultiplierRuntime.cs`
- `Assets/_Project/Runtime/Features/Items/Relics/RelicLogic_DashCooldownMultiplier.cs`
- `Assets/_Project/Runtime/Features/Items/Relics/RelicLogic_LastStandCritical.cs`
- `Assets/_Project/Runtime/Features/Items/Relics/RelicLogic_MoveSpeedOnKill_Managed.cs`
- `Assets/_Project/Runtime/Features/Items/Relics/Procs/MoveSpeedOnKillProc.cs`
- `Assets/_Project/Runtime/Features/Items/Relics/RelicLogic_FeatherOrbit_Managed.cs`
- `Assets/_Project/Runtime/Features/Items/Relics/RelicDetailCodes/FeatherOrbitRelic/FeatherOrbitController.cs`
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
- Kill-reward multipliers are queried by the two combat reward owners before
  pickup splitting. Do not move them into `CurrencyManager` or
  `RunLevelProgression`, because those lower-level sinks also receive refunds,
  gambling, workout, and scripted rewards that these relics must not modify.

## Extension Points

- Use `RelicLogic_StatModifiers` for simple permanent attribute changes.
- Use `RelicLogic_TimedStatOnGameplayEvent_Managed` when an existing `GameplayTag` event should apply a temporary stat buff after validating instigator/target ownership.
- Use `RelicLogic_StatWhileHealthRatio_Managed` for stat modifiers that stay active while current health ratio is inside a serialized range.
- Create a new `RelicLogic` only when the behavior needs state, event payload data, or runtime ownership that the shared logic cannot express.
- Ability-specific cooldown relics should use
  `AbilitySystem.AddScopedCooldownDurationMultiplier` with exact definition
  identity and retain/dispose its handle through token-owned relic lifecycle.
- Absolute-health conditional relics should subscribe through a token-owned
  proc, recalculate immediately on attach, and keep gameplay modifier and status
  handle in the same activate/deactivate lifecycle.

## Known Pitfalls

- Max-health modifiers change maximum health only; they do not heal current health unless a separate effect explicitly does so.
- `KillConfirmed` currently should not be used for critical-kill effects because the event payload does not carry critical-hit context.
- Boss-specific relics need a reliable boss identity and damage calculation path before they can be implemented safely.
- New `.cs` files may not be included in the generated `.csproj` until Unity refreshes project files; in that case command-line MSBuild does not cover them.
- New ScriptableObject logic and YAML assets require Unity import/compile validation before final gameplay confidence.
- Last Stand guarantees critical hits only for damage paths that already opt into
  the shared critical formula. Burn and other damage-over-time sources that
  explicitly disallow critical hits remain unchanged.
- A relic definition with `maxLevel > 1` must either consume `ctx.level` in runtime
  logic or provide an explicit level table; otherwise upgrades can silently have no
  gameplay effect even when the inventory level increases.
- Manually generated Unity YAML should serialize empty lists inline as `field: []`; a split `field:` then `[]` line can make later fields deserialize as defaults.
- Apply percentage bonuses to the reward total, not each pickup. Small integer
  rewards need fractional carry or their effective percentage will be distorted.

## Promotion Candidate

If more relics are added before demo lock, the reusable logic selection rules above may be worth promoting into a dedicated content-authoring guide or contract section.


### Weapon-exclusive relic gates (2026-09-16)

- Six independent RelicDefinition IDs live in WeaponExclusiveRelics: LightningSpear.ThirdStrike / TargetedRain, OddIron.Magazine, CrimsonBoundary.KillShot / LavaBall, ApprenticeHeroSword.ChargeLink. Each is independently registered in ItemDatabase allRelics/defaultUnlockedRelics. Numbered weapon effects are separate acquisitions.
- WeaponExclusiveRelics reads the owner's RelicInventory; weapon ability/runtime code owns execution. RelicLogic_WeaponExclusive supplies definition.description to the existing detail UI, plus the magazine's level as remaining reload count. It introduces no runtime hooks or modifiers.
- OddIron.Magazine has maxLevel 3, dropLevel 1. RelicInventory.TryConsumeLevel uses the existing level-change/removal lifecycle. WeaponAbilitySelector chooses Shot for both Attack and barrage inputs when a reload is possible. Either empty-magazine input starts OddIronReloadExecutor through the runner: it waits for AL_OddIronShot's tracked reload sound to finish, then consumes one relic level and refills OddIronRuntimeData without firing. Cancellation preserves ammo/relic state. The reload activation ends without firing; a later input with loaded ammo can shoot or barrage. The barrage AL never refills ammo itself. HUD/selection never reloads. Existing relic and ammo persistence remain the state owners.
- Other five definitions have maxLevel 1. Initial names describe effects and icons reuse their respective weapon artwork; these are authoring placeholders.
- Extension points: the six definitions, shared RL_WeaponExclusive strategy, and the corresponding weapon execution code. Avoid coupling the two spear or two Crimson gates. No Architecture/Contracts promotion requested.
