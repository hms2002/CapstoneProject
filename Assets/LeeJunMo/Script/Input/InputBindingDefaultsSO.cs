using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "Input/Input Binding Defaults", fileName = "InputBindingDefaults")]
public sealed class InputBindingDefaultsSO : ScriptableObject
{
    private const string ResourcePath = "InputBindingDefaults";
#if UNITY_EDITOR
    private const string EditorAssetPath = "Assets/LeeJunMo/Datas/Resources/InputBindingDefaults.asset";
#endif

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

    private static InputBindingDefaultsSO runtimeInstance;

    [SerializeField] private List<InputBindingEntry> defaultBindings = new();

    public static InputBindingDefaultsSO LoadOrCreate()
    {
        if (runtimeInstance != null)
            return runtimeInstance;

        runtimeInstance = Resources.Load<InputBindingDefaultsSO>(ResourcePath);
        if (runtimeInstance != null)
        {
            runtimeInstance.EnsureDefaultBindingEntries();
            return runtimeInstance;
        }

#if UNITY_EDITOR
        runtimeInstance = CreateOrRestoreEditorAsset();
        if (runtimeInstance != null)
            return runtimeInstance;
#endif

        runtimeInstance = CreateInstance<InputBindingDefaultsSO>();
        runtimeInstance.hideFlags = HideFlags.DontUnloadUnusedAsset;
        runtimeInstance.ResetToBuiltInDefaults();
        return runtimeInstance;
    }

    public IReadOnlyList<InputActionId> GetRemappableActions()
    {
        return RemappableActions;
    }

    public InputBinding GetDefaultBinding(InputActionId action)
    {
        EnsureDefaultBindingEntries();
        return TryGetSerializedEntry(action, out InputBindingEntry entry)
            ? NormalizeEntry(entry).ToBinding()
            : GetBuiltInDefaultBinding(action);
    }

    internal static bool SupportsSecondaryBinding(InputActionId action)
    {
        return action != InputActionId.DialogueAdvance;
    }

    [ContextMenu("Reset To Built-In Defaults")]
    public void ResetToBuiltInDefaults()
    {
        defaultBindings = new List<InputBindingEntry>(RemappableActions.Length);
        for (int i = 0; i < RemappableActions.Length; i++)
            defaultBindings.Add(GetBuiltInDefaultEntry(RemappableActions[i]));
    }

    private void Reset()
    {
        ResetToBuiltInDefaults();
    }

    private void OnValidate()
    {
        EnsureDefaultBindingEntries();
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

    private static InputBindingEntry NormalizeEntry(InputBindingEntry entry)
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

#if UNITY_EDITOR
    private static InputBindingDefaultsSO CreateOrRestoreEditorAsset()
    {
        InputBindingDefaultsSO defaults = AssetDatabase.LoadAssetAtPath<InputBindingDefaultsSO>(EditorAssetPath);
        bool created = false;

        if (defaults == null)
        {
            string directoryPath = Path.GetDirectoryName(EditorAssetPath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            defaults = CreateInstance<InputBindingDefaultsSO>();
            defaults.ResetToBuiltInDefaults();
            AssetDatabase.CreateAsset(defaults, EditorAssetPath);
            created = true;
        }

        if (defaults == null)
            return null;

        defaults.EnsureDefaultBindingEntries();

        if (created)
        {
            EditorUtility.SetDirty(defaults);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        InputBindingDefaultsSO loadedFromResources = Resources.Load<InputBindingDefaultsSO>(ResourcePath);
        return loadedFromResources != null ? loadedFromResources : defaults;
    }
#endif
}
