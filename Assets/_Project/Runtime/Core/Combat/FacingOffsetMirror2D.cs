using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// - SpriteRenderer.flipX에 맞춰 등록된 Transform들의 초기 localPosition.x를 좌우 반전한다.
    /// - 그림자, 비대칭 body/hurtbox, 접촉 피해 범위처럼 visual 방향과 함께 따라가야 하는 child offset만 담당한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FacingOffsetMirror2D : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private SpriteRenderer facingSprite;
        [SerializeField] private Transform[] mirroredTargets;

        [Header("Options")]
        [SerializeField] private bool captureInitialPositionsOnAwake = true;
        [SerializeField] private bool invertWhenFlipX = true;

        private Vector3[] baseLocalPositions;
        private bool[] basePositionCaptured;
        private Transform[] capturedTargets;

        private void Awake()
        {
            CacheReferences();
            CaptureBasePositions(force: captureInitialPositionsOnAwake);
            ApplyFacingOffset();
        }

        private void OnEnable()
        {
            CacheReferences();
            CaptureBasePositions(force: false);
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

        /// <summary>현재 대상 위치를 기준 위치로 다시 저장하고 즉시 facing 보정을 적용한다.</summary>
        public void RecaptureBasePositions()
        {
            CaptureBasePositions(force: true);
            ApplyFacingOffset();
        }

        private void CacheReferences()
        {
            if (facingSprite == null)
                facingSprite = GetComponentInChildren<SpriteRenderer>(true);
        }

        private void CaptureBasePositions(bool force)
        {
            int targetCount = mirroredTargets != null ? mirroredTargets.Length : 0;
            if (baseLocalPositions == null || baseLocalPositions.Length != targetCount)
            {
                baseLocalPositions = new Vector3[targetCount];
                basePositionCaptured = new bool[targetCount];
                capturedTargets = new Transform[targetCount];
            }

            for (int i = 0; i < targetCount; i++)
            {
                Transform target = mirroredTargets[i];
                if (target == null)
                {
                    baseLocalPositions[i] = Vector3.zero;
                    basePositionCaptured[i] = false;
                    capturedTargets[i] = null;
                    continue;
                }

                if (force || !basePositionCaptured[i] || capturedTargets[i] != target)
                {
                    baseLocalPositions[i] = target.localPosition;
                    basePositionCaptured[i] = true;
                    capturedTargets[i] = target;
                }
            }
        }

        /// <summary>현재 flipX 상태에 맞춰 등록 대상들의 local X 오프셋을 반전한다.</summary>
        private void ApplyFacingOffset()
        {
            if (facingSprite == null || mirroredTargets == null || baseLocalPositions == null)
                return;

            bool shouldMirror = facingSprite.flipX == invertWhenFlipX;
            int count = Mathf.Min(mirroredTargets.Length, baseLocalPositions.Length);
            for (int i = 0; i < count; i++)
            {
                Transform target = mirroredTargets[i];
                if (target == null)
                    continue;

                Vector3 nextPosition = baseLocalPositions[i];
                if (shouldMirror)
                    nextPosition.x = -nextPosition.x;

                target.localPosition = nextPosition;
            }
        }
    }
}
