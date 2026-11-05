using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.CSV;
using UnityEditor.Localization.Plugins.CSV.Columns;
using UnityEngine;

/// <summary>등불 탄막을 공유하는 그림자 혼불의 아이콘/정의/전략과 번역을 제작한다.</summary>
public static class ShadowWispAuthoring
{
    public const string RelicId = "RD_ShadowWisp";
    private const string Definition = "Assets/_Project/Data/Items/Relics/Definitions/RD_ShadowWisp.asset";
    private const string Strategy = "Assets/_Project/Data/Items/Relics/Strategies/RL_ShadowWisp.asset";
    private const string Icon = "Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_ShadowWisp.png";

    [MenuItem("Tools/Content/Relics/Author Shadow Wisp")]
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

        var lantern = AssetDatabase.LoadAssetAtPath<RelicLogic_ChloeLantern>(
            "Assets/_Project/Data/Items/Relics/Strategies/RL_ChloeLantern.asset");
        RelicLogic_ShadowWisp logic = LoadOrCreate<RelicLogic_ShadowWisp>(Strategy);
        logic.projectilePrefab = lantern.projectilePrefab;
        logic.damageEffect = lantern.damageEffect;
        logic.targetLayers = lantern.targetLayers;
        logic.wallLayers = lantern.wallLayers;
        logic.projectileSpeedMultiplier = lantern.projectileSpeedMultiplier;
        logic.projectileLifetime = lantern.projectileLifetime;
        logic.homingTurnRate = lantern.homingTurnRate;
        EditorUtility.SetDirty(logic);
        RelicDefinition definition = LoadOrCreate<RelicDefinition>(Definition);
        definition.relicId = RelicId;
        definition.displayName = "그림자 혼불";
        definition.description = "적을 처치하면 다른 적을 추적하는 혼불이 피어납니다.";
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
        // Affection reward wiring remains separate; do not add a default unlock.
        AssetDatabase.SaveAssets();
        Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        Debug.Log("[ShadowWisp] Authored " + RelicId + "; definition GUID=" + AssetDatabase.AssetPathToGUID(Definition));
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
        using (var reader = File.OpenText("Library/CodexShadowWisp/content.csv"))
            Csv.ImportInto(reader, collection, mappings, true, removeMissingEntries: false);
        EditorUtility.SetDirty(collection.SharedData);
        foreach (var table in collection.StringTables) EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        Debug.Log("[ShadowWisp] Imported name/description/effect in five locales.");
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
