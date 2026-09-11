using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates optional-chest authoring prefabs without converting existing room placements.
/// Preview sprites have no interaction/loot components and are never realized at runtime.
/// </summary>
public static class ChestPossibleAuthoringUtility
{
    public const string NormalPrefabPath = "Assets/_Project/Prefabs/Items/Chests/ChestPossible.prefab";
    public const string KillLockPrefabPath = "Assets/_Project/Prefabs/Items/Chests/ChestPossible_KillLock.prefab";

    [MenuItem("Tools/Dungeon/Create Chest Possible Prefabs")]
    public static void CreatePrefabs()
    {
        CreateIfMissing(NormalPrefabPath, "Assets/_Project/Prefabs/Items/Chests/TreasureChest.prefab", false);
        CreateIfMissing(KillLockPrefabPath, "Assets/_Project/Prefabs/Items/Chests/KillLockTresureChest.prefab", true);
        AssetDatabase.SaveAssets();
    }

    private static void CreateIfMissing(string path, string sourcePath, bool requireKillLock)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            return;
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        TreasureChest chest = source != null ? source.GetComponent<TreasureChest>() : null;
        if (chest == null || (requireKillLock && source.GetComponentInChildren<ChestMonsterKillLock>(true) == null))
            throw new InvalidOperationException($"Invalid chest source: {sourcePath}");

        var root = new GameObject(requireKillLock ? "ChestPossible_KillLock" : "ChestPossible");
        try
        {
            root.AddComponent<ChestPossible>().EditorConfigure(chest);
            var chestData = new SerializedObject(chest);
            var sourceRenderer = chestData.FindProperty("chestSpriteRenderer").objectReferenceValue as SpriteRenderer;
            if (sourceRenderer == null)
                sourceRenderer = source.GetComponentInChildren<SpriteRenderer>(true);
            if (sourceRenderer != null)
            {
                var preview = new GameObject("CandidatePreview_EditorOnly");
                preview.transform.SetParent(root.transform, false);
                preview.transform.localPosition = source.transform.InverseTransformPoint(sourceRenderer.transform.position);
                preview.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * sourceRenderer.transform.rotation;
                preview.transform.localScale = sourceRenderer.transform.lossyScale;
                var renderer = preview.AddComponent<SpriteRenderer>();
                renderer.sprite = sourceRenderer.sprite;
                renderer.color = new Color(1f, 0.85f, 0.4f, 0.5f);
                renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                renderer.sortingOrder = sourceRenderer.sortingOrder;
            }
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                throw new InvalidOperationException($"Could not create {path}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
