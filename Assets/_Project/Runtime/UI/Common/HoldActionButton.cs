using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
#endif

[DisallowMultipleComponent]
public sealed class HoldActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ISubmitHandler
{
    private enum HoldSource
    {
        None,
        Pointer,
        Keyboard,
        External
    }

    [Serializable]
    public sealed class FloatEvent : UnityEvent<float>
    {
    }

    [Header("Target")]
    [SerializeField] private Button button;
    [SerializeField] private HoldFillButtonView holdView;

    [Header("Hold")]
    [SerializeField, Min(0.01f)] private float holdSeconds = 0.75f;
    [SerializeField] private bool enablePointerHold = true;
    [SerializeField] private bool enableKeyboardHold;
    [SerializeField] private KeyCode holdKey = KeyCode.Space;
    [SerializeField] private bool startKeyboardHoldWhileKeyIsPressed = true;
    [SerializeField] private bool interactable = true;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool requireInteractable = true;
    [SerializeField] private bool cancelOnPointerExit = true;
    [SerializeField] private bool resetProgressOnComplete = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onHoldStarted;
    [SerializeField] private UnityEvent onHoldCanceled;
    [SerializeField] private UnityEvent onHoldCompleted;
    [SerializeField] private FloatEvent onProgressChanged;

    private float holdElapsed;
    private bool isHolding;
    private bool blockKeyboardRestartUntilRelease;
    private HoldSource activeHoldSource = HoldSource.None;

    public event Action HoldStarted;
    public event Action HoldCanceled;
    public event Action HoldCompleted;
    public event Action<float> ProgressChanged;

    public bool IsHolding => isHolding;
    public bool Interactable => interactable;
    public float Progress => holdSeconds > 0f ? Mathf.Clamp01(holdElapsed / holdSeconds) : 1f;
    public HoldFillButtonView HoldView
    {
        get
        {
            ResolveReferences();
            return holdView;
        }
    }

    private void Reset()
    {
        button = GetComponent<Button>();
        holdView = GetComponent<HoldFillButtonView>();
    }

    private void Awake()
    {
        ResolveReferences();
        if (!interactable && button != null)
            button.interactable = false;

        SetProgress(0f);
        ApplyInteractableVisual();
    }

    private void Update()
    {
        UpdateKeyboardHoldStart();

        if (!isHolding)
            return;

        if (activeHoldSource == HoldSource.Keyboard && !IsKeyboardHoldPressed())
        {
            CancelHold();
            return;
        }

        if (!CanUse())
        {
            CancelHold();
            return;
        }

        holdElapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float progress = Progress;
        SetProgress(progress);

        if (progress >= 1f)
            CompleteHold();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!enablePointerHold)
            return;

        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        BeginHold(HoldSource.Pointer);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isHolding || activeHoldSource != HoldSource.Pointer)
            return;

        CancelHold();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (cancelOnPointerExit && isHolding && activeHoldSource == HoldSource.Pointer)
            CancelHold();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (CanStartKeyboardHoldFromSubmit())
            BeginHold(HoldSource.Keyboard);
    }

    public void BeginHold()
    {
        BeginHold(HoldSource.External);
    }

    public void SetHoldSeconds(float seconds)
    {
        holdSeconds = Mathf.Max(0.01f, seconds);
        if (isHolding)
            SetProgress(Progress);
    }

    public void SetKeyboardHold(bool enabled, KeyCode key)
    {
        enableKeyboardHold = enabled;
        holdKey = key;

        if (!enableKeyboardHold && isHolding && activeHoldSource == HoldSource.Keyboard)
            CancelHold();
    }

    public void SetKeyboardHold(bool enabled, KeyCode key, bool startWhilePressed)
    {
        startKeyboardHoldWhileKeyIsPressed = startWhilePressed;
        SetKeyboardHold(enabled, key);
    }

    public void SetPointerHoldEnabled(bool enabled)
    {
        enablePointerHold = enabled;

        if (!enablePointerHold && isHolding && activeHoldSource == HoldSource.Pointer)
            CancelHold();
    }

    public void SetInteractable(bool value)
    {
        ResolveReferences();
        interactable = value;

        if (button != null && button.interactable != value)
            button.interactable = value;

        if (!interactable)
        {
            if (isHolding)
                CancelHold();
            else
                SetProgress(0f);
        }

        ApplyInteractableVisual();
    }

    private void BeginHold(HoldSource source)
    {
        if (!CanUse())
            return;
        if (isHolding)
            return;

        isHolding = true;
        activeHoldSource = source;
        holdElapsed = 0f;
        SetProgress(0f);
        HoldStarted?.Invoke();
        onHoldStarted?.Invoke();
    }

    public void CancelHold()
    {
        if (!isHolding)
            return;

        isHolding = false;
        activeHoldSource = HoldSource.None;
        holdElapsed = 0f;
        SetProgress(0f);
        HoldCanceled?.Invoke();
        onHoldCanceled?.Invoke();
    }

    public void ResetHold()
    {
        isHolding = false;
        activeHoldSource = HoldSource.None;
        blockKeyboardRestartUntilRelease = false;
        holdElapsed = 0f;
        SetProgress(0f);
    }

    private void CompleteHold()
    {
        SetProgress(1f);
        bool completedFromKeyboard = activeHoldSource == HoldSource.Keyboard;
        isHolding = false;
        activeHoldSource = HoldSource.None;

        HoldCompleted?.Invoke();
        onHoldCompleted?.Invoke();

        if (resetProgressOnComplete)
            ResetHold();

        if (completedFromKeyboard)
            blockKeyboardRestartUntilRelease = true;
    }

    private void UpdateKeyboardHoldStart()
    {
        if (!enableKeyboardHold)
            return;

        bool isPressed = IsKeyboardHoldPressed();
        if (!isPressed)
        {
            blockKeyboardRestartUntilRelease = false;
            return;
        }

        if (isHolding || blockKeyboardRestartUntilRelease)
            return;

        bool canStartFromHeldKey = startKeyboardHoldWhileKeyIsPressed && CanUse();
        if (WasKeyboardHoldPressedThisFrame() || canStartFromHeldKey)
            BeginHold(HoldSource.Keyboard);
    }

    private bool IsKeyboardHoldPressed()
    {
        return enableKeyboardHold &&
               (InputKeyCompatibility.IsPressed(holdKey) || IsSelectedSubmitHoldPressed());
    }

    private bool WasKeyboardHoldPressedThisFrame()
    {
        return enableKeyboardHold &&
               (InputKeyCompatibility.WasPressedThisFrame(holdKey) || WasSelectedSubmitPressedThisFrame());
    }

    private bool CanStartKeyboardHoldFromSubmit()
    {
        if (!enableKeyboardHold)
            return false;
        if (isHolding || blockKeyboardRestartUntilRelease)
            return false;
        if (!CanUse())
            return false;

        return InputKeyCompatibility.WasPressedThisFrame(holdKey) ||
               InputKeyCompatibility.IsPressed(holdKey) ||
               IsSelectedSubmitHoldPressed();
    }

    private void SetProgress(float normalized)
    {
        float value = Mathf.Clamp01(normalized);
        holdView?.SetProgress(value);
        ProgressChanged?.Invoke(value);
        onProgressChanged?.Invoke(value);
    }

    private bool CanUse()
    {
        // Serialized compatibility only: legacy Unity Button state must not gate hold input.
        _ = requireInteractable;
        return interactable;
    }

    private void ResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (holdView == null)
            holdView = GetComponent<HoldFillButtonView>();
    }

    private void OnDisable()
    {
        ResetHold();
    }

    private void ApplyInteractableVisual()
    {
        holdView?.SetInteractableVisual(interactable);
    }

    private bool IsSelectedSubmitHoldPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return TryGetSelectedSubmitAction(out InputAction submitAction) &&
               IsSubmitActionUsingHoldKey(submitAction) &&
               submitAction.IsPressed();
#else
        return false;
#endif
    }

    private bool WasSelectedSubmitPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return TryGetSelectedSubmitAction(out InputAction submitAction) &&
               IsSubmitActionUsingHoldKey(submitAction) &&
               submitAction.WasPressedThisFrame();
#else
        return false;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private bool TryGetSelectedSubmitAction(out InputAction submitAction)
    {
        submitAction = null;
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || eventSystem.currentSelectedGameObject != gameObject)
            return false;
        if (eventSystem.currentInputModule is not InputSystemUIInputModule inputModule)
            return false;

        submitAction = inputModule.submit != null ? inputModule.submit.action : null;
        return submitAction != null;
    }

    private bool IsSubmitActionUsingHoldKey(InputAction submitAction)
    {
        return submitAction != null &&
               InputKeyCompatibility.TryGetButtonControl(holdKey, out ButtonControl holdControl) &&
               IsSameInputControl(submitAction.activeControl, holdControl);
    }

    private static bool IsSameInputControl(InputControl actual, InputControl expected)
    {
        if (actual == null || expected == null)
            return false;
        if (ReferenceEquals(actual, expected))
            return true;

        return actual.device == expected.device && actual.path == expected.path;
    }
#endif
}
