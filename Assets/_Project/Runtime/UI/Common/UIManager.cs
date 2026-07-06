using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 전역 UI 서비스와 공통 캔버스 진입점을 관리하고, 씬 전환 시 UI 상태를 정리한다.
/// - gameplay 계층이 구체 UI 구현을 직접 알지 않도록 팝업/프롬프트/잠금 요청을 중계한다.
/// </summary>
public class UIManager : MonoBehaviour, IWarningPopupBackend, IUiInteractionStateBackend, IUiCommandBackend, IUiStackBackend, ITitleScenePersistentCleanupTarget
{
    private const string BlockControlByUiTagSetResourcePath = "Tags/TagSet/TS_BlockControlByUI";

    public static UIManager Instance { get; private set; }

    [Header("Controllers")]
    [SerializeField] private HoverUIController hoverUIController;
    [SerializeField] private WorldInteractionPromptController worldPromptController;

    [Header("Feedback")]
    [SerializeField] private WarningPopupService warningPopupService;

    [Header("Gameplay Lock")]
    [SerializeField] private GameplayTagSet blockControlByUiTagSet;
    [SerializeField, HideInInspector] private UIGameplayLockProfile dialogueGameplayLockProfile;

    [Header("Pause Menu")]
    [SerializeField] private string titleSceneNameOverride = string.Empty;

    private readonly PopupStackState popupStack = new PopupStackState();
    private readonly WorldPromptCoordinator worldPromptCoordinator = new WorldPromptCoordinator();
    private readonly HashSet<int> gameplayHudCurrencyHideOwners = new HashSet<int>();
    private readonly HashSet<Object> externalUiInputBlockOwners = new HashSet<Object>();
    private CurrencyUI gameplayHudCurrencyUI;
    private PauseMenuUI pauseMenu;
    private SettingsPanelUI settingsPanel;
    private KeyBindingPanelUI keyBindingPanel;
    private bool settingsHiddenByKeyBinding;
    private bool gameplayHudCurrencyWasActive = true;
    private PlayerUIControlLockBridge activeControlLockBridge;
    private bool isControlLockApplied;
    private bool isTimeFrozenByUi;

    public bool IsExternalUiInputBlocked => HasExternalUiInputBlockers();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        WarningPopupPlayback.RegisterBackend(this);
        UiInteractionStateQuery.RegisterBackend(this);
        UiCommandPlayback.RegisterBackend(this);
        UiStackPlayback.RegisterBackend(this);
        GlobalUIRoot.AdoptService(transform);
        MarkPersistent();

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
        {
            WarningPopupPlayback.RegisterBackend(null);
            UiInteractionStateQuery.RegisterBackend(null);
            UiCommandPlayback.RegisterBackend(null);
            UiStackPlayback.RegisterBackend(null);
        }

