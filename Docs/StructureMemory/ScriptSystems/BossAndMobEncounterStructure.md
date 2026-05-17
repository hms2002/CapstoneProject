---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-05-17
---

# Boss And Mob Encounter Structure

## Purpose

Map boss, mob, spawn, hazard, lock, and shared enemy combat scripts before any physical reorganization.

The file name is kept for link stability, but the working boundary is no longer "everything is an encounter".
Bosses use an `Encounter -> Battle -> BattleEnd` flow. General mobs use `Population / Spawn -> Battle Runtime -> Death Result`, with optional room/chest lock overlays.

## Current Inventory Groups

| Area | Count | Responsibility |
| --- | ---: | --- |
| Boss Encounter | 92 | Boss FSM core/states/configs, boss-specific controllers, pattern actors, boss presentation, BT/GAS bridge actions. |
| Mob AI | 62 | General mob FSM, mob coordinators, attack decision sources, pattern runners, mob-specific actors and cleanup hooks. |
| Monster Spawn | 16 | Scene spawn director, spawner state, room profiles, spawn points, difficulty receivers, spawn context. |
| Hazards / Puddles | 12 | Puddle and poison cloud runtime, hazard areas, puddle visuals, pool/placement logic. |
| Enemy Shared Combat | 3 | Enemy base, death command, shared cleanup utility. |

## Flow Boundary Map

| Flow boundary | Primary scripts | Responsibility |
| --- | --- | --- |
| Boss Encounter | Boss encounter intro, dialogue, target activation, combat entry hooks. | Prepare the boss fight and start combat. It should not own rewards, portals, or persistent run result processing. |
| Boss Battle | Boss FSM core/states/configs, boss-specific controllers, boss abilities, pattern actors, BT/GAS bridge. | Run the fight: phase evaluation, pattern selection/execution, damage/status interaction, groggy, and attack presentation. |
| Boss BattleEnd | Boss death presentation, `RunProgressCoordinator`, route-linked `BossSpecialRewardPresetSO`, scene-authored `BossBattleEndHandler`, authored `TreasureChest`/`ScenePortal` references, physical magic stone/field-heal drops, and `BossRewardFallbackService`. | Convert boss defeat into run/world results. The former `BossDrop` adapter and split reward/portal/anchor components have been removed; final-route contexts skip chest activation; authoring gaps are checked by `Docs/RefactorBacklog/BossDropResponsibilitySplit.md` and the validator. |
| Mob Population / Spawn | `MonsterSpawner`, `SceneMonsterSpawnDirector`, spawn containers, room groups/profiles, difficulty/context/pathfinding injection. | Instantiate and configure general mobs. This is the normal placement entry point for mobs, not a boss-style encounter. |
| Mob Battle Runtime | `Mob`, mob FSM, chase/facing/home return, attack decision source, `MobAbilityCoordinator`, pattern runners, mob ability logic. | Keep spawned mobs battle-ready, run chase/attack behavior, and perform cleanup according to `Docs/Contracts/MobCleanupContract.md`. |
| Mob Death Result | `Mob.OnDeathStarted`, monster loot spawn, spawned-monster tracking cleanup. | Convert mob death into immediate results. Long-term clear/lock semantics should not be assumed from root GameObject destruction alone. |
| Room / Chest Lock Overlay | `RoomDoorMonsterKillLock`, `ChestMonsterKillLock`, `ChestMonsterKillLockNavigationView`, spawn-time lock registration. | Observe registered combatants, unlock doors/chests, and show local chest guidance presentation. Count spawn-registered roots and Slime split descendants; do not count general direct summons. |
| Hazards / Puddles | Puddle runtime, conversion service, hazard damage, puddle visuals, boss-specific puddle interaction triggers. | Battle environment. Boss-specific triggers can live in boss ability logic, but hazard actors should avoid accumulating boss-specific policy. |
| Enemy Shared Combat | `Enemy`, death command, shared cleanup utility. | Shared enemy combat base, cleanup helpers, and canonical player target resolution for player-owned child/orbit colliders. |

### Boss Battle Implementation Breakdown

