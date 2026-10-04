using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.CSV;
using UnityEditor.Localization.Plugins.CSV.Columns;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

internal static class LocalizationSheetSetup
{
    private const string Folder = "Assets/_Project/Data/Localization";
    private const string CsvPath = "DataSheets/Localization/GameText.csv";
    private static readonly string[] Codes = { "ko", "en", "ja", "zh-Hans", "zh-Hant" };

    [MenuItem("Tools/Localization/Compile Merchant Ink Sample")]
    public static void CompileMerchantSample()
    {
        const string path = "Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated";
        var story = new Ink.Compiler(File.ReadAllText(path + ".ink")).Compile();
        if (story == null) throw new InvalidOperationException("Merchant Ink compilation failed.");
        File.WriteAllText(path + ".json", story.ToJson(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(path + ".json");
    }

    public static void CompileMerchantBatch()
    {
        CompileMerchantSample();
        LocalizationValidation.RunBatch();
    }

    [MenuItem("Tools/Localization/Import GameText CSV")]
    public static void Import()
    {
        ValidateCsv(); // Validate everything before creating or updating assets.
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, Folder + "/LocalizationSettings.asset");
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }
        var locales = new List<Locale>();
        foreach (string code in Codes)
        {
            var locale = LocalizationEditorSettings.GetLocales().FirstOrDefault(l => l.Identifier.Code == code);
            if (locale == null)
            {
                locale = Locale.CreateLocale(code);
                AssetDatabase.CreateAsset(locale, Folder + "/Locale_" + code + ".asset");
                LocalizationEditorSettings.AddLocale(locale);
            }
            locales.Add(locale);
        }
        LocalizationSettings.ProjectLocale = locales.First(l => l.Identifier.Code == "ko");
        var collection = LocalizationEditorSettings.GetStringTableCollection(GameText.TableName) ??
            AssetDatabase.LoadAssetAtPath<StringTableCollection>(Folder + "/GameText.asset") ??
            LocalizationEditorSettings.CreateStringTableCollection(GameText.TableName, Folder, locales);
        if (collection == null) throw new InvalidOperationException("GameText collection could not be loaded or created.");
        var mappings = new List<CsvColumns>
        {
            new KeyIdColumns { KeyFieldName = "key", IncludeId = false, IncludeSharedComments = false }
        };
        foreach (string code in Codes)
            mappings.Add(new LocaleColumns { LocaleIdentifier = code, FieldName = code, IncludeComments = false });
        using (var reader = File.OpenText(CsvPath)) Csv.ImportInto(reader, collection, mappings, true);
        foreach (var table in collection.StringTables)
        {
            LocalizationEditorSettings.SetPreloadTableFlag(table, true);
            EditorUtility.SetDirty(table);
        }
        EditorUtility.SetDirty(settings);
        EditorUtility.SetDirty(collection.SharedData);
        EditorUtility.SetDirty(collection);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
        AssetDatabase.SaveAssets();
        Debug.Log("[Localization] Imported GameText CSV with 5 explicit locale mappings.");
    }

    public static void ImportBatch()
    {
        try { Import(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static void ValidateCsv()
    {
        using var reader = File.OpenText(CsvPath);
        var rows = ReadRows(reader).ToList();
        if (rows.Count == 0) throw new FormatException("Empty localization CSV.");
        string[] headers = new[] { "key" }.Concat(Codes).ToArray();
        if (!rows[0].SequenceEqual(headers)) throw new FormatException("Expected key and 5 language columns.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.Skip(1))
        {
            if (row.Length != 6) throw new FormatException("Expected exactly 6 translation columns.");
            string key = row[0];
            if (string.IsNullOrWhiteSpace(key) || !keys.Add(key)) throw new FormatException("Empty or duplicate key: " + key);
            string[] tokens = Tokens(row[1]);
            for (int i = 0; i < Codes.Length; i++)
            {
                string text = row[i + 1];
                if (string.IsNullOrWhiteSpace(text) || !tokens.SequenceEqual(Tokens(text)))
                    throw new FormatException($"Missing text or placeholder mismatch: {key}/{Codes[i]}");
            }
        }
        if (keys.Count == 0) throw new FormatException("No translation rows.");
    }

    private static IEnumerable<string[]> ReadRows(TextReader reader)
    {
        var cells = new List<string>();
        var value = new StringBuilder();
        bool quoted = false, closed = false;
        int next;
        while ((next = reader.Read()) >= 0)
        {
            char c = (char)next;
            if (quoted)
            {
                if (c != '"') value.Append(c);
                else if (reader.Peek() == '"') { reader.Read(); value.Append('"'); }
                else { quoted = false; closed = true; }
            }
            else if (c == ',') { cells.Add(value.ToString()); value.Clear(); closed = false; }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && reader.Peek() == '\n') reader.Read();
                cells.Add(value.ToString());
                if (cells.Count != 1 || cells[0].Length != 0) yield return cells.ToArray();
                cells.Clear(); value.Clear(); closed = false;
            }
            else if (c == '"' && value.Length == 0 && !closed) quoted = true;
            else if (closed || c == '"') throw new FormatException("Invalid CSV quoting.");
            else value.Append(c);
        }
        if (quoted) throw new FormatException("Unclosed CSV quote.");
        if (cells.Count > 0 || value.Length > 0 || closed)
        {
            cells.Add(value.ToString()); yield return cells.ToArray();
        }
    }

    private static string[] Tokens(string text) => Regex.Matches(text ?? "", @"\{\w+\}")
        .Cast<Match>().Select(m => m.Value).OrderBy(s => s, StringComparer.Ordinal).ToArray();
}
