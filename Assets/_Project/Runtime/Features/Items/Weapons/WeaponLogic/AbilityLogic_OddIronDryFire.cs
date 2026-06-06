using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 기묘한 쇳덩이 빈 탄창 격발 피드백만 출력한다.
    /// - 잔탄, 투사체, 피해 상태를 변경하지 않아 dry-fire가 순수한 실패 피드백으로 남게 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_OddIronDryFire", menuName = "GAS/Weapon/Odd Iron/Dry Fire Logic")]
    public sealed class AbilityLogic_OddIronDryFire : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || spec?.Definition == null)
                yield break;

            OddIronDryFireData data = spec.Definition.sourceObject as OddIronDryFireData;
            if (data == null)
                yield break;

            Vector2 direction = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
            Vector3 position = OddIronAbilityUtility.ResolveSpawnPosition(system, direction, data.vfxOffset);

            if (data.dryFireVfxPrefab != null)
                Object.Instantiate(data.dryFireVfxPrefab, position, Quaternion.identity);

            AbilityAudioRouter.PlayOneShot(data.dryFireSound, system, spec, sourceObjectOverride: data);
        }
    }
}
