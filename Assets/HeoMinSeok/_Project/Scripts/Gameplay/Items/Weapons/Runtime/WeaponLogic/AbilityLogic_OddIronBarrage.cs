using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 기묘한 쇳덩이 Skill2 전탄 난사의 탄 수 확정, 전탄 소비, 빠른 연속 사격을 담당한다.
    /// - 난사 시작 시점에 잔탄을 모두 소비해 취소되더라도 일회용 무기 리스크가 유지되게 한다.
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

            int roundsToFire = runtimeData.ConsumeAllRounds();
            if (roundsToFire <= 0)
                yield break;

            float interval = Mathf.Max(0f, data.barrageInterval);
            for (int i = 0; i < roundsToFire; i++)
            {
                if (spec.Token != null && spec.Token.IsCancelled)
                    yield break;

                AbilityLogic_OddIronShot.FireOnce(system, spec, data, data.barrageSpreadAngle);

                if (interval > 0f && i < roundsToFire - 1)
                    yield return new WaitForSeconds(interval);
            }
        }
    }
}
