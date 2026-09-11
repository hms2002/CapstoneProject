# Dungeon Return Portals

Status: implementation navigation aid, not an Architecture or Contracts source of truth.

## Purpose And Boundaries

Return from generated dead-end rooms to the same dungeon's Start room without reloading the scene. The arrival view opens above a safe floor point, the existing Hub fall/wake presentation plays there, and the arrival view closes at landing rather than waiting for wake-up. The source interaction remains reusable. No BGM, route progression, reward, NPC or scene-transition rules are changed.

- Dead end means exactly one actual `DungeonSocketConnection`, regardless of unused authored sockets. Start is excluded; a terminal Boss entrance room is included and retains its existing boss-travel gate.
- Combat rooms reveal after the final room wave and all pending/live split-aware units and encounter holds clear, with a short stable-clear delay. Doors opening early is not treated as combat completion.
- Event rooms reveal on entry, but interaction is blocked while their encounter is busy. Other rooms without an encounter group also reveal on entry.
- An unavailable or obstructed destination blocks interaction rather than moving the player into geometry. A room with no safe portal placement warns and skips that portal instead of failing dungeon generation.

## Runtime Ownership

- `DungeonReturnPortalPlacement.cs`: deterministic actual-connection checks, cardinal directions, reachable-cell flood fill and opposite-wall selection. No prefab or physics ownership.
- `DungeonRoomBuilder.ReturnPortals.cs`: one generation-time post-pass after room objects and encounter binding. Owns the generated rig/portals, cached reachable Start cells, landing validation, optional anchor lookup and reveal snapshots. Rebuild cancels travel before disposing its own generated root.
- `DungeonRoomDiscoveryTrigger2D.cs`: reports entry by the player's designated body collider to optional map discovery and local subscribers. Portal entry does not require an enabled minimap UI.
- `DungeonReturnPortal.cs`: local entry/reveal/encounter gate and `InteractableBase` prompt. Room counts are checked at 0.1-second intervals; there is no per-frame global monster search.
- `DungeonReturnTravel.cs`: one scene-owned sequence. Uses existing cinematic protection, targetability, timer-pause, camera-focus and optional fade contracts. Stops movement, disables colliders through wake-up, warps to the safe floor, hides the player during portal opening, then calls `PlayerHubSpawnPresentation2D.TryPlayPortalArrival`. Landing updates map discovery and begins portal closing. Completion waits for both waking and closing.
- `PlayerHubSpawnPresentation2D.cs`: owns the shared fall curves, whole-player spin, detached grounded shadow, landing pose/sound and input/automatic wake. During the fall it disables Rigidbody simulation as in Hub. Portal entry supplies an explicit start position and an owner token; it bypasses Hub eligibility without modifying `hasPlayedThisScene` or firing the Hub-only completion event. Owner-checked cancellation restores the landing pose and captured state; a wrong owner cannot cancel another sequence.
- `PlayerPortalArrivalVisual2D.cs`: keeps the existing authored renderer bindings and only captures/restores visibility. It no longer writes child positions or shadow scales in LateUpdate, which would compete with the shared Hub sequence.
- `DungeonReturnPortalView.cs`: four directional visual slots with optional Animator states; no interaction or travel decisions. Missing controllers/states use a simple scale open/close, not generated fallback artwork.

Travel has a single busy guard, rejects non-idle/busy players and validates landing again after fade-out. Completion, explicit cancel, disable, run end and regeneration release acquired protection, targetability, timer pause, fade/camera ownership, collider state and render pose/visibility. Nothing is kept alive through a new singleton or `DontDestroyOnLoad` object.

Reveal persistence uses a synthetic `return-portal:{placementId}` entry in the existing generated-object snapshot. It is metadata, not a separately restored physical object. No persistent enum, managed-reference type or save DTO was added. Older snapshots without this entry still use normal entry/completion gates.

## Placement

Start and each dead end are searched from just inside their connected socket. Only reachable floor cells are considered; wall tiles, solid props and hole traps constrain traversal. Portal candidates prefer the wall opposite the entry, then proximity to the entry's lateral coordinate. Interaction placement rejects other interactable triggers as well as solids.

