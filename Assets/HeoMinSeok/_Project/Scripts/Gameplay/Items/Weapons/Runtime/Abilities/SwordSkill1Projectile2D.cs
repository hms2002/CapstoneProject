using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    [RequireComponent(typeof(Collider2D))]
    public class SwordSkill1Projectile2D : MonoBehaviour
    {
        private Vector2 dir;
        private float speed;
        private float life;
        private LayerMask wallLayers;
        private LayerMask damageLayers;

        private AbilitySystem ownerSystem;
        private GameplayEffect damageEffect;
        private float damage;
        private float staggerBuildUp;
        private ElementDamageResult[] elementDamages;
        private GameObject ignoreGO;

        public void Setup(
            AbilitySystem owner,
            Vector2 direction,
            float spd,
            float lifetime,
            LayerMask walls,
            LayerMask dmgLayers,
            GameplayEffect dmgEffect,
            float dmg,
            float staggerBuildUp,
            ElementDamageResult[] elementDamages,
            GameObject ignore)
        {
            ownerSystem = owner;
            dir = direction.normalized;
            speed = spd;
            life = lifetime;
            wallLayers = walls;
            damageLayers = dmgLayers;
            damageEffect = dmgEffect;
            damage = dmg;
            this.staggerBuildUp = staggerBuildUp;
            this.elementDamages = elementDamages;
            ignoreGO = ignore;

            // owner 충돌 무시(가능하면)
            var myCol = GetComponent<Collider2D>();
            var ownerCol = ignoreGO != null ? ignoreGO.GetComponent<Collider2D>() : null;
            if (myCol != null && ownerCol != null)
                Physics2D.IgnoreCollision(myCol, ownerCol, true);
        }

        private void Update()
        {
            transform.position += (Vector3)(dir * speed * Time.deltaTime);
            life -= Time.deltaTime;
            if (life <= 0f) Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null) return;

            var go = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            if (go == null || go == ignoreGO) return;

            int layerBit = 1 << go.layer;

            // 벽이면 그냥 소멸
            if ((wallLayers.value & layerBit) != 0)
            {
                Destroy(gameObject);
                return;
            }

            // 데미지 대상이면 데미지 적용 후 소멸
            if ((damageLayers.value & layerBit) != 0 && damageEffect != null && ownerSystem != null)
            {
                CombatDamageAction.ApplyDamageAndEmitHit(
                    ownerSystem,
                    ownerSystem.CurrentExecSpec,
                    damageEffect,
                    go,
                    damage,
                    staggerBuildUp,
                    elementDamages,
                    hitConfirmedTag: null,
                    causer: ownerSystem.gameObject
                );

                Destroy(gameObject);
                return;
            }

            // 그 외 충돌은 무시(원하면 여기서 소멸 처리 가능)
        }
    }
}
