using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 원거리 슬라임 여왕의 십자 물기둥 패턴과 발동 동시 사운드를 실행한다.
/// </summary>
public sealed class AbilityLogic_SlimeQueenCrossWaterPillar : AbilityLogic
{
    [Header("Sound")]
    [SerializeField] private WorldPresentationHook castPresentation = new WorldPresentationHook
    {
        sound = SoundRef.FromKey("sound_slimeQueen_CrossWaterPillar1"),
        additionalSounds = new[]
        {
            SoundRef.FromKey("sound_slimeQueen_CrossWaterPillar2")
        }
    };

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

            SlimeQueenPresentationAudioUtility.PlayPresentation(
                castPresentation,
                slimeQueen.gameObject,
                slimeQueen.transform.position,
                this,
                initialTarget);
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