Landing defaults near the Start room's bounds center but must belong to its reachable floor component. Per-use checks include a conservative player body footprint, not only its center. Physics overlap-buffer overflow fails closed. The expensive room search is generation-time only; interaction-time validation is a local overlap/footprint check.

Optional anchor prefabs use existing `ProceduralRoomAnchor` with LocalRoom scope. Place these as Prop objects through Room Piece and save the template:

- `ReturnLanding`: preferred landing position inside Start.
- `ReturnPortal_Up`, `ReturnPortal_Right`, `ReturnPortal_Down`, `ReturnPortal_Left`: preferred source position for the corresponding opposite-wall direction.

Invalid anchors do not force unsafe placement; the automatic safe search remains available. The direction names denote the wall side: a lower entrance chooses `Up`. New room shapes/large props should be checked in actual gameplay and can use anchors where automatic selection is aesthetically poor.

## Authoring And Assets

Folder: `Assets/_Project/Prefabs/Map/Procedural/ReturnPortals/`.

- `DungeonReturnPortal.prefab`: source trigger/prompt plus `DirectionalVisuals/Up|Right|Down|Left`.
- `DungeonReturnTravelRig.prefab`: landing transform and `ArrivalPortal/Up|Right|Down|Left`; no interaction collider. The authored rig selects Up, the opposite of the original Down slot, so the current arrival artwork faces the falling player. Direction labels describe wall sockets, not necessarily the visible opening of an imported sprite. The setting remains adjustable.
- Each direction has a SpriteRenderer and Animator. The initially empty arrival rig now references the supplied OrangePortal art/controller with the source portal's directional orientation. This is an authoring-time copy, not an automatic runtime linkage; later source-art changes do not overwrite arrival art.
- Default Animator states are `Open`, `Idle`, `Close`, played directly without parameters or Animation Events. State names are configurable on the view. Match `Open Seconds` / `Close Seconds` to the authored clips; animation playback is unscaled.
- Keep portal physics and interaction on the prefab root and animate only its directional child. Player visibility roots no longer own motion; the Hub presenter owns whole-player fall/spin with physics suspended during descent.
- Source/arrival renderer order starts at Default/62, above the standard wall layers. Final sprite size, pivot, wall alignment and player occlusion are art-review tasks.
- The rig retains fade duration and portal/fall height (3 units initially, kept visible rather than using Hub's offscreen start). Fall duration, curves, spins, landing lock, shadow, sound and wake tuning now come from `PF Player/PlayerHubSpawnPresentation2D`; the independent rig fall-duration/landing-hold fields were removed. Current player authoring uses a 0.85-second fall, two spins, 90-degree landing, 2-second landing lock and input-or-automatic wake after up to 2 more seconds. Hub tutorial gates do not run during portal arrival.
- The supplied controller currently has a looping `OrangePortal` state, not separate Open/Close clips. Arrival uses that idle state and the view's scale open/close fallback (0.25 seconds each). Dedicated Open/Close states can still be authored later without changing travel code.

`Tools/Dungeon/Install Dead End Return Portals` creates missing prefabs/anchors and wires the three production corridor builders and player. It preserves existing prefab art and configured player visual bindings, and refuses to save an already-dirty corridor scene. The full theme scene reconstruction tool does not automatically invoke this focused installer; reapply it after reconstructing those scenes. Visual-only previews do not create gameplay portals.

## Verification Entry Points

`DungeonReturnPortalPlayModeTests` covers cardinal placement, disconnected/blocked cells, actual connections versus authored sockets, reveal persistence, event/combat gates, split-family tracking, duplicate requests, cancellation during shared falling, safe landing, repeatable same-scene completion, directional prefab bindings and the current seed's room tile geometry for all three production themes. It also checks spin/shadow/physics state, closing while still lying, Hub-only completion/scene gating, and owner-checked cancellation. Run alongside `RoomMonsterWavePlayModeTests` and `ChestPossiblePlayModeTests` for encounter/reward regressions.

Headless tests are not a rendered animation review or a full production-actor playthrough. Manual checks still include active weapon presentation, camera/fade feel, authored props around chosen positions, all four supplied animations, and return after a real multi-wave/event encounter.

Promotion candidate: if additional same-scene transport features adopt these boundaries, consider promoting travel ownership/cleanup and authoring contracts with explicit approval. No broader transport abstraction is required for this implementation.
