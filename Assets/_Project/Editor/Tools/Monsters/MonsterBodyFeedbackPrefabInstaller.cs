using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 기존 Player와 일반 몬스터 프리팹에 진영별 몸체 충돌 정책을 일괄 저장한다.
/// - 기존 몬스터의 소프트 분리 참조와 흰색 피격 점멸 authoring 누락을 보정한다.
/// </summary>
public static class MonsterBodyFeedbackPrefabInstaller
{
    private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/PF Player.prefab";
    private const string MonsterPrefabFolder = "Assets/_Project/Prefabs/Monsters";
    private const string FlashMaterialPath = "Assets/_Project/Art/Materials/FlashMat.mat";

    private static readonly string[] EnemyLayerNames = { "Enemy", "TEMP_Enemy_LAYER" };

    [MenuItem("Tools/Authoring/Apply Monster Body Collision And Hit Flash")]
    public static void Apply()
    {
        Material flashMaterial = AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
        if (flashMaterial == null)
            throw new InvalidOperationException($"Required hit flash material is missing: {FlashMaterialPath}");

        int playerMask = LayerMask.GetMask("Player");
        int enemyMask = ResolveLayerMask(EnemyLayerNames);
        if (playerMask == 0 || enemyMask == 0)
            throw new InvalidOperationException("Player or Enemy physics layer could not be resolved.");

        ConfigurePlayerPrefab(playerMask);

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { MonsterPrefabFolder });
        int configuredMonsterCount = 0;
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            if (!ShouldConfigureMonsterPrefab(path))
                continue;

