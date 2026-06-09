using System;

namespace UnityGAS
{
    /// 한 번의 공격/피격 계산에서 사용되는 속성 피해 입력값.
    /// 공식 계산 전 authored base value를 담는다.
    /// <summary>
    /// Legacy serialized per-hit element input kept so old weapon data can still load.
    /// Applied element build-up is resolved from ElementOffenseSource instead.
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
    /// Resolved element build-up channel used by ElementBuildUpResolver and ElementGaugeSystem.
    /// </summary>
    [Serializable]
    public struct ElementDamageResult
    {
        public GameplayTag elementType;
        public float damage;
    }
}
