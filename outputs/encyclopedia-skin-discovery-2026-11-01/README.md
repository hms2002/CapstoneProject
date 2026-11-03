# Encyclopedia skin and discovery integration

User-approved revision 6 is integrated into the actual encyclopedia prefab. Unencountered Monsters/Bosses and unacquired Weapons/Relics remain selectable as black silhouettes; their names become `???` and their details are hidden. Encounter/acquisition reveals them. NPC/Consumable visibility is unchanged.

## Presentation

- Beveled pixel cards, frames, buttons, dividers, reward panels and scroll parts use authored sprites.
- Black transparent fades appear only on Boss cards/details. Names have no separate nameplate.
- The Boss frame aligns with the fade bounds and has no corner ornament. Title/portrait inset prevents crown/name overlap.
- Full view uses four outward arrows. A moving window displays at most five portrait indicators without discarding any portraits.
- The original book keeps its dimensions, palette and sprite metadata; matching stationary inner-page corner ornaments are removed.
- The unknown creature detail uses an authored silhouette-only root. It exposes no story, kills, affection, rewards, portrait navigation or full view. Weapon/Relic silhouettes appear in the existing item grid; the authored item detail has no header icon and shows only `???` when unknown.

## Discovery and saves

- Actual successful inventory mutations/restores record Weapon/Relic IDs. Spawn-table unlock lists are not acquisition history.
- Monster room entry and subsequent spawns in that entered room record actual encounters. Recognition of the player covers preplaced/summoned Monsters. Mere spawning outside an encounter does not reveal a species. Tutorial/Test Monster encounters are excluded.
- Boss discovery reads existing encounter/victory/defeat records. Existing Monster kills count as prior encounter evidence.
- `GameData.encyclopediaDiscoveryData` stores confirmed profile progress. `GamePlayData.pendingEncyclopediaDiscoveryData` stores current-run progress, committed/cleared through the existing run lifecycle. Profile loading also clears pending discovery. Hub acquisitions use the existing deferred-save request.
- Old saves safely initialize missing fields. Historical item acquisitions cannot be reconstructed from spawn unlocks; actual future acquisition/inventory restoration records them. Valid unknown IDs survive normalization. Active-run quit behavior remains the project's existing discard policy.

## Changed files and reasons

Paths below are relative to the project root.

| Files | Reason |
| --- | --- |
| `Assets/_Project/Art/Sprites/UI/Encyclopedia/PixelSkin/` and metadata | Native Aseprite sources, approved exports, import/slice definitions and maintenance notes |
| `Assets/_Project/Art/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites/Inventory Book SpriteSheet.png` | Remove inner-corner ornaments using original colors; no metadata change |
| `Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab` | Authored sprite decoration, five dots, four-arrow icon and unknown detail root |
| `Assets/_Project/Editor/Tools/Encyclopedia/EncyclopediaSkinAuthoring.cs` (+ meta) | Repeatable scoped import/prefab authoring with original font and unrelated serialized-record preservation |
| `Tools/Art/remove_encyclopedia_book_corners.lua` | Native Aseprite exact-template edit of book-sheet stationary corners |
| `Assets/_Project/Runtime/Core/Save/EncyclopediaDiscoveryProgress.cs` (+ meta) | Additive discovery DTO, projection, recording, normalization and pending commit/reset |
| `Assets/_Project/Runtime/Core/Save/GameData.cs`, `GamePlayData.cs` | Profile and run discovery fields |
| `Assets/_Project/Runtime/Infrastructure/Save/GameDataManager.cs`, `GamePlayDataManager.cs`, `RunSessionProgressCommitPolicy.cs` | Existing load/save normalization, profile reset and run commit integration |
| `Assets/_Project/Runtime/Features/Items/Weapons/Inventory/WeaponInventory2D.cs`, `Features/Items/Relics/RelicInventory.cs` | Record actual owned items after successful inventory changes |
| `Assets/_Project/Runtime/Features/Encyclopedia/MonsterEncyclopediaIdentity.cs`, `MonsterEncyclopediaProgress.cs`, `Features/Monsters/Spawning/MonsterSpawnRoomGroup.cs` | Actual Monster encounter producers |
| `Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaCharacterEntryView.cs`, `EncyclopediaCreatureTab.cs`, `EncyclopediaCreatureDetailView.cs` | Creature silhouettes, gated details, Boss-only fades and state refresh |
| `Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaEntryButton.cs`, `EncyclopediaItemTab.cs`, `EncyclopediaItemRightPage.cs` | Item silhouettes, hidden details and profile/discovery refresh |
| `Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaPortraitUtility.cs` | Centered moving window of up to five indicators |
| `Assets/_Project/Tests/EditMode/EncyclopediaDiscoveryEditModeTests.cs` (+ meta), `Tests/PlayMode/Procedural/EncyclopediaCreaturePlayModeTests.cs` | Save compatibility/pending reset, real acquisition producers, hidden/revealed transitions, native capture and retained UI regressions |
| `Docs/ActiveTasks/encyclopedia-skin-discovery.md`, `Docs/TaskIndex.md`, `Docs/SessionLogs/2026-11-01.md` | Approved scope and implementation/verification receipt |
| `Docs/StructureMemory/EncyclopediaCreatureTabs.md`, `Docs/StructureMemory/ScriptSystems/EncyclopediaStructure.md`, `Docs/DecisionLog.md`, `Docs/ErrorLog.md` | Current ownership map, discovery semantics and known trace-file test isolation |

