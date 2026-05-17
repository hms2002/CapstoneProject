---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-05-15
---

# Weapon And GAS Structure

## Purpose

Map the weapon, GAS/ability, combat, status, movement, and player-adjacent runtime scripts before any physical reorganization.

## Current Structure

| Area | Count | Responsibility |
| --- | ---: | --- |
| Weapons | 143 | Weapon definitions, inventory/equip runtime, runtime data/processors, loadouts, selection strategies, executors, actors, interaction rules, weapon-specific ability logic. |
| GAS / Abilities | 83 | AbilitySystem, ability definitions/specs, effects, attributes, tags, cues, gameplay event relay, presentation routing, root gameplay tag constants. |
| Combat | 47 | Damage requests/snapshots/application, hurtboxes, invulnerability/evasion, stagger, element gauges, telegraphs, hit feedback, combat height helpers. |
| Player | 14 | Player interactor, input, health/death, spawn/cinematic protection, animation, pickup collection, player presentation. |
| Movement | 8 | Shared 2D movement motor, knockback, external movement, soft collision, collision profile, ability motion. |
| Status | 6 | Player status runtime, buff/debuff application, runtime stat projection sources. |

### Weapon Breakdown

| Area | Count | Responsibility |
| --- | ---: | --- |
| Ability Runtime | 66 | Weapon loadouts, selectors, runtime states, executors, actors, and ability-facing runtime helpers. |
| Weapon Data / Tuning | 34 | Weapon runtime data, processors, attack data, payload config, and tuning ScriptableObjects. |
| Weapon Logic Implementations | 21 | Weapon-specific ability logic and damage helpers. |
| Weapon Interactions | 7 | Pair rules and interaction-layer helpers for weapon-to-weapon state influence. |
| Equipment / Presentation Rig | 6 | Equip controller, aim presentation settings, and weapon visual/presentation rigs. |
| Weapon Inventory Runtime | 5 | Weapon inventory, equip runtime, and inventory/runtime binders. |
| Weapon Runtime Root | 4 | Weapon definition, drop, persistent payload, and runtime state bridge root scripts. |

### Weapon Hotspots

| Parent path | Hotspot | Count | Responsibility |
| --- | --- | ---: | --- |
| Weapons > Ability Runtime | Weapon Ability Framework | 15 | Shared weapon ability bridge, selector, loadout, executor, execution context, runtime state, and base attack framework. |
| Weapons > Ability Runtime | Lightning Spear Runtime | 13 | Lightning Spear loadout/state/selection, marks, recovered spear actors/projectiles, feedback, trails, hit config, and event relays. |
| Weapons > Ability Runtime | Named Blade Runtime | 12 | Sun, Moon, Eclipse, and Mark Sword loadouts, runtime states, selection strategies, and related named blade runtime helpers. |
| Weapons > Ability Runtime | Fragment Blade Runtime | 7 | Fragment Blade loadout/state/selection, presentation actors, bound shard visuals, and shard actor. |
| Weapons > Ability Runtime | Execution Weapon Runtime | 6 | Execution Gun and Executioner Greatsword loadouts, runtime states, and selection strategies. |
| Weapons > Ability Runtime | Odd Iron Runtime | 6 | Odd Iron loadout/state/selection, projectile/thrown projectile, and break VFX. |
| Weapons > Ability Runtime | Chain Spear Runtime | 4 | Chain Spear loadout/state/selection and throw executor. |
| Weapons > Ability Runtime | Shared Actors / Presentation | 3 | Shared melee hitbox, hitbox visual animator, and sword projectile actor. |
| Weapons > Weapon Data / Tuning | Runtime Data Infrastructure | 6 | Shared weapon runtime data, factory, processor, processor factory, coordinator, and process context. |
| Weapons > Weapon Data / Tuning | Weapon Runtime Data / Processors | 12 | Weapon-specific runtime data and processors for Sun, Moon, Mark, Odd Iron, Lightning Spear, Fragment Blade, Execution Gun, and Eclipse Sword. |
| Weapons > Weapon Data / Tuning | Attack / Skill Data | 15 | Weapon attack/skill data objects, combo data, recall data, dry-fire/shot/throw data, and damage payload config. |
| Weapons > Weapon Data / Tuning | Data Interfaces | 1 | Attack-speed-scaled step data interface. |
| Weapons > Weapon Logic Implementations | Legacy / Sword / RealWeapon | 6 | Sword combo/projectile/big slash logic and RealWeapon attack/rush/speed-strike logic. |
| Weapons > Weapon Logic Implementations | Odd Iron | 5 | Odd Iron throw, shot, dry-fire, barrage logic, and utility. |
| Weapons > Weapon Logic Implementations | Fragment Blade | 4 | Fragment Blade attack, bind enhance, recall, and damage utility. |
| Weapons > Weapon Logic Implementations | Lightning Spear | 3 | Lightning Spear attack, skill 1, and skill 2 logic. |
| Weapons > Weapon Logic Implementations | Debug Actions | 3 | Executioner, Eclipse, and Chain Spear debug action ability logic. |

