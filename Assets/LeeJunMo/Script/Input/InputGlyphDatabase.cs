using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct InputGlyphEntry
{
    public KeyCode key;
    public string displayLabel;
    public Sprite icon;

    public InputGlyphEntry(KeyCode key, string displayLabel, Sprite icon = null)
    {
        this.key = key;
        this.displayLabel = displayLabel;
        this.icon = icon;
    }
}

public readonly struct InputGlyphPresentation
{
    public InputGlyphPresentation(KeyCode key, string displayLabel, Sprite icon)
    {
        Key = key;
        DisplayLabel = displayLabel;
        Icon = icon;
    }

    public KeyCode Key { get; }
    public string DisplayLabel { get; }
    public Sprite Icon { get; }
    public bool HasIcon => Icon != null;
}

[CreateAssetMenu(menuName = "Input/Input Glyph Database", fileName = "InputGlyphDatabase")]
public sealed class InputGlyphDatabase : ScriptableObject
{
    private const string ResourcePath = "InputGlyphDatabase";

    private static InputGlyphDatabase runtimeInstance;

    [SerializeField] private List<InputGlyphEntry> glyphEntries = new();

    private readonly Dictionary<KeyCode, InputGlyphEntry> glyphLookup = new();
    private bool cacheDirty = true;

    public static InputGlyphDatabase LoadOrCreate()
    {
        if (runtimeInstance != null)
            return runtimeInstance;

        runtimeInstance = Resources.Load<InputGlyphDatabase>(ResourcePath);
        if (runtimeInstance != null)
        {
            if (runtimeInstance.glyphEntries == null || runtimeInstance.glyphEntries.Count == 0)
                runtimeInstance.ResetToDefaultEntries();

            runtimeInstance.MarkCacheDirty();
            return runtimeInstance;
        }

        runtimeInstance = CreateInstance<InputGlyphDatabase>();
        runtimeInstance.hideFlags = HideFlags.DontUnloadUnusedAsset;
        runtimeInstance.ResetToDefaultEntries();
        return runtimeInstance;
    }

    public static InputGlyphPresentation Resolve(KeyCode key)
    {
        return LoadOrCreate().ResolveInternal(key);
    }

    public static string GetDisplayLabel(KeyCode key)
    {
        return Resolve(key).DisplayLabel;
    }

    public static Sprite GetIcon(KeyCode key)
    {
        return Resolve(key).Icon;
    }

    public void ResetToDefaultEntries()
    {
        glyphEntries = CreateDefaultEntries();
        MarkCacheDirty();
    }

    public void SetEntry(KeyCode key, string displayLabel, Sprite icon)
    {
        string resolvedLabel = string.IsNullOrWhiteSpace(displayLabel)
            ? GetFallbackDisplayLabel(key)
            : displayLabel;

        int existingIndex = glyphEntries.FindIndex(entry => entry.key == key);
        if (existingIndex >= 0)
        {
            glyphEntries[existingIndex] = new InputGlyphEntry(key, resolvedLabel, icon);
        }
        else
        {
            glyphEntries.Add(new InputGlyphEntry(key, resolvedLabel, icon));
        }

        MarkCacheDirty();
    }

    public void SetIcon(KeyCode key, Sprite icon)
    {
        int existingIndex = glyphEntries.FindIndex(entry => entry.key == key);
        if (existingIndex >= 0)
        {
            InputGlyphEntry entry = glyphEntries[existingIndex];
            entry.icon = icon;
            if (string.IsNullOrWhiteSpace(entry.displayLabel))
                entry.displayLabel = GetFallbackDisplayLabel(key);

            glyphEntries[existingIndex] = entry;
        }
        else
        {
            glyphEntries.Add(new InputGlyphEntry(key, GetFallbackDisplayLabel(key), icon));
        }

        MarkCacheDirty();
    }

    public void ClearAllIcons()
    {
        for (int i = 0; i < glyphEntries.Count; i++)
        {
            InputGlyphEntry entry = glyphEntries[i];
            entry.icon = null;
            glyphEntries[i] = entry;
        }

        MarkCacheDirty();
    }

    [ContextMenu("Reset To Default Glyph Entries")]
    private void ResetToDefaultEntriesContext()
    {
        ResetToDefaultEntries();
    }

    private void OnEnable()
    {
        MarkCacheDirty();
    }

    private void OnValidate()
    {
        MarkCacheDirty();
    }

    private InputGlyphPresentation ResolveInternal(KeyCode key)
    {
        RebuildLookupIfNeeded();

        if (glyphLookup.TryGetValue(key, out InputGlyphEntry entry))
        {
            string displayLabel = string.IsNullOrWhiteSpace(entry.displayLabel)
                ? GetFallbackDisplayLabel(key)
                : entry.displayLabel;
            return new InputGlyphPresentation(key, displayLabel, entry.icon);
        }

        return new InputGlyphPresentation(key, GetFallbackDisplayLabel(key), null);
    }

    private void MarkCacheDirty()
    {
        cacheDirty = true;
    }

    private void RebuildLookupIfNeeded()
    {
        if (!cacheDirty)
            return;

        glyphLookup.Clear();
        for (int i = 0; i < glyphEntries.Count; i++)
        {
            InputGlyphEntry entry = glyphEntries[i];
            if (entry.key == KeyCode.None || glyphLookup.ContainsKey(entry.key))
                continue;

            glyphLookup.Add(entry.key, entry);
        }

        cacheDirty = false;
    }

