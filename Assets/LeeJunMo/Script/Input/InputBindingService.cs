using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

[Serializable]
public struct InputBinding
{
    public KeyCode primary;
    public KeyCode secondary;

    public InputBinding(KeyCode primary, KeyCode secondary = KeyCode.None)
    {
        this.primary = primary;
        this.secondary = secondary;
    }
}

internal static class InputKeyCompatibility
{
    public static bool IsPressed(KeyCode key)
    {
        if (key == KeyCode.None)
            return false;

        if (Input.GetKey(key))
            return true;

#if ENABLE_INPUT_SYSTEM
        return TryGetButtonControl(key, out ButtonControl control) && control.isPressed;
#else
        return false;
#endif
    }

    public static bool WasPressedThisFrame(KeyCode key)
    {
        if (key == KeyCode.None)
            return false;

        if (Input.GetKeyDown(key))
            return true;

#if ENABLE_INPUT_SYSTEM
        return TryGetButtonControl(key, out ButtonControl control) && control.wasPressedThisFrame;
#else
        return false;
#endif
    }

    public static bool WasReleasedThisFrame(KeyCode key)
    {
        if (key == KeyCode.None)
            return false;

        if (Input.GetKeyUp(key))
            return true;

#if ENABLE_INPUT_SYSTEM
        return TryGetButtonControl(key, out ButtonControl control) && control.wasReleasedThisFrame;
#else
        return false;
#endif
    }

    public static bool TryReadPressedKeyThisFrame(out KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (TryReadPressedKeyThisFrameFromInputSystem(out key))
            return true;
#endif

        Array values = Enum.GetValues(typeof(KeyCode));
        for (int i = 0; i < values.Length; i++)
        {
            KeyCode candidate = (KeyCode)values.GetValue(i);
            if (candidate != KeyCode.None && Input.GetKeyDown(candidate))
            {
                key = candidate;
                return true;
            }
        }

        key = KeyCode.None;
        return false;
    }

#if ENABLE_INPUT_SYSTEM
    private static bool TryReadPressedKeyThisFrameFromInputSystem(out KeyCode key)
    {
        Array values = Enum.GetValues(typeof(KeyCode));
        for (int i = 0; i < values.Length; i++)
        {
            KeyCode candidate = (KeyCode)values.GetValue(i);
            if (candidate == KeyCode.None)
                continue;

            if (TryGetButtonControl(candidate, out ButtonControl control) && control.wasPressedThisFrame)
            {
                key = candidate;
                return true;
            }
        }

        key = KeyCode.None;
        return false;
    }

    private static bool TryGetButtonControl(KeyCode key, out ButtonControl control)
    {
        if (TryGetMouseButtonControl(key, out control))
            return true;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            control = null;
            return false;
        }

