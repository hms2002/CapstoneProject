using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>Installs and validates one scene-owned pathfinder, its ground maps and spawner bindings without replacing authored navigation tuning.</summary>
public static class MonsterSceneNavigationInstaller
{
    [MenuItem("Tools/Monsters/Navigation/Install In Active Scene")]
    public static void InstallActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsureSceneNavigation(SceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Monsters/Navigation/Validate Active Scene")]
    public static void ValidateActiveScene()
    {
        Validate(SceneManager.GetActiveScene());
        Debug.Log("[MonsterNavigation] Active scene navigation is valid.");
    }

    [MenuItem("Tools/Monsters/Navigation/Install All Monster Scenes")]
    public static void InstallAllMonsterScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        var report = new StringBuilder();
        var failures = new List<string>();
        int installed = 0;
        try
        {
            foreach (string path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal))
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (!IsMonsterScene(scene)) continue;
                try
                {
                    string original = File.ReadAllText(path);
                    EnsureSceneNavigation(scene, out bool changed);
                    if (changed)
                    {
                        EditorSceneManager.SaveScene(scene);
                        try { File.WriteAllText(path, RetainNavigationChanges(original, File.ReadAllText(path)), new UTF8Encoding(false)); }
                        catch { File.WriteAllText(path, original, new UTF8Encoding(false)); throw; }
                    }
                    installed++;
                    report.AppendLine(path);
                }
                catch (Exception exception)
                {
                    failures.Add(path + ": " + exception.Message);
                }
            }
        }
        finally
        {
            if (!Application.isBatchMode) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        Debug.Log($"[MonsterNavigation] Validated {installed} scenes.\n{report}");
        if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
    }

    public static TilemapPathfinder2D EnsureSceneNavigation(Scene scene) => EnsureSceneNavigation(scene, out _);

    private static TilemapPathfinder2D EnsureSceneNavigation(Scene scene, out bool changed)
    {
        changed = false;
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Load the target scene first.");
        TilemapPathfinder2D[] existing = Find<TilemapPathfinder2D>(scene);
        if (existing.Length > 1) throw new InvalidOperationException("Multiple pathfinders: choose an owner before installing.");
        TilemapPathfinder2D pathfinder = existing.SingleOrDefault();
        var floors = new List<Tilemap>();
        foreach (var builder in Find<DungeonRoomBuilder>(scene))
            if (builder.FloorTilemap != null) floors.Add(builder.FloorTilemap);
        if (pathfinder != null)
        {
            var serialized = new SerializedObject(pathfinder);
            var primary = serialized.FindProperty("groundTilemap").objectReferenceValue as Tilemap;
            if (primary != null && primary.gameObject.scene == scene) floors.Add(primary);
            var additional = serialized.FindProperty("additionalGroundTilemaps");
            for (int i = 0; i < additional.arraySize; i++)
                if (additional.GetArrayElementAtIndex(i).objectReferenceValue is Tilemap map && map.gameObject.scene == scene)
                    floors.Add(map);
        }
        if (floors.Count == 0)
        {
            Tilemap[] candidates = Find<Tilemap>(scene).Where(t => t.gameObject.activeInHierarchy &&
                (t.name == "Ground" || t.name == "Floor")).ToArray();
            if (candidates.Length != 1) throw new InvalidOperationException($"Expected one Ground/Floor tilemap, found {candidates.Length}. Assign groundTilemap explicitly.");
            floors.Add(candidates[0]);
        }
        floors = floors.Distinct().ToList();
        Grid grid = floors[0].GetComponentInParent<Grid>();
        if (grid == null || floors.Any(t => t.GetComponentInParent<Grid>() != grid || t.gameObject.scene != scene))
            throw new InvalidOperationException("Ground maps must share a scene-local Grid.");
        int wall = LayerMask.NameToLayer("Wall");
        if (wall < 0) throw new InvalidOperationException("Wall layer is missing.");
        if (pathfinder == null)
        {
            var root = new GameObject("MonsterNavigation");
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Install monster navigation");
            pathfinder = Undo.AddComponent<TilemapPathfinder2D>(root);
            changed = true;
        }
        var data = new SerializedObject(pathfinder);
        data.FindProperty("grid").objectReferenceValue = grid;
        data.FindProperty("groundTilemap").objectReferenceValue = floors[0];
        var extra = data.FindProperty("additionalGroundTilemaps");
        extra.arraySize = floors.Count - 1;
        for (int i = 1; i < floors.Count; i++) extra.GetArrayElementAtIndex(i - 1).objectReferenceValue = floors[i];
        data.FindProperty("blockedLayers").intValue |= 1 << wall;
        if (data.ApplyModifiedProperties()) { changed = true; EditorSceneManager.MarkSceneDirty(scene); }
        foreach (MonsterSpawner spawner in Find<MonsterSpawner>(scene))
        {
            var spawnData = new SerializedObject(spawner);
            spawnData.FindProperty("pathfinder").objectReferenceValue = pathfinder;
            if (spawnData.ApplyModifiedProperties()) { changed = true; EditorSceneManager.MarkSceneDirty(scene); }
        }
        Validate(scene);
        return pathfinder;
    }

    public static void Validate(Scene scene)
    {
        TilemapPathfinder2D[] paths = Find<TilemapPathfinder2D>(scene);
        if (paths.Length != 1 || !paths[0].isActiveAndEnabled)
            throw new InvalidOperationException("Scene needs exactly one active pathfinder.");
        var data = new SerializedObject(paths[0]);
        var grid = data.FindProperty("grid").objectReferenceValue as Grid;
        var ground = data.FindProperty("groundTilemap").objectReferenceValue as Tilemap;
        if (grid == null || ground == null || grid.gameObject.scene != scene || ground.gameObject.scene != scene ||
            !ground.isActiveAndEnabled || ground.GetComponentInParent<Grid>() != grid)
            throw new InvalidOperationException("Invalid scene-local navigation Grid/ground binding.");
        if ((data.FindProperty("blockedLayers").intValue & LayerMask.GetMask("Wall")) == 0)
            throw new InvalidOperationException("Navigation does not block Wall.");
        if (paths[0].GetComponentInParent<MonsterSpawner>() != null)
            throw new InvalidOperationException("Pathfinder must not live under the persistent MonsterSpawner.");
        foreach (var builder in Find<DungeonRoomBuilder>(scene))
            if (builder.FloorTilemap != ground && !ContainsGround(data, builder.FloorTilemap))
                throw new InvalidOperationException("Generated dungeon FloorTilemap is not registered for navigation.");
        foreach (var spawner in Find<MonsterSpawner>(scene))
            if (new SerializedObject(spawner).FindProperty("pathfinder").objectReferenceValue != paths[0])
                throw new InvalidOperationException("Spawner navigation binding is missing or belongs to another scene.");
    }

    private static bool ContainsGround(SerializedObject data, Tilemap target)
    {
        var extra = data.FindProperty("additionalGroundTilemaps");
        for (int i = 0; i < extra.arraySize; i++)
            if (extra.GetArrayElementAtIndex(i).objectReferenceValue == target) return true;
        return false;
    }

    public static bool IsMonsterScene(Scene scene) => Find<Enemy>(scene).Length > 0 || Find<MonsterSpawner>(scene).Length > 0 ||
        Find<MonsterSpawnRoomGroup>(scene).Length > 0 || Find<MonsterSpawnContainer>(scene).Length > 0 ||
        Find<DungeonRoomBuilder>(scene).Length > 0 || Find<TilemapPathfinder2D>(scene).Length > 0;

    public static void VerifyInstalledScenes()
    {
        int verified = 0;
        var missing = new List<string>();
        foreach (string path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal))
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (!IsMonsterScene(scene)) continue;
            if (Find<TilemapPathfinder2D>(scene).Length == 0) { missing.Add(path); continue; }
            Validate(scene);
            EnsureSceneNavigation(scene, out bool changed);
            if (changed) throw new InvalidOperationException("Installation is not idempotent: " + path);
            verified++;
        }
        Debug.Log($"[MonsterNavigation] Idempotent and valid: {verified}; missing: {missing.Count}\n{string.Join("\n", missing)}");
        var enabled = new HashSet<string>(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
        if (missing.Any(enabled.Contains)) throw new InvalidOperationException("Enabled build scene still lacks navigation.");
    }

    private static T[] Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    // Batch migration saves only navigation data; unrelated ExecuteAlways/OnValidate UI changes are not authoring intent.
    public static string RetainNavigationChanges(string original, string saved)
    {
        if (original == saved) return original;
        const string pathGuid = "731106cde3b130a4687e22786a70aacf";
        const string spawnGuid = "e46908affd274f2419d3e3d26578055f";
        var documents = new Regex(@"(?ms)^--- !u!\d+ &(?<id>-?\d+)(?: stripped)?\r?\n.*?(?=^--- !u!|\z)");
        var old = documents.Matches(original).Cast<Match>().ToDictionary(m => m.Groups["id"].Value, m => m.Value);
        var next = documents.Matches(saved).Cast<Match>().ToDictionary(m => m.Groups["id"].Value, m => m.Value);
        var additions = new List<string>();
        var roots = new List<string>();
        foreach (var item in next.Where(p => p.Value.Contains("guid: " + pathGuid)))
        {
            if (old.ContainsKey(item.Key)) continue;
            string goId = Regex.Match(item.Value, @"m_GameObject: \{fileID: (\d+)\}").Groups[1].Value;
            if (old.ContainsKey(goId)) throw new InvalidOperationException("New navigation must own a separate scene root.");
            string go = next[goId];
            additions.Add(go);
            foreach (Match component in Regex.Matches(go, @"- component: \{fileID: (\d+)\}"))
            {
                string id = component.Groups[1].Value;
                if (old.ContainsKey(id)) throw new InvalidOperationException("Navigation fileID collision.");
                additions.Add(next[id]);
                if (next[id].StartsWith("--- !u!4 ", StringComparison.Ordinal)) roots.Add(id);
            }
        }
        if (roots.Count > 1) throw new InvalidOperationException("Multiple navigation roots in migration.");
        string newline = original.Contains("\r\n") ? "\r\n" : "\n";
        bool rootsWritten = roots.Count == 0;
        string result = documents.Replace(original, match =>
        {
            string block = match.Value;
            string id = match.Groups["id"].Value;
            if (block.Contains("guid: " + pathGuid))
            {
                foreach (string field in new[] { "grid", "groundTilemap", "additionalGroundTilemaps", "blockedLayers" })
                {
                    string pattern = @"(?ms)^  " + field + @":.*?(?=^  [A-Za-z_]|\z)";
                    string replacement = Regex.Match(next[id], pattern).Value;
                    if (replacement.Length == 0) throw new InvalidOperationException("Missing navigation field " + field);
                    block = Regex.Replace(block, pattern, _ => replacement);
                }
            }
            else if (block.Contains("guid: " + spawnGuid))
            {
                string replacement = Regex.Match(next[id], @"(?m)^  pathfinder:.*$").Value;
                if (replacement.Length == 0)
                {
                    if (additions.Count > 0) throw new InvalidOperationException("Missing spawner binding.");
                    return block;
                }
                block = Regex.IsMatch(block, @"(?m)^  pathfinder:")
                    ? Regex.Replace(block, @"(?m)^  pathfinder:.*$", _ => replacement)
                    : block.TrimEnd('\r', '\n') + newline + replacement.TrimEnd('\r') + newline;
            }
            if (block.StartsWith("--- !u!1660057539 ", StringComparison.Ordinal) && roots.Count > 0)
            {
                block = string.Concat(additions) + block.TrimEnd('\r', '\n') + newline +
                    string.Concat(roots.Select(root => "  - {fileID: " + root + "}" + newline));
                rootsWritten = true;
            }
            return block;
        });
        if (!rootsWritten) throw new InvalidOperationException("Missing SceneRoots document.");
        return result;
    }
}
