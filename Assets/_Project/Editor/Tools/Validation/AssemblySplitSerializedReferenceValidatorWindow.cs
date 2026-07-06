using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임:
/// - asmdef 분리 이후 YAML 직렬화에 남은 Assembly-CSharp 참조와 m_Script GUID 손상을 점검한다.
/// - Behavior Graph 같은 managed-reference 직렬화의 missing-type / placeholder 플래그를 점검한다.
/// - Addressables 그룹 엔트리가 살아 있고 로드 가능한 고유 에셋 GUID와 주소 경로를 가리키는지 점검한다.
/// - 주요 씬, 프리팹, ScriptableObject 에셋이 AssetDatabase에서 로드 가능한지와 씬/프리팹 hierarchy missing script가 없는지 점검한다.
/// - Assets 전체 .meta GUID 누락/중복과 primary scan 밖 Assembly-CSharp / m_Script 잔여를 별도 점검한다.
/// - 여러 어셈블리에 걸친 네임스페이스를 보고해 네임스페이스와 asmdef 경계 혼동을 방지한다.
/// - target assembly 간 중복 top-level 타입 선언을 점검한다.
/// - 전체 asmdef 이름 중복과 target asmdef의 기대 경로를 점검한다.
/// - target source root 내부 nested asmdef/asmref를 점검해 6개 project assembly 경계를 보존한다.
/// - 프로덕션 C# 소스가 6개 target source root 밖으로 새지 않는지 점검한다.
/// - target asmdef의 플랫폼, 참조 옵션, 직접 참조 방향을 점검한다.
/// - 외부 패키지 API 사용과 asmdef package reference 누락 여부를 점검한다.
/// - Core/Gameplay가 상위 계층의 대표 구체 타입, 네임스페이스 import, qualified name을 직접 참조하는지 점검한다.
/// - Core/Gameplay가 구체 UI/카메라/트윈/조명 표현 API를 직접 참조하는지 점검한다.
/// - 검증 도구 밖 프로젝트 소스에 기본 Assembly-CSharp 어셈블리명 하드코딩이 남았는지 점검한다.
/// - 현재 타입이 명확히 살아 있는 UnityEvent 대상 어셈블리명과 m_Script GUID만 수동 수정 도구로 제공한다.
/// </summary>
public sealed class AssemblySplitSerializedReferenceValidatorWindow : EditorWindow
{
    private static readonly string[] ScanRoots =
    {
        "Assets/_Project",
        "Assets/AddressableAssetsData",
        "ProjectSettings"
    };

    private static readonly string[] SerializedExtensions =
    {
        ".asset",
        ".prefab",
        ".unity",
        ".xml",
        ".controller",
        ".overrideController"
    };

    private static readonly string[] AssetImportLoadabilityExtensions =
    {
        ".asset",
        ".prefab",
        ".unity"
    };

    private static readonly string[] TargetAssemblyNames =
    {
        "Core",
        "Gameplay",
        "Infrastructure",
        "Presentation",
        "UI",
        "Editor"
    };

    private static readonly string[] RuntimeAssemblyNames =
    {
        "Core",
        "Gameplay",
        "Infrastructure",
        "Presentation",
        "UI"
    };

    private static readonly Dictionary<string, string> ExpectedTargetAsmdefPaths =
        new Dictionary<string, string>
        {
            { "Core", "Assets/_Project/Runtime/Core/Core.asmdef" },
            { "Gameplay", "Assets/_Project/Runtime/Features/Gameplay.asmdef" },
            { "Infrastructure", "Assets/_Project/Runtime/Infrastructure/Infrastructure.asmdef" },
            { "Presentation", "Assets/_Project/Runtime/Presentation/Presentation.asmdef" },
            { "UI", "Assets/_Project/Runtime/UI/UI.asmdef" },
            { "Editor", "Assets/_Project/Editor/Editor.asmdef" }
        };

    private static readonly Dictionary<string, string> ExpectedSupportAsmdefPaths =
        new Dictionary<string, string>
        {
            { "PlayModeTests", "Assets/_Project/Tests/PlayMode/PlayModeTests.asmdef" },
            { "DOTween.Modules", "Assets/Plugins/Demigiant/DOTween/Modules/DOTween.Modules.asmdef" },
            { "DOTweenPro.Scripts", "Assets/Plugins/Demigiant/DOTweenPro/DOTweenPro.Scripts.asmdef" },
            { "DOTweenPro.Scripts.Editor", "Assets/Plugins/Demigiant/DOTweenPro/Editor/DOTweenPro.Scripts.Editor.asmdef" },
            { "Ink-Libraries", "Assets/Ink/InkLibs/Ink-Libraries.asmdef" },
            { "InkEditor", "Assets/Ink/Editor/InkEditor.asmdef" },
            { "Ink.Demos.Basic", "Assets/Ink/Demos/Basic Demo/Scripts/Ink.Demos.Basic.asmdef" },
            { "Ink.Demos.Basic.Editor", "Assets/Ink/Demos/Basic Demo/Scripts/Editor/Ink.Demos.Basic.Editor.asmdef" }
        };

    private static readonly Dictionary<string, string> TargetAssemblySourceRoots =
        new Dictionary<string, string>
        {
            { "Core", "Assets/_Project/Runtime/Core" },
            { "Gameplay", "Assets/_Project/Runtime/Features" },
            { "Infrastructure", "Assets/_Project/Runtime/Infrastructure" },
            { "Presentation", "Assets/_Project/Runtime/Presentation" },
            { "UI", "Assets/_Project/Runtime/UI" },
            { "Editor", "Assets/_Project/Editor" }
        };

    private static readonly string[] TestOnlyProjectSourceRoots =
    {
        "Assets/_Project/Tests"
    };

    private static readonly string[] AllowedAssetSourceAssemblies =
    {
        "Core",
        "Gameplay",
        "Infrastructure",
        "Presentation",
        "UI",
        "Editor",
        "PlayModeTests",
        "DOTween.Modules",
        "DOTweenPro.Scripts",
        "DOTweenPro.Scripts.Editor",
        "Ink-Libraries",
        "InkEditor",
        "Ink.Demos.Basic",
        "Ink.Demos.Basic.Editor"
    };

    private static readonly Dictionary<string, string[]> AllowedProjectAssemblyReferences =
        new Dictionary<string, string[]>
        {
            { "Core", Array.Empty<string>() },
            { "Gameplay", new[] { "Core" } },
            { "Infrastructure", new[] { "Core", "Gameplay" } },
            { "Presentation", new[] { "Core", "Gameplay", "Infrastructure" } },
            { "UI", new[] { "Core", "Gameplay", "Infrastructure", "Presentation" } },
            { "Editor", new[] { "Core", "Gameplay", "Infrastructure", "Presentation", "UI" } }
        };

    private static readonly Regex MissingScriptRegex = new Regex(
        @"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-f]{32}),\s*type:\s*3\}",
        RegexOptions.Compiled);

    private static readonly Regex ManagedReferenceIntegrityFlagRegex = new Regex(
        @"^\s*(m_HasMissingTypeInManagedRef|m_BlackboardMissingManagedRef|m_GraphMissingManagedRef|m_WasCompileWithPlaceholderNode|IsPlaceholder):\s*1\s*$",
        RegexOptions.Compiled);

    private static readonly Regex EditorClassIdentifierAssemblyRegex = new Regex(
        @"m_EditorClassIdentifier:\s*(Assembly-CSharp(?:-Editor)?)(?:::|$)",
        RegexOptions.Compiled);

    private static readonly Regex AddressableEntryGuidRegex = new Regex(
        @"^\s*-\s*m_GUID:\s*([0-9a-f]{32})\s*$",
        RegexOptions.Compiled);

