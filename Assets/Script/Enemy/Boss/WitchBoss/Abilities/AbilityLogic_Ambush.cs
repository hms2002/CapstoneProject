using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "AL_Ambush", menuName = "GAS/Ability Logic/Witch Boss/AL_Ambush")]
    public class AbilityLogic_Ambush : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            Debug.Log("AbilityLogic_Ambush Activate");
            yield return null;
        }
    }
}