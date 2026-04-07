using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 공격자 하나가 현재 표시 중인 공통 공격 예고 연출 인스턴스를 생성/재사용/숨김 관리한다.
    /// - 공격 로직은 Spec만 넘기고, 실제 표시 뷰의 생명주기 관리는 이 서비스가 맡는다.
    /// </summary>
    public sealed class AttackTelegraphService : MonoBehaviour
    {
        [SerializeField] private AttackTelegraphView telegraphPrefab;
        [SerializeField] private AttackTelegraphStyle defaultStyle;

        private AttackTelegraphView activeView;

        public bool HasActiveTelegraph => activeView != null && activeView.IsVisible;

        /// <summary>
        /// 책임 :
        /// - 공통 공격 예고 뷰를 필요 시 생성하고, 전달된 Spec으로 즉시 표시한다.
        /// </summary>
        public void Show(AttackTelegraphSpec spec)
        {
            AttackTelegraphView view = GetOrCreateView();
            if (view == null)
                return;

            view.Show(spec, defaultStyle);
        }

        /// <summary>
        /// 책임 :
        /// - 현재 표시 중인 공격 예고 뷰를 즉시 숨긴다.
        /// </summary>
        public void HideCurrent()
        {
            if (activeView == null)
                return;

            activeView.HideImmediate();
        }

        private AttackTelegraphView GetOrCreateView()
        {
            if (activeView != null)
                return activeView;

            if (telegraphPrefab == null)
                return null;

            activeView = Instantiate(telegraphPrefab, transform);
            activeView.HideImmediate();
            return activeView;
        }
    }
}
