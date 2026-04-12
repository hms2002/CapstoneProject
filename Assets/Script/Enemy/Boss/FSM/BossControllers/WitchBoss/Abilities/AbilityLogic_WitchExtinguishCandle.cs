using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "AL_Witch_ExtinguishCandle", menuName = "GAS/Ability Logic/Witch Boss/AL_Witch_ExtinguishCandle")]
    public class AbilityLogic_WitchExtinguishCandle : AbilityLogic
    {
        // 이 클래스의 책임:
        // 마녀 보스의 촛불 끄기 패턴 ability logic 진입점을 제공한다.

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            yield return null;
        }
    }
}
