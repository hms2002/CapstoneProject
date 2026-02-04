using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "AL_Wave", menuName = "GAS/Ability Logic/Witch Boss/AL_Wave")]
    public class AbilityLogic_Wave : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            Debug.Log("AbilityLogic_Wave Activate");
            yield return null;
        }
    }
}