using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// Centralized "apply damage result" utility.
/// - HP damage: applied via GameplayEffect(Spec) + SetByCaller damageKey (GE_Damage_Spec)
/// - Knockback: applied via separate GE_Knockback_Spec
/// - Stagger build-up: applied to StaggerGaugeSystem on target (if present)
/// - Element build-up: applied to ElementGaugeSystem on target (if present)
/// - hit confirm 태그가 비어 있을 때 공용 기본 태그를 fallback으로 공급한다.
///
/// NOTE: In this project, "element damage" is treated as "element gauge build-up".
/// </summary>
public static class CombatDamageAction
{
    private const string DefaultHitConfirmTagResourcePath = "Tags/Event.HitConfirm";
    private const string GroggyTagResourcePath = "Tags/State.Status.Groggy";
    private const string StaggerImmuneTagResourcePath = "Tags/State.Status.StaggerImmune";
    private static GameplayTag s_defaultHitConfirmTag;
    private static GameplayTag s_groggyTag;
    private static GameplayTag s_staggerImmuneTag;

    // Backward-compatible overload (no stagger / no knockback effect)
    public static void ApplyDamageAndEmitHit(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GameObject target,
        float finalHpDamage,
        IReadOnlyList<ElementDamageResult> elementDamages,
        GameplayTag hitConfirmedTag,
        GameObject causer,
        bool isCriticalHit = false)
    {
        ApplyDamageAndEmitHit(
            system,
            spec,
            damageEffect,
            target,
            finalHpDamage,
            elementDamages,
            hitConfirmedTag,
            target != null ? target.transform.position : Vector3.zero,
            causer,
            isCriticalHit);
    }

    public static void ApplyDamageAndEmitHit(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GameObject target,
        float finalHpDamage,
        IReadOnlyList<ElementDamageResult> elementDamages,
        GameplayTag hitConfirmedTag,
        Vector3 hitWorldPosition,
        GameObject causer,
        bool isCriticalHit = false)
    {
        ApplyDamageAndEmitHit(
            system,
            spec,
            damageEffect,
            knockbackEffect: null,
            target: target,
            finalHpDamage: finalHpDamage,
            finalStaggerBuildUp: 0f,
            elementBuildUps: elementDamages,
            finalKnockbackImpulse: 0f,
            hitConfirmedTag: hitConfirmedTag,
            hitWorldPosition: hitWorldPosition,
            causer: causer,
            isCriticalHit: isCriticalHit);
    }

    // New overload: includes knockback effect + impulse
    public static void ApplyDamageAndEmitHit(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GE_Knockback_Spec knockbackEffect,
        GameObject target,
        float finalHpDamage,
        float finalStaggerBuildUp,
        IReadOnlyList<ElementDamageResult> elementBuildUps,
        float finalKnockbackImpulse,
        GameplayTag hitConfirmedTag,
        GameObject causer,
        bool isCriticalHit = false)
    {
        ApplyDamageAndEmitHit(
            system,
            spec,
            damageEffect,
            knockbackEffect,
            target,
            finalHpDamage,
            finalStaggerBuildUp,
            elementBuildUps,
            finalKnockbackImpulse,
            hitConfirmedTag,
            target != null ? target.transform.position : Vector3.zero,
            causer,
            isCriticalHit);
    }

    public static void ApplyDamageAndEmitHit(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GE_Knockback_Spec knockbackEffect,
        GameObject target,
        float finalHpDamage,
        float finalStaggerBuildUp,
        IReadOnlyList<ElementDamageResult> elementBuildUps,
        float finalKnockbackImpulse,
        GameplayTag hitConfirmedTag,
        Vector3 hitWorldPosition,
        GameObject causer,
        bool isCriticalHit = false)
    {
        ApplyDamageAndEmitHit_Internal(
            system,
            spec,
            damageEffect,
            knockbackEffect,
            target,
            finalHpDamage,
            finalStaggerBuildUp,
            elementBuildUps,
            finalKnockbackImpulse,
            hitConfirmedTag,
            hitWorldPosition,
            causer,
            isCriticalHit);
    }

    /// <summary>
    /// Apply HP damage + knockback + stagger build-up + element build-up,
    /// then optionally emit hit-confirmed event.
    /// </summary>
    public static void ApplyDamageAndEmitHit(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GE_Knockback_Spec knockbackEffect,
        GameObject target,
        float finalHpDamage,
        float finalStaggerBuildUp,
        IReadOnlyList<ElementDamageResult> elementBuildUps,
        GameplayTag hitConfirmedTag,
        GameObject causer,
        bool isCriticalHit = false)
    {
        ApplyDamageAndEmitHit(
            system,
            spec,
            damageEffect,
            knockbackEffect,
            target,
            finalHpDamage,
            finalStaggerBuildUp,
            elementBuildUps,
            hitConfirmedTag,
            target != null ? target.transform.position : Vector3.zero,
            causer,
            isCriticalHit);
    }

