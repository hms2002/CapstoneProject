using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 수치 payload 없이 duration과 granted tag만 필요한 상태성 효과를 표현한다.
    /// - 시야 제한처럼 GE 수명과 tag 존재만 있으면 되는 효과를 공용 GameplayEffect 경로에 태울 수 있게 만든다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStatusOnlyDurationEffect", menuName = "GAS/Effects/Status Only Duration")]
    public sealed class GE_StatusOnlyDuration : GameplayEffect
    {
        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1)
        {
        }

        public override void Remove(GameObject target, GameObject instigator)
        {
        }
    }
}
