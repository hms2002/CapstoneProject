# Procedural Dungeon Room Pipeline

Status: early v0 structure memory. This document is a navigation aid, not an Architecture or Contracts source of truth.

## Purpose

This pipeline turns reusable floor/wall/object room templates into a connected runtime Tilemap dungeon. Room authoring, layout decisions, and runtime realization stay separate so monster, chest, portal, prop, later trap/reward, and theme-specific door data do not couple the layout algorithm to a theme.

## Current Flow

1. `RoomPieceEditorWindow` creates or reloads a room authoring copy.
2. `RoomPieceAuthoring` owns room metadata and Floor/Wall Tilemap references; `RoomSocketAuthoring` marks the start cell and outward direction of a two-cell logical boundary socket; `RoomObjectAuthoring` marks one prefab placement.
3. The editor bakes the authoring copy into `RoomTemplateSO`.
4. A theme-specific `RoomThemeLibrarySO` groups the templates available for one dungeon/boss theme.
5. `DungeonLayoutAssembler` uses a local `System.Random` Seed to choose and place templates.
6. Each new room aligns an equal-width, opposite-facing two-cell socket beyond a configurable straight-corridor span, then rejects room/corridor reservation overlap with rectangular `RectInt` checks.
7. `DungeonRoomBuilder` paints every room with all socket Walls closed, creates one span-sized physical blocker per logical socket, opens only connected endpoints, paints the two-cell-wide straight corridor and side Walls, then creates one non-permanent Door at each connected socket endpoint.
8. For every room containing a Monster placement, the builder creates a full-bounds rectangular `MonsterRoomArea2D`, a `MonsterSpawnRoomGroup`, and a separate `RoomEncounterEntryTrigger2D` inset one cell from the boundary Walls. Each connected endpoint door owned by that room receives the existing `RoomDoorMonsterKillLock` behavior.
9. The builder realizes each room's Grid-relative object placements under a dedicated generated root. It creates non-monsters first, resolves each monster's optional target chest Placement Id, and passes the room area, source room group, and resulting `ChestMonsterKillLock` through the existing `MonsterSpawnRequest` path. Chests, portals, and props are instantiated directly.
10. `DungeonGenerator` coordinates one assembly/build request and retains the last `DungeonLayoutResult`.

## Key Files

- `Assets/_Project/Runtime/Features/Map/Procedural/RoomTemplateSO.cs`
  - Baked layout/build data, typed object placement data, room type, socket data, and shared two-cell socket geometry rules.
- `Assets/_Project/Runtime/Features/Map/Procedural/RoomObjectAuthoring.cs`
  - Scene authoring marker that converts a prefab Transform into room Grid-relative placement data.
- `Assets/_Project/Runtime/Features/Map/Procedural/RoomThemeLibrarySO.cs`
  - Theme catalog and room-role candidate queries.
- `Assets/_Project/Runtime/Features/Map/Procedural/DungeonLayoutModels.cs`
  - Room placement, socket connection, and complete/partial result models.
- `Assets/_Project/Runtime/Features/Map/Procedural/DungeonLayoutAssembler.cs`
  - Deterministic weighted selection, socket alignment, and overlap rejection.
- `Assets/_Project/Runtime/Features/Map/Procedural/DungeonRoomBuilder.cs`
  - Runtime Tilemap/object realization, connected socket wall removal, and generated door/blocker/object lifecycle.
- `Assets/_Project/Runtime/Features/Map/Procedural/DungeonGenerator.cs`
  - Runtime orchestration entry point.
- `Assets/_Project/Editor/Tools/Dungeon/RoomPieceEditorWindow.cs`
  - Room creation, reload/edit, validation, and bake/apply workflow.
- `Assets/_Project/Editor/Tools/Dungeon/ProceduralDungeonSceneInstaller.cs`
  - Reproducible prototype/theme data creation, isolated scene wiring, Hub route connection, loading-manifest refresh, UI synchronization, and batch verification entry point.
- `Assets/_Project/Scenes/ProceduralDungeonV0Test.unity`
  - Verified corridor-derived integration scene; generates six rooms on Start.
- `Assets/_Project/Scenes/Procedural{Shadow|Dragon|Slime|Demonking}Corridor.unity`
  - Production-route candidates that share the generator shell while referencing one boss-specific room library each.

## Ownership And Lifecycle