        control = key switch
        {
            KeyCode.A => keyboard.aKey,
            KeyCode.B => keyboard.bKey,
            KeyCode.C => keyboard.cKey,
            KeyCode.D => keyboard.dKey,
            KeyCode.E => keyboard.eKey,
            KeyCode.F => keyboard.fKey,
            KeyCode.G => keyboard.gKey,
            KeyCode.H => keyboard.hKey,
            KeyCode.I => keyboard.iKey,
            KeyCode.J => keyboard.jKey,
            KeyCode.K => keyboard.kKey,
            KeyCode.L => keyboard.lKey,
            KeyCode.M => keyboard.mKey,
            KeyCode.N => keyboard.nKey,
            KeyCode.O => keyboard.oKey,
            KeyCode.P => keyboard.pKey,
            KeyCode.Q => keyboard.qKey,
            KeyCode.R => keyboard.rKey,
            KeyCode.S => keyboard.sKey,
            KeyCode.T => keyboard.tKey,
            KeyCode.U => keyboard.uKey,
            KeyCode.V => keyboard.vKey,
            KeyCode.W => keyboard.wKey,
            KeyCode.X => keyboard.xKey,
            KeyCode.Y => keyboard.yKey,
            KeyCode.Z => keyboard.zKey,
            KeyCode.Alpha0 => keyboard.digit0Key,
            KeyCode.Alpha1 => keyboard.digit1Key,
            KeyCode.Alpha2 => keyboard.digit2Key,
            KeyCode.Alpha3 => keyboard.digit3Key,
            KeyCode.Alpha4 => keyboard.digit4Key,
            KeyCode.Alpha5 => keyboard.digit5Key,
            KeyCode.Alpha6 => keyboard.digit6Key,
            KeyCode.Alpha7 => keyboard.digit7Key,
            KeyCode.Alpha8 => keyboard.digit8Key,
            KeyCode.Alpha9 => keyboard.digit9Key,
            KeyCode.Keypad0 => keyboard.numpad0Key,
            KeyCode.Keypad1 => keyboard.numpad1Key,
            KeyCode.Keypad2 => keyboard.numpad2Key,
            KeyCode.Keypad3 => keyboard.numpad3Key,
            KeyCode.Keypad4 => keyboard.numpad4Key,
            KeyCode.Keypad5 => keyboard.numpad5Key,
            KeyCode.Keypad6 => keyboard.numpad6Key,
            KeyCode.Keypad7 => keyboard.numpad7Key,
            KeyCode.Keypad8 => keyboard.numpad8Key,
            KeyCode.Keypad9 => keyboard.numpad9Key,
            KeyCode.UpArrow => keyboard.upArrowKey,
            KeyCode.DownArrow => keyboard.downArrowKey,
            KeyCode.LeftArrow => keyboard.leftArrowKey,
            KeyCode.RightArrow => keyboard.rightArrowKey,
            KeyCode.Space => keyboard.spaceKey,
            KeyCode.Tab => keyboard.tabKey,
            KeyCode.Return => keyboard.enterKey,
            KeyCode.Escape => keyboard.escapeKey,
            KeyCode.Backspace => keyboard.backspaceKey,
            KeyCode.Delete => keyboard.deleteKey,
            KeyCode.Insert => keyboard.insertKey,
            KeyCode.Home => keyboard.homeKey,
            KeyCode.End => keyboard.endKey,
            KeyCode.PageUp => keyboard.pageUpKey,
            KeyCode.PageDown => keyboard.pageDownKey,
            KeyCode.CapsLock => keyboard.capsLockKey,
            KeyCode.Numlock => keyboard.numLockKey,
            KeyCode.ScrollLock => keyboard.scrollLockKey,
            KeyCode.Print => keyboard.printScreenKey,
            KeyCode.Pause => keyboard.pauseKey,
            KeyCode.LeftShift => keyboard.leftShiftKey,
            KeyCode.RightShift => keyboard.rightShiftKey,
            KeyCode.LeftControl => keyboard.leftCtrlKey,
            KeyCode.RightControl => keyboard.rightCtrlKey,
            KeyCode.LeftAlt => keyboard.leftAltKey,
            KeyCode.RightAlt => keyboard.rightAltKey,
            KeyCode.BackQuote => keyboard.backquoteKey,
            KeyCode.Minus => keyboard.minusKey,
            KeyCode.Equals => keyboard.equalsKey,
            KeyCode.LeftBracket => keyboard.leftBracketKey,
            KeyCode.RightBracket => keyboard.rightBracketKey,
            KeyCode.Backslash => keyboard.backslashKey,
            KeyCode.Semicolon => keyboard.semicolonKey,
            KeyCode.Quote => keyboard.quoteKey,
            KeyCode.Comma => keyboard.commaKey,
            KeyCode.Period => keyboard.periodKey,
            KeyCode.Slash => keyboard.slashKey,
            KeyCode.KeypadPeriod => keyboard.numpadPeriodKey,
            KeyCode.KeypadDivide => keyboard.numpadDivideKey,
            KeyCode.KeypadMultiply => keyboard.numpadMultiplyKey,
            KeyCode.KeypadMinus => keyboard.numpadMinusKey,
            KeyCode.KeypadPlus => keyboard.numpadPlusKey,
            KeyCode.KeypadEnter => keyboard.numpadEnterKey,
            KeyCode.F1 => keyboard.f1Key,
            KeyCode.F2 => keyboard.f2Key,
            KeyCode.F3 => keyboard.f3Key,
            KeyCode.F4 => keyboard.f4Key,
            KeyCode.F5 => keyboard.f5Key,
            KeyCode.F6 => keyboard.f6Key,
            KeyCode.F7 => keyboard.f7Key,
            KeyCode.F8 => keyboard.f8Key,
            KeyCode.F9 => keyboard.f9Key,
            KeyCode.F10 => keyboard.f10Key,
            KeyCode.F11 => keyboard.f11Key,
            KeyCode.F12 => keyboard.f12Key,
            _ => null,
        };

