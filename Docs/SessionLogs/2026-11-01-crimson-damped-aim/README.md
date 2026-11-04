# Crimson opaque charge and damped aim

Mode: Implementation. User requested removal of charge translucency and slight cursor-follow damping with a distance bound so fast motion cannot leave the circle behind. The stated distance was interpreted as maximum allowed separation, not a dead zone or a minimum enforced gap.

## Changed files and behavior

- `Assets/_Project/Runtime/Features/Items/Weapons/CrimsonBoundary/AbilityLogic_CrimsonBoundaryCharge.cs`: per-cast aim initializes at the cursor, then uses exponential damping with a0.06s response constant. Cursor separation is capped at0.75 world units (12 pixels at16PPU). Large jumps catch up to this bound immediately; remaining separation eases out. There is no dead zone and no overshoot/inertial velocity to reset. Gameplay delta time drives the response; zero delta time preserves the damped position.
- Gameplay owns the single filtered aim center. Existing viewport clamping is reused after filtering. `ShowCharge`, released meteor, impact effects and damage query all receive that same center. Release locks the currently displayed center rather than jumping to the raw mouse position. Each new cast starts at its new cursor position.
- `Assets/_Project/Runtime/Presentation/VFX/Presentation/CrimsonBoundaryChargePresentation.cs`: charge sprites use alpha1 at every hold level. Flight stays alpha1; only the existing post-impact0.25s fade changes opacity. Growth, opposite rotation, damage/range, cooldown, charge times, diagonal collision-free meteor and cleanup remain unchanged.
- `Assets/_Project/Tests/PlayMode/Procedural/CrimsonChargePlayModeTests.cs`: update hold opacity expectations; add frame-rate/lag/pause/reversal checks and an integration case for moving aim, release locking and matching damage center.
- `Assets/_Project/Editor/Tools/Weapons/CrimsonChargeVisualValidation.cs`: update hold-alpha assertions to1. Impact-fade assertions remain intact.

No scenes, prefabs, textures, materials, metadata, serialized fields, interfaces, managers, packages or project settings changed. Constants are deliberately local to the existing charge logic rather than a new serialized schema.

## Verification

- MSBuild `Editor.csproj` and `ProceduralPlayModeTests.csproj` exited0: [Editor build](msbuild-editor.log), [test assembly build](msbuild-tests.log). All changed C# sources are included in generated projects. Existing warnings remain.
- [510 checks passed](compiled-damping-results.json) by executing the compiled Gameplay method with Unity's real managed Vector3/Mathf implementation outside the Editor: identical0.2s response at30/60/120 FPS, monotonic approach, no overshoot, bounded rapid direction changes, zero-delta pause. Maximum measured separation0.750005 reflects float rounding. [Reproducible check](check-compiled-damping.ps1).
- [157 protected files](protected-verification.json) remain byte-identical, including all Crimson VFX art, prefab/material/shader and charge data. Runtime diff reviewed for unchanged flight/damage/cancel/lifecycle paths and assembly boundaries.
- Play Mode tests and native GPU capture were **not executed**. Unity window activation initially timed out during script compilation; after recovery the Computer Use helper rejected the screenshot identifier and then reported an active request. No user process was closed and no second Editor was launched. Managed math checks are not scene/coroutine/physics coverage.

Remaining: run `CrimsonChargePlayModeTests` and manually check the0.06s/0.75-unit feel, rapid cursor jumps/reversals, release while moving, viewport edges/camera movement and impact fade. The moving-aim integration test is compiled but not claimed as passed.

Doc Impact Check: SessionLog + StructureMemory + DecisionLog. ErrorLog unchanged; no Architecture/Contracts update or Presentation HTML stale candidate.
