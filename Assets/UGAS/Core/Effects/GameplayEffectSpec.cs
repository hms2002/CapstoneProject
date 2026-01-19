using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Unreal의 GameplayEffectSpecHandle 느낌:
    /// - 어떤 GameplayEffect를 적용할지
    /// - SetByCaller(동적 파라미터)
    /// - Context(Instigator/SourceObject/Hit 등)
    /// </summary>
    public sealed class GameplayEffectSpec
    {
        public GameplayEffect Effect { get; }
        public GameplayEffectContext Context { get; }
        public int StackCount { get; set; } = 1;

        // SetByCaller: "Data.Damage" 같은 태그 키로 값 주입
        private readonly Dictionary<GameplayTag, float> setByCaller = new();

        public GameplayEffectSpec(GameplayEffect effect, GameplayEffectContext context)
        {
            Effect = effect;
            Context = context;
        }
        public GameplayEffectSpec(GameplayEffect effect, GameObject instigator, GameObject causer = null)
        {
            Effect = effect;
            Context = new GameplayEffectContext(instigator, causer != null ? causer : instigator);
        }
        // Duration Override 지원
        public bool HasDurationOverride { get; private set; }
        public float DurationOverride { get; private set; }

        public void SetDuration(float seconds)
        {
            HasDurationOverride = true;
            DurationOverride = Mathf.Max(0f, seconds);
        }

        public float GetDurationOrDefault(float defaultDuration)
        {
            return HasDurationOverride ? DurationOverride : defaultDuration;
        }

        public void SetSetByCallerMagnitude(GameplayTag key, float value)
        {
            if (key == null) return;
            setByCaller[key] = value;
        }

        public bool TryGetSetByCallerMagnitude(GameplayTag key, out float value)
        {
            if (key != null && setByCaller.TryGetValue(key, out value))
                return true;

            value = 0f;
            return false;
        }

    }

    public sealed class GameplayEffectContext
    {
        public GameObject Instigator { get; }
        public GameObject Causer { get; }
        public Object SourceObject { get; set; }  // 무기 데이터/유물 데이터 등
        public RaycastHit? Hit3D { get; set; }
        public RaycastHit2D? Hit2D { get; set; }

        public GameplayEffectContext(GameObject instigator, GameObject causer)
        {
            Instigator = instigator;
            Causer = causer;
        }
    }

    /// <summary>
    /// “Spec을 지원하는 GE”만 선택적으로 구현하면 됨.
    /// 기존 GE들은 건드리지 않아도 돌아가게 만들기 위한 어댑터.
    /// </summary>
    public interface ISpecGameplayEffect
    {
        void Apply(GameplayEffectSpec spec, GameObject target);
    }
}
