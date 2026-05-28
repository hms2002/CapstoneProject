using System;
using System.Collections;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// 취룡 술통/술병 투척 actor로서 직선 비행, 회전 연출, 비행 중 대상 충돌 감지를 수행하고 착탄 콜백을 알린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DragonThrownKegActor : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private CircleCollider2D collisionShape;

        [Header("Vertical Drop Presentation")]
        [SerializeField] private Transform shadowRoot;
        [SerializeField] private SpriteRenderer shadowRenderer;
        [SerializeField] private bool autoCreateShadow = true;
        [SerializeField] private Color shadowColor = new(0f, 0f, 0f, 0.35f);
        [SerializeField] private Vector3 shadowGroundScale = new(1f, 0.35f, 1f);
        [SerializeField] private Vector3 shadowAirScale = new(0.45f, 0.16f, 1f);
        [SerializeField, Range(0f, 1f)] private float shadowGroundAlpha = 0.35f;
        [SerializeField, Range(0f, 1f)] private float shadowAirAlpha = 0.12f;

        private Coroutine flightRoutine;
        private Vector3 visualBaseLocalPosition;
        private bool hasVisualBaseLocalPosition;

        /// <summary>술통 actor를 지정한 시작점에서 착탄점까지 직선 이동시키고 충돌 또는 도착 시 콜백을 호출한다.</summary>
        public void Launch(
            Vector3 startPosition,
            Vector3 impactPosition,
            float travelSeconds,
            float spinDegrees,
            LayerMask collisionMask,
            Action<Vector3, GameObject> onImpact)
        {
            if (flightRoutine != null)
                StopCoroutine(flightRoutine);

            gameObject.SetActive(true);
            transform.position = startPosition;
            flightRoutine = StartCoroutine(FlyRoutine(
                startPosition,
                impactPosition,
                Mathf.Max(0.01f, travelSeconds),
                spinDegrees,
                ResolveCollisionRadius(),
                collisionMask,
                onImpact));
        }

        /// <summary>술통 actor를 바닥 위치에 고정한 채 visual height만 낮추는 수직 낙하로 실행한다.</summary>
        public void LaunchVerticalDrop(
            Vector3 impactPosition,
            float startHeight,
            float travelSeconds,
            float spinDegrees,
            LayerMask collisionMask,
            Action<Vector3, GameObject> onImpact)
        {
            if (flightRoutine != null)
                StopCoroutine(flightRoutine);

            gameObject.SetActive(true);
            transform.position = impactPosition;
            CacheVisualBaseLocalPosition();
            EnsureShadowPresentation();
            flightRoutine = StartCoroutine(VerticalDropRoutine(
                impactPosition,
                Mathf.Max(0f, startHeight),
                Mathf.Max(0.01f, travelSeconds),
                spinDegrees,
                ResolveCollisionRadius(),
                collisionMask,
                onImpact));
        }

        private IEnumerator FlyRoutine(
            Vector3 startPosition,
            Vector3 impactPosition,
            float travelSeconds,
            float spinDegrees,
            float collisionRadius,
            LayerMask collisionMask,
            Action<Vector3, GameObject> onImpact)
        {
            float elapsed = 0f;
            Transform rotateTarget = visualRoot != null ? visualRoot : transform;
            Vector3 previousPosition = startPosition;

            while (elapsed < travelSeconds)
            {
                float t = Mathf.Clamp01(elapsed / travelSeconds);
                Vector3 nextPosition = ApplyFlightPose(startPosition, impactPosition, spinDegrees, rotateTarget, t);

                if (TryResolveCollision(previousPosition, nextPosition, collisionRadius, collisionMask, out Vector3 hitPosition, out GameObject hitTarget))
                {
                    FinishFlight(hitPosition, hitTarget, onImpact);
                    yield break;
                }

                previousPosition = nextPosition;
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            Vector3 finalPosition = ApplyFlightPose(startPosition, impactPosition, spinDegrees, rotateTarget, 1f);
            FinishFlight(finalPosition, null, onImpact);
        }

        private IEnumerator VerticalDropRoutine(
            Vector3 impactPosition,
            float startHeight,
            float travelSeconds,
            float spinDegrees,
            float collisionRadius,
            LayerMask collisionMask,
            Action<Vector3, GameObject> onImpact)
        {
            float elapsed = 0f;
            Transform rotateTarget = visualRoot != null ? visualRoot : transform;

            while (elapsed < travelSeconds)
            {
                float normalizedTime = Mathf.Clamp01(elapsed / travelSeconds);
                ApplyVerticalDropPose(startHeight, spinDegrees, rotateTarget, normalizedTime);

                elapsed += Time.deltaTime;
                yield return null;
            }

            ApplyVerticalDropPose(startHeight, spinDegrees, rotateTarget, 1f);
            GameObject hitTarget = ResolveImpactOverlap(impactPosition, collisionRadius, collisionMask);
            FinishFlight(impactPosition, hitTarget, onImpact);
        }

        private Vector3 ApplyFlightPose(
            Vector3 startPosition,
            Vector3 impactPosition,
            float spinDegrees,
            Transform rotateTarget,
            float normalizedTime)
        {
            Vector3 position = Vector3.Lerp(startPosition, impactPosition, normalizedTime);
            transform.position = position;

            if (rotateTarget != null)
                rotateTarget.localRotation = Quaternion.Euler(0f, 0f, spinDegrees * normalizedTime);

            return position;
        }

        private void ApplyVerticalDropPose(
            float startHeight,
            float spinDegrees,
            Transform rotateTarget,
            float normalizedTime)
        {
            float easedFall = 1f - Mathf.Pow(1f - Mathf.Clamp01(normalizedTime), 2f);
            float height = Mathf.Lerp(startHeight, 0f, easedFall);
            Transform visual = visualRoot != null ? visualRoot : transform;

            visual.localPosition = visualBaseLocalPosition + (Vector3.up * height);

            if (rotateTarget != null)
                rotateTarget.localRotation = Quaternion.Euler(0f, 0f, spinDegrees * normalizedTime);

            UpdateShadowPresentation(startHeight, height);
        }

        private bool TryResolveCollision(
            Vector3 previousPosition,
            Vector3 nextPosition,
            float collisionRadius,
            LayerMask collisionMask,
            out Vector3 hitPosition,
            out GameObject hitTarget)
        {
            hitPosition = nextPosition;
            hitTarget = null;

            if (collisionRadius <= 0f || collisionMask.value == 0)
                return false;

            Vector2 delta = nextPosition - previousPosition;
            float distance = delta.magnitude;
            if (distance > 0.0001f)
            {
                RaycastHit2D castHit = Physics2D.CircleCast(previousPosition, collisionRadius, delta / distance, distance, collisionMask);
                if (castHit.collider != null)
                {
                    hitPosition = castHit.point;
                    hitTarget = ResolveDamageTarget(castHit.collider);
                    return hitTarget != null;
                }
            }

            Collider2D overlap = Physics2D.OverlapCircle(nextPosition, collisionRadius, collisionMask);
            if (overlap == null)
                return false;

            hitPosition = overlap.ClosestPoint(nextPosition);
            hitTarget = ResolveDamageTarget(overlap);
            return hitTarget != null;
        }

        private GameObject ResolveImpactOverlap(
            Vector3 impactPosition,
            float collisionRadius,
            LayerMask collisionMask)
        {
            if (collisionRadius <= 0f || collisionMask.value == 0)
                return null;

            Collider2D overlap = Physics2D.OverlapCircle(impactPosition, collisionRadius, collisionMask);
            return ResolveDamageTarget(overlap);
        }

        private static GameObject ResolveDamageTarget(Collider2D hit)
        {
            return hit != null ? CombatTargetResolver2D.ResolveDamageTarget(hit) : null;
        }

        private float ResolveCollisionRadius()
        {
            if (collisionShape == null)
                return 0f;

            Vector3 scale = collisionShape.transform.lossyScale;
            float largestAxis = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            return collisionShape.radius * largestAxis;
        }

        private void CacheVisualBaseLocalPosition()
        {
            if (hasVisualBaseLocalPosition)
                return;

            Transform visual = visualRoot != null ? visualRoot : transform;
            visualBaseLocalPosition = visual.localPosition;
            hasVisualBaseLocalPosition = true;
        }

        private void EnsureShadowPresentation()
        {
            if (shadowRenderer != null)
                return;

            if (shadowRoot == null && autoCreateShadow)
            {
                GameObject shadowObject = new("KegShadow");
                shadowObject.transform.SetParent(transform, false);
                shadowObject.transform.localPosition = Vector3.zero;
                shadowRoot = shadowObject.transform;
            }

            if (shadowRoot == null)
                return;

            shadowRenderer = shadowRoot.GetComponent<SpriteRenderer>();
            if (shadowRenderer == null && autoCreateShadow)
                shadowRenderer = shadowRoot.gameObject.AddComponent<SpriteRenderer>();

            SpriteRenderer visualRenderer = ResolveVisualRenderer();
            if (shadowRenderer != null && visualRenderer != null)
            {
                shadowRenderer.sprite = visualRenderer.sprite;
                shadowRenderer.sortingLayerID = visualRenderer.sortingLayerID;
                shadowRenderer.sortingOrder = visualRenderer.sortingOrder - 1;
            }
        }

        private SpriteRenderer ResolveVisualRenderer()
        {
            if (visualRoot != null && visualRoot.TryGetComponent(out SpriteRenderer visualRenderer))
                return visualRenderer;

            return GetComponentInChildren<SpriteRenderer>();
        }

        private void UpdateShadowPresentation(float startHeight, float currentHeight)
        {
            if (shadowRoot == null || shadowRenderer == null)
                return;

            float airRatio = startHeight > 0.0001f
                ? Mathf.Clamp01(currentHeight / startHeight)
                : 0f;

            shadowRoot.localScale = Vector3.Lerp(shadowGroundScale, shadowAirScale, airRatio);
            Color color = shadowColor;
            color.a = Mathf.Lerp(shadowGroundAlpha, shadowAirAlpha, airRatio);
            shadowRenderer.color = color;
        }

        private void FinishFlight(
            Vector3 impactPosition,
            GameObject hitTarget,
            Action<Vector3, GameObject> onImpact)
        {
            transform.position = impactPosition;
            if (hasVisualBaseLocalPosition)
            {
                Transform visual = visualRoot != null ? visualRoot : transform;
                visual.localPosition = visualBaseLocalPosition;
            }

            flightRoutine = null;
            onImpact?.Invoke(impactPosition, hitTarget);
            Destroy(gameObject);
        }
    }
}
