---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-11-04
---

# Relic Runtime Structure

## Purpose

Fast context map for runtime relic work. Source-of-truth rules still live in `Docs/Contracts/` and `Docs/Architecture/`; this file records the current implementation shape so future relic additions can start from the right extension point.

## Current Structure

- `RelicDefinition` assets hold player-facing relic identity: `relicId`, display name, icon, rarity, short description, level/drop settings, and a `RelicLogic` reference.
- `RelicLogic` assets own runtime behavior and tooltip body generation through `BuildTooltip(...)`.
- `ItemDatabase.asset.allRelics` retains registered definitions for ID lookup, including retired content. `defaultUnlockedRelics` supplies initial unlocks; loot eligibility is filtered separately.
- Melta affection level 2 unlocks `RD_SlimeBottle`; Chloe affection level 2 unlocks `RD_ChloeLantern`, `RD_ShadowEcho`, and `RD_ShadowWisp` through their existing UnlockItemAffectionEffect assets. All four are absent from defaults and registered in allRelics; reward updates do not change their definitions or the database. `ItemManager.RestoreEarnedAffectionUnlocks` adds every missing referenced entitlement from committed profile affection on initialization and unlock-state/list queries. It waits for item/NPC database readiness, retries on later queries, adds through existing validated UnlockWeapon/UnlockRelic methods, and never clears old unlocks, calls other reward effects or opens UI. Existing ready-manager SaveData collection persists the resulting sets without a new save schema or forced save. [Scope](../../ActiveTasks/melta-chloe-tier-two-unlocks.md).
- Spiri affection level 2 unlocks `RD_ScorchingSong` (Fire Dragon's Fang), `RD_SunFragment`, and `RD_DrunkenRush` through `SpiriRelicUnlockReward`, an existing `UnlockItemAffectionEffect` instance. These three remain registered in `allRelics` but are absent from `defaultUnlockedRelics`. `ItemManager` merges saved unlock IDs with defaults, so old saved unlocks remain valid; fresh profiles earn them at the threshold. Unlocks affect subsequent eligible chest/shop rolls, not already generated rewards. See [scope and compatibility](../../ActiveTasks/spiri-tier-two-relic-unlocks.md).
- `RelicDefinition.IsRetiredId` is the shared, nonserialized predicate for the seven relics excluded from acquisition and the encyclopedia on 2026-11-04. Inventory restore and existing effects do not consult it, so previously owned relics retain their identity/level/behavior. Source boundaries and the seven IDs are mapped in [LootRewardStructure](LootRewardStructure.md#retired-relics-2026-11-04).
- Common always-on stat relics use `RelicLogic_StatModifiers`.
- `그림자 잔향` (`RD_ShadowEcho`, 전설/maximum level 1) uses
  `RelicLogic_ShadowEcho`, `ShadowEchoController`, and `ShadowEchoActor` under
  `Assets/_Project/Runtime/Features/Items/Relics/`. The controller subscribes only
  to the owner's actual execution lifecycle for the exact global `AD_Dash` asset.
  It snapshots the departure position/body sprite, flip, world transform and
  AttackFinal at execution start, then confirms the actor only when the dash ends
  normally with `Dash2D.Completed=1`. A cancelled/invalid dash removes its actor;
  if dash execution outlasts the configured delay, damage waits for confirmation.
  `PlayerAnimatorController2D.PrimaryVisualRenderer` is a read-only view of the
  existing authored body-renderer reference, avoiding weapons/shadows during capture.
  The detached actor owns a 0.7-second warning and one area submission, followed by
  a 0.35-second visual fade.
  `RelicLogic_ShadowEcho.ExplosionSound` selects `sound_goblinGunner_GunShot`;
  the actor requests it once at confirmed detonation at the fixed echo position,
  including when the blast has no targets. Cancelled echoes do not play it.
  Its static dashed-ring and burst-disc meshes use the
  same radius 1.5 / Y scale 0.7 as `TopDownEllipseHitUtility2D.ContainsCollider`.
  This is a full ellipse, not a wall-clipped area. Living enemy hurtboxes are
  queried at detonation and deduplicated by root. CombatHitPayload carries the
  creation-time AttackFinal, critical=false and empty elemental arrays through
  the shared combat route; it adds no custom knockback/stagger or weapon scaling.
  `PF_ShadowEcho.prefab` contains the frozen-player SpriteRenderer, dashed range,
  filled/ring burst renderers and two authored ParticleSystems. Runtime creates
  only instances of that prefab; gameplay elapsed time drives display/fade.
  There is no runtime UI hierarchy, sprite, mesh, material or ParticleSystem
  construction. MaterialPropertyBlock allocation occurs in Awake.
  The attachment token owns live actors and event subscriptions. Unequip/reapply,
  restore-detach, owner disable/death/destruction and scene teardown cancel them.
  Re-enable/restoration reconnect future dash hooks without replaying an echo;
  pending actors and timers are not saved. The effect is independent of weapons.
  `ShadowEchoAuthoring.Apply` authors three static meshes in `Art/Meshes`,
  `Art/Materials/VFX/M_ShadowEcho.mat`, the prefab, and the definition/strategy.
  `RD_ShadowEcho` is registered only in `ItemDatabase.allRelics`; reward unlock
  wiring is separate. Its icon uses the supplied `Icon_ShadowEcho.png` under
  `Art/Sprites/Items/RelicIcon`, copied unchanged with Point/Single import settings.
  `ApplyIcon` updates only that binding; the full authoring entry point uses the
  same icon default. `Localize` imports three keys
  for the name, description and formatted effect in five locales.
- `그림자 혼불` (`RD_ShadowWisp`, 전설/maximum level 1) uses
  `RelicLogic_ShadowWisp` and owner-local `ShadowWispController`, with a token-owned
  `IRelicProc` registered in the existing `RelicProcManager`. Owner kill events
  spawn one shot at the killed enemy's projectile aim point. The nearest living
  other enemy is selected by distance from that origin among camera-visible
  hurtboxes with an unblocked wall ray; no eligible target means no shot. Repeated
  kill notifications for the same still-present victim are coalesced.
  The shot is the existing `PF_ChloeLanternProjectile` / `ChloeLanternProjectile2D`
  with AttackFinal captured at creation, no critical roll, and explicit empty
  elemental damage/build-up arrays. Speed multiplier 1.4, lifetime 5 seconds,
  homing 1080 degrees/second, size/animation and contact policy match the lantern.
  Its payload causer is the actual spawned projectile. During the synchronous
  kill event the controller recognizes that projectile in its owned list and
  suppresses only its own re-trigger. Lantern/weapon kills remain eligible and
  other relics still receive ordinary hit/kill events.
  The token owns all launched shots. Unequip/reapply/restore-detach/owner disable,
  death and destruction clear them; restore reattaches without replaying kills.
  Targets, death-coalescing state and in-flight shots are not persisted.
  `ShadowWispAuthoring.Apply` authors
  `Assets/_Project/Data/Items/Relics/Definitions/RD_ShadowWisp.asset` and
  `RL_ShadowWisp.asset`, registering only in `ItemDatabase.allRelics`. Affection
  reward wiring is separate. The icon uses the supplied purple flame PNG at
  `Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_ShadowWisp.png`, imported with
  Point filtering and no pixel changes. The authoring default references that
  same icon. Projectile art still shares the lantern prefab; a future wisp-only
  projectile art swap needs a separate authored prefab reference on this strategy
  so the lantern remains intact. `Localize` imports the three-key content CSV subset.
- `병 속의 슬라임` (`RD_SlimeBottle`, 전설/maximum level 1) uses
  `RelicLogic_SlimeBottle` and the existing `RelicProcManager`. Its token-owned
  proc registers a player-filtered callback in Core `CombatHitPrevention` and
  disposes it on unequip/reapply/restore-detach/manager destruction. Restore
  reconnects once without replaying a block. Disabled/destroyed/dead owners and
  missing tokens cannot block; no chance/counter is saved.
  `CombatDamageAction` checks prevention after invulnerability/evasion and before
  damage effects, shield consumption, knockback, stagger, elemental build-up and
  hit events. `HazardDamageAction` checks the same gate independently of
  `ignoreEvasion`, so player pit-fall damage is eligible. The proc rolls 10%
  independently for each eligible positive-damage application; it does not modify
  EvasionFinal or create a temporary invulnerability window.
  Ground fire/poison-cloud hazard applications remain eligible even when they
  repeat while the player stays inside. Status damage-over-time is excluded via
  the optional `isDamageOverTime` argument; `BurnStatus2D` marks its tick explicitly.
  Future status tick callers using either entry point must carry that meaning
  rather than classifying all repeated ground hazards as status DoT. Existing
  status effects that apply damage outside these entry points do not use the gate.
  A blocked fall retains its existing fall animation/safe-position recovery.
  Successful prevention sends literal `Slime!` through the existing popup backend.
  UI resolves the player's text color to #74DBAF and projects an `ElasticPop`
  runtime view flag into a damped squash/stretch. Pooled text resets that flag;
  ordinary damage and Evade/EVADE keep their existing style. No new popup prefab,
  hierarchy, serialized schema or persistent enum value is needed.
  `SlimeBottleAuthoring.Apply` authors the definition/strategy/icon and registers
  `Assets/_Project/Data/Items/Relics/Definitions/RD_SlimeBottle.asset` in
  `ItemDatabase.allRelics` only. Melta affection unlock wiring is a separate task
  and should reference this exact ID. `Localize` imports its three-key CSV subset;
  the requested `Slime!` combat label is intentionally literal across locales.
- `클로에의 등불` (`RD_ChloeLantern`, 전설/maximum level 1) uses
  `RelicLogic_ChloeLantern`, `ChloeLanternOrbitController`, and
  `ChloeLanternProjectile2D` under `Assets/_Project/Runtime/Features/Items/Relics/`.
  The definition is registered in `ItemDatabase.allRelics` only. Chloe affection
  level 1 unlock/reward wiring belongs to a separate task; use this exact ID and
  `Assets/_Project/Data/Items/Relics/Definitions/RD_ChloeLantern.asset` there.
  The controller instantiates authored summon/projectile prefabs, orbits one
  summon at the Sun Fragment radius/center/angular speed, and scans camera-visible
  living Enemy hurtboxes every two game-time seconds. It selects by distance from
  the player, requiring clear wall rays from the player and the summon. A summon
  separated from its owner by a wall cannot fire.
  `ChloeLanternOrbitController.IsDialogueBlockingCombat` reads the existing
  `DialoguePlayback.IsPlaying` and the owner's `PlayerInteractor2D.Talking` state.
  The latter spans boss camera lead-in, dialogue and camera return. While blocked,
  the summon keeps orbiting, owned shots are deactivated/destroyed, and the fire
  timer resets without detaching the relic token. After release, a full normal
  fire interval elapses before the next shot. `ChloeLanternProjectile2D` also
  checks this gate in movement and both trigger callbacks, discarding without
  damage even if physics runs before the controller's Update. This contact guard
  also applies to Shadow Wisp shots because they share the projectile type.
  `RelicLogic_ChloeLantern.ShotSound` selects `sound_goblinGunner_GunShot`;
  the orbit controller requests it once after each launch at the summon position.
  The shared projectile has no launch-audio hook, so Shadow Wisp shots do not
  acquire this sound just by sharing the projectile prefab.
  Launch snapshots AttackFinal directly into CombatHitPayload with critical=false
  and explicit empty element damage/build-up arrays; the shared combat application
  still owns protection, shields, outgoing modifiers and hit events. No weapon
  damage-category scaling or critical formula is invoked by this relic.
  The dedicated projectile steers up to 1080 degrees/second without a forward-cone
  limit and sweeps its collider radius before moving. Its first living enemy or
  wall contact consumes it once, including protected enemies. A lost/dead target
  leaves it flying straight until contact or its five-second lifetime expires.
  The attachment token owns the summon and outstanding shots: unequip/reapply,
  owner disable/death/destruction and scene teardown clear both. Restore attaches
  a fresh controller state; angle, cooldown and in-flight shots are not saved.
  `ChloeLanternAuthoring.Apply` is the Unity authoring entry point; its `Localize`
  method imports the prepared three-key CSV subset. Source PNGs remain unchanged;
  the projectile sheet is eight 32x32 frames with a looping 12 fps authored clip.
- Authored Attack bonuses use `ModifierType.Flat` on `AttackAddAttribute`, which
  enters `(AttackBase + AttackAdd) * AttackMul`. Worn/Master Whetstone use 1/2/3;
  Heavy Bracelet uses 2/3/4; Last Breath grants 3 at Health <= 50%; Red Contract
  grants 2 for four seconds after a kill. Master's basic-attack damage percentage
  and Heavy Bracelet's movement-speed penalty keep their existing separate entries.
- Critical-hit movement stacking uses the existing `RelicLogic_MoveSpeedStackOnCriticalHit_Managed`.
- Event-timed stat buffs use `RelicLogic_TimedStatOnGameplayEvent_Managed`.
- `RelicLogic_MoveSpeedOnKill_Managed` registers separate token-owned temporary
  modifiers for its attack and movement attributes. `광전사의 보석` (`RD_RedContract`)
  uses the existing `Relic Logic_Move Speed On Kill_Managed` strategy and grants AttackAdd
  +2 and movement speed +10% for four seconds, refreshing without stacking.
  `회전하는 철구` (`RD_RotatingIornBall`) instead uses the existing
  `Relic Logic_Common_RedContract` timed-event strategy: AttackAdd +2 for four
  seconds, refreshing without stacking, with no movement bonus. The historical
  strategy filenames/identities are retained; definition bindings distinguish
  the rare combined buff from the common attack-only buff.
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
