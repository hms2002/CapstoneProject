using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public sealed class SettingStepperControl
{
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text valueText;

    public Button PreviousButton => previousButton;
    public Button NextButton => nextButton;
    public TMP_Text ValueText => valueText;
}

public sealed class SettingsPanelUI : MonoBehaviour, IStackableUI
{
    private const float DisabledStepperAlpha = 0.45f;
    private const float EnabledStepperAlpha = 1f;
    private const int SystemCursorPriority = 300;

    private static readonly GameWindowMode[] WindowModeOptions =
    {
        GameWindowMode.Windowed,
        GameWindowMode.Borderless,
        GameWindowMode.Fullscreen,
    };

    private static readonly UiScalePreset[] UiScaleOptions =
    {
        UiScalePreset.Small,
        UiScalePreset.Medium,
        UiScalePreset.Large,
    };

    private static readonly GameLanguageOption[] LanguageOptions =
    {
        GameLanguageOption.Korean,
    };

    public static SettingsPanelUI Instance { get; private set; }

    [Header("Display")]
    [SerializeField] private SettingStepperControl windowModeStepper;
    [SerializeField] private SettingStepperControl resolutionStepper;
    [SerializeField] private Button applyDisplayButton;

    [Header("Gameplay")]
    [SerializeField] private SettingStepperControl screenShakeStepper;

