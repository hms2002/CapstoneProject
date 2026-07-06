using System.Collections;
using UnityEngine;

/// <summary>
/// 책임 : 번개 창 회수 창 actor의 소유자 추적, 배치 전환, 부유 연출, 생성/소멸 presentation 이벤트를 관리한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class LightningSpearRecoveredSpearActor : MonoBehaviour
{
    private enum Ease
    {
        Unset = 0,
        Linear = 1,
        InSine = 2,
        OutSine = 3,
        InOutSine = 4,
        InQuad = 5,
        OutQuad = 6,
        InOutQuad = 7,
        InCubic = 8,
        OutCubic = 9,
        InOutCubic = 10,
        InQuart = 11,
        OutQuart = 12,
        InOutQuart = 13,
        InQuint = 14,
        OutQuint = 15,
        InOutQuint = 16,
        InBack = 26,
        OutBack = 27,
        InOutBack = 28
    }

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
    private Coroutine layoutMoveRoutine;
    private Coroutine floatRoutine;
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

        StopLayoutMoveRoutine();

        if (moveTweenSeconds <= 0f)
        {
            localOffset = offset;
            layoutRotationDegrees = rotationDegrees;
            ApplyWorldPosition(false);
            return;
        }

        layoutMoveRoutine = StartCoroutine(PlayLayoutMoveRoutine(
            localOffset,
            offset,
            layoutRotationDegrees,
            rotationDegrees,
            moveTweenSeconds));
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

        StopLayoutMoveRoutine();
        StopFloatRoutine();
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
        StopFloatRoutine();
        floatOffsetY = 0f;

        if (floatAmplitude <= 0f)
            return;

        floatRoutine = StartCoroutine(PlayFloatRoutine());
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
        StopLayoutMoveRoutine();
        StopFloatRoutine();
        Destroyed?.Invoke(this);
        Destroyed = null;
    }

    private void StopLayoutMoveRoutine()
    {
        if (layoutMoveRoutine == null)
            return;

        StopCoroutine(layoutMoveRoutine);
        layoutMoveRoutine = null;
    }

    private void StopFloatRoutine()
    {
        if (floatRoutine == null)
            return;

        StopCoroutine(floatRoutine);
        floatRoutine = null;
    }

    private IEnumerator PlayLayoutMoveRoutine(
        Vector2 startOffset,
        Vector2 targetOffset,
        float startRotation,
        float targetRotation,
        float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && !despawning)
        {
            elapsed += Time.deltaTime;
            float t = duration > 0f ? elapsed / duration : 1f;
            float easedT = EvaluateEase(moveEase, t);

            localOffset = Vector2.LerpUnclamped(startOffset, targetOffset, easedT);
            layoutRotationDegrees = Mathf.LerpUnclamped(startRotation, targetRotation, easedT);
            ApplyWorldPosition(false);
            yield return null;
        }

        if (!despawning)
        {
            localOffset = targetOffset;
            layoutRotationDegrees = targetRotation;
            ApplyWorldPosition(false);
        }

        layoutMoveRoutine = null;
    }

    private IEnumerator PlayFloatRoutine()
    {
        float halfDuration = Mathf.Max(0.01f, floatDuration * 0.5f);

        while (!despawning)
        {
            yield return PlayFloatHalfCycle(0f, floatAmplitude, halfDuration);
            yield return PlayFloatHalfCycle(floatAmplitude, 0f, halfDuration);
        }
    }

    private IEnumerator PlayFloatHalfCycle(float startValue, float targetValue, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && !despawning)
        {
            elapsed += Time.deltaTime;
            float t = duration > 0f ? elapsed / duration : 1f;
            floatOffsetY = Mathf.LerpUnclamped(startValue, targetValue, EvaluateEase(floatEase, t));
            ApplyWorldPosition(false);
            yield return null;
        }

        if (!despawning)
        {
            floatOffsetY = targetValue;
            ApplyWorldPosition(false);
        }
    }

    private static float EvaluateEase(Ease ease, float t)
    {
        t = Mathf.Clamp01(t);
        return ease switch
        {
            Ease.InSine => 1f - Mathf.Cos(t * Mathf.PI * 0.5f),
            Ease.OutSine => Mathf.Sin(t * Mathf.PI * 0.5f),
            Ease.InOutSine => -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f,
            Ease.InQuad => t * t,
            Ease.OutQuad => 1f - (1f - t) * (1f - t),
            Ease.InOutQuad => t < 0.5f
                ? 2f * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f,
            Ease.InCubic => t * t * t,
            Ease.OutCubic => 1f - Mathf.Pow(1f - t, 3f),
            Ease.InOutCubic => t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f,
            Ease.InQuart => t * t * t * t,
            Ease.OutQuart => 1f - Mathf.Pow(1f - t, 4f),
            Ease.InOutQuart => t < 0.5f
                ? 8f * t * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 4f) * 0.5f,
            Ease.InQuint => t * t * t * t * t,
            Ease.OutQuint => 1f - Mathf.Pow(1f - t, 5f),
            Ease.InOutQuint => t < 0.5f
                ? 16f * t * t * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 5f) * 0.5f,
            Ease.InBack => EaseInBack(t),
            Ease.OutBack => 1f - EaseInBack(1f - t),
            Ease.InOutBack => t < 0.5f
                ? EaseInBack(t * 2f) * 0.5f
                : 1f - EaseInBack((1f - t) * 2f) * 0.5f,
            _ => t
        };
    }

    private static float EaseInBack(float t)
    {
        const float back = 1.70158f;
        return (back + 1f) * t * t * t - back * t * t;
    }
}
