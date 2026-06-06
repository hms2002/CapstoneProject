using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// SwordSkill1 투사체의 이동 규칙만 담당한다.
    /// 공통 수명, 충돌 해석, 피해 적용, 제거 정책은 AttackBase가 담당한다.
    /// </summary>
    public sealed class SwordSkill1Projectile2D : AttackBase
    {
        private Vector2 dir;
        private float speed;

        /// <summary>
        /// 책임:
        /// 투사체 전용 이동값을 초기화하고, 공통 공격체 초기화를 이어서 수행한다.
        /// </summary>
        public void Setup(ProjectileAttackSpawnContext context)
        {
            if (context == null)
            {
                Debug.LogError($"[{nameof(SwordSkill1Projectile2D)}] context is null.", this);
                enabled = false;
                return;
            }

            dir = context.direction.sqrMagnitude > 0.0001f
                ? context.direction.normalized
                : Vector2.right;

            speed = Mathf.Max(0f, context.speed);

            SetupBase(context);
        }

        protected override void TickAttack(float deltaTime)
        {
            transform.position += (Vector3)(dir * speed * deltaTime);
        }
    }
}