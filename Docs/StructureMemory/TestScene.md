---
status: active
authority: structure-memory
category: test-scene
last_reviewed: 2026-10-07
---

# TestScene

## Purpose

Scene-local gameplay test arena and reproducible Lightning Spear placement measurements. This is a context map, not an Architecture/Contracts replacement.

## Entry Points

- [TestScene](../../Assets/_Project/Scenes/Tests/TestScene.unity): open directly and enter Play Mode.
- [TestSceneBuilder](../../Assets/_Project/Editor/Tools/Testing/TestSceneBuilder.cs): `Tools > Testing > Build TestScene` and `Validate TestScene`.
- [TestSceneControlPanel](../../Assets/_Project/Runtime/UI/Testing/TestSceneControlPanel.cs): saved references on `TestSceneCanvas`, separate from the existing `GlobalUIRoot` prefab instance.
- [TestSceneValidation](../../Assets/_Project/Editor/Tools/Testing/TestSceneValidation.cs): batch Editor compile/author/Play Mode controls exercise and screenshot capture.

## Current Structure

- `Arena_30x30`: 900 floor cells and perimeter walls, authored Grid/Tilemap.
- `ArenaRoom`: room area and existing MonsterSpawnRoomGroup.
- Existing PlayerSpawner/PlayerSpawnPoint and GlobalUIRoot; existing scene navigation installer.
- Scene-authored BossCam and a test-only Demon King prefab bundling its authored EgoSword companion; original boss assets are unchanged.
- TestSceneCanvas: scrollable test commands/status, a separate toggle button, and a centered catalog window.
- [TestSceneCatalogGrid](../../Assets/_Project/Runtime/UI/Testing/TestSceneCatalogGrid.cs) projects four authored 6-column grids (weapons, relics, monsters, bosses). [TestSceneCatalogCell](../../Assets/_Project/Runtime/UI/Testing/TestSceneCatalogCell.cs) handles card clicks, icon/name display and the selected highlight. The grid owns only UI selection indices; the control panel dispatches grant/spawn requests to existing gameplay owners.
- Category tabs and side-panel buttons open the same catalog window. Its footer provides weapon-slot or monster-count options and an explicit execute button. Boss generation always requests one and replaces previous test bosses/companions; natural gameplay phase splitting remains with the original boss logic. The four content selectors no longer use dropdowns; chest/event/reward/slot selectors retain their existing dropdown UI.
- The central footer projects the control panel's latest command result. Actor cards use authored EnemyName labels; duplicate names include the prefab identifier so entries remain distinguishable.
- [Catalog authoring](../../Assets/_Project/Editor/Tools/Testing/TestSceneBuilder.CatalogGrid.cs) saves every card/icon in TestScene through Unity Editor APIs. Item icons come from ItemDatabase definitions; actor icons come from each prefab's authored body SpriteRenderer, avoiding shadow/companion renderers. No runtime UI hierarchy is created.
- Builder catalogs: ItemDatabase weapons/relics, six boss prefabs (including phase-two bodies), 21 Mob prefabs, six chest prefabs, and 16 current event templates. Abandoned RunSpecialNpc construction/teleport templates are excluded.
- Event templates become test-only prefab snapshots under [EventRooms](../../Assets/_Project/Prefabs/Testing/EventRooms/). Rebuild after changing a source template.

## Ownership And Lifetime

- Gameplay state remains with existing weapon/relic inventories, RunLevelProgression and RunLevelRewards.
- Test controls own created fixture instances and undo receipts. The Canvas/controller stays enabled when the child panel is hidden.
- The catalog controller stays on `CatalogHost` when its child overlay closes, preserving category selections. The open modal blocks test combat inputs; hiding the test panel or opening ordinary level-reward UI closes the catalog overlay. Cell and command listeners detach on disable.
- Successful actor spawning closes the catalog and releases test-owned input gates. Test boss instances disable BossEncounterDirector/BossTalkManager and BossDeathPresentation; FinishEncounterIntro runs in the spawn frame before Start/FSM, followed by BeginCombatEncounter with the player. Production prefab authoring/normal-scene cinematic behavior remains unchanged. Disabling an already enabled encounter controller uses its normal OnDisable release path for input/protection/timer/state locks.
- Event testing replaces arena terrain and room boundary, switches navigation Grid/Ground, and restores them on clear. Existing procedural runtime feature binding receives the event template/group/area.
- Event snapshots retain visual tile layers and authored composite child poses. SafetyTracker tilemap references are refreshed whenever test terrain is replaced or removed.
- Player reset uses existing PlayerSpawner; it retains levels/persistent selections and resets equipment/instant rewards.
- [TestSceneProfileStore](../../Assets/_Project/Runtime/UI/Testing/TestSceneProfileStore.cs) keeps a disposable profile copy. [TestSceneRunStore](../../Assets/_Project/Runtime/UI/Testing/TestSceneRunStore.cs) forwards normal volatile run APIs but intercepts terminal commits. Original backends/run state are restored on shutdown.
- GamePlayDataManager.EndRun respects an alternate live RunSessionStore backend, covering legacy direct callers. Its ordinary registered-manager path is unchanged.
- Test-owned travel/ending, death-return and timer-return paths are guarded for arena iteration.
- [Editor trace isolation](../../Assets/_Project/Editor/Tools/Testing/TestScenePresentationTraceIsolation.cs) temporarily replaces prewarm trace collection and suspends its disk-flush callbacks during TestScene play, then restores them. Test fixture spawns do not overwrite the existing project-wide prewarm history. It depends on the current Editor trace callback/field names and should be updated if that tool is refactored.

