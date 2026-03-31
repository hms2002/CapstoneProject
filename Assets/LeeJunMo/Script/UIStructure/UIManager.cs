using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Controllers")]
    [SerializeField] private HoverUIController hoverUIController;
    [SerializeField] private WorldInteractionPromptController worldPromptController;

    private readonly PopupStackState popupStack = new PopupStackState();
    private readonly WorldPromptCoordinator worldPromptCoordinator = new WorldPromptCoordinator();
    private readonly HashSet<int> gameplayHudCurrencyHideOwners = new HashSet<int>();
    private CurrencyUI gameplayHudCurrencyUI;
    private bool gameplayHudCurrencyWasActive = true;

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

        hoverUIController?.RefreshCanvasReference();
        worldPromptCoordinator.Initialize(worldPromptController);
        worldPromptCoordinator.OnSceneLoaded();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        popupStack.PruneDeadEntries();

        if (Input.GetKeyDown(KeyCode.Escape))
            CloseTopUI();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        popupStack.Clear();
        HideHoverImmediate();
        hoverUIController?.RefreshCanvasReference();
        worldPromptCoordinator.OnSceneLoaded();
        gameplayHudCurrencyUI = null;
        gameplayHudCurrencyHideOwners.Clear();
        gameplayHudCurrencyWasActive = true;
    }

    public void PushUI(IStackableUI ui)
    {
        if (ui == null)
            return;

        popupStack.Push(ui);
        ui.OpenUI();
    }

    public void PopUI(IStackableUI ui)
    {
        if (ui == null)
            return;

        if (!popupStack.Remove(ui))
            return;

        ui.CloseUI();
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

}