        return control != null;
    }

    private static bool TryGetMouseButtonControl(KeyCode key, out ButtonControl control)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            control = null;
            return false;
        }

        control = key switch
        {
            KeyCode.Mouse0 => mouse.leftButton,
            KeyCode.Mouse1 => mouse.rightButton,
            KeyCode.Mouse2 => mouse.middleButton,
            KeyCode.Mouse3 => mouse.backButton,
            KeyCode.Mouse4 => mouse.forwardButton,
            _ => null,
        };

        return control != null;
    }
#endif
}

[Serializable]
public struct InputBindingEntry
{
    public InputActionId action;
    public KeyCode primary;
    public KeyCode secondary;

    public InputBindingEntry(InputActionId action, KeyCode primary, KeyCode secondary = KeyCode.None)
    {
        this.action = action;
        this.primary = primary;
        this.secondary = secondary;
    }

    public InputBinding ToBinding()
    {
        return new InputBinding(primary, secondary);
    }
}

[DefaultExecutionOrder(-950)]
public sealed class InputBindingService : MonoBehaviour
{
    private const string PrefKeyPrefix = "settings.input.";

    public static InputBindingService Instance { get; private set; }

    private readonly Dictionary<InputActionId, InputBinding> bindings = new();
    private bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static InputBindingService EnsureInstance()
    {
        if (Instance != null)
            return Instance;

#if UNITY_2023_1_OR_NEWER
        InputBindingService existing = FindAnyObjectByType<InputBindingService>();
#else
        InputBindingService existing = FindObjectOfType<InputBindingService>();
#endif
        if (existing != null)
        {
            Instance = existing;
            existing.EnsureInitialized();
            return existing;
        }

        GameObject root = new GameObject(nameof(InputBindingService));
        return root.AddComponent<InputBindingService>();
    }

    public bool WasPressedThisFrame(InputActionId action)
    {
        return Matches(action, InputKeyCompatibility.WasPressedThisFrame);
    }

    public bool WasPressedThisFrame(InputContextShortcutId shortcut)
    {
        return InputContextShortcutDefaultsSO.LoadOrCreate().WasPressedThisFrame(shortcut);
    }

    public bool WasReleasedThisFrame(InputActionId action)
    {
        return Matches(action, InputKeyCompatibility.WasReleasedThisFrame);
    }

    public bool IsPressed(InputActionId action)
    {
        return Matches(action, InputKeyCompatibility.IsPressed);
    }

    public Vector2 GetMoveVectorRaw()
    {
        EnsureInitialized();

        float x = 0f;
        float y = 0f;

        if (IsPressed(InputActionId.MoveLeft))
            x -= 1f;
        if (IsPressed(InputActionId.MoveRight))
            x += 1f;
        if (IsPressed(InputActionId.MoveDown))
            y -= 1f;
        if (IsPressed(InputActionId.MoveUp))
            y += 1f;

        return new Vector2(x, y);
    }

    public Vector2 GetMoveVectorNormalized()
    {
        Vector2 raw = GetMoveVectorRaw();
        return raw.sqrMagnitude > 0.0001f ? raw.normalized : Vector2.zero;
    }

