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
        private SpriteRenderer ownerRenderer;

        public bool HasActiveTelegraph => activeView != null && activeView.IsVisible;

        private void Awake()
        {
            ownerRenderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// 책임 :
        /// - 공통 공격 예고 뷰를 필요 시 생성하고, 전달된 Spec으로 즉시 표시한다.
        /// </summary>
        public void Show(AttackTelegraphSpec spec)
        {
            AttackTelegraphView view = GetOrCreateView();
            if (view == null)
                return;

            SyncViewSorting(view);
            view.Show(spec, defaultStyle);
        }

        /// <summary>
        /// 책임 :
        /// - 현재 표시 중인 텔레그래프의 진행도는 유지한 채 도형의 위치/회전/크기만 갱신한다.
        /// - 추적형 경고선처럼 목표를 따라가야 하는 연출이 사용한다.
        /// </summary>
        public void UpdateCurrentGeometry(AttackTelegraphSpec spec)
        {
            if (activeView == null || !activeView.IsVisible)
                return;

            activeView.UpdateGeometry(spec);
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

        /// <summary>
        /// 책임 :
        /// - 공용 설정을 재사용하면서 독립적으로 관리되는 텔레그래프 뷰를 하나 생성한다.
        /// - 여러 공격 경고를 동시에 띄워야 하는 패턴이 사용한다.
        /// </summary>
        public AttackTelegraphView SpawnDetachedView(AttackTelegraphSpec spec, Transform parent = null)
        {
            if (telegraphPrefab == null)
                return null;

            AttackTelegraphView view = Instantiate(telegraphPrefab, parent != null ? parent : transform);
            view.HideImmediate();
            SyncViewSorting(view);
            view.Show(spec, defaultStyle);
            StartCoroutine(DestroyDetachedViewAfter(view, spec.duration));
            return view;
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

        /// <summary>
        /// 책임 :
        /// - 생성된 텔레그래프 뷰가 소유자 스프라이트 위에서 보이도록 정렬값을 맞춘다.
        /// </summary>
        private void SyncViewSorting(AttackTelegraphView view)
        {
            if (view == null)
                return;

            if (ownerRenderer == null)
                ownerRenderer = GetComponent<SpriteRenderer>();

            view.SyncSorting(ownerRenderer);
        }

        private System.Collections.IEnumerator DestroyDetachedViewAfter(AttackTelegraphView view, float duration)
        {
            if (view == null)
                yield break;

            if (duration > 0f)
                yield return new WaitForSeconds(duration);

            if (view != null)
                Destroy(view.gameObject);
        }
    }
}
