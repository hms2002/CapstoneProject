---
status: resolved
authority: refactor-backlog
category: refactor-item
last_reviewed: 2026-10-10
---

# Combat Element Build-Up Source Unification

## Status

resolved

## Current Problem

Elemental build-up used to expose two visible authoring/calculation paths.

- `DamagePayloadConfig.elementFormulas` and several weapon helpers could produce per-hit `CombatDamageSnapshot.ElementBuildUps`.
- `CombatDamageAction.ApplyElements(...)` applied gauge build-up from the attacker's `ElementOffenseSource` through `ElementBuildUpResolver`, not from that per-hit payload.

The gauge source remains explicit: attacker-wide `ElementOffenseSource` is evaluated through `ElementBuildUpResolver`. The current builder captures those values at attack creation into `CombatDamageSnapshot.FinalElementBuildUps`; `CombatHitPayload.elementBuildUps` and `CombatDamageAction` carry the resolved gauge snapshot. Legacy callers without a resolved snapshot retain `ResolveForApplication(...)` at application time. These runtime snapshots are not a return to legacy authored `DamagePayloadConfig.elementFormulas`, which remain ignored.

The 2026-10-10 direct-hit task adds separate `FinalElementHpDamages` / `elementHpDamages` for four final-stat HP constituents. They use the hit's shared HP postprocess multiplier and critical result, not gauge profiles or legacy per-hit element formulas. See [ElementDirectHitDamage](../StructureMemory/ElementDirectHitDamage.md) for that independent path.

The remaining serialized fields are intentionally kept as compatibility data. Removing them is not a behavior-preserving code refactor because it changes ScriptableObject schemas and requires asset migration or an explicit decision to abandon the old serialized values.

Remaining debt is serialized compatibility cleanup: legacy authored fields still exist so old assets keep loading.

## Why It Exists

- The project migrated from per-hit or per-weapon element payloads toward a central attacker attribute/profile model.
- Serialized data fields remain so older weapon assets keep loading.
- Old weapon helpers no longer build ignored `ElementDamageInput` lists for the applied path, and runtime API parameters that only carried ignored per-hit build-up data have been removed.

## Target Shape

- Applied elemental build-up has one calculation source: attacker `ElementOffenseSource` plus `ElementBuildUpResolver`. Current direct weapon snapshots retain creation-time values; unresolved legacy callers keep application-time evaluation.
- Legacy `DamagePayloadConfig.elementFormulas`, `critAffectsElement`, and old weapon data `ElementDamageInput` fields remain compatibility-only until serialized weapon data is migrated or intentionally abandoned.
- Direct-hit elemental HP constituents remain separate from gauge snapshots, profiles/modifiers and serialized compatibility fields; introducing them does not reopen the legacy gauge-authoring path.
- If future per-hit elemental tuning is required, add a named merge/override policy instead of reusing the old silently ignored payload path.

## Risks

- Existing weapon ScriptableObjects may still have serialized `DamagePayloadConfig.elementFormulas` values; two temporary RealWeapon data assets currently contain non-empty entries.
- Balance can change if a future task changes from attacker-wide source to a merge policy.
- `critAffectsElement` is now serialized compatibility data only and should stay compatibility-only until removed with the old formula fields.
- `ElementOffenseSource` requires attacker-side stat provider wiring; missing `IStatProvider` still skips automatic element build-up.

## Reopen Trigger

- Adding or tuning an elemental weapon where attacker-wide `ElementOffenseSource` is not enough.
- Migrating old weapon damage data assets.
- Explicitly approving removal of legacy element fields from `DamagePayloadConfig` or old weapon data classes.
- Editing `DamageSnapshotBuilder`, `CombatDamageAction`, `ElementBuildUpResolver`, or `ElementOffenseSource`.
- A playtest finds missing or unexpected attacker-wide element build-up.

## Related Documents

