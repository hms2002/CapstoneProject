# Monster Projectile Cadence

Status: implementation map, not a technical contract. Last verified: 2026-09-11.

## Purpose And Ownership

- `Runtime/Features/Monsters/Common/MobProjectileBurstCadence.cs` owns one monster's successful-shot count and rest deadline. It is a plain runtime object, not an SO, component, singleton, or scheduler.
- Each owner keeps a private readonly instance. Every cycle chooses an inclusive 3-5 shot budget. The final shot starts a 2-second `Time.time` deadline, independent of attack speed. Pausing game time also pauses this rest.
- Constants in the helper are the current common tuning point. No serialized schemas or prefab authoring references were added.
- Queries and cancelled preparations do not consume shots. Rest is not reset by leaving an attack/stagger state or by disabling/re-enabling the same monster. A newly instantiated monster owns fresh state.

## Integration

- `GoblinGunner`, `LizardMage`, `BeerMonster`, `StrangeCandlestick`, and `Wizard` gate attack decisions and execution contexts, and guard the actual fire method.
- Successful projectile setup records one shot. Wizard records once after a successful scatter volley, not once per pellet.
- LizardMage preserves the AL's per-sequence shot count and interval. If its running total reaches the rest threshold partway through a sequence, its runner breaks immediately rather than firing the remaining shots. A 3-shot AL can therefore produce 3 shots, then 1-2 on the next sequence, before resting.
- The five owners extend `ResolvePostAttackRecoverSeconds` with `max(existing recovery, remaining rest)`. This reuses normal recovery/retreat behavior instead of keeping an exclusive ability busy while resting. Request guards still protect the deadline if recovery is interrupted.
- Default Mob/FSM, melee attacks, Bishop ground blasts and boss-pattern executors are unchanged. The five shared monster types use this rhythm outside corridors too, without requiring room context.

## Extension And Pitfalls

- Another ranged owner can keep its own cadence instance, record only successful emissions, and use the same decision/context/fire guards. Never put mutable counters on a shared AL/SO.
- Count sequential bullets separately and simultaneous pellets as one firing action. Do not feed this rest through `CombatTimingService`, or elite speed will shorten it.
- Existing ordinary cooldowns and next-attack warnings remain in place. Two seconds is the minimum post-burst gap before the next attack can begin, not a forced exact interval between visible projectiles.
- No new Update loop, physics query, scene scan or coroutine is used for cadence; checks are constant-time alongside the existing attack flow.

## Verification

- `MonsterProjectileBurstCadencePlayModeTests` covers random cycle bounds/rerolls, deadline boundaries, ignored rest-time emissions, isolated instance counters, all five real prefab firing paths, scatter accounting, invalid Wizard payloads and Lizard runner early termination.
- Manual corridor pacing remains a gameplay tuning check. No Architecture/Contracts promotion or Presentation HTML change is currently needed.
