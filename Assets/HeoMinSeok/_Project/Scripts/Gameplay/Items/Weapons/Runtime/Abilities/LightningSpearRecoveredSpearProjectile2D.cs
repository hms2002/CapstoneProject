using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class LightningSpearRecoveredSpearProjectile2D : AttackBase
{
    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private GameObject despawnEffectVisual;
    [SerializeField] private Animator animator;
    [SerializeField] private HitboxVisualAnimatorPlayer visualPlayer;
    [SerializeField] private AnimationClip despawnEffectClip;
    [SerializeField] private string spawnTrigger = "Spawn";
    [SerializeField] private string stuckTrigger = "Stuck";
    [SerializeField] private string despawnTrigger = "DeSpawn";
    [SerializeField] private bool alignToDirection = true;

    [Header("Fallback Timing")]
    [SerializeField, Min(0f)] private float spawnFallbackSeconds;
    [SerializeField, Min(0f)] private float stuckLifetimeSeconds = 0.35f;
    [SerializeField, Min(0f)] private float despawnFallbackSeconds = 0.12f;

    private readonly HashSet<int> hitTargets = new HashSet<int>();

    private Collider2D ownCollider;
    private Vector2 direction = Vector2.right;
    private float speed;
    private float moveLifetimeSeconds = 0.75f;
    private bool moving;
    private bool spawnCompleted;
    private bool despawnCompleted;
    private bool despawning;
    private Coroutine spawnRoutine;
    private Coroutine moveLifetimeRoutine;
    private Coroutine stuckRoutine;
    private Coroutine despawnRoutine;

    public event Action<LightningSpearRecoveredSpearProjectile2D> Destroyed;

    public void Setup(
        ProjectileAttackSpawnContext context,
        Vector2 spawnPosition,
        float activeMoveLifetime,
        float spawnFallback,
        float stuckLifetime,
        float despawnFallback)
    {
        if (context == null)
        {
            Debug.LogError($"[{nameof(LightningSpearRecoveredSpearProjectile2D)}] context is null.", this);
            enabled = false;
            return;
        }

        ResolveReferences();

        transform.position = new Vector3(spawnPosition.x, spawnPosition.y, transform.position.z);
        direction = context.direction.sqrMagnitude > 0.0001f
            ? context.direction.normalized
            : Vector2.right;
        speed = Mathf.Max(0f, context.speed);
        moveLifetimeSeconds = Mathf.Max(0.01f, activeMoveLifetime);
        spawnFallbackSeconds = Mathf.Max(0f, spawnFallback);
        stuckLifetimeSeconds = Mathf.Max(0f, stuckLifetime);
        despawnFallbackSeconds = Mathf.Max(0f, despawnFallback);
        moving = false;
        spawnCompleted = false;
        despawnCompleted = false;
        despawning = false;
        hitTargets.Clear();

        if (alignToDirection)
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        if (ownCollider != null)
            ownCollider.enabled = spawnFallbackSeconds <= 0f;

        SetupBase(context);
        PlayTrigger(spawnTrigger);

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(CoStartAfterSpawn());
    }

    public void NotifySpawnAnimationComplete()
    {
        spawnCompleted = true;
    }

    public void NotifyDespawnAnimationComplete()
    {
        despawnCompleted = true;
    }

    protected override void TickAttack(float deltaTime)
    {
        if (!moving || despawning)
            return;

        transform.position += (Vector3)(direction * speed * deltaTime);
    }

    protected override bool CanHitTarget(GameObject target)
    {
        if (target == null)
            return false;

        int targetId = target.GetInstanceID();
        if (hitTargets.Contains(targetId))
            return false;

        hitTargets.Add(targetId);
        return true;
    }

    protected override void OnHitTarget(GameObject target, Collider2D hitCollider)
    {
    }

    protected override void OnHitWall(GameObject wall, Collider2D hitCollider)
    {
        if (despawning)
            return;

        moving = false;

        if (ownCollider != null)
            ownCollider.enabled = false;

        PlayTrigger(stuckTrigger);

        if (moveLifetimeRoutine != null)
        {
            StopCoroutine(moveLifetimeRoutine);
            moveLifetimeRoutine = null;
        }

        if (stuckRoutine != null)
            StopCoroutine(stuckRoutine);

        stuckRoutine = StartCoroutine(CoDespawnAfterStuck());
    }

    private void ResolveReferences()
    {
        if (visualRoot == null)
            visualRoot = transform;

        if (ownCollider == null)
            ownCollider = GetComponent<Collider2D>();

        if (visualPlayer == null && despawnEffectVisual != null)
            visualPlayer = despawnEffectVisual.GetComponent<HitboxVisualAnimatorPlayer>();
    }

    private IEnumerator CoStartAfterSpawn()
    {
        float elapsed = 0f;
        while (!spawnCompleted && elapsed < spawnFallbackSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        spawnCompleted = true;

        if (ownCollider != null)
            ownCollider.enabled = true;

        moving = true;
        spawnRoutine = null;

        if (moveLifetimeRoutine != null)
            StopCoroutine(moveLifetimeRoutine);

        moveLifetimeRoutine = StartCoroutine(CoMoveLifetime());
    }

    private IEnumerator CoMoveLifetime()
    {
        yield return new WaitForSeconds(moveLifetimeSeconds);
        moveLifetimeRoutine = null;
        BeginDespawn();
    }

    private IEnumerator CoDespawnAfterStuck()
    {
        if (stuckLifetimeSeconds > 0f)
            yield return new WaitForSeconds(stuckLifetimeSeconds);

        stuckRoutine = null;
        BeginDespawn();
    }

    private void BeginDespawn()
    {
        if (despawning)
            return;

        despawning = true;
        moving = false;

        if (ownCollider != null)
            ownCollider.enabled = false;

        PlayDespawnPresentation();

        if (despawnRoutine != null)
            StopCoroutine(despawnRoutine);

        despawnRoutine = StartCoroutine(CoDestroyAfterDespawn());
        enabled = false;
    }

    private void PlayDespawnPresentation()
    {
        if (despawnEffectClip != null && visualPlayer != null)
        {
            if (visualRoot != null && visualRoot.gameObject != despawnEffectVisual)
                visualRoot.gameObject.SetActive(false);

            if (despawnEffectVisual != null)
                despawnEffectVisual.SetActive(true);

            visualPlayer.PlayClip(despawnEffectClip);
            if (visualPlayer.CurrentClipDuration > 0f)
                despawnFallbackSeconds = Mathf.Max(despawnFallbackSeconds, visualPlayer.CurrentClipDuration);
            return;
        }

        PlayTrigger(despawnTrigger);
    }

    private IEnumerator CoDestroyAfterDespawn()
    {
        float elapsed = 0f;
        while (!despawnCompleted && elapsed < despawnFallbackSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        despawnRoutine = null;
        Destroy(gameObject);
    }

    private void PlayTrigger(string trigger)
    {
        if (animator == null || string.IsNullOrWhiteSpace(trigger))
            return;

        animator.ResetTrigger(trigger);
        animator.SetTrigger(trigger);
    }

    private void OnDestroy()
    {
        Destroyed?.Invoke(this);
        Destroyed = null;
    }
}