### GAS / Ability Breakdown

| Area | Count | Responsibility |
| --- | ---: | --- |
| Ability Runtime Core | 31 | AbilitySystem, definitions/specs, execution coordination, cooldowns, events, presentation and cleanup routing. |
| Gameplay Cues | 15 | Cue database, cue manager, cue notify scripts, hit/groggy/falling/camera presentation cue implementations. |
| Attributes | 14 | Attribute definitions, sets, modifiers, stat providers, catalogs, and stat binding support. |
| Gameplay Effects | 12 | GameplayEffect specs/runners, active effect repository, cooldown/status/damage/knockback effect definitions. |
| Gameplay Tags | 6 | Gameplay tag runtime, registry, masks, tag sets, and root `UGAS_Tags` constants. |
| Ability Visuals | 2 | Ability afterimage and visual runtime helpers. |
| Generic Ability Logics / Debug Tools | 2 | Generic dash logic and realtime hitbox debug helper. |
| Animation Relay | 1 | Ability animation event relay. |

### GAS / Ability Hotspots

| Parent path | Hotspot | Count | Responsibility |
| --- | --- | ---: | --- |
| GAS / Abilities > Ability Runtime Core | Presentation Runtime | 8 | Gameplay presentation runtime/definition/phase, world object presentation, particle/afterimage/runtime guard visual helpers. |
| GAS / Abilities > Ability Runtime Core | Lifecycle / Execution | 7 | AbilitySystem, definition/spec, base logic, task model, execution coordinator, and parallel execution. |
| GAS / Abilities > Ability Runtime Core | Gameplay Event Pipeline | 5 | Ability event data, event channel/relay, event waiter, and gameplay event listener contract. |
| GAS / Abilities > Ability Runtime Core | State / Cooldown / Cleanup | 4 | Ability cooldown, cancellation token, persistent state, and cleanup contract. |
| GAS / Abilities > Ability Runtime Core | Resolver / Movement Support | 3 | Attack speed resolver, move direction resolver, and movement state provider. |
| GAS / Abilities > Ability Runtime Core | Routing / Audio / Cues | 2 | Ability presentation/audio routing and hit cue routing. |
| GAS / Abilities > Ability Runtime Core | Effect Containers | 1 | Ability effect container. |
| GAS / Abilities > Ability Runtime Core | Runtime Other | 1 | Remaining runtime support script not yet worth a narrower bucket. |
| GAS / Abilities > Gameplay Cues | Cue Framework | 7 | Cue database, definition, manager, notify, params, tag bridge, and transform stack. |
| GAS / Abilities > Gameplay Cues | Hit / Particle Cues | 3 | Slash hit, hit spark particles, and particle-system cue implementations. |
| GAS / Abilities > Gameplay Cues | Groggy Cues | 3 | Groggy presentation and groggy-break sprite/driver cues. |
| GAS / Abilities > Gameplay Cues | Movement / Camera Cues | 2 | Falling and camera shake cues. |

### Combat Breakdown

