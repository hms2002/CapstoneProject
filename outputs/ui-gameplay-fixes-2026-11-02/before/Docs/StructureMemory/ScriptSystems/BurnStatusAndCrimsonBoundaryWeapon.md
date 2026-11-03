---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-11-01
---

# Burn Status And Crimson Boundary Weapon

## Purpose

Map the first independent integer-stack elemental status and the Lean weapon used to validate it. This document is context, not a replacement for Contracts or Architecture.

## Runtime Flow

Current Skill1 is the hold/release charge described below. Historical Ignite/burn-consumption sections document the previous implementation; Q continues to use the existing BigExplosion logic.

`홍련의 한계선` ability logic reads the attacker's live `FireFinal`, applies direct damage through `CombatDamageAction`, and explicitly supplies an empty resolved element result so the legacy fire gauge is not built. The basic projectile then applies `BurnStatus2D` to the resolved damage-target root.

`BurnStatus2D` is target-owned. It stores 0–99 stacks, the most recent source system/effect/causer, and a tick accumulator. Active reapplication does not reset the accumulator. Each tick reads the source's current Fire Damage, applies the current Burn ratio, rounds final damage, emits a UI pulse, then consumes one stack.

`BurnSourceRuntime` is source-owned and lazily attached when Burn is first applied. Its default rules are one-second ticks and 50% Fire Damage. Fire relics write token-keyed modifiers for tick interval, damage ratio, application amount, first application, critical permission, and target-stack-based damage scaling. Relic removal removes only its own token.

Immediately before a Burn tick reaches `CombatDamageAction`, `BurnStatus2D` applies the source runtime's target-stack multiplier. This keeps `홍련의 왕관` separate from the base Burn coefficient and makes its cap apply to its own contribution. A source-level minimum Fire value may also provide a Burn-only floor without increasing the weapon's direct Fire damage.

## Key Files

- `Assets/_Project/Runtime/Features/Combat/Status/Burn/BurnStatus2D.cs`
- `Assets/_Project/Runtime/Features/Combat/Status/Burn/BurnSourceRuntime.cs`
- `Assets/_Project/Runtime/Core/Status/IMonsterStackStatusSource.cs`
- `Assets/_Project/Runtime/UI/Combat/StackStatus/MonsterStackStatusWorldView.cs`
- `Assets/_Project/Runtime/Features/Items/Weapons/CrimsonBoundary/`
- `Assets/_Project/Runtime/Features/Items/Relics/RelicLogic_BurnModifier_Managed.cs`
- `Assets/_Project/Runtime/Features/Items/Relics/RelicLogic_SunFragment_Managed.cs`
- `Assets/_Project/Runtime/Features/Items/Relics/SunFragmentOrbitController.cs`
- `Assets/_Project/Data/Items/Weapons/Definitions/WD_CrimsonBoundary.asset`
- `Assets/_Project/Data/Items/Relics/Definitions/RD_FireBonusRelic.asset`
- `Assets/_Project/Data/Items/Relics/Definitions/RD_MeltedBell.asset`
- `Assets/_Project/Data/Items/Relics/Definitions/RD_SmokyIncense.asset`
- `Assets/_Project/Data/Items/Relics/Definitions/RD_PortableBrazier.asset`
- `Assets/_Project/Data/Items/Relics/Definitions/RD_SunFragment.asset`
- `Assets/_Project/Data/Items/Relics/Definitions/RD_ScorchingSong.asset`
- `Assets/_Project/Data/Items/Relics/Definitions/RD_BurningFeather.asset`
- `Assets/_Project/Data/Items/Relics/Definitions/RD_CrimsonKing.asset`

## Ownership And Cleanup

- Burn gameplay state belongs to the damaged target; the UI only projects `IMonsterStackStatusSource`.
- The UI backend attaches a dormant reusable view component to the target and creates only target-child square/text presentation. Target destruction cleans the view hierarchy.
- `CrimsonBoundaryRuntimeState` belongs to the equipped weapon instance and destroys registered projectile/warning/explosion objects on disable or destroy.
- `SunFragmentOrbitController` is attached to the relic owner only while needed, owns its generated fragments, and destroys all fragments when its relic token is disabled or the owner is destroyed. The dormant controller may remain attached after unequip but owns no active gameplay state.
- Crimson weapon/attack presentation now uses authored sprite prefabs (see approved graphics section below). The separate monster stack-status UI retains its previous presentation path.

