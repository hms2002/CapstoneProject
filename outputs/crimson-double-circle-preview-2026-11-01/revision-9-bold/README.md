# Fuller glyphs and unified gold borders

- Scope: preview-only art refinement requested by the user. Keep80/120px assets, whole-inner ownership, independent outer ring and existing charge/rotation behavior.
- [Inner PNG](inner-preview.png) / [Aseprite](inner.aseprite); [outer PNG](outer-preview.png) / [Aseprite](outer.aseprite). All visible source pixels now use `FFC65A`; orange rails and ivory border accents were removed. Alpha stays binary.
- Circular borders use two authored pixel paths instead of one. The octagram also has heavier square/diagonal strokes. Major runes grow5×5→7×7 with thicker strokes, while counts remain12/12 plus4 central marks.
- Widened both rune bands to use existing blank space: inner lower rail radius30.5→27.5, outer lower rail50.5→47.5. Adjusted the octagram/central rings to fit the wider bands and larger symbols. Overall asset diameters and pivots are unchanged.
- [Authoring script](author-native.lua) keeps borders, octagram, runes and cardinal symbols in editable Aseprite layers. No generated-image downsampling or runtime border drawing.
- [Full composite3×](charge-100-3x.png), [rotated](charge-rotated.png), [lighter floor](charge-light.png), [verification](preview-check.json). Checked single-color output, two-pixel cardinal borders, native mapping, staged sizes/bounds, full inner at50%, layer isolation, circumference, opposite rotation and pause/resume; inspected the three composite views.
- Proposed20PPU still preserves4/6-unit world diameters. Unity source/assets/importers/camera unchanged. Browser DOM/layout, Unity Play Mode and final camera acceptance remain unexecuted.
