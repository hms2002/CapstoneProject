using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - GameplayEvent 발행 시 공통으로 전달할 컨텍스트 데이터를 담는다.
    /// - 적중 결과(예: 치명타 여부)를 후속 시스템에 전달하는 공용 payload 역할을 한다.
    /// </summary>
    public struct AbilityEventData
    {
        public AbilitySystem AbilitySystem;
        public AbilitySpec Spec;

        public GameObject Instigator;
        public GameObject Target;

        public Vector3 WorldPosition;
        public object Causer;
        public bool IsCriticalHit;
    }
}
