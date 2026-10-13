using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

// Reads compiled UI text paths and loaded Korean tables; does not author or save assets/scenes.
public static class ElectricRelicDescriptionReadback
{
    private static readonly string[] Ids =
    {
        "RD_CurrentShard", "Relic.Electric.ThunderHeart", "Relic.Electric.StormStep",
        "Relic.Electric.DashCoil", "Relic.Electric.ThunderKnot", "Relic.Electric.ResonanceBell",
        "Relic.Electric.TinglingShackles", "Relic.Electric.CurrentJar",
        "Relic.Electric.EchoCoil", "Relic.Electric.ConductorRing"
    };

    [Serializable] private sealed class Report
    {
        public string result = "PASS", locale = "ko", generatedUtc;
        public Entry[] entries;
    }
    [Serializable] private sealed class Entry
    {
        public string relicId, definitionPath, title, description;
        public int maxLevel;
        public Level[] levels;
    }
    [Serializable] private sealed class Level
    {
        public int level;
        public string effectText, displayText;
    }

    public static void ExportBatch()
    {
        int exitCode = 1;
        try
        {
            var initialization = LocalizationSettings.InitializationOperation;
            initialization.WaitForCompletion();
            Require(initialization.Status == AsyncOperationStatus.Succeeded, "Localization initialization failed.");
            var originalLocale = LocalizationSettings.SelectedLocale;
            try
            {
                var korean = LocalizationSettings.AvailableLocales.GetLocale("ko");
                Require(korean != null, "Korean locale is missing.");
                var operation = LocalizationSettings.StringDatabase.GetTableAsync(GameText.TableName, korean);
                StringTable table = operation.WaitForCompletion();
                Require(operation.Status == AsyncOperationStatus.Succeeded && table != null, "Korean GameText table did not load.");
                LocalizationSettings.SelectedLocale = korean;
                foreach (string template in new[] { "scene", "health", "dash", "extension", "targets", "paralysis", "expiry", "echo", "conductor" })
                {
                    string key = "relic.electric." + template + ".template";
                    string translated = table.GetEntry(key)?.Value;
                    Require(!string.IsNullOrWhiteSpace(translated) && GameText.Get(key, "MISSING") == translated,
                        "Runtime Korean template lookup failed: " + key);
                }

                var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/_Project/Data/Items/ItemDatabase.asset");
                Require(database != null && database.allRelics != null, "ItemDatabase is missing.");
                var selected = database.allRelics.Where(definition => definition != null &&
                    (definition.relicId == Ids[0] || definition.relicId.StartsWith("Relic.Electric.", StringComparison.Ordinal))).ToArray();
                Require(selected.Length == 10 && selected.Select(definition => definition.relicId).Distinct().Count() == 10 &&
                    Ids.All(id => selected.Any(definition => definition.relicId == id)), "Expected CurrentShard and exactly nine registered electric relics.");
                var report = new Report { generatedUtc = DateTime.UtcNow.ToString("o"), entries = new Entry[Ids.Length] };
                var readable = new StringBuilder("전기 유물 실제 게임 설명 (한국어)\n\n");
                for (int index = 0; index < Ids.Length; index++)
                {
                    var definition = selected.Single(relic => relic.relicId == Ids[index]);
                    Require(definition.logic != null && definition.maxLevel > 0, "Missing logic or levels: " + definition.relicId);
                    string title = definition.DisplayName;
                    string description = GameText.Asset(definition, "description", definition.description);
                    CheckAssetLookup(table, definition, "displayName", title);
                    CheckAssetLookup(table, definition, "description", description);
                    CheckPlayerText(title, definition.relicId);
                    CheckPlayerText(description, definition.relicId);
                    var entry = new Entry { relicId = definition.relicId, definitionPath = AssetDatabase.GetAssetPath(definition),
                        title = title, description = description, maxLevel = definition.maxLevel, levels = new Level[definition.maxLevel] };
                    report.entries[index] = entry;
                    readable.AppendLine(Plain(title) + " (" + definition.relicId + ")");
                    readable.AppendLine("설명: " + Plain(description));
                    for (int level = 1; level <= definition.maxLevel; level++)
                    {
                        string effect = definition.logic.BuildTooltip(definition, level, null)?.effectText;
                        Require(!string.IsNullOrWhiteSpace(effect), "Empty runtime tooltip: " + definition.relicId + "/" + level);
                        CheckPlayerText(effect, definition.relicId);
                        string display = Plain(effect);
                        entry.levels[level - 1] = new Level { level = level, effectText = effect, displayText = display };
                        readable.AppendLine("Lv " + level + "/" + definition.maxLevel + "\n" + display);
                    }
                    readable.AppendLine();
                }
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "CodexElectricHealthText"));
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "descriptions.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(directory, "descriptions.txt"), readable.ToString(), new UTF8Encoding(false));
                Debug.Log("[ElectricRelicDescriptionReadback] PASS: 10 registered relics / " + report.entries.Sum(entry => entry.levels.Length) +
                    " authored-level tooltips exported to " + directory);
                exitCode = 0;
            }
            finally { LocalizationSettings.SelectedLocale = originalLocale; }
        }
        catch (Exception exception) { Debug.LogException(exception); }
        if (Application.isBatchMode) EditorApplication.Exit(exitCode);
    }

    private static void CheckAssetLookup(StringTable table, RelicDefinition definition, string field, string actual)
    {
        string key = GameText.AssetKey(definition, field);
        Require(key != null && !string.IsNullOrWhiteSpace(table.GetEntry(key)?.Value) && table.GetEntry(key).Value == actual,
            "Runtime localized asset lookup failed: " + definition.relicId + "/" + field);
    }
    private static void CheckPlayerText(string raw, string id)
    {
        string text = Plain(raw);
        Require(!text.Contains("현재 씬") && !text.Contains("발현"), "Developer terminology in player text: " + id + "/" + text);
        if (id == "Relic.Electric.StormStep")
            Require(!Regex.IsMatch(text, @"최대\s*\d+\s*(?:칸|단계)|(?:칸|단계)\s*(?:한도|까지만)|상한"), "Obsolete maximum-health cap clause: " + text);
    }
    private static string Plain(string raw) => Regex.Replace(DetailTextFormatter.Format(raw ?? string.Empty, null), @"</?(?:color|link|b|i)(?:=[^>]*)?>", string.Empty);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
