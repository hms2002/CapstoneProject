using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EncyclopediaEntryButton : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text indexText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject selectedMarker;
    [SerializeField] private GameObject hoverMarker;
    [SerializeField] private GameObject lockedMarker;
    [SerializeField] private Animator animator;

    private EncyclopediaCategory category;
    private int entryIndex = -1;
    private Action<EncyclopediaCategory, int> onSelected;
    private ItemDisplayIconDefaultState iconDefaultState;
    private bool selected;
    private bool locked;
    private bool hovered;
    private bool pressed;

    private static readonly int HoveredHash = Animator.StringToHash("Hovered");
    private static readonly int PressedHash = Animator.StringToHash("Pressed");
    private static readonly int SelectedHash = Animator.StringToHash("Selected");
    private static readonly int LockedHash = Animator.StringToHash("Locked");
    private static readonly int IdleStateHash = Animator.StringToHash("Idle");
    private static readonly int HoveredStateHash = Animator.StringToHash("Hovered");
    private static readonly int PressedStateHash = Animator.StringToHash("Pressed");
    private static readonly int SelectedStateHash = Animator.StringToHash("Selected");
    private static readonly int LockedStateHash = Animator.StringToHash("Locked");
    private static readonly int LayerIdleStateHash = Animator.StringToHash("Base Layer.Idle");
    private static readonly int LayerHoveredStateHash = Animator.StringToHash("Base Layer.Hovered");
    private static readonly int LayerPressedStateHash = Animator.StringToHash("Base Layer.Pressed");
    private static readonly int LayerSelectedStateHash = Animator.StringToHash("Base Layer.Selected");
    private static readonly int LayerLockedStateHash = Animator.StringToHash("Base Layer.Locked");

    public int EntryIndex => entryIndex;

    private void Awake()
    {
        ResolveReferences();
        CaptureIconDefaultState();
        if (button != null)
            button.onClick.AddListener(HandleClick);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveReferences();
        CaptureIconDefaultState();
    }

    [ContextMenu("Auto Wire References")]
    private void AutoWireReferences()
    {
        ResolveReferences();
        CaptureIconDefaultState();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private void OnDisable()
    {
        SetItemSlotCursorInteractable(false);
        hovered = false;
        pressed = false;
        RefreshPresentation();
    }

    private void OnDestroy()
    {
        SetItemSlotCursorInteractable(false);
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    public void Configure(
        EncyclopediaCategory newCategory,
        int newIndex,
        string displayName,
        ScriptableObject iconItem,
        Sprite fallbackIcon,
        bool selected,
        bool locked,
        Action<EncyclopediaCategory, int> selectedCallback)
    {
        ResolveReferences();
        CaptureIconDefaultState();

        category = newCategory;
        entryIndex = newIndex;
        onSelected = selectedCallback;

        SetText(indexText, string.Empty);
        SetText(titleText, string.Empty);
        SetTextObjectActive(indexText, false);
        SetTextObjectActive(titleText, false);
        SetIcon(iconItem, fallbackIcon);
        SetSelected(selected);
        SetLocked(locked);
    }

    public void Clear()
    {
        SetItemSlotCursorInteractable(false);
        entryIndex = -1;
        onSelected = null;
        hovered = false;
        pressed = false;
        SetText(indexText, string.Empty);
        SetText(titleText, string.Empty);
        SetIcon(null, null);
        SetSelected(false);
        SetLocked(false);
    }

    public void SetSelected(bool selected)
    {
        this.selected = selected;
        RefreshPresentation();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsCursorItemSlot())
        {
            SetItemSlotCursorInteractable(false);
            return;
        }

        hovered = true;
        SetItemSlotCursorInteractable(true);
        RefreshPresentation();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetItemSlotCursorInteractable(false);
        hovered = false;
        pressed = false;
        RefreshPresentation();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (locked || entryIndex < 0)
            return;

        pressed = true;
        RefreshPresentation();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pressed)
            return;

        pressed = false;
        RefreshPresentation();
    }

    private void RefreshPresentation()
    {
        if (selectedMarker != null)
            selectedMarker.SetActive(selected);

        if (hoverMarker != null)
            hoverMarker.SetActive(hovered && !selected && !locked);

        if (lockedMarker != null)
            lockedMarker.SetActive(locked);

        RefreshAnimatorParameters();
    }

    private void SetLocked(bool locked)
    {
        this.locked = locked;

        if (button != null)
            button.interactable = !locked;

        if (locked)
            SetItemSlotCursorInteractable(false);

        RefreshPresentation();
    }

    private void SetIcon(ScriptableObject iconItem, Sprite fallbackIcon)
    {
        if (iconImage == null)
            return;

        if (iconItem != null)
            ItemDisplayIconUtility.Apply(iconImage, iconItem, ItemDisplayIconContext.InventorySlot, iconDefaultState);
        else
            ItemDisplayIconUtility.ApplyRaw(iconImage, fallbackIcon, iconDefaultState);
    }

    private void HandleClick()
    {
        if (entryIndex < 0 || locked)
            return;

        onSelected?.Invoke(category, entryIndex);
    }

    private bool IsCursorItemSlot()
    {
        return entryIndex >= 0 &&
               !locked &&
               (category == EncyclopediaCategory.Weapon ||
                category == EncyclopediaCategory.Relic ||
                category == EncyclopediaCategory.Consumable);
    }

    private void SetItemSlotCursorInteractable(bool active)
    {
        if (active && IsCursorItemSlot())
        {
            MouseCursorService.EnsureInstance().SetInteractable(this, true);
            return;
        }

        MouseCursorService.Instance?.SetInteractable(this, false);
    }

    private void ResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (indexText == null)
            indexText = FindChildComponentByName<TMP_Text>("IndexText");

        if (titleText == null)
            titleText = FindChildComponentByName<TMP_Text>("TitleText") ?? GetComponentInChildren<TMP_Text>(true);

        if (iconImage == null)
            iconImage = FindChildComponentByName<Image>("Icon");

        if (hoverMarker == null)
            hoverMarker = FindChildGameObjectByName("HoverMarker");

        if (selectedMarker == null)
            selectedMarker = FindChildGameObjectByName("SelectedMarker", "SelectMarker", "SelectionMarker");

        if (lockedMarker == null)
            lockedMarker = FindChildGameObjectByName("LockedMarker");

        if (animator == null)
            animator = GetComponent<Animator>();

        if (iconImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image != null && image.transform != transform && IsLikelyIconImage(image))
                {
                    iconImage = image;
                    break;
                }
            }
        }
    }

    private void CaptureIconDefaultState()
    {
        if (iconImage != null)
            iconDefaultState = ItemDisplayIconDefaultState.Stretch(iconImage, preserveAspect: true);
    }

    private GameObject FindChildGameObjectByName(params string[] childNames)
    {
        if (childNames == null || childNames.Length == 0)
            return null;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null)
                continue;

            for (int j = 0; j < childNames.Length; j++)
            {
                string childName = childNames[j];
                if (!string.IsNullOrWhiteSpace(childName) &&
                    string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                {
                    return child.gameObject;
                }
            }
        }

        return null;
    }

    private void RefreshAnimatorParameters()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        SetAnimatorBool(HoveredHash, hovered);
        SetAnimatorBool(PressedHash, pressed);
        SetAnimatorBool(SelectedHash, selected);
        SetAnimatorBool(LockedHash, locked);
        ResolveAnimatorStateHash(out int stateHash, out int layerStateHash);
        PlayAnimatorState(stateHash, layerStateHash);
    }

    private void ResolveAnimatorStateHash(out int stateHash, out int layerStateHash)
    {
        if (locked)
        {
            stateHash = LockedStateHash;
            layerStateHash = LayerLockedStateHash;
            return;
        }

        if (pressed)
        {
            stateHash = PressedStateHash;
            layerStateHash = LayerPressedStateHash;
            return;
        }

        if (selected)
        {
            stateHash = SelectedStateHash;
            layerStateHash = LayerSelectedStateHash;
            return;
        }

        if (hovered)
        {
            stateHash = HoveredStateHash;
            layerStateHash = LayerHoveredStateHash;
            return;
        }

        stateHash = IdleStateHash;
        layerStateHash = LayerIdleStateHash;
    }

    private void SetAnimatorBool(int parameterHash, bool value)
    {
        if (HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Bool))
            animator.SetBool(parameterHash, value);
    }

    private void PlayAnimatorState(int stateHash, int layerStateHash)
    {
        if (animator.HasState(0, stateHash))
        {
            animator.Play(stateHash, 0, 0f);
            return;
        }

        if (animator.HasState(0, layerStateHash))
            animator.Play(layerStateHash, 0, 0f);
    }

    private bool HasAnimatorParameter(int parameterHash, AnimatorControllerParameterType type)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == parameterHash && parameter.type == type)
                return true;
        }

        return false;
    }

    private T FindChildComponentByName<T>(string childName) where T : Component
    {
        if (string.IsNullOrWhiteSpace(childName))
            return null;

        T[] components = GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component != null && component.name == childName)
                return component;
        }

        return null;
    }

    private static bool IsLikelyIconImage(Image image)
    {
        string imageName = image.name;
        if (string.IsNullOrWhiteSpace(imageName))
            return false;

        return imageName.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value ?? string.Empty;
    }

    private static void SetTextObjectActive(TMP_Text text, bool active)
    {
        if (text != null)
            text.gameObject.SetActive(active);
    }
}
