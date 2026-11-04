# Octagram preview revision

- Scope: preview-only inner sigil revision; Unity integration is pending approval.
- Method: built-in `image_gen` edit, followed by Aseprite reduction to 127 × 127 for the existing pixel preview. Existing preview line-core filtering and palette mapping remain unchanged.
- Edit target: [previous inner source](../revision-2-sequential/inner-source.png).
- Outputs: [edited source](inner-source.png), [reduced preview](inner-preview.png), [composite at full charge](charge-100.png).
- Inner motif: two equal concentric squares offset by 45 degrees, forming eight tips, with one small circular outline in the empty center. Outer-circle artwork and revision 3 scaling/rotation remain unchanged.
- Verification: [offline check](preview-check.json); full-charge and 50% composites visually inspected. Browser DOM/layout and Unity Play Mode were not run.

## Final prompt — built-in image_gen

Use case: precise-object-edit. Edit target: the attached orange/gold pixel magic-circle sprite. Change ONLY the central geometric sigil: remove both triangles and the six-pointed star COMPLETELY. Replace with EXACTLY TWO overlapping closed SQUARE OUTLINES sharing the same center and size, one rotated 45 degrees relative to the other, forming a regular EIGHT-POINTED STAR (octagram). This is an axis-aligned square overlapping a diamond-oriented square, 8 straight sides total, 8 equally spaced outer tips, no triangles. Add ONE clean thin concentric small circular outline in the empty center of the octagram; its radius is about 0.30 of the whole magic-circle radius, with a clear gap to all square edges. Center must otherwise be transparent and empty. Keep the surrounding concentric circular rails and rune band at the exact same size, position, palette and design as the input. The squares fit just inside the rune band's inner circular rail. Flat top-down, perfectly centered, no perspective or tilt. Preserve crisp stepped pixel art with narrow continuous golden line cores and restrained orange edge, readable when reduced to 127x127. No additional ornaments, radial spokes, plus/cross decorations, central dot, flames, lighting rays, background, text, or new rings. Genuine transparent background, including the empty spaces inside the sigil; do not paint a black background.
