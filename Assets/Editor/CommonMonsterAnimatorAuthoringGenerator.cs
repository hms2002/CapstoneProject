using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 책임:
/// - 공통 복도 몬스터별 AnimatorController FSM과 파라미터를 몬스터 전용 이름 규칙으로 재구성한다.
/// - 생성된 몬스터 프리팹의 Visual Animator, Mob 참조, CommonMonsterAnimatorBridge trigger 이름을 함께 갱신한다.
/// </summary>
public static class CommonMonsterAnimatorAuthoringGenerator
{
    private const string AnimationFolder = "Assets/Sprites/Characters/Mob/CommonMonster";
    private const string PrefabFolder = "Assets/Prefabs/Enemies/Mobs/CommonCorridor";
    private const string MoveBool = "isMoving";

    [MenuItem("Tools/Authoring/Configure Common Monster Animators")]
    public static void Configure()
    {
        MonsterAnimatorConfig[] configs =
        {
            MonsterAnimatorConfig.Basic("GoblinWarrior", "PrepareAttack", "Attack", "RecoverAttack", "Die"),
            MonsterAnimatorConfig.Basic("GoblinGunner", "ShotReady", "Shot", "ShotRecovery", "Die"),
            MonsterAnimatorConfig.Basic("GoblinTank", "PrepareAttack", "Attack", "RecoveryAttack", "Die"),
            MonsterAnimatorConfig.Basic("LizardWarrior", "AttackPrepare", "Attack", null, "Die"),
            MonsterAnimatorConfig.LizardMage(),
            MonsterAnimatorConfig.Basic("ArcaneMeleeGolem", "PrepareDash", "Dash1", "Dash2", "Die"),
            MonsterAnimatorConfig.ArcaneTank("ArcaneTankGolem")
        };

        for (int i = 0; i < configs.Length; i++)
        {
            AnimatorController controller = ConfigureController(configs[i]);
            AssignPrefabController(configs[i], controller);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CommonMonsterAnimatorAuthoringGenerator] Common monster animators configured.");
    }

    private static AnimatorController ConfigureController(MonsterAnimatorConfig config)
    {
        string controllerPath = $"{AnimationFolder}/AC_{config.MonsterName}.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogError($"[CommonMonsterAnimatorAuthoringGenerator] Missing controller: {controllerPath}");
            return null;
        }

        ResetParameters(controller, config);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ClearStateMachine(stateMachine);

        AnimatorState idle = AddState(stateMachine, "Idle", LoadClip(config.MonsterName, "Idle"), new Vector3(250f, 60f, 0f));
        AnimatorState walk = AddState(stateMachine, "Walk", LoadClip(config.MonsterName, "Walk"), new Vector3(250f, 160f, 0f));
        stateMachine.defaultState = idle;

        AddBoolTransition(idle, walk, true);
        AddBoolTransition(walk, idle, false);

        AnimatorState attackReady = AddOptionalState(stateMachine, config.AttackReadyStateName, config.MonsterName, config.AttackReadyClipSuffix, new Vector3(520f, -80f, 0f));
        AnimatorState attack = AddOptionalState(stateMachine, config.AttackStateName, config.MonsterName, config.AttackClipSuffix, new Vector3(520f, 20f, 0f));
        AnimatorState recover = AddOptionalState(stateMachine, config.RecoverStateName, config.MonsterName, config.RecoverClipSuffix, new Vector3(520f, 120f, 0f));
        AnimatorState jump = AddOptionalState(stateMachine, "Jump", config.MonsterName, config.JumpClipSuffix, new Vector3(520f, 220f, 0f));
        AnimatorState land = AddOptionalState(stateMachine, "Land", config.MonsterName, config.LandClipSuffix, new Vector3(520f, 320f, 0f));
        AnimatorState die = AddOptionalState(stateMachine, "Die", config.MonsterName, config.DieClipSuffix, new Vector3(250f, 300f, 0f));

        AddTriggerTransition(stateMachine, attackReady, config.AttackReadyTrigger, config.HoldAttackReadyUntilNextTrigger ? null : idle);
        AddTriggerTransition(stateMachine, attack, config.AttackTrigger, config.HoldAttackUntilRecover ? null : idle);
        AddTriggerTransition(stateMachine, recover, config.RecoverTrigger, idle);
        AddTriggerTransition(stateMachine, jump, config.JumpTrigger, idle);
        if (!string.IsNullOrWhiteSpace(config.LandEndTrigger))
        {
            AddTriggerTransition(stateMachine, land, config.LandTrigger, null);
            AddStateTriggerTransition(land, idle, config.LandEndTrigger);
        }
        else
        {
            AddTriggerTransition(stateMachine, land, config.LandTrigger, idle);
        }
        AddTriggerTransition(stateMachine, die, config.DieTrigger, null);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void AssignPrefabController(MonsterAnimatorConfig config, AnimatorController controller)
    {
        if (controller == null)
            return;

        string prefabPath = $"{PrefabFolder}/{config.MonsterName}.prefab";
        GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform visual = prefabContents.transform.Find("Visual");
            if (visual == null)
            {
                Debug.LogError($"[CommonMonsterAnimatorAuthoringGenerator] {prefabPath}: Visual child not found.");
                return;
            }

            Animator animator = visual.GetComponent<Animator>();
            if (animator == null)
                animator = visual.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            CommonMonsterAnimatorBridge bridge = prefabContents.GetComponent<CommonMonsterAnimatorBridge>();
            if (bridge == null)
                bridge = prefabContents.AddComponent<CommonMonsterAnimatorBridge>();
            bridge.Configure(
                animator,
                config.AttackReadyTrigger,
                config.AttackTrigger,
                config.RecoverTrigger,
                config.DieTrigger,
                config.JumpTrigger,
                config.LandTrigger,
                config.LandEndTrigger);

            Mob mob = prefabContents.GetComponent<Mob>();
            if (mob != null)
            {
                SerializedObject serialized = new(mob);
                SetObject(serialized, "animator", animator);
                SetObject(serialized, "sprite", visual.GetComponent<SpriteRenderer>());
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }

    private static void ResetParameters(AnimatorController controller, MonsterAnimatorConfig config)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
            controller.RemoveParameter(parameter);

        controller.AddParameter(MoveBool, AnimatorControllerParameterType.Bool);
        AddTriggerIfNeeded(controller, config.AttackReadyTrigger);
        AddTriggerIfNeeded(controller, config.AttackTrigger);
        AddTriggerIfNeeded(controller, config.RecoverTrigger);
        AddTriggerIfNeeded(controller, config.DieTrigger);
        AddTriggerIfNeeded(controller, config.JumpTrigger);
        AddTriggerIfNeeded(controller, config.LandTrigger);
        AddTriggerIfNeeded(controller, config.LandEndTrigger);
    }

