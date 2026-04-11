using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "AL_NormalAttack1", menuName = "GAS/Ability Logic/Witch Boss/AL_NormalAttack1")]
    public class AbilityLogic_NormalAttack1 : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            yield return null;
        }
    }
}
