using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WorldInteractionPromptController : MonoBehaviour
{
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

    private IInteractable currentTarget;
    private Transform currentAnchor;

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
        ApplyIcon(defaultIcon);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Hide();
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
        string description = target != null ? target.GetInteractDescription() : string.Empty;

        if (descriptionText != null)
            descriptionText.text = description;

        ApplyIcon(defaultIcon);
    }

    private void ApplyIcon(Sprite icon)
    {
        if (promptIconImage != null)
        {
            promptIconImage.sprite = icon;
            promptIconImage.enabled = icon != null;
        }

        if (promptIconSpriteRenderer != null)
        {
            promptIconSpriteRenderer.sprite = icon;
            promptIconSpriteRenderer.enabled = icon != null;
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

    private static Transform ResolveAnchor(IInteractable target)
    {
        if (target == null)
            return null;

        Transform anchor = target.GetPromptAnchor();
        if (anchor != null)
            return anchor;

        if (target is MonoBehaviour mb)
            return mb.transform;

        return null;
    }

    private static bool IsTargetAlive(IInteractable target)
    {
        if (target == null)
            return false;

        if (target is MonoBehaviour mb)
            return mb != null;

        return true;
    }

}
