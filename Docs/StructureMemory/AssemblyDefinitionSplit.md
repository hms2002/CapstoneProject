---
status: active
authority: structure-memory
category: architecture-migration
last_reviewed: 2026-07-05
---

# Assembly Definition Split

## Purpose

Track the current migration state for splitting project-owned Unity C# code into the target assemblies:

- `Core`
- `Gameplay`
- `Infrastructure`
- `Presentation`
- `UI`
- `Editor`

This is a working migration map, not a final Architecture or Contract document.

## Current Evidence

Checked on 2026-07-05:

- Project-owned `.asmdef` files under `Assets/_Project`: `Core`, `Gameplay`, `Infrastructure`, `Presentation`, `UI`, `Editor`, and test-only `PlayModeTests`. The empty `Assets/_Project/Scenes/Tests/Tests.asmdef` was removed because it owned no source files.
- Vendor/support `.asmdef` files used by the split: `DOTween.Modules`, `DOTweenPro.Scripts`, `DOTweenPro.Scripts.Editor`, `Ink-Libraries`, `InkEditor`, `Ink.Demos.Basic`, and `Ink.Demos.Basic.Editor`.
- Static asset-asmdef path validation found all `14` `Assets` asmdefs are approved target/test/support assemblies in their expected paths.
- Static asset-asmdef reference policy validation found all `14` `Assets` asmdef references resolve, avoid `Assembly-CSharp` default assemblies, and vendor/support asmdefs do not reference the six project target assemblies.
- Static asmdef meta validation found all `14` `Assets` asmdef `.meta` files are present and contain `AssemblyDefinitionImporter` metadata.
- Static project test asmdef policy validation found `PlayModeTests` is the only approved `_Project` test asmdef exception, is marked with `optionalUnityReferences: TestAssemblies`, and owns all current project test C# source under `Assets/_Project/Tests/PlayMode` (`Sources=2`).
- Static source-boundary scan found `0` `.cs` files under `Assets` outside an asmdef/asmref boundary.
- Static source-owner scan found all `1,409` C# files under `Assets` are owned by approved target, test, or vendor/support assemblies: `Core=223`, `Gameplay=687`, `Infrastructure=66`, `Presentation=44`, `UI=177`, `Editor=76`, `PlayModeTests=2`, `DOTween.Modules=8`, `DOTweenPro.Scripts=6`, `DOTweenPro.Scripts.Editor=2`, `Ink-Libraries=98`, `InkEditor=17`, `Ink.Demos.Basic=2`, and `Ink.Demos.Basic.Editor=1`.
- Static project-owned production assembly-set validation passes: `Assets/_Project` contains exactly the six target production asmdefs (`Core`, `Gameplay`, `Infrastructure`, `Presentation`, `UI`, `Editor`) plus one test-only `PlayModeTests` exception.
- Static project-source default assembly literal validation passes: no hardcoded `Assembly-CSharp` or `Assembly-CSharp-Editor` literals exist in project source outside the validation tooling folder (`Sources=1269`).
- Static asmref validation found no `.asmref` files under `Assets`; future `.asmref` files are now checked to ensure their name or `GUID:` reference resolves to a known `.asmdef`.
- Static C# script meta pairing scan found `1,409` `.cs` files and `1,409` `.cs.meta` files under `Assets`, with `0` missing pairs, `0` missing script GUIDs, and `0` orphan `.cs.meta` files.
- Static diff-based script move pairing scan found all deleted C# source files in the current diff have matching deleted `.cs.meta` files and all deleted `.cs.meta` files have matching deleted C# source files (`DeletedSources=131`, `DeletedMetas=131`).
- Static asset meta GUID uniqueness scan found `12,527` `.meta` files under `Assets`, with `0` missing GUIDs and `0` duplicate GUIDs.
- Current static asmdef graph validation found all six project assemblies, `0` unresolved asmdef references, allowed lower-layer project dependency directions, and no project assembly cycles. Project asmdef references are normalized through assembly-name references or `GUID:` references before dependency-direction and cycle checks.
- Static asmdef platform validation passes: `Core`, `Gameplay`, `Infrastructure`, `Presentation`, and `UI` have no platform include/exclude restrictions, while `Editor` is restricted to `Editor` only.
- Static target asmdef option validation passes: all six target asmdefs keep `rootNamespace` empty, avoid `overrideReferences`, direct `precompiledReferences`, `noEngineReferences`, `allowUnsafeCode`, `defineConstraints`, and `versionDefines`, and all six keep `autoReferenced` enabled.
- Static Core dependency validation passes: `Core.asmdef` declares zero assembly references.
- Static lower-layer namespace dependency validation passes: `Core` and `Gameplay` source contain no imports or qualified references to upper-layer-only namespaces after removing comments and string literals.
- Static lower-layer concrete presentation API validation passes: `Core` and `Gameplay` source contain no direct TextMeshPro, Unity UI, Cinemachine, DOTween, or URP 2D lighting API references after removing comments and string literals.
- Static asmdef Editor-reference validation passes: runtime target asmdefs do not reference `Editor`-only assemblies such as `UnityEditor.*`, `*.Editor`, `InkEditor`, or the project `Editor` assembly.
- Static runtime editor conditional classification reports `77` runtime files / `135` `UNITY_EDITOR` conditionals. `RuntimeEditorConditionalOnlySource` explicitly reports that no runtime source files are fully wrapped in `UNITY_EDITOR` under runtime asmdef roots. The stricter UnityEditor isolation gate reports no runtime Editor source paths, known UnityEditor API surface references, or unguarded UnityEditor references.
- `Gameplay.asmdef` now directly references package assemblies it uses at compile time: `Unity.Behavior` for custom Behavior Tree nodes and `Unity.2D.Animation.Runtime` for `SpriteLibraryAsset`. `UI.asmdef` also references `Unity.2D.Animation.Runtime` for dialogue portrait sprite-library presentation. `Editor.asmdef` references `Unity.2D.PixelPerfect` for `PixelPerfectCamera` authoring tools.
- Runtime Editor isolation validation found no runtime C# files under Editor paths/names, no known UnityEditor API surface references after removing comments and string literals, and no unguarded runtime `UnityEditor` references under `Assets/_Project/Runtime`. `UIManager.QuitGame()` no longer calls `UnityEditor.EditorApplication` directly; it now uses Core `ApplicationQuitPlayback`, while Editor `ApplicationQuitEditorBackend` handles Play Mode termination from the Editor assembly. Runtime authoring helpers in Encyclopedia UI, Upgrade UI, scene-domain development start support, loot/input defaults, merchant/door ID generation, and selected gizmo label drawers now go through Core `EditorAuthoringPlayback`, with Editor `EditorAuthoringEditorBackend` owning dirty marking, persistent-object checks, prefab asset checks, editor time, delay calls, asset lookup/create/save/refresh, selection checks, and SceneView handle labels. Prewarm trace capture now lives in the Editor assembly and records through Core `PresentationPrewarmTracePlayback`.
- Generated legacy `Assembly-CSharp*.csproj` files are absent after Unity regeneration.
- `CapstoneProject.slnx` is the current generated solution file. The validation scripts treat `.slnx` as a valid solution artifact alongside `.sln`, and the generated solution content gate confirms that it includes all six target project files while excluding legacy `Assembly-CSharp*.csproj` project files.
- Current generated target `.csproj` files exist and include all current asmdef-owned source files. The generated project legacy-reference and compile-ownership gates also pass for all six target projects.
- `Library/ScriptAssemblies` contains all six target outputs: `Core.dll`, `Gameplay.dll`, `Infrastructure.dll`, `Presentation.dll`, `UI.dll`, and `Editor.dll`. Legacy `Assembly-CSharp.dll`, `Assembly-CSharp-Editor.dll`, `Assembly-CSharp-firstpass.dll`, and `Assembly-CSharp-Editor-firstpass.dll` are absent from the observed output folder.
- `Assets/_Project/Editor` is covered by `Editor.asmdef`, and Unity import produced `Editor.dll`.
- All project-owned runtime folders have local asmdefs, and Unity import produced the five runtime target DLLs.
- Unity batchmode validation now runs after quote/wait fixes. The latest strict run reports `Errors=0`, `Warnings=2`, and `Infos=91`; the remaining strict failure is only the secondary/root `Assembly-CSharp` serialized residual policy, not Visual Scripting, compile, generated project regeneration, or primary AssetDatabase loadability.
- Static serialized YAML scan covers `8,406` files under `Assets/_Project`, `Assets/AddressableAssetsData`, and `ProjectSettings`: `.asset=7,933`, `.controller=100`, `.overrideController=5`, `.prefab=337`, `.unity=31`.
- Current serialized scan found `0` UnityEvent `m_TargetAssemblyTypeName` references to `Assembly-CSharp`, `0` non-cache serialized `Assembly-CSharp` strings, `0` unknown missing `m_Script` GUIDs, and `0` serialized managed-reference missing-type / placeholder flags.
- Secondary serialized scan outside the primary roots found `12` files with `362` `Assembly-CSharp` strings: `Assets/GlobalUIRoot Copy.prefab` and `11` `Assets/_Recovery/*.unity` files. Current classification is `361` stale `m_EditorClassIdentifier` cache strings and `1` actual UnityEvent `m_TargetAssemblyTypeName` reference in `Assets/GlobalUIRoot Copy.prefab` (`UnlockResultUI, Assembly-CSharp`). These are separated from the primary `_Project` serialized validation because they appear to be root-level copy/recovery assets, but they still block a broad "all Assets serialized strings are clean" claim unless intentionally deleted, excluded, migrated, or reserialized.
- Current secondary residual breakdown:
  - `Assets/GlobalUIRoot Copy.prefab`: `31` occurrences.
  - `Assets/_Recovery/0.unity`: `5` occurrences.
  - `Assets/_Recovery/0 (1).unity`: `3` occurrences.
  - `Assets/_Recovery/0 (2).unity`: `92` occurrences.
  - `Assets/_Recovery/0 (3).unity`: `1` occurrence.
  - `Assets/_Recovery/0 (5).unity`: `2` occurrences.
  - `Assets/_Recovery/0 (6).unity`: `7` occurrences.
  - `Assets/_Recovery/0 (7).unity`: `7` occurrences.
  - `Assets/_Recovery/0 (8).unity`: `8` occurrences.
  - `Assets/_Recovery/0 (9).unity`: `98` occurrences.
  - `Assets/_Recovery/0 (10).unity`: `99` occurrences.
  - `Assets/_Recovery/0 (11).unity`: `9` occurrences.
