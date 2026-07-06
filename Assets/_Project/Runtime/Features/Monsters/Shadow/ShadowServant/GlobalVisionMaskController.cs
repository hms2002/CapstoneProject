using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 전역 어둠 오버레이와 플레이어 시야 마스크를 관리하고, 여러 시야 제한 요청자의 상태를 합산한다.
/// </summary>
[DisallowMultipleComponent]
public class GlobalVisionMaskController : MonoBehaviour
{
    private static GlobalVisionMaskController instance;

    [SerializeField] private GameObject darkMaskRoot;
    [SerializeField] private SpriteRenderer darkOverlayRenderer;
    [SerializeField] private GameObject playerVisionMaskPrefab;
    [SerializeField] private Transform playerVisionMaskParent;
    [SerializeField] private Vector3 playerVisionMaskOffset;
    [SerializeField, Range(0f, 1f)] private float defaultOverlayAlpha = 200f / 255f;
    [SerializeField, Range(0f, 1f)] private float fogOverlayAlpha = 1f;
    [SerializeField, Min(0f)] private float enterFogFadeDuration = 0.3f;
    [SerializeField, Min(0f)] private float exitFogFadeDuration = 0.2f;
    [SerializeField] private bool useUnscaledOverlayFadeTime = true;
    [SerializeField] private int fogOverlaySortingOrderBoost = 1;

    private readonly HashSet<int> activeRequesterIds = new();
    private PlayerVisionMaskFollower spawnedVisionMaskFollower;
    private Coroutine overlayAlphaFadeCoroutine;
    private bool hasCapturedBaseSortingOrder;
    private int baseOverlaySortingOrder;

    public static GlobalVisionMaskController Instance => instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning(
                $"[{nameof(GlobalVisionMaskController)}] Duplicate instance detected. Keeping the existing instance active.",
                this);
            SyncMaskState(false, instant: true);
            return;
        }

        instance = this;
        EnsureOverlayRenderer();
        SyncMaskState(false, instant: true);
    }

    private void OnDestroy()
    {
        StopOverlayAlphaFade();

        if (instance == this)
            instance = null;
    }

    public void AcquireDarkness(Object requester)
    {
        int requesterId = GetRequesterId(requester);
        activeRequesterIds.Add(requesterId);
        SyncMaskState(true, instant: false);
    }

    public void ReleaseDarkness(Object requester)
    {
        int requesterId = GetRequesterId(requester);
        activeRequesterIds.Remove(requesterId);
        SyncMaskState(activeRequesterIds.Count > 0, instant: false);
    }

    public void AttachToPlayer(Transform player)
    {
        if (player == null)
            return;

        EnsurePlayerVisionMask();
        spawnedVisionMaskFollower?.SetLocalOffset(playerVisionMaskOffset);
        spawnedVisionMaskFollower?.Bind(player);
    }

    private int GetRequesterId(Object requester)
    {
        return requester != null ? requester.GetInstanceID() : 0;
    }

    private void EnsurePlayerVisionMask()
    {
        if (spawnedVisionMaskFollower != null)
            return;

        if (playerVisionMaskPrefab == null)
            return;

        Transform parent = playerVisionMaskParent != null ? playerVisionMaskParent : transform;
        GameObject maskObject = Instantiate(playerVisionMaskPrefab, parent);

        spawnedVisionMaskFollower = maskObject.GetComponent<PlayerVisionMaskFollower>();
        if (spawnedVisionMaskFollower == null)
            spawnedVisionMaskFollower = maskObject.AddComponent<PlayerVisionMaskFollower>();
    }

    private void SyncMaskState(bool isActive, bool instant)
    {
        if (darkMaskRoot != null && !darkMaskRoot.activeSelf)
            darkMaskRoot.SetActive(true);

        EnsureOverlayRenderer();
        if (darkOverlayRenderer == null)
            return;

        ApplyOverlaySortingOrder(isActive);

        float targetAlpha = isActive ? fogOverlayAlpha : defaultOverlayAlpha;
        float fadeDuration = isActive ? enterFogFadeDuration : exitFogFadeDuration;
        ApplyOverlayAlpha(targetAlpha, instant, fadeDuration);
    }

    private void EnsureOverlayRenderer()
    {
        if (darkOverlayRenderer != null)
        {
            CacheBaseOverlaySortingOrder();
            return;
        }

        if (darkMaskRoot == null)
            return;

        darkOverlayRenderer = darkMaskRoot.GetComponentInChildren<SpriteRenderer>(true);
        CacheBaseOverlaySortingOrder();
    }

    private void CacheBaseOverlaySortingOrder()
    {
        if (darkOverlayRenderer == null || hasCapturedBaseSortingOrder)
            return;

        baseOverlaySortingOrder = darkOverlayRenderer.sortingOrder;
        hasCapturedBaseSortingOrder = true;
    }

    private void ApplyOverlaySortingOrder(bool isActive)
    {
        if (darkOverlayRenderer == null)
            return;

        CacheBaseOverlaySortingOrder();
        if (!hasCapturedBaseSortingOrder)
            return;

        darkOverlayRenderer.sortingOrder = isActive
            ? baseOverlaySortingOrder + fogOverlaySortingOrderBoost
            : baseOverlaySortingOrder;
    }

    private void ApplyOverlayAlpha(float targetAlpha, bool instant, float duration)
    {
        if (darkOverlayRenderer == null)
            return;

        StopOverlayAlphaFade();

        targetAlpha = Mathf.Clamp01(targetAlpha);

        if (instant || duration <= 0f)
        {
            Color immediateColor = darkOverlayRenderer.color;
            immediateColor.a = targetAlpha;
            darkOverlayRenderer.color = immediateColor;
            return;
        }

        overlayAlphaFadeCoroutine = StartCoroutine(FadeOverlayAlphaRoutine(targetAlpha, duration));
    }

    private void StopOverlayAlphaFade()
    {
        if (overlayAlphaFadeCoroutine == null)
            return;

        StopCoroutine(overlayAlphaFadeCoroutine);
        overlayAlphaFadeCoroutine = null;
    }

    private IEnumerator FadeOverlayAlphaRoutine(float targetAlpha, float duration)
    {
        float startAlpha = darkOverlayRenderer != null
            ? darkOverlayRenderer.color.a
            : targetAlpha;
        float elapsed = 0f;

        while (elapsed < duration && darkOverlayRenderer != null)
        {
            elapsed += useUnscaledOverlayFadeTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = duration > 0f ? elapsed / duration : 1f;
            SetOverlayAlpha(Mathf.LerpUnclamped(startAlpha, targetAlpha, EaseOutSine(t)));
            yield return null;
        }

        SetOverlayAlpha(targetAlpha);
        overlayAlphaFadeCoroutine = null;
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (darkOverlayRenderer == null)
            return;

        Color color = darkOverlayRenderer.color;
        color.a = Mathf.Clamp01(alpha);
        darkOverlayRenderer.color = color;
    }

    private static float EaseOutSine(float t)
    {
        return Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * 0.5f);
    }
}
