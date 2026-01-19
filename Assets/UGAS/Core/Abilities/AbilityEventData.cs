using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// UE GAS의 FGameplayEventData 느낌.
    /// 필요하면 HitPoint, Normal, Payload(오브젝트) 같은 걸 추가해도 됨.
    /// </summary>
    public struct AbilityEventData
    {
        public AbilitySystem AbilitySystem;
        public AbilitySpec Spec;

        public GameObject Instigator;
        public GameObject Target;

        public Vector3 WorldPosition;
        public object Causer;
    }
}
