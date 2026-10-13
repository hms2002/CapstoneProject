---
status: active
authority: structure-memory
category: combat-flow-map
last_reviewed: 2026-10-11
---

# Element Direct-Hit Damage

Navigation and ownership map, not an Architecture or Contracts source of truth.

## Purpose and current structure

Direct weapon hits can carry four additional HP damage constituents alongside the existing base damage. These constituents are separate from elemental gauge build-up and from gauge-triggered effects. Existing weapon formulas, build-up profiles/modifiers, threshold behavior and effect damage keep their existing owners.

| Final stat | HP constituent tag |
| --- | --- |
| `BleedFinal` | `Element.Blood` |
| `FireFinal` | `Element.Fire` |
| `PoisonFinal` | `Element.Poison` |
| `ElectricFinal` | `Element.Electric` |

Each positive final stat is multiplied by the **same HP postprocess multiplier** used for the hit's base damage: `FinalMul` and, if that hit was critical, `CritMultiplier`. `DamageFormulaUtil.PostProcess` returns the multiplier with its single critical decision; capture does not reroll critical chance. Extra constituents do not multiply the weapon formula coefficient again.

`ElementHitDamageCalculator` is declared in `DamageSnapshotBuilder.cs`. It reads these final stats, captures tagged values in a fresh array, and sums valid positive constituents with base HP damage. Missing stats/provider or tags yield no positive constituent; it does not create gameplay state or Unity objects.

## Capture and transport ownership

- `DamageSnapshotBuilder.Build` / `BuildFromBaseValues` capture base HP, stagger, knockback, the critical result, the pre-existing gauge snapshot and the new HP constituents at attack creation. All current builder callers are direct weapon damage producers; future non-weapon callers must review this automatic capture boundary.
- `CombatDamageSnapshot.FinalHpDamage` remains base HP damage. `FinalElementHpDamages` holds the extra HP constituents, while `FinalElementBuildUps` / `HasResolvedElementBuildUps` continue to describe gauge build-up. `TotalHpDamage` sums HP constituents only.
- `CombatHitPayload.FromSnapshot` and `CombatDamageApplicator` forward the two separate element arrays. Payload `elementHpDamages` and the optional base `damagePopupElementTag` are nonserialized runtime metadata.
- Long-lived attack actors retain their captured arrays, and reconstructed payloads copy those arrays independently from gauge amounts. Existing base-only weapon/relic coefficients leave the extra constituents unchanged under the approved final-stat formula. Actor/ability cleanup remains in its existing owner; this data introduces no manager, DDOL object or retained subscription.
- Odd Iron deliberately calls the builder with a null physical provider: fixed base damage keeps multiplier `1` and noncritical behavior. The builder separately resolves the owner's elemental stat provider from `elementSource`, then captures element final stats with that same multiplier `1`; it does not apply the owner's normal physical postprocess to the fixed base.

The pre-existing gauge path still captures `ElementOffenseSource` through `ElementBuildUpResolver` when an element source is supplied. A resolved empty gauge snapshot suppresses application-time re-evaluation; legacy callers without a resolved snapshot keep the existing resolver fallback. `includeElementBuildUp`, element definitions/profiles/modifiers, and legacy serialized `elementFormulas` / `critAffectsElement` do not configure the new HP constituents.

## Explicit Crimson Boundary paths

Crimson's existing base damage is fire-based and remains independently calculated. `CrimsonBoundaryUtility.CalculateDirectDamage` has an overload that returns captured extra HP constituents using the same postprocess result as the base damage. Its basic projectile and direct explosion paths forward those constituents and mark the base popup as Fire.

Ignite captures the additional HP values alongside each consumed-stack explosion before area damage can remove another source. Its existing burn-consumption calculation remains noncritical. `CrimsonBoundaryLavaProjectile2D` preserves its live per-hit calculation and captures the extra constituents at that same hit-time boundary, rather than changing projectile timing to a creation snapshot.

