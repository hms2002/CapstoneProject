#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityGAS;

/// <summary>Authors only the approved repeatable rewards, their image, level curve, and new text keys.</summary>
public static class LevelRewardStatAuthoring
{
    public const string Root = "Assets/_Project/Data/Progression/Leveling/";
    public const string SpritePath = "Assets/_Project/Art/Sprites/UI/LevelRewardCards/Stat_Card.png";
    private const string AttributeRoot = "Assets/_Project/Data/Attributes/Definitions/";
    private const string CsvPath = "DataSheets/Localization/GameText.csv";
    private const string InventoryPath = "DataSheets/Localization/AssetInventory.json";
    private const string KeyMapPath = "Assets/_Project/Runtime/Core/Presentation/GameTextAssetKeys.g.cs";
    private static readonly string[] Locales = { "ko", "en", "ja", "zh-Hans", "zh-Hant" };
    public static readonly int[] Requirements = { 40, 50, 60, 70, 85, 100, 115, 130, 145, 165, 185, 205, 230, 255, 280, 310, 345, 380, 420 };
    private static readonly string[] TrainingSuffixes = { " 훈련", " Training", "訓練", "训练", "訓練" };

    private sealed class Stat
    {
        public string Name, Attribute;
        public StatId Id;
        public ModifierType Type;
        public float[] Values;
        public string[] Labels;
        public bool Percent;
        public Stat(string name, StatId id, string attribute, ModifierType type, bool percent, float[] values, params string[] labels)
        { Name = name; Id = id; Attribute = attribute; Type = type; Percent = percent; Values = values; Labels = labels; }
    }

    private static readonly Stat[] Stats =
    {
        new Stat("Attack", StatId.AttackFinal, "DamageCalcAttribute/AttackAddAttribute", ModifierType.Flat, false, new[] { 2f, 4f, 6f }, "공격력", "Attack", "攻撃力", "攻击力", "攻擊力"),
        new Stat("CritChance", StatId.CritChanceFinal, "DamageCalcAttribute/CritChanceAddAttribute", ModifierType.Flat, true, new[] { .02f, .04f, .06f }, "치명타 확률", "Critical Chance", "クリティカル率", "暴击率", "暴擊率"),
        new Stat("CritDamage", StatId.CritMultiplier, "DamageCalcAttribute/CritMultiplierAttribute", ModifierType.Flat, true, new[] { .04f, .08f, .12f }, "치명타 피해", "Critical Damage", "クリティカルダメージ", "暴击伤害", "暴擊傷害"),
        new Stat("Evasion", StatId.EvasionFinal, "DamageCalcAttribute/EvasionAddAttribute", ModifierType.Flat, true, new[] { .01f, .02f, .03f }, "회피율", "Evasion", "回避率", "闪避率", "閃避率"),
        new Stat("AttackSpeed", StatId.AttackSpeedFinal, "DamageCalcAttribute/AttackSpeedBaseAttribute", ModifierType.Percent, true, new[] { .02f, .04f, .06f }, "공격 속도", "Attack Speed", "攻撃速度", "攻击速度", "攻擊速度"),
        new Stat("MoveSpeed", StatId.MoveSpeedFinal, "MoveSpeedMulAttribute", ModifierType.Percent, true, new[] { .03f, .05f, .08f }, "이동 속도", "Movement Speed", "移動速度", "移动速度", "移動速度"),
        new Stat("NormalDamage", StatId.NormalMul, "DamageCalcAttribute/NormalMulAttribute", ModifierType.Percent, true, new[] { .02f, .04f, .06f }, "기본 공격 피해", "Basic Attack Damage", "通常攻撃ダメージ", "普通攻击伤害", "普通攻擊傷害"),
        new Stat("SkillDamage", StatId.SkillMul, "DamageCalcAttribute/SkillMulAttribute", ModifierType.Percent, true, new[] { .02f, .04f, .06f }, "스킬 피해", "Skill Damage", "スキルダメージ", "技能伤害", "技能傷害"),
        new Stat("Fire", StatId.FireFinal, "ElementAttribute/FireElementAddAttribute", ModifierType.Flat, false, new[] { 1f, 2f, 3f }, "화염 피해", "Fire Damage", "炎ダメージ", "火焰伤害", "火焰傷害"),
        new Stat("Electric", StatId.ElectricFinal, "ElementAttribute/ElectricElementAddAttribute", ModifierType.Flat, false, new[] { 1f, 2f, 3f }, "전기 피해", "Electric Damage", "電気ダメージ", "电击伤害", "電擊傷害"),
        new Stat("Poison", StatId.PoisonFinal, "ElementAttribute/PoisonElementAddAttribute", ModifierType.Flat, false, new[] { 1f, 2f, 3f }, "중독 피해", "Poison Damage", "毒ダメージ", "中毒伤害", "中毒傷害"),
        new Stat("Bleed", StatId.BleedFinal, "ElementAttribute/BloodElementAddAttribute", ModifierType.Flat, false, new[] { 1f, 2f, 3f }, "출혈 피해", "Bleed Damage", "出血ダメージ", "流血伤害", "流血傷害")
    };

