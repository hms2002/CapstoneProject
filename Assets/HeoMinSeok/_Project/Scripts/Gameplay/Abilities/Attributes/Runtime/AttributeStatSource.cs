using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Unity 컴포넌트 세계에서 IStatProvider로 접근할 수 있게 해주는 브리지.
    /// 내부적으로 AttributeStatProvider를 사용한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttributeStatSource : MonoBehaviour, IStatProvider
    {
        [Header("Refs")]
        [SerializeField] private AttributeSet attributeSet;
        [SerializeField] private AbilitySystem abilitySystem;

        [Header("Optional Override")]
        [Tooltip("비워두면 AbilitySystem.DamageProfile의 StatBindings를 사용합니다.")]
        [SerializeField] private StatTypeBindings statBindingsOverride;

        private AttributeStatProvider cachedProvider;

        private void Awake()
        {
            if (attributeSet == null)
                attributeSet = GetComponent<AttributeSet>();

            if (abilitySystem == null)
                abilitySystem = GetComponent<AbilitySystem>();

            RebuildProvider();
        }

        public float Get(StatId id)
        {
            if (cachedProvider == null)
                RebuildProvider();

            return cachedProvider != null ? cachedProvider.Get(id) : 0f;
        }

        public void RebuildProvider()
        {
            var bindings = ResolveBindings();
            if (attributeSet == null || bindings == null)
            {
                cachedProvider = null;
                return;
            }

            cachedProvider = new AttributeStatProvider(attributeSet, bindings);
        }

        private StatTypeBindings ResolveBindings()
        {
            if (statBindingsOverride != null)
                return statBindingsOverride;

            if (abilitySystem != null && abilitySystem.DamageProfile != null)
                return abilitySystem.DamageProfile.GetStatBindings();

            return null;
        }
    }
}