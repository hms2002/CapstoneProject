using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 공통 복도 몬스터 자동 생성 산출물이 일반 몬스터 제작 규격을 만족하는지 에디터에서 검증한다.
/// - 프리팹 필수 컴포넌트, 핵심 직렬화 참조, AD/AL 연결, StageMonsterSet 해석 가능 여부를 한 번에 점검한다.
/// </summary>
public static class CommonMonsterAuthoringValidator
{
    private const string PrefabFolder = "Assets/_Project/Prefabs/Monsters/CommonCorridor";
    private const string AbilityDefinitionFolder = "Assets/_Project/Data/Abilities/Definitions/Monsters/CommonCorridor";
    private const string SpawnDataFolder = "Assets/_Project/Data/Monsters/SpawnSets";
    private const string AnimationFolder = "Assets/_Project/Art/Sprites/Monsters/CommonMonster";
    private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/PF Player.prefab";
    private const string FlashMaterialPath = "Assets/_Project/Art/Materials/FlashMat.mat";
    private const string MoveBool = "isMoving";
    private static readonly string[] EnemyLayerNames = { "Enemy", "TEMP_Enemy_LAYER" };

    private static readonly string[] ExpectedPrefabNames =
    {
        "GoblinWarrior",
        "GoblinGunner",
        "GoblinTank",
        "LizardWarrior",
        "LizardMage",
        "ArcaneMeleeGolem",
        "ArcaneTankGolem"
    };

    private static readonly string[] ExpectedAbilityNames =
    {
        "GoblinWarriorCharge",
        "GoblinGunnerShot",
        "GoblinTankSlam",
        "LizardWarriorCharge",
        "LizardMageBurst",
        "ArcaneMeleeGolemCharge",
        "ArcaneTankGolemSlam"
    };

    private static readonly string[] ExpectedStageSetNames =
    {
        "CommonMeleeStageMonsterSet",
        "CommonRangedStageMonsterSet",
        "CommonTankStageMonsterSet"
    };

    private static readonly AnimatorExpectation[] ExpectedAnimators =
    {
        AnimatorExpectation.Basic("GoblinWarrior", "PrepareAttack", "Attack", "RecoverAttack", "Die"),
        AnimatorExpectation.Basic("GoblinGunner", "ShotReady", "Shot", "ShotRecovery", "Die"),
        AnimatorExpectation.Basic("GoblinTank", "PrepareAttack", "Attack", "RecoveryAttack", "Die"),
        AnimatorExpectation.Basic("LizardWarrior", "AttackPrepare", "Attack", null, "Die"),
        AnimatorExpectation.LizardMage(),
        AnimatorExpectation.Basic("ArcaneMeleeGolem", "PrepareDash", "Dash1", "Dash2", "Die"),
        AnimatorExpectation.ArcaneTank("ArcaneTankGolem")
    };

    [MenuItem("Tools/Authoring/Validate Common Monsters")]
    public static void ValidateFromMenu()
    {
        ValidateOrThrow();
        Debug.Log("[CommonMonsterAuthoringValidator] Common monster authoring validation passed.");
    }

    public static void ValidateOrThrow()
    {
        List<string> errors = new();
        ValidatePlayerCollisionProfile(errors);
        ValidatePrefabs(errors);
        ValidateAbilities(errors);
        ValidateStageMonsterSets(errors);

        if (errors.Count == 0)
        {
            Debug.Log("[CommonMonsterAuthoringValidator] Common monster authoring validation passed.");
            return;
        }

        string message = "[CommonMonsterAuthoringValidator] Validation failed:\n- " + string.Join("\n- ", errors);
        Debug.LogError(message);
        throw new InvalidOperationException(message);
    }

