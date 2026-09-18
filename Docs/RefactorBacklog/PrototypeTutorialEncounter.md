# Prototype tutorial encounter compatibility

- Status: proposed
- Problem: PrototypeTutorialUpgrade retains the old seven-dummy branch when skillMonsterPrefab is unassigned, alongside the new four-warrior encounter. The previous dummy scene instances remain inactive.
- Why: existing isolated mission/dash regressions use the original minimal fixtures; this slice replaces the playable scene without mixing in their wholesale migration.
- Target: migrate remaining fixtures to the real encounter contract, then remove the unassigned-prefab branch and inactive dummy instances together.
- Trigger: the real encounter passes the full scene playthrough and becomes the accepted tutorial baseline.
- Risks: removing serialized references prematurely can invalidate older fixtures or obscure regression coverage for held combos, skill counting and dash timing. Do not reactivate dead Mob instances as a replacement for spawning new ones.
- Related: [TutorialSupportStructure](../StructureMemory/ScriptSystems/TutorialSupportStructure.md), [session log](../SessionLogs/2026-09-18.md).
- Tilemap migration follow-up (2026-09-18): the 22 old OuterWall boxes are retained disabled alongside the blockout reference geometry. The old layout validator models those rectangles and the old encounter validator assumes the initial tile palette; replace those acceptance checks with live painted-map geometry coverage before removing the reference blockout. New tilemap structure/native validators cover actual collider generation and paint/erase updates.
