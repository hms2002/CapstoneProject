using System;

namespace UnityGAS
{
    /// 한 번의 공격/피격 계산에서 사용되는 속성 피해 입력값.
    /// 공식 계산 전 authored base value를 담는다.
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

    /// 한 번의 공격/피격 계산에서 사용되는 속성 피해 입력값.
    /// 공식 계산 전 authored base value를 담는다.
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
