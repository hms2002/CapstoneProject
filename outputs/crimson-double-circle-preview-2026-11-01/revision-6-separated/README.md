# Sparse runes and separate whole-circle layers

- Scope: preview revision only; no Unity scene, prefab, script, import settings or gameplay data changed.
- Inner layer: [64 × 64 PNG](inner-preview.png), [editable Aseprite source](inner.aseprite). This contains the complete magic circle, its enclosing rune band, both overlapping squares and the center circle/band. All rotate together counterclockwise. Outer band art targets twelve simple marks including four medallions, central band eight marks.
- Outer layer: [96 × 96 PNG](outer-preview.png), [editable Aseprite source](outer.aseprite). Separate sparse twelve-glyph annular ring with no interior artwork, rotating clockwise.
- Both are sampled as whole independent sprites. No runtime radial split, angular fill, procedural rail drawing or artwork recoloring remains in this preview. The view selector shows either layer alone.
- Charge: 0–50% scales the complete inner layer to 64px diameter. 50–100% retains it and expands the separate outer ring from 64px to 96px diameter. No 3D tilt.

## Pixel-density evidence

- Existing Crimson charge assets: `meteor_core_01.png` 32 × 32, `fire_puff_01.png` / `smoke_puff_01.png` 48 × 48, `impact_core_01.png` 64 × 64 under `Assets/_Project/Art/Sprites/VFX/CrimsonBoundary/ChargeSixColor/Frames/`.
- Their importer uses Point filtering and 16 PPU. The current charge data has `maximumDiameter: 6`; a nominal full-size targeting ring therefore spans 96 logical pixels. Inner uses 64 logical pixels at that same nominal density.
- Other project VFX are heterogeneous: e.g. AttackSmoke and LightningSparkParticle import at 32 PPU, FireEffectParticle_sheet1 at 100 PPU. This revision matches the related Crimson six-color family, not a nonexistent project-wide universal PPU setting.
- Generated art was prepared in Aseprite: isolate pale line cores, crop to robust line bounds, coverage-based reduction to native size, quantize to the existing palette's `FFC65A`, and binary alpha. One foreground color is intentional for the thin indicator. Native PNG and Aseprite files are the actual preview inputs.
- At full size and zero rotation, one source pixel maps to one logical canvas pixel, verified exactly. Scaling, sprite rotation and the existing particle sizes can change on-screen pixel footprints; final camera/gameplay acceptance remains unexecuted.

## Verification

- [Full composite at 3× nearest-neighbor zoom](charge-100-3x.png), [inner only](layer-inner.png), [outer only](layer-outer.png), [check results](preview-check.json).
- Offline exact-JavaScript renderer checks passed at 0/25/50/75/100%, including complete inner artwork at 50%, separate layer switching, source-pixel mapping, palette/alpha, scaled bounds, full circumference, opposite rotation and pause/resume.
- Read-only checks confirmed current 16 PPU/Point import and maximumDiameter 6. Visually inspected native assets and the combined 3× view. Browser DOM/layout and Unity Play Mode were not run.

## Built-in image_gen prompts

### Inner — edit of revision 5

Use case: precise-object-edit. Edit target is the provided gold magic-circle sprite. Make a MUCH SIMPLER, LESS DENSE 64x64 logical-pixel sprite, displayed enlarged with hard pixel edges, matching 16-pixels-per-world-unit low-resolution 2D game effects. Preserve the COMPLETE composition in this ONE inner-circle asset: outer circular rune band enclosing two big equal squares offset 45 degrees (8-point star), then a small central circular band and empty round center. All of these belong to the same sprite. Reduce lettering aggressively: on the outer band exactly 12 well-separated SIMPLE glyphs total, including 4 cardinal glyphs enclosed in tiny round medallions and just 2 other glyphs in each quadrant. At least one glyph-width of clean empty space between glyphs. On the small central band only 8 tiny simple glyphs, wide empty gaps. Glyphs are chunky 2x3 or 3x4 logical-pixel marks, not calligraphic words, no filigree, no tiny dots or texture. Keep the big two squares clearly legible BETWEEN the outer band and central circle, not inside the center. Thin ONE-logical-pixel lines on the 64x64 grid. Flat gold #FFC65A, restrained orange #F16B32 and pale highlight #FFF0BF only; no bevels, glow, bloom, gradients, antialiasing, sparks or orange background fills. Preserve true transparency in all empty spaces, with a clear margin around the complete circle. Perfect overhead circle, no perspective. Do NOT add the separate larger outer ring: this asset is only the complete inner magic circle. The reduced rune count and clear low-resolution silhouette matter more than ornament detail.

### Outer — new independent asset

Generate ONE standalone 2D pixel-art sprite, a sparse outer MAGIC RING layer for a small game effect. It is a monochrome stencil / alpha-mask sprite in solid pale ivory #FFF0BF ONLY, on a true transparent background. NO other colors, NO glow or shading. Logical art resolution 96x96 pixels, enlarge using nearest neighbor only if needed. Perfect overhead circular annulus, two clean thin concentric outlines (outer radius 46 logical pixels, inner radius 39 logical pixels), each ONE logical pixel thick. Exactly TWELVE separate simple rune glyphs evenly spaced around the annulus, like 12 clock-hour locations. Each glyph only 3x4 or 4x5 logical pixels, with large empty gaps at least twice the glyph width. Glyphs may look like small L, angled E, hook, fork, zigzag marks; avoid diamonds, stars or dots. Rune strokes one logical pixel thick. The annulus has NO FILLED BACKPLATE: the space between the two circles must be transparent everywhere except those twelve small glyphs. Inside the smaller circle is COMPLETELY empty transparency, no central symbol, no squares, no inner circle, no cross, no extra marks. No medallions. Crisp stepped pixel edges, no gradients, bloom, antialiasing, sparkle, texture, border frame, or background. Keep a small transparent margin around the ring. Intended to rotate independently outside another separate magic-circle sprite.