    public Vector3 GetPointerScreenPosition()
    {
        return Input.mousePosition;
    }

    public Vector3 GetPointerWorldPosition(Camera camera, float z = 0f)
    {
        if (camera == null)
            return Vector3.zero;

        Vector3 world = camera.ScreenToWorldPoint(GetPointerScreenPosition());
        world.z = z;
        return world;
    }

    public InputBinding GetBinding(InputActionId action)
    {
        EnsureInitialized();
        return bindings.TryGetValue(action, out InputBinding binding)
            ? binding
            : GetConfiguredDefaultBinding(action);
    }

    public InputBinding GetDefaultBinding(InputActionId action)
    {
        EnsureInitialized();
        return GetConfiguredDefaultBinding(action);
    }

    public KeyCode GetPrimaryKey(InputActionId action)
    {
        return GetBinding(action).primary;
    }

    public KeyCode GetSecondaryKey(InputActionId action)
    {
        if (!SupportsSecondaryBinding(action))
            return KeyCode.None;

        return GetBinding(action).secondary;
    }

    public KeyCode GetKey(InputActionId action, bool secondary = false)
    {
        return secondary ? GetSecondaryKey(action) : GetPrimaryKey(action);
    }

    public bool SupportsSecondaryBinding(InputActionId action)
    {
        return InputBindingDefaultsSO.SupportsSecondaryBinding(action);
    }

    public string GetBindingDisplayLabel(InputActionId action, bool secondary = false)
    {
        if (secondary && !SupportsSecondaryBinding(action))
            return string.Empty;

        return InputGlyphDatabase.GetDisplayLabel(GetKey(action, secondary));
    }

    public string GetKeyDisplayLabel(KeyCode key)
    {
        return key switch
        {
            KeyCode.None => "-",
            KeyCode.Mouse0 => "LMB",
            KeyCode.Mouse1 => "RMB",
            KeyCode.Mouse2 => "MMB",
            KeyCode.Mouse3 => "Mouse4",
            KeyCode.Mouse4 => "Mouse5",
            KeyCode.Mouse5 => "Mouse6",
            KeyCode.Space => "Space",
            KeyCode.Tab => "Tab",
            KeyCode.Return => "Enter",
            KeyCode.LeftShift => "LShift",
            KeyCode.RightShift => "RShift",
            KeyCode.LeftControl => "LCtrl",
            KeyCode.RightControl => "RCtrl",
            KeyCode.LeftAlt => "LAlt",
            KeyCode.RightAlt => "RAlt",
            KeyCode.UpArrow => "↑",
            KeyCode.DownArrow => "↓",
            KeyCode.LeftArrow => "←",
            KeyCode.RightArrow => "→",
            KeyCode.Alpha0 => "0",
            KeyCode.Alpha1 => "1",
            KeyCode.Alpha2 => "2",
            KeyCode.Alpha3 => "3",
            KeyCode.Alpha4 => "4",
            KeyCode.Alpha5 => "5",
            KeyCode.Alpha6 => "6",
            KeyCode.Alpha7 => "7",
            KeyCode.Alpha8 => "8",
            KeyCode.Alpha9 => "9",
            _ => key.ToString(),
        };
    }

    public InputGlyphPresentation GetBindingGlyph(InputActionId action, bool secondary = false)
    {
        if (secondary && !SupportsSecondaryBinding(action))
            return InputGlyphDatabase.Resolve(KeyCode.None);

        return GetKeyGlyph(GetKey(action, secondary));
    }

    public InputGlyphPresentation GetContextShortcutGlyph(InputContextShortcutId shortcut)
    {
        return InputContextShortcutDefaultsSO.LoadOrCreate().GetGlyph(shortcut);
    }

    public InputGlyphPresentation GetKeyGlyph(KeyCode key)
    {
        return InputGlyphDatabase.Resolve(key);
    }

    public Sprite GetBindingIcon(InputActionId action, bool secondary = false)
    {
        return GetBindingGlyph(action, secondary).Icon;
    }

