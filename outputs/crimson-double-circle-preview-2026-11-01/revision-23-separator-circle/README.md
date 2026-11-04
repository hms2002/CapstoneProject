# Separator circle — revision23

Mode: Micro-fix, preview only. Add exactly one orange circle between the small and large octagrams. The order is central circle → small octagram → separator circle → large octagram. Preserve both square pairs, the22.5° angular offset, rune layout and opposing group rotation.

Changed files: [author-native.lua](author-native.lua) adds one radius entry; [inner.aseprite](inner.aseprite), [inner.png](inner.png), [combined-192.png](combined-192.png), [enlarged PNG](combined-3x.png), [before/after comparison](comparison-before-after.png) and [rotation GIF](opposing-rotation.gif) export the revised design. Other exported layers retain their previous pixels.

Verification: [pixel comparison](change-verification.json) confirms14 unaffected layers and252 added pixels confined to the single separator annulus. [Aseprite native reopen](media-verification.json) confirms192×192 PNGs and480px GIF with120×80ms frames. Static and rotated samples visually inspected. [Protected hashes](protected-verification.json) confirm the previous preview and game circle assets/runtime presentation source are unchanged.

No game import, Unity compilation or Play Mode was executed for this art-only revision. Integration remains pending selection. Doc Impact Check: SessionLog only; ErrorLog/DecisionLog unchanged, no Presentation HTML stale candidate.