    private static List<InputGlyphEntry> CreateDefaultEntries()
    {
        List<InputGlyphEntry> entries = new()
        {
            new InputGlyphEntry(KeyCode.None, "-"),
            new InputGlyphEntry(KeyCode.Mouse0, "LMB"),
            new InputGlyphEntry(KeyCode.Mouse1, "RMB"),
            new InputGlyphEntry(KeyCode.Mouse2, "MMB"),
            new InputGlyphEntry(KeyCode.Mouse3, "Mouse4"),
            new InputGlyphEntry(KeyCode.Mouse4, "Mouse5"),
            new InputGlyphEntry(KeyCode.Mouse5, "Mouse6"),
            new InputGlyphEntry(KeyCode.Space, "Space"),
            new InputGlyphEntry(KeyCode.Tab, "Tab"),
            new InputGlyphEntry(KeyCode.Return, "Enter"),
            new InputGlyphEntry(KeyCode.Backspace, "Back"),
            new InputGlyphEntry(KeyCode.Escape, "Esc"),
            new InputGlyphEntry(KeyCode.Delete, "Del"),
            new InputGlyphEntry(KeyCode.Insert, "Ins"),
            new InputGlyphEntry(KeyCode.Home, "Home"),
            new InputGlyphEntry(KeyCode.End, "End"),
            new InputGlyphEntry(KeyCode.PageUp, "PgUp"),
            new InputGlyphEntry(KeyCode.PageDown, "PgDn"),
            new InputGlyphEntry(KeyCode.CapsLock, "Caps"),
            new InputGlyphEntry(KeyCode.BackQuote, "`"),
            new InputGlyphEntry(KeyCode.Minus, "-"),
            new InputGlyphEntry(KeyCode.Equals, "="),
            new InputGlyphEntry(KeyCode.LeftBracket, "["),
            new InputGlyphEntry(KeyCode.RightBracket, "]"),
            new InputGlyphEntry(KeyCode.Backslash, "\\"),
            new InputGlyphEntry(KeyCode.Semicolon, ";"),
            new InputGlyphEntry(KeyCode.Quote, "'"),
            new InputGlyphEntry(KeyCode.Comma, ","),
            new InputGlyphEntry(KeyCode.Period, "."),
            new InputGlyphEntry(KeyCode.Slash, "/"),
            new InputGlyphEntry(KeyCode.LeftShift, "LShift"),
            new InputGlyphEntry(KeyCode.RightShift, "RShift"),
            new InputGlyphEntry(KeyCode.LeftControl, "LCtrl"),
            new InputGlyphEntry(KeyCode.RightControl, "RCtrl"),
            new InputGlyphEntry(KeyCode.LeftAlt, "LAlt"),
            new InputGlyphEntry(KeyCode.RightAlt, "RAlt"),
            new InputGlyphEntry(KeyCode.UpArrow, "\u2191"),
            new InputGlyphEntry(KeyCode.DownArrow, "\u2193"),
            new InputGlyphEntry(KeyCode.LeftArrow, "\u2190"),
            new InputGlyphEntry(KeyCode.RightArrow, "\u2192"),
        };

        for (KeyCode key = KeyCode.A; key <= KeyCode.Z; key++)
            entries.Add(new InputGlyphEntry(key, key.ToString().ToUpperInvariant()));

        for (KeyCode key = KeyCode.Alpha0; key <= KeyCode.Alpha9; key++)
            entries.Add(new InputGlyphEntry(key, ((int)(key - KeyCode.Alpha0)).ToString()));

        for (KeyCode key = KeyCode.F1; key <= KeyCode.F12; key++)
            entries.Add(new InputGlyphEntry(key, key.ToString()));

        for (KeyCode key = KeyCode.Keypad0; key <= KeyCode.Keypad9; key++)
            entries.Add(new InputGlyphEntry(key, $"Num {(int)(key - KeyCode.Keypad0)}"));

        return entries;
    }

    private static string GetFallbackDisplayLabel(KeyCode key)
    {
        if (key == KeyCode.None)
            return "-";

        if (key >= KeyCode.A && key <= KeyCode.Z)
            return key.ToString().ToUpperInvariant();

        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            return ((int)(key - KeyCode.Alpha0)).ToString();

        if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
            return $"Num {(int)(key - KeyCode.Keypad0)}";

        return key switch
        {
            KeyCode.Mouse0 => "LMB",
            KeyCode.Mouse1 => "RMB",
            KeyCode.Mouse2 => "MMB",
            KeyCode.Mouse3 => "Mouse4",
            KeyCode.Mouse4 => "Mouse5",
            KeyCode.Mouse5 => "Mouse6",
            KeyCode.Space => "Space",
            KeyCode.Tab => "Tab",
            KeyCode.Return => "Enter",
            KeyCode.Backspace => "Back",
            KeyCode.Escape => "Esc",
            KeyCode.Delete => "Del",
            KeyCode.Insert => "Ins",
            KeyCode.Home => "Home",
            KeyCode.End => "End",
            KeyCode.PageUp => "PgUp",
            KeyCode.PageDown => "PgDn",
            KeyCode.CapsLock => "Caps",
            KeyCode.LeftShift => "LShift",
            KeyCode.RightShift => "RShift",
            KeyCode.LeftControl => "LCtrl",
            KeyCode.RightControl => "RCtrl",
            KeyCode.LeftAlt => "LAlt",
            KeyCode.RightAlt => "RAlt",
            KeyCode.LeftBracket => "[",
            KeyCode.RightBracket => "]",
            KeyCode.Backslash => "\\",
            KeyCode.Semicolon => ";",
            KeyCode.Quote => "'",
            KeyCode.Comma => ",",
            KeyCode.Period => ".",
            KeyCode.Slash => "/",
            KeyCode.UpArrow => "\u2191",
            KeyCode.DownArrow => "\u2193",
            KeyCode.LeftArrow => "\u2190",
            KeyCode.RightArrow => "\u2192",
            _ => key.ToString(),
        };
    }
}