    private static readonly Regex AsmdefNameRegex = new Regex(
        @"""name""\s*:\s*""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex QuotedStringRegex = new Regex(
        @"""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex NamespaceDeclarationRegex = new Regex(
        @"^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)\b",
        RegexOptions.Compiled);

    private static readonly Regex TypeDeclarationRegex = new Regex(
        @"^\s*(?:\[[^\]]+\]\s*)*(?:(?:public|internal|private|protected|sealed|abstract|static|partial|readonly|unsafe|new)\s+)*(?:class|interface|struct|record(?:\s+struct|\s+class)?|enum)\s+([A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled);

    private static readonly RequiredExternalAsmdefReferenceRule[] RequiredExternalAsmdefReferenceRules =
    {
        new RequiredExternalAsmdefReferenceRule("DOTween.Modules", @"(?m)^\s*using\s+DG\.Tweening\s*;|\bDG\.Tweening\.", "DOTween APIs"),
        new RequiredExternalAsmdefReferenceRule("Ink-Libraries", @"(?m)^\s*using\s+Ink\.Runtime\s*;|\bInk\.Runtime\.", "Ink runtime APIs"),
        new RequiredExternalAsmdefReferenceRule("Unity.2D.Animation.Runtime", @"(?m)^\s*using\s+UnityEngine\.U2D\.Animation\s*;|\bSpriteLibraryAsset\b", "2D Animation sprite-library APIs"),
        new RequiredExternalAsmdefReferenceRule("Unity.2D.PixelPerfect", @"\bPixelPerfectCamera\b", "2D Pixel Perfect camera APIs"),
        new RequiredExternalAsmdefReferenceRule("Unity.Behavior", @"(?m)^\s*using\s+Unity\.Behavior\s*;|\bUnity\.Behavior\.", "Unity Behavior APIs"),
        new RequiredExternalAsmdefReferenceRule("Unity.Cinemachine", @"(?m)^\s*using\s+Unity\.Cinemachine\s*;|\bUnity\.Cinemachine\.", "Cinemachine APIs"),
        new RequiredExternalAsmdefReferenceRule("Unity.InputSystem", @"(?m)^\s*using\s+UnityEngine\.InputSystem(?:\.|\s*;)|\bUnityEngine\.InputSystem\.", "Input System APIs"),
        new RequiredExternalAsmdefReferenceRule("Unity.TextMeshPro", @"(?m)^\s*using\s+TMPro\s*;|\bTMPro\.", "TextMeshPro APIs"),
        new RequiredExternalAsmdefReferenceRule("UnityEngine.UI", @"(?m)^\s*using\s+UnityEngine\.UI\s*;|\bUnityEngine\.UI\.", "Unity UI APIs"),
        new RequiredExternalAsmdefReferenceRule("Unity.Addressables", @"(?m)^\s*using\s+UnityEngine\.AddressableAssets(?:\.|\s*;)|\bUnityEngine\.AddressableAssets\.", "Addressables APIs"),
        new RequiredExternalAsmdefReferenceRule("Unity.ResourceManager", @"(?m)^\s*using\s+UnityEngine\.ResourceManagement(?:\.|\s*;)|\bUnityEngine\.ResourceManagement\.", "Resource Manager APIs"),
        new RequiredExternalAsmdefReferenceRule("Unity.RenderPipelines.Universal.Runtime", @"(?m)^\s*using\s+UnityEngine\.Rendering\.Universal\s*;|\bUnityEngine\.Rendering\.Universal\.", "URP runtime APIs"),
        new RequiredExternalAsmdefReferenceRule("Unity.RenderPipelines.Universal.2D.Runtime", @"\b(Light2D|ShadowCaster2D)\b", "URP 2D renderer APIs")
    };

    private static readonly ForbiddenSourceApiRule[] LowerLayerForbiddenPresentationApiRules =
    {
        new ForbiddenSourceApiRule(@"(?m)^\s*using\s+TMPro\s*;|\bTMPro\.|\bTMP_Text\b|\bTextMeshPro(?:UGUI)?\b", "TextMeshPro concrete UI APIs"),
        new ForbiddenSourceApiRule(@"(?m)^\s*using\s+UnityEngine\.UI\s*;|\bUnityEngine\.UI\.", "UnityEngine.UI concrete UI APIs"),
        new ForbiddenSourceApiRule(@"(?m)^\s*using\s+Unity\.Cinemachine\s*;|\bUnity\.Cinemachine\.|\bCinemachine(?:Camera|Brain|ImpulseSource|VirtualCameraBase)\b", "Cinemachine concrete camera APIs"),
        new ForbiddenSourceApiRule(@"(?m)^\s*using\s+DG\.Tweening\s*;|\bDG\.Tweening\.|\bDOTween\b|\bDOVirtual\b", "DOTween concrete tween APIs"),
        new ForbiddenSourceApiRule(@"(?m)^\s*using\s+UnityEngine\.Rendering\.Universal\s*;|\bUnityEngine\.Rendering\.Universal\.|\bLight2D\b|\bShadowCaster2D\b", "URP concrete 2D lighting APIs")
    };

    private static readonly string[] CoreForbiddenConcreteTypes =
    {
        "SoundManager",
        "CombatHitAudioRouter",
        "CameraBootstrap",
        "CameraShakeService",
        "WorldPresentationRuntime",
        "PresentationSpawnService",
        "DamagePopupService",
        "DamagePopupListener2D",
        "GlobalUIRoot",
        "DialogueView",
        "GameSettingsService",
        "AttackTelegraphService",
        "AttackTelegraphView",
        "BossGroggyHeadTimer",
        "TimedAnimatedHitEffect2D",
        "GameplayCue_HitSparkParticles",
        "PlayerIntentInput2D",
        "ChestUIManager",
        "UIManager"
    };

    private static readonly string[] GameplayForbiddenConcreteTypes =
    {
        "CameraBootstrap",
        "CameraShakeService",
        "WorldPresentationRuntime",
        "PresentationSpawnService",
        "AttackTelegraphService",
        "AttackTelegraphView",
        "DialogueView",
        "ChestUIManager",
        "UIManager",
        "GameOverPresentationController",
        "EndingOutroView",
        "TutorialInfoPanel",
        "TutorialPresentationHpView",
        "AffectionUI",
        "RewardDisplayService",
        "UpgradeTreeUI",
        "BossHudController",
        "DamagePopupService"
    };

    private static readonly string[] RuntimeForbiddenEditorApiNames =
    {
        "EditorWindow",
        "EditorGUILayout",
        "EditorGUI",
        "CustomEditor",
        "MenuItem",
        "AssetDatabase",
        "PrefabUtility",
        "SerializedObject",
        "SerializedProperty",
        "EditorApplication",
        "EditorUtility",
        "SceneView",
        "Handles"
    };

    private static readonly SafeUnityEventTargetAssemblyReplacement[] SafeUnityEventTargetAssemblyReplacements =
    {
        new SafeUnityEventTargetAssemblyReplacement(
            "UpgradeTreeUI, Assembly-CSharp",
            "UpgradeTreeUI, UI",
            "55879c0390757294b87bcadbd98ac5ed"),
        new SafeUnityEventTargetAssemblyReplacement(
            "UnlockResultUI, Assembly-CSharp",
            "RewardDisplayUI, UI",
            "87828d5a4061cd940bf17f9c78f7da21"),
        new SafeUnityEventTargetAssemblyReplacement(
            "TutorialSceneSequenceDirector, Assembly-CSharp",
            "TutorialSceneSequenceDirector, Gameplay",
            "a5515df252ac4bbbb19e3b03d903bdcd"),
        new SafeUnityEventTargetAssemblyReplacement(
            "TutorialPlayerAutoMove, Assembly-CSharp",
            "TutorialPlayerAutoMove, Gameplay",
            "5b10edc9ffa34df99636c2988ce2a6ad"),
        new SafeUnityEventTargetAssemblyReplacement(
            "TutorialInfoTrigger, Assembly-CSharp",
            "TutorialInfoTrigger, Gameplay",
            "bbcc2e2b1020e644faf496c27241f760"),
        new SafeUnityEventTargetAssemblyReplacement(
            "TutorialCombatIntroSequence, Assembly-CSharp",
            "TutorialCombatIntroSequence, Gameplay",
            "837d7f5f8f124f64bb86cebcf4029894")
    };

    /// <summary>
    /// 책임:
    /// - UnityEvent 대상 타입 문자열을 바꿀 때 실제 target 컴포넌트 MonoScript GUID까지 함께 검증한다.
    /// </summary>
    private sealed class SafeUnityEventTargetAssemblyReplacement
    {
        public SafeUnityEventTargetAssemblyReplacement(string legacyTypeName, string replacementTypeName, string expectedTargetScriptGuid)
        {
            LegacyTypeName = legacyTypeName;
            ReplacementTypeName = replacementTypeName;
            ExpectedTargetScriptGuid = expectedTargetScriptGuid;
        }

        public string LegacyTypeName { get; }
        public string ReplacementTypeName { get; }
        public string ExpectedTargetScriptGuid { get; }
    }

    /// <summary>
    /// 책임:
    /// - Core/Gameplay 소스에서 금지할 구체 표현 API 정규식과 설명을 검증 로직에 전달한다.
    /// </summary>
    private sealed class ForbiddenSourceApiRule
    {
        public ForbiddenSourceApiRule(string pattern, string description)
        {
            Pattern = new Regex(pattern, RegexOptions.Compiled);
            Description = description;
        }

        public Regex Pattern { get; }
        public string Description { get; }
    }

    private static readonly Dictionary<string, string> SafeScriptGuidReplacements =
        new Dictionary<string, string>
        {
            { "2ac5f84fdf6c49fdb88721db1b68ef98", "4a4e8e4b6b0b77a45a9ed3732ce9ad4f" },
            { "6f5a90f75efdf6745b16ec72c1d92a8c", "af77d418566312547b4c270f72388509" }
        };

    private static readonly Dictionary<string, string> KnownPackageMissingScriptGuids =
        new Dictionary<string, string>
        {
            { "65bae8b9f1bd244b3a27e92af4b23b2a", "Unity.VisualScripting.DictionaryAsset in ProjectSettings/VisualScriptingSettings.asset" },
            { "95e66c6366d904e98bc83428217d4fd7", "Unity.VisualScripting.ScriptGraphAsset in Visual Scripting graph assets" },
            { "765181c9ef4b24d32a4f7cbd2ef370dc", "Unity.VisualScripting.SceneVariables in PixelLightTest" },
            { "e741851cba3ad425c91ecf922cc6b379", "Unity.VisualScripting.Variables in PixelLightTest" }
        };

    private enum Severity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// 책임:
    /// - 직렬화 참조 검증에서 발견한 단일 위치와 판단 결과를 에디터 창에 전달한다.
    /// </summary>
    private sealed class ValidationResult
    {
        public string Path;
        public int LineNumber;
        public Severity SeverityLevel;
        public string Message;
        public string Line;
    }

    /// <summary>
    /// 책임:
    /// - asmdef 파일의 이름, 경로, 참조, 플랫폼, 위험 옵션 설정을 검증 로직에 전달한다.
    /// </summary>
    private sealed class AsmdefInfo
    {
        public string Name;
        public string Path;
        public string RootNamespace;
        public string[] References;
        public string[] IncludePlatforms;
        public string[] ExcludePlatforms;
        public string[] OptionalUnityReferences;
        public string[] PrecompiledReferences;
        public string[] DefineConstraints;
        public string[] VersionDefines;
        public bool OverrideReferences;
        public bool AllowUnsafeCode;
        public bool AutoReferenced;
        public bool NoEngineReferences;
    }

    /// <summary>
    /// 책임:
    /// - 소스 패턴으로 감지할 외부 패키지 API와 필요한 asmdef reference 이름을 묶어 전달한다.
    /// </summary>
    private sealed class RequiredExternalAsmdefReferenceRule
    {
        public RequiredExternalAsmdefReferenceRule(string reference, string pattern, string description)
        {
            Reference = reference;
            Pattern = new Regex(pattern, RegexOptions.Compiled);
            Description = description;
        }

        public string Reference { get; }
        public Regex Pattern { get; }
        public string Description { get; }
    }

    /// <summary>
    /// 책임:
    /// - top-level 타입 선언의 assembly, 파일 경로, 줄 번호를 중복 타입 검증에 전달한다.
    /// </summary>
    private sealed class TypeDeclarationLocation
    {
        public TypeDeclarationLocation(string assemblyName, string path, int lineNumber)
        {
            AssemblyName = assemblyName;
            Path = path;
            LineNumber = lineNumber;
        }

        public string AssemblyName { get; }
        public string Path { get; }
        public int LineNumber { get; }
    }

    private readonly List<ValidationResult> results = new List<ValidationResult>();
    private Vector2 scrollPosition;

    [MenuItem("Tools/Validation/Assembly Split Serialized References")]
    public static void ShowWindow()
    {
        GetWindow<AssemblySplitSerializedReferenceValidatorWindow>("Assembly Split Validator");
    }

    public static void RunAllValidationsFromCommandLine()
    {
        bool applyVisualScriptingCleanup = Environment.GetCommandLineArgs()
            .Any(argument => string.Equals(argument, "-assemblySplitApplyVisualScriptingCleanup", StringComparison.OrdinalIgnoreCase));
        bool allowWarnings = Environment.GetCommandLineArgs()
            .Any(argument => string.Equals(argument, "-assemblySplitAllowValidationWarnings", StringComparison.OrdinalIgnoreCase));

        bool succeeded = RunAllValidationsForBatch(applyVisualScriptingCleanup, failOnWarnings: !allowWarnings);
        EditorApplication.Exit(succeeded ? 0 : 1);
    }

    private static bool RunAllValidationsForBatch(bool applyVisualScriptingCleanup, bool failOnWarnings)
    {
        AssemblySplitSerializedReferenceValidatorWindow validator = CreateInstance<AssemblySplitSerializedReferenceValidatorWindow>();
        List<ValidationResult> aggregateResults = new List<ValidationResult>();

        try
        {
            if (applyVisualScriptingCleanup &&
                !AssemblySplitVisualScriptingResidualCleanupTool.ApplyVisualScriptingResidualCleanup(exitEditorWhenFinished: false))
            {
                Debug.LogError("Assembly split batch validation aborted because Visual Scripting residual cleanup failed.");
                return false;
            }

            validator.RunValidationStepForBatch("Serialized References", validator.ValidateSerializedReferences, aggregateResults);
            validator.RunValidationStepForBatch("Asset Import Loadability", validator.ValidateAssetImportLoadability, aggregateResults);
            validator.RunValidationStepForBatch("Addressables", validator.ValidateAddressableEntries, aggregateResults);
            validator.RunValidationStepForBatch("Assembly Boundaries", validator.ValidateAssemblyBoundaries, aggregateResults);
            validator.RunValidationStepForBatch("Unity Compile Outputs", validator.ValidateUnityCompileOutputs, aggregateResults);
            validator.RunValidationStepForBatch("Generated Projects", validator.ValidateGeneratedProjectFiles, aggregateResults);

            int errorCount = aggregateResults.Count(result => result.SeverityLevel == Severity.Error);
            int warningCount = aggregateResults.Count(result => result.SeverityLevel == Severity.Warning);
            int infoCount = aggregateResults.Count(result => result.SeverityLevel == Severity.Info);

            Debug.Log($"Assembly split Editor validation summary: Errors={errorCount}, Warnings={warningCount}, Infos={infoCount}");
            for (int i = 0; i < aggregateResults.Count; i++)
            {
                ValidationResult result = aggregateResults[i];
                string line = string.IsNullOrWhiteSpace(result.Line) ? string.Empty : $" | {result.Line}";
                string message = $"{result.SeverityLevel} {result.Path}:{result.LineNumber} {result.Message}{line}";
                if (result.SeverityLevel == Severity.Error)
                    Debug.LogError(message);
                else if (result.SeverityLevel == Severity.Warning)
                    Debug.LogWarning(message);
            }

            return errorCount == 0 && (!failOnWarnings || warningCount == 0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
        finally
        {
            DestroyImmediate(validator);
        }
    }

    private void RunValidationStepForBatch(string stepName, Action validationAction, List<ValidationResult> aggregateResults)
    {
        validationAction();

        for (int i = 0; i < results.Count; i++)
        {
            ValidationResult result = results[i];
            aggregateResults.Add(new ValidationResult
            {
                Path = result.Path,
                LineNumber = result.LineNumber,
                SeverityLevel = result.SeverityLevel,
                Message = $"[{stepName}] {result.Message}",
                Line = result.Line
            });
        }
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawSummary();
        DrawResults();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Validate Serialized References", EditorStyles.toolbarButton))
                ValidateSerializedReferences();

            if (GUILayout.Button("Validate Asset Import Loadability", EditorStyles.toolbarButton))
                ValidateAssetImportLoadability();

            if (GUILayout.Button("Validate Addressables", EditorStyles.toolbarButton))
                ValidateAddressableEntries();

            if (GUILayout.Button("Validate Assembly Boundaries", EditorStyles.toolbarButton))
                ValidateAssemblyBoundaries();

            if (GUILayout.Button("Validate Unity Compile Outputs", EditorStyles.toolbarButton))
                ValidateUnityCompileOutputs();

            if (GUILayout.Button("Validate Generated Projects", EditorStyles.toolbarButton))
                ValidateGeneratedProjectFiles();

            if (GUILayout.Button("Apply Safe UnityEvent Assembly Fixes", EditorStyles.toolbarButton))
                ApplySafeUnityEventAssemblyFixes();

            if (GUILayout.Button("Apply Safe Secondary UnityEvent Fixes", EditorStyles.toolbarButton))
                ApplySafeSecondaryUnityEventAssemblyFixes();

            if (GUILayout.Button("Apply Safe m_Script GUID Fixes", EditorStyles.toolbarButton))
                ApplySafeScriptGuidFixes();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
                results.Clear();
        }
    }

    private void DrawSummary()
    {
        int errorCount = results.Count(result => result.SeverityLevel == Severity.Error);
        int warningCount = results.Count(result => result.SeverityLevel == Severity.Warning);
        int infoCount = results.Count(result => result.SeverityLevel == Severity.Info);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Errors", errorCount.ToString());
        EditorGUILayout.LabelField("Warnings", warningCount.ToString());
        EditorGUILayout.LabelField("Infos", infoCount.ToString());
        EditorGUILayout.Space(6f);
    }

    private void DrawResults()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (results.Count == 0)
        {
            EditorGUILayout.HelpBox("No results yet. Run validation to inspect serialized assembly split references.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        foreach (ValidationResult result in results)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{result.SeverityLevel} - {result.Path}:{result.LineNumber}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(result.Message, EditorStyles.wordWrappedLabel);

                if (!string.IsNullOrWhiteSpace(result.Line))
                    EditorGUILayout.LabelField(result.Line, EditorStyles.wordWrappedMiniLabel);

                if (GUILayout.Button("Ping", GUILayout.Width(64f)))
                    PingPath(result.Path);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void ValidateSerializedReferences()
    {
        results.Clear();
        int fileCount = 0;
        Dictionary<string, int> filesByExtension = new Dictionary<string, int>();
        Dictionary<string, int> editorClassIdentifierCounts = new Dictionary<string, int>();

        foreach (string path in EnumerateSerializedFiles())
        {
            fileCount++;
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (!filesByExtension.ContainsKey(extension))
                filesByExtension[extension] = 0;

            filesByExtension[extension]++;

            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                ValidateAssemblyCSharpLine(path, index + 1, line, editorClassIdentifierCounts);
                ValidateMissingScriptLine(path, index + 1, line);
                ValidateManagedReferenceIntegrityLine(path, index + 1, line);
            }
        }

        bool hasPrimaryFindings = results.Count > 0;
        AddEditorClassIdentifierCacheSummary(editorClassIdentifierCounts);
        AddResult(
            string.Empty,
            0,
            Severity.Info,
            $"Serialized scan covered {fileCount} files.",
            string.Join(", ", filesByExtension.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}")));

        if (!hasPrimaryFindings)
            AddResult(string.Empty, 0, Severity.Info, "No primary serialized Assembly-CSharp references, missing/non-C# m_Script GUIDs, or managed-reference integrity flags were found.", string.Empty);

        ValidateSecondarySerializedAssemblyCSharpResiduals();
        ValidateSecondarySerializedScriptReferences();
    }

    private void ValidateAssetImportLoadability()
    {
        results.Clear();

        int assetCount = 0;
        int sceneCount = 0;
        int prefabCount = 0;
        int scriptableObjectCount = 0;
        int addressablesDataAssetCount = 0;
        Dictionary<string, int> filesByExtension = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (string path in EnumerateAssetImportLoadabilityFiles())
        {
            assetCount++;
            string normalizedPath = NormalizeProjectPath(path);
            string extension = Path.GetExtension(normalizedPath).ToLowerInvariant();
            if (!filesByExtension.ContainsKey(extension))
                filesByExtension[extension] = 0;

            filesByExtension[extension]++;
            if (normalizedPath.StartsWith("Assets/AddressableAssetsData/", StringComparison.Ordinal))
                addressablesDataAssetCount++;

            string guid = AssetDatabase.AssetPathToGUID(normalizedPath);
            if (string.IsNullOrWhiteSpace(guid))
            {
                AddResult(normalizedPath, 0, Severity.Error, "AssetDatabase has no GUID for this asset path.", string.Empty);
                continue;
            }

            Type mainAssetType = AssetDatabase.GetMainAssetTypeAtPath(normalizedPath);
            if (mainAssetType == null)
            {
                AddResult(normalizedPath, 0, Severity.Error, "AssetDatabase could not resolve a main asset type.", string.Empty);
                continue;
            }

            UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(normalizedPath);
            if (mainAsset == null)
            {
                AddResult(normalizedPath, 0, Severity.Error, $"AssetDatabase could not load the main asset. Type={mainAssetType.FullName}", string.Empty);
                continue;
            }

            if (extension == ".unity")
            {
                sceneCount++;
                if (!(mainAsset is SceneAsset))
                    AddResult(normalizedPath, 0, Severity.Error, $"Scene asset loaded as unexpected type: {mainAsset.GetType().FullName}", string.Empty);
                else
                    ValidateSceneHierarchyMissingScripts(normalizedPath);
            }
            else if (extension == ".prefab")
            {
                prefabCount++;
                GameObject prefabRoot = mainAsset as GameObject;
                if (prefabRoot == null)
                {
                    AddResult(normalizedPath, 0, Severity.Error, $"Prefab asset loaded as unexpected type: {mainAsset.GetType().FullName}", string.Empty);
                    continue;
                }

                int missingScriptCount = CountMissingScriptsInHierarchy(prefabRoot);
                if (missingScriptCount > 0)
                    AddResult(normalizedPath, 0, Severity.Error, $"Prefab hierarchy contains missing script components. Count={missingScriptCount}", string.Empty);
            }
            else if (extension == ".asset" && typeof(ScriptableObject).IsAssignableFrom(mainAssetType))
            {
                scriptableObjectCount++;
            }
        }

        if (results.Count == 0)
        {
            AddResult(
                string.Empty,
                0,
                Severity.Info,
                $"AssetDatabase loadability validation passed. Assets={assetCount}; Scenes={sceneCount}; Prefabs={prefabCount}; ScriptableObjects={scriptableObjectCount}; AddressablesDataAssets={addressablesDataAssetCount}",
                string.Join(", ", filesByExtension.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}")));
        }
    }

    private void ValidateAddressableEntries()
    {
        results.Clear();

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string groupRoot = Path.Combine(projectRoot, "Assets/AddressableAssetsData/AssetGroups".Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(groupRoot))
        {
            AddResult(groupRoot, 0, Severity.Error, "Addressables asset group folder is missing.", string.Empty);
            return;
        }

        int entryCount = 0;
        int loadableEntryCount = 0;
        Dictionary<string, string> entryLocationsByGuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> loadableEntriesByType = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string groupPath in Directory.GetFiles(groupRoot, "*.asset", SearchOption.TopDirectoryOnly))
        {
            string[] lines = File.ReadAllLines(groupPath);
            for (int index = 0; index < lines.Length; index++)
            {
                Match entryMatch = AddressableEntryGuidRegex.Match(lines[index]);
                if (!entryMatch.Success)
                    continue;

                entryCount++;
                string entryGuid = entryMatch.Groups[1].Value;
                int lineNumber = index + 1;
                string entryLocation = $"{ToProjectRelativePath(groupPath)}:{lineNumber}";
                string resolvedAssetPath = AssetDatabase.GUIDToAssetPath(entryGuid);
                string entryAddress = ReadAddressableEntryAddress(lines, index + 1);

                if (entryLocationsByGuid.TryGetValue(entryGuid, out string firstLocation))
                    AddResult(groupPath, lineNumber, Severity.Error, $"Duplicate Addressable entry GUID found. Guid={entryGuid} First={firstLocation}", lines[index].Trim());
                else
                    entryLocationsByGuid[entryGuid] = entryLocation;

                if (string.IsNullOrEmpty(resolvedAssetPath))
                {
                    AddResult(groupPath, lineNumber, Severity.Error, $"Addressable entry GUID does not resolve to an asset: {entryGuid}", lines[index].Trim());
                }
                else
                {
                    UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(resolvedAssetPath);
                    if (mainAsset == null)
                    {
                        AddResult(groupPath, lineNumber, Severity.Error, $"Addressable entry asset could not be loaded by AssetDatabase. Guid={entryGuid} Path={resolvedAssetPath}", lines[index].Trim());
                    }
                    else
                    {
                        loadableEntryCount++;
                        string typeName = mainAsset.GetType().Name;
                        if (!loadableEntriesByType.ContainsKey(typeName))
                            loadableEntriesByType[typeName] = 0;

                        loadableEntriesByType[typeName]++;
                    }
                }

                if (string.IsNullOrWhiteSpace(entryAddress))
                {
                    AddResult(groupPath, lineNumber, Severity.Warning, $"Addressable entry has no m_Address field: {entryGuid}", lines[index].Trim());
                    continue;
                }

                string normalizedAddress = NormalizeAssetPath(entryAddress);
                if (!normalizedAddress.StartsWith("Assets/", StringComparison.Ordinal))
                    continue;

                string addressGuid = AssetDatabase.AssetPathToGUID(normalizedAddress);
                if (string.IsNullOrEmpty(addressGuid))
                {
                    AddResult(groupPath, lineNumber, Severity.Error, $"Addressable entry address path is missing: {normalizedAddress}", lines[index].Trim());
                    continue;
                }

                if (!string.Equals(addressGuid, entryGuid, StringComparison.OrdinalIgnoreCase))
                    AddResult(groupPath, lineNumber, Severity.Error, $"Addressable entry GUID does not match address asset GUID. Entry={entryGuid} Address={normalizedAddress} AddressGuid={addressGuid}", lines[index].Trim());
            }
        }

        if (results.Count == 0)
        {
            string typeSummary = string.Join(", ", loadableEntriesByType
                .OrderBy(pair => pair.Key)
                .Select(pair => $"{pair.Key}={pair.Value}"));
            AddResult(string.Empty, 0, Severity.Info, $"Addressables validation passed. Entries={entryCount}; UniqueGuids={entryLocationsByGuid.Count}; LoadableAssets={loadableEntryCount}", typeSummary);
        }

        ValidateAddressablesLinkXml(projectRoot);
    }

    private void ValidateAddressablesLinkXml(string projectRoot)
    {
        string linkXmlPath = Path.Combine(projectRoot, "Assets/AddressableAssetsData/link.xml".Replace('/', Path.DirectorySeparatorChar));
        string linkXmlMetaPath = linkXmlPath + ".meta";
        if (!File.Exists(linkXmlPath))
        {
            if (File.Exists(linkXmlMetaPath))
            {
                AddResult(linkXmlMetaPath, 0, Severity.Warning, "link.xml is absent but link.xml.meta remains.", string.Empty);
                return;
            }

            AddResult(linkXmlPath, 0, Severity.Info, "Addressables linker preserve file is absent; no stale Assembly-CSharp preserve references exist there, but previous preserve intent is not proven.", string.Empty);
            return;
        }

        if (!File.Exists(linkXmlMetaPath))
            AddResult(linkXmlMetaPath, 0, Severity.Error, "link.xml exists without a .meta file; Unity asset GUID preservation is not proven.", string.Empty);

        string[] lines = File.ReadAllLines(linkXmlPath);
        bool hasAssemblyCSharpReference = false;
        for (int index = 0; index < lines.Length; index++)
        {
            if (!lines[index].Contains("Assembly-CSharp"))
                continue;

            hasAssemblyCSharpReference = true;
            AddResult(linkXmlPath, index + 1, Severity.Error, $"link.xml still references Assembly-CSharp: {lines[index].Trim()}", lines[index].Trim());
        }

        if (!hasAssemblyCSharpReference)
            AddResult(linkXmlPath, 0, Severity.Info, "link.xml contains no Assembly-CSharp preserve references.", string.Empty);

        ValidateAddressablesLinkXmlProjectTypeMappings(projectRoot, linkXmlPath);
    }

    private void ValidateAddressablesLinkXmlProjectTypeMappings(string projectRoot, string linkXmlPath)
    {
        string linkXmlText = File.ReadAllText(linkXmlPath);
        Dictionary<string, string> targetTypes = LoadRuntimeTargetTopLevelTypeAssemblyMap(projectRoot);
        HashSet<string> runtimeAssemblyNames = new HashSet<string>(
            new[] { "Core", "Gameplay", "Infrastructure", "Presentation", "UI" },
            StringComparer.Ordinal);
        Dictionary<string, int> entryCountsByAssembly = new Dictionary<string, int>(StringComparer.Ordinal);
        int projectEntryCount = 0;
        int issueCount = 0;

        foreach (Match assemblyMatch in Regex.Matches(
            linkXmlText,
            @"<assembly\s+[^>]*fullname=""([^""]+)""[^>]*>(.*?)</assembly>",
            RegexOptions.Singleline))
        {
            string assemblyName = assemblyMatch.Groups[1].Value.Split(',')[0].Trim();
            string assemblyBody = assemblyMatch.Groups[2].Value;
            foreach (Match typeMatch in Regex.Matches(assemblyBody, @"<type\s+[^>]*fullname=""([^""]+)"""))
            {
                string typeFullName = typeMatch.Groups[1].Value;
                string outerTypeName = typeFullName.Split('/')[0];
                if (targetTypes.TryGetValue(outerTypeName, out string expectedAssembly))
                {
                    if (!string.Equals(assemblyName, expectedAssembly, StringComparison.Ordinal))
                    {
                        issueCount++;
                        AddResult(
                            linkXmlPath,
                            0,
                            Severity.Error,
                            $"Project preserve type is under the wrong assembly block: {typeFullName}. Actual={assemblyName} Expected={expectedAssembly}",
                            typeMatch.Value);
                        continue;
                    }

                    projectEntryCount++;
                    if (!entryCountsByAssembly.ContainsKey(assemblyName))
                        entryCountsByAssembly[assemblyName] = 0;
                    entryCountsByAssembly[assemblyName]++;
                    continue;
                }

                if (runtimeAssemblyNames.Contains(assemblyName))
                {
                    issueCount++;
                    AddResult(
                        linkXmlPath,
                        0,
                        Severity.Error,
                        $"Project assembly preserve entry does not resolve to a current top-level type declaration: {assemblyName} -> {typeFullName}",
                        typeMatch.Value);
                }
            }
        }

        if (issueCount == 0)
        {
            string summary = string.Join(", ", entryCountsByAssembly
                .OrderBy(pair => pair.Key)
                .Select(pair => $"{pair.Key}={pair.Value}"));
            AddResult(
                linkXmlPath,
                0,
                Severity.Info,
                $"link.xml project preserve entries resolve to current runtime target assembly type declarations. Entries={projectEntryCount}; {summary}",
                string.Empty);
        }
    }

    private static Dictionary<string, string> LoadRuntimeTargetTopLevelTypeAssemblyMap(string projectRoot)
    {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, string> assemblyRoots = new Dictionary<string, string>
        {
            { "Core", "Assets/_Project/Runtime/Core" },
            { "Gameplay", "Assets/_Project/Runtime/Features" },
            { "Infrastructure", "Assets/_Project/Runtime/Infrastructure" },
            { "Presentation", "Assets/_Project/Runtime/Presentation" },
            { "UI", "Assets/_Project/Runtime/UI" }
        };

        Dictionary<string, List<TypeDeclarationLocation>> declarationsByName = new Dictionary<string, List<TypeDeclarationLocation>>();
        foreach (KeyValuePair<string, string> entry in assemblyRoots)
        {
            string absoluteRoot = Path.Combine(projectRoot, entry.Value.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(absoluteRoot))
                continue;

            foreach (string sourcePath in Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
                CollectTopLevelTypeDeclarations(entry.Key, sourcePath, declarationsByName);
        }

        foreach (KeyValuePair<string, List<TypeDeclarationLocation>> entry in declarationsByName)
        {
            string[] assemblies = entry.Value
                .Select(value => value.AssemblyName)
                .Distinct()
                .ToArray();
            if (assemblies.Length == 1 && !result.ContainsKey(entry.Key))
                result[entry.Key] = assemblies[0];
        }

        return result;
    }

    private void ValidateAssemblyBoundaries()
    {
        results.Clear();

        Dictionary<string, AsmdefInfo> asmdefsByName = LoadProjectAsmdefs();
        ValidateTargetAssemblies(asmdefsByName);
        ValidateAsmdefNameUniqueness();
        ValidateAssetAsmdefAllowedPaths();
        ValidateTargetSourceRootNestedAssemblyBoundaries();
        ValidateProjectSourceRootOwnership();
        ValidateProjectTestAsmdefPolicy(asmdefsByName);
        ValidateAsmdefPlatformSettings(asmdefsByName);
        ValidateAsmdefReferenceOptionSettings(asmdefsByName);
        ValidateRuntimeAsmdefEditorReferences(asmdefsByName);
        ValidateAsmdefMetaImporters(asmdefsByName);
        ValidateExtraProjectAsmdefs(asmdefsByName);
        ValidateProjectOwnedProductionAssemblySet(asmdefsByName);
        ValidateProjectAssemblyReferences(asmdefsByName);
        ValidateCoreAsmdefHasNoReferences(asmdefsByName);
        ValidateAsmdefReferenceResolution(asmdefsByName);
        ValidateAssetAsmdefReferencePolicy();
        ValidateAsmrefReferenceResolution();
        ValidateAsmdefRequiredExternalReferences(asmdefsByName);
        ValidateProjectAssemblyCycles(asmdefsByName);
        ValidateCSharpSourceBoundaries();
        ValidateAssetSourceAssemblyOwners();
        ValidateProjectNamespaceAssemblySpans();
        ValidateDuplicateTargetTypeDeclarations();
        ValidateKnownForbiddenConcreteDependencies();
        ValidateLowerLayerForbiddenNamespaceReferences();
        ValidateLowerLayerForbiddenPresentationApiReferences();
        ValidateProjectSourceDefaultAssemblyLiterals();
        ValidateRuntimeEditorSourceIsolation();
        ValidateCSharpMetaPairing();
        ValidateAssetMetaGuidUniqueness();

        if (results.Count == 0)
            AddResult(string.Empty, 0, Severity.Info, "Assembly boundary validation passed without findings.", string.Empty);
    }

    private void ValidateUnityCompileOutputs()
    {
        results.Clear();

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string scriptAssembliesPath = Path.Combine(projectRoot, "Library/ScriptAssemblies".Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(scriptAssembliesPath))
        {
            AddResult(string.Empty, 0, Severity.Error, "Library/ScriptAssemblies does not exist. Unity has not produced script assembly outputs for this project.", string.Empty);
            return;
        }

        for (int i = 0; i < TargetAssemblyNames.Length; i++)
        {
            string assemblyName = TargetAssemblyNames[i];
            if (!TargetAssemblySourceRoots.TryGetValue(assemblyName, out string sourceRoot))
            {
                AddResult(string.Empty, 0, Severity.Error, $"Target assembly source root is not registered: {assemblyName}", string.Empty);
                continue;
            }

            string absoluteSourceRoot = Path.Combine(projectRoot, sourceRoot.Replace('/', Path.DirectorySeparatorChar));
            DateTime latestAssemblySourceTime = GetLatestWriteTimeUtc(
                absoluteSourceRoot,
                "*.cs",
                "*.asmdef");

            string outputPath = Path.Combine(scriptAssembliesPath, assemblyName + ".dll");
            if (!File.Exists(outputPath))
            {
                AddResult(string.Empty, 0, Severity.Error, $"Target assembly output is missing from Library/ScriptAssemblies: {assemblyName}.dll", string.Empty);
                continue;
            }

            FileInfo outputInfo = new FileInfo(outputPath);
            Severity severity = outputInfo.LastWriteTimeUtc < latestAssemblySourceTime
                ? Severity.Warning
                : Severity.Info;
            string message = severity == Severity.Warning
                ? $"Target assembly output exists but is older than current assembly source: {assemblyName}.dll"
                : $"Target assembly output exists: {assemblyName}.dll";
            AddResult(outputPath, 0, severity, message, outputInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        ValidateLegacyDefaultAssemblyOutput(scriptAssembliesPath, "Assembly-CSharp.dll");
        ValidateLegacyDefaultAssemblyOutput(scriptAssembliesPath, "Assembly-CSharp-Editor.dll");
        ValidateLegacyDefaultAssemblyOutput(scriptAssembliesPath, "Assembly-CSharp-firstpass.dll");
        ValidateLegacyDefaultAssemblyOutput(scriptAssembliesPath, "Assembly-CSharp-Editor-firstpass.dll");

        if (results.Count == 0)
            AddResult(string.Empty, 0, Severity.Info, "Unity compile output validation passed without findings.", string.Empty);
    }

    private void ValidateGeneratedProjectFiles()
    {
        results.Clear();

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string[] solutionFiles = Directory.GetFiles(projectRoot, "*.sln", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(projectRoot, "*.slnx", SearchOption.TopDirectoryOnly))
            .ToArray();
        if (solutionFiles.Length == 0)
        {
            AddResult(string.Empty, 0, Severity.Error, "Generated solution file is missing. Regenerate Unity project files before solution build verification.", string.Empty);
        }
        else
        {
            for (int i = 0; i < solutionFiles.Length; i++)
            {
                FileInfo solutionInfo = new FileInfo(solutionFiles[i]);
                AddResult(solutionFiles[i], 0, Severity.Info, "Generated solution file exists.", solutionInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
            }

            ValidateGeneratedSolutionContents(solutionFiles);
        }

        Dictionary<string, AsmdefInfo> asmdefsByName = LoadProjectAsmdefs();
        for (int i = 0; i < TargetAssemblyNames.Length; i++)
        {
            string assemblyName = TargetAssemblyNames[i];
            string projectFile = Path.Combine(projectRoot, assemblyName + ".csproj");
            if (!File.Exists(projectFile))
            {
                AddResult(string.Empty, 0, Severity.Error, $"Generated project file is missing for target assembly: {assemblyName}.csproj", string.Empty);
                continue;
            }

            FileInfo projectInfo = new FileInfo(projectFile);
            AddResult(projectFile, 0, Severity.Info, $"Generated project file exists for target assembly: {assemblyName}.csproj", projectInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
            ValidateGeneratedProjectLegacyDefaultReferences(projectFile, assemblyName);
            ValidateGeneratedProjectReferences(projectFile, assemblyName, asmdefsByName);
            if (asmdefsByName.TryGetValue(assemblyName, out AsmdefInfo asmdefInfo))
                ValidateGeneratedProjectCompileItems(projectFile, assemblyName, asmdefInfo);
        }

        ValidateLegacyGeneratedProjectFile(projectRoot, "Assembly-CSharp.csproj");
        ValidateLegacyGeneratedProjectFile(projectRoot, "Assembly-CSharp-Editor.csproj");
        ValidateLegacyGeneratedProjectFile(projectRoot, "Assembly-CSharp-firstpass.csproj");
        ValidateLegacyGeneratedProjectFile(projectRoot, "Assembly-CSharp-Editor-firstpass.csproj");
    }

    private void ValidateLegacyGeneratedProjectFile(string projectRoot, string projectFileName)
    {
        string projectFile = Path.Combine(projectRoot, projectFileName);
        if (!File.Exists(projectFile))
        {
            AddResult(string.Empty, 0, Severity.Info, $"Legacy generated project file is absent: {projectFileName}", string.Empty);
            return;
        }

        FileInfo projectInfo = new FileInfo(projectFile);
        AddResult(projectFile, 0, Severity.Warning, $"Legacy generated project file still exists: {projectFileName}", projectInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    private void ValidateGeneratedProjectLegacyDefaultReferences(string projectFile, string assemblyName)
    {
        string[] lines = File.ReadAllLines(projectFile);
        int issueCount = 0;
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (!line.Contains("Assembly-CSharp", StringComparison.Ordinal))
                continue;

            issueCount++;
            AddResult(projectFile, index + 1, Severity.Error, $"Generated target project still contains a legacy default assembly reference: {assemblyName}", line.Trim());
        }

        if (issueCount == 0)
            AddResult(projectFile, 0, Severity.Info, $"Generated project file contains no legacy default assembly references: {assemblyName}", string.Empty);
    }

    private void ValidateGeneratedProjectReferences(string projectFile, string assemblyName, Dictionary<string, AsmdefInfo> asmdefsByName)
    {
        if (!asmdefsByName.TryGetValue(assemblyName, out AsmdefInfo asmdefInfo))
            return;

        string projectText = File.ReadAllText(projectFile);
        foreach (string reference in asmdefInfo.References)
        {
            if (string.IsNullOrWhiteSpace(reference))
                continue;

            string escapedReference = Regex.Escape(reference);
            bool hasAssemblyReference = Regex.IsMatch(projectText, $"<Reference\\s+Include=\"{escapedReference}(\"|,)");
            bool hasProjectReference = Regex.IsMatch(projectText, $"<ProjectReference\\s+Include=\"{escapedReference}\\.csproj\"");
            if (hasAssemblyReference || hasProjectReference)
                continue;

            AddResult(projectFile, 0, Severity.Error, $"Generated project file is stale; asmdef reference is missing from csproj: {assemblyName} -> {reference}", string.Empty);
        }
    }

    private void ValidateGeneratedSolutionContents(string[] solutionFiles)
    {
        int issueCount = 0;
        for (int i = 0; i < solutionFiles.Length; i++)
        {
            string solutionFile = solutionFiles[i];
            string solutionText = File.ReadAllText(solutionFile);
            for (int assemblyIndex = 0; assemblyIndex < TargetAssemblyNames.Length; assemblyIndex++)
            {
                string projectFileName = TargetAssemblyNames[assemblyIndex] + ".csproj";
                if (solutionText.Contains(projectFileName, StringComparison.Ordinal))
                    continue;

                issueCount++;
                AddResult(solutionFile, 0, Severity.Error, $"Generated solution does not include target project: {projectFileName}", string.Empty);
            }

            string[] legacyProjectNames =
            {
                "Assembly-CSharp.csproj",
                "Assembly-CSharp-Editor.csproj",
                "Assembly-CSharp-firstpass.csproj",
                "Assembly-CSharp-Editor-firstpass.csproj"
            };

            for (int legacyIndex = 0; legacyIndex < legacyProjectNames.Length; legacyIndex++)
            {
                string legacyProjectName = legacyProjectNames[legacyIndex];
                if (!solutionText.Contains(legacyProjectName, StringComparison.Ordinal))
                    continue;

                issueCount++;
                AddResult(solutionFile, 0, Severity.Error, $"Generated solution still includes legacy default project: {legacyProjectName}", string.Empty);
            }
        }

        if (issueCount == 0)
            AddResult(string.Empty, 0, Severity.Info, $"Generated solution contents include all target project files and no legacy Assembly-CSharp project files. Solutions={solutionFiles.Length}", string.Empty);
    }

    private void ValidateGeneratedProjectCompileItems(string projectFile, string assemblyName, AsmdefInfo asmdefInfo)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string asmdefPath = Path.Combine(projectRoot, asmdefInfo.Path.Replace('/', Path.DirectorySeparatorChar));
        string asmdefDirectory = Path.GetDirectoryName(asmdefPath);
        if (string.IsNullOrWhiteSpace(asmdefDirectory) || !Directory.Exists(asmdefDirectory))
            return;

        string projectText = File.ReadAllText(projectFile);
        HashSet<string> existingCompilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string> unexpectedCompileItems = new List<string>();
        foreach (Match match in Regex.Matches(projectText, @"<Compile\s+Include=""([^""]+)"""))
        {
            string includePath = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(includePath))
                continue;

            string absoluteIncludePath = ToAbsoluteProjectPath(projectRoot, includePath);
            existingCompilePaths.Add(absoluteIncludePath);

            bool isUnderAsmdef = IsPathUnderDirectory(absoluteIncludePath, asmdefDirectory);
            bool isUnderNestedAssembly = isUnderAsmdef && IsUnderNestedAssemblyBoundary(absoluteIncludePath, asmdefDirectory);
            if (!isUnderAsmdef || isUnderNestedAssembly)
                unexpectedCompileItems.Add(ToProjectRelativePath(absoluteIncludePath));
        }

        List<string> missingSources = new List<string>();
        foreach (string sourcePath in Directory.GetFiles(asmdefDirectory, "*.cs", SearchOption.AllDirectories))
        {
            if (IsUnderNestedAssemblyBoundary(sourcePath, asmdefDirectory))
                continue;

            string fullSourcePath = Path.GetFullPath(sourcePath);
            if (existingCompilePaths.Contains(fullSourcePath))
                continue;

            missingSources.Add(ToProjectRelativePath(fullSourcePath));
        }

        if (missingSources.Count == 0)
        {
            AddResult(projectFile, 0, Severity.Info, $"Generated project file includes all current asmdef source Compile items: {assemblyName}", string.Empty);
        }
        else
        {
            AddResult(
                projectFile,
                0,
                Severity.Error,
                $"Generated project file is stale; Compile items are missing for current asmdef sources: Assembly={assemblyName}; Count={missingSources.Count}",
                string.Join(", ", missingSources.OrderBy(path => path).Take(12)));
        }

        if (unexpectedCompileItems.Count == 0)
        {
            AddResult(projectFile, 0, Severity.Info, $"Generated project file contains no Compile items outside the current asmdef source boundary: {assemblyName}", string.Empty);
        }
        else
        {
            AddResult(
                projectFile,
                0,
                Severity.Error,
                $"Generated project file includes Compile items outside the current asmdef source boundary: Assembly={assemblyName}; Count={unexpectedCompileItems.Count}",
                string.Join(", ", unexpectedCompileItems.OrderBy(path => path).Take(12)));
        }
    }

    private static string ToAbsoluteProjectPath(string projectRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        return Path.GetFullPath(Path.Combine(projectRoot, path));
    }

    private static bool IsPathUnderDirectory(string path, string directory)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directory))
            return false;

        string fullPath = Path.GetFullPath(path).TrimEnd('\\', '/');
        string fullDirectory = Path.GetFullPath(directory).TrimEnd('\\', '/');
        return string.Equals(fullPath, fullDirectory, StringComparison.OrdinalIgnoreCase) ||
            fullPath.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnderNestedAssemblyBoundary(string sourcePath, string asmdefDirectory)
    {
        string root = Path.GetFullPath(asmdefDirectory).TrimEnd('\\', '/');
        DirectoryInfo directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath));
        while (directory != null)
        {
            string current = directory.FullName.TrimEnd('\\', '/');
            if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase))
                return false;

            if (current.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(current) &&
                (Directory.GetFiles(current, "*.asmdef").Length > 0 ||
                 Directory.GetFiles(current, "*.asmref").Length > 0))
            {
                return true;
            }

            directory = directory.Parent;
        }

        return false;
    }

    private static DateTime GetLatestWriteTimeUtc(string rootPath, params string[] searchPatterns)
    {
        DateTime latest = DateTime.MinValue;
        if (!Directory.Exists(rootPath))
            return latest;

        for (int i = 0; i < searchPatterns.Length; i++)
        {
            foreach (string path in Directory.GetFiles(rootPath, searchPatterns[i], SearchOption.AllDirectories))
            {
                DateTime writeTime = File.GetLastWriteTimeUtc(path);
                if (writeTime > latest)
                    latest = writeTime;
            }
        }

        return latest;
    }

    private void ValidateLegacyDefaultAssemblyOutput(string scriptAssembliesPath, string assemblyFileName)
    {
        string outputPath = Path.Combine(scriptAssembliesPath, assemblyFileName);
        if (!File.Exists(outputPath))
        {
            AddResult(string.Empty, 0, Severity.Info, $"Legacy default assembly output is absent: {assemblyFileName}", string.Empty);
            return;
        }

        FileInfo outputInfo = new FileInfo(outputPath);
        AddResult(outputPath, 0, Severity.Warning, $"Legacy default assembly output still exists: {assemblyFileName}. Confirm it is stale or remove remaining default-assembly source.", outputInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    private static Dictionary<string, AsmdefInfo> LoadProjectAsmdefs()
    {
        Dictionary<string, AsmdefInfo> asmdefsByName = new Dictionary<string, AsmdefInfo>();
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string projectAsmdefRoot = Path.Combine(projectRoot, "Assets/_Project".Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(projectAsmdefRoot))
            return asmdefsByName;

        foreach (string path in Directory.GetFiles(projectAsmdefRoot, "*.asmdef", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            Match nameMatch = AsmdefNameRegex.Match(text);
            if (!nameMatch.Success)
                continue;

            string name = nameMatch.Groups[1].Value;
            asmdefsByName[name] = new AsmdefInfo
            {
                Name = name,
                Path = ToProjectRelativePath(path),
                RootNamespace = ParseAsmdefStringProperty(text, "rootNamespace"),
                References = ParseAsmdefStringArray(text, "references")
                    .Select(ResolveAsmdefReferenceName)
                    .ToArray(),
                IncludePlatforms = ParseAsmdefStringArray(text, "includePlatforms"),
                ExcludePlatforms = ParseAsmdefStringArray(text, "excludePlatforms"),
                OptionalUnityReferences = ParseAsmdefStringArray(text, "optionalUnityReferences"),
                PrecompiledReferences = ParseAsmdefStringArray(text, "precompiledReferences"),
                DefineConstraints = ParseAsmdefStringArray(text, "defineConstraints"),
                VersionDefines = ParseAsmdefStringArray(text, "versionDefines"),
                OverrideReferences = ParseAsmdefBool(text, "overrideReferences"),
                AllowUnsafeCode = ParseAsmdefBool(text, "allowUnsafeCode"),
                AutoReferenced = ParseAsmdefBool(text, "autoReferenced", true),
                NoEngineReferences = ParseAsmdefBool(text, "noEngineReferences")
            };
        }

        return asmdefsByName;
    }

    private static Dictionary<string, AsmdefInfo> LoadAssetAsmdefs()
    {
        Dictionary<string, AsmdefInfo> asmdefsByName = new Dictionary<string, AsmdefInfo>();
        string assetsRoot = Application.dataPath;
        if (!Directory.Exists(assetsRoot))
            return asmdefsByName;

        foreach (string path in Directory.GetFiles(assetsRoot, "*.asmdef", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            Match nameMatch = AsmdefNameRegex.Match(text);
            if (!nameMatch.Success)
                continue;

            string name = nameMatch.Groups[1].Value;
            asmdefsByName[name] = new AsmdefInfo
            {
                Name = name,
                Path = ToProjectRelativePath(path),
                RootNamespace = ParseAsmdefStringProperty(text, "rootNamespace"),
                References = ParseAsmdefStringArray(text, "references")
                    .Select(ResolveAsmdefReferenceName)
                    .ToArray(),
                IncludePlatforms = ParseAsmdefStringArray(text, "includePlatforms"),
                ExcludePlatforms = ParseAsmdefStringArray(text, "excludePlatforms"),
                OptionalUnityReferences = ParseAsmdefStringArray(text, "optionalUnityReferences"),
                PrecompiledReferences = ParseAsmdefStringArray(text, "precompiledReferences"),
                DefineConstraints = ParseAsmdefStringArray(text, "defineConstraints"),
                VersionDefines = ParseAsmdefStringArray(text, "versionDefines"),
                OverrideReferences = ParseAsmdefBool(text, "overrideReferences"),
                AllowUnsafeCode = ParseAsmdefBool(text, "allowUnsafeCode"),
                AutoReferenced = ParseAsmdefBool(text, "autoReferenced", true),
                NoEngineReferences = ParseAsmdefBool(text, "noEngineReferences")
            };
        }

        return asmdefsByName;
    }

    private static string ResolveAsmdefReferenceName(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return reference;

        Match guidMatch = Regex.Match(reference, @"^GUID:([0-9a-f]{32})$");
        if (!guidMatch.Success)
            return reference;

        Dictionary<string, string> guidNames = LoadKnownAsmdefGuidNames();
        return guidNames.TryGetValue(guidMatch.Groups[1].Value, out string name)
            ? name
            : reference;
    }

    private static bool ParseAsmdefBool(string text, string propertyName)
    {
        return ParseAsmdefBool(text, propertyName, false);
    }

    private static bool ParseAsmdefBool(string text, string propertyName, bool defaultValue)
    {
        Regex boolRegex = new Regex(
            $@"""{Regex.Escape(propertyName)}""\s*:\s*(?<value>true|false)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Match match = boolRegex.Match(text);
        if (!match.Success)
            return defaultValue;

        return
            string.Equals(match.Groups["value"].Value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string ParseAsmdefStringProperty(string text, string propertyName)
    {
        Regex stringRegex = new Regex(
            $@"""{Regex.Escape(propertyName)}""\s*:\s*""(?<value>[^""]*)""",
            RegexOptions.Compiled);
        Match match = stringRegex.Match(text);
        return match.Success
            ? match.Groups["value"].Value
            : string.Empty;
    }

    private static string[] ParseAsmdefStringArray(string text, string propertyName)
    {
        Regex arrayRegex = new Regex(
            $@"""{Regex.Escape(propertyName)}""\s*:\s*\[(?<values>.*?)\]",
            RegexOptions.Compiled | RegexOptions.Singleline);
        Match referencesMatch = arrayRegex.Match(text);
        if (!referencesMatch.Success)
            return Array.Empty<string>();

        List<string> values = new List<string>();
        foreach (Match match in QuotedStringRegex.Matches(referencesMatch.Groups["values"].Value))
            values.Add(match.Groups[1].Value);

        return values.ToArray();
    }

    private void ValidateTargetAssemblies(Dictionary<string, AsmdefInfo> asmdefsByName)
    {
        for (int i = 0; i < TargetAssemblyNames.Length; i++)
        {
            string assemblyName = TargetAssemblyNames[i];
            if (!asmdefsByName.TryGetValue(assemblyName, out AsmdefInfo info))
            {
                AddResult(string.Empty, 0, Severity.Error, $"Target project assembly is missing: {assemblyName}", string.Empty);
                continue;
            }

            if (ExpectedTargetAsmdefPaths.TryGetValue(assemblyName, out string expectedPath) &&
                !string.Equals(NormalizeProjectPath(info.Path), expectedPath, StringComparison.Ordinal))
            {
                AddResult(info.Path, 0, Severity.Error, $"Target assembly asmdef is in the wrong path: {assemblyName}. Expected={expectedPath}", string.Empty);
            }
            else
            {
                AddResult(info.Path, 0, Severity.Info, $"Target assembly asmdef path is valid: {assemblyName}", string.Empty);
            }

            AddResult(info.Path, 0, Severity.Info, $"Target project assembly found: {assemblyName}", string.Join(", ", info.References));
        }
    }

    private void ValidateAsmdefNameUniqueness()
    {
        Dictionary<string, List<string>> pathsByName = new Dictionary<string, List<string>>();
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string[] roots =
        {
            "Assets",
            "Packages",
            "Library/PackageCache"
        };

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            string root = Path.Combine(projectRoot, roots[rootIndex].Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root))
                continue;

            foreach (string path in Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(path);
                Match nameMatch = AsmdefNameRegex.Match(text);
                if (!nameMatch.Success || string.IsNullOrWhiteSpace(nameMatch.Groups[1].Value))
                {
                    AddResult(path, 0, Severity.Error, "Asmdef has no assembly name.", string.Empty);
                    continue;
                }

                string name = nameMatch.Groups[1].Value;
                if (!pathsByName.TryGetValue(name, out List<string> paths))
                {
                    paths = new List<string>();
                    pathsByName[name] = paths;
                }

                paths.Add(ToProjectRelativePath(path));
            }
        }

        int duplicateCount = 0;
        foreach (KeyValuePair<string, List<string>> entry in pathsByName.OrderBy(pair => pair.Key))
        {
            if (entry.Value.Count <= 1)
                continue;

            duplicateCount++;
            AddResult(entry.Value[0], 0, Severity.Error, $"Duplicate asmdef assembly name found: {entry.Key}", string.Join(", ", entry.Value));
        }

        if (duplicateCount == 0)
            AddResult(string.Empty, 0, Severity.Info, "All asmdef assembly names are unique across Assets, Packages, and Library/PackageCache.", string.Empty);
    }

    private void ValidateAssetAsmdefAllowedPaths()
    {
        Dictionary<string, string> expectedPaths = new Dictionary<string, string>(ExpectedTargetAsmdefPaths, StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in ExpectedSupportAsmdefPaths)
            expectedPaths[entry.Key] = entry.Value;

        string assetsRoot = Application.dataPath;
        if (!Directory.Exists(assetsRoot))
        {
            AddResult(assetsRoot, 0, Severity.Error, "Assets folder is missing.", string.Empty);
            return;
        }

        int issueCount = 0;
        int asmdefCount = 0;
        foreach (string path in Directory.GetFiles(assetsRoot, "*.asmdef", SearchOption.AllDirectories))
        {
            asmdefCount++;
            string text = File.ReadAllText(path);
            Match nameMatch = AsmdefNameRegex.Match(text);
            if (!nameMatch.Success || string.IsNullOrWhiteSpace(nameMatch.Groups[1].Value))
            {
                issueCount++;
                AddResult(path, 0, Severity.Error, "Asmdef has no assembly name.", string.Empty);
                continue;
            }

            string assemblyName = nameMatch.Groups[1].Value;
            string relativePath = NormalizeProjectPath(ToProjectRelativePath(path));
            if (!expectedPaths.TryGetValue(assemblyName, out string expectedPath))
            {
                issueCount++;
                AddResult(path, 0, Severity.Error, $"Assets asmdef is not an approved target/test/support assembly: {assemblyName}", string.Empty);
                continue;
            }

            if (!string.Equals(relativePath, expectedPath, StringComparison.Ordinal))
            {
                issueCount++;
                AddResult(path, 0, Severity.Error, $"Approved asmdef is in the wrong path: {assemblyName}. Expected={expectedPath}", string.Empty);
            }
        }

        if (issueCount == 0)
            AddResult(string.Empty, 0, Severity.Info, $"All Assets asmdefs are approved target/test/support assemblies in expected paths. Count={asmdefCount}", string.Empty);
    }

    private void ValidateTargetSourceRootNestedAssemblyBoundaries()
    {
        int issueCount = 0;
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        for (int i = 0; i < TargetAssemblyNames.Length; i++)
        {
            string assemblyName = TargetAssemblyNames[i];
            string sourceRoot = TargetAssemblySourceRoots[assemblyName];
            string sourceRootPath = Path.GetFullPath(Path.Combine(projectRoot, sourceRoot.Replace('/', Path.DirectorySeparatorChar)));
            if (!Directory.Exists(sourceRootPath))
            {
                issueCount++;
                AddResult(sourceRoot, 0, Severity.Error, $"Target assembly source root is missing: {assemblyName}", string.Empty);
                continue;
            }

            string expectedAsmdefPath = Path.GetFullPath(Path.Combine(
                projectRoot,
                ExpectedTargetAsmdefPaths[assemblyName].Replace('/', Path.DirectorySeparatorChar)));

            IEnumerable<string> boundaryFiles = Directory.GetFiles(sourceRootPath, "*.asmdef", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(sourceRootPath, "*.asmref", SearchOption.AllDirectories));
            foreach (string boundaryFile in boundaryFiles)
            {
                string boundaryFullPath = Path.GetFullPath(boundaryFile);
                if (string.Equals(boundaryFullPath, expectedAsmdefPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                issueCount++;
                AddResult(
                    boundaryFile,
                    0,
                    Severity.Error,
                    $"Nested asmdef/asmref found under target source root: {assemblyName}",
                    string.Empty);
            }
        }

        if (issueCount == 0)
            AddResult(string.Empty, 0, Severity.Info, "No nested asmdef or asmref files were found inside the six target source roots.", string.Empty);
    }

    private void ValidateProjectSourceRootOwnership()
    {
        int issueCount = 0;
        int productionSourceCount = 0;
        int testSourceCount = 0;
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string projectSourceRoot = Path.Combine(projectRoot, "Assets/_Project".Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(projectSourceRoot))
        {
            AddResult(projectSourceRoot, 0, Severity.Error, "Project source root is missing.", string.Empty);
            return;
        }

        string[] targetRoots = TargetAssemblySourceRoots.Values
            .Select(root => Path.GetFullPath(Path.Combine(projectRoot, root.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();
        string[] testRoots = TestOnlyProjectSourceRoots
            .Select(root => Path.GetFullPath(Path.Combine(projectRoot, root.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();

        foreach (string sourcePath in Directory.GetFiles(projectSourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            string fullSourcePath = Path.GetFullPath(sourcePath);
            if (testRoots.Any(root => IsPathUnderDirectory(fullSourcePath, root)))
            {
                testSourceCount++;
                continue;
            }

            productionSourceCount++;
            if (targetRoots.Any(root => IsPathUnderDirectory(fullSourcePath, root)))
                continue;

            issueCount++;
            AddResult(sourcePath, 0, Severity.Error, "Production C# source under Assets/_Project is outside the six target source roots.", string.Empty);
        }

        if (issueCount == 0)
        {
            AddResult(
                string.Empty,
                0,
                Severity.Info,
                $"All production C# source under Assets/_Project is inside the six target source roots. Sources={productionSourceCount}; TestSources={testSourceCount}",
                string.Empty);
        }
    }

    private void ValidateProjectTestAsmdefPolicy(Dictionary<string, AsmdefInfo> asmdefsByName)
    {
        int issueCount = 0;
        if (!asmdefsByName.TryGetValue("PlayModeTests", out AsmdefInfo playModeTests))
        {
            AddResult(string.Empty, 0, Severity.Info, "No PlayModeTests asmdef is present.", string.Empty);
            return;
        }

        string expectedPath = ExpectedSupportAsmdefPaths["PlayModeTests"];
        if (!string.Equals(NormalizeProjectPath(playModeTests.Path), expectedPath, StringComparison.Ordinal))
        {
            issueCount++;
            AddResult(playModeTests.Path, 0, Severity.Error, $"PlayModeTests asmdef is outside its expected test path. Expected={expectedPath}", string.Empty);
        }

        if (playModeTests.OptionalUnityReferences.Length != 1 ||
            !string.Equals(playModeTests.OptionalUnityReferences[0], "TestAssemblies", StringComparison.Ordinal))
        {
            issueCount++;
            AddResult(playModeTests.Path, 0, Severity.Error, "PlayModeTests asmdef must declare optionalUnityReferences exactly as TestAssemblies.", string.Join(", ", playModeTests.OptionalUnityReferences));
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string testsRoot = Path.Combine(projectRoot, "Assets/_Project/Tests".Replace('/', Path.DirectorySeparatorChar));
        string playModeRoot = Path.Combine(projectRoot, "Assets/_Project/Tests/PlayMode".Replace('/', Path.DirectorySeparatorChar));
        int sourceCount = 0;
        if (Directory.Exists(testsRoot))
        {
            foreach (string sourcePath in Directory.GetFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
            {
                sourceCount++;
                if (IsPathUnderDirectory(sourcePath, playModeRoot))
                    continue;

                issueCount++;
                AddResult(sourcePath, 0, Severity.Error, "Project test C# source is outside the PlayModeTests asmdef boundary.", string.Empty);
            }
        }

        if (issueCount == 0)
            AddResult(string.Empty, 0, Severity.Info, $"Project test asmdef policy is valid: PlayModeTests is test-marked and owns all project test C# source. Sources={sourceCount}", string.Empty);
    }

    private void ValidateAsmdefPlatformSettings(Dictionary<string, AsmdefInfo> asmdefsByName)
    {
        int issueCount = 0;
        for (int i = 0; i < RuntimeAssemblyNames.Length; i++)
        {
            string assemblyName = RuntimeAssemblyNames[i];
            if (!asmdefsByName.TryGetValue(assemblyName, out AsmdefInfo info))
                continue;

            if (info.IncludePlatforms.Length > 0)
            {
                issueCount++;
                AddResult(info.Path, 0, Severity.Error, $"Runtime target assembly must not restrict includePlatforms: {assemblyName}", string.Join(", ", info.IncludePlatforms));
            }

            if (info.ExcludePlatforms.Length > 0)
            {
                issueCount++;
                AddResult(info.Path, 0, Severity.Error, $"Runtime target assembly must not restrict excludePlatforms: {assemblyName}", string.Join(", ", info.ExcludePlatforms));
            }
        }

        if (asmdefsByName.TryGetValue("Editor", out AsmdefInfo editorInfo))
        {
            if (editorInfo.IncludePlatforms.Length != 1 ||
                !string.Equals(editorInfo.IncludePlatforms[0], "Editor", StringComparison.Ordinal))
            {
                issueCount++;
                AddResult(editorInfo.Path, 0, Severity.Error, "Editor target assembly must include only the Editor platform.", string.Join(", ", editorInfo.IncludePlatforms));
            }

            if (editorInfo.ExcludePlatforms.Length > 0)
            {
                issueCount++;
                AddResult(editorInfo.Path, 0, Severity.Error, "Editor target assembly should not also set excludePlatforms.", string.Join(", ", editorInfo.ExcludePlatforms));
            }
        }

        if (issueCount == 0)
            AddResult(string.Empty, 0, Severity.Info, "Target asmdef platform settings are valid: runtime assemblies are unrestricted and Editor is Editor-only.", string.Empty);
    }

    private void ValidateAsmdefReferenceOptionSettings(Dictionary<string, AsmdefInfo> asmdefsByName)
    {
        int issueCount = 0;
        for (int i = 0; i < TargetAssemblyNames.Length; i++)
        {
            string assemblyName = TargetAssemblyNames[i];
            if (!asmdefsByName.TryGetValue(assemblyName, out AsmdefInfo info))
                continue;

            if (!string.IsNullOrWhiteSpace(info.RootNamespace))
            {
                issueCount++;
                AddResult(info.Path, 0, Severity.Error, $"Target assembly rootNamespace must stay empty during the split: {assemblyName}", info.RootNamespace);
            }

            if (info.OverrideReferences)
            {
                issueCount++;
                AddResult(info.Path, 0, Severity.Error, $"Target assembly must not override automatic references during the split: {assemblyName}", string.Empty);
            }

            if (info.PrecompiledReferences.Length > 0)
            {
                issueCount++;
                AddResult(info.Path, 0, Severity.Error, $"Target assembly must not directly pin precompiledReferences during the split: {assemblyName}", string.Join(", ", info.PrecompiledReferences));
            }

            if (info.NoEngineReferences)
            {
                issueCount++;
                AddResult(info.Path, 0, Severity.Error, $"Target assembly must keep Unity engine references enabled: {assemblyName}", string.Empty);
            }

            if (info.AllowUnsafeCode)
            {
                issueCount++;
                AddResult(info.Path, 0, Severity.Error, $"Target assembly must not enable allowUnsafeCode during the split: {assemblyName}", string.Empty);
            }

            if (!info.AutoReferenced)
            {
                issueCount++;
                AddResult(info.Path, 0, Severity.Error, $"Target assembly must stay autoReferenced during the split: {assemblyName}", string.Empty);
            }

            if (info.DefineConstraints.Length > 0)
            {
                issueCount++;
                AddResult(info.Path, 0, Severity.Error, $"Target assembly must not be hidden behind defineConstraints during the split: {assemblyName}", string.Join(", ", info.DefineConstraints));
            }

            if (info.VersionDefines.Length > 0)
            {
                issueCount++;
                AddResult(info.Path, 0, Severity.Error, $"Target assembly must not be hidden behind versionDefines during the split: {assemblyName}", string.Join(", ", info.VersionDefines));
            }
        }

        if (issueCount == 0)
            AddResult(string.Empty, 0, Severity.Info, "Target asmdef reference options are valid: empty rootNamespace, no overrideReferences, direct precompiledReferences, noEngineReferences, allowUnsafeCode, defineConstraints, or versionDefines, and autoReferenced stays enabled.", string.Empty);
    }

    private void ValidateRuntimeAsmdefEditorReferences(Dictionary<string, AsmdefInfo> asmdefsByName)
    {
        int issueCount = 0;
        for (int i = 0; i < RuntimeAssemblyNames.Length; i++)
        {
            string assemblyName = RuntimeAssemblyNames[i];
            if (!asmdefsByName.TryGetValue(assemblyName, out AsmdefInfo info))
                continue;

            for (int referenceIndex = 0; referenceIndex < info.References.Length; referenceIndex++)
            {
                string reference = info.References[referenceIndex];
                if (string.IsNullOrWhiteSpace(reference) ||
                    !reference.Contains("Editor", StringComparison.Ordinal))
                {
                    continue;
                }

                issueCount++;
                AddResult(info.Path, 0, Severity.Error, $"Runtime target assembly references an Editor-only assembly: {assemblyName} -> {reference}", string.Join(", ", info.References));
            }
        }

        if (issueCount == 0)
            AddResult(string.Empty, 0, Severity.Info, "Runtime target asmdefs do not reference Editor-only assemblies.", string.Empty);
    }

    private void ValidateExtraProjectAsmdefs(Dictionary<string, AsmdefInfo> asmdefsByName)
    {
        HashSet<string> targetNames = new HashSet<string>(TargetAssemblyNames);
        foreach (AsmdefInfo info in asmdefsByName.Values.OrderBy(value => value.Name))
        {
            if (targetNames.Contains(info.Name))
                continue;

            bool isTestAssembly = string.Equals(info.Name, "PlayModeTests", StringComparison.Ordinal);
            Severity severity = isTestAssembly ? Severity.Info : Severity.Error;
            string message = isTestAssembly
                ? "Test-only asmdef exists outside the six production project assemblies by Unity Test Runner design."
                : "Unexpected project asmdef exists outside the six target assemblies.";
            AddResult(info.Path, 0, severity, $"{message} Assembly={info.Name}", string.Join(", ", info.References));
        }
    }

    private void ValidateProjectOwnedProductionAssemblySet(Dictionary<string, AsmdefInfo> asmdefsByName)
    {
        int missingTargetCount = 0;
        int wrongTargetPathCount = 0;
        HashSet<string> targetNames = new HashSet<string>(TargetAssemblyNames, StringComparer.Ordinal);

        for (int i = 0; i < TargetAssemblyNames.Length; i++)
        {
            string assemblyName = TargetAssemblyNames[i];
            if (!asmdefsByName.TryGetValue(assemblyName, out AsmdefInfo info))
            {
                missingTargetCount++;
                continue;
            }

            if (ExpectedTargetAsmdefPaths.TryGetValue(assemblyName, out string expectedPath) &&
                !string.Equals(NormalizeProjectPath(info.Path), expectedPath, StringComparison.Ordinal))
            {
                wrongTargetPathCount++;
            }
        }

        int unexpectedProjectAsmdefCount = 0;
        int testExceptionCount = 0;
        foreach (AsmdefInfo info in asmdefsByName.Values)
        {
            if (targetNames.Contains(info.Name))
                continue;

            if (string.Equals(info.Name, "PlayModeTests", StringComparison.Ordinal))
                testExceptionCount++;
            else
                unexpectedProjectAsmdefCount++;
        }

        if (missingTargetCount == 0 &&
            wrongTargetPathCount == 0 &&
            unexpectedProjectAsmdefCount == 0)
        {
            AddResult(
                string.Empty,
                0,
                Severity.Info,
                $"Project-owned production asmdef set is exactly the six target assemblies. Production={string.Join(", ", TargetAssemblyNames)}; TestExceptions=PlayModeTests:{testExceptionCount}; ProjectAsmdefs={asmdefsByName.Count}",
                string.Empty);
        }
    }

    private void ValidateAsmdefMetaImporters(Dictionary<string, AsmdefInfo> asmdefsByName)
    {
        int issueCount = 0;
        int asmdefCount = 0;
        string assetsRoot = Application.dataPath;
        if (!Directory.Exists(assetsRoot))
            return;

        foreach (string asmdefPath in Directory.GetFiles(assetsRoot, "*.asmdef", SearchOption.AllDirectories).OrderBy(path => path))
        {
            asmdefCount++;
            string metaPath = asmdefPath + ".meta";
            if (!File.Exists(metaPath))
            {
                issueCount++;
                AddResult(asmdefPath, 0, Severity.Error, "Asmdef meta file is missing; Unity cannot preserve the asmdef asset GUID.", string.Empty);
                continue;
            }

            string metaText = File.ReadAllText(metaPath);
            if (metaText.Contains("AssemblyDefinitionImporter:", StringComparison.Ordinal))
                continue;

            issueCount++;
            AddResult(metaPath, 0, Severity.Error, "Asmdef meta file is missing AssemblyDefinitionImporter metadata.", string.Empty);
        }

        if (issueCount == 0)
            AddResult(string.Empty, 0, Severity.Info, $"All Assets asmdef meta files are present and contain AssemblyDefinitionImporter metadata. Count={asmdefCount}", string.Empty);
    }

    private void ValidateProjectAssemblyReferences(Dictionary<string, AsmdefInfo> asmdefsByName)
    {
        HashSet<string> projectNames = new HashSet<string>(TargetAssemblyNames);
        int issueCount = 0;
        foreach (string assemblyName in TargetAssemblyNames)
        {
            if (!asmdefsByName.TryGetValue(assemblyName, out AsmdefInfo info))
                continue;

            HashSet<string> allowedReferences = new HashSet<string>(AllowedProjectAssemblyReferences[assemblyName]);
            foreach (string reference in info.References)
            {
                if (!projectNames.Contains(reference))
                    continue;

                if (allowedReferences.Contains(reference))
                    continue;

                issueCount++;
                AddResult(info.Path, 0, Severity.Error, $"Invalid project assembly reference: {assemblyName} -> {reference}", string.Join(", ", info.References));
            }
        }

        if (issueCount == 0)
            AddResult(string.Empty, 0, Severity.Info, "Project target assembly references follow the allowed lower-layer dependency directions.", string.Empty);
    }

    private void ValidateCoreAsmdefHasNoReferences(Dictionary<string, AsmdefInfo> asmdefsByName)
    {
        if (!asmdefsByName.TryGetValue("Core", out AsmdefInfo coreInfo))
            return;

        if (coreInfo.References.Length == 0)
        {
            AddResult(coreInfo.Path, 0, Severity.Info, "Core target asmdef declares zero assembly references.", string.Empty);
            return;
        }

        AddResult(coreInfo.Path, 0, Severity.Error, "Core target asmdef must not reference any assembly.", string.Join(", ", coreInfo.References));
    }

    private void ValidateAsmdefReferenceResolution(Dictionary<string, AsmdefInfo> asmdefsByName)
    {
        HashSet<string> knownAssemblyNames = LoadKnownAsmdefNames();
        int missingCount = 0;

        foreach (AsmdefInfo info in asmdefsByName.Values.OrderBy(value => value.Name))
        {
            for (int i = 0; i < info.References.Length; i++)
            {
                string reference = info.References[i];
                if (string.IsNullOrWhiteSpace(reference))
                    continue;

                if (knownAssemblyNames.Contains(reference))
                    continue;

                missingCount++;
                AddResult(info.Path, 0, Severity.Error, $"Asmdef reference does not resolve to any known asmdef in Assets, Packages, or Library/PackageCache: {reference}", string.Join(", ", info.References));
            }
        }

        if (missingCount == 0)
            AddResult(string.Empty, 0, Severity.Info, "All project asmdef references resolve to known asmdef names or GUIDs.", string.Empty);
    }

    private void ValidateAssetAsmdefReferencePolicy()
    {
        Dictionary<string, AsmdefInfo> asmdefsByName = LoadAssetAsmdefs();
        HashSet<string> knownAssemblyNames = LoadKnownAsmdefNames();
        HashSet<string> targetAssemblyNames = new HashSet<string>(TargetAssemblyNames, StringComparer.Ordinal);
        HashSet<string> supportAssemblyNames = new HashSet<string>(ExpectedSupportAsmdefPaths.Keys, StringComparer.Ordinal);
        int issueCount = 0;

        foreach (AsmdefInfo info in asmdefsByName.Values.OrderBy(value => value.Name))
        {
            for (int i = 0; i < info.References.Length; i++)
            {
                string reference = info.References[i];
                if (string.IsNullOrWhiteSpace(reference))
                    continue;

                if (string.Equals(reference, "Assembly-CSharp", StringComparison.Ordinal) ||
                    string.Equals(reference, "Assembly-CSharp-Editor", StringComparison.Ordinal))
                {
                    issueCount++;
                    AddResult(info.Path, 0, Severity.Error, $"Assets asmdef must not reference Unity default assemblies: {info.Name} -> {reference}", string.Join(", ", info.References));
                    continue;
                }

                if (!knownAssemblyNames.Contains(reference))
                {
                    issueCount++;
                    AddResult(info.Path, 0, Severity.Error, $"Assets asmdef reference does not resolve to any known asmdef in Assets, Packages, or Library/PackageCache: {info.Name} -> {reference}", string.Join(", ", info.References));
                    continue;
                }

                if (supportAssemblyNames.Contains(info.Name) &&
                    !string.Equals(info.Name, "PlayModeTests", StringComparison.Ordinal) &&
                    targetAssemblyNames.Contains(reference))
                {
                    issueCount++;
                    AddResult(info.Path, 0, Severity.Error, $"Vendor/support asmdef must not reference project target assemblies: {info.Name} -> {reference}", string.Join(", ", info.References));
                }
            }
        }

        if (issueCount == 0)
            AddResult(string.Empty, 0, Severity.Info, $"All Assets asmdef references resolve, avoid Assembly-CSharp defaults, and vendor/support asmdefs do not reference project targets. Count={asmdefsByName.Count}", string.Empty);
    }

    private void ValidateAsmrefReferenceResolution()
    {
        string assetsRoot = Application.dataPath;
        if (!Directory.Exists(assetsRoot))
            return;

        HashSet<string> knownAssemblyNames = LoadKnownAsmdefNames();
        Dictionary<string, string> knownAsmdefGuids = LoadKnownAsmdefGuidNames();
        string[] asmrefFiles = Directory.GetFiles(assetsRoot, "*.asmref", SearchOption.AllDirectories);
        if (asmrefFiles.Length == 0)
        {
            AddResult(string.Empty, 0, Severity.Info, "No asmref files were found under Assets.", string.Empty);
            return;
        }

        int missingCount = 0;
        for (int i = 0; i < asmrefFiles.Length; i++)
        {
            string path = asmrefFiles[i];
            string text = File.ReadAllText(path);
            Match referenceMatch = Regex.Match(text, @"""reference""\s*:\s*""([^""]*)""");
            if (!referenceMatch.Success || string.IsNullOrWhiteSpace(referenceMatch.Groups[1].Value))
            {
                missingCount++;
                AddResult(path, 0, Severity.Error, "Asmref has no reference value.", string.Empty);
                continue;
            }

            string reference = referenceMatch.Groups[1].Value;
            Match guidMatch = Regex.Match(reference, @"^GUID:([0-9a-f]{32})$");
            if (guidMatch.Success)
            {
                string guid = guidMatch.Groups[1].Value;
                if (knownAsmdefGuids.ContainsKey(guid))
                    continue;

                missingCount++;
                AddResult(path, 0, Severity.Error, $"Asmref GUID reference does not resolve to a known asmdef meta GUID: {reference}", string.Empty);
                continue;
            }

            if (knownAssemblyNames.Contains(reference))
                continue;

            missingCount++;
            AddResult(path, 0, Severity.Error, $"Asmref reference does not resolve to any known asmdef name: {reference}", string.Empty);
        }

        if (missingCount == 0)
            AddResult(string.Empty, 0, Severity.Info, $"All asmref references resolve to known asmdefs. Files={asmrefFiles.Length}", string.Empty);
    }

    private void ValidateAsmdefRequiredExternalReferences(Dictionary<string, AsmdefInfo> asmdefsByName)
    {
        int missingCount = 0;
        foreach (AsmdefInfo info in asmdefsByName.Values.OrderBy(value => value.Name))
        {
            string sourceRoot = ResolveProjectRelativeDirectory(info.Path);
            if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
                continue;

            string[] sourcePaths = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
            if (sourcePaths.Length == 0)
                continue;

            string sourceText = string.Join("\n", sourcePaths.Select(File.ReadAllText));
            HashSet<string> references = new HashSet<string>(info.References);
            for (int i = 0; i < RequiredExternalAsmdefReferenceRules.Length; i++)
            {
                RequiredExternalAsmdefReferenceRule rule = RequiredExternalAsmdefReferenceRules[i];
                if (references.Contains(rule.Reference))
                    continue;

                if (!rule.Pattern.IsMatch(sourceText))
                    continue;

                missingCount++;
                AddResult(info.Path, 0, Severity.Error, $"Source uses {rule.Description}, but asmdef does not reference package assembly: {rule.Reference}", string.Join(", ", info.References));
            }
        }

        if (missingCount == 0)
            AddResult(string.Empty, 0, Severity.Info, "All detected external package API usages have matching asmdef references.", string.Empty);
    }

    private static string ResolveProjectRelativeDirectory(string projectRelativePath)
    {
        if (string.IsNullOrWhiteSpace(projectRelativePath))
            return string.Empty;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string absolutePath = Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        return Path.GetDirectoryName(absolutePath);
    }

    private static HashSet<string> LoadKnownAsmdefNames()
    {
        HashSet<string> names = new HashSet<string>();
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;

        string[] roots =
        {
            "Assets",
            "Packages",
            "Library/PackageCache"
        };

        for (int i = 0; i < roots.Length; i++)
        {
            string root = Path.Combine(projectRoot, roots[i].Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root))
                continue;

            foreach (string path in Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(path);
                Match nameMatch = AsmdefNameRegex.Match(text);
                if (nameMatch.Success)
                    names.Add(nameMatch.Groups[1].Value);
            }
        }

        return names;
    }

    private static Dictionary<string, string> LoadKnownAsmdefGuidNames()
    {
        Dictionary<string, string> guidNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;

        string[] roots =
        {
            "Assets",
            "Packages",
            "Library/PackageCache"
        };

        for (int i = 0; i < roots.Length; i++)
        {
            string root = Path.Combine(projectRoot, roots[i].Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root))
                continue;

            foreach (string path in Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(path);
                Match nameMatch = AsmdefNameRegex.Match(text);
                if (!nameMatch.Success)
                    continue;

                string metaPath = path + ".meta";
                if (!File.Exists(metaPath))
                    continue;

                Match guidMatch = Regex.Match(File.ReadAllText(metaPath), @"(?m)^guid: ([0-9a-f]{32})\s*$");
                if (guidMatch.Success)
                    guidNames[guidMatch.Groups[1].Value] = nameMatch.Groups[1].Value;
            }
        }

        return guidNames;
    }

    private void ValidateProjectAssemblyCycles(Dictionary<string, AsmdefInfo> asmdefsByName)
    {
        HashSet<string> projectNames = new HashSet<string>(TargetAssemblyNames);
        HashSet<string> visited = new HashSet<string>();
        HashSet<string> visiting = new HashSet<string>();
        Stack<string> path = new Stack<string>();
        int issueCount = 0;

        for (int i = 0; i < TargetAssemblyNames.Length; i++)
            issueCount += VisitAssembly(TargetAssemblyNames[i], asmdefsByName, projectNames, visited, visiting, path);

        if (issueCount == 0)
            AddResult(string.Empty, 0, Severity.Info, "Project target assembly graph contains no cycles.", string.Empty);
    }

    private int VisitAssembly(
        string assemblyName,
        Dictionary<string, AsmdefInfo> asmdefsByName,
        HashSet<string> projectNames,
        HashSet<string> visited,
        HashSet<string> visiting,
        Stack<string> path)
    {
        if (visited.Contains(assemblyName))
            return 0;

        if (visiting.Contains(assemblyName))
        {
            AddResult(string.Empty, 0, Severity.Error, $"Project assembly cycle detected at {assemblyName}.", string.Join(" -> ", path.Reverse()));
            return 1;
        }

        if (!asmdefsByName.TryGetValue(assemblyName, out AsmdefInfo info))
            return 0;

        visiting.Add(assemblyName);
        path.Push(assemblyName);

        int issueCount = 0;
        for (int i = 0; i < info.References.Length; i++)
        {
            string reference = info.References[i];
            if (projectNames.Contains(reference))
                issueCount += VisitAssembly(reference, asmdefsByName, projectNames, visited, visiting, path);
        }

        path.Pop();
        visiting.Remove(assemblyName);
        visited.Add(assemblyName);
        return issueCount;
    }

    private void ValidateCSharpSourceBoundaries()
    {
        string assetsRoot = Application.dataPath;
        if (!Directory.Exists(assetsRoot))
            return;

        int uncoveredCount = 0;
        foreach (string sourcePath in Directory.GetFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsUnderAssemblyBoundary(sourcePath, assetsRoot))
                continue;

            uncoveredCount++;
            AddResult(sourcePath, 0, Severity.Error, "C# source file is outside any asmdef or asmref boundary and will compile into a default Unity assembly.", string.Empty);
        }

        if (uncoveredCount == 0)
            AddResult(string.Empty, 0, Severity.Info, "All C# source files under Assets are covered by an asmdef or asmref boundary.", string.Empty);
    }

    private void ValidateAssetSourceAssemblyOwners()
    {
        string assetsRoot = Application.dataPath;
        if (!Directory.Exists(assetsRoot))
            return;

        HashSet<string> allowedOwners = new HashSet<string>(AllowedAssetSourceAssemblies, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> ownerCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int issueCount = 0;
        int sourceCount = 0;

        foreach (string sourcePath in Directory.GetFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
        {
            sourceCount++;
            string owner = FindSourceAssemblyOwner(sourcePath, assetsRoot);
            if (string.IsNullOrWhiteSpace(owner))
            {
                issueCount++;
                AddResult(sourcePath, 0, Severity.Error, "C# source has no asmdef/asmref owner and would compile into a default Unity assembly.", string.Empty);
                continue;
            }

            if (!ownerCounts.ContainsKey(owner))
                ownerCounts[owner] = 0;

            ownerCounts[owner]++;
            if (!allowedOwners.Contains(owner))
            {
                issueCount++;
                AddResult(sourcePath, 0, Severity.Error, $"C# source is owned by an unapproved Assets assembly: {owner}", string.Empty);
            }
        }

        if (issueCount == 0)
        {
            string summary = string.Join(", ", ownerCounts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
            AddResult(string.Empty, 0, Severity.Info, $"All C# source under Assets is owned by approved target/test/support assemblies. Sources={sourceCount}; Owners={summary}", string.Empty);
        }
    }

    private static string FindSourceAssemblyOwner(string sourcePath, string assetsRoot)
    {
        DirectoryInfo directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath));
        string normalizedAssetsRoot = assetsRoot.Replace('\\', '/').TrimEnd('/');

        while (directory != null && directory.FullName.Replace('\\', '/').StartsWith(normalizedAssetsRoot, StringComparison.OrdinalIgnoreCase))
        {
            string asmdefPath = Directory.GetFiles(directory.FullName, "*.asmdef").FirstOrDefault();
            if (!string.IsNullOrEmpty(asmdefPath))
            {
                string text = File.ReadAllText(asmdefPath);
                Match nameMatch = AsmdefNameRegex.Match(text);
                return nameMatch.Success
                    ? nameMatch.Groups[1].Value
                    : "<invalid-asmdef>";
            }

            string asmrefPath = Directory.GetFiles(directory.FullName, "*.asmref").FirstOrDefault();
            if (!string.IsNullOrEmpty(asmrefPath))
            {
                string text = File.ReadAllText(asmrefPath);
                Match referenceMatch = Regex.Match(text, @"""reference""\s*:\s*""([^""]+)""");
                return referenceMatch.Success
                    ? ResolveAsmdefReferenceName(referenceMatch.Groups[1].Value)
                    : "<invalid-asmref>";
            }

            directory = directory.Parent;
        }

        return null;
    }

    private void ValidateProjectNamespaceAssemblySpans()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        Dictionary<string, string> assemblyRoots = new Dictionary<string, string>
        {
            { "Core", "Assets/_Project/Runtime/Core" },
            { "Gameplay", "Assets/_Project/Runtime/Features" },
            { "Infrastructure", "Assets/_Project/Runtime/Infrastructure" },
            { "Presentation", "Assets/_Project/Runtime/Presentation" },
            { "UI", "Assets/_Project/Runtime/UI" },
            { "Editor", "Assets/_Project/Editor" }
        };

        Dictionary<string, HashSet<string>> namespaceAssemblies = new Dictionary<string, HashSet<string>>();
        foreach (KeyValuePair<string, string> entry in assemblyRoots)
        {
            string absoluteRoot = Path.Combine(projectRoot, entry.Value.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(absoluteRoot))
                continue;

            foreach (string sourcePath in Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(sourcePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    Match match = NamespaceDeclarationRegex.Match(lines[i]);
                    if (!match.Success)
                        continue;

                    string namespaceName = match.Groups[1].Value;
                    if (!namespaceAssemblies.TryGetValue(namespaceName, out HashSet<string> assemblies))
                    {
                        assemblies = new HashSet<string>();
                        namespaceAssemblies[namespaceName] = assemblies;
                    }

                    assemblies.Add(entry.Key);
                }
            }
        }

        List<string> spans = new List<string>();
        foreach (KeyValuePair<string, HashSet<string>> pair in namespaceAssemblies.OrderBy(pair => pair.Key))
        {
            if (pair.Value.Count < 2)
                continue;

            spans.Add($"{pair.Key}({string.Join(",", pair.Value.OrderBy(value => value))})");
        }

        if (spans.Count == 0)
        {
            AddResult(string.Empty, 0, Severity.Info, "No declared namespace is shared by multiple target project assemblies.", string.Empty);
            return;
        }

        AddResult(
            string.Empty,
            0,
            Severity.Info,
            $"Declared namespaces span multiple target project assemblies. Count={spans.Count}. Treat namespaces as API/serialization compatibility labels, not as assembly-boundary proof.",
            string.Join("; ", spans.Take(8)));
    }

    private void ValidateDuplicateTargetTypeDeclarations()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        Dictionary<string, string> assemblyRoots = new Dictionary<string, string>
        {
            { "Core", "Assets/_Project/Runtime/Core" },
            { "Gameplay", "Assets/_Project/Runtime/Features" },
            { "Infrastructure", "Assets/_Project/Runtime/Infrastructure" },
            { "Presentation", "Assets/_Project/Runtime/Presentation" },
            { "UI", "Assets/_Project/Runtime/UI" },
            { "Editor", "Assets/_Project/Editor" }
        };

        Dictionary<string, List<TypeDeclarationLocation>> declarationsByName = new Dictionary<string, List<TypeDeclarationLocation>>();
        foreach (KeyValuePair<string, string> entry in assemblyRoots)
        {
            string absoluteRoot = Path.Combine(projectRoot, entry.Value.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(absoluteRoot))
                continue;

            foreach (string sourcePath in Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
                CollectTopLevelTypeDeclarations(entry.Key, sourcePath, declarationsByName);
        }

        int duplicateCount = 0;
        foreach (KeyValuePair<string, List<TypeDeclarationLocation>> entry in declarationsByName.OrderBy(pair => pair.Key))
        {
            if (entry.Value.Select(value => value.AssemblyName).Distinct().Count() <= 1)
                continue;

            duplicateCount++;
            AddResult(
                entry.Value[0].Path,
                entry.Value[0].LineNumber,
                Severity.Error,
                $"Top-level type is declared in multiple target assemblies: {entry.Key}",
                string.Join(", ", entry.Value.Take(8).Select(value => $"{value.AssemblyName}:{value.Path}:{value.LineNumber}")));
        }

        if (duplicateCount == 0)
            AddResult(string.Empty, 0, Severity.Info, $"No duplicate top-level type declarations were found across target assemblies. Types={declarationsByName.Count}", string.Empty);
    }

    private static void CollectTopLevelTypeDeclarations(
        string assemblyName,
        string sourcePath,
        Dictionary<string, List<TypeDeclarationLocation>> declarationsByName)
    {
        string currentNamespace = string.Empty;
        int typeDeclarationDepth = 0;
        int braceDepth = 0;
        string[] lines = File.ReadAllLines(sourcePath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            Match namespaceMatch = NamespaceDeclarationRegex.Match(line);
            if (namespaceMatch.Success)
            {
                currentNamespace = namespaceMatch.Groups[1].Value;
                typeDeclarationDepth = line.Contains(";", StringComparison.Ordinal)
                    ? braceDepth
                    : braceDepth + 1;
            }

            Match typeMatch = TypeDeclarationRegex.Match(line);
            if (braceDepth == typeDeclarationDepth && typeMatch.Success)
            {
                string typeName = typeMatch.Groups[1].Value;
                string fullName = string.IsNullOrWhiteSpace(currentNamespace)
                    ? typeName
                    : currentNamespace + "." + typeName;
                if (!declarationsByName.TryGetValue(fullName, out List<TypeDeclarationLocation> locations))
                {
                    locations = new List<TypeDeclarationLocation>();
                    declarationsByName[fullName] = locations;
                }

                locations.Add(new TypeDeclarationLocation(assemblyName, ToProjectRelativePath(sourcePath), i + 1));
            }

            braceDepth += CountBraceDelta(line);
        }
    }

    private void ValidateKnownForbiddenConcreteDependencies()
    {
        int hitCount = 0;
        hitCount += ValidateKnownForbiddenConcreteDependenciesForAssembly(
            "Core",
            "Assets/_Project/Runtime/Core",
            CoreForbiddenConcreteTypes);
        hitCount += ValidateKnownForbiddenConcreteDependenciesForAssembly(
            "Gameplay",
            "Assets/_Project/Runtime/Features",
            GameplayForbiddenConcreteTypes);

        if (hitCount == 0)
            AddResult(string.Empty, 0, Severity.Info, "No known forbidden concrete upper-layer type references were found in Core or Gameplay source after removing comments and string literals.", string.Empty);
    }

    private void ValidateLowerLayerForbiddenNamespaceReferences()
    {
        Dictionary<string, HashSet<string>> namespaceOwners = LoadTargetNamespaceOwners();
        int hitCount = 0;
        hitCount += ValidateLowerLayerForbiddenNamespaceReferencesForAssembly(
            "Core",
            "Assets/_Project/Runtime/Core",
            new[] { "Gameplay", "Infrastructure", "Presentation", "UI", "Editor" },
            new[] { "Core" },
            namespaceOwners);
        hitCount += ValidateLowerLayerForbiddenNamespaceReferencesForAssembly(
            "Gameplay",
            "Assets/_Project/Runtime/Features",
            new[] { "Infrastructure", "Presentation", "UI", "Editor" },
            new[] { "Core", "Gameplay" },
            namespaceOwners);

        if (hitCount == 0)
            AddResult(string.Empty, 0, Severity.Info, "No upper-layer-only namespace imports or qualified references were found in Core or Gameplay source after removing comments and string literals.", string.Empty);
    }

    private static Dictionary<string, HashSet<string>> LoadTargetNamespaceOwners()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        Dictionary<string, string> assemblyRoots = new Dictionary<string, string>
        {
            { "Core", "Assets/_Project/Runtime/Core" },
            { "Gameplay", "Assets/_Project/Runtime/Features" },
            { "Infrastructure", "Assets/_Project/Runtime/Infrastructure" },
            { "Presentation", "Assets/_Project/Runtime/Presentation" },
            { "UI", "Assets/_Project/Runtime/UI" },
            { "Editor", "Assets/_Project/Editor" }
        };
        Dictionary<string, HashSet<string>> namespaceOwners = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> entry in assemblyRoots)
        {
            string absoluteRoot = Path.Combine(projectRoot, entry.Value.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(absoluteRoot))
                continue;

            foreach (string sourcePath in Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(sourcePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    Match namespaceMatch = NamespaceDeclarationRegex.Match(lines[i]);
                    if (!namespaceMatch.Success)
                        continue;

                    string namespaceName = namespaceMatch.Groups[1].Value;
                    if (!namespaceOwners.TryGetValue(namespaceName, out HashSet<string> owners))
                    {
                        owners = new HashSet<string>(StringComparer.Ordinal);
                        namespaceOwners[namespaceName] = owners;
                    }

                    owners.Add(entry.Key);
                }
            }
        }

        return namespaceOwners;
    }

    private int ValidateLowerLayerForbiddenNamespaceReferencesForAssembly(
        string assemblyName,
        string sourceRoot,
        string[] forbiddenAssemblies,
        string[] allowedProviderAssemblies,
        Dictionary<string, HashSet<string>> namespaceOwners)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string absoluteRoot = Path.Combine(projectRoot, sourceRoot.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(absoluteRoot))
            return 0;

        HashSet<string> forbiddenSet = new HashSet<string>(forbiddenAssemblies, StringComparer.Ordinal);
        HashSet<string> allowedSet = new HashSet<string>(allowedProviderAssemblies, StringComparer.Ordinal);
        List<string> forbiddenNamespaces = namespaceOwners
            .Where(pair => pair.Value.Any(forbiddenSet.Contains) && !pair.Value.Any(allowedSet.Contains))
            .Select(pair => pair.Key)
            .ToList();
        if (forbiddenNamespaces.Count == 0)
            return 0;

        string namespaceAlternation = string.Join("|", forbiddenNamespaces
            .OrderByDescending(namespaceName => namespaceName.Length)
            .Select(Regex.Escape));
        Regex usingRegex = new Regex(
            @"(?m)^\s*using\s+(?:static\s+)?(?<Namespace>" + namespaceAlternation + @")(?:\.[A-Za-z_][A-Za-z0-9_.]*)?\s*;",
            RegexOptions.Compiled);
        Regex qualifiedRegex = new Regex(
            @"\b(?<Namespace>" + namespaceAlternation + @")\.",
            RegexOptions.Compiled);
        int hitCount = 0;

        foreach (string sourcePath in Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
        {
            string sourceText = RemoveCSharpTrivia(File.ReadAllText(sourcePath));
            HashSet<string> usingHits = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> qualifiedHits = new HashSet<string>(StringComparer.Ordinal);

            foreach (Match match in usingRegex.Matches(sourceText))
            {
                string namespaceName = match.Groups["Namespace"].Value;
                if (usingHits.Add(namespaceName))
                {
                    hitCount++;
                    AddResult(sourcePath, 0, Severity.Error, $"{assemblyName} source imports upper-layer namespace: {namespaceName}", string.Empty);
                }
            }

            foreach (Match match in qualifiedRegex.Matches(sourceText))
            {
                string namespaceName = match.Groups["Namespace"].Value;
                if (usingHits.Contains(namespaceName))
                    continue;

                if (qualifiedHits.Add(namespaceName))
                {
                    hitCount++;
                    AddResult(sourcePath, 0, Severity.Error, $"{assemblyName} source references upper-layer namespace with a qualified name: {namespaceName}", string.Empty);
                }
            }
        }

        return hitCount;
    }

    private void ValidateLowerLayerForbiddenPresentationApiReferences()
    {
        int hitCount = 0;
        hitCount += ValidateLowerLayerForbiddenPresentationApiReferencesForAssembly(
            "Core",
            "Assets/_Project/Runtime/Core");
        hitCount += ValidateLowerLayerForbiddenPresentationApiReferencesForAssembly(
            "Gameplay",
            "Assets/_Project/Runtime/Features");

        if (hitCount == 0)
            AddResult(string.Empty, 0, Severity.Info, "No concrete TextMeshPro, Unity UI, Cinemachine, DOTween, or URP 2D lighting API references were found in Core or Gameplay source after removing comments and string literals.", string.Empty);
    }

    private int ValidateLowerLayerForbiddenPresentationApiReferencesForAssembly(
        string assemblyName,
        string projectRelativeRoot)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string absoluteRoot = Path.Combine(projectRoot, projectRelativeRoot.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(absoluteRoot))
            return 0;

        int hitCount = 0;
        foreach (string sourcePath in Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
        {
            string sourceText = RemoveCSharpTrivia(File.ReadAllText(sourcePath));
            for (int i = 0; i < LowerLayerForbiddenPresentationApiRules.Length; i++)
            {
                ForbiddenSourceApiRule rule = LowerLayerForbiddenPresentationApiRules[i];
                if (!rule.Pattern.IsMatch(sourceText))
                    continue;

                hitCount++;
                AddResult(sourcePath, 0, Severity.Error, $"{assemblyName} source references forbidden concrete presentation API: {rule.Description}", string.Empty);
            }
        }

        return hitCount;
    }

    private void ValidateProjectSourceDefaultAssemblyLiterals()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string absoluteRoot = Path.Combine(projectRoot, "Assets/_Project".Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(absoluteRoot))
        {
            AddResult("Assets/_Project", 0, Severity.Error, "Project source root is missing.", string.Empty);
            return;
        }

        int sourceCount = 0;
        int issueCount = 0;
        foreach (string sourcePath in Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
        {
            string relativePath = NormalizeProjectPath(ToProjectRelativePath(sourcePath));
            if (relativePath.StartsWith("Assets/_Project/Editor/Tools/Validation/", StringComparison.Ordinal))
                continue;

            sourceCount++;
            string sourceText = RemoveCSharpComments(File.ReadAllText(sourcePath));
            if (!Regex.IsMatch(sourceText, @"Assembly-CSharp(?:-Editor)?"))
                continue;

            issueCount++;
            AddResult(relativePath, 0, Severity.Error, "Project source contains a hardcoded default Unity assembly name outside validation tooling.", string.Empty);
        }

        if (issueCount == 0)
            AddResult(string.Empty, 0, Severity.Info, $"No hardcoded Assembly-CSharp or Assembly-CSharp-Editor literals were found in project source outside validation tooling. Sources={sourceCount}", string.Empty);
    }

    private int ValidateKnownForbiddenConcreteDependenciesForAssembly(
        string assemblyName,
        string projectRelativeRoot,
        string[] forbiddenTypes)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string absoluteRoot = Path.Combine(projectRoot, projectRelativeRoot.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(absoluteRoot))
            return 0;

        int hitCount = 0;
        foreach (string sourcePath in Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
        {
            string sourceText = RemoveCSharpTrivia(File.ReadAllText(sourcePath));
            for (int i = 0; i < forbiddenTypes.Length; i++)
            {
                string typeName = forbiddenTypes[i];
                if (!Regex.IsMatch(sourceText, $@"\b{Regex.Escape(typeName)}\b"))
                    continue;

                hitCount++;
                AddResult(sourcePath, 0, Severity.Error, $"{assemblyName} source references forbidden concrete upper-layer type: {typeName}", string.Empty);
            }
        }

        return hitCount;
    }

    private void ValidateRuntimeEditorSourceIsolation()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string runtimeRoot = Path.Combine(projectRoot, "Assets/_Project/Runtime".Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(runtimeRoot))
            return;

        int hitCount = 0;
        int editorConditionalCount = 0;
        HashSet<string> editorConditionalFiles = new HashSet<string>(StringComparer.Ordinal);
        List<string> editorConditionalOnlyFiles = new List<string>();
        foreach (string sourcePath in Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories))
        {
            string relativePath = NormalizeProjectPath(ToProjectRelativePath(sourcePath));
            if (relativePath.Contains("/Editor/", StringComparison.Ordinal) ||
                relativePath.EndsWith("Editor.cs", StringComparison.Ordinal))
            {
                hitCount++;
                AddResult(sourcePath, 0, Severity.Error, "Runtime source is under an Editor path/name and should live in the Editor assembly.", string.Empty);
            }

            string sourceText = RemoveCSharpTrivia(File.ReadAllText(sourcePath));
            for (int i = 0; i < RuntimeForbiddenEditorApiNames.Length; i++)
            {
                string apiName = RuntimeForbiddenEditorApiNames[i];
                if (!Regex.IsMatch(sourceText, $@"\b{Regex.Escape(apiName)}\b"))
                    continue;

                hitCount++;
                AddResult(sourcePath, 0, Severity.Error, $"Runtime source references a known UnityEditor API surface after removing comments and string literals: {apiName}", string.Empty);
            }

            string[] sourceLines = File.ReadAllLines(sourcePath);
            if (IsFullyUnityEditorConditionalSource(sourceLines))
                editorConditionalOnlyFiles.Add(relativePath);

            for (int i = 0; i < sourceLines.Length; i++)
            {
                if (!Regex.IsMatch(sourceLines[i].Trim(), @"^#if\s+.*UNITY_EDITOR"))
                    continue;

                editorConditionalCount++;
                editorConditionalFiles.Add(relativePath);
            }
        }

        if (hitCount == 0)
        {
            AddResult(string.Empty, 0, Severity.Info, "No runtime Editor source paths or known UnityEditor API surface references were found after removing comments and string literals.", string.Empty);
            if (editorConditionalCount > 0)
                AddResult(string.Empty, 0, Severity.Info, $"Runtime UNITY_EDITOR conditionals remain without known UnityEditor API surface references. Files={editorConditionalFiles.Count}; Occurrences={editorConditionalCount}", string.Empty);

            if (editorConditionalOnlyFiles.Count > 0)
            {
                string sample = string.Join(", ", editorConditionalOnlyFiles.Take(10));
                AddResult(string.Empty, 0, Severity.Info, $"Runtime source files fully wrapped in UNITY_EDITOR remain in runtime asmdef roots. Files={editorConditionalOnlyFiles.Count}; Sample={sample}", string.Empty);
            }
            else
            {
                AddResult(string.Empty, 0, Severity.Info, "No runtime source files are fully wrapped in UNITY_EDITOR under runtime asmdef roots.", string.Empty);
            }
        }
    }

    private static bool IsFullyUnityEditorConditionalSource(string[] sourceLines)
    {
        if (sourceLines == null || sourceLines.Length == 0)
            return false;

        int firstIndex = -1;
        int lastIndex = -1;
        for (int i = 0; i < sourceLines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(sourceLines[i]))
                continue;

            if (firstIndex < 0)
                firstIndex = i;
            lastIndex = i;
        }

        if (firstIndex < 0 || lastIndex < 0)
            return false;

        if (!Regex.IsMatch(sourceLines[firstIndex].Trim(), @"^#if\s+.*\bUNITY_EDITOR\b"))
            return false;

        if (!Regex.IsMatch(sourceLines[lastIndex].Trim(), @"^#endif\b"))
            return false;

        int depth = 0;
        for (int i = firstIndex; i <= lastIndex; i++)
        {
            string trimmed = sourceLines[i].Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            if (Regex.IsMatch(trimmed, @"^#if\b"))
            {
                depth++;
                continue;
            }

            if (Regex.IsMatch(trimmed, @"^#endif\b"))
            {
                depth--;
                if (depth < 0)
                    return false;
                continue;
            }

            if (Regex.IsMatch(trimmed, @"^#else\b") || Regex.IsMatch(trimmed, @"^#elif\b"))
            {
                if (depth == 1)
                    return false;

                continue;
            }

            if (depth == 0)
                return false;
        }

        return depth == 0;
    }

    private static string RemoveCSharpTrivia(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        string withoutBlockComments = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        string withoutLineComments = Regex.Replace(withoutBlockComments, @"//.*$", " ", RegexOptions.Multiline);
        string withoutVerbatimStrings = Regex.Replace(withoutLineComments, @"@""(?:[^""]|"""")*""", "\"\"");
        string withoutStrings = Regex.Replace(withoutVerbatimStrings, @"""(?:\\.|[^""\\])*""", "\"\"");
        return Regex.Replace(withoutStrings, @"'(?:\\.|[^'\\])'", "''");
    }

    private static string RemoveCSharpComments(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        string withoutBlockComments = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(withoutBlockComments, @"//.*$", " ", RegexOptions.Multiline);
    }

    private static int CountBraceDelta(string line)
    {
        if (string.IsNullOrEmpty(line))
            return 0;

        int delta = 0;
        for (int i = 0; i < line.Length; i++)
        {
            char character = line[i];
            if (character == '{')
                delta++;
            else if (character == '}')
                delta--;
        }

        return delta;
    }

    private void ValidateCSharpMetaPairing()
    {
        string assetsRoot = Application.dataPath;
        if (!Directory.Exists(assetsRoot))
            return;

        int sourceCount = 0;
        int metaCount = 0;
        int issueCount = 0;

        foreach (string sourcePath in Directory.GetFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
        {
            sourceCount++;
            string metaPath = sourcePath + ".meta";
            if (!File.Exists(metaPath))
            {
                issueCount++;
                AddResult(sourcePath, 0, Severity.Error, "C# source file is missing its .cs.meta pair; script GUID preservation is not proven.", string.Empty);
                continue;
            }

            string metaText = File.ReadAllText(metaPath);
            if (!Regex.IsMatch(metaText, @"(?m)^guid: [0-9a-f]{32}\s*$"))
            {
                issueCount++;
                AddResult(metaPath, 0, Severity.Error, "C# meta file is missing a Unity GUID.", string.Empty);
            }
        }

        foreach (string metaPath in Directory.GetFiles(assetsRoot, "*.cs.meta", SearchOption.AllDirectories))
        {
            metaCount++;
            string sourcePath = metaPath.Substring(0, metaPath.Length - ".meta".Length);
            if (File.Exists(sourcePath))
                continue;

            issueCount++;
            AddResult(metaPath, 0, Severity.Warning, "C# meta file has no matching .cs source file.", string.Empty);
        }

        if (issueCount == 0)
            AddResult(string.Empty, 0, Severity.Info, $"All C# source files have .cs.meta pairs with GUIDs, and no orphan .cs.meta files were found. Sources={sourceCount}; Metas={metaCount}", string.Empty);
    }

    private void ValidateAssetMetaGuidUniqueness()
    {
        string assetsRoot = Application.dataPath;
        if (!Directory.Exists(assetsRoot))
        {
            AddResult(string.Empty, 0, Severity.Error, "Assets folder is missing; asset meta GUID uniqueness cannot be verified.", string.Empty);
            return;
        }

        Dictionary<string, string> pathByGuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int metaCount = 0;
        int issueCount = 0;

        foreach (string metaPath in Directory.GetFiles(assetsRoot, "*.meta", SearchOption.AllDirectories))
        {
            metaCount++;
            Match guidMatch = Regex.Match(File.ReadAllText(metaPath), @"(?m)^guid: ([0-9a-f]{32})\s*$");
            if (!guidMatch.Success)
            {
                issueCount++;
                AddResult(metaPath, 0, Severity.Error, "Asset meta file is missing a Unity GUID.", string.Empty);
                continue;
            }

            string guid = guidMatch.Groups[1].Value;
            if (pathByGuid.TryGetValue(guid, out string firstPath))
            {
                issueCount++;
                AddResult(metaPath, 0, Severity.Error, $"Duplicate Unity meta GUID found. Guid={guid} First={ToProjectRelativePath(firstPath)}", string.Empty);
                continue;
            }

            pathByGuid[guid] = metaPath;
        }

        if (issueCount == 0)
            AddResult(string.Empty, 0, Severity.Info, $"All asset meta files under Assets have unique Unity GUIDs. Metas={metaCount}; UniqueGuids={pathByGuid.Count}", string.Empty);
    }

    private static bool IsUnderAssemblyBoundary(string sourcePath, string assetsRoot)
    {
        DirectoryInfo directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath));
        string normalizedAssetsRoot = assetsRoot.Replace('\\', '/').TrimEnd('/');

        while (directory != null && directory.FullName.Replace('\\', '/').StartsWith(normalizedAssetsRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.GetFiles(directory.FullName, "*.asmdef").Length > 0 ||
                Directory.GetFiles(directory.FullName, "*.asmref").Length > 0)
            {
                return true;
            }

            directory = directory.Parent;
        }

        return false;
    }

    private void ApplySafeUnityEventAssemblyFixes()
    {
        if (!EditorUtility.DisplayDialog(
                "Apply Safe UnityEvent Assembly Fixes",
                "This will rewrite known-safe UnityEvent target assembly names in primary scan-root scene and prefab YAML files. Continue?",
                "Apply",
                "Cancel"))
        {
            return;
        }

        int changedFiles = 0;
        int changedOccurrences = 0;

        foreach (string path in EnumerateSerializedFiles().Where(IsSceneOrPrefabPath))
        {
            string originalText = File.ReadAllText(path);
            string updatedText = ApplySafeUnityEventAssemblyFixes(originalText, out int fileChangedOccurrences);
            changedOccurrences += fileChangedOccurrences;

            if (updatedText == originalText)
                continue;

            File.WriteAllText(path, updatedText);
            changedFiles++;
            ImportAssetIfNeeded(path);
        }

        AssetDatabase.Refresh();
        ValidateSerializedReferences();
        results.Insert(0, new ValidationResult
        {
            Path = string.Empty,
            LineNumber = 0,
            SeverityLevel = Severity.Info,
            Message = $"Applied {changedOccurrences} safe UnityEvent assembly replacements in {changedFiles} files.",
            Line = string.Empty
        });
    }

    private void ApplySafeSecondaryUnityEventAssemblyFixes()
    {
        if (!EditorUtility.DisplayDialog(
                "Apply Safe Secondary UnityEvent Fixes",
                "This will rewrite known-safe UnityEvent target assembly names in scene and prefab YAML files outside the primary scan roots, including root-level copies and recovery scenes. Continue?",
                "Apply",
                "Cancel"))
        {
            return;
        }

        int changedFiles = 0;
        int changedOccurrences = 0;

        foreach (string path in EnumerateSecondarySerializedFiles().Where(IsSceneOrPrefabPath))
        {
            string originalText = File.ReadAllText(path);
            string updatedText = ApplySafeUnityEventAssemblyFixes(originalText, out int fileChangedOccurrences);
            changedOccurrences += fileChangedOccurrences;

            if (updatedText == originalText)
                continue;

            File.WriteAllText(path, updatedText);
            changedFiles++;
            ImportAssetIfNeeded(path);
        }

        AssetDatabase.Refresh();
        ValidateSerializedReferences();
        results.Insert(0, new ValidationResult
        {
            Path = string.Empty,
            LineNumber = 0,
            SeverityLevel = Severity.Info,
            Message = $"Applied {changedOccurrences} safe secondary UnityEvent assembly replacements in {changedFiles} files.",
            Line = string.Empty
        });
    }

    private void ApplySafeScriptGuidFixes()
    {
        if (!EditorUtility.DisplayDialog(
                "Apply Safe m_Script GUID Fixes",
                "This will rewrite known-safe m_Script GUIDs where the current replacement MonoScript is known. Continue?",
                "Apply",
                "Cancel"))
        {
            return;
        }

        int changedFiles = 0;
        int changedOccurrences = 0;

        foreach (string path in EnumerateSerializedFiles())
        {
            string originalText = File.ReadAllText(path);
            string updatedText = originalText;

            foreach (KeyValuePair<string, string> replacement in SafeScriptGuidReplacements)
            {
                string oldGuidField = $"guid: {replacement.Key}";
                string newGuidField = $"guid: {replacement.Value}";
                changedOccurrences += CountOccurrences(updatedText, oldGuidField);
                updatedText = updatedText.Replace(oldGuidField, newGuidField);
            }

            if (updatedText == originalText)
                continue;

            File.WriteAllText(path, updatedText);
            changedFiles++;
            ImportAssetIfNeeded(path);
        }

        AssetDatabase.Refresh();
        ValidateSerializedReferences();
        results.Insert(0, new ValidationResult
        {
            Path = string.Empty,
            LineNumber = 0,
            SeverityLevel = Severity.Info,
            Message = $"Applied {changedOccurrences} safe m_Script GUID replacements in {changedFiles} files.",
            Line = string.Empty
        });
    }

    private static IEnumerable<string> EnumerateSerializedFiles()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;

        foreach (string relativeRoot in ScanRoots)
        {
            string absoluteRoot = Path.Combine(projectRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(absoluteRoot))
            {
                if (HasSerializedExtension(absoluteRoot))
                    yield return absoluteRoot;
                continue;
            }

            if (!Directory.Exists(absoluteRoot))
                continue;

            foreach (string path in Directory.GetFiles(absoluteRoot, "*.*", SearchOption.AllDirectories))
            {
                if (HasSerializedExtension(path))
                    yield return path;
            }
        }
    }

    private static IEnumerable<string> EnumerateAssetImportLoadabilityFiles()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string[] relativeRoots =
        {
            "Assets/_Project",
            "Assets/AddressableAssetsData"
        };

        for (int i = 0; i < relativeRoots.Length; i++)
        {
            string absoluteRoot = Path.Combine(projectRoot, relativeRoots[i].Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(absoluteRoot))
                continue;

            foreach (string path in Directory.GetFiles(absoluteRoot, "*.*", SearchOption.AllDirectories))
            {
                if (HasAssetImportLoadabilityExtension(path))
                    yield return ToProjectRelativePath(path);
            }
        }
    }

    private static IEnumerable<string> EnumerateSecondarySerializedFiles()
    {
        string assetsRoot = Application.dataPath;
        if (!Directory.Exists(assetsRoot))
            yield break;

        foreach (string path in Directory.GetFiles(assetsRoot, "*.*", SearchOption.AllDirectories))
        {
            if (!HasSerializedExtension(path) || IsUnderPrimaryScanRoot(path))
                continue;

            yield return path;
        }
    }

    private void ValidateSecondarySerializedAssemblyCSharpResiduals()
    {
        string assetsRoot = Application.dataPath;
        if (!Directory.Exists(assetsRoot))
            return;

        List<string> filesWithResiduals = new List<string>();
        List<string> filesWithNonCacheResiduals = new List<string>();
        int occurrenceCount = 0;
        int editorClassIdentifierCount = 0;
        int unityEventTargetCount = 0;
        int otherSerializedCount = 0;

        foreach (string path in Directory.GetFiles(assetsRoot, "*.*", SearchOption.AllDirectories))
        {
            if (!HasSerializedExtension(path) || IsUnderPrimaryScanRoot(path))
                continue;

            string relativePath = ToProjectRelativePath(path);
            string[] lines = File.ReadAllLines(path);
            int fileOccurrenceCount = 0;
            int fileNonCacheCount = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int lineOccurrences = CountOccurrences(line, "Assembly-CSharp");
                if (lineOccurrences == 0)
                    continue;

                fileOccurrenceCount += lineOccurrences;
                if (EditorClassIdentifierAssemblyRegex.IsMatch(line))
                {
                    editorClassIdentifierCount += lineOccurrences;
                }
                else if (line.Contains("m_TargetAssemblyTypeName"))
                {
                    unityEventTargetCount += lineOccurrences;
                    fileNonCacheCount += lineOccurrences;
                    AddResult(
                        relativePath,
                        i + 1,
                        Severity.Warning,
                        "Secondary-scope UnityEvent target assembly still points at Assembly-CSharp. Use the safe secondary fix action only after confirming this root/recovery asset should be kept.",
                        line.Trim());
                }
                else
                {
                    otherSerializedCount += lineOccurrences;
                    fileNonCacheCount += lineOccurrences;
                }
            }

            if (fileOccurrenceCount == 0)
                continue;

            occurrenceCount += fileOccurrenceCount;
            filesWithResiduals.Add(relativePath);
            if (fileNonCacheCount > 0)
                filesWithNonCacheResiduals.Add(relativePath);
        }

        if (filesWithResiduals.Count == 0)
        {
            AddResult(string.Empty, 0, Severity.Info, "No Assembly-CSharp serialized strings were found outside the primary serialized scan roots.", string.Empty);
            return;
        }

        string sample = string.Join(", ", filesWithResiduals.OrderBy(path => path).Take(20));
        string nonCacheSample = string.Join(", ", filesWithNonCacheResiduals.OrderBy(path => path).Take(10));
        string detail =
            $"Assembly-CSharp serialized strings remain outside primary scan roots. Files={filesWithResiduals.Count}; Occurrences={occurrenceCount}; " +
            $"EditorClassIdentifierCache={editorClassIdentifierCount}; UnityEventTargets={unityEventTargetCount}; OtherSerialized={otherSerializedCount}";
        if (!string.IsNullOrWhiteSpace(nonCacheSample))
            detail += $"; NonCacheSample={nonCacheSample}";

        AddResult(
            string.Empty,
            0,
            Severity.Warning,
            detail,
            sample);
    }

    private void ValidateSecondarySerializedScriptReferences()
    {
        int fileCount = 0;
        int scriptReferenceCount = 0;
        int issueCount = 0;

        foreach (string path in EnumerateSecondarySerializedFiles())
        {
            fileCount++;
            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++)
            {
                Match match = MissingScriptRegex.Match(lines[index]);
                if (!match.Success)
                    continue;

                scriptReferenceCount++;
                string guid = match.Groups[1].Value;
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    issueCount++;
                    AddResult(path, index + 1, Severity.Warning, $"Secondary-scope m_Script GUID is missing from the AssetDatabase: {guid}", lines[index].Trim());
                    continue;
                }

                if (!string.Equals(Path.GetExtension(assetPath), ".cs", StringComparison.OrdinalIgnoreCase))
                {
                    issueCount++;
                    AddResult(path, index + 1, Severity.Warning, $"Secondary-scope m_Script GUID resolves to a non-C# asset path: {guid} -> {assetPath}", lines[index].Trim());
                }
            }
        }

        if (issueCount == 0)
        {
            AddResult(
                string.Empty,
                0,
                Severity.Info,
                $"No missing or non-C# m_Script GUID references were found outside the primary serialized scan roots. Files={fileCount}; ScriptReferences={scriptReferenceCount}",
                string.Empty);
        }
    }

    private static bool IsUnderPrimaryScanRoot(string path)
    {
        string normalizedPath = Path.GetFullPath(path).Replace('\\', '/');
        string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/').TrimEnd('/');

        for (int i = 0; i < ScanRoots.Length; i++)
        {
            string normalizedRoot = Path.GetFullPath(Path.Combine(projectRoot, ScanRoots[i].Replace('/', Path.DirectorySeparatorChar)))
                .Replace('\\', '/')
                .TrimEnd('/');
            if (normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool HasSerializedExtension(string path)
    {
        string extension = Path.GetExtension(path);
        return SerializedExtensions.Any(candidate => string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasAssetImportLoadabilityExtension(string path)
    {
        string extension = Path.GetExtension(path);
        return AssetImportLoadabilityExtensions.Any(candidate => string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountMissingScriptsInHierarchy(GameObject root)
    {
        if (root == null)
            return 0;

        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
        Transform rootTransform = root.transform;
        for (int i = 0; i < rootTransform.childCount; i++)
            count += CountMissingScriptsInHierarchy(rootTransform.GetChild(i).gameObject);

        return count;
    }

    private void ValidateSceneHierarchyMissingScripts(string scenePath)
    {
        Scene openedScene = default;
        bool sceneOpened = false;

        try
        {
            openedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            sceneOpened = openedScene.IsValid();
            if (!sceneOpened)
            {
                AddResult(scenePath, 0, Severity.Error, "Scene could not be opened for hierarchy missing-script validation.", string.Empty);
                return;
            }

            int missingScriptCount = 0;
            GameObject[] rootObjects = openedScene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
                missingScriptCount += CountMissingScriptsInHierarchy(rootObjects[i]);

            if (missingScriptCount > 0)
                AddResult(scenePath, 0, Severity.Error, $"Scene hierarchy contains missing script components. Count={missingScriptCount}", string.Empty);
        }
        catch (Exception exception)
        {
            AddResult(scenePath, 0, Severity.Error, $"Scene could not be validated for hierarchy missing scripts: {exception.Message}", string.Empty);
        }
        finally
        {
            if (sceneOpened)
                EditorSceneManager.CloseScene(openedScene, true);
        }
    }

    private static string ReadAddressableEntryAddress(string[] lines, int startIndex)
    {
        for (int index = startIndex; index < lines.Length; index++)
        {
            if (AddressableEntryGuidRegex.IsMatch(lines[index]))
                return null;

            Match addressMatch = Regex.Match(lines[index], @"^\s*m_Address:\s*(.*)$");
            if (!addressMatch.Success)
                continue;

            string address = addressMatch.Groups[1].Value.Trim();
            for (int continuationIndex = index + 1; continuationIndex < lines.Length; continuationIndex++)
            {
                string continuationLine = lines[continuationIndex];
                if (AddressableEntryGuidRegex.IsMatch(continuationLine) ||
                    Regex.IsMatch(continuationLine, @"^\s*m_(ReadOnly|SerializedLabels):") ||
                    Regex.IsMatch(continuationLine, @"^\s*FlaggedDuringContentUpdateRestriction:"))
                {
                    break;
                }

                Match continuationMatch = Regex.Match(continuationLine, @"^\s{6,}(.+)$");
                if (continuationMatch.Success)
                    address = $"{address} {continuationMatch.Groups[1].Value.Trim()}";
                else
                    break;
            }

            return address;
        }

        return null;
    }

    private static string NormalizeAssetPath(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('\\', '/').Trim();
    }

    private static bool IsSceneOrPrefabPath(string path)
    {
        string extension = Path.GetExtension(path);
        return string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase);
    }

    private static string ApplySafeUnityEventAssemblyFixes(string text, out int replacementCount)
    {
        int localReplacementCount = 0;
        if (string.IsNullOrEmpty(text))
        {
            replacementCount = 0;
            return text;
        }

        Dictionary<string, string> scriptGuidByFileId = BuildMonoBehaviourScriptGuidMap(text);
        string updatedText = text;

        foreach (SafeUnityEventTargetAssemblyReplacement replacement in SafeUnityEventTargetAssemblyReplacements)
        {
            string pattern =
                @"(?ms)(- m_Target: \{fileID: (?<target>-?\d+)\}\s*\r?\n\s*m_TargetAssemblyTypeName: )" +
                Regex.Escape(replacement.LegacyTypeName) +
                @"(?<suffix>\s*\r?\n)";

            updatedText = Regex.Replace(updatedText, pattern, match =>
            {
                string targetFileId = match.Groups["target"].Value;
                if (!scriptGuidByFileId.TryGetValue(targetFileId, out string actualGuid))
                    return match.Value;

                if (!string.Equals(actualGuid, replacement.ExpectedTargetScriptGuid, StringComparison.OrdinalIgnoreCase))
                    return match.Value;

                localReplacementCount++;
                return match.Groups[1].Value + replacement.ReplacementTypeName + match.Groups["suffix"].Value;
            });
        }

        replacementCount = localReplacementCount;
        return updatedText;
    }

    private static Dictionary<string, string> BuildMonoBehaviourScriptGuidMap(string text)
    {
        Dictionary<string, string> map = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(text))
            return map;

        foreach (Match match in Regex.Matches(
                     text,
                     @"(?ms)^--- !u!114 &(?<fileId>-?\d+)\s*\r?\nMonoBehaviour:.*?^  m_Script: \{fileID: 11500000, guid: (?<guid>[0-9a-f]{32}), type: 3\}"))
        {
            map[match.Groups["fileId"].Value] = match.Groups["guid"].Value;
        }

        return map;
    }

    private static int CountOccurrences(string text, string value)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value))
            return 0;

        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private void AddEditorClassIdentifierCacheSummary(Dictionary<string, int> cacheCountsByAssembly)
    {
        if (cacheCountsByAssembly.Count == 0)
        {
            AddResult(string.Empty, 0, Severity.Info, "No stale Assembly-CSharp editor class identifier cache strings were found.", string.Empty);
            return;
        }

        foreach (KeyValuePair<string, int> pair in cacheCountsByAssembly.OrderBy(pair => pair.Key))
        {
            AddResult(
                string.Empty,
                0,
                Severity.Info,
                $"Stale m_EditorClassIdentifier cache strings remain for {pair.Key}: {pair.Value}. These should be cleared by Unity reserialization, not broad text replacement.",
                string.Empty);
        }
    }

    private void ValidateAssemblyCSharpLine(
        string path,
        int lineNumber,
        string line,
        Dictionary<string, int> editorClassIdentifierCounts)
    {
        if (!line.Contains("Assembly-CSharp"))
            return;

        Match editorClassIdentifierMatch = EditorClassIdentifierAssemblyRegex.Match(line);
        if (editorClassIdentifierMatch.Success)
        {
            string assemblyName = editorClassIdentifierMatch.Groups[1].Value;
            if (!editorClassIdentifierCounts.ContainsKey(assemblyName))
                editorClassIdentifierCounts[assemblyName] = 0;

            editorClassIdentifierCounts[assemblyName]++;
            return;
        }

        if (line.Contains("m_TargetAssemblyTypeName"))
        {
            string trimmed = line.Trim();
            Severity severity = SafeUnityEventTargetAssemblyReplacements.Any(replacement => trimmed.Contains(replacement.LegacyTypeName))
                ? Severity.Warning
                : Severity.Error;
            string message = severity == Severity.Warning
                ? "UnityEvent target assembly still points at Assembly-CSharp. A safe target-verified replacement is available from this window."
                : "UnityEvent target assembly points at Assembly-CSharp, but the target type is missing or renamed. Review this manually before migration.";

            AddResult(path, lineNumber, severity, message, trimmed);
            return;
        }

        AddResult(path, lineNumber, Severity.Error, "Serialized data still contains Assembly-CSharp outside the known editor identifier categories.", line.Trim());
    }

    private void ValidateMissingScriptLine(string path, int lineNumber, string line)
    {
        Match match = MissingScriptRegex.Match(line);
        if (!match.Success)
            return;

        string guid = match.Groups[1].Value;
        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
        if (!string.IsNullOrEmpty(assetPath))
        {
            if (!string.Equals(Path.GetExtension(assetPath), ".cs", StringComparison.OrdinalIgnoreCase))
                AddResult(path, lineNumber, Severity.Error, $"m_Script GUID resolves to a non-C# asset path: {guid} -> {assetPath}", line.Trim());

            return;
        }

        if (SafeScriptGuidReplacements.ContainsKey(guid))
        {
            AddResult(path, lineNumber, Severity.Warning, $"m_Script GUID is missing but has a known-safe replacement: {guid}", line.Trim());
            return;
        }

        if (KnownPackageMissingScriptGuids.TryGetValue(guid, out string packageDescription))
        {
            AddResult(path, lineNumber, Severity.Warning, $"m_Script GUID belongs to a missing package component: {packageDescription}. Restore the package or remove the stale asset/component deliberately.", line.Trim());
            return;
        }

        AddResult(path, lineNumber, Severity.Error, $"m_Script GUID is missing from the AssetDatabase: {guid}", line.Trim());
    }

    private void ValidateManagedReferenceIntegrityLine(string path, int lineNumber, string line)
    {
        Match match = ManagedReferenceIntegrityFlagRegex.Match(line);
        if (!match.Success)
            return;

        AddResult(path, lineNumber, Severity.Error, $"Serialized managed-reference integrity flag is set: {match.Groups[1].Value}", line.Trim());
    }

    private void AddResult(string path, int lineNumber, Severity severity, string message, string line)
    {
        results.Add(new ValidationResult
        {
            Path = ToProjectRelativePath(path),
            LineNumber = lineNumber,
            SeverityLevel = severity,
            Message = message,
            Line = line
        });
    }

    private static void ImportAssetIfNeeded(string absolutePath)
    {
        string relativePath = ToProjectRelativePath(absolutePath);
        if (!relativePath.StartsWith("Assets/", StringComparison.Ordinal))
            return;

        AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);
    }

    private static void PingPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal))
            return;

        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        if (asset != null)
            EditorGUIUtility.PingObject(asset);
    }

    private static string ToProjectRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string normalizedPath = Path.GetFullPath(path).Replace('\\', '/');
        string normalizedRoot = projectRoot.Replace('\\', '/').TrimEnd('/');

        if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            return normalizedPath;

        return normalizedPath.Substring(normalizedRoot.Length + 1);
    }

    private static string NormalizeProjectPath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('\\', '/');
    }
}
