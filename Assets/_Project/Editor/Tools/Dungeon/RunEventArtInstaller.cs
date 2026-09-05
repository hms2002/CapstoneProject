using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

/// <summary>Authors event presentation only; does not rebuild rooms or change event rewards.</summary>
public static class RunEventArtInstaller
{
    private const string Art = "Assets/_Project/Art/Sprites/Events/";
    private const string Npcs = "Assets/_Project/Data/Dialogue/NPC/";
    private const string InkFolder = "Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/";
    private const string Modules = "Assets/_Project/Prefabs/Map/Procedural/Events/";

    [MenuItem("Tools/Dungeon/Run Events/Apply Event NPC Art and Dialogue")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before authoring event prefabs.");

        ApplyPrefab(Modules + "ParcelDelivery/ParcelPickupEventModule.prefab", ConfigureParcel);
        ApplyPrefab(Modules + "ParcelDelivery/ParcelDeliveryPointModule.prefab", ConfigureDelivery);
        ApplyPrefab(Modules + "BuffyHealthTime/BuffyHealthTimeEventModule.prefab", ConfigureBuffy);
        AssetDatabase.SaveAssets();
        Debug.Log("[RunEventArtInstaller] Applied Parcel/Buffy NPC data, portraits, box pile and three dummy visuals. Room generation and rewards unchanged.");
    }

