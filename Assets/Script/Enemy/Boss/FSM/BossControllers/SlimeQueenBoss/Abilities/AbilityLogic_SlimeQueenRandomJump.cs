using System.Collections;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 슬라임 여왕 계열 보스의 랜덤 위치 점프 패턴 실행 순서와 점프/착지 사운드를 조율한다.
/// </summary>
public sealed class AbilityLogic_SlimeQueenRandomJump : AbilityLogic
{
    [Header("Sound")]
    [SerializeField] private SoundRef jumpSound = SoundRef.FromKey("sound_slimeQueen_Jump");
    [SerializeField] private SoundRef landSound = SoundRef.FromKey("sound_slimeQueen_Land");

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
        SlimeQueenBossBase facingLockOwner = randomJumpHost as SlimeQueenBossBase;
        facingLockOwner?.BeginPatternFacingLock(initialTarget);
        randomJumpHost.ShowJumpWarning(landingPosition);
        SlimeQueen phaseOneQueen = randomJumpHost as SlimeQueen;
        SlimeQueenP2Long phaseTwoLongQueen = randomJumpHost as SlimeQueenP2Long;
        SlimeQueenBossBase afterimageOwner = randomJumpHost as SlimeQueenBossBase;
        if (phaseOneQueen != null)
            phaseOneQueen.BeginRandomJumpAnimation();
        if (phaseTwoLongQueen != null)
            phaseTwoLongQueen.BeginRandomJumpAnimation();
        afterimageOwner?.BeginPatternAfterimage();

        SlimeQueenPresentationAudioUtility.PlaySound(
            jumpSound,
            hostComponent.gameObject,
            hostComponent.transform.position,
            this,
            initialTarget);

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
            afterimageOwner?.StopPatternAfterimage(IsAbilityCancelled(spec));
            facingLockOwner?.EndPatternFacingLock();

            randomJumpHost.SetPatternMoveDamageBlocked(false);
            if (pitFallBlockOwner != null)
                pitFallBlockOwner.PopPitFallTriggerBlock();
        }

        randomJumpHost.ApplyJumpLandingDamage(spec, landingPosition);
        SlimeQueenPresentationAudioUtility.PlaySound(
            landSound,
            hostComponent.gameObject,
            landingPosition,
            this,
            initialTarget);
        randomJumpHost.FaceCurrentTarget();
    }
}
