# Encyclopedia pixel skin — approval previews

Latest: [revision6 beveled skin and boss-only gradients](revision-6-depth-boss-gradient/README.md) restores UI depth, restricts black fades to Boss cards/portrait, and aligns the large boss portrait frame with its fades without corner motifs. Names remain without nameplates. The clean book corners, silhouettes, four-arrow full-view icon and maximum-five markers remain. The images and comparisons below retain revision1 for comparison.

Scope: create book-matching UI sprites and composed Monster/Boss previews before game integration. The latest display policy also includes unacquired relic silhouettes. This folder does not implement acquisition/discovery tracking or change any Unity asset, prefab, scene or runtime script.

## Review images

- [Monster page](monster-preview.png): original catalog Idle-first sprites, eight revealed examples and four black silhouettes. The same 4×3 grid and book remain. The 0-kill count and discovery states are mockup values.
- [Boss page](boss-preview.png): original Melta/Chloe portraits and existing heart sprites; two question-mark cards demonstrate undiscovered entries. Progress, reward text and story are illustrative layout content, not a save-data or localization change.
- [Skin sheet at 4×](skin-sheet-preview.png): nearest-neighbor presentation of the native sheet. Transparent regions are intentional.
- Same-scale comparison: [Monster before/after](comparisons/monster-before-after.png), [Boss before/after](comparisons/boss-before-after.png). Left: Korean `current-actual-*` native captures from 2026-10-12, cropped at (400,220) to1180×740. Right: the existing proposal PNG unchanged. Each board is2432×850; no source image is resized or retouched. New discovery states, typography and portrait fitting remain part of the proposal and are not a skin-only controlled comparison. Native Aseprite exit0/stderr0 and [pixel equality checks](comparisons/validation.txt) passed for both complete panels; final boards visually inspected. Reproduction sources and editable board files are in `comparisons/`.

These are **Aseprite compositions**, not new Unity screenshots. They reuse the flat-top book from the existing 2026-10-12 native capture and current source sprite references. Preview font rendering uses Galmuri9 through GDI single-bit rasterization; production TMP font/material and localization behavior have not been verified here. Monster previews follow `previewFrames[0]`, matching the current runtime, with `image` only as a fallback.

## Editable assets

- [EncyclopediaPixelSkin.aseprite](EncyclopediaPixelSkin.aseprite): native 256×144 source, layer and named slice per component.
- [EncyclopediaPixelSkin.png](EncyclopediaPixelSkin.png): transparent sheet, **21 sprites / 7 used opaque colors**, alpha 0 or 255. The design swatch list has eight candidates; the darkest text-only swatch is not used in the skin PNG.
- [Individual PNGs](assets/): CardFrame, SelectedFrame, LockedCard, IllustrationFrame, Nameplate, RewardPanel, ButtonNormal/Hover/Pressed, Divider, ArrowLeft/Right, DotEmpty/Selected, ScrollTrack/Thumb, UnknownQuestion/Medallion, DividerLine/Ornament and SectionLabel.
- [slices.json](slices.json): top-left sheet rectangles and pixel borders for future native Sprite Editor slicing. The two-part DividerLine + DividerOrnament preserves the ornament at different widths; the complete Divider is also supplied as a compact fixed-size alternative.
- [monster-preview.aseprite](monster-preview.aseprite), [boss-preview.aseprite](boss-preview.aseprite): composite image and separate editable text layer.

Style: warm paper, thin sepia contours, stepped corners, small square marks, ivory selection corners, dark nameplates. The characters and book are original game art, so their colors are independent of the seven-color UI palette. Future integration should use Point filtering, no mipmaps and lossless/uncompressed sprites; actual Unity import/slice authoring waits for approval.

## Reproduce and verify

1. `python prepare_sources.py` resolves catalog references, reads Unity sprite rectangles, and records original-source hashes. Python reads metadata only; it does not edit images.
2. Run `render_text.ps1` with this output directory and the project root to generate the preview-only text layers.
3. Run native Aseprite `-b --script-param outputDir=... --script-param projectDir=... --script make_skin.lua` after creating `assets/`.

[make_skin.lua](make_skin.lua) draws the actual pixel sprites, exports each component, and composes the source artwork. [sources.json](sources.json) identifies all 20 source sprite references and hashes; no character art is generated. The introductory built-in image_gen study used [this prompt](imagegen-concept-prompt.txt), but its broad shaded frames were not used in the final asset pack or previews. Final deliverables were authored/exported with native Aseprite; no API/CLI image generation fallback was used.

- Native Aseprite exit0, stderr empty; [native checks](aseprite-validation.txt).
- All 21 slice rectangles/exported PNG sizes valid, no slice overlap; [artifact checks](artifact-validation.txt).
- Seven opaque sheet colors, alpha only 0/255; both previews 1180×740.
- Galmuri label widths checked; [text report](text-validation.txt). Both final compositions visually inspected, including the correct monster Idle sprites and crown/nameplate separation.
- Four protected project files and all 20 referenced source sprites retained their hashes.
- Unity import, compilation, Play Mode, actual UI navigation and Player build were not executed. Approval is required before production integration; no approval is implied by generating these previews.

Doc Impact: SessionLog only. No StructureMemory, ErrorLog, DecisionLog, Architecture/Contracts or Presentation HTML changes/staleness.
