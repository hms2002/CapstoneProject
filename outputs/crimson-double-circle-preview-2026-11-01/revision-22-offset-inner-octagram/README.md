# Offset inner octagram — revision22

Mode: Micro-fix. Standalone192×192 Aseprite preview; no Unity asset or runtime changes.

- [author-native.lua](author-native.lua) removes the intermediate concentric circles, including revision21's added circle, while preserving the innermost gold circle. Two new squares enclose that circle at22.5° and67.5°, giving eight tips offset22.5° from the outer octagram. The intermediate border pair is also removed so it does not cross the new square outlines. Corner ornaments and central runes remain absent.
- [inner.aseprite](inner.aseprite) includes an independently editable inset-octagram layer. [inner.png](inner.png), [inner-inset-star.png](inner-inset-star.png), [combined-192.png](combined-192.png) and [enlarged preview](combined-3x.png) export the new center. The outer circle, outer octagram, larger runes and medallions are unchanged.
- [comparison-before-after.png](comparison-before-after.png) shows revision21 left/revision22 right. [opposing-rotation.gif](opposing-rotation.gif) and [rotation-native.aseprite](rotation-native.aseprite) update the preview while retaining120×80ms opposite circle rotation. Both octagrams belong to the inner-circle group, so their22.5° offset remains fixed during motion. This GIF is an Aseprite render, not Unity footage.

Verification: [pixel comparison](change-verification.json) confirms12 unaffected layers, intact central circle, eight offset tips, and changes restricted to intermediate rings plus the new square layer. [Native reopen](media-verification.json) verifies192px PNGs and480px/120-frame GIF timing. Static and rotated views were visually inspected. [Protected hashes](protected-verification.json) confirm the previous preview, game circle assets and runtime presentation source are unchanged.

Unity import, compilation and Play Mode were not executed for this preview-only revision. Integration remains pending user selection. Doc Impact Check: SessionLog only; ErrorLog/DecisionLog unchanged, no Presentation HTML stale candidate.
