# Procedural Dungeon Room Pipeline

Status: early v0 structure memory. This document is a navigation aid, not an Architecture or Contracts source of truth.

## Purpose

This pipeline turns reusable fixed-layer/object room templates into a connected runtime Tilemap dungeon. Room authoring, layout decisions, and runtime realization stay separate so decoration, monster, chest, portal, prop, later trap/reward, and theme-specific door data do not couple the layout algorithm to a theme.

Designer workflow: [절차적 던전 방 제작 툴 사용 가이드](../Guides/ContentAuthoring/ProceduralDungeonRoomAuthoringGuide.md)

## Current Flow

1. `RoomPieceEditorWindow` selects a theme library, uses its stage-monster catalog plus fixed monsters already referenced by its rooms for the quick-pick list, then creates, reloads, or duplicates one room authoring copy inside a non-saved additive `RoomAuthoringWorkspace` scene.
2. The workspace stays separate from every loaded gameplay scene. A dirty unsaved Untitled scene blocks workspace creation instead of being saved, replaced, or modified implicitly; Unity's clean empty startup scene can remain loaded beside the additive workspace.
3. `RoomPieceAuthoring` owns room metadata and eight fixed Tilemap references (`UnderFloor`, `Floor`, `FloorDetail`, `GroundDecoration`, `Wall`, `WallDetail`, `Foreground`, `OverlayFX`); `RoomSocketAuthoring` marks the start cell and outward direction of a two-cell logical boundary socket; `RoomObjectAuthoring` marks one prefab, common monster role, or stage-fixed monster placement and captures opt-in child-pose overrides exposed by a composite prefab; `RoomTravelEndpointAuthoring` marks one scene-travel slot independently of its final connection.
4. The editor bakes the validated authoring copy into `RoomTemplateSO` and can register it explicitly with the selected theme-specific `RoomThemeLibrarySO` without duplicate references.
5. Production `DungeonGenerator` instances read their theme library, layout policy, Seed, room count, placement attempts, and adaptive corridor values from one `DungeonGenerationProfileSO`. With no profile, legacy scene-local serialized fields remain the compatibility fallback.
6. `DungeonGenerator` selects one of two deterministic layout paths. With no layout policy it uses the legacy incremental `DungeonLayoutAssembler`; with a `DungeonLayoutPolicySO` and Boss generation enabled it uses `DungeonGraphLayoutAssembler`.
7. The graph-first path creates abstract room nodes and edges, validates critical-path distance, meaningful branches, cycles, and POI quotas, assigns room roles, selects compatible templates/sockets, then embeds the graph into physical room coordinates. Both paths reject room/corridor reservation overlap with rectangular `RectInt` checks.
8. `DungeonRoomBuilder` paints every room across the eight fixed visual Tilemaps with all socket Walls closed, creates one span-sized physical blocker per logical socket, opens only connected endpoints, paints the two-cell-wide straight corridor and side Walls, then creates one non-permanent Door at each connected socket endpoint.
9. When the active generation profile references a `CorridorDecorationProfileSO`, `CorridorDecorationComposer` deterministically fills each connection's usable span from registered Start, Short, Middle, Landmark, Filler, and End modules of the matching axis. `DungeonRoomBuilder` uses Horizontal modules for Right/Left and Vertical modules for Up/Down, reflecting within an axis for the reverse direction without quarter-turning horizontal art into vertical art. Authored Floor/Wall cells replace the base corridor while the other fixed layers overlay it.
10. For every room containing a Monster placement, the builder creates a full-bounds rectangular `MonsterRoomArea2D`, a `MonsterSpawnRoomGroup`, and a separate `RoomEncounterEntryTrigger2D` inset one cell from the boundary Walls. Each connected endpoint door owned by that room receives the existing `RoomDoorMonsterKillLock` behavior.
11. The builder realizes each room's Grid-relative object placements under a dedicated generated root. For a composite non-monster prefab it applies validated slot-level position, rotation, and scale overrides after the root pose and before gameplay feature binding; unchecked channels keep the prefab defaults. It creates other non-monsters directly, but realizes each Monster placement only as a deferred `MonsterSpawnContainer`. A common-role placement stores Warrior, Mage, or Tank plus its matching `StageMonsterSetSO` and resolves the current boss-progression index. A stage-fixed placement stores one explicit Enemy prefab and resolves it unchanged. On first room entry `SceneMonsterSpawnDirector` instantiates and registers either source through the same encounter contract.
12. The builder separately realizes room travel slots under a dedicated endpoint root. A scene-local `(roomId, slotId)` binding supplies `SceneConnectionSO` and its A/B side; the selected medium adds the interaction adapter, automatic 2D trigger adapter, or no departure adapter for arrival-only use. Automatic Trigger world size is authored explicitly and compensated against medium Transform scale before applying the generated BoxCollider2D.
13. `DungeonGenerator` resolves a run-scoped seed according to `RegenerateOnEntry`, `ResetContentsKeepLayout`, or `PreserveDuringRun`. Preserve mode captures stable generated-object states before scene exit and reapplies them immediately after the same layout is built.
14. `PlayerSpawner` waits for the destination endpoint registry when a data-driven transition targets procedural content, then places the player and completes arrival presentation before releasing the fade service's input lock.
15. The Room Piece tool's Map Preview step runs the same selected legacy or graph-first assembler against a transient copy of the selected library. The unsaved current room replaces its source template only in memory; `DungeonRoomBuilder` paints tiles/corridors with `DungeonBuildOptions.VisualOnly`, while Scene View handles represent room bounds, connections, and object kinds without instantiating gameplay prefabs.
16. `현재 미리보기 설정을 테마에 적용` writes the tested values into that library's `DungeonGenerationProfileSO`. The three production procedural Corridor scenes already reference their theme profiles, so the next runtime generation reads the new values without rewriting the scenes. `CorridorDecorationCompletedPreview` is a separate non-saving preview that composes one requested straight-corridor length with the same runtime composer and reports its exact module sequence.

