#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

// Appends only the labels introduced by Monster/Boss/NPC encyclopedia tabs.
public static class EncyclopediaCreatureLocalizationAuthoring
{
    private const string CsvPath = "DataSheets/Localization/GameText.csv";
    private const string ReportPath = "outputs/encyclopedia-implementation-2026-10-11/creature-localization-report.txt";
    private static readonly string[] Codes = { "ko", "en", "ja", "zh-Hans", "zh-Hant" };
    private static readonly string[][] Rows =
    {
        new[] { "encyclopedia.tab_npc", "NPC", "NPC", "NPC", "NPC", "NPC" },
        new[] { "encyclopedia.page_indicator", "{0} / {1}", "{0} / {1}", "{0} / {1}", "{0} / {1}", "{0} / {1}" },
        new[] { "encyclopedia.entry_count", "{0}종", "{0} entries", "{0}種", "{0}种", "{0}種" },
        new[] { "encyclopedia.entries_pending", "등록된 항목 없음", "No registered entries", "登録された項目なし", "暂无登记条目", "尚無登記項目" },
        new[] { "encyclopedia.monster_kill_count", "누적 처치 수 {0}회", "Total kills: {0}", "累計討伐数 {0}体", "累计击败 {0}次", "累計擊敗 {0}次" },
        new[] { "encyclopedia.description", "설명", "Description", "説明", "说明", "說明" },
        new[] { "encyclopedia.current_affection", "현재 호감도", "Current affection", "現在の好感度", "当前好感度", "目前好感度" },
        new[] { "encyclopedia.affection_rewards", "호감도 보상", "Affection rewards", "好感度報酬", "好感度奖励", "好感度獎勵" },
        new[] { "encyclopedia.character_story", "설명 · 스토리", "Description · Story", "説明・ストーリー", "说明 · 故事", "說明 · 故事" },
        new[] { "encyclopedia.portrait_pending", "일러스트 준비 중", "Illustration pending", "イラスト準備中", "插画准备中", "插畫準備中" },
        new[] { "encyclopedia.affection_reward_level", "호감도 {0}", "Affection {0}", "好感度 {0}", "好感度 {0}", "好感度 {0}" },
        new[] { "encyclopedia.full_view", "전체 보기", "Full view", "全体表示", "查看全图", "查看全圖" },
        new[] { "encyclopedia.close_full_view", "닫기", "Close", "閉じる", "关闭", "關閉" }
    };

    public static void Populate()
    {
        var collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>("Assets/_Project/Data/Localization/GameText.asset");
        if (collection == null) throw new InvalidOperationException("Existing GameText collection is missing.");
        var tables = Codes.Select(code => collection.StringTables.FirstOrDefault(table => table.LocaleIdentifier.Code == code)).ToArray();
        if (tables.Any(table => table == null)) throw new InvalidOperationException("All five existing GameText tables are required.");
        string csv = File.ReadAllText(CsvPath);
        string header = csv.TrimStart('\uFEFF').Split(new[] { '\r', '\n' }, 2)[0];
        if (header != "key,ko,en,ja,zh-Hans,zh-Hant") throw new InvalidOperationException("GameText CSV columns changed; do not guess the mapping.");
        ValidateRows();

        int addedKeys = 0, addedValues = 0;
        foreach (string[] row in Rows)
        {
            if (collection.SharedData.GetEntry(row[0]) != null) continue;
            collection.SharedData.AddKey(row[0]);
            addedKeys++;
            for (int i = 0; i < tables.Length; i++)
            {
                tables[i].AddEntry(row[0], row[i + 1]);
                EditorUtility.SetDirty(tables[i]);
                addedValues++;
            }
        }
        if (addedKeys > 0) EditorUtility.SetDirty(collection.SharedData);

        string newline = csv.Contains("\r\n") ? "\r\n" : "\n";
        var appended = new StringBuilder();
        if (csv.Length > 0 && !csv.EndsWith("\n", StringComparison.Ordinal) && !csv.EndsWith("\r", StringComparison.Ordinal))
            appended.Append(newline);
        int addedCsvRows = 0;
        foreach (string[] row in Rows)
        {
            if (Regex.IsMatch(csv, "(?:^|\\r?\\n)\"?" + Regex.Escape(row[0]) + "\"?,")) continue;
            appended.Append(string.Join(",", row.Select(CsvCell))).Append(newline);
            addedCsvRows++;
        }
        if (addedCsvRows > 0) File.AppendAllText(CsvPath, appended.ToString(), new UTF8Encoding(false));
        AssetDatabase.SaveAssets();

        int verifiedValues = 0;
        foreach (string[] row in Rows)
            foreach (StringTable table in tables)
            {
                var entry = table.GetEntry(row[0]);
                if (entry == null || string.IsNullOrEmpty(entry.Value)) throw new InvalidOperationException("Missing creature UI translation: " + row[0] + "/" + table.LocaleIdentifier.Code);
                if (!Tokens(row[1]).SequenceEqual(Tokens(entry.Value))) throw new InvalidOperationException("Creature UI placeholder mismatch: " + row[0]);
                verifiedValues++;
            }
        string report = $"Creature encyclopedia localization: {addedKeys} new keys, {addedValues} localized values, {addedCsvRows} appended CSV rows; {verifiedValues} translations/placeholder sets verified. Existing keys and CSV rows preserved.\n";
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report, new UTF8Encoding(false));
        Debug.Log(report);
    }

    private static void ValidateRows()
    {
        if (Rows.Select(row => row[0]).Distinct(StringComparer.Ordinal).Count() != Rows.Length)
            throw new InvalidOperationException("Duplicate creature localization key.");
        foreach (string[] row in Rows)
        {
            if (row.Length != Codes.Length + 1 || row.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("Incomplete creature localization row.");
            for (int i = 2; i < row.Length; i++)
                if (!Tokens(row[1]).SequenceEqual(Tokens(row[i]))) throw new InvalidOperationException("Creature localization row placeholder mismatch: " + row[0]);
        }
    }

    private static string[] Tokens(string value) => Regex.Matches(value, @"\{\w+\}").Cast<Match>()
        .Select(match => match.Value).OrderBy(value => value, StringComparer.Ordinal).ToArray();

    private static string CsvCell(string value) => value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0
        ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
}
#endif