        if (Instance == this)
            Instance = null;
    }

    private void MarkPersistent()
    {
        Transform persistentRoot = transform.root;
        if (persistentRoot == null)
            return;

        if (persistentRoot.parent != null)
            return;

        DontDestroyOnLoad(persistentRoot.gameObject);
    }

    private void Update()
    {
        popupStack.PruneDeadEntries();

        if (IsInputBlockedByLoading())
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            HandleEscapeInput();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        popupStack.Clear();
        pauseMenu?.RefreshCanvasParent();
        pauseMenu?.CloseUI();
        settingsPanel?.RefreshCanvasParent();
        settingsPanel?.CloseUI();
        keyBindingPanel?.RefreshCanvasParent();
        keyBindingPanel?.CloseUI();
        settingsHiddenByKeyBinding = false;
        externalUiInputBlockOwners.Clear();
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
        return CanOpenUI(ui, null);
    }

    public bool CanOpenUIForExternalBlockOwner(Object owner, IStackableUI ui)
    {
        return owner != null && CanOpenUI(ui, owner);
    }

    private bool CanOpenUI(IStackableUI ui, Object allowedExternalBlockOwner)
    {
        return CanOpenUI(ui, allowedExternalBlockOwner, false);
    }

    private bool CanOpenUI(IStackableUI ui, Object allowedExternalBlockOwner, bool ignoreExternalInputBlockers)
    {
        if (ui == null)
            return false;

        if (IsNewUiOpeningBlocked(allowedExternalBlockOwner, ignoreExternalInputBlockers))
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

    public bool CanOpenFlowOwnedUI(IStackableUI ui)
    {
        return CanOpenUI(ui, null, true);
    }

    public bool TryPushUI(IStackableUI ui)
    {
        if (!CanOpenUI(ui))
            return false;

        PushUI(ui);
        return true;
    }

    public bool TryPushUIForExternalBlockOwner(Object owner, IStackableUI ui)
    {
        if (owner == null)
            return TryPushUI(ui);

        if (!CanOpenUI(ui, owner))
            return false;

        PushUI(ui, owner);
        return true;
    }

    public bool TryPushFlowOwnedUI(IStackableUI ui)
    {
        if (!CanOpenFlowOwnedUI(ui))
            return false;

        PushUI(ui, null, true);
        return true;
    }

    public void PushUI(IStackableUI ui)
    {
        PushUI(ui, null);
    }

    private void PushUI(IStackableUI ui, Object allowedExternalBlockOwner)
    {
        PushUI(ui, allowedExternalBlockOwner, false);
    }

    private void PushUI(IStackableUI ui, Object allowedExternalBlockOwner, bool ignoreExternalInputBlockers)
    {
        if (ui == null)
            return;

        if (!CanOpenUI(ui, allowedExternalBlockOwner, ignoreExternalInputBlockers))
            return;

        popupStack.Push(ui);
        ui.OpenUI();
        ApplyGameplayLockState();
    }

    public void PopUI(IStackableUI ui)
    {
        if (ui is ICloseRequestHandler closeHandler && closeHandler.TryHandleCloseRequest())
            return;

        PopUIImmediate(ui);
    }

    private void PopUIImmediate(IStackableUI ui)
    {
        if (ui == null)
            return;

        if (!popupStack.Remove(ui))
            return;

        ui.CloseUI();

        if (ReferenceEquals(ui, keyBindingPanel) && settingsHiddenByKeyBinding)
        {
            settingsPanel?.SetTemporarilyHidden(false);
            settingsHiddenByKeyBinding = false;
        }

        ApplyGameplayLockState();
        HideHoverImmediate();
    }

    public void CloseTopUI()
    {
        if (!popupStack.TryGetTop(out IStackableUI topUI))
            return;

        if (topUI is ICloseRequestHandler closeHandler && closeHandler.TryHandleCloseRequest())
            return;

        if (topUI.CanCloseOnEscape)
            PopUI(topUI);
    }

    private void HandleEscapeInput()
    {
        if (IsInputBlockedByLoading() || IsExternalUiInputBlocked)
            return;

        if (popupStack.TryGetTop(out IStackableUI topUI))
        {
            if (topUI is ICloseRequestHandler closeHandler && closeHandler.TryHandleCloseRequest())
                return;

            if (topUI.CanCloseOnEscape)
                PopUI(topUI);

            return;
        }

        TogglePauseMenu();
    }

    private void TogglePauseMenu()
    {
        if (IsInputBlockedByLoading() || IsExternalUiInputBlocked)
            return;

        PauseMenuUI panel = ResolvePauseMenu();
        if (panel == null)
            return;

        if (panel.IsActive)
        {
            PopUI(panel);
            return;
        }

        HideHoverImmediate();
        HideWorldPrompt();
        TryPushUI(panel);
    }

    public bool OpenSettingsPanel()
    {
        if (IsInputBlockedByLoading() || IsExternalUiInputBlocked)
            return false;

        SettingsPanelUI panel = ResolveSettingsPanel();
        if (panel == null || panel.IsActive)
            return false;

        HideHoverImmediate();
        HideWorldPrompt();
        return TryPushUI(panel);
    }

    public bool OpenKeyBindingPanel()
    {
        if (IsInputBlockedByLoading() || IsExternalUiInputBlocked)
            return false;

        KeyBindingPanelUI panel = ResolveKeyBindingPanel();
        if (panel == null || panel.IsActive)
            return false;

        SettingsPanelUI ownerSettingsPanel = ResolveSettingsPanel();
        settingsHiddenByKeyBinding = ownerSettingsPanel != null && ownerSettingsPanel.IsActive;
        if (settingsHiddenByKeyBinding)
            ownerSettingsPanel.SetTemporarilyHidden(true);

        HideHoverImmediate();
        HideWorldPrompt();
        bool opened = TryPushUI(panel);
        if (!opened && settingsHiddenByKeyBinding)
        {
            ownerSettingsPanel?.SetTemporarilyHidden(false);
            settingsHiddenByKeyBinding = false;
        }

        return opened;
    }

    public void ReturnToTitleScreen()
    {
        TitleReturnRequest request = new TitleReturnRequest(
            this,
            ResolveTitleSceneName(),
            GamePlayDataManager.Instance);
        if (!request.IsValid)
        {
            Debug.LogWarning("[UIManager] Title scene name could not be resolved.", this);
            return;
        }

        TitleReturnService.Execute(request);
    }

    public void QuitGame()
    {
        CloseAllPopups();
        HideHoverImmediate();
        HideWorldPrompt();

        if (GamePlayDataManager.Instance != null)
            GamePlayDataManager.Instance.EndRun(RunEndReason.None);

        ApplicationQuitPlayback.Quit();
    }

    private PauseMenuUI ResolvePauseMenu()
    {
        if (pauseMenu != null)
            return pauseMenu;

        pauseMenu = PauseMenuUI.EnsureInstance();
        pauseMenu?.RefreshCanvasParent();
        return pauseMenu;
    }

    private SettingsPanelUI ResolveSettingsPanel()
    {
        if (settingsPanel != null)
            return settingsPanel;

        settingsPanel = SettingsPanelUI.EnsureInstance();
        settingsPanel?.RefreshCanvasParent();
        return settingsPanel;
    }

    private KeyBindingPanelUI ResolveKeyBindingPanel()
    {
        if (keyBindingPanel != null)
            return keyBindingPanel;

        keyBindingPanel = KeyBindingPanelUI.EnsureInstance();
        keyBindingPanel?.RefreshCanvasParent();
        return keyBindingPanel;
    }

    private string ResolveTitleSceneName()
    {
        return TitleSceneNameResolver.Resolve(titleSceneNameOverride);
    }

    private static bool IsInputBlockedByLoading()
    {
        if (SceneTransitionCoordinator.Instance != null &&
            SceneTransitionCoordinator.Instance.IsTransitionActive)
        {
            return true;
        }

        return LoadingOverlayController.Instance != null &&
               LoadingOverlayController.Instance.IsActiveLoadingPresentation;
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

            PopUIImmediate(ui);
        }
    }

    public bool HasActivePopup()
    {
        return popupStack.HasAny();
    }

    /// <summary>
    /// 책임 :
    /// - 현재 popup stack에 올라온 UI 중 특정 canvas 아래에 실제로 열린 항목이 있는지 판별한다.
    /// - canvas raycast gate가 상시 활성 매니저 오브젝트가 아니라 열린 stackable UI를 기준으로 입력 차단 여부를 결정하게 만든다.
    /// </summary>
    public bool HasActivePopupInCanvas(Canvas canvas)
    {
        if (canvas == null)
            return false;

        var snapshot = popupStack.Snapshot();
        if (snapshot == null || snapshot.Count == 0)
            return false;

        Transform canvasTransform = canvas.transform;
        for (int i = 0; i < snapshot.Count; i++)
        {
            IStackableUI openedUi = snapshot[i];
            if (openedUi == null || !openedUi.IsActive)
                continue;

            if (openedUi is not MonoBehaviour behaviour || behaviour == null)
                continue;

            if (behaviour.transform.IsChildOf(canvasTransform))
                return true;
        }

        return false;
    }

    public bool HasBlockingUI()
    {
        return HasActivePopup() || IsExternalUiInputBlocked;
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

    /// <summary>
    /// 책임 :
    /// - 자주 사용하는 경고 사유 코드를 UI 전용 메시지로 해석해 WarningPopupService에 전달한다.
    /// - gameplay/domain 계층이 경고 문구와 팝업 서비스 구현을 직접 알지 않도록 UIManager 진입점을 제공한다.
    /// </summary>
    public void ShowWarning(WarningPopupCode code, float duration = WarningPopupUI.DefaultDuration)
    {
        if (code == WarningPopupCode.None)
            return;

        string message = ResolveWarningMessage(code);
        if (string.IsNullOrWhiteSpace(message))
            return;

        ShowWarning(message, duration);
    }

    /// <summary>
    /// 책임 :
    /// - 외부 시스템이 전달한 경고 문자열을 공통 WarningPopupService로 위임한다.
    /// - UIManager가 보유한 고정 서비스 참조만 사용해 경고 표시 경로를 일관되게 유지한다.
    /// </summary>
    public void ShowWarning(string message, float duration = WarningPopupUI.DefaultDuration)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        WarningPopupService service = ResolveWarningPopupService();
        if (service == null)
        {
            Debug.LogWarning("[UIManager] WarningPopupService could not be resolved.", this);
            return;
        }

        service.ShowWarning(message, duration);
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

    public bool SetExternalUiInputBlocked(Object owner, bool blocked)
    {
        if (owner == null)
            return false;

        if (blocked)
        {
            externalUiInputBlockOwners.Add(owner);
            HideHoverImmediate();
            HideWorldPrompt();
            ApplyGameplayLockState();
            return true;
        }

        externalUiInputBlockOwners.Remove(owner);
        ApplyGameplayLockState();
        return true;
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

    /// <summary>
    /// 책임 :
    /// - 경고 팝업 서비스 참조를 UIManager가 가진 직렬화 필드에서만 제공한다.
    /// - 경고 팝업 의존 경로를 UIManager -> WarningPopupService 한 단계로 단순화한다.
    /// </summary>
    private WarningPopupService ResolveWarningPopupService()
    {
        return warningPopupService;
    }

    /// <summary>
    /// 책임 :
    /// - Core 경고 팝업 요청을 UIManager가 보유한 WarningPopupService 출력 경로로 변환한다.
    /// - 요청이 duration을 생략하면 기존 UI 기본 표시 시간을 그대로 사용한다.
    /// </summary>
    void IWarningPopupBackend.ShowWarning(in WarningPopupRequest request)
    {
        if (request.Code != WarningPopupCode.None)
        {
            if (request.HasDuration)
                ShowWarning(request.Code, request.Duration);
            else
                ShowWarning(request.Code);

            return;
        }

        if (request.HasDuration)
            ShowWarning(request.Message, request.Duration);
        else
            ShowWarning(request.Message);
    }

    /// <summary>
    /// 책임 :
    /// - WarningPopupCode를 실제 플레이어에게 보여줄 한국어 경고 문구로 변환한다.
    /// - 경고 문구의 일관성과 향후 수정 포인트를 UIManager 한 곳으로 모은다.
    /// </summary>
    private static string ResolveWarningMessage(WarningPopupCode code)
    {
        return code switch
        {
            WarningPopupCode.RelicInventoryFull => "유물 인벤토리가 가득 찼습니다.",
            WarningPopupCode.RelicAlreadyMaxLevel => "이미 최대 레벨인 유물입니다.",
            WarningPopupCode.WeaponInventoryFull => "무기 인벤토리가 가득 찼습니다.",
            WarningPopupCode.ConsumableInventoryFull => "일회용 아이템 인벤토리가 가득 찼습니다.",
            WarningPopupCode.CannotDropHere => "여기에는 버릴 수 없습니다.",
            WarningPopupCode.LastWeaponCannotLeaveInventory => "마지막 무기는 버리거나 옮길 수 없습니다.",
            WarningPopupCode.RelicChangeWouldDefeatPlayer => "현재 체력이 부족해 해제할 수 없습니다.",
            WarningPopupCode.UpgradeNotEnoughMagicStone => "\uB9C8\uC815\uC11D\uC774 \uBD80\uC871\uD569\uB2C8\uB2E4.",
            WarningPopupCode.UpgradeLocked => "\uC544\uC9C1 \uD574\uAE08\uB418\uC9C0 \uC54A\uC740 \uC5C5\uADF8\uB808\uC774\uB4DC\uC785\uB2C8\uB2E4.",
            WarningPopupCode.UpgradeUnavailable => "\uD604\uC7AC \uAD6C\uB9E4\uD560 \uC218 \uC5C6\uB294 \uC5C5\uADF8\uB808\uC774\uB4DC\uC785\uB2C8\uB2E4.",
            _ => string.Empty,
        };
    }

    private void HandlePlayerRegistered(PlayerInteractor2D player)
    {
        ApplyGameplayLockState();
    }

    /// <summary>
    /// Blocks new stack UI openings while loading or an explicit external game flow blocker is active.
    /// </summary>
    private bool IsNewUiOpeningBlocked(Object allowedExternalBlockOwner = null, bool ignoreExternalInputBlockers = false)
    {
        if (IsInputBlockedByLoading())
            return true;

        return !ignoreExternalInputBlockers &&
               HasExternalUiInputBlockersExcept(allowedExternalBlockOwner);
    }

    private bool HasExternalUiInputBlockers()
    {
        return HasExternalUiInputBlockersExcept(null);
    }

    private bool HasExternalUiInputBlockersExcept(Object allowedOwner)
    {
        if (externalUiInputBlockOwners.Count == 0)
            return false;

        bool hasBlockingOwner = false;
        List<Object> deadOwners = null;
        foreach (Object owner in externalUiInputBlockOwners)
        {
            if (owner == null)
            {
                deadOwners ??= new List<Object>();
                deadOwners.Add(owner);
                continue;
            }

            if (allowedOwner != null && owner == allowedOwner)
                continue;

            hasBlockingOwner = true;
        }

        if (deadOwners != null)
        {
            for (int i = 0; i < deadOwners.Count; i++)
                externalUiInputBlockOwners.Remove(deadOwners[i]);
        }

        return hasBlockingOwner;
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

        if (HasExternalUiInputBlockers() &&
            UIGameplayLockProfile.BlockControlOnly > highestProfile)
        {
            highestProfile = UIGameplayLockProfile.BlockControlOnly;
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

        TimeScalePauseService.Acquire(this);
        isTimeFrozenByUi = true;
    }

    private void RestoreTimeScaleIfNeeded()
    {
        if (!isTimeFrozenByUi)
            return;

        TimeScalePauseService.Release(this);
        isTimeFrozenByUi = false;
    }

}

