using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public class AbilityLogic_WitchLightAllCandles : AbilityLogic
{
    // 이 클래스의 책임:
    // 페이즈 전환 패턴 동안 중앙 이동, 맵 전역 경고, 전 촛대 봉인, 제한 시간 종료 피해를 순서대로 처리한다.

    private const float MoveToCenterDuration = 0.45f;
    private const float RelightDeadlineSeconds = 8f;
    private const float ShieldBreakWaitGraceSeconds = 1.5f;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        Witch witch = system != null ? system.GetComponent<Witch>() : null;
        if (witch == null)
            yield break;

        witch.PlayPatternAttackMotion();
        witch.MoveToPhaseTransitionCenter(MoveToCenterDuration);
        witch.ActivateShield();
        witch.EnableStaggerImmuneDuringPhaseTransition();

        yield return new WaitForSeconds(MoveToCenterDuration);

        Vector3 center = witch.GetPhaseTransitionCenter();
        witch.SpeakSituation(BossSpeechSituationEnum.UltimateWarning);
        witch.ShowMapWideWarning(center, RelightDeadlineSeconds);
        witch.SealAllCandles();
        float deadlineTime = Time.time + RelightDeadlineSeconds;
        bool allCandlesRelit = false;

        while (Time.time < deadlineTime)
        {
            if (!witch.HasAnySealedCandles())
            {
                allCandlesRelit = true;
                break;
            }

            yield return null;
        }

        if (allCandlesRelit)
        {
            float shieldBreakDeadline = Time.time + ShieldBreakWaitGraceSeconds;

            while (witch.ShieldController != null && witch.ShieldController.HasShield && Time.time < shieldBreakDeadline)
                yield return null;

            if (witch.ShieldController != null && witch.ShieldController.HasShield)
                witch.BreakShield();

            witch.DisableStaggerImmuneDuringPhaseTransition();
            witch.ApplyGroggyStatus();
            yield break;
        }

        if (witch.HasAnySealedCandles())
        {
            witch.DisableStaggerImmuneDuringPhaseTransition();
            witch.ClearShield();
            witch.ApplyMapWideDamage(initialTarget);
        }
    }
}
