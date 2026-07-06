# Runtime UnityEditor Guarded Code Split

Status: resolved

## Current Problem

The assembly split static audit now finds no direct `UnityEditor` references under `Assets/_Project/Runtime`.

- `0` runtime files.
- `0` `UnityEditor` occurrences.

The previous guarded references were removed by routing authoring-only behavior through Core playback contracts and Editor assembly backends.

The audit no longer reports any runtime-root source files that are fully wrapped in `UNITY_EDITOR`.

- `PrewarmTraceRuntime` moved to `Assets/_Project/Editor/Build/Loading/PrewarmTraceRuntime.cs` and now registers as an Editor assembly backend for Core `PresentationPrewarmTracePlayback`.
- `SceneDomainEditorDirectStartPolicy` was reduced to runtime-safe development-start constants/predicates, renamed to `SceneDomainDevelopmentStartPolicy`, and moved to `SceneDomainDevelopmentStartPolicy.cs`.

## Why It Exists

Many runtime `MonoBehaviour` and `ScriptableObject` types keep authoring conveniences near their serialized runtime data:

- `OnValidate` auto-wiring and ID generation.
- SceneView gizmo labels and handle drawing.
- editor-only asset lookup or dirty marking.
- authoring-time default database generation.

During the asmdef split, these blocks were kept in place because moving them can alter prefab/scene authoring behavior and often requires custom inspectors, editor services, or asset postprocessors.

The guarded dependency groups removed during this cleanup:

- `UIManager.QuitGame()` now delegates through Core `ApplicationQuitPlayback`, and Editor `ApplicationQuitEditorBackend` handles Play Mode termination from the Editor assembly.
- Encyclopedia UI, Upgrade UI authoring helpers, loading/prewarm utilities, scene-domain direct-start support, loot/input defaults, merchant/door ID generation, selected gizmo label drawers, and `UpgradeNodeSO` now delegate dirty marking, persistent-object checks, prefab asset checks, editor time, delay calls, editor asset lookup/create/save/refresh, selection checks, and SceneView label drawing through Core `EditorAuthoringPlayback`, with Editor `EditorAuthoringEditorBackend` owning the concrete editor API calls.

## Target Shape

Runtime assemblies should contain only runtime behavior and runtime-safe serialized contracts.

Editor behavior should move to the `Editor` assembly through one of these patterns:

- Custom inspectors or property drawers for authoring-time buttons and validation.
- Editor utility classes that operate on selected runtime components/assets.
- Asset postprocessors or menu tools for bulk/default asset generation.
- Runtime-facing methods that expose safe data mutation hooks without referencing `UnityEditor`.

Runtime source may keep plain data validation that does not require `UnityEditor`, but direct `UnityEditor` API references should not remain in runtime folders for the strict final split.

## Risks

- Moving `OnValidate` logic can change when authored IDs, cached references, and default values are generated.
- Moving SceneView labels/handles can reduce designer feedback if custom editors do not preserve the exact workflows.
- Some editor-only setup currently relies on private runtime fields, so extraction may require small runtime-facing methods or serialized authoring contracts.
- Broad moves can break prefab/scene workflows even when player compilation remains green.

## Direct UnityEditor API Resolution

Resolved on 2026-07-05 for direct `UnityEditor` API references and fully wrapped editor-only runtime-root source files. Runtime `UNITY_EDITOR` conditionals remain classified by the static audit as information-only when they do not expose known UnityEditor API surface references.

## Verification

Required checks after cleanup:

- Static audit reports `0` direct runtime `UnityEditor` references.
- Static audit reports no runtime-root files fully wrapped in `UNITY_EDITOR`.
- Unity import/compile succeeds.
- Scene/prefab authoring workflows that previously depended on `OnValidate`, `SetDirty`, SceneView handles, or editor asset lookup are manually checked or covered by editor validation tools.

## Related Documents

- `Docs/StructureMemory/AssemblyDefinitionSplit.md`
- `Docs/SessionLogs/2026-07-05.md`