- Static Addressables entry validation checked `1,893` Addressables entries and found `0` duplicate entry GUIDs, `0` missing asset GUID references, and `0` `Assets/` address-to-`.meta` GUID mismatches.
- `Assets/AddressableAssetsData/link.xml` and `link.xml.meta` are absent in the current worktree. Current validation reports no stale `Assembly-CSharp` preserve references there, but the previous linker preserve intent is not proven and still needs a project decision before final player-build preservation can be claimed.
- `Tools/Validation/Invoke-AddressablesLinkXmlMigrationReport.ps1` is a read-only dry-run helper for that decision. It reads the deleted `HEAD:Assets/AddressableAssetsData/link.xml` or an explicitly supplied legacy file, maps former `Assembly-CSharp` preserve entries to current runtime target assembly type declarations, preserves external/package assembly entries unchanged, writes only `Temp/AddressablesLinkXmlMigrationReport.txt` plus `Temp/AddressablesLinkXmlMigrationProposal.xml`, and fails if any former `Assembly-CSharp` project entry is unresolved, if the proposal still contains `Assembly-CSharp`, or if proposal type-entry count differs from the legacy file.
- Latest link.xml dry-run result: legacy entries `661`; migrated former `Assembly-CSharp` project entries `550`; preserved external/package entries `111`; unresolved former `Assembly-CSharp` entries `0`; proposal assemblies `23`; proposal type entries `661`; proposal `Assembly-CSharp` references `0`. Current migrated project split is `Core=73`, `Gameplay=322`, `Infrastructure=13`, `Presentation=26`, and `UI=116`.
- `Assets/_Project/Data/Bosses/ShadowBoss/BehaviorGraphs/BG_Witch.asset` no longer contains `Assembly-CSharp` in Behavior graph serialized type strings; `UnityGAS.AbilityDefinition` resolves to `Core`, while `BT_RandomSelector` and `ActivateGASAbilityAction` resolve to `Gameplay`.
- `ProjectSettings/VisualScriptingSettings.asset` was removed by the approved Visual Scripting residual cleanup; no `ProjectSettings/VisualScriptingSettings.asset.meta` existed before cleanup.
- Static YAML `m_Script` validation now finds `0` known Visual Scripting package missing-script references and `0` unknown missing `m_Script` GUIDs after the approved cleanup.
- Static Visual Scripting residual readiness validation now reports the cleanup target set is empty: `PackageInstalled=False`, `ExistingGraphAssets=0`, `UnreferencedGraphAssets=0`, `ProjectSettingsExists=False`, and `PixelLightMissingComponents=0`.
- `Assets/_Project/Editor/Tools/Validation/AssemblySplitSerializedReferenceValidatorWindow.cs` provides an Editor validation window for remaining primary-root `Assembly-CSharp` serialized strings, stale `m_EditorClassIdentifier` cache counts, secondary `Assets` serialized residuals outside primary roots, primary/secondary missing or non-C# `m_Script` GUIDs, managed-reference missing-type / placeholder flags, read-only AssetDatabase loadability for `.asset`, `.prefab`, and `.unity` assets under `Assets/_Project` and `Assets/AddressableAssetsData`, scene/prefab hierarchy missing-script counts, Addressables group entry GUID/address integrity, duplicate-entry detection, and entry main-asset loadability, target asmdef presence/reference direction/cycles/platform settings/runtime Editor-reference isolation, target source root nested asmdef/asmref boundaries, production source ownership by the six target source roots, asmref reference resolution, external package API usage vs asmdef package references, `.cs` source files outside an asmdef/asmref boundary, declared namespace spans across target assemblies, duplicate top-level type declarations across target assemblies, known forbidden Core/Gameplay concrete upper-layer type references, C# script/meta pairing, `Assets`-wide `.meta` GUID uniqueness, Unity `Library/ScriptAssemblies` compile outputs, and generated `.sln` / target `.csproj` files. Its manual fix buttons only replace known-safe UnityEvent target assembly names after verifying the target component's MonoScript GUID, known-safe secondary UnityEvent target assembly names outside primary roots after explicit confirmation, or known-safe `m_Script` GUIDs for currently existing replacement types.
- `Tools/Validation/Invoke-AssemblySplitStaticAudit.ps1` provides the same core checks outside Unity. Current run exits `0` with `0` errors, `2` warnings, and `105` infos. The remaining warnings are the secondary serialized residual summary and one secondary `Assets/GlobalUIRoot Copy.prefab` UnityEvent target that still names `Assembly-CSharp`.
  - `AsmdefGraph` reports that the six target assemblies follow allowed dependency directions and contain no cycles.
  - `CoreDependency` reports that `Core.asmdef` declares zero assembly references.
  - `ProjectAssemblySet` reports that project-owned production asmdefs are exactly the six target assemblies, with `PlayModeTests` as the sole test-only exception (`ProjectAsmdefs=7`).
  - `SourceDependencyNamespace` reports no upper-layer-only namespace imports or qualified references in `Core` or `Gameplay`.
  - `SourceDependencyPresentationApi` reports no direct concrete TextMeshPro, Unity UI, Cinemachine, DOTween, or URP 2D lighting API references in `Core` or `Gameplay`.
  - `SourceDefaultAssemblyLiteral` reports no hardcoded `Assembly-CSharp` / `Assembly-CSharp-Editor` literals in project source outside validation tooling.
  - `RuntimeEditorConditional` reports `UNITY_EDITOR` conditional blocks still exist in runtime source (`Files=77`, `Occurrences=135`) but without known UnityEditor API surface references. `RuntimeEditorConditionalOnlySource` explicitly reports that no runtime source files are fully wrapped in `UNITY_EDITOR` under runtime asmdef roots.
  - `CompletionGateSummary` reports the current blocker classes in one line: `UnityLock=False`, `MissingTargetAssemblyOutputs=0`, `StaleGeneratedProjectErrors=0`, `KnownPackageMissingScripts=0`, `UnknownMissingScriptErrors=0`, and `SecondaryAssemblyCSharpWarnings=2`.
  - `CompletionGateSummary` now emits the explicit-approval note only for the actual remaining secondary targets: `Assets/GlobalUIRoot Copy.prefab` and `Assets/_Recovery`, not Visual Scripting scene/settings assets.
  - The generated project Compile item check proves all six target `.csproj` files include all current asmdef-owned source files.
  - The generated project Compile ownership check proves all six target `.csproj` files contain no Compile items outside their current asmdef source boundary.
  - The generated project legacy-reference check proves all six target `.csproj` files contain no `Assembly-CSharp` default-assembly references.
  - The generated solution content check proves `CapstoneProject.slnx` includes all six target project files and no legacy default assembly project files.
  - The primary serialized scan covers `8,406` files (`.asset=7,933`, `.controller=100`, `.overrideController=5`, `.prefab=337`, `.unity=31`) and reports that every resolved `m_Script` GUID points to a C# MonoScript meta file; known package missing scripts and unknown missing `m_Script` GUIDs are both `0`.
  - The secondary residual warning classifies Assembly-CSharp strings as `EditorClassIdentifierCache=361`, `UnityEventTargets=1`, and `OtherSerialized=0`, and emits a separate `SerializedReferenceSecondaryUnityEvent` warning for `Assets/GlobalUIRoot Copy.prefab:9820`, so asset cleanup can distinguish Unity reserialization cache from real serialized target assembly names.
  - `SecondaryResidualReferenceUse` reports no references to the root `Assets/GlobalUIRoot Copy.prefab` GUID/path/name outside its own prefab/meta files.
  - `SecondaryResidualReferenceUse` reports no references to `_Recovery` scene GUIDs or paths outside `Assets/_Recovery` (`RecoverySceneGuids=12`).
  - `SecondaryRecoveryCacheOnly` reports `_Recovery` scene `Assembly-CSharp` strings are editor class identifier cache only (`Files=12`, `Occurrences=331`, `EditorClassIdentifierCache=331`).
  - The secondary serialized script scan reports no missing or non-C# `m_Script` GUID references outside the primary serialized scan roots (`Files=28`, `ScriptReferences=1318`).
  - `AsmdefReference` resolves project asmdef references by assembly name or `GUID:` meta GUID before reporting all project asmdef references valid.
  - `AsmdefAssetReference` reports that all `14` `Assets` asmdef references resolve, avoid `Assembly-CSharp` defaults, and vendor/support asmdefs do not reference project target assemblies.
  - `AsmrefReference` reports no `.asmref` files under `Assets`; if they are added later, the audit resolves both assembly-name and `GUID:` references to known `.asmdef` assets.
  - `AsmdefName`, `AsmdefAssetPath`, `AsmdefMeta`, `AsmdefNestedBoundary`, `AsmdefPath`, `AsmdefPlatform`, `AsmdefOptions`, `AsmdefEditorReference`, `TestAsmdefPolicy`, `ProjectSourceRoot`, and `SourceAssemblyOwner` all report the expected target/test/support assembly boundary state.
  - `DuplicateType` reports no duplicate top-level type declarations across the six target assemblies (`Types=1706`); the parser now handles block namespaces whose opening brace is on the following line.
  - `UnityProjectLock` reports `Temp/UnityLockfile` is absent.
  - `Core.dll`, `Gameplay.dll`, `Infrastructure.dll`, `Presentation.dll`, `UI.dll`, and `Editor.dll` all exist under `Library/ScriptAssemblies`; legacy default `Assembly-CSharp*.csproj` files and `Assembly-CSharp*.dll` outputs are absent.
  - The audit also reports matching external package API asmdef references, unique `Assets` `.meta` GUIDs, deleted C# source/meta move pairing (`DeletedSources=131`, `DeletedMetas=131`), moved script meta GUID preservation (`DeletedMetas=131`, `PreservedGuids=131`), type responsibility comments for changed/touched C# declarations, no known forbidden Core/Gameplay concrete upper-layer type references after removing comments and string literals, no upper-layer-only namespace imports or qualified references in `Core` or `Gameplay`, no runtime Editor source paths/names or known UnityEditor API surface references after removing comments and string literals, no stale `Assembly-CSharp` preserve reference in the absent Addressables `link.xml`, Visual Scripting residual cleanup target count `0`, and five namespace spans across target assemblies (`Cainos.PixelArtTopDown_Basic`, `CapstoneAudio`, `CapstonePresentation`, `UnityGAS`, and `UnityGAS.Sample`).
- `Tools/Validation/Invoke-AssemblySplitUnityValidation.ps1` now accepts `.slnx` and prefers Visual Studio MSBuild for solution restore/build when available, because local `dotnet` SDK `10.0.103` does not produce useful `.slnx` build diagnostics in this environment. Before VS MSBuild restore it removes only guarded `Temp/obj/**/*.tmp` files under the resolved project root, because failed restore attempts can leave temp files that deny access to later restores. It also launches MSBuild with a normalized single `Path` environment so Codex/PowerShell sessions that expose both `PATH` and `Path` do not fail Roslyn with duplicate environment-key errors before compilation begins. Unity batchmode executes `AssemblySplitSerializedReferenceValidatorWindow.RunAllValidationsFromCommandLine`, which runs the Editor/AssetDatabase-backed serialized reference, AssetDatabase loadability, Addressables, assembly-boundary, compile-output, and generated-project checks inside Unity. Unity is launched through `Start-Process -Wait -PassThru` with quoted arguments, and the old `Temp/AssemblySplitUnityValidation.log` is deleted before each run so stale logs cannot be reported as current output. `-ApplyVisualScriptingCleanup` is forwarded as a custom command-line flag and only then runs the existing Visual Scripting residual cleanup before validation. If `Temp/UnityLockfile` exists, the script fails fast with visible Unity process IDs, start times, and executable paths by default; passing `-WaitForUnityClose` makes it poll for lock release before starting batchmode, with `-WaitForUnityCloseTimeoutSeconds` and `-WaitForUnityClosePollSeconds` controlling the wait. After Unity batchmode exits successfully, the script scans `Temp/AssemblySplitUnityValidation.log` for C# compiler errors, missing script/import messages, managed-reference integrity errors, missing/non-C# `m_Script` GUID validation output, and AssetDatabase loadability failures, so an import log with serialization damage cannot pass solely because Unity returned exit code `0`.
- `Tools/Validation/Invoke-AssemblySplitCompletionReport.ps1` creates `Temp/AssemblySplitCompletionReport.txt` as a compact completion-readiness summary. It reruns static audit and the Addressables link.xml dry-run, reads the latest Unity validation log, and can run a fresh generated-solution MSBuild pass with `-RunMSBuild`. Latest `-RunMSBuild` report output says static audit exits `0` with `0` errors / `2` warnings / `105` infos, link.xml dry-run exits `0` with `LegacyEntries=661`, `MigratedProject=550`, `PreservedExternal=111`, `UnresolvedProject=0`, `ProposalEntries=661`, and `ProposalAssemblyCSharpReferences=0`, and fresh MSBuild exits `0` for `1` solution after removing `14` stale `Temp/obj` `.tmp` files. Current incomplete reasons are the two secondary serialized warnings, the fact that the link.xml proposal has not been restored to `Assets/AddressableAssetsData/link.xml`, and the latest strict Unity validation log still having two warnings.
- `Tools/Validation/Invoke-AssemblySplitOfflineCompileProbe.ps1` copies generated solution/project files into `Temp/AssemblySplitOfflineCompileProbe`, patches only the copies with current asmdef references, removes stale Compile items from copied generated projects when the source file no longer exists, adds missing Compile items from current asmdef source folders, redirects intermediate/output paths into the probe folder, reuses existing restore artifacts, and runs VS MSBuild with restore disabled. Its previous successful run remains useful as secondary source-graph evidence, but the current stronger evidence is Unity-generated target DLL output plus the real `CapstoneProject.slnx` MSBuild pass.
- Approved Visual Scripting residual cleanup deleted the unreferenced graph assets under `Assets/_Project/Data/VisualScripting/Graphs`, removed the `PixelLightTest` `VisualScripting SceneVariables` object with its two missing components, and deleted `ProjectSettings/VisualScriptingSettings.asset`.
- `Assets/_Project/Editor/Tools/Rendering/PixelLightTestScaleWaveReplacementTool.cs` remains the cleanup implementation for `PixelLightTest`; the latest direct scene scan finds no Visual Scripting SceneVariables object and no missing Visual Scripting GUID references.
- Diff-aware `.cs` type declaration responsibility scan currently reports `0` hard errors for added/changed declaration lines, `0` hard errors for touched type declarations, and `0` broad advisory misses. The touched-type scan covered `275` type declarations affected by added/changed lines, and the broad changed-file scan found nearby `책임` / `Responsibility` context for all `436` type declarations in `246` changed C# files.
- Legacy compatibility scripts restored during the verification slice:
  - `DamagePopupSceneAnchor` (`90321de462d66a740b565c4e066219cc`) under UI/DamagePopup.
  - `MonsterDefinition` (`c039e5d2080389d46bdff58fbfed8e73`) under Gameplay monster spawning.
  - `UIHoverKeepAliveArea` (`6c23454781dce014d9f504ba8eb14dcb`) under UI inventory detail.
  - `BossDrop` (`42667dfbf3529e6489882ac43545fe3b`) under Gameplay loot.

