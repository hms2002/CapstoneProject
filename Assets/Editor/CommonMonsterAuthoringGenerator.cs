using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 공통 복도 몬스터의 AD/AL 에셋, 구조 우선 테스트 프리팹, StageMonsterSetSO를 일괄 생성한다.
/// - 기존 일반 몬스터 프리팹을 구조 템플릿으로 사용해 필수 전투/이동/충돌 authoring 누락을 줄인다.
/// </summary>
public static class CommonMonsterAuthoringGenerator
{
    private const string AbilityFolder = "Assets/Script/Enemy/Mob/Abilities/CommonMonsters";
    private const string PrefabFolder = "Assets/Prefabs/Enemies/Mobs/CommonCorridor";
    private const string SpawnDataFolder = "Assets/HeoMinSeok/_Project/Data/MonsterSpawnPoolData/Common";

    private const string TemplatePrefabPath = "Assets/Prefabs/Enemies/Mobs/SlimeCorridor/Rook.prefab";
    private const string LightBeadPrefabPath = "Assets/Prefabs/Enemies/Mobs/ShadowCorridor/StrangeCandlestick/LightBead.prefab";
    private const string DamageEffectPath = "Assets/HeoMinSeok/_Project/Data/Abilities/Effects/GE_MobAttackDamage_Spec.asset";
    private const string KnockbackEffectPath = "Assets/HeoMinSeok/_Project/Data/Abilities/Effects/GE_Knockback_Spec.asset";
    private const string RunOnceMarkerPath = "Temp/CommonMonsterAuthoringGenerator.runonce";
    private static readonly string[] EnemyLayerNames = { "Enemy", "TEMP_Enemy_LAYER" };

    [InitializeOnLoadMethod]
    private static void GenerateWhenRunOnceMarkerExists()
    {
        if (!File.Exists(RunOnceMarkerPath))
            return;

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(RunOnceMarkerPath))
                return;

