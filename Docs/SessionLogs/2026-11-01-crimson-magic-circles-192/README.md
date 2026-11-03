# Approved192px Crimson charge art integration

Mode: Implementation. Apply approved revision24 art to the existing right-click charge while preserving charge behavior, ranges and opacity logic. Allowed scope is the four circle source files, their necessary presentation-size calibration, existing verification tools/tests and task memory. No scene/prefab/schema/package/settings or gameplay changes.

## Changed files and reasons

- `Assets/_Project/Art/Sprites/VFX/CrimsonBoundary/MagicCircle/inner.png`, `outer.png`, `inner.aseprite`, `outer.aseprite`: replace the old128px sources with the exact approved [revision24](../../../outputs/crimson-double-circle-preview-2026-11-01/revision-24-touching-orange-geometry/README.md)192px sources. Touching circles/squares,22.5° interleaving, two orange geometry colors and yellow-only runes are retained. Existing paths, GUIDs, fileIDs and import settings are preserved.
- `inner.aseprite.meta`, `outer.aseprite.meta`: Unity's native importer regenerated sprite rectangles/pivot/UV/canvas/cache sizes for the new source dimensions. Only those derived fields changed; GUIDs and sprite IDs remain stable. The runtime prefab uses the PNG sprites, whose metadata remains byte-identical. [Inner metadata diff](inner-importer-changes.json), [outer metadata diff](outer-importer-changes.json).
- `Assets/_Project/Runtime/Presentation/VFX/Presentation/CrimsonBoundaryChargePresentation.cs`: change only the inner artwork's visible fraction argument to128/192. The previous86/128 gameplay ratio remains unchanged. This calibration keeps the actual visible diameter4.03125 at50% charge, rather than accidentally reducing it to4.0 with the new raster bounds. Outer full diameter remains6.
- `Assets/_Project/Tests/PlayMode/Procedural/CrimsonChargePlayModeTests.cs`: assert192px imported sprites and use128/192px painted diameters when checking world size. Add a75% release case to the existing flight/fade test alongside50%/100%.
- `Assets/_Project/Editor/Tools/Weapons/CrimsonChargeVisualValidation.cs`: update source/dimension checks to revision24, preserve earlier capture output by using a new directory, verify charge/flight/fade alpha, and expose the existing capture through `Tools > Weapons > Crimson Boundary > Capture and Validate Charge Visuals`.

## Preserved behavior

- Hold0–50% grows the inner circle;50–100% grows the outer circle. Opposite rotation remains37.5°/s. Cursor follow, release target lock and all gameplay ranges/timings/damage/cooldown are unchanged.
- Holding, including100% hold, uses alpha.5. Valid partial/full release immediately uses alpha1. Circles keep their size through flight; impact fades them over.25s. Gauge, collision-free diagonal meteor, particles and cleanup are unchanged.
- Runtime code was compared to the pre-task snapshot: exactly one source-fraction argument and its explanatory comment changed. All state/opacity expressions remain byte-identical after newline normalization.

## Verification

- MSBuild `Editor.csproj` and `ProceduralPlayModeTests.csproj` exited0: [Editor build](msbuild-editor.log), [test assembly build](msbuild-tests.log). Generated projects include all three changed C# files. Existing warnings remain; no new namespaces, assemblies, serialized fields or lifecycle methods.
- [Source verification](source-verification.json): all four installed art files match approved source SHA256; PNGs are192×192; prefab bindings and Point/no mipmaps/uncompressed/16PPU metadata are preserved. Calculated sizes at0/25/50/75/100% match the existing ranges.
- [Protected verification](protected-verification.json):171 files unchanged, including prefab/material/shader, PNG metadata, other Crimson VFX and charge gameplay/data/ability files. The only two protected-file differences are the expected Aseprite importer-derived fields described above.
- Prior preview evidence verifies geometry colors and3,840 raster tangency checks across120 Aseprite frames. This proves the approved source design, not native Unity rendering.
- **All four native Unity art imports completed**, confirmed in [Editor import log](unity-art-import.log). Native GPU capture and Play Mode execution were **not run** in this slice. The open Editor has no connected Pipeline/MCP; window activation repeatedly timed out and a refresh input was rejected after concurrent user input. No second Editor was opened, no user process was closed, and no old capture/test result is presented as current evidence.

## Remaining checks and documentation impact

Run the capture menu above and `CrimsonChargePlayModeTests`. Confirm the new192px design in the actual game, translucent full hold, opaque partial/full release and unchanged impact fade. A Player build/GPU profiling was not run. There is no new Inspector wiring requirement.

Doc Impact Check: SessionLog + existing StructureMemory + DecisionLog. ErrorLog unchanged. No Architecture/Contracts update or Presentation HTML stale candidate.
