using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Settings;

internal static class LocalizationValidation
{
    private static int count;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        count++;
    }

    public static void RunBatch() => Run(true);
    internal static void RunChecks() => Run(false);

    private static void Run(bool exit)
    {
        try
        {
            var collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>("Assets/_Project/Data/Localization/GameText.asset");
            Check(collection != null && collection.StringTables.Count == 5, "Five String Tables required.");
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            string[] codes = { "ko", "en", "ja", "zh-Hans", "zh-Hant" };
            var logic = AssetDatabase.LoadAssetAtPath<RelicLogic_LastStandCritical>(
                "Assets/_Project/Data/Items/Relics/Strategies/RelicLogic_LastStand.asset");
            Check(logic != null, "Last Stand logic source.");
            foreach (string code in codes)
            {
                var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
                Check(locale != null, "Missing locale: " + code);
                LocalizationSettings.StringDatabase.GetTableAsync(GameText.TableName, locale).WaitForCompletion();
                LocalizationSettings.SelectedLocale = locale;
                var table = collection.StringTables.Single(t => t.LocaleIdentifier.Code == code);
                Check(table.Values.Count == collection.SharedData.Entries.Count && table.Values.Count >= 10,
                    "Complete key set: " + code);
                foreach (var key in collection.SharedData.Entries)
                    Check(!string.IsNullOrWhiteSpace(table.GetEntry(key.Id)?.Value), "Missing translation: " + key.Key + "/" + code);
                Check(LocalizationEditorSettings.GetPreloadTableFlag(table), "Table preload: " + code);
                Check(GameText.Get("settings.window.windowed", "MISSING") == table.GetEntry("settings.window.windowed").Value,
                    "Actual runtime key lookup: " + code);
                Check(GameText.Get("does.not.exist", "authored fallback") == "authored fallback", "Missing key fallback.");
                Check(GameText.FromInkTags(new[] { "anim:normal", "loc:dialogue.merchant.001" }, "MISSING") ==
                      table.GetEntry("dialogue.merchant.001").Value, "Ink tag key lookup: " + code);
                for (int level = 1; level <= 3; level++)
                {
                    string effect = logic.BuildTooltip(null, level, null).effectText;
                    Check(!effect.Contains("{threshold}") && effect.Contains("{val:" + level + "}"),
                        "Shared numeric token at level " + level + "/" + code);
                }
            }
            var resolve = typeof(GameSettingsService).GetMethod("ResolveSystemLanguage", BindingFlags.NonPublic | BindingFlags.Static);
            var languages = new[] { SystemLanguage.Korean, SystemLanguage.English, SystemLanguage.Japanese,
                SystemLanguage.ChineseSimplified, SystemLanguage.ChineseTraditional };
            for (int i = 0; i < languages.Length; i++)
                Check((int)(GameLanguageOption)resolve.Invoke(null, new object[] { languages[i] }) == i, "OS mapping " + languages[i]);
            Check((GameLanguageOption)resolve.Invoke(null, new object[] { SystemLanguage.French }) == GameLanguageOption.English,
                "Unsupported OS language fallback.");
            Check((int)GameLanguageOption.Korean == 0 && Enum.GetValues(typeof(GameLanguageOption)).Length == 5,
                "Preserved Korean=0 and no Auto enum.");

            string source = File.ReadAllText("Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink");
            var original = new Ink.Compiler(Regex.Replace(source, @" # loc:[\w.]+", "")).Compile();
            var localized = new Ink.Compiler(source).Compile();
            Check(original != null && localized != null, "Both Ink variants compile.");
            Check(File.ReadAllText("Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.json")
                .Contains("loc:dialogue.merchant.001"), "Authored runtime JSON includes localization tags.");
            while (original.canContinue)
            {
                Check(localized.canContinue && original.Continue() == localized.Continue(), "Ink text unchanged.");
            }
            Check(localized.currentChoices.Count == 2 && original.currentChoices.Count == 2, "Choice count unchanged.");
            Check(localized.currentChoices[0].text == original.currentChoices[0].text &&
                  localized.currentChoices[1].text == original.currentChoices[1].text, "Choice text unchanged.");
            Check(localized.currentChoices[0].tags.Contains("loc:dialogue.merchant.003") &&
                  localized.currentChoices[1].tags.Contains("loc:dialogue.merchant.006"), "Choice loc tags survive compilation.");
            Debug.Log("[LocalizationValidation] PASS " + count + " checks. UI glyph/layout and Play Mode not covered.");
            if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            if (!exit) throw;
            Debug.LogException(e);
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }
}
