#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityGAS;

/// <summary>Authors the approved Spiri reward and player mug through Unity APIs, preserving existing asset identities.</summary>
public static class SpiriAlcoholRewardAuthoring
{
    private const string Effects = "Assets/_Project/Data/Abilities/Effects/";
    private const string PlayerPath = "Assets/_Project/Prefabs/Player/PF Player.prefab";
    private const string TagPath = "Assets/_Project/Resources/Tags/State.Confusion.asset";
    private const string RewardPath = "Assets/_Project/Data/Dialogue/Affection/SpiriAlcoholPuddleReward.asset";
    private const string RewardIconPath = "Assets/_Project/Art/Sprites/UI/ApprovedUiSkin/Icon_SpiriAlcoholReward.png";
    private const string SpritePath = "Assets/_Project/Art/Sprites/Monsters/DragonLevel_BeerMonster/Drink.png";
    private const string HeadMugPath = "Assets/_Project/Art/Sprites/UI/StatusIcons/AlcoholMonsterMug.png";
    private const string CsvPath = "DataSheets/Localization/GameText.csv";
    private const string InventoryPath = "DataSheets/Localization/AssetInventory.json";
    private const string KeyMapPath = "Assets/_Project/Runtime/Core/Presentation/GameTextAssetKeys.g.cs";
    private const string ReportFolder = "Library/CodexSpiriAlcohol";
    private static readonly string[] LocaleCodes = { "ko", "en", "ja", "zh-Hans", "zh-Hant" };
    private static readonly string[][] Translations =
    {
        new[] { "혼란", "Confusion", "混乱", "混乱", "混亂" },
        new[] { "술기운에 방향 감각이 흐려진다.", "Alcohol clouds your sense of direction.", "酔いで方向感覚が鈍る。", "醉意让方向感变得模糊。", "醉意讓方向感變得模糊。" },
        new[]
        {
            "일반 걷기의 상하좌우 입력이 반전됩니다. 장판을 벗어나 맥주잔이 비워지면 해제됩니다.",
            "Normal walking inputs are reversed in all four directions. Ends when the mug empties after leaving the puddle.",
            "通常歩行の上下左右の入力が反転します。酒の床から離れ、ジョッキが空になると解除されます。",
            "普通行走的上下左右输入反转。离开酒液区域后，啤酒杯清空时解除。",
            "普通行走的上下左右輸入反轉。離開酒液區域後，啤酒杯清空時解除。"
        },
        new[]
        {
            "더 이상 술 위에서 혼란에 빠지지 않으며, 발걸음은 가벼워지고 공격은 빨라집니다.",
            "Alcohol puddles no longer confuse you. Your steps grow lighter and your attacks faster.",
            "酒の上でも混乱しなくなり、足取りは軽く、攻撃は速くなります。",
            "不再因脚下的酒液而陷入混乱，步伐更加轻盈，攻击更加迅捷。",
            "不再因腳下的酒液而陷入混亂，步伐更加輕盈，攻擊更加迅捷。"
        }
    };

    [MenuItem("Tools/Spiri/Author Alcohol Exposure And Affection Reward")]
    public static void Apply()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode before authoring Spiri assets.");
        var alcoholEffect = Load<GameplayEffect>(Effects + "GE_Puddle_AlcoholBuff.asset");
        var alcoholDefinition = Load<CombatBuffDebuffApplicationDefinition>(Effects + "CBD_Puddle_AlcoholBuff.asset");
        var npc = Load<NPCData>("Assets/_Project/Data/Dialogue/NPC/DragonBossNpc.asset");
        Require(npc.id == 2001, "The target NPC must remain Spiri (2001).");
        Require(alcoholDefinition.GameplayEffect == alcoholEffect, "Alcohol definition references an unexpected GameplayEffect.");
        Load<GameObject>(PlayerPath);
        var mugSprite = AssetDatabase.LoadAllAssetsAtPath(SpritePath).OfType<Sprite>().Single(sprite => sprite.name == "Drink_0");
        var rewardIcon = Load<Sprite>(RewardIconPath);
        var shader = Load<Shader>("Assets/_Project/Art/Shaders/AlcoholMugFill.shader");
        var collection = Load<StringTableCollection>("Assets/_Project/Data/Localization/GameText.asset");
        Require(LocaleCodes.All(code => collection.StringTables.Any(table => table.LocaleIdentifier.Code == code)), "All five existing GameText locales are required.");
        Require(File.ReadAllText(CsvPath).TrimStart('\uFEFF').Split(new[] { '\r', '\n' }, 2)[0] == "key,ko,en,ja,zh-Hans,zh-Hant", "GameText CSV locale columns changed.");