## Fire Relics

| Relic | Runtime rule | Levels |
| --- | --- | --- |
| 타오르는 심핵 | `FireAdd` +2 per level | 8 |
| 녹아내린 종 | Burn interval 0.9/0.8/0.7/0.6/0.5 seconds | 5 |
| 매캐한 향로 | Burn coefficient +10/+20/+30/+40/+50 percentage points | 5 |
| 휴대용 화로 | On a critical hit against an already-burning target, apply 1/2/3 Burn; global cooldown 0.5 seconds | 3 |
| 태양의 파편 | Every 2 seconds, generate up to 1/2/3 orbiting squares; contact deals 100% Fire Damage and applies 4/6/8 Burn | 3 |
| 작열하는 송곳 | After hit resolution, apply 2/3/4/5/6 Burn only if the target is still not burning; its Burn ticks use at least 2 Fire | 5 |
| 불타는 깃털 | Every Burn application +1/+2/+3 stacks | 3 |
| 홍련의 왕관 | Every 20/15/10 target stacks grants +5/+4/+5% Burn damage, capped at +30% | 3 |

The source table labels `홍련의 왕관` as maximum level 5 but defines only three level rows. The Lean implementation intentionally uses three levels until design supplies levels 4–5.

## Ability Semantics

- Attack / left click: cooldown 0.5 seconds, Fire Damage, Burn 3, destroy on enemy or wall. Every successful enemy or wall hit applies the same shot payload and Burn 3 to nearby targets in a diameter-2.5 top-down ellipse (Y scale 0.70), excluding the already-hit direct target, owner, dead enemies and targets blocked by WallLayers. Target roots are deduplicated and the impact is claimed before secondary damage; bursts do not recursively explode. Wall impact uses the same area damage and Burn; the impact guard prevents duplicate damage while wall line-of-sight blocks targets behind the wall. The existing Fireball_Hit prefab uses Fire_Explosion frames with an approximately 2.5 x 1.75 visible maximum footprint, remains upright, and retains Crimson runtime ownership/cleanup. Ignite relic homing shots reuse this same normal-projectile behavior.
- Skill 1 / right click: cooldown 5 seconds, consumes up to 5 Burn from every visible Burn target, and creates overlapping diameter-5 explosions. With no eligible visible target the runtime state consumes input before ability commit, so cooldown does not start.
- `한계선의 잔불 추격` / Ignite kill shot: `SpawnRelicKillShot` selects the nearest living enemy at spawn, then `CrimsonBoundaryProjectile2D` steers toward that target's current position on each attack tick, turning at up to 720 degrees/second. Movement, sprite rotation and both collision sweeps share the steered direction; speed, lifetime, damage and Burn retain the basic-projectile data. A dead, inactive, disabled or destroyed target triggers an immediate nearest-active-enemy search within radius 40 of the projectile's current position, reusing `WeaponExclusiveRelics.FindEnemies`. A valid current target is retained even if another enemy becomes closer. Empty searches leave the last heading intact and retry every 0.1 gameplay seconds while the original lifetime remains; zero-delta ticks do not search or steer. The projectile's nonserialized homing flag remains set when its target disappears, allowing retries and dead-hurtbox exclusion. `Setup` clears the flag, target and retry timer before the relic supplies a new target; normal attacks do not acquire targets. Existing weapon-transient cleanup ownership is unchanged.
- Skill 2 / Q: cooldown 12 seconds, locks cursor impact position, warns for 0.6 seconds, and consumes each target's full Burn. Its normalized skill Fire value is multiplied by 13 for base damage and by 2 per consumed stack. Both the falling explosion and Crimson Lava Ball relic path use these Skill-2-only coefficients; Skill 1 retains its separate authored burn-consumption multiplier of 6.

## Lean Presentation Sorting

| Presentation | Sorting Layer | Order in Layer |
| --- | --- | ---: |
| Equipped weapon square | `Entity` | 0 |
| Basic fireball | `Projectile` | 5 |
| Ignite explosion | `FloatingAOE` | 0 |
| Big Explosion warning | `AttackTelegraph` | 1 |
| Big Explosion impact | `FloatingAOE` | 1 |
| Burn bar background / fill / icon / number | `UI` | 0 / 1 / 2 / 3 |
| Sun Fragment square | `Projectile` | 4 |

Big Explosion reuses its warning renderer, so impact changes both color and sorting from `AttackTelegraph/1` to `FloatingAOE/1` in the impact frame.

## Known Pitfalls

- Do not route Burn attacks through unresolved element buildup; doing so also fills the legacy fire gauge.
- Burn ticks intentionally disable hit-confirm emission but keep damage popup and kill attribution.
- Sun Fragment direct hits use the ordinary hit-confirm path; the Burn applied by the fragment keeps the player owner as causer so destroying the square does not leave the status with a destroyed causer reference.
- The status UI backend must stay in UI assembly; Gameplay accesses it only through the Core playback contract.
- Do not replace or disable the legacy element gauge until the user explicitly approves that migration.

## Promotion Candidate

If additional elemental stack statuses adopt this contract, promote the shared stack-status ownership and damage-event rules into Architecture/Contracts with explicit approval.


## Approved Crimson graphics (2026-09-13)

- Original FireStaff_asset.zip supplies ten textures under Art/Sprites/Items/Weapon/CrimsonBoundary. Sheets use 32x32 frames in row-major visual order (Unity rect Y converted from top row); skill icons are 24x24. WeaponPrefab_CrimsonBoundary authors a 4-frame FireStaff child with a -45-degree orientation correction. Weapon/skill definitions bind the original icons.
- CrimsonBoundaryWeaponData owns seven CrimsonBoundaryVisual2D prefab references under Prefabs/VFX/CrimsonBoundary, plus igniteChargeSeconds (.24). Visual2D reads authored sprite frames/duration/loop, destroys finished one-shots, and registers/forgets weapon-owned transients. Projectile/meteor loop until their combat owner removes them. No sprite or presentation hierarchy is generated at runtime.
- Attack instantiates the authored Fireball prefab (kinematic body, trigger, existing projectile actor). Actor root stays scale 1; child art size is independent of the existing .32 trigger and .28 swept query. Wall sweep advances to contact centroid before Fireball_Hit. Impacts are emitted once; timeout emits no hit.
- Projectile passes Flame_Explosion into target-owned BurnStatus2D. The status retains its authored effect for subsequent ticks and existing reapplications without one, allowing burn to outlive the equipped weapon. Tick VFX are detached, self-expiring world objects; stack rules and UI pulses are unchanged.
- Ignite snapshots visible burn targets on cast, plays attached Charge_Fire, then consumes current stacks and applies all explosion damage after .24 seconds. Charge cleanup runs in finally; cancellation/weapon disable before impact consumes no stacks through this skill. Dead targets are skipped. Explosions use current target positions at impact and retain existing overlapping-area behavior.
- Q captures cursor position once, moves Lavaball down from above camera over existing .6 seconds, removes it in finally, then plays Lavaball_Hit and applies existing damage/consumption at captured point. Preview dashed range guide is not game art.
- RuntimeState clears registered combat presentation on weapon disable/destroy. Completed visuals remove their registration; dead projectile references are pruned on registration. Burn tick effects expire independently.
- Validation: new class included via external temporary MSBuild targets; ProceduralPlayModeTests project build exit 0, new type found in Gameplay.dll, sprite counts/bounds/IDs and prefab references checked. Native Unity import/render and Play Mode tests not executed.


### Burn sustain / tick frame split (2026-09-13)