The Crimson kill-shot relic retains its existing base fire calculation and Fire popup tag, but passes no extra HP constituents. Null `elementHpDamages` is the compatibility boundary for legacy direct APIs, DoT, puddle/environment damage and relic procs: application does not query attacker stats and manufacture additional HP channels for these callers.

## One hit application and constituent popups

`CombatDamageAction` sums base plus extra HP constituents before calling `CombatOutgoingDamageModifiers` and builds **one** damage `GameplayEffectSpec`. The existing `GE_Damage_Spec` then applies incoming modifiers, shield absorption and HP modification through the existing pipeline. Invulnerability/evasion exits and knockback, stagger, gauges, hit/kill/event and hit-feel routing remain one hit's existing flow; element constituents are not separate hit applications.

For constituent presentation, Combat attributes actual HP damage proportionally to the requested positive weights: `popup constituent = actual HP loss × requested constituent / requested total`. This accounts for outgoing/incoming modifiers, shield absorption, HP floor/lethal clamping and zero-HP-damage hits without claiming that each constituent independently passed through the shield. An HP-change observation scoped to the synchronous effect call can retain the damage amount when a training target auto-heals inside its change notification; the observer is removed in `finally`.

Within that one hit, the tagged base and additional HP constituents with the same element are displayed as one primary popup. Duplicate secondary element tags are also combined. An untagged base remains a separate ordinary popup; if base damage is zero, the largest grouped element becomes the full-size primary. This grouping is presentation-only and does not merge separate attacks, damage effects, gauge triggers or DoT ticks.

Secondary popup size is `0.42 + 0.43 × sqrt(clamp01(secondary weight / primary weight))`. It stays at or below 85% of the selected style's scale and retains a readable lower bound. Runtime-only `DamagePopupRequest.SizeScale` carries the factor; the existing profile and code fallback multiply only start/end scale. Critical motion/color rules remain in the profile, and default requests use scale 1. `DamagePopupService` still chooses positions through its existing random/overlap allocator; the preview's deliberate layout is not runtime placement.

`GameplayEffectContext.DamagePopupHandledByCombat` marks hits whose constituent/base-element popups belong to Combat. `GE_Damage_Spec` still reserves duplicate suppression for the HP listener, but skips its own element popup for those hits. Ordinary element effects keep their existing GE popup path. `DamagePopupRequest.Element` carries the hit's critical flag; `DamagePopupFormatProfileSO` uses critical motion/size/text with the element's color. Existing player-target damage red takes precedence over constituent color. Display rounding remains a UI rule, so rendered integer labels need not sum to the exact float HP loss.

## Key files and extension entry points

