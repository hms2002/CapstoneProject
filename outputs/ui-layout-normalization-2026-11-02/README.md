# Settings and encyclopedia layout normalization — 2026-11-02

Mode: Implementation, authorized by the user's request to normalize the layouts after the screenshot investigation.

Follow-up status: the user confirmed settings are fixed, but item details remained clipped. This first repair missed scene overrides addressing inherited nested object IDs absent from GlobalUIRoot's explicit YAML records. The [follow-up](../encyclopedia-scene-layout-followup-2026-11-02/README.md) removes one remaining ContentPanel X=285 override from each of the 12 scenes. The earlier source-inspection result covers only its direct-ID lookup scope, not the complete nested inheritance chain; it did not establish a complete runtime fix.

The settings rows were authored with distinct top-anchored positions, but scene overrides forced them to bottom anchors at Y=0. Footer and key-mapping confirmation controls also retained conflicting geometry. Separately, the nested EncyclopediaUI instance in GlobalUIRoot shifted ContentPanel to X=285, overriding its standalone prefab's X=0.

Removed 634 conflicting geometry overrides from 13 files: 633 across the 12 enabled scenes containing the main GlobalUIRoot, and one in the main prefab. The existing authored layout now supplies those values. No runtime layout workaround was added.

## Changed assets

| File | Removed overrides | Reason |
| --- | ---: | --- |
| [GlobalUIRoot.prefab](../../Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab) | 1 | Inherit encyclopedia detail X=0 instead of the nested X=285 override. |
| [PrototypeTutorialUpgradeScene.unity](../../Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity) | 53 | Inherit settings row spacing, footer positions and key-mapping confirmation layout from the main UI prefab. |
| [DarkLord_Tutorial.unity](../../Assets/_Project/Scenes/DarkLord_Tutorial.unity) | 50 | Inherit settings row spacing, footer positions and key-mapping confirmation layout from the main UI prefab. |
| [ProtoTypeHub.unity](../../Assets/_Project/Scenes/ProtoTypeHub.unity) | 53 | Inherit settings row spacing, footer positions and key-mapping confirmation layout from the main UI prefab. |
| [HeoMinSeok_Boss_Shadow.unity](../../Assets/_Project/Scenes/HeoMinSeok_Boss_Shadow.unity) | 53 | Inherit settings row spacing, footer positions and key-mapping confirmation layout from the main UI prefab. |
| [HeoMinSeok_Boss_Dragon.unity](../../Assets/_Project/Scenes/HeoMinSeok_Boss_Dragon.unity) | 53 | Inherit settings row spacing, footer positions and key-mapping confirmation layout from the main UI prefab. |
| [SangHyup_Boss_SlimeQueen.unity](../../Assets/_Project/Scenes/SangHyup_Boss_SlimeQueen.unity) | 53 | Inherit settings row spacing, footer positions and key-mapping confirmation layout from the main UI prefab. |
| [DemonkingCorridor.unity](../../Assets/_Project/Scenes/DemonkingCorridor.unity) | 53 | Inherit settings row spacing, footer positions and key-mapping confirmation layout from the main UI prefab. |
| [LeeJunmo_Boss_DemonKing.unity](../../Assets/_Project/Scenes/LeeJunmo_Boss_DemonKing.unity) | 53 | Inherit settings row spacing, footer positions and key-mapping confirmation layout from the main UI prefab. |
| [ProceduralShadowCorridor.unity](../../Assets/_Project/Scenes/ProceduralShadowCorridor.unity) | 53 | Inherit settings row spacing, footer positions and key-mapping confirmation layout from the main UI prefab. |
| [ProceduralDragonCorridor.unity](../../Assets/_Project/Scenes/ProceduralDragonCorridor.unity) | 53 | Inherit settings row spacing, footer positions and key-mapping confirmation layout from the main UI prefab. |
| [ProceduralSlimeCorridor.unity](../../Assets/_Project/Scenes/ProceduralSlimeCorridor.unity) | 53 | Inherit settings row spacing, footer positions and key-mapping confirmation layout from the main UI prefab. |
| [Grand Hall.unity](../../Assets/_Project/Scenes/Grand%20Hall.unity) | 53 | Inherit settings row spacing, footer positions and key-mapping confirmation layout from the main UI prefab. |

## Source inspection and preservation

- [Exact removed properties and hashes](changes.json), [source inspection](source-inspection.json).
- Every changed file equals its immediate before snapshot with only the listed property override records removed. All existing serialized object IDs remain present; hierarchy and object references are untouched.
- `git diff --check` against HEAD reports pre-existing Unity-generated empty-value trailing spaces in GlobalUIRoot's earlier uncommitted changes. This repair adds no asset lines; exact comparison with the immediate backup confirms that those lines were preserved rather than introduced or reformatted.
- No conflicting geometry overrides remain in the repaired scope. Inactive scrollbars, disabled/legacy scenes and alternate UI copies are outside this repair. TitleScene's separate authored layout required no change.
- C#, sprite/font/material data, localization, metadata, gameplay/save state and ProjectSettings were not changed. No test source or one-off executable tool was created.
- This is source/reference/diff inspection only. Automated tests, Unity play-mode checks, native rendering/import, compilation and Player builds were not executed. It is not a runtime visual-success claim.

## Editor handoff

Reload the externally changed scene data if the Editor still has the previous version loaded; do not overwrite the repaired files with an older in-memory scene. User manual checks: settings rows and footer alignment, immediate key-page switch and confirmation dialog, and acquired Weapon/Relic details fitting the right page. Existing settings/save/discovery behavior should remain unchanged because this repair only removes geometry overrides.

## Backup and documentation

[Before snapshot](before.zip) contains the 13 files immediately before this repair, including all prior uncommitted work. Use the property receipt for scoped rollback rather than replacing later work with whole historical files.

[SessionLog](../../Docs/SessionLogs/2026-11-02.md), [SettingsPanelLayout](../../Docs/StructureMemory/SettingsPanelLayout.md), [EncyclopediaStructure](../../Docs/StructureMemory/ScriptSystems/EncyclopediaStructure.md) and [ErrorLog](../../Docs/ErrorLog.md) record the repair. DecisionLog is unchanged. No Architecture/Contracts or Presentation HTML impact was identified.
