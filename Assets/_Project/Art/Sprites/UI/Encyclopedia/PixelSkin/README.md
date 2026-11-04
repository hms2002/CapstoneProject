# Encyclopedia pixel skin

Approved source: `outputs/encyclopedia-pixel-skin-preview-2026-10-31/revision-6-depth-boss-gradient/`.

- `EncyclopediaPixelSkin.aseprite` is the editable native atlas; PNG and `slices.json` are its exports (256×144, 23 named sprites, seven opaque palette colors).
- `BlackFadeTop/Bottom.aseprite` and PNG supply Boss-only transparent black gradients. Monster/NPC pages do not show these decorations.
- `DotEmptyLight/SelectedLight.png` supply up to five portrait indicators. All portraits remain accessible through a moving indicator window.
- `EncyclopediaSkinAuthoring.Apply` imports Point/uncompressed/full-rectangle sprites and authors the existing `EncyclopediaUI.prefab`. Run this last after older creature layout builders; those older builders describe the pre-skin layout.
- Frames, buttons, dividers, reward backgrounds and scrollbar parts are authored Images. Runtime code projects selection, discovery, affection and sprite choice; it does not create these decorations.

The original book sheet is separate. `Tools/Art/remove_encyclopedia_book_corners.lua` removes exact matching stationary inner-corner ornaments using native Aseprite and existing sheet colors. Its source backup, changed-region report and native preview are in `outputs/encyclopedia-skin-discovery-2026-11-01/`.
