# Touching orange geometry — revision24

Mode: Micro-fix. Standalone192×192 Aseprite preview only; no game assets or runtime changes.

## Changes

- [author-native.lua](author-native.lua) derives the nested circles and squares from the existing outer sigil circle radius. Large-square vertices touch that circle; their sides and small-square vertices touch the separator circle; small-square sides touch the center circle. Central shapes grow to close the previous gaps. The22.5° offset remains.
- Geometry uses only the existing dark orange `B63D28` and bright orange `F16B32`. Yellow `FFC65A` is exclusive to runes, including removal of yellow from the outer rim glints. Rune positions and shapes remain unchanged. Flat discrete colors, no gradients.
- [inner.aseprite](inner.aseprite), [outer.aseprite](outer.aseprite), [inner.png](inner.png), [outer.png](outer.png), [combined-192.png](combined-192.png) and [enlarged PNG](combined-3x.png) export the revised geometry/palette. [Before/after](comparison-before-after.png) shows revision23 left and revision24 right.
- [Rotation GIF](opposing-rotation.gif) and [native animation](rotation-native.aseprite) retain120 frames×80ms. Two octagrams stay together in the inner rotating group; the outer rune ring rotates oppositely. The GIF is Aseprite rendering, not a Unity capture.

## Verification and limits

- [Native contact verification](contact-verification.json):32 geometric circle/square contacts per frame, all120 frames. All3,840 checks found a shared opaque pixel within2px of the intended tangent point.
- [Color checks](color-verification.json): every visible geometry pixel uses one of the two oranges, and every yellow composite pixel belongs to a rune. Seven unaffected layers, including both rune layers, are byte-identical. Binary source alpha retained.
- [Native reopen](media-verification.json) verifies192px PNGs and480px/120-frame GIF timing. Static and rotated samples visually inspected.
- [Protected hashes](protected-verification.json): previous preview, game circle artwork and runtime presentation source unchanged.

Game import, Unity compilation and Play Mode were not executed for this preview-only task. Integration remains pending user selection. Doc Impact Check: SessionLog only; ErrorLog/DecisionLog unchanged; no Presentation HTML stale candidate.
