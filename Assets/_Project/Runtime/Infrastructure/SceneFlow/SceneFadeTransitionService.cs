using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
// 이 클래스의 책임: 씬 전환 중 페이드, 플레이어 입력 잠금, 로드 직후 안정화 대기를 한 곳에서 관리한다.
public sealed class SceneFadeTransitionService : MonoBehaviour, ISceneFadeTransitionHandle
{
    private const int ActiveOverlaySortingOrder = short.MaxValue;

    public static SceneFadeTransitionService Instance { get; private set; }

    [Header("Fade")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.2f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private bool deactivateOverlayWhenIdle = true;

    [Header("Post Load Hold")]
    [SerializeField, Min(0)] private int postLoadBlackFrames = 4;
    [SerializeField, Min(0f)] private float postLoadBlackHoldSeconds = 1f;

    [Header("Overlay Refs")]
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private CanvasGroup overlayCanvasGroup;
    [SerializeField] private Image overlayImage;

    private bool isTransitionActive;
    private bool isInitialized;
    private bool ownsRuntimeOverlay;
    private SceneFadeTransitionService pendingReplacementInstance;
    private Canvas elevatedOverlayCanvas;
    private bool hasSavedOverlayCanvasSorting;
    private bool savedOverlayCanvasOverrideSorting;
    private int savedOverlayCanvasSortingOrder;
    private bool hasSavedOverlayRectLayout;
    private Vector2 savedOverlayAnchorMin;
    private Vector2 savedOverlayAnchorMax;
    private Vector2 savedOverlayOffsetMin;
    private Vector2 savedOverlayOffsetMax;
    private readonly Dictionary<int, Object> externalPlayerUnlockBlockers = new();
    private static readonly ISceneFadeTransitionBackend PlaybackBackend = new SceneFadeTransitionBackend();

    public bool IsTransitionActive => isTransitionActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterPlaybackBackend()
    {
        SceneFadeTransitionPlayback.RegisterBackend(PlaybackBackend);
    }

    public void SetPlayerUnlockBlocked(Object owner, bool blocked)
    {
        if (owner == null)
            return;

        int ownerId = owner.GetInstanceID();
        if (blocked)
        {
            externalPlayerUnlockBlockers[ownerId] = owner;
            return;
        }

        externalPlayerUnlockBlockers.Remove(ownerId);
    }

    public static SceneFadeTransitionService EnsureInstance(bool allowRuntimeFallback = false)
    {
        if (Instance != null)
        {
            Instance.EnsureOverlaySetup(allowRuntimeFallback);
            return Instance;
        }

        SceneFadeTransitionService existing = FindFirstObjectByType<SceneFadeTransitionService>();
        if (existing != null)
        {
            Instance = existing;
            existing.EnsureOverlaySetup(allowRuntimeFallback);
            existing.Initialize();
            return existing;
        }

        if (allowRuntimeFallback)
        {
            GameObject host = new GameObject(nameof(SceneFadeTransitionService));
            SceneFadeTransitionService created = host.AddComponent<SceneFadeTransitionService>();
            created.CreateRuntimeOverlayIfNeeded();
            created.Initialize();
            return created;
        }

        return null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            ResolveOverlayReferences();
            if (ShouldReplaceExistingInstance(Instance))
            {
                SceneFadeTransitionService previousInstance = Instance;
                Instance = this;
                DestroyServiceGameObject(previousInstance);
            }
            else if (ShouldDeferReplacementUntilTransitionEnd(Instance))
            {
                Instance.DeferReplacementUntilTransitionEnds(this);
                return;
            }
            else
            {
                DestroyServiceGameObject(this);
                return;
            }
        }

        Instance = this;
        SceneFadeTransitionPlayback.RegisterBackend(PlaybackBackend);
        Initialize();
    }

