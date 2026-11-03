---
status: active
authority: structure-memory
category: presentation
last_reviewed: 2026-11-01
---

# Puddle pixel presentation

> Tool retention update (2026-11-01): completed task-specific skin/layout/migration authoring and acceptance tools named below are historical and have been moved outside Assets. See [current retained tools and archive](TestAndEditorToolRetention.md). Gameplay/save regressions and reusable profiling remain. Automated tests run only when explicitly requested.


Scope and verification status: [active task](../ActiveTasks/puddle-pixel-integration.md).
This map describes the presentation integration; it does not replace gameplay contracts.

## Selected artwork

- Beer body/ripples use revision16 RGB; foam and three rising bubble variants use revision17.
- Ground opacity remains166/255. Surface foam and bubbles have their own softer alpha.
- Fire ground and the separate dark near/bright airborne particle sprites retain revision15/16 art.
- Native Aseprite sources and reproducible authoring: `outputs/puddle-pixel-integration-2026-11-01/`.
- Engine textures: `Assets/_Project/Art/Textures/Puddles/ApprovedPixel/`.

## Ownership and flow

- `PuddleAreaBase` owns ground/ignition/absorb/projectile/consumed modes and collider state.
- `PuddleConversionService` retains the0.8-second gameplay ignition timing.
- `AlcoholPuddleArea` projects normalized ignition progress to both `IPuddleShaderVisual` and `IPuddleParticleVisual`; damage/buff/conversion rules are unchanged.
- `PuddleShaderVisual` retains the mesh, wall clipping, radius, sorting, property block and authored projectile cross-scaling.
- `PuddleShaderVisual.shader` samples the approved4-frame ground or16-frame center ignition sheets when `_PixelGroundEnabled` is enabled and the effective mode is ground/igniting. Existing materials and non-ground fallback rendering retain their former path.
- `PuddleParticleVisual` retains the existing particle owner and buff indicators. Authored pixel mode uses sprite-shaped local emission, two flame groups and center-first ignition. It creates no new runtime hierarchy for the new artwork.

## Authored assets

- `AlcoholPuddle.prefab` and `FirePuddle.prefab` remain the gameplay spawn targets.
- `M_PuddlePixelGround.mat` is a dedicated copy of the former ground material with approved sheets enabled. The original material is preserved for existing fallback/regression comparisons.
- Assign the new material to `PuddleShaderVisual.materialTemplate` as well as its renderer. The component restores that template in `Awake`/`OnValidate`; a renderer-only assignment is overwritten.
- `PixelNearFlames` and `PixelAirborneFlames` are authored children. Their references occupy indices0/1 of the existing `flameParticleSystems` array.
- Bubble sheet:5 columns/3 randomly selected rows,400ms life,7 births/s.
- Near flame sheet:8 frames,640ms life,62.5 births/s; last frame empty gives about35 visible flames.
- Airborne sheet:8 frames,640ms life,15 births/s; last frame empty gives about8-9 visible flames. Upward velocity and horizontal drift/noise distinguish it from the near layer.
- Seven textures use Point filtering, no mipmaps, uncompressed RGBA and unchanged NPOT dimensions. `EmissionShape.png` is a tight sprite with64 pixels/unit.
- `PuddlePixelArtworkAuthoring` is an explicit Unity menu/batch command. It updates the two prefab presentation sections through Unity APIs and disables superseded ParticleSystem renderers/emission without deleting their objects.

## Lifecycle and extension points

- `SetSurfaceRadius` rescales emission and particle artwork using the existing ground quad's2.2 multiplier and64px canvas.
- `ApplyMode(Igniting)` resets the visual ignition state. `SetIgnitionProgress` grows the emission region/rate, emits the first near flame at the center once, and removes bubbles covered by the advancing front.
- Existing absorb/consumed mode handling stops emission. Pixel particles additionally clear on owner disable, preventing stale particles on reuse.
- Buff arrows/rising streaks retain their previous authoring and lifecycle; their unrelated existing runtime hierarchy behavior was not rewritten.
- Extend art through the native sources/PNGs and explicit authoring command. Changing gameplay timing/radius belongs to the gameplay owners, not particle lifetime or texture frame count.

## Pitfalls and verification

- A preview GIF contains a floor and enlargement; engine assets must use the RGBA sheets.
- Sheet frame count is presentation data;16 frames follow normalized gameplay progress rather than changing the gameplay delay to the preview duration.
- Native image Y points down while local particle Y points up; center-burn bubble checks account for this.
- Do not run MSBuild into the shared `Temp/bin` concurrently with Editor startup or another chat's Unity batch run.
- Use explicit Unity object null checks when adding missing components in Editor tooling; `??` does not honor Unity's missing-object comparison.
- Particle velocity X/Y/Z use the same curve mode. Prewarming runs only in Play Mode; the native preview explicitly simulates its warm-up outside validation callbacks.
- `PuddlePixelVisualValidation` renders a disposable preview scene and never saves a gameplay scene. Focused PlayMode regressions live in `PuddleProjectileVisualPlayModeTests`, with existing alcohol contact/exposure suites covering preserved gameplay.
- Source-of-truth promotion: no new Architecture/Contracts document is needed for this local extension of the existing presentation contracts.
