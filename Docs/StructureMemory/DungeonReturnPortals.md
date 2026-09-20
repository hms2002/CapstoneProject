# Dungeon Return Portals

Status: implementation navigation aid, not an Architecture or Contracts source of truth.

## Purpose And Boundaries

Return from generated dead-end rooms to the same dungeon's Start room without reloading the scene. The arrival view opens above a safe floor point, the existing Hub fall/wake presentation plays there, and the arrival view closes at landing rather than waiting for wake-up. The source interaction remains reusable. No BGM, route progression, reward, NPC or scene-transition rules are changed.

- Dead end means exactly one actual `DungeonSocketConnection`, regardless of unused authored sockets. Start is excluded; a terminal Boss entrance room is included and retains its existing boss-travel gate.
- Graph-first generation now reserves a distinct farthest degree-one endpoint for each Start exit, including exits participating in a cycle. A shared cycle leaf is not counted for multiple exits. This is a graph guarantee, not a bypass of the builder's safe placement or encounter gates; see [per-exit terminal coverage](ProceduralDungeonRoomPipeline.md#per-exit-terminal-coverage-2026-09-13). Legacy layouts and visual-only preview behavior are unchanged.
- Combat rooms reveal after the final room wave and all pending/live split-aware units and encounter holds clear, with a short stable-clear delay. Doors opening early is not treated as combat completion.
- Event rooms with an encounter group reveal only after basic room waves finish and all live/pending units and encounter holds clear. Delayed initial spawns and gaps between waves remain hidden. Starting bell combat hides an already revealed portal and disables its trigger; clearing combat reveals it again. Saved reveal state cannot bypass current encounter readiness. Rooms without an encounter group still reveal on entry.
- An unavailable or obstructed destination blocks interaction rather than moving the player into geometry. A room with no safe portal placement warns and skips that portal instead of failing dungeon generation.

## Runtime Ownership

- The production portal owns a CapsuleCollider2D trigger (long axis 2.2, short axis 1.2). Configure aligns its long axis horizontally for Up/Down walls and vertically for Left/Right, without rotating/scaling the visual or accumulating size on reuse. This extends the old radius-0.6 circle by 0.5 units on each tangential side. Authoring utility creates the same capsule; legacy non-capsule colliders retain their authored shape.

- `DungeonReturnPortalPlacement.cs`: deterministic actual-connection checks, cardinal directions, reachable-cell flood fill, nearest-center and legacy opposite-wall selection. No prefab or physics ownership.
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

Start and each dead end are searched from just inside their connected socket. Only reachable floor cells are considered; wall tiles, solid props, hole tiles and hole traps constrain traversal. Traversal uses the configured return clearance, rather than a smaller point probe. Hole tiles are excluded even before physics colliders refresh.

Automatic portal candidates now prefer the safe cell center nearest the room bounds center. A concave room, central pit/prop or disconnected central island falls back to the nearest safe cell in the entrance's reachable component. Coordinate tie-breaks keep results independent of traversal order. `DungeonRoomBuilder > Dead End Return Portals > Prefer Return Portal Room Center` defaults to true. Disable it for legacy deepest opposite-wall/longest-segment/midpoint selection.

Both guides and automatic candidates validate the standing footprint including diagonal samples inside the room, solids, hole traps and other interaction triggers. An additional conservative box around the actual directional capsule rejects overlapping chest/NPC/bell interaction areas even when the center itself is clear. Physics buffer saturation fails closed. No safe candidate means warning and omission, not dungeon-generation failure or unsafe forced placement. Searches run during generation, not every frame.

Room Piece Editor's object section exposes a return portal direction and guide selection button. Select Up/Right/Down/Left, select the guide, add it using normal object placement, move it in Scene View and save the room. Existing ReturnPortal_DIRECTION Prop prefabs carry the direction through their ProceduralRoomAnchor slot and existing object serialization; cyan gizmos show position/orientation. Prefer one guide per room. A valid guide overrides both position and direction, even when it differs from the wall opposite the entrance. Multiple guides prefer the opposite direction, then Up/Right/Down/Left order. An unsafe chosen guide warns and falls back to automatic placement. No new room schema or installation step is required.

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
- Portal calls apply a sequence-local recovery multiplier of 0.5: the authored 2-second landing lock becomes 1 second and the additional 2-second automatic-wake window becomes 1 second. Falling, portal open/close, physics restoration and authored fields are not sped up or rewritten. Ordinary Hub playback uses multiplier 1, even after a portal sequence on the same component. Manual sleep-mode wait thresholds also scale for portal calls, but that mode still requires input to wake; no automatic-wake setting is changed.
- The supplied controller currently has a looping `OrangePortal` state, not separate Open/Close clips. Arrival uses that idle state and the view's scale open/close fallback (0.25 seconds each). Dedicated Open/Close states can still be authored later without changing travel code.