- `RoomThemeLibrarySO` and `RoomTemplateSO` are authored assets and do not own runtime state.
- `DungeonLayoutAssembler` is stateless. The same library ordering, template data, settings, and Seed produce the same selection sequence.
- `DungeonLayoutResult` owns the generated placement/connection snapshot for one generation request.
- `DungeonGenerator` owns the last result reference but not the library assets or Tilemaps.
- `DungeonRoomBuilder` owns writes to its explicitly assigned Floor/Wall Tilemaps, generated-door root, generated-socket-blocker root, generated-object root, and generated-room-encounter root. Each build clears all five generated content categories before rebuilding.
- Runtime Grid, Tilemaps, renderers, colliders, and generator objects must be authored in a scene or prefab; this pipeline does not create them at runtime.

## V0 Rules

- One theme is represented by one `RoomThemeLibrarySO` rather than a theme field on every room.
- A usable template has positive finite selection weight, valid bounds, and at least one valid boundary socket.
- Generation requires a usable `Start` room. When `includeBossRoom` is enabled, the final placement requires a usable `Boss` room.
- Intermediate candidates exclude `Start`, `Boss`, and `Exit` room types.
- Rooms are not rotated.
- Collision uses room bounds plus each straight corridor's reserved Floor-and-side-Wall rectangle.
- Each logical socket has width `2`. Up/Down sockets extend right from `localCell`; Left/Right sockets extend upward from `localCell`.
- Compatible sockets must have opposite directions and equal width. Their start-cell distance is the configured corridor length plus one boundary step; v0 supports only a straight fixed-width corridor, not bends or pathfinding.
- Both cells of every socket must contain Floor and Wall data. Runtime realization begins with the logical socket closed by both Wall tiles and one span-sized physical blocker; only connected endpoints remove all closure.
- One connection creates two doors: one at each room socket endpoint, not one per socket cell. Each door sits between the centers of that endpoint's two-cell span.
- Left/right connections rotate the shared Door prefab 90 degrees; up/down connections keep its default rotation.
- Generated doors are `Normal` and non-permanent, with deterministic run-local IDs derived from Seed and connection index.
- Generated doors begin open. A room containing at least one Monster placement owns one generated encounter group; when the player's body is fully inside its trigger, every connected endpoint door belonging to that room closes through `RoomDoorMonsterKillLock` and reopens after the group's tracked monsters are cleared.
- The generated entry trigger is inset one cell from all room bounds so the player must fully pass the socket-end Door before encounter entry. The full-bounds room area remains separate for monster containment and outside-room safeguards.
- A generated door lock receives the socket's exact inward direction. It closes only after every tracked monster's active non-trigger collider bounds are fully inside the room and fully past that door's center plane plus a small clearance; a monster straddling the doorway therefore keeps the relevant door open.
- `RoomBuildData.objectPlacements` stores `Prop`, `Monster`, `Chest`, or `Portal` plus prefab and Grid-relative transform data. A Monster can additionally store one target Kill Lock chest Placement Id. Placement cells must be inside room bounds and contain Floor.
- Placement IDs are unique within one room template. Runtime instance names combine the room placement ID and placement ID so repeated templates remain distinguishable.
- Play Mode `Monster` placements require an active `MonsterSpawner` and use its single-spawn request path. A linked chest is resolved before spawning and supplied through `MonsterSpawnRequest.LinkedChestKillLock`, so `SceneMonsterSpawnDirector` remains responsible for registration. Other kinds instantiate directly from their referenced prefab.
- A chest prefab owns its `ChestMonsterKillLock` behavior and presentation. The procedural pipeline never adds the component or selects a presentation prefab; room data chooses the complete prefab.
- `Prototype_Treasure_Sacrifice` stores one composite `Prop` prefab containing the existing `StatueShortcut`, its linked non-permanent `Locked` `DoorObject`, and the standard `TreasureChest`. The layout assembler sees only the Treasure room bounds and sockets; the prefab owns payment, animation, opening, and reward behavior.
- The sacrifice room is an `18x12` left/right through-room. A horizontal internal Wall separates the upper reward alcove from the main route, with a two-cell opening blocked by the linked Door. Paying five Magic Stones opens only that internal reward door and never blocks critical traversal.
- Generated objects, including monsters, currently appear as soon as the dungeon build succeeds. Room entry activates the door lock, while delayed room-entry monster spawning remains a later extension.
- `Prototype_Boss` is a terminal-room test asset with one Left socket. The installer verifies its socket is consumed and sweeps Seeds `0..127` for complete six-room layouts.
- A failed later placement can still produce a partial layout for visual diagnosis; `DungeonGenerator.Generate()` returns `false` for that result.

