using System.Collections;
using UnityEngine;
using UnityGAS;

public sealed class AbilityLogic_SlimeQueenDropMediumSlime : AbilityLogic
{
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
            landingPosition);

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
