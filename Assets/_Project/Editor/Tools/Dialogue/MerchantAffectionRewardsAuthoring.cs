using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

/// <summary>Scoped asset authoring; never exits the user's Editor or accesses a save slot.</summary>
public static class MerchantAffectionRewardsAuthoring
{
    public const string Output = "outputs/merchant-rewards-2026-11-01";
    private const string Folder = "Assets/_Project/Data/Dialogue/Affection/";
    private const string KeyMap = "Assets/_Project/Runtime/Core/Presentation/GameTextAssetKeys.g.cs";
    private const string InventoryPath = "DataSheets/Localization/AssetInventory.json";
    public static readonly string[] SlotPaths = {
        "Assets/_Project/Prefabs/UI/Dialogue/ShopSlot_RunGold.prefab",
        "Assets/_Project/Prefabs/UI/Dialogue/ShopSlot.prefab"
    };

    [MenuItem("Tools/NPC/Apply Merchant Permanent Rewards")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author merchant assets outside Play Mode.");
        Directory.CreateDirectory(Output);
        var merchant = AssetDatabase.LoadAssetAtPath<NPCData>(NpcAffectionDialogueAuthoring.MerchantPath);
        ConfigureMerchant(merchant);
        AssetDatabase.SaveAssets();
        Debug.Log("[MerchantRewards] Four permanent tiers applied.");
    }

    public static void ConfigureMerchant(NPCData merchant)
    {
        if (merchant == null) throw new InvalidOperationException("Sasha NPCData is missing.");
        var special = Ensure<MerchantAffectionRewardEffect>(Folder + "SashaSpecialOffer.asset");
        Configure(special, "specialDiscountMinPercent", 10);
        Configure(special, "specialDiscountMaxPercent", 20);
        var gold = Ensure<MerchantAffectionRewardEffect>(Folder + "SashaGoldRetention.asset");
        Configure(gold, "goldRetentionLimit", 500);
        var quality = Ensure<MerchantAffectionRewardEffect>(Folder + "SashaBetterRelics.asset");
        Configure(quality, "rareRelicWeightBonus", 0.10f);
        Configure(quality, "epicRelicWeightBonus", 0.15f);
        var discount = AssetDatabase.LoadAssetAtPath<ShopAffectionDiscountEffect>(Folder + "SashaShopDiscount.asset");
        if (discount == null) throw new InvalidOperationException("Existing Sasha discount is missing.");
        Configure(discount, "discountRate", 0.05f);
        var icon = discount.rewardIcon;
        foreach (var effect in new AffectionEffect[] { special, gold, quality }) { effect.rewardIcon = icon; EditorUtility.SetDirty(effect); }
        merchant.affectionRewards = new[] { special, gold, quality }.Cast<AffectionEffect>().Concat(new[] { discount })
            .Select((effect, index) => new AffectionReward { targetLevel = index + 1, effect = effect }).ToList();
        ConfigureConversations(merchant);
        EditorUtility.SetDirty(merchant);

        ImportTexts(new[] {
            Text(special, new[] { "상점마다 무작위 상품 하나가 10~20% 추가 할인됩니다.", "One random item in each shop receives an extra 10-20% discount.", "各ショップでランダムな商品1点が追加で10～20%割引されます。", "每家商店随机一件商品额外优惠10～20%。", "每家商店隨機一件商品額外優惠10～20%。" }),
            Text(gold, new[] { "게임 오버 시 보유 금화를 최대 500까지 다음 런으로 가져갑니다.", "After defeat or timeout, carry up to 500 gold into your next run.", "ゲームオーバー時、所持金を最大500ゴールドまで次の挑戦に持ち越します。", "游戏结束时，最多保留500金币用于下一轮冒险。", "遊戲結束時，最多保留500金幣用於下一輪冒險。" }),
            Text(quality, new[] { "상점에서 희귀·영웅 등급 유물이 조금 더 자주 등장합니다.", "Rare and Epic relics appear slightly more often in shops.", "ショップでレア・エピック等級の遺物が少し出やすくなります。", "商店中稀有与史诗遗物的出现几率小幅提高。", "商店中稀有與史詩遺物的出現機率小幅提高。" }),
            Text(discount, new[] { "상점 판매 가격 5% 할인", "5% discount on shop prices", "店の販売価格が5%割引", "商店售价优惠5%", "商店售價優惠5%" }),
            new TextRow { key = "shop.special_offer", values = new[] { "특가 -{0}%", "SALE -{0}%", "特価 -{0}%", "特价 -{0}%", "特價 -{0}%" } }
        });
    }

