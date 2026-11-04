# Native pixel cleanup

The previous image-generation reduction left uneven strokes, crowded intersections and ambiguous glyphs. This revision rebuilds the existing Aseprite assets on their native grids, following the user's established Aseprite workflow. No new image-generation pass or source-image downsampling is used.

- [Inner PNG](inner-preview.png), [inner Aseprite](inner.aseprite): 64 × 64; complete octagram, central circle and enclosing rune band. Twelve outer-band marks including four small medallions; four minimal central marks.
- [Outer PNG](outer-preview.png), [outer Aseprite](outer.aseprite): 96 × 96; separate circular band with twelve spaced runes.
- [Native authoring script](author-native.lua): explicitly authored pixel paths and glyph bitmaps through Aseprite Lua. Editable layers separate circular rails, octagram, runes and accents. One-pixel line paths, consistent spacing and transparent gaps replace the prior thresholded shapes.
- Colors: `FFC65A` main geometry, `F16B32` secondary circle rails, `FFF0BF` sparse accents, all from the existing six-color palette; binary alpha. Inner/outer sizes, nominal16PPU, growth phases and opposite planar rotation are unchanged.
- [Full composite at 3×](charge-100-3x.png), [rotated pose](charge-rotated.png), [checks](preview-check.json). Verified native pixel mapping, source palette/alpha, full inner completion at50%, separate layers, staged bounds, circumference, rotation and pause/resume. Visually inspected full and rotated poses.
- Preview only. No Unity files changed. Browser DOM/layout, Unity Play Mode and final camera acceptance remain unexecuted; rotating/scaling a point-filtered low-resolution sprite can change its on-screen pixel footprint.
