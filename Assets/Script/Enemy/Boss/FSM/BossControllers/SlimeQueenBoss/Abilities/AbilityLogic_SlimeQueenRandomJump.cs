using System.Collections;
using UnityEngine;
using UnityGAS;

public sealed class AbilityLogic_SlimeQueenRandomJump : AbilityLogic
{
    /// <summary>슬라임 여왕 계열 보스가 바운더리 안의 랜덤 위치 위로 이동한 뒤 체공/급강하합니다.</summary>
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
        SlimeQueen phaseOneQueen = randomJumpHost as SlimeQueen;
        SlimeQueenP2Long phaseTwoLongQueen = randomJumpHost as SlimeQueenP2Long;
        if (phaseOneQueen != null)
            phaseOneQueen.BeginRandomJumpAnimation();
        if (phaseTwoLongQueen != null)
            phaseTwoLongQueen.BeginRandomJumpAnimation();

        Vector3 startPosition = hostComponent.transform.position;
        startPosition.z = landingPosition.z;

        SlimeQueenBossBase pitFallBlockOwner = phaseOneQueen;
        randomJumpHost.SetPatternMoveDamageBlocked(true);
        if (pitFallBlockOwner != null)
            pitFallBlockOwner.PushPitFallTriggerBlock();

        try
        {
            float elapsedSeconds = 0f;
            while (elapsedSeconds < randomJumpHost.JumpDurationSeconds)
            {
                if (IsAbilityCancelled(spec))
                {
                    randomJumpHost.SnapToJumpLanding(startPosition);
                    yield break;
                }

                elapsedSeconds += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsedSeconds / randomJumpHost.JumpDurationSeconds);
                randomJumpHost.SetJumpPose(startPosition, landingPosition, normalizedTime);
                yield return null;
            }

            randomJumpHost.SnapToJumpLanding(landingPosition);
        }
        finally
        {
            if (phaseOneQueen != null)
                phaseOneQueen.EndRandomJumpAnimation();
            if (phaseTwoLongQueen != null)
                phaseTwoLongQueen.EndRandomJumpAnimation();

            randomJumpHost.SetPatternMoveDamageBlocked(false);
            if (pitFallBlockOwner != null)
                pitFallBlockOwner.PopPitFallTriggerBlock();
        }

        randomJumpHost.ApplyJumpLandingDamage(spec, landingPosition);
        randomJumpHost.FaceCurrentTarget();
    }
}