Project-owned runtime source layout after the current slice:

| Folder | C# files |
| --- | ---: |
| `Assets/_Project/Runtime/Core` | 223 |
| `Assets/_Project/Runtime/Features` | 687 |
| `Assets/_Project/Runtime/Infrastructure` | 66 |
| `Assets/_Project/Runtime/Presentation` | 44 |
| `Assets/_Project/Runtime/UI` | 177 |

Project-owned editor source layout after the current slice:

| Folder | C# files |
| --- | ---: |
| `Assets/_Project/Editor` | 76 |

Current project-owned assembly files:

| Assembly | Path | Direct project references |
| --- | --- | --- |
| `Core` | `Assets/_Project/Runtime/Core/Core.asmdef` | none |
| `Gameplay` | `Assets/_Project/Runtime/Features/Gameplay.asmdef` | `Core` |
| `Infrastructure` | `Assets/_Project/Runtime/Infrastructure/Infrastructure.asmdef` | `Core`, `Gameplay` |
| `Presentation` | `Assets/_Project/Runtime/Presentation/Presentation.asmdef` | `Core`, `Gameplay`, `Infrastructure` |
| `UI` | `Assets/_Project/Runtime/UI/UI.asmdef` | `Core`, `Gameplay`, `Infrastructure`, `Presentation` |
| `Editor` | `Assets/_Project/Editor/Editor.asmdef` | all five runtime assemblies |

Current vendor/demo support assembly files added to remove default assembly residual source:

| Assembly | Path | Boundary |
| --- | --- | --- |
| `DOTweenPro.Scripts` | `Assets/Plugins/Demigiant/DOTweenPro/DOTweenPro.Scripts.asmdef` | DOTweenPro runtime source companion to vendor DLLs |
| `DOTweenPro.Scripts.Editor` | `Assets/Plugins/Demigiant/DOTweenPro/Editor/DOTweenPro.Scripts.Editor.asmdef` | DOTweenPro editor source companion to vendor DLLs |
| `Ink.Demos.Basic` | `Assets/Ink/Demos/Basic Demo/Scripts/Ink.Demos.Basic.asmdef` | Ink demo runtime source |
| `Ink.Demos.Basic.Editor` | `Assets/Ink/Demos/Basic Demo/Scripts/Editor/Ink.Demos.Basic.Editor.asmdef` | Ink demo editor source |

## Target Direction

The target dependency direction should be:

```txt
Core
  <- Gameplay
  <- Infrastructure
  <- Presentation
  <- UI
Gameplay
  <- Presentation
  <- UI
Infrastructure
  <- Presentation/UI only when they use concrete platform services
Presentation
  <- UI when UI consumes presentation helpers
Runtime assemblies
  <- Editor
```

Core must not reference project-owned `Gameplay`, `Infrastructure`, `Presentation`, `UI`, or `Editor` assemblies.

Gameplay should not directly reference upper presentation/UI implementation. If gameplay needs audio, VFX, camera, or UI output, use Core-level contracts/events/data and let Presentation/UI/Infrastructure adapt them.

## Completion Audit Matrix

Current status on 2026-07-05:

| Requirement | Current evidence | Status |
| --- | --- | --- |
| Project-owned production code is split into exactly `Core`, `Gameplay`, `Infrastructure`, `Presentation`, `UI`, and `Editor` | `ProjectAssemblySet`, `AsmdefAssetPath`, `ProjectSourceRoot`, and `SourceAssemblyOwner` pass. `PlayModeTests` is isolated as a test-only exception. | Static pass |
| Core does not depend on other project assemblies | `CoreDependency` reports `Core.asmdef` declares zero assembly references. | Static pass |
| Project assembly dependency direction has no cycles | `AsmdefGraph` reports allowed directions and no cycles across the six target assemblies. | Static pass |
| Gameplay/Core do not directly depend on upper presentation/UI implementation | `SourceDependencyNamespace`, `SourceDependencyPresentationApi`, and `SourceDependencyConcreteType` pass for `Core` and `Gameplay`. | Static pass |
| Editor code is separated from runtime assemblies | `UnityEditor` reports no runtime Editor source paths, known UnityEditor API surface references, or unguarded UnityEditor references. `RuntimeEditorConditionalOnlySource` reports no fully wrapped editor-only runtime-root source files. | Static pass |
| Unity serialization GUIDs and `.cs/.meta` move pairing are preserved | `ScriptMeta`, `ScriptMetaMovePair`, and `ScriptMetaGuidPreservation` pass with `DeletedSources=131`, `DeletedMetas=131`, and `PreservedGuids=131`. | Static pass |
| Default `Assembly-CSharp` source and generated solution dependency are removed | `SourceCoverage`, `SourceDefaultAssemblyLiteral`, `GeneratedSolution`, generated project legacy-reference checks, and legacy compile-output absence checks pass. | Static pass, generated project pass |
| Current source compiles in generated solution | Visual Studio MSBuild on `CapstoneProject.slnx` exits `0`; `Invoke-AssemblySplitOfflineCompileProbe.ps1` also exits `0` as secondary evidence. | Solution build pass |
| Unity import, target assembly DLL output, and generated project regeneration pass | `CompletionGateSummary` reports `UnityLock=False`, `MissingTargetAssemblyOutputs=0`, and `StaleGeneratedProjectErrors=0`; all six target DLLs exist under `Library/ScriptAssemblies`. | Unity compile/import pass |
| Primary scene/prefab/ScriptableObject serialized references under `_Project`, Addressables, and ProjectSettings are clean of default assembly references | `SerializedReferenceSummary` reports no primary UnityEvent `Assembly-CSharp` target names, no non-cache primary `Assembly-CSharp` strings, no unknown missing `m_Script` GUIDs, and no managed-reference missing-type placeholders. | Static primary pass |
| Visual Scripting residual missing scripts are resolved or accepted | Approved cleanup removed the three graph assets, `PixelLightTest` Visual Scripting SceneVariables object, and `ProjectSettings/VisualScriptingSettings.asset`. `VisualScriptingResidualReadiness` reports `ExistingGraphAssets=0`, `ProjectSettingsExists=False`, and `PixelLightMissingComponents=0`. | Resolved |
| Secondary/recovery/root asset serialized `Assembly-CSharp` strings are resolved or accepted | `SerializedReferenceSecondaryScope` and `SerializedReferenceSecondaryUnityEvent` warn about `_Recovery` scenes and `Assets/GlobalUIRoot Copy.prefab`, including one real UnityEvent target. | Pending explicit prefab/recovery asset decision |
| Addressables are safe after import | Static Addressables GUID/address checks pass and latest Unity batch validation reports no Addressables errors. `link.xml` is absent, so stale default-assembly preserve references are gone but previous linker preserve intent remains unproven. | Static/Unity pass for current entries, preserve decision pending |
| Scene/prefab hierarchy missing-script checks pass after import | Latest Unity batch validation runs the scene hierarchy gate and reports no Visual Scripting missing-component failure. Strict validation exits `1` only because warning-fail mode still sees the secondary/root `Assembly-CSharp` residual warnings. | Unity pass for hierarchy; secondary warning policy pending |
| Added/changed type declarations have responsibility comments | `TypeResponsibility` reports changed/touched C# declarations have nearby responsibility comments. | Static pass |

## Residual Asset Cleanup Approval Map

These items are not code-boundary fixes. They touch scene, prefab, recovery, or ProjectSettings assets and need explicit approval before edits.

| Residual group | Exact current targets | Current evidence | Approval decision needed |
| --- | --- | --- | --- |
| Visual Scripting package residuals | Former targets were `Hazard.asset`, `Input Movement.asset`, `Scale Wave.asset`, `PixelLightTest` Visual Scripting SceneVariables/Variables components, and `ProjectSettings/VisualScriptingSettings.asset`. | Approved cleanup completed. Static audit now reports `KnownPackageMissingScripts=0`; direct scans find no Visual Scripting GUIDs in `PixelLightTest`, no graph assets, and no Visual Scripting settings asset. | Resolved; no further approval needed for this residual group. |
| Secondary root prefab UnityEvent | `Assets/GlobalUIRoot Copy.prefab:9820` has `m_TargetAssemblyTypeName: UnlockResultUI, Assembly-CSharp` on a `Close` button callback. | Static secondary scan reports one real secondary UnityEvent target outside primary roots. | Decide whether `GlobalUIRoot Copy.prefab` should be kept and migrated, deleted as a stale copy, or ignored by policy. |
| Secondary root/recovery editor class identifier cache | `Assets/GlobalUIRoot Copy.prefab` and `Assets/_Recovery/*.unity` contain stale `m_EditorClassIdentifier: Assembly-CSharp::...` cache strings. | Static secondary scan reports `EditorClassIdentifierCache=361` and `OtherSerialized=0`. Automated `SecondaryResidualReferenceUse` checks report no references to `Assets/GlobalUIRoot Copy.prefab` outside its own files and no references to `_Recovery` scene GUIDs/paths outside `Assets/_Recovery`. `SecondaryRecoveryCacheOnly` reports all `_Recovery` `Assembly-CSharp` strings are editor class identifier cache only. | Decide whether to keep/reserialize these root/recovery assets, delete stale recovery/copy assets, or explicitly exclude them from final scope. |

## Current Blockers

