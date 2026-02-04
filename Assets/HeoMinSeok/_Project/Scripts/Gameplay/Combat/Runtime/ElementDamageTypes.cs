using System;

namespace UnityGAS
{
    /// <summary>
    /// A single element damage channel for one hit.
    /// - elementType: e.g. Element.Fire / Element.Bleed / Element.Poison (GameplayTag)
    /// - baseDamage: the base value authored on the attack/relic/etc.
    /// </summary>
    [Serializable]
    public struct ElementDamageInput
    {
        public GameplayTag elementType;
        public float baseDamage;
    }

    /// <summary>
    /// Final computed element damage for one hit.
    /// (Still "delivered" only; application can be implemented later.)
    /// </summary>
    [Serializable]
    public struct ElementDamageResult
    {
        public GameplayTag elementType;
        public float damage;
    }
}
