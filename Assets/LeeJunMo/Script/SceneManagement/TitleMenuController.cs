using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TitleMenuController : MonoBehaviour
{
    [Header("Main Menu")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private CanvasGroup mainMenuCanvasGroup;
    [SerializeField] private UIChainDropPresentation mainMenuPresentation;

    [Header("Panels")]
    [SerializeField] private TitleProfileSlotPanelUI profileSlotPanel;
    [SerializeField] private SettingsPanelUI settingsPanel;

    [Header("Flow")]
    [SerializeField] private bool openMainMenuOnStart = true;
    [SerializeField] private bool lockMainMenuWhileSlotPanelOpen = true;

    private bool listenersBound;
    private bool isLoading;
    private TitleProfileSlotPanelMode currentSlotMode = TitleProfileSlotPanelMode.Continue;

    private void Awake()
    {
        ResolveReferences();
        BindListeners();
        TitleProfileSlotService.EnsureInstance();

        if (profileSlotPanel != null)
            profileSlotPanel.gameObject.SetActive(false);
    }

    private void Start()
    {
        RefreshMainMenuState();

        if (openMainMenuOnStart)
            mainMenuPresentation?.PlayOpen();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape) || isLoading)
            return;

        if (profileSlotPanel != null && profileSlotPanel.TryHandleCloseRequest())
            return;

        if (settingsPanel != null && settingsPanel.IsActive)
            settingsPanel.CloseUI();
    }

    private void ResolveReferences()
    {
        if (mainMenuCanvasGroup == null)
            mainMenuCanvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (mainMenuPresentation == null)
            mainMenuPresentation = GetComponentInChildren<UIChainDropPresentation>(true);
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;

        if (newGameButton != null)
            newGameButton.onClick.AddListener(HandleNewGamePressed);

        if (continueButton != null)
            continueButton.onClick.AddListener(HandleContinuePressed);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(HandleSettingsPressed);

        if (quitButton != null)
            quitButton.onClick.AddListener(HandleQuitPressed);

        listenersBound = true;
    }

    private void HandleNewGamePressed()
    {
        OpenSlotPanel(TitleProfileSlotPanelMode.NewGame);
    }

    private void HandleContinuePressed()
    {
        OpenSlotPanel(TitleProfileSlotPanelMode.Continue);
    }

    private void HandleSettingsPressed()
    {
        settingsPanel?.OpenUI();
    }

    private void HandleQuitPressed()
    {
        Application.Quit();
    }

    private void OpenSlotPanel(TitleProfileSlotPanelMode mode)
    {
        if (profileSlotPanel == null)
            return;

        currentSlotMode = mode;
        SetMainMenuInteractable(!lockMainMenuWhileSlotPanelOpen);
        profileSlotPanel.Open(mode, HandleSlotSelected, HandleSlotPanelClosed);
    }

    private void HandleSlotPanelClosed()
    {
        SetMainMenuInteractable(true);
        RefreshMainMenuState();
    }

    private void HandleSlotSelected(int slotIndex)
    {
        TitleProfileSlotService service = TitleProfileSlotService.EnsureInstance();
        if (service == null)
            return;

        if (currentSlotMode == TitleProfileSlotPanelMode.NewGame &&
            service.NeedsOverwriteConfirmationForNewGame(slotIndex))
        {
            Debug.Log($"[TitleMenuController] Slot {slotIndex + 1} already has an active run. Add overwrite confirmation UI here.", this);
            return;
        }

        if (!service.TryCreateLaunchRequest(currentSlotMode, slotIndex, out TitleProfileLaunchRequest request))
            return;

        TitleProfileLaunchContext.SetPendingRequest(request);
        LoadScene(request.TargetSceneName);
    }

    private void LoadScene(string targetSceneName)
    {
        if (isLoading || string.IsNullOrWhiteSpace(targetSceneName))
            return;

        isLoading = true;

        SceneFadeTransitionService transitionService = SceneFadeTransitionService.EnsureInstance();
        if (transitionService != null && transitionService.TryLoadScene(targetSceneName))
            return;

        SceneManager.LoadScene(targetSceneName);
    }

    private void RefreshMainMenuState()
    {
        TitleProfileSlotService service = TitleProfileSlotService.EnsureInstance();
        if (continueButton != null && service != null)
            continueButton.interactable = service.HasAnyContinuableRun();
    }

    private void SetMainMenuInteractable(bool enabled)
    {
        if (mainMenuCanvasGroup == null)
            return;

        mainMenuCanvasGroup.interactable = enabled;
        mainMenuCanvasGroup.blocksRaycasts = enabled;
    }
}