- `Assets/_Project/Runtime/Core` has `Core.asmdef`, Unity import produced `Core.dll`, and Core remains the dependency root for shared gameplay contracts.
  - Audio request data and control surface now live in Core: `SoundRef`, `SoundAnchorPolicy`, `SoundPlaybackContext`, `AudioHandle`, `ISoundPlaybackBackend`, and `SoundPlaybackUtility`.
  - Core no longer calls `SoundManager` directly; Infrastructure registers `SoundManager` as the `ISoundPlaybackBackend` provider.
  - The Core audio contract now also covers music start/stop, tracked one-shot playback, and combat SFX ducking, so Gameplay feature code no longer directly references `SoundManager`.
  - Ability, typing, and run-route BGM request helpers now live behind Core audio entry points: `AbilityAudioRouter`, `TypingAudioUtility`, and `RunRouteBgmPlayback`. Infrastructure `SoundManager` and `RunRouteBgmService` remain the concrete playback/BGM implementations.
  - Camera shake request data/control now live in Core: `CameraShakeHook`, `CameraShakeRequest`, `CameraManualShakeSettings`, `ICameraShakeBackend`, and `CameraShakePlayback`.
  - World presentation request data/control now live in Core: `WorldPresentationHook`, `SpawnedPresentationHook`, `WorldPresentationContext`, `IWorldPresentationBackend`, and `WorldPresentationPlayback`.
  - The Core world presentation contract now covers signal-only play, deferred play, one-shot spawn, deferred one-shot spawn, persistent spawn/release, and spawned-instance initialization. Gameplay feature code no longer directly references `WorldPresentationRuntime` or `PresentationSpawnService`.
  - Core now owns `ITimedHitEffect2D`; Presentation `TimedAnimatedHitEffect2D` implements it, and Gameplay uses the contract for timed hit-effect playback.
  - Damage popup request data/control now live in Core: `DamagePopupKind`, `DamagePopupRequest`, `IDamagePopupBackend`, and `DamagePopupPlayback`.
  - Damage popup duplicate suppression now lives in Core through `DamagePopupDuplicateSuppressor` / `DamagePopupSuppressionKind`.
  - Warning popup request data/control now live in Core through `WarningPopupCode`, `WarningPopupRequest`, `IWarningPopupBackend`, and `WarningPopupPlayback`; UIManager registers the concrete UI backend.
  - UI input/popup state query now lives in Core through `IUiInteractionStateBackend` and `UiInteractionStateQuery`; UIManager registers the concrete UI state backend.
  - Common UI commands now live in Core through `IUiCommandBackend` and `UiCommandPlayback`; UIManager registers the concrete command backend. World interaction prompt fallback is exposed only through Core `IWorldInteractionPromptView`, while `WorldInteractionPromptController` remains the concrete UI implementation.
  - UI stack request and external input-block ownership now live in Core through `IUIView`, `IStackableUI`, `ICloseRequestHandler`, `GameFlowInputBlocker`, `IUiStackBackend`, and `UiStackPlayback`; UIManager registers the concrete stack backend.
  - World item hover detail requests now live in Core through `IWorldItemHoverBackend` and `WorldItemHoverPlayback`; UI `WorldItemDetailPresenter` registers the concrete hover backend.
  - Cinematic letterbox requests now live in Core through `ICinematicLetterboxOverlayHandle`, `ICinematicLetterboxBackend`, and `CinematicLetterboxPlayback`; UI `CinematicLetterboxOverlay` registers the concrete backend.
  - Global canvas layer identity and root access now live in Core through `GlobalCanvasLayer`, `IGlobalCanvasBackend`, and `GlobalCanvasPlayback`; UI `GlobalUIRoot` registers the concrete backend.
  - Default HUD visibility markers now live in Core through `IDefaultHudVisibilityTarget`. UI HUD widgets implement the marker, and tutorial gameplay hides authored HUD roots without concrete HUD class references.
  - Status HUD display contracts now live in Core through `StatusHudDefinition`, `StatusHudEntry`, `StatusHudGroup`, `IStatusHudSource`, and `StatusHudSourceRegistry`. Gameplay-owned source components register through the Core registry, while UI `StatusHudService` remains a facade that collects entries for the HUD presenter.
  - Damage payload configuration now lives in Core through `DamagePayloadConfig` and `ElementFormulaEntry`; `DamageSnapshotBuilder` no longer depends on a Features-owned weapon data type.
  - Speech data and speech request contracts now live in Core through `BossSpeechData`, `PlayerSpeechData`, `SpeechBubbleThemeSettings`, `DialogueAnimType`, `IBossSpeechPlayback`, and `ISpeechBubblePlayback`. UI `BossSpeechController` and `SpeechBubbleComponent` implement those contracts, while Gameplay callers use the contracts or generic `MonoBehaviour` serialized references.
  - HUD-facing boss and monster gauge request contracts now live in Core through `IMonsterElementGaugeViewInstaller`, `IBossHudSource`, `IBossHudBackend`, and `BossHudPlayback`. UI owns `MonsterElementGaugeViewInstaller` and `BossHudController` implementations, while Gameplay only calls Core contracts. `BossHudHealthBarTheme` uses Core `BossHudFrameImageType` instead of Unity UI `Image.Type` to keep Core independent from UI module types.
  - Item container/detail projection contracts now live with Gameplay/Features: `IItemContainer`, `IRelicLevelProvider`, `IRelicSlotReceiver`, `ItemDetailContext`, `ItemDetailActionHint`, `AbilityTooltipVariant`, `IAbilityTooltipVariantProvider`, `IDetailProvider`, `ItemDetailBlock`, and `InventoryWeaponRetentionPolicy`. UI inventory/detail views consume those contracts instead of owning them.
  - Player backpack runtime storage now lives with Gameplay under `Assets/_Project/Runtime/Features/Player/Inventory/PlayerBackpackInventory.cs`; dialogue/gameplay condition code reads that component without depending on UI.
  - Inventory delivery warning mapping now lives with Gameplay under `Assets/_Project/Runtime/Features/Player/Inventory/InventoryDeliveryWarningResolver.cs`; UI inventory flows and gameplay pickup flows share the same mapping without Features depending on UI.
  - Monster element-gauge visibility filtering now lives in Core through `IMonsterGaugeVisibilityFilter`; UI gauge views query this contract and monster feature components implement it.
  - Serialized text projection now has a Core fallback helper through `ITextValueSink` and `TextPresentationBinding`, allowing Gameplay code to update authored text components without directly referencing TextMeshPro. `MerchantRefreshInteractable`, `StatueShortcut`, and `ShopSlot` now use this helper instead of direct `TMP_Text` references.
  - Camera presentation requests now live in Core through `ICameraPresentationDirector`, `ICameraPresentationSettingsReceiver`, `ICameraPresentationFactoryBackend`, and `CameraPresentationPlayback`; Presentation `CameraPresentationDirector` implements the contracts and registers the legacy factory backend. Legacy `BossTalkManager` now passes camera settings through generic `Component` references instead of serialized `CinemachineCamera` fields.
  - Temporary gameplay camera focus now has a Core contract through `IGameplayCameraFocusSession`, `IGameplayCameraFocusBackend`, and `GameplayCameraFocusPlayback`; Presentation `GameplayCameraFocusService` owns the concrete Cinemachine/CameraBootstrap/legacy-follow implementation, including Follow/LookAt, priority, orthographic lens size, settle waits, and snap-to-target behavior. `LeverShortcut`, `RunSpecialNpcInteractor`, `TutorialCombatIntroSequence`, `HubIntroAfterDarkLordSequence`, `TutorialBossEncounterSequence`, `PlayerDeathReturnToHub2D`, and `PlayerHubSpawnPresentation2D` now use this contract instead of directly referencing Cinemachine or camera bootstrap types.
  - Gameplay camera view lookup now has a Core query through `IGameplayCameraViewBackend` and `GameplayCameraViewQuery`; Presentation `GameplayCameraFocusService` registers the concrete camera bootstrap-backed implementation. `RoomEnemyNavigationOverlay` and `PlayerHubSpawnPresentation2D` now use this query instead of `CameraBootstrap.GetMainCamera()`.
  - Gameplay map zoom now has a Core contract through `IGameplayCameraMapZoomSession`, `IGameplayCameraMapZoomBackend`, and `GameplayCameraMapZoomPlayback`; Presentation `GameplayCameraFocusService` owns the concrete Cinemachine snapshot, lens, priority, position, and restore behavior. `DemoCheatService` now owns only map bounds/transition flow and no longer references Cinemachine or `CameraBootstrap`.
  - `MerchantActivationCinematic` now lives under `Assets/_Project/Runtime/Presentation/Dialogue/NPC/Merchant` with its MonoScript GUID preserved. It remains a concrete Cinemachine/letterbox/input-lock presentation component and is no longer part of the future Gameplay source folder.
  - Chest UI opening is currently inverted through the Gameplay-owned `ChestUiOpenPlayback` / `IChestUiOpenBackend` contract because the UI backend needs concrete `TreasureChest` and `ChestInventory` gameplay data. This removes Gameplay's direct `ChestUIManager` dependency while allowing UI to depend downward on Gameplay data.
  - Dialogue playback is currently inverted through the Gameplay-owned `DialoguePlayback` / `IDialoguePlaybackBackend` contract because the request API uses gameplay dialogue data (`NPCData`, `DialogueStorySegment`, `NPCFeatureController`, `DialoguePresentationOptions`). Concrete `DialogueService`, `DialogueController`, and `DialogueRuntimeReferenceResolver` now live in UI.
  - Dialogue portrait emote presentation now lives in UI/Dialogue. Gameplay `DialogueTagHandler` only raises portrait-emote requests, while UI `DialogueController` / `PortraitController` resolve those requests to concrete `EmoteController` playback.
  - Run-special NPC choice presentation is currently inverted through the Gameplay-owned `IRunSpecialNpcChoicePresenter` / `IRunSpecialNpcChoiceAnchorFollower` contracts. `RunSpecialNpcInteractor` owns flow/camera/clock/feature execution, while UI `RunSpecialNpcChoicePresenter` and `RunSpecialNpcChoiceAnchorFollower` own Button/TMP/Image/Canvas projection and anchor following.
  - Game-over presentation is currently inverted through the Gameplay-owned `GameOverPresentationPlayback` / `IGameOverPresentationBackend` contract because the request API carries gameplay/run outcome data. Concrete `GameOverPresentationController` now lives in UI/GameOver and owns the `InventoryScreen` integration.
  - Ending outro view presentation is currently inverted through the Gameplay-owned `IEndingOutroView` contract. Gameplay `EndingOutroPlayer` owns sequence playback and input/typing flow, while UI `EndingOutroView` owns TMP/Image/CanvasGroup widget updates.
  - Tutorial presentation HP view ownership is currently inverted through the Gameplay-owned `ITutorialPresentationHpView` contract. Tutorial cutscene/laser gameplay owns HP operation timing, while UI `TutorialPresentationHpView` owns TMP/CanvasGroup/heart-slot rendering.
  - Tutorial info panel ownership is currently inverted through the Gameplay-owned `ITutorialInfoPanel` contract. Tutorial triggers/sequences own request timing and completion gating, while UI `TutorialInfoPanel` owns TMP/Image/Button/CanvasGroup page rendering, hold progress, input glyphs, and open/close presentation.
  - Affection UI ownership is currently inverted through the Gameplay-owned `IAffectionPresentationView` contract. `AffectionManager` owns affection state/gain flow, while UI `AffectionUI` owns TMP/Image/DOTween gain rendering and the gain-screen effect.
  - Affection scene preparation uses Core `AffectionPresentationPlayback` / `IAffectionPresentationBackend`; UI `AffectionGainScreenEffect` registers the concrete backend, and `SceneDomainScopes` no longer calls the concrete UI type directly.
  - Upgrade UI opening is inverted through Gameplay `UpgradeUiPlayback` / `IUpgradeUiBackend`. `UpgradeManager` owns upgrade progress/purchase/runtime effects, while UI `UpgradeTreeUI` owns the concrete `UpgradeUiOpenFlow` and registers as the backend.
  - Reward display presentation is inverted through Gameplay `RewardDisplayPlayback` / `IRewardDisplayBackend`. Affection and upgrade gameplay request reward display through the contract, while UI `RewardDisplayService` owns queuing, view registration, and concrete `RewardDisplayUI` presentation.
  - Shared dialogue presentation theme authoring data now lives in Core through `DialogueThemeSO`. `NPCData` can serialize the theme without depending on UI, and UI `DialogueView` consumes it when rendering.
  - Item display icon projection now lives under UI/Common/ItemDisplay. UI inventory/encyclopedia screens own `Image` projection details, while item display profiles and item definitions remain gameplay data under Features.
  - Chest monster kill-lock view/navigation presentation now lives under Presentation/Loot. The gameplay lock remains in Features and owns unlock/counting rules, while Presentation observes the lock and updates authored TMP text/effect/arrow objects.
  - Item/world-drop display presentation now lives under Presentation/Items/Display and Presentation/Loot/WorldDrops. Gameplay `ShopSlot`, `WorldItemPickup2D`, and `WeaponDrop2D` use `IItemDisplayVisualPresenter`, `IWorldDropSpritePresenter`, and world-drop animation contracts instead of concrete display/animation presenter components.
  - World item/weapon drop motion is inverted through Gameplay `WorldItemDropAnimationPlayback` / `IWorldItemDropAnimationBackend` / `IWorldItemDropAnimator`. Presentation `WorldItemDropAnimationService` owns runtime backend registration and `WorldItemDropTweenAnimator` owns DOTween movement/landing visual playback.
  - `FieldHealPickup2D` no longer imports DOTween; its drop arc is now a local Unity coroutine so this gameplay pickup does not force a DOTween reference into the future Gameplay assembly.
  - `DoorObject`, `GlobalVisionMaskController`, and `DeadsSkeleton` no longer import DOTween; their fallback door movement/shake, overlay alpha fade, and self-destruct sight-mask scale animations are now local Unity coroutines.
  - `ScenePortal`, `TutorialScenePortal`, and `HoleTrap` no longer import DOTween. Portal entrance pull-in animation is now local coroutine interpolation, and `HoleTrap` no longer performs a gameplay-side DOTween initialization.
  - `LightningSpearRecoveredSpearActor` no longer imports DOTween. Its former DOTween `Ease` serialized fields keep the same field names and numeric enum values through a local actor-owned ease enum, preserving the current prefab values (`moveEase: 6`, `floatEase: 4`) while removing the Gameplay-to-DOTween source dependency.
  - Static scan under `Assets/_Project/Runtime/Features` no longer finds direct `DG.Tweening`, `DOTween`, `DOVirtual`, `Tweener`, or DOTween shortcut API usage; remaining `Sequence` matches are gameplay/menu naming text, not tween API usage.
  - Scene transition, scene fade, loading overlay activity, and global time-scale pause now route through Core contracts: `SceneTransitionPlayback`, `SceneFadeTransitionPlayback`, `LoadingPresentationQuery`, and `TimeScalePausePlayback`. Infrastructure `SceneTransitionCoordinator`, `SceneFadeTransitionService`, `LoadingOverlayController`, and `TimeScalePauseService` register the concrete backends, so Features no longer directly reference those Infrastructure service types.
  - Training dummy damage readout presentation now lives under Presentation/Tutorial. The gameplay dummy remains in Features and owns hit reaction/auto-heal behavior, while the readout observes Core `AttributeSet` health changes and updates authored TMP text.
  - Puddle visual rendering now lives under Presentation/Map/Puddles. Gameplay `PuddleAreaBase` owns lifecycle/collider/absorb state and talks to `IPuddleShaderVisual`, `IPuddleParticleVisual`, and `IPuddleBlobVisual` contracts instead of concrete visual components.
  - Flowering Bloom presentation is currently inverted through the Gameplay-owned `FloweringBloomPresentationPlayback` / `IFloweringBloomPresentation` contract. `FloweringRuntimeState` owns Bloom gameplay state, while Presentation `FloweringBloomPresentationController` owns cut-in, screen border, player silhouette, weapon reveal, and cleanup presentation.
  - Combat hit impact audio now routes through Core `CombatHitAudioPlayback` / `ICombatHitAudioBackend`; Infrastructure `CombatHitAudioRouter` registers the backend.
  - Ability/effect presentation routers now live in Core and call audio/world-presentation backends instead of concrete Infrastructure services.
  - Ability spec-lifetime visual ownership now lives in Core through `AbilityVisualRouter`, because `AbilitySystem` owns and exposes that router.
  - Ability spec-lifetime afterimage and movement-aligned particle requests now use Core playback contracts: `AfterimageEmitterPlayback` / `IAfterimageEmitter2D` and `MotionAlignedParticlePlayback` / `IMotionAlignedParticleVisual2D`. Presentation `SpriteAfterimageEmitter2D` and `MotionAlignedParticleVisual2D` register the concrete backends.
  - Manual world-object presentation helpers now live in Core: `GameplayPresentationRuntime`, `WorldObjectPresentationDefinition`, and `WorldObjectPresentationRuntime`. They route sound/world presentation through Core playback contracts instead of concrete `SoundManager` or `WorldPresentationRuntime` calls.
  - Combat height presentation access now uses Core `ICombatHeightPresentation2D`; Presentation `CombatHeightPresentation2D` implements it and Gameplay callers no longer reference the concrete component.
  - Combat timing overrides now use Core `ICombatTimingProfile`; `MonsterCombatTimingProfile` implements the contract from Features.
  - Combat target resolution now uses Core `IAttackCollisionSource2D` instead of concrete `AttackBase`.
  - Core owns persistence/snapshot DTOs used by Core runtime services: `ActiveGameplayEffectSnapshot`, `ExplicitTagSnapshot`, and `ElementGaugeUiModel`.
  - Core no longer has direct source calls to known concrete project implementations such as `SoundManager`, `CameraShakeService`, `WorldPresentationRuntime`, `PresentationSpawnService`, `DamagePopupService`, `ElectricChainRibbonVfx`, `BossGroggyHeadTimer`, `TimedAnimatedHitEffect2D`, `GameplayCue_HitSparkParticles`, or `PlayerIntentInput2D`.
  - Input action identity and gameplay input reads now route through Core `InputActionId` and `InputActionQuery` / `IInputActionQueryBackend`. Infrastructure `InputBindingService` is the concrete backend, while Features no longer need direct `InputBindingService` / `InputKeyCompatibility` references for action polling.
  - Gameplay cursor interaction/hidden-state requests now route through Core `MouseCursorPlayback` / `IMouseCursorBackend`; Infrastructure `MouseCursorService` remains the concrete cursor renderer/state service.
  - First-run intro preload refresh requests now route through Core `PresentationPreloadPlayback` / `IPresentationPreloadBackend`; Infrastructure `PresentationPreloadService` remains the concrete preload window/provider owner.
  - Durable save DTOs and run/session DTOs now live in Core: `GameData`, `GamePlayData`, `MerchantRuntimeState`, `MerchantStockEntryState`, `RunEndReason`, `TransitionType`, `SceneTransitionContext`, and `PlayerRuntimeState`. Infrastructure managers/repositories remain the concrete save/session services.
  - `MerchantStockEntryState` no longer resolves item definitions through `ItemManager`; the concrete lookup lives in Features as `MerchantStockEntryStateDefinitionExtensions`, keeping the Core DTO data-only.
  - Persistent save and run session access now route through Core `GameDataStore` / `IGameDataStoreBackend` and `RunSessionStore` / `IRunSessionStoreBackend`. Infrastructure `GameDataManager` and `GamePlayDataManager` register as the concrete backends, so Features can query save/run state, request saves, restore pending player state, and handle run completion without importing those manager types.
  - Shortcut progress access now routes through Core `ShortcutProgressStore` / `IShortcutProgressStoreBackend`. Infrastructure `ShortcutProgressService` remains the concrete save/run-session implementation, while Features query or unlock shortcuts through the Core gateway.
  - Portal travel execution is now inverted through Gameplay-owned `ScenePortalTravelPlayback` / `IScenePortalTravelBackend`; Infrastructure `ScenePortalTravelService` registers the concrete backend and keeps the route/run/scene-transition implementation.
  - Sprite hit flash playback now uses Core `IHitFlashController2D`; Infrastructure `SpriteHitFlashController` implements the contract, while player/monster/candlestick Gameplay code no longer references the concrete component.
  - Realtime hitbox debug recording now uses Core `IRealtimeHitboxGizmo2D`; Infrastructure `RealtimeHitboxGizmo2D` implements the concrete gizmo recorder, while weapon gameplay uses the contract.
  - Presentation prefab resolution now uses Core `PresentationAssetPlayback` / `IPresentationAssetBackend`; Infrastructure `PresentationAssetProvider` registers the concrete backend, while Gameplay and Presentation callers resolve prefabs without importing the provider type.
  - Scene domain name checks now use Core `SceneDomainNamePolicy`; Features/UI callers no longer need the Infrastructure-only `SceneDomainScenePolicy`.
  - Run progress event dispatch now uses Gameplay `RunProgressPlayback` / `IRunProgressBackend`; Infrastructure `RunProgressCoordinator` registers the concrete backend, while boss gameplay publishes progress/reward events through the playback contract.
  - Run route state and portal route readiness now use Gameplay `RunRoutePlayback` / `IRunRouteBackend`; Infrastructure `PortalRouteManager` registers as the backend, while portals, boss reward policy, game-over location naming, and monster stage scaling no longer reference the concrete manager type.
  - Route authoring ScriptableObjects now live with Gameplay under `Assets/_Project/Runtime/Features/Map/Routes`: `CorridorBossRouteSetSO` and `RunRouteCatalogSO`. Their `.meta` GUIDs were preserved.
  - Shared route/loading data now lives in Core: `PortalRouteDecision`, `LoadManifestSO`, `RouteSetLoadManifestSO`, and `LoadScopeKind`. Their `.meta` GUIDs were preserved.
  - Static type-name scan under `Assets/_Project/Runtime/Features` no longer reports direct references to Infrastructure-defined types. Previous route/progress/presentation/debug families have been replaced by Core or Gameplay contracts.
  - Core now owns attack telegraph request/catalog data under `Assets/_Project/Runtime/Core/Presentation/Telegraph`: `AttackTelegraphSpec`, `AttackTelegraphShape`, `AttackTelegraphStyle`, and `AttackTelegraphStyleUtility`.
  - Core now owns attack telegraph presenter/handle contracts: `IAttackTelegraphPresenter`, `IAttackTelegraphHandle`, and `AttackTelegraphPresenterResolver`.
  - Core now owns `GameSettingsQuery` / `IGameSettingsBackend`, allowing Presentation services to query screen-shake settings without referencing UI's `GameSettingsService`.
  - Core now owns `PlayerUIControlLockBridge`, preserving its MonoScript GUID. UI, Gameplay, and Infrastructure can coordinate player control-lock tag sets without depending on the UI source folder.
  - Core still owns some presentation-like behavior through generic Unity objects and legacy cue paths, especially `GameplayCueManager` legacy VFX prefab spawning, `ElementGaugeSystem` trigger/sustain VFX spawning, and `StaggerGaugeSystem` optional presentation prefab spawning. These do not currently create project-assembly dependencies, but they remain architecture cleanup candidates before the final Presentation split is considered complete.
