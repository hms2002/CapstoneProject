using System.Collections;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LightningSpearRecoveredSpearActor : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private GameObject bodyVisual;
    [SerializeField] private GameObject effectVisual;
    [SerializeField] private HitboxVisualAnimatorPlayer effectPlayer;
    [SerializeField] private AnimationClip spawnEffectClip;
    [SerializeField] private AnimationClip despawnEffectClip;

    [Header("Legacy Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string spawnTrigger = "Spawn";
    [SerializeField] private string idleTrigger = "Idle";
    [SerializeField] private string despawnTrigger = "DeSpawn";

    [Header("Fallback Timing")]
    [SerializeField, Min(0f)] private float spawnFallbackSeconds = 0.12f;
    [SerializeField, Min(0f)] private float bodyHideFallbackSeconds = 0.12f;
    [SerializeField, Min(0f)] private float despawnFallbackSeconds = 0.34f;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float moveTweenSeconds = 0.12f;
    [SerializeField, Min(0f)] private float followSmoothTime = 0.08f;
    [SerializeField, Min(0f)] private float warpSnapDistance = 3f;
    [SerializeField, Min(0f)] private float floatAmplitude = 0.12f;
    [SerializeField, Min(0.01f)] private float floatDuration = 0.8f;
    [SerializeField] private Ease moveEase = Ease.OutQuad;
    [SerializeField] private Ease floatEase = Ease.InOutSine;

    private Transform owner;
    private Vector2 localOffset;
    private Vector2 followVelocity;
    private Vector3 visualInitialLocalPosition;
    private float layoutRotationDegrees;
    private float visualForwardOffset;
    private float floatOffsetY;
    private bool visualInitialLocalPositionCaptured;
    private bool hasAppliedWorldPosition;
    private bool spawnCompleted;
    private bool despawnCompleted;
    private bool despawning;
    private Tweener offsetTween;
    private Tweener rotationTween;
    private Tweener floatTween;
    private Coroutine spawnRoutine;
    private Coroutine effectHideRoutine;
    private Coroutine bodyHideRoutine;
    private Coroutine despawnRoutine;

    public event System.Action<LightningSpearRecoveredSpearActor> Destroyed;

    public Vector2 CurrentPosition => transform.position;
    public Vector2 ProjectileSpawnPosition => visualRoot != null ? visualRoot.position : transform.position;

    public void Initialize(
        Transform ownerTransform,
        Vector2 offset,
        float rotationDegrees,
        float forwardOffset,
        float spawnFallback,
        float despawnFallback,
        float moveSeconds,
        float amplitude,
        float duration,
        float smoothTime = 0.08f,
        float snapDistance = 3f)
    {
        owner = ownerTransform;
        localOffset = offset;
        layoutRotationDegrees = rotationDegrees;
        visualForwardOffset = Mathf.Max(0f, forwardOffset);
        spawnFallbackSeconds = Mathf.Max(0f, spawnFallback);
        despawnFallbackSeconds = Mathf.Max(0f, despawnFallback);
        moveTweenSeconds = Mathf.Max(0f, moveSeconds);
        followSmoothTime = Mathf.Max(0f, smoothTime);
        warpSnapDistance = Mathf.Max(0f, snapDistance);
        floatAmplitude = Mathf.Max(0f, amplitude);
        floatDuration = Mathf.Max(0.01f, duration);
        followVelocity = Vector2.zero;
        hasAppliedWorldPosition = false;
        despawning = false;
        spawnCompleted = false;
        despawnCompleted = false;

        ResolveReferences();
        SetBodyVisible(false);
        SetEffectVisible(false);
        ApplyVisualOffset();
        ApplyWorldPosition(true);
        PlaySpawnPresentation();
        RestartFloating();

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(CoShowBodyFallback());
    }

    public void SetFollowSettings(float smoothTime, float snapDistance)
    {
        followSmoothTime = Mathf.Max(0f, smoothTime);
        warpSnapDistance = Mathf.Max(0f, snapDistance);
    }

    public void SetLayout(Vector2 offset, float rotationDegrees, float forwardOffset, float moveSeconds)
    {
        if (despawning)
            return;

        moveTweenSeconds = Mathf.Max(0f, moveSeconds);
        visualForwardOffset = Mathf.Max(0f, forwardOffset);
        ApplyVisualOffset();

        if (offsetTween != null && offsetTween.IsActive())
            offsetTween.Kill();

        if (rotationTween != null && rotationTween.IsActive())
            rotationTween.Kill();

        if (moveTweenSeconds <= 0f)
        {
            localOffset = offset;
            layoutRotationDegrees = rotationDegrees;
            ApplyWorldPosition(false);
            return;
        }

        Vector2 startOffset = localOffset;
        offsetTween = DOTween.To(
                () => startOffset,
                value =>
                {
                    startOffset = value;
                    localOffset = value;
                    ApplyWorldPosition(false);
                },
                offset,
                moveTweenSeconds)
            .SetEase(moveEase)
            .SetLink(gameObject);

        float startRotation = layoutRotationDegrees;
        rotationTween = DOTween.To(
                () => startRotation,
                value =>
                {
                    startRotation = value;
                    layoutRotationDegrees = value;
                    ApplyWorldPosition(false);
                },
                rotationDegrees,
                moveTweenSeconds)
            .SetEase(moveEase)
            .SetLink(gameObject);
    }

    public void PlayDespawnAndDestroy(float fallbackSeconds)
    {
        if (despawning)
            return;

        despawning = true;
        owner = null;
        despawnCompleted = false;
        despawnFallbackSeconds = Mathf.Max(0f, fallbackSeconds);

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        KillTween(ref offsetTween);
        KillTween(ref rotationTween);
        KillTween(ref floatTween);
        PlayDespawnPresentation();

        if (bodyHideRoutine != null)
            StopCoroutine(bodyHideRoutine);

        bodyHideRoutine = StartCoroutine(CoHideBodyFallback());

        if (despawnRoutine != null)
            StopCoroutine(despawnRoutine);

        despawnRoutine = StartCoroutine(CoDestroyAfterDespawn());
    }

    public void ShowBodyVisual()
    {
        if (spawnCompleted)
            return;

        spawnCompleted = true;
        SetBodyVisible(true);
        PlayTrigger(idleTrigger);
    }

    public void HideBodyVisual()
    {
        SetBodyVisible(false);
    }

    public void CompleteDespawnAnimation()
    {
        despawnCompleted = true;
    }

    public void NotifySpawnAnimationComplete()
    {
        ShowBodyVisual();
    }

    public void NotifyDespawnAnimationComplete()
    {
        HideBodyVisual();
        CompleteDespawnAnimation();
    }

    private void Update()
    {
        if (!despawning)
            ApplyWorldPosition(false);
    }

    private void ResolveReferences()
    {
        if (visualRoot == null)
            visualRoot = transform;

        if (bodyVisual == null && visualRoot != null)
            bodyVisual = visualRoot.gameObject;

        if (effectPlayer == null && effectVisual != null)
            effectPlayer = effectVisual.GetComponent<HitboxVisualAnimatorPlayer>();

        if (!visualInitialLocalPositionCaptured)
        {
            visualInitialLocalPosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
            visualInitialLocalPositionCaptured = true;
        }
    }

    private void ApplyWorldPosition(bool forceSnap)
    {
        if (owner == null)
            return;

        Vector2 target = (Vector2)owner.position + localOffset + Vector2.up * floatOffsetY;
        Vector2 current = transform.position;
        float snapSqr = warpSnapDistance * warpSnapDistance;
        bool shouldSnap =
            forceSnap ||
            !hasAppliedWorldPosition ||
            followSmoothTime <= 0f ||
            (warpSnapDistance > 0f && (target - current).sqrMagnitude >= snapSqr);

        Vector2 next = shouldSnap
            ? target
            : Vector2.SmoothDamp(current, target, ref followVelocity, followSmoothTime);

        if (shouldSnap)
            followVelocity = Vector2.zero;

        transform.position = new Vector3(next.x, next.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0f, 0f, layoutRotationDegrees);
        hasAppliedWorldPosition = true;
    }

    private void ApplyVisualOffset()
    {
        if (visualRoot == null || visualRoot == transform)
            return;

        visualRoot.localPosition = visualInitialLocalPosition + Vector3.up * visualForwardOffset;
    }

    private void RestartFloating()
    {
        KillTween(ref floatTween);
        floatOffsetY = 0f;

        if (floatAmplitude <= 0f)
            return;

        floatTween = DOTween.To(
                () => floatOffsetY,
                value =>
                {
                    floatOffsetY = value;
                    ApplyWorldPosition(false);
                },
                floatAmplitude,
                floatDuration * 0.5f)
            .SetEase(floatEase)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject);
    }

    private void PlaySpawnPresentation()
    {
        PlayEffectClip(spawnEffectClip);
        PlayTrigger(spawnTrigger);
    }

    private void PlayDespawnPresentation()
    {
        PlayEffectClip(despawnEffectClip);
        PlayTrigger(despawnTrigger);
    }

    private void PlayEffectClip(AnimationClip clip)
    {
        if (clip == null || effectPlayer == null)
            return;

        SetEffectVisible(true);
        effectPlayer.PlayClip(clip);

        if (effectHideRoutine != null)
            StopCoroutine(effectHideRoutine);

        float duration = effectPlayer.CurrentClipDuration > 0f
            ? effectPlayer.CurrentClipDuration
            : clip.length;
        effectHideRoutine = StartCoroutine(CoHideEffectAfterClip(duration));
    }

    private IEnumerator CoShowBodyFallback()
    {
        if (spawnFallbackSeconds > 0f)
            yield return new WaitForSeconds(spawnFallbackSeconds);

        ShowBodyVisual();
        spawnRoutine = null;
    }

    private IEnumerator CoHideBodyFallback()
    {
        if (bodyHideFallbackSeconds > 0f)
            yield return new WaitForSeconds(bodyHideFallbackSeconds);

        HideBodyVisual();
        bodyHideRoutine = null;
    }

    private IEnumerator CoHideEffectAfterClip(float duration)
    {
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        SetEffectVisible(false);
        effectHideRoutine = null;
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

    private void SetBodyVisible(bool visible)
    {
        if (bodyVisual != null && bodyVisual.activeSelf != visible)
            bodyVisual.SetActive(visible);
    }

    private void SetEffectVisible(bool visible)
    {
        if (effectVisual != null && effectVisual.activeSelf != visible)
            effectVisual.SetActive(visible);
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
        KillTween(ref offsetTween);
        KillTween(ref rotationTween);
        KillTween(ref floatTween);
        Destroyed?.Invoke(this);
        Destroyed = null;
    }

    private static void KillTween(ref Tweener tween)
    {
        if (tween != null && tween.IsActive())
            tween.Kill();

        tween = null;
    }
}
