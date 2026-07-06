using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 : 일시정지 메뉴 화면의 버튼 입력, UI stack 상태, 타이틀/설정 진입 명령을 처리한다.
/// </summary>
public sealed class PauseMenuUI : MonoBehaviour, IStackableUI, ITitleScenePersistentCleanupTarget
{
    private const int SystemCursorPriority = 300;

    public static PauseMenuUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button titleSceneButton;
    [SerializeField] private Button quitGameButton;
    [SerializeField] private UIChainDropPresentation dropPresentation;

    private bool listenersBound;
    private bool isClosing;

    public bool IsActive => gameObject.activeSelf && !isClosing;
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
        isClosing = false;
        MouseCursorService.Instance?.ClearDomain(this);
    }

    public void OpenUI()
    {
        isClosing = false;
        ResolveReferences();
        RefreshCanvasParent();
        BindListeners();
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

        ResolveReferences();
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

    private void ResolveReferences()
    {
        if (dropPresentation == null)
            dropPresentation = GetComponent<UIChainDropPresentation>();

        if (dropPresentation == null)
            dropPresentation = GetComponentInChildren<UIChainDropPresentation>(true);
    }

    private void FinalizeClose()
    {
        gameObject.SetActive(false);
        isClosing = false;
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
