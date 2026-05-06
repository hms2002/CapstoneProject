using System.Collections;
using UnityEngine;
using UnityGAS;

public sealed class AbilityLogic_SlimeQueenBishopLineBlast : AbilityLogic
{
    /// <summary>SlimeQueen이 자신을 중심으로 4방향 Bishop 물기둥 패턴을 실행합니다.</summary>
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        SlimeQueen slimeQueen = system != null ? system.GetComponent<SlimeQueen>() : null;
        if (slimeQueen == null)
            yield break;

        slimeQueen.FaceCurrentTarget();
        slimeQueen.ShowBishopLineWarnings();

        if (slimeQueen.BishopLineWarningSeconds > 0f)
            yield return WaitForSecondsUnlessCancelled(slimeQueen.BishopLineWarningSeconds, spec);

        if (IsAbilityCancelled(spec))
            yield break;

        slimeQueen.FireBishopLineBlasts(spec);

        if (slimeQueen.BishopLineBlastViewSeconds > 0f)
            yield return WaitForSecondsUnlessCancelled(slimeQueen.BishopLineBlastViewSeconds, spec);
    }
}
