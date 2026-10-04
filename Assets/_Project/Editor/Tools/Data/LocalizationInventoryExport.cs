using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class LocalizationInventoryExport
{
    [Serializable] internal sealed class Row
    {
        public string key, path, type, owner, field, text;
    }
    [Serializable] private sealed class Document { public List<Row> rows = new(); }
    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
    {
        "displayName", "storyText", "description", "simpleDescription", "abilityName", "effectTemplate",
        "attributeName", "attackStyle", "stageText", "title", "body", "label", "displayNameOverride", "labelOverride",
        "npcName", "rewardDescription", "rewardText", "interactPromptText", "upgradeName", "nameText", "effectText", "text"
    };

    public static void ExportFlowBatch()
    {
        try
        {
            var document = new Document();
            var paths = AssetDatabase.FindAssets("t:ScriptableObject", new[] {
                "Assets/_Project/Data/Dialogue/SpeechData", "Assets/_Project/Data/Dialogue/IntroAndOutro",
                "Assets/_Project/Data/SceneFlow/Routes" }).Select(AssetDatabase.GUIDToAssetPath);
            foreach (string path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (!(asset is BossSpeechData || asset is PlayerSpeechData || asset is TitleIntroSequenceSO ||
                      asset is EndingOutroSequenceSO || asset is CorridorBossRouteSetSO)) continue;
                string guid = AssetDatabase.AssetPathToGUID(path);
                var property = new SerializedObject(asset).GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.String || string.IsNullOrWhiteSpace(property.stringValue)) continue;
                    string field = property.propertyPath;
                    if (!(System.Text.RegularExpressions.Regex.IsMatch(field, @"^entries\.Array\.data\[\d+\]\.lines\.Array\.data\[\d+\]$") ||
                          System.Text.RegularExpressions.Regex.IsMatch(field, @"^slides\.Array\.data\[\d+\]\.text$") ||
                          field == "corridorLocationName" || field == "bossLocationName")) continue;
                    document.rows.Add(new Row { key = "asset." + guid + "." + field, path = path,
                        type = asset.GetType().FullName, owner = asset.name, field = field, text = property.stringValue });
                }
            }
            File.WriteAllText("DataSheets/Localization/FlowNewAssetFields.json", JsonUtility.ToJson(document, true));
            Debug.Log("[FlowLocalization] Exported " + document.rows.Count + " reviewed nested display fields; source assets unchanged.");
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }

    public static void RunBatch() => Export(true);

    private static void Export(bool exit)
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Localization inventory must run in a separate batch Editor after the interactive Editor closes.");
        try
        {
            var document = new Document();
            var approved = JsonUtility.FromJson<Document>(File.ReadAllText("DataSheets/Localization/AssetInventory.json"));
            foreach (string path in approved.rows.Select(r => r.path).Distinct(StringComparer.Ordinal))
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                Debug.Log("[LocalizationInventory] Reading asset: " + path);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null) continue;
                var serialized = new SerializedObject(asset);
                var property = serialized.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    // Do not walk recursive SerializeReference graphs: only authored value fields are text sources.
                    enterChildren = property.propertyType != SerializedPropertyType.ManagedReference;
                    if (property.propertyType != SerializedPropertyType.String || !Fields.Contains(property.name) ||
                        string.IsNullOrWhiteSpace(property.stringValue)) continue;
                    document.rows.Add(new Row
                    {
                        key = "asset." + guid + "." + property.propertyPath,
                        path = path, type = asset.GetType().FullName, owner = asset.name,
                        field = property.propertyPath, text = property.stringValue
                    });
                }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Monsters", "Assets/_Project/Prefabs/Bosses" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var enemy in prefab.GetComponentsInChildren<Enemy>(true))
                {
                    var property = new SerializedObject(enemy).FindProperty("enemyName");
                    if (property == null || string.IsNullOrWhiteSpace(property.stringValue)) continue;
                    var id = GlobalObjectId.GetGlobalObjectIdSlow(enemy);
                    document.rows.Add(new Row
                    {
                        key = "asset." + guid + "." + id.targetObjectId + ".enemyName",
                        path = path, type = enemy.GetType().FullName, owner = enemy.name,
                        field = "enemyName", text = property.stringValue
                    });
                }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/UI" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("GlobalUIRoot_") || path.Contains("_Backup")) continue;
                Debug.Log("[LocalizationInventory] Reading prefab: " + path);
                var root = PrefabUtility.LoadPrefabContents(path);
                try { AppendTexts(document, path, guid, root.GetComponentsInChildren<TMP_Text>(true)); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var sceneSetting in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                Debug.Log("[LocalizationInventory] Reading scene: " + sceneSetting.path);
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(sceneSetting.path);
                bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
                if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(sceneSetting.path, OpenSceneMode.Additive);
                try
                {
                    AppendTexts(document, sceneSetting.path, AssetDatabase.AssetPathToGUID(sceneSetting.path),
                        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)));
                }
                finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true); }
            }
            Directory.CreateDirectory("DataSheets/Localization");
            File.WriteAllText("DataSheets/Localization/Inventory.json", JsonUtility.ToJson(document, true));
            Debug.Log("[LocalizationInventory] Exported " + document.rows.Count + " authored fields.");
            if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(1); }
    }

    private static void AppendTexts(Document document, string path, string guid, IEnumerable<TMP_Text> texts)
    {
        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text.text) || text.text == "New Text" || text.text == "New Text1") continue;
            UnityEngine.Object persistentText = PrefabUtility.GetCorrespondingObjectFromSource(text);
            var id = GlobalObjectId.GetGlobalObjectIdSlow(persistentText != null ? persistentText : text);
            if (id.targetObjectId == 0)
                throw new InvalidOperationException("Text has no persistent identity: " + path + " :: " + Hierarchy(text.transform));
            document.rows.Add(new Row
            {
                key = "ui." + guid + "." + id.targetObjectId,
                path = path, type = text.GetType().FullName, owner = Hierarchy(text.transform),
                field = "m_text", text = text.text
            });
        }
    }

    private static string Hierarchy(Transform transform) =>
        transform.parent != null ? Hierarchy(transform.parent) + "/" + transform.name : transform.name;
}
