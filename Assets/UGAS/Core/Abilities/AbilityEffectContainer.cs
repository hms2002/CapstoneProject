using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    public enum AbilityEffectTiming
    {
        OnActivate,   // 캐스팅 완료 직후(발동 시작)
        OnEvent,      // 특정 GameplayTag 이벤트 수신 시
        OnEnd         // 능력 종료 시
    }

    public enum AbilityEffectTargetPolicy
    {
        Caster,         // 시전자(AbilitySystem 보유자)에게 적용
        ExplicitTarget  // Activate 시 전달된 target에게 적용 (null이면 스킵)
    }

    [Serializable]
    public class AbilityEffectContainer
    {
        public string name;

        public AbilityEffectTiming timing = AbilityEffectTiming.OnActivate;

        [Tooltip("timing이 OnEvent일 때 기다릴 이벤트 태그")]
        public GameplayTag eventTag;

        [Tooltip("적용할 GameplayEffect 목록")]
        public List<GameplayEffect> effects = new List<GameplayEffect>();

        [Tooltip("효과를 시전자에게 줄지, 명시적 타겟에게 줄지 결정합니다. (AoE/다중타겟은 Logic에서 처리 권장)")]
        public AbilityEffectTargetPolicy targetPolicy = AbilityEffectTargetPolicy.ExplicitTarget;
    }
}
