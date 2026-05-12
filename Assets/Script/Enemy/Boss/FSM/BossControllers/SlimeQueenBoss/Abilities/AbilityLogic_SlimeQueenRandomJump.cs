using System.Collections;
using UnityEngine;
using UnityGAS;

public sealed class AbilityLogic_SlimeQueenRandomJump : AbilityLogic
{
    /// <summary>슬라임 여왕 계열 보스가 바운더리 안의 랜덤 위치로 포물선 점프 이동합니다.</summary>
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        ISlimeQueenRandomJumpHost randomJumpHost = system != null ? system.GetComponent<ISlimeQueenRandomJumpHost>() : null;
        Component hostComponent = randomJumpHost as Component;
        if (randomJumpHost == null || hostComponent == null)
            yield break;

        if (!randomJumpHost.TryGetRandomJumpLandingPosition(out Vector3 landingPosition))
            yield break;

        randomJumpHost.FaceCurrentTarget();
        randomJumpHost.ShowJumpWarning(landingPosition);

        Vector3 startPosition = hostComponent.transform.position;
        startPosition.z = landingPosition.z;

        randomJumpHost.SetPatternMoveDamageBlocked(true);

        float elapsedSeconds = 0f;
        while (elapsedSeconds < randomJumpHost.JumpDurationSeconds)
        {
            if (IsAbilityCancelled(spec))
            {
                randomJumpHost.SnapToJumpLanding(startPosition);
                randomJumpHost.SetPatternMoveDamageBlocked(false);
                yield break;
            }

            elapsedSeconds += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedSeconds / randomJumpHost.JumpDurationSeconds);
            randomJumpHost.SetJumpPose(startPosition, landingPosition, normalizedTime);
            yield return null;
        }

        randomJumpHost.SnapToJumpLanding(landingPosition);
        randomJumpHost.SetPatternMoveDamageBlocked(false);
        randomJumpHost.ApplyJumpLandingDamage(spec, landingPosition);
        randomJumpHost.FaceCurrentTarget();
    }
}
