# Retired one-off UI/VFX acceptance tools

These files are archived source/evidence, outside Unity's Assets import tree. C#, metadata and scripts have a `.txt` suffix so they are not executable/discoverable tools. PNG source art retains its original extension. Nothing in this directory should be run automatically.

Retired with user approval on2026-11-01:

- Six presentation acceptance suites: atlas pixel equivalence, legacy/layered UI comparison, TMP refresh parity, mask migration comparison, monster HP raster/mesh migration, and puddle visual migration.82 cases total.
- Only two visual methods from CrimsonChargePlayModeTests: staged circle size/rotation and flight/fade presentation, eight parameterized cases. The full pre-cleanup source is in `Partial/`;14 gameplay/aim/range/cooldown/cancel cases remain active.
-17 fixed Editor authoring/capture/migration tools for approved stats/rewards, settings, title language, encyclopedia layout/skin, monster HP, puddle art, Crimson charge and render layers.
- Five fixed Aseprite preparation inputs/scripts and the settings-skin preservation script from Tools. Existing editable game art, actual assets, historical captures and reports are retained.

Source-relative paths are mirrored below this folder. Original bytes, GUIDs, hashes and destinations are recorded in the [manifest](../../../Docs/SessionLogs/2026-11-01-test-tool-cleanup/archive-manifest.json). [Scope, retained tools and static inspection](../../../Docs/SessionLogs/2026-11-01-test-tool-cleanup/README.md).

## Recovery

Restore only a needed group to its original manifest path, remove the extra `.txt` suffix, and restore each matching `.meta` unchanged. Restore dependencies together. Never replace a newer active file with the historical full Crimson snapshot; recover only the two removed methods if requested. Unity will import/recompile after a normal refresh. Restoring a test does not authorize executing it.