    public static void ApplyBatch()
    {
        try
        {
            Apply();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            Debug.Log("[LevelRewardStats] AUTHORING_PASS: 36 rewards, 20 levels, 72 text keys in five locales.");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("Tools/Progression/Apply Approved Level Reward Stats")]
    public static void Apply()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode before authoring rewards.");
        foreach (var stat in Stats) Load<AttributeDefinition>(AttributeRoot + stat.Attribute + ".asset");
        var catalog = Load<LevelRewardCatalogSO>(Root + "Rewards/Catalog/LevelRewardCatalog.asset");
        var config = Load<LevelProgressionConfigSO>(Root + "LevelProgressionConfig.asset");
        var collection = Load<StringTableCollection>("Assets/_Project/Data/Localization/GameText.asset");
        Require(Locales.All(code => collection.StringTables.Any(table => table.LocaleIdentifier.Code == code)), "Five existing locales are required.");
        var sprite = ImportSprite();
        var textRows = new List<TextRow>();
        var rewards = catalog.Rewards.ToList();

        foreach (var stat in Stats)
        {
            var attribute = Load<AttributeDefinition>(AttributeRoot + stat.Attribute + ".asset");
            if (stat.Id == StatId.CritChanceFinal || stat.Id == StatId.EvasionFinal)
            {
                // Keep raw overflow; the existing combat probability roll still clamps to 100%.
                attribute.maxValue = float.MaxValue;
                EditorUtility.SetDirty(attribute);
                AssetDatabase.SaveAssetIfDirty(attribute);
            }

            for (int tier = 0; tier < 3; tier++)
            {
                string suffix = "Stat_" + stat.Name + "_" + (tier + 1);
                var effect = Ensure<StatIncreaseLevelRewardEffectSO>(Root + "Rewards/Effects/Effect_" + suffix + ".asset");
                var effectData = new SerializedObject(effect);
                effectData.FindProperty("effectId").stringValue = "level_effect.stat." + stat.Name.ToLowerInvariant() + ".t" + (tier + 1);
                effectData.FindProperty("statId").intValue = (int)stat.Id;
                effectData.FindProperty("attribute").objectReferenceValue = attribute;
                effectData.FindProperty("modifierType").intValue = (int)stat.Type;
                effectData.FindProperty("value").floatValue = stat.Values[tier];
                effectData.FindProperty("tier").intValue = tier;
                effectData.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(effect);

                var reward = Ensure<LevelRewardDefinitionSO>(Root + "Rewards/Definitions/Reward_" + suffix + ".asset");
                string amount = "+" + (stat.Percent ? Mathf.RoundToInt(stat.Values[tier] * 100f) : stat.Values[tier]).ToString(CultureInfo.InvariantCulture) + (stat.Percent ? "%" : "");
                string[] names = stat.Labels.Select((label, index) => label + TrainingSuffixes[index]).ToArray();
                string[] descriptions = stat.Labels.Select(label => label + " <color=#197A67>" + amount + "</color>").ToArray();
                var rewardData = new SerializedObject(reward);
                rewardData.FindProperty("rewardId").stringValue = "level_reward.stat." + stat.Name.ToLowerInvariant() + ".t" + (tier + 1);
                rewardData.FindProperty("allowMultipleSelections").boolValue = true;
                rewardData.FindProperty("displayName").stringValue = names[0];
                rewardData.FindProperty("description").stringValue = descriptions[0];
                rewardData.FindProperty("icon").objectReferenceValue = sprite;
                var effects = rewardData.FindProperty("effects");
                effects.arraySize = 1;
                effects.GetArrayElementAtIndex(0).objectReferenceValue = effect;
                rewardData.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(reward);
                if (!rewards.Contains(reward)) rewards.Add(reward);
                textRows.Add(Text(reward, "displayName", names));
                textRows.Add(Text(reward, "description", descriptions));
            }
        }

        var catalogData = new SerializedObject(catalog);
        var entries = catalogData.FindProperty("rewards");
        entries.arraySize = rewards.Count;
        for (int i = 0; i < rewards.Count; i++) entries.GetArrayElementAtIndex(i).objectReferenceValue = rewards[i];
        catalogData.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(catalog);

        var configData = new SerializedObject(config);
        var curve = configData.FindProperty("nextLevelRequirements");
        curve.arraySize = Requirements.Length;
        for (int i = 0; i < Requirements.Length; i++) curve.GetArrayElementAtIndex(i).intValue = Requirements[i];
        configData.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(config);
        AppendLocalization(collection, textRows);

        Directory.CreateDirectory("Library/CodexLevelRewards20261030");
        File.WriteAllText("Library/CodexLevelRewards20261030/authoring.json", JsonUtility.ToJson(new Report
        { rewardCount = rewards.Count, statRewardCount = 36, maxLevel = config.MaxLevel, totalExperience = Requirements.Sum(), texts = textRows.ToArray() }, true));
    }

