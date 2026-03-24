using UnityEngine;
using UnityGAS;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// 발사 시점에 고정되어야 하는 피해 payload를 보관한다.
    /// AttackBase가 충돌 시 CombatDamageAction으로 넘길 수 있는 값을 하나로 묶는다.
    /// </summary>
    [System.Serializable]
    public sealed class AttackHitPayload
    {
        public GameplayEffect damageEffect;
        public GE_Knockback_Spec knockbackEffect;
        public float finalHpDamage;
        public float finalStaggerBuildUp;
        public ElementDamageResult[] elementDamages;
        public float finalKnockbackImpulse;
        public GameplayTag hitConfirmedTag;
    }

    /// <summary>
    /// 책임:
    /// 모든 공격체가 공통으로 필요로 하는 생성 문맥을 보관한다.
    /// 소유자, 발사 시점 spec, 충돌 레이어, lifetime, 피해 payload를 묶어 전달한다.
    /// </summary>
    public class AttackSpawnContext
    {
        public AbilitySystem ownerSystem;
        public AbilitySpec sourceSpec;
        public GameObject causer;
        public GameObject ignoreTarget;
        public float lifetime;
        public LayerMask wallLayers;
        public LayerMask damageLayers;
        public AttackHitPayload hitPayload;
    }

    /// <summary>
    /// 책임:
    /// 투사체 공격체가 추가로 필요로 하는 이동 문맥을 보관한다.
    /// 방향과 속도처럼 Projectile 전용 초기화 값을 전달한다.
    /// </summary>
    public sealed class ProjectileAttackSpawnContext : AttackSpawnContext
    {
        public Vector2 direction;
        public float speed;
    }

    /// <summary>
    /// 책임:
    /// 월드에 생성된 실제 공격 엔티티의 공통 흐름을 담당한다.
    /// 발사 시점 문맥 보관, lifetime 감소, 충돌 해석, 피해 적용, 기본 제거 정책을 관리한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public abstract class AttackBase : MonoBehaviour
    {
        protected AbilitySystem OwnerSystem { get; private set; }
        protected AbilitySpec SourceSpec { get; private set; }
        protected GameObject Causer { get; private set; }
        protected GameObject IgnoreTarget { get; private set; }
        protected LayerMask WallLayers { get; private set; }
        protected LayerMask DamageLayers { get; private set; }
        protected AttackHitPayload HitPayload { get; private set; }

        private float lifeRemaining;
        private bool isInitialized;

        /// <summary>
        /// 책임:
        /// 공격체 공통 초기화를 수행한다.
        /// 발사 시점 문맥을 고정하고, owner와의 충돌 무시 및 파생형 후속 초기화를 시작한다.
        /// </summary>
        public virtual void SetupBase(AttackSpawnContext context)
        {
            if (context == null)
            {
                Debug.LogError($"[{nameof(AttackBase)}] context is null.", this);
                enabled = false;
                return;
            }

            if (context.ownerSystem == null)
            {
                Debug.LogError($"[{nameof(AttackBase)}] ownerSystem is null.", this);
                enabled = false;
                return;
            }

            OwnerSystem = context.ownerSystem;
            SourceSpec = context.sourceSpec;
            Causer = context.causer != null ? context.causer : context.ownerSystem.gameObject;
            IgnoreTarget = context.ignoreTarget;
            WallLayers = context.wallLayers;
            DamageLayers = context.damageLayers;
            HitPayload = context.hitPayload;
            lifeRemaining = context.lifetime;
            isInitialized = true;

            IgnoreOwnerCollision();
            OnSetupCompleted();
        }

        /// <summary>
        /// 책임:
        /// 파생형이 공통 초기화 직후 필요한 후처리를 추가할 수 있게 한다.
        /// </summary>
        protected virtual void OnSetupCompleted()
        {
        }

        /// <summary>
        /// 책임:
        /// 파생형의 프레임별 이동/추적 등 개별 동작을 수행하게 한다.
        /// </summary>
        protected virtual void TickAttack(float deltaTime)
        {
        }

        /// <summary>
        /// 책임:
        /// 파생형이 대상별 타격 허용 여부를 오버라이드할 수 있게 한다.
        /// 기본값은 "맞출 수 있으면 1회 처리"다.
        /// </summary>
        protected virtual bool CanHitTarget(GameObject target)
        {
            return true;
        }

        /// <summary>
        /// 책임:
        /// 벽 충돌 후 기본 반응을 정의한다.
        /// 기본 정책은 즉시 제거다.
        /// </summary>
        protected virtual void OnHitWall(GameObject wall, Collider2D hitCollider)
        {
            DestroySelf();
        }

        /// <summary>
        /// 책임:
        /// 유효한 피해 대상 타격 후 기본 반응을 정의한다.
        /// 기본 정책은 즉시 제거다.
        /// </summary>
        protected virtual void OnHitTarget(GameObject target, Collider2D hitCollider)
        {
            DestroySelf();
        }

        protected virtual void Update()
        {
            if (!isInitialized)
                return;

            TickAttack(Time.deltaTime);

            lifeRemaining -= Time.deltaTime;
            if (lifeRemaining <= 0f)
                DestroySelf();
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (!isInitialized || other == null)
                return;

            var targetRoot = ResolveHitRoot(other);
            if (targetRoot == null || targetRoot == IgnoreTarget)
                return;

            int layerBit = 1 << targetRoot.layer;

            if ((WallLayers.value & layerBit) != 0)
            {
                OnHitWall(targetRoot, other);
                return;
            }

            if ((DamageLayers.value & layerBit) == 0)
                return;

            if (!CanHitTarget(targetRoot))
                return;

            if (!TryApplyHit(targetRoot))
                return;

            OnHitTarget(targetRoot, other);
        }

        /// <summary>
        /// 책임:
        /// 충돌한 콜라이더로부터 실제 타격 대상으로 사용할 루트 GameObject를 정규화한다.
        /// Rigidbody2D가 있으면 그 GameObject를 우선 사용한다.
        /// </summary>
        protected GameObject ResolveHitRoot(Collider2D other)
        {
            if (other == null)
                return null;

            if (other.attachedRigidbody != null)
                return other.attachedRigidbody.gameObject;

            return other.gameObject;
        }

        /// <summary>
        /// 책임:
        /// 발사 시점에 캡처한 문맥으로 피해를 적용한다.
        /// 현재 실행 중인 Ability 상태를 다시 읽지 않고, 생성 당시의 sourceSpec/payload를 사용한다.
        /// </summary>
        protected bool TryApplyHit(GameObject target)
        {
            if (target == null)
                return false;

            if (OwnerSystem == null || HitPayload == null || HitPayload.damageEffect == null)
                return false;

            CombatDamageAction.ApplyDamageAndEmitHit(
                system: OwnerSystem,
                spec: SourceSpec,
                damageEffect: HitPayload.damageEffect,
                knockbackEffect: HitPayload.knockbackEffect,
                target: target,
                finalHpDamage: HitPayload.finalHpDamage,
                finalStaggerBuildUp: HitPayload.finalStaggerBuildUp,
                elementBuildUps: HitPayload.elementDamages,
                finalKnockbackImpulse: HitPayload.finalKnockbackImpulse,
                hitConfirmedTag: HitPayload.hitConfirmedTag,
                causer: Causer);

            return true;
        }

        /// <summary>
        /// 책임:
        /// owner와 공격체의 첫 충돌을 무시해 자기 자신을 맞추는 일을 방지한다.
        /// 현재는 대표 Collider 1개 기준으로 처리한다.
        /// </summary>
        private void IgnoreOwnerCollision()
        {
            var myCol = GetComponent<Collider2D>();
            var ownerCol = IgnoreTarget != null ? IgnoreTarget.GetComponent<Collider2D>() : null;

            if (myCol != null && ownerCol != null)
                Physics2D.IgnoreCollision(myCol, ownerCol, true);
        }

        /// <summary>
        /// 책임:
        /// 공격체 제거를 한 곳에서 수행한다.
        /// </summary>
        protected void DestroySelf()
        {
            if (gameObject != null)
                Destroy(gameObject);
        }
    }
}