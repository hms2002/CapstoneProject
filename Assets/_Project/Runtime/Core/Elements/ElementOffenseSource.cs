using UnityEngine;

namespace UnityGAS
{
    public sealed class ElementOffenseSource : MonoBehaviour
    {
        [SerializeField] private ElementBuildUpFormulaProfile profile;
        [SerializeField] private bool applyToAllDamage = true;

        public ElementBuildUpFormulaProfile Profile => profile;
        public bool ApplyToAllDamage => applyToAllDamage;
    }
}