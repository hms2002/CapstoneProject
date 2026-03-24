using System.Collections;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - AbilityDefinition이 실행할 실제 로직 코루틴을 제공한다.
    /// - 필요 시 씬 이동/강제 리셋 시점에 자신이 만든 일시 런타임 상태를 정리하는 훅을 제공한다.
    /// </summary>
    public abstract class AbilityLogic : ScriptableObject
    {
        public abstract IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget);

        /// <summary>
        /// 책임 :
        /// - 씬 이동 직전에 AbilityLogic이 직접 만든 일시 상태(태그, modifier, motion, 구독 등)를 정리한다.
        /// - 기본 구현은 아무 것도 하지 않으며, 상태를 남기는 로직만 override 한다.
        /// </summary>
        public virtual void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
        {
        }
    }
}
