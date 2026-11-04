# Crimson charge opacity and 192px preview

Mode: Implementation. User requested a higher-resolution preview without replacing game images, and clarified that valid mouse release, including partial charge, is the moment that becomes opaque.

## Changed files and purpose

- `Assets/_Project/Runtime/Presentation/VFX/Presentation/CrimsonBoundaryChargePresentation.cs`: `ShowCharge` applies alpha.5 to both circles, including holding at100%. `BeginFlight` immediately applies alpha1 on valid release. Existing post-impact.25s alpha fade, size, rotation, damage and cancellation are unchanged.
- `Assets/_Project/Tests/PlayMode/Procedural/CrimsonChargePlayModeTests.cs`: extend existing stage and flight/fade cases to assert translucent hold and immediate opaque partial/full release.
- [Preview folder](../../../outputs/crimson-double-circle-preview-2026-11-01/revision-20-192-preview/README.md): native Aseprite192×192 redraw, split PNG/Aseprite sources, same-size comparison and opposite-rotation GIF. Palette/layout unchanged.192px means50% more pixels on each axis,125% more canvas pixels in total.
- SessionLog, feature StructureMemory and DecisionLog record the release-based opacity rule and preview-only scope. No ErrorLog change.

## Verification

- `Presentation.csproj` and `ProceduralPlayModeTests.csproj` MSBuild both exited0: [presentation build](msbuild-presentation.log), [test assembly build](msbuild-tests.log). Existing obsolete-API warnings remain. Both changed C# files are present in their generated projects.
- [154 protected file hashes](protected-verification.json) unchanged: Crimson VFX textures/Aseprite/metas, prefabs/materials, magic-circle shader and charge gameplay data. No serialized field, namespace, assembly reference, scene, prefab, shader, material or game image change in this slice.
- Source diff reviewed against the saved pre-task copies. Runtime changes are only two tint assignments and their explanatory comment; state ownership/lifecycle stay unchanged.
- Native Aseprite export/reopen and raw-pixel checks verify192px dimensions, exact four-color palette, binary alpha, connected runes in128 direction samples, empty medallions and120×80ms GIF timing. Same-size comparison visually inspected.
- Play Mode tests were **not executed in this slice**. The existing Editor was open; Pipeline was unavailable and native window activation timed out twice while Editor was compiling. No second batch Editor was opened and the user's Editor was not closed. Prior17/17 results belong to the previous integration and are not current-opacity test evidence.

## Remaining verification and documentation impact

Manual playtest and GPU appearance of the new opacity transition remain unconfirmed. Check full hold stays translucent; partial/full release snaps opaque; landing footprint keeps its size; impact fades within.25s. The192px art is still a preview requiring user selection before any integration.

Doc Impact Check: SessionLog + StructureMemory + DecisionLog. ErrorLog unchanged. No Architecture/Contracts update or Presentation HTML stale candidate.