## Key Files

- `Assets/_Project/Runtime/Features/Map/Procedural/RoomTemplateSO.cs`
  - Baked layout/build data, typed object placement data, room type, socket data, and shared two-cell socket geometry rules.
- `Assets/_Project/Runtime/Features/Map/Procedural/RoomObjectAuthoring.cs`
  - Scene authoring marker that converts a prefab Transform into room Grid-relative placement data and captures enabled composite child-pose channels.
- `Assets/_Project/Runtime/Features/Map/Procedural/RoomCompositePoseAuthoring.cs`
  - Prefab-owned stable slot contract for room-specific child Transform overrides; validates slot identity, target references, allowed channels, and nonzero scale.
- `Assets/_Project/Runtime/Features/Map/Procedural/RoomTravelEndpointAuthoring.cs`
  - Scene authoring marker that converts a travel Slot Id, medium kind, optional prefab, and Transform into reusable room data.
- `Assets/_Project/Runtime/Features/Map/Procedural/RoomThemeLibrarySO.cs`
  - Theme catalog and room-role candidate queries.
- `Assets/_Project/Runtime/Features/Map/Procedural/DungeonLayoutModels.cs`
  - Room placement, socket connection, and complete/partial result models.
- `Assets/_Project/Runtime/Features/Map/Procedural/DungeonLayoutAssembler.cs`
  - Deterministic weighted selection, socket alignment, and overlap rejection.
- `Assets/_Project/Runtime/Features/Map/Procedural/DungeonLayoutPolicySO.cs`
  - Optional graph topology, critical-path, branch/cycle, and room-role quota policy.
- `Assets/_Project/Runtime/Features/Map/Procedural/DungeonGenerationProfileSO.cs`
  - Theme library, layout policy, Seed, room count, placement attempts, adaptive corridor settings, and optional theme decoration-profile reference shared by preview and runtime generation.
- `Assets/_Project/Runtime/Features/Map/Procedural/CorridorDecorationModuleSO.cs`
  - One axis-specific two-cell-wide corridor module containing Horizontal/Vertical axis, role, length, eight fixed tile layers, and GroundProp pivot placements.
- `Assets/_Project/Runtime/Features/Map/Procedural/CorridorDecorationProfileSO.cs`
  - Per-theme landmark limit, registered module catalog, and deterministic `CorridorDecorationComposer`.
- `Assets/_Project/Runtime/Features/Map/Procedural/DungeonGraphLayoutAssembler.cs`
  - Graph-first topology construction, role assignment, directional socket/template selection, and size-aware physical embedding.
- `Assets/_Project/Runtime/Features/Map/Procedural/DungeonRoomBuilder.cs`
  - Runtime Tilemap/object realization, connected socket wall removal, and generated door/blocker/object lifecycle.
- `Assets/_Project/Runtime/Features/Map/Procedural/DungeonGenerator.cs`
  - Runtime orchestration entry point and per-dungeon reentry policy owner.
- `Assets/_Project/Runtime/Core/SceneFlow/SceneTravelContracts.cs`
  - Direction endpoint/gate/run/restore contracts and stable reentry policy values.
- `Assets/_Project/Runtime/Core/SceneFlow/SceneConnectionSO.cs`
  - Bidirectional scene connection asset. The ScriptableObject stays in its matching file so Unity can retain a valid script reference.
- `Assets/_Project/Runtime/Features/Map/Travel/SceneTravelEndpoint.cs`
  - Generated or authored endpoint identity, anchors, registry participation, and runtime connection binding.
