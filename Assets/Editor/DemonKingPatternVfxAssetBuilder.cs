using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

using Object = UnityEngine.Object;

internal static class DemonKingPatternVfxAssetBuilder
{
    private const string OutputFolder = "Assets/Resources/DemonKing/Vfx";
    private const string SpriteFolder = "Assets/Sprites/Characters/Boss/DarkLord";
    private const float FrameRate = 12f;
    private const string ProjectileSortingLayer = "Projectile";
    private const int ProjectileSortingOrder = 1;

    private static readonly OneShotSpec[] OneShotSpecs =
    {
        new(
            "DemonKingExplosion",
            $"{SpriteFolder}/DemonKingExplosion.png",
            $"{OutputFolder}/DemonKingExplosion_Play.anim",
            $"{OutputFolder}/DemonKingExplosionVfx.controller",
            $"{OutputFolder}/DemonKingExplosionVfx.prefab"),
        new(
            "DemonKingImpact",
            $"{SpriteFolder}/DemonKingImpact.png",
            $"{OutputFolder}/DemonKingImpact_Play.anim",
            $"{OutputFolder}/DemonKingImpactVfx.controller",
            $"{OutputFolder}/DemonKingImpactVfx.prefab"),
        new(
            "DemonKingStab",
            $"{SpriteFolder}/DemonKingStab.png",
            $"{OutputFolder}/DemonKingStab_Play.anim",
            $"{OutputFolder}/DemonKingStabVfx.controller",
            $"{OutputFolder}/DemonKingStabVfx.prefab"),
        new(
            "DarkLordSlash",
            $"{SpriteFolder}/DarkLordSlash.png",
            $"{OutputFolder}/DarkLordSlash_Play.anim",
            $"{OutputFolder}/DarkLordSlashVfx.controller",
            $"{OutputFolder}/DarkLordSlashVfx.prefab"),
        new(
            "DarkLordGroggyReleaseEffect",
            $"{SpriteFolder}/DarkLordGroggyReleaseEffect.png",
            $"{OutputFolder}/DarkLordGroggyReleaseEffect_Play.anim",
            $"{OutputFolder}/DarkLordGroggyReleaseVfx.controller",
            $"{OutputFolder}/DarkLordGroggyReleaseVfx.prefab"),
        new(
            "DemonKingEyeLight",
            $"{SpriteFolder}/DemonKingEyeLight.png",
            $"{OutputFolder}/DemonKingEyeLight_Play.anim",
            $"{OutputFolder}/DemonKingEyeLightVfx.controller",
            $"{OutputFolder}/DemonKingEyeLightVfx.prefab"),
        new(
            "EgoSwordAttack",
            $"{SpriteFolder}/EgoSwordAttack.png",
            $"{OutputFolder}/EgoSwordAttack_Play.anim",
            $"{OutputFolder}/EgoSwordAttackVfx.controller",
            $"{OutputFolder}/EgoSwordAttackVfx.prefab"),
        new(
            "DarkLordExplosion2",
            $"{SpriteFolder}/DarkLordExplosion2.png",
            $"{OutputFolder}/DarkLordExplosion2_Play.anim",
            $"{OutputFolder}/DarkLordExplosion2Vfx.controller",
            $"{OutputFolder}/DarkLordExplosion2Vfx.prefab"),
    };

    private static readonly LoopSpec[] LoopSpecs =
    {
        new(
            "DarkLordFragment",
            $"{SpriteFolder}/DarkLordFragment.png",
            $"{OutputFolder}/DarkLordFragment_Idle.anim",
            $"{OutputFolder}/DarkLordFragmentVfx.controller",
            $"{OutputFolder}/DarkLordFragmentVfx.prefab",
            "Idle"),
        new(
            "SwordSpin4Frame",
            $"{SpriteFolder}/SwordSpin4Frame.png",
            $"{OutputFolder}/SwordSpin4Frame_Loop.anim",
            $"{OutputFolder}/SwordSpin4FrameVfx.controller",
            $"{OutputFolder}/SwordSpin4FrameVfx.prefab",
            "Loop"),
    };

