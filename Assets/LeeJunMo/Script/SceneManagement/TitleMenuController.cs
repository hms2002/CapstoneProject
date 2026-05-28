using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

[DisallowMultipleComponent]
public sealed class TitleMenuController : MonoBehaviour
{
    private const string DontDestroyOnLoadSceneName = "DontDestroyOnLoad";

    [Header("Main Menu")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private CanvasGroup mainMenuCanvasGroup;
    [SerializeField] private UIChainDropPresentation mainMenuPresentation;

    [Header("Panels")]
    [SerializeField] private TitleProfileSlotPanelUI profileSlotPanel;
    [SerializeField] private SettingsPanelUI settingsPanel;
    [SerializeField] private KeyBindingPanelUI keyBindingPanel;

    [Header("Intro")]
    [SerializeField] private TitleIntroPlayer introPlayer;
    [SerializeField] private bool playIntroForNewProfile = true;

    [Header("Flow")]
    [SerializeField] private bool openMainMenuOnStart = true;
    [SerializeField] private bool lockMainMenuWhileSlotPanelOpen = true;
    [SerializeField, Min(0f)] private float mainMenuInputUnlockDelay = 0.18f;

    [Header("Presentation")]
    [SerializeField] private bool adaptSceneCanvasToPresentationViewport = true;
    [SerializeField] private PresentationCanvasAdapter presentationCanvasAdapter;

    private bool listenersBound;
    private bool isLoading;
    private Coroutine mainMenuUnlockCoroutine;

    private void Awake()
    {
        SceneDomainCoordinator.EnsureInstance();
        ResolveReferences();
        EnsurePresentationCanvasAdapter();
        EnsureUiInputReady();
        BindListeners();
        TitleProfileSlotService.EnsureInstance();
    }

    private void Start()
    {
        ResolveReferences();
        EnsurePresentationCanvasAdapter();

        if (profileSlotPanel != null && profileSlotPanel.gameObject.activeSelf)
            profileSlotPanel.gameObject.SetActive(false);

        EnsureUiInputReady();
        SetMainMenuInteractable(true);

        if (openMainMenuOnStart)
        {
            mainMenuPresentation?.PlayOpen();
            StartMainMenuUnlockLead();
        }
    }

    private void OnDisable()
    {
        StopMainMenuUnlockLead();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape) || isLoading || IsIntroActive)
            return;

        ResolveReferences();

        if (profileSlotPanel != null && profileSlotPanel.TryHandleCloseRequest())
            return;

        if (keyBindingPanel != null && keyBindingPanel.TryHandleCloseRequest())
            return;

