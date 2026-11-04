using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.CSV;
using UnityEditor.Localization.Plugins.CSV.Columns;
using UnityEngine;

/// <summary>병 속의 슬라임의 아이콘/유물 데이터와 번역을 Unity API로 제작한다.</summary>
public static class SlimeBottleAuthoring
{
    public const string RelicId = "RD_SlimeBottle";
    private const string Definition = "Assets/_Project/Data/Items/Relics/Definitions/RD_SlimeBottle.asset";
    private const string Strategy = "Assets/_Project/Data/Items/Relics/Strategies/RL_SlimeBottle.asset";
    private const string Icon = "Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_SlimeBottle.png";

    [MenuItem("Tools/Content/Relics/Author Slime Bottle")]
    public static void Apply()
    {
        AssetDatabase.ImportAsset(Icon, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(Icon);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();

        RelicLogic_SlimeBottle logic = LoadOrCreate<RelicLogic_SlimeBottle>(Strategy);
        logic.blockChance = 0.1f;
        EditorUtility.SetDirty(logic);
        RelicDefinition definition = LoadOrCreate<RelicDefinition>(Definition);
        definition.relicId = RelicId;
        definition.displayName = "병 속의 슬라임";
        definition.description = "작은 슬라임이 가끔 튀어나와 피해를 막아줍니다.";
        definition.icon = AssetDatabase.LoadAssetAtPath<Sprite>(Icon);
        definition.rarity = ItemRarity.Epic;
        definition.maxLevel = 1;
        definition.dropLevel = 1;
        definition.logic = logic;
        EditorUtility.SetDirty(definition);
        ItemDatabase database = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/_Project/Data/Items/ItemDatabase.asset");
        database.allRelics ??= new List<RelicDefinition>();
        if (!database.allRelics.Contains(definition)) database.allRelics.Add(definition);
        EditorUtility.SetDirty(database);
        // Melta affection reward wiring belongs to another task. Do not grant a default unlock here.
        AssetDatabase.SaveAssets();
        Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        Debug.Log("[SlimeBottle] Authored " + RelicId + "; definition GUID=" + AssetDatabase.AssetPathToGUID(Definition));
    }

    public static void Localize()
    {
        var collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>("Assets/_Project/Data/Localization/GameText.asset");
        var mappings = new List<CsvColumns>
        {
            new KeyIdColumns { KeyFieldName = "key", IncludeId = false, IncludeSharedComments = false }
        };
        foreach (string code in new[] { "ko", "en", "ja", "zh-Hans", "zh-Hant" })
            mappings.Add(new LocaleColumns { LocaleIdentifier = code, FieldName = code, IncludeComments = false });
        using (var reader = File.OpenText("Library/CodexSlimeBottle/content.csv"))
            Csv.ImportInto(reader, collection, mappings, true, removeMissingEntries: false);
        EditorUtility.SetDirty(collection.SharedData);
        foreach (var table in collection.StringTables) EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        Debug.Log("[SlimeBottle] Imported name/description/effect in five locales. Slime! remains the requested literal.");
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
