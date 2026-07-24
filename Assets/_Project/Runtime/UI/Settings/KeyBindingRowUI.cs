using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class KeyBindingRowUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private Button primaryBindingButton;
    [SerializeField] private TMP_Text primaryBindingLabel;
    [SerializeField] private Image primaryBindingIcon;
    [SerializeField] private GameObject secondaryRoot;
    [SerializeField] private Button secondaryBindingButton;
    [SerializeField] private TMP_Text secondaryBindingLabel;
    [SerializeField] private Image secondaryBindingIcon;
    [SerializeField] private Button resetButton;

    private KeyBindingPanelUI owner;
    private InputActionId action;
    private bool listenersBound;

    public InputActionId Action => action;

    public void Bind(KeyBindingPanelUI panel, InputActionId boundAction)
    {
        owner = panel;
        action = boundAction;
        BindListeners();
        Refresh();
    }

    public void Refresh()
    {
        if (owner == null)
            return;

        bool supportsSecondary = owner.SupportsSecondaryBinding(action);

        if (actionLabel != null)
            actionLabel.text = owner.GetActionLabel(action);

        ApplyBindingVisual(primaryBindingLabel, primaryBindingIcon, action, false);

        if (primaryBindingButton != null)
            primaryBindingButton.interactable = true;

        if (secondaryRoot != null)
            secondaryRoot.SetActive(supportsSecondary);

        if (secondaryBindingButton != null)
            secondaryBindingButton.interactable = supportsSecondary;

        if (supportsSecondary)
            ApplyBindingVisual(secondaryBindingLabel, secondaryBindingIcon, action, true);
        else
            InputGlyphVisualUtility.ApplyRaw(secondaryBindingLabel, secondaryBindingIcon, string.Empty, null);

        if (resetButton != null)
            resetButton.interactable = owner.CanResetAction(action);
    }

    public void SetListeningState(bool primary, bool secondary)
    {
        if (primary)
            InputGlyphVisualUtility.ApplyRaw(primaryBindingLabel, primaryBindingIcon, "Input...", null);

        if (secondary)
            InputGlyphVisualUtility.ApplyRaw(secondaryBindingLabel, secondaryBindingIcon, "Input...", null);

        if (primaryBindingButton != null)
            primaryBindingButton.interactable = false;

        if (secondaryBindingButton != null)
            secondaryBindingButton.interactable = false;

        if (resetButton != null)
            resetButton.interactable = false;
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;

        if (primaryBindingButton != null)
            primaryBindingButton.onClick.AddListener(HandlePrimaryRebind);

        if (secondaryBindingButton != null)
            secondaryBindingButton.onClick.AddListener(HandleSecondaryRebind);

        if (resetButton != null)
            resetButton.onClick.AddListener(HandleReset);

        listenersBound = true;
    }

    private void HandlePrimaryRebind()
    {
        owner?.BeginRebind(this, action, false);
    }

    private void HandleSecondaryRebind()
    {
        owner?.BeginRebind(this, action, true);
    }

    private void HandleReset()
    {
        owner?.ResetAction(action);
    }

    private void ApplyBindingVisual(TMP_Text label, Image icon, InputActionId boundAction, bool secondary)
    {
        if (owner == null)
            return;

        InputGlyphVisualUtility.ApplyRaw(
            label,
            icon,
            owner.GetBindingDisplayLabel(boundAction, secondary),
            owner.GetBindingIcon(boundAction, secondary));
    }
}
