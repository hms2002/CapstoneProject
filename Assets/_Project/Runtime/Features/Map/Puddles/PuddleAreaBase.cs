using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 술/불 장판 actor의 공통 생명주기, collider 모드 전환, 흡수 탄막 이동을 관리한다.
    /// - 실제 술 버프, 불 DOT, 점화 규칙은 파생 클래스와 서비스가 소유한다.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class PuddleAreaBase : MonoBehaviour
    {
        [Header("Puddle")]
        [SerializeField, Min(0.01f)] private float groundRadius = 1.35f;
        [SerializeField, Min(0.01f)] private float projectileRadius = 0.25f;
        [SerializeField, Range(0.1f, 1f)] private float ignitionContactRadiusScale = 0.85f;

        [Header("Colliders")]
        [SerializeField] private CircleCollider2D groundCollider;
        [SerializeField] private CircleCollider2D projectileCollider;

        [Header("Absorb Projectile")]
        [SerializeField, Min(0.01f)] private float absorbMoveSpeed = 5f;
        [SerializeField, Min(0f)] private float absorbGatherDelaySeconds = 0.75f;
        [SerializeField, Min(0.01f)] private float arriveDistance = 0.15f;

        [Header("Presentation")]
        [SerializeField] private PuddleShaderVisual shaderVisual;
        [SerializeField] private PuddleParticleVisual particleVisual;
        [SerializeField] private PuddleBlobVisual blobVisual;
        [SerializeField] private bool logDebugMessages;

        private Coroutine absorbRoutine;
        private Action<PuddleAreaBase> onAbsorbArrived;
        private readonly HashSet<GameObject> projectileHitTargets = new();
        private PuddleAreaMode modeBeforeAbsorb = PuddleAreaMode.Ground;

        public event Action<PuddleAreaBase> Consumed;

        public abstract PuddleElementType ElementType { get; }
        public PuddleAreaMode Mode { get; private set; } = PuddleAreaMode.Ground;
        public float GroundRadius => groundRadius;
        public float ProjectileRadius => projectileRadius;
        public float IgnitionContactRadius => groundRadius * ignitionContactRadiusScale;
        public bool CanApplyGroundEffect => Mode == PuddleAreaMode.Ground;
        public bool IsAbsorbTransitioning => Mode == PuddleAreaMode.AbsorbPreparing;
        public bool CanApplyProjectileEffect => Mode == PuddleAreaMode.AbsorbProjectile;
        public bool IsGroundActive => Mode == PuddleAreaMode.Ground || Mode == PuddleAreaMode.Igniting;
        protected PuddleShaderVisual ShaderVisual => shaderVisual;
        protected PuddleParticleVisual ParticleVisual => particleVisual;
        protected PuddleBlobVisual BlobVisual => blobVisual;

        protected virtual void Awake()
        {
            CacheColliders();
            CachePresentation();
            SyncPresentationContext();
            ApplyColliderMode(PuddleAreaMode.Ground);
            shaderVisual?.SetMode(PuddleAreaMode.Ground);
            particleVisual?.ApplyMode(PuddleAreaMode.Ground);
        }

        protected virtual void OnEnable()
        {
            CacheColliders();
            CachePresentation();
            SyncPresentationContext();
            PuddleManager.ResolveForScene()?.Register(this);
            ApplyColliderMode(Mode);
            shaderVisual?.SetMode(Mode);
            particleVisual?.ApplyMode(Mode);
        }

        protected virtual void OnDisable()
        {
            StopAbsorbMotion();
            PuddleManager.ResolveForScene()?.Unregister(this);
        }

        protected virtual void OnValidate()
        {
            groundRadius = Mathf.Max(0.01f, groundRadius);
            projectileRadius = Mathf.Max(0.01f, projectileRadius);
            ignitionContactRadiusScale = Mathf.Clamp(ignitionContactRadiusScale, 0.1f, 1f);
            CacheColliders();
            CachePresentation();
            SyncPresentationContext();
            SyncColliderRadii();
        }

        /// <summary>
        /// 책임 :
        /// - 장판을 흡수 탄막 모드로 전환하고 지정 흡수 지점으로 이동시킨다.
        /// - 도착 결과 적용은 콜백을 통해 보스/흡수 서비스가 처리한다.
        /// </summary>
        public void EnterAbsorbProjectile(
            Transform absorbAnchor,
            float speedOverride,
            Action<PuddleAreaBase> onArrived)
        {
            if (absorbAnchor == null || Mode == PuddleAreaMode.Consumed)
                return;

            StopAbsorbMotion();
            onAbsorbArrived = onArrived;
            modeBeforeAbsorb = IsGroundActive ? Mode : PuddleAreaMode.Ground;
            shaderVisual?.SetAbsorbAnchor(absorbAnchor);
            SetMode(PuddleAreaMode.AbsorbPreparing);

            float speed = speedOverride > 0f ? speedOverride : absorbMoveSpeed;
            LogDebug($"흡수 탄막 준비 시작: anchor={absorbAnchor.name}, speedOverride={speedOverride}, resolvedSpeed={speed}, gatherDelay={absorbGatherDelaySeconds}");
            absorbRoutine = StartCoroutine(AbsorbGatherThenMoveRoutine(absorbAnchor, speed));
        }

        /// <summary>
        /// 책임 :
        /// - 장판을 소비 완료 상태로 전환하고 collider 판정을 모두 끈다.
        /// - 풀링/파괴 정책은 호출자가 선택할 수 있게 GameObject 파괴는 수행하지 않는다.
        /// </summary>
        public virtual void MarkConsumed()
        {
            if (Mode == PuddleAreaMode.Consumed)
                return;

            StopAbsorbMotion();
            SetMode(PuddleAreaMode.Consumed);
            Consumed?.Invoke(this);
        }

        /// <summary>
        /// 책임 :
        /// - 흡수 패턴이 사망/그로기 등으로 중단될 때 이동 중인 장판 탄막을 보상 처리 없이 제거한다.
        /// - 도착 콜백을 끊어 보스 흡수 결과와 스태거 증감이 취소 후 뒤늦게 발생하지 않도록 보장한다.
        /// </summary>
        public void CancelAbsorbAsConsumed()
        {
            if (Mode == PuddleAreaMode.Consumed)
                return;

            onAbsorbArrived = null;
            StopAbsorbMotion();
            SetMode(PuddleAreaMode.Consumed);
            Consumed?.Invoke(this);
        }

        /// <summary>
        /// 책임 :
        /// - 흡수 패턴이 그로기 등으로 중단될 때 장판 탄막을 소비하지 않고 다시 장판 후보 상태로 복구한다.
        /// - 도착 콜백을 끊어 취소 후 보스 흡수 결과가 뒤늦게 적용되지 않도록 보장한다.
        /// </summary>
        public void CancelAbsorbToGround()
        {
            if (Mode == PuddleAreaMode.Consumed)
                return;

            onAbsorbArrived = null;
            StopAbsorbMotion();

            PuddleAreaMode restoreMode = modeBeforeAbsorb == PuddleAreaMode.Igniting
                ? PuddleAreaMode.Igniting
                : PuddleAreaMode.Ground;
            SetMode(restoreMode);
        }

        protected void SetIgnitingMode()
        {
            if (Mode == PuddleAreaMode.Ground)
                SetMode(PuddleAreaMode.Igniting);
        }

        protected void SetGroundMode()
        {
            SetMode(PuddleAreaMode.Ground);
        }

        protected virtual void HandleModeChanged(PuddleAreaMode previousMode, PuddleAreaMode newMode)
        {
        }

        protected GameObject ResolvePuddleTarget(Collider2D other)
        {
            if (other == null)
                return null;

            // 장판 피해는 실제 피격 허트박스만 대상으로 삼는다.
            // 플레이어 자식으로 붙은 공격 이펙트/히트박스 콜라이더가 부모 플레이어로 승격되는 것을 막는다.
            return CombatTargetResolver2D.ResolveDamageTarget(other);
        }

        protected static bool CanApplyGroundEffectTo(GameObject target)
        {
            return CombatHeightFilter2D.CanAffectGroundTarget(target);
        }

        protected bool TryRegisterProjectileHit(GameObject target)
        {
            if (!CanApplyProjectileEffect || target == null)
                return false;

            return projectileHitTargets.Add(target);
        }

        protected static AbilitySystem ResolveAbilitySystem(GameObject target)
        {
            if (target == null)
                return null;

            AbilitySystem system = target.GetComponent<AbilitySystem>();
            if (system != null)
                return system;

            system = target.GetComponentInParent<AbilitySystem>();
            if (system != null)
                return system;

            return target.GetComponentInChildren<AbilitySystem>(true);
        }

        protected static bool IsPlayerTarget(GameObject target)
        {
            if (target == null)
                return false;

            return target.GetComponent<PlayerInteractor2D>() != null ||
                   target.GetComponentInParent<PlayerInteractor2D>() != null ||
                   target.GetComponentInChildren<PlayerInteractor2D>(true) != null;
        }

        private IEnumerator AbsorbGatherThenMoveRoutine(Transform absorbAnchor, float moveSpeed)
        {
            if (absorbGatherDelaySeconds > 0f)
            {
                float elapsed = 0f;
                while (elapsed < absorbGatherDelaySeconds && absorbAnchor != null)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            if (absorbAnchor == null)
            {
                CancelAbsorbAsConsumed("흡수 준비 중 anchor가 사라져 장판을 소비 처리합니다.");
                yield break;
            }

            SetMode(PuddleAreaMode.AbsorbProjectile);
            LogDebug($"흡수 탄막 이동 시작: anchor={(absorbAnchor != null ? absorbAnchor.name : "null")}, moveSpeed={moveSpeed}");
            yield return AbsorbMoveRoutine(absorbAnchor, moveSpeed);
        }

        private IEnumerator AbsorbMoveRoutine(Transform absorbAnchor, float moveSpeed)
        {
            while (absorbAnchor != null)
            {
                Vector3 target = absorbAnchor.position;
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    target,
                    Mathf.Max(0.01f, moveSpeed) * Time.deltaTime);

                float distance = Vector2.Distance(transform.position, target);
                if (distance <= arriveDistance)
                    break;

                yield return null;
            }

            absorbRoutine = null;

            if (absorbAnchor == null)
            {
                CancelAbsorbAsConsumed("흡수 탄막 이동 중 anchor가 사라져 장판을 소비 처리합니다.");
                yield break;
            }

            if (Mode == PuddleAreaMode.AbsorbProjectile)
                onAbsorbArrived?.Invoke(this);
        }

        private void CancelAbsorbAsConsumed(string reason)
        {
            absorbRoutine = null;
            onAbsorbArrived = null;
            LogDebug(reason);

            if (Mode != PuddleAreaMode.Consumed)
                MarkConsumed();
        }

        private void StopAbsorbMotion()
        {
            if (absorbRoutine == null)
                return;

            StopCoroutine(absorbRoutine);
            absorbRoutine = null;
        }

        private void SetMode(PuddleAreaMode newMode)
        {
            if (Mode == newMode)
                return;

            PuddleAreaMode previousMode = Mode;
            Mode = newMode;
            projectileHitTargets.Clear();
            ApplyColliderMode(newMode);
            shaderVisual?.SetMode(newMode);
            particleVisual?.ApplyMode(newMode);
            // Blob visual + outline은 Noita풍 particle field 전환 테스트 동안 비활성화한다.
            // ApplyBlobVisualMode(newMode);
            HandleModeChanged(previousMode, newMode);
            LogDebug($"모드 변경: {previousMode} -> {newMode}");
        }

        private void ApplyColliderMode(PuddleAreaMode mode)
        {
            SyncColliderRadii();

            if (groundCollider != null)
                groundCollider.enabled = mode == PuddleAreaMode.Ground || mode == PuddleAreaMode.Igniting;

            if (projectileCollider != null)
                projectileCollider.enabled = mode == PuddleAreaMode.AbsorbProjectile;
        }

        private void CacheColliders()
        {
            if (groundCollider == null)
                groundCollider = GetComponent<CircleCollider2D>();
        }

        private void CachePresentation()
        {
            if (shaderVisual == null)
                shaderVisual = GetComponentInChildren<PuddleShaderVisual>(true);

            if (particleVisual == null)
                particleVisual = GetComponentInChildren<PuddleParticleVisual>(true);

            if (blobVisual == null)
                blobVisual = GetComponentInChildren<PuddleBlobVisual>(true);
        }

        protected void RefreshPresentationReferences()
        {
            CachePresentation();
            SyncPresentationContext();
        }

        private void ApplyBlobVisualMode(PuddleAreaMode mode)
        {
            if (blobVisual == null)
                return;

            if (mode == PuddleAreaMode.AbsorbProjectile)
            {
                blobVisual.SetAbsorbTarget(1f);
                blobVisual.SetProjectileMotion(true);
                return;
            }

            blobVisual.SetAbsorbTarget(0f);
            blobVisual.SetProjectileMotion(false);
        }

        private void SyncColliderRadii()
        {
            if (groundCollider != null)
            {
                groundCollider.isTrigger = true;
                groundCollider.radius = groundRadius;
            }

            if (projectileCollider != null)
            {
                projectileCollider.isTrigger = true;
                projectileCollider.radius = projectileRadius;
            }
        }

        private void SyncPresentationContext()
        {
            if (shaderVisual != null)
            {
                shaderVisual.SetElementType(ElementType);
                shaderVisual.SetRadii(groundRadius, projectileRadius);
                shaderVisual.SetMode(Mode);
            }

            if (particleVisual != null)
            {
                particleVisual.SetElementType(ElementType);
                particleVisual.SetSurfaceRadius(groundRadius);
            }
        }

        private void LogDebug(string message)
        {
            if (!logDebugMessages)
                return;

            Debug.Log($"[{nameof(PuddleAreaBase)}] {message}", this);
        }
    }
}