        GameplayTag confusionTag = AuthorConfusionTag();
        var confusionEffect = Ensure<GE_StatusOnlyDuration>(Effects + "GE_Puddle_Confusion.asset");
        Undo.RecordObject(confusionEffect, "Author alcohol confusion effect");
        confusionEffect.effectName = "GE_Puddle_Confusion";
        confusionEffect.description = "술 장판에서 잔이 넘치면 일반 걷기 입력을 반전하고, 잔이 비워지면 해제합니다.";
        confusionEffect.icon = mugSprite;
        confusionEffect.duration = 5f;
        confusionEffect.canStack = false;
        confusionEffect.maxStacks = 1;
        confusionEffect.grantedTags = new List<GameplayTag> { confusionTag };
        EditorUtility.SetDirty(confusionEffect);

        var hud = Ensure<StatusHudDefinition>(Effects + "SHD_Puddle_Confusion.asset");
        var hudData = new SerializedObject(hud);
        Property(hudData, "statusId").stringValue = "puddle_confusion";
        Property(hudData, "group").intValue = (int)StatusHudGroup.Debuff;
        Property(hudData, "priority").intValue = 80;
        Property(hudData, "icon").objectReferenceValue = mugSprite;
        Property(hudData, "nameText").stringValue = Translations[0][0];
        Property(hudData, "storyText").stringValue = Translations[1][0];
        Property(hudData, "effectText").stringValue = Translations[2][0];
        Property(hudData, "showStacksByDefault").boolValue = false;
        // Confusion ends at drain completion; the renewable GE duration is not the visible lifetime.
        Property(hudData, "showDurationByDefault").boolValue = false;
        hudData.ApplyModifiedProperties();
        var confusionDefinition = Ensure<CombatBuffDebuffApplicationDefinition>(Effects + "CBD_Puddle_Confusion.asset");
        var definitionData = new SerializedObject(confusionDefinition);
        Property(definitionData, "gameplayEffect").objectReferenceValue = confusionEffect;
        Property(definitionData, "statusHudDefinition").objectReferenceValue = hud;
        Property(definitionData, "showOnPlayerHud").boolValue = true;
        Property(definitionData, "lifetimePolicy").intValue = (int)BuffDebuffLifetimePolicy.IndependentDuration;
        definitionData.ApplyModifiedProperties();

        Undo.RecordObject(alcoholEffect, "Set Spiri alcohol buff duration");
        alcoholEffect.duration = 5f;
        EditorUtility.SetDirty(alcoholEffect);
        var reward = Ensure<InfoOnlyAffectionEffect>(RewardPath);
        Undo.RecordObject(reward, "Author Spiri alcohol affection reward");
        reward.rewardText = Translations[3][0];
        reward.rewardIcon = rewardIcon;
        EditorUtility.SetDirty(reward);
        Undo.RecordObject(npc, "Register Spiri affection level 1 reward");
        npc.affectionRewards ??= new List<AffectionReward>();
        Require(npc.affectionRewards.All(entry => entry.effect != reward || entry.targetLevel == 1), "Existing Spiri reward is registered at another level.");
        if (!npc.affectionRewards.Any(entry => entry.effect == reward))
            npc.affectionRewards.Add(new AffectionReward { targetLevel = 1, effect = reward });
        EditorUtility.SetDirty(npc);

