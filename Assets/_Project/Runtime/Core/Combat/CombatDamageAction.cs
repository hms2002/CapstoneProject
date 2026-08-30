using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 생성 시점에 확정된 피해 payload를 대상에게 적용하는 중앙 유틸리티다.
/// - HP 피해는 GameplayEffect로, 스태거/원소 누적은 대상 gauge system으로 라우팅한다.
/// - payload가 원소 snapshot을 제공하면 그 값을 사용하고, legacy 직접 호출만 공격자 현재 상태 조회로 후방 호환한다.
/// </summary>
public static class CombatDamageAction
{
    private const string DefaultHitConfirmTagResourcePath = "Tags/Event.HitConfirm";
    private const string GroggyTagResourcePath = "Tags/State.Status.Groggy";
    private const string StaggerImmuneTagResourcePath = "Tags/State.Status.StaggerImmune";

    private static GameplayTag s_defaultHitConfirmTag;
    private static GameplayTag s_groggyTag;
    private static GameplayTag s_staggerImmuneTag;
    private static readonly List<ElementDamageResult> s_resolvedElements = new(8);
    private static readonly SoundRef PlayerEvadeSound = SoundRef.FromKey("sound_player_Evade");

    public static void ApplyDamageAndEmitHit(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GameObject target,
        float finalHpDamage,
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
            finalKnockbackImpulse: 0f,
            hitConfirmedTag: hitConfirmedTag,
            hitWorldPosition: hitWorldPosition,
            causer: causer,
            isCriticalHit: isCriticalHit);
    }

    public static void ApplyDamageAndEmitHit(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GE_Knockback_Spec knockbackEffect,
        GameObject target,
        float finalHpDamage,
        float finalStaggerBuildUp,
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
            finalKnockbackImpulse: 0f,
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
        GameplayTag hitConfirmedTag,
        Vector3 hitWorldPosition,
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
            finalKnockbackImpulse: 0f,
            hitConfirmedTag,
            hitWorldPosition,
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
        float finalKnockbackImpulse,
        GameplayTag hitConfirmedTag,
        Vector3 hitWorldPosition,
        GameObject causer,
        bool isCriticalHit = false,
        ElementDamageResult[] elementBuildUps = null,
        bool hasResolvedElementBuildUps = false,
        HitImpactCueKind hitImpactCueKind = HitImpactCueKind.Default,
        bool emitHitConfirmed = true)
    {
        ApplyDamageAndEmitHitInternal(
            system,
            spec,
            damageEffect,
            knockbackEffect,
            target,
            finalHpDamage,
            finalStaggerBuildUp,
            finalKnockbackImpulse,
            hitConfirmedTag,
            hitWorldPosition,
            causer,
            isCriticalHit,
            elementBuildUps,
            hasResolvedElementBuildUps,
            hitImpactCueKind,
            emitHitConfirmed);
    }

    private static void ApplyDamageAndEmitHitInternal(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GE_Knockback_Spec knockbackEffect,
        GameObject target,
        float finalHpDamage,
        float finalStaggerBuildUp,
        float finalKnockbackImpulse,
        GameplayTag hitConfirmedTag,
        Vector3 hitWorldPosition,
        GameObject causer,
        bool isCriticalHit,
        ElementDamageResult[] elementBuildUps,
        bool hasResolvedElementBuildUps,
        HitImpactCueKind hitImpactCueKind,
        bool emitHitConfirmed)
    {
        if (!Validate(system, damageEffect, target))
            return;

        if (CombatInvulnerabilityUtil.IsDamageSuppressed(target, damageEffect as GE_Damage_Spec))
            return;

        if (CombatEvasionUtil.TryRollEvasion(target))
        {
            DamagePopupPlayback.ShowText("EVADE", target.transform.position);
            TryPlayPlayerEvadeSound(target);
            return;
        }

        var runner = system.EffectRunner;
        var geDamage = damageEffect as GE_Damage_Spec;
        var hpCheck = CaptureHpCheck(target, geDamage);

        finalHpDamage = CombatOutgoingDamageModifiers.Apply(
            new CombatOutgoingDamageContext(system, spec, target, finalHpDamage));

        var damageSpec = BuildDamageSpec(
            system,
            spec,
            damageEffect,
            geDamage,
            finalHpDamage,
            causer);

        ReserveFallbackDamagePopupSuppression(target, hpCheck, geDamage, finalHpDamage);
        runner.ApplyEffectSpec(damageSpec, target);

        TryShowHpDamagePopup(target, hpCheck, hitWorldPosition, isCriticalHit);

        EmitDamagedTaken(system, damageEffect, target, spec, causer, hpCheck);

        ApplyKnockbackEffect(
            system,
            spec,
            knockbackEffect,
            target,
            finalKnockbackImpulse,
            causer);

        TryEmitKillConfirmed(system, spec, target, causer, hpCheck);

        ApplyStagger(target, finalStaggerBuildUp, system.gameObject, causer);
        ApplyElements(target, system.gameObject, causer, elementBuildUps, hasResolvedElementBuildUps);

        if (emitHitConfirmed)
            EmitHitConfirmed(system, spec, target, causer, hitConfirmedTag, hitWorldPosition, isCriticalHit, hitImpactCueKind);
    }

    private static bool Validate(AbilitySystem system, GameplayEffect damageEffect, GameObject target)
    {
        if (system == null || damageEffect == null || target == null) return false;
        if (system.EffectRunner == null) return false;
        return true;
    }

    private static GameplayEffectSpec BuildDamageSpec(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GE_Damage_Spec geDamage,
        float finalHpDamage,
        GameObject causer)
    {
        var geSpec = system.MakeSpec(
            damageEffect,
            causer: causer,
            sourceObject: spec != null ? spec.Definition : null);

        if (geDamage != null && geDamage.damageKey != null)
            geSpec.SetSetByCallerMagnitude(geDamage.damageKey, finalHpDamage);

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

    // 책임: 피해 적용 전후 HP 차이를 계산하기 위한 대상 HP 스냅샷을 보관한다.
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

    private static HpCheckData CaptureHpCheck(GameObject target, GE_Damage_Spec geDamage)
    {
        if (geDamage == null || geDamage.healthAttribute == null)
            return default;

        var hpAttr = geDamage.healthAttribute;
        var targetAttrs = target.GetComponent<AttributeSet>();
        if (targetAttrs == null)
            return new HpCheckData(preHp: -1f, hpAttr, targetAttrs: null);

        float preHp = targetAttrs.GetAttributeValue(hpAttr);
        return new HpCheckData(preHp, hpAttr, targetAttrs);
    }

    /// <summary>플레이어가 공격을 회피했을 때만 회피 성공 사운드를 재생합니다.</summary>
    private static void TryPlayPlayerEvadeSound(GameObject target)
    {
        if (target == null || !target.CompareTag("Player"))
            return;

        SoundPlaybackUtility.Play(PlayerEvadeSound, target: target, position: target.transform.position);
    }

    /// <summary>
    /// 책임 :
    /// - AttributeSet 변경 이벤트가 동기적으로 발생하기 전에 fallback 데미지 팝업을 먼저 억제 예약한다.
    /// - CombatDamageAction 메타데이터 팝업과 DamagePopupListener2D fallback 팝업이 같은 타격에서 중복 표시되는 일을 막는다.
    /// </summary>
    private static void ReserveFallbackDamagePopupSuppression(
        GameObject target,
        HpCheckData hpCheck,
        GE_Damage_Spec geDamage,
        float requestedHpDamage)
    {
        if (target == null || !hpCheck.IsValid || requestedHpDamage <= 0f)
            return;

        float expectedDamage = requestedHpDamage;
        if (geDamage != null && geDamage.absorbShieldAttribute != null)
        {
            float shield = hpCheck.TargetAttrs.GetAttributeValue(geDamage.absorbShieldAttribute);
            expectedDamage = Mathf.Max(0f, expectedDamage - Mathf.Max(0f, shield));
        }

        expectedDamage = Mathf.Min(expectedDamage, Mathf.Max(0f, hpCheck.PreHp - hpCheck.HpAttr.minValue));
        if (expectedDamage <= 0f)
            return;

        DamagePopupDuplicateSuppressor.Register(target, expectedDamage);
    }

    /// <summary>
    /// 책임 :
    /// - 실제 HP 감소량이 확인된 전투 피해를 메타데이터가 포함된 데미지 팝업으로 표시한다.
    /// - Attribute 감소 listener fallback과 중복 표시되지 않도록 같은 피해를 짧게 suppress 등록한다.
    /// </summary>
    private static void TryShowHpDamagePopup(
        GameObject target,
        HpCheckData hpCheck,
        Vector3 hitWorldPosition,
        bool isCriticalHit)
    {
        if (!hpCheck.IsValid || target == null)
            return;

        float postHp = hpCheck.TargetAttrs.GetAttributeValue(hpCheck.HpAttr);
        float appliedDamage = Mathf.Max(0f, hpCheck.PreHp - postHp);
        if (appliedDamage <= 0f)
            return;

        Vector3 popupPosition = hitWorldPosition != Vector3.zero ? hitWorldPosition : target.transform.position;
        DamagePopupPlayback.Show(DamagePopupRequest.Damage(appliedDamage, popupPosition, isCriticalHit));
        DamagePopupDuplicateSuppressor.Register(target, appliedDamage);
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
        if (postHp >= hpCheck.PreHp) return;

        CombatActivityEvents.RaiseDamageApplied(
            sourceSystem != null ? sourceSystem.gameObject : causer,
            target,
            hpCheck.PreHp - postHp);

        CombatHitAudioPlayback.PlayImpact(
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

    private static void ApplyElements(
        GameObject target,
        GameObject instigator,
        GameObject causer,
        ElementDamageResult[] elementBuildUps,
        bool hasResolvedElementBuildUps)
    {
        if (target == null) return;

        var gaugeSystem = target.GetComponent<ElementGaugeSystem>();
        if (gaugeSystem == null) return;

        IReadOnlyList<ElementDamageResult> resolved = elementBuildUps;
        if (!hasResolvedElementBuildUps)
        {
            resolved = ElementBuildUpResolver.ResolveForApplication(
                instigator,
                target,
                s_resolvedElements);
        }

        if (resolved == null || resolved.Count == 0) return;

        for (int i = 0; i < resolved.Count; i++)
        {
            var element = resolved[i];
            if (element.elementType == null) continue;
            if (element.damage <= 0f) continue;

            gaugeSystem.AddBuildUp(
                element.elementType,
                element.damage,
                instigator: instigator,
                causer: causer);
        }
    }

    private static void EmitHitConfirmed(
        AbilitySystem system,
        AbilitySpec spec,
        GameObject target,
        GameObject causer,
        GameplayTag hitConfirmedTag,
        Vector3 hitWorldPosition,
        bool isCriticalHit,
        HitImpactCueKind hitImpactCueKind)
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
            IsCriticalHit = isCriticalHit,
            HitImpactCueKind = hitImpactCueKind
        });
    }

    private static GameplayTag ResolveHitConfirmedTag(GameplayTag hitConfirmedTag)
    {
        if (hitConfirmedTag != null)
            return hitConfirmedTag;

        if (s_defaultHitConfirmTag == null)
            s_defaultHitConfirmTag = Resources.Load<GameplayTag>(DefaultHitConfirmTagResourcePath);

        return s_defaultHitConfirmTag;
    }
}
