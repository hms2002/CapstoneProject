using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 근접 히트박스 공격체가 추가로 필요로 하는 생성 문맥을 보관한다.
    /// - 월드 위치, 히트박스 크기, 타겟당 1회 타격 여부, 방향 정보를 전달한다.
    /// </summary>
    public sealed class MeleeHitboxSpawnContext : AttackSpawnContext
    {
        public Vector2 worldPosition;
        public Vector2 hitboxSize = Vector2.one;
        public bool hitOncePerTarget = true;
        public bool destroyOnFirstHit = false;
        public Vector2 direction = Vector2.right;
    }

    /// <summary>
    /// 책임 :
    /// - 짧게 생성되는 근접 공격 판정 엔티티를 담당한다.
    /// - BoxCollider2D를 히트박스로 사용하고, 생성 직후 즉시 스캔 + 이후 Trigger 진입을 처리한다.
    /// - 타겟당 1회 타격 정책을 유지하면서 짧은 수명 동안 실제 공격 범위를 월드에 유지한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class MeleeHitboxActor : AttackBase
    {
        [Header("Optional Visual")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private bool flipVisualXByDirection = true;

        private BoxCollider2D hitboxCollider;
        private readonly HashSet<int> hitTargetIds = new();

        private bool hitOncePerTarget = true;
        private bool destroyOnFirstHit = false;

        /// <summary>
        /// 책임 :
        /// - 근접 히트박스의 위치, 크기, 타격 정책을 초기화한다.
        /// - 공통 공격체 초기화를 완료한 뒤 생성 직후 겹쳐 있는 적을 즉시 스캔한다.
        /// </summary>
        public void Setup(MeleeHitboxSpawnContext context)
        {
            if (context == null)
            {
                Debug.LogError($"[{nameof(MeleeHitboxActor)}] context is null.", this);
                enabled = false;
                return;
            }

            if (hitboxCollider == null)
                hitboxCollider = GetComponent<BoxCollider2D>();

            transform.position = context.worldPosition;
            hitboxCollider.size = context.hitboxSize;
            hitboxCollider.isTrigger = true;

            hitOncePerTarget = context.hitOncePerTarget;
            destroyOnFirstHit = context.destroyOnFirstHit;

            ApplyVisualDirection(context.direction);
            SetupBase(context);

            PerformImmediateScan();
        }

        /// <summary>
        /// 책임 :
        /// - 타겟당 1회 타격 정책을 처리한다.
        /// - 정책이 켜져 있으면 이미 맞은 대상은 다시 맞지 않게 막는다.
        /// </summary>
        protected override bool CanHitTarget(GameObject target)
        {
            if (!hitOncePerTarget || target == null)
                return true;

            return hitTargetIds.Add(target.GetInstanceID());
        }

        /// <summary>
        /// 책임 :
        /// - 대상 적중 후 제거 정책을 처리한다.
        /// - 근접 1타는 기본적으로 다수 대상을 동시에 맞출 수 있어야 하므로 기본값은 제거하지 않는다.
        /// </summary>
        protected override void OnHitTarget(GameObject target, Collider2D hitCollider)
        {
            if (destroyOnFirstHit)
                DestroySelf();
        }

        /// <summary>
        /// 책임 :
        /// - 시각 루트가 있을 경우 좌우 방향에 맞춰 비주얼을 반전한다.
        /// - 근접 비주얼과 실제 공격 범위가 같은 엔티티에서 움직이도록 맞춘다.
        /// </summary>
        private void ApplyVisualDirection(Vector2 direction)
        {
            if (!flipVisualXByDirection || visualRoot == null)
                return;

            if (Mathf.Abs(direction.x) < 0.0001f)
                return;

            var scale = visualRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * (direction.x >= 0f ? 1f : -1f);
            visualRoot.localScale = scale;
        }

        /// <summary>
        /// 책임 :
        /// - 생성 직후 이미 겹쳐 있는 대상을 즉시 판정한다.
        /// - 기존 OverlapBox 1회 방식이 갖던 "즉시 적중" 특성을 AttackBase 구조에서도 유지한다.
        /// </summary>
        private void PerformImmediateScan()
        {
            if (hitboxCollider == null)
                return;

            Vector2 center = hitboxCollider.bounds.center;
            Vector2 size = hitboxCollider.bounds.size;

            Collider2D[] results = Physics2D.OverlapBoxAll(center, size, 0f, DamageLayers);
            if (results == null || results.Length == 0)
                return;

            for (int i = 0; i < results.Length; i++)
            {
                var other = results[i];
                if (other == null)
                    continue;

                var targetRoot = ResolveHitRoot(other);
                if (targetRoot == null || targetRoot == IgnoreTarget)
                    continue;

                if (!CanHitTarget(targetRoot))
                    continue;

                if (!TryApplyHit(targetRoot))
                    continue;

                OnHitTarget(targetRoot, other);

                if (destroyOnFirstHit)
                    return;
            }
        }
    }
}