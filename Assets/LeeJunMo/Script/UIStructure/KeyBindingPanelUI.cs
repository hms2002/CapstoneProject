using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class KeyBindingPanelUI : MonoBehaviour, IStackableUI, ICloseRequestHandler
{
    private const int SystemCursorPriority = 300;

    public static KeyBindingPanelUI Instance { get; private set; }

    [Header("List")]
    [SerializeField] private Transform rowContainer;
    [SerializeField] private KeyBindingRowUI rowPrefab;
    [SerializeField] private List<InputActionId> actionOrder = new();

    [Header("Guide")]
    [FormerlySerializedAs("guidePanel")]
    [SerializeField] private GameObject guideRoot;
    [SerializeField] private TMP_Text guideText;
    [SerializeField] private GameObject guideButtonGroup;
    [SerializeField] private Button confirmSwapButton;
    [SerializeField] private Button cancelSwapButton;

    [Header("Buttons")]
    [SerializeField] private Button applyButton;
    [SerializeField] private Button resetAllButton;
    [SerializeField] private Button closeButton;

    private readonly List<KeyBindingRowUI> spawnedRows = new();
    private readonly Dictionary<InputActionId, InputBinding> workingBindings = new();
    private readonly Dictionary<InputActionId, InputBinding> savedBindings = new();

    private KeyBindingRowUI listeningRow;
    private InputActionId listeningAction;
    private bool listeningSecondary;
    private int listeningStartFrame = -1;
    private bool listenersBound;

    private bool awaitingSwapConfirmation;
    private bool awaitingCloseConfirmation;
    private bool resumeListeningOnConflictCancel;
    private Dictionary<InputActionId, InputBinding> pendingPreviewBindings;

    public bool IsActive => gameObject.activeSelf;
    public bool CanCloseOnEscape => listeningRow == null;
    public UIOpenGroup OpenGroup => UIOpenGroup.Overlay;
    public UIOpenGroup BlockedOpenGroups => UIOpenGroup.None;
    public UIGameplayLockProfile GameplayLockProfile => UIGameplayLockProfile.FreezeAndBlockControl;

    public static KeyBindingPanelUI EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        KeyBindingPanelUI[] existing = Resources.FindObjectsOfTypeAll<KeyBindingPanelUI>();
        for (int i = 0; i < existing.Length; i++)
        {
            KeyBindingPanelUI candidate = existing[i];
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
        HideGuide();
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!gameObject.activeInHierarchy || listeningRow == null)
            return;

        if (awaitingSwapConfirmation)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                CancelRebind();

            return;
        }

        if (Time.frameCount == listeningStartFrame)
            return;

        KeyCode key = ReadPressedKey();
        if (key == KeyCode.None)
            return;

        if (key == KeyCode.Escape)
        {
            CancelRebind();
            return;
        }

        InputBindingService input = InputBindingService.EnsureInstance();
        if (!input.IsSupportedKeyboardBindingKey(key))
        {
            ShowGuide("지원하지 않는 키입니다.\n다른 키를 입력해 주세요.", showButtons: false);
            return;
        }

        InputBinding previewTarget = GetWorkingBinding(listeningAction);
        if (listeningSecondary)
            previewTarget.secondary = key;
        else
            previewTarget.primary = key;

        if (TryBuildPreviewForBindingChange(listeningAction, previewTarget, out Dictionary<InputActionId, InputBinding> previewBindings, out HashSet<InputActionId> conflicts))
        {
            if (conflicts.Count > 0)
            {
                BeginConflictConfirmation(previewBindings, conflicts, resumeListeningAfterCancel: true);
                return;
            }

            ApplyWorkingBindings(previewBindings);
            FinishRebind();
            return;
        }

        ShowGuide("키 변경을 적용할 수 없습니다.\n다른 키를 입력해 주세요.", showButtons: false);
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
        MouseCursorService.Instance?.ClearDomain(this);
    }

    public void OpenUI()
    {
        RefreshCanvasParent();
        BindListeners();
        LoadWorkingBindingsFromService();
        EnsureRows();
        RefreshRows();
        HideGuide();
        gameObject.SetActive(true);
    }

    public void CloseUI()
    {
        CancelRebind();
        HideGuide();
        ClearWorkingState();
        gameObject.SetActive(false);
    }

    public void RefreshCanvasParent()
    {
        GlobalUIRoot.AdoptToCanvas(GlobalCanvasLayer.Popup, transform, false);
    }

    public bool TryHandleCloseRequest()
    {
        if (!gameObject.activeInHierarchy)
            return false;

        if (awaitingCloseConfirmation)
            return true;

        CancelTransientInputState();

        if (HasPendingChanges())
        {
            BeginCloseConfirmation();
            return true;
        }

        PerformClose();
        return true;
    }

    public void BeginRebind(KeyBindingRowUI row, InputActionId action, bool secondary)
    {
        if (row == null)
            return;

        CancelRebind();

        InputBindingService input = InputBindingService.EnsureInstance();
        if (secondary && !input.SupportsSecondaryBinding(action))
            return;

        listeningRow = row;
        listeningAction = action;
        listeningSecondary = secondary;
        listeningStartFrame = Time.frameCount;
        awaitingSwapConfirmation = false;
        resumeListeningOnConflictCancel = false;
        pendingPreviewBindings = null;

        row.SetListeningState(primary: !secondary, secondary: secondary);
        ShowGuide("할당하실 키를 입력해 주세요.", showButtons: false);
        UpdateBottomButtons();
    }

    public void ResetAction(InputActionId action)
    {
        if (!CanResetAction(action))
            return;

        if (listeningRow != null && listeningAction == action)
            CancelRebind();

        InputBindingService input = InputBindingService.EnsureInstance();
        InputBinding defaultBinding = input.GetDefaultBinding(action);

        if (!TryBuildPreviewForBindingChange(action, defaultBinding, out Dictionary<InputActionId, InputBinding> previewBindings, out HashSet<InputActionId> conflicts))
            return;

        if (conflicts.Count > 0)
        {
            BeginConflictConfirmation(previewBindings, conflicts, resumeListeningAfterCancel: false, isResetConflict: true);
            return;
        }

        ApplyWorkingBindings(previewBindings);
        RefreshRows();
    }

    public bool SupportsSecondaryBinding(InputActionId action)
    {
        return InputBindingService.EnsureInstance().SupportsSecondaryBinding(action);
    }

    public string GetActionLabel(InputActionId action)
    {
        return InputBindingService.EnsureInstance().GetActionLabel(action);
    }

    public string GetBindingDisplayLabel(InputActionId action, bool secondary = false)
    {
        if (secondary && !SupportsSecondaryBinding(action))
            return string.Empty;

        return InputGlyphDatabase.GetDisplayLabel(GetWorkingKey(action, secondary));
    }

    public Sprite GetBindingIcon(InputActionId action, bool secondary = false)
    {
        if (secondary && !SupportsSecondaryBinding(action))
            return null;

        return InputGlyphDatabase.GetIcon(GetWorkingKey(action, secondary));
    }

    public bool CanResetAction(InputActionId action)
    {
        return !AreBindingsEqual(GetWorkingBinding(action), InputBindingService.EnsureInstance().GetDefaultBinding(action));
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;

        if (applyButton != null)
            applyButton.onClick.AddListener(HandleApply);

        if (resetAllButton != null)
            resetAllButton.onClick.AddListener(HandleResetAll);

        if (closeButton != null)
            closeButton.onClick.AddListener(HandleClose);

        if (confirmSwapButton != null)
            confirmSwapButton.onClick.AddListener(HandleConfirmSwap);

        if (cancelSwapButton != null)
            cancelSwapButton.onClick.AddListener(HandleCancelSwap);

        listenersBound = true;
    }

    private void EnsureRows()
    {
        if (rowContainer == null || rowPrefab == null)
            return;

        if (spawnedRows.Count > 0)
            return;

        IReadOnlyList<InputActionId> actions = ResolveActionOrder();
        for (int i = 0; i < actions.Count; i++)
        {
            KeyBindingRowUI row = Instantiate(rowPrefab, rowContainer);
            row.gameObject.SetActive(true);
            row.Bind(this, actions[i]);
            spawnedRows.Add(row);
        }
    }

    private IReadOnlyList<InputActionId> ResolveActionOrder()
    {
        if (actionOrder != null && actionOrder.Count > 0)
            return actionOrder;

        return InputBindingService.EnsureInstance().GetRemappableActions();
    }

    private void LoadWorkingBindingsFromService()
    {
        workingBindings.Clear();
        savedBindings.Clear();

        InputBindingService input = InputBindingService.EnsureInstance();
        IReadOnlyList<InputActionId> actions = input.GetRemappableActions();
        for (int i = 0; i < actions.Count; i++)
        {
            InputActionId action = actions[i];
            InputBinding binding = input.GetBinding(action);
            workingBindings[action] = binding;
            savedBindings[action] = binding;
        }
    }

    private void ClearWorkingState()
    {
        workingBindings.Clear();
        savedBindings.Clear();
        pendingPreviewBindings = null;
        resumeListeningOnConflictCancel = false;
    }

    private InputBinding GetWorkingBinding(InputActionId action)
    {
        if (workingBindings.TryGetValue(action, out InputBinding binding))
            return binding;

        InputBinding fallback = InputBindingService.EnsureInstance().GetBinding(action);
        workingBindings[action] = fallback;
        return fallback;
    }

    private KeyCode GetWorkingKey(InputActionId action, bool secondary)
    {
        InputBinding binding = GetWorkingBinding(action);
        return secondary ? binding.secondary : binding.primary;
    }

    private void SetWorkingKey(InputActionId action, bool secondary, KeyCode key)
    {
        InputBinding binding = GetWorkingBinding(action);
        if (secondary)
            binding.secondary = key;
        else
            binding.primary = key;

        if (!SupportsSecondaryBinding(action))
            binding.secondary = KeyCode.None;

        workingBindings[action] = binding;
    }

    private void ApplyWorkingBindings(Dictionary<InputActionId, InputBinding> source)
    {
        workingBindings.Clear();
        foreach (KeyValuePair<InputActionId, InputBinding> pair in source)
            workingBindings[pair.Key] = pair.Value;

        RefreshRows();
    }

    private void RefreshRows()
    {
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            if (spawnedRows[i] != null)
                spawnedRows[i].Refresh();
        }

        UpdateBottomButtons();
    }

    private void UpdateBottomButtons()
    {
        bool inputLocked = listeningRow != null || awaitingSwapConfirmation || awaitingCloseConfirmation;

        if (applyButton != null)
            applyButton.interactable = !inputLocked && HasPendingChanges();

        if (resetAllButton != null)
            resetAllButton.interactable = !inputLocked && HasAnyNonDefaultBinding();
    }

    private bool HasPendingChanges()
    {
        InputBindingService input = InputBindingService.EnsureInstance();
        IReadOnlyList<InputActionId> actions = input.GetRemappableActions();
        for (int i = 0; i < actions.Count; i++)
        {
            InputActionId action = actions[i];
            if (!savedBindings.TryGetValue(action, out InputBinding savedBinding))
                savedBinding = input.GetBinding(action);

            if (!AreBindingsEqual(GetWorkingBinding(action), savedBinding))
                return true;
        }

        return false;
    }

    private bool HasAnyNonDefaultBinding()
    {
        InputBindingService input = InputBindingService.EnsureInstance();
        IReadOnlyList<InputActionId> actions = input.GetRemappableActions();
        for (int i = 0; i < actions.Count; i++)
        {
            InputActionId action = actions[i];
            if (!AreBindingsEqual(GetWorkingBinding(action), input.GetDefaultBinding(action)))
                return true;
        }

        return false;
    }

    private void HandleApply()
    {
        if (!HasPendingChanges())
            return;

        CancelRebind();

        InputBindingService input = InputBindingService.EnsureInstance();
        IReadOnlyList<InputActionId> actions = input.GetRemappableActions();
        for (int i = 0; i < actions.Count; i++)
        {
            InputActionId action = actions[i];
            InputBinding binding = GetWorkingBinding(action);
            input.SetBinding(action, binding);
            savedBindings[action] = binding;
        }

        RefreshRows();
    }

    private void HandleResetAll()
    {
        if (!HasAnyNonDefaultBinding())
            return;

        CancelRebind();

        InputBindingService input = InputBindingService.EnsureInstance();
        IReadOnlyList<InputActionId> actions = input.GetRemappableActions();
        for (int i = 0; i < actions.Count; i++)
            workingBindings[actions[i]] = input.GetDefaultBinding(actions[i]);

        RefreshRows();
    }

    private void HandleClose()
    {
        TryHandleCloseRequest();
    }

    private void HandleConfirmSwap()
    {
        if (awaitingCloseConfirmation)
        {
            HandleApply();
            PerformClose();
            return;
        }

        if (!awaitingSwapConfirmation || pendingPreviewBindings == null)
            return;

        ApplyWorkingBindings(pendingPreviewBindings);
        pendingPreviewBindings = null;
        awaitingSwapConfirmation = false;
        resumeListeningOnConflictCancel = false;

        if (listeningRow != null)
            FinishRebind();
        else
            HideGuide();
    }

    private void HandleCancelSwap()
    {
        if (awaitingCloseConfirmation)
        {
            RevertWorkingBindingsToSaved();
            PerformClose();
            return;
        }

        if (!awaitingSwapConfirmation)
            return;

        awaitingSwapConfirmation = false;
        pendingPreviewBindings = null;

        if (resumeListeningOnConflictCancel && listeningRow != null)
        {
            resumeListeningOnConflictCancel = false;
            listeningStartFrame = Time.frameCount;
            ShowGuide("할당하실 키를 입력해 주세요.", showButtons: false);
            UpdateBottomButtons();
            return;
        }

        resumeListeningOnConflictCancel = false;
        HideGuide();
        RefreshRows();
    }

    private void BeginConflictConfirmation(
        Dictionary<InputActionId, InputBinding> previewBindings,
        HashSet<InputActionId> conflicts,
        bool resumeListeningAfterCancel,
        bool isResetConflict = false)
    {
        awaitingSwapConfirmation = true;
        resumeListeningOnConflictCancel = resumeListeningAfterCancel;
        pendingPreviewBindings = previewBindings;

        ShowGuide(BuildConflictMessage(conflicts, isResetConflict), showButtons: true);
        UpdateBottomButtons();
    }

    private void BeginCloseConfirmation()
    {
        awaitingCloseConfirmation = true;
        ShowGuide("변경사항이 있습니다.\n적용하시겠습니까?", showButtons: true);
        UpdateBottomButtons();
    }

    private string BuildConflictMessage(HashSet<InputActionId> conflicts, bool isResetConflict)
    {
        if (conflicts == null || conflicts.Count == 0)
            return "서로 교체하시겠습니까?";

        if (conflicts.Count == 1)
        {
            foreach (InputActionId action in conflicts)
            {
                string label = GetActionLabel(action);
                if (isResetConflict)
                    return $"기본 키가 현재 '{label}'에 할당되어 있습니다.\n서로 교체하시겠습니까?";

                return $"이 키는 현재 '{label}'에 할당되어 있습니다.\n서로 교체하시겠습니까?";
            }
        }

        return isResetConflict
            ? "기본 키가 현재 다른 동작에 할당되어 있습니다.\n서로 교체하시겠습니까?"
            : "이 키는 현재 다른 동작에 할당되어 있습니다.\n서로 교체하시겠습니까?";
    }

    private bool TryBuildPreviewForBindingChange(
        InputActionId targetAction,
        InputBinding targetBinding,
        out Dictionary<InputActionId, InputBinding> previewBindings,
        out HashSet<InputActionId> conflicts)
    {
        previewBindings = CloneBindings(workingBindings);
        conflicts = new HashSet<InputActionId>();

        if (!ApplySlotChange(previewBindings, targetAction, secondary: false, targetBinding.primary, conflicts))
            return false;

        if (SupportsSecondaryBinding(targetAction))
        {
            if (!ApplySlotChange(previewBindings, targetAction, secondary: true, targetBinding.secondary, conflicts))
                return false;
        }
        else
        {
            InputBinding binding = previewBindings[targetAction];
            binding.secondary = KeyCode.None;
            previewBindings[targetAction] = binding;
        }

        return true;
    }

    private bool ApplySlotChange(
        Dictionary<InputActionId, InputBinding> previewBindings,
        InputActionId targetAction,
        bool secondary,
        KeyCode newKey,
        HashSet<InputActionId> conflicts)
    {
        if (secondary && !SupportsSecondaryBinding(targetAction))
            return true;

        KeyCode currentKey = GetKey(previewBindings, targetAction, secondary);
        if (currentKey == newKey)
            return true;

        if (FindConflict(previewBindings, targetAction, secondary, newKey, out InputActionId conflictAction, out bool conflictSecondary))
        {
            conflicts.Add(conflictAction);
            SetKey(previewBindings, conflictAction, conflictSecondary, currentKey);
        }

        SetKey(previewBindings, targetAction, secondary, newKey);
        return true;
    }

    private bool FindConflict(
        Dictionary<InputActionId, InputBinding> bindings,
        InputActionId targetAction,
        bool targetSecondary,
        KeyCode key,
        out InputActionId conflictAction,
        out bool conflictSecondary)
    {
        if (key == KeyCode.None)
        {
            conflictAction = default;
            conflictSecondary = false;
            return false;
        }

        InputBindingService input = InputBindingService.EnsureInstance();
        IReadOnlyList<InputActionId> actions = input.GetRemappableActions();
        for (int i = 0; i < actions.Count; i++)
        {
            InputActionId action = actions[i];
            InputBinding binding = bindings[action];

            if (binding.primary == key && (!Equals(action, targetAction) || targetSecondary))
            {
                conflictAction = action;
                conflictSecondary = false;
                return true;
            }

            if (input.SupportsSecondaryBinding(action) &&
                binding.secondary == key &&
                binding.secondary != KeyCode.None &&
                (!Equals(action, targetAction) || !targetSecondary))
            {
                conflictAction = action;
                conflictSecondary = true;
                return true;
            }
        }

        conflictAction = default;
        conflictSecondary = false;
        return false;
    }

    private static Dictionary<InputActionId, InputBinding> CloneBindings(Dictionary<InputActionId, InputBinding> source)
    {
        Dictionary<InputActionId, InputBinding> clone = new(source.Count);
        foreach (KeyValuePair<InputActionId, InputBinding> pair in source)
            clone[pair.Key] = pair.Value;
        return clone;
    }

    private static KeyCode GetKey(Dictionary<InputActionId, InputBinding> bindings, InputActionId action, bool secondary)
    {
        if (!bindings.TryGetValue(action, out InputBinding binding))
            return KeyCode.None;

        return secondary ? binding.secondary : binding.primary;
    }

    private void SetKey(Dictionary<InputActionId, InputBinding> bindings, InputActionId action, bool secondary, KeyCode key)
    {
        InputBinding binding = bindings[action];
        if (secondary)
            binding.secondary = key;
        else
            binding.primary = key;

        if (!SupportsSecondaryBinding(action))
            binding.secondary = KeyCode.None;

        bindings[action] = binding;
    }

    private static bool AreBindingsEqual(InputBinding left, InputBinding right)
    {
        return left.primary == right.primary && left.secondary == right.secondary;
    }

    private void FinishRebind()
    {
        KeyBindingRowUI row = listeningRow;
        listeningRow = null;
        listeningSecondary = false;
        listeningStartFrame = -1;
        awaitingSwapConfirmation = false;
        awaitingCloseConfirmation = false;
        resumeListeningOnConflictCancel = false;
        pendingPreviewBindings = null;

        RefreshRows();
        HideGuide();

        if (row != null)
            row.Refresh();
    }

    private void CancelRebind()
    {
        bool hadActiveRebind = CancelTransientInputState();

        if (hadActiveRebind)
            RefreshRows();

        HideGuide();
    }

    private void ShowGuide(string message, bool showButtons)
    {
        if (guideText != null)
            guideText.text = message;

        if (guideButtonGroup != null)
            guideButtonGroup.SetActive(showButtons);

        if (guideRoot != null)
            guideRoot.SetActive(true);
    }

    private void HideGuide()
    {
        if (guideText != null)
            guideText.text = string.Empty;

        if (guideButtonGroup != null)
            guideButtonGroup.SetActive(false);

        if (guideRoot != null)
            guideRoot.SetActive(false);
    }

    private static KeyCode ReadPressedKey()
    {
        return InputKeyCompatibility.TryReadPressedKeyThisFrame(out KeyCode key)
            ? key
            : KeyCode.None;
    }

    private bool CancelTransientInputState()
    {
        bool hadTransientState = listeningRow != null || awaitingSwapConfirmation || awaitingCloseConfirmation;
        listeningRow = null;
        listeningSecondary = false;
        listeningStartFrame = -1;
        awaitingSwapConfirmation = false;
        awaitingCloseConfirmation = false;
        resumeListeningOnConflictCancel = false;
        pendingPreviewBindings = null;
        return hadTransientState;
    }

    private void RevertWorkingBindingsToSaved()
    {
        workingBindings.Clear();
        foreach (KeyValuePair<InputActionId, InputBinding> pair in savedBindings)
            workingBindings[pair.Key] = pair.Value;
    }

    private void PerformClose()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.PopUI(this);
        else
            CloseUI();
    }
}
