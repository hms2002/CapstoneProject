using System.Collections;
using UnityEngine;
using UnityGAS;

public sealed class AbilityLogic_SlimeQueenRepeatedSlam : AbilityLogic
{
    private const float PostLandingPauseSeconds = 0.5f;

    /// <summary>슬라임 여왕 2페이즈 개체가 플레이어 위치를 연속 조준해 체공/급강하 내려찍기를 반복합니다.</summary>
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        SlimeQueenPhaseTwoBase slimeQueen = system != null ? system.GetComponent<SlimeQueenPhaseTwoBase>() : null;
        if (slimeQueen == null)
            yield break;

        int slamCount = slimeQueen.Phase2SlamCount;
        for (int slamIndex = 0; slamIndex < slamCount; slamIndex++)
        {
            if (!slimeQueen.TryGetPhase2SlamLandingPosition(initialTarget, out Vector3 landingPosition))
                yield break;

            slimeQueen.FaceCurrentTarget();
            slimeQueen.ShowPhase2SlamWarning(landingPosition);

            Vector3 startPosition = slimeQueen.transform.position;
            startPosition.z = landingPosition.z;

            SlimeQueenBossBase pitFallBlockOwner = slimeQueen is SlimeQueenP2Short ? slimeQueen : null;
            slimeQueen.SetPatternMoveDamageBlocked(true);
            if (pitFallBlockOwner != null)
                pitFallBlockOwner.PushPitFallTriggerBlock();

            try
            {
                float elapsedSeconds = 0f;
                while (elapsedSeconds < slimeQueen.Phase2SlamIntervalSeconds)
                {
                    if (IsAbilityCancelled(spec))
                    {
                        slimeQueen.SnapToPhase2SlamLanding(startPosition);
                        yield break;
                    }

                    elapsedSeconds += Time.deltaTime;
                    float normalizedTime = Mathf.Clamp01(elapsedSeconds / slimeQueen.Phase2SlamIntervalSeconds);
                    slimeQueen.SetPhase2SlamPose(startPosition, landingPosition, normalizedTime);
                    yield return null;
                }

                slimeQueen.SnapToPhase2SlamLanding(landingPosition);
            }
            finally
            {
                slimeQueen.SetPatternMoveDamageBlocked(false);
                if (pitFallBlockOwner != null)
                    pitFallBlockOwner.PopPitFallTriggerBlock();
            }

            slimeQueen.ApplyPhase2SlamDamage(spec, landingPosition);
            slimeQueen.FaceCurrentTarget();

            if (slamIndex >= slamCount - 1)
                continue;

            float postLandingElapsedSeconds = 0f;
            while (postLandingElapsedSeconds < PostLandingPauseSeconds)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                postLandingElapsedSeconds += Time.deltaTime;
                yield return null;
            }
        }
    }
}
