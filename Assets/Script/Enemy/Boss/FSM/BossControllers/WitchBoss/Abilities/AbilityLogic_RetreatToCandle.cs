using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "AL_RetreatToCandle", menuName = "GAS/Ability Logic/Witch Boss/AL_RetreatToCandle")]
    public class AbilityLogic_RetreatToCandle : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            yield return null;
        }
    }
}
