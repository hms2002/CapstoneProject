using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 실제 타격 성공 시 어떤 impact sound를 재생할지 중앙 규칙으로 결정한다.
/// - 플레이어 타깃이면 PlayerHitFeedback2D의 전용 피격음을 우선하고, 아니면 GE_Damage의 공통 피격음을 기본값으로 사용한다.
/// - 공통 효과에 기본값이 없을 때만 공격 AbilityDefinition의 impactSound를 후순위 fallback으로 사용한다.
/// </summary>
public static class CombatHitAudioRouter
{
    private const string DefaultDeadTagResourcePath = "Tags/State.Dead";
    private static GameplayTag s_defaultDeadTag;

    /// <summary>
    /// 책임 :
    /// - 실제 HP 감소가 확인된 타격에 대해 최종 재생할 SoundRef를 선택하고 재생한다.
    /// - 공격자/타깃/위치 문맥을 SoundManager가 해석할 수 있도록 공통 SoundPlaybackContext를 구성한다.
    /// </summary>
    public static void PlayImpact(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GameObject target,
        GameObject causer)
    {
        if (target == null)
            return;

        if (IsDeadTarget(target))
            return;

        SoundRef resolved = ResolveImpactSound(spec, damageEffect, target);
        if (!resolved.IsSet)
            return;

        SoundManager.EnsureInstance().Play(resolved, new SoundPlaybackContext
        {
            Instigator = system != null ? system.gameObject : null,
            Causer = causer,
            Target = target,
            Position = target.transform.position,
            SourceObject = damageEffect != null ? damageEffect : (spec != null ? spec.Definition : null)
        });
    }

    /// <summary>
    /// 책임 :
    /// - 타깃이 플레이어면 플레이어 전용 피격음을 선택한다.
    /// - 플레이어가 아니면 GE_Damage의 공통 피격음을 우선 사용하고, 비어 있을 때만 공격 정의 impactSound를 사용한다.
    /// - 오디오 선택 규칙을 CombatDamageAction 밖으로 분리해 재사용 가능하게 유지한다.
    /// </summary>
    private static SoundRef ResolveImpactSound(AbilitySpec spec, GameplayEffect damageEffect, GameObject target)
    {
        if (target != null)
        {
            PlayerHitFeedback2D playerHitFeedback = target.GetComponent<PlayerHitFeedback2D>();
            if (playerHitFeedback != null && playerHitFeedback.PlayerHitSound.IsSet)
                return playerHitFeedback.PlayerHitSound;
        }

        if (damageEffect != null && damageEffect.audioOnExecute.IsSet)
            return damageEffect.audioOnExecute;

        AbilityDefinition def = spec != null ? spec.Definition : null;
        return def != null ? def.impactSound : default;
    }

    /// <summary>
    /// 책임 :
    /// - 사망 태그가 붙은 타깃에 대해서는 추가 피격 사운드를 재생하지 않도록 차단한다.
    /// - 사망 직후 남아 있는 후속 타격 연출이 청각적으로 이어지지 않게 방어한다.
    /// </summary>
    private static bool IsDeadTarget(GameObject target)
    {
        if (target == null)
            return true;

        if (s_defaultDeadTag == null)
            s_defaultDeadTag = Resources.Load<GameplayTag>(DefaultDeadTagResourcePath);

        if (s_defaultDeadTag == null)
            return false;

        TagSystem tags = target.GetComponent<TagSystem>();
        return tags != null && tags.HasTag(s_defaultDeadTag);
    }
}
