using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TutorialInfoPanel : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Visuals")]
    [SerializeField] private Image windowImage;
    [SerializeField] private Image titleImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Image contentImage;
    [SerializeField] private Sprite defaultWindowSprite;
    [SerializeField] private Sprite defaultTitleSprite;
    [SerializeField] private bool hideContentImageWhenEmpty = true;

    [Header("Progress")]
    [SerializeField] private TMP_Text advanceGuideText;
    [SerializeField] private Image holdProgressImage;
    [SerializeField, Min(0.05f)] private float defaultHoldSeconds = 0.75f;
    [SerializeField] private string advanceGuideFormat = "Hold {0}";
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Flow")]
    [SerializeField] private bool blockGameFlowWhileOpen = true;
    [SerializeField] private UnityEvent onShown;
    [SerializeField] private UnityEvent onCompleted;
    [SerializeField] private UnityEvent onHidden;

    private TutorialInfoRequest activeRequest;
    private GameFlowInputBlocker inputBlocker;
    private float heldSeconds;
    private bool isOpen;

    public bool IsOpen => isOpen;

    private void Reset()
    {
        root = gameObject;
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        if (root == null)
            root = gameObject;
    }

    private void Update()
    {
        if (!isOpen)
            return;

        float holdSeconds = ResolveHoldSeconds();
        if (IsAdvanceHeld())
            heldSeconds += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        else
            heldSeconds = 0f;

        SetProgress(holdSeconds <= 0f ? 1f : heldSeconds / holdSeconds);
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
        SetVisible(true);
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
        SetImageSprite(windowImage, request.windowSprite != null ? request.windowSprite : defaultWindowSprite);
        SetImageSprite(titleImage, request.titleSprite != null ? request.titleSprite : defaultTitleSprite);
        SetImageSprite(contentImage, request.contentSprite);

        if (contentImage != null && hideContentImageWhenEmpty)
            contentImage.gameObject.SetActive(request.contentSprite != null);

        if (titleText != null)
            titleText.text = request.title ?? string.Empty;

        if (bodyText != null)
            bodyText.text = request.body ?? string.Empty;

        if (advanceGuideText != null)
            advanceGuideText.text = FormatAdvanceGuide();
    }

    private void SetVisible(bool visible)
    {
        isOpen = visible;

        if (root != null)
            root.SetActive(visible);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }

    private void HideInternal()
    {
        ReleaseInputBlocker();
        SetVisible(false);
        onHidden?.Invoke();
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
        if (holdProgressImage != null)
            holdProgressImage.fillAmount = Mathf.Clamp01(value);
    }

    private static void SetImageSprite(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private void OnDisable()
    {
        ReleaseInputBlocker();
        isOpen = false;
    }

    private void OnDestroy()
    {
        ReleaseInputBlocker();
    }
}
