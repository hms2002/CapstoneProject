# One-off test and tool retirement

Mode: Implementation. User explicitly selected retirement of one-off UI/effect/migration acceptance tests while retaining gameplay/save regressions, then included task-limited tools. Future automated tests require an explicit user request. No tests or Computer Use were executed after that instruction.

## Outcome

| Source inventory | Before | After |
|---|---:|---:|
| Project PlayMode cases |1215|1125|
| Project test C# files |75|69|
| Project cases including EditMode |1235|1145|

Counts are declarations inspected on disk, not a new Unity Test Runner discovery. Package-provided tests are outside this cleanup.

Six whole suites/82 cases were retired: `UiAtlasPixelEquivalencePlayModeTests`18, `UiRenderLayerAuthoredPlayModeTests`2, `UiTextMeshRefreshRegressionPlayModeTests`24, `UiRectMaskRegressionPlayModeTests`5, `MonsterHealthBarMeshPlayModeTests`13, `PuddleProjectileVisualPlayModeTests`20. These cover accepted presentation/optimization/migration behavior rather than damage, save data or dungeon rules. Their automatic coverage is intentionally relinquished to manual review; this does not claim the underlying runtime features are unused.

`CrimsonChargePlayModeTests.cs` retains its fixture and14 gameplay/aim/range/cooldown/cancellation checks. Only `MagicCircles_GrowInTwoStages_AndMatchReleasedRange` (5 cases) and `FlightWarning_HoldsSizeUntilImpact_ThenFadesWithoutShrinking` (3 cases) were removed. A complete before-copy is retained for reference.

17 Editor tools were retired as complete dependency groups: Crimson authoring/capture; stats/reward authoring/capture; settings authoring/capture; title-language authoring/capture; encyclopedia initial UI/localization/layout/skin authoring; monster HP mesh migration/pixel validation; puddle pixel authoring/capture; render-layer migration. Five fixed Aseprite preparation inputs/scripts under Tools/Art and SettingsSkinScopeValidation.py were also retired.23 tool/input files total, excluding their metadata.

All52 moved source/metadata/input files remain byte-identical in [the archive](../../../Tools/Archive/2026-11-01-one-off/README.md). [Exact source/destination/hash manifest](archive-manifest.json), [full inventory and selected types](inventory-plan.json). No scene, prefab, gameplay data, runtime implementation, shared build pipeline, asmdef, package, ProjectSettings or existing asset GUID was edited by this cleanup.

## Retained

- All gameplay/save EditMode tests, discovery persistence, input/state rules, combat/damage/cooldown/burn, inventory/relics, dungeon generation/travel/navigation and scene smoke checks.
- Mixed encyclopedia tests remain because they cover discovery/progress as well as visuals. Generic UI layout ownership, input cleanup, status lifecycle and reusable performance benchmarks remain; visual naming alone is not a retirement reason.
- Reusable frame/CPU/GPU profiling, UiBatchingValidationRunner/Live/StructuralScenarios, MonsterHealthBarGpuValidation, common-monster generators/validators, localization pipeline, TestScene builder/trace isolation, weapon rig and room authoring tools. They remain usable only when requested, rather than being automatically invoked.
- Existing history reports, evidence and scripts already under dated outputs/SessionLogs remain as historical artifacts; they are not Unity menu/test registrations.

## Static inspection only

- No incoming references to the23 retired top-level C# types from retained Assets C# sources; searched both identifiers and reflection strings. No retired script GUID references in serialized prefabs/scenes/assets/materials/controllers. No incoming references from retained Tools scripts.
- Every moved file's SHA256 matches its pre-move source. Crimson's remaining source is the before-copy with exactly the two methods removed. Other test sources were not edited by this cleanup.
- Final [source/filesystem inspection](static-inspection.json):52 archive hashes match, removed source paths are absent, Crimson's exact two-method removal is confirmed, and counts are69 sources/1125 PlayMode/1145 total project declarations. New report/archive navigation links resolve. This is static inspection, not test execution.
- Script files and their metadata moved together, preserving restoration identity. No generated csproj was hand-edited. Unity compilation/discovery after retirement and MSBuild were not run: the user stopped Computer Use, and generated projects still require Unity's normal refresh after removals. Prior Crimson build evidence does not cover this cleanup.
- Native PlayMode was already stopped after the pre-cleanup trace-lock failure. User Escape then ended Computer Use. The existing PrewarmTrace IO1224 problem is Suggested Later; no recorder workaround or unrelated repair was added.

## Project memory

AGENTS.md records explicit-request-only automated tests and no unsolicited disposable test/tool creation. The existing StructureMemory maps link the archive instead of presenting retired commands as current entry points. SessionLog and DecisionLog updated; ErrorLog already documents the observed prior traps and is unchanged. Doc Impact Check: SessionLog + StructureMemory + DecisionLog. No Architecture/Contracts edits or Presentation HTML stale candidate.
