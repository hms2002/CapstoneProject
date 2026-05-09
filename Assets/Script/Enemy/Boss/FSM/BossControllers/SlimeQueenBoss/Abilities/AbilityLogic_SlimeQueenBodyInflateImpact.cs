using System.Collections;
using UnityEngine;
using UnityGAS;

public sealed class AbilityLogic_SlimeQueenBodyInflateImpact : AbilityLogic
{
    /// <summary>SlimeQueen이 원형 경고 후 몸 부풀림 충돌 피해와 넉백을 적용합니다.</summary>
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        SlimeQueen slimeQueen = system != null ? system.GetComponent<SlimeQueen>() : null;
        if (slimeQueen == null)
            yield break;

        slimeQueen.FaceCurrentTarget();
        slimeQueen.ShowBodyInflateWarning();

        if (slimeQueen.BodyInflateWarningSeconds > 0f)
            yield return WaitForSecondsUnlessCancelled(slimeQueen.BodyInflateWarningSeconds, spec);

        if (IsAbilityCancelled(spec))
            yield break;

        slimeQueen.ApplyBodyInflateImpact(spec);
        slimeQueen.FaceCurrentTarget();
    }
}
