using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "AL_LightningSpearAttack", menuName = "GAS/Weapon/Lightning Spear/Logic Attack")]
    public sealed class AbilityLogic_LightningSpearAttack : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || spec?.Definition == null)
                yield break;

            LightningSpearAttackData data = spec.Definition.sourceObject as LightningSpearAttackData;
            if (data == null || data.Combo == null)
            {
                Debug.LogError("[LightningSpearAttack] AbilityDefinition.sourceObject must be LightningSpearAttackData.");
                yield break;
            }

            var callbacks = new WeaponComboAttackCallbacks
            {
                onStepCompleted = OnStepCompleted
            };

            yield return WeaponComboAttack2DRunner.Execute(system, spec, data.Combo, this, callbacks);
        }

        private void OnStepCompleted(WeaponComboAttackExecutionContext context)
        {
            // Extension point for future Lightning Spear upgrades, e.g. third-hit mark creation.
        }
    }
}
