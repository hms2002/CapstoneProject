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
    }
}