| Area | Count | Responsibility |
| --- | ---: | --- |
| Damage / Hit Pipeline | 12 | Damage requests, snapshots, applicators/actions, hurtboxes, hit payload, and target resolution. |
| Element Gauge / Elemental Damage | 7 | Element definitions, catalogs, buildup resolver/formula, offense source, and element gauge system. |
| Attack Telegraph | 6 | Telegraph service, specs, shapes, styles, and wall-clipped/standard telegraph views. |
| Aim / Facing / Socket Utilities | 5 | Aim/facing direction sources, mirrored sockets, facing offsets, and aim resolver helpers. |
| Feedback / Camera / Audio | 5 | Player/monster hit feedback, camera shake/impulse, and combat hit audio routing. |
| Combat Rules Utilities | 4 | Invulnerability, evasion, hazard damage action, and stagger gauge helpers. |
| Combat Height | 4 | Height state, filter, presentation, and collision binding. |
| Formulas / Attribute Mutation | 4 | Scaled/stack stat formulas and attribute mutation policy/extensions. |

## Key Files

- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Abilities/Runtime/AbilitySystem.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Abilities/WeaponExecutorRunner.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Data/WeaponRuntimeData.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Combat/Runtime/CombatDamageAction.cs`

## Ownership And Lifecycle

- Weapon state should stay owned by weapon runtime data, processors, and selected weapon runtime state objects.
- GAS/ASC should execute selected ability behavior; it should not become the owner of weapon-specific persistent state.
- Combat damage/hit pipeline is shared infrastructure and should remain independent from a single weapon or enemy.
- HUD and tooltip-facing weapon data should project current runtime state rather than own it.

## Runtime Boundary Review

The current combat/status structure is broadly aligned with the Architecture documents: damage is applied through GAS/effects, status HUD data is projected through `PlayerStatusRuntime`, and HUD views should display current state rather than own it.

The concrete risks are narrower than a full combat rewrite.

| Boundary | Intended responsibility | Current pressure point |
| --- | --- | --- |
| Element Build-Up Source | Keep one explicit source of truth for applied elemental build-up calculation. | `CombatDamageAction` routes application through `ElementBuildUpResolver.ResolveForApplication(...)`, which uses attacker `ElementOffenseSource`; runtime hit payload APIs no longer carry per-hit element build-up, while legacy serialized fields remain compatibility-only data until an explicit asset/schema migration. |
| Damage Preflight | Keep invulnerability, evasion, HP change checks, and damaged-event emission consistent across combat and hazards. | `CombatDamageAction` and `HazardDamageAction` intentionally differ on hit/kill confirm, but they duplicate several preflight and post-damage checks. |
| Status Runtime | Keep `PlayerStatusRuntime` as an apply/projection hub, not as the real owner of gameplay effects. | Current `CombatBuffDebuffApplier` flow mostly follows this, but `GetOrAdd` component creation remains a prefab/scene authoring risk when the player root is missing expected components. |
| Combat Presentation Hooks | Keep combat state and authored presentation references separate enough to move safely later. | `ElementGaugeSystem` owns trigger/sustain VFX instances and `StaggerGaugeSystem` can spawn a boss groggy timer prefab, so file movement needs serialized-reference review. |

### Refactor Candidate

- `Docs/RefactorBacklog/CombatElementBuildUpSourceUnification.md` is resolved for runtime/code debt. Reopen it only for explicit asset/schema migration, new elemental weapon tuning policy, or a regression in attacker-wide build-up application.
- Do not treat endpoint weapon-specific elemental behavior as the problem. The applied source is attacker-wide `ElementOffenseSource`; legacy serialized data names are compatibility artifacts and do not imply per-hit tuning still applies.

## Extension Entry Points

- Add new weapon behavior through weapon data, loadout/selection, executor/logic, and runtime state patterns already present in this map.
- Add shared ability behavior through GAS runtime/core when multiple systems need it.
- Add combat rules only when they belong in shared hit/damage/element/telegraph flow rather than a single weapon.

## Known Pitfalls

- Do not move MonoBehaviour or ScriptableObject weapon/GAS scripts without Unity serialized reference review.
- Do not collapse weapon runtime state into GAS unless an Architecture update explicitly approves it.
- Applied elemental build-up policy is attacker `ElementOffenseSource`; do not reintroduce per-hit payload application without a named merge/override policy and serialized weapon data audit.
- `SwordCombo2D` and RealWeapon naming appear legacy/sample-like; treat them as existing behavior, not project-wide policy, unless a task targets them.

## Promotion Candidate

Some stable rules already exist in `Docs/Architecture/GameplayAbilityWeaponArchitecture.md` and combat architecture. This map should remain `StructureMemory` until a focused task proves a new rule should be promoted.
