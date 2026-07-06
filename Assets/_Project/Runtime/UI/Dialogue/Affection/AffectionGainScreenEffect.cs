using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 : 호감도 증가 시 화면 가장자리 그라데이션과 하트 파티클 UI 연출을 재생한다.
/// </summary>
public sealed class AffectionGainScreenEffect : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private AffectionGradientBorderGraphic borderGraphic;
    [SerializeField] private RectTransform heartRoot;
    [SerializeField] private RectTransform overlayRect;
    [SerializeField] private bool allowRuntimeFallback;

    [Header("Border")]
    [SerializeField] private Color borderColor = new Color(1f, 0.42f, 0.18f, 0.92f);
    [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;
    [SerializeField, Min(0f)] private float holdDuration = 0.35f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;

    [Header("Hearts")]
    [SerializeField] private Sprite heartSprite;
    [SerializeField] private Color heartColor = new Color(1f, 0.28f, 0.46f, 0.95f);
    [SerializeField, Min(0)] private int minHeartCount = 7;
    [SerializeField, Min(0)] private int maxHeartCount = 12;
    [SerializeField] private Vector2 heartSpawnXRange = new Vector2(0.12f, 0.88f);
    [SerializeField] private Vector2 heartSpawnYRange = new Vector2(0.08f, 0.48f);
    [SerializeField] private Vector2 heartSizeRange = new Vector2(34f, 62f);
    [SerializeField] private Vector2 heartRiseDistanceRange = new Vector2(130f, 260f);
    [SerializeField] private Vector2 heartDurationRange = new Vector2(0.75f, 1.1f);
    [SerializeField] private Vector2 heartSpawnDelayRange = new Vector2(0f, 0.18f);
    [SerializeField] private Vector2 heartHorizontalDriftRange = new Vector2(-65f, 65f);
    [SerializeField] private List<Image> heartPool = new List<Image>();

    private static Sprite fallbackHeartSprite;

    private readonly List<GameObject> activeHearts = new List<GameObject>();
    private readonly HashSet<GameObject> pooledHeartObjects = new HashSet<GameObject>();
    private GameObject overlayObject;
    private Canvas overlayCanvas;
    private RectTransform borderRect;
    private Sequence borderSequence;
    private bool warnedMissingSceneSetup;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterPlaybackBackend()
    {
        AffectionPresentationPlayback.RegisterBackend(new Backend());
    }

    public static AffectionGainScreenEffect PrepareSceneInstance()
    {
        AffectionGainScreenEffect[] effects =
            FindObjectsByType<AffectionGainScreenEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (effects == null || effects.Length == 0)
            return null;

        AffectionGainScreenEffect effect = effects[0];
        effect.PrepareSceneOwnedOverlay();
        return effect;
    }

    /// <summary>
    /// 책임 : Core 호감도 presentation 준비 요청을 현재 씬의 AffectionGainScreenEffect 준비 동작으로 연결한다.
    /// </summary>
    private sealed class Backend : IAffectionPresentationBackend
    {
        public void PrepareSceneInstance()
        {
            AffectionGainScreenEffect.PrepareSceneInstance();
        }
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
        ClearHearts();
        HidePooledHearts();
    }

    private void OnDestroy()
    {
        StopActiveEffect();
    }

    public void Play(Action onComplete = null)
    {
        EnsureOverlay();

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

    public void ClearPreview()
    {
        StopActiveEffect();
        HidePooledHearts();
    }

    private void EnsureOverlay()
    {
        ResolveCanvas();
        ResolveSceneReferences();

        if (overlayObject != null && borderGraphic != null && heartRoot != null)
            return;

        if (!allowRuntimeFallback)
            return;

        CreateRuntimeFallbackOverlay();
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

        if (overlayCanvas == null && overlayObject != null)
            overlayCanvas = overlayObject.GetComponent<Canvas>();

        if (borderGraphic == null && overlayObject != null)
            borderGraphic = overlayObject.GetComponentInChildren<AffectionGradientBorderGraphic>(true);

        if (borderGraphic != null && borderRect == null)
            borderRect = borderGraphic.transform as RectTransform;

        if (heartRoot == null && overlayObject != null)
        {
            Transform heartRootTransform = FindChildByName(overlayObject.transform, "Hearts");
            if (heartRootTransform != null)
                heartRoot = heartRootTransform as RectTransform;
        }

        ResolveHeartPool();
        TryDisableTransparentCull(borderGraphic);
    }

    private void CreateRuntimeFallbackOverlay()
    {
        if (targetCanvas == null)
            return;

        if (overlayObject != null && overlayObject != gameObject)
            return;

        overlayObject = new GameObject("AffectionGainScreenEffect", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        overlayObject.transform.SetParent(targetCanvas.transform, false);

        overlayRect = overlayObject.transform as RectTransform;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.localScale = Vector3.one;

        overlayCanvas = overlayObject.GetComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingLayerID = targetCanvas.sortingLayerID;
        overlayCanvas.sortingOrder = Mathf.Max(1000, targetCanvas.sortingOrder + 1000);

        CanvasGroup canvasGroup = overlayObject.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject borderObject = new GameObject(
            "GradientBorder",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(AffectionGradientBorderGraphic));
        borderObject.transform.SetParent(overlayRect, false);

        borderRect = borderObject.transform as RectTransform;
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        borderRect.localScale = Vector3.one;

        borderGraphic = borderObject.GetComponent<AffectionGradientBorderGraphic>();
        borderGraphic.raycastTarget = false;
        borderGraphic.color = borderColor;
        borderGraphic.Intensity = 0f;
        borderGraphic.RevealProgress = 0f;
        TryDisableTransparentCull(borderGraphic);

        GameObject heartRootObject = new GameObject("Hearts", typeof(RectTransform));
        heartRootObject.transform.SetParent(overlayRect, false);

        heartRoot = heartRootObject.transform as RectTransform;
        heartRoot.anchorMin = Vector2.zero;
        heartRoot.anchorMax = Vector2.one;
        heartRoot.offsetMin = Vector2.zero;
        heartRoot.offsetMax = Vector2.zero;
        heartRoot.localScale = Vector3.one;

        ApplyOverlayRect();
        overlayObject.SetActive(false);
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

        Rect rect = overlayRect.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            rect = new Rect(0f, 0f, Screen.width, Screen.height);

        Sprite sprite = heartSprite != null ? heartSprite : GetFallbackHeartSprite();
        int heartCount = UnityEngine.Random.Range(minHeartCount, Mathf.Max(minHeartCount, maxHeartCount) + 1);

        ResolveHeartPool();
        if (heartPool != null && heartPool.Count > 0)
            heartCount = Mathf.Min(heartCount, heartPool.Count);

        for (int i = 0; i < heartCount; i++)
        {
            Image pooledHeart = heartPool != null && i < heartPool.Count ? heartPool[i] : null;
            SpawnHeart(sprite, rect, pooledHeart);
        }
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
        int heartCount = Mathf.Min(
            heartPool.Count,
            UnityEngine.Random.Range(minHeartCount, Mathf.Max(minHeartCount, maxHeartCount) + 1));

        for (int i = 0; i < heartCount; i++)
        {
            Image image = heartPool[i];
            if (image == null)
                continue;

            GameObject heartObject = image.gameObject;
            RectTransform heartRect = heartObject.transform as RectTransform;
            if (heartRect == null)
                continue;

            activeHearts.Add(heartObject);
            pooledHeartObjects.Add(heartObject);

            heartObject.SetActive(true);
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.color = heartColor;

            float size = UnityEngine.Random.Range(heartSizeRange.x, heartSizeRange.y);
            heartRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
            heartRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);

            float normalizedX = UnityEngine.Random.Range(heartSpawnXRange.x, heartSpawnXRange.y);
            float normalizedY = UnityEngine.Random.Range(heartSpawnYRange.x, heartSpawnYRange.y);
            float startX = Mathf.Lerp(-rect.width * 0.5f, rect.width * 0.5f, normalizedX);
            float startY = Mathf.Lerp(-rect.height * 0.5f, rect.height * 0.5f, normalizedY);
            float risePreviewOffset = UnityEngine.Random.Range(0f, heartRiseDistanceRange.y * 0.35f);

            heartRect.anchoredPosition = new Vector2(startX, startY + risePreviewOffset);
            heartRect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-22f, 22f));
            heartRect.localScale = Vector3.one * UnityEngine.Random.Range(0.82f, 1.08f);
        }
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

    private void SpawnHeart(Sprite sprite, Rect screenRect, Image pooledHeart)
    {
        if (sprite == null)
            return;

        GameObject heartObject;
        Image image;
        if (pooledHeart != null)
        {
            image = pooledHeart;
            heartObject = pooledHeart.gameObject;
            heartObject.SetActive(true);
            pooledHeartObjects.Add(heartObject);
        }
        else if (allowRuntimeFallback)
        {
            heartObject = new GameObject("Heart", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            heartObject.transform.SetParent(heartRoot, false);
            image = heartObject.GetComponent<Image>();
        }
        else
        {
            return;
        }

        activeHearts.Add(heartObject);

        RectTransform heartRect = heartObject.transform as RectTransform;
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;

        Color startColor = heartColor;
        startColor.a = 0f;
        image.color = startColor;

        float size = UnityEngine.Random.Range(heartSizeRange.x, heartSizeRange.y);
        heartRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
        heartRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);

        float normalizedX = UnityEngine.Random.Range(heartSpawnXRange.x, heartSpawnXRange.y);
        float normalizedY = UnityEngine.Random.Range(heartSpawnYRange.x, heartSpawnYRange.y);
        float startX = Mathf.Lerp(-screenRect.width * 0.5f, screenRect.width * 0.5f, normalizedX);
        float startY = Mathf.Lerp(-screenRect.height * 0.5f, screenRect.height * 0.5f, normalizedY);
        float riseDistance = UnityEngine.Random.Range(heartRiseDistanceRange.x, heartRiseDistanceRange.y);
        float horizontalDrift = UnityEngine.Random.Range(heartHorizontalDriftRange.x, heartHorizontalDriftRange.y);
        float duration = UnityEngine.Random.Range(heartDurationRange.x, heartDurationRange.y);
        float delay = UnityEngine.Random.Range(heartSpawnDelayRange.x, heartSpawnDelayRange.y);

        heartRect.anchoredPosition = new Vector2(startX, startY);
        heartRect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-18f, 18f));
        heartRect.localScale = Vector3.one * UnityEngine.Random.Range(0.82f, 1.08f);

        Sequence heartSequence = DOTween.Sequence();
        heartSequence.SetTarget(heartObject);
        heartSequence.SetUpdate(true);
        heartSequence.AppendInterval(delay);
        heartSequence.Append(image.DOFade(heartColor.a, 0.14f).SetEase(Ease.OutSine).SetUpdate(true));
        heartSequence.Join(heartRect.DOAnchorPos(
                new Vector2(startX + horizontalDrift, startY + riseDistance),
                duration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true));
        heartSequence.Join(heartRect.DORotate(
                new Vector3(0f, 0f, UnityEngine.Random.Range(-32f, 32f)),
                duration)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true));
        heartSequence.Insert(delay + duration * 0.58f, image.DOFade(0f, duration * 0.36f).SetEase(Ease.InSine).SetUpdate(true));
        heartSequence.OnComplete(() =>
        {
            activeHearts.Remove(heartObject);
            if (heartObject != null)
                FinishHeart(heartObject);
        });
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
            GameObject heartObject = activeHearts[i];
            if (heartObject == null)
                continue;

            DOTween.Kill(heartObject);
            heartObject.transform.DOKill();
            Image image = heartObject.GetComponent<Image>();
            if (image != null)
                image.DOKill();

            FinishHeart(heartObject);
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
            Image image = heartPool[i];
            if (image == null)
                continue;

            image.DOKill();
            image.transform.DOKill();

            Color color = image.color;
            color.a = 0f;
            image.color = color;

            image.gameObject.SetActive(false);
            pooledHeartObjects.Add(image.gameObject);
        }
    }

    private void FinishHeart(GameObject heartObject)
    {
        if (heartObject == null)
            return;

        if (pooledHeartObjects.Contains(heartObject))
        {
            Image image = heartObject.GetComponent<Image>();
            if (image != null)
            {
                Color color = image.color;
                color.a = 0f;
                image.color = color;
            }

            heartObject.SetActive(false);
            return;
        }

        if (Application.isPlaying)
            Destroy(heartObject);
        else
            DestroyImmediate(heartObject);
    }

    private void ResolveHeartPool()
    {
        heartPool ??= new List<Image>();
        heartPool.RemoveAll(image => image == null);

        if (heartPool.Count > 0 || heartRoot == null)
            return;

        Image[] pooledImages = heartRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < pooledImages.Length; i++)
        {
            Image image = pooledImages[i];
            if (image == null || heartPool.Contains(image))
                continue;

            heartPool.Add(image);
        }
    }

    private void WarnMissingSceneSetup()
    {
        if (warnedMissingSceneSetup)
            return;

        warnedMissingSceneSetup = true;
        Debug.LogWarning(
            "[AffectionGainScreenEffect] Scene-owned overlay setup is missing. Add an AffectionGainScreenEffect object with GradientBorder and Hearts children to the play UI root.",
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
        if (fallbackHeartSprite != null)
            return fallbackHeartSprite;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "GeneratedHeartSprite";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = ((x + 0.5f) / size - 0.5f) * 2.6f;
                float ny = ((y + 0.5f) / size - 0.43f) * 2.6f;
                float value = Mathf.Pow(nx * nx + ny * ny - 1f, 3f) - nx * nx * Mathf.Pow(ny, 3f);
                float alpha = Mathf.Clamp01((-value + 0.018f) / 0.04f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();

        fallbackHeartSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        fallbackHeartSprite.name = "GeneratedHeartSprite";
        fallbackHeartSprite.hideFlags = HideFlags.HideAndDontSave;
        return fallbackHeartSprite;
    }
}
