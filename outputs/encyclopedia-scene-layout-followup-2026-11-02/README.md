# Encyclopedia item detail scene override follow-up — 2026-11-02

The user confirmed the settings repair but reported unchanged clipping in item details. Monster/Boss pages display correctly. The repair remains limited to the existing item-detail geometry.

## Cause and correction

The first repair removed the intermediate GlobalUIRoot override, but all 12 gameplay scenes still overrode the same content object's X to 285. Their inherited target fileID (3812683333260759081) differs from the standalone EncyclopediaUI fileID (7990481634182577215) and has no standalone record in GlobalUIRoot YAML. The prior direct-record lookup silently omitted it.

The correspondence follows the nested prefab instance 6488204919046368278 and agrees with all 35 explicitly serialized source pairs in GlobalUIRoot. After resolving that inheritance, the remaining differing item-content geometry in each enabled scene was exactly this X override. Each was removed, allowing X=0 to inherit through the already repaired intermediate prefab.

| Changed scene | Purpose |
| --- | --- |
| [PrototypeTutorialUpgradeScene.unity](../../Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity) | Remove the inherited ContentPanel X=285 override; inherit X=0. |
| [DarkLord_Tutorial.unity](../../Assets/_Project/Scenes/DarkLord_Tutorial.unity) | Remove the inherited ContentPanel X=285 override; inherit X=0. |
| [ProtoTypeHub.unity](../../Assets/_Project/Scenes/ProtoTypeHub.unity) | Remove the inherited ContentPanel X=285 override; inherit X=0. |
| [HeoMinSeok_Boss_Shadow.unity](../../Assets/_Project/Scenes/HeoMinSeok_Boss_Shadow.unity) | Remove the inherited ContentPanel X=285 override; inherit X=0. |
| [HeoMinSeok_Boss_Dragon.unity](../../Assets/_Project/Scenes/HeoMinSeok_Boss_Dragon.unity) | Remove the inherited ContentPanel X=285 override; inherit X=0. |
| [SangHyup_Boss_SlimeQueen.unity](../../Assets/_Project/Scenes/SangHyup_Boss_SlimeQueen.unity) | Remove the inherited ContentPanel X=285 override; inherit X=0. |
| [DemonkingCorridor.unity](../../Assets/_Project/Scenes/DemonkingCorridor.unity) | Remove the inherited ContentPanel X=285 override; inherit X=0. |
| [LeeJunmo_Boss_DemonKing.unity](../../Assets/_Project/Scenes/LeeJunmo_Boss_DemonKing.unity) | Remove the inherited ContentPanel X=285 override; inherit X=0. |
| [ProceduralShadowCorridor.unity](../../Assets/_Project/Scenes/ProceduralShadowCorridor.unity) | Remove the inherited ContentPanel X=285 override; inherit X=0. |
| [ProceduralDragonCorridor.unity](../../Assets/_Project/Scenes/ProceduralDragonCorridor.unity) | Remove the inherited ContentPanel X=285 override; inherit X=0. |
| [ProceduralSlimeCorridor.unity](../../Assets/_Project/Scenes/ProceduralSlimeCorridor.unity) | Remove the inherited ContentPanel X=285 override; inherit X=0. |
| [Grand Hall.unity](../../Assets/_Project/Scenes/Grand%20Hall.unity) | Remove the inherited ContentPanel X=285 override; inherit X=0. |

## Source inspection and limits

- [Exact changes and hashes](changes.json); [source inspection](source-inspection.json); [immediate before snapshots](before.zip).
- Each scene equals its before snapshot with exactly one four-line override record removed. Existing object IDs and all other scene data remain unchanged, including settings and creature UI. GlobalUIRoot and EncyclopediaUI hashes are unchanged by this follow-up.
- No runtime C#, art, metadata, references, gameplay/save state or project settings changed. No one-off executable tool or test source was created.
- Automated tests, Unity play/visual checks, compilation and native import were not executed. The original intermediate-prefab repair remains necessary; only its claim of complete scene coverage was incorrect. Actual item-detail visuals after this follow-up still need user confirmation.
- If the Editor still holds the earlier scene data, reload externally modified scene files rather than saving stale in-memory copies over the repair.

## Documentation

[SessionLog](../../Docs/SessionLogs/2026-11-02.md), [EncyclopediaStructure](../../Docs/StructureMemory/ScriptSystems/EncyclopediaStructure.md) and [ErrorLog](../../Docs/ErrorLog.md) record the missing inherited-ID coverage. The [first report](../ui-layout-normalization-2026-11-02/README.md) is annotated. DecisionLog is unchanged; no Architecture/Contracts or Presentation HTML impact.
