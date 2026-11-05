# TestScene live content catalog

On 2026-11-05, the saved scene had six weapon cards for five database weapons and 66 relic cards for 77 relics. Choosing a weapon used an index into a newer list. New actors, chests and event templates also needed a full arena rebuild.

## Changes

- [TestSceneBuilder](../../Assets/_Project/Editor/Tools/Testing/TestSceneBuilder.cs) discovers supported prefabs and event templates when Unity processes the TestScene copy. It connects one inactive event shell and keeps the Demon King companion and abandoned NPC exclusions.
- [Catalog authoring](../../Assets/_Project/Editor/Tools/Testing/TestSceneBuilder.CatalogGrid.cs) resizes the four existing grids and binds each card's label, sprite and target together.
- [Catalog cell](../../Assets/_Project/Runtime/UI/Testing/TestSceneCatalogCell.cs) stores that target. [Catalog grid](../../Assets/_Project/Runtime/UI/Testing/TestSceneCatalogGrid.cs) resolves the selected target and rebinds resized grids.
- [Control panel](../../Assets/_Project/Runtime/UI/Testing/TestSceneControlPanel.cs) passes the selected object to the inventory or actor spawner. Event selection now populates the room on demand from the chosen template, then uses the existing navigation, binding and cleanup paths.
- [Existing validation commands](../../Assets/_Project/Editor/Tools/Testing/TestSceneValidation.cs) enter Play through the same preparation path. Their catalog checks use current definitions and prefab identities.
- [TestScene structure map](../StructureMemory/TestScene.md) records the registration directories, scene-processing lifecycle and event-room ownership.

## Verification and handoff

- Checked generated UI and Editor project coverage, card-to-grant references, folder filters, scene-processing scope, event activation and arena restoration. The existing TestScene scene file already had local changes and was left alone.
- Generated Editor project MSBuild including UI passed. Unity Play Mode, automated tests and a standalone Player build were not run.
- Manual acceptance in the Editor remains open. Start TestScene, grant the first weapon and the last relic, then spawn and clear a newly imported prefab or event room. Check the granted definition, visible room contents and return to the arena.
