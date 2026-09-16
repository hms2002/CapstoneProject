using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 기묘한 쇳덩이 Skill2 전탄 난사의 탄 수 확정과 빠른 연속 사격을 담당한다.
    /// - 실제 발사마다 한 발씩 소비하고, 취소 시 아직 발사하지 않은 잔탄은 보존한다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_OddIronBarrage", menuName = "GAS/Weapon/Odd Iron/Barrage Logic")]
    public sealed class AbilityLogic_OddIronBarrage : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || spec?.Definition == null)
                yield break;

            OddIronShotData data = spec.Definition.sourceObject as OddIronShotData;
            OddIronRuntimeData runtimeData = OddIronAbilityUtility.ResolveRuntimeData(system);
            if (data == null || data.projectilePrefab == null || runtimeData == null)
                yield break;

            if (!runtimeData.HasAmmo) WeaponExclusiveRelics.TryReload(system.gameObject, runtimeData);
            int roundsToFire = runtimeData.CurrentAmmo;
            if (roundsToFire <= 0)
                yield break;

            float interval = Mathf.Max(0f, data.barrageInterval);
            for (int i = 0; i < roundsToFire; i++)
            {
                if (spec.Token != null && spec.Token.IsCancelled)
                    yield break;

                if (!runtimeData.HasAmmo ||
                    !AbilityLogic_OddIronShot.FireOnce(system, spec, data, data.barrageSpreadAngle))
                    yield break;

                runtimeData.TryConsumeOneRound();

                if (interval > 0f && i < roundsToFire - 1)
                    yield return new WaitForSeconds(interval);
            }
        }
    }
}