    public Sprite GetContextShortcutIcon(InputContextShortcutId shortcut)
    {
        return GetContextShortcutGlyph(shortcut).Icon;
    }

    public Sprite GetKeyIcon(KeyCode key)
    {
        return GetKeyGlyph(key).Icon;
    }

    public void SetPrimaryKey(InputActionId action, KeyCode key)
    {
        EnsureInitialized();
        InputBinding binding = GetBinding(action);
        binding.primary = key;
        SetBindingInternal(action, binding);
    }

    public void SetSecondaryKey(InputActionId action, KeyCode key)
    {
        if (!SupportsSecondaryBinding(action))
            return;

        EnsureInitialized();
        InputBinding binding = GetBinding(action);
        binding.secondary = key;
        SetBindingInternal(action, binding);
    }

    public void SetKey(InputActionId action, bool secondary, KeyCode key)
    {
        if (secondary)
            SetSecondaryKey(action, key);
        else
            SetPrimaryKey(action, key);
    }

    public void SetBinding(InputActionId action, InputBinding binding)
    {
        EnsureInitialized();
        SetBindingInternal(action, binding);
    }

    public void SwapBindings(InputActionId firstAction, bool firstSecondary, InputActionId secondAction, bool secondSecondary)
    {
        EnsureInitialized();

        KeyCode firstKey = GetKey(firstAction, firstSecondary);
        KeyCode secondKey = GetKey(secondAction, secondSecondary);

        SetKey(firstAction, firstSecondary, secondKey);
        SetKey(secondAction, secondSecondary, firstKey);
    }

    public void ResetBinding(InputActionId action)
    {
        EnsureInitialized();
        SetBindingInternal(action, GetConfiguredDefaultBinding(action));
    }

    public void ResetAllBindings()
    {
        EnsureInitialized();
        IReadOnlyList<InputActionId> actions = GetRemappableActions();
        for (int i = 0; i < actions.Count; i++)
            SetBindingInternal(actions[i], GetConfiguredDefaultBinding(actions[i]));
    }

    public IReadOnlyList<InputActionId> GetRemappableActions()
    {
        return InputBindingDefaultsSO.LoadOrCreate().GetRemappableActions();
    }

    public bool TryFindConflict(
        InputActionId targetAction,
        KeyCode key,
        bool targetSecondary,
        out InputActionId conflictingAction,
        out bool conflictingSecondary)
    {
        EnsureInitialized();

        if (key == KeyCode.None)
        {
            conflictingAction = default;
            conflictingSecondary = false;
            return false;
        }

        IReadOnlyList<InputActionId> actions = GetRemappableActions();
        for (int i = 0; i < actions.Count; i++)
        {
            InputActionId action = actions[i];
            InputBinding binding = GetBinding(action);

            if (binding.primary == key && (!Equals(action, targetAction) || targetSecondary))
            {
                conflictingAction = action;
                conflictingSecondary = false;
                return true;
            }

            if (SupportsSecondaryBinding(action) &&
                binding.secondary == key &&
                binding.secondary != KeyCode.None &&
                (!Equals(action, targetAction) || !targetSecondary))
            {
                conflictingAction = action;
                conflictingSecondary = true;
                return true;
            }
        }

        conflictingAction = default;
        conflictingSecondary = false;
        return false;
    }

    public string GetActionLabel(InputActionId action)
    {
        return action switch
        {
            InputActionId.MoveUp => "위로 이동",
            InputActionId.MoveDown => "아래로 이동",
            InputActionId.MoveLeft => "왼쪽으로 이동",
            InputActionId.MoveRight => "오른쪽으로 이동",
            InputActionId.PrimaryAttack => "공격",
            InputActionId.Interact => "상호작용",
            InputActionId.Dash => "대시",
            InputActionId.Skill1 => "스킬 1",
            InputActionId.Skill2 => "스킬 2",
            InputActionId.SwapWeapon => "무기 교체",
            InputActionId.ConsumableSlot1 => "소모품 1",
            InputActionId.ConsumableSlot2 => "소모품 2",
            InputActionId.ConsumableSlot3 => "소모품 3",
            InputActionId.ConsumableSlot4 => "소모품 4",
            InputActionId.InventoryToggle => "인벤토리",
            InputActionId.DialogueAdvance => "대화 넘기기",
            _ => action.ToString(),
        };
    }