            File.Delete(RunOnceMarkerPath);
            Generate();
        };
    }

    [MenuItem("Tools/Authoring/Generate Common Monsters")]
    public static void Generate()
    {
        EnsureFolder(AbilityFolder);
        EnsureFolder(PrefabFolder);
        EnsureFolder(SpawnDataFolder);

        GE_Damage_Spec damageEffect = LoadRequired<GE_Damage_Spec>(DamageEffectPath);
        GE_Knockback_Spec knockbackEffect = LoadOptional<GE_Knockback_Spec>(KnockbackEffectPath);
        GameObject projectilePrefab = LoadRequired<GameObject>(LightBeadPrefabPath);
        GameObject templatePrefab = LoadRequired<GameObject>(TemplatePrefabPath);

        var abilities = CreateAbilityAssets();
        var prefabs = CreateMonsterPrefabs(templatePrefab, abilities, damageEffect, knockbackEffect, projectilePrefab);
        CreateStageMonsterSets(prefabs);
        CommonMonsterAnimatorAuthoringGenerator.Configure();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CommonMonsterAuthoringGenerator] Common monster authoring assets generated.");
    }

    private static CommonMonsterAbilityAssets CreateAbilityAssets()
    {
        return new CommonMonsterAbilityAssets
        {
            GoblinWarrior = CreateAbility<AbilityLogic_GoblinWarriorCharge>(
                "GoblinWarriorCharge",
                "AD_GoblinWarriorCharge",
                "고블린 전사 1회 돌진 공격"),
            GoblinGunner = CreateAbility<AbilityLogic_GoblinGunnerShot>(
                "GoblinGunnerShot",
                "AD_GoblinGunnerShot",
                "고블린 사수 단발 사격"),
            GoblinTank = CreateAbility<AbilityLogic_GoblinTankSlam>(
                "GoblinTankSlam",
                "AD_GoblinTankSlam",
                "고블린 탱커 내려치기"),
            LizardWarrior = CreateAbility<AbilityLogic_LizardWarriorCharge>(
                "LizardWarriorCharge",
                "AD_LizardWarriorCharge",
                "리자드맨 전사 2연타 돌진"),
            LizardMage = CreateAbility<AbilityLogic_LizardMageBurst>(
                "LizardMageBurst",
                "AD_LizardMageBurst",
                "리자드맨 마법사 3연속 사격"),
            ArcaneMeleeGolem = CreateAbility<AbilityLogic_ArcaneMeleeGolemCharge>(
                "ArcaneMeleeGolemCharge",
                "AD_ArcaneMeleeGolemCharge",
                "마도 근접 골렘 빠른 2연타 돌진"),
            ArcaneTankGolem = CreateAbility<AbilityLogic_ArcaneTankGolemSlam>(
                "ArcaneTankGolemSlam",
                "AD_ArcaneTankGolemSlam",
                "마도 탱커 골렘 점프 착지와 4방향 낙석")
        };
    }

    private static AbilityDefinition CreateAbility<TLogic>(string abilityName, string assetName, string description)
        where TLogic : AbilityLogic
    {
        string logicPath = $"{AbilityFolder}/AL_{abilityName}.asset";
        TLogic logic = AssetDatabase.LoadAssetAtPath<TLogic>(logicPath);
        if (logic == null)
        {
            logic = ScriptableObject.CreateInstance<TLogic>();
            AssetDatabase.CreateAsset(logic, logicPath);
        }

        string abilityPath = $"{AbilityFolder}/{assetName}.asset";
        AbilityDefinition ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(abilityPath);
        if (ability == null)
        {
            ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            AssetDatabase.CreateAsset(ability, abilityPath);
        }

        SerializedObject serialized = new SerializedObject(ability);
        SetString(serialized, "abilityName", abilityName);
        SetString(serialized, "description", description);
        SetFloat(serialized, "cooldown", 0.5f);
        SetFloat(serialized, "castTime", 0f);
        SetFloat(serialized, "recoveryTime", 0f);
        SetBool(serialized, "canCastWhileMoving", true);
        SetBool(serialized, "interruptible", true);
        SetBool(serialized, "requireTargetObject", false);
        SetObject(serialized, "logic", logic);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(logic);
        EditorUtility.SetDirty(ability);
        return ability;
    }

    private static CommonMonsterPrefabs CreateMonsterPrefabs(
        GameObject templatePrefab,
        CommonMonsterAbilityAssets abilities,
        GE_Damage_Spec damageEffect,
        GE_Knockback_Spec knockbackEffect,
        GameObject projectilePrefab)
    {
        GameObject goblinWarrior = CreateMonsterPrefab<GoblinWarrior, GoblinWarriorChargeRunner>(
            templatePrefab,
            "GoblinWarrior",
            abilities.GoblinWarrior,
            damageEffect,
            knockbackEffect,
            null,
            monster => SetFloat(new SerializedObject(monster), "maxHealth", 6f));

        GameObject goblinGunner = CreateMonsterPrefab<GoblinGunner, GoblinGunnerShotRunner>(
            templatePrefab,
            "GoblinGunner",
            abilities.GoblinGunner,
            damageEffect,
            null,
            projectilePrefab,
            monster => SetFloat(new SerializedObject(monster), "maxHealth", 5f));

        GameObject goblinTank = CreateMonsterPrefab<GoblinTank, GoblinTankSlamRunner>(
            templatePrefab,
            "GoblinTank",
            abilities.GoblinTank,
            damageEffect,
            knockbackEffect,
            null,
            monster => SetFloat(new SerializedObject(monster), "maxHealth", 18f));

        GameObject lizardWarrior = CreateMonsterPrefab<LizardWarrior, LizardWarriorChargeRunner>(
            templatePrefab,
            "LizardWarrior",
            abilities.LizardWarrior,
            damageEffect,
            knockbackEffect,
            null,
            monster => SetFloat(new SerializedObject(monster), "maxHealth", 8f));

        GameObject lizardMage = CreateMonsterPrefab<LizardMage, LizardMageBurstRunner>(
            templatePrefab,
            "LizardMage",
            abilities.LizardMage,
            damageEffect,
            null,
            projectilePrefab,
            monster => SetFloat(new SerializedObject(monster), "maxHealth", 7f));

        GameObject arcaneMeleeGolem = CreateMonsterPrefab<ArcaneMeleeGolem, ArcaneMeleeGolemChargeRunner>(
            templatePrefab,
            "ArcaneMeleeGolem",
            abilities.ArcaneMeleeGolem,
            damageEffect,
            knockbackEffect,
            null,
            monster => SetFloat(new SerializedObject(monster), "maxHealth", 14f));

        GameObject arcaneTankGolem = CreateMonsterPrefab<ArcaneTankGolem, ArcaneTankGolemSlamRunner>(
            templatePrefab,
            "ArcaneTankGolem",
            abilities.ArcaneTankGolem,
            damageEffect,
            knockbackEffect,
            null,
            monster => SetFloat(new SerializedObject(monster), "maxHealth", 28f));

        return new CommonMonsterPrefabs
        {
            GoblinWarrior = goblinWarrior,
            GoblinGunner = goblinGunner,
            GoblinTank = goblinTank,
            LizardWarrior = lizardWarrior,
            LizardMage = lizardMage,
            ArcaneMeleeGolem = arcaneMeleeGolem,
            ArcaneTankGolem = arcaneTankGolem
        };
    }

    private static GameObject CreateMonsterPrefab<TMonster, TRunner>(
        GameObject templatePrefab,
        string prefabName,
        AbilityDefinition ability,
        GE_Damage_Spec damageEffect,
        GE_Knockback_Spec knockbackEffect,
        GameObject projectilePrefab,
        Action<TMonster> configureMonster)
        where TMonster : Mob, IMobAttackDecisionSource
        where TRunner : MonoBehaviour, IMobPatternRunner
    {
        string prefabPath = $"{PrefabFolder}/{prefabName}.prefab";
        GameObject instance = CreateBaseMonsterInstance(templatePrefab, prefabName);

        TMonster monster = instance.GetComponent<TMonster>();
        if (monster == null)
            monster = instance.AddComponent<TMonster>();

        TRunner runner = instance.GetComponent<TRunner>();
        if (runner == null)
            runner = instance.AddComponent<TRunner>();

        ApplyCommonMonsterReferences(instance, monster, prefabName);
        ApplyMonsterSpecificReferences(monster, ability, damageEffect, knockbackEffect, projectilePrefab);
        ApplyRunnerReferences(instance, runner, monster);
        ConfigureArcaneTankHeightPresentation(instance, prefabName);
        ConfigureAbilitySystem(instance, ability);

        configureMonster?.Invoke(monster);
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        UnityEngine.Object.DestroyImmediate(instance);
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    private static GameObject CreateBaseMonsterInstance(GameObject templatePrefab, string prefabName)
    {
        GameObject instance = new GameObject(prefabName);
        instance.tag = "Enemy";
        instance.layer = ResolveLayer("Default", EnemyLayerNames);

        CreateVisualChild(instance);
        CapsuleCollider2D bodyCollider = CreateBodyCollisionChild(instance);
        CapsuleCollider2D hurtboxCollider = CreateHurtboxChild(instance);

        CopyTemplateComponent(templatePrefab, instance, instance.AddComponent<Rigidbody2D>());

        EntityCollisionProfile2D collisionProfile = instance.AddComponent<EntityCollisionProfile2D>();
        CopyTemplateComponent(templatePrefab, instance, collisionProfile);

        AttackTelegraphService telegraphService = instance.AddComponent<AttackTelegraphService>();
        CopyTemplateComponent(templatePrefab, instance, telegraphService);

        EnemyChaseIntent2D chaseIntent = instance.AddComponent<EnemyChaseIntent2D>();
        CopyTemplateComponent(templatePrefab, instance, chaseIntent);

        EnsureCoreEnemyComponents(instance);
        CopyTemplateCoreComponents(templatePrefab, instance);
        ConfigureCoreReferences(instance, bodyCollider, hurtboxCollider, chaseIntent, collisionProfile);
        return instance;
    }

    /// <summary>
    /// 책임:
    /// - 점프 패턴을 가진 ArcaneTankGolem 프리펩에 높이 표현 컴포넌트를 연결한다.
    /// - Visual만 떠오르고 root/collider와 fallback shadow는 바닥 좌표에 남도록 authoring한다.
    /// </summary>
    private static void ConfigureArcaneTankHeightPresentation(GameObject instance, string prefabName)
    {
        if (prefabName != "ArcaneTankGolem")
            return;

        CombatHeightPresentation2D presentation = EnsureComponent<CombatHeightPresentation2D>(instance);
        Transform visual = instance.transform.Find("Visual");

        SerializedObject serialized = new SerializedObject(presentation);
        SetObject(serialized, "heightState", instance.GetComponent<CombatHeightState2D>());
        SetObject(serialized, "visualRoot", visual);
        SetObject(serialized, "shadowRoot", null);
        SetObject(serialized, "shadowRenderer", null);
        SetBool(serialized, "createFallbackShadow", true);
        SetVector3(serialized, "fallbackShadowLocalScale", new Vector3(0.7f, 0.22f, 1f));
        SetColor(serialized, "fallbackShadowColor", new Color(0f, 0f, 0f, 0.32f));
        SetString(serialized, "fallbackShadowSortingLayerName", "Entity");
        SetInt(serialized, "fallbackShadowSortingOrder", -1);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateVisualChild(GameObject root)
    {
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sortingLayerName = "Entity";
        renderer.sortingOrder = 0;
    }

    private static CapsuleCollider2D CreateBodyCollisionChild(GameObject root)
    {
        GameObject body = new GameObject("BodyCollision");
        body.transform.SetParent(root.transform, false);
        body.layer = ResolveLayer("Default", EnemyLayerNames);
        CapsuleCollider2D collider = body.AddComponent<CapsuleCollider2D>();
        collider.isTrigger = false;
        collider.size = new Vector2(0.75f, 0.75f);
        collider.direction = CapsuleDirection2D.Vertical;
        return collider;
    }

    private static CapsuleCollider2D CreateHurtboxChild(GameObject root)
    {
        GameObject hurtbox = new GameObject("Hurtbox");
        hurtbox.transform.SetParent(root.transform, false);
        hurtbox.layer = ResolveLayer("Default", EnemyLayerNames);
        CapsuleCollider2D collider = hurtbox.AddComponent<CapsuleCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(0.85f, 0.85f);

        CombatHurtbox2D combatHurtbox = hurtbox.AddComponent<CombatHurtbox2D>();
        SerializedObject serialized = new SerializedObject(combatHurtbox);
        SetObject(serialized, "targetRoot", root);
        SetObjectArray(serialized, "ownedColliders", new UnityEngine.Object[] { collider });
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return collider;
    }

    private static void EnsureCoreEnemyComponents(GameObject instance)
    {
        EnsureComponent<AbilitySystem>(instance);
        EnsureComponent<AttributeSet>(instance);
        EnsureComponent<GameplayEffectRunner>(instance);
        EnsureComponent<TagSystem>(instance);
        EnsureComponent<MovementMotor2D>(instance);
        EnsureComponent<AttributeStatSource>(instance);
        EnsureComponent<AbilityMotionController2D>(instance);
        EnsureComponent<ExternalMovementController2D>(instance);
        EnsureComponent<KnockbackReceiver2D>(instance);
        EnsureComponent<MobAbilityCoordinator>(instance);
        EnsureComponent<MonsterDifficultyReceiver>(instance);
        EnsureComponent<GroggyOverheadEffectPresenter2D>(instance);
        EnsureComponent<CombatHeightState2D>(instance);
        EnsureComponent<CommonMonsterAnimatorBridge>(instance);
    }

    private static void CopyTemplateCoreComponents(GameObject templatePrefab, GameObject instance)
    {
        CopyTemplateComponent(templatePrefab, instance, instance.GetComponent<AbilitySystem>());
        CopyTemplateComponent(templatePrefab, instance, instance.GetComponent<AttributeSet>());
        CopyTemplateComponent(templatePrefab, instance, instance.GetComponent<GameplayEffectRunner>());
        CopyTemplateComponent(templatePrefab, instance, instance.GetComponent<TagSystem>());
        CopyTemplateComponent(templatePrefab, instance, instance.GetComponent<MovementMotor2D>());
        CopyTemplateComponent(templatePrefab, instance, instance.GetComponent<AttributeStatSource>());
        CopyTemplateComponent(templatePrefab, instance, instance.GetComponent<AbilityMotionController2D>());
        CopyTemplateComponent(templatePrefab, instance, instance.GetComponent<ExternalMovementController2D>());
        CopyTemplateComponent(templatePrefab, instance, instance.GetComponent<KnockbackReceiver2D>());
        CopyTemplateComponent(templatePrefab, instance, instance.GetComponent<MonsterDifficultyReceiver>());
        CopyTemplateComponent(templatePrefab, instance, instance.GetComponent<GroggyOverheadEffectPresenter2D>());
    }

    private static void ConfigureCoreReferences(
        GameObject instance,
        CapsuleCollider2D bodyCollider,
        CapsuleCollider2D hurtboxCollider,
        EnemyChaseIntent2D chaseIntent,
        EntityCollisionProfile2D collisionProfile)
    {
        Rigidbody2D body = instance.GetComponent<Rigidbody2D>();
        MovementMotor2D movementMotor = instance.GetComponent<MovementMotor2D>();
        AttributeStatSource statSource = instance.GetComponent<AttributeStatSource>();
        ExternalMovementController2D externalMovement = instance.GetComponent<ExternalMovementController2D>();
        AbilityMotionController2D motionController = instance.GetComponent<AbilityMotionController2D>();

        SerializedObject movement = new SerializedObject(movementMotor);
        SetObject(movement, "body", body);
        SetObject(movement, "intentSourceBehaviour", chaseIntent);
        SetObject(movement, "statProviderBehaviour", statSource);
        SetObject(movement, "externalMovement", externalMovement);
        SetObject(movement, "motionController", motionController);
        SetLayerMask(movement, "wallCollisionLayers", LayerMask.GetMask("Wall"));
        movement.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject profile = new SerializedObject(collisionProfile);
        SetObjectArray(profile, "bodyColliders", new UnityEngine.Object[] { bodyCollider });
        SetLayerMask(profile, "actorLayers", ResolveLayerMask("Player", EnemyLayerNames));
        profile.ApplyModifiedPropertiesWithoutUndo();

        AbilitySystem abilitySystem = instance.GetComponent<AbilitySystem>();
        SerializedObject ability = new SerializedObject(abilitySystem);
        SetObject(ability, "attributeSet", instance.GetComponent<AttributeSet>());
        SetObject(ability, "effectRunner", instance.GetComponent<GameplayEffectRunner>());
        SetObject(ability, "tagSystem", instance.GetComponent<TagSystem>());
        ability.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject stats = new SerializedObject(statSource);
        SetObject(stats, "attributeSet", instance.GetComponent<AttributeSet>());
        SetObject(stats, "abilitySystem", abilitySystem);
        stats.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject coordinator = new SerializedObject(instance.GetComponent<MobAbilityCoordinator>());
        SetObject(coordinator, "abilitySystem", abilitySystem);
        SetObject(coordinator, "tagSystem", instance.GetComponent<TagSystem>());
        coordinator.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject knockback = new SerializedObject(instance.GetComponent<KnockbackReceiver2D>());
        SetObject(knockback, "externalMovement", externalMovement);
        knockback.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject difficulty = new SerializedObject(instance.GetComponent<MonsterDifficultyReceiver>());
        SetObject(difficulty, "healthAttribute", LoadByGuid<AttributeDefinition>("3ff045849daafe84d97370c69cd17747"));
        SetObject(difficulty, "maxHealthAttribute", LoadByGuid<AttributeDefinition>("0e177e1d15e428745b5859fac08ce203"));
        difficulty.ApplyModifiedPropertiesWithoutUndo();

        GroggyOverheadEffectPresenter2D groggyPresenter = instance.GetComponent<GroggyOverheadEffectPresenter2D>();
        SerializedObject groggy = new SerializedObject(groggyPresenter);
        SetObject(groggy, "tagSystem", instance.GetComponent<TagSystem>());
        SetObject(groggy, "followAnchor", instance.transform);
        SetObject(groggy, "boundsSource", instance.GetComponentInChildren<SpriteRenderer>(true));
        groggy.ApplyModifiedPropertiesWithoutUndo();

        CommonMonsterAnimatorBridge animatorBridge = instance.GetComponent<CommonMonsterAnimatorBridge>();
        SerializedObject bridge = new SerializedObject(animatorBridge);
        SetObject(bridge, "animator", instance.GetComponentInChildren<Animator>(true));
        bridge.ApplyModifiedPropertiesWithoutUndo();

        // Hurtbox collider는 CombatHurtbox2D가 소유하고, body collider는 이동 충돌 전용으로 분리한다.
        _ = hurtboxCollider;
    }

    private static T EnsureComponent<T>(GameObject instance) where T : Component
    {
        T component = instance.GetComponent<T>();
        return component != null ? component : instance.AddComponent<T>();
    }

    private static int ResolveLayer(string fallbackLayerName, params string[][] candidateGroups)
    {
        for (int i = 0; i < candidateGroups.Length; i++)
        {
            string[] candidates = candidateGroups[i];
            for (int j = 0; j < candidates.Length; j++)
            {
                int layer = LayerMask.NameToLayer(candidates[j]);
                if (layer >= 0)
                    return layer;
            }
        }

        int fallback = LayerMask.NameToLayer(fallbackLayerName);
        return fallback >= 0 ? fallback : 0;
    }

    private static int ResolveLayerMask(params object[] layerNamesOrGroups)
    {
        int mask = 0;
        for (int i = 0; i < layerNamesOrGroups.Length; i++)
        {
            switch (layerNamesOrGroups[i])
            {
                case string layerName:
                    AddLayerToMask(layerName, ref mask);
                    break;
                case string[] layerNames:
                    for (int j = 0; j < layerNames.Length; j++)
                        AddLayerToMask(layerNames[j], ref mask);
                    break;
            }
        }

        return mask;
    }

    private static void AddLayerToMask(string layerName, ref int mask)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
            mask |= 1 << layer;
    }

    private static void CopyTemplateComponent<T>(GameObject templatePrefab, GameObject instance, T target)
        where T : Component
    {
        T source = templatePrefab.GetComponent<T>();
        if (source == null || target == null)
            return;

        EditorUtility.CopySerialized(source, target);
    }

    private static void ApplyCommonMonsterReferences(GameObject instance, Mob monster, string enemyName)
    {
        SerializedObject serialized = new SerializedObject(monster);
        SetObject(serialized, "sprite", instance.GetComponentInChildren<SpriteRenderer>(true));
        SetObject(serialized, "animator", instance.GetComponentInChildren<Animator>(true));
        SetObject(serialized, "maxHealthDef", LoadByGuid<AttributeDefinition>("0e177e1d15e428745b5859fac08ce203"));
        SetObject(serialized, "healthDef", LoadByGuid<AttributeDefinition>("3ff045849daafe84d97370c69cd17747"));
        SetString(serialized, "enemyName", enemyName);
        SetObject(serialized, "chaseIntent", instance.GetComponent<EnemyChaseIntent2D>());
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(monster);
    }

    private static void ApplyMonsterSpecificReferences(
        Component monster,
        AbilityDefinition ability,
        GE_Damage_Spec damageEffect,
        GE_Knockback_Spec knockbackEffect,
        GameObject projectilePrefab)
    {
        SerializedObject serialized = new SerializedObject(monster);
        SetFirstExistingObject(serialized, ability, "chargeAbility", "shotAbility", "slamAbility", "burstAbility");
        SetObject(serialized, "damageEffect", damageEffect);
        SetObject(serialized, "knockbackEffect", knockbackEffect);
        SetObject(serialized, "projectilePrefab", projectilePrefab);
        SetLayerMask(serialized, "targetLayers", LayerMask.GetMask("Player"));
        SetLayerMask(serialized, "wallLayers", LayerMask.GetMask("Wall"));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(monster);
    }

    private static void ApplyRunnerReferences(GameObject instance, Component runner, Component owner)
    {
        SerializedObject serialized = new SerializedObject(runner);
        SetObject(serialized, "owner", owner);
        SetObject(serialized, "abilityCoordinator", instance.GetComponent<MobAbilityCoordinator>());
        SetObject(serialized, "telegraphService", instance.GetComponent<AttackTelegraphService>());
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(runner);
    }

    private static void ConfigureAbilitySystem(GameObject instance, AbilityDefinition ability)
    {
        AbilitySystem abilitySystem = instance.GetComponent<AbilitySystem>();
        if (abilitySystem == null)
            return;

        SerializedObject serialized = new SerializedObject(abilitySystem);
        SetObjectArray(serialized, "initialAbilities", new UnityEngine.Object[] { ability });
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(abilitySystem);
    }

    private static void CreateStageMonsterSets(CommonMonsterPrefabs prefabs)
    {
        CreateStageMonsterSet("CommonMeleeStageMonsterSet", new[]
        {
            prefabs.GoblinWarrior,
            prefabs.LizardWarrior,
            prefabs.ArcaneMeleeGolem
        });

        CreateStageMonsterSet("CommonRangedStageMonsterSet", new[]
        {
            prefabs.GoblinGunner,
            prefabs.LizardMage,
            prefabs.LizardMage
        });

        CreateStageMonsterSet("CommonTankStageMonsterSet", new[]
        {
            prefabs.GoblinTank,
            prefabs.GoblinTank,
            prefabs.ArcaneTankGolem
        });
    }

    private static void CreateStageMonsterSet(string assetName, GameObject[] prefabs)
    {
        string path = $"{SpawnDataFolder}/{assetName}.asset";
        StageMonsterSetSO set = AssetDatabase.LoadAssetAtPath<StageMonsterSetSO>(path);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<StageMonsterSetSO>();
            AssetDatabase.CreateAsset(set, path);
        }

        SerializedObject serialized = new SerializedObject(set);
        SetObjectArray(serialized, "stagePrefabs", prefabs);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(set);
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static T LoadRequired<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new InvalidOperationException($"Required asset not found: {path}");
        return asset;
    }

    private static T LoadOptional<T>(string path) where T : UnityEngine.Object
    {
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static T LoadByGuid<T>(string guid) where T : UnityEngine.Object
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static void SetFirstExistingObject(SerializedObject serialized, UnityEngine.Object value, params string[] propertyNames)
    {
        for (int i = 0; i < propertyNames.Length; i++)
        {
            SerializedProperty property = serialized.FindProperty(propertyNames[i]);
            if (property == null)
                continue;

            property.objectReferenceValue = value;
            return;
        }
    }

    private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetObjectArray(SerializedObject serialized, string propertyName, IReadOnlyList<UnityEngine.Object> values)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || !property.isArray)
            return;

        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void SetString(SerializedObject serialized, string propertyName, string value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
    }

    private static void SetFloat(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            return;

        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(SerializedObject serialized, string propertyName, bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetInt(SerializedObject serialized, string propertyName, int value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
    }

    private static void SetVector3(SerializedObject serialized, string propertyName, Vector3 value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.vector3Value = value;
    }

    private static void SetColor(SerializedObject serialized, string propertyName, Color value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.colorValue = value;
    }

    private static void SetLayerMask(SerializedObject serialized, string propertyName, int value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
    }

    private sealed class CommonMonsterAbilityAssets
    {
        public AbilityDefinition GoblinWarrior;
        public AbilityDefinition GoblinGunner;
        public AbilityDefinition GoblinTank;
        public AbilityDefinition LizardWarrior;
        public AbilityDefinition LizardMage;
        public AbilityDefinition ArcaneMeleeGolem;
        public AbilityDefinition ArcaneTankGolem;
    }

    private sealed class CommonMonsterPrefabs
    {
        public GameObject GoblinWarrior;
        public GameObject GoblinGunner;
        public GameObject GoblinTank;
        public GameObject LizardWarrior;
        public GameObject LizardMage;
        public GameObject ArcaneMeleeGolem;
        public GameObject ArcaneTankGolem;
    }
}
