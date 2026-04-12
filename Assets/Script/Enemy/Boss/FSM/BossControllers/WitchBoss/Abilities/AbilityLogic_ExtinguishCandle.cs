using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "AL_ExtinguishCandle", menuName = "GAS/Ability Logic/Witch Boss/AL_ExtinguishCandle")]
    public class AbilityLogic_ExtinguishCandle : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            yield return null;
        }
    }
}