## Scene Setup Entry Point

The ready-to-run v0 entry point is `Assets/_Project/Scenes/ProceduralDungeonV0Test.unity`. It uses `PrototypeCorridorV0Library.asset`, Seed `20260811`, six rooms, adaptive straight corridors, and a required final single-socket Boss room. The library contains `12x8` Start, `10x8` compact Combat, `18x8` wide Combat, `10x14` tall Combat, `18x12` sacrifice Treasure, and `18x12` Boss samples. The fixed Seed selects three Combat rooms and one sacrifice Treasure room. Generated connection doors start open for traversal; the Treasure room's internal reward door remains locked until its offering succeeds. Its `GlobalUIRoot` mirrors the active prefab overrides from `ProtoTypeHub.unity`. Entering Play Mode invokes the pipeline through `generateOnStart`.

To recreate the integration scene and demo data, run `Tools/Dungeon/Install V0 Prototype Corridor Test Scene`. To update only UI parity after a Hub layout change, run `Tools/Dungeon/Sync Hub UI To V0 Test Scene`. For another scene:

1. Create a dedicated runtime Grid with separate Floor and Wall Tilemaps. Put generated collision on the gameplay `Ground` layer and configure the Wall Tilemap with a static `Rigidbody2D` plus merged `CompositeCollider2D`.
2. Add `DungeonRoomBuilder`; assign those Tilemaps, corridor Floor/Wall tiles, a connected Door prefab, and dedicated generated-door, generated-socket-blocker, generated-object, and generated-room-encounter roots.
3. Add `DungeonGenerator`, assign the builder and one theme library, then configure Seed, room count, and corridor length.
4. Ensure the library contains at least one Start room, expansion room, and (when enabled) Boss room.
5. Enter Play Mode; `generateOnStart` invokes the pipeline.

## Boss Theme Corridor Installation

Run `Tools/Dungeon/Install Boss Theme Procedural Corridor Scenes` to rebuild and verify the four boss-theme variants. The installer reads the `Ground` palette and collidable `Wall` topology from each authored Corridor scene, creates eight room templates and one library per theme, installs a generator shell, adds the scene to Build Settings, updates that boss's `CorridorBossRouteSetSO`, verifies the `ProtoTypeHub` start portal's `RunRouteCatalogSO`, and refreshes the existing RouteSet loading manifests.

- Shadow uses ShadowCorridor tiles and Shadow/Candlestick/Skeleton monster prefabs.
- Dragon uses DragonCorridor tiles and Beer/Goblin/Lizard monster prefabs.
- Slime uses SlimeCorridor tiles and Pawn/Knight/Wizard monster prefabs.
- DemonKing uses DemonkingCorridor tiles and Arcane melee/tank golems plus Goblin Gunner.
- Every library keeps the same layout contract: `12x8` Start, three rectangular Combat rooms, `56x56` ㄴ-shaped Combat, `60x52` ㄱ-shaped Combat, `18x12` sacrifice Treasure, and a terminal one-socket Boss room. Theme changes data and art, not layout behavior.
- The installer keeps up to eight frequent Ground variants. It groups collidable Wall tiles by eight-neighbor and cardinal-neighbor masks, then bakes each room cell with a deterministic palette choice. Exact topology wins; cardinal and horizontal/vertical groups are fallbacks.
- `DungeonRoomBuilder` stores the primary tiles for compatibility plus theme-specific Floor and horizontal/vertical Wall variant lists for generated corridors. Cell coordinates, layout Seed, and connection index choose variants deterministically, so rebuilding the same layout does not visually reshuffle it.
- Tile palettes are implementation data. `DungeonLayoutAssembler` still sees only room bounds, sockets, weights, and types, and remains unaware of boss themes or sprites.
- The ㄴ/ㄱ rooms reserve their full outer rectangle for overlap checks, but Floor and Wall data describe only two joined 18-cell-thick legs. The empty inner corner therefore remains physically outside the walkable room while the layout assembler retains simple `RectInt` collision.
- Rectangular Combat samples contain `2/4/4` monsters; each large corner sample contains `8`. Every theme library carries at least five monster prefab kinds and 26 total monster placements, all linked to their room-local Kill Lock chest.
- Representative scene Seeds must select both large corner shapes plus the sacrifice Treasure room. Theme Seed ranges are separated so the four installed scenes do not reuse the same layout.
- Hub is not given four hardcoded portals. Its existing `HubToRunStart` portal continues to build a run from `RunRouteCatalog`; each route set now points to its matching procedural Corridor, and the generated Boss-room portal resolves the current route's Boss scene.
- `ProceduralShadowCorridor` additionally owns one scene-root instance of `GlobalVisionMaskRoot.prefab`, matching the authored `ShadowCorridor`. `SceneRestrictedVisionController` applies the persistent `restricted_vision` status when the player registers; `GlobalVisionMaskController` keeps the dark overlay active and spawns a mask follower bound to the player. Monster fog can acquire the same controller for a temporary full-black overlay without introducing a second darkness system.
- The darkness prefab is scene-level infrastructure, not room data and not generated content. Regeneration clears rooms without destroying it, and the installer verifies exactly one global/scene controller in Shadow while keeping the other three theme scenes free of this prefab.