            ConfigureMonsterPrefab(path, enemyMask, flashMaterial);
            configuredMonsterCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MonsterBodyFeedbackPrefabInstaller] Configured Player and {configuredMonsterCount} monster prefabs.");
    }

    /// <summary>
    /// 책임:
    /// 배치 실행에서 공통 몬스터 재생성, 기존 프리팹 보정, authoring 검증을 한 순서로 완료한다.
    /// </summary>
    public static void GenerateApplyAndValidate()
    {
        CommonMonsterAuthoringGenerator.Generate();
        Apply();
        ValidateOrThrow();
    }

    [MenuItem("Tools/Authoring/Validate Monster Body Collision And Hit Flash")]
    public static void ValidateOrThrow()
    {
        List<string> errors = new();
        Material flashMaterial = AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
        int playerMask = LayerMask.GetMask("Player");
        int enemyMask = ResolveLayerMask(EnemyLayerNames);

        ValidateCollisionMask(PlayerPrefabPath, playerMask, errors);

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { MonsterPrefabFolder });
        int validatedMonsterCount = 0;
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            if (!ShouldConfigureMonsterPrefab(path))
                continue;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            ValidateCollisionMask(path, enemyMask, errors);
            ValidateSoftCollision(path, prefab, enemyMask, errors);
            ValidateHitFlash(path, prefab, flashMaterial, errors);
            validatedMonsterCount++;
        }

        if (errors.Count > 0)
        {
            string message = "[MonsterBodyFeedbackPrefabInstaller] Validation failed:\n- " + string.Join("\n- ", errors);
            Debug.LogError(message);
            throw new InvalidOperationException(message);
        }

        Debug.Log($"[MonsterBodyFeedbackPrefabInstaller] Player and {validatedMonsterCount} monster prefabs passed validation.");
    }

    /// <summary>
    /// 책임:
    /// Player 프리팹이 같은 Player 레이어만 통과하고 Enemy 몸체와는 실제 충돌하도록 저장한다.
    /// </summary>
    private static void ConfigurePlayerPrefab(int playerMask)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            EntityCollisionProfile2D profile = root.GetComponent<EntityCollisionProfile2D>();
            if (profile == null)
                throw new InvalidOperationException($"{PlayerPrefabPath}: EntityCollisionProfile2D is missing.");

            Collider2D[] bodyColliders = ResolveManagedBodyColliders(profile, root);
            ConfigureCollisionProfile(profile, bodyColliders, playerMask);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 책임:
    /// 지정 프리팹의 EntityCollisionProfile2D가 기대한 동일 진영 레이어만 통과하는지 검증한다.
    /// </summary>
    private static void ValidateCollisionMask(string path, int expectedMask, List<string> errors)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        EntityCollisionProfile2D profile = prefab != null ? prefab.GetComponent<EntityCollisionProfile2D>() : null;
        if (profile == null)
        {
            errors.Add($"{path}: EntityCollisionProfile2D is missing.");
            return;
        }

        SerializedObject serialized = new(profile);
        int actualMask = serialized.FindProperty("actorLayers")?.intValue ?? 0;
        if (actualMask != expectedMask)
            errors.Add($"{path}: collision actorLayers is {actualMask}, expected {expectedMask}.");
    }

    /// <summary>
    /// 책임:
    /// 일반 몬스터 프리팹의 소프트 분리 컴포넌트, 필수 참조, Enemy 전용 대상 마스크를 검증한다.
    /// </summary>
    private static void ValidateSoftCollision(
        string path,
        GameObject prefab,
        int enemyMask,
        List<string> errors)
    {
        ActorSoftCollision2D softCollision = prefab.GetComponent<ActorSoftCollision2D>();
        if (softCollision == null)
        {
            errors.Add($"{path}: ActorSoftCollision2D is missing.");
            return;
        }

        SerializedObject serialized = new(softCollision);
        string[] requiredReferences = { "bodyCollider", "body", "externalMovement", "collisionProfile" };
        for (int i = 0; i < requiredReferences.Length; i++)
        {
            SerializedProperty property = serialized.FindProperty(requiredReferences[i]);
            if (property == null || property.objectReferenceValue == null)
                errors.Add($"{path}: ActorSoftCollision2D.{requiredReferences[i]} is not assigned.");
        }

        int actualMask = serialized.FindProperty("actorLayers")?.intValue ?? 0;
        if (actualMask != enemyMask)
            errors.Add($"{path}: soft collision actorLayers is {actualMask}, expected {enemyMask}.");

        bool suspendForPassThrough = serialized.FindProperty("suspendWhileBodyPassesThroughActors")?.boolValue ?? true;
        if (suspendForPassThrough)
            errors.Add($"{path}: soft collision is suspended by the Enemy pass-through profile.");

        if (path.StartsWith("Assets/_Project/Prefabs/Monsters/CommonCorridor/", StringComparison.Ordinal))
            ValidateCommonMonsterBodyPreset(path, prefab, serialized, errors);
    }

    /// <summary>
    /// 책임:
    /// 공통 몬스터 역할에 맞는 Rigidbody 질량과 소프트 분리 저항이 프리팹에 저장되었는지 검증한다.
    /// </summary>
    private static void ValidateCommonMonsterBodyPreset(
        string path,
        GameObject prefab,
        SerializedObject softCollision,
        List<string> errors)
    {
        float expectedMass;
        float expectedResistance;
        if (prefab.name.Contains("Tank"))
        {
            expectedMass = 2.5f;
            expectedResistance = 2.25f;
        }
        else if (prefab.name.Contains("Gunner") || prefab.name.Contains("Mage"))
        {
            expectedMass = 0.85f;
            expectedResistance = 0.8f;
        }
        else
        {
            expectedMass = 1.25f;
            expectedResistance = 1.25f;
        }

        Rigidbody2D body = prefab.GetComponent<Rigidbody2D>();
        float actualResistance = softCollision.FindProperty("pushResistance")?.floatValue ?? 0f;
        if (body == null || !Mathf.Approximately(body.mass, expectedMass))
            errors.Add($"{path}: body mass does not match the role preset {expectedMass:0.##}.");
        if (!Mathf.Approximately(actualResistance, expectedResistance))
            errors.Add($"{path}: push resistance does not match the role preset {expectedResistance:0.##}.");
    }

    /// <summary>
    /// 책임:
    /// 일반 몬스터 프리팹의 점멸 대상, FlashMat, 흰색과 0.08초 설정을 검증한다.
    /// </summary>
    private static void ValidateHitFlash(
        string path,
        GameObject prefab,
        Material expectedMaterial,
        List<string> errors)
    {
        SpriteHitFlashController flash = prefab.GetComponent<SpriteHitFlashController>();
        if (flash == null)
        {
            errors.Add($"{path}: SpriteHitFlashController is missing.");
            return;
        }

        SerializedObject serialized = new(flash);
        SerializedProperty targets = serialized.FindProperty("targetRenderers");
        if (targets == null || !targets.isArray || targets.arraySize == 0)
        {
            errors.Add($"{path}: hit flash targetRenderers is empty.");
        }
        else
        {
            for (int i = 0; i < targets.arraySize; i++)
            {
                SpriteRenderer renderer = targets.GetArrayElementAtIndex(i).objectReferenceValue as SpriteRenderer;
                if (renderer == null)
                    errors.Add($"{path}: hit flash targetRenderers[{i}] is null.");
                else if (renderer.sharedMaterial != expectedMaterial)
                    errors.Add($"{path}: hit flash renderer '{renderer.name}' does not use FlashMat.");
            }
        }

        Color color = serialized.FindProperty("flashColor")?.colorValue ?? Color.clear;
        float duration = serialized.FindProperty("flashDuration")?.floatValue ?? 0f;
        if (color != Color.white)
            errors.Add($"{path}: hit flash color is not white.");
        if (!Mathf.Approximately(duration, 0.08f))
            errors.Add($"{path}: hit flash duration is {duration:0.###}, expected 0.08.");
    }

    /// <summary>
    /// 책임:
    /// 실제 이동 가능한 일반 몬스터 프리팹만 몸체 상호작용 및 점멸 보정 대상으로 선별한다.
    /// </summary>
    private static bool ShouldConfigureMonsterPrefab(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            return false;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return prefab != null &&
               prefab.CompareTag("Enemy") &&
               prefab.GetComponent<Mob>() != null &&
               prefab.GetComponent<Rigidbody2D>() != null &&
               prefab.GetComponent<ExternalMovementController2D>() != null &&
               prefab.GetComponent<EntityCollisionProfile2D>() != null;
    }

    /// <summary>
    /// 책임:
    /// 일반 몬스터 프리팹 하나에 Enemy 간 소프트 분리, Player 실제 충돌, 흰색 점멸 설정을 저장한다.
    /// </summary>
    private static void ConfigureMonsterPrefab(string path, int enemyMask, Material flashMaterial)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            ExternalMovementController2D externalMovement = root.GetComponent<ExternalMovementController2D>();
            EntityCollisionProfile2D profile = root.GetComponent<EntityCollisionProfile2D>();
            Collider2D[] bodyColliders = ResolveManagedBodyColliders(profile, root);
            Collider2D primaryBodyCollider = ResolvePrimaryBodyCollider(root, bodyColliders);
            if (primaryBodyCollider == null)
                throw new InvalidOperationException($"{path}: no non-trigger body collider was found.");

            ConfigureCollisionProfile(profile, bodyColliders, enemyMask);

            ActorSoftCollision2D softCollision = root.GetComponent<ActorSoftCollision2D>();
            if (softCollision == null)
                softCollision = root.AddComponent<ActorSoftCollision2D>();

            float pushResistance = ResolveExistingPushResistance(softCollision);
            if (TryResolveCommonMonsterBodyPreset(path, out float roleMass, out float roleResistance))
            {
                body.mass = roleMass;
                pushResistance = roleResistance;
            }

            softCollision.Configure(
                primaryBodyCollider,
                body,
                externalMovement,
                profile,
                enemyMask,
                LayerMask.GetMask("Wall"),
                suspendForPassThroughMode: false,
                configuredPushSpeed: 2.8f,
                configuredPushResistance: pushResistance,
                configuredPushDurationSeconds: 0.08f,
                configuredWallProbeDistance: 0.08f,
                configuredMaxActorsPerTick: 8);

            ConfigureHitFlash(root, flashMaterial, path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 책임:
    /// 프로필이 관리하던 몸체 collider와 base 제외 레이어는 유지하면서 동일 진영만 통과하도록 갱신한다.
    /// </summary>
    private static void ConfigureCollisionProfile(
        EntityCollisionProfile2D profile,
        Collider2D[] bodyColliders,
        int sameFactionLayerMask)
    {
        SerializedObject serialized = new(profile);
        int baseExcludedLayers = serialized.FindProperty("baseExcludedLayers")?.intValue ?? 0;
        EntityCollisionProfile2D.BodyCollisionMode defaultMode =
            (EntityCollisionProfile2D.BodyCollisionMode)(serialized.FindProperty("defaultMode")?.enumValueIndex ?? 1);

        profile.Configure(
            bodyColliders,
            baseExcludedLayers,
            sameFactionLayerMask,
            defaultMode,
            applyImmediately: true);
    }

    /// <summary>
    /// 책임:
    /// 저장된 EntityCollisionProfile2D 몸체 collider 배열을 우선 재사용하고, 비어 있으면 비트리거 collider를 복구한다.
    /// </summary>
    private static Collider2D[] ResolveManagedBodyColliders(EntityCollisionProfile2D profile, GameObject root)
    {
        SerializedObject serialized = new(profile);
        SerializedProperty bodyProperty = serialized.FindProperty("bodyColliders");
        List<Collider2D> colliders = new();
        if (bodyProperty != null && bodyProperty.isArray)
        {
            for (int i = 0; i < bodyProperty.arraySize; i++)
            {
                Collider2D collider = bodyProperty.GetArrayElementAtIndex(i).objectReferenceValue as Collider2D;
                if (collider != null && !collider.isTrigger && !colliders.Contains(collider))
                    colliders.Add(collider);
            }
        }

        if (colliders.Count > 0)
            return colliders.ToArray();

        Collider2D[] candidates = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            Collider2D candidate = candidates[i];
            if (candidate != null && !candidate.isTrigger)
                colliders.Add(candidate);
        }

        return colliders.ToArray();
    }

    /// <summary>
    /// 책임:
    /// 소프트 분리가 사용할 대표 몸체 collider를 BodyCollision 자식 우선으로 결정한다.
    /// </summary>
    private static Collider2D ResolvePrimaryBodyCollider(GameObject root, IReadOnlyList<Collider2D> bodyColliders)
    {
        Transform authoredBody = root.transform.Find("BodyCollision");
        Collider2D authoredCollider = authoredBody != null ? authoredBody.GetComponent<Collider2D>() : null;
        if (authoredCollider != null && !authoredCollider.isTrigger)
            return authoredCollider;

        return bodyColliders != null && bodyColliders.Count > 0 ? bodyColliders[0] : null;
    }

    /// <summary>
    /// 책임:
    /// 이미 authoring된 역할별 밀림 저항은 보존하고, 구버전 프리팹에는 중립값을 제공한다.
    /// </summary>
    private static float ResolveExistingPushResistance(ActorSoftCollision2D softCollision)
    {
        SerializedObject serialized = new(softCollision);
        SerializedProperty property = serialized.FindProperty("pushResistance");
        return property != null && property.floatValue >= 0.05f ? property.floatValue : 1f;
    }

    /// <summary>
    /// 책임:
    /// 공통 복도 몬스터의 역할 이름을 물리 질량과 밀림 저항 프리셋으로 변환한다.
    /// </summary>
    private static bool TryResolveCommonMonsterBodyPreset(
        string path,
        out float mass,
        out float pushResistance)
    {
        mass = 1f;
        pushResistance = 1f;
        if (!path.StartsWith("Assets/_Project/Prefabs/Monsters/CommonCorridor/", StringComparison.Ordinal))
            return false;

        string prefabName = System.IO.Path.GetFileNameWithoutExtension(path);
        if (prefabName.Contains("Tank"))
        {
            mass = 2.5f;
            pushResistance = 2.25f;
        }
        else if (prefabName.Contains("Gunner") || prefabName.Contains("Mage"))
        {
            mass = 0.85f;
            pushResistance = 0.8f;
        }
        else
        {
            mass = 1.25f;
            pushResistance = 1.25f;
        }

        return true;
    }

    /// <summary>
    /// 책임:
    /// 기존 target renderer authoring을 보존하면서 누락된 점멸 컨트롤러와 FlashMat, 흰색 0.08초 설정을 저장한다.
    /// </summary>
    private static void ConfigureHitFlash(GameObject root, Material flashMaterial, string path)
    {
        SpriteHitFlashController flash = root.GetComponent<SpriteHitFlashController>();
        if (flash == null)
            flash = root.AddComponent<SpriteHitFlashController>();

        SpriteRenderer[] targets = ResolveFlashTargets(root, flash, flashMaterial);
        if (targets.Length == 0)
            throw new InvalidOperationException($"{path}: no SpriteRenderer was suitable for hit flash.");

        for (int i = 0; i < targets.Length; i++)
            targets[i].sharedMaterial = flashMaterial;

        SerializedObject serialized = new(flash);
        SetObjectArray(serialized.FindProperty("targetRenderers"), targets);
        serialized.FindProperty("flashColor").colorValue = Color.white;
        serialized.FindProperty("flashMultiply").floatValue = 1.5f;
        serialized.FindProperty("flashDuration").floatValue = 0.08f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 책임:
    /// 기존 점멸 대상, Visual 자식, FlashMat 사용 renderer 순으로 실제 몬스터 외형 renderer를 찾는다.
    /// </summary>
    private static SpriteRenderer[] ResolveFlashTargets(
        GameObject root,
        SpriteHitFlashController flash,
        Material flashMaterial)
    {
        SerializedObject serialized = new(flash);
        SerializedProperty targetsProperty = serialized.FindProperty("targetRenderers");
        List<SpriteRenderer> targets = new();
        if (targetsProperty != null && targetsProperty.isArray)
        {
            for (int i = 0; i < targetsProperty.arraySize; i++)
            {
                SpriteRenderer renderer = targetsProperty.GetArrayElementAtIndex(i).objectReferenceValue as SpriteRenderer;
                if (renderer != null && !targets.Contains(renderer))
                    targets.Add(renderer);
            }
        }

        if (targets.Count > 0)
            return targets.ToArray();

        Transform visual = root.transform.Find("Visual");
        SpriteRenderer visualRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (visualRenderer != null)
            return new[] { visualRenderer };

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].sharedMaterial == flashMaterial)
                targets.Add(renderers[i]);
        }

        if (targets.Count > 0)
            return targets.ToArray();

        SpriteRenderer rootRenderer = root.GetComponent<SpriteRenderer>();
        return rootRenderer != null ? new[] { rootRenderer } : Array.Empty<SpriteRenderer>();
    }

    private static void SetObjectArray(SerializedProperty property, IReadOnlyList<SpriteRenderer> values)
    {
        if (property == null || !property.isArray)
            return;

        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static int ResolveLayerMask(params string[] layerNames)
    {
        int mask = 0;
        for (int i = 0; i < layerNames.Length; i++)
        {
            int layer = LayerMask.NameToLayer(layerNames[i]);
            if (layer >= 0)
                mask |= 1 << layer;
        }

        return mask;
    }
}
