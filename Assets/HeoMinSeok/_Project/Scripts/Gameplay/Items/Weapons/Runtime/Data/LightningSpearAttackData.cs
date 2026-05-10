using UnityEngine;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "ALData_LightningSpearAttack", menuName = "GAS/Weapon/Lightning Spear/Attack Data")]
    public sealed class LightningSpearAttackData : ScriptableObject
    {
        [SerializeField] private WeaponComboAttack2DConfig combo = new();

        public WeaponComboAttack2DConfig Combo => combo;
    }
}
