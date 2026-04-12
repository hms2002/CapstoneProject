using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "AL_Witch_NormalAttack1", menuName = "GAS/Ability Logic/Witch Boss/AL_Witch_NormalAttack1")]
    public class AbilityLogic_WitchNormalAttack1 : AbilityLogic
    {
        // 이 클래스의 책임:
        // 마녀 보스의 평타1 패턴 ability logic 진입점을 제공한다.

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            yield return null;
        }
    }
}