    public static void ApplyDamageAndEmitHit(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GE_Knockback_Spec knockbackEffect,
        GameObject target,
        float finalHpDamage,
        float finalStaggerBuildUp,
        IReadOnlyList<ElementDamageResult> elementBuildUps,
        GameplayTag hitConfirmedTag,
        Vector3 hitWorldPosition,
        GameObject causer,
        bool isCriticalHit = false)
    {
        ApplyDamageAndEmitHit_Internal(
            system,
            spec,
            damageEffect,
            knockbackEffect,
            target,
            finalHpDamage,
            finalStaggerBuildUp,
            elementBuildUps,
            finalKnockbackImpulse: 0f,
            hitConfirmedTag,
            hitWorldPosition,
            causer,
            isCriticalHit);
    }

    // ---- Internal orchestration -------------------------------------------------------------

    private static void ApplyDamageAndEmitHit_Internal(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GE_Knockback_Spec knockbackEffect,
        GameObject target,
        float finalHpDamage,
        float finalStaggerBuildUp,
        IReadOnlyList<ElementDamageResult> elementBuildUps,
        float finalKnockbackImpulse,
        GameplayTag hitConfirmedTag,
        Vector3 hitWorldPosition,
        GameObject causer,
        bool isCriticalHit)
    {
        if (!Validate(system, damageEffect, target)) return;
        if (CombatEvasionUtil.TryRollEvasion(target))
        {
            DamagePopupService.ShowText("EVADE", target.transform.position);
            return;
        }

        var runner = system.EffectRunner;

        // 0) Extract GE_Damage_Spec once
        var geDmg = damageEffect as GE_Damage_Spec;

        // 1) capture pre-HP for KillConfirmed / Damaged check
        var hpCheck = CaptureHpCheck(target, geDmg);

        // 2) Apply damage effect
        var damageSpec = BuildDamageSpec(
            system,
            spec,
            damageEffect,
            geDmg,
            finalHpDamage,
            elementBuildUps,
            causer);

        runner.ApplyEffectSpec(damageSpec, target);

        // 3) target-side damaged event
        EmitDamagedTaken(system, damageEffect, target, spec, causer, hpCheck);
        

        // 4) Apply knockback effect separately
        ApplyKnockbackEffect(
            system,
            spec,
            knockbackEffect,
            target,
            finalKnockbackImpulse,
            causer);

        // 5) KillConfirmed
        TryEmitKillConfirmed(system, spec, target, causer, hpCheck);

        // 6) Post systems
        ApplyStagger(target, finalStaggerBuildUp, system.gameObject, causer);
        ApplyElements(target, elementBuildUps, system.gameObject, causer);

        // 7) Hit confirmed event
        EmitHitConfirmed(system, spec, target, causer, hitConfirmedTag, hitWorldPosition, isCriticalHit);
    }

    private static bool Validate(AbilitySystem system, GameplayEffect damageEffect, GameObject target)
    {
        if (system == null || damageEffect == null || target == null) return false;
        if (system.EffectRunner == null) return false;
        return true;
    }

    // ---- Spec building ----------------------------------------------------------------------

    private static GameplayEffectSpec BuildDamageSpec(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GE_Damage_Spec geDmg,
        float finalHpDamage,
        IReadOnlyList<ElementDamageResult> elementBuildUps,
        GameObject causer)
    {
        var geSpec = system.MakeSpec(
            damageEffect,
            causer: causer,
            sourceObject: spec != null ? spec.Definition : null);

        // HP damage via SetByCaller
        if (geDmg != null && geDmg.damageKey != null)
            geSpec.SetSetByCallerMagnitude(geDmg.damageKey, finalHpDamage);

        // Keep element breakdown in context as payload
        if (elementBuildUps != null && elementBuildUps.Count > 0)
        {
            var dst = geSpec.Context.ElementDamages;
            dst.Clear();
            for (int i = 0; i < elementBuildUps.Count; i++)
                dst.Add(elementBuildUps[i]);
        }

        return geSpec;
    }

