using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

[Serializable]
public struct InputContextShortcutEntry
{
    public InputContextShortcutId shortcut;
    public KeyCode key;

    public InputContextShortcutEntry(InputContextShortcutId shortcut, KeyCode key)
    {
        this.shortcut = shortcut;
        this.key = key;
    }
}

[CreateAssetMenu(menuName = "Input/Input Context Shortcut Defaults", fileName = "InputContextShortcutDefaults")]
public sealed class InputContextShortcutDefaultsSO : ScriptableObject
{
    private const string ResourcePath = "InputContextShortcutDefaults";
#if UNITY_EDITOR
    private const string EditorAssetPath = "Assets/_Project/Resources/InputContextShortcutDefaults.asset";
#endif

    private static readonly InputContextShortcutId[] Shortcuts =
    {
        InputContextShortcutId.RelicPreviewPrevious,
        InputContextShortcutId.RelicPreviewNext,
        InputContextShortcutId.TooltipVariantNext,
    };

    private static InputContextShortcutDefaultsSO runtimeInstance;

    [SerializeField] private List<InputContextShortcutEntry> shortcuts = new();

    public static InputContextShortcutDefaultsSO LoadOrCreate()
    {
        if (runtimeInstance != null)
            return runtimeInstance;

        runtimeInstance = Resources.Load<InputContextShortcutDefaultsSO>(ResourcePath);
        if (runtimeInstance != null)
        {
            runtimeInstance.EnsureShortcutEntries();
            return runtimeInstance;
        }

#if UNITY_EDITOR
        runtimeInstance = CreateOrRestoreEditorAsset();
        if (runtimeInstance != null)
            return runtimeInstance;
#endif

        runtimeInstance = CreateInstance<InputContextShortcutDefaultsSO>();
        runtimeInstance.hideFlags = HideFlags.DontUnloadUnusedAsset;
        runtimeInstance.ResetToBuiltInDefaults();
        return runtimeInstance;
    }

    public KeyCode GetKey(InputContextShortcutId shortcut)
    {
        EnsureShortcutEntries();
        return TryGetSerializedEntry(shortcut, out InputContextShortcutEntry entry)
            ? entry.key
            : GetBuiltInDefaultKey(shortcut);
    }

    public bool WasPressedThisFrame(InputContextShortcutId shortcut)
    {
        return InputKeyCompatibility.WasPressedThisFrame(GetKey(shortcut));
    }

    public InputGlyphPresentation GetGlyph(InputContextShortcutId shortcut)
    {
        return InputGlyphDatabase.Resolve(GetKey(shortcut));
    }

    [ContextMenu("Reset To Built-In Defaults")]
    public void ResetToBuiltInDefaults()
    {
        shortcuts = new List<InputContextShortcutEntry>(Shortcuts.Length);
        for (int i = 0; i < Shortcuts.Length; i++)
            shortcuts.Add(new InputContextShortcutEntry(Shortcuts[i], GetBuiltInDefaultKey(Shortcuts[i])));
    }

    private void Reset()
    {
        ResetToBuiltInDefaults();
    }

    private void OnValidate()
    {
        EnsureShortcutEntries();
    }

    private void EnsureShortcutEntries()
    {
        if (shortcuts == null)
            shortcuts = new List<InputContextShortcutEntry>();

        List<InputContextShortcutEntry> normalized = new(Shortcuts.Length);
        for (int i = 0; i < Shortcuts.Length; i++)
        {
            InputContextShortcutId shortcut = Shortcuts[i];
            if (TryGetSerializedEntry(shortcut, out InputContextShortcutEntry existing))
                normalized.Add(existing);
            else
                normalized.Add(new InputContextShortcutEntry(shortcut, GetBuiltInDefaultKey(shortcut)));
        }

        shortcuts = normalized;
    }

    private bool TryGetSerializedEntry(InputContextShortcutId shortcut, out InputContextShortcutEntry entry)
    {
        if (shortcuts != null)
        {
            for (int i = 0; i < shortcuts.Count; i++)
            {
                if (shortcuts[i].shortcut == shortcut)
                {
                    entry = shortcuts[i];
                    return true;
                }
            }
        }

        entry = default;
        return false;
    }

    private static KeyCode GetBuiltInDefaultKey(InputContextShortcutId shortcut)
    {
        return shortcut switch
        {
            InputContextShortcutId.RelicPreviewPrevious => KeyCode.Q,
            InputContextShortcutId.RelicPreviewNext => KeyCode.E,
            InputContextShortcutId.TooltipVariantNext => KeyCode.BackQuote,
            _ => KeyCode.None,
        };
    }

#if UNITY_EDITOR
    private static InputContextShortcutDefaultsSO CreateOrRestoreEditorAsset()
    {
        InputContextShortcutDefaultsSO defaults = AssetDatabase.LoadAssetAtPath<InputContextShortcutDefaultsSO>(EditorAssetPath);
        bool created = false;

        if (defaults == null)
        {
            string directoryPath = Path.GetDirectoryName(EditorAssetPath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            defaults = CreateInstance<InputContextShortcutDefaultsSO>();
            defaults.ResetToBuiltInDefaults();
            AssetDatabase.CreateAsset(defaults, EditorAssetPath);
            created = true;
        }

        if (defaults == null)
            return null;

        defaults.EnsureShortcutEntries();

        if (created)
        {
            EditorUtility.SetDirty(defaults);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        InputContextShortcutDefaultsSO loadedFromResources = Resources.Load<InputContextShortcutDefaultsSO>(ResourcePath);
        return loadedFromResources != null ? loadedFromResources : defaults;
    }
#endif
}

