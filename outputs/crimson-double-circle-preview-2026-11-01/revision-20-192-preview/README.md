# 192px magic-circle preview

Preview only. No image in `Assets/` is replaced.

- Revision19's approved layout is redrawn in native Aseprite Lua on a192×192 canvas, instead of enlarging the128px bitmap. Width/height increase50%; canvas pixel count increases125%.
- Same four opaque colors: `B63D28 F16B32 FFC65A FFF0BF`. Rune strokes remain solid gold. No gradients or new ornaments.
- Preserve the two-square octagram, central circle, sixteen outer runes, empty four medallions and absence of rune-band dividers. Geometry and stroke width use1.5× coordinates; rasterized inner bounds are128px on the192px canvas.
- [Same display size comparison](comparison-128-left-192-right.png): left128px×3, right192px×2; both384px wide.
- [192px transparent composite](combined-192.png), [enlarged composite](combined-3x.png), [inner PNG](inner.png), [outer PNG](outer.png).
- Editable sources: [inner Aseprite](inner.aseprite), [outer Aseprite](outer.aseprite), [authoring Lua](author-native.lua).
- [Opposing rotation GIF](opposing-rotation.gif): outer clockwise, inner counterclockwise,120 frames×80ms. This is an Aseprite preview that redraws strokes at each angle, not a Unity capture or a runtime flipbook requirement. Game images remain128px.

Verification: [pixel checks](pixel-verification.json) confirm four colors, binary alpha,128 connected rune/direction samples,16 distinct outer runes, empty medallions and no dividers. [Native media reopen](media-verification.json) verifies PNG dimensions and GIF frame timing. The source and rotation sample PNGs were visually inspected.

Runtime opacity is a separate code change: alpha.5 while holding, alpha1 immediately on valid release, then the existing.25s fade after impact. See [implementation record](../../../Docs/SessionLogs/2026-11-01-crimson-charge-opacity/README.md).
