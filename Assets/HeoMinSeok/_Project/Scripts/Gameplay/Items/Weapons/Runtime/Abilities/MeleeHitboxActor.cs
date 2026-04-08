using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 근접 히트박스 공격체가 추가로 필요로 하는 생성 문맥을 보관한다.
    /// - 월드 위치, 히트박스 크기, 타겟당 1회 타격 여부, 방향 정보를 전달한다.
    /// </summary>
    public sealed class MeleeHitboxSpawnContext : AttackSpawnContext
    {
        public Vector2 worldPosition;
        public Vector2 hitboxSize = Vector2.one;
        public bool hitOncePerTarget = true;
        public bool destroyOnFirstHit = false;
        public Vector2 direction = Vector2.right;
    }

    /// <summary>
    /// 책임 :
    /// - 짧게 생성되는 근접 공격 판정 엔티티를 담당한다.
    /// - BoxCollider2D를 히트박스로 사용하고, 생성 직후 즉시 스캔 + 이후 Trigger 진입을 처리한다.
    /// - 타겟당 1회 타격 정책을 유지하면서 짧은 수명 동안 실제 공격 범위를 월드에 유지한다.
    /// - 공격 방향에 맞춰 공격체 자체를 회전시켜, 비주얼과 히트박스가 함께 방향을 맞추게 한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class MeleeHitboxActor : AttackBase
    {
        [Header("Optional Visual")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float actorAngleOffsetDeg = 0f;
        [SerializeField] private float visualLocalAngleOffsetDeg = 0f;

        private BoxCollider2D hitboxCollider;
        private readonly HashSet<int> hitTargetIds = new();

        private bool hitOncePerTarget = true;
        private bool destroyOnFirstHit = false;
        private bool authoredShapeCached;
        private Vector2 authoredColliderSize = Vector2.one;
        private Vector3 authoredLocalScale = Vector3.one;

        /// <summary>
        /// 책임 :
        /// - 근접 히트박스의 위치, 월드 크기, 회전, 타격 정책을 초기화한다.
        /// - 공통 공격체 초기화를 완료한 뒤 생성 직후 겹쳐 있는 적을 즉시 스캔한다.
        /// </summary>
        public void Setup(MeleeHitboxSpawnContext context)
        {
            if (context == null)
            {
                Debug.LogError($"[{nameof(MeleeHitboxActor)}] context is null.", this);
                enabled = false;
                return;
            }

            if (hitboxCollider == null)
                hitboxCollider = GetComponent<BoxCollider2D>();

            CacheAuthoredShapeIfNeeded();

            transform.position = context.worldPosition;
            ApplyActorScale(context.hitboxSize);
            ApplyActorRotation(context.direction);

            hitboxCollider.size = authoredColliderSize;
            hitboxCollider.isTrigger = true;

            hitOncePerTarget = context.hitOncePerTarget;
            destroyOnFirstHit = context.destroyOnFirstHit;

            ApplyVisualLocalRotation();
            SetupBase(context);

            PerformImmediateScan();
        }

        /// <summary>
        /// 책임 :
        /// - 프리팹이 원래 가지고 있던 콜라이더 크기와 로컬 스케일을 한 번만 캐시한다.
        /// - 이후 원하는 월드 크기를 transform scale로 환산할 때 기준값으로 사용한다.
        /// </summary>
        private void CacheAuthoredShapeIfNeeded()
        {
            if (authoredShapeCached)
                return;

            authoredShapeCached = true;
            authoredColliderSize = hitboxCollider != null ? hitboxCollider.size : Vector2.one;
            authoredLocalScale = transform.localScale;
        }

        /// <summary>
        /// 책임 :
        /// - 원하는 월드 히트박스 크기를 프리팹의 기본 콜라이더 크기 기준 transform scale로 변환한다.
        /// - 비주얼과 BoxCollider2D가 같은 transform scale을 공유하게 만들어 판정과 연출 크기를 일치시킨다.
        /// </summary>
        private void ApplyActorScale(Vector2 desiredWorldSize)
        {
            if (!authoredShapeCached)
                return;

            float baseWidth = Mathf.Abs(authoredColliderSize.x) > 0.0001f ? Mathf.Abs(authoredColliderSize.x) : 1f;
            float baseHeight = Mathf.Abs(authoredColliderSize.y) > 0.0001f ? Mathf.Abs(authoredColliderSize.y) : 1f;

            float scaleX = Mathf.Max(0.0001f, desiredWorldSize.x) / baseWidth;
            float scaleY = Mathf.Max(0.0001f, desiredWorldSize.y) / baseHeight;

            transform.localScale = new Vector3(
                authoredLocalScale.x * scaleX,
                authoredLocalScale.y * scaleY,
                authoredLocalScale.z);
        }

        /// <summary>
        /// 책임 :
        /// - 타겟당 1회 타격 정책을 처리한다.
        /// - 정책이 켜져 있으면 이미 맞은 대상은 다시 맞지 않게 막는다.
        /// </summary>
        protected override bool CanHitTarget(GameObject target)
        {
            if (!hitOncePerTarget || target == null)
                return true;

            return hitTargetIds.Add(target.GetInstanceID());
        }

        /// <summary>
        /// 책임 :
        /// - 대상 적중 후 제거 정책을 처리한다.
        /// - 근접 1타는 기본적으로 다수 대상을 동시에 맞출 수 있어야 하므로 기본값은 제거하지 않는다.
        /// </summary>
        protected override void OnHitTarget(GameObject target, Collider2D hitCollider)
        {
            if (destroyOnFirstHit)
                DestroySelf();
        }

        /// <summary>
        /// 책임 :
        /// - 공격 방향에 맞춰 공격체 전체를 회전시킨다.
        /// - BoxCollider2D 판정도 함께 회전하므로, 히트박스와 비주얼의 방향이 일치한다.
        /// - 기본 정면은 +X(오른쪽) 기준으로 가정한다.
        /// </summary>
        private void ApplyActorRotation(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            Vector2 dir = direction.normalized;
            float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angleDeg + actorAngleOffsetDeg);
        }

        /// <summary>
        /// 책임 :
        /// - visualRoot가 있을 경우, 공격체 회전 위에 추가 로컬 보정 회전을 적용한다.
        /// - 이펙트 프리팹의 기본 정면 축이 다를 때 미세 보정 용도로 사용한다.
        /// </summary>
        private void ApplyVisualLocalRotation()
        {
            if (visualRoot == null)
                return;

            visualRoot.localRotation = Quaternion.Euler(0f, 0f, visualLocalAngleOffsetDeg);
        }

        /// <summary>
        /// 책임 :
        /// - 생성 직후 이미 겹쳐 있는 대상을 즉시 판정한다.
        /// - Trigger 경로와 같은 기준(ResolveDamageTarget 후 루트 레이어 검사)을 사용한다.
        /// - 회전된 BoxCollider2D의 중심/크기/각도를 그대로 사용해 즉시 스캔 결과를 맞춘다.
        /// </summary>
        private void PerformImmediateScan()
        {
            if (hitboxCollider == null)
                return;

            Vector2 worldCenter = hitboxCollider.transform.TransformPoint(hitboxCollider.offset);

            Vector3 lossy = hitboxCollider.transform.lossyScale;
            Vector2 absScale = new Vector2(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y));
            Vector2 worldSize = Vector2.Scale(hitboxCollider.size, absScale);

            float angleDeg = hitboxCollider.transform.eulerAngles.z;

            Collider2D[] results = Physics2D.OverlapBoxAll(worldCenter, worldSize, angleDeg);
            if (results == null || results.Length == 0)
                return;

            for (int i = 0; i < results.Length; i++)
            {
                var other = results[i];
                if (other == null)
                    continue;

                var targetRoot = CombatTargetResolver2D.ResolveDamageTarget(other);
                if (targetRoot == null || targetRoot == IgnoreTarget)
                    continue;

                int layerBit = 1 << targetRoot.layer;
                if ((DamageLayers.value & layerBit) == 0)
                    continue;

                if (!CanHitTarget(targetRoot))
                    continue;

                if (!TryApplyHit(targetRoot, other))
                    continue;

                OnHitTarget(targetRoot, other);

                if (destroyOnFirstHit)
                    return;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (hitboxCollider == null)
                hitboxCollider = GetComponent<BoxCollider2D>();

            if (hitboxCollider == null)
                return;

            Gizmos.matrix = hitboxCollider.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(hitboxCollider.offset, hitboxCollider.size);
        }
#endif
    }
}
