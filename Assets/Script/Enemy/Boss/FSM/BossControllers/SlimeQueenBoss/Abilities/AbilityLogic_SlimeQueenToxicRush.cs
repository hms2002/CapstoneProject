using System.Collections;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 근거리 슬라임 여왕의 독성 돌진 반복과 돌진/독구름 사운드 주입을 담당한다.
/// </summary>
public sealed class AbilityLogic_SlimeQueenToxicRush : AbilityLogic
{
    [Header("Sound")]
    [SerializeField] private SoundRef dashSound = SoundRef.FromKey("sound_slimeQueen_Dash");
    [SerializeField] private SoundRef poisonCloudLoopSound = SoundRef.FromKey("sound_slimeQueen_PoisonMist");

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
                slimeQueen.BeginPatternFacingLockTowards(segment.End);
                slimeQueen.ShowToxicRushWarning(segment);

                if (slimeQueen.ToxicRushWarningSeconds > 0f)
                    yield return WaitForSecondsUnlessCancelled(slimeQueen.ToxicRushWarningSeconds, spec);

                if (IsAbilityCancelled(spec))
                    yield break;

                slimeQueen.BeginToxicRushAnimation();
                slimeQueen.BeginPatternAfterimage();
                SlimeQueenPresentationAudioUtility.PlaySound(
                    dashSound,
                    slimeQueen.gameObject,
                    slimeQueen.transform.position,
                    this,
                    initialTarget);
                slimeQueen.ClearToxicRushWarnings();
                slimeQueen.BeginToxicRushTrail(segment.Start, poisonCloudLoopSound);

                float traveledDistance = 0f;
                bool hitPlayer = false;
                while (traveledDistance < segment.Length)
                {
                    if (IsAbilityCancelled(spec))
                        yield break;

                    traveledDistance += slimeQueen.ToxicRushSpeed * Time.deltaTime;
                    slimeQueen.SetToxicRushPose(segment, traveledDistance);
                    if (slimeQueen.HasToxicRushHitPlayer())
                    {
                        hitPlayer = true;
                        break;
                    }

                    yield return null;
                }

                if (hitPlayer)
                    slimeQueen.FinishToxicRushAtCurrentPosition();
                else
                    slimeQueen.FinishToxicRushSegment(segment);

                slimeQueen.StopPatternAfterimage(IsAbilityCancelled(spec));
                slimeQueen.EndToxicRushAnimation();
                slimeQueen.EndPatternFacingLock();
                slimeQueen.FaceCurrentTarget();

                if (slimeQueen.ToxicRushIntervalSeconds > 0f && rushIndex < slimeQueen.ToxicRushRepeatCount - 1)
                    yield return WaitForSecondsUnlessCancelled(slimeQueen.ToxicRushIntervalSeconds, spec);

                if (IsAbilityCancelled(spec))
                    yield break;
            }
        }
        finally
        {
            slimeQueen.StopPatternAfterimage(IsAbilityCancelled(spec));
            slimeQueen.EndToxicRushAnimation();
            slimeQueen.EndPatternFacingLock();
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
        slimeQueen.EndPatternFacingLock();
        slimeQueen.StopPatternAfterimage(clearGhosts: true);
        slimeQueen.CleanupToxicRushPresentation();
    }
}
