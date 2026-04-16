using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
#endif

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
#if UNITY_EDITOR
    private const string EditorAssetPath = "Assets/LeeJunMo/Datas/Resources/InputGlyphDatabase.asset";
    private const string KeyboardMapPath = "Assets/Sprites/UI/Common/KeyBoardMap.png";
    private static readonly Regex SpriteIndexPattern = new(@"_(\d+)$", RegexOptions.Compiled);
#endif

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

#if UNITY_EDITOR
            runtimeInstance.EnsureEditorIconMapping();
#endif

            runtimeInstance.MarkCacheDirty();
            return runtimeInstance;
        }

#if UNITY_EDITOR
        runtimeInstance = CreateOrRestoreEditorAsset();
        if (runtimeInstance != null)
        {
            runtimeInstance.MarkCacheDirty();
            return runtimeInstance;
        }
#endif

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

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void EnsureEditorDatabaseAssetExistsOnLoad()
    {
        EditorApplication.delayCall += EnsureEditorDatabaseAssetExistsDelayed;
    }

    private static void EnsureEditorDatabaseAssetExistsDelayed()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        InputGlyphDatabase database = AssetDatabase.LoadAssetAtPath<InputGlyphDatabase>(EditorAssetPath);
        if (database == null)
        {
            CreateOrRestoreEditorAsset();
            return;
        }

        database.EnsureEditorIconMapping();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
    }

    private readonly struct GlyphSpriteBinding
    {
        public GlyphSpriteBinding(KeyCode key, int spriteIndex)
        {
            Key = key;
            SpriteIndex = spriteIndex;
        }

        public KeyCode Key { get; }
        public int SpriteIndex { get; }
    }

    private static InputGlyphDatabase CreateOrRestoreEditorAsset()
    {
        InputGlyphDatabase database = AssetDatabase.LoadAssetAtPath<InputGlyphDatabase>(EditorAssetPath);
        bool created = false;

        if (database == null)
        {
            string directoryPath = Path.GetDirectoryName(EditorAssetPath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            database = CreateInstance<InputGlyphDatabase>();
            database.ResetToDefaultEntries();
            AssetDatabase.CreateAsset(database, EditorAssetPath);
            created = true;
        }

        if (database == null)
            return null;

        if (created || database.glyphEntries == null || database.glyphEntries.Count == 0)
            database.ResetToDefaultEntries();

        database.EnsureEditorIconMapping();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        InputGlyphDatabase loadedFromResources = Resources.Load<InputGlyphDatabase>(ResourcePath);
        return loadedFromResources != null ? loadedFromResources : database;
    }

    private void EnsureEditorIconMapping()
    {
        if (HasAssignedIcons())
            return;

        Sprite[] sprites = LoadKeyboardMapSprites();
        if (sprites == null || sprites.Length == 0)
            return;

        if (glyphEntries == null || glyphEntries.Count == 0)
            ResetToDefaultEntries();

        GlyphSpriteBinding[] bindings = GetDefaultBindings();
        for (int i = 0; i < bindings.Length; i++)
        {
            GlyphSpriteBinding binding = bindings[i];
            if (binding.SpriteIndex < 0 || binding.SpriteIndex >= sprites.Length)
                continue;

            SetIcon(binding.Key, sprites[binding.SpriteIndex]);
        }
    }

    private bool HasAssignedIcons()
    {
        if (glyphEntries == null)
            return false;

        for (int i = 0; i < glyphEntries.Count; i++)
        {
            if (glyphEntries[i].icon != null)
                return true;
        }

        return false;
    }

    private static Sprite[] LoadKeyboardMapSprites()
    {
        return AssetDatabase.LoadAllAssetsAtPath(KeyboardMapPath)
            .OfType<Sprite>()
            .OrderBy(GetSpriteIndex)
            .ToArray();
    }

    private static int GetSpriteIndex(Sprite sprite)
    {
        Match match = SpriteIndexPattern.Match(sprite.name);
        return match.Success && int.TryParse(match.Groups[1].Value, out int index)
            ? index
            : int.MaxValue;
    }

    private static GlyphSpriteBinding[] GetDefaultBindings()
    {
        return new[]
        {
            new GlyphSpriteBinding(KeyCode.F1, 0),
            new GlyphSpriteBinding(KeyCode.F2, 1),
            new GlyphSpriteBinding(KeyCode.F3, 2),
            new GlyphSpriteBinding(KeyCode.F4, 3),
            new GlyphSpriteBinding(KeyCode.F5, 4),
            new GlyphSpriteBinding(KeyCode.F6, 5),
            new GlyphSpriteBinding(KeyCode.F7, 6),
            new GlyphSpriteBinding(KeyCode.F8, 7),
            new GlyphSpriteBinding(KeyCode.F9, 8),
            new GlyphSpriteBinding(KeyCode.F10, 9),
            new GlyphSpriteBinding(KeyCode.F11, 10),
            new GlyphSpriteBinding(KeyCode.F12, 11),
            new GlyphSpriteBinding(KeyCode.Alpha1, 12),
            new GlyphSpriteBinding(KeyCode.Alpha2, 13),
            new GlyphSpriteBinding(KeyCode.Alpha3, 14),
            new GlyphSpriteBinding(KeyCode.Alpha4, 15),
            new GlyphSpriteBinding(KeyCode.Alpha5, 16),
            new GlyphSpriteBinding(KeyCode.Alpha6, 17),
            new GlyphSpriteBinding(KeyCode.Alpha7, 18),
            new GlyphSpriteBinding(KeyCode.Alpha8, 19),
            new GlyphSpriteBinding(KeyCode.Alpha9, 20),
            new GlyphSpriteBinding(KeyCode.Alpha0, 21),
            new GlyphSpriteBinding(KeyCode.Minus, 22),
            new GlyphSpriteBinding(KeyCode.Equals, 23),
            new GlyphSpriteBinding(KeyCode.Q, 24),
            new GlyphSpriteBinding(KeyCode.W, 25),
            new GlyphSpriteBinding(KeyCode.E, 26),
            new GlyphSpriteBinding(KeyCode.R, 27),
            new GlyphSpriteBinding(KeyCode.T, 28),
            new GlyphSpriteBinding(KeyCode.Y, 29),
            new GlyphSpriteBinding(KeyCode.U, 30),
            new GlyphSpriteBinding(KeyCode.I, 31),
            new GlyphSpriteBinding(KeyCode.O, 32),
            new GlyphSpriteBinding(KeyCode.P, 33),
            new GlyphSpriteBinding(KeyCode.LeftBracket, 34),
            new GlyphSpriteBinding(KeyCode.RightBracket, 35),
            new GlyphSpriteBinding(KeyCode.A, 36),
            new GlyphSpriteBinding(KeyCode.S, 37),
            new GlyphSpriteBinding(KeyCode.D, 38),
            new GlyphSpriteBinding(KeyCode.F, 39),
            new GlyphSpriteBinding(KeyCode.G, 40),
            new GlyphSpriteBinding(KeyCode.H, 41),
            new GlyphSpriteBinding(KeyCode.J, 42),
            new GlyphSpriteBinding(KeyCode.K, 43),
            new GlyphSpriteBinding(KeyCode.L, 44),
            new GlyphSpriteBinding(KeyCode.Semicolon, 45),
            new GlyphSpriteBinding(KeyCode.Quote, 46),
            new GlyphSpriteBinding(KeyCode.Backslash, 47),
            new GlyphSpriteBinding(KeyCode.Z, 49),
            new GlyphSpriteBinding(KeyCode.X, 50),
            new GlyphSpriteBinding(KeyCode.C, 51),
            new GlyphSpriteBinding(KeyCode.V, 52),
            new GlyphSpriteBinding(KeyCode.B, 53),
            new GlyphSpriteBinding(KeyCode.N, 54),
            new GlyphSpriteBinding(KeyCode.M, 55),
            new GlyphSpriteBinding(KeyCode.Comma, 56),
            new GlyphSpriteBinding(KeyCode.Period, 57),
            new GlyphSpriteBinding(KeyCode.Slash, 58),
            new GlyphSpriteBinding(KeyCode.BackQuote, 60),
            new GlyphSpriteBinding(KeyCode.Home, 82),
            new GlyphSpriteBinding(KeyCode.UpArrow, 77),
            new GlyphSpriteBinding(KeyCode.DownArrow, 78),
            new GlyphSpriteBinding(KeyCode.LeftArrow, 79),
            new GlyphSpriteBinding(KeyCode.RightArrow, 80),
            new GlyphSpriteBinding(KeyCode.Escape, 83),
            new GlyphSpriteBinding(KeyCode.LeftAlt, 84),
            new GlyphSpriteBinding(KeyCode.RightAlt, 84),
            new GlyphSpriteBinding(KeyCode.PageDown, 85),
            new GlyphSpriteBinding(KeyCode.PageUp, 86),
            new GlyphSpriteBinding(KeyCode.Delete, 88),
            new GlyphSpriteBinding(KeyCode.End, 90),
            new GlyphSpriteBinding(KeyCode.Insert, 91),
            new GlyphSpriteBinding(KeyCode.LeftControl, 92),
            new GlyphSpriteBinding(KeyCode.RightControl, 92),
            new GlyphSpriteBinding(KeyCode.Tab, 93),
            new GlyphSpriteBinding(KeyCode.CapsLock, 94),
            new GlyphSpriteBinding(KeyCode.Backspace, 96),
            new GlyphSpriteBinding(KeyCode.Return, 97),
            new GlyphSpriteBinding(KeyCode.LeftShift, 98),
            new GlyphSpriteBinding(KeyCode.RightShift, 98),
            new GlyphSpriteBinding(KeyCode.Space, 102),
            new GlyphSpriteBinding(KeyCode.Mouse0, 113),
            new GlyphSpriteBinding(KeyCode.Mouse1, 114),
            new GlyphSpriteBinding(KeyCode.Mouse2, 115),
            new GlyphSpriteBinding(KeyCode.Mouse3, 116),
            new GlyphSpriteBinding(KeyCode.Mouse4, 117),
            new GlyphSpriteBinding(KeyCode.Mouse5, 118),
        };
    }
#endif
}
