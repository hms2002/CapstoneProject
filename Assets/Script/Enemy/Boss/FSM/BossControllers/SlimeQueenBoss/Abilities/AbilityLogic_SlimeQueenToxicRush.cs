using System.Collections;
using UnityEngine;
using UnityGAS;

public sealed class AbilityLogic_SlimeQueenToxicRush : AbilityLogic
{
    /// <summary>근거리 슬라임 여왕이 플레이어 방향으로 독성 돌진을 반복하고 독구름 트레일을 남깁니다.</summary>
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        SlimeQueenP2Short slimeQueen = system != null ? system.GetComponent<SlimeQueenP2Short>() : null;
        if (slimeQueen == null)
            yield break;

        try
        {
            for (int rushIndex = 0; rushIndex < slimeQueen.ToxicRushRepeatCount; rushIndex++)
            {
                if (!slimeQueen.TryBuildToxicRushSegment(initialTarget, out SlimeQueenP2Short.ToxicRushSegment segment))
                    yield break;

                slimeQueen.FaceCurrentTarget();
                slimeQueen.ShowToxicRushWarning(segment);

                if (slimeQueen.ToxicRushWarningSeconds > 0f)
                    yield return WaitForSecondsUnlessCancelled(slimeQueen.ToxicRushWarningSeconds, spec);

                if (IsAbilityCancelled(spec))
                    yield break;

                slimeQueen.BeginToxicRushAnimation();
                slimeQueen.ClearToxicRushWarnings();
                slimeQueen.BeginToxicRushTrail(segment.Start);

                float traveledDistance = 0f;
                while (traveledDistance < segment.Length)
                {
                    if (IsAbilityCancelled(spec))
                        yield break;

                    traveledDistance += slimeQueen.ToxicRushSpeed * Time.deltaTime;
                    slimeQueen.SetToxicRushPose(segment, traveledDistance);
                    yield return null;
                }

                slimeQueen.FinishToxicRushSegment(segment);
                slimeQueen.EndToxicRushAnimation();
                slimeQueen.FaceCurrentTarget();

                if (slimeQueen.ToxicRushIntervalSeconds > 0f && rushIndex < slimeQueen.ToxicRushRepeatCount - 1)
                    yield return WaitForSecondsUnlessCancelled(slimeQueen.ToxicRushIntervalSeconds, spec);

                if (IsAbilityCancelled(spec))
                    yield break;
            }
        }
        finally
        {
            slimeQueen.EndToxicRushAnimation();
            slimeQueen.CleanupToxicRushPresentation();
        }
    }

    /// <summary>씬 전환이나 강제 정리 시 근거리 슬라임 여왕의 독성 돌진 경고 표시를 제거합니다.</summary>
    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        SlimeQueenP2Short slimeQueen = system != null ? system.GetComponent<SlimeQueenP2Short>() : null;
        if (slimeQueen == null)
            return;

        slimeQueen.EndToxicRushAnimation();
        slimeQueen.CleanupToxicRushPresentation();
    }
}
