using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임: Gameplay 코드가 구체 AttackTelegraphView 없이 생성된 공격 경고 표시를 갱신/정리하게 하는 핸들 계약이다.
    /// </summary>
    public interface IAttackTelegraphHandle
    {
        bool IsVisible { get; }
        void Show(AttackTelegraphSpec spec);
        void UpdateGeometry(AttackTelegraphSpec spec);
        void HideImmediate();
        void Release();
    }

    /// <summary>
    /// 책임: Gameplay 코드가 구체 AttackTelegraphService 없이 공격 경고 표시를 요청하게 하는 presenter 계약이다.
    /// </summary>
    public interface IAttackTelegraphPresenter
    {
        bool HasActiveTelegraph { get; }
        void Show(AttackTelegraphSpec spec);
        void UpdateCurrentGeometry(AttackTelegraphSpec spec);
        void HideCurrent();
        void ClearAll();
        IAttackTelegraphHandle SpawnDetachedView(AttackTelegraphSpec spec, Transform parent = null);
    }

    /// <summary>
    /// 책임: Gameplay MonoBehaviour가 같은 오브젝트나 직렬화된 MonoBehaviour 참조에서 텔레그래프 presenter 계약 구현을 찾게 돕는다.
    /// </summary>
    public static class AttackTelegraphPresenterResolver
    {
        public static IAttackTelegraphPresenter Resolve(MonoBehaviour configuredComponent, Component fallbackOwner)
        {
            if (configuredComponent is IAttackTelegraphPresenter configuredPresenter)
                return configuredPresenter;

            return Resolve(fallbackOwner);
        }

        public static IAttackTelegraphPresenter Resolve(Component owner)
        {
            if (owner == null)
                return null;

            return Resolve(owner.gameObject);
        }

        public static IAttackTelegraphPresenter Resolve(GameObject owner)
        {
            if (owner == null)
                return null;

            MonoBehaviour[] behaviours = owner.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IAttackTelegraphPresenter presenter)
                    return presenter;
            }

            return null;
        }
    }
}