- The `UnityGAS`, `UnityGAS.Sample`, `CapstonePresentation`, `CapstoneAudio`, and `Cainos.PixelArtTopDown_Basic` namespaces currently span multiple target assemblies, so namespace is not a reliable assembly boundary. Use asmdef membership and concrete type ownership as the authoritative boundary evidence.
- The target `Presentation` source folder now exists and contains moved VFX, camera presentation, combat-height presentation, attack telegraph rendering scripts, loot lock presentation, and tutorial training readout presentation. Presentation code is still mixed into gameplay feature presentation folders and UI-adjacent systems.
- `Runtime/Infrastructure/Rendering/Telegraph` was split: request data moved to Core, concrete renderers moved to Presentation. Gameplay feature code now uses Core `IAttackTelegraphPresenter` / `IAttackTelegraphHandle` contracts instead of direct `AttackTelegraphService` / `AttackTelegraphView` references; static search under `Assets/_Project/Runtime/Features` found no remaining concrete telegraph implementation references.
- Vendor/default scripts under `Assets/Plugins` and Ink demo scripts still compile into default assemblies. They should be classified separately from project-owned migration unless the final release policy requires wrapping or excluding them.

## Work Completed

2026-07-04:

- Moved `InteractableTool.cs` and `InteractableTool.cs.meta` from `Assets/_Project/Runtime/Core/Interaction/` to `Assets/_Project/Editor/Tools/Interaction/`.
- Added `Assets/_Project/Editor/Tools/Interaction.meta`.
- Result: `Assets/_Project/Runtime/Core` no longer contains direct `UnityEditor` references.
- Removed an unused `DG.Tweening` import from `AttributeValue.cs`, so Core no longer has a direct DOTween source import.
- Moved `SoundRef.cs` and `SoundRef.cs.meta` from `Assets/_Project/Runtime/Infrastructure/Audio/` to `Assets/_Project/Runtime/Core/Audio/`.
- Moved `SoundPlaybackContext.cs`, `AudioHandle.cs`, `SoundPlaybackUtility.cs`, and their `.meta` files from `Assets/_Project/Runtime/Infrastructure/Audio/` to `Assets/_Project/Runtime/Core/Audio/`.
- Added `Assets/_Project/Runtime/Core/Audio.meta`.
- Split `SoundPlaybackUtility` behind `ISoundPlaybackBackend`; `SoundManager` now registers a backend during runtime initialization.
- Result: Core-owned ability/effect/cue/damage code can request audio without requiring Core to depend on the Infrastructure audio assembly once asmdefs are introduced.
- Moved `AbilityAudioRouter.cs` and `TypingAudioUtility.cs` with their `.meta` files into `Assets/_Project/Runtime/Core/Audio/`, preserving their script GUIDs.
- Extended `SoundPlaybackUtility` / `ISoundPlaybackBackend` with tracked one-shot playback so `TypingAudioUtility` no longer calls `SoundManager` directly.
- Added Core `RunRouteBgmPlayback` / `IRunRouteBgmBackend`; `RunRouteBgmService` registers the concrete backend and Features no longer call the service directly.
- Moved status HUD display contracts (`StatusHudDefinition`, `StatusHudEntry`, `StatusHudGroup`, `IStatusHudSource`) from `Assets/_Project/Runtime/UI/HUD/Status` to `Assets/_Project/Runtime/Core/Presentation/StatusHud`, preserving their script GUIDs.
- Added Core `StatusHudSourceRegistry` so gameplay-owned status HUD sources can register without calling UI services.
- Moved `PlayerStatusHudSource` into `Assets/_Project/Runtime/Features/Player/Status` and `SunMoonStatusHudSource` into `Assets/_Project/Runtime/Features/Items/Weapons/Inventory`, preserving their `.meta` files.
- Updated UI `StatusHudService` to delegate registration and collection to the Core registry instead of owning the source set directly.
- Moved item detail projection contracts (`ItemDetailContext`, `IAbilityTooltipVariantProvider`, `IDetailProvider`) from UI inventory folders to `Assets/_Project/Runtime/Features/Items/Display`, preserving their `.meta` GUIDs.
- Extracted `IItemContainer`, `IRelicLevelProvider`, and `IRelicSlotReceiver` out of UI `ItemSlotUI` into Gameplay `ItemContainerContracts`.
- Moved `InventoryWeaponRetentionPolicy` from UI inventory code to `Assets/_Project/Runtime/Features/Items/Weapons/Inventory`, preserving its `.meta` GUID.
- Moved `IMonsterGaugeVisibilityFilter` from UI element gauge code to `Assets/_Project/Runtime/Core/Elements`, preserving its `.meta` GUID.
- Added Core `AfterimageEmitterPlayback` / `IAfterimageEmitter2D` and `MotionAlignedParticlePlayback` / `IMotionAlignedParticleVisual2D`.
- Updated Presentation `SpriteAfterimageEmitter2D` and `MotionAlignedParticleVisual2D` to implement/register those Core backends.
- Converted DemonKing, EgoSword, Dragon, SlimeQueen, and Rush gameplay callers away from concrete `SpriteAfterimageEmitter2D` / `MotionAlignedParticleVisual2D` references.
- Moved `GameplayPresentationRuntime.cs` and `WorldObjectPresentation.cs` with their `.meta` files from `Assets/_Project/Runtime/Presentation/VFX/AbilityPresentation` to `Assets/_Project/Runtime/Core/Presentation`, preserving script GUIDs.
- Converted `GameplayPresentationRuntime` from concrete `SoundManager` / `WorldPresentationRuntime` calls to Core `SoundPlaybackUtility` / `WorldPresentationPlayback`.
- Added Core `ICombatHeightPresentation2D`; Presentation `CombatHeightPresentation2D` implements it, and SlimeQueen/SlimeSplit gameplay code now uses the contract.
- Moved `DamagePayloadConfig.cs` and `.meta` from `Assets/_Project/Runtime/Features/Items/Weapons/Data` to `Assets/_Project/Runtime/Core/Combat`, preserving script GUID `f6f43c8a1d122be4d9fc7903965b6663`, and removed the duplicate global `DamagePayloadConfig` declaration.
- Moved `BossSpeechData.cs`, `PlayerSpeechData.cs`, and `SpeechBubbleThemeSettings.cs` with their `.meta` files from UI speech folders to `Assets/_Project/Runtime/Core/Presentation/Speech`, preserving script GUIDs.
- Added Core `DialogueAnimType`, `IBossSpeechPlayback`, and `ISpeechBubblePlayback`.
- Updated UI `BossSpeechController` / `SpeechBubbleComponent` to implement the Core speech contracts.
- Converted boss, player, merchant, run-special NPC, SlimeQueen, and hub intro Gameplay callers away from concrete `BossSpeechController` / `SpeechBubbleComponent` references.
- Added Core `IMonsterElementGaugeViewInstaller`; UI `MonsterElementGaugeViewInstaller` implements it, and monster spawning / boss death cleanup now use the contract.
- Moved `BossHudHealthBarTheme.cs` and `.meta` from `Assets/_Project/Runtime/UI/HUD` to `Assets/_Project/Runtime/Core/Presentation/HUD`, preserving script GUID `5fcb1f5a88eff10418ecac00f7273c84`.
- Added Core `IBossHudSource`, `IBossHudBackend`, and `BossHudPlayback`; `BossControllerBase` implements the source contract and calls `BossHudPlayback` instead of `BossHudController`.
- Updated UI `BossHudController` to implement `IBossHudBackend`, and changed `BossHudSnapshot` / `BossHudValueUtility` to read `IBossHudSource` instead of `BossControllerBase`.
- Added Core `IWorldInteractionPromptView`; player interaction gameplay now uses `UiCommandPlayback` plus the Core prompt view fallback instead of concrete `WorldInteractionPromptController`.
- Moved `PlayerBackpackInventory` from UI inventory code to `Assets/_Project/Runtime/Features/Player/Inventory`, preserving its script GUID.
- Added Gameplay `UpgradeUiPlayback` / `IUpgradeUiBackend`; `UpgradeManager` no longer references concrete `UpgradeTreeUI` or `UpgradeUiOpenFlow`, and UI `UpgradeTreeUI` registers as the backend.
- Split `InventoryDeliveryWarningResolver` into Gameplay player inventory code so world pickup/player inventory flows no longer depend on UI inventory transfer files.
- Moved `PlayerUIControlLockBridge` and `DialogueThemeSO` into Core/Presentation, preserving their script GUIDs.
- Added Gameplay `RewardDisplayPlayback` / `IRewardDisplayBackend`; affection and upgrade gameplay no longer reference concrete `RewardDisplayService`.
- Added Core `IDefaultHudVisibilityTarget`; tutorial boss sequence default HUD hiding no longer lists concrete UI HUD classes.
- Moved `CameraShakeHook.cs`, `CameraShakeRequest.cs`, `WorldPresentationHook.cs`, `GameplayPresentationPhase.cs`, `GameplayPresentationDefinition.cs`, `AbilityPresentationRouter.cs`, `GameplayEffectPresentationRouter.cs`, `MonsterStageHpScalingSettings.cs`, and their `.meta` files into Core folders.
- Split camera shake, world presentation, damage popup, electric chain presentation, and stagger presentation creation behind Core-level contracts/backends.
- Added Core folders: `Audio`, `Camera`, `Presentation`, and `Scaling`.
- Removed Core's direct `Unity.AppUI.Redux` import from `AttributeStatProvider`.
- Added `Assets/_Project/Runtime/Core/Core.asmdef`.
- Moved `CombatHeightPresentation2D` into `Assets/_Project/Runtime/Presentation/Combat`.
- Moved concrete `Infrastructure/VFX` scripts into `Assets/_Project/Runtime/Presentation/VFX`.
- Moved concrete camera shake/presentation scripts into `Assets/_Project/Runtime/Presentation/Camera`, leaving camera bootstrap/policy/follow in Infrastructure.
- Added Core `SharedHitRegistry2D` so sustained hit logic and timed-hit presentation no longer share a presentation-owned nested type.
- Added Core `GameplayCuePrefabInstanceProviders`; `GameplayCueManager` now delegates HitSpark pooling/lifetime policy to a provider registered by `GameplayCue_HitSparkParticles`.
- Moved the top-down circle Y scale constant into Core `TopDownEllipseHitUtility2D`, removing Core's `AttackTelegraphSpec` reference.
- Moved presentation cue data assets (`PresentationCueSO`, `CueCatalogSO`, `CueRef`, `PresentationReference`) into Core and presentation runtime services (`CueCatalogService`, `PresentationSpawnService`, `PresentationRoutineRunner`, `RuntimePresentationFallbackAudit`) into Infrastructure.
- Moved `CinematicLetterboxOverlay` into UI and split `CameraCinematicWaitUtility` into Presentation/Camera so Presentation no longer depends on UI for camera settle waits.
- Added Core `GameSettingsQuery`; `CameraShakeService` now uses it instead of `GameSettingsService`.
- Moved `WindowModeBootstrap` into UI/Settings because it only applies boot settings through `GameSettingsService`.
- Split attack telegraph ownership: request/style/spec data now live in Core/Presentation/Telegraph, while `AttackTelegraphService`, `AttackTelegraphView`, and `AttackTelegraphWallClippedMeshView` live in Presentation/Telegraph.
- Added Core `IAttackTelegraphPresenter` / `IAttackTelegraphHandle`; `AttackTelegraphService` and `AttackTelegraphView` implement those contracts.
- Converted SlimeQueen, DemonKing, DragonBoss, and Knight jump slam gameplay code to use the Core telegraph contracts instead of concrete Presentation types.
- Converted the remaining general monster, Shadow monster, and ShadowBoss telegraph usage to the same Core contracts. `WitchNormalAttack1Tile` now discovers an `IAttackTelegraphHandle` on the authored object instead of requiring `AttackTelegraphView`.
- Expanded Core audio and world presentation contracts so Features no longer directly call `SoundManager`, `WorldPresentationRuntime`, or `PresentationSpawnService`.
- Added Core `ITimedHitEffect2D` and converted remaining Gameplay direct references away from concrete `TimedAnimatedHitEffect2D`.
- Moved damage popup suppression, element gauge UI snapshot, active effect snapshot, and explicit tag snapshot data into Core.
- Added Core contracts for hit impact audio, combat timing profile overrides, and attack-collider markers.
- Removed Core source references to concrete `CombatHitAudioRouter`, `MonsterCombatTimingProfile`, `AttackBase`, and UI-owned `ElementGaugeUiModel`.
- Moved `WarningPopupCode` into Core and converted Features warning popup calls from `UIManager.Instance.ShowWarning(...)` to `WarningPopupPlayback`.
- Added Core `UiInteractionStateQuery` and converted Features-side `UIManager` state queries (`HasBlockingUI`, `HasActivePopup`, `IsExternalUiInputBlocked`) to the Core query contract.
- Added Core `UiCommandPlayback` and converted Features-side `UIManager` command calls (`CloseAllPopups`, `HideHoverImmediate`, `HideWorldPrompt`, `RefreshWorldPrompt`) to the Core command contract.
- Moved UI screen contracts and `GameFlowInputBlocker` into Core, added `UiStackPlayback`, and registered `UIManager` as the concrete stack/input-block backend.
- Moved `EncyclopediaInteractable` into UI because it serializes and opens `EncyclopediaScreen`.
- Added Gameplay `ChestUiOpenPlayback`; `TreasureChest` now opens chest UI through the contract and `ChestUIManager` registers as the UI backend.
- Moved `WorldItemDetailPresenter` into UI detail UI and added Core `WorldItemHoverPlayback`; world drops, weapon drops, and shop slots now request hover detail UI through Core instead of referencing the concrete presenter.
- Moved `GlobalCanvasLayer` into Core, added Core `CinematicLetterboxPlayback` and `GlobalCanvasPlayback`, and converted Features away from concrete `CinematicLetterboxOverlay` / `GlobalUIRoot` calls.
- Added Gameplay `DialoguePlayback`, converted Features away from concrete `DialogueService` calls, and moved `DialogueService`, `DialogueController`, and `DialogueRuntimeReferenceResolver` into UI/Dialogue.
- Added Core `CameraPresentationPlayback`, converted Features away from concrete `CameraPresentationDirector` calls, and left the concrete Cinemachine implementation in Presentation/Camera.
- Converted legacy `BossTalkManager.playerCam` / `bossCam` serialized fields from `CinemachineCamera` to generic `Component`, preserving field names while removing that file's direct `Unity.Cinemachine` source dependency.
- Added Gameplay `GameOverPresentationPlayback`, converted Features away from concrete `GameOverPresentationController` calls, and moved the concrete game-over UI controller into UI/GameOver.
- Added Gameplay `IEndingOutroView`, converted `EndingOutroPlayer` away from concrete `EndingOutroView`, and moved the concrete outro view into UI/Progression/Ending.
- Added Gameplay `ITutorialPresentationHpView`, converted tutorial boss sequence/laser callers away from concrete `TutorialPresentationHpView`, and moved the concrete tutorial HP view into UI/Tutorial.
- Added Gameplay `ITutorialInfoPanel`, converted tutorial trigger/combat intro callers away from concrete `TutorialInfoPanel`, and moved the concrete tutorial info panel into UI/Tutorial.
- Moved `TrainingDummyDamageReadout2D.cs` and `.meta` from `Assets/_Project/Runtime/Features/Tutorial/Training` to `Assets/_Project/Runtime/Presentation/Tutorial`, preserving the MonoBehaviour script GUID.
- Moved `ItemDisplayIconUtility.cs` and `.meta` from `Assets/_Project/Runtime/Features/Items/Display` to `Assets/_Project/Runtime/UI/Common/ItemDisplay`, preserving the script GUID.
- Moved `ChestMonsterKillLockView.cs`, `ChestMonsterKillLockNavigationView.cs`, and their `.meta` files from `Assets/_Project/Runtime/Features/Loot/Chests/LockedChest` to `Assets/_Project/Runtime/Presentation/Loot`, preserving MonoBehaviour script GUIDs.
- Added Gameplay `IAffectionPresentationView`; `AffectionManager` now links to that contract instead of concrete `AffectionUI`.
- Added Core `AffectionPresentationPlayback`; `SceneDomainScopes` now requests affection presentation preparation through the Core backend contract.
- Moved `AffectionUI.cs` and `AffectionGainScreenEffect.cs` with `.meta` files from `Assets/_Project/Runtime/Features/Dialogue/Affection` to `Assets/_Project/Runtime/UI/Dialogue/Affection`, preserving MonoBehaviour script GUIDs.
- Moved shared `AffectionGradientBorderGraphic.cs` and `.meta` from `Assets/_Project/Runtime/Features/Dialogue/Affection` to `Assets/_Project/Runtime/Presentation/Common`, preserving the script GUID.
- Added Gameplay `FloweringBloomPresentationPlayback` / `IFloweringBloomPresentation`; `FloweringRuntimeState` now requests Bloom presentation through that contract instead of concrete `FloweringBloomPresentationController`.
- Moved `FloweringBloomPresentationController.cs` and `.meta` from `Assets/_Project/Runtime/Features/Items/Weapons/Flowering` to `Assets/_Project/Runtime/Presentation/Items/Weapons/Flowering`, preserving the script GUID.
- Added Gameplay `RunSpecialNpcChoicePresentationContracts.cs`; `RunSpecialNpcInteractor` now uses `IRunSpecialNpcChoicePresenter` / `IRunSpecialNpcChoiceAnchorFollower` instead of concrete choice UI types.
- Moved `RunSpecialNpcChoicePresenter.cs`, `RunSpecialNpcChoiceAnchorFollower.cs`, and their `.meta` files from `Assets/_Project/Runtime/Features/Dialogue/RunSpecial` to `Assets/_Project/Runtime/UI/Dialogue/RunSpecial`, preserving MonoBehaviour script GUIDs.
- Moved `EmoteController.cs` and `.meta` from `Assets/_Project/Runtime/Features/Dialogue` to `Assets/_Project/Runtime/UI/Dialogue`, preserving the script GUID.
- Added Core `TextPresentationBinding` / `ITextValueSink`; `MerchantRefreshInteractable` now keeps its serialized `remainingCountText` component field but no longer imports or references `TMP_Text`.
- Converted `StatueShortcut.requirementAmountText` from `TMP_Text` to a generic serialized `Component` and routes the cost label update through Core `TextPresentationBinding`.
- Expanded `TextPresentationBinding` with narrow text read/preferred-width/mesh-update helpers and converted `ShopSlot.priceText` from `TMP_Text` to a generic serialized `Component`.
- Added Core temporary camera focus contracts in `GameplayCameraFocusPlayback.cs` and Presentation `GameplayCameraFocusService`.
- Converted `LeverShortcut` door reveal camera motion to use `IGameplayCameraFocusSession` instead of direct `CinemachineCamera`, `CinemachineBrain`, `CameraBootstrap`, `CameraFollow`, or `CameraCinematicWaitUtility` references.
- Converted `RunSpecialNpcInteractor` NPC focus/return presentation to the same `IGameplayCameraFocusSession` contract, removing that file's direct Cinemachine/camera-bootstrap dependency.
- Expanded `IGameplayCameraFocusSession` with orthographic lens access and snap-to-target control, implemented by Presentation `GameplayCameraFocusService`.
- Converted `TutorialCombatIntroSequence`, `HubIntroAfterDarkLordSequence`, and `TutorialBossEncounterSequence` fallback camera focus/zoom/restore flows to `IGameplayCameraFocusSession`, removing those files' direct Cinemachine/camera-bootstrap dependencies.
- Added Core `GameplayCameraViewQuery` / `IGameplayCameraViewBackend` for gameplay output camera lookup.
- Moved input action identity into Core and added `InputActionQuery` / `IInputActionQueryBackend`; `InputBindingService` now registers as the backend.
- Moved `GameData`, `GamePlayData`, `MerchantRuntimeState`, `RunEndReason`, `TransitionType`, `SceneTransitionContext`, and `PlayerRuntimeState` with their `.meta` files into Core folders, preserving script GUIDs.
- Split merchant stock item-definition lookup out of the Core DTO into `MerchantStockEntryStateDefinitionExtensions` under merchant Features.
- Added Core `GameDataStore` / `RunSessionStore` gateways and backend contracts; `GameDataManager` and `GamePlayDataManager` register as Infrastructure backends.
- Converted save/run-state callers in currency, affection, boss dialogue progress, tutorial progress, run-special construction progress, upgrade runtime, run timer, item manager, and selected game-over/death flows away from direct `GameDataManager`, `GamePlayDataManager`, and `GameDataSaveCoordinator` calls.
- Converted player spawn/restore, tutorial portal pending-state preparation/rollback, and boss defeat ending completion away from direct `GamePlayDataManager` references. Static search under `Assets/_Project/Runtime/Features` now finds no direct `GameDataManager`, `GamePlayDataManager`, or `GameDataSaveCoordinator` references.
- Added Core `ShortcutProgressStore` / `IShortcutProgressStoreBackend`; `ShortcutProgressService` registers as the concrete backend, and shortcut/construction gameplay no longer references the Infrastructure service directly.
- Added Core `MouseCursorPlayback` / `IMouseCursorBackend`; `MouseCursorService` registers as the concrete backend, and Lightning Spear / ending outro gameplay no longer references the Infrastructure cursor service directly.
- Added Core `PresentationPreloadPlayback` / `IPresentationPreloadBackend`; `PresentationPreloadService` registers as the concrete backend, and hub intro gameplay no longer references the Infrastructure preload service directly.
- Added Gameplay `ScenePortalTravelPlayback` / `IScenePortalTravelBackend`; `ScenePortalTravelService` registers as the concrete backend, and `ScenePortal` no longer references the Infrastructure travel service directly.
- Added Core `IHitFlashController2D`; `SpriteHitFlashController` implements it, and player/monster/candlestick hit feedback code now uses the contract instead of the concrete Infrastructure component.
- Converted `PlayerDeathReturnToHub2D`, `RoomEnemyNavigationOverlay`, and `PlayerHubSpawnPresentation2D` away from direct `CameraBootstrap` / `CameraFollow` calls.
- Added Core `IGameplayCameraMapZoomSession` / `IGameplayCameraMapZoomBackend` / `GameplayCameraMapZoomPlayback`; Presentation `GameplayCameraFocusService` now owns the concrete Cinemachine map-zoom snapshot and restore behavior.
- Converted `DemoCheatService` map zoom to use the Core map-zoom session contract instead of direct `CinemachineCamera` / `CameraBootstrap` / lens manipulation.
- Moved `MerchantActivationCinematic.cs` and `.meta` from `Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant` to `Assets/_Project/Runtime/Presentation/Dialogue/NPC/Merchant`, preserving MonoScript GUID `8a4baf67e4a44d43bbe9cfe614880bf1`.
- Added Presentation folder metas for `Dialogue`, `Dialogue/NPC`, and `Dialogue/NPC/Merchant`.
- Added Gameplay puddle presentation contracts: `IPuddleShaderVisual`, `IPuddleParticleVisual`, and `IPuddleBlobVisual`.
- Converted `PuddleAreaBase` serialized visual fields from concrete `PuddleShaderVisual` / `PuddleParticleVisual` / `PuddleBlobVisual` types to generic `Component` references with contract-based access.
- Moved `PuddleShaderVisual.cs`, `PuddleParticleVisual.cs`, `PuddleBlobVisual.cs`, and their `.meta` files from `Assets/_Project/Runtime/Features/Map/Puddles/Presentation` to `Assets/_Project/Runtime/Presentation/Map/Puddles`, preserving MonoScript GUIDs.
- Added Presentation folder metas for `Map` and `Map/Puddles`.
- Added Gameplay item/world-drop presentation contracts: `IItemDisplayVisualPresenter`, `IWorldDropSpritePresenter`, and `IWorldItemDropLandingVisual`.
- Converted `ShopSlot`, `WorldItemPickup2D`, `WeaponDrop2D`, and `WorldItemDropTweenAnimator` to use those contracts instead of concrete item/world-drop presenter classes.
- Moved `ItemDisplayVisualPresenter2D.cs`, `ItemDisplayVisualInstance2D.cs`, `LightningSpearDropLandingVisual2D.cs`, `WorldDropSpritePresenter2D.cs`, and their `.meta` files into Presentation folders, preserving MonoScript GUIDs.
- Added Presentation folder metas for `Items/Display` and `Loot/WorldDrops`.
- Expanded `WorldDropPresentationContracts.cs` with `IWorldItemDropAnimator`, `IWorldItemDropAnimationBackend`, and `WorldItemDropAnimationPlayback`.
- Moved `WorldItemDropTweenAnimator.cs` and its `.meta` file from `Features/Loot/WorldDrops` to `Presentation/Loot/WorldDrops`, preserving MonoScript GUID `99391c7ccad24bc481d608cd20cfba93`.
- Added Presentation `WorldItemDropAnimationService` to register the concrete drop-animation backend and attach/use `WorldItemDropTweenAnimator` without Gameplay referencing the concrete type.
- Updated `LootSpawnService` and `WeaponDrop2D` so animated world item/weapon drops call `WorldItemDropAnimationPlayback` instead of directly getting/adding `WorldItemDropTweenAnimator`.
- Changed `IWorldItemDropLandingVisual` landing playback to coroutine-based `PlayDropLandingRoutine()` so the Gameplay-owned contract does not expose DOTween types.
- Converted `FieldHealPickup2D` drop motion from DOTween `Sequence` to a local coroutine while preserving the serialized drop timing/arc fields and prefab script GUID.
- Converted `DoorObject` fallback model open/close movement and locked-door shake from DOTween calls to local coroutines.
- Converted `GlobalVisionMaskController` overlay alpha fade from DOTween to a local coroutine with the same unscaled/scaled time option and OutSine easing.
- Converted `DeadsSkeleton` self-destruct sight-mask scale expand/reset from DOTween to local linear scale coroutines.
- Removed `HoleTrap`'s gameplay-side `DOTween.Init()` dependency.
- Converted `ScenePortal` and `TutorialScenePortal` entrance pull-in animation from DOTween sequences to local coroutines that preserve the previous OutQuart movement, InBack scale, and InCubic spin timing.
- Converted `LightningSpearRecoveredSpearActor` layout movement and idle float from DOTween tweens to local coroutines, preserving the serialized `moveEase` / `floatEase` field names and current enum numeric values through a local ease enum.
- Added Core `IRealtimeHitboxGizmo2D` and converted weapon gameplay hitbox-debug calls away from concrete `RealtimeHitboxGizmo2D`.
- Added Core `PresentationAssetPlayback` / `IPresentationAssetBackend` and converted Gameplay/Presentation prefab resolve callers away from concrete `PresentationAssetProvider`.
- Added Core `SceneDomainNamePolicy` and converted Features/UI scene-name checks away from Infrastructure `SceneDomainScenePolicy`.
- Added Gameplay `RunProgressPlayback` / `IRunProgressBackend`; `RunProgressCoordinator` registers as backend and boss gameplay no longer references the coordinator directly.
- Added Gameplay `RunRoutePlayback` / `IRunRouteBackend`; `PortalRouteManager` registers as backend and Features no longer reference the concrete manager directly.
- Moved `CorridorBossRouteSetSO.cs` and `RunRouteCatalogSO.cs` with `.meta` files from Infrastructure to `Assets/_Project/Runtime/Features/Map/Routes`, preserving GUIDs.
- Moved `PortalRouteDecision.cs` with `.meta` from Infrastructure to Core `SceneFlow`, preserving GUID `b320d7eed42729448afe06e7a9fa2eaa`.
- Moved `LoadManifestSO.cs`, `RouteSetLoadManifestSO.cs`, and `LoadScopeKind.cs` with `.meta` files from Infrastructure to Core `Presentation/Loading`, preserving GUIDs.
- Converted the remaining easy UI/Presentation direct references to `SceneDomainScenePolicy` and `PresentationAssetProvider` to Core `SceneDomainNamePolicy` and `PresentationAssetPlayback`.

