using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 모든 공격체가 공통으로 필요로 하는 생성 문맥을 보관한다.
    /// - owner, 발사 시점 spec, lifetime, 충돌 레이어, 공통 피해 payload를 하나로 묶어 전달한다.
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
        public CombatHitPayload hitPayload;
    }

    /// <summary>
    /// 책임 :
    /// - 투사체 공격체가 추가로 필요로 하는 이동 초기값을 보관한다.
    /// - 방향과 속도를 AttackSpawnContext에 덧붙인다.
    /// </summary>
    public sealed class ProjectileAttackSpawnContext : AttackSpawnContext
    {
        public Vector2 direction;
        public float speed;
    }

    /// <summary>
    /// 책임 :
    /// - 월드에 생성된 실제 공격 엔티티의 공통 흐름을 담당한다.
    /// - 발사 시점 문맥 보관, 수명 감소, 충돌 판정 해석, 공통 payload 기반 피해 적용, 기본 제거 정책을 관리한다.
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
        protected CombatHitPayload HitPayload { get; private set; }

        private float lifeRemaining;
        private bool isInitialized;

        /// <summary>
        /// 책임 :
        /// - 공격체 공통 초기화를 수행한다.
        /// - 발사 시점 문맥을 고정하고 owner와의 충돌 무시를 설정한 뒤 파생형 후처리를 호출한다.
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

            NormalizeHitPayload();
            IgnoreOwnerCollision();
            OnSetupCompleted();
        }

        /// <summary>
        /// 책임 :
        /// - 파생형이 공통 초기화 직후 필요한 후처리를 추가할 수 있게 한다.
        /// </summary>
        protected virtual void OnSetupCompleted()
        {
        }

        /// <summary>
        /// 책임 :
        /// - 파생형이 매 프레임 수행할 이동/추적 같은 개별 동작을 처리하게 한다.
        /// </summary>
        protected virtual void TickAttack(float deltaTime)
        {
        }

        /// <summary>
        /// 책임 :
        /// - 파생형이 타격 허용 여부를 정책적으로 제어할 수 있게 한다.
        /// - 기본 구현은 "유효 대상이면 타격 가능"이다.
        /// </summary>
        protected virtual bool CanHitTarget(GameObject target)
        {
            return true;
        }

        /// <summary>
        /// 책임 :
        /// - 벽 충돌 후 기본 반응을 정의한다.
        /// - 기본 정책은 즉시 제거다.
        /// </summary>
        protected virtual void OnHitWall(GameObject wall, Collider2D hitCollider)
        {
            DestroySelf();
        }

        /// <summary>
        /// 책임 :
        /// - 유효한 타격 대상 적중 후 기본 반응을 정의한다.
        /// - 기본 정책은 즉시 제거다.
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

            var targetRoot = CombatTargetResolver2D.ResolveDamageTarget(other);
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
        /// 책임 :
        /// - 현재 공격체가 가진 payload를 공용 피해 적용 규약에 맞게 정규화한다.
        /// - sourceSystem/sourceSpec/causer가 비어 있으면 AttackBase가 가진 발사 문맥으로 보정한다.
        /// </summary>
        private void NormalizeHitPayload()
        {
            if (HitPayload == null)
                return;

            if (HitPayload.sourceSystem == null)
                HitPayload.sourceSystem = OwnerSystem;

            if (HitPayload.sourceSpec == null)
                HitPayload.sourceSpec = SourceSpec;

            if (HitPayload.causer == null)
                HitPayload.causer = Causer;
        }

        /// <summary>
        /// 책임 :
        /// - 공용 CombatHitPayloadApplier를 통해 피해를 적용한다.
        /// - AttackBase는 더 이상 직접 CombatDamageAction 세부 인자를 펼치지 않는다.
        /// </summary>
        protected bool TryApplyHit(GameObject target)
        {
            if (target == null || HitPayload == null)
                return false;

            return CombatHitPayloadApplier.Apply(target, HitPayload);
        }

        /// <summary>
        /// 책임 :
        /// - owner와 공격체의 첫 충돌을 무시해 자기 자신을 맞추는 상황을 방지한다.
        /// - 현재는 대표 Collider2D 1개 기준으로 처리한다.
        /// </summary>
        private void IgnoreOwnerCollision()
        {
            var myCol = GetComponent<Collider2D>();
            var ownerCol = IgnoreTarget != null ? IgnoreTarget.GetComponent<Collider2D>() : null;

            if (myCol != null && ownerCol != null)
                Physics2D.IgnoreCollision(myCol, ownerCol, true);
        }

        /// <summary>
        /// 책임 :
        /// - 공격체 제거를 한 곳에서 수행한다.
        /// </summary>
        protected void DestroySelf()
        {
            if (gameObject != null)
                Destroy(gameObject);
        }
    }
}