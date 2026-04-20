using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - AbilitySystem이 발행한 GameplayEvent를 공용 relay를 통해 수신하는 소비자 계약을 정의한다.
    /// - 무기 runtime state 전달, 유물 proc, 패시브 반응처럼 ASC 이벤트를 소비하는 시스템이 같은 경로를 타게 만든다.
    /// </summary>
    public interface IAbilityGameplayEventListener
    {
        void HandleGameplayEvent(GameplayTag tag, in AbilityEventData data);
    }
}