    private void OnValidate()
    {
        ResolveOverlayReferences();
        ConfigureOverlayVisuals();
    }

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
    }

    private void OnDestroy()
    {
        if (isTransitionActive)
            RestoreTimeScaleImmediately();

        RestoreOverlayRectLayout();
        RestoreOverlayCanvasSorting();

        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 책임 : Core의 씬 페이드 playback 요청을 현재 런타임 SceneFadeTransitionService static 진입점으로 연결한다.
    /// </summary>
    private sealed class SceneFadeTransitionBackend : ISceneFadeTransitionBackend
    {
        public ISceneFadeTransitionHandle Instance => SceneFadeTransitionService.Instance;

        public ISceneFadeTransitionHandle EnsureInstance(bool allowRuntimeFallback = false)
        {
            return SceneFadeTransitionService.EnsureInstance(allowRuntimeFallback);
        }
    }

    public bool TryLoadScene(string targetSceneName)
    {
        SceneTransitionCoordinator coordinator = SceneTransitionCoordinator.EnsureInstance();
        return coordinator != null && coordinator.TryLoadScene(targetSceneName);
    }

    private void Initialize()
    {
        EnsureOverlaySetup(allowRuntimeFallback: false);

        if (isInitialized)
        {
            ResolveOverlayReferences();
            ConfigureOverlayVisuals();
            if (!isTransitionActive)
                ApplyOverlayVisualState(alpha: 0f, active: !deactivateOverlayWhenIdle);
            return;
        }

        EnsurePersistence();
        ResolveOverlayReferences();
        ConfigureOverlayVisuals();
        ApplyOverlayVisualState(alpha: 0f, active: !deactivateOverlayWhenIdle);
        isInitialized = true;
    }

    private void EnsureOverlaySetup(bool allowRuntimeFallback)
    {
        ResolveOverlayReferences();
        if (!allowRuntimeFallback || HasValidOverlaySetup())
            return;

        CreateRuntimeOverlayIfNeeded();
        ResolveOverlayReferences();
        ConfigureOverlayVisuals();
    }

    public bool TryBeginTransitionSession()
    {
        if (isTransitionActive)
            return false;

        Initialize();
        if (!HasValidOverlaySetup())
        {
            Debug.LogError(
                "[SceneFadeTransitionService] Missing overlay references. Attach the service manually and assign a full-screen overlay root, CanvasGroup, and Image.",
                this);
            return false;
        }

        isTransitionActive = true;
        PrepareTransitionUi();
        LockCurrentPlayer();
        TimeScalePauseService.Acquire(this);
        ApplyOverlayVisualState(alpha: overlayCanvasGroup != null ? overlayCanvasGroup.alpha : 0f, active: true);
        return true;
    }

    public IEnumerator FadeOutAsync()
    {
        RestoreOverlayRectLayout();
        yield return FadeCanvasGroup(toAlpha: 1f, duration: fadeOutDuration);
    }

    public IEnumerator FadeOutAsync(float duration)
    {
        RestoreOverlayRectLayout();
        yield return FadeCanvasGroup(toAlpha: 1f, duration: duration);
    }

    public IEnumerator FadeInAsync()
    {
        RestoreOverlayRectLayout();
        yield return FadeCanvasGroup(toAlpha: 0f, duration: fadeInDuration);
    }

    public IEnumerator FadeInAsync(float duration)
    {
        RestoreOverlayRectLayout();
        yield return FadeCanvasGroup(toAlpha: 0f, duration: duration);
    }

    /// <summary>
    /// 책임 : 기존 페이드 세션의 입력 잠금과 검은 오버레이를 재사용해 화면 오른쪽에서 왼쪽으로 덮는다.
    /// </summary>
    public IEnumerator WipeCoverRightToLeftAsync(float duration)
    {
        yield return AnimateHorizontalWipe(cover: true, duration);
    }

    /// <summary>
    /// 책임 : 로드 후 검은 오버레이의 오른쪽 경계를 왼쪽으로 이동시켜 같은 방향으로 화면을 드러낸다.
    /// </summary>
    public IEnumerator WipeRevealRightToLeftAsync(float duration)
    {
        yield return AnimateHorizontalWipe(cover: false, duration);
    }

    public bool TryBeginOverlayFadeSession(float initialAlpha = 0f)
    {
        if (isTransitionActive)
            return false;

        Initialize();
        if (!HasValidOverlaySetup())
        {
            Debug.LogError(
                "[SceneFadeTransitionService] Missing overlay references. Attach the service manually and assign a full-screen overlay root, CanvasGroup, and Image.",
                this);
            return false;
        }

        ApplyOverlayVisualState(alpha: initialAlpha, active: true);
        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.blocksRaycasts = true;
            overlayCanvasGroup.interactable = false;
        }

        return true;
    }

    public void EndOverlayFadeSession()
    {
        if (isTransitionActive)
            return;

        if (overlayCanvasGroup != null)
            overlayCanvasGroup.alpha = 0f;

        RestoreOverlayRectLayout();
        if (deactivateOverlayWhenIdle && overlayRoot != null)
            overlayRoot.SetActive(false);
        else
            ApplyOverlayVisualState(alpha: 0f, active: true);

        RestoreOverlayCanvasSorting();
    }

    public void ShowBlackImmediately()
    {
        RestoreOverlayRectLayout();
        ApplyOverlayVisualState(alpha: 1f, active: true);
    }

    public void HideOverlayImmediately()
    {
        RestoreOverlayRectLayout();
        ApplyOverlayVisualState(alpha: 0f, active: !deactivateOverlayWhenIdle);
    }

    public IEnumerator WaitForPostLoadSettleAsync()
    {
        yield return WaitForPostLoadSettle();
        LockCurrentPlayer();
    }

    public void EndTransitionSession()
    {
        UnlockCurrentPlayer();
        RestoreTimeScaleImmediately();
        isTransitionActive = false;

        RestoreOverlayRectLayout();
        if (deactivateOverlayWhenIdle && overlayRoot != null)
            overlayRoot.SetActive(false);
        else
            ApplyOverlayVisualState(alpha: 0f, active: true);

        RestoreOverlayCanvasSorting();
        PromotePendingReplacementIfAvailable();
    }

    private IEnumerator FadeCanvasGroup(float toAlpha, float duration)
    {
        if (overlayCanvasGroup == null)
            yield break;

        ApplyOverlayVisualState(overlayCanvasGroup.alpha, active: true);
        float fromAlpha = overlayCanvasGroup.alpha;
        if (duration <= 0f)
        {
            ApplyOverlayVisualState(toAlpha, active: true);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            BringOverlayToFront();
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            overlayCanvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
            yield return null;
        }

        ApplyOverlayVisualState(toAlpha, active: true);
    }

    private IEnumerator AnimateHorizontalWipe(bool cover, float duration)
    {
        RectTransform overlayRect = overlayImage != null ? overlayImage.rectTransform : null;
        if (overlayRect == null || overlayCanvasGroup == null)
            yield break;

        SaveOverlayRectLayout();
        ApplyOverlayVisualState(alpha: 1f, active: true);

        if (duration <= 0f)
        {
            ApplyHorizontalWipeLayout(overlayRect, cover, 1f);
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                BringOverlayToFront();
                elapsed += Time.unscaledDeltaTime;
                ApplyHorizontalWipeLayout(overlayRect, cover, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            ApplyHorizontalWipeLayout(overlayRect, cover, 1f);
        }

        if (!cover)
        {
            overlayCanvasGroup.alpha = 0f;
            RestoreOverlayRectLayout();
        }
    }

    private void SaveOverlayRectLayout()
    {
        if (hasSavedOverlayRectLayout || overlayImage == null)
            return;

        RectTransform overlayRect = overlayImage.rectTransform;
        savedOverlayAnchorMin = overlayRect.anchorMin;
        savedOverlayAnchorMax = overlayRect.anchorMax;
        savedOverlayOffsetMin = overlayRect.offsetMin;
        savedOverlayOffsetMax = overlayRect.offsetMax;
        hasSavedOverlayRectLayout = true;
    }

    private void RestoreOverlayRectLayout()
    {
        if (!hasSavedOverlayRectLayout)
            return;

        if (overlayImage != null)
        {
            RectTransform overlayRect = overlayImage.rectTransform;
            overlayRect.anchorMin = savedOverlayAnchorMin;
            overlayRect.anchorMax = savedOverlayAnchorMax;
            overlayRect.offsetMin = savedOverlayOffsetMin;
            overlayRect.offsetMax = savedOverlayOffsetMax;
        }

        hasSavedOverlayRectLayout = false;
    }

    private static void ApplyHorizontalWipeLayout(RectTransform overlayRect, bool cover, float progress)
    {
        float t = Mathf.Clamp01(progress);
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.anchorMin = cover ? new Vector2(1f - t, 0f) : Vector2.zero;
        overlayRect.anchorMax = cover ? Vector2.one : new Vector2(1f - t, 1f);
    }

    private IEnumerator WaitForPostLoadSettle()
    {
        int frames = Mathf.Max(0, postLoadBlackFrames);
        for (int i = 0; i < frames; i++)
            yield return null;

        float holdSeconds = Mathf.Max(0f, postLoadBlackHoldSeconds);
        if (holdSeconds <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < holdSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void PrepareTransitionUi()
    {
        UiCommandPlayback.CloseAllPopups(force: true);
        UiCommandPlayback.HideHoverImmediate();
        UiCommandPlayback.HideWorldPrompt();
    }

    private void LockCurrentPlayer()
    {
        PlayerInteractor2D player = PlayerRuntimeRegistry.CurrentPlayer != null
            ? PlayerRuntimeRegistry.CurrentPlayer
            : PlayerInteractor2D.Instance;

        if (player != null && player.CurrentState != InteractState.None)
            player.SetInteractState(InteractState.None);
    }

    private void UnlockCurrentPlayer()
    {
        if (HasExternalPlayerUnlockBlockers())
            return;

        PlayerInteractor2D player = PlayerRuntimeRegistry.CurrentPlayer != null
            ? PlayerRuntimeRegistry.CurrentPlayer
            : PlayerInteractor2D.Instance;

        if (player != null && player.CurrentState == InteractState.None)
            player.SetInteractState(InteractState.Idle);
    }

    private void HandlePlayerRegistered(PlayerInteractor2D player)
    {
        if (!isTransitionActive || player == null)
            return;

        player.SetInteractState(InteractState.None);
    }

    private bool HasExternalPlayerUnlockBlockers()
    {
        if (externalPlayerUnlockBlockers.Count == 0)
            return false;

        List<int> deadOwnerIds = null;
        foreach (KeyValuePair<int, Object> pair in externalPlayerUnlockBlockers)
        {
            if (pair.Value != null)
                return true;

            deadOwnerIds ??= new List<int>();
            deadOwnerIds.Add(pair.Key);
        }

        if (deadOwnerIds != null)
        {
            for (int i = 0; i < deadOwnerIds.Count; i++)
                externalPlayerUnlockBlockers.Remove(deadOwnerIds[i]);
        }

        return false;
    }

    private void EnsurePersistence()
    {
        if (GetComponent<IGlobalCanvasRootMarker>() != null)
            return;

        GlobalCanvasPlayback.AdoptService(transform);

        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    private void ResolveOverlayReferences()
    {
        if (overlayRoot == null)
        {
            if (overlayCanvasGroup != null)
                overlayRoot = overlayCanvasGroup.gameObject;
            else if (overlayImage != null)
                overlayRoot = overlayImage.gameObject;
        }

        if (overlayCanvasGroup == null)
        {
            if (overlayRoot != null)
            {
                overlayCanvasGroup = overlayRoot.GetComponent<CanvasGroup>();
                overlayCanvasGroup ??= overlayRoot.GetComponentInChildren<CanvasGroup>(includeInactive: true);
            }
        }

        if (overlayImage == null)
        {
            if (overlayRoot != null)
            {
                overlayImage = overlayRoot.GetComponent<Image>();
                overlayImage ??= overlayRoot.GetComponentInChildren<Image>(includeInactive: true);
            }
        }

        if (overlayRoot == null)
        {
            if (overlayCanvasGroup != null)
                overlayRoot = overlayCanvasGroup.gameObject;
            else if (overlayImage != null)
                overlayRoot = overlayImage.gameObject;
        }
    }

    private void CreateRuntimeOverlayIfNeeded()
    {
        if (HasValidOverlaySetup())
            return;

        var canvasObject = new GameObject(
            "RuntimeFadeCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var overlayObject = new GameObject(
            "Overlay",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image));

        overlayObject.transform.SetParent(canvasObject.transform, false);

        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        overlayRoot = overlayObject;
        overlayCanvasGroup = overlayObject.GetComponent<CanvasGroup>();
        overlayImage = overlayObject.GetComponent<Image>();
        overlayImage.raycastTarget = true;
        overlayImage.color = fadeColor;
        overlayCanvasGroup.alpha = 0f;
        overlayCanvasGroup.blocksRaycasts = true;
        overlayCanvasGroup.interactable = false;
        ownsRuntimeOverlay = true;

        EnsureRuntimeEventSystemExists();
    }

    private bool ShouldReplaceExistingInstance(SceneFadeTransitionService existingInstance)
    {
        if (existingInstance == null || existingInstance == this)
            return false;

        if (existingInstance.IsTransitionActive)
            return false;

        if (!existingInstance.ownsRuntimeOverlay)
            return false;

        return HasAuthoredOverlaySetup();
    }

    private bool ShouldDeferReplacementUntilTransitionEnd(SceneFadeTransitionService existingInstance)
    {
        if (existingInstance == null || existingInstance == this)
            return false;

        if (!existingInstance.IsTransitionActive)
            return false;

        return HasAuthoredOverlaySetup();
    }

    private void DeferReplacementUntilTransitionEnds(SceneFadeTransitionService replacement)
    {
        if (replacement == null || replacement == this)
            return;

        replacement.ResolveOverlayReferences();
        replacement.ConfigureOverlayVisuals();
        replacement.ApplyOverlayVisualState(alpha: 0f, active: !replacement.deactivateOverlayWhenIdle);
        pendingReplacementInstance = replacement;
    }

    private void PromotePendingReplacementIfAvailable()
    {
        SceneFadeTransitionService replacement = pendingReplacementInstance;
        pendingReplacementInstance = null;

        if (replacement == null || replacement == this)
            return;

        replacement.ResolveOverlayReferences();
        if (!replacement.HasAuthoredOverlaySetup())
            return;

        Instance = replacement;
        replacement.Initialize();
        DestroyServiceGameObject(this);
    }

    private static void DestroyServiceGameObject(SceneFadeTransitionService service)
    {
        if (service == null)
            return;

        GameObject owner = service.gameObject;
        if (owner != null)
            Destroy(owner);
    }

    private bool HasAuthoredOverlaySetup()
    {
        return HasValidOverlaySetup() && !ownsRuntimeOverlay;
    }

    private void EnsureRuntimeEventSystemExists()
    {
        EventSystem existing = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (existing != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        DontDestroyOnLoad(eventSystemObject);
    }

    private void ConfigureOverlayVisuals()
    {
        if (overlayImage != null)
            overlayImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.interactable = false;
            overlayCanvasGroup.blocksRaycasts = true;
            overlayCanvasGroup.alpha = Mathf.Clamp01(overlayCanvasGroup.alpha);
        }
    }

    private bool HasValidOverlaySetup()
    {
        ResolveOverlayReferences();
        return overlayRoot != null && overlayCanvasGroup != null && overlayImage != null;
    }

    private void RestoreTimeScaleImmediately()
    {
        TimeScalePauseService.Release(this);
    }

    private void ApplyOverlayVisualState(float alpha, bool active)
    {
        if (overlayRoot != null)
        {
            if (active)
                BringOverlayToFront();

            overlayRoot.SetActive(active);
        }

        if (overlayCanvasGroup != null)
            overlayCanvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    private void BringOverlayToFront()
    {
        if (overlayRoot == null)
            return;

        Transform overlayTransform = overlayRoot.transform;
        if (overlayTransform.parent != null)
            overlayTransform.SetAsLastSibling();

        ElevateOverlayCanvas(overlayTransform);
    }

    private void ElevateOverlayCanvas(Transform overlayTransform)
    {
        Canvas overlayCanvas = ResolveOverlayCanvas(overlayTransform);
        if (overlayCanvas == null)
            return;

        if (elevatedOverlayCanvas != overlayCanvas)
        {
            RestoreOverlayCanvasSorting();
            SaveOverlayCanvasSorting(overlayCanvas);
        }
        else if (!hasSavedOverlayCanvasSorting)
        {
            SaveOverlayCanvasSorting(overlayCanvas);
        }

        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = ActiveOverlaySortingOrder;

        Transform canvasTransform = overlayCanvas.transform;
        if (canvasTransform.parent != null)
            canvasTransform.SetAsLastSibling();
    }

    private static Canvas ResolveOverlayCanvas(Transform overlayTransform)
    {
        if (overlayTransform == null)
            return null;

        Canvas[] parentCanvases = overlayTransform.GetComponentsInParent<Canvas>(true);
        return parentCanvases != null && parentCanvases.Length > 0
            ? parentCanvases[0]
            : null;
    }

    private void SaveOverlayCanvasSorting(Canvas overlayCanvas)
    {
        if (overlayCanvas == null)
            return;

        elevatedOverlayCanvas = overlayCanvas;
        savedOverlayCanvasOverrideSorting = overlayCanvas.overrideSorting;
        savedOverlayCanvasSortingOrder = overlayCanvas.sortingOrder;
        hasSavedOverlayCanvasSorting = true;
    }

    private void RestoreOverlayCanvasSorting()
    {
        if (!hasSavedOverlayCanvasSorting)
            return;

        if (elevatedOverlayCanvas != null)
        {
            elevatedOverlayCanvas.overrideSorting = savedOverlayCanvasOverrideSorting;
            elevatedOverlayCanvas.sortingOrder = savedOverlayCanvasSortingOrder;
        }

        elevatedOverlayCanvas = null;
        hasSavedOverlayCanvasSorting = false;
    }
}