    private static void AddTriggerIfNeeded(AnimatorController controller, string triggerName)
    {
        if (!string.IsNullOrWhiteSpace(triggerName))
            controller.AddParameter(triggerName, AnimatorControllerParameterType.Trigger);
    }

    private static void ClearStateMachine(AnimatorStateMachine stateMachine)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            AnimatorState state = childState.state;
            foreach (AnimatorStateTransition transition in state.transitions.ToArray())
                state.RemoveTransition(transition);
        }

        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
            stateMachine.RemoveAnyStateTransition(transition);

        foreach (ChildAnimatorState childState in stateMachine.states.ToArray())
            stateMachine.RemoveState(childState.state);
    }

    private static AnimatorState AddState(AnimatorStateMachine stateMachine, string stateName, AnimationClip clip, Vector3 position)
    {
        AnimatorState state = stateMachine.AddState(stateName, position);
        state.motion = clip;
        state.writeDefaultValues = true;
        return state;
    }

    private static AnimatorState AddOptionalState(AnimatorStateMachine stateMachine, string stateName, string monsterName, string clipSuffix, Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(stateName) || string.IsNullOrWhiteSpace(clipSuffix))
            return null;

        AnimationClip clip = LoadClip(monsterName, clipSuffix);
        return clip != null ? AddState(stateMachine, stateName, clip, position) : null;
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, bool isMoving)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0f;
        transition.AddCondition(isMoving ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, MoveBool);
    }

    private static void AddTriggerTransition(AnimatorStateMachine stateMachine, AnimatorState target, string triggerName, AnimatorState returnState)
    {
        if (target == null || string.IsNullOrWhiteSpace(triggerName))
            return;

        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(target);
        transition.hasExitTime = false;
        transition.duration = 0f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);

        if (returnState == null)
            return;

        AnimatorStateTransition returnTransition = target.AddTransition(returnState);
        returnTransition.hasExitTime = true;
        returnTransition.exitTime = 0.95f;
        returnTransition.duration = 0f;
    }

    private static void AddStateTriggerTransition(AnimatorState from, AnimatorState to, string triggerName)
    {
        if (from == null || to == null || string.IsNullOrWhiteSpace(triggerName))
            return;

        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0f;
        transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
    }

    private static AnimationClip LoadClip(string monsterName, string suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
            return null;

        string path = $"{AnimationFolder}/AClip_{monsterName}_{suffix}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
            Debug.LogWarning($"[CommonMonsterAnimatorAuthoringGenerator] Missing animation clip: {path}");
        return clip;
    }

    private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private readonly struct MonsterAnimatorConfig
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

        public string AttackReadyTrigger => $"{MonsterName}_AttackReady";
        public string AttackTrigger => string.IsNullOrWhiteSpace(AttackClipSuffix) ? null : $"{MonsterName}_Attack";
        public string RecoverTrigger => string.IsNullOrWhiteSpace(RecoverClipSuffix) ? null : $"{MonsterName}_Recover";
        public string DieTrigger => $"{MonsterName}_Die";
        public string JumpTrigger => string.IsNullOrWhiteSpace(JumpClipSuffix) ? null : $"{MonsterName}_Jump";
        public string LandTrigger => string.IsNullOrWhiteSpace(LandClipSuffix) ? null : $"{MonsterName}_Land";
        public string LandEndTrigger => string.IsNullOrWhiteSpace(LandClipSuffix) ? null : $"{MonsterName}_LandEnd";

        private MonsterAnimatorConfig(
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

        public static MonsterAnimatorConfig Basic(
            string monsterName,
            string attackReadyClipSuffix,
            string attackClipSuffix,
            string recoverClipSuffix,
            string dieClipSuffix)
        {
            string readyState = monsterName == "ArcaneMeleeGolem" ? "PrepareDash" : "AttackReady";
            string attackState = monsterName == "ArcaneMeleeGolem" ? "Dash1" : "Attack";
            string recoverState = monsterName == "ArcaneMeleeGolem" ? "Dash2" : "Recover";
            return new MonsterAnimatorConfig(
                monsterName,
                attackReadyClipSuffix,
                attackClipSuffix,
                recoverClipSuffix,
                dieClipSuffix,
                null,
                null,
                readyState,
                attackState,
                recoverState);
        }

        public static MonsterAnimatorConfig LizardMage()
        {
            return new MonsterAnimatorConfig(
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

        public static MonsterAnimatorConfig ArcaneTank(string monsterName)
        {
            return new MonsterAnimatorConfig(
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
}
