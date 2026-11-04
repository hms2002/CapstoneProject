# Clean octagram preview — revision21

Mode: Micro-fix. This is a revision of the192×192 preview only; game images and runtime logic are not changed.

## Changed files and purpose

- [author-native.lua](author-native.lua): remove all eight corner accents from the two overlapping squares; retain their straight outlines. Remove the four smallest central runes and add one orange concentric circle between the central gold circle and its surrounding orange border.
- [inner.aseprite](inner.aseprite), [inner.png](inner.png), [combined-192.png](combined-192.png): editable source and transparent exports of the simplified center.
- [comparison-before-after.png](comparison-before-after.png): revision20 on the left, revision21 on the right, both displayed at2×.
- [opposing-rotation.gif](opposing-rotation.gif), [rotation-native.aseprite](rotation-native.aseprite): updated preview animation; same opposite rotation,120 frames and80ms timing. This is Aseprite rendering, not a Unity capture.
- [outer.png](outer.png) and outer layer exports are retained unchanged. Inner rim, medallions and larger runes also retain their original pixels. The removed corner highlights were the only cream pixels, so the composite now uses three existing colors: `B63D28 F16B32 FFC65A`.

## Verification and limitations

[Pixel comparison](change-verification.json): ten unaffected layers are identical; central rune pixels are removed, the only base-layer addition is the middle circle, and octagram changes are confined to the eight corner accents. Native [PNG/GIF reopening](media-verification.json) verifies dimensions and animation timing. Static comparison and rotated sample visually inspected. [Protected files](protected-verification.json) confirm the previous preview and game circle images/runtime presentation source are untouched.

Unity import, compilation and Play Mode were not run: this task only changes preview files. Integration remains pending user selection. Doc Impact Check: SessionLog only; ErrorLog/DecisionLog unchanged, no Presentation HTML stale candidate.
