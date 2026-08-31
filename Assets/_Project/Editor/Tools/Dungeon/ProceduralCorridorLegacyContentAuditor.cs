using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임:
/// - 운영 절차 복도 씬에 정적으로 남은 레거시 게임플레이 오브젝트를 생성기 전용 루트와 구분해 보고한다.
/// - 포털·문·상자와 그 레거시 기믹·스폰 지점만 제한적으로 제거하고 MonsterSpawner의 해당 참조를 함께 정리한다.
/// - 제거 판단 전에 오브젝트 계층, 핵심 컴포넌트와 원본 프리팹 경로를 재현 가능한 형태로 제공한다.
/// </summary>
public static class ProceduralCorridorLegacyContentAuditor
{
    private static readonly string[] ScenePaths =
    {
        "Assets/_Project/Scenes/ProceduralShadowCorridor.unity",
        "Assets/_Project/Scenes/ProceduralDragonCorridor.unity",
        "Assets/_Project/Scenes/ProceduralSlimeCorridor.unity"
    };

    private static readonly Type[] GameplayTypes =
    {
        typeof(ScenePortal),
        typeof(TreasureChest),
        typeof(DoorObject),
        typeof(Enemy),
        typeof(ChestMonsterKillLock),
        typeof(LeverShortcut),
        typeof(StatueShortcut),
        typeof(MonsterSpawnContainer),
        typeof(BossTalkManager),
        typeof(RoomDoorMonsterKillLock),
        typeof(MonsterSpawnRoomGroup),
        typeof(SceneTravelEndpoint)
    };

    private static readonly Type[] SafeCleanupTypes =
    {
        typeof(ScenePortal),
        typeof(TreasureChest),
        typeof(DoorObject),
        typeof(LeverShortcut),
        typeof(StatueShortcut),
        typeof(MonsterSpawnContainer)
    };

    [MenuItem("Tools/Dungeon/Audit Procedural Corridor Legacy Scene Objects")]
    public static void Audit()
    {
        for (int sceneIndex = 0; sceneIndex < ScenePaths.Length; sceneIndex++)
            AuditScene(ScenePaths[sceneIndex]);
    }

