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

        /// <summary>
        /// 책임 :
        /// - AbilityLogic 구현체가 공통 취소 토큰을 직접 파고들지 않고 현재 실행 취소 여부를 확인하게 한다.
        /// - 그로기/사망/씬 전환처럼 실행 중단이 필요한 경로에서 각 Logic의 자연 종료와 finally cleanup을 보장하도록 돕는다.
        /// </summary>
        protected static bool IsAbilityCancelled(AbilitySpec spec)
        {
            return spec != null && spec.Token != null && spec.Token.IsCancelled;
        }

        /// <summary>
        /// 책임 :
        /// - 긴 WaitForSeconds 구간을 취소 가능한 프레임 단위 대기로 바꿔 제압/사망에 빠르게 반응하게 한다.
        /// - 호출자는 대기 후 IsCancelled를 확인해 남은 패턴 실행 여부를 결정한다.
        /// </summary>
        protected static IEnumerator WaitForSecondsUnlessCancelled(float seconds, AbilitySpec spec)
        {
            if (seconds <= 0f)
                yield break;

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