- `Assets/_Project/Runtime/Infrastructure/SceneFlow/SceneConnectionTravelService.cs`
  - Shared gate-first travel execution, player capture, run action, departure presentation, and scene transition handoff.
- `Assets/_Project/Editor/Tools/Dungeon/RoomPieceEditorWindow.cs`
  - Designer-facing staged workflow, theme room browser, create/reload/duplicate, Tilemap selection, socket placement, data-derived prefab suggestions, validation, publish/apply, and dynamic map-preview flow.
- `Assets/_Project/Editor/Tools/Dungeon/RoomAuthoringDungeonPreview.cs`
  - Editor-only transient library, real assembler-driven tile preview, object plus `EI`/`ET`/`EA` travel-slot markers, and preview-root lifecycle.
- `Assets/_Project/Editor/Tools/Dungeon/CorridorDecorationEditorWindow.cs`
  - Designer-facing profile/module authoring, eight-layer Tilemap selection, GroundProp pivot placement, validation, bake, registration, and complete-corridor preview controls.
- `Assets/_Project/Editor/Tools/Dungeon/CorridorDecorationCompletedPreview.cs`
  - Non-saving Horizontal(+X) or Vertical(+Y) preview for one requested length, Seed, and connection index using the runtime composer.
- `Assets/_Project/Editor/Tools/Dungeon/CorridorDecorationExampleInstaller.cs`
  - Idempotent Shadow/Dragon/Slime example-profile and six-module installation plus asset and completed-preview validation menus.
- `Assets/_Project/Editor/Tools/Dungeon/ProceduralTravelBindingEditorWindow.cs`
  - Designer-facing room/slot-to-builder connection-side binding, direction-policy/presentation editing, scene-match validation, and explicit non-saving scene mutation.
- `Assets/_Project/Editor/Tools/Dungeon/DungeonGenerationProfileAssetUtility.cs`
  - Theme-profile lookup/creation and active Build Settings scene-reference reporting for the authoring tool.
- `Assets/_Project/Editor/Tools/Dungeon/RoomAuthoringWorkspace.cs`
  - Non-saved additive authoring scene lifecycle, original active-scene restoration, and unsaved-session tracking.
- `Assets/_Project/Editor/Tools/Dungeon/RoomAuthoringToolValidator.cs`
  - Manual component-level validation that the authoring workspace leaves already loaded saved scenes unchanged.
- `Assets/_Project/Editor/Tools/Dungeon/ProceduralDungeonSceneInstaller.cs`
  - Reproducible prototype/theme data creation, isolated scene wiring, Hub route connection, loading-manifest refresh, UI synchronization, and batch verification entry point.
- `Assets/_Project/Scenes/ProceduralDungeonV0Test.unity`
  - Verified corridor-derived integration scene; generates six rooms on Start.
- `Assets/_Project/Scenes/Procedural{Shadow|Dragon|Slime}Corridor.unity`
  - Normal-boss production-route candidates that share the generator shell while referencing one boss-specific room library each.
- `Assets/_Project/Scenes/DemonkingCorridor.unity`
  - Fixed combat-free rest Corridor for the final DemonKing RouteSet. The existing HUB interaction portal selects it through `DemonkingHubRouteCatalog`; it is not a procedurally generated route.

## Ownership And Lifecycle