        if (settingsPanel != null && settingsPanel.IsActive)
            settingsPanel.CloseUI();
    }

    private void ResolveReferences()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (profileSlotPanel == null || profileSlotPanel.gameObject.scene != activeScene)
            profileSlotPanel = FindSceneComponent<TitleProfileSlotPanelUI>(activeScene);

        if (settingsPanel == null || settingsPanel.gameObject.scene != activeScene)
            settingsPanel = FindSceneComponent<SettingsPanelUI>(activeScene);

        if (keyBindingPanel == null || keyBindingPanel.gameObject.scene != activeScene)
            keyBindingPanel = FindSceneComponent<KeyBindingPanelUI>(activeScene);

        if (introPlayer == null || introPlayer.gameObject.scene != activeScene)
            introPlayer = FindSceneComponent<TitleIntroPlayer>(activeScene);

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

        if (settingsButton != null)
            settingsButton.onClick.AddListener(HandleSettingsPressed);

        if (quitButton != null)
            quitButton.onClick.AddListener(HandleQuitPressed);

        listenersBound = true;
    }

    private void HandleNewGamePressed()
    {
        ResolveReferences();
        OpenSlotPanel();
    }

    private void HandleSettingsPressed()
    {
        ResolveReferences();
        settingsPanel?.OpenUI();
    }

    private void HandleQuitPressed()
    {
        Application.Quit();
    }

    private void OpenSlotPanel()
    {
        ResolveReferences();
        if (profileSlotPanel == null)
            return;

        StopMainMenuUnlockLead();
        SetMainMenuInteractable(!lockMainMenuWhileSlotPanelOpen);
        profileSlotPanel.Open(HandleSlotSelected, HandleSlotPanelClosed);
    }

    private void HandleSlotPanelClosed()
    {
        if (isLoading || IsIntroActive)
            return;

        SetMainMenuInteractable(true);
        StartMainMenuUnlockLead();
    }

    private void HandleSlotSelected(int slotIndex)
    {
        if (isLoading || IsIntroActive)
            return;

        TitleProfileSlotService service = TitleProfileSlotService.EnsureInstance();
        if (service == null)
            return;

        if (!service.TryCreateLaunchRequest(slotIndex, out TitleProfileLaunchRequest request))
            return;

        if (ShouldPlayIntroBeforeLaunch(request))
        {
            BeginIntroLaunch(request);
            return;
        }

        PrepareAndLoad(request);
    }

    private bool ShouldPlayIntroBeforeLaunch(TitleProfileLaunchRequest request)
    {
        return playIntroForNewProfile &&
               request.Action == TitleProfileLaunchAction.StartNewRun &&
               introPlayer != null;
    }

    private void BeginIntroLaunch(TitleProfileLaunchRequest request)
    {
        StopMainMenuUnlockLead();
        SetMainMenuInteractable(false);
        SetProfileSlotPanelInteractionBlocked(true);

        bool didStartIntro =
            introPlayer != null &&
            introPlayer.TryPlay(() => PrepareAndLoad(request), keepViewVisibleOnCompleted: true);
        if (!didStartIntro)
        {
            PrepareAndLoad(request);
            return;
        }
    }

    private void PrepareAndLoad(TitleProfileLaunchRequest request)
    {
        TitleProfileLaunchResult launchResult =
            TitleProfileLaunchService.PrepareLaunch(request, GameDataManager.Instance);
        if (!launchResult.Succeeded)
        {
            introPlayer?.HideViewImmediate();
            SetProfileSlotPanelInteractionBlocked(false);
            SetMainMenuInteractable(true);
            return;
        }

        LoadScene(launchResult.TargetSceneName);
    }

    private void LoadScene(string targetSceneName)
    {
        if (isLoading || string.IsNullOrWhiteSpace(targetSceneName))
            return;

        isLoading = true;

        SceneTransitionCoordinator transitionCoordinator = SceneTransitionCoordinator.EnsureInstance();
        if (transitionCoordinator != null && transitionCoordinator.TryLoadScene(targetSceneName))
            return;

        SceneManager.LoadScene(targetSceneName);
    }

    private bool IsIntroActive => introPlayer != null && introPlayer.IsPlaying;

    private void SetProfileSlotPanelInteractionBlocked(bool blocked)
    {
        if (profileSlotPanel == null)
            return;

        profileSlotPanel.SetInteractionBlocked(blocked);
    }

    private void SetMainMenuInteractable(bool enabled)
    {
        if (mainMenuCanvasGroup == null)
            return;

        mainMenuCanvasGroup.interactable = enabled;
        mainMenuCanvasGroup.blocksRaycasts = enabled;
    }

    private void EnsureUiInputReady()
    {
        EnsurePresentationCanvasAdapter();
        EnsureEventSystemExists();
        EnsureCanvasRaycasterExists();
    }

    private void EnsurePresentationCanvasAdapter()
    {
        if (!adaptSceneCanvasToPresentationViewport)
            return;

        Canvas sceneCanvas = ResolveSceneCanvas();
        if (sceneCanvas == null)
            return;

        if (presentationCanvasAdapter == null || presentationCanvasAdapter.gameObject != sceneCanvas.gameObject)
            presentationCanvasAdapter = sceneCanvas.GetComponent<PresentationCanvasAdapter>();

        if (presentationCanvasAdapter == null)
            presentationCanvasAdapter = sceneCanvas.gameObject.AddComponent<PresentationCanvasAdapter>();

        presentationCanvasAdapter.ApplyNow(true);
    }

    private Canvas ResolveSceneCanvas()
    {
        Canvas ownCanvas = GetComponentInParent<Canvas>();
        if (ownCanvas != null)
            return ownCanvas;

        Scene activeScene = SceneManager.GetActiveScene();
        Canvas[] sceneCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sceneCanvases.Length; i++)
        {
            Canvas canvas = sceneCanvases[i];
            if (canvas == null || canvas.gameObject.scene != activeScene)
                continue;

            return canvas;
        }

        return null;
    }

    private void EnsureEventSystemExists()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        EventSystem[] existingEventSystems =
            FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        EventSystem activeSceneEventSystem = null;
        for (int i = 0; i < existingEventSystems.Length; i++)
        {
            EventSystem candidate = existingEventSystems[i];
            if (candidate == null)
                continue;

            if (candidate.gameObject.scene == activeScene)
            {
                activeSceneEventSystem = candidate;
                break;
            }
        }

        if (activeSceneEventSystem == null)
        {
            for (int i = 0; i < existingEventSystems.Length; i++)
            {
                EventSystem candidate = existingEventSystems[i];
                if (candidate == null)
                    continue;

                if (!string.Equals(candidate.gameObject.scene.name, DontDestroyOnLoadSceneName, System.StringComparison.Ordinal))
                    continue;

                Destroy(candidate.gameObject);
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            SceneManager.MoveGameObjectToScene(eventSystemObject, activeScene);
            activeSceneEventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        if (!activeSceneEventSystem.gameObject.activeSelf)
            activeSceneEventSystem.gameObject.SetActive(true);

#if ENABLE_INPUT_SYSTEM
        if (activeSceneEventSystem.GetComponent<InputSystemUIInputModule>() == null)
            activeSceneEventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
#else
        if (activeSceneEventSystem.GetComponent<StandaloneInputModule>() == null)
            activeSceneEventSystem.gameObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private void EnsureCanvasRaycasterExists()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Canvas[] sceneCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sceneCanvases.Length; i++)
        {
            Canvas canvas = sceneCanvases[i];
            if (canvas == null || canvas.gameObject.scene != activeScene)
                continue;

            if (canvas.GetComponent<GraphicRaycaster>() != null)
                continue;

            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private void StartMainMenuUnlockLead()
    {
        if (mainMenuInputUnlockDelay <= 0f)
        {
            ForceUnlockMainMenuIfPossible();
            return;
        }

        StopMainMenuUnlockLead();
        mainMenuUnlockCoroutine = StartCoroutine(MainMenuUnlockRoutine());
    }

    private void StopMainMenuUnlockLead()
    {
        if (mainMenuUnlockCoroutine == null)
            return;

        StopCoroutine(mainMenuUnlockCoroutine);
        mainMenuUnlockCoroutine = null;
    }

    private System.Collections.IEnumerator MainMenuUnlockRoutine()
    {
        float elapsed = 0f;
        while (elapsed < mainMenuInputUnlockDelay)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        mainMenuUnlockCoroutine = null;
        ForceUnlockMainMenuIfPossible();
    }

    private void ForceUnlockMainMenuIfPossible()
    {
        if (isLoading)
            return;

        if (profileSlotPanel != null && profileSlotPanel.IsActive)
            return;

        if (settingsPanel != null && settingsPanel.IsActive)
            return;

        SetMainMenuInteractable(true);
    }
    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        T[] candidates = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate == null || candidate.gameObject.scene != scene)
                continue;

            return candidate;
        }

        return null;
    }
}