| Area | Count | Responsibility |
| --- | ---: | --- |
| Witch Boss | 28 | Witch controller, candle/shield services, pattern conditions/executors, states, actors, and abilities. |
| Slime Queen Boss | 16 | Slime Queen controller/base scripts, phase-two behaviors, summon/drop/jump/body-inflate ability logic. |
| Drunken Dragon Boss | 16 | Drunken Dragon controller/runtime data, arena/presentation helpers, and ability actors/logic. |
| FSM Core | 11 | Boss controller base, blackboard, state machine, pattern runtime/eval/select, groggy/death presentation core. |
| Demon King Boss | 8 | Demon King controller/runtime data, combat utility, actors, and ability logic. |
| FSM States | 7 | Spawn, dialogue, idle, pattern select/execute, groggy, and death states. |
| FSM Configs | 3 | Pattern entry, condition, and phase config data. |
| Behavior Tree Bridge | 2 | Boss behavior-tree selector and GAS action bridge scripts. |
| Boss FSM Root | 1 | Root `Boss` FSM component. |

### Boss Battle Hotspots

| Parent path | Hotspot | Count | Responsibility |
| --- | --- | ---: | --- |
| Boss Encounter > Witch Boss | Witch Ability Logics | 6 | Witch candle, basic attack, normal attack, light-all-candles, retreat, and sealed-candle rampage ability logic. |
| Boss Encounter > Witch Boss | Candle / Extinguish Flow | 5 | Candle service, sealed candle condition, extinguish condition/executor, and light-all-candles executor. |
| Boss Encounter > Witch Boss | Pattern Conditions / Executors | 4 | Witch pattern condition, normal attack executor, retreat executor, and related pattern selection helpers. |
| Boss Encounter > Witch Boss | Witch States | 4 | Witch retreat, normal attack, extinguish, and extinguish-pattern states. |
| Boss Encounter > Witch Boss | Attack Actors / Telegraphs | 4 | Witch projectile helper, normal attack tile, basic attack ring telegraph, and ring telegraph view. |
| Boss Encounter > Witch Boss | Shield Flow | 3 | Witch shield controller, visual controller, and shield receiver contract. |
| Boss Encounter > Witch Boss | Controller / Runtime Data | 2 | Witch controller and runtime data. |
| Boss Encounter > Slime Queen Boss | Slime Queen Ability Logics | 7 | Slime Queen summon, body inflate, drop, pillar, jump, slam, and toxic rush ability logic. |
| Boss Encounter > Slime Queen Boss | Phase Two Behaviors | 3 | Phase-two base, short, and long behaviors. |
| Boss Encounter > Slime Queen Boss | Controller / Base | 2 | Slime Queen controller and base. |
| Boss Encounter > Slime Queen Boss | Interfaces | 2 | Random jump and body-inflate host contracts. |
| Boss Encounter > Slime Queen Boss | Movement Bounds | 1 | Slime Queen random move bounds. |
| Boss Encounter > Slime Queen Boss | Summon Helpers | 1 | Falling summon helper. |
| Boss Encounter > Drunken Dragon Boss | Ability Logics / Actors | 8 | Drunken Dragon ability logic and thrown keg/spin projectile actors. |
| Boss Encounter > Drunken Dragon Boss | Cone Presentation | 3 | Cone pattern visual spec, particle visual, and visual interface. |
| Boss Encounter > Drunken Dragon Boss | Controller / Runtime Data | 3 | Drunken Dragon controller, runtime data, and animation keys. |
| Boss Encounter > Drunken Dragon Boss | Dialogue Selector | 1 | Dialogue start knot selector. |
| Boss Encounter > Drunken Dragon Boss | Drunken Dragon Other | 1 | Remaining Drunken Dragon support script. |

### Mob Battle Runtime Breakdown

| Area | Count | Responsibility |
| --- | ---: | --- |
| FSM Core | 11 | Mob state machine, context, states, attack request/decision source, and transition utility. |
| Slime Mob | 10 | Pawn, rook, bishop, knight, wizard, base slime, and slime pattern runners. |
| Strange Candlestick Mob | 10 | Strange Candlestick controller, candle/seal/light-zone helpers, projectiles, and attack runner. |
| Mob Abilities | 8 | Mob ability logic for bishop, skeleton, knight, candlestick, shadow servant, rook, tackle, and wizard attacks. |
| Mob Root / Shared Runtime | 7 | Base mob component, facing/chase intent, home return, ability bridge/coordinator, and pattern runner interface. |
| Shadow Servant Mob | 7 | Shadow servant controller, attack runner, fog, vision mask, and restricted vision presentation. |
| Dead Skeleton Mob | 3 | Skeleton controller, self-destruct pattern executor, and destroy-after-animation helper. |
| Single Mob Folders | 3 | Beer monster, corridor candlestick monster, and treasure monster scripts. |
| Shadow Monster Mob | 2 | Shadow monster controller and gauge visibility filter. |
| Shared Attack Legacy | 1 | Legacy/shared tackle attack script. |

