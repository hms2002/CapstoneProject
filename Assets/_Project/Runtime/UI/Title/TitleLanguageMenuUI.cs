using UnityEngine;
using UnityEngine.UI;

/// <summary>Projects the existing settings language into an authored title menu.</summary>
[DisallowMultipleComponent]
public sealed class TitleLanguageMenuUI : MonoBehaviour
{
    [SerializeField] private Button toggleButton;
    [SerializeField] private GameObject dropdownRoot;
    [SerializeField] private Button dismissButton;
    [SerializeField] private Button[] languageButtons;
    [SerializeField] private GameObject[] selectionMarks;

    private static readonly GameLanguageOption[] Languages =
    {
        GameLanguageOption.Korean, GameLanguageOption.English, GameLanguageOption.Japanese,
        GameLanguageOption.SimplifiedChinese, GameLanguageOption.TraditionalChinese
    };

    private ButtonNavigationGroup navigation;
    private bool available;

    public Button ToggleButton => toggleButton;
    public bool IsOpen => dropdownRoot != null && dropdownRoot.activeSelf;

    private void Awake()
    {
        toggleButton.onClick.AddListener(Toggle);
        dismissButton.onClick.AddListener(Dismiss);
        for (int i = 0; i < languageButtons.Length; i++)
        {
            int index = i;
            languageButtons[i].onClick.AddListener(() => SelectLanguage(index));
        }
        navigation = new ButtonNavigationGroup(false, languageButtons);
        dropdownRoot.SetActive(false);
    }

    private void OnDisable()
    {
        Close();
    }

    // Driven by TitleMenuController so the main menu and language list cannot
    // both claim keyboard focus, including during intro/loading/panel transitions.
    public void RefreshInput(bool inputAllowed)
    {
        available = inputAllowed;
        toggleButton.interactable = inputAllowed;
        toggleButton.gameObject.SetActive(inputAllowed);
        if (!inputAllowed) Close();
        navigation?.Refresh(inputAllowed && IsOpen);
    }

    public bool TryHandleCloseRequest()
    {
        if (!IsOpen) return false;
        Close();
        return true;
    }

    private void Toggle()
    {
        if (!available) return;
        if (IsOpen) { Close(); return; }
        var settings = GameSettingsService.Instance;
        if (settings == null) return;
        dropdownRoot.SetActive(true);
        int selected = 0;
        for (int i = 0; i < Languages.Length; i++)
        {
            bool current = Languages[i] == settings.CurrentLanguage;
            selectionMarks[i].SetActive(current);
            if (current) selected = i;
        }
        navigation.SetEntryControl(languageButtons[selected]);
    }

    private void SelectLanguage(int index)
    {
        if (!available || !IsOpen) return;
        GameSettingsService.Instance?.SetLanguage(Languages[index]);
        Close();
    }

    private void Dismiss() => Close();

    private void Close()
    {
        if (!IsOpen) return;
        navigation?.Reset();
        dropdownRoot.SetActive(false);
    }
}
