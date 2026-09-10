# Dungeon Minimap

Status: structure memory, not a technical contract. Last reviewed: 2026-09-11.

## Purpose And Flow

- `DungeonMapRuntimeController` owns the active dungeon graph and discovery state. `DungeonMapDiscoveryModel` reveals visited rooms and their direct neighbors; UI resizing does not change discovery or saves.
- `DungeonMapGraphSnapshot.Create` builds room shapes and normalized interior icon anchors once per room template for that graph. Its temporary dictionaries do not retain templates across scenes.
- `DungeonMapRoomShapeBuilder.ResolveInteriorAnchor` in `DungeonMapModels.cs` computes eight-neighbor clearance to empty cells with a padded two-pass distance transform. It maximizes square icon clearance, then prefers proximity to the bounding rectangle center. Rectangle seams/overlaps are not boundaries; holes remain empty. Empty shapes use the center. Cost and temporary memory are O(grid width * grid height), not per frame.
- `DungeonMinimapPresenter` projects the snapshot through `DungeonMinimapNodeView` and line templates. The node consumes the cached icon anchor; discovery refresh and zoom do not recompute it.

## Authoring And Display

- Prefab: `Assets/_Project/Prefabs/UI/Map/DungeonMinimapView.prefab`.
- Icon mapping: `Assets/_Project/ScriptableObjects/UI/Minimap/DungeonMinimapIconSet.asset`. Match RoomType to normal/silhouette sprites and tints.
- `GlobalUIRoot` supplies the presenter prefab and icon set through `GameplayHUDCanvas`.
- `DungeonMinimapSizeController` owns only local presentation state: collapsed -> normal -> expanded. Plus steps up; minus steps down; endpoint buttons are disabled. Initial state is normal, and the choice lasts for that UI instance, including scene changes.
- `MapBody` contains MapFrame and LocationName. Its authored scale is captured in Awake and restored exactly at normal size. Expansion multiplies X/Y by sqrt(expandedAreaMultiplier), default sqrt(6), so the area is six times normal. Buttons are siblings, not children of MapBody: they remain normal-sized and usable while folded.
- The root uses a top-right anchor. The body grows left/down; toolbar buttons stay at the top-right. No runtime creation of buttons, text, Canvas or EventSystem is used.
- Presenter visibility controls alpha, interactability and raycast blocking together. When no graph is available the entire minimap, including controls, is hidden. Only button background graphics receive raycasts; the map itself remains display-only. Existing Gameplay HUD scene visibility is preserved.
- Button listeners are attached in OnEnable and detached in OnDisable. Disabling the UI does not reset its size preference. No new singleton, input action, saved preference or gameplay pause is introduced.

## Extension Points And Limits

- Change button appearance/layout on the prefab. Change expansion area on its size controller, not on the room generation profiles.
- Automatic placement maximizes available interior space, but cannot guarantee that a fixed-size icon fits a room that renders narrower than the icon itself. Per-room manual anchors and icon-size fitting are not implemented.
- Actual button clicks, mouse input coexistence, unusual aspect ratios and scene visibility should be checked in Unity Play Mode. The standard 1920x1080 reference layout fits the default expanded body.
- This is a feature-level structure map; no Architecture/Contracts promotion is required for this local display change.