    public static void InstallBatch()
    {
        try { Install(); }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    // Also called by the original test-content installers so rebuilding cannot restore placeholders.
    public static void ConfigureParcel(GameObject root)
    {
        NPCData data = PrepareNpc("Parcel", "파셀", 4101, "Parcel/Harpy_standing.png");
        Transform guide = RequiredChild(root.transform, "DeliveryGuideNpc");
        ReplaceGuide<ParcelGuideNpcInteractable>(guide, data,
            RequiredChild(guide, "NpcBody").GetComponent<SpriteRenderer>(),
            ImportSprite("Parcel/Harpy_SD.png"), 1.8f);

        Transform pile = RequiredChild(root.transform, "PermanentParcelPile");
        // The supplied box picture already contains the entire pile.
        foreach (string name in new[] { "ParcelBox_Right", "ParcelBox_Top" })
        {
            Transform obsolete = pile.Find(name);
            if (obsolete != null) UnityEngine.Object.DestroyImmediate(obsolete.gameObject);
        }
        SetVisual(RequiredChild(pile, "ParcelBox_Left").GetComponent<SpriteRenderer>(),
            ImportSprite("Parcel/Harpy_BoX.png"), 1.8f);
    }

    public static void ConfigureDelivery(GameObject root)
    {
        SetVisual(RequiredChild(root.transform, "DeliveryPointBody").GetComponent<SpriteRenderer>(),
            ImportSprite("Parcel/Harpy_BoX.png"), 1.8f);
    }

    public static void ConfigureBuffy(GameObject root)
    {
        NPCData data = PrepareNpc("Buffy", "버피", 4102, "Buffy/Orc_standing.png");
        Transform guide = RequiredChild(root.transform, "BuffyGuideNpc");
        ReplaceGuide<BuffyGuideNpcInteractable>(guide, data,
            RequiredChild(guide, "BuffyBody").GetComponent<SpriteRenderer>(),
            ImportSprite("Buffy/Orc_SD.png"), 2.1f);

        // Copy the sprite only, never the dummy's combat, health or reward components.
        Sprite dummy = Required<GameObject>("Assets/_Project/Prefabs/Monsters/TrainingDummy.prefab")
            .GetComponent<SpriteRenderer>().sprite;
        foreach (string name in new[] { "StrengthEquipment", "WheelEquipment", "LogEquipment" })
            SetVisual(RequiredChild(root.transform, name + "/" + name + "Body").GetComponent<SpriteRenderer>(), dummy, 1.8f);
    }

    private static NPCData PrepareNpc(string key, string displayName, int id, string portraitPath)
    {
        string path = Npcs + key + "EventNpc.asset";
        NPCData npc = AssetDatabase.LoadAssetAtPath<NPCData>(path);
        // IDs are persisted by the dialogue system. Reject collisions instead of replacing another NPC.
        foreach (string guid in AssetDatabase.FindAssets("t:NPCData"))
        {
            NPCData existing = Required<NPCData>(AssetDatabase.GUIDToAssetPath(guid));
            if (existing != npc && existing.id == id)
                throw new InvalidOperationException($"NPC ID {id} already belongs to {existing.name}.");
        }

        Sprite portrait = ImportSprite(portraitPath);
        string libraryPath = Npcs + "SpriteLibrary/" + key + "EventPortrait.asset";
        SpriteLibraryAsset library = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(libraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<SpriteLibraryAsset>();
            AssetDatabase.CreateAsset(library, libraryPath);
        }
        library.AddCategoryLabel(portrait, "Face", "Normal");
        EditorUtility.SetDirty(library);

        string inkPath = InkFolder + key + "EventDialogue.ink";
        // The importer's asynchronous compiler can still be queued in batch mode.
        // Compile this self-contained dialogue synchronously before assigning its JSON.
        Ink.Runtime.Story story = new Ink.Compiler(File.ReadAllText(inkPath)).Compile();
        if (story == null) throw new InvalidOperationException($"Ink compilation failed: {inkPath}");
        string jsonPath = Path.ChangeExtension(inkPath, ".json");
        File.WriteAllText(jsonPath, story.ToJson());
        AssetDatabase.ImportAsset(jsonPath, ImportAssetOptions.ForceSynchronousImport);
        TextAsset json = Required<TextAsset>(jsonPath);

        if (npc == null)
        {
            npc = ScriptableObject.CreateInstance<NPCData>();
            AssetDatabase.CreateAsset(npc, path);
        }
        npc.id = id;
        npc.npcName = displayName;
        npc.isBoss = false;
        npc.spriteLibraryAsset = library;
        npc.emoteOffset = new Vector2(75f, 100f);
        SetReference(npc, "primaryInk", json);
        SetReference(npc, "dialogueTheme", Required<DialogueThemeSO>(Npcs + "DialogueTheme/NpcDialogueTheme.asset"));
        EditorUtility.SetDirty(npc);
        NPCDatabase database = Required<NPCDatabase>(Npcs + "NPC Database.asset");
        if (!database.npcList.Contains(npc))
        {
            database.npcList.Add(npc);
            EditorUtility.SetDirty(database);
        }
        return npc;
    }

    private static void ReplaceGuide<T>(Transform guide, NPCData npc, SpriteRenderer renderer, Sprite sprite, float height)
        where T : Component
    {
        T placeholder = guide.GetComponent<T>();
        if (placeholder != null) UnityEngine.Object.DestroyImmediate(placeholder);
        DialogueTrigger trigger = guide.GetComponent<DialogueTrigger>();
        if (trigger == null) trigger = guide.gameObject.AddComponent<DialogueTrigger>();
        SetVisual(renderer, sprite, height);
        Transform prompt = guide.Find("DialoguePrompt");
        if (prompt == null)
        {
            prompt = new GameObject("DialoguePrompt").transform;
            prompt.SetParent(guide, false);
        }
        prompt.localPosition = new Vector3(0f, height * 0.5f + 0.3f, 0f);
        SetReference(trigger, "npcData", npc);
        SetReference(trigger, "spriteRenderer", renderer);
        SetReference(trigger, "promptAnchor", prompt);
    }

    private static Sprite ImportSprite(string relativePath)
    {
        string path = Art + relativePath;
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException($"Missing event texture: {path}");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return Required<Sprite>(path);
    }

    private static void SetVisual(SpriteRenderer renderer, Sprite sprite, float height)
    {
        if (renderer == null || sprite == null || sprite.bounds.size.y <= 0f)
            throw new InvalidOperationException("Missing event renderer or sprite.");
        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sortingLayerName = "Entity";
        renderer.sortingOrder = 0;
        renderer.transform.localPosition = Vector3.zero;
        float scale = height / sprite.bounds.size.y;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private static void ApplyPrefab(string path, Action<GameObject> configure)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            configure(root);
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                throw new InvalidOperationException($"Could not save event prefab: {path}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Transform RequiredChild(Transform parent, string name) =>
        parent.Find(name) ?? throw new InvalidOperationException($"Missing child: {parent.name}/{name}");

    private static T Required<T>(string path) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"Missing asset: {path}");

    private static void SetReference(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null) throw new InvalidOperationException($"Missing field: {target.name}.{name}");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