    [Header("Audio")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Text masterValueText;
    [SerializeField] private TMP_Text musicValueText;
    [SerializeField] private TMP_Text sfxValueText;

    [Header("UI")]
    [SerializeField] private SettingStepperControl uiScaleStepper;

    [Header("Controls")]
    [SerializeField] private Button keyMappingButton;
    [SerializeField] private UnityEvent onRequestOpenKeyMappingPanel;

    [Header("Language")]
    [SerializeField] private SettingStepperControl languageStepper;

    [Header("Common")]
    [SerializeField] private Button closeButton;
    [SerializeField] private UIChainDropPresentation dropPresentation;
    [SerializeField] private CanvasGroup temporaryHiddenCanvasGroup;

    private bool listenersBound;
    private bool isClosing;
    private bool suppressCallbacks;
    private bool hasStoredTemporaryCanvasState;
    private bool storedCanvasInteractable = true;
    private bool storedCanvasBlocksRaycasts = true;
    private int pendingWindowModeIndex;
    private int pendingResolutionIndex;

    public bool IsActive => gameObject.activeSelf && !isClosing;
    public bool CanCloseOnEscape => true;
    public UIOpenGroup OpenGroup => UIOpenGroup.Overlay;
    public UIOpenGroup BlockedOpenGroups => UIOpenGroup.None;
    public UIGameplayLockProfile GameplayLockProfile => UIGameplayLockProfile.FreezeAndBlockControl;

    public static SettingsPanelUI EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        SettingsPanelUI[] existing = Resources.FindObjectsOfTypeAll<SettingsPanelUI>();
        for (int i = 0; i < existing.Length; i++)
        {
            SettingsPanelUI candidate = existing[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid())
                continue;

            Instance = candidate;
            candidate.RefreshCanvasParent();
            return candidate;
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
        ResolveReferences();
        BindListeners();
        RefreshCanvasParent();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        MouseCursorService.Instance?.ClearDomain(this);

        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        MouseCursorService.EnsureInstance().SetDomain(this, MouseCursorDomain.SystemUi, priority: SystemCursorPriority);
    }

    private void OnDisable()
    {
        ApplyTemporaryHiddenState(false);
        isClosing = false;
        MouseCursorService.Instance?.ClearDomain(this);
    }

    public void OpenUI()
    {
        isClosing = false;
        ResolveReferences();
        ApplyTemporaryHiddenState(false);
        RefreshCanvasParent();
        BindListeners();
        RefreshBindings();
        gameObject.SetActive(true);
        dropPresentation?.PlayOpen();
    }

    public void CloseUI()
    {
        if (isClosing)
            return;

        if (!gameObject.activeSelf)
        {
            isClosing = false;
            return;
        }

        if (dropPresentation == null)
        {
            gameObject.SetActive(false);
            isClosing = false;
            return;
        }

        isClosing = true;
        dropPresentation.PlayClose(FinalizeClose);
    }

    public void RefreshCanvasParent()
    {
        GlobalUIRoot.AdoptToCanvas(GlobalCanvasLayer.Popup, transform, false);
    }

    public void SetTemporarilyHidden(bool hidden)
    {
        ResolveReferences();

        if (hidden)
        {
            isClosing = false;
            dropPresentation?.SnapOpen();
            ApplyTemporaryHiddenState(true);
            return;
        }

        isClosing = false;
        ApplyTemporaryHiddenState(false);
        RefreshBindings();
    }

    private void FinalizeClose()
    {
        ApplyTemporaryHiddenState(false);
        gameObject.SetActive(false);
        isClosing = false;
    }

    private void ResolveReferences()
    {
        if (dropPresentation == null)
            dropPresentation = GetComponent<UIChainDropPresentation>();

        if (dropPresentation == null)
            dropPresentation = GetComponentInChildren<UIChainDropPresentation>(true);

        if (temporaryHiddenCanvasGroup == null && dropPresentation != null)
            temporaryHiddenCanvasGroup = dropPresentation.GetComponent<CanvasGroup>();

        if (temporaryHiddenCanvasGroup == null)
            temporaryHiddenCanvasGroup = GetComponentInChildren<CanvasGroup>(true);
    }

    private void ApplyTemporaryHiddenState(bool hidden)
    {
        if (temporaryHiddenCanvasGroup == null)
            return;

        if (hidden)
        {
            if (!hasStoredTemporaryCanvasState)
            {
                storedCanvasInteractable = temporaryHiddenCanvasGroup.interactable;
                storedCanvasBlocksRaycasts = temporaryHiddenCanvasGroup.blocksRaycasts;
                hasStoredTemporaryCanvasState = true;
            }

            temporaryHiddenCanvasGroup.interactable = false;
            temporaryHiddenCanvasGroup.blocksRaycasts = false;
            return;
        }

        if (!hasStoredTemporaryCanvasState)
            return;

        temporaryHiddenCanvasGroup.interactable = storedCanvasInteractable;
        temporaryHiddenCanvasGroup.blocksRaycasts = storedCanvasBlocksRaycasts;
        hasStoredTemporaryCanvasState = false;
    }

    public void RefreshBindings()
    {
        GameSettingsService settings = GameSettingsService.EnsureInstance();
        if (settings == null)
            return;

        suppressCallbacks = true;

        pendingWindowModeIndex = GetWindowModeOptionIndex(settings.CurrentWindowMode);
        pendingResolutionIndex = Mathf.Max(0, settings.GetCurrentResolutionIndex());

        RefreshDisplayStepperState(settings);
        RefreshScreenShakeStepper(settings);
        RefreshUiScaleStepper(settings);
        RefreshLanguageStepper(settings);

        if (masterSlider != null)
            masterSlider.SetValueWithoutNotify(settings.GetMasterVolume());

        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(settings.GetMusicVolume());

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(settings.GetSfxVolume());

        UpdateValueLabel(masterValueText, masterSlider != null ? masterSlider.value : 0f);
        UpdateValueLabel(musicValueText, musicSlider != null ? musicSlider.value : 0f);
        UpdateValueLabel(sfxValueText, sfxSlider != null ? sfxSlider.value : 0f);
        UpdateApplyDisplayButton(settings);

        suppressCallbacks = false;
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;

        BindStepperButton(windowModeStepper?.PreviousButton, HandlePreviousWindowMode);
        BindStepperButton(windowModeStepper?.NextButton, HandleNextWindowMode);
        BindStepperButton(resolutionStepper?.PreviousButton, HandlePreviousResolution);
        BindStepperButton(resolutionStepper?.NextButton, HandleNextResolution);
        BindStepperButton(screenShakeStepper?.PreviousButton, HandlePreviousScreenShake);
        BindStepperButton(screenShakeStepper?.NextButton, HandleNextScreenShake);
        BindStepperButton(uiScaleStepper?.PreviousButton, HandlePreviousUiScale);
        BindStepperButton(uiScaleStepper?.NextButton, HandleNextUiScale);
        BindStepperButton(languageStepper?.PreviousButton, HandlePreviousLanguage);
        BindStepperButton(languageStepper?.NextButton, HandleNextLanguage);

        if (applyDisplayButton != null)
            applyDisplayButton.onClick.AddListener(HandleApplyDisplay);

        if (masterSlider != null)
            masterSlider.onValueChanged.AddListener(HandleMasterSliderChanged);

        if (musicSlider != null)
            musicSlider.onValueChanged.AddListener(HandleMusicSliderChanged);

        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(HandleSfxSliderChanged);

        if (keyMappingButton != null)
            keyMappingButton.onClick.AddListener(HandleOpenKeyMapping);

        if (closeButton != null)
            closeButton.onClick.AddListener(RequestClose);

        listenersBound = true;
    }

    private void RefreshDisplayStepperState(GameSettingsService settings)
    {
        GameWindowMode pendingMode = WindowModeOptions[pendingWindowModeIndex];
        SetStepperState(
            windowModeStepper,
            settings.GetWindowModeLabel(pendingMode),
            pendingWindowModeIndex > 0,
            pendingWindowModeIndex < WindowModeOptions.Length - 1);

        IReadOnlyList<DisplayResolutionOption> options = settings.GetResolutionOptions();
        bool canEditResolution = CanEditResolutionForPendingMode();
        DisplayResolutionOption displayedResolution = settings.GetDisplayedResolutionOption(pendingMode, pendingResolutionIndex);
        string resolutionLabel = displayedResolution.width > 0 && displayedResolution.height > 0
            ? displayedResolution.ToString()
            : options.Count > 0 && pendingResolutionIndex >= 0 && pendingResolutionIndex < options.Count
                ? options[pendingResolutionIndex].ToString()
                : "-";

        SetStepperState(
            resolutionStepper,
            resolutionLabel,
            canEditResolution && options.Count > 0 && pendingResolutionIndex > 0,
            canEditResolution && options.Count > 0 && pendingResolutionIndex < options.Count - 1,
            canEditResolution);
    }

    private void RefreshScreenShakeStepper(GameSettingsService settings)
    {
        bool enabled = settings.ScreenShakeEnabled;
        int stateIndex = enabled ? 1 : 0;

        SetStepperState(
            screenShakeStepper,
            settings.GetOnOffLabel(enabled),
            stateIndex > 0,
            stateIndex < 1);
    }

    private void RefreshUiScaleStepper(GameSettingsService settings)
    {
        int index = GetUiScaleOptionIndex(settings.CurrentUiScalePreset);
        SetStepperState(
            uiScaleStepper,
            settings.GetUiScaleLabel(UiScaleOptions[index]),
            index > 0,
            index < UiScaleOptions.Length - 1);
    }

    private void RefreshLanguageStepper(GameSettingsService settings)
    {
        int index = GetLanguageOptionIndex(settings.CurrentLanguage);
        SetStepperState(
            languageStepper,
            settings.GetLanguageLabel(LanguageOptions[index]),
            index > 0,
            index < LanguageOptions.Length - 1);
    }

    private void UpdateApplyDisplayButton(GameSettingsService settings)
    {
        if (applyDisplayButton == null)
            return;

        int currentResolutionIndex = Mathf.Max(0, settings.GetCurrentResolutionIndex());
        bool windowModeChanged = WindowModeOptions[pendingWindowModeIndex] != settings.CurrentWindowMode;
        bool shouldCompareResolution = CanEditResolutionForPendingMode() || settings.CurrentWindowMode == GameWindowMode.Windowed;
        bool resolutionChanged = shouldCompareResolution && pendingResolutionIndex != currentResolutionIndex;
        bool hasChanges = windowModeChanged || resolutionChanged;
        applyDisplayButton.interactable = hasChanges;
    }

    private void HandlePreviousWindowMode()
    {
        ChangePendingWindowMode(-1);
    }

    private void HandleNextWindowMode()
    {
        ChangePendingWindowMode(1);
    }

    private void ChangePendingWindowMode(int direction)
    {
        if (suppressCallbacks)
            return;

        int nextIndex = Mathf.Clamp(pendingWindowModeIndex + direction, 0, WindowModeOptions.Length - 1);
        if (nextIndex == pendingWindowModeIndex)
            return;

        pendingWindowModeIndex = nextIndex;
        HandlePendingDisplaySelectionChanged();
    }

    private void HandlePreviousResolution()
    {
        ChangePendingResolution(-1);
    }

    private void HandleNextResolution()
    {
        ChangePendingResolution(1);
    }

    private void ChangePendingResolution(int direction)
    {
        if (suppressCallbacks)
            return;

        if (!CanEditResolutionForPendingMode())
            return;

        GameSettingsService settings = GameSettingsService.EnsureInstance();
        if (settings == null)
            return;

        IReadOnlyList<DisplayResolutionOption> options = settings.GetResolutionOptions();
        if (options.Count == 0)
            return;

        int nextIndex = Mathf.Clamp(pendingResolutionIndex + direction, 0, options.Count - 1);
        if (nextIndex == pendingResolutionIndex)
            return;

        pendingResolutionIndex = nextIndex;
        HandlePendingDisplaySelectionChanged();
    }

    private void HandlePendingDisplaySelectionChanged()
    {
        GameSettingsService settings = GameSettingsService.EnsureInstance();
        RefreshDisplayStepperState(settings);

        if (applyDisplayButton == null)
        {
            CommitPendingDisplaySelection(settings);
            return;
        }

        UpdateApplyDisplayButton(settings);
    }

    private void HandleApplyDisplay()
    {
        if (suppressCallbacks)
            return;

        CommitPendingDisplaySelection(GameSettingsService.EnsureInstance());
    }

    private void CommitPendingDisplaySelection(GameSettingsService settings)
    {
        if (settings == null)
            return;

        settings.ApplyDisplaySelection(WindowModeOptions[pendingWindowModeIndex], pendingResolutionIndex);
        RefreshBindings();
    }

    private void HandlePreviousScreenShake()
    {
        ChangeScreenShake(-1);
    }

    private void HandleNextScreenShake()
    {
        ChangeScreenShake(1);
    }

    private void ChangeScreenShake(int direction)
    {
        if (suppressCallbacks)
            return;

        GameSettingsService settings = GameSettingsService.EnsureInstance();
        int currentIndex = settings.ScreenShakeEnabled ? 1 : 0;
        int nextIndex = Mathf.Clamp(currentIndex + direction, 0, 1);
        if (nextIndex == currentIndex)
            return;

        settings.SetScreenShakeEnabled(nextIndex == 1);
        RefreshScreenShakeStepper(settings);
    }

    private void HandleMasterSliderChanged(float value)
    {
        UpdateValueLabel(masterValueText, value);
        if (!suppressCallbacks)
            GameSettingsService.EnsureInstance().SetMasterVolume(value);
    }

    private void HandleMusicSliderChanged(float value)
    {
        UpdateValueLabel(musicValueText, value);
        if (!suppressCallbacks)
            GameSettingsService.EnsureInstance().SetMusicVolume(value);
    }

    private void HandleSfxSliderChanged(float value)
    {
        UpdateValueLabel(sfxValueText, value);
        if (!suppressCallbacks)
            GameSettingsService.EnsureInstance().SetSfxVolume(value);
    }

    private void HandlePreviousUiScale()
    {
        ChangeUiScale(-1);
    }

    private void HandleNextUiScale()
    {
        ChangeUiScale(1);
    }

    private void ChangeUiScale(int direction)
    {
        if (suppressCallbacks)
            return;

        GameSettingsService settings = GameSettingsService.EnsureInstance();
        int currentIndex = GetUiScaleOptionIndex(settings.CurrentUiScalePreset);
        int nextIndex = Mathf.Clamp(currentIndex + direction, 0, UiScaleOptions.Length - 1);
        if (nextIndex == currentIndex)
            return;

        settings.SetUiScalePreset(UiScaleOptions[nextIndex]);
        RefreshUiScaleStepper(settings);
    }

    private void HandlePreviousLanguage()
    {
        ChangeLanguage(-1);
    }

    private void HandleNextLanguage()
    {
        ChangeLanguage(1);
    }

    private void ChangeLanguage(int direction)
    {
        if (suppressCallbacks)
            return;

        GameSettingsService settings = GameSettingsService.EnsureInstance();
        int currentIndex = GetLanguageOptionIndex(settings.CurrentLanguage);
        int nextIndex = Mathf.Clamp(currentIndex + direction, 0, LanguageOptions.Length - 1);
        if (nextIndex == currentIndex)
            return;

        settings.SetLanguage(LanguageOptions[nextIndex]);
        RefreshLanguageStepper(settings);
    }

    private void HandleOpenKeyMapping()
    {
        if (onRequestOpenKeyMappingPanel != null &&
            onRequestOpenKeyMappingPanel.GetPersistentEventCount() > 0)
        {
            onRequestOpenKeyMappingPanel.Invoke();
            return;
        }

        UIManager.Instance?.OpenKeyBindingPanel();
    }

    private void RequestClose()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.PopUI(this);
        else
            CloseUI();
    }

