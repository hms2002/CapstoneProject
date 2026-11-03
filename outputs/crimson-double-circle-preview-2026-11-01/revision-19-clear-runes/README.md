# Crimson circle — clear runes and empty medallions

Mode: preview Implementation. Current request authorizes clearer letters, restoration of the small circles with empty interiors, an opposing-rotation sample and original PNG delivery. Only sample art, authoring/verification helpers and SessionLog are changed; Unity integration is outside this slice.

## Deliverables

- [Opposing-rotation GIF](opposing-rotation.gif): inner counterclockwise, outer clockwise. Full-charge demonstration, 480×480 at exact 3× native preview scale, 120 frames × 80ms = 9.6 seconds per loop. This is a review animation, not a proposed 120-frame runtime sprite asset.
- Original transparent PNGs, each 128×128: [combined](combined-128.png), [inner](inner.png), [outer](outer.png). Shared center63.5,63.5; visible diameters86/128px. [Combined 4× view](combined-4x.png).
- Editable sources: [inner](inner.aseprite), [outer](outer.aseprite), [rune atlas](glyph-atlas.aseprite), [native review animation](rotation-native.aseprite). Inner/outer sources retain separate borders, rim marks, octagram, medallions and rune layers.
- [Native Aseprite authoring script](author-native.lua) reproduces the assets. The neighboring `inner-*` and `outer-*` PNGs expose those same authored layers; [medallion](medallion.png) is the restored original frame.

## Changes

- Replaced small branched bitmap symbols with eight distinct, connected 5×9 stroke designs. Gold is solid `FFC65A` across every rune, eliminating small color interruptions. Overall palette remains `B63D28`, `F16B32`, `FFC65A`, `FFF0BF` with binary source alpha.
- Glyphs still face radially outward. Each preview orientation is rasterized from connected endpoints, avoiding repeated resampling of a tiny bitmap. [Direction atlas](glyph-directions-4x.png) shows all eight designs at sixteen headings.
- The four small medallions use exactly the former 10×10 frame and contain no characters. Their empty interior is transparent in the PNGs.
- Outer rune separators remain absent. Main concentric borders retain their exact source pixels. Their circular shape is invariant during rotation; rune positions/headings, rim marks and the octagram rotate. Main circle/octagram layout is retained.

## Verification and limitations

- [Native checks](native-verification.txt): 380 checks passed, including saved Aseprite reopen/PNG equality, exact restoration of the old medallion, original concentric borders, transparent medallion interiors, all120 exported GIF frames/timings, exact colors and uniform3× scaling. Rendered frame2 confirms the inner medallion moves left from the top and the outer F moves right.
- [Pixel checks](pixel-verification.json): 128 rune/direction samples each form one connected shape; sixteen distinct outer runes; solid glyph color, exact four-color family, binary alpha and absent outer rune dividers. Helpers: [native checks](verify-native.lua), [pixel checks](check-pixels.cjs).
- Visually inspected the original enlarged PNG, direction atlas and [rotation contact sheet](rotation-contact.png).
- This revision provides a full-charge rotation sample; charge progression was not retested. No Unity import/compilation/Play Mode or actual-camera acceptance was executed. The preview redraws connected strokes and circular geometry at each orientation; ordinary bitmap SpriteRenderer rotation can sample differently. A future integration must explicitly choose and validate its rendering method. Original PNGs are delivered without a claim that simply rotating them reproduces this preview pixel for pixel.
- Doc Impact Check: SessionLog only. ErrorLog/DecisionLog/StructureMemory unchanged; no Presentation HTML stale candidate.
