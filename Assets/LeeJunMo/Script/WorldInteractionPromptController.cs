using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WorldInteractionPromptController : MonoBehaviour
{
    private const InputActionId PromptAction = InputActionId.Interact;
    private const string RuntimeCanvasName = "PromptLayout";

    public static WorldInteractionPromptController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform promptRoot;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image promptIconImage;
    [SerializeField] private SpriteRenderer promptIconSpriteRenderer;

    [Header("Display")]
    [SerializeField] private Sprite defaultIcon;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private bool hideWhenDescriptionEmpty = true;
    [SerializeField] private bool prependBindingLabelWhenIconMissing = true;

    [Header("World Layout")]
    [SerializeField] private bool useWorldSpaceCanvasLayout = true;
    [SerializeField] private Vector2 canvasSize = new Vector2(280f, 72f);
    [SerializeField] private float canvasScale = 0.01f;
    [SerializeField] private Vector2 layoutPadding = new Vector2(8f, 4f);
    [SerializeField] private float layoutSpacing = 8f;
    [SerializeField] private float glyphHeight = 40f;
    [SerializeField] private float minGlyphWidth = 40f;

    private IInteractable currentTarget;
    private Transform currentAnchor;

    private Canvas runtimeCanvas;
    private RectTransform runtimeCanvasRect;
    private RectTransform runtimeContentRect;
    private Image runtimePromptIconImage;
    private LayoutElement runtimeIconLayoutElement;
    private TextMeshProUGUI runtimeDescriptionText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (promptRoot == null)
            promptRoot = transform;

        EnsurePromptLayout();
        Hide();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void LateUpdate()
    {
        SyncCanvasCamera();

        if (!IsTargetAlive(currentTarget))
        {
            Hide();
            return;
        }

        if (currentTarget == null)
            return;

        currentAnchor = ResolveAnchor(currentTarget);
        if (currentAnchor == null)
        {
            Hide();
            return;
        }

        ApplyVisuals(currentTarget);
        UpdatePosition();
        SetVisible(ShouldShow(currentTarget));
    }

    public void Show(IInteractable target)
    {
        Refresh(target);
    }

    public void Refresh(IInteractable target)
    {
        if (!IsTargetAlive(target))
        {
            Hide();
            return;
        }

        currentTarget = target;
        currentAnchor = ResolveAnchor(target);

        if (currentAnchor == null)
        {
            Hide();
            return;
        }

        ApplyVisuals(target);
        UpdatePosition();
        SetVisible(ShouldShow(target));
    }

    public void Hide()
    {
        currentTarget = null;
        currentAnchor = null;
        SetVisible(false);
    }

    public void SetDefaultIcon(Sprite icon)
    {
        defaultIcon = icon;

        if (currentTarget != null)
        {
            ApplyVisuals(currentTarget);
            return;
        }

        ApplyIcon(defaultIcon);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Hide();
        SyncCanvasCamera();
    }

    private bool ShouldShow(IInteractable target)
    {
        if (target == null)
            return false;

        if (!hideWhenDescriptionEmpty)
            return true;

        return !string.IsNullOrWhiteSpace(target.GetInteractDescription());
    }

    private void ApplyVisuals(IInteractable target)
    {
        InputBindingService bindingService = InputBindingService.EnsureInstance();
        string description = target != null ? target.GetInteractDescription() : string.Empty;
        InputGlyphPresentation glyph = bindingService.GetBindingGlyph(PromptAction);
        Sprite icon = InputGlyphVisualUtility.ResolveIcon(glyph, fallbackIcon: defaultIcon);
        string bindingLabel = InputGlyphVisualUtility.ResolveLabel(glyph);

        if (prependBindingLabelWhenIconMissing && icon == null)
        {
            if (!string.IsNullOrWhiteSpace(bindingLabel))
                description = string.IsNullOrWhiteSpace(description) ? bindingLabel : $"{bindingLabel}  {description}";
        }

        SetDescription(description);
        ApplyIcon(icon);
    }

    private void SetDescription(string description)
    {
        if (runtimeDescriptionText != null)
        {
            runtimeDescriptionText.text = description;
            return;
        }

        if (descriptionText != null)
            descriptionText.text = description;
    }

    private void ApplyIcon(Sprite icon)
    {
        if (runtimePromptIconImage != null)
        {
            runtimePromptIconImage.sprite = icon;
            runtimePromptIconImage.enabled = icon != null;
            runtimePromptIconImage.gameObject.SetActive(icon != null);

            if (runtimeIconLayoutElement != null)
            {
                if (icon == null)
                {
                    runtimeIconLayoutElement.preferredWidth = 0f;
                    runtimeIconLayoutElement.preferredHeight = 0f;
                }
                else
                {
                    float aspect = icon.rect.height > 0f ? icon.rect.width / icon.rect.height : 1f;
                    runtimeIconLayoutElement.preferredHeight = glyphHeight;
                    runtimeIconLayoutElement.preferredWidth = Mathf.Max(minGlyphWidth, glyphHeight * aspect);
                }
            }
        }

        if (promptIconImage != null)
        {
            promptIconImage.sprite = icon;
            promptIconImage.enabled = icon != null;
        }

        if (promptIconSpriteRenderer != null)
        {
            promptIconSpriteRenderer.sprite = icon;
            promptIconSpriteRenderer.enabled = icon != null && !useWorldSpaceCanvasLayout;
        }
    }

    private void UpdatePosition()
    {
        if (promptRoot == null || currentAnchor == null)
            return;

        promptRoot.position = currentAnchor.position + worldOffset;
    }

    private void SetVisible(bool visible)
    {
        if (promptRoot != null)
            promptRoot.gameObject.SetActive(visible);
    }

    private void EnsurePromptLayout()
    {
        if (!useWorldSpaceCanvasLayout || promptRoot == null)
            return;

        if (runtimeCanvas != null && runtimeDescriptionText != null && runtimePromptIconImage != null)
            return;

        Transform canvasTransform = promptRoot.name == RuntimeCanvasName
            ? promptRoot
            : promptRoot.Find(RuntimeCanvasName);

        if (canvasTransform == null)
            return;

        runtimeCanvasRect = canvasTransform as RectTransform;
        runtimeCanvas = canvasTransform.GetComponent<Canvas>();
        runtimePromptIconImage = promptIconImage;
        runtimeIconLayoutElement = promptIconImage != null ? promptIconImage.GetComponent<LayoutElement>() : null;
        runtimeDescriptionText = descriptionText as TextMeshProUGUI;
        runtimeContentRect = runtimeDescriptionText != null ? runtimeDescriptionText.transform.parent as RectTransform : null;

        if (runtimeCanvas == null || runtimeCanvasRect == null || runtimePromptIconImage == null || runtimeDescriptionText == null)
        {
            runtimeCanvas = null;
            runtimeCanvasRect = null;
            runtimePromptIconImage = null;
            runtimeIconLayoutElement = null;
            runtimeDescriptionText = null;
            runtimeContentRect = null;
            return;
        }

        DisableLegacyVisuals();
        SyncCanvasCamera();
    }

    private void DisableLegacyVisuals()
    {
        if (promptIconSpriteRenderer != null)
            promptIconSpriteRenderer.enabled = false;

        if (descriptionText != null && runtimeDescriptionText == null)
        {
            descriptionText.enabled = false;

            Renderer descriptionRenderer = descriptionText.GetComponent<Renderer>();
            if (descriptionRenderer != null)
                descriptionRenderer.enabled = false;
        }
    }

    private void SyncCanvasCamera()
    {
        if (runtimeCanvas == null)
            return;

        Camera worldCamera = Camera.main;
        if (worldCamera != null && runtimeCanvas.worldCamera != worldCamera)
            runtimeCanvas.worldCamera = worldCamera;
    }

    private static Transform ResolveAnchor(IInteractable target)
    {
        if (target == null)
            return null;

        Transform anchor = target.GetPromptAnchor();
        if (anchor != null)
            return anchor;

        if (target is MonoBehaviour behaviour)
            return behaviour.transform;

        return null;
    }

    private static bool IsTargetAlive(IInteractable target)
    {
        if (target == null)
            return false;

        if (target is MonoBehaviour behaviour)
            return behaviour != null;

        return true;
    }
}
