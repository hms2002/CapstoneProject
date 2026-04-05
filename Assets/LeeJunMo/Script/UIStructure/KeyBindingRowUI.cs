using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class KeyBindingRowUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private Button primaryBindingButton;
    [SerializeField] private TMP_Text primaryBindingLabel;
    [SerializeField] private GameObject secondaryRoot;
    [SerializeField] private Button secondaryBindingButton;
    [SerializeField] private TMP_Text secondaryBindingLabel;
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

        if (primaryBindingLabel != null)
            primaryBindingLabel.text = owner.GetBindingDisplayLabel(action);

        if (primaryBindingButton != null)
            primaryBindingButton.interactable = true;

        if (secondaryRoot != null)
            secondaryRoot.SetActive(supportsSecondary);

        if (secondaryBindingButton != null)
            secondaryBindingButton.interactable = supportsSecondary;

        if (secondaryBindingLabel != null)
            secondaryBindingLabel.text = supportsSecondary
                ? owner.GetBindingDisplayLabel(action, secondary: true)
                : string.Empty;

        if (resetButton != null)
            resetButton.interactable = owner.CanResetAction(action);
    }

    public void SetListeningState(bool primary, bool secondary)
    {
        if (primaryBindingLabel != null && primary)
            primaryBindingLabel.text = "입력...";

        if (secondaryBindingLabel != null && secondary)
            secondaryBindingLabel.text = "입력...";

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
        owner?.BeginRebind(this, action, secondary: false);
    }

    private void HandleSecondaryRebind()
    {
        owner?.BeginRebind(this, action, secondary: true);
    }

    private void HandleReset()
    {
        owner?.ResetAction(action);
    }
}
