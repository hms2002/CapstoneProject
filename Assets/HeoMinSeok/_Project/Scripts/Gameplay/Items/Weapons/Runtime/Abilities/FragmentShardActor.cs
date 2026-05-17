using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 파편검 조각 하나의 world actor와 trail 연출을 담당한다.
/// - gameplay 상태는 FragmentBladeRuntimeData가 소유하고, 이 actor는 위치 이동/표시/정리만 수행한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class FragmentShardActor : MonoBehaviour
{
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField, Min(1f)] private float pierceTurnDegreesPerSecond = 540f;
    [SerializeField, Min(0.01f)] private float pierceTargetPassRadius = 0.2f;

    private Coroutine motionRoutine;
    private Collider2D hitCollider;
    private readonly HashSet<int> hitTargetIds = new();
    private CombatHitPayload hitPayload;
    private LayerMask damageLayers;
    private GameObject ignoreTarget;
    private bool damageActive;

    public int ShardId { get; private set; } = -1;

    private void Awake()
    {
        if (trailRenderer == null)
            trailRenderer = GetComponentInChildren<TrailRenderer>(true);

        hitCollider = GetComponent<Collider2D>();
        if (hitCollider != null)
        {
            hitCollider.isTrigger = true;
            hitCollider.enabled = false;
        }
    }

    /// <summary>
    /// 책임 :
    /// - 풀에서 꺼낸 actor를 특정 shard id에 연결한다.
    /// - 이전 trail/이동 상태가 다음 shard에 섞이지 않도록 초기화한다.
    /// </summary>
    public void BindShard(int shardId)
    {
        ShardId = shardId;
        StopMotion();
        ClearTrail();
    }

    /// <summary>
    /// 책임 :
    /// - 조각을 월드에 떨어진 상태로 표시한다.
    /// - 실제 detached 판정은 runtime data가 이미 수행했으므로 actor는 visual 위치만 맞춘다.
    /// </summary>
    public void ShowDetached(Vector3 worldPosition)
    {
        StopMotion();
        transform.SetParent(null, worldPositionStays: true);
        transform.position = worldPosition;
        gameObject.SetActive(true);
        ClearTrail();
    }

    /// <summary>
    /// 책임 :
    /// - 이전 이동 명령을 중단하고 새 회수 이동을 시작한다.
    /// - 완료 시점 callback을 통해 runtime data가 bound 확정을 수행하게 한다.
    /// </summary>
    public void BeginRecall(
        Transform destination,
        float durationSeconds,
        CombatHitPayload recallPayload,
        LayerMask recallDamageLayers,
        GameObject recallIgnoreTarget,
        Action<int, Vector3> onCompleted)
    {
        StopMotion();
        EnableDamage(recallPayload, recallDamageLayers, recallIgnoreTarget);
        gameObject.SetActive(true);
        motionRoutine = StartCoroutine(RecallRoutine(destination, durationSeconds, onCompleted));
    }

    /// <summary>
    /// 책임 :
    /// - Skill2 강화 중 기본 공격 적중에 반응해 조각을 대상 방향으로 관통 이동시킨다.
    /// - 조각 이동 명령은 항상 최신 명령이 이전 회수/관통 명령을 덮어쓰게 한다.
    /// </summary>
    public void BeginPierce(
        Vector3 targetPosition,
        Vector3 initialDirection,
        float overshootDistance,
        float durationSeconds,
        CombatHitPayload piercePayload,
        LayerMask pierceDamageLayers,
        GameObject pierceIgnoreTarget,
        Action<int, Vector3> onCompleted)
    {
        StopMotion();
        EnableDamage(piercePayload, pierceDamageLayers, pierceIgnoreTarget);
        gameObject.SetActive(true);
        motionRoutine = StartCoroutine(PierceRoutine(
            targetPosition,
            initialDirection,
            overshootDistance,
            durationSeconds,
            onCompleted));
    }

    /// <summary>
    /// 책임 :
    /// - 강제 cleanup 시 이동과 trail을 즉시 끊고 풀 안의 비활성 actor로 되돌린다.
    /// </summary>
    public void CancelAndHide(Transform poolRoot)
    {
        StopMotion();
        DisableDamage();
        ClearTrail();
        transform.SetParent(poolRoot, worldPositionStays: false);
        gameObject.SetActive(false);
        ShardId = -1;
    }

    public void ClearTrail()
    {
        if (trailRenderer != null)
            trailRenderer.Clear();
    }

    private IEnumerator RecallRoutine(
        Transform destination,
        float durationSeconds,
        Action<int, Vector3> onCompleted)
    {
        Vector3 start = transform.position;
        float duration = Mathf.Max(0.01f, durationSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 target = destination != null ? destination.position : start;
            transform.position = Vector3.Lerp(start, target, SmoothStep(t));
            yield return null;
        }

        Vector3 completedPosition = destination != null ? destination.position : transform.position;
        transform.position = completedPosition;
        motionRoutine = null;
        onCompleted?.Invoke(ShardId, completedPosition);
        DisableDamage();
    }

    private IEnumerator PierceRoutine(
        Vector3 targetPosition,
        Vector3 initialDirection,
        float overshootDistance,
        float durationSeconds,
        Action<int, Vector3> onCompleted)
    {
        Vector2 target = targetPosition;
        Vector2 direction = ResolveInitialPierceDirection(initialDirection, target - (Vector2)transform.position);
        float distanceBudget = Vector2.Distance(transform.position, targetPosition) + Mathf.Max(0f, overshootDistance);
        float duration = Mathf.Max(0.01f, durationSeconds);
        float speed = Mathf.Max(0.01f, distanceBudget / duration);
        float remainingOvershoot = Mathf.Max(0f, overshootDistance);
        float maxElapsed = duration * 3f + 0.5f;
        float elapsed = 0f;
        bool passedTarget = false;

        while (elapsed < maxElapsed)
        {
            elapsed += Time.deltaTime;

            Vector2 previousPosition = transform.position;
            if (!passedTarget)
            {
                Vector2 toTarget = target - previousPosition;
                if (toTarget.sqrMagnitude > 0.0001f)
                    direction = RotateTowards(direction, toTarget.normalized, pierceTurnDegreesPerSecond * Time.deltaTime);
            }

            Vector2 nextPosition = previousPosition + direction * (speed * Time.deltaTime);
            transform.position = new Vector3(nextPosition.x, nextPosition.y, transform.position.z);

            if (!passedTarget && HasPassedPierceTarget(previousPosition, nextPosition, target))
                passedTarget = true;

            if (passedTarget)
            {
                remainingOvershoot -= Vector2.Distance(previousPosition, nextPosition);
                if (remainingOvershoot <= 0f)
                    break;
            }

            yield return null;
        }

        Vector3 completedPosition = transform.position;
        motionRoutine = null;
        onCompleted?.Invoke(ShardId, completedPosition);
        DisableDamage();
    }

    private void StopMotion()
    {
        if (motionRoutine == null)
            return;

        StopCoroutine(motionRoutine);
        motionRoutine = null;
    }

    private static float SmoothStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static Vector2 ResolveInitialPierceDirection(Vector3 initialDirection, Vector2 fallbackDirection)
    {
        Vector2 direction = initialDirection;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = fallbackDirection;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector2.right;

        return direction.normalized;
    }

    private static Vector2 RotateTowards(Vector2 current, Vector2 target, float maxDegreesDelta)
    {
        float currentAngle = Mathf.Atan2(current.y, current.x) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg;
        float nextAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, Mathf.Max(0f, maxDegreesDelta));
        float radians = nextAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private bool HasPassedPierceTarget(Vector2 previousPosition, Vector2 nextPosition, Vector2 target)
    {
        if (Vector2.Distance(nextPosition, target) <= pierceTargetPassRadius)
            return true;

        Vector2 previousToTarget = target - previousPosition;
        Vector2 nextToTarget = target - nextPosition;
        return Vector2.Dot(previousToTarget, nextToTarget) <= 0f;
    }

    /// <summary>
    /// 책임 :
    /// - 회수/관통 중 조각이 사용하는 피해 payload와 대상 레이어를 활성화한다.
    /// - 타겟당 1회 hit 정책을 조각 actor 단위로 보장한다.
    /// </summary>
    private void EnableDamage(
        CombatHitPayload payload,
        LayerMask layers,
        GameObject targetToIgnore)
    {
        hitPayload = ClonePayloadForThisActor(payload);
        damageLayers = layers;
        ignoreTarget = targetToIgnore;
        hitTargetIds.Clear();
        damageActive = hitPayload != null;

        if (hitCollider != null)
            hitCollider.enabled = damageActive;
    }

    private void DisableDamage()
    {
        damageActive = false;
        hitPayload = null;
        ignoreTarget = null;
        hitTargetIds.Clear();

        if (hitCollider != null)
            hitCollider.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!damageActive || other == null || hitPayload == null)
            return;

        GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(other);
        if (targetRoot == null || IsIgnoredTarget(targetRoot))
            return;

        int layerBit = 1 << targetRoot.layer;
        if ((damageLayers.value & layerBit) == 0)
            return;

        if (!hitTargetIds.Add(targetRoot.GetInstanceID()))
            return;

        CombatHitPayloadApplier.Apply(targetRoot, hitPayload, transform.position);
    }

    private bool IsIgnoredTarget(GameObject targetRoot)
    {
        if (targetRoot == null || ignoreTarget == null)
            return false;

        if (targetRoot == ignoreTarget)
            return true;

        Transform targetTransform = targetRoot.transform;
        Transform ignoreTransform = ignoreTarget.transform;
        return targetTransform.IsChildOf(ignoreTransform) || ignoreTransform.IsChildOf(targetTransform);
    }

    private CombatHitPayload ClonePayloadForThisActor(CombatHitPayload payload)
    {
        if (payload == null)
            return null;

        return new CombatHitPayload
        {
            sourceSystem = payload.sourceSystem,
            sourceSpec = payload.sourceSpec,
            damageEffect = payload.damageEffect,
            knockbackEffect = payload.knockbackEffect,
            finalHpDamage = payload.finalHpDamage,
            finalStaggerBuildUp = payload.finalStaggerBuildUp,
            finalKnockbackImpulse = payload.finalKnockbackImpulse,
            hitConfirmedTag = payload.hitConfirmedTag,
            causer = gameObject,
            isCriticalHit = payload.isCriticalHit
        };
    }
}
