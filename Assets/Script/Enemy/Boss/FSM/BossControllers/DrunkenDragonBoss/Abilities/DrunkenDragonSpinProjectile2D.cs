using UnityEngine;
using UnityGAS;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// 취룡 회전 패턴에서 발사되는 단순 직선 탄막의 이동만 담당하고, 수명/충돌/피해 적용은 AttackBase에 위임한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
    public sealed class DrunkenDragonSpinProjectile2D : AttackBase
    {
        private Vector2 direction = Vector2.right;
        private float speed;

        /// <summary>
        /// 책임:
        /// 회전 패턴이 전달한 발사 방향과 속도를 고정하고 공통 공격체 초기화를 수행한다.
        /// </summary>
        public void Setup(ProjectileAttackSpawnContext context)
        {
            if (context == null)
            {
                Debug.LogError($"[{nameof(DrunkenDragonSpinProjectile2D)}] context is null.", this);
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
    }
}