- Flame_Sustain loops original Flame_Explosion frames 1–5 over .25 seconds; existing Flame_Explosion now plays frames 6–13 once over .4 seconds, preserving 20 FPS. Both retain prior visual scale.
- Crimson data/projectile pass both authored prefabs into BurnStatus2D. Status owns exactly one target-child sustain instance, also attaching when an already-capped stack receives its first visual reference. Reapplication with the same prefab does not restart the loop. Zero stacks, skill consumption to zero, disable and target destruction remove sustain. Re-enable with remaining stacks recreates it.
- Tick effect remains a separate self-expiring world effect; final-stack sustain removal does not cut off the last tick animation. Damage, stack rules and tick intervals are unchanged.


### Staff basic attack motion (2026-09-13)

- Equipped prefab follows WeaponVisualRig2D's authored WeaponVisualRoot/MirrorRoot/MotionRoot/RenderRoot hierarchy. Muzzle is a MotionRoot child; FireStaff sprite loop and art correction remain under RenderRoot. Aim/facing continue through WeaponEquipController/WeaponPresentationRig2D.
- RuntimeState samples three authored AM_Crimson clips (opening/downward/upward) on the weapon only. Opening starts at idle, alternating clips start at the previous stroke's endpoint. Existing PlayerCombatInput2D primary-hold state controls endpoint hold; release resets instantly after .18s. OnDisable resets sequence to opening.
- Motion clock uses final attack speed captured at activation and respects owner CombatHitPause2D. Basic attack coroutine fires once after .12 motion seconds from the exact authored muzzle pose; cancellation before release resets presentation. No Animator parameters, Animation Events or player root motion added. Existing AD cooldown starts on activation and remains one second.
- Unity Editor import and Play Mode visual/lifecycle validation remain required; MSBuild only covers C#.


### Staff motion source revision (2026-09-13)

- Supersedes the custom preview curves above: AM_CrimsonOpening/Downward now copy Apprentice sword AM_Swing1; AM_CrimsonUpward copies AM_Swing2. Original motion curves and timing retained. Copies remove sword Animation Events and disable looping; source sword assets remain untouched.
- Runtime samples through actual clip length (~.1833s), holds the endpoint while primary attack remains held, and otherwise snaps to idle after completion. Existing .12s release timing remains. GUIDs and prefab bindings unchanged.


### Approved reference motion / player origin revision (2026-09-13)

- Supersedes copied sword curves: AM_CrimsonOpening/Downward/Upward now use approved reference-preview hand translation and rotation (.08s anticipation, .14s endpoint), preserving GUIDs and existing rig. No trail presentation added.
- Runtime samples these clips and counts .4 seconds after both completion and primary release before snapping to idle; held input clears that timer. Hold uses game delta time without attack-speed scaling and freezes during owner hit pause.
- Projectile release remains at .12 attack-speed-scaled motion seconds, but the ability now owns spawn position directly: system.transform.position with zero offset. Runtime MarkProjectileReleased only ends release-pending state. Muzzle field and authored child removed.
- Preview repetition interval (.4s) is not the gameplay cooldown: AD_CrimsonBoundaryAttack retains its existing one-second cooldown. Native Unity visual/lifecycle checks remain outstanding.

## Boss death cleanup

BossControllerBase consumes all independent Burn stacks immediately on death entry. Existing GameplayEffectRunner cleanup does not own these stacks. BurnStatus2D rejects application to a dead boss and stops its pulse/VFX continuation if damage synchronously cleared all stacks. ConsumeAll releases the active registry entry, stack view, tick accumulator and sustain visual through the existing status owner.

### 2026-09-16 — Shared authored monster HUD supersedes independent Burn view

The former runtime-generated Burn bar/view described above is superseded for the 19 migrated monster prefabs. Burn now implements IMonsterStatusSource, registers in MonsterStatusRuntime, and is displayed as an orange icon/count below HP by an authored MonsterWorldHud prefab. Electrocuted shows the actual GE remaining seconds alongside it. Enemy death clears the common hub before subclass callbacks; disable consumes Burn rather than retaining stacks for re-enable. Existing damage rules and weapon/relic APIs remain. See `Docs/StructureMemory/MonsterWorldHudAndStatus.md` for current ownership, scope and validation limits.

