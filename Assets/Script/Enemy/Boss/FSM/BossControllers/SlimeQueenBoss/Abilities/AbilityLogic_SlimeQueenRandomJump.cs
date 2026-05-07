using System.Collections;
using UnityEngine;
using UnityGAS;

public sealed class AbilityLogic_SlimeQueenRandomJump : AbilityLogic
{
    /// <summary>SlimeQueen이 바운더리 안의 랜덤 위치로 포물선 점프 이동합니다.</summary>
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        SlimeQueen slimeQueen = system != null ? system.GetComponent<SlimeQueen>() : null;
        if (slimeQueen == null)
            yield break;

        if (!slimeQueen.TryGetRandomJumpLandingPosition(out Vector3 landingPosition))
            yield break;

        slimeQueen.FaceCurrentTarget();
        slimeQueen.ShowJumpWarning(landingPosition);

        Vector3 startPosition = slimeQueen.transform.position;
        startPosition.z = landingPosition.z;

        slimeQueen.SetPatternMoveDamageBlocked(true);

        float elapsedSeconds = 0f;
        while (elapsedSeconds < slimeQueen.JumpDurationSeconds)
        {
            if (IsAbilityCancelled(spec))
            {
                slimeQueen.SnapToJumpLanding(startPosition);
                slimeQueen.SetPatternMoveDamageBlocked(false);
                yield break;
            }

            elapsedSeconds += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedSeconds / slimeQueen.JumpDurationSeconds);
            slimeQueen.SetJumpPose(startPosition, landingPosition, normalizedTime);
            yield return null;
        }

        slimeQueen.SnapToJumpLanding(landingPosition);
        slimeQueen.SetPatternMoveDamageBlocked(false);
        slimeQueen.ApplyJumpLandingDamage(spec, landingPosition);
        slimeQueen.FaceCurrentTarget();
    }
}
