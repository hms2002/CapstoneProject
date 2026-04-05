using UnityEngine;
using UnityEngine.UI;

public sealed class PauseMenuUI : MonoBehaviour, IStackableUI
{
    public static PauseMenuUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button titleSceneButton;
    [SerializeField] private Button quitGameButton;

    private bool listenersBound;

    public bool IsActive => gameObject.activeSelf;
    public bool CanCloseOnEscape => true;
    public UIOpenGroup OpenGroup => UIOpenGroup.ExclusiveModal;
    public UIOpenGroup BlockedOpenGroups => UIOpenGroup.ExclusiveModal;
    public UIGameplayLockProfile GameplayLockProfile => UIGameplayLockProfile.FreezeAndBlockControl;

    public static PauseMenuUI EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        PauseMenuUI[] existing = Resources.FindObjectsOfTypeAll<PauseMenuUI>();
        for (int i = 0; i < existing.Length; i++)
        {
            PauseMenuUI candidate = existing[i];
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
        BindListeners();
        RefreshCanvasParent();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void OpenUI()
    {
        RefreshCanvasParent();
        BindListeners();
        gameObject.SetActive(true);
    }

    public void CloseUI()
    {
        gameObject.SetActive(false);
    }

    public void RefreshCanvasParent()
    {
        GlobalUIRoot.AdoptToCanvas(GlobalCanvasLayer.Popup, transform, false);
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;

        if (continueButton != null)
            continueButton.onClick.AddListener(HandleContinue);

        if (optionsButton != null)
            optionsButton.onClick.AddListener(HandleOpenSettings);

        if (titleSceneButton != null)
            titleSceneButton.onClick.AddListener(HandleReturnToTitle);

        if (quitGameButton != null)
            quitGameButton.onClick.AddListener(HandleQuitGame);

        listenersBound = true;
    }

    private void HandleContinue()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.PopUI(this);
        else
            CloseUI();
    }

    private void HandleOpenSettings()
    {
        UIManager.Instance?.OpenSettingsPanel();
    }

    private void HandleReturnToTitle()
    {
        UIManager.Instance?.ReturnToTitleScreen();
    }

    private void HandleQuitGame()
    {
        UIManager.Instance?.QuitGame();
    }
}
