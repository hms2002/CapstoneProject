using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    private Coroutine transitionRoutine;
    private bool isTransitionActive;
    private float savedTimeScale = 1f;
    private bool isInitialized;

    public bool IsTransitionActive => isTransitionActive;

    public static SceneFadeTransitionService EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        SceneFadeTransitionService existing = FindFirstObjectByType<SceneFadeTransitionService>();
        if (existing != null)
        {
            Instance = existing;
            existing.Initialize();
            return existing;
        }

        return null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
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
        if (string.IsNullOrWhiteSpace(targetSceneName))
            return false;

        if (transitionRoutine != null)
            return false;

        Initialize();
        if (!HasValidOverlaySetup())
        {
            Debug.LogError(
                "[SceneFadeTransitionService] Missing overlay references. Attach the service manually and assign a full-screen overlay root, CanvasGroup, and Image.",
                this);
            return false;
        }

        transitionRoutine = StartCoroutine(CoLoadSceneWithFade(targetSceneName));
        return true;
    }

    private void Initialize()
    {
        if (isInitialized)
        {
            ResolveOverlayReferences();
            ConfigureOverlayVisuals();
            return;
        }

        EnsurePersistence();
        ResolveOverlayReferences();
        ConfigureOverlayVisuals();
        ApplyOverlayVisualState(alpha: 0f, active: !deactivateOverlayWhenIdle);
        isInitialized = true;
    }

    private IEnumerator CoLoadSceneWithFade(string targetSceneName)
    {
        isTransitionActive = true;

        PrepareTransitionUi();
        LockCurrentPlayer();
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        ApplyOverlayVisualState(alpha: overlayCanvasGroup != null ? overlayCanvasGroup.alpha : 0f, active: true);

        yield return FadeCanvasGroup(toAlpha: 1f, duration: fadeOutDuration);

        AsyncOperation loadOperation = null;
        try
        {
            loadOperation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SceneFadeTransitionService] Failed to load scene '{targetSceneName}': {ex.Message}", this);
        }

        if (loadOperation == null)
        {
            yield return FadeCanvasGroup(toAlpha: 0f, duration: fadeInDuration);
            FinishTransition();
            yield break;
        }

        while (!loadOperation.isDone)
            yield return null;

        yield return WaitForPostLoadSettle();
        LockCurrentPlayer();

        yield return FadeCanvasGroup(toAlpha: 0f, duration: fadeInDuration);

        FinishTransition();
    }

    private void FinishTransition()
    {
        UnlockCurrentPlayer();
        RestoreTimeScaleImmediately();
        isTransitionActive = false;
        transitionRoutine = null;

        if (deactivateOverlayWhenIdle && overlayRoot != null)
            overlayRoot.SetActive(false);
        else
            ApplyOverlayVisualState(alpha: 0f, active: true);
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
