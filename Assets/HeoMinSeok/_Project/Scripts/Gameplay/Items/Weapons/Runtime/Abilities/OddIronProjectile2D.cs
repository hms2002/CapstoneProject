using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 기묘한 쇳덩이 탄환의 직선 이동과 방향 회전만 담당한다.
    /// - 수명, 충돌 해석, 피해 적용, 제거 정책은 AttackBase 공통 규약을 따른다.
    /// </summary>
    public sealed class OddIronProjectile2D : AttackBase
    {
        private readonly RaycastHit2D[] sweepHits = new RaycastHit2D[12];

        private Vector2 direction;
        private float speed;
        private Collider2D projectileCollider;
        private float sweepRadius = 0.03f;

        public void Setup(ProjectileAttackSpawnContext context)
        {
            if (context == null)
            {
                Debug.LogError($"[{nameof(OddIronProjectile2D)}] context is null.", this);
                enabled = false;
                return;
            }

            direction = context.direction.sqrMagnitude > 0.0001f
                ? context.direction.normalized
                : Vector2.right;
            speed = Mathf.Max(0f, context.speed);

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            SetupBase(context);
        }

        protected override void OnSetupCompleted()
        {
            projectileCollider = GetComponent<Collider2D>();
            sweepRadius = ResolveSweepRadius(projectileCollider);
        }

        protected override void TickAttack(float deltaTime)
        {
            Vector2 displacement = direction * speed * deltaTime;
            if (TrySweepHit(displacement))
                return;

            transform.position += (Vector3)displacement;
        }

        /// <summary>
        /// 책임 :
        /// - 빠른 OddIron 탄환이 프레임 사이에 얇은 TilemapCollider/허트박스를 건너뛰지 않도록 이동 전 swept cast를 수행한다.
        /// - 실제 충돌 해석과 피해 적용은 AttackBase의 공통 wall/target 규약을 그대로 따른다.
        /// </summary>
        private bool TrySweepHit(Vector2 displacement)
        {
            float distance = displacement.magnitude;
            if (distance <= 0.0001f)
                return false;

            int layerMask = WallLayers.value | DamageLayers.value;
            if (layerMask == 0)
                return false;

            int hitCount = Physics2D.CircleCastNonAlloc(
                transform.position,
                sweepRadius,
                direction,
                sweepHits,
                distance,
                layerMask);

            if (hitCount <= 0)
                return false;

            System.Array.Sort(sweepHits, 0, hitCount, RaycastHitDistanceComparer.Instance);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = sweepHits[i];
                Collider2D hitCollider = hit.collider;
                if (hitCollider == null || hitCollider == projectileCollider)
                    continue;

                int hitLayerBit = 1 << hitCollider.gameObject.layer;
                if ((WallLayers.value & hitLayerBit) != 0)
                {
                    if (!CanHitWall(hitCollider.gameObject, hitCollider))
                        continue;

                    transform.position = hit.centroid;
                    OnHitWall(hitCollider.gameObject, hitCollider);
                    return true;
                }

                GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(hitCollider);
                if (targetRoot == null || IsIgnoredTarget(targetRoot))
                    continue;

                int targetLayerBit = 1 << targetRoot.layer;
                if ((DamageLayers.value & targetLayerBit) == 0)
                    continue;

                if (!CanHitTarget(targetRoot))
                    continue;

                transform.position = hit.centroid;
                if (!TryApplyHit(targetRoot, hitCollider))
                    return true;

                OnHitTarget(targetRoot, hitCollider);
                return true;
            }

            return false;
        }

        private static float ResolveSweepRadius(Collider2D collider)
        {
            if (collider is CircleCollider2D circle)
                return Mathf.Max(0.01f, circle.radius * Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x), Mathf.Abs(circle.transform.lossyScale.y)));

            if (collider != null)
                return Mathf.Max(0.01f, Mathf.Min(collider.bounds.extents.x, collider.bounds.extents.y));

            return 0.03f;
        }

        /// <summary>
        /// 책임 :
        /// - swept cast 결과를 가까운 충돌부터 처리할 수 있도록 거리순으로 정렬한다.
        /// </summary>
        private sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit2D>
        {
            public static readonly RaycastHitDistanceComparer Instance = new();

            public int Compare(RaycastHit2D x, RaycastHit2D y)
            {
                return x.distance.CompareTo(y.distance);
            }
        }
    }
}