## Next Migration Slice

Recommended next slice:

1. Treat `PreExistingMissingAssetReference` info as content debt outside the asmdef split regression gate unless the current task explicitly expands into scene/prefab/content repair.
2. For future Addressables/linker work, validate the generated `Library/com.unity.addressables/aa/<BuildTarget>/AddressablesLink/link.xml` output instead of forcing `Assets/AddressableAssetsData/link.xml` to persist. Addressables deletes the ConfigFolder copy on editor load and only recreates it temporarily for player-build linker processing.

Resolved cleanup:

- Cleanup 1 classified generic missing serialized asset GUIDs against `HEAD`.
  - Static audit now reports `MissingAssetReferenceErrors=0` and `PreExistingMissingAssetReferences=110`.
  - The 110 pre-existing lines are still real content debt, but all already existed in the same files at `HEAD`.
  - Largest content-debt groups remain old `UpgradeTreePanel` prefab refs in `LEeJunmo` scenes, legacy Ink JSON refs across boss/dialogue scenes, `BG_Witch.asset` behavior graph refs, and GlobalUIRoot variant sprite refs.
  - No scene, prefab, ScriptableObject, renderer, or behavior graph YAML was changed in this cleanup.
- `Assets/GlobalUIRoot Copy.prefab`, `Assets/GlobalUIRoot Copy.prefab.meta`, `Assets/_Recovery`, and `Assets/_Recovery.meta` were deleted after static evidence showed they were not referenced by active assets.
- Cleanup removed the final secondary/root serialized `Assembly-CSharp` warnings.
- Latest post-cleanup verification:
  - Static audit: `Errors=0`, `Warnings=0`, `Infos=216`, `MissingAssetReferenceErrors=0`, `PreExistingMissingAssetReferences=110`, `SecondaryAssemblyCSharpWarnings=0`.
  - Unity batch validation: `Errors=0`, `Warnings=0`, `Infos=92`.
  - Addressables build link validation: generated `Library/com.unity.addressables/aa/Windows/AddressablesLink/link.xml` with `Assemblies=23`, `Entries=661`, `Assembly-CSharp references=0`.
  - Completion report with fresh Unity validation, fresh Addressables build link validation, and fresh MSBuild: Unity wrapper exit `0`, Addressables build validation exit `0`, solution build exit `0`.
  - Addressables link.xml proposal: `LegacyEntries=661`, `MigratedProject=550`, `PreservedExternal=111`, `UnresolvedProject=0`, `ProposalAssemblyCSharpReferences=0`, `ProposalMetaGuid=01fd12cf0f26bc7468d405cc646d5eaa`.
  - Addressables ConfigFolder link.xml restore validation: `ExitCode=0`, `TargetExists=False`, `TargetMetaExists=False`, `TargetMatchesProposal=False`; this path is a temporary player-build copy deleted by Addressables editor load.
  - Current completion report lists no incomplete reasons.

