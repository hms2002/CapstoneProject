using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public class GlobalVisionMaskController : MonoBehaviour
{
    // 이 클래스의 책임:
    // - 전역 어둠 오버레이 알파를 관리한다.
    // - 플레이어 시야 마스크 프리팹을 생성하고 추적을 붙인다.
    // - 여러 요청자가 동시에 시야 제한을 요청해도 안정적으로 상태를 유지한다.

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

    private readonly HashSet<int> activeRequesterIds = new();
    private PlayerVisionMaskFollower spawnedVisionMaskFollower;
    private Tween overlayAlphaTween;

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
        overlayAlphaTween?.Kill();
        overlayAlphaTween = null;

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

        float targetAlpha = isActive ? fogOverlayAlpha : defaultOverlayAlpha;
        float fadeDuration = isActive ? enterFogFadeDuration : exitFogFadeDuration;
        ApplyOverlayAlpha(targetAlpha, instant, fadeDuration);
    }

    private void EnsureOverlayRenderer()
    {
        if (darkOverlayRenderer != null)
            return;

        if (darkMaskRoot == null)
            return;

        darkOverlayRenderer = darkMaskRoot.GetComponentInChildren<SpriteRenderer>(true);
    }

    private void ApplyOverlayAlpha(float targetAlpha, bool instant, float duration)
    {
        if (darkOverlayRenderer == null)
            return;

        overlayAlphaTween?.Kill();
        overlayAlphaTween = null;

        targetAlpha = Mathf.Clamp01(targetAlpha);

        if (instant || duration <= 0f)
        {
            Color immediateColor = darkOverlayRenderer.color;
            immediateColor.a = targetAlpha;
            darkOverlayRenderer.color = immediateColor;
            return;
        }

        overlayAlphaTween = DOTween.To(
                () => darkOverlayRenderer != null ? darkOverlayRenderer.color.a : targetAlpha,
                value =>
                {
                    if (darkOverlayRenderer == null)
                        return;

                    Color tweenColor = darkOverlayRenderer.color;
                    tweenColor.a = value;
                    darkOverlayRenderer.color = tweenColor;
                },
                targetAlpha,
                duration)
            .SetEase(Ease.OutSine)
            .SetUpdate(useUnscaledOverlayFadeTime);
    }
}
