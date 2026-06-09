using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class InputGlyphDatabaseEditorUtility
{
    private const string AssetPath = "Assets/_Project/Resources/InputGlyphDatabase.asset";
    private const string KeyboardMapPath = "Assets/_Project/Art/Sprites/UI/Common/KeyBoardMap.png";

    private static readonly Regex SpriteIndexPattern = new(@"_(\d+)$", RegexOptions.Compiled);

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

    [MenuItem("Tools/Input/Open Or Create Input Glyph Database")]
    public static void OpenOrCreateDatabase()
    {
        InputGlyphDatabase database = LoadOrCreateDatabase(out bool created);
        if (database == null)
            return;

        if (created)
        {
            ApplyKeyboardMapGlyphMapping(database);
        }

        Selection.activeObject = database;
        EditorGUIUtility.PingObject(database);
    }

    [MenuItem("Tools/Input/Apply KeyBoardMap Glyph Mapping")]
    public static void ApplyKeyboardMapGlyphMappingMenu()
    {
        InputGlyphDatabase database = LoadOrCreateDatabase(out _);
        if (database == null)
            return;

        ApplyKeyboardMapGlyphMapping(database);
        Selection.activeObject = database;
        EditorGUIUtility.PingObject(database);
    }

    private static InputGlyphDatabase LoadOrCreateDatabase(out bool created)
    {
        created = false;

        InputGlyphDatabase database = AssetDatabase.LoadAssetAtPath<InputGlyphDatabase>(AssetPath);
        if (database != null)
            return database;

        string directoryPath = Path.GetDirectoryName(AssetPath);
        if (!string.IsNullOrEmpty(directoryPath))
            Directory.CreateDirectory(directoryPath);

        database = ScriptableObject.CreateInstance<InputGlyphDatabase>();
        database.ResetToDefaultEntries();
        AssetDatabase.CreateAsset(database, AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        created = true;
        return database;
    }

    private static void ApplyKeyboardMapGlyphMapping(InputGlyphDatabase database)
    {
        Sprite[] sprites = LoadKeyboardMapSprites();
        if (sprites.Length == 0)
        {
            Debug.LogWarning($"No sliced sprites were found at '{KeyboardMapPath}'.");
            return;
        }

        database.ResetToDefaultEntries();
        database.ClearAllIcons();

        foreach (GlyphSpriteBinding binding in GetDefaultBindings())
        {
            if (binding.SpriteIndex < 0 || binding.SpriteIndex >= sprites.Length)
                continue;

            database.SetIcon(binding.Key, sprites[binding.SpriteIndex]);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
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
}


