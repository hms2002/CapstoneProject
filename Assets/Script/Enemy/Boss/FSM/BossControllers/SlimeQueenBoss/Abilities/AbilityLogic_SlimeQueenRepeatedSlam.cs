using System.Collections;
using UnityEngine;
using UnityGAS;

public sealed class AbilityLogic_SlimeQueenRepeatedSlam : AbilityLogic
{
    /// <summary>SlimeQueen이 플레이어 위치를 연속 조준해 포물선 내려찍기를 반복합니다.</summary>
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        SlimeQueen slimeQueen = system != null ? system.GetComponent<SlimeQueen>() : null;
        if (slimeQueen == null)
            yield break;

        for (int slamIndex = 0; slamIndex < slimeQueen.Phase2SlamCount; slamIndex++)
        {
            if (!slimeQueen.TryGetPhase2SlamLandingPosition(initialTarget, out Vector3 landingPosition))
                yield break;

            slimeQueen.FaceCurrentTarget();
            slimeQueen.ShowPhase2SlamWarning(landingPosition);

            Vector3 startPosition = slimeQueen.transform.position;
            startPosition.z = landingPosition.z;

            slimeQueen.SetPatternMoveDamageBlocked(true);

            float elapsedSeconds = 0f;
            while (elapsedSeconds < slimeQueen.Phase2SlamIntervalSeconds)
            {
                if (IsAbilityCancelled(spec))
                {
                    slimeQueen.SnapToPhase2SlamLanding(startPosition);
                    slimeQueen.SetPatternMoveDamageBlocked(false);
                    yield break;
                }

                elapsedSeconds += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsedSeconds / slimeQueen.Phase2SlamIntervalSeconds);
                slimeQueen.SetPhase2SlamPose(startPosition, landingPosition, normalizedTime);
                yield return null;
            }

            slimeQueen.SnapToPhase2SlamLanding(landingPosition);
            slimeQueen.SetPatternMoveDamageBlocked(false);
            slimeQueen.ApplyPhase2SlamDamage(spec, landingPosition);
            slimeQueen.FaceCurrentTarget();
        }
    }
}