## Validation

- Unity 6000.4.2f1 compiled/imported and applied the authored prefab successfully (`authoring-final-unity.log`, exit 0).
- [EditMode 15/15](editmode-results.xml): legacy JSON, normalization, unknown-ID retention, idempotent commit, run reset and existing species-kill regressions.
- [PlayMode regression 28 passed](playmode-isolated-results.xml) plus [final discovery transition 1/1](playmode-discovery-final-results.xml): all 29 unique selected cases now pass. The final rerun targeted only the corrected discovery test; unchanged passing regressions were not rerun again. It checks actual inventory success/rejection, no spawn-only reveal, hidden/revealed Monster/Boss/Weapon/Relic data, five portrait controls, full-view blocking, locale changes, profile switching, NPC fades, and safe save requests. Earlier full-suite results retain the obsolete optional-reference test assertions and the separately identified editor trace-lock failures for traceability.
- [MSBuild 3/3](msbuild-results.json): Editor, CorePerformanceRegressionEditModeTests and ProceduralPlayModeTests all exit 0. Generated project files contain the new sources; they were not hand-edited.
- [Preservation 10/10](preservation-report.json): all 2,583 existing prefab record IDs, unrelated records, fonts/materials and existing metadata preserved; approved source exports match byte-for-byte. Original book palette/dimensions retained with 15,399 changed pixels limited to114 reported stationary-corner patches. C# diff whitespace clean; Unity-native empty YAML strings are retained.
- [Saves 6/6 preserved](save-preservation.json): Editor initialization added the new empty discovery field to Slot 1's two storage copies; both were restored byte-for-byte from the pre-task backups after Unity exited. All six original files now match their original SHA256. No real discovery progress was changed. Test-generated prewarm trace sessions were also removed by restoring that initially clean file.
- Native Monster/Boss and Weapon/Relic silhouettes were visually inspected. Existing regression captures also cover book reveal/input/layout behavior.

Native captures: [Boss discovered](native/boss-revealed.png), [Boss unknown](native/boss-locked.png), [Monster discovered](native/monster-revealed.png), [Monster unknown](native/monster-locked.png), [Weapon unknown](native/weapon-locked.png), [Relic unknown](native/relic-locked.png).

Preservation check: `verify_preservation.py` (Python + Pillow, read-only source/art analysis). Temporary Pillow dependency was installed under `%TEMP%/codex-encyclopedia-validation-libs`; no project package changes.

## Limits and follow-ups

- Native automated PlayMode fixtures were used; a full hub-to-combat manual acceptance pass and Player build are not executed.
- Old-save item acquisition history was never stored and is not guessed.
- The old creature builder still describes the pre-skin layout. Apply `EncyclopediaSkinAuthoring.Apply` last after using it.
- Existing editor `PrewarmTraceRuntime` Windows mapped-file I/O errors are separate from this change. UI batch tests use the existing `-uiBatchValidationTests` isolation option; production trace code and error handling are unchanged.
- Existing localization/TMP warnings in simplified regression fixtures are retained. Native captures wait for the project's real font bindings.
- Doc impact: SessionLog + StructureMemory + DecisionLog + existing ErrorLog entry. Discovery ownership is an Architecture/Contracts promotion candidate. Those documents and Presentation HTML are unchanged; no Presentation HTML stale candidate was identified.
