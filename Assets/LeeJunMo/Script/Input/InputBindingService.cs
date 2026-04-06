using System;
using System.Collections.Generic;
using UnityEngine;

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

    private static readonly InputActionId[] RemappableActions =
    {
        InputActionId.MoveUp,
        InputActionId.MoveDown,
        InputActionId.MoveLeft,
        InputActionId.MoveRight,
        InputActionId.PrimaryAttack,
        InputActionId.Interact,
        InputActionId.Dash,
        InputActionId.Skill1,
        InputActionId.Skill2,
        InputActionId.SwapWeapon,
        InputActionId.ConsumableSlot1,
        InputActionId.ConsumableSlot2,
        InputActionId.ConsumableSlot3,
        InputActionId.ConsumableSlot4,
        InputActionId.InventoryToggle,
        InputActionId.DialogueAdvance,
    };

    public static InputBindingService Instance { get; private set; }

    [Header("Default Bindings")]
    [SerializeField] private List<InputBindingEntry> defaultBindings = new();

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
        return Matches(action, Input.GetKeyDown);
    }

    public bool WasReleasedThisFrame(InputActionId action)
    {
        return Matches(action, Input.GetKeyUp);
    }

    public bool IsPressed(InputActionId action)
    {
        return Matches(action, Input.GetKey);
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
        return action != InputActionId.DialogueAdvance;
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

    public InputGlyphPresentation GetKeyGlyph(KeyCode key)
    {
        return InputGlyphDatabase.Resolve(key);
    }

    public Sprite GetBindingIcon(InputActionId action, bool secondary = false)
    {
        return GetBindingGlyph(action, secondary).Icon;
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
        for (int i = 0; i < RemappableActions.Length; i++)
            SetBindingInternal(RemappableActions[i], GetConfiguredDefaultBinding(RemappableActions[i]));
    }

    public IReadOnlyList<InputActionId> GetRemappableActions()
    {
        return RemappableActions;
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

        for (int i = 0; i < RemappableActions.Length; i++)
        {
            InputActionId action = RemappableActions[i];
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
        EnsureDefaultBindingEntries();
        EnsureInitialized();
    }

    private void Reset()
    {
        EnsureDefaultBindingEntries();
    }

    private void OnValidate()
    {
        EnsureDefaultBindingEntries();
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
        EnsureDefaultBindingEntries();
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

    private void LoadBindings()
    {
        bindings.Clear();
        for (int i = 0; i < RemappableActions.Length; i++)
        {
            InputActionId action = RemappableActions[i];
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

    private void EnsureDefaultBindingEntries()
    {
        if (defaultBindings == null)
            defaultBindings = new List<InputBindingEntry>();

        List<InputBindingEntry> normalized = new(RemappableActions.Length);
        for (int i = 0; i < RemappableActions.Length; i++)
        {
            InputActionId action = RemappableActions[i];
            if (TryGetSerializedEntry(action, out InputBindingEntry existing))
                normalized.Add(NormalizeEntry(existing));
            else
                normalized.Add(GetBuiltInDefaultEntry(action));
        }

        defaultBindings = normalized;
    }

    private bool TryGetSerializedEntry(InputActionId action, out InputBindingEntry entry)
    {
        if (defaultBindings != null)
        {
            for (int i = 0; i < defaultBindings.Count; i++)
            {
                if (defaultBindings[i].action == action)
                {
                    entry = defaultBindings[i];
                    return true;
                }
            }
        }

        entry = default;
        return false;
    }

    private InputBinding GetConfiguredDefaultBinding(InputActionId action)
    {
        return TryGetSerializedEntry(action, out InputBindingEntry entry)
            ? NormalizeEntry(entry).ToBinding()
            : GetBuiltInDefaultBinding(action);
    }

    [ContextMenu("Reset Default Binding Entries")]
    private void ResetDefaultBindingEntries()
    {
        defaultBindings = new List<InputBindingEntry>(RemappableActions.Length);
        for (int i = 0; i < RemappableActions.Length; i++)
            defaultBindings.Add(GetBuiltInDefaultEntry(RemappableActions[i]));
    }

    [ContextMenu("Clear Saved Binding Overrides")]
    private void ClearSavedBindingOverrides()
    {
        for (int i = 0; i < RemappableActions.Length; i++)
        {
            InputActionId action = RemappableActions[i];
            PlayerPrefs.DeleteKey(GetPrimaryPrefKey(action));
            PlayerPrefs.DeleteKey(GetSecondaryPrefKey(action));
        }

        initialized = false;
        EnsureInitialized();
    }

    private InputBindingEntry NormalizeEntry(InputBindingEntry entry)
    {
        if (!SupportsSecondaryBinding(entry.action))
            entry.secondary = KeyCode.None;

        return entry;
    }

    private static InputBindingEntry GetBuiltInDefaultEntry(InputActionId action)
    {
        InputBinding binding = GetBuiltInDefaultBinding(action);
        return new InputBindingEntry(action, binding.primary, binding.secondary);
    }

    private static InputBinding GetBuiltInDefaultBinding(InputActionId action)
    {
        return action switch
        {
            InputActionId.MoveUp => new InputBinding(KeyCode.W, KeyCode.UpArrow),
            InputActionId.MoveDown => new InputBinding(KeyCode.S, KeyCode.DownArrow),
            InputActionId.MoveLeft => new InputBinding(KeyCode.A, KeyCode.LeftArrow),
            InputActionId.MoveRight => new InputBinding(KeyCode.D, KeyCode.RightArrow),
            InputActionId.PrimaryAttack => new InputBinding(KeyCode.Mouse0),
            InputActionId.Interact => new InputBinding(KeyCode.F),
            InputActionId.Dash => new InputBinding(KeyCode.Space),
            InputActionId.Skill1 => new InputBinding(KeyCode.Q),
            InputActionId.Skill2 => new InputBinding(KeyCode.E),
            InputActionId.SwapWeapon => new InputBinding(KeyCode.Tab),
            InputActionId.ConsumableSlot1 => new InputBinding(KeyCode.Alpha1),
            InputActionId.ConsumableSlot2 => new InputBinding(KeyCode.Alpha2),
            InputActionId.ConsumableSlot3 => new InputBinding(KeyCode.Alpha3),
            InputActionId.ConsumableSlot4 => new InputBinding(KeyCode.Alpha4),
            InputActionId.InventoryToggle => new InputBinding(KeyCode.I),
            InputActionId.DialogueAdvance => new InputBinding(KeyCode.Space),
            _ => new InputBinding(KeyCode.None),
        };
    }
}