- `Docs/StructureMemory/ScriptSystems/WeaponAndGASStructure.md`
- `Docs/Architecture/CombatArchitecture.md`
- `Docs/DecisionLog.md`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Combat/Runtime/CombatDamageAction.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Combat/Runtime/DamageSnapshotBuilder.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Combat/Runtime/ElementBuildUpResolver.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Combat/Runtime/ElementOffenseSource.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Items/Weapons/Runtime/Data/DamagePayloadConfig.cs`

## Implementation Notes (Historical)

The following completion notes describe the earlier source-unification slices. Runtime gauge snapshot transport was subsequently restored from the same central source; use Current Problem and the linked structure map for the present flow.

Completed slice:

- Inventoried serialized `DamagePayloadConfig.elementFormulas`; most entries are empty, while `ALData_RW_Attack1.asset` and `ALData_RW_Skill2_SpeedStrike.asset` have non-empty legacy entries.
- Chose attacker-wide `ElementOffenseSource` as the applied source of truth.
- Added `ElementBuildUpResolver.ResolveForApplication(...)` so `CombatDamageAction` names the compatibility policy explicitly.
- Stopped `DamageSnapshotBuilder` from turning legacy per-hit element formulas or `elementInputs` into applied `CombatDamageSnapshot.ElementBuildUps`.
- Removed legacy per-hit element payload copying into `GameplayEffectSpec.Context.ElementDamages`.
- Kept serialized fields and public method parameters in place for compatibility, with editor-only warnings when legacy inputs are still used.

Follow-up producer cleanup slice:

- Removed ignored `ElementDamageInput` list construction from:
  - `FragmentBladeDamageUtility`
  - `AbilityLogic_LightningSpearAttack`
  - `AbilityLogic_SwordCombo2D`
  - `AbilityLogic_SwordSkill1_Projectile`
  - `AbilityLogic_SwordSkill2_BigSlash`
  - `AbilityLogic_RealWeaponSkill2SpeedStrike`
- Left all weapon data assets, serialized fields, ScriptableObject schemas, public combat APIs, and prefab/scene data unchanged.
- Kept `DamageSnapshotBuilder.BuildFromBaseValues(...)` as the compatibility boundary and routed the targeted helper call sites through `elementInputs: null`.
- Added central `BuildFromBaseValues(...)` warning coverage for legacy `DamagePayloadConfig.elementFormulas` so non-empty legacy configs are still visible even after producer code no longer evaluates those formulas.

Runtime API cleanup slice:

- Removed `CombatDamageSnapshot.ElementBuildUps`.
- Removed `CombatHitPayload.elementBuildUps`.
- Removed public/internal `CombatDamageAction` `elementBuildUps` / `elementDamages` parameters.
- Removed `GameplayEffectContext.ElementDamages`.
- Removed `DamageSnapshotBuilder.BuildFromBaseValues(... elementInputs)` and the ignored `DamageFormulaUtil.PostProcess(...)` element input/output parameters.
- Updated weapon, relic, mob, and boss call sites to use the runtime damage payload without per-hit element data.
- Kept `ElementDamageInput` itself because old weapon data classes still expose serialized fields of that type.

Closure review:

- `rg` confirms the runtime application path still resolves applied element build-up through `CombatDamageAction.ApplyElements(...)` and `ElementBuildUpResolver.ResolveForApplication(...)`.
- `rg` confirms ignored per-hit runtime payload APIs remain removed: `CombatDamageSnapshot.ElementBuildUps`, `CombatHitPayload.elementBuildUps`, `CombatDamageAction` element payload parameters, `GameplayEffectContext.ElementDamages`, and `DamageSnapshotBuilder.BuildFromBaseValues(... elementInputs)` are not present.
- `DamagePayloadConfig.elementFormulas`, `critAffectsElement`, `ElementDamageInput`, and legacy data classes remain as serialized compatibility fields only.
- The two temporary RealWeapon data assets with non-empty `elementFormulas` are not migrated or cleaned in this slice because the current project direction does not require polishing those temporary weapons.
- Reopen only for explicit asset/schema migration, a new elemental weapon tuning policy, or a regression in attacker-wide build-up application.