    private static void ApplyKnockbackEffect(
        AbilitySystem system,
        AbilitySpec spec,
        GE_Knockback_Spec knockbackEffect,
        GameObject target,
        float finalKnockbackImpulse,
        GameObject causer)
    {
        if (system == null || system.EffectRunner == null) return;
        if (knockbackEffect == null) return;
        if (target == null) return;
        if (finalKnockbackImpulse <= 0f) return;

        var knockbackSpec = system.MakeSpec(
            knockbackEffect,
            causer: causer,
            sourceObject: spec != null ? spec.Definition : null);

        if (knockbackEffect.knockbackKey != null)
            knockbackSpec.SetSetByCallerMagnitude(
                knockbackEffect.knockbackKey,
                finalKnockbackImpulse);

        system.EffectRunner.ApplyEffectSpec(knockbackSpec, target);
    }

    // ---- Kill confirmed ---------------------------------------------------------------------

    private readonly struct HpCheckData
    {
        public readonly float PreHp;
        public readonly AttributeDefinition HpAttr;
        public readonly AttributeSet TargetAttrs;

        public HpCheckData(float preHp, AttributeDefinition hpAttr, AttributeSet targetAttrs)
        {
            PreHp = preHp;
            HpAttr = hpAttr;
            TargetAttrs = targetAttrs;
        }

        public bool IsValid => TargetAttrs != null && HpAttr != null && PreHp >= 0f;
    }
    private static HpCheckData CaptureHpCheck(GameObject target, GE_Damage_Spec geDmg)
    {
        if (geDmg == null || geDmg.healthAttribute == null)
            return default;

        var hpAttr = geDmg.healthAttribute;
        var targetAttrs = target.GetComponent<AttributeSet>();
        if (targetAttrs == null)
            return new HpCheckData(preHp: -1f, hpAttr, targetAttrs: null);

        float preHp = targetAttrs.GetAttributeValue(hpAttr);
        return new HpCheckData(preHp, hpAttr, targetAttrs);
    }
    private static void EmitDamagedTaken(
        AbilitySystem sourceSystem,
        GameplayEffect damageEffect,
        GameObject target,
        AbilitySpec sourceSpec,
        GameObject causer,
        HpCheckData hpCheck)
    {
        if (!hpCheck.IsValid) return;

        float postHp = hpCheck.TargetAttrs.GetAttributeValue(hpCheck.HpAttr);
        if (postHp >= hpCheck.PreHp) return; // 실제 감소 없으면 발행 안 함

        CombatHitAudioRouter.PlayImpact(
            sourceSystem,
            sourceSpec,
            damageEffect,
            target,
            causer);

        var targetSystem = target.GetComponent<AbilitySystem>();
        if (targetSystem == null) return;
        if (targetSystem.DamagedTag == null) return;

        targetSystem.SendGameplayEvent(targetSystem.DamagedTag, new AbilityEventData
        {
            AbilitySystem = targetSystem,
            Spec = sourceSpec,
            Instigator = causer,
            Target = target,
            WorldPosition = target.transform.position,
            Causer = causer
        });
    }
    private static void TryEmitKillConfirmed(
        AbilitySystem system,
        AbilitySpec spec,
        GameObject target,
        GameObject causer,
        HpCheckData hpCheck)
    {
        if (system.KillConfirmedTag == null) return;
        if (!hpCheck.IsValid) return;

        float postHp = hpCheck.TargetAttrs.GetAttributeValue(hpCheck.HpAttr);
        if (postHp > 0f) return;

        system.SendGameplayEvent(system.KillConfirmedTag, new AbilityEventData
        {
            AbilitySystem = system,
            Spec = spec,
            Instigator = system.gameObject,
            Target = target,
            WorldPosition = target.transform.position,
            Causer = causer
        });
    }

    // ---- Post systems -----------------------------------------------------------------------

    private static void ApplyStagger(
        GameObject target,
        float finalStaggerBuildUp,
        GameObject instigator,
        GameObject causer)
    {
        if (finalStaggerBuildUp <= 0f) return;
        if (IsStaggerSuppressed(target)) return;

        var stagger = target.GetComponent<StaggerGaugeSystem>();
        if (stagger == null) return;

        stagger.AddBuildUp(finalStaggerBuildUp, instigator: instigator, causer: causer);
    }

