# Reveal silhouette inspection — 2026-10-12

Read-only investigation of source PNG alpha and serialized authored geometry. No source PNG, prefab, runtime script, Unity state, or save file was modified by this inspection. The Python script writes only this evidence directory. The projections are annotated analytical images, not Unity captures.

## Finding

Putting `PageCover` after both Creature pages corrects the main Reveal draw-order defect, but its old rounded silhouette still fails to cover the expanded portrait's two upper corners. The `Illustration` rectangle is `[43,-56,594,389]` in Book coordinates. Cover 1.png is 896×720, displayed with PreserveAspect in the 2320×1480 Book rectangle; its height gives 1480/720 UI units per PNG pixel. The Book tile is 816×720, so the apparent 40-pixel horizontal coordinate offset is normal centered alignment.

With the existing cover geometry, nearest-point alpha inspection sampled every 0.5 UI unit:

| Target | Exposed rectangle area at the first frame | Exposed upper edge |
| --- | ---: | ---: |
| Expanded Illustration 594×389 | 1.70319%, 3,935.5 UI² | 25.5892% |
| Monster title 550×52 | 3.35839%, 960.5 UI² | 6.1818% |
| Creature left title 550×52 | 3.43182%, 981.5 UI² | 6.1818% |
| Monster IdleFrame 550×250 | 0% | 0% |
| Creature footer buttons and page text | 0% | 0% |
| Item title group and icon, layout-derived | 0% | 0% |

TMP rectangle exposure does not imply exposed text glyphs; the text is centered and the missing pixels are in the corners. The Illustration contains full-width border and gradient presentation, so its upper-corner exposure is relevant even where the fitted portrait has transparent pixels.

The Item title group has serialized width 0 because HorizontalLayoutGroup + ContentSizeFitter owns its width. Its authored child widths 70 + 194.09, zero spacing/padding, and childControlWidth=false give the projected 264.09 width. The icon's 70-unit height extends 10 units above and below the 50-unit group. This is a layout-derived source inspection, not a native layout readback.

## Geometry-only candidates

| Cover change | Portrait exposure | Footer exposure | Opaque area outside Book alpha |
| --- | ---: | ---: | ---: |
| Existing geometry | 1.70319% | 0% | 0 UI² |
| Move +52 UI units upward | 0% | 94% | 21,889 UI² |
| Move +55 UI units upward | 0% | 100% | 24,985 UI² |
| Height 1540, upward 40 | 0% | 37% | 24,708 UI² |
| Height 1600, upward 29 | 0.25253% | 0% | 43,330.75 UI² |
| Height 1750, no upward offset | 0.92593% | 0% | 160,017.5 UI² |

Increasing the height also increases width because PreserveAspect remains true. This widens the mask's center gutter, exposing a strip at the portrait's left edge. It paints beyond the book silhouette and covers more brown border/decoration. Moving up fixes the portrait but moves the lower edge above the footer. A height 1480–1750 / 10-unit step and upward offset 0–60 / 2-unit step search found no configuration covering both the full portrait and footer. These candidates are not recommended.

This is visual overdraw, not a raycast change: PageCover has raycastTarget=false. The geometry should remain unchanged; source mask shape is the narrower correction. Whole-cover enlargement is especially inappropriate because it affects every future revealed frame, its gutter, bottom edge, and surrounding decorations.

The main bookmark rail remains under `RightPage/TabButtonGroup`, with center x707 (page right640 + anchored x67) and width100, giving x657–757. The group's x scale -1 mirrors it without changing its bounds. The baseline mask's outer opaque edge is approximately x639, leaving about18 UI units before the rail. Height1600 expands that edge to about690 and can overpaint the rail's left33 UI units; height1750 approaches753, almost its full width. A source top-only alpha addition preserves the existing x range and avoids this overdraw. Full-view order must still remain after PageCover.

## Minimal source-mask proposal

The content clip uses source files 1.png through 19.png. Frame 19 is the existing end frame, with 15 residual nontransparent pixels; BookPresentation hides the cover when sampling completes. This existing endpoint must remain unchanged. Frames 20–36 are not used by this clip and need no change for this task.

For used frames 1–18, preserve the existing transparent reveal pattern and existing pixels, and fill only the first-frame silhouette's rounded gap above each column's original upper boundary. The page column ranges are left `[138,436)` and right `[468,759)` in the 896×720 PNG. A flat upper edge at PNG row 197 covers the portrait top (approximately row 198) while remaining inside the existing flat book border. A column should be extended using that frame's color/alpha at the original first-frame boundary; a currently transparent boundary must stay transparent so the reveal progression remains intact. Frame 3 has a pre-existing one-pixel silhouette shift, so its original boundary needs a nearby lookup within two PNG rows rather than blindly reading the first-frame boundary. Do not scan farther into intentionally revealed holes or replace later frames with the first-frame shape.

`coverage-report.json` includes the per-frame additive-pixel proposal and the current top-band alpha metrics. Subsequent frames intentionally expose more of the content; their raw exposed percentages are not defects by themselves. The corrective condition is removing the persistent rounded gap while preserving the original pixel disappearance sequence.

## Evidence and verification limit

- [Report](coverage-report.json) — geometry, six candidates, search result, all source-frame top-band metrics, per-frame additive-mask proposal.
- [Inspection script](inspect_reveal.py) — reproduces evidence from source files.
- [Existing geometry projection](projection-baseline.png).
- [Upward-shift projection](projection-raise_55.png).
- [Height-enlargement projection](projection-height_1750.png).

No Unity render, import, test, compile, or actual ProtoTypeHub play was performed for this inspection. After the approved integration fix, native early/middle/final Reveal captures must verify the entire illustration and footer, preserved Book outline, bookmark rail and full-view ordering.