## Extension Points

- Add trap/reward kinds and per-placement configuration without exposing those details to `DungeonLayoutAssembler`.
- Optionally delay procedural monster creation until entry by feeding generated placement requests into the existing room-entry spawn presentation flow; keep the generated room group as the encounter owner.
- Generalize the currently fixed width beyond two cells and add socket categories before adding multi-cell corridors.
- Replace the fixed straight corridor with a routed/bent corridor planner when topology needs turns or intersections.
- Add room rotation by transforming bounds, tile cells, and socket directions together.
- Add layout policies for branches, critical path length, treasure/shop quotas, and difficulty progression above the current assembler.
- Move the door prefab/policy into theme data when themes require different door visuals or behavior.

## Known Pitfalls

- A library containing only Combat templates cannot generate because Start is required.
- Enabling the boss option without a usable Boss template returns a partial/failed result.
- Floor and Wall builder references must be different Tilemaps and should be dedicated to generated content because every build clears them.
- A positive corridor length requires both corridor Floor and Wall tiles on `DungeonRoomBuilder`.
- The generated-door root must also be dedicated to generated content because every build destroys all of its children.
- The generated-socket-blocker root must be dedicated to generated content for the same lifecycle reason.
- The generated-object root must be dedicated to generated content because regeneration destroys all of its children, including monsters registered with `MonsterSpawner`.
- The generated-room-encounter root must be dedicated to generated content because regeneration destroys its room groups, entry triggers, and door Kill Lock controllers.
- A Play Mode build containing `Monster` placements fails when no active `MonsterSpawner` exists. Its `Awake` must run before `DungeonGenerator.Start`, as in the current scene setup.
- Object placement validates the anchor cell, not the full sprite/collider footprint. Large prefabs still need manual room-boundary clearance checks.
- A monster's linked chest Placement Id is room-local and must resolve to a Chest placement whose prefab contains `ChestMonsterKillLock`. Renaming a target Placement Id requires updating its monster links in the room editor.
- A Wall tile at a socket is not sufficient evidence of physical closure: sprite collider geometry may leave a cell center passable. Validate collision coverage across both cells and keep one explicit span-sized blocker per unused logical socket.
- `localCell` is the canonical minimum-coordinate start cell, not the geometric center. For an even-width socket, the door center lies between its two cells.
- Reordering templates in a library can change the weighted selection sequence even with the same Seed.
- Composite room props may contain multiple gameplay objects, but their cross-prefab references must remain internal and be validated after saving; duplicating shortcut rules in `DungeonRoomBuilder` would break prefab ownership.
- Adjacent non-connected room edges are allowed when bounds do not overlap; their Wall tiles remain closed.
- The Seed sweep currently covers a single-socket final Boss, not a one-socket expansion room. A mixed library with intermediate dead ends needs open-socket-budget validation before it becomes production data.
- When an editor installer opens another scene, reload unreferenced ScriptableObject assets by path before assigning them; opening the scene can invalidate a previously loaded Unity wrapper.
- Replacing a prefab instance can null references held by other scene components to stripped objects inside that instance. Capture those references by original prefab object, instantiate the replacement, then remap and verify them before saving.
- Unity batchmode can reserialize assets beyond the intended target. Compare Git status before and after installer runs and isolate cleanup from user-authored dirty files.

## Promotion Candidate

If this pipeline becomes the production corridor-generation path, promote the stable data boundary and generation invariants into `Docs/Architecture/` or `Docs/Contracts/` with explicit approval.
