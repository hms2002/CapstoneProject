using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 2페이즈 두 슬라임 여왕의 캐슬링 이동과 실제 교차 이동 시작 사운드를 실행한다.
/// </summary>
public sealed class AbilityLogic_SlimeQueenCastling : AbilityLogic
{
    private const float ShortQueenSpeechDelaySeconds = 0.7f;

    [Header("Sound")]
    [SerializeField] private SoundRef castlingSound = SoundRef.FromKey("sound_SlimeQueen_Castling");

    /// <summary>2페이즈 근거리/원거리 슬라임 여왕이 발동 순간 위치를 기준으로 서로의 위치를 교환합니다.</summary>
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        SlimeQueenPhaseTwoBase slimeQueen = system != null ? system.GetComponent<SlimeQueenPhaseTwoBase>() : null;
        if (slimeQueen == null)
            yield break;

        if (!slimeQueen.TryBeginCastlingPattern(out SlimeQueenPhaseTwoBase.CastlingContext context))
            yield break;

        HashSet<GameObject> damagedTargets = new HashSet<GameObject>();

        try
        {
            slimeQueen.ShowCastlingWarning(context);
            yield return PlayCastlingWarningSpeechSequence(slimeQueen, context, spec);

            if (IsAbilityCancelled(spec) || !slimeQueen.CanContinueCastlingPattern(context, out _))
                yield break;

            slimeQueen.ClearCastlingWarnings();
            SlimeQueenPresentationAudioUtility.PlaySound(
                castlingSound,
                slimeQueen.gameObject,
                slimeQueen.transform.position,
                this,
                initialTarget);

            float traveledDistance = 0f;
            while (traveledDistance < context.Distance)
            {
                if (IsAbilityCancelled(spec) || !slimeQueen.CanContinueCastlingPattern(context, out _))
                    yield break;

                traveledDistance += slimeQueen.CastlingRushSpeed * Time.deltaTime;
                float normalizedTime = context.Distance <= 0.0001f
                    ? 1f
                    : Mathf.Clamp01(traveledDistance / context.Distance);

                context.ShortQueen.SetCastlingPose(
                    context.ShortStartPosition,
                    context.LongStartPosition,
                    normalizedTime);

                context.LongQueen.SetCastlingPose(
                    context.LongStartPosition,
                    context.ShortStartPosition,
                    normalizedTime);

                context.ShortQueen.TryApplyCastlingDamage(system, spec, damagedTargets);
                context.LongQueen.TryApplyCastlingDamage(system, spec, damagedTargets);
                yield return null;
            }

            if (IsAbilityCancelled(spec) || !slimeQueen.CanContinueCastlingPattern(context, out _))
                yield break;

            context.ShortQueen.SnapToCastlingDestination(context.LongStartPosition);
            context.LongQueen.SnapToCastlingDestination(context.ShortStartPosition);
        }
        finally
        {
            slimeQueen.EndCastlingPattern(context);
        }
    }

    /// <summary>씬 전환이나 강제 정리 시 캐슬링 경고와 합동 패턴 잠금을 제거합니다.</summary>
    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        SlimeQueenPhaseTwoBase slimeQueen = system != null ? system.GetComponent<SlimeQueenPhaseTwoBase>() : null;
        if (slimeQueen == null)
            return;

        if (slimeQueen.TryResolveCastlingPair(out SlimeQueenP2Short shortQueen, out SlimeQueenP2Long longQueen))
        {
            shortQueen.ForceCleanupCastlingPattern();
            longQueen.ForceCleanupCastlingPattern();
            return;
        }

        slimeQueen.ForceCleanupCastlingPattern();
    }

    /// <summary>캐슬링 경고 시간 안에서 Long 요청 대사와 Short 응답 대사를 순서대로 출력합니다.</summary>
    private static IEnumerator PlayCastlingWarningSpeechSequence(
        SlimeQueenPhaseTwoBase slimeQueen,
        SlimeQueenPhaseTwoBase.CastlingContext context,
        AbilitySpec spec)
    {
        float warningSeconds = slimeQueen.CastlingWarningSeconds;
        if (warningSeconds <= 0f)
            yield break;

        float replyDelaySeconds = Mathf.Min(ShortQueenSpeechDelaySeconds, warningSeconds);
        context.LongQueen.TryShowCastlingSpeech(BossSpeechSituationEnum.SlimeQueenCastlingRequest, replyDelaySeconds);

        if (replyDelaySeconds > 0f)
            yield return WaitForSecondsUnlessCancelled(replyDelaySeconds, spec);

        if (IsAbilityCancelled(spec) || !slimeQueen.CanContinueCastlingPattern(context, out _))
            yield break;

        float remainingWarningSeconds = Mathf.Max(0f, warningSeconds - replyDelaySeconds);
        context.ShortQueen.TryShowCastlingSpeech(BossSpeechSituationEnum.SlimeQueenCastlingReply, remainingWarningSeconds);

        if (remainingWarningSeconds > 0f)
            yield return WaitForSecondsUnlessCancelled(remainingWarningSeconds, spec);
    }
}