    private static void BindStepperButton(Button button, UnityAction callback)
    {
        if (button == null || callback == null)
            return;

        button.onClick.AddListener(callback);
    }

    private bool CanEditResolutionForPendingMode()
    {
        return WindowModeOptions[pendingWindowModeIndex] == GameWindowMode.Windowed;
    }

    private static void SetStepperState(SettingStepperControl stepper, string value, bool canGoPrevious, bool canGoNext, bool isVisuallyEnabled = true)
    {
        if (stepper == null)
            return;

        if (stepper.ValueText != null)
        {
            stepper.ValueText.text = value;
            SetGraphicAlpha(stepper.ValueText, isVisuallyEnabled ? EnabledStepperAlpha : DisabledStepperAlpha);
        }

        if (stepper.PreviousButton != null)
        {
            stepper.PreviousButton.interactable = canGoPrevious;
            SetButtonGraphicsAlpha(stepper.PreviousButton, isVisuallyEnabled ? EnabledStepperAlpha : DisabledStepperAlpha);
        }

        if (stepper.NextButton != null)
        {
            stepper.NextButton.interactable = canGoNext;
            SetButtonGraphicsAlpha(stepper.NextButton, isVisuallyEnabled ? EnabledStepperAlpha : DisabledStepperAlpha);
        }
    }

    private static void SetButtonGraphicsAlpha(Button button, float alpha)
    {
        if (button == null)
            return;

        Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            SetGraphicAlpha(graphics[i], alpha);
    }

    private static void SetGraphicAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
            return;

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private static void UpdateValueLabel(TMP_Text label, float value)
    {
        if (label == null)
            return;

        label.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
    }

    private static int GetWindowModeOptionIndex(GameWindowMode mode)
    {
        for (int i = 0; i < WindowModeOptions.Length; i++)
        {
            if (WindowModeOptions[i] == mode)
                return i;
        }

        return 0;
    }

    private static int GetUiScaleOptionIndex(UiScalePreset preset)
    {
        for (int i = 0; i < UiScaleOptions.Length; i++)
        {
            if (UiScaleOptions[i] == preset)
                return i;
        }

        return 0;
    }

    private static int GetLanguageOptionIndex(GameLanguageOption language)
    {
        for (int i = 0; i < LanguageOptions.Length; i++)
        {
            if (LanguageOptions[i] == language)
                return i;
        }

        return 0;
    }
}
