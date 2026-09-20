# Monster Scene Navigation

Navigation aid, not an Architecture or Contracts source of truth.

## Ownership

- `PawnOrbitContactIntent2D` receives `MonsterSpawnContext` for room-local range bypass and target reacquisition. Both actor and target must be inside the owning room. `Slime` preserves and forwards this context through split generations; unscoped summons keep authored detection range.
- Pawn now uses A* waypoints only when the direct body-envelope segment to the player is blocked. The guide changes direction, not the existing crawl pulse/rest clock. Clear sight restores normal approach/orbit. Active body bounds exclude triggers; the shared pathfinder also checks hole triggers. Missing navigation preserves legacy movement, with a throttled same-scene fallback lookup.
- Pawn caches copied waypoints and rebuilds for exhausted paths or target movement, at 0.35-0.45 second retry intervals. Instance-hashed initial delay plus a Pawn-only one-search-per-rendered-frame budget spreads searches without gameplay RNG. A blocked next segment clears the cache and waits rather than pushing into a wall. Stops, enable/pool reuse and spawn context replacement reset navigation; stopping does not reset the crawl clock.
- `PawnNavigationPlayModeTests` covers real pathfinder detours, cached-segment rejection, frame search budget, crawl pulse/rest preservation and stop cleanup. It is not a full generated-room or crowd performance test. Existing overlap/capsule-envelope trapping remains a separate issue; Rook was not changed in this patch.

- One active `TilemapPathfinder2D` belongs to each monster scene, on a separate scene root. It must not be parented under the persistent `MonsterSpawner`.
- The pathfinder uses a scene-local Grid, primary ground Tilemap and optional additional ground Tilemaps sharing that Grid. Procedural scenes reference `DungeonRoomBuilder.FloorTilemap` before generation; the same Tilemap is populated at runtime.
- `SceneMonsterSpawnDirector` resolves scene services and passes navigation through `MonsterSpawnContext` to receivers. Direct/boss-spawned monsters can use `EnemyChaseIntent2D`'s same-scene fallback.
- Fallback lookup caches active navigation, rejects foreign-scene or disabled references, and retries a miss at roughly one-second instance-staggered intervals. Re-enable resets fallback state. This is lookup recovery, not automatic runtime installation.
- Ranged lane-aware pursuit remains in `EnemyChaseIntent2D`; stationary monsters stay stationary. Connection does not make every monster run A* each frame.

## Authoring

`Tools/Monsters/Navigation` provides Install In Active Scene, Validate Active Scene and Install All Monster Scenes.

- Reuse the existing pathfinder; reject duplicates. Preserve probe/search tuning and existing valid additional floors. Ensure Wall remains in blockedLayers and bind all scene spawners.
- Prefer the procedural builder's floor; otherwise retain explicit existing ground references. Only a single active Tilemap named Ground or Floor is auto-selected when no explicit reference exists. Ambiguity is reported instead of choosing arbitrary decorative tiles.
- `ProceduralDungeonSceneInstaller` calls the shared installer for newly built theme/test scenes. `SceneSetupValidatorWindow` reports missing navigation and its Auto Fix invokes the installer.
- The all-scenes batch identifies monster-bearing scenes (including inactive authoring), spawn containers/groups, spawners, builders and existing pathfinders. It saves only navigation changes. The bounded YAML filter preserves unrelated ExecuteAlways/OnValidate changes to UI and tile serialization; it fails on unexpected navigation fileIDs or missing root documents.
- The active-scene operation leaves saving to the user. Batch operations restore the user's scene setup outside batchmode.

## Current Coverage And Pitfalls

- `MonsterNavigationFootprint2D` is a per-query immutable world-space AABB envelope plus root-relative center offset, derived from enabled non-trigger colliders attached to the actor's Rigidbody. Chase caches collider membership on enable/hierarchy changes and reads current bounds each query, so authored scaling, facing, child placement and collider offset are reflected without mutating the shared pathfinder. Independently simulated children and hurtboxes are excluded.
- Chase direct casts, A* cell/edge checks, endpoint-neighbor selection, waypoint validation, return-home A* and wall-stall diagnostics use the same footprint. Callers without a valid body retain the authored default probe. This is a conservative bounds box, not an exact capsule/polygon sweep; actual penetrations and corners may still require separate handling. MovementMotor2D now runs its existing bounded depenetration before zero/low-speed early exits, including when navigation is stopped. Unsimulated bodies, hard stops and hit pause are excluded. No teleport fallback is used.

- Chase detection ignores authored distance only when both monster and target are inside its injected RoomArea. Explicit special-monster overrides and closed-door/death perception checks remain independent. Missing-target acquisition expands its bounded search to cover the room; unscoped summons retain their authored range.
- A blocked projectile lane no longer forces a new A* request every interval. Cached paths persist until exhausted, the target moves, or the next segment becomes blocked. Rebuilds skip the initial cell center only when the next waypoint is directly safe. Failed routes stop rather than pushing into a known wall; missing/disabled pathfinding retains legacy pursuit.
- A* validates swept segments between cells as well as cell occupancy. Runtime pursuit also validates the next cached segment, preserving its retry deadline on blockage.
- Native tests cover translated Grid/Floor/TilemapCollider walls, repeated rebuilds with simulated position steps, unreachable targets, room range policy and existing projectile cases. This is not a full generated Shadow corridor gameplay or performance test.

- Migration validated 29 scenes including shipping procedural corridors, four boss arenas, tutorial boss and fixed corridors. Existing navigation was reused; 19 new scene roots were added and two old Dragon ground bindings were repaired.
- Two non-build legacy scenes need authoring decisions: `ProtoTypeCorridor` has its Grid disabled; `HeoMinSeokScene` has no unambiguous active Ground/Floor candidate. Neither was force-enabled or assigned guessed terrain. Running the all-scenes command reports these exceptions after processing valid scenes.
- No guarantee exists for unreachable targets, missing floor tiles or navigation grids with disconnected islands. Scene linkage validation is not a full dungeon reachability proof.
- Adding a new independently offset Grid requires explicit support; do not combine unrelated grids into one pathfinder.

Key files: `MonsterSceneNavigationInstaller.cs`, `SceneSetupValidatorWindow.cs`, `ProceduralDungeonSceneInstaller.cs`, `TilemapPathfinder2D.cs`, `SceneMonsterSpawnDirector.cs`, `EnemyChaseIntent2D.cs`.

## Corner and door approach recovery (2026-09-20)

- When the target cell is blocked, TilemapPathfinder2D considers all walkable cells within two cells in one bounded search. It selects the reachable candidate nearest the requested cell, instead of committing to the first free but potentially disconnected candidate. Search remains subject to the existing visit limit; reaching the current cell alone does not end the search while closer candidates may be reachable.
- EnemyChaseIntent2D detects less than 0.05 world units of displacement over one second of continuous pursuit and retries using the existing rebuild interval, including forcing path evaluation when the direct probe reports clear. Movement resumes normal direct pursuit after progress; inactive sampling gaps and StopChase reset the observation.
- Pawn cached-path stalls use at least one second and two crawl cycles before retry, preserving the per-frame search budget and retry interval. Intentional crawl rests are included in that allowance.
- Slime landings validate the containing ground tile at unsnapped candidate coordinates. Ground is mandatory; wall/hole queries use the actual candidate pose and footprint.
- Remaining limits: conservative AABB path footprint, authored door perception, missing navigation, dynamic crowd blockages, and actual encounter geometry still need play verification. Retrying cannot create a route through a physically impassable opening.