    [MenuItem("Tools/Dungeon/Clean Safe Procedural Corridor Legacy Objects")]
    public static void CleanSafeLegacyObjects()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        int totalRemoved = 0;
        for (int sceneIndex = 0; sceneIndex < ScenePaths.Length; sceneIndex++)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePaths[sceneIndex], OpenSceneMode.Single);
            int removed = CleanSafeLegacyObjectsInScene(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Failed to save cleaned scene: {ScenePaths[sceneIndex]}");

            totalRemoved += removed;
            Debug.Log(
                $"[ProceduralCorridorLegacyCleanup] Scene={scene.name}, RemovedRoots={removed}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log(
            $"[ProceduralCorridorLegacyCleanup] Cleaned {ScenePaths.Length} scenes, " +
            $"RemovedRoots={totalRemoved}.");
    }

    private static int CleanSafeLegacyObjectsInScene(Scene scene)
    {
        DungeonRoomBuilder builder = FindSceneComponent<DungeonRoomBuilder>(scene);
        MonsterSpawner monsterSpawner = FindSceneComponent<MonsterSpawner>(scene);
        if (builder == null || monsterSpawner == null)
            throw new InvalidOperationException($"Required procedural infrastructure is missing in '{scene.name}'.");

        HashSet<GameObject> candidates = CollectSafeCleanupRoots(scene, builder);
        List<string> externalReferences = FindExternalReferences(scene, candidates);
        for (int referenceIndex = 0; referenceIndex < externalReferences.Count; referenceIndex++)
        {
            if (!externalReferences[referenceIndex].Contains(
                    "Owner=MonsterSpawner:MonsterSpawner, Property=spawnPoints.",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unexpected external reference blocks cleanup in '{scene.name}': " +
                    externalReferences[referenceIndex]);
            }
        }

        RemoveCandidateSpawnPointReferences(monsterSpawner, candidates);
        var removedNames = new List<string>();
        foreach (GameObject candidate in candidates)
        {
            if (candidate == null)
                continue;

            removedNames.Add(candidate.name);
            UnityEngine.Object.DestroyImmediate(candidate);
        }

        HashSet<GameObject> remaining = CollectSafeCleanupRoots(scene, builder);
        if (remaining.Count != 0 ||
            FindSceneComponent<DungeonGenerator>(scene) == null ||
            FindSceneComponent<PlayerSpawner>(scene) == null ||
            FindSceneComponent<GlobalUIRoot>(scene) == null ||
            builder.FloorTilemap == null ||
            builder.WallTilemap == null)
        {
            throw new InvalidOperationException(
                $"Cleanup verification failed in '{scene.name}'. Remaining={remaining.Count}.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(
            $"[ProceduralCorridorLegacyCleanup] Removed from {scene.name}: " +
            string.Join(", ", removedNames));
        return removedNames.Count;
    }

    private static HashSet<GameObject> CollectSafeCleanupRoots(
        Scene scene,
        DungeonRoomBuilder builder)
    {
        var candidates = new HashSet<GameObject>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Component[] components = roots[rootIndex].GetComponentsInChildren<Component>(true);
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                Component component = components[componentIndex];
                if (component == null || !IsSafeCleanupComponent(component.GetType()))
                    continue;

                GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(component.gameObject);
                GameObject removalRoot = prefabRoot != null ? prefabRoot : component.gameObject;
                if (!IsUnderGeneratedRoot(removalRoot.transform, builder))
                    candidates.Add(removalRoot);
            }
        }

        return candidates;
    }

    private static void RemoveCandidateSpawnPointReferences(
        MonsterSpawner monsterSpawner,
        IReadOnlyCollection<GameObject> candidates)
    {
        var serializedSpawner = new SerializedObject(monsterSpawner);
        serializedSpawner.Update();
        SerializedProperty spawnPoints = serializedSpawner.FindProperty("spawnPoints");
        if (spawnPoints == null)
            throw new InvalidOperationException("MonsterSpawner spawnPoints contract changed.");

        for (int index = spawnPoints.arraySize - 1; index >= 0; index--)
        {
            UnityEngine.Object referencedObject =
                spawnPoints.GetArrayElementAtIndex(index).objectReferenceValue;
            Transform referencedTransform = referencedObject switch
            {
                GameObject gameObject => gameObject.transform,
                Component component => component.transform,
                _ => null
            };
            if (!IsInsideCandidate(referencedTransform, candidates))
                continue;

            int previousSize = spawnPoints.arraySize;
            spawnPoints.DeleteArrayElementAtIndex(index);
            if (spawnPoints.arraySize == previousSize)
                spawnPoints.DeleteArrayElementAtIndex(index);
        }

        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(monsterSpawner);
    }

    private static void AuditScene(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        DungeonRoomBuilder builder = FindSceneComponent<DungeonRoomBuilder>(scene);
        var report = new StringBuilder();
        report.AppendLine($"[ProceduralCorridorLegacyAudit] Scene={scene.name}");
        var candidateRoots = new HashSet<GameObject>();

        int matchCount = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        report.AppendLine($"[ProceduralCorridorLegacyAudit] RootCount={roots.Length}");
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Component[] directComponents = roots[rootIndex].GetComponents<Component>();
            var componentNames = new List<string>();
            for (int componentIndex = 0; componentIndex < directComponents.Length; componentIndex++)
            {
                if (directComponents[componentIndex] != null &&
                    directComponents[componentIndex] is not Transform)
                {
                    componentNames.Add(directComponents[componentIndex].GetType().Name);
                }
            }

            string rootPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(roots[rootIndex]);
            report.Append(" - Root=")
                .Append(roots[rootIndex].name)
                .Append(", Active=")
                .Append(roots[rootIndex].activeSelf)
                .Append(", Children=")
                .Append(roots[rootIndex].transform.childCount)
                .Append(", Components=")
                .Append(componentNames.Count > 0 ? string.Join("|", componentNames) : "<none>")
                .Append(", Prefab=")
                .Append(string.IsNullOrWhiteSpace(rootPrefabPath) ? "<none>" : rootPrefabPath)
                .AppendLine();
        }

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            if (string.Equals(roots[rootIndex].name, "BossCam", StringComparison.Ordinal) ||
                string.Equals(roots[rootIndex].name, "Grid", StringComparison.Ordinal))
            {
                candidateRoots.Add(roots[rootIndex]);
            }

            Component[] components = roots[rootIndex].GetComponentsInChildren<Component>(true);
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                Component component = components[componentIndex];
                if (component == null || !IsGameplayComponent(component.GetType()))
                    continue;

                matchCount++;
                GameObject target = component.gameObject;
                GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(target);
                GameObject removalRoot = prefabRoot != null ? prefabRoot : target;
                if (!IsUnderGeneratedRoot(removalRoot.transform, builder))
                    candidateRoots.Add(removalRoot);
                string prefabPath = prefabRoot != null
                    ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabRoot)
                    : string.Empty;
                report.Append(" - Type=")
                    .Append(component.GetType().Name)
                    .Append(", Path=")
                    .Append(GetHierarchyPath(target.transform))
                    .Append(", PrefabRoot=")
                    .Append(prefabRoot != null ? GetHierarchyPath(prefabRoot.transform) : "<none>")
                    .Append(", Prefab=")
                    .Append(string.IsNullOrWhiteSpace(prefabPath) ? "<none>" : prefabPath)
                    .Append(", Generated=")
                    .Append(IsUnderGeneratedRoot(target.transform, builder))
                    .AppendLine();
            }
        }

        List<string> externalReferences = FindExternalReferences(scene, candidateRoots);
        report.AppendLine(
            $"[ProceduralCorridorLegacyAudit] Matches={matchCount}, " +
            $"CandidateRoots={candidateRoots.Count}, ExternalReferences={externalReferences.Count}");
        for (int referenceIndex = 0; referenceIndex < externalReferences.Count; referenceIndex++)
            report.Append(" - ExternalReference=").AppendLine(externalReferences[referenceIndex]);
        Debug.Log(report.ToString());
    }

    private static List<string> FindExternalReferences(
        Scene scene,
        IReadOnlyCollection<GameObject> candidateRoots)
    {
        var references = new List<string>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Component[] components = roots[rootIndex].GetComponentsInChildren<Component>(true);
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                Component owner = components[componentIndex];
                if (owner == null || IsInsideCandidate(owner.transform, candidateRoots))
                    continue;

                var serializedOwner = new SerializedObject(owner);
                SerializedProperty property = serializedOwner.GetIterator();
                while (property.Next(enterChildren: true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    UnityEngine.Object referencedObject = property.objectReferenceValue;
                    Transform referencedTransform = referencedObject switch
                    {
                        GameObject gameObject => gameObject.transform,
                        Component component => component.transform,
                        _ => null
                    };
                    if (!IsInsideCandidate(referencedTransform, candidateRoots))
                        continue;

                    references.Add(
                        $"Owner={GetHierarchyPath(owner.transform)}:{owner.GetType().Name}, " +
                        $"Property={property.propertyPath}, Target={referencedObject.name}");
                }
            }
        }

        return references;
    }

    private static bool IsInsideCandidate(
        Transform target,
        IReadOnlyCollection<GameObject> candidateRoots)
    {
        if (target == null)
            return false;

        foreach (GameObject candidateRoot in candidateRoots)
        {
            if (candidateRoot != null &&
                (target == candidateRoot.transform || target.IsChildOf(candidateRoot.transform)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGameplayComponent(Type componentType)
    {
        for (int typeIndex = 0; typeIndex < GameplayTypes.Length; typeIndex++)
        {
            if (GameplayTypes[typeIndex].IsAssignableFrom(componentType))
                return true;
        }

        return false;
    }

    private static bool IsSafeCleanupComponent(Type componentType)
    {
        for (int typeIndex = 0; typeIndex < SafeCleanupTypes.Length; typeIndex++)
        {
            if (SafeCleanupTypes[typeIndex].IsAssignableFrom(componentType))
                return true;
        }

        return false;
    }

    private static bool IsUnderGeneratedRoot(Transform target, DungeonRoomBuilder builder)
    {
        if (target == null || builder == null)
            return false;

        return IsChildOf(target, builder.GeneratedDoorRoot) ||
               IsChildOf(target, builder.GeneratedObjectRoot) ||
               IsChildOf(target, builder.GeneratedTravelEndpointRoot) ||
               IsChildOf(target, builder.GeneratedEncounterRoot);
    }

    private static bool IsChildOf(Transform target, Transform root)
    {
        return root != null && (target == root || target.IsChildOf(root));
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            T component = roots[rootIndex].GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
            return "<missing>";

        var names = new List<string>();
        Transform cursor = target;
        while (cursor != null)
        {
            names.Add(cursor.name);
            cursor = cursor.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }
}