Tooling notes:

- `Invoke-AssemblySplitUnityValidation.ps1` writes `UnityValidationLogPath=...` and falls back to a timestamped Unity log if `Temp/AssemblySplitUnityValidation.log` cannot be removed.
- `Invoke-AssemblySplitAddressablesBuildValidation.ps1` runs Unity batchmode Addressables player content build and validates the generated `AddressablesLink/link.xml` output for current asmdef preserve entries.
- `Invoke-AssemblySplitCompletionReport.ps1` removes stale `Temp/UnityLockfile` before and after Unity validation when no Unity process is visible, captures child PowerShell stderr without aborting report generation, reads the actual Unity result log path from the wrapper, and can include Addressables build link validation via `-RunAddressablesBuildValidation`.
- If sandboxed Unity batchmode reports `Project folder or disk is read only`, rerun the completion report with unsandboxed/elevated filesystem access before treating it as a Unity compile/import failure.

## Verification Commands

Current useful static checks:

```powershell
powershell -ExecutionPolicy Bypass -File Tools\Validation\Invoke-AssemblySplitStaticAudit.ps1
powershell -ExecutionPolicy Bypass -File Tools\Validation\Invoke-AssemblySplitCompletionReport.ps1 -RunUnityValidation -RunAddressablesBuildValidation -RunMSBuild
powershell -ExecutionPolicy Bypass -File Tools\Validation\Invoke-AssemblySplitCompletionReport.ps1
powershell -ExecutionPolicy Bypass -File Tools\Validation\Invoke-AssemblySplitAddressablesBuildValidation.ps1 -WaitForUnityClose
powershell -ExecutionPolicy Bypass -File Tools\Validation\Invoke-AddressablesLinkXmlRestore.ps1
powershell -ExecutionPolicy Bypass -File Tools\Validation\Invoke-AssemblySplitOfflineCompileProbe.ps1
powershell -ExecutionPolicy Bypass -File Tools\Validation\Invoke-AssemblySplitUnityValidation.ps1 -WaitForUnityClose
rg -n 'UnityEditor' 'Assets/_Project/Runtime' -g '*.cs'
rg -n 'AttackTelegraph|GameplayCue_HitSparkParticles|TimedAnimatedHitEffect2D|DamagePopupService|WorldPresentationRuntime|PresentationSpawnService|SoundManager\.EnsureInstance|CameraShakeService|ElectricChainRibbonVfx|BossGroggyHeadTimer|BossControllerBase|PlayerIntentInput2D|Unity\.AppUI|DG\.Tweening|UIManager\.Instance\.ShowWarning' 'Assets/_Project/Runtime/Core' -g '*.cs'
rg -n 'AttackTelegraphService|AttackTelegraphView|List<AttackTelegraphView>|GetComponent<AttackTelegraph|RequireComponent\(typeof\(AttackTelegraph' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n 'SoundManager|WorldPresentationRuntime|PresentationSpawnService|TimedAnimatedHitEffect2D' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n 'SoundManager|RunRouteBgmService|CombatHitAudioRouter|AudioCatalogSO' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n 'StatusHudService|StatusHudPresenter|StatusHudTooltipView|StatusHudEntryView|BossHealthBarUI|BossHudController' 'Assets/_Project/Runtime/Features/Player' 'Assets/_Project/Runtime/Features/Items' -g '*.cs'
rg -n 'class PlayerStatusHudSource|class SunMoonStatusHudSource' 'Assets/_Project/Runtime/UI' -g '*.cs'
rg -n 'guid: (d8f3b7b0879f4661acd56b78a4c1e3f2|b7494c3a77154343b95b14a9f6d6a5fe|98034f9f8af84d0bbc81b8011c787739|7f6bc3c0aef24b6ab8623297e6e4b95d)' 'Assets/_Project/Runtime' -g '*.meta'
rg -n 'public interface IItemContainer|public interface IRelicLevelProvider|public interface IRelicSlotReceiver|public sealed class ItemDetailContext|public readonly struct AbilityTooltipVariant|public interface IAbilityTooltipVariantProvider|public interface IDetailProvider|public struct ItemDetailBlock|public static class InventoryWeaponRetentionPolicy' 'Assets/_Project/Runtime/UI' -g '*.cs'
rg -n 'guid: (a830af196033e3846b46932684d40282|7a9f140dbb0349549a0de3f620e2e3d6|f4b96c93a2495a140a8d3b964a9181bc|2d2f5f8b6d8a43a7a38b6016c66bc851|0d96e7a11d3140da8ae3944ef4f7b8cb|2e21f9044a2541c694c414c6f4cbaf35)' 'Assets/_Project/Runtime' -g '*.meta'
rg -n '\bSpriteAfterimageEmitter2D\b|\bMotionAlignedParticleVisual2D\b|GetOrAddOwnedComponent<\s*(SpriteAfterimageEmitter2D|MotionAlignedParticleVisual2D)\s*>|GetOwnedComponent<\s*(SpriteAfterimageEmitter2D|MotionAlignedParticleVisual2D)\s*>|AddComponent<\s*(SpriteAfterimageEmitter2D|MotionAlignedParticleVisual2D)\s*>|GetComponent<\s*(SpriteAfterimageEmitter2D|MotionAlignedParticleVisual2D)\s*>' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n 'AfterimageEmitterPlayback|MotionAlignedParticlePlayback' 'Assets/_Project/Runtime/Features' 'Assets/_Project/Runtime/Core' 'Assets/_Project/Runtime/Presentation' -g '*.cs'
rg -n '\b(CombatHeightPresentation2D|WorldObjectPresentationRuntime|WorldObjectPresentationDefinition|GameplayPresentationRuntime|SpriteAfterimageEmitter2D|MotionAlignedParticleVisual2D|AttackTelegraphService|AttackTelegraphView|TimedAnimatedHitEffect2D|TrainingDummyDamageReadout2D|FloweringBloomPresentationController|PuddleShaderVisual|PuddleParticleVisual|PuddleBlobVisual|ItemDisplayVisualPresenter2D|WorldDropSpritePresenter2D|LightningSpearDropLandingVisual2D|WorldItemDropTweenAnimator)\b' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n 'SoundManager|WorldPresentationRuntime|PresentationSpawnService' 'Assets/_Project/Runtime/Core/Presentation/GameplayPresentationRuntime.cs' 'Assets/_Project/Runtime/Core/Presentation/WorldObjectPresentation.cs'
rg -n 'UIManager\.Instance\?*\.ShowWarning|UIManager\.Instance\.ShowWarning' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n 'UIManager\.Instance[^\n]*(HasBlockingUI|IsExternalUiInputBlocked|HasActivePopup)' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n 'UIManager\.Instance[^\n]*(HideWorldPrompt|HideHoverImmediate|CloseAllPopups|RefreshWorldPrompt)' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n '\bUIManager\b|\bChestUIManager\b|WorldItemDetailPresenter' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n '\bUIManager\b|\bChestUIManager\b|\bGlobalUIRoot\b|\bInventoryScreen\b|\bChestScreen\b|WorldItemDetailPresenter' 'Assets/_Project/Runtime/Core' -g '*.cs'
rg -n '\bCinematicLetterboxOverlay\b|GlobalUIRoot\.GetCanvas|GlobalUIRoot\.AdoptService|GlobalUIRoot\.AdoptToCanvas|GlobalUIRoot\.Instance' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n '\bDialogueService\b|\bDialogueController\b|\bDialogueView\b|DialogueRuntimeReferenceResolver|DialogueResolvedReferences' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n '\bCameraPresentationDirector\b' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n '\bGameOverPresentationController\b|\bInventoryScreen\b' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n '\bEndingOutroView\b' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n '\bTutorialPresentationHpView\b' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n '\bTutorialInfoPanel\b' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n '\bTrainingDummyDamageReadout2D\b' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n '\bItemDisplayIconUtility\b|\bItemDisplayIconDefaultState\b' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n '\bChestMonsterKillLockView\b|\bChestMonsterKillLockNavigationView\b' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n '\bAffectionUI\b|\bAffectionGainScreenEffect\b|\bFloweringBloomPresentationController\b' 'Assets/_Project/Runtime/Features' 'Assets/_Project/Runtime/Infrastructure' -g '*.cs'
rg -n '\bRunSpecialNpcChoicePresenter\b|\bRunSpecialNpcChoiceAnchorFollower\b' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n '\bEmoteController\b' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n 'using TMPro|TMP_Text|TextMeshPro|TextMeshProUGUI' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n 'using Unity\.Cinemachine|CinemachineCamera|CinemachineBrain|CameraBootstrap|CameraFollow|CameraCinematicWaitUtility' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n '\bPuddleShaderVisual\b|\bPuddleParticleVisual\b|\bPuddleBlobVisual\b' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n '\bItemDisplayVisualPresenter2D\b|\bItemDisplayVisualInstance2D\b|\bWorldDropSpritePresenter2D\b|\bLightningSpearDropLandingVisual2D\b' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n '\bWorldItemDropTweenAnimator\b|CreateDropLandingTween|using DG\.Tweening|DOTween|DOVirtual' 'Assets/_Project/Runtime/Features/Loot' 'Assets/_Project/Runtime/Features/Items/Weapons/WeaponDrop2D.cs' -g '*.cs'
rg -n 'using DG\.Tweening|DOTween|DOVirtual|DOScale|DOShake|DOLocal' 'Assets/_Project/Runtime/Features/Map/Shortcuts/DoorObject.cs' 'Assets/_Project/Runtime/Features/Monsters/Shadow/ShadowServant/GlobalVisionMaskController.cs' 'Assets/_Project/Runtime/Features/Monsters/Common/DeadsSkeleton/DeadsSkeleton.cs'
rg -n 'using DG\.Tweening|DG\.Tweening|DOTween|DOVirtual|DOScale|DOLocalRotate|DOMove|Tweener' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n 'SceneTransitionCoordinator|SceneFadeTransitionService|LoadingOverlayController|TimeScalePauseService' 'Assets/_Project/Runtime/Features' -g '*.cs'
rg -n 'CameraBootstrap|CameraFollow|CinemachineCamera|CinemachineBrain|CameraCinematicWaitUtility|using Cainos|using Unity\.Cinemachine' 'Assets/_Project/Runtime/Features/Player/Health/PlayerDeathReturnToHub2D.cs' 'Assets/_Project/Runtime/Features/Monsters/Spawning/RoomEnemyNavigationOverlay.cs' 'Assets/_Project/Runtime/Features/Player/Scene/PlayerHubSpawnPresentation2D.cs'
rg -n 'using Unity\.Cinemachine|CinemachineCamera|CinemachineBrain|CameraBootstrap|CameraFollow|CameraCinematicWaitUtility' 'Assets/_Project/Runtime/Features/Map/Shortcuts/LeverShortcut.cs'
rg -n 'using Unity\.Cinemachine|CinemachineCamera|CinemachineBrain|CameraBootstrap|CameraFollow|CameraCinematicWaitUtility' 'Assets/_Project/Runtime/Features/Dialogue/RunSpecial/RunSpecialNpcInteractor.cs' 'Assets/_Project/Runtime/Features/Tutorial/TutorialCombatIntroSequence.cs' 'Assets/_Project/Runtime/Features/Tutorial/HubIntroAfterDarkLordSequence.cs' 'Assets/_Project/Runtime/Features/Tutorial/TutorialBossEncounterSequence.cs'
rg -n 'Instantiate\(|Destroy\(|AddComponent<SpriteRenderer>|vfxPrefab|sustainVfx|triggerVfx' 'Assets/_Project/Runtime/Core' -g '*.cs'
rg --files -g '*.asmdef' -g '*.asmref' Assets Packages ProjectSettings
```

The final completion audit must also include Unity compile, generated solution build, asmdef dependency review, scene/prefab missing-script checks, ScriptableObject import review, and default `Assembly-CSharp`/`Assembly-CSharp-Editor` residual file audit.