    private static readonly DualStateSpec[] DualStateSpecs =
    {
        new(
            "DemonChargeEffect",
            $"{SpriteFolder}/DemonChargeEffect.png",
            $"{OutputFolder}/DemonChargeEffect_Loop.anim",
            $"{SpriteFolder}/DemonChargeEffectDisapear.png",
            $"{OutputFolder}/DemonChargeEffectDisapear_Play.anim",
            $"{OutputFolder}/DemonChargeEffectVfx.controller",
            $"{OutputFolder}/DemonChargeEffectVfx.prefab",
            "Loop",
            "Disappear"),
    };

    private static readonly AuraSpec EgoSwordAuraSpec = new(
        "EgoSwordAttackAura",
        $"{SpriteFolder}/EgoSwordAttackAura.png",
        $"{OutputFolder}/EgoSwordAttackAura_Start.anim",
        $"{OutputFolder}/EgoSwordAttackAura_Idle.anim",
        $"{OutputFolder}/EgoSwordAttackAura_End.anim",
        $"{OutputFolder}/EgoSwordAttackAuraVfx.controller");

    private static bool autoRepairQueued;

    [InitializeOnLoadMethod]
    private static void QueueAutoRepair()
    {
        if (autoRepairQueued)
            return;

        autoRepairQueued = true;
        EditorApplication.delayCall += AutoRepairIfNeeded;
    }

    [MenuItem("Tools/DemonKing/Rebuild Pattern VFX Assets")]
    private static void RebuildFromMenu()
    {
        RebuildAll();
    }

    private static void AutoRepairIfNeeded()
    {
        autoRepairQueued = false;
        if (Application.isBatchMode)
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            QueueAutoRepair();
            return;
        }

        if (!NeedsRebuild())
            return;