- `RoomThemeLibrarySO` and `RoomTemplateSO` are authored assets and do not own runtime state.
- `DungeonGenerationProfileSO` owns persistent per-theme generation settings, not generated layout state. Editing it changes the next generation request for every scene that references it.
- `CorridorDecorationProfileSO` and its module assets own authored visual composition data, not layout length or runtime state. The generation profile owns only the reference to the theme profile.
- `RoomAuthoringWorkspace` owns temporary Editor objects only. It does not save a scene asset and never searches, removes, or disables gameplay-scene roots.
- Preview mutations are scoped separately from authoring mutations. Generating or clearing `[Preview] Procedural Dungeon` does not set the room's unsaved-session flag.
- `[Preview] Completed Corridor` is also transient and mutually exclusive with the full dungeon preview. It never writes profile, module, scene, or room assets.
- The preview owns transient `RoomThemeLibrarySO`/`RoomTemplateSO` copies only for one generation call and destroys them immediately after copying Scene View display data. Source assets are never edited or registered by preview.
- A normal room load is marked clean after reconstruction. New rooms, duplicates, Tilemap edits, socket/object changes, and metadata changes remain unsaved until publish/apply.
- Composite prefab defaults remain prefab-owned. `RoomTemplateSO` stores only explicitly enabled child position/rotation/scale channels per placement, so one room can move an Alcove child without mutating the prefab or other rooms.
- New-room save location is chosen through Unity's asset save panel. The tool does not encode a project room-folder path or scan prefab folders; recommendations come from the selected library's existing room data.
- Both layout assemblers are stateless. The same library ordering, template data, policy, settings, and Seed produce the same result.
- `DungeonLayoutResult` owns the generated placement/connection snapshot for one generation request.
- `DungeonGenerator` owns the last result reference but not the library assets or Tilemaps.
- `DungeonRoomBuilder` owns writes to its explicitly assigned eight fixed Tilemaps, generated-door root, generated-socket-blocker root, generated-object root, generated-travel-endpoint root, and generated-room-encounter root. Each build clears all configured Tilemaps and generated roots before rebuilding.
- `DungeonRoomBuilder.TryBuild(layout)` remains the full runtime path. The explicit `DungeonBuildOptions.VisualOnly` overload shares room/socket/corridor realization but skips connected Door and gameplay-object/encounter creation.
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
- Corridor-decoration modules are authored in one explicit axis. Horizontal uses progress `x=0..Length-1`, Floor rows `y=0,1`, and Wall rows `y=-1,2`; Vertical uses progress `y=0..Length-1`, Floor columns `x=0,1`, and Wall columns `x=-1,2`. Runtime axis selection and reverse-direction reflection are builder-owned.
- Decoration modules may use the complete requested corridor span. An exact full-length `Short` module takes precedence; otherwise optional Start and End modules touch the first and last corridor cells while the body draws from unweighted Middle, Filler, and eligible Landmark candidates.
- `MaxLandmarksPerCorridor` is an upper bound, not a minimum. Random selection and remaining length can produce zero landmarks.
- Decoration module Floor/Wall cells selectively replace the generated base corridor. UnderFloor, FloorDetail, GroundDecoration, WallDetail, Foreground, and OverlayFX are overlaid without changing collision ownership.
- Decoration objects are currently `RoomObjectKind.Prop` only. Their canonical cell/offset/rotation/scale is transformed with the module; gameplay behavior and collision stay prefab-owned.
- Both cells of every socket must contain Floor and Wall data. Runtime realization begins with the logical socket closed by both Wall tiles and one span-sized physical blocker; only connected endpoints remove all closure.
- One connection creates two doors: one at each room socket endpoint, not one per socket cell. Each door sits between the centers of that endpoint's two-cell span.
- Left/right connections rotate the shared Door prefab 90 degrees; up/down connections keep its default rotation.
- Generated doors are `Normal` and non-permanent, with deterministic run-local IDs derived from Seed and connection index.
- Generated doors begin open. A room containing at least one Monster placement owns one generated encounter group; when the player's body is fully inside its trigger, every connected endpoint door belonging to that room closes through `RoomDoorMonsterKillLock` and reopens after the group's tracked monsters are cleared.
- The generated entry trigger is inset one cell from all room bounds so the player must fully pass the socket-end Door before encounter entry. The full-bounds room area remains separate for monster containment and outside-room safeguards.
- A generated door lock receives the socket's exact inward direction. It closes only after every tracked monster's active non-trigger collider bounds are fully inside the room and fully past that door's center plane plus a small clearance; a monster straddling the doorway therefore keeps the relevant door open.
- `RoomBuildData.objectPlacements` stores `Prop`, `Monster`, `Chest`, or `Portal` plus Grid-relative root transform data and optional composite child-pose overrides. A Monster stores exactly one source: a common-role `StageMonsterSetSO` or a stage-fixed Enemy prefab, and can additionally store one target Kill Lock chest Placement Id. Placement cells must be inside room bounds and contain Floor.
- A child-pose override is accepted only when the instantiated prefab exposes the same stable Slot Id through `RoomCompositePoseAuthoring`. Empty or duplicate IDs, missing targets, disallowed channels, and zero scale fail validation instead of silently changing another child.
- Placement IDs are unique within one room template. Runtime instance names combine the room placement ID and placement ID so repeated templates remain distinguishable.
- Play Mode `Monster` placements become inactive runtime spawn anchors during the dungeon build. The designer chooses either Warrior/Mage/Tank, which maps to CommonMelee/CommonRanged/CommonTank `StageMonsterSetSO`, or one explicit stage-fixed Enemy prefab. On first entry common roles resolve the current-stage prefab without weighted room-level composition, while fixed points keep their authored prefab. A linked chest is supplied through `MonsterSpawnRequest.LinkedChestKillLock`, so `SceneMonsterSpawnDirector` remains responsible for registration.
- A chest prefab owns its `ChestMonsterKillLock` behavior and presentation. The procedural pipeline never adds the component or selects a presentation prefab; room data chooses the complete prefab.
- Traps, levers, puzzles, and runtime anchors are currently authored as self-contained `Prop` prefabs. A `ProceduralRoomAnchor` may live inside that prefab, but anchors are not an independent `RoomBuildData` placement collection. Arbitrary internal component state is preserved only when the prefab participates in an explicit stable-ID capture/restore contract.
- `Prototype_Treasure_Sacrifice` stores one composite `Prop` prefab containing the existing `StatueShortcut`, its linked non-permanent `Locked` `DoorObject`, and the standard `TreasureChest`. The layout assembler sees only the Treasure room bounds and sockets; the prefab owns payment, animation, opening, and reward behavior.
- The sacrifice room is an `18x12` left/right through-room. A horizontal internal Wall separates the upper reward alcove from the main route, with a two-cell opening blocked by the linked Door. Paying five Magic Stones opens only that internal reward door and never blocks critical traversal.
- Generated chests, portals, and props appear when the dungeon build succeeds. Monsters do not exist before their room is entered; the entry trigger schedules the progression-aware spawn plan and activates the door lock only after the encounter is ready.
- `Prototype_Boss` is a terminal-room test asset with one Left socket. The installer verifies its socket is consumed and sweeps Seeds `0..127` for complete six-room layouts.
- A failed later placement can still produce a partial layout for visual diagnosis; `DungeonGenerator.Generate()` returns `false` for that result.
- A graph policy is opt-in. A generator with no policy keeps the legacy incremental layout behavior, so the V0 test scene and unrelated scenes do not change implicitly.
- Graph-first generation requires Boss generation because its topology is anchored by Start and Boss. Policy limits are clamped to a feasible room count before generation, and an unsatisfied topology or role quota fails explicitly instead of silently weakening the policy.

