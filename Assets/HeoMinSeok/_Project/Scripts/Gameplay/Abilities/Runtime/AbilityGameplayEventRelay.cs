using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - AbilitySystem.GameplayEventRaised를 한 번만 구독해 같은 오브젝트의 여러 소비자에게 공용으로 분배한다.
    /// - 무기, 유물, 패시브가 각자 ASC 이벤트 채널을 직접 구독하지 않고 동일한 reaction 경계를 타게 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AbilitySystem))]
    public sealed class AbilityGameplayEventRelay : MonoBehaviour
    {
        [SerializeField] private AbilitySystem abilitySystem;

        private readonly List<IAbilityGameplayEventListener> listeners = new();

        private void Awake()
        {
            if (abilitySystem == null)
                abilitySystem = GetComponent<AbilitySystem>();
        }

        private void OnEnable()
        {
            if (abilitySystem == null)
                abilitySystem = GetComponent<AbilitySystem>();

            if (abilitySystem != null)
                abilitySystem.GameplayEventRaised += Dispatch;
        }

        private void OnDisable()
        {
            if (abilitySystem != null)
                abilitySystem.GameplayEventRaised -= Dispatch;
        }

        public void Register(IAbilityGameplayEventListener listener)
        {
            if (listener == null || listeners.Contains(listener))
                return;

            listeners.Add(listener);
        }

        public void Unregister(IAbilityGameplayEventListener listener)
        {
            if (listener == null)
                return;

            listeners.Remove(listener);
        }

        /// <summary>
        /// 책임 :
        /// - 현재 등록된 listener 스냅샷에만 이벤트를 전달해 dispatch 중 등록/해제가 일어나도 안전하게 처리한다.
        /// - relay는 이벤트 해석을 하지 않고 전달만 담당해 소비자별 정책은 각 listener에 남긴다.
        /// </summary>
        private void Dispatch(GameplayTag tag, AbilityEventData data)
        {
            if (listeners.Count == 0)
                return;

            for (int i = 0; i < listeners.Count; i++)
            {
                IAbilityGameplayEventListener listener = listeners[i];
                listener?.HandleGameplayEvent(tag, data);
            }
        }
    }
}
