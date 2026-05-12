using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public sealed class AbilityLogic_SlimeQueenCrossWaterPillar : AbilityLogic
{
    /// <summary>원거리 슬라임 여왕이 자신 중심 네 방향 경고선 후 물기둥 공격을 실행합니다.</summary>
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        SlimeQueenP2Long slimeQueen = system != null ? system.GetComponent<SlimeQueenP2Long>() : null;
        if (slimeQueen == null)
            yield break;

        List<SlimeQueenP2Long.CrossWaterPillarSegment> segments = new List<SlimeQueenP2Long.CrossWaterPillarSegment>(4);
        slimeQueen.BuildCrossWaterPillarSegments(segments);
        if (segments.Count == 0)
            yield break;

        try
        {
            slimeQueen.FaceCurrentTarget();
            slimeQueen.ShowCrossWaterPillarWarnings(segments);

            if (slimeQueen.CrossWaterPillarWarningSeconds > 0f)
                yield return WaitForSecondsUnlessCancelled(slimeQueen.CrossWaterPillarWarningSeconds, spec);

            if (IsAbilityCancelled(spec))
                yield break;

            slimeQueen.FireCrossWaterPillars(system, spec, segments);
            slimeQueen.FaceCurrentTarget();

            if (slimeQueen.CrossWaterPillarBlastViewSeconds > 0f)
                yield return WaitForSecondsUnlessCancelled(slimeQueen.CrossWaterPillarBlastViewSeconds, spec);
        }
        finally
        {
            slimeQueen.CleanupCrossWaterPillarPresentation();
        }
    }

    /// <summary>씬 전환이나 강제 정리 시 원거리 슬라임 여왕의 물기둥 경고 표시를 제거합니다.</summary>
    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        SlimeQueenP2Long slimeQueen = system != null ? system.GetComponent<SlimeQueenP2Long>() : null;
        if (slimeQueen == null)
            return;

        slimeQueen.CleanupCrossWaterPillarPresentation();
    }
}
