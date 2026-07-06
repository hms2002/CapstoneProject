using UnityEngine;
using System.Collections.Generic;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 공격자 하나가 현재 표시 중인 공통 공격 예고 연출 인스턴스를 생성/재사용/숨김 관리한다.
    /// - 공격 로직은 Spec만 넘기고, 실제 표시 뷰의 생명주기 관리는 이 서비스가 맡는다.
    /// </summary>
    public sealed class AttackTelegraphService : MonoBehaviour, IAttackTelegraphPresenter
    {
        [SerializeField] private AttackTelegraphView telegraphPrefab;
        [SerializeField] private AttackTelegraphStyle defaultStyle;

        [Header("Wall Clipping")]
        [SerializeField] private bool useDefaultWallClipping;
        [SerializeField] private LayerMask defaultWallClipLayers;
        [SerializeField, Min(3)] private int defaultWallClipSampleCount = 48;
        [SerializeField, Min(0f)] private float defaultWallClipSkinWidth = 0.03f;

        private AttackTelegraphView activeView;
        private readonly List<AttackTelegraphView> detachedViews = new();
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

            spec = ApplyDefaultWallClipping(spec);
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

            spec = ApplyDefaultWallClipping(spec);
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
        /// - 이 서비스가 생성한 공용/분리형 공격 예고 뷰를 모두 즉시 제거한다.
        /// - 사망, 패턴 강제 중단, 씬 언로드처럼 duration을 기다리면 안 되는 cleanup 지점에서 사용한다.
        /// </summary>
        public void ClearAll()
        {
            HideCurrent();

            for (int i = detachedViews.Count - 1; i >= 0; i--)
            {
                AttackTelegraphView view = detachedViews[i];
                if (view == null)
                    continue;

                view.HideImmediate();
                Destroy(view.gameObject);
            }

            detachedViews.Clear();
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

            spec = ApplyDefaultWallClipping(spec);
            AttackTelegraphView view = Instantiate(telegraphPrefab, parent);
            view.HideImmediate();
            view.Show(spec, defaultStyle);
            detachedViews.Add(view);
            StartCoroutine(DestroyDetachedViewAfter(view, spec.duration));
            return view;
        }

        IAttackTelegraphHandle IAttackTelegraphPresenter.SpawnDetachedView(AttackTelegraphSpec spec, Transform parent)
        {
            return SpawnDetachedView(spec, parent);
        }

        private AttackTelegraphSpec ApplyDefaultWallClipping(AttackTelegraphSpec spec)
        {
            if (spec.useWallClipping || !useDefaultWallClipping || defaultWallClipLayers.value == 0)
                return spec;

            return spec.WithWallClipping(
                defaultWallClipLayers,
                defaultWallClipSampleCount,
                defaultWallClipSkinWidth);
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

        private System.Collections.IEnumerator DestroyDetachedViewAfter(AttackTelegraphView view, float duration)
        {
            if (view == null)
                yield break;

            if (duration > 0f)
                yield return new WaitForSeconds(duration);

            detachedViews.Remove(view);

            if (view != null)
                Destroy(view.gameObject);
        }

        private void OnDisable()
        {
            ClearAll();
        }
    }
}
