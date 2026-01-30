using UnityEngine;

namespace UnityGAS
{
    [CreateAssetMenu(fileName = "GE_Cooldown", menuName = "GAS/Effects/Cooldown (Tag Only)")]
    public class GE_Cooldown : GameplayEffect
    {
        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1)
        {
            // No-op (태그/지속시간/큐는 Runner가 관리)
        }

        public override void Remove(GameObject target, GameObject instigator)
        {
            // No-op
        }
    }
}
