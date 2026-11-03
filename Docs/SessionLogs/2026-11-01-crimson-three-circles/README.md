# Crimson three-circle charge presentation

Mode: Implementation. User requested two yellow central squares and independent rotation of their enclosing circle. Integration is complete; subsequent user instructions retire one-off acceptance tests/tools and require explicit requests before future test execution.

## Changes

- `Assets/_Project/Art/Sprites/VFX/CrimsonBoundary/MagicCircle/inner.png` and `.aseprite`: move the smallest orange circle out of the middle layer. Outer art remains byte-identical.
- `center.png`, `center.aseprite` and Unity-generated metadata in the same directory: new independent smallest circle with two yellow inscribed squares. All three sources are192×192; the center painted bounds are46×46. Palette remains B63D28/F16B32/FFC65A.
- `Assets/_Project/Runtime/Presentation/VFX/Presentation/CrimsonBoundaryChargePresentation.cs`: serialized center renderer; first-stage scale shared with the middle layer; separate clockwise56.25°/s rotation. Middle remains counterclockwise37.5°/s and outer clockwise37.5°/s. All layers stay opaque during charge/flight and fade together over0.25s after impact.
- `Assets/_Project/Prefabs/VFX/CrimsonBoundary/ChargeSixColor/CrimsonChargePresentation.prefab`: Unity-native authoring adds CenterCircle/GameObject/Transform/SpriteRenderer plus parent-child and serialized references. Existing133 records retained;131 unchanged, two receive one reference each. No runtime hierarchy creation.
- Authoring, capture and PlayMode acceptance sources were updated for three layers. Their later retirement is recorded in the test/tool cleanup session; historical before copies are retained here.
- [Aseprite source and previews](../../../outputs/crimson-double-circle-preview-2026-11-01/revision-25-three-circles/): central gold pair, independent three-group animation,192px combined PNG and clearly named magnified previews.

Gameplay charge times,50%/50% staging, range4.03125/6, cooldown, damage,0.06s/0.75-unit cursor damping, release locking, diagonal collision-free meteor and cleanup are unchanged.

## Evidence and limits

- Before the user's instruction to stop automatic testing: MSBuild Editor and ProceduralPlayModeTests assemblies both exited0. See [Editor build](msbuild-editor.log) and [test assembly build](msbuild-tests.log).
- Native Unity import and prefab authoring completed. The capture validator completed its art/import/binding/gauge/staged-size/alpha checks and195 native sequence frames, plus14 stills. [Native log](native-authoring-capture.log), [captures](captures/), [art report](captures/art-validation.txt).
- No clean-Console claim: the existing preview tool's native `SendMessage("Awake")` emitted a `ShouldRunBehaviour()` assertion before rendering. It did not terminate capture. This is a disposable capture initialization issue, not a PlayMode pass.
- [Read-only source report](source-verification.json): all installed sources match revision25;133 protected files unchanged, including original metadata, outer art, gameplay and material/shader. [Source checker](verify_sources.py) uses Pillow from the existing `tmp/pdf_deps` path. It reads images only.
- Aseprite's240 preview frames passed9,600 tangent-contact checks. GIF is480×480 with margins and2× enlargement; PNG source canvases are192×192.
- The22-case PlayMode suite was attempted through Test Runner but blocked in Enter Play Mode by the existing `PrewarmTraceRuntime.Flush` Win32 IO1224 file-lock exception. It was cancelled and Play mode was toggled off; no test pass is claimed. [Blocker](playmode-blocker.log). The user then stopped Computer Use with Escape; no further computer input was issued.
- Full combat/manual acceptance and Player build were not completed. The user will perform playtesting and has asked that future automated tests run only on explicit request.

Doc Impact Check: SessionLog + existing StructureMemory + DecisionLog. Existing ErrorLog already covers trace IO1224 and preview lifecycle assertions; no duplicate entry added. No Architecture/Contracts changes or Presentation HTML stale candidate.
