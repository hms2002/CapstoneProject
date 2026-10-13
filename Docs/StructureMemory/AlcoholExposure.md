---
status: active
authority: structure-memory
category: alcohol-exposure
last_verified: 2026-10-12
---

# Alcohol exposure and Spiri reward

## Purpose and source of truth

Current navigation map for the recipient-owned alcohol mug and affection reward.
Technical authority remains [Gameplay Buff/Debuff Architecture](../Architecture/GameplayBuffDebuffArchitecture.md)
and [Presentation Authoring Contract](../Contracts/PresentationAuthoringContract.md).

## Current structure and key files

- [AlcoholPuddleArea](../../Assets/_Project/Runtime/Features/Map/Puddles/AlcoholPuddleArea.cs)
  tracks valid hurtbox colliders and notifies one recipient per puddle. Stay
  callbacks support landing inside a puddle; mode changes and disable release
  all contacts. Projectile damage and ignition/absorption remain on the puddle.
- [PlayerAlcoholExposure](../../Assets/_Project/Runtime/Features/Player/Status/PlayerAlcoholExposure.cs)
  owns charge, the overlap-source list, overflow latch, drain and effect pulses.
  Three seconds fills the mug; strictly exceeding three seconds activates the
  effect. Last-source exit drains the current quantity over 1.5 seconds.
- [PlayerIntentInput2D](../../Assets/_Project/Runtime/Features/Player/Input/PlayerIntentInput2D.cs)
  projects confusion through `ApplyConfusionToDirection` for ordinary walking
  and [directional dash](../../Assets/_Project/Runtime/Features/Player/Abilities/AbilityLogic_Dash2D.cs).
  Both cached raw intent and direct-query dash fallback use that projection.
  Raw input, other ability input, forced movement and no-input aim fallback
  retain their existing direction semantics.
- [AlcoholExposurePlayerVisual](../../Assets/_Project/Runtime/Presentation/Map/Puddles/AlcoholExposurePlayerVisual.cs)
  projects exposure onto the player prefab's authored mug/drop renderers. The
  [mug shader](../../Assets/_Project/Art/Shaders/AlcoholMugFill.shader) changes the
  liquid interior without changing Drink.png or generating runtime objects.
- [SpiriAlcoholRewardAuthoring](../../Assets/_Project/Editor/Tools/Data/SpiriAlcoholRewardAuthoring.cs)
  registers confusion GE/GameplayTag/HUD, player references and the level-1
  information reward on DragonBossNpc (ID2001). Four text fields use the existing
  GameText asset-key mapping and five locales.

## Ownership and lifecycle

- One exposure owner per player combines all valid ground sources. Reentry
  continues remaining charge; an already-earned residual speed buff refreshes
  during reentry even before the next overflow.
- Current Spiri affection >=1 selects the existing speed GE (+25% movement,
  attack-speed additive +0.4); below1 selects the confusion status GE. Current
  runtime affinity and profile/pending run state are queried through the existing
  stores; no additional unlock state is saved.
- Effect source is the exposure component. CombatBuffDebuffApplier and the
  recipient's PlayerStatusRuntime project GE results onto the status HUD.
- Contact/drain refresh at0.2s; the speed GE asset itself has duration5 so its
  attribute modifiers and GE end together. Emptying refreshes speed for the final
  five seconds or removes confusion immediately. Timings are quantized to the
  gameplay frame that crosses the boundary.
- Disable/destroy removes owned effects and clears charge. Source mode changes,
  disable and destroyed colliders release contact. The visual restores authored
  pose and hides renderers on disable. Charge/drain/sway follow scaled time.
- PlayerRuntimeCaptureCoordinator excludes only the exposure's exact owned GE
  and granted explicit tag snapshots. Scene-local charge is not saved; a new
  recipient starts from contact. Other tags/effects retain their snapshot path.

## Entry points and pitfalls

- `EnterPuddle`/`ExitPuddle` are gameplay contact entry points; visual properties
  are read-only projections. New effect definitions remain authored references.
- Confusion uses `State.Confusion` and a duration-hidden HUD definition: its
  renewable five-second GE timer is not the actual drain-controlled lifetime.
  CombatBuffDebuffApplier respects authored duration defaults.
- DurationMultiModifierEffect uses its asset duration for attributes. A spec-only
  duration override would not extend the speed modifiers; retain synchronized
  five-second authoring and verify both clocks after timing changes.
- GE-granted tags also enter explicit counts. Saving both paths could restore
  confusion twice and leave one count after effect removal. Exposure-specific
  capture filtering covers both paths without altering save DTOs or hiding the
  residual speed buff's HUD timer.
- The old puddle buff definition/interval/duration fields are hidden compatibility
  data, not active settings. See [migration debt](../RefactorBacklog/AlcoholPuddleLegacyAuthoring.md).

## Recent structure and recovery

The immediate per-puddle speed buff has been replaced by recipient exposure.
Use the focused AlcoholExposure, AlcoholPuddleContact and AlcoholExposurePlayerVisual
PlayMode suites, plus the existing alcohol arrow and overlap-HUD regressions.
Unity6000.4.2f1 native selected38 unique cases passed, including actual modifier
expiry, scene capture, scaled motion/pause and destroyed authored children. The
[evidence report](../../outputs/spiri-alcohol-exposure-2026-10-12/README.md) includes
the native six-state mug render. Actual full-scene manual combat remains untested.
Editor and test generated-project MSBuild also passed after native execution.
The later requested directional-dash extension updates fifteen cardinal/diagonal
input cases; its separate validation is recorded in the SessionLog. Earlier
native results are not evidence that these new dash expectations were executed.
Re-running the narrow authoring menu is idempotent; inspect its diff and do not
rewrite existing localized entries to resolve unrelated authoring conflicts.

## Promotion candidate

This map is not a new architecture/contract. Promotion is unnecessary for this
single feature; reconsider only if other exposure mechanics share the flow.