## Scene Setup Entry Point

The ready-to-run v0 entry point is `Assets/_Project/Scenes/ProceduralDungeonV0Test.unity`. It uses `PrototypeCorridorV0Library.asset`, Seed `20260811`, six rooms, adaptive straight corridors, and a required final single-socket Boss room. The library contains `12x8` Start, `10x8` compact Combat, `18x8` wide Combat, `10x14` tall Combat, `18x12` sacrifice Treasure, and `18x12` Boss samples. The fixed Seed selects three Combat rooms and one sacrifice Treasure room. Generated connection doors start open for traversal; the Treasure room's internal reward door remains locked until its offering succeeds. Its `GlobalUIRoot` mirrors the active prefab overrides from `ProtoTypeHub.unity`. Entering Play Mode invokes the pipeline through `generateOnStart`.

The V0 scene is a procedural runtime shell rather than a disabled copy of the authored `ProtoTypeCorridor` map. It keeps camera, player spawn, `MonsterSpawner`, gameplay/item/loot/cue services, interaction prompts, UI, and `[ProceduralDungeonV0]`. It does not keep the authored Grid, fixed monster spawn points, map doors/shortcuts/chests/portal, disabled Boss camera objects, or NPC/dialogue/affection-only managers. The retained `MonsterSpawner` has empty authored spawn lists; generated room placements are its runtime input.

To recreate the demo data and rebuild the generated shell inside the existing integration scene, run `Tools/Dungeon/Install V0 Prototype Corridor Test Scene`. Scene cleanup is stored directly in `ProceduralDungeonV0Test.unity`; the installer does not delete objects by name or prefab path and does not modify another scene as a cleanup source. It verifies component-level shell invariants before and after rebuilding. If the dedicated scene asset is missing, installation fails instead of copying `ProtoTypeCorridor`. To update only UI parity after a Hub layout change, run `Tools/Dungeon/Sync Hub UI To V0 Test Scene`. For another scene:

1. Create a dedicated runtime Grid with the eight fixed Tilemaps. Only Floor and Wall use the gameplay `Ground` physics layer; decorative Tilemaps remain on `Default`. Configure only Wall with a static `Rigidbody2D` plus merged `CompositeCollider2D`.
2. Add `DungeonRoomBuilder`; assign all fixed Tilemaps, corridor Floor/Wall tiles, a connected Door prefab, and dedicated generated-door, generated-socket-blocker, generated-object, generated-travel-endpoint, and generated-room-encounter roots. `Tools/Dungeon/Install Fixed Room Tile Layers Only` upgrades the four existing procedural scenes without rebuilding their generator configuration.
3. Create a `DungeonGenerationProfileSO`, assign its theme library, policy, Seed, room count, and corridor values, then assign that profile and the builder to `DungeonGenerator`. Scene-local values remain only as a compatibility fallback.
4. Ensure the library contains at least one Start room, expansion room, and (when enabled) Boss room.
5. Enter Play Mode; `generateOnStart` invokes the pipeline.

## Boss Theme Corridor Installation

