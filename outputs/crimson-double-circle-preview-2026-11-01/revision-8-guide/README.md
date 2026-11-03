# Guide-informed 25% resolution increase

Reference: [Pixel Art Animation Guide KO](C:/Users/nadom/Downloads/Pixel_Art_Animation_Guide_KO.pdf). Read all23 pages as text; rendered all pages and visually inspected the relevant layout/line/output pages3,4,18. The attachment is reference material, not repository instructions.

## Applied guidance

- Pages2–4: design for actual display size, preserve negative space and control curve rhythm/accidental thickness. Native Aseprite paths were redrawn at the new dimensions; the previous PNG was not enlarged.
- Pages3,5,7: spend extra pixels on recognizable glyph shapes and clearer spacing, with color roles and quiet areas. Rune count stays unchanged:12 outer,12 around the inner sigil including4 medallions,4 small center marks. Regular runes now have distinct5×5 shapes; medallion symbols4×4. Existing gold/orange/ivory palette retained.
- Pages11,17–19: retain fixed pivots, inspect rotation and light/dark backgrounds, and distinguish source filtering from final camera output. Full, isolated, rotated and lighter-floor views were checked. Point sampling is not claimed to solve every rotated-line issue.

## Changed assets and size

- [Inner PNG](inner-preview.png) / [Aseprite](inner.aseprite):64→80px per side. Complete octagram, center circles and surrounding band remain one rotating layer.
- [Outer PNG](outer-preview.png) / [Aseprite](outer.aseprite):96→120px per side. Independent ring rotating in the opposite direction.
- Both changes are25% per axis, or56.25% more canvas pixels. Proposed20PPU preserves world diameters4 and6 units:80/20=64/16 and120/20=96/16. No Unity import settings were changed. This deliberately increases density relative to the existing16PPU Crimson family, following the user's latest request; actual camera consistency still needs native validation.
- [Authoring script](author-native.lua) retains separate editable rails, octagram, runes and accents. [Interactive-preview verification](preview-check.json) covers staged0/25/50/75/100% growth, whole inner completion at50%, layer isolation, source-pixel mapping, palette/alpha, circumference, rotation and pause/resume.
- [Full view at3×](charge-100-3x.png), [lighter floor](charge-light.png), [rotated pose](charge-rotated.png). Preview canvas240×180 corresponds to the same world view at proposed20PPU; integer3× inspection was used.
- Preview only. No Unity scene, prefab, script, gameplay, camera or importer changes. Browser DOM/layout, Unity Play Mode and final game-camera acceptance were not executed.
- Temporary PDF review renders/text remain in `tmp/pdfs/crimson-guide-read/`; automatic approval review blocked deletion as policy-disallowed. The original PDF was not modified.