`Tools/Dungeon/Install Dead End Return Portals` creates missing prefabs/anchors and wires the three production corridor builders and player. It preserves existing prefab art and configured player visual bindings, and refuses to save an already-dirty corridor scene. The full theme scene reconstruction tool does not automatically invoke this focused installer; reapply it after reconstructing those scenes. Visual-only previews do not create gameplay portals.

## Verification Entry Points

`DungeonReturnPortalPlayModeTests` covers cardinal placement, disconnected/blocked cells, actual connections versus authored sockets, reveal persistence, event/combat gates, split-family tracking, duplicate requests, cancellation during shared falling, safe landing, repeatable same-scene completion, directional prefab bindings and the current seed's room tile geometry for all three production themes. It also checks spin/shadow/physics state, closing while still lying, Hub-only completion/scene gating, and owner-checked cancellation. Run alongside `RoomMonsterWavePlayModeTests` and `ChestPossiblePlayModeTests` for encounter/reward regressions.

Headless tests are not a rendered animation review or a full production-actor playthrough. Manual checks still include active weapon presentation, camera/fade feel, authored props around chosen positions, all four supplied animations, and return after a real multi-wave/event encounter.

Promotion candidate: if additional same-scene transport features adopt these boundaries, consider promoting travel ownership/cleanup and authoring contracts with explicit approval. No broader transport abstraction is required for this implementation.

## Portal interaction outlines (2026-09-21)
- DungeonReturnPortal forwards focus callbacks to DungeonReturnPortalView. The view writes _OutlineEnabled only on the selected directional body's existing SpriteRenderer, preserving other MaterialPropertyBlock values. Highlight requires a visible, fully opened, non-closing, active view.
- Close, encounter shrink/hide, HideImmediate, direction replacement and disable clear the old body's outline. Travel permissions, encounter completion and opening animation timing retain their existing owners.
- DungeonReturnPortal.prefab's four directional bodies and DungeonBossShortcut.prefab's BluePortal body use the existing white OutlineMaterial. Arrival-only views remain unhighlighted unless explicitly driven by an interactable.
- SceneTravelInteractable separately projects focus to its same-object outline-capable SpriteRenderer and retains the authored highlightTarget behavior; leave/disable clear both. ProceduralSceneTravelPortal already has the correct body material. Particles and unrelated non-outline travel renderers are not changed.
- Regression coverage is in DungeonReturnPortalPlayModeTests: four directions, property-block preservation, unhighlight/shrink/disable, boss shortcut opening/close and scene-travel leave/disable. Tests compiled against previously built dependencies; Play Mode not executed. Main full build was subsequently blocked by unrelated missing ShopAffectionDiscountEffect in RunModifierAggregationService.cs.

## Same-scene portal ability lifetime (2026-09-21)
- Return-to-Start and Start-to-Boss use `DungeonReturnTravel` and the explicit-position `TryPlayPortalArrival` path. Arrival preparation preserves running abilities on this path, including Flowering Bloom's runtime data, coroutine, modifiers and weapon-swap lock. The existing busy gate still rejects casting/exclusive execution before travel.
- Bloom's remaining duration continues on its existing scaled-time clock during travel; it is neither refreshed nor saved/restored. Natural expiry owns buff cleanup, swap unlock and cooldown start. Cancelling the arrival animation does not cancel Bloom.
- Ordinary Hub, death-return and scripted arrivals retain `ResetTransientRuntimeState`. Scene-transition cleanup is unchanged. No serialized asset migration is required.
- Regression entry points: `PortalArrival_PreservesBloomAcrossCompletionAndCancel_ThenUnlocksOnExpiry` and `ScriptedArrival_StillCleansBloomAndSwapLock` in `DungeonReturnPortalPlayModeTests`. Presentation is replaced by a silent test double; full in-game visuals, especially expiry during the falling/wake animation, still require manual review.
