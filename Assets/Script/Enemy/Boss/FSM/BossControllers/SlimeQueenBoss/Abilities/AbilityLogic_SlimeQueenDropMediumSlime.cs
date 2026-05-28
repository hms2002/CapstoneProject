using System.Collections;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 1페이즈 슬라임 여왕의 중형 슬라임 낙하 소환 패턴과 소환체 착지 랜덤 사운드를 주입한다.
/// </summary>
public sealed class AbilityLogic_SlimeQueenDropMediumSlime : AbilityLogic
{
    [Header("Sound")]
    [SerializeField] private WorldPresentationHook minionLandingPresentation = new WorldPresentationHook
    {
        randomSounds = new[]
        {
            SoundRef.FromKey("sound_slimeQeen_landMinion1"),
            SoundRef.FromKey("sound_slimeQeen_landMinion2"),
            SoundRef.FromKey("sound_slimeQeen_landMinion3"),
            SoundRef.FromKey("sound_slimeQeen_landMinion4")
        }
    };

    /// <summary>SlimeQueen이 플레이어 위치에 중형 슬라임 낙하 소환 패턴을 실행합니다.</summary>
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        SlimeQueen slimeQueen = system != null ? system.GetComponent<SlimeQueen>() : null;
        if (slimeQueen == null)
            yield break;

        Transform target = initialTarget != null ? initialTarget.transform : slimeQueen.CurrentTarget;
        if (target == null)
            yield break;

        slimeQueen.FaceCurrentTarget();

        Vector3 landingPosition = target.position;
        landingPosition.z = slimeQueen.transform.position.z;

        slimeQueen.ShowSummonWarning(landingPosition);
        yield return WaitForSecondsUnlessCancelled(slimeQueen.SummonWarningSeconds, spec);

        if (IsAbilityCancelled(spec))
            yield break;

        GameObject summonPrefab = slimeQueen.GetRandomMediumSlimePrefab();
        SlimeQueenFallingSummon fallingSummon = slimeQueen.SpawnFallingMediumSlime(
            summonPrefab,
            spec,
            landingPosition,
            minionLandingPresentation,
            this);

        if (fallingSummon == null)
            yield break;

        while (fallingSummon != null && !fallingSummon.IsFinished)
        {
            if (IsAbilityCancelled(spec))
            {
                fallingSummon.CancelFall();
                yield break;
            }

            yield return null;
        }
    }
}
