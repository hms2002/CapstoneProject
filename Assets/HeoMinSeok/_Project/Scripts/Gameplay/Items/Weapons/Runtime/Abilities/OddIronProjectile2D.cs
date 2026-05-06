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
        private Vector2 direction;
        private float speed;

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

        protected override void TickAttack(float deltaTime)
        {
            transform.position += (Vector3)(direction * speed * deltaTime);
        }
    }
}
