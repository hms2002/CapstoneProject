using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.U2D.Animation;
using UnityEngine;
using UnityEngine.U2D.Animation;

/// <summary>Authors the approved merchant slice through Unity asset/prefab APIs.</summary>
public static class NpcAffectionDialogueAuthoring
{
    public const string MerchantPath = "Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset";
    public const string PrefabPath = "Assets/_Project/Prefabs/Loot/RunMerchantGroup.prefab";
    public const string InkPath = "Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink";
    public const string SheetPath = "Assets/_Project/Art/Sprites/UI/Dialogue/NpcAffectionAvailable.png";
    public const string NormalPortraitPath = "Assets/_Project/Art/Sprites/NPCs/merchant/merchant.png";
    public const string SurprisedPortraitPath = "Assets/_Project/Art/Sprites/NPCs/merchant/merchant_surprised.png";
    public const string PortraitLibraryPath = "Assets/_Project/Data/Dialogue/NPC/SpriteLibrary/SpriteLibraryAsset_Npc_Merchant.spriteLib";

    public static void ApplyBatch()
    {
        try
        {
            Apply();
            NpcAffectionDialogueValidation.Run();
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    [MenuItem("Tools/NPC/Apply Merchant Run Affection Content")]
    public static void Apply()
    {
        var story = new Ink.Compiler(File.ReadAllText(InkPath)).Compile();
        if (story == null) throw new InvalidOperationException("Merchant Ink failed to compile.");
        string jsonPath = Path.ChangeExtension(InkPath, ".json");
        AssetDatabase.ReleaseCachedFileHandles();
        string temporary = jsonPath + ".affection-tmp";
        File.WriteAllText(temporary, story.ToJson(), new UTF8Encoding(false));
        File.Replace(temporary, jsonPath, null);
        AssetDatabase.ImportAsset(jsonPath, ImportAssetOptions.ForceUpdate);

        var merchant = AssetDatabase.LoadAssetAtPath<NPCData>(MerchantPath);
        if (merchant == null || merchant.affectionRewards == null || merchant.affectionRewards.Count == 0)
            throw new InvalidOperationException("Merchant's first affection reward is required.");
        MerchantAffectionRewardsAuthoring.ConfigureMerchant(merchant);
        merchant.greetingPaths = new[] { "greeting_a", "greeting_b", "greeting_c" };
        merchant.fullBackpackGreetingPaths = new[] { "greeting_full_a", "greeting_full_b" };
        EditorUtility.SetDirty(merchant);

        AssetDatabase.ImportAsset(SheetPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 32;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
#pragma warning disable 618
        importer.spritesheet = Enumerable.Range(0, 3).Select(index => new SpriteMetaData
        {
            name = "NpcAffectionAvailable_" + index,
            rect = new Rect(index * 32, 0, 32, 32),
            pivot = new Vector2(0.5f, 0.5f), alignment = (int)SpriteAlignment.Center
        }).ToArray();
#pragma warning restore 618
        importer.SaveAndReimport();
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
        if (frames.Length != 3) throw new InvalidOperationException("Expected three 32x32 frames.");

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            DialogueTrigger trigger = root.GetComponentsInChildren<DialogueTrigger>(true).Single(item => item.NpcData == merchant);
            var triggerData = new SerializedObject(trigger);
            var actor = (SpriteRenderer)triggerData.FindProperty("spriteRenderer").objectReferenceValue;
            Transform anchor = trigger.transform.Find("AffectionBubbleAnchor");
            if (anchor == null)
            {
                anchor = new GameObject("AffectionBubbleAnchor").transform;
                anchor.SetParent(trigger.transform, false);
                anchor.position = new Vector3(actor.bounds.center.x - 0.65f, actor.bounds.max.y + 0.3f, actor.bounds.center.z);
            }
            var bubble = trigger.GetComponentInChildren<NpcAffectionBubble>(true);
            if (bubble == null)
            {
                var view = new GameObject("AffectionBubble");
                view.transform.SetParent(trigger.transform, false);
                bubble = view.AddComponent<NpcAffectionBubble>();
            }
            SpriteRenderer renderer = bubble.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null)
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(bubble.transform, false);
                renderer = visual.AddComponent<SpriteRenderer>();
            }
            renderer.sprite = frames[0];
            renderer.enabled = false;
            renderer.sortingLayerID = actor.sortingLayerID;
            renderer.sortingOrder = actor.sortingOrder + 20;
            renderer.transform.position = anchor.position;
            renderer.transform.localScale = Vector3.one * 0.8f;
            var data = new SerializedObject(bubble);
            data.FindProperty("dialogue").objectReferenceValue = trigger;
            data.FindProperty("anchor").objectReferenceValue = anchor;
            data.FindProperty("bubbleRenderer").objectReferenceValue = renderer;
            var serializedFrames = data.FindProperty("frames");
            serializedFrames.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) serializedFrames.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        ImportMerchantText();
        AssetDatabase.SaveAssets();
        Debug.Log("[NpcAffection] Authored merchant: 4 reward levels, 8 cumulative conversation candidates, 5 greetings, 3-frame bubble.");
    }

    public static void UpdatePortraitsAndDiscountBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated asset-authoring batch.");
        try { AuthorPortraits(); UpdateConversationsBatch(); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void AuthorPortraits()
    {
        AssetDatabase.ImportAsset(NormalPortraitPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(SurprisedPortraitPath, ImportAssetOptions.ForceUpdate);
        var normalImporter = (TextureImporter)AssetImporter.GetAtPath(NormalPortraitPath);
        var surprisedImporter = (TextureImporter)AssetImporter.GetAtPath(SurprisedPortraitPath);
        if (normalImporter == null || surprisedImporter == null) throw new InvalidOperationException("Both supplied portrait images are required.");
        var settings = new TextureImporterSettings();
        normalImporter.ReadTextureSettings(settings);
        surprisedImporter.SetTextureSettings(settings);
        surprisedImporter.textureType = TextureImporterType.Sprite;
        surprisedImporter.spriteImportMode = SpriteImportMode.Single;
        surprisedImporter.spritePixelsPerUnit = normalImporter.spritePixelsPerUnit;
        surprisedImporter.filterMode = FilterMode.Point;
        surprisedImporter.mipmapEnabled = false;
        surprisedImporter.alphaIsTransparency = true;
        surprisedImporter.textureCompression = TextureImporterCompression.Uncompressed;
        surprisedImporter.maxTextureSize = 2048;
        surprisedImporter.SaveAndReimport();

        var library = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(PortraitLibraryPath);
        if (library == null) throw new InvalidOperationException("Existing merchant portrait library is missing.");
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(library, out string guid, out long localId);
        var edited = UnityEngine.Object.Instantiate(library);
        try
        {
            edited.AddCategoryLabel(AssetDatabase.LoadAssetAtPath<Sprite>(NormalPortraitPath), "Face", "Normal");
            edited.AddCategoryLabel(AssetDatabase.LoadAssetAtPath<Sprite>(SurprisedPortraitPath), "Face", "Surprised");
            AssetDatabase.ReleaseCachedFileHandles();
            SpriteLibrarySourceAssetFactory.SaveAsSourceAsset(edited, PortraitLibraryPath);
            AssetDatabase.ImportAsset(PortraitLibraryPath, ImportAssetOptions.ForceUpdate);
        }
        finally { UnityEngine.Object.DestroyImmediate(edited); }
        var updated = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(PortraitLibraryPath);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(updated, out string updatedGuid, out long updatedId);
        if (guid != updatedGuid || localId != updatedId) throw new InvalidOperationException("Portrait library identity changed.");
    }

    // Scoped migration: preserve reward tuning, prefab placement and the existing sprite import.
    public static void UpdateConversationsBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run the isolated batch migration outside the user's Editor.");
        try
        {
            var story = new Ink.Compiler(File.ReadAllText(InkPath)).Compile();
            if (story == null) throw new InvalidOperationException("Merchant Ink failed to compile.");
            string jsonPath = Path.ChangeExtension(InkPath, ".json");
            AssetDatabase.ReleaseCachedFileHandles();
            string temporary = jsonPath + ".affection-tmp";
            File.WriteAllText(temporary, story.ToJson(), new UTF8Encoding(false));
            File.Replace(temporary, jsonPath, null);
            AssetDatabase.ImportAsset(jsonPath, ImportAssetOptions.ForceUpdate);
            var merchant = AssetDatabase.LoadAssetAtPath<NPCData>(MerchantPath);
            if (merchant == null) throw new InvalidOperationException("Merchant NPCData is missing.");
            MerchantAffectionRewardsAuthoring.ConfigureConversations(merchant);
            ImportMerchantText();
            AssetDatabase.SaveAssets();
            NpcAffectionDialogueValidation.Run();
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void ImportMerchantText()
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(GameText.TableName);
        if (collection == null) throw new InvalidOperationException("Existing GameText collection is required.");
        string[] codes = { "ko", "en", "ja", "zh-Hans", "zh-Hant" };
        using (var reader = File.OpenText("DataSheets/Localization/GameText.csv"))
        {
            var rows = LocalizationSheetSetup.ReadRows(reader).ToArray();
            if (!rows[0].SequenceEqual(new[] { "key", "ko", "en", "ja", "zh-Hans", "zh-Hant" }))
                throw new InvalidOperationException("Unexpected GameText CSV header.");
            foreach (var row in rows.Skip(1).Where(row => row[0].StartsWith("dialogue.merchant.run.", StringComparison.Ordinal)))
                for (int i = 0; i < codes.Length; i++)
                {
                    var table = collection.StringTables.Single(item => item.LocaleIdentifier.Code == codes[i]);
                    table.AddEntry(row[0], row[i + 1]);
                    EditorUtility.SetDirty(table);
                }
        }
        EditorUtility.SetDirty(collection.SharedData);
    }
}
