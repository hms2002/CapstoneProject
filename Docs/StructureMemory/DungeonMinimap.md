# Dungeon Minimap

Status: structure memory, not a technical contract. Last reviewed: 2026-09-11.

## Purpose And Flow

- `DungeonMapRuntimeController` owns the active dungeon graph and discovery state. `DungeonMapDiscoveryModel` reveals visited rooms and their direct neighbors; UI resizing does not change discovery or saves.
- `DungeonMapGraphSnapshot.Create` builds room shapes and normalized interior icon anchors once per room template for that graph. Its temporary dictionaries do not retain templates across scenes.
- `DungeonMapRoomShapeBuilder.ResolveInteriorAnchor` in `DungeonMapModels.cs` computes eight-neighbor clearance to empty cells with a padded two-pass distance transform. It maximizes square icon clearance, then prefers proximity to the bounding rectangle center. Rectangle seams/overlaps are not boundaries; holes remain empty. Empty shapes use the center. Cost and temporary memory are O(grid width * grid height), not per frame.
- `DungeonMinimapPresenter` projects the snapshot through `DungeonMinimapNodeView` and line templates. The node consumes the cached icon anchor; discovery refresh and zoom do not recompute it.

## Live Room Contents

- Room roles and live content kinds are independent. `DungeonMapContentKind` currently supports closed chests, opened chests and hearts. `DungeonMapContentModel` aggregates source instance IDs into immutable `DungeonMapRoomContents` counts; duplicate notifications are idempotent and a closed-to-open replacement publishes only the final counts.
- `DungeonMapRuntimeController` owns one disposable `DungeonMapContentTracker` for its scene/graph. Configuration and re-enable bootstrap existing sources once, including restored chest states; afterward source events update only affected room views. Clear, disable and destroy detach all static source-event subscriptions. No additional singleton or per-frame scene search was added.
- `TreasureChest.WorldStateChanged` reports enable/disable and actual IsOpened changes, including restore. Do not use OpenedUi/FirstOpenedUi as the map's truth: a real opening can happen without UI success, and a restored opening need not open a UI at all.
- `FieldHealPickup2D.WorldStateChanged` reports enable/disable, drop destination changes, landing and successful collection. `GroundPosition` uses the landing destination during a drop so the visual arc does not move map membership. Full-health collection attempts leave the heart visible; successful collection removes it before delayed Destroy. Destruction/disable also unregisters it.
- Sources do not reference map/UI classes. The tracker filters by scene and uses occupied shape rectangles to assign a room. `DungeonGenerator` passes `DungeonRoomBuilder.FloorTilemap` into the map controller; the tracker converts source world positions through `WorldToLocal` and `LocalToCellInterpolated` before comparing the graph. The graph's legacy `WorldBounds` name refers to dungeon layout cells, not Unity world units. Hearts/objects on a connector or just beyond a room edge group with the nearest actual room shape; stable placement IDs break distance ties.
- `GetRoomContents` returns empty data for unvisited rooms; NodeView independently gates content badges on Visited. Room-role silhouettes keep their previous discovery behavior.
- Content counts are projections of current live objects, not a new save owner. Opened chests follow the existing object-state restore path. A heart which the existing scene persistence does not restore is not kept as a stale map icon.
- Coordinate conversion happens on source notifications/bootstrap only, not in a new Update scan. The same grid is retained when the controller is disabled/re-enabled. A null grid remains an identity-coordinate compatibility path for synthetic graphs/tests, not the generated scene path.

## Authoring And Display

- Prefab: `Assets/_Project/Prefabs/UI/Map/DungeonMinimapView.prefab`.
- Icon mapping: `Assets/_Project/ScriptableObjects/UI/Minimap/DungeonMinimapIconSet.asset`. Match RoomType to normal/silhouette sprites and tints.
- The same asset's `Content Icons` list maps ClosedChest/OpenedChest/Heart to sprites/tints. Initial references reuse the project's chest closed/opened sprites and field-heart sprite; opened chest tint is subdued.
- The authored RoomTemplate contains an IconGroup with the existing role icon and three `DungeonMinimapContentBadgeView` children, each with an Image and TMP count label. Counts display only above one, capped visually at `99+`. No badge GameObjects or labels are created by runtime code; ordinary room-template cloning supplies them.
- NodeView packs visible icons into at most two columns and fits the group within the automatically selected interior's safe square. Safe area is calculated once when configuring each view; content changes only lay out the few existing RectTransforms. The current-room overlay is behind icons, and count labels stay within their badge rectangles.
- `GlobalUIRoot` supplies the presenter prefab and icon set through `GameplayHUDCanvas`.
- `DungeonMinimapSizeController` owns only local presentation state: collapsed -> normal -> expanded. Plus steps up; minus steps down; endpoint buttons are disabled. Initial state is normal, and the choice lasts for that UI instance, including scene changes.
- `MapBody` contains MapFrame and LocationName. Its authored scale is captured in Awake and restored exactly at normal size. Expansion multiplies X/Y by sqrt(expandedAreaMultiplier), default sqrt(6), so the area is six times normal. Buttons are siblings, not children of MapBody: they remain normal-sized and usable while folded.
- The root uses a top-right anchor. The body grows left/down; toolbar buttons stay at the top-right. No runtime creation of buttons, text, Canvas or EventSystem is used.
- Presenter visibility controls alpha, interactability and raycast blocking together. When no graph is available the entire minimap, including controls, is hidden. Only button background graphics receive raycasts; the map itself remains display-only. Existing Gameplay HUD scene visibility is preserved.
- Button listeners are attached in OnEnable and detached in OnDisable. Disabling the UI does not reset its size preference. No new singleton, input action, saved preference or gameplay pause is introduced.

## Extension Points And Limits

- Change button appearance/layout on the prefab. Change expansion area on its size controller, not on the room generation profiles.
- Automatic placement and group-size fitting avoid empty concave regions. In very small rooms multiple badges/counts may be tiny in the normal-sized map; expansion improves readability. Per-room manual anchors are not implemented.
- `DungeonMapContentPlayModeTests.cs` contains pure aggregation/shape-fit tests and Unity source-lifecycle/discovery tests, including translated/rotated/nonuniform grids, drop landing positions, opening/collection and controller re-enable. Test pickups require a concrete CircleCollider2D before adding FieldHealPickup2D; its abstract Collider2D requirement alone cannot create a valid test object. Source collection, scene lifecycle and actual UI rendering require Play Mode.
- Actual button clicks, mouse input coexistence, unusual aspect ratios and scene visibility should be checked in Unity Play Mode. The standard 1920x1080 reference layout fits the default expanded body.
- This is a feature-level structure map; no Architecture/Contracts promotion is required for this local display change.