Run `Tools/Dungeon/Install Boss Theme Procedural Corridor Scenes` to rebuild and verify the three normal-boss variants. The installer reads the `Ground` palette and collidable `Wall` topology from each authored Corridor scene, creates eight room templates and one library per theme, installs a generator shell, adds the scene to Build Settings, updates that boss's `CorridorBossRouteSetSO`, and refreshes the existing RouteSet loading manifests. It separately restores and validates the final `DemonkingRouteSet` against the fixed `DemonkingCorridor` rest scene, binds the existing `ProtoTypeHub` interaction portal to a zero-normal-stage `DemonkingHubRouteCatalog`, disables the retired `ProceduralDemonkingCorridor` Build Settings entry, and disables both directions of its two legacy connection assets.

- Shadow, Dragon, and Slime keep their respective Corridor tile palettes. Their installed Combat samples use explicit Warrior/Mage/Tank positions with direct Common Melee/Ranged/Tank `StageMonsterSetSO` references; room-level weighted monster profiles are not part of the active procedural path. Designers can add theme-specific stage monsters as explicit fixed-prefab points without changing the layout algorithm.
- Every library keeps the same layout contract: `12x8` Start, three rectangular Combat rooms, `56x56` ㄴ-shaped Combat, `60x52` ㄱ-shaped Combat, `18x12` sacrifice Treasure, and a terminal one-socket Boss room. Theme changes data and art, not layout behavior.
- The three normal-themed generators share `ExplorationCorridorPrototypePolicy.asset` and request 15 rooms. The policy enforces a Start-to-Boss graph distance of `6..8`, `2..4` meaningful branches, `1..2` cycles, at least one Treasure room, and at least four Combat rooms. Event and Shop quotas remain zero until matching room content is authored.
- Shadow, Dragon, and Slime each own one `Procedural{Theme}GenerationProfile.asset`. The Room Piece tool can load it into Map Preview and save the tested Seed, room count, placement attempts, minimum corridor length, room-size ratio, and random length range back to the theme.
- The same three profiles reference theme decoration profiles under `Assets/_Project/Data/Dungeon/CorridorDecorations/{Theme}/`. Each example profile registers six Horizontal modules (`Start_02`, `Middle_03`, `Landmark_04`, `Filler_01`, `End_02`, `Short_02`) and six matching `Vertical_*` modules; the landmark prop differs per theme.
- All three production procedural Corridors use unique state IDs with `PreserveDuringRun`. Formal scene travel captures object state before exit, rebuilds the same Seed on reentry, and restores supported generated-object state. `StartRun` and `EndRun` clear every stored Corridor state.
- Re-running the full theme installer creates missing profiles and relinks generators, but preserves existing profile and layout-policy values. Installer constants are first-creation defaults only.
- Graph-first generation completes abstract topology and room roles before choosing exact handcrafted room templates. Special roles prefer compatible dead ends and are spread across the graph when alternatives exist.
- Physical embedding reserves full rectangular room bounds and derives row/column spacing from the largest selected room extents. Existing straight two-cell corridor and builder contracts are retained; a cycle is represented by an additional square detour rather than by a new corridor pathfinding system.
- The installer keeps up to eight frequent Ground variants. It groups collidable Wall tiles by eight-neighbor and cardinal-neighbor masks, then bakes each room cell with a deterministic palette choice. Exact topology wins; cardinal and horizontal/vertical groups are fallbacks.
- `DungeonRoomBuilder` stores the primary tiles for compatibility plus theme-specific Floor and horizontal/vertical Wall variant lists for generated corridors. Cell coordinates, layout Seed, and connection index choose variants deterministically, so rebuilding the same layout does not visually reshuffle it.
- Tile palettes are implementation data. `DungeonLayoutAssembler` still sees only room bounds, sockets, weights, and types, and remains unaware of boss themes or sprites.
- The ㄴ/ㄱ rooms reserve their full outer rectangle for overlap checks, but Floor and Wall data describe only two joined 18-cell-thick legs. The empty inner corner therefore remains physically outside the walkable room while the layout assembler retains simple `RectInt` collision.
- Rectangular Combat samples contain `2/4/4` role spawn anchors; each large corner sample contains `8`. The installer assigns Warrior, Mage, and Tank cyclically while preserving every authored position and stable Placement Id. Each role owns a direct Common Melee, Ranged, or Tank `StageMonsterSetSO` reference, so the role count is deterministic and only its concrete prefab changes with boss progression.
- Representative scene Seeds must select both large corner shapes plus the sacrifice Treasure room and satisfy every graph metric. Theme Seed ranges are separated so the three installed scenes do not reuse the same layout. The installer additionally sweeps 64 Seeds per theme against the graph policy.
- Every normal-themed Start room owns a `LobbyGate` trigger slot, and every normal terminal Boss room owns a `BossGate` interaction slot using `ProceduralSceneTravelPortal.prefab`. Scene-local builder bindings connect them to the theme's Lobby↔Corridor and Corridor↔Boss `SceneConnectionSO` assets.
- Each normal-theme Lobby↔Corridor connection uses `LobbyToCorridorWipeTravel.asset` in both directions. Direction-specific run actions remain different (`StartRun` into the Corridor, `None` back to HUB); only the authored transition presentation is shared.
- The legacy `ExitPortal` room object was removed from the three active normal-themed Boss templates. Corridor→Boss now uses the gate-first data-driven connection and the current-run stable boss theme Id.
- Each normal authored Boss scene owns one arrival-only `SceneTravelEndpoint` at its configured `PlayerSpawnPoint`, so procedural Corridor→Boss placement does not fall back to a static spawn after a timeout.
- Direct inter-Corridor pipe travel is sealed. The old source/destination room, connection, profile, and prefab assets remain for history, but the travel installer unregisters those rooms from every theme library and guaranteed-room list and does not create scene bindings for them.
- The Slime construction-shortcut and remote-teleport NPC room assets are likewise sealed. Their installer may refresh dormant assets, but removes the rooms from the active library/profile and removes the old teleport destination from the Start room.
- `ProtoTypeHub` owns active trigger gates for Shadow, Dragon, and Slime. The focused staging installer preserves position and active state for an existing gate; only a newly missing gate is created inactive for manual placement. DemonKing has no automatic trigger gate: the HUB's existing interactive `ScenePortal` uses `DemonkingHubRouteCatalog` with zero normal stages and enters the fixed `DemonkingCorridor` directly. The rest scene's authored spawn lists are empty and its existing `CorridorToBoss` portal leads to `LeeJunmo_Boss_DemonKing`.
- Running `Tools/Dungeon/Install Boss Theme Procedural Corridor Scenes` now reapplies this focused travel configuration after rebuilding the base theme content. `Tools/Dungeon/Install Procedural Corridor Travel Configuration` reapplies only the travel slice.
- `ProceduralShadowCorridor` additionally owns one scene-root instance of `GlobalVisionMaskRoot.prefab`, matching the authored `ShadowCorridor`. `SceneRestrictedVisionController` applies the persistent `restricted_vision` status when the player registers; `GlobalVisionMaskController` keeps the dark overlay active and spawns a mask follower bound to the player. Monster fog can acquire the same controller for a temporary full-black overlay without introducing a second darkness system.
- The darkness prefab is scene-level infrastructure, not room data and not generated content. Regeneration clears rooms without destroying it, and the installer verifies exactly one global/scene controller in Shadow while keeping the other two normal theme scenes free of this prefab.