    private static void ValidatePrefabs(List<string> errors)
    {
        for (int i = 0; i < ExpectedPrefabNames.Length; i++)
        {
            string prefabName = ExpectedPrefabNames[i];
            string path = $"{PrefabFolder}/{prefabName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                errors.Add($"Missing common monster prefab: {path}");
                continue;
            }

            ValidatePrefab(path, prefab, errors);
        }
    }

    private static void ValidatePrefab(string path, GameObject prefab, List<string> errors)
    {
        int missingScripts = CountMissingScripts(prefab.transform);
        if (missingScripts > 0)
            errors.Add($"{path}: contains {missingScripts} missing script component(s).");

        RequireComponent<Mob>(path, prefab, errors);
        RequireComponent<IMobAttackDecisionSource>(path, prefab, errors);
        RequireComponent<IMobPatternRunner>(path, prefab, errors);
        RequireComponent<MobAbilityCoordinator>(path, prefab, errors);
        RequireComponent<AbilitySystem>(path, prefab, errors);
        RequireComponent<AttributeSet>(path, prefab, errors);
        RequireComponent<TagSystem>(path, prefab, errors);
        RequireComponent<MovementMotor2D>(path, prefab, errors);
        RequireComponent<EnemyChaseIntent2D>(path, prefab, errors);
        RequireComponent<EntityCollisionProfile2D>(path, prefab, errors);
        RequireComponent<ActorSoftCollision2D>(path, prefab, errors);
        RequireComponent<SpriteHitFlashController>(path, prefab, errors);
        RequireComponent<GroggyOverheadEffectPresenter2D>(path, prefab, errors);
        RequireComponent<CommonMonsterAnimatorBridge>(path, prefab, errors);
        RequireComponent<ElementGaugeSystem>(path, prefab, errors);
        RequireComponent<MonsterElementGaugeViewInstaller>(path, prefab, errors);

        ValidateVisual(path, prefab, errors);
        ValidateChildColliders(path, prefab, errors);
        ValidateBodyInteractionAndHitFlash(path, prefab, errors);
        ValidateAbilitySystem(path, prefab, errors);
        ValidateCoordinator(path, prefab, errors);
        ValidateGroggyPresenter(path, prefab, errors);
        ValidateElementGauge(path, prefab, errors);
        ValidateAnimator(path, prefab, errors);
        ValidateHeightPresentation(path, prefab, errors);
    }

    /// <summary>
    /// 책임:
    /// Player 프리팹이 Enemy가 아닌 같은 Player 진영만 통과하도록 authoring되었는지 검증한다.
    /// </summary>
    private static void ValidatePlayerCollisionProfile(List<string> errors)
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (player == null)
        {
            errors.Add($"Missing Player prefab: {PlayerPrefabPath}");
            return;
        }

        EntityCollisionProfile2D profile = player.GetComponent<EntityCollisionProfile2D>();
        if (profile == null)
        {
            errors.Add($"{PlayerPrefabPath}: EntityCollisionProfile2D is missing.");
            return;
        }

        SerializedObject serialized = new(profile);
        int actualMask = serialized.FindProperty("actorLayers")?.intValue ?? 0;
        int expectedMask = LayerMask.GetMask("Player");
        if (actualMask != expectedMask)
            errors.Add($"{PlayerPrefabPath}: actorLayers must contain only Player for Player/Enemy body collision.");
    }

    /// <summary>
    /// 책임:
    /// 공통 몬스터의 Enemy 간 소프트 분리, 역할별 무게, Player 실제 충돌, 흰색 0.08초 점멸 authoring을 검증한다.
    /// </summary>
    private static void ValidateBodyInteractionAndHitFlash(string path, GameObject prefab, List<string> errors)
    {
        int enemyMask = ResolveLayerMask(EnemyLayerNames);
        EntityCollisionProfile2D profile = prefab.GetComponent<EntityCollisionProfile2D>();
        if (profile != null)
        {
            SerializedObject profileSerialized = new(profile);
            int actualMask = profileSerialized.FindProperty("actorLayers")?.intValue ?? 0;
            if (actualMask != enemyMask)
                errors.Add($"{path}: EntityCollisionProfile2D.actorLayers must contain only Enemy layers.");
        }

        ActorSoftCollision2D softCollision = prefab.GetComponent<ActorSoftCollision2D>();
        if (softCollision != null)
        {
            SerializedObject softSerialized = new(softCollision);
            RequireObjectReference(path, softSerialized, "bodyCollider", errors);
            RequireObjectReference(path, softSerialized, "body", errors);
            RequireObjectReference(path, softSerialized, "externalMovement", errors);
            RequireObjectReference(path, softSerialized, "collisionProfile", errors);

            int actualActorMask = softSerialized.FindProperty("actorLayers")?.intValue ?? 0;
            if (actualActorMask != enemyMask)
                errors.Add($"{path}: ActorSoftCollision2D.actorLayers must contain only Enemy layers.");

            bool suspendForPassThrough = softSerialized.FindProperty("suspendWhileBodyPassesThroughActors")?.boolValue ?? true;
            if (suspendForPassThrough)
                errors.Add($"{path}: ActorSoftCollision2D must stay active while Enemy body colliders pass through each other.");

            ValidateBodyPreset(path, prefab, softSerialized, errors);
        }

        SpriteHitFlashController flash = prefab.GetComponent<SpriteHitFlashController>();
        if (flash == null)
            return;

        Material expectedMaterial = AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
        SerializedObject flashSerialized = new(flash);
        SerializedProperty targets = flashSerialized.FindProperty("targetRenderers");
        if (targets == null || !targets.isArray || targets.arraySize == 0)
        {
            errors.Add($"{path}: SpriteHitFlashController.targetRenderers is empty.");
        }
        else
        {
            for (int i = 0; i < targets.arraySize; i++)
            {
                SpriteRenderer renderer = targets.GetArrayElementAtIndex(i).objectReferenceValue as SpriteRenderer;
                if (renderer == null)
                    errors.Add($"{path}: SpriteHitFlashController.targetRenderers[{i}] is null.");
                else if (renderer.sharedMaterial != expectedMaterial)
                    errors.Add($"{path}: hit flash renderer '{renderer.name}' does not use FlashMat.");
            }
        }

        Color flashColor = flashSerialized.FindProperty("flashColor")?.colorValue ?? Color.clear;
        float duration = flashSerialized.FindProperty("flashDuration")?.floatValue ?? 0f;
        if (flashColor != Color.white)
            errors.Add($"{path}: hit flash color must be white.");
        if (!Mathf.Approximately(duration, 0.08f))
            errors.Add($"{path}: hit flash duration must be 0.08 seconds.");
    }

    /// <summary>
    /// 책임:
    /// 공통 몬스터 이름에 대응하는 역할별 Rigidbody 질량과 소프트 분리 저항 프리셋을 검증한다.
    /// </summary>
    private static void ValidateBodyPreset(
        string path,
        GameObject prefab,
        SerializedObject softSerialized,
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
        float resistance = softSerialized.FindProperty("pushResistance")?.floatValue ?? 0f;
        if (body == null || !Mathf.Approximately(body.mass, expectedMass))
            errors.Add($"{path}: Rigidbody2D mass must be {expectedMass:0.##} for its role.");
        if (!Mathf.Approximately(resistance, expectedResistance))
            errors.Add($"{path}: soft collision resistance must be {expectedResistance:0.##} for its role.");
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

    /// <summary>
    /// 책임:
    /// - 공통 몬스터가 속성 피해 누적 시스템과 월드 게이지 View authoring을 함께 갖췄는지 검증한다.
    /// - 생성기 재실행 후 catalog/view prefab 참조가 비는 회귀를 잡는다.
    /// </summary>
    private static void ValidateElementGauge(string path, GameObject prefab, List<string> errors)
    {
        ElementGaugeSystem gauge = prefab.GetComponent<ElementGaugeSystem>();
        if (gauge != null)
        {
            SerializedObject serialized = new(gauge);
            RequireObjectReference(path, serialized, "catalog", errors);
        }

        MonsterElementGaugeViewInstaller installer = prefab.GetComponent<MonsterElementGaugeViewInstaller>();
        if (installer == null)
            return;

        SerializedObject installerSerialized = new(installer);
        RequireObjectReference(path, installerSerialized, "viewPrefab", errors);
        RequireObjectReference(path, installerSerialized, "uiParentOverride", errors);
    }

    private static void ValidateVisual(string path, GameObject prefab, List<string> errors)
    {
        Transform visual = prefab.transform.Find("Visual");
        if (visual == null)
        {
            errors.Add($"{path}: missing Visual child.");
            return;
        }

        SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
        if (renderer == null)
            errors.Add($"{path}: Visual child has no SpriteRenderer.");

        Mob mob = prefab.GetComponent<Mob>();
        if (mob == null)
            return;

        SerializedObject serialized = new(mob);
        RequireObjectReference(path, serialized, "sprite", errors);
    }

    private static void ValidateChildColliders(string path, GameObject prefab, List<string> errors)
    {
        Transform bodyCollision = prefab.transform.Find("BodyCollision");
        if (bodyCollision == null)
        {
            errors.Add($"{path}: missing BodyCollision child.");
        }
        else
        {
            Collider2D collider = bodyCollision.GetComponent<Collider2D>();
            if (collider == null)
                errors.Add($"{path}: BodyCollision has no Collider2D.");
            else if (collider.isTrigger)
                errors.Add($"{path}: BodyCollision collider must be non-trigger.");
        }

        Transform hurtbox = prefab.transform.Find("Hurtbox");
        if (hurtbox == null)
        {
            errors.Add($"{path}: missing Hurtbox child.");
            return;
        }

        Collider2D hurtboxCollider = hurtbox.GetComponent<Collider2D>();
        if (hurtboxCollider == null)
            errors.Add($"{path}: Hurtbox has no Collider2D.");
        else if (!hurtboxCollider.isTrigger)
            errors.Add($"{path}: Hurtbox collider must be trigger.");

        CombatHurtbox2D combatHurtbox = hurtbox.GetComponent<CombatHurtbox2D>();
        if (combatHurtbox == null)
            errors.Add($"{path}: Hurtbox has no CombatHurtbox2D.");
    }

    private static void ValidateAbilitySystem(string path, GameObject prefab, List<string> errors)
    {
        AbilitySystem abilitySystem = prefab.GetComponent<AbilitySystem>();
        if (abilitySystem == null)
            return;

        SerializedObject serialized = new(abilitySystem);
        RequireObjectReference(path, serialized, "attributeSet", errors);
        RequireObjectReference(path, serialized, "effectRunner", errors);
        RequireObjectReference(path, serialized, "tagSystem", errors);
        RequireObjectReference(path, serialized, "damageProfile", errors);

        SerializedProperty initialAbilities = serialized.FindProperty("initialAbilities");
        if (initialAbilities == null || !initialAbilities.isArray || initialAbilities.arraySize <= 0)
        {
            errors.Add($"{path}: AbilitySystem.initialAbilities is empty.");
            return;
        }

        for (int i = 0; i < initialAbilities.arraySize; i++)
        {
            if (initialAbilities.GetArrayElementAtIndex(i).objectReferenceValue == null)
                errors.Add($"{path}: AbilitySystem.initialAbilities[{i}] is null.");
        }
    }

    private static void ValidateCoordinator(string path, GameObject prefab, List<string> errors)
    {
        MobAbilityCoordinator coordinator = prefab.GetComponent<MobAbilityCoordinator>();
        if (coordinator == null)
            return;

        SerializedObject serialized = new(coordinator);
        RequireObjectReference(path, serialized, "abilitySystem", errors);
        RequireObjectReference(path, serialized, "tagSystem", errors);
    }

    private static void ValidateGroggyPresenter(string path, GameObject prefab, List<string> errors)
    {
        GroggyOverheadEffectPresenter2D presenter = prefab.GetComponent<GroggyOverheadEffectPresenter2D>();
        if (presenter == null)
            return;

        SerializedObject serialized = new(presenter);
        RequireObjectReference(path, serialized, "tagSystem", errors);
        RequireObjectReference(path, serialized, "followAnchor", errors);
        RequireObjectReference(path, serialized, "boundsSource", errors);
        RequireObjectReference(path, serialized, "effectPrefab", errors);
    }

    private static void ValidateAnimator(string path, GameObject prefab, List<string> errors)
    {
        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            errors.Add($"{path}: missing child Animator.");
            return;
        }

        if (animator.runtimeAnimatorController == null)
        {
            errors.Add($"{path}: child Animator has no runtimeAnimatorController.");
        }
        else
        {
            ValidateAnimatorController(path, prefab.name, animator.runtimeAnimatorController, errors);
        }

        Mob mob = prefab.GetComponent<Mob>();
        if (mob != null)
        {
            SerializedObject mobSerialized = new(mob);
            RequireObjectReference(path, mobSerialized, "animator", errors);
        }

        CommonMonsterAnimatorBridge bridge = prefab.GetComponent<CommonMonsterAnimatorBridge>();
        if (bridge == null)
            return;

        SerializedObject serialized = new(bridge);
        RequireObjectReference(path, serialized, "animator", errors);
        ValidateBridgeTrigger(path, serialized, "attackReadyTrigger", ExpectedTrigger(prefab.name, "AttackReady"), errors);
        ValidateBridgeTrigger(path, serialized, "attackTrigger", ExpectedAttackTrigger(prefab.name), errors);
        ValidateBridgeTrigger(path, serialized, "recoverTrigger", ExpectedRecoverTrigger(prefab.name), errors);
        ValidateBridgeTrigger(path, serialized, "dieTrigger", ExpectedTrigger(prefab.name, "Die"), errors);
        ValidateBridgeTrigger(path, serialized, "jumpTrigger", ExpectedJumpTrigger(prefab.name), errors);
        ValidateBridgeTrigger(path, serialized, "landTrigger", ExpectedLandTrigger(prefab.name), errors);
        ValidateBridgeTrigger(path, serialized, "landEndTrigger", ExpectedLandEndTrigger(prefab.name), errors);
    }

    /// <summary>
    /// 책임:
    /// - 점프 패턴을 가진 공통 몬스터가 CombatHeightState2D 높이를 실제 visual/shadow 표현으로 연결했는지 검증한다.
    /// - 전용 그림자가 없을 때는 fallback shadow 생성 옵션이 켜져 있는지 확인한다.
    /// </summary>
    private static void ValidateHeightPresentation(string path, GameObject prefab, List<string> errors)
    {
        if (prefab.name != "ArcaneTankGolem")
            return;

        CombatHeightPresentation2D presentation = prefab.GetComponent<CombatHeightPresentation2D>();
        if (presentation == null)
        {
            errors.Add($"{path}: ArcaneTankGolem needs CombatHeightPresentation2D for jump shadow presentation.");
            return;
        }

        SerializedObject serialized = new(presentation);
        RequireObjectReference(path, serialized, "heightState", errors);
        RequireObjectReference(path, serialized, "visualRoot", errors);

        bool hasShadowRoot = HasObjectReference(serialized, "shadowRoot");
        bool hasShadowRenderer = HasObjectReference(serialized, "shadowRenderer");
        bool createFallbackShadow = serialized.FindProperty("createFallbackShadow")?.boolValue ?? false;
        if ((!hasShadowRoot || !hasShadowRenderer) && !createFallbackShadow)
            errors.Add($"{path}: CombatHeightPresentation2D needs shadow refs or createFallbackShadow enabled.");
    }

    private static void ValidateAnimatorController(string path, string prefabName, RuntimeAnimatorController runtimeController, List<string> errors)
    {
        AnimatorController controller = runtimeController as AnimatorController;
        if (controller == null)
        {
            errors.Add($"{path}: runtimeAnimatorController must be AnimatorController, but was {runtimeController.GetType().Name}.");
            return;
        }

        AnimatorExpectation expectation = FindAnimatorExpectation(prefabName);
        if (!expectation.IsValid)
        {
            errors.Add($"{path}: no animator expectation for prefab '{prefabName}'.");
            return;
        }

        string expectedControllerName = $"AC_{prefabName}";
        if (controller.name != expectedControllerName)
            errors.Add($"{path}: expected controller '{expectedControllerName}', but assigned '{controller.name}'.");

        ValidateAnimatorParameters(path, controller, expectation, errors);
        ValidateAnimatorStates(path, controller, expectation, errors);
        ValidateNoLegacyAnimatorParameters(path, controller, errors);
    }

    private static void ValidateAnimatorParameters(string path, AnimatorController controller, AnimatorExpectation expectation, List<string> errors)
    {
        RequireAnimatorParameter(path, controller, MoveBool, AnimatorControllerParameterType.Bool, errors);
        RequireAnimatorParameter(path, controller, expectation.AttackReadyTrigger, AnimatorControllerParameterType.Trigger, errors);
        RequireAnimatorParameter(path, controller, expectation.AttackTrigger, AnimatorControllerParameterType.Trigger, errors);
        RequireAnimatorParameter(path, controller, expectation.RecoverTrigger, AnimatorControllerParameterType.Trigger, errors);
        RequireAnimatorParameter(path, controller, expectation.DieTrigger, AnimatorControllerParameterType.Trigger, errors);
        RequireAnimatorParameter(path, controller, expectation.JumpTrigger, AnimatorControllerParameterType.Trigger, errors);
        RequireAnimatorParameter(path, controller, expectation.LandTrigger, AnimatorControllerParameterType.Trigger, errors);
        RequireAnimatorParameter(path, controller, expectation.LandEndTrigger, AnimatorControllerParameterType.Trigger, errors);
    }

    private static void ValidateAnimatorStates(string path, AnimatorController controller, AnimatorExpectation expectation, List<string> errors)
    {
        if (controller.layers == null || controller.layers.Length == 0)
        {
            errors.Add($"{path}: AnimatorController has no layer.");
            return;
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idle = RequireStateWithClip(path, stateMachine, "Idle", expectation.Clip("Idle"), errors);
        AnimatorState walk = RequireStateWithClip(path, stateMachine, "Walk", expectation.Clip("Walk"), errors);
        AnimatorState attackReady = RequireOptionalStateWithClip(path, stateMachine, expectation.AttackReadyStateName, expectation, expectation.AttackReadyClipSuffix, errors);
        AnimatorState attack = RequireOptionalStateWithClip(path, stateMachine, expectation.AttackStateName, expectation, expectation.AttackClipSuffix, errors);
        AnimatorState recover = RequireOptionalStateWithClip(path, stateMachine, expectation.RecoverStateName, expectation, expectation.RecoverClipSuffix, errors);
        AnimatorState jump = RequireOptionalStateWithClip(path, stateMachine, "Jump", expectation, expectation.JumpClipSuffix, errors);
        AnimatorState land = RequireOptionalStateWithClip(path, stateMachine, "Land", expectation, expectation.LandClipSuffix, errors);
        AnimatorState die = RequireOptionalStateWithClip(path, stateMachine, "Die", expectation, expectation.DieClipSuffix, errors);

        RequireBoolTransition(path, idle, walk, MoveBool, AnimatorConditionMode.If, errors);
        RequireBoolTransition(path, walk, idle, MoveBool, AnimatorConditionMode.IfNot, errors);
        RequireAnyStateTriggerTransition(path, stateMachine, attackReady, expectation.AttackReadyTrigger, errors);
        RequireAnyStateTriggerTransition(path, stateMachine, attack, expectation.AttackTrigger, errors);
        RequireAnyStateTriggerTransition(path, stateMachine, recover, expectation.RecoverTrigger, errors);
        RequireAnyStateTriggerTransition(path, stateMachine, jump, expectation.JumpTrigger, errors);
        RequireAnyStateTriggerTransition(path, stateMachine, land, expectation.LandTrigger, errors);
        RequireAnyStateTriggerTransition(path, stateMachine, die, expectation.DieTrigger, errors);

        if (expectation.HoldAttackReadyUntilNextTrigger)
            RequireNoReturnToIdle(path, attackReady, idle, errors);
        else
            RequireReturnToIdle(path, attackReady, idle, errors);

        if (expectation.HoldAttackUntilRecover)
            RequireNoReturnToIdle(path, attack, idle, errors);
        else
            RequireReturnToIdle(path, attack, idle, errors);

        RequireReturnToIdle(path, recover, idle, errors);
        RequireReturnToIdle(path, jump, idle, errors);
        if (!RequireStateTriggerReturnToIdle(path, land, idle, expectation.LandEndTrigger, errors))
            RequireReturnToIdle(path, land, idle, errors);

        if (die != null && die.transitions.Any(transition => transition.destinationState == idle))
            errors.Add($"{path}: Die state must not transition back to Idle.");
    }

    private static void ValidateNoLegacyAnimatorParameters(string path, AnimatorController controller, List<string> errors)
    {
        string[] legacyTriggers = { "attackReady", "attack", "recover", "die", "jump", "land" };
        for (int i = 0; i < legacyTriggers.Length; i++)
        {
            if (controller.parameters.Any(parameter => parameter.name == legacyTriggers[i]))
                errors.Add($"{path}: AnimatorController still contains legacy trigger '{legacyTriggers[i]}'.");
        }
    }

    private static void ValidateBridgeTrigger(string path, SerializedObject serialized, string propertyName, string expectedValue, List<string> errors)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            errors.Add($"{path}: missing serialized property '{propertyName}'.");
            return;
        }

        string actualValue = property.stringValue ?? string.Empty;
        expectedValue ??= string.Empty;
        if (actualValue != expectedValue)
            errors.Add($"{path}: CommonMonsterAnimatorBridge.{propertyName} expected '{expectedValue}', but was '{actualValue}'.");
    }

    private static void ValidateAbilities(List<string> errors)
    {
        for (int i = 0; i < ExpectedAbilityNames.Length; i++)
        {
            string abilityName = ExpectedAbilityNames[i];
            string abilityPath = $"{AbilityDefinitionFolder}/AD_{abilityName}.asset";
            AbilityDefinition ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(abilityPath);
            if (ability == null)
            {
                errors.Add($"Missing common monster AD: {abilityPath}");
                continue;
            }

            SerializedObject serialized = new(ability);
            RequireObjectReference(abilityPath, serialized, "logic", errors);
        }
    }

    private static void ValidateStageMonsterSets(List<string> errors)
    {
        for (int i = 0; i < ExpectedStageSetNames.Length; i++)
        {
            string setName = ExpectedStageSetNames[i];
            string path = $"{SpawnDataFolder}/{setName}.asset";
            StageMonsterSetSO stageSet = AssetDatabase.LoadAssetAtPath<StageMonsterSetSO>(path);
            if (stageSet == null)
            {
                errors.Add($"Missing StageMonsterSetSO: {path}");
                continue;
            }

            for (int stageIndex = 0; stageIndex <= 3; stageIndex++)
            {
                if (!stageSet.TryResolveMonsterPrefab(stageIndex, out GameObject resolved) || resolved == null)
                    errors.Add($"{path}: cannot resolve prefab for stage index {stageIndex}.");
            }
        }
    }

    private static void RequireComponent<T>(string path, GameObject prefab, List<string> errors)
    {
        bool exists = typeof(T).IsInterface
            ? prefab.GetComponents<MonoBehaviour>().Any(component => component is T)
            : prefab.GetComponent(typeof(T)) != null;

        if (!exists)
            errors.Add($"{path}: missing required component/interface {typeof(T).Name}.");
    }

    private static void RequireObjectReference(string context, SerializedObject serialized, string propertyName, List<string> errors)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            errors.Add($"{context}: missing serialized property '{propertyName}'.");
            return;
        }

        if (property.objectReferenceValue == null)
            errors.Add($"{context}: '{propertyName}' is not assigned.");
    }

    private static bool HasObjectReference(SerializedObject serialized, string propertyName)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        return property != null && property.objectReferenceValue != null;
    }

    private static void RequireAnimatorParameter(
        string path,
        AnimatorController controller,
        string parameterName,
        AnimatorControllerParameterType parameterType,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            return;

        bool exists = controller.parameters.Any(parameter => parameter.name == parameterName && parameter.type == parameterType);
        if (!exists)
            errors.Add($"{path}: AnimatorController missing {parameterType} parameter '{parameterName}'.");
    }

    private static AnimatorState RequireStateWithClip(
        string path,
        AnimatorStateMachine stateMachine,
        string stateName,
        AnimationClip expectedClip,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(stateName) && expectedClip == null)
            return null;

        if (string.IsNullOrWhiteSpace(stateName))
        {
            errors.Add($"{path}: animator state name is empty for expected clip '{expectedClip.name}'.");
            return null;
        }

        AnimatorState state = FindState(stateMachine, stateName);
        if (state == null)
        {
            errors.Add($"{path}: AnimatorController missing state '{stateName}'.");
            return null;
        }

        if (expectedClip == null)
        {
            errors.Add($"{path}: expected clip for state '{stateName}' could not be loaded.");
            return state;
        }

        if (state.motion != expectedClip)
            errors.Add($"{path}: state '{stateName}' expected clip '{expectedClip.name}', but assigned '{(state.motion != null ? state.motion.name : "null")}'.");

        return state;
    }

    private static AnimatorState RequireOptionalStateWithClip(
        string path,
        AnimatorStateMachine stateMachine,
        string stateName,
        AnimatorExpectation expectation,
        string clipSuffix,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(clipSuffix))
            return null;

        return RequireStateWithClip(path, stateMachine, stateName, expectation.Clip(clipSuffix), errors);
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
    {
        if (stateMachine == null || string.IsNullOrWhiteSpace(stateName))
            return null;

        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state != null && states[i].state.name == stateName)
                return states[i].state;
        }

        return null;
    }

    private static void RequireBoolTransition(
        string path,
        AnimatorState from,
        AnimatorState to,
        string parameterName,
        AnimatorConditionMode conditionMode,
        List<string> errors)
    {
        if (from == null || to == null)
            return;

        bool exists = from.transitions.Any(transition =>
            transition.destinationState == to &&
            transition.conditions.Any(condition =>
                condition.parameter == parameterName &&
                condition.mode == conditionMode));

        if (!exists)
            errors.Add($"{path}: missing transition {from.name} -> {to.name} with {conditionMode} '{parameterName}'.");
    }

    private static void RequireAnyStateTriggerTransition(
        string path,
        AnimatorStateMachine stateMachine,
        AnimatorState target,
        string triggerName,
        List<string> errors)
    {
        if (target == null && string.IsNullOrWhiteSpace(triggerName))
            return;

        if (target == null || string.IsNullOrWhiteSpace(triggerName))
        {
            errors.Add($"{path}: animator trigger/state pair is incomplete. state='{(target != null ? target.name : "null")}', trigger='{triggerName}'.");
            return;
        }

        bool exists = stateMachine.anyStateTransitions.Any(transition =>
            transition.destinationState == target &&
            transition.conditions.Any(condition =>
                condition.parameter == triggerName &&
                condition.mode == AnimatorConditionMode.If));

        if (!exists)
            errors.Add($"{path}: missing Any State -> {target.name} transition with trigger '{triggerName}'.");
    }

    private static void RequireReturnToIdle(string path, AnimatorState from, AnimatorState idle, List<string> errors)
    {
        if (from == null || idle == null)
            return;

        bool exists = from.transitions.Any(transition => transition.destinationState == idle && transition.hasExitTime);
        if (!exists)
            errors.Add($"{path}: state '{from.name}' must return to Idle with exit time.");
    }

    private static void RequireNoReturnToIdle(string path, AnimatorState from, AnimatorState idle, List<string> errors)
    {
        if (from == null || idle == null)
            return;

        bool exists = from.transitions.Any(transition => transition.destinationState == idle);
        if (exists)
            errors.Add($"{path}: state '{from.name}' must stay active until the next trigger, but has an Idle transition.");
    }

    private static bool RequireStateTriggerReturnToIdle(
        string path,
        AnimatorState from,
        AnimatorState idle,
        string triggerName,
        List<string> errors)
    {
        if (from == null || idle == null || string.IsNullOrWhiteSpace(triggerName))
            return false;

        bool exists = from.transitions.Any(transition =>
            transition.destinationState == idle &&
            transition.conditions.Any(condition =>
                condition.parameter == triggerName &&
                condition.mode == AnimatorConditionMode.If));

        if (!exists)
            errors.Add($"{path}: state '{from.name}' must return to Idle with trigger '{triggerName}'.");

        return true;
    }


    private static AnimatorExpectation FindAnimatorExpectation(string prefabName)
    {
        for (int i = 0; i < ExpectedAnimators.Length; i++)
        {
            if (ExpectedAnimators[i].MonsterName == prefabName)
                return ExpectedAnimators[i];
        }

        return default;
    }

    private static string ExpectedTrigger(string prefabName, string cueName)
    {
        return $"{prefabName}_{cueName}";
    }

    private static string ExpectedRecoverTrigger(string prefabName)
    {
        AnimatorExpectation expectation = FindAnimatorExpectation(prefabName);
        return expectation.IsValid ? expectation.RecoverTrigger : string.Empty;
    }

    private static string ExpectedAttackTrigger(string prefabName)
    {
        AnimatorExpectation expectation = FindAnimatorExpectation(prefabName);
        return expectation.IsValid ? expectation.AttackTrigger : string.Empty;
    }

    private static string ExpectedJumpTrigger(string prefabName)
    {
        AnimatorExpectation expectation = FindAnimatorExpectation(prefabName);
        return expectation.IsValid ? expectation.JumpTrigger : string.Empty;
    }

    private static string ExpectedLandTrigger(string prefabName)
    {
        AnimatorExpectation expectation = FindAnimatorExpectation(prefabName);
        return expectation.IsValid ? expectation.LandTrigger : string.Empty;
    }

    private static string ExpectedLandEndTrigger(string prefabName)
    {
        AnimatorExpectation expectation = FindAnimatorExpectation(prefabName);
        return expectation.IsValid ? expectation.LandEndTrigger : string.Empty;
    }

    /// <summary>
    /// 책임:
    /// - 공통 몬스터별 AnimatorController가 가져야 할 상태, clip suffix, trigger 이름 규칙을 validator에 제공한다.
    /// </summary>
    private readonly struct AnimatorExpectation
    {
        public readonly string MonsterName;
        public readonly string AttackReadyClipSuffix;
        public readonly string AttackClipSuffix;
        public readonly string RecoverClipSuffix;
        public readonly string DieClipSuffix;
        public readonly string JumpClipSuffix;
        public readonly string LandClipSuffix;
        public readonly string AttackReadyStateName;
        public readonly string AttackStateName;
        public readonly string RecoverStateName;
        public readonly bool HoldAttackReadyUntilNextTrigger;
        public readonly bool HoldAttackUntilRecover;

        public bool IsValid => !string.IsNullOrWhiteSpace(MonsterName);
        public string AttackReadyTrigger => $"{MonsterName}_AttackReady";
        public string AttackTrigger => string.IsNullOrWhiteSpace(AttackClipSuffix) ? string.Empty : $"{MonsterName}_Attack";
        public string RecoverTrigger => string.IsNullOrWhiteSpace(RecoverClipSuffix) ? string.Empty : $"{MonsterName}_Recover";
        public string DieTrigger => $"{MonsterName}_Die";
        public string JumpTrigger => string.IsNullOrWhiteSpace(JumpClipSuffix) ? string.Empty : $"{MonsterName}_Jump";
        public string LandTrigger => string.IsNullOrWhiteSpace(LandClipSuffix) ? string.Empty : $"{MonsterName}_Land";
        public string LandEndTrigger => string.IsNullOrWhiteSpace(LandClipSuffix) ? string.Empty : $"{MonsterName}_LandEnd";

        private AnimatorExpectation(
            string monsterName,
            string attackReadyClipSuffix,
            string attackClipSuffix,
            string recoverClipSuffix,
            string dieClipSuffix,
            string jumpClipSuffix,
            string landClipSuffix,
            string attackReadyStateName,
            string attackStateName,
            string recoverStateName,
            bool holdAttackReadyUntilNextTrigger = false,
            bool holdAttackUntilRecover = false)
        {
            MonsterName = monsterName;
            AttackReadyClipSuffix = attackReadyClipSuffix;
            AttackClipSuffix = attackClipSuffix;
            RecoverClipSuffix = recoverClipSuffix;
            DieClipSuffix = dieClipSuffix;
            JumpClipSuffix = jumpClipSuffix;
            LandClipSuffix = landClipSuffix;
            AttackReadyStateName = attackReadyStateName;
            AttackStateName = attackStateName;
            RecoverStateName = recoverStateName;
            HoldAttackReadyUntilNextTrigger = holdAttackReadyUntilNextTrigger;
            HoldAttackUntilRecover = holdAttackUntilRecover;
        }

        public AnimationClip Clip(string suffix)
        {
            if (string.IsNullOrWhiteSpace(suffix))
                return null;

            string path = $"{AnimationFolder}/AClip_{MonsterName}_{suffix}.anim";
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }

        public static AnimatorExpectation Basic(
            string monsterName,
            string attackReadyClipSuffix,
            string attackClipSuffix,
            string recoverClipSuffix,
            string dieClipSuffix)
        {
            string readyState = monsterName == "ArcaneMeleeGolem" ? "PrepareDash" : "AttackReady";
            string attackState = monsterName == "ArcaneMeleeGolem" ? "Dash1" : "Attack";
            string recoverState = monsterName == "ArcaneMeleeGolem" ? "Dash2" : "Recover";
            return new AnimatorExpectation(
                monsterName,
                attackReadyClipSuffix,
                attackClipSuffix,
                recoverClipSuffix,
                dieClipSuffix,
                null,
                null,
                readyState,
                attackState,
                recoverState,
                holdAttackReadyUntilNextTrigger: monsterName == "GoblinTank");
        }

        public static AnimatorExpectation LizardMage()
        {
            return new AnimatorExpectation(
                "LizardMage",
                "PrepareAttack",
                "Attack",
                "RecoverAttack",
                "Die",
                null,
                null,
                "AttackReady",
                "Attack",
                "Recover",
                true,
                true);
        }

        public static AnimatorExpectation ArcaneTank(string monsterName)
        {
            return new AnimatorExpectation(
                monsterName,
                "ReadyJump",
                null,
                null,
                "Die",
                "Jump",
                "Land",
                "ReadyJump",
                null,
                null);
        }
    }

    private static int CountMissingScripts(Transform root)
    {
        if (root == null)
            return 0;

        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root.gameObject);
        for (int i = 0; i < root.childCount; i++)
            count += CountMissingScripts(root.GetChild(i));

        return count;
    }
}



