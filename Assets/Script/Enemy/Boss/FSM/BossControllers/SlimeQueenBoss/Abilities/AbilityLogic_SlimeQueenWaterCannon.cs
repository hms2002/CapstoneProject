using System.Collections;
using UnityEngine;
using UnityGAS;

public sealed class AbilityLogic_SlimeQueenWaterCannon : AbilityLogic
{
    /// <summary>원거리 슬라임 여왕이 플레이어를 추적하는 경고 후 고정 방향 물대포를 발사합니다.</summary>
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        SlimeQueenP2Long slimeQueen = system != null ? system.GetComponent<SlimeQueenP2Long>() : null;
        if (slimeQueen == null)
            yield break;

        try
        {
            slimeQueen.FaceCurrentTarget();
            if (!slimeQueen.ShowWaterCannonWarning(initialTarget))
                yield break;

            float warningElapsedSeconds = 0f;
            while (warningElapsedSeconds < slimeQueen.WaterCannonWarningSeconds)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                slimeQueen.FaceCurrentTarget();
                slimeQueen.UpdateWaterCannonWarning(initialTarget);
                warningElapsedSeconds += Time.deltaTime;
                yield return null;
            }

            if (IsAbilityCancelled(spec))
                yield break;

            slimeQueen.FaceCurrentTarget();
            if (!slimeQueen.StartWaterCannonBeam(system, spec, initialTarget))
                yield break;

            float elapsedSeconds = 0f;
            while (elapsedSeconds < slimeQueen.WaterCannonActiveSeconds)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                slimeQueen.UpdateWaterCannonBeam(system, spec, initialTarget);
                elapsedSeconds += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            slimeQueen.CleanupWaterCannonPresentation();
        }
    }

    /// <summary>씬 전환이나 강제 정리 시 원거리 슬라임 여왕의 물대포 표시를 제거합니다.</summary>
    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        SlimeQueenP2Long slimeQueen = system != null ? system.GetComponent<SlimeQueenP2Long>() : null;
        if (slimeQueen == null)
            return;

        slimeQueen.CleanupWaterCannonPresentation();
    }
}