- [DamageSnapshotBuilder.cs](../../Assets/_Project/Runtime/Core/Combat/DamageSnapshotBuilder.cs), [DamageFormulaUtil.cs](../../Assets/_Project/Runtime/Core/Combat/DamageFormulaUtil.cs): capture formula, shared postprocess result, `ElementHitDamageCalculator`.
- [CombatDamageSnapshot.cs](../../Assets/_Project/Runtime/Core/Combat/CombatDamageSnapshot.cs), [CombatHitPayload.cs](../../Assets/_Project/Runtime/Core/Combat/CombatHitPayload.cs), [CombatDamageApplicator.cs](../../Assets/_Project/Runtime/Core/Combat/CombatDamageApplicator.cs): base versus gauge versus HP transport.
- [CombatDamageAction.cs](../../Assets/_Project/Runtime/Core/Combat/CombatDamageAction.cs), [CombatHurtbox2D.cs](../../Assets/_Project/Runtime/Core/Combat/CombatHurtbox2D.cs): combined hit application, popup allocation and hit eligibility.
- [GameplayEffectSpec.cs](../../Assets/_Project/Runtime/Core/Effects/GameplayEffectSpec.cs), [GE_Damage_Spec.cs](../../Assets/_Project/Runtime/Core/Effects/GE_Damage_Spec.cs): context popup ownership, existing shield/incoming/HP path.
- [ElementBuildUpResolver.cs](../../Assets/_Project/Runtime/Core/Elements/ElementBuildUpResolver.cs), [ElementOffenseSource.cs](../../Assets/_Project/Runtime/Core/Elements/ElementOffenseSource.cs): independent gauge source and policy.
- [CrimsonBoundaryUtility.cs](../../Assets/_Project/Runtime/Features/Items/Weapons/CrimsonBoundary/CrimsonBoundaryUtility.cs), [AbilityLogic_CrimsonBoundaryAttack.cs](../../Assets/_Project/Runtime/Features/Items/Weapons/CrimsonBoundary/AbilityLogic_CrimsonBoundaryAttack.cs), [AbilityLogic_CrimsonBoundaryIgnite.cs](../../Assets/_Project/Runtime/Features/Items/Weapons/CrimsonBoundary/AbilityLogic_CrimsonBoundaryIgnite.cs), [AbilityLogic_CrimsonBoundaryBigExplosion.cs](../../Assets/_Project/Runtime/Features/Items/Weapons/CrimsonBoundary/AbilityLogic_CrimsonBoundaryBigExplosion.cs), [CrimsonBoundaryLavaProjectile2D.cs](../../Assets/_Project/Runtime/Features/Items/Weapons/CrimsonBoundary/CrimsonBoundaryLavaProjectile2D.cs): explicit capture timing and relic exclusion.
- [OddIronAbilityUtility.cs](../../Assets/_Project/Runtime/Features/Items/Weapons/WeaponLogic/OddIronAbilityUtility.cs): fixed physical damage with owner element source.
- [DamagePopupRequest.cs](../../Assets/_Project/Runtime/Core/Presentation/DamagePopupRequest.cs), [DamagePopupFormatProfileSO.cs](../../Assets/_Project/Runtime/UI/Combat/DamagePopup/DamagePopupFormatProfileSO.cs): element/critical semantic projection. Popup lifetime stays in [DamagePopupPooling](DamagePopupPooling.md).

## Known pitfalls and validation handoff

- Checking only `FinalHpDamage` rejects an element-only hit; HP eligibility must use the combined value where the caller carries these constituents.
- Rebuilding/scaling a snapshot while dropping `FinalElementHpDamages` loses captured weapon damage. Querying live stats during application changes projectile snapshot behavior.
- Using gauge formulas/profile scaling for extra HP channels couples two independently approved systems. Legacy compatibility formulas remain ignored.
- Applying one GE per constituent multiplies modifiers, shields, invulnerability and hit events. Popup decomposition belongs after the single effect application.
- A null physical provider does not necessarily mean no elemental source; Odd Iron is the explicit fixed-damage case.
- A tagged Crimson base and its extra Fire constituent share one Fire popup. Grouping must not replace the base formula or apply an additional damage effect. Secondary sizes use the merged primary weight, and an element-only hit must still show its largest channel at full size.
- Auto-heal can erase a final pre/post HP difference. Popup evidence must observe the actual damage notification, without changing existing gameplay event policies.

The focused regression entry is `ElementHitDamagePlayModeTests` in [CombatPresentationRegressionPlayModeTests.cs](../../Assets/_Project/Tests/PlayMode/Procedural/CombatPresentationRegressionPlayModeTests.cs). See [the direct-hit validation record](../SessionLogs/2026-10-10-element-hits/README.md) and [the grouping/scale continuation](../SessionLogs/2026-10-11-element-popup-ratio/README.md) for exact coverage. Interactive all-weapon acceptance remains a manual handoff. This change does not modify scenes, prefabs, `.meta`/GUIDs, persistent enum values, ScriptableObject schemas, or authored popup assets. Architecture/Contracts promotion is a future explicitly approved documentation slice if this flow becomes a durable source-of-truth contract.

Related decision: [2026-10-10 direct weapon elemental HP channels](../DecisionLog.md). Related gauge compatibility debt: [CombatElementBuildUpSourceUnification](../RefactorBacklog/CombatElementBuildUpSourceUnification.md).