## Reward Removal

- Persistent effect selection removal disposes/rebuilds existing effect handles.
- [TestSceneRewardReceipt](../../Assets/_Project/Runtime/UI/Testing/TestSceneRewardReceipt.cs) records instant resource changes and only reverses them if the affected values still match the recorded post-selection state.
- Later combat/inventory changes can make instant undo unsafe. The UI reports that case; player reset is the recovery path.
- Removing selections does not refund player levels or pending reward choices.

## Placement Measurement

- [PlacementTools](PlacementTools.md): reusable candidate sources, exact nearest heaps, geometry/session reservations and flow operations. The old LightningSpearPlacementSearch is retained only by Editor snapshots.
- RuntimeState remains the live mark owner. Reusable physical result buffers preserve all hits; null-mark compaction happens separately from spacing checks.
- The [placement structure map](LightningSpearPlacement.md) describes the internal Planner's synchronous reservations/search and WorldQuery's map/physics buffers. E copies the borrowed plan before yielding.
- [Editor baseline partial](../../Assets/_Project/Runtime/Features/Items/Weapons/Abilities/LightningSpearRuntimeState.PlacementBenchmark.cs) retains the previous placement implementation under `UNITY_EDITOR` only.
- `-lightningCompareStructure` selects the frozen **optimized** implementation before responsibility separation; the default comparison still uses the earlier full-sort legacy implementation.
- [Benchmark entry](../../Assets/_Project/Editor/Tools/Validation/LightningSpearPlacementBenchmark.cs): `Run` for the equipped spear and `RunFixtureSuite` for seeded controlled cases, in Play Mode.
- Reports live under `Library/LightningSpearPlacementBenchmark`; validation/screenshot under `Library/TestSceneValidation`. These are generated, ignored outputs; retain portfolio summaries separately.
- `Tools > Testing > Lightning Spear` exposes the equipped-weapon benchmark and fixture suite (Play Mode required).
- Geometry caching is used for targeted/nearest searches; ordinary random samples avoid unnecessary cache inserts.
- Preserved behavior: RNG sampling, exact preferred target positions, nearest distance/x/y ordering, round-robin allocation, spacing, attempt cap, and spawn-request copy before coroutine execution.
- Heap progress reuses capacity but can retain `O(K*T)` candidate storage. This is a memory-for-repeat-work tradeoff, not an unconditional space-complexity improvement.
- The internal soft-wall layer passage policy is a future authoring/gameplay slice; the optimization keeps current layer masks.

## Known Pitfalls / Follow-ups

- Do not hide/disable the stable controller to collapse UI: that releases sandbox ownership. Use TogglePanel.
- Catalog cards are authored snapshots. After changing ItemDatabase or actor catalogs, rebuild the test scene or update its saved grid cards/references; scene validation detects mismatched counts and missing icons.
- Event-room testing covers authored contents and local interaction, not inter-scene route completion.
- Initial existing app bootstrap can load/migrate development slot zero before the test controller installs its scoped backends.
- Performance counters cover synchronous placement only, excluding prefab creation/rendering. Fixture results are not FPS claims.
- The allocation-byte API did not detect a 4096-byte canary in the measured Editor. Byte samples are marked unavailable, not zero; use a functioning Profiler allocation recording before making GC/byte claims.
- Preprocessing and heap buffers require comparing cold and warm behavior; a zero-result fixture is not a valid performance case.
- No Architecture/Contracts promotion is currently required. A shared placement boundary across production features would be a later promotion candidate.