    public bool IsSupportedKeyboardBindingKey(KeyCode key)
    {
        if (key == KeyCode.None)
            return false;

        string name = key.ToString();
        return !name.StartsWith("Joystick", StringComparison.Ordinal);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ApplyImePolicy();
        EnsureInitialized();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            ApplyImePolicy();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        initialized = true;
        LoadBindings();
    }

    private bool Matches(InputActionId action, Func<KeyCode, bool> matcher)
    {
        EnsureInitialized();

        InputBinding binding = GetBinding(action);
        if (binding.primary != KeyCode.None && matcher(binding.primary))
            return true;

        if (binding.secondary != KeyCode.None && matcher(binding.secondary))
            return true;

        if (action == InputActionId.DialogueAdvance)
        {
            InputBinding interactBinding = GetBinding(InputActionId.Interact);
            if (interactBinding.primary != KeyCode.None &&
                interactBinding.primary != binding.primary &&
                interactBinding.primary != binding.secondary &&
                matcher(interactBinding.primary))
            {
                return true;
            }

            if (interactBinding.secondary != KeyCode.None &&
                interactBinding.secondary != binding.primary &&
                interactBinding.secondary != binding.secondary &&
                matcher(interactBinding.secondary))
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyImePolicy()
    {
        Input.imeCompositionMode = IMECompositionMode.Off;
    }

    private void LoadBindings()
    {
        bindings.Clear();
        IReadOnlyList<InputActionId> actions = GetRemappableActions();
        for (int i = 0; i < actions.Count; i++)
        {
            InputActionId action = actions[i];
            InputBinding defaultBinding = GetConfiguredDefaultBinding(action);
            InputBinding loaded = new InputBinding(
                PlayerPrefs.HasKey(GetPrimaryPrefKey(action))
                    ? (KeyCode)PlayerPrefs.GetInt(GetPrimaryPrefKey(action))
                    : defaultBinding.primary,
                PlayerPrefs.HasKey(GetSecondaryPrefKey(action))
                    ? (KeyCode)PlayerPrefs.GetInt(GetSecondaryPrefKey(action))
                    : defaultBinding.secondary);
            bindings[action] = loaded;
        }
    }

    private void SetBindingInternal(InputActionId action, InputBinding binding)
    {
        if (!SupportsSecondaryBinding(action))
            binding.secondary = KeyCode.None;

        bindings[action] = binding;
        PlayerPrefs.SetInt(GetPrimaryPrefKey(action), (int)binding.primary);
        PlayerPrefs.SetInt(GetSecondaryPrefKey(action), (int)binding.secondary);
    }

    private static string GetPrimaryPrefKey(InputActionId action)
    {
        return $"{PrefKeyPrefix}{action}.primary";
    }

    private static string GetSecondaryPrefKey(InputActionId action)
    {
        return $"{PrefKeyPrefix}{action}.secondary";
    }

    private InputBinding GetConfiguredDefaultBinding(InputActionId action)
    {
        return InputBindingDefaultsSO.LoadOrCreate().GetDefaultBinding(action);
    }

    [ContextMenu("Clear Saved Binding Overrides")]
    private void ClearSavedBindingOverrides()
    {
        IReadOnlyList<InputActionId> actions = GetRemappableActions();
        for (int i = 0; i < actions.Count; i++)
        {
            InputActionId action = actions[i];
            PlayerPrefs.DeleteKey(GetPrimaryPrefKey(action));
            PlayerPrefs.DeleteKey(GetSecondaryPrefKey(action));
        }

        initialized = false;
        EnsureInitialized();
    }
}
