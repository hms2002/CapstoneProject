using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 : 보스 대화 선택지에서 호감도 실패/부정 리액션 화면 연출을 재생한다.
/// 실제 호감도 수치 감소가 없어도 부정 판정 자체의 피드백을 담당한다.
/// </summary>
public sealed class ChoiceFailureScreenEffect : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private AffectionGradientBorderGraphic borderGraphic;
    [SerializeField] private RectTransform heartRoot;
    [SerializeField] private RectTransform overlayRect;

    [Header("Border")]
    [SerializeField] private Color borderColor = new Color(0.05f, 0.12f, 0.42f, 0.92f);
    [SerializeField, Min(0f)] private float fadeInDuration = 0.16f;
    [SerializeField, Min(0f)] private float holdDuration = 0.18f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.28f;

    [Header("Broken Hearts")]
    [SerializeField] private Sprite heartSprite;
    [SerializeField] private Color heartColor = new Color(0.48f, 0.66f, 1f, 0.94f);
    [SerializeField, Min(0)] private int minHeartCount = 5;
    [SerializeField, Min(0)] private int maxHeartCount = 8;
    [SerializeField] private Vector2 heartSpawnXRange = new Vector2(0.16f, 0.84f);
    [SerializeField] private Vector2 heartSpawnYRange = new Vector2(0.58f, 0.9f);
    [SerializeField] private Vector2 heartSizeRange = new Vector2(38f, 66f);
    [SerializeField] private Vector2 heartFallDistanceRange = new Vector2(170f, 310f);
    [SerializeField] private Vector2 heartDurationRange = new Vector2(0.78f, 1.18f);
    [SerializeField] private Vector2 heartSpawnDelayRange = new Vector2(0f, 0.14f);
    [SerializeField] private Vector2 heartHorizontalDriftRange = new Vector2(-48f, 48f);
    [SerializeField] private Vector2 breakTimeRatioRange = new Vector2(0.34f, 0.5f);
    [SerializeField] private Vector2 splitDistanceRange = new Vector2(16f, 36f);
    [SerializeField] private Vector2 breakDropDistanceRange = new Vector2(18f, 70f);
    [SerializeField] private Vector2 pieceRotationRange = new Vector2(12f, 34f);
    [SerializeField] private List<ChoiceFailureBrokenHeartItem> heartPool = new List<ChoiceFailureBrokenHeartItem>();

    private readonly List<ChoiceFailureBrokenHeartItem> activeHearts = new List<ChoiceFailureBrokenHeartItem>();
    private GameObject overlayObject;
    private RectTransform borderRect;
    private Sequence borderSequence;
    private bool warnedMissingSceneSetup;

    public static ChoiceFailureScreenEffect PrepareSceneInstance()
    {
        ChoiceFailureScreenEffect[] effects =
            FindObjectsByType<ChoiceFailureScreenEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (effects == null || effects.Length == 0)
            return null;

        ChoiceFailureScreenEffect effect = effects[0];
        effect.PrepareSceneOwnedOverlay();
        return effect;
    }

    public void PrepareSceneOwnedOverlay()
    {
        ResolveCanvas();
        ResolveSceneReferences();

        if (overlayObject == null || borderGraphic == null || heartRoot == null)
            return;

        ApplyOverlayRect();
        borderGraphic.color = borderColor;
        borderGraphic.Intensity = 0f;
        borderGraphic.RevealProgress = 0f;
        HidePooledHearts();
    }

    private void OnDestroy()
    {
        StopActiveEffect();
    }

    public void Play(Action onComplete = null)
    {
        EnsureOverlay();
        PlayFailureSound();

        if (overlayObject == null || borderGraphic == null || heartRoot == null)
        {
            WarnMissingSceneSetup();
            onComplete?.Invoke();
            return;
        }

        StopActiveEffect();

        overlayObject.SetActive(true);
        ApplyOverlayRect();
        overlayRect.SetAsLastSibling();
        borderGraphic.color = borderColor;
        borderGraphic.Intensity = 1f;
        borderGraphic.RevealProgress = 0f;

        SpawnHearts();

        borderSequence = DOTween.Sequence();
        borderSequence.SetUpdate(true);
        borderSequence.Append(DOTween.To(
                () => borderGraphic.RevealProgress,
                value => borderGraphic.RevealProgress = value,
                1f,
                fadeInDuration)
            .SetEase(Ease.OutSine)
            .SetUpdate(true));
        borderSequence.AppendInterval(holdDuration);
        borderSequence.Append(DOTween.To(
                () => borderGraphic.RevealProgress,
                value => borderGraphic.RevealProgress = value,
                0f,
                fadeOutDuration)
            .SetEase(Ease.InSine)
            .SetUpdate(true));
        borderSequence.OnComplete(() =>
        {
            borderGraphic.Intensity = 0f;
            borderGraphic.RevealProgress = 0f;
            ClearHearts();
            overlayObject.SetActive(false);
            onComplete?.Invoke();
        });
    }

    public void Preview()
    {
        if (Application.isPlaying)
        {
            Play();
            return;
        }

        PreviewStatic();
    }

    public void PreviewStatic()
    {
        EnsureOverlay();

        if (overlayObject == null || borderGraphic == null || heartRoot == null)
        {
            WarnMissingSceneSetup();
            return;
        }

        StopActiveEffect();

        overlayObject.SetActive(true);
        ApplyOverlayRect();
        overlayRect.SetAsLastSibling();
        borderGraphic.color = borderColor;
        borderGraphic.Intensity = 1f;
        borderGraphic.RevealProgress = 1f;

        SpawnStaticPreviewHearts();
    }

    /// <summary>
    /// 책임 : 호감도 수치 감소가 없는 부정 선택지에서도 동일한 실패 피드백 사운드를 재생한다.
    /// </summary>
    private static void PlayFailureSound()
    {
        AffectionFeedbackSoundPlayer.PlayDown();
    }

    public void ClearPreview()
    {
        StopActiveEffect();
        HidePooledHearts();
    }

    private void EnsureOverlay()
    {
        ResolveCanvas();
        ResolveSceneReferences();
    }

    private void ResolveSceneReferences()
    {
        if (overlayRect != null)
            overlayObject = overlayRect.gameObject;

        if (overlayObject == null)
            overlayObject = gameObject;

        if (overlayRect == null)
            overlayRect = overlayObject.transform as RectTransform;

        if (targetCanvas == null && overlayObject != null)
            targetCanvas = overlayObject.GetComponentInParent<Canvas>();

        if (borderGraphic == null && overlayObject != null)
            borderGraphic = overlayObject.GetComponentInChildren<AffectionGradientBorderGraphic>(true);

        if (borderGraphic != null && borderRect == null)
            borderRect = borderGraphic.transform as RectTransform;

        if (heartRoot == null && overlayObject != null)
        {
            Transform heartRootTransform = FindChildByName(overlayObject.transform, "BrokenHearts");
            if (heartRootTransform != null)
                heartRoot = heartRootTransform as RectTransform;
        }

        ResolveHeartPool();
        TryDisableTransparentCull(borderGraphic);
    }

    private void ResolveCanvas()
    {
        if (targetCanvas != null)
            return;

        targetCanvas = GetComponentInParent<Canvas>();
        if (targetCanvas == null)
            targetCanvas = FindAnyObjectByType<Canvas>();
    }

    private void SpawnHearts()
    {
        Canvas.ForceUpdateCanvases();
        ApplyOverlayRect();
        ResolveHeartPool();

        if (heartPool == null || heartPool.Count == 0)
            return;

        Rect rect = overlayRect.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            rect = new Rect(0f, 0f, Screen.width, Screen.height);

        Sprite sprite = heartSprite != null ? heartSprite : GetFallbackHeartSprite();
        int heartCount = Mathf.Min(
            heartPool.Count,
            UnityEngine.Random.Range(minHeartCount, Mathf.Max(minHeartCount, maxHeartCount) + 1));

        for (int i = 0; i < heartCount; i++)
            SpawnHeart(sprite, rect, heartPool[i]);
    }

    private void SpawnStaticPreviewHearts()
    {
        Canvas.ForceUpdateCanvases();
        ApplyOverlayRect();
        ResolveHeartPool();

        if (heartPool == null || heartPool.Count == 0)
            return;

        Rect rect = overlayRect.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            rect = new Rect(0f, 0f, Screen.width, Screen.height);

        Sprite sprite = heartSprite != null ? heartSprite : GetFallbackHeartSprite();
        int heartCount = Mathf.Min(heartPool.Count, maxHeartCount);

        for (int i = 0; i < heartCount; i++)
        {
            ChoiceFailureBrokenHeartItem item = heartPool[i];
            if (item == null)
                continue;

            activeHearts.Add(item);
            float size = UnityEngine.Random.Range(heartSizeRange.x, heartSizeRange.y);
            Vector2 position = ResolveSpawnPosition(rect);
            float splitDistance = UnityEngine.Random.Range(splitDistanceRange.x, splitDistanceRange.y);
            item.ShowStaticPreview(sprite, heartColor, position, size, splitDistance);
        }
    }

    private void SpawnHeart(Sprite sprite, Rect screenRect, ChoiceFailureBrokenHeartItem item)
    {
        if (sprite == null || item == null)
            return;

        activeHearts.Add(item);

        Vector2 startPosition = ResolveSpawnPosition(screenRect);
        float size = UnityEngine.Random.Range(heartSizeRange.x, heartSizeRange.y);
        float fallDistance = UnityEngine.Random.Range(heartFallDistanceRange.x, heartFallDistanceRange.y);
        float horizontalDrift = UnityEngine.Random.Range(heartHorizontalDriftRange.x, heartHorizontalDriftRange.y);
        float duration = UnityEngine.Random.Range(heartDurationRange.x, heartDurationRange.y);
        float delay = UnityEngine.Random.Range(heartSpawnDelayRange.x, heartSpawnDelayRange.y);
        float breakTimeRatio = UnityEngine.Random.Range(breakTimeRatioRange.x, breakTimeRatioRange.y);
        float splitDistance = UnityEngine.Random.Range(splitDistanceRange.x, splitDistanceRange.y);
        float breakDrop = UnityEngine.Random.Range(breakDropDistanceRange.x, breakDropDistanceRange.y);
        float rotation = UnityEngine.Random.Range(pieceRotationRange.x, pieceRotationRange.y);

        item.Play(
            sprite,
            heartColor,
            startPosition,
            size,
            fallDistance,
            horizontalDrift,
            duration,
            delay,
            breakTimeRatio,
            splitDistance,
            breakDrop,
            rotation,
            completedItem => activeHearts.Remove(completedItem));
    }

    private Vector2 ResolveSpawnPosition(Rect rect)
    {
        float normalizedX = UnityEngine.Random.Range(heartSpawnXRange.x, heartSpawnXRange.y);
        float normalizedY = UnityEngine.Random.Range(heartSpawnYRange.x, heartSpawnYRange.y);
        float startX = Mathf.Lerp(-rect.width * 0.5f, rect.width * 0.5f, normalizedX);
        float startY = Mathf.Lerp(-rect.height * 0.5f, rect.height * 0.5f, normalizedY);
        return new Vector2(startX, startY);
    }

    private void ApplyOverlayRect()
    {
        if (overlayRect == null)
            return;

        Canvas.ForceUpdateCanvases();

        RectTransform canvasRect = targetCanvas != null ? targetCanvas.transform as RectTransform : null;
        bool canFillCanvasRect = canvasRect != null && canvasRect.rect.width > 1f && canvasRect.rect.height > 1f;

        if (canFillCanvasRect)
        {
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
        }
        else
        {
            Vector2 fallbackSize = ResolveCanvasFallbackSize();
            overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
            overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.anchoredPosition = Vector2.zero;
            overlayRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fallbackSize.x);
            overlayRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, fallbackSize.y);
        }

        overlayRect.localScale = Vector3.one;
        ApplyStretchRect(borderRect);
        ApplyStretchRect(heartRoot);
        borderGraphic?.SetVerticesDirty();
    }

    private Vector2 ResolveCanvasFallbackSize()
    {
        if (targetCanvas != null)
        {
            CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.referenceResolution.x > 1f && scaler.referenceResolution.y > 1f)
                return scaler.referenceResolution;

            Rect pixelRect = targetCanvas.pixelRect;
            if (pixelRect.width > 1f && pixelRect.height > 1f)
                return pixelRect.size;
        }

        return new Vector2(Mathf.Max(1f, Screen.width), Mathf.Max(1f, Screen.height));
    }

    private static void ApplyStretchRect(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }

    private void StopActiveEffect()
    {
        borderSequence?.Kill();
        borderSequence = null;

        if (borderGraphic != null)
        {
            borderGraphic.Intensity = 0f;
            borderGraphic.RevealProgress = 0f;
        }

        ClearHearts();

        if (overlayObject != null)
            overlayObject.SetActive(false);
    }

    private void ClearHearts()
    {
        for (int i = activeHearts.Count - 1; i >= 0; i--)
        {
            ChoiceFailureBrokenHeartItem item = activeHearts[i];
            if (item != null)
                item.ResetState();
        }

        activeHearts.Clear();
    }

    private void HidePooledHearts()
    {
        ResolveHeartPool();

        if (heartPool == null)
            return;

        for (int i = 0; i < heartPool.Count; i++)
        {
            ChoiceFailureBrokenHeartItem item = heartPool[i];
            if (item != null)
                item.ResetState();
        }
    }

    private void ResolveHeartPool()
    {
        heartPool ??= new List<ChoiceFailureBrokenHeartItem>();
        heartPool.RemoveAll(item => item == null);

        if (heartPool.Count > 0 || heartRoot == null)
            return;

        ChoiceFailureBrokenHeartItem[] pooledItems = heartRoot.GetComponentsInChildren<ChoiceFailureBrokenHeartItem>(true);
        for (int i = 0; i < pooledItems.Length; i++)
        {
            ChoiceFailureBrokenHeartItem item = pooledItems[i];
            if (item != null && !heartPool.Contains(item))
                heartPool.Add(item);
        }
    }

    private void WarnMissingSceneSetup()
    {
        if (warnedMissingSceneSetup)
            return;

        warnedMissingSceneSetup = true;
        Debug.LogWarning(
            "[ChoiceFailureScreenEffect] Scene-owned overlay setup is missing. Add a ChoiceFailureScreenEffect object with GradientBorder and BrokenHearts children to the play UI root.",
            this);
    }

    private static void TryDisableTransparentCull(Graphic graphic)
    {
        if (graphic == null)
            return;

        CanvasRenderer renderer = graphic.GetComponent<CanvasRenderer>();
        if (renderer != null)
            renderer.cullTransparentMesh = false;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        Transform directChild = root.Find(childName);
        if (directChild != null)
            return directChild;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
                return child;
        }

        return null;
    }

    private static Sprite GetFallbackHeartSprite()
    {
        return null;
    }
}