    public static void ConfigureConversations(NPCData merchant)
    {
        merchant.affectionDialogues = new[]
        {
            new NpcAffectionDialogueEntry { affectionLevel = 0, startPath = "affection_0_a", requiredThemeId = string.Empty },
            new NpcAffectionDialogueEntry { affectionLevel = 0, startPath = "affection_0_b", requiredThemeId = "dragon" },
            new NpcAffectionDialogueEntry { affectionLevel = 0, startPath = "affection_0_c", requiredThemeId = string.Empty },
            new NpcAffectionDialogueEntry { affectionLevel = 1, startPath = "affection_1_c", requiredThemeId = string.Empty }
        }.ToList();
        EditorUtility.SetDirty(merchant);
    }

    private static void ImportTexts(TextRow[] rows)
    {
        const string csvPath = "DataSheets/Localization/GameText.csv";
        var collection = LocalizationEditorSettings.GetStringTableCollection(GameText.TableName);
        if (collection == null) throw new InvalidOperationException("Existing GameText collection is required.");
        string[] codes = { "ko", "en", "ja", "zh-Hans", "zh-Hant" };
        string csv = File.ReadAllText(csvPath);
        string newline = csv.Contains("\r\n") ? "\r\n" : "\n";
        foreach (var row in rows)
        {
            if (row.values.Length != 5 || row.values.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("Incomplete merchant localization.");
            if (row.asset != null) { row.asset.rewardText = row.values[0]; EditorUtility.SetDirty(row.asset); }
            for (int i = 0; i < codes.Length; i++)
            {
                var table = collection.StringTables.Single(table => table.LocaleIdentifier.Code == codes[i]);
                table.AddEntry(row.key, row.values[i]); EditorUtility.SetDirty(table);
            }
            string serialized = string.Join(",", new[] { row.key }.Concat(row.values).Select(CsvCell));
            string expression = "(?m)^" + Regex.Escape(row.key) + ",[^\\r\\n]*";
            csv = Regex.IsMatch(csv, expression) ? Regex.Replace(csv, expression, _ => serialized)
                : csv.TrimEnd('\r', '\n') + newline + serialized + newline;
        }
        EditorUtility.SetDirty(collection.SharedData);
        File.WriteAllText(csvPath, csv, new UTF8Encoding(false));
        RegisterAssetKeys(rows.Where(row => row.asset != null).ToArray());
    }

    private static void RegisterAssetKeys(TextRow[] rows)
    {
        string source = File.ReadAllText(KeyMap);
        var additions = new StringBuilder();
        foreach (var row in rows)
        {
            string identity = row.asset.GetType().FullName + "/" + row.asset.name + "/rewardText";
            if (!source.Contains("\"" + identity + "\"")) additions.AppendLine("        { \"" + identity + "\", \"" + row.key + "\" },");
        }
        if (additions.Length > 0)
        {
            int end = source.LastIndexOf("    };", StringComparison.Ordinal);
            if (end < 0) throw new InvalidOperationException("Asset key map format changed.");
            File.WriteAllText(KeyMap, source.Insert(end, additions.ToString()), new UTF8Encoding(false));
        }
        string inventory = File.ReadAllText(InventoryPath);
        foreach (var row in rows)
        {
            var record = new InventoryRow { key = row.key, path = AssetDatabase.GetAssetPath(row.asset), type = row.asset.GetType().FullName,
                owner = row.asset.name, field = "rewardText", text = row.values[0] };
            string block = JsonUtility.ToJson(record, true);
            var match = Regex.Match(inventory, "(?s)\\{[^{}]*\"key\"\\s*:\\s*\"" + Regex.Escape(row.key) + "\"[^{}]*\\}");
            if (match.Success) inventory = inventory.Substring(0, match.Index) + block + inventory.Substring(match.Index + match.Length);
            else
            {
                int end = inventory.LastIndexOf(']');
                while (end > 0 && char.IsWhiteSpace(inventory[end - 1])) end--;
                inventory = inventory.Insert(end, ",\n    " + block.Replace("\n", "\n    "));
            }
        }
        File.WriteAllText(InventoryPath, inventory, new UTF8Encoding(false));
    }

    private static TextRow Text(AffectionEffect asset, string[] values) => new TextRow
    { asset = asset, key = "asset." + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)) + ".rewardText", values = values };
    private static string CsvCell(string value) => value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
    private static T Ensure<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>(); asset.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(asset, path); return asset;
    }
    private static void Configure(UnityEngine.Object asset, string field, int value)
    { var data = new SerializedObject(asset); data.FindProperty(field).intValue = value; data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset); }
    private static void Configure(UnityEngine.Object asset, string field, float value)
    { var data = new SerializedObject(asset); data.FindProperty(field).floatValue = value; data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset); }
    private sealed class TextRow { public string key; public string[] values; public AffectionEffect asset; }
    [Serializable] private sealed class InventoryRow { public string key, path, type, owner, field, text; }
}