## Extension Points

- Add trap/reward kinds and per-placement configuration without exposing those details to `DungeonLayoutAssembler`.
- Optionally delay procedural monster creation until entry by feeding generated placement requests into the existing room-entry spawn presentation flow; keep the generated room group as the encounter owner.
- Generalize the currently fixed width beyond two cells and add socket categories before adding multi-cell corridors.
- Replace the fixed straight corridor with a routed/bent corridor planner when topology needs turns or intersections.
- Add room rotation by transforming bounds, tile cells, and socket directions together.
- Author Event and Shop room templates, then enable their existing policy quotas. Add graph-distance-based difficulty progression as a separate role/content policy rather than changing physical embedding.
- Move the door prefab/policy into theme data when themes require different door visuals or behavior.

## Known Pitfalls

- A library containing only Combat templates cannot generate because Start is required.
- Enabling the boss option without a usable Boss template returns a partial/failed result.
- Assigning a graph policy while disabling Boss generation falls back to the legacy assembler; graph-first topology currently requires both Start and Boss anchors.
- A nonzero required role quota needs at least one usable template of that exact room type. Missing Treasure/Event/Shop candidates fail generation with a role-specific error.
- The policy needs enough requested rooms to satisfy the minimum Boss distance, branches, and cycles. The assembler clamps random targets to feasible ranges but does not invent rooms beyond `roomCount`.
- Opposite left/right sockets selected for a graph edge must share a physical row, and opposite up/down sockets must share a column. The graph embedder chooses compatible sockets and rejects a topology/template assignment that cannot preserve this straight-corridor invariant.
- Every configured fixed-layer builder reference must be a dedicated Tilemap because every build clears it. New decorative data fails the build explicitly when its target Tilemap reference is missing; old Floor/Wall-only scenes retain compatibility until upgraded.
- `FloorDetail` and `GroundDecoration` authoring cells require Floor below them; `WallDetail` requires Wall below it. `WallDetail` has no duplicate collider and inherits blocking from the base Wall.
- `GroundProp` is represented by `RoomObjectKind.Prop`, not a ninth Tilemap. Its prefab owns collision and physics-layer configuration.
- A positive corridor length requires both corridor Floor and Wall tiles on `DungeonRoomBuilder`.
- Decoration is optional. A missing profile, an empty module catalog, or a cell not covered by a fitting module leaves the deterministic base corridor intact.
- A one-cell Filler is the safest way to cover arbitrary remaining spans. Without a fitting candidate, the composer advances over that cell rather than stretching or clipping a module.
- The complete-corridor preview can independently show Horizontal(+X) and Vertical(+Y) source data. It does not prove reverse-direction reflection or room-socket placement, so validate Right/Left/Up/Down in Play Mode.
- Map Preview corridor tile overrides are optional. When empty, the editor picks the most frequently referenced Floor/Wall tile in the transient preview library; choose explicit overrides when that statistical fallback is not visually representative of the theme.
- Including the current room makes it a valid weighted candidate, not a forced placement. A complete preview can report zero current-room placements for a particular Seed; change the Seed instead of altering runtime selection weight only for preview.
- The generated-door root must also be dedicated to generated content because every build destroys all of its children.
- The generated-socket-blocker root must be dedicated to generated content for the same lifecycle reason.
- The generated-object root must be dedicated to generated content because regeneration destroys all of its children, including deferred spawn anchors and monsters registered with `MonsterSpawner` after room entry.
- The generated-travel-endpoint root must be dedicated to generated content because regeneration destroys all generated media and registry endpoints under it.
- The generated-room-encounter root must be dedicated to generated content because regeneration destroys its room groups, entry triggers, and door Kill Lock controllers.
- A Play Mode build can create deferred Monster anchors before `MonsterSpawner` is ready, but room-entry spawning requires an active `MonsterSpawner`/`SceneMonsterSpawnDirector`. Their `Awake` must complete before the player can enter an encounter trigger.
- Object placement validates the anchor cell, not the full sprite/collider footprint. Large prefabs still need manual room-boundary clearance checks.
- A monster's linked chest Placement Id is room-local and must resolve to a Chest placement whose prefab contains `ChestMonsterKillLock`. Renaming a target Placement Id requires updating its monster links in the room editor.
- A Wall tile at a socket is not sufficient evidence of physical closure: sprite collider geometry may leave a cell center passable. Validate collision coverage across both cells and keep one explicit span-sized blocker per unused logical socket.
- `localCell` is the canonical minimum-coordinate start cell, not the geometric center. For an even-width socket, the door center lies between its two cells.
- Reordering templates in a library can change the weighted selection sequence even with the same Seed.
- `PreserveDuringRun` keys objects by generated room Placement Id plus authored object Placement Id. Renaming or reordering room content while a run is active invalidates that runtime-only mapping.
- A room travel slot without a matching scene-local builder binding remains disabled. A binding whose connection side names a different scene fails the dungeon build instead of silently registering the wrong destination.
- `SceneConnectionSO` gate subject Id must match `CorridorBossRouteSetSO.StableThemeId`; use the serialized theme Id rather than scene names.
- An authored Lobby gate is not only a `BoxCollider2D`. It needs `SceneTravelEndpoint`, `SceneTravelTrigger2D`, one Lobby-side connection binding, an arrival anchor, and specific `RunRouteCatalog`/route-set context activation before it can replace the legacy run-start portal.
- A trigger used as both departure and arrival suppresses the arriving player for its configured reactivation delay and until no player collider remains inside the trigger. During suppression, `SceneTravelTrigger2D` creates and enables a narrow Wall-layer blocker on the side opposite the arrival anchor, so the player cannot walk out of the map while the travel request is inactive. The blocker is disabled when suppression is released.
- A one-way inter-Corridor destination must use `ArrivalOnly` room data as well as a disabled reverse `SceneConnectionSO` direction. Relying on only one of those guards makes later prefab or binding edits capable of exposing an unintended return path.
- Composite room props may contain multiple gameplay objects, but their cross-prefab references must remain internal and be validated after saving; duplicating shortcut rules in `DungeonRoomBuilder` would break prefab ownership.
- Adjacent non-connected room edges are allowed when bounds do not overlap; their Wall tiles remain closed.
- The legacy V0 Seed sweep covers a single-socket final Boss. The themed graph-first sweep additionally covers intermediate dead ends, cycles, role quotas, and one-socket terminal Boss consumption across 64 Seeds per theme.
- When an editor installer opens another scene, reload unreferenced ScriptableObject assets by path before assigning them; opening the scene can invalidate a previously loaded Unity wrapper.
- Replacing a prefab instance can null references held by other scene components to stripped objects inside that instance. Capture those references by original prefab object, instantiate the replacement, then remap and verify them before saving.
- Unity batchmode can reserialize assets beyond the intended target. Compare Git status before and after installer runs and isolate cleanup from user-authored dirty files.

## Promotion Candidate

If this pipeline becomes the production corridor-generation path, promote the stable data boundary and generation invariants into `Docs/Architecture/` or `Docs/Contracts/` with explicit approval.
