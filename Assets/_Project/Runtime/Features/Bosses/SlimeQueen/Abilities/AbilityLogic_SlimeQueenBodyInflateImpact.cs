using System.Collections;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 슬라임 여왕 계열 보스의 몸 부풀림 충격 패턴과 충격 시작 사운드를 실행한다.
/// </summary>
public sealed class AbilityLogic_SlimeQueenBodyInflateImpact : AbilityLogic
{
    private const float BodyInflateAttackAnimationHoldSeconds = 2f;

    [Header("Sound")]
    [SerializeField] private SoundRef bodyInflateSound = SoundRef.FromKey("sound_slimeQueen_Bigger");

    /// <summary>슬라임 여왕 계열 보스가 원형 경고 후 몸 부풀림 충돌 효과를 적용합니다.</summary>
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        ISlimeQueenBodyInflateHost slimeQueen = system != null ? system.GetComponent<ISlimeQueenBodyInflateHost>() : null;
        if (slimeQueen == null)
            yield break;

        SlimeQueenPhaseTwoBase phaseTwoHost = system != null ? system.GetComponent<SlimeQueenPhaseTwoBase>() : null;
        if (phaseTwoHost != null)
            phaseTwoHost.SetPassiveContactDamageBlocked(true);

        SlimeQueen phaseOneQueen = slimeQueen as SlimeQueen;
        SlimeQueenP2Short phaseTwoShortQueen = slimeQueen as SlimeQueenP2Short;
        bool shouldHoldBodyInflateAnimation = phaseOneQueen != null || phaseTwoShortQueen != null;

        try
        {
            slimeQueen.FaceCurrentTarget();
            if (phaseOneQueen != null)
                phaseOneQueen.TriggerBodyInflateReadyAnimation();
            if (phaseTwoShortQueen != null)
                phaseTwoShortQueen.TriggerBodyInflateReadyAnimation();

            slimeQueen.ShowBodyInflateWarning();

            if (slimeQueen.BodyInflateWarningSeconds > 0f)
                yield return WaitForSecondsUnlessCancelled(slimeQueen.BodyInflateWarningSeconds, spec);

            if (IsAbilityCancelled(spec))
                yield break;

            if (phaseOneQueen != null)
                phaseOneQueen.BeginBodyInflateImpactAnimation();
            if (phaseTwoShortQueen != null)
                phaseTwoShortQueen.BeginBodyInflateImpactAnimation();

            Component hostComponent = slimeQueen as Component;
            if (hostComponent != null)
            {
                SlimeQueenPresentationAudioUtility.PlaySound(
                    bodyInflateSound,
                    hostComponent.gameObject,
                    hostComponent.transform.position,
                    this,
                    initialTarget);
            }

            slimeQueen.ApplyBodyInflateImpact(spec);

            if (shouldHoldBodyInflateAnimation && BodyInflateAttackAnimationHoldSeconds > 0f)
                yield return WaitForSecondsUnlessCancelled(BodyInflateAttackAnimationHoldSeconds, spec);

            if (IsAbilityCancelled(spec))
                yield break;

            if (phaseOneQueen != null)
                phaseOneQueen.EndBodyInflateImpactAnimation();
            if (phaseTwoShortQueen != null)
                phaseTwoShortQueen.EndBodyInflateImpactAnimation();

            slimeQueen.FaceCurrentTarget();
        }
        finally
        {
            slimeQueen.CleanupBodyInflatePresentation();

            if (phaseOneQueen != null)
            {
                phaseOneQueen.ResetBodyInflateReadyAnimation();
                phaseOneQueen.EndBodyInflateImpactAnimation();
            }

            if (phaseTwoShortQueen != null)
            {
                phaseTwoShortQueen.ResetBodyInflateReadyAnimation();
                phaseTwoShortQueen.EndBodyInflateImpactAnimation();
            }

            if (phaseTwoHost != null)
                phaseTwoHost.SetPassiveContactDamageBlocked(false);
        }
    }
}
