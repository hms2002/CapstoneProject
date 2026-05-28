using CapstoneAudio;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EncyclopediaScreen : MonoBehaviour, IStackableUI, ICloseRequestHandler
{
    private static readonly SoundRef OpenDictionarySound = SoundRef.FromKey("sound_ui_OpenDictionary");
    private static readonly SoundRef CloseDictionarySound = SoundRef.FromKey("sound_ui_CloseDictionary");

    [Header("Data Gate")]
    [SerializeField] private bool resetToItemWeaponOnOpen = true;
    [SerializeField] private bool requireDataSourceToOpen;

    [Header("Scope")]
    [SerializeField] private bool enableMonsterCategory;
    [SerializeField] private bool enableBossCategory;
    [SerializeField] private bool hideDisabledCategoryTabs = true;

    [Header("Root")]
    [SerializeField] private GameObject screenActiveRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button closeButton;

    [Header("Item Tab")]
    [SerializeField] private EncyclopediaItemTab itemTab;

    [Header("Right Page Main Tabs")]
    [SerializeField] private Button itemMainTabButton;
    [SerializeField] private Button monsterMainTabButton;
    [SerializeField] private Button bossMainTabButton;
    [SerializeField] private GameObject itemMainSelectedMarker;
    [SerializeField] private GameObject monsterMainSelectedMarker;
    [SerializeField] private GameObject bossMainSelectedMarker;

    [Header("Presentation")]
    [SerializeField] private BookPixelRevealPresentation revealPresentation;
    [SerializeField] private EncyclopediaBookPresentation bookPresentation;
    [SerializeField] private UISlideFadePresentation rootSlideFadePresentation;
    [SerializeField] private bool playRootSlideFadePresentation = true;
    [SerializeField] private bool playBookOpenOnOpen = true;
    [SerializeField] private bool playBookCloseOnClose = true;

    [Header("Runtime Startup")]
    [SerializeField] private bool closeOnRuntimeAwake = true;

    private EncyclopediaMainTab currentMainTab = EncyclopediaMainTab.Item;
    private bool listenersBound;
    private bool closePresentationRequested;
    private bool closePresentationComplete;
    private bool warnedMissingItemTab;
    private bool warnedMissingBookPresentation;
    private bool warnedMissingScreenActiveRoot;
    private bool warnedMissingCanvasGroup;
    private bool openingViaOpenUI;

    public bool IsActive => GetScreenActiveSelf();
    public bool CanCloseOnEscape => true;
    public UIOpenGroup OpenGroup => UIOpenGroup.ExclusiveModal;
    public UIOpenGroup BlockedOpenGroups => UIOpenGroup.ExclusiveModal;
    public UIGameplayLockProfile GameplayLockProfile => UIGameplayLockProfile.FreezeAndBlockControl;

    public bool HasOpenableDataSource
    {
        get
        {
            if (!requireDataSourceToOpen)
                return true;

            return itemTab != null && itemTab.HasDataSource;
        }
    }

    private void Awake()
    {
        InitializeRuntimeReferences();
        BindListeners();
        CloseIfActiveAtRuntimeStart();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveReferences();
    }

    [ContextMenu("Auto Wire References")]
    private void AutoWireReferences()
    {
        ResolveReferences();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private void OnDestroy()
    {
        UnbindListeners();
    }

    private void OnDisable()
    {
        revealPresentation?.CancelAndHide();
        if (gameObject.activeInHierarchy)
            bookPresentation?.SnapClosed();

        SetInteractionEnabled(true);
        closePresentationRequested = false;
        closePresentationComplete = false;
    }

    public void SetCatalog(EncyclopediaCatalogSO newCatalog)
    {
        _ = newCatalog;
        // Monster/Boss catalog data is outside the current Item-tab scope.
    }

    public void SetItemDatabase(ItemDatabase newItemDatabase)
    {
        itemTab?.SetItemDatabase(newItemDatabase);
        if (itemTab == null)
            WarnMissingItemTab();
    }

    public void OpenUI()
    {
        openingViaOpenUI = true;
        try
        {
            OpenUIInternal();
        }
        finally
        {
            openingViaOpenUI = false;
        }
    }

    public void CloseUI()
    {
        revealPresentation?.CancelAndHide();
        SetScreenActiveRootActive(false);
        bookPresentation?.SnapClosed();
        closePresentationRequested = false;
        closePresentationComplete = false;
    }

    public bool TryHandleCloseRequest()
    {
        if (closePresentationComplete)
            return false;

        if (closePresentationRequested)
            return true;

        if (!CanPlayClosePresentation())
            return false;

        closePresentationRequested = true;
        SetInteractionEnabled(false);
        revealPresentation?.CancelAndHide();
        itemTab?.SetContentVisible(false);
        PlayClosePresentationThenPop();
        return true;
    }

    public void RequestClose()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.PopUI(this);
        else
            CloseUI();
    }

    public void OpenItemTab()
    {
        RequestMainTab(EncyclopediaMainTab.Item);
    }

    public void OpenMonsterTab()
    {
        RequestMainTab(EncyclopediaMainTab.Monster);
    }

    public void OpenBossTab()
    {
        RequestMainTab(EncyclopediaMainTab.Boss);
    }

    public void OpenWeaponSubTab()
    {
        currentMainTab = EncyclopediaMainTab.Item;
        RefreshMainTabState();
        itemTab?.OpenWeaponSubTab();
    }

    public void OpenRelicSubTab()
    {
        currentMainTab = EncyclopediaMainTab.Item;
        RefreshMainTabState();
        itemTab?.OpenRelicSubTab();
    }

    public void OpenConsumableSubTab()
    {
        currentMainTab = EncyclopediaMainTab.Item;
        RefreshMainTabState();
        itemTab?.OpenConsumableSubTab();
    }

    public void RequestPreviousPage()
    {
        itemTab?.RequestPreviousPage();
    }

    public void RequestNextPage()
    {
        itemTab?.RequestNextPage();
    }

    private void OpenUIInternal()
    {
        SetScreenActiveRootActive(true);
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        SoundPlaybackUtility.Play(OpenDictionarySound, sourceObject: this);

        InitializeRuntimeReferences();
        BindListeners();

        if (resetToItemWeaponOnOpen || !IsMainTabEnabled(currentMainTab))
            currentMainTab = EncyclopediaMainTab.Item;

        closePresentationRequested = false;
        closePresentationComplete = false;
        SetInteractionEnabled(false);
        RefreshMainTabState();

        if (bookPresentation != null && playBookOpenOnOpen)
        {
            bookPresentation.PlayOpen(
                ShowOpenContent,
                () => SetInteractionEnabled(true));
            return;
        }

        if (rootSlideFadePresentation != null && playRootSlideFadePresentation)
            rootSlideFadePresentation.PlayOpen(PlayImmediateOpenContent);
        else
            PlayImmediateOpenContent();
    }

    private void PlayImmediateOpenContent()
    {
        ShowOpenContent();

        if (revealPresentation != null)
            revealPresentation.PlayFirstOpen(() => SetInteractionEnabled(true));
        else
            SetInteractionEnabled(true);
    }

    private void ShowOpenContent()
    {
        if (currentMainTab != EncyclopediaMainTab.Item)
            currentMainTab = EncyclopediaMainTab.Item;

        RefreshMainTabState();

        if (itemTab != null)
        {
            itemTab.SetBookPresentation(bookPresentation);
            itemTab.ShowDefault();
            return;
        }

        WarnMissingItemTab();
    }

    private void RequestMainTab(EncyclopediaMainTab tab)
    {
        if (!IsMainTabEnabled(tab))
            return;

        if (tab == currentMainTab)
        {
            if (tab == EncyclopediaMainTab.Item)
                itemTab?.ShowCurrent();

            return;
        }

        SetInteractionEnabled(false);

        void Swap()
        {
            currentMainTab = tab;
            RefreshMainTabState();

            if (tab == EncyclopediaMainTab.Item)
                itemTab?.ShowCurrent();
            else
                itemTab?.Clear();
        }

        if (bookPresentation != null && gameObject.activeInHierarchy)
            bookPresentation.PlayRightPageTurn(Swap, () => SetInteractionEnabled(true));
        else
        {
            Swap();
            SetInteractionEnabled(true);
        }
    }

    private void PlayClosePresentationThenPop()
    {
        if (bookPresentation != null && playBookCloseOnClose && bookPresentation.CanPlayClose)
        {
            bookPresentation.PlayClose(PlayRootCloseThenPop);
            return;
        }

        PlayRootCloseThenPop();
    }

    private void PlayRootCloseThenPop()
    {
        if (rootSlideFadePresentation != null && playRootSlideFadePresentation && (bookPresentation == null || !playBookCloseOnClose))
        {
            rootSlideFadePresentation.DeactivateAfterClose = false;
            rootSlideFadePresentation.PlayClose(FinishDeferredClose);
            return;
        }

        FinishDeferredClose();
    }

    private void FinishDeferredClose()
    {
        closePresentationComplete = true;
        SoundPlaybackUtility.Play(CloseDictionarySound, sourceObject: this);

        if (UIManager.Instance != null)
            UIManager.Instance.PopUI(this);
        else
            CloseUI();
    }

    private bool CanPlayClosePresentation()
    {
        if (!IsScreenActiveInHierarchy())
            return false;

        if (bookPresentation != null && playBookCloseOnClose && bookPresentation.CanPlayClose)
            return true;

        return rootSlideFadePresentation != null && playRootSlideFadePresentation;
    }

    private bool IsMainTabEnabled(EncyclopediaMainTab tab)
    {
        return tab switch
        {
            EncyclopediaMainTab.Item => true,
            EncyclopediaMainTab.Monster => enableMonsterCategory,
            EncyclopediaMainTab.Boss => enableBossCategory,
            _ => false
        };
    }

    private void RefreshMainTabState()
    {
        SetMainTabState(itemMainTabButton, itemMainSelectedMarker, available: true, selected: currentMainTab == EncyclopediaMainTab.Item);
        SetMainTabState(monsterMainTabButton, monsterMainSelectedMarker, available: enableMonsterCategory, selected: currentMainTab == EncyclopediaMainTab.Monster);
        SetMainTabState(bossMainTabButton, bossMainSelectedMarker, available: enableBossCategory, selected: currentMainTab == EncyclopediaMainTab.Boss);
    }

    private void SetInteractionEnabled(bool enabled)
    {
        if (canvasGroup != null)
        {
            canvasGroup.interactable = enabled;
            canvasGroup.blocksRaycasts = enabled;
        }

        if (closeButton != null)
            closeButton.interactable = enabled;

        itemTab?.SetInteractionEnabled(enabled);

        if (itemMainTabButton != null)
            itemMainTabButton.interactable = enabled;
        if (monsterMainTabButton != null)
            monsterMainTabButton.interactable = enabled && enableMonsterCategory;
        if (bossMainTabButton != null)
            bossMainTabButton.interactable = enabled && enableBossCategory;
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;

        if (closeButton != null)
            closeButton.onClick.AddListener(RequestClose);

        if (itemMainTabButton != null)
            itemMainTabButton.onClick.AddListener(OpenItemTab);
        if (monsterMainTabButton != null)
            monsterMainTabButton.onClick.AddListener(OpenMonsterTab);
        if (bossMainTabButton != null)
            bossMainTabButton.onClick.AddListener(OpenBossTab);

        itemTab?.BindListeners();

        listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (!listenersBound)
            return;

        if (closeButton != null)
            closeButton.onClick.RemoveListener(RequestClose);

        if (itemMainTabButton != null)
            itemMainTabButton.onClick.RemoveListener(OpenItemTab);
        if (monsterMainTabButton != null)
            monsterMainTabButton.onClick.RemoveListener(OpenMonsterTab);
        if (bossMainTabButton != null)
            bossMainTabButton.onClick.RemoveListener(OpenBossTab);

        itemTab?.UnbindListeners();

        listenersBound = false;
    }

    private void ResolveReferences()
    {
        ResolveScreenActiveRoot();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (closeButton == null)
            closeButton = EncyclopediaReferenceResolver.FindComponent<Button>(transform, "CloseButton", "Close");

        if (revealPresentation == null)
            revealPresentation = GetComponentInChildren<BookPixelRevealPresentation>(true);

        if (bookPresentation == null)
            bookPresentation = GetComponentInChildren<EncyclopediaBookPresentation>(true);

        if (rootSlideFadePresentation == null)
            rootSlideFadePresentation = GetComponent<UISlideFadePresentation>();

        ResolveItemTabReferences();
        ResolveRightPageTabReferences();

        if (itemTab != null)
        {
            itemTab.SetBookPresentation(bookPresentation);
            itemTab.ResolveReferences();
        }
    }

    private void InitializeRuntimeReferences()
    {
        EnsureRuntimeActiveBoundary();

        if (bookPresentation == null && playBookOpenOnOpen && !warnedMissingBookPresentation)
        {
            warnedMissingBookPresentation = true;
            Debug.LogWarning("[EncyclopediaScreen] EncyclopediaBookPresentation is not assigned. Book open/page/close animation will use immediate fallback.", this);
        }

        if (screenActiveRoot == null && !warnedMissingScreenActiveRoot)
        {
            warnedMissingScreenActiveRoot = true;
            Debug.LogWarning("[EncyclopediaScreen] screenActiveRoot is not assigned. Wire it to EncyclopediaUI so DimPanel and Book share one active boundary.", this);
        }

        if (canvasGroup == null && !warnedMissingCanvasGroup)
        {
            warnedMissingCanvasGroup = true;
            Debug.LogWarning("[EncyclopediaScreen] CanvasGroup is not assigned. Interaction lock during book animation may not block raycasts.", this);
        }

        if (itemTab != null)
            itemTab.SetBookPresentation(bookPresentation);
        else
            WarnMissingItemTab();
    }

    private void ResolveItemTabReferences()
    {
        if (itemTab == null)
            itemTab = GetComponentInChildren<EncyclopediaItemTab>(true);
    }

    private void ResolveRightPageTabReferences()
    {
        Transform rightPage = EncyclopediaReferenceResolver.FindTransform(transform, "RightPage");
        Transform searchRoot = rightPage != null ? rightPage : transform;

        if (itemMainTabButton == null)
            itemMainTabButton = EncyclopediaReferenceResolver.FindComponent<Button>(searchRoot, "ItemTab", "ItemTabButton", "Item", "ItemButton");

        if (monsterMainTabButton == null)
            monsterMainTabButton = EncyclopediaReferenceResolver.FindComponent<Button>(searchRoot, "MonsterTab", "MonsterTabButton", "Monster", "MonsterButton");

        if (bossMainTabButton == null)
            bossMainTabButton = EncyclopediaReferenceResolver.FindComponent<Button>(searchRoot, "BossTab", "BossTabButton", "Boss", "BossButton");

        if (itemMainSelectedMarker == null)
            itemMainSelectedMarker = EncyclopediaReferenceResolver.FindMarker(searchRoot, "ItemTab") ??
                EncyclopediaReferenceResolver.FindMarker(searchRoot, "Item");

        if (monsterMainSelectedMarker == null)
            monsterMainSelectedMarker = EncyclopediaReferenceResolver.FindMarker(searchRoot, "MonsterTab") ??
                EncyclopediaReferenceResolver.FindMarker(searchRoot, "Monster");

        if (bossMainSelectedMarker == null)
            bossMainSelectedMarker = EncyclopediaReferenceResolver.FindMarker(searchRoot, "BossTab") ??
                EncyclopediaReferenceResolver.FindMarker(searchRoot, "Boss");
    }

    private void CloseIfActiveAtRuntimeStart()
    {
        if (!Application.isPlaying || !closeOnRuntimeAwake || openingViaOpenUI)
            return;

        SnapClosedState();

        SetScreenActiveRootActive(false);
    }

    private void SnapClosedState()
    {
        revealPresentation?.CancelAndHide();
        bookPresentation?.SnapClosed();
        rootSlideFadePresentation?.SnapClosed(deactivate: false);
        itemTab?.Clear();
        closePresentationRequested = false;
        closePresentationComplete = false;
    }

    private void WarnMissingItemTab()
    {
        if (warnedMissingItemTab)
            return;

        warnedMissingItemTab = true;
        Debug.LogWarning("[EncyclopediaScreen] EncyclopediaItemTab is not assigned. Item tab UI cannot be populated.", this);
    }

    private void SetMainTabState(Button button, GameObject selectedMarker, bool available, bool selected)
    {
        if (button != null)
        {
            button.gameObject.SetActive(available || !hideDisabledCategoryTabs);
            button.interactable = available && (canvasGroup == null || canvasGroup.interactable);
        }

        SetActive(selectedMarker, available && selected);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }

    private void ResolveScreenActiveRoot()
    {
        if (screenActiveRoot != null)
            return;

        Transform current = transform;
        while (current != null)
        {
            if (string.Equals(current.name, "EncyclopediaUI", System.StringComparison.OrdinalIgnoreCase))
            {
                screenActiveRoot = current.gameObject;
                return;
            }

            current = current.parent;
        }

        screenActiveRoot = gameObject;
    }

    private void SetScreenActiveRootActive(bool active)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            ResolveScreenActiveRoot();
#endif
        if (Application.isPlaying)
            EnsureRuntimeActiveBoundary();

        GameObject activeRoot = screenActiveRoot != null ? screenActiveRoot : gameObject;
        if (active)
        {
            if (!activeRoot.activeSelf)
                activeRoot.SetActive(true);

            if (activeRoot != gameObject && !gameObject.activeSelf)
                gameObject.SetActive(true);

            return;
        }

        if (activeRoot.activeSelf)
            activeRoot.SetActive(false);
        else if (activeRoot != gameObject && gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private bool GetScreenActiveSelf()
    {
        GameObject activeRoot = screenActiveRoot != null ? screenActiveRoot : gameObject;
        return activeRoot.activeSelf;
    }

    private bool IsScreenActiveInHierarchy()
    {
        GameObject activeRoot = screenActiveRoot != null ? screenActiveRoot : gameObject;
        return activeRoot.activeInHierarchy && gameObject.activeInHierarchy;
    }

    private void EnsureRuntimeActiveBoundary()
    {
        if (screenActiveRoot != null)
            return;

        Transform current = transform;
        while (current != null)
        {
            if (string.Equals(current.name, "EncyclopediaUI", System.StringComparison.OrdinalIgnoreCase))
            {
                screenActiveRoot = current.gameObject;
                return;
            }

            current = current.parent;
        }
    }
}

internal static class EncyclopediaReferenceResolver
{
    public static Transform FindChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && string.Equals(child.name, childName, System.StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return null;
    }

    public static T FindComponent<T>(Transform root, params string[] childNames) where T : Component
    {
        if (root == null || childNames == null)
            return null;

        for (int i = 0; i < childNames.Length; i++)
        {
            Transform child = FindChild(root, childNames[i]);
            if (child == null)
                continue;

            T component = child.GetComponent<T>();
            if (component != null)
                return component;
        }

        return null;
    }

    public static GameObject FindGameObject(Transform root, params string[] childNames)
    {
        Transform child = FindTransform(root, childNames);
        return child != null ? child.gameObject : null;
    }

    public static Transform FindTransform(Transform root, params string[] childNames)
    {
        if (root == null || childNames == null)
            return null;

        for (int i = 0; i < childNames.Length; i++)
        {
            Transform child = FindChild(root, childNames[i]);
            if (child != null)
                return child;
        }

        return null;
    }

    public static T FindComponentUnderParent<T>(Transform root, string parentName, params string[] childNames) where T : Component
    {
        Transform parent = FindChild(root, parentName);
        if (parent == null)
            return null;

        T component = FindComponent<T>(parent, childNames);
        if (component != null)
            return component;

        return parent.GetComponentInChildren<T>(true);
    }

    public static GameObject FindMarker(Transform root, string ownerName)
    {
        Transform owner = FindChild(root, ownerName);
        if (owner == null)
            return null;

        return FindGameObject(owner, "SelectedMarker", "SelectMarker", "Selected", "Selection", "Highlight", "Highlighter");
    }
}
