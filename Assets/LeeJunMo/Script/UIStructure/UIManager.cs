using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Controllers")]
    [SerializeField] private HoverUIController hoverUIController;
    [SerializeField] private WorldInteractionPromptController worldPromptController;

    [Header("Gameplay Lock")]
    [SerializeField] private GameplayTagSet blockControlByUiTagSet;
    [SerializeField] private UIGameplayLockProfile dialogueGameplayLockProfile = UIGameplayLockProfile.BlockControlOnly;

    private readonly PopupStackState popupStack = new PopupStackState();
    private readonly WorldPromptCoordinator worldPromptCoordinator = new WorldPromptCoordinator();
    private readonly HashSet<int> gameplayHudCurrencyHideOwners = new HashSet<int>();
    private CurrencyUI gameplayHudCurrencyUI;
    private bool gameplayHudCurrencyWasActive = true;
    private PlayerUIControlLockBridge activeControlLockBridge;
    private bool isControlLockApplied;
    private bool isTimeFrozenByUi;
    private bool wasDialoguePlaying;
    private float frozenPreviousTimeScale = 1f;
    private const string BlockControlByUiTagSetResourcePath = "Tags/TagSet/TS_BlockControlByUI";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        GlobalUIRoot.AdoptService(transform);
        DontDestroyOnLoad(gameObject);

        if (hoverUIController == null)
            hoverUIController = GetComponent<HoverUIController>();

        if (blockControlByUiTagSet == null)
            blockControlByUiTagSet = Resources.Load<GameplayTagSet>(BlockControlByUiTagSetResourcePath);

        hoverUIController?.RefreshCanvasReference();
        worldPromptCoordinator.Initialize(worldPromptController);
        worldPromptCoordinator.OnSceneLoaded();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        popupStack.PruneDeadEntries();
        RefreshDialogueDrivenGameplayLock();

        if (Input.GetKeyDown(KeyCode.Escape))
            CloseTopUI();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        popupStack.Clear();
        wasDialoguePlaying = false;
        ReleaseControlLock();
        RestoreTimeScaleIfNeeded();
        HideHoverImmediate();
        hoverUIController?.RefreshCanvasReference();
        worldPromptCoordinator.OnSceneLoaded();
        gameplayHudCurrencyUI = null;
        gameplayHudCurrencyHideOwners.Clear();
        gameplayHudCurrencyWasActive = true;
    }

    public bool CanOpenUI(IStackableUI ui)
    {
        if (ui == null)
            return false;

        var snapshot = popupStack.Snapshot();
        for (int i = 0; i < snapshot.Count; i++)
        {
            var openedUi = snapshot[i];
            if (openedUi == null || ReferenceEquals(openedUi, ui))
                continue;

            if (AreGroupsConflicting(ui, openedUi))
                return false;
        }

        return true;
    }

    public bool TryPushUI(IStackableUI ui)
    {
        if (!CanOpenUI(ui))
            return false;

        PushUI(ui);
        return true;
    }

    public void PushUI(IStackableUI ui)
    {
        if (ui == null)
            return;

        if (!CanOpenUI(ui))
            return;

        popupStack.Push(ui);
        ui.OpenUI();
        ApplyGameplayLockState();
    }

    public void PopUI(IStackableUI ui)
    {
        if (ui == null)
            return;

        if (!popupStack.Remove(ui))
            return;

        ui.CloseUI();
        ApplyGameplayLockState();
        HideHoverImmediate();
    }

    public void CloseTopUI()
    {
        if (!popupStack.TryGetTop(out IStackableUI topUI))
            return;

        if (topUI.CanCloseOnEscape)
            PopUI(topUI);
    }

    public void CloseAllPopups(bool force = true)
    {
        var snapshot = popupStack.Snapshot();
        if (snapshot == null || snapshot.Count == 0)
            return;

        for (int i = snapshot.Count - 1; i >= 0; i--)
        {
            var ui = snapshot[i];
            if (ui == null)
                continue;

            if (!force && !ui.CanCloseOnEscape)
                continue;

            PopUI(ui);
        }
    }

    public bool HasActivePopup()
    {
        return popupStack.HasAny();
    }

    public bool HasBlockingUI()
    {
        return HasActivePopup() || (DialogueService.Instance != null && DialogueService.Instance.IsPlaying);
    }

    public void ShowWorldPrompt(IInteractable target)
    {
        worldPromptCoordinator.Show(target, HasBlockingUI());
    }

    public void RefreshWorldPrompt(IInteractable target)
    {
        worldPromptCoordinator.Refresh(target, HasBlockingUI());
    }

    public void HideWorldPrompt()
    {
        worldPromptCoordinator.Hide();
    }

    public void ShowHover(IHoverView view, RectTransform targetRect, object data, object context = null)
    {
        if (hoverUIController != null)
            hoverUIController.ShowHover(view, targetRect, data, context);
    }

    public void HideHover(IHoverView view, RectTransform targetRect)
    {
        if (hoverUIController != null)
            hoverUIController.HideHover(view, targetRect);
    }

    public void HideHoverImmediate()
    {
        if (hoverUIController != null)
            hoverUIController.HideImmediate();
    }

    public void SetGameplayHudCurrencyHidden(Object owner, bool hidden)
    {
        if (owner == null)
            return;

        int ownerId = owner.GetInstanceID();
        if (hidden)
            gameplayHudCurrencyHideOwners.Add(ownerId);
        else
            gameplayHudCurrencyHideOwners.Remove(ownerId);

        ApplyGameplayHudCurrencyVisibility();
    }

    private void ApplyGameplayHudCurrencyVisibility()
    {
        CurrencyUI hudCurrency = ResolveGameplayHudCurrencyUI();
        if (hudCurrency == null)
            return;

        bool shouldHide = gameplayHudCurrencyHideOwners.Count > 0;
        if (shouldHide)
        {
            gameplayHudCurrencyWasActive = hudCurrency.gameObject.activeSelf;
            if (hudCurrency.gameObject.activeSelf)
                hudCurrency.gameObject.SetActive(false);
            return;
        }

        hudCurrency.gameObject.SetActive(gameplayHudCurrencyWasActive);
    }

    private CurrencyUI ResolveGameplayHudCurrencyUI()
    {
        if (gameplayHudCurrencyUI != null)
            return gameplayHudCurrencyUI;

        Canvas hudCanvas = GlobalUIRoot.GetCanvas(GlobalCanvasLayer.GameplayHUD);
        if (hudCanvas == null)
            return null;

        CurrencyUI[] currencyUIs = hudCanvas.GetComponentsInChildren<CurrencyUI>(true);
        if (currencyUIs == null || currencyUIs.Length == 0)
            return null;

        gameplayHudCurrencyUI = currencyUIs[0];
        return gameplayHudCurrencyUI;
    }

    private void HandlePlayerRegistered(PlayerInteractor2D player)
    {
        ApplyGameplayLockState();
    }

    /// <summary>
    /// 책임 :
    /// - 대화 시작/종료처럼 팝업 스택 변경 없이 발생하는 UI 상태 변화를 감지해 gameplay lock을 재평가한다.
    /// </summary>
    private void RefreshDialogueDrivenGameplayLock()
    {
        bool isDialoguePlaying = DialogueService.Instance != null && DialogueService.Instance.IsPlaying;
        if (isDialoguePlaying == wasDialoguePlaying)
            return;

        wasDialoguePlaying = isDialoguePlaying;
        ApplyGameplayLockState();
    }

    /// <summary>
    /// 책임 :
    /// - 현재 열린 팝업 UI들의 lock profile을 집계해 시간 정지와 플레이어 조작 차단을 공통 정책으로 적용한다.
    /// </summary>
    private void ApplyGameplayLockState()
    {
        UIGameplayLockProfile highestProfile = GetHighestGameplayLockProfile();

        bool shouldBlockControl = highestProfile >= UIGameplayLockProfile.BlockControlOnly;
        bool shouldFreezeTime = highestProfile >= UIGameplayLockProfile.FreezeAndBlockControl;

        if (shouldBlockControl) ApplyControlLock();
        else ReleaseControlLock();

        if (shouldFreezeTime) FreezeTimeIfNeeded();
        else RestoreTimeScaleIfNeeded();
    }

    private UIGameplayLockProfile GetHighestGameplayLockProfile()
    {
        UIGameplayLockProfile highestProfile = UIGameplayLockProfile.None;
        var snapshot = popupStack.Snapshot();

        for (int i = 0; i < snapshot.Count; i++)
        {
            var ui = snapshot[i];
            if (ui == null)
                continue;

            if (ui.GameplayLockProfile > highestProfile)
                highestProfile = ui.GameplayLockProfile;
        }

        if (DialogueService.Instance != null &&
            DialogueService.Instance.IsPlaying &&
            dialogueGameplayLockProfile > highestProfile)
        {
            highestProfile = dialogueGameplayLockProfile;
        }

        return highestProfile;
    }

    private static bool AreGroupsConflicting(IStackableUI incoming, IStackableUI opened)
    {
        if (incoming == null || opened == null)
            return false;

        bool incomingBlocksOpened = incoming.BlockedOpenGroups != UIOpenGroup.None &&
                                    (incoming.BlockedOpenGroups & opened.OpenGroup) != 0;
        bool openedBlocksIncoming = opened.BlockedOpenGroups != UIOpenGroup.None &&
                                    (opened.BlockedOpenGroups & incoming.OpenGroup) != 0;
        return incomingBlocksOpened || openedBlocksIncoming;
    }

    private void ApplyControlLock()
    {
        if (isControlLockApplied)
            return;

        if (blockControlByUiTagSet == null)
            return;

        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform == null && PlayerInteractor2D.Instance != null)
            playerTransform = PlayerInteractor2D.Instance.transform;

        activeControlLockBridge = PlayerUIControlLockBridge.GetOrAdd(playerTransform);
        if (activeControlLockBridge == null)
            return;

        if (activeControlLockBridge.Acquire(this, blockControlByUiTagSet))
            isControlLockApplied = true;
    }

    private void ReleaseControlLock()
    {
        if (!isControlLockApplied)
            return;

        if (activeControlLockBridge != null && blockControlByUiTagSet != null)
            activeControlLockBridge.Release(this, blockControlByUiTagSet);

        activeControlLockBridge = null;
        isControlLockApplied = false;
    }

    private void FreezeTimeIfNeeded()
    {
        if (isTimeFrozenByUi)
            return;

        frozenPreviousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        isTimeFrozenByUi = true;
    }

    private void RestoreTimeScaleIfNeeded()
    {
        if (!isTimeFrozenByUi)
            return;

        Time.timeScale = frozenPreviousTimeScale;
        isTimeFrozenByUi = false;
        frozenPreviousTimeScale = 1f;
    }

}
