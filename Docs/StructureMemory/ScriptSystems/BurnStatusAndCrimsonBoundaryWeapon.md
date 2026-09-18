---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-09-17
---

# Burn Status And Crimson Boundary Weapon

## Purpose

Map the first independent integer-stack elemental status and the Lean weapon used to validate it. This document is context, not a replacement for Contracts or Architecture.

## Runtime Flow

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
