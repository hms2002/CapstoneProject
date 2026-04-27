using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - SpriteRenderer.flipX에 맞춰 몬스터별 그림자 local X 오프셋을 좌우 반전한다.
    /// - 높이/공중 연출과 분리된 facing 기반 그림자 위치 보정만 담당한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShadowFacingOffsetMirror2D : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private SpriteRenderer facingSprite;
        [SerializeField] private Transform shadowRoot;

        private Vector3 baseLocalPosition;

        private void Awake()
        {
            CacheReferences();
            CaptureBasePosition();
            ApplyFacingOffset();
        }

        private void OnEnable()
        {
            CacheReferences();
            ApplyFacingOffset();
        }

        private void OnValidate()
        {
            CacheReferences();
        }

        private void LateUpdate()
        {
            ApplyFacingOffset();
        }

        private void CacheReferences()
        {
            if (facingSprite == null)
                facingSprite = GetComponentInChildren<SpriteRenderer>(true);

            if (shadowRoot == null)
                shadowRoot = transform;
        }

        private void CaptureBasePosition()
        {
            if (shadowRoot != null)
                baseLocalPosition = shadowRoot.localPosition;
        }

        /// <summary>현재 바라보는 방향에 맞춰 그림자 기준 X 오프셋을 반전한다.</summary>
        private void ApplyFacingOffset()
        {
            if (shadowRoot == null || facingSprite == null)
                return;

            Vector3 nextPosition = baseLocalPosition;
            if (facingSprite.flipX)
                nextPosition.x = -baseLocalPosition.x;

            shadowRoot.localPosition = nextPosition;
        }
    }
}
