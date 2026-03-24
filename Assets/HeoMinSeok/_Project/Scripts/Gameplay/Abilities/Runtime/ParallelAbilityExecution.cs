using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 병행 실행 중인 Ability 1건의 런타임 상태를 보관한다.
    /// - 어떤 spec이 어떤 target으로 실행 중인지와, 연결된 coroutine을 추적한다.
    /// </summary>
    public sealed class ParallelAbilityExecution
    {
        public AbilitySpec Spec;
        public GameObject Target;
        public Coroutine Coroutine;
    }
}