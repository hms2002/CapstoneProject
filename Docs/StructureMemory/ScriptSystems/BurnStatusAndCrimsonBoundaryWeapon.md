---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-08-20
---

# Burn Status And Crimson Boundary Weapon

## Purpose

Map the first independent integer-stack elemental status and the Lean weapon used to validate it. This document is context, not a replacement for Contracts or Architecture.

## Runtime Flow

`홍련의 한계선` ability logic reads the attacker's live `FireFinal`, applies direct damage through `CombatDamageAction`, and explicitly supplies an empty resolved element result so the legacy fire gauge is not built. The basic projectile then applies `BurnStatus2D` to the resolved damage-target root.

`BurnStatus2D` is target-owned. It stores 0–99 stacks, the most recent source system/effect/causer, and a tick accumulator. Active reapplication does not reset the accumulator. Each tick reads the source's current Fire Damage, applies the current Burn ratio, rounds final damage, emits a UI pulse, then consumes one stack.

`BurnSourceRuntime` is source-owned and lazily attached when Burn is first applied. Its default rules are one-second ticks and 50% Fire Damage. Fire relics write token-keyed modifiers for tick interval, damage ratio, application amount, first application, critical permission, and target-stack-based damage scaling. Relic removal removes only its own token.

Immediately before a Burn tick reaches `CombatDamageAction`, `BurnStatus2D` applies the source runtime's target-stack multiplier. This keeps `홍련의 왕` separate from the base Burn coefficient and makes its cap apply to its own contribution.

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
- Runtime-created squares are prototype presentation explicitly accepted for this slice. Replace them with authored prefabs when final visuals are approved.

## Fire Relics

| Relic | Runtime rule | Levels |
| --- | --- | --- |
| 타오르는 심핵 | `FireAdd` +2 per level | 8 |
| 녹아내린 종 | Burn interval 0.9/0.8/0.7/0.6/0.5 seconds | 5 |
| 매캐한 향로 | Burn coefficient +10/+20/+30/+40/+50 percentage points | 5 |
| 휴대용 화로 | First application to a non-burning target +1/+2/+3/+4/+5 stacks | 5 |
| 태양의 파편 | Every 2 seconds, generate up to 1/2/3 orbiting squares; contact deals 100% Fire Damage and applies 4/6/8 Burn | 3 |
| 작열하는 송 | Burn ticks may use normal critical chance and multiplier | 1 |
| 불타는 깃털 | Every Burn application +1/+2/+3 stacks | 3 |
| 홍련의 왕 | Every 20/15/10 target stacks grants +5/+4/+5% Burn damage, capped at +30% | 3 |

The Notion table labels `홍련의 왕` as maximum level 5 but defines only three level rows. The Lean implementation intentionally uses three levels until design supplies levels 4–5.

## Ability Semantics

- Attack / left click: cooldown 1 second, Fire Damage, Burn 3, destroy on enemy or wall.
- Skill 1 / right click: cooldown 5 seconds, consumes up to 5 Burn from every visible Burn target, and creates overlapping diameter-5 explosions. With no eligible visible target the runtime state consumes input before ability commit, so cooldown does not start.
- Skill 2 / Q: cooldown 12 seconds, locks cursor impact position, warns for 0.6 seconds, deals 200% Fire Damage, consumes each target's full Burn, and adds 50% Fire Damage per consumed stack.

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