        RebuildAll();
    }

    private static bool NeedsRebuild()
    {
        foreach (OneShotSpec spec in OneShotSpecs)
        {
            if (!IsClipValid(spec.ClipPath, loop: false)
                || !IsControllerValid(spec.ControllerPath, new[] { "Play" })
                || !IsPrefabValid(spec.PrefabPath))
            {
                return true;
            }
        }

        foreach (LoopSpec spec in LoopSpecs)
        {
            if (!IsClipValid(spec.ClipPath, loop: true)
                || !IsControllerValid(spec.ControllerPath, new[] { spec.StateName })
                || !IsPrefabValid(spec.PrefabPath))
            {
                return true;
            }
        }

        foreach (DualStateSpec spec in DualStateSpecs)
        {
            if (!IsClipValid(spec.LoopClipPath, loop: true)
                || !IsClipValid(spec.EndClipPath, loop: false)
                || !IsControllerValid(spec.ControllerPath, new[] { spec.LoopStateName, spec.EndStateName })
                || !IsPrefabValid(spec.PrefabPath))
            {
                return true;
            }
        }

        return !IsClipValid(EgoSwordAuraSpec.StartClipPath, loop: false)
            || !IsClipValid(EgoSwordAuraSpec.IdleClipPath, loop: true)
            || !IsClipValid(EgoSwordAuraSpec.EndClipPath, loop: false)
            || !IsControllerValid(EgoSwordAuraSpec.ControllerPath, new[] { "Start", "Idle", "End" });
    }

    private static void RebuildAll()
    {
        EnsureOutputFolder();

        foreach (OneShotSpec spec in OneShotSpecs)
            RebuildOneShot(spec);

        foreach (LoopSpec spec in LoopSpecs)
            RebuildLoop(spec);

        foreach (DualStateSpec spec in DualStateSpecs)
            RebuildDualState(spec);

        RebuildEgoSwordAura();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("DemonKing pattern VFX assets were rebuilt from DarkLord sprite sheets.");
    }

    private static void RebuildOneShot(OneShotSpec spec)
    {
        Sprite[] frames = LoadSprites(spec.SpritePath);
        AnimationClip playClip = CreateOrUpdateClip(spec.ClipPath, $"{spec.BaseName}_Play", frames, loop: false);
        AnimatorController controller = CreateOrUpdateController(
            spec.ControllerPath,
            Path.GetFileNameWithoutExtension(spec.ControllerPath),
            new[] { new StateClip("Play", playClip) });
        CreateOrUpdatePrefab(spec.PrefabPath, Path.GetFileNameWithoutExtension(spec.PrefabPath), frames[0], controller);
    }

    private static void RebuildLoop(LoopSpec spec)
    {
        Sprite[] frames = LoadSprites(spec.SpritePath);
        AnimationClip loopClip = CreateOrUpdateClip(spec.ClipPath, $"{spec.BaseName}_{spec.StateName}", frames, loop: true);
        AnimatorController controller = CreateOrUpdateController(
            spec.ControllerPath,
            Path.GetFileNameWithoutExtension(spec.ControllerPath),
            new[] { new StateClip(spec.StateName, loopClip) });
        CreateOrUpdatePrefab(spec.PrefabPath, Path.GetFileNameWithoutExtension(spec.PrefabPath), frames[0], controller);
    }

    private static void RebuildDualState(DualStateSpec spec)
    {
        Sprite[] loopFrames = LoadSprites(spec.LoopSpritePath);
        Sprite[] endFrames = LoadSprites(spec.EndSpritePath);
        AnimationClip loopClip = CreateOrUpdateClip(
            spec.LoopClipPath,
            $"{spec.BaseName}_{spec.LoopStateName}",
            loopFrames,
            loop: true);
        AnimationClip endClip = CreateOrUpdateClip(
            spec.EndClipPath,
            $"{spec.BaseName}_{spec.EndStateName}",
            endFrames,
            loop: false);
        AnimatorController controller = CreateOrUpdateController(
            spec.ControllerPath,
            Path.GetFileNameWithoutExtension(spec.ControllerPath),
            new[]
            {
                new StateClip(spec.LoopStateName, loopClip),
                new StateClip(spec.EndStateName, endClip),
            });
        CreateOrUpdatePrefab(spec.PrefabPath, Path.GetFileNameWithoutExtension(spec.PrefabPath), loopFrames[0], controller);
    }

    private static void RebuildEgoSwordAura()
    {
        Sprite[] allFrames = LoadSprites(EgoSwordAuraSpec.SpritePath);
        Sprite[] idleFrames = SliceFrames(allFrames, startIndex: 0, count: 5, EgoSwordAuraSpec.SpritePath);
        Sprite[] startFrames = SliceFrames(allFrames, startIndex: 5, count: 4, EgoSwordAuraSpec.SpritePath);
        Sprite[] endFrames = startFrames.Reverse().ToArray();

        AnimationClip startClip = CreateOrUpdateClip(
            EgoSwordAuraSpec.StartClipPath,
            "EgoSwordAttackAura_Start",
            startFrames,
            loop: false);
        AnimationClip idleClip = CreateOrUpdateClip(
            EgoSwordAuraSpec.IdleClipPath,
            "EgoSwordAttackAura_Idle",
            idleFrames,
            loop: true);
        AnimationClip endClip = CreateOrUpdateClip(
            EgoSwordAuraSpec.EndClipPath,
            "EgoSwordAttackAura_End",
            endFrames,
            loop: false);
        CreateOrUpdateController(
            EgoSwordAuraSpec.ControllerPath,
            "EgoSwordAttackAuraVfx",
            new[]
            {
                new StateClip("Start", startClip),
                new StateClip("Idle", idleClip),
                new StateClip("End", endClip),
            });
    }

    private static AnimationClip CreateOrUpdateClip(string path, string clipName, IReadOnlyList<Sprite> frames, bool loop)
    {
        if (frames == null || frames.Count == 0)
            throw new InvalidOperationException($"Cannot create {clipName}: no sprites were loaded.");

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }

        clip.name = clipName;
        clip.frameRate = FrameRate;
        clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
        clip.ClearCurves();

        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            AnimationUtility.SetObjectReferenceCurve(clip, binding, null);

        EditorCurveBinding spriteBinding = new()
        {
            type = typeof(SpriteRenderer),
            path = string.Empty,
            propertyName = "m_Sprite",
        };

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[frames.Count];
        for (int i = 0; i < frames.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / FrameRate,
                value = frames[i],
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController CreateOrUpdateController(
        string path,
        string controllerName,
        IReadOnlyList<StateClip> states)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        controller.name = controllerName;
        if (controller.layers == null || controller.layers.Length == 0)
            controller.AddLayer("Base Layer");

        AnimatorControllerLayer layer = controller.layers[0];
        layer.name = "Base Layer";
        AnimatorStateMachine stateMachine = layer.stateMachine;

        foreach (ChildAnimatorState childState in stateMachine.states)
            stateMachine.RemoveState(childState.state);
        foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
            stateMachine.RemoveStateMachine(childStateMachine.stateMachine);
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
            stateMachine.RemoveAnyStateTransition(transition);

        for (int i = 0; i < states.Count; i++)
        {
            StateClip stateClip = states[i];
            AnimatorState state = stateMachine.AddState(stateClip.StateName, new Vector3(260f, 70f + i * 70f, 0f));
            state.motion = stateClip.Clip;
            state.speed = 1f;
            state.writeDefaultValues = true;
            if (i == 0)
                stateMachine.defaultState = state;
        }

        layer.stateMachine = stateMachine;
        AnimatorControllerLayer[] layers = controller.layers;
        layers[0] = layer;
        controller.layers = layers;

        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void CreateOrUpdatePrefab(
        string path,
        string prefabName,
        Sprite firstSprite,
        RuntimeAnimatorController controller)
    {
        GameObject root = new(prefabName);
        try
        {
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = firstSprite;
            renderer.sortingLayerName = ProjectileSortingLayer;
            renderer.sortingOrder = ProjectileSortingOrder;
            renderer.maskInteraction = SpriteMaskInteraction.None;

            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static bool IsClipValid(string path, bool loop)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
            return false;

        EditorCurveBinding binding = AnimationUtility.GetObjectReferenceCurveBindings(clip)
            .FirstOrDefault(candidate =>
                candidate.type == typeof(SpriteRenderer)
                && candidate.propertyName == "m_Sprite");
        if (string.IsNullOrEmpty(binding.propertyName))
            return false;

        ObjectReferenceKeyframe[] curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        if (curve == null || curve.Length == 0 || curve.Any(key => key.value == null))
            return false;

        return AnimationUtility.GetAnimationClipSettings(clip).loopTime == loop;
    }

    private static bool IsControllerValid(string path, IReadOnlyCollection<string> expectedStates)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null || controller.layers == null || controller.layers.Length == 0)
            return false;
        if (controller.name != Path.GetFileNameWithoutExtension(path))
            return false;

        ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
        foreach (string expectedState in expectedStates)
        {
            if (!states.Any(child => child.state != null
                && child.state.name == expectedState
                && child.state.motion != null))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPrefabValid(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            return false;
        if (prefab.name != Path.GetFileNameWithoutExtension(path))
            return false;

        SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
        Animator animator = prefab.GetComponent<Animator>();
        return renderer != null
            && renderer.sprite != null
            && animator != null
            && animator.runtimeAnimatorController != null;
    }

    private static Sprite[] LoadSprites(string spritePath)
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(spritePath)
            .OfType<Sprite>()
            .OrderBy(sprite => ExtractFrameIndex(sprite.name))
            .ThenBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToArray();

        if (sprites.Length == 0)
            throw new InvalidOperationException($"No sprites found at {spritePath}.");

        return sprites;
    }

    private static Sprite[] SliceFrames(Sprite[] frames, int startIndex, int count, string spritePath)
    {
        if (frames.Length < startIndex + count)
        {
            throw new InvalidOperationException(
                $"{spritePath} has {frames.Length} frame(s), but frames {startIndex}..{startIndex + count - 1} are required.");
        }

        return frames.Skip(startIndex).Take(count).ToArray();
    }

    private static int ExtractFrameIndex(string spriteName)
    {
        int separatorIndex = spriteName.LastIndexOf('_');
        if (separatorIndex < 0 || separatorIndex >= spriteName.Length - 1)
            return 0;

        return int.TryParse(
            spriteName.Substring(separatorIndex + 1),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int frameIndex)
            ? frameIndex
            : 0;
    }

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/DemonKing"))
            AssetDatabase.CreateFolder("Assets/Resources", "DemonKing");
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/Resources/DemonKing", "Vfx");
    }

    private readonly struct OneShotSpec
    {
        public OneShotSpec(
            string baseName,
            string spritePath,
            string clipPath,
            string controllerPath,
            string prefabPath)
        {
            BaseName = baseName;
            SpritePath = spritePath;
            ClipPath = clipPath;
            ControllerPath = controllerPath;
            PrefabPath = prefabPath;
        }

        public string BaseName { get; }
        public string SpritePath { get; }
        public string ClipPath { get; }
        public string ControllerPath { get; }
        public string PrefabPath { get; }
    }

    private readonly struct AuraSpec
    {
        public AuraSpec(
            string baseName,
            string spritePath,
            string startClipPath,
            string idleClipPath,
            string endClipPath,
            string controllerPath)
        {
            BaseName = baseName;
            SpritePath = spritePath;
            StartClipPath = startClipPath;
            IdleClipPath = idleClipPath;
            EndClipPath = endClipPath;
            ControllerPath = controllerPath;
        }

        public string BaseName { get; }
        public string SpritePath { get; }
        public string StartClipPath { get; }
        public string IdleClipPath { get; }
        public string EndClipPath { get; }
        public string ControllerPath { get; }
    }

    private readonly struct LoopSpec
    {
        public LoopSpec(
            string baseName,
            string spritePath,
            string clipPath,
            string controllerPath,
            string prefabPath,
            string stateName)
        {
            BaseName = baseName;
            SpritePath = spritePath;
            ClipPath = clipPath;
            ControllerPath = controllerPath;
            PrefabPath = prefabPath;
            StateName = stateName;
        }

        public string BaseName { get; }
        public string SpritePath { get; }
        public string ClipPath { get; }
        public string ControllerPath { get; }
        public string PrefabPath { get; }
        public string StateName { get; }
    }

    private readonly struct DualStateSpec
    {
        public DualStateSpec(
            string baseName,
            string loopSpritePath,
            string loopClipPath,
            string endSpritePath,
            string endClipPath,
            string controllerPath,
            string prefabPath,
            string loopStateName,
            string endStateName)
        {
            BaseName = baseName;
            LoopSpritePath = loopSpritePath;
            LoopClipPath = loopClipPath;
            EndSpritePath = endSpritePath;
            EndClipPath = endClipPath;
            ControllerPath = controllerPath;
            PrefabPath = prefabPath;
            LoopStateName = loopStateName;
            EndStateName = endStateName;
        }

        public string BaseName { get; }
        public string LoopSpritePath { get; }
        public string LoopClipPath { get; }
        public string EndSpritePath { get; }
        public string EndClipPath { get; }
        public string ControllerPath { get; }
        public string PrefabPath { get; }
        public string LoopStateName { get; }
        public string EndStateName { get; }
    }

    private readonly struct StateClip
    {
        public StateClip(string stateName, AnimationClip clip)
        {
            StateName = stateName;
            Clip = clip;
        }

        public string StateName { get; }
        public AnimationClip Clip { get; }
    }
}