        var mugMaterial = AuthorMaterial("Assets/_Project/Art/Materials/M_AlcoholMugFill.mat", shader, false);
        var dropMaterial = AuthorMaterial("Assets/_Project/Art/Materials/M_AlcoholMugOverflowDrop.mat", shader, true);
        AuthorPlayer(alcoholDefinition, confusionDefinition, confusionTag, mugSprite, mugMaterial, dropMaterial);
        var texts = new[]
        {
            Text(hud, "nameText", Translations[0]), Text(hud, "storyText", Translations[1]),
            Text(hud, "effectText", Translations[2]), Text(reward, "rewardText", Translations[3])
        };
        AppendLocalization(collection, texts);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(ReportFolder);
        File.WriteAllText(ReportFolder + "/authoring.json", JsonUtility.ToJson(new Report { confusionTagId = confusionTag.Id, texts = texts }, true), new UTF8Encoding(false));
        Debug.Log("[SpiriAlcohol] Authored confusion, level 1 reward, player mug and four text keys in five locales. Existing asset identities and alcohol modifiers retained.");
    }

    public static void ApplyBatch()
    {
        try
        {
            Apply();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static GameplayTag AuthorConfusionTag()
    {
        var existing = AssetDatabase.FindAssets("t:GameplayTag")
            .Select(guid => Load<GameplayTag>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
        var tag = AssetDatabase.LoadAssetAtPath<GameplayTag>(TagPath);
        if (tag == null)
        {
            Require(AssetDatabase.LoadMainAssetAtPath(TagPath) == null, "Confusion tag path contains an incompatible asset.");
            tag = ScriptableObject.CreateInstance<GameplayTag>();
            tag.name = "State.Confusion";
            tag.Editor_SetParent(Load<GameplayTag>("Assets/_Project/Resources/Tags/State.asset"));
            tag.Editor_SetName("Confusion");
            tag.Editor_SetId(existing.Select(value => value.Id).DefaultIfEmpty(0).Max() + 1);
            AssetDatabase.CreateAsset(tag, TagPath);
        }
        Require(tag.Path == "State.Confusion" && tag.Id > 0 && existing.All(value => value == tag || value.Id != tag.Id), "Confusion tag path or stable ID is invalid.");
        return tag;
    }

    private static Material AuthorMaterial(string path, Shader shader, bool droplet)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Require(AssetDatabase.LoadMainAssetAtPath(path) == null, "Material path contains an incompatible asset: " + path);
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
        }
        Undo.RecordObject(material, "Author alcohol mug material");
        material.shader = shader;
        material.SetFloat("_FillAmount", 0f);
        material.SetFloat("_DropletMode", droplet ? 1f : 0f);
        material.SetColor("_Color", Color.white);
        material.SetColor("_EmptyColor", new Color(.20f, .22f, .24f, 1f));
        material.SetColor("_BeerColor", new Color(1f, .62f, .12f, 1f));
        material.SetColor("_FoamColor", new Color(1f, .97f, .82f, 1f));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void AuthorPlayer(CombatBuffDebuffApplicationDefinition alcohol, CombatBuffDebuffApplicationDefinition confusion, GameplayTag tag, Sprite sprite, Material mugMaterial, Material dropMaterial)
    {
        var player = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            var input = player.GetComponent<PlayerIntentInput2D>();
            Require(input != null, "Player prefab is missing PlayerIntentInput2D.");
            var inputData = new SerializedObject(input);
            Property(inputData, "confusionTag").objectReferenceValue = tag;
            inputData.ApplyModifiedPropertiesWithoutUndo();
            var exposure = player.GetComponent<PlayerAlcoholExposure>();
            if (exposure == null) exposure = player.AddComponent<PlayerAlcoholExposure>();
            var exposureData = new SerializedObject(exposure);
            Property(exposureData, "alcoholBuffDefinition").objectReferenceValue = alcohol;
            Property(exposureData, "confusionDefinition").objectReferenceValue = confusion;
            exposureData.ApplyModifiedPropertiesWithoutUndo();

            var root = Child(player.transform, "AlcoholExposureVisual");
            root.localPosition = new Vector3(0f, 1.8f, 0f);
            root.localScale = new Vector3(.8f, .8f, 1f);
            root.gameObject.SetActive(true);
            var poses = AssetDatabase.LoadAllAssetsAtPath(HeadMugPath).OfType<Sprite>().ToDictionary(pose => pose.name);
            var mug = Renderer(root, "Mug", poses["Mug_Charging"], mugMaterial, new Vector3(0f, -.36f, 0f), Vector3.one, 30);
            var drops = new[]
            {
                Renderer(root, "OverflowDropLeft", sprite, dropMaterial, new Vector3(-.19f, .06f, 0f), new Vector3(.14f, .14f, 1f), 31),
                Renderer(root, "OverflowDropRight", sprite, dropMaterial, new Vector3(.17f, .10f, 0f), new Vector3(.14f, .14f, 1f), 31),
                Renderer(root, "OverflowDropOuter", sprite, dropMaterial, new Vector3(.22f, .02f, 0f), new Vector3(.14f, .14f, 1f), 31)
            };
            var view = player.GetComponent<AlcoholExposurePlayerVisual>();
            if (view == null) view = player.AddComponent<AlcoholExposurePlayerVisual>();
            var viewData = new SerializedObject(view);
            Property(viewData, "exposure").objectReferenceValue = exposure;
            Property(viewData, "visualRoot").objectReferenceValue = root;
            Property(viewData, "mug").objectReferenceValue = mug;
            Property(viewData, "confusedMug").objectReferenceValue = poses["Mug_Confused"];
            Property(viewData, "buffedMug").objectReferenceValue = poses["Mug_Buffed"];
            var dropReferences = Property(viewData, "overflowDroplets");
            dropReferences.arraySize = drops.Length;
            for (int i = 0; i < drops.Length; i++) dropReferences.GetArrayElementAtIndex(i).objectReferenceValue = drops[i];
            viewData.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;
        var gameObject = new GameObject(name);
        gameObject.layer = parent.gameObject.layer;
        gameObject.transform.SetParent(parent, false);
        return gameObject.transform;
    }

    private static SpriteRenderer Renderer(Transform parent, string name, Sprite sprite, Material material, Vector3 position, Vector3 scale, int order)
    {
        Transform child = Child(parent, name);
        child.localPosition = position;
        child.localScale = scale;
        var renderer = child.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = "Entity";
        renderer.sortingOrder = order;
        renderer.enabled = false;
        return renderer;
    }

    private static void AppendLocalization(StringTableCollection collection, TextRow[] rows)
    {
        string csv = File.ReadAllText(CsvPath);
        string newline = csv.Contains("\r\n") ? "\r\n" : "\n";
        var appended = new StringBuilder();
        if (!csv.EndsWith("\n", StringComparison.Ordinal)) appended.Append(newline);
        foreach (TextRow row in rows)
        {
            for (int i = 0; i < LocaleCodes.Length; i++)
            {
                var table = collection.StringTables.Single(value => value.LocaleIdentifier.Code == LocaleCodes[i]);
                var entry = table.GetEntry(row.key);
                Require(entry == null || entry.Value == row.values[i], "Existing localization differs: " + row.key + "/" + LocaleCodes[i]);
                if (entry == null) { table.AddEntry(row.key, row.values[i]); EditorUtility.SetDirty(table); }
            }
            if (!Regex.IsMatch(csv, "(?:^|\\r?\\n)\"?" + Regex.Escape(row.key) + "\"?,"))
                appended.Append(string.Join(",", new[] { row.key }.Concat(row.values).Select(CsvCell))).Append(newline);
        }
        EditorUtility.SetDirty(collection.SharedData);
        if (appended.Length > 0) File.AppendAllText(CsvPath, appended.ToString(), new UTF8Encoding(false));

        string source = File.ReadAllText(KeyMapPath);
        var mappings = new StringBuilder();
        foreach (TextRow row in rows)
        {
            string identity = row.type + "/" + row.owner + "/" + row.field;
            if (source.Contains("\"" + identity + "\""))
                Require(source.Contains("{ \"" + identity + "\", \"" + row.key + "\" }"), "Existing localization identity points to another key: " + identity);
            else mappings.Append("        { \"").Append(identity).Append("\", \"").Append(row.key).Append("\" },\n");
        }
        int mapEnd = source.LastIndexOf("    };", StringComparison.Ordinal);
        Require(mapEnd >= 0, "Asset key map format changed.");
        if (mappings.Length > 0) File.WriteAllText(KeyMapPath, source.Insert(mapEnd, mappings.ToString()), new UTF8Encoding(false));

        // Register only these rows so the existing generator retains the new keys on its next run.
        string inventory = File.ReadAllText(InventoryPath);
        var document = JsonUtility.FromJson<Inventory>(inventory);
        var additions = rows.Where(row => !document.rows.Any(existing => existing.key == row.key)).ToArray();
        if (additions.Length > 0)
        {
            int arrayEnd = inventory.LastIndexOf(']');
            Require(arrayEnd >= 0, "Asset localization inventory format changed.");
            int insertion = arrayEnd;
            while (insertion > 0 && char.IsWhiteSpace(inventory[insertion - 1])) insertion--;
            string addedRows = string.Join(",\n", additions.Select(row => "    " + JsonUtility.ToJson(new InventoryRow
            {
                key = row.key, path = row.path, type = row.type, owner = row.owner, field = row.field, text = row.values[0]
            }, true).Replace("\n", "\n    ")));
            File.WriteAllText(InventoryPath, inventory.Insert(insertion, (document.rows.Length > 0 ? "," : string.Empty) + "\n" + addedRows), new UTF8Encoding(false));
        }
    }

    private static TextRow Text(UnityEngine.Object asset, string field, string[] values)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        return new TextRow { key = "asset." + AssetDatabase.AssetPathToGUID(path) + "." + field, path = path, type = asset.GetType().FullName, owner = asset.name, field = field, values = values };
    }

    private static string CsvCell(string value) => value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0
        ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
    private static SerializedProperty Property(SerializedObject target, string field) => target.FindProperty(field)
        ?? throw new InvalidOperationException("Missing serialized field: " + target.targetObject.GetType().FullName + "/" + field);
    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Missing or incompatible asset: " + path);
        return asset;
    }
    private static T Ensure<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        Require(AssetDatabase.LoadMainAssetAtPath(path) == null, "Asset path contains an incompatible asset: " + path);
        asset = ScriptableObject.CreateInstance<T>();
        asset.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [Serializable] private sealed class Report { public int confusionTagId; public TextRow[] texts; }
    [Serializable] private sealed class TextRow { public string key, path, type, owner, field; public string[] values; }
    [Serializable] private sealed class Inventory { public InventoryRow[] rows; }
    [Serializable] private sealed class InventoryRow { public string key, path, type, owner, field, text; }
}
#endif
