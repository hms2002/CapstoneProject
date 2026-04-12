using UnityEngine;

namespace UnityGAS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
    public sealed class LightBeadProjectile2D : AttackBase
    {
        private Vector2 direction = Vector2.right;
        private float speed;

        /// <summary>발사 방향과 속도를 받아 초기화합니다.</summary>
        public void Setup(ProjectileAttackSpawnContext context)
        {
            if (context == null)
            {
                Debug.LogError($"[{nameof(LightBeadProjectile2D)}] context is null.", this);
                enabled = false;
                return;
            }

            direction = context.direction.sqrMagnitude > 0.0001f
                ? context.direction.normalized
                : Vector2.right;

            speed = Mathf.Max(0f, context.speed);

            SetupBase(context);
        }

        protected override void TickAttack(float deltaTime)
        {
            transform.position += (Vector3)(direction * speed * deltaTime);
        }

        /// <summary>
        /// 책임 :
        /// - LightBead 투사체는 다른 공격체를 유효 타격 대상으로 보지 않게 한다.
        /// - 폭주 패턴처럼 여러 발이 동시에 생성될 때 투사체끼리 부딪혀 즉시 삭제되는 문제를 막는다.
        /// </summary>
        protected override bool CanHitTarget(GameObject target)
        {
            if (target == null)
                return false;

            return target.GetComponent<AttackBase>() == null;
        }

        /// <summary>
        /// 책임 :
        /// - LightBead 투사체가 촛대 본체나 촛불 광역 트리거에 닿아도 환경 충돌로 소멸하지 않게 한다.
        /// - 봉인된 촛대 폭주 패턴에서는 플레이어나 실제 벽 충돌일 때만 사라지게 한다.
        /// </summary>
        protected override bool CanHitWall(GameObject wall, Collider2D hitCollider)
        {
            if (wall == null && hitCollider == null)
                return false;

            if ((wall != null && wall.GetComponentInParent<Candlestick>() != null) ||
                (hitCollider != null && hitCollider.GetComponentInParent<Candlestick>() != null))
            {
                return false;
            }

            if ((wall != null && wall.GetComponentInParent<CandlestickLightZone>() != null) ||
                (hitCollider != null && hitCollider.GetComponentInParent<CandlestickLightZone>() != null))
            {
                return false;
            }

            return true;
        }
    }
}