### Player-build swing sampling requirement (2026-09-17)

- WeaponPrefab_CrimsonBoundary now authors an enabled Animator on its root, the same GameObject passed to SampleAnimation. It intentionally has no controller and applies no root motion; CrimsonBoundaryRuntimeState retains the manual clip clock, endpoint hold and reset ownership.
- The player log exposed the missing Animator requirement for non-Legacy clip sampling; Editor playback alone did not reveal it. Clip bindings, ability release timing and the shared basic-attack movement lock are unchanged.
- Prefab binding checks and C# build passed; native player playback acceptance remains pending. See the 2026-09-17 SessionLog.

### Crimson Boundary authored audio (2026-09-17)

- Six original clips live under `Assets/_Project/Audio/Imported/CrimsonBoundary/`; `Assets/_Project/Resources/Audio/DefaultAudioCatalog.asset` owns clip references and mix tuning. `CrimsonBoundaryWeaponData` / `ALData_CrimsonBoundary.asset` author five SoundRefs for swing, basic projectile launch, LavaBall summon/hit and Ignite explosion.
- Attack requests swing audio at BeginSwing and launch audio after the projectile is spawned at the existing .12 motion-second release. Cancellation before release skips launch audio. Normal Q plays summon at the captured ground position (the meteor starts above camera), then impact after the existing cancellation guard. Relic Q plays summon at launch and impact once at its terminal wall hit; piercing enemy hits and lifetime expiry do not play impact.
- Ignite requests one shot per actual explosion after charge/cancellation checks. The spawned explosion is the audio causer so simultaneous explosions are not collapsed by the backend's same-source suppression.
- BurnStatus2D owns the shared `status.burn.tick` request before tick damage, including lethal/final ticks. The target is the audio causer, preventing different burning monsters from suppressing one another. This applies to relic-origin Burn too; it does not require the Crimson weapon to remain equipped. Existing backend same-source 50ms suppression still applies to unusually close requests on the same monster.
- Initial catalog volumes: Burn .015, swing .065, launch/summon .07, LavaBall hit/Ignite .08. Current catalog global multiplier is 10, yielding .15/.65/.7/.8 before user mix and attenuation. Cooldown 0, pitch/speed 1, spatial one-shots. SoundManager owns playback sources and natural completion; no new AudioSource, manager, loop or cleanup owner.
- Native import, listening balance and Play Mode timing still need verification. Use Tools/Audio/Audio Catalog for mix tuning.

### Crimson audio 2D correction (2026-09-17)

- Supersedes the spatial-one-shot setting above: all six new keys use catalog `spatial: 0`. The camera AudioListener is authored at Z=-10; using 2D playback prevents camera-depth attenuation of these combat cues. Volumes and event bindings remain unchanged. User-reported silence prompted this correction; runtime audibility still requires confirmation.

### Ignite single sound and impact shake revision (2026-09-17)

- Supersedes per-explosion audio: Ignite uses `692923__dustywind__crunchy-explosion.wav` once per cast with actual explosions, after charge/cancellation checks. It also requests one `igniteExplosionShake` (.2) outside the target loop.
- LavaBall hit uses `651532__h2p34__explode-1-small.wav` for both normal landing and relic terminal wall collision, with `lavaBallHitShake` (.3). Both CameraShakeHooks are authored in CrimsonBoundaryWeaponData / ALData_CrimsonBoundary and honor the existing screen-shake setting through the Core backend.
- Existing 2D playback and catalog gains remain. No stack/area-damage/collision changes. Native audio and shake acceptance remains pending.

### Six-color charged Skill1 (2026-10-30)