    /// <summary>
    /// 책임 :
    /// - 대상이 현재 stagger buildup을 무시해야 하는 상태인지 판정한다.
    /// - 패턴 전용 stagger 면역과 그로기 진행 중 상태를 함께 처리해 중복 buildup을 막는다.
    /// </summary>
    private static bool IsStaggerSuppressed(GameObject target)
    {
        if (target == null)
            return false;

        if (s_groggyTag == null)
            s_groggyTag = Resources.Load<GameplayTag>(GroggyTagResourcePath);

        if (s_staggerImmuneTag == null)
            s_staggerImmuneTag = Resources.Load<GameplayTag>(StaggerImmuneTagResourcePath);

        var tagSystem = target.GetComponent<TagSystem>();
        if (tagSystem == null)
            return false;

        if (s_groggyTag != null && tagSystem.HasTag(s_groggyTag))
            return true;

        if (s_staggerImmuneTag != null && tagSystem.HasTag(s_staggerImmuneTag))
            return true;

        return false;
    }

    private static readonly List<ElementDamageResult> s_AutoResolvedElements = new(8);
#if UNITY_EDITOR
    private static bool s_warnedLegacyElementBuildUpsIgnored;
#endif

    private static readonly List<ElementDamageResult> s_resolvedElements = new(8);
    private static void ApplyElements(
        GameObject target,
        IReadOnlyList<ElementDamageResult> elementBuildUps,
        GameObject instigator,
        GameObject causer)
    {
        if (target == null) return;

        var gaugeSystem = target.GetComponent<ElementGaugeSystem>();
        if (gaugeSystem == null) return;

        // Legacy path intentionally ignored.
        // 과거에는 스킬/투사체가 elementBuildUps 파라미터로 속성 누적량을 직접 전달했지만,
        // 현재는 공격자의 AttributeSet + ElementOffenseSource 를 기준으로
        // CombatDamageAction 에서 속성 누적량을 일괄 계산한다.
        // 따라서 전달된 elementBuildUps 는 의도적으로 무시한다.
        // 레거시 생산자(ability/projcetile 쪽 element formula 생성 코드) 정리 후
        // 이 파라미터 자체를 API 에서 제거할 예정.
#if UNITY_EDITOR
        if (!s_warnedLegacyElementBuildUpsIgnored &&
            elementBuildUps != null &&
            elementBuildUps.Count > 0)
        {
            s_warnedLegacyElementBuildUpsIgnored = true;
            Debug.LogWarning(
                "[CombatDamageAction] Legacy elementBuildUps parameter was provided but is ignored. " +
                "Element build-up is now resolved centrally from attacker attributes. " +
                "Remove old element formula producers from ability / projectile code.",
                causer != null ? causer : target);
        }
#endif

        var resolved = ElementBuildUpResolver.Evaluate(instigator, target, s_resolvedElements);
        if (resolved == null || resolved.Count == 0) return;

        for (int i = 0; i < resolved.Count; i++)
        {
            var e = resolved[i];
            if (e.elementType == null) continue;
            if (e.damage <= 0f) continue;

            gaugeSystem.AddBuildUp(
                e.elementType,
                e.damage,
                instigator: instigator,
                causer: causer);
        }
    }

    // ---- Hit confirmed ----------------------------------------------------------------------

    private static void EmitHitConfirmed(
        AbilitySystem system,
        AbilitySpec spec,
        GameObject target,
        GameObject causer,
        GameplayTag hitConfirmedTag,
        Vector3 hitWorldPosition,
        bool isCriticalHit)
    {
        var resolvedHitConfirmedTag = ResolveHitConfirmedTag(hitConfirmedTag);
        if (resolvedHitConfirmedTag == null)
            return;

        system.SendGameplayEvent(resolvedHitConfirmedTag, new AbilityEventData
        {
            AbilitySystem = system,
            Spec = spec,
            Instigator = system.gameObject,
            Target = target,
            WorldPosition = hitWorldPosition != Vector3.zero ? hitWorldPosition : target.transform.position,
            Causer = causer,
            IsCriticalHit = isCriticalHit
        });
    }

    /// <summary>
    /// 책임 :
    /// - 개별 공격/스킬이 hit confirm 태그를 지정하지 않은 경우 공용 Event.HitConfirm 태그를 기본값으로 해석한다.
    /// - 개별 자식 태그가 지정된 경우에는 그 태그를 그대로 유지해 세부 분기를 허용한다.
    /// </summary>
    private static GameplayTag ResolveHitConfirmedTag(GameplayTag hitConfirmedTag)
    {
        if (hitConfirmedTag != null)
            return hitConfirmedTag;

        if (s_defaultHitConfirmTag == null)
            s_defaultHitConfirmTag = Resources.Load<GameplayTag>(DefaultHitConfirmTagResourcePath);

        return s_defaultHitConfirmTag;
    }
}
