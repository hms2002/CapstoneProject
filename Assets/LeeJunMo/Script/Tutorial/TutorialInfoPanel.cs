using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TutorialInfoPanel : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Visuals")]
    [FormerlySerializedAs("windowImage")]
    [SerializeField] private Image tutorialPanelImage;
    [SerializeField] private Image titleImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Image contentImage;
    [FormerlySerializedAs("defaultWindowSprite")]
    [SerializeField] private Sprite defaultTutorialPanelSprite;
    [SerializeField] private Sprite defaultTitleSprite;
    [SerializeField] private bool hideContentImageWhenEmpty = true;

    [Header("Pages")]
    [SerializeField] private Button previousPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private GameObject previousPageRoot;
    [SerializeField] private GameObject nextPageRoot;
    [SerializeField] private GameObject pageKeyGuideRoot;
    [SerializeField] private GameObject previousPageKeyGuideRoot;
    [SerializeField] private GameObject nextPageKeyGuideRoot;
    [SerializeField] private Image previousPageKeyGlyphImage;
    [SerializeField] private Image nextPageKeyGlyphImage;
    [SerializeField] private TMP_Text pageNumberText;
    [SerializeField] private bool enableKeyboardPageInput = true;
    [SerializeField] private KeyCode previousPageKey = KeyCode.A;
    [SerializeField] private KeyCode nextPageKey = KeyCode.D;
    [SerializeField] private Color disabledPageKeyGlyphColor = new(1f, 1f, 1f, 0.35f);

    [Header("Progress")]
    [SerializeField] private TMP_Text advanceGuideText;
    [SerializeField] private GameObject advanceHoldButtonRoot;
    [SerializeField] private HoldFillButtonView holdButtonView;
    [SerializeField] private HoldActionButton advanceHoldButton;
    [SerializeField] private Image holdProgressImage;
    [SerializeField, Min(0.05f)] private float defaultHoldSeconds = 0.75f;
    [SerializeField] private string advanceGuideFormat = "Hold {0}";
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Presentation")]
    [FormerlySerializedAs("dimPanelGroup")]
    [SerializeField] private CanvasGroup dimPanel;
    [FormerlySerializedAs("panelMotionRoot")]
    [SerializeField] private RectTransform tutorialPanel;
    [SerializeField, Range(0f, 1f)] private float dimVisibleAlpha = 0.65f;
    [SerializeField] private Vector2 hiddenPanelOffset = new(0f, -96f);
    [SerializeField, Min(0f)] private float openDuration = 0.28f;
    [SerializeField, Min(0f)] private float closeDuration = 0.18f;
    [SerializeField, Min(0f)] private float overshoot = 1.4f;
    [SerializeField] private bool animateOpenClose = true;

    [Header("Flow")]
    [SerializeField] private bool hideOnPlayStart = true;
    [SerializeField] private bool blockGameFlowWhileOpen = true;
    [SerializeField] private UnityEvent onShown;
    [SerializeField] private UnityEvent onCompleted;
    [SerializeField] private UnityEvent onHidden;

    private TutorialInfoRequest activeRequest;
    private GameFlowInputBlocker inputBlocker;
    private float heldSeconds;
    private bool isOpen;
    private bool advanceHoldButtonBound;
    private bool pageButtonsBound;
    private bool isClosing;
    private TutorialInfoPage[] activePages = Array.Empty<TutorialInfoPage>();
    private int currentPageIndex;
    private Vector2 panelOpenAnchoredPosition;
    private bool hasPanelOpenAnchoredPosition;
    private Coroutine presentationRoutine;
    private Image cachedPreviousPageKeyGlyphImage;
    private Image cachedNextPageKeyGlyphImage;
    private Color previousPageKeyGlyphOriginalColor;
    private Color nextPageKeyGlyphOriginalColor;
    private bool hasPreviousPageKeyGlyphOriginalColor;
    private bool hasNextPageKeyGlyphOriginalColor;

    public bool IsOpen => isOpen;

    private void Reset()
    {
        root = gameObject;
        canvasGroup = GetComponent<CanvasGroup>();
        tutorialPanel = transform as RectTransform;
    }

    private void Awake()
    {
        if (root == null)
            root = gameObject;
    }

    private void OnEnable()
    {
        BindAdvanceHoldButton();
        BindPageButtons();
    }

    private void Start()
    {
        if (hideOnPlayStart)
            HideForPlayStart();
    }

    private void Update()
    {
        if (!isOpen || isClosing)
            return;

        HandlePageKeyboardInput();

        if (!IsOnFinalPage())
        {
            heldSeconds = 0f;
            return;
        }

        if (advanceHoldButton != null)
        {
            heldSeconds = 0f;
            return;
        }

        float holdSeconds = Mathf.Max(0.01f, ResolveHoldSeconds());
        if (IsAdvanceHeld())
            heldSeconds += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        else
            heldSeconds = 0f;

        SetProgress(heldSeconds / holdSeconds);
        if (heldSeconds >= holdSeconds)
            Complete();
    }

    public bool Show(TutorialInfoRequest request)
    {
        if (request.usePersistentCompletion &&
            !request.allowReplayWhenCompleted &&
            TutorialProgressStore.IsCompleted(request.tutorialId))
        {
            return false;
        }

        activeRequest = request;
        heldSeconds = 0f;

        ApplyRequest(request);
        SetProgress(0f);
        SetVisible(true, animateOpenClose);
        PrepareAdvanceHoldButtonForOpen();
        AcquireInputBlocker();
        onShown?.Invoke();
        return true;
    }

    public void OpenEmpty()
    {
        Show(new TutorialInfoRequest());
    }

    public void OpenById(string tutorialId)
    {
        Show(new TutorialInfoRequest
        {
            tutorialId = tutorialId,
            usePersistentCompletion = true,
            markCompletedOnClose = true
        });
    }

    public void Complete()
    {
        if (!isOpen)
            return;

        if (activeRequest.usePersistentCompletion && activeRequest.markCompletedOnClose)
            TutorialProgressStore.MarkCompleted(activeRequest.tutorialId);

        onCompleted?.Invoke();
        HideInternal();
    }

    public void Hide()
    {
        if (!isOpen)
            return;

        HideInternal();
    }

    private void ApplyRequest(TutorialInfoRequest request)
    {
        SetImageSprite(tutorialPanelImage, request.tutorialPanelSprite != null ? request.tutorialPanelSprite : defaultTutorialPanelSprite);
        SetImageSprite(titleImage, request.titleSprite != null ? request.titleSprite : defaultTitleSprite);

        if (advanceGuideText != null && advanceHoldButton == null)
            advanceGuideText.text = FormatAdvanceGuide();

        activePages = ResolvePages(request);
        currentPageIndex = 0;
        ApplyCurrentPage();
    }

    private void SetVisible(bool visible, bool animate)
    {
        isOpen = visible;

        if (visible)
        {
            isClosing = false;
            SetRootActive(true);
            PlayOpenPresentation(animate);
            return;
        }

        PlayClosePresentation(animate);
    }

    private void HideInternal()
    {
        if (isClosing)
            return;

        isOpen = false;
        isClosing = true;
        CleanupAdvanceHoldButtonForClose();
        SetVisible(false, animateOpenClose);
    }

    private void HideForPlayStart()
    {
        StopPresentationRoutine();
        isOpen = false;
        isClosing = false;
        heldSeconds = 0f;
        SetProgress(0f);
        CleanupAdvanceHoldButtonForClose();
        SetInteractionEnabled(false);
        SetDimPanelActive(false);
        ReleaseInputBlocker();
        SetRootActive(false);
    }

    public void ShowPreviousPage()
    {
        if (!CanNavigatePages() || currentPageIndex <= 0)
            return;

        currentPageIndex--;
        ApplyCurrentPage();
    }

    public void ShowNextPage()
    {
        if (!CanNavigatePages() || currentPageIndex >= GetLastPageIndex())
            return;

        currentPageIndex++;
        ApplyCurrentPage();
    }

    private TutorialInfoPage[] ResolvePages(TutorialInfoRequest request)
    {
        if (request.pages != null && request.pages.Length > 0)
            return request.pages;

        return Array.Empty<TutorialInfoPage>();
    }

    private void ApplyCurrentPage()
    {
        TutorialInfoPage page = GetCurrentPage();

        if (titleText != null)
            titleText.text = page.title ?? string.Empty;

        if (bodyText != null)
            bodyText.text = page.body ?? string.Empty;

        SetImageSprite(contentImage, page.contentSprite);

        if (contentImage != null && hideContentImageWhenEmpty)
            contentImage.gameObject.SetActive(page.contentSprite != null);

        RefreshPageControlsAndAdvanceState();
    }

    private TutorialInfoPage GetCurrentPage()
    {
        if (activePages == null || activePages.Length == 0)
            return default;

        currentPageIndex = Mathf.Clamp(currentPageIndex, 0, activePages.Length - 1);
        return activePages[currentPageIndex];
    }

    private bool CanNavigatePages()
    {
        return isOpen && !isClosing && activePages != null && activePages.Length > 1;
    }

    private int GetLastPageIndex()
    {
        return activePages != null && activePages.Length > 0 ? activePages.Length - 1 : 0;
    }

    private bool IsOnFinalPage()
    {
        return currentPageIndex >= GetLastPageIndex();
    }

    private void HandlePageKeyboardInput()
    {
        if (!enableKeyboardPageInput || activePages == null || activePages.Length <= 1)
            return;

        if (InputKeyCompatibility.WasPressedThisFrame(previousPageKey))
            ShowPreviousPage();
        else if (InputKeyCompatibility.WasPressedThisFrame(nextPageKey))
            ShowNextPage();
    }

    private void RefreshPageControlsAndAdvanceState()
    {
        bool hasMultiplePages = activePages != null && activePages.Length > 1;
        bool canGoPrevious = hasMultiplePages && currentPageIndex > 0;
        bool canGoNext = hasMultiplePages && currentPageIndex < GetLastPageIndex();

        SetPageControlState(previousPageRoot, previousPageButton, hasMultiplePages, canGoPrevious);
        SetPageControlState(nextPageRoot, nextPageButton, hasMultiplePages, canGoNext);
        RefreshPageKeyGuides(hasMultiplePages, canGoPrevious, canGoNext);
        RefreshPageNumberText();

        bool canAdvance = IsOnFinalPage();
        heldSeconds = 0f;
        SetProgress(0f);

        if (advanceHoldButton != null)
        {
            BindAdvanceHoldButton();
            advanceHoldButton.ResetHold();
            advanceHoldButton.SetInteractable(canAdvance);
            SetAdvanceHoldButtonRootActive(canAdvance);
        }
    }

    private void RefreshPageNumberText()
    {
        if (pageNumberText == null)
            return;

        int totalPages = activePages != null ? activePages.Length : 0;
        if (totalPages <= 0)
        {
            pageNumberText.text = string.Empty;
            return;
        }

        int displayPage = Mathf.Clamp(currentPageIndex + 1, 1, totalPages);
        pageNumberText.text = $"{displayPage}/{totalPages}";
    }

    private void RefreshPageKeyGuides(bool hasMultiplePages, bool canGoPrevious, bool canGoNext)
    {
        bool showKeyboardGuides = enableKeyboardPageInput && hasMultiplePages;

        SetOptionalRootActive(pageKeyGuideRoot, showKeyboardGuides);
        SetOptionalRootActive(previousPageKeyGuideRoot, showKeyboardGuides);
        SetOptionalRootActive(nextPageKeyGuideRoot, showKeyboardGuides);

        ApplyPageKeyGlyphs(canGoPrevious, canGoNext);
    }

    private void ApplyPageKeyGlyphs(bool canGoPrevious, bool canGoNext)
    {
        if (previousPageKeyGlyphImage == null)
            previousPageKeyGlyphImage = ResolveKeyGlyphImage(previousPageKeyGuideRoot);

        if (nextPageKeyGlyphImage == null)
            nextPageKeyGlyphImage = ResolveKeyGlyphImage(nextPageKeyGuideRoot);

        ApplyPageKeyGlyph(
            previousPageKeyGlyphImage,
            previousPageKey,
            canGoPrevious,
            ref cachedPreviousPageKeyGlyphImage,
            ref hasPreviousPageKeyGlyphOriginalColor,
            ref previousPageKeyGlyphOriginalColor);
        ApplyPageKeyGlyph(
            nextPageKeyGlyphImage,
            nextPageKey,
            canGoNext,
            ref cachedNextPageKeyGlyphImage,
            ref hasNextPageKeyGlyphOriginalColor,
            ref nextPageKeyGlyphOriginalColor);
    }

    private static Image ResolveKeyGlyphImage(GameObject rootObject)
    {
        if (rootObject == null)
            return null;

        Image image = rootObject.GetComponent<Image>();
        if (image != null)
            return image;

        return rootObject.GetComponentInChildren<Image>(true);
    }

    private void ApplyPageKeyGlyph(
        Image image,
        KeyCode key,
        bool interactable,
        ref Image cachedImage,
        ref bool hasOriginalColor,
        ref Color originalColor)
    {
        if (image == null)
            return;

        if (cachedImage != image)
        {
            cachedImage = image;
            hasOriginalColor = false;
        }

        if (!hasOriginalColor)
        {
            originalColor = image.color;
            hasOriginalColor = true;
        }

        InputGlyphPresentation glyph = InputGlyphDatabase.Resolve(key);
        Sprite icon = InputGlyphVisualUtility.ResolveIcon(glyph);
        image.sprite = icon;
        image.enabled = icon != null;
        image.color = interactable ? originalColor : disabledPageKeyGlyphColor;
        image.raycastTarget = false;
    }

    private void AcquireInputBlocker()
    {
        if (!blockGameFlowWhileOpen)
            return;

        inputBlocker ??= GameFlowInputBlocker.GetOrAdd(this);
        inputBlocker?.Acquire();
    }

    private void ReleaseInputBlocker()
    {
        inputBlocker?.Release();
    }

    private void BindPageButtons()
    {
        if (pageButtonsBound)
            return;

        if (previousPageButton != null)
            previousPageButton.onClick.AddListener(ShowPreviousPage);

        if (nextPageButton != null)
            nextPageButton.onClick.AddListener(ShowNextPage);

        pageButtonsBound = true;
    }

    private void UnbindPageButtons()
    {
        if (!pageButtonsBound)
            return;

        if (previousPageButton != null)
            previousPageButton.onClick.RemoveListener(ShowPreviousPage);

        if (nextPageButton != null)
            nextPageButton.onClick.RemoveListener(ShowNextPage);

        pageButtonsBound = false;
    }

    private static void SetPageControlState(GameObject rootOverride, Button button, bool visible, bool interactable)
    {
        GameObject rootObject = rootOverride != null ? rootOverride : button != null ? button.gameObject : null;
        if (rootObject != null && rootObject.activeSelf != visible)
            rootObject.SetActive(visible);

        if (button != null)
            button.interactable = visible && interactable;
    }

    private void SetOptionalRootActive(GameObject rootObject, bool active)
    {
        if (rootObject == null)
            return;

        if (rootObject == gameObject || rootObject == root)
            return;

        if (rootObject.activeSelf != active)
            rootObject.SetActive(active);
    }

    private void SetAdvanceHoldButtonRootActive(bool active)
    {
        GameObject rootObject = ResolveAdvanceHoldButtonRoot();
        if (rootObject == null)
            return;

        if (rootObject == gameObject || rootObject == root)
            return;

        if (rootObject.activeSelf != active)
            rootObject.SetActive(active);
    }

    private GameObject ResolveAdvanceHoldButtonRoot()
    {
        if (advanceHoldButtonRoot != null)
            return advanceHoldButtonRoot;

        if (advanceHoldButton != null)
            return advanceHoldButton.gameObject;

        return null;
    }

    private void SetRootActive(bool active)
    {
        if (root != null)
        {
            if (root.activeSelf != active)
                root.SetActive(active);
        }
        else if (gameObject.activeSelf != active)
        {
            gameObject.SetActive(active);
        }
    }

    private void PlayOpenPresentation(bool animate)
    {
        StopPresentationRoutine();
        CapturePanelOpenPosition();

        SetInteractionEnabled(false);
        SetDimPanelActive(true);

        if (!animate || openDuration <= 0f)
        {
            ApplyPresentationPose(dimVisibleAlpha, panelOpenAnchoredPosition);
            SetInteractionEnabled(true);
            return;
        }

        ApplyPresentationPose(0f, panelOpenAnchoredPosition + hiddenPanelOffset);
        presentationRoutine = StartCoroutine(OpenPresentationRoutine());
    }

    private void PlayClosePresentation(bool animate)
    {
        StopPresentationRoutine();
        CapturePanelOpenPosition();

        SetInteractionEnabled(false);
        SetDimPanelActive(true);

        if (!animate || closeDuration <= 0f)
        {
            FinishClosePresentation();
            return;
        }

        presentationRoutine = StartCoroutine(ClosePresentationRoutine());
    }

    private IEnumerator OpenPresentationRoutine()
    {
        float elapsed = 0f;
        Vector2 fromPosition = panelOpenAnchoredPosition + hiddenPanelOffset;
        Vector2 toPosition = panelOpenAnchoredPosition;

        while (elapsed < openDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / openDuration);
            ApplyPresentationPose(
                Mathf.Lerp(0f, dimVisibleAlpha, EaseOutCubic(t)),
                Vector2.LerpUnclamped(fromPosition, toPosition, EaseOutBack(t, overshoot)));
            yield return null;
        }

        ApplyPresentationPose(dimVisibleAlpha, toPosition);
        presentationRoutine = null;
        SetInteractionEnabled(true);
    }

    private IEnumerator ClosePresentationRoutine()
    {
        float elapsed = 0f;
        Vector2 fromPosition = tutorialPanel != null ? tutorialPanel.anchoredPosition : panelOpenAnchoredPosition;
        Vector2 toPosition = panelOpenAnchoredPosition + hiddenPanelOffset;
        float fromDimAlpha = dimPanel != null ? dimPanel.alpha : 0f;

        while (elapsed < closeDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / closeDuration);
            ApplyPresentationPose(
                Mathf.Lerp(fromDimAlpha, 0f, EaseInCubic(t)),
                Vector2.LerpUnclamped(fromPosition, toPosition, EaseInBack(t, overshoot)));
            yield return null;
        }

        FinishClosePresentation();
    }

    private void FinishClosePresentation()
    {
        ApplyPresentationPose(0f, panelOpenAnchoredPosition + hiddenPanelOffset);
        SetInteractionEnabled(false);
        SetDimPanelActive(false);
        isClosing = false;
        presentationRoutine = null;
        ReleaseInputBlocker();
        onHidden?.Invoke();
        SetRootActive(false);
    }

    private void CapturePanelOpenPosition()
    {
        if (hasPanelOpenAnchoredPosition || tutorialPanel == null)
            return;

        panelOpenAnchoredPosition = tutorialPanel.anchoredPosition;
        hasPanelOpenAnchoredPosition = true;
    }

    private void ApplyPresentationPose(float dimAlpha, Vector2 panelAnchoredPosition)
    {
        if (dimPanel != null)
            dimPanel.alpha = Mathf.Clamp01(dimAlpha);

        if (tutorialPanel != null)
            tutorialPanel.anchoredPosition = panelAnchoredPosition;

        if (canvasGroup != null && canvasGroup != dimPanel)
            canvasGroup.alpha = 1f;
    }

    private void SetInteractionEnabled(bool enabled)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.interactable = enabled;
        canvasGroup.blocksRaycasts = enabled;
    }

    private void SetDimPanelActive(bool active)
    {
        if (dimPanel == null)
            return;

        GameObject dimObject = dimPanel.gameObject;
        bool canToggleDimObject = dimObject != gameObject && dimObject != root;
        if (canToggleDimObject && dimObject.activeSelf != active)
            dimObject.SetActive(active);

        if (!active)
            dimPanel.alpha = 0f;

        dimPanel.interactable = false;
        dimPanel.blocksRaycasts = active;
    }

    private void StopPresentationRoutine()
    {
        if (presentationRoutine == null)
            return;

        StopCoroutine(presentationRoutine);
        presentationRoutine = null;
    }

    private bool IsAdvanceHeld()
    {
        InputBindingService input = InputBindingService.EnsureInstance();
        return input != null && input.IsPressed(InputActionId.DialogueAdvance);
    }

    private float ResolveHoldSeconds()
    {
        return activeRequest.holdSeconds > 0f ? activeRequest.holdSeconds : defaultHoldSeconds;
    }

    private string FormatAdvanceGuide()
    {
        if (string.IsNullOrEmpty(advanceGuideFormat))
            return string.Empty;

        string label = InputBindingService.EnsureInstance().GetBindingDisplayLabel(InputActionId.DialogueAdvance);
        if (string.IsNullOrEmpty(label))
            label = "Space";

        return string.Format(advanceGuideFormat, label);
    }

    private void SetProgress(float value)
    {
        holdButtonView?.SetProgress(value);

        if (holdProgressImage != null)
            holdProgressImage.fillAmount = Mathf.Clamp01(value);
    }

    private void BindAdvanceHoldButton()
    {
        if (advanceHoldButton == null || advanceHoldButtonBound)
            return;

        advanceHoldButton.ProgressChanged += SetProgress;
        advanceHoldButton.HoldCanceled += ResetHoldProgress;
        advanceHoldButton.HoldCompleted += Complete;
        advanceHoldButtonBound = true;
    }

    private void UnbindAdvanceHoldButton()
    {
        if (advanceHoldButton == null || !advanceHoldButtonBound)
            return;

        advanceHoldButton.ProgressChanged -= SetProgress;
        advanceHoldButton.HoldCanceled -= ResetHoldProgress;
        advanceHoldButton.HoldCompleted -= Complete;
        advanceHoldButtonBound = false;
    }

    private void ResetHoldProgress()
    {
        heldSeconds = 0f;
        SetProgress(0f);
    }

    private void PrepareAdvanceHoldButtonForOpen()
    {
        RefreshPageControlsAndAdvanceState();
    }

    private void CleanupAdvanceHoldButtonForClose()
    {
        heldSeconds = 0f;

        if (advanceHoldButton == null)
        {
            SetProgress(0f);
            return;
        }

        advanceHoldButton.ResetHold();
        advanceHoldButton.SetInteractable(false);
        SetProgress(0f);
        SetAdvanceHoldButtonRootActive(false);
    }

    private static void SetImageSprite(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private static float EaseOutCubic(float t)
    {
        t = 1f - Mathf.Clamp01(t);
        return 1f - t * t * t;
    }

    private static float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    private static float EaseOutBack(float t, float amount)
    {
        t = Mathf.Clamp01(t) - 1f;
        float c1 = Mathf.Max(0f, amount);
        float c3 = c1 + 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    private static float EaseInBack(float t, float amount)
    {
        t = Mathf.Clamp01(t);
        float c1 = Mathf.Max(0f, amount);
        float c3 = c1 + 1f;
        return c3 * t * t * t - c1 * t * t;
    }

    private void OnDisable()
    {
        StopPresentationRoutine();
        SetDimPanelActive(false);
        UnbindPageButtons();
        UnbindAdvanceHoldButton();
        CleanupAdvanceHoldButtonForClose();
        ReleaseInputBlocker();
        isOpen = false;
        isClosing = false;
    }

    private void OnDestroy()
    {
        ReleaseInputBlocker();
    }
}
