using CapstoneAudio;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 기묘한 쇳덩이 투척체의 직선 이동, 회전, 충돌/만료 파괴 피드백을 담당한다.
    /// - 실제 피해 적용은 AttackBase의 CombatHitPayload 경로를 그대로 사용한다.
    /// </summary>
    public sealed class OddIronThrownWeaponProjectile2D : AttackBase
    {
        private Vector2 direction;
        private float speed;
        private float angularSpeedDegrees;
        private GameObject impactVfxPrefab;
        private SoundRef impactSound;

        public void Setup(
            ProjectileAttackSpawnContext context,
            float angularSpeedDegrees,
            GameObject impactVfxPrefab,
            in SoundRef impactSound)
        {
            if (context == null)
            {
                Debug.LogError($"[{nameof(OddIronThrownWeaponProjectile2D)}] context is null.", this);
                enabled = false;
                return;
            }

            direction = context.direction.sqrMagnitude > 0.0001f
                ? context.direction.normalized
                : Vector2.right;
            speed = Mathf.Max(0f, context.speed);
            this.angularSpeedDegrees = angularSpeedDegrees;
            this.impactVfxPrefab = impactVfxPrefab;
            this.impactSound = impactSound;

            SetupBase(context);
        }

        protected override void TickAttack(float deltaTime)
        {
            transform.position += (Vector3)(direction * speed * deltaTime);
            transform.Rotate(0f, 0f, angularSpeedDegrees * deltaTime);
        }

        protected override void OnHitWall(GameObject wall, Collider2D hitCollider)
        {
            PlayImpactFeedback(wall);
            base.OnHitWall(wall, hitCollider);
        }

        protected override void OnHitTarget(GameObject target, Collider2D hitCollider)
        {
            PlayImpactFeedback(target);
            base.OnHitTarget(target, hitCollider);
        }

        protected override void OnLifetimeExpired()
        {
            PlayImpactFeedback(null);
        }

        private void PlayImpactFeedback(GameObject target)
        {
            Vector3 position = transform.position;

            if (impactVfxPrefab != null)
            {
                GameObject resolvedPrefab = PresentationAssetPlayback.ResolvePrefab(impactVfxPrefab);
                Object.Instantiate(resolvedPrefab, position, Quaternion.identity);
            }

            if (!impactSound.IsSet)
                return;

            SoundPlaybackUtility.Play(impactSound, new SoundPlaybackContext
            {
                Instigator = OwnerSystem != null ? OwnerSystem.gameObject : null,
                Causer = Causer,
                Target = target,
                Position = position,
                SourceObject = this
            });
        }
    }
}