- Shared charge gauge (2026-11-02): the existing HeadChargeGauge subtree now has ChargeGaugePresentation. CrimsonBoundaryChargePresentation delegates ShowCharge/Hide to it; fill/color/offset references moved intact to that component. Apprentice sword uses a separate authored ChargeGauge.prefab with the same view. User selected A later the same day: both gauges now use24×4 geometry at20PPU, offset1.4, two1px fill bands and a gold top edge/bright tip at full charge. Existing object IDs/references are retained; the old32×10 geometry is superseded. See [shared charge gauge structure](WeaponAndGASStructure.md#charging-skill-gauges-2026-11-02) and [four preview-only designs](../../../outputs/charge-gauge-2026-11-02/README.md).

- `AD_CrimsonBoundaryIgnite` retains its GUID/name but binds `AbilityLogic_CrimsonBoundaryCharge` and `ALData_CrimsonBoundaryCharge`. The previous Ignite logic/data remain available for historical/other references. Basic attack and Q bindings are unchanged.
- Charge data owns minimum/full times .7/1.4s, maximum diameter6, flight .5s, partial/full Fire multipliers3/4 and Burn10/12. `ChargeDiameter` follows the approved86/128 inner/outer artwork ratio: first half grows the inner diameter to4.03125; second half grows the outer diameter from4.03125 to6. This intentionally increases the minimum released diameter from3 to4.03125, as selected by the user. Holding beyond full does not fire. Early release cancels with no cooldown. A valid release calls `AbilitySystem.CommitDeferredCooldown`; normal GE, scoped modifiers and cooldown decrement remain central. `SkipCooldownOnEnd` prevents a second start at impact/cancellation.
- Target starts at the cursor and follows it with0.06s exponential damping during hold, capped at0.75 world-unit separation. `AbilityLogic_CrimsonBoundaryCharge.DampChargeTarget` uses gameplay delta time; rapid jumps catch up to the distance bound before easing the remaining gap. Gameplay owns this per-cast center, clamps it to the current viewport and supplies it to presentation. Valid release locks the displayed damped center for both the meteor and damage query; it does not snap to raw mouse position. Following the user's diagonal-flight correction, the visual meteor enters from the target's upper right at45 degrees and linearly reaches that position in .5s. `ResolveFlightStart` extends that diagonal past the first top/right viewport boundary with a3-unit margin; the camera-free fallback uses the same direction. The native preview uses this runtime function too. At arrival, Gameplay uses a circle query and deduplicates explicit hurtbox roots. Walls do not occlude this query. The outermost visible circle matches the new Skill1 query, unlike the older Q's ellipse helper, and retains its released size throughout flight.
- Damage uses `CrimsonBoundaryUtility` / `CombatDamageAction`, preserving crit/final multipliers and independent direct element HP channels. The stated3×/4× is base Fire scaling before that shared processing. Burn is applied after impact without consuming prior stacks. The existing Ignite kill-shot relic is dispatched for kills made by the new Skill1.
- `ICrimsonBoundaryChargePresentation` is a Gameplay contract; its `CapstonePresentation.CrimsonBoundaryChargePresentation` implementation lives in Presentation, avoiding a Gameplay→Presentation assembly reference. Gameplay owns charge/flight/impact timing and damage. The component owns only rendering, phase flipbooks and visual expiry.
- Historical gauge design before the user-selected A migration on2026-11-02: `CrimsonChargePresentation.prefab` authors all presentation objects. Its32×10 uGUI gauge preserved the approved Aseprite v2 chamfered frame, inset trough and three fill shade bands. The three horizontal `Image.fillAmount` values stay synchronized: red below50%, orange from50%, original cream/orange full palette with a golden frame at100%; there is no minimum-charge marker. The frame and fill parts use authored rectangles sharing one white pixel, not a flattened animation. The gauge sits1.3 units above the owner at16 pixels/unit. `vertexColorAlwaysGammaSpace` preserves the dark reference colors in the project's linear rendering. `ChargeWarning/InnerCircle`, `OuterCircle` and `CenterCircle` hold three transparent PNG sprites. The middle (`InnerCircle`) and center groups share a centered192px canvas and grow together through the first50%; the outer group appears just above50% at the middle diameter and expands to full size. Middle rotation is counterclockwise37.5 degrees/second, outer clockwise37.5, center clockwise56.25, all in the flat XY plane. Center has an independent wrapped phase and shares first-stage scale, alpha and lifetime; it does not create a third charge phase. No runtime hierarchy, sprite or mesh creation remains in this warning.
- Separate sprite renderers: meteor core, four-frame flame loop, three-frame impact flash, scorch. Separate ParticleSystems: flight smoke/detached flames, two fire-lobe variants, two residual-smoke variants, rays, embers, and bounced stone fragments through the existing `TopDownDebrisBounceEmitter2D`. No Collider/Rigidbody/attack actor or particle collision/trigger module exists in the presentation. World-space trail puffs finish after emission stops at impact.
- Landing warning lifetime (2026-11-01, supersedes charge translucency and flight shrinking): `ShowCharge` keeps both circle alphas at1 throughout hold. Valid partial/full release calls `BeginFlight`, preserves alpha1, hides the gauge and locks both circle sizes/target. `MoveMeteor` moves only the collision-free meteor. `PlayImpact` starts the existing impact phase without hiding the warning. Its shared phase clock fades both SpriteRenderer alphas from1 to0 over.25 gameplay seconds, then disables the warning child. Rotation continues through the fade; scales never contract. The impact sprites/particles keep their existing2.1s tail. Cancellation/weapon disable/scene cleanup still destroy the tracked presentation root, including a partly faded warning. See [damped aim/opaque hold verification](../../SessionLogs/2026-11-01-crimson-damped-aim/README.md).
- Magic-circle sources are revision25 PNG/Aseprite layers under `Art/Sprites/VFX/CrimsonBoundary/MagicCircle`, all192×192, Point/uncompressed/no mipmaps/16PPU. Middle/outer painted diameters remain128/192px; center painted bounds are46px. The smallest orange circle is removed from the middle image and owned by `center.png`, together with two inscribed yellow squares. Existing orange geometry remains B63D28/F16B32; runes and the new central square pair use FFC65A. The two orange octagrams retain their22.5° offset and tangent circles; the central yellow pair rotates independently inside the smallest circle. Presentation uses128/192 only to calibrate the inner sprite's painted size; the gameplay range ratio remains86/128, so half/full attack diameters stay4.03125/6. Existing GUIDs, prefab and material are retained. `MagicCircle.mat` uses `Project/Crimson Magic Circle`: nine point samples across one screen-pixel footprint select an existing source color to retain fine strokes during noninteger scaling/rotation. It preserves SpriteRenderer tint/alpha and shares one material; no camera, pipeline, resolution or global filtering setting is changed. This conservative sampling can slightly thicken a subpixel stroke; it does not reproduce the preview's vector redraw pixel for pixel. See [three-circle integration and native verification](../../SessionLogs/2026-11-01-crimson-three-circles/README.md).
- The six opaque VFX source colors are `2F242B 876657 B63D28 F16B32 FFC65A FFF0BF`; alpha fades are presentation-only. This VFX palette does not replace the separately approved gauge reference palette. Source frames/strips live under `Art/Sprites/VFX/CrimsonBoundary/ChargeSixColor`, imported Point, uncompressed, no mipmaps,16PPU. Flame pivot is(.25,.25); other sprites use the center.
- `CrimsonBoundaryRuntimeState` tracks the active cast and all detached tails. Cancellation/finally and scene cleanup remove an unfinished cast. On impact it hands the visual to its own2.1s expiry, while weapon disable/destruction still clears it through the transient registry. No persistent save state, manager, scene hierarchy creation, or Q schema migration was introduced.
- Current authoring uses the existing prefab and native PNG/Aseprite sources. Fixed `CrimsonChargeAssetAuthoring` and `CrimsonChargeVisualValidation` commands are retired to the [one-off tool archive](../TestAndEditorToolRetention.md); previous native captures/build records remain historical evidence. `CrimsonChargePlayModeTests` retains14 gameplay/aim/range/cooldown/cancel checks; the eight circle geometry/rotation/flight-fade acceptance cases are archived for user-led manual review. Automated tests run only on explicit request. No Architecture/Contracts promotion is required.