### Population, Lock, And Hazard Breakdown

| Area | Count | Responsibility |
| --- | ---: | --- |
| Spawn Core / Request / Profile | 9 | Scene spawn director/policy, spawner/container/context/request, spawn profile, and spawn context receiver. This is mob population, not boss-style encounter flow. |
| Room / Door Lock Overlay | 3 | Room area/group, entry trigger, and door monster-kill lock. Treat as an overlay that observes spawned mobs. |
| Chest Lock Bridge | planned boundary | `MonsterSpawnContainer` and `SceneMonsterSpawnDirector` currently bridge spawned mobs to `ChestMonsterKillLock`. `ChestMonsterKillLockNavigationView` reads the lock's alive registered monsters for presentation only. Keep this boundary visible when reviewing spawn code. |
| Difficulty Receivers | 3 | Difficulty modifier and difficulty receiver contracts/components. |
| Pathfinding | 1 | Tilemap pathfinder helper. |
| Puddle Presentation | 3 | Shader, particle, and blob visuals. |
| Puddle Area Types | 3 | Alcohol, fire, and poison cloud area scripts. |
| Puddle Core / Conversion | 3 | Puddle manager, type definitions, and conversion service. |
| Puddle Other | 2 | Base puddle area and ignition source. |
| Puddle Debug | 1 | Debug puddle spawner. |

### Boss HUD Boundary Note

`BossHudController` belongs to the UI/HUD script group, but the boss review found a boss-flow boundary issue: the common Boss HUD currently knows the concrete `SlimeQueenP2Short` and `SlimeQueenP2Long` phase-two types.

Slime Queen phase two is allowed to be special. The issue is only where the special case lives.

Target direction:

- `BossHudController` should read a generic boss HUD source or snapshot.
- Normal bosses can be adapted by a single-boss HUD source.
- Slime Queen phase two can keep its custom rules in a `SlimeQueenPhaseTwoHudSource`-style adapter that knows the short/long bodies, labels, visibility, and groggy behavior.
- The common HUD should not keep adding boss-type-specific `Find*` branches for future split, multi-body, or shared-health bosses.

Track the concrete candidate in `Docs/RefactorBacklog/BossHudSpecialCaseSourceSplit.md`.

## Key Files

