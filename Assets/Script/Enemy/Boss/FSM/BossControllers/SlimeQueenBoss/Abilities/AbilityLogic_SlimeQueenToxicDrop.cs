using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public sealed class AbilityLogic_SlimeQueenToxicDrop : AbilityLogic
{
    /// <summary>원거리 슬라임 여왕이 플레이어 주변 삼각형 지점 세 곳에 독구름 장판을 동시에 투하합니다.</summary>
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        SlimeQueenP2Long slimeQueen = system != null ? system.GetComponent<SlimeQueenP2Long>() : null;
        if (slimeQueen == null)
            yield break;

        List<Vector3> dropPositions = new List<Vector3>(3);
        if (!slimeQueen.BuildToxicDropPositions(initialTarget, dropPositions))
            yield break;

        try
        {
            slimeQueen.FaceCurrentTarget();
            slimeQueen.ShowToxicDropWarnings(dropPositions);

            if (slimeQueen.ToxicDropWarningSeconds > 0f)
                yield return WaitForSecondsUnlessCancelled(slimeQueen.ToxicDropWarningSeconds, spec);

            if (IsAbilityCancelled(spec))
                yield break;

            slimeQueen.ClearToxicDropWarnings();
            bool launchedProjectiles = slimeQueen.LaunchToxicDropProjectiles(dropPositions);
            while (launchedProjectiles && !slimeQueen.AreToxicDropProjectilesFinished())
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                yield return null;
            }

            if (IsAbilityCancelled(spec))
                yield break;

            slimeQueen.SpawnToxicDropPoisonClouds(dropPositions);
            slimeQueen.ClearToxicDropProjectiles();
            slimeQueen.FaceCurrentTarget();
        }
        finally
        {
            slimeQueen.CleanupToxicDropPresentation();
        }
    }

    /// <summary>씬 전환이나 강제 정리 시 원거리 슬라임 여왕의 독성 투하 경고 표시를 제거합니다.</summary>
    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        SlimeQueenP2Long slimeQueen = system != null ? system.GetComponent<SlimeQueenP2Long>() : null;
        if (slimeQueen == null)
            return;

        slimeQueen.CleanupToxicDropPresentation();
    }
}
