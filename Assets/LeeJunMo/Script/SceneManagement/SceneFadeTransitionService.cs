using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class SceneFadeTransitionService : MonoBehaviour
{
    public static SceneFadeTransitionService Instance { get; private set; }

    [Header("Fade")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.2f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private bool deactivateOverlayWhenIdle = true;

    [Header("Post Load Hold")]
    [SerializeField, Min(0)] private int postLoadBlackFrames = 2;
    [SerializeField, Min(0f)] private float postLoadBlackHoldSeconds = 0.1f;

    [Header("Overlay Refs")]
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private CanvasGroup overlayCanvasGroup;
    [SerializeField] private Image overlayImage;

    private bool isTransitionActive;
    private float savedTimeScale = 1f;
    private bool isInitialized;
    private bool ownsRuntimeOverlay;
    private SceneFadeTransitionService pendingReplacementInstance;
    private readonly Dictionary<int, Object> externalPlayerUnlockBlockers = new();

    public bool IsTransitionActive => isTransitionActive;

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
                Destroy(previousInstance.gameObject);
            }
            else if (ShouldDeferReplacementUntilTransitionEnd(Instance))
            {
                Instance.DeferReplacementUntilTransitionEnds(this);
                return;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        Instance = this;
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

        if (Instance == this)
            Instance = null;
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
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        ApplyOverlayVisualState(alpha: overlayCanvasGroup != null ? overlayCanvasGroup.alpha : 0f, active: true);
        return true;
    }

    public IEnumerator FadeOutAsync()
    {
        yield return FadeCanvasGroup(toAlpha: 1f, duration: fadeOutDuration);
    }

    public IEnumerator FadeOutAsync(float duration)
    {
        yield return FadeCanvasGroup(toAlpha: 1f, duration: duration);
    }

    public IEnumerator FadeInAsync()
    {
        yield return FadeCanvasGroup(toAlpha: 0f, duration: fadeInDuration);
    }

    public IEnumerator FadeInAsync(float duration)
    {
        yield return FadeCanvasGroup(toAlpha: 0f, duration: duration);
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

        if (deactivateOverlayWhenIdle && overlayRoot != null)
            overlayRoot.SetActive(false);
        else
            ApplyOverlayVisualState(alpha: 0f, active: true);
    }

    public void ShowBlackImmediately()
    {
        ApplyOverlayVisualState(alpha: 1f, active: true);
    }

    public void HideOverlayImmediately()
    {
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

        if (deactivateOverlayWhenIdle && overlayRoot != null)
            overlayRoot.SetActive(false);
        else
            ApplyOverlayVisualState(alpha: 0f, active: true);

        PromotePendingReplacementIfAvailable();
    }

    private IEnumerator FadeCanvasGroup(float toAlpha, float duration)
    {
        if (overlayCanvasGroup == null)
            yield break;

        float fromAlpha = overlayCanvasGroup.alpha;
        if (duration <= 0f)
        {
            overlayCanvasGroup.alpha = toAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            overlayCanvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
            yield return null;
        }

        overlayCanvasGroup.alpha = toAlpha;
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
        if (UIManager.Instance == null)
            return;

        UIManager.Instance.CloseAllPopups();
        UIManager.Instance.HideHoverImmediate();
        UIManager.Instance.HideWorldPrompt();
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
        if (GetComponent<GlobalUIRoot>() != null)
            return;

        GlobalUIRoot.AdoptService(transform);

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
        Destroy(gameObject);
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
        Time.timeScale = savedTimeScale;
        savedTimeScale = 1f;
    }

    private void ApplyOverlayVisualState(float alpha, bool active)
    {
        if (overlayRoot != null)
            overlayRoot.SetActive(active);

        if (overlayCanvasGroup != null)
            overlayCanvasGroup.alpha = Mathf.Clamp01(alpha);
    }
}