- `Assets/Script/Enemy/Boss/FSM/Core/BossControllerBase.cs`
- `Assets/Script/Enemy/Boss/FSM/Core/BossStateMachine.cs`
- `Assets/Script/Enemy/Mob/Mob.cs`
- `Assets/Script/Enemy/Mob/FSM/MobStateMachine.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/MonsterSpawn/MonsterSpawner.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/MonsterSpawn/SceneMonsterSpawnDirector.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/MonsterSpawn/RoomDoorMonsterKillLock.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Inventory/Chest/Runtime/LockedChest/ChestMonsterKillLock.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Inventory/Chest/Runtime/LockedChest/ChestMonsterKillLockNavigationView.cs`
- `Assets/HeoMinSeok/_Project/Prefabs/Gameplay/Items/KillLockMonsterNavigationArrow.prefab`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Puddles/Runtime/FirePuddleArea.cs`
- `Assets/LeeJunMo/Script/Editor/BossBattleEndMigrationValidatorWindow.cs`

## Ownership And Lifecycle

- Boss controllers own boss battle runtime; boss encounter setup and boss battle-end results should remain visible as separate flow boundaries.
- General mobs are spawned through population systems, then stay battle-ready through their own runtime FSM. Do not treat all mob work as encounter work.
- Spawn systems own instantiation/configuration and may bridge to lock overlays, but lock semantics are not the same as spawn semantics.
- `ChestMonsterKillLockNavigationView` is presentation-only: it reads alive registered monsters from `ChestMonsterKillLock`, spawns authored arrow prefabs locally, and must not decide unlock, spawn, or combatant counting rules. The authored arrow prefab is SpriteRenderer-only; avoid renderer-driving scripts and MeshRenderer/MeshFilter presentation for this 2D guidance. Its selected-object gizmos are authoring/debug visualization only.
- Puddles/hazards are battle environment systems and should stay separate from boss-specific policy unless the boss ability owns only a trigger.
- Enemy cleanup rules should follow `Docs/Contracts/MobCleanupContract.md` when general mobs are involved.
- Shared enemy player targeting should resolve to the canonical player root through `PlayerRuntimeRegistry`/`PlayerInteractor2D`; player-attached orbit/effect colliders or directly assigned child transforms should not become the boss target transform.

## Extension Entry Points

- Add new boss encounter setup through encounter/dialogue/director hooks, not reward or portal systems.
- Add new boss battle behavior through boss-specific controller/state/ability buckets.
- Add new boss battle-end behavior through route-linked special reward presets, a scene-authored `BossBattleEndHandler`, authored chest/portal references, and the resolved `BossDrop` split backlog.
- Use `Tools/Validation/Boss Battle-End Migration Validator` when relying on boss battle-end data for a boss scene. Prefab scans are stale-component checks only; scene Auto Fix buttons are a first wiring pass, not final authoring.
- Add new mob behavior through mob FSM, ability logic, and pattern runner patterns.
- Add mob population behavior through MonsterSpawn context/profile/director rather than individual enemies.
- Add lock overlay behavior only through explicit registration. Spawn-registered roots count, Slime split descendants inherit the same lock context, general direct summons do not count, and death presentation keeps counting until the tracked root is destroyed.

## Lock Count Policy

- Spawn-registered combatant roots count toward room/chest unlock conditions.
- Slime split descendants are the special split exception: they inherit the parent's room/chest lock context and keep the lock closed while alive.
- General direct summons are not lock targets. This includes boss/local summons that instantiate enemies without the room spawn registration path.
- Transform or phase changes on the same tracked GameObject are the same enemy for lock purposes.
- Death presentation still counts as alive while the tracked GameObject exists. The lock releases only after the tracked root is destroyed and compacted from the lock list.
- No-loot/gimmick enemies count only when they entered the lock through the spawn registration path or an explicit Slime split inheritance path.

## Known Pitfalls

- Boss-specific states, actors, and ability logic are mixed by boss; do not move them without prefab/scene reference checks.
- Pattern presentation ownership should follow `Docs/Contracts/PresentationAuthoringContract.md`.
- The former `BossDrop` legacy reward flow is resolved in `Docs/RefactorBacklog/BossDropResponsibilitySplit.md`; unhandled reward/portal authoring now reports editor/development warnings through `BossRewardFallbackService` instead of running a dynamic fallback.
- The boss battle-end migration validator can find missing scene `BossBattleEndHandler` coverage, missing authored chest/portal references, optional RouteSet special reward presets, stale deleted component/catalog GUIDs, stale definition/profile fields, and boss exit portal semantic mistakes. Scene Auto Fix can create first-pass handler/boss wiring, but final chest and portal objects must be placed and assigned in the Inspector.
- `RoomDoorMonsterKillLock` and `ChestMonsterKillLock` track registered root GameObjects. Do not infer lock participation from enemy type alone; use spawn registration or Slime split inheritance.
- KillLock chest navigation arrows follow the same registered root GameObject lifetime rule, so delayed destruction keeps arrows visible until the tracked root becomes null.
- `FirePuddleArea` has boss-specific target exclusion logic. If more hazard actors learn boss policy, move the rule toward a combat target policy instead of spreading concrete boss checks.
- Do not add new concrete boss type branches to common Boss HUD for split or multi-body bosses. Add a boss-specific HUD source/adapter instead.
- Player-attached relic/effect objects should avoid unintended `Player` tags, hurtboxes, or blocking body colliders. The shared `Enemy` target resolver maps player-owned child colliders and assigned child transforms back to the player root, but collision/hurtbox authoring still needs separate review.

## Promotion Candidate

Stable boss/mob rules already live in Architecture and Contracts. This map should stay in `StructureMemory` until the new Boss Flow, Mob Flow, and Lock Overlay boundaries prove stable enough for promotion.