    private static Sprite ImportSprite()
    {
        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
#pragma warning disable CS0618
        importer.spritesheet = new[] { new SpriteMetaData { name = "Stat_Card", rect = new Rect(112, 160, 80, 128), pivot = new Vector2(.5f, .5f), alignment = (int)SpriteAlignment.Center } };
#pragma warning restore CS0618
        importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(SpritePath).OfType<Sprite>().Single(value => value.name == "Stat_Card");
    }

    private static void AppendLocalization(StringTableCollection collection, List<TextRow> rows)
    {
        string csv = File.ReadAllText(CsvPath);
        string newline = csv.Contains("\r\n") ? "\r\n" : "\n";
        var appended = new StringBuilder();
        if (!csv.EndsWith("\n", StringComparison.Ordinal)) appended.Append(newline);
        foreach (var row in rows)
        {
            for (int i = 0; i < Locales.Length; i++)
            {
                var table = collection.StringTables.Single(value => value.LocaleIdentifier.Code == Locales[i]);
                var entry = table.GetEntry(row.key);
                Require(entry == null || entry.Value == row.values[i], "Existing translation differs: " + row.key);
                if (entry == null) { table.AddEntry(row.key, row.values[i]); EditorUtility.SetDirty(table); }
            }
            if (!Regex.IsMatch(csv, "(?:^|\\r?\\n)\"?" + Regex.Escape(row.key) + "\"?,"))
                appended.Append(string.Join(",", new[] { row.key }.Concat(row.values).Select(CsvCell))).Append(newline);
        }
        if (appended.Length > 0) File.AppendAllText(CsvPath, appended.ToString(), new UTF8Encoding(false));
        EditorUtility.SetDirty(collection.SharedData);
        foreach (var table in collection.StringTables) AssetDatabase.SaveAssetIfDirty(table);
        AssetDatabase.SaveAssetIfDirty(collection.SharedData);

        string source = File.ReadAllText(KeyMapPath);
        var mappings = new StringBuilder();
        foreach (var row in rows)
        {
            string identity = row.type + "/" + row.owner + "/" + row.field;
            string mapping = "{ \"" + identity + "\", \"" + row.key + "\" }";
            if (source.Contains("\"" + identity + "\"")) Require(source.Contains(mapping), "Existing text identity differs: " + identity);
            else mappings.Append("        ").Append(mapping).Append(",\n");
        }
        int end = source.LastIndexOf("    };", StringComparison.Ordinal);
        Require(end >= 0, "Text map format changed.");
        if (mappings.Length > 0) File.WriteAllText(KeyMapPath, source.Insert(end, mappings.ToString()), new UTF8Encoding(false));

        string inventory = File.ReadAllText(InventoryPath);
        var document = JsonUtility.FromJson<Inventory>(inventory);
        var additions = rows.Where(row => !document.rows.Any(existing => existing.key == row.key)).ToArray();
        if (additions.Length > 0)
        {
            int insertion = inventory.LastIndexOf(']');
            Require(insertion >= 0, "Localization inventory format changed.");
            while (insertion > 0 && char.IsWhiteSpace(inventory[insertion - 1])) insertion--;
            string text = string.Join(",\n", additions.Select(row => "    " + JsonUtility.ToJson(new InventoryRow
            { key = row.key, path = row.path, type = row.type, owner = row.owner, field = row.field, text = row.values[0] }, true).Replace("\n", "\n    ")));
            File.WriteAllText(InventoryPath, inventory.Insert(insertion, (document.rows.Length > 0 ? "," : "") + "\n" + text), new UTF8Encoding(false));
        }
    }

    private static TextRow Text(UnityEngine.Object asset, string field, string[] values)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        return new TextRow { key = "asset." + AssetDatabase.AssetPathToGUID(path) + "." + field,
            path = path, type = asset.GetType().FullName, owner = asset.name, field = field, values = values };
    }

    private static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path)
        ?? throw new InvalidOperationException("Missing asset: " + path);
    private static T Ensure<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        Require(AssetDatabase.LoadMainAssetAtPath(path) == null, "Incompatible asset: " + path);
        asset = ScriptableObject.CreateInstance<T>();
        asset.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
    private static string CsvCell(string value) => value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0
        ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [Serializable] private sealed class TextRow { public string key, path, type, owner, field; public string[] values; }
    [Serializable] private sealed class Inventory { public InventoryRow[] rows; }
    [Serializable] private sealed class InventoryRow { public string key, path, type, owner, field, text; }
    [Serializable] private sealed class Report { public int rewardCount, statRewardCount, maxLevel, totalExperience; public TextRow[] texts; }
}
#endif
