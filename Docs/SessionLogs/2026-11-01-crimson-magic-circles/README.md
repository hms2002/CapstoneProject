# Crimson right-click magic-circle integration

Mode: Implementation. User approved revision19 artwork, two-stage scale growth, opposite flat rotation, size retention during meteor flight and a short post-impact fade. In a clarification reply the user explicitly selected the86/128 preview ratio and the corresponding minimum released attack diameter4.03125 instead of the previous3. Maximum diameter remains6.

## Result

- 0–50%: inner circle scales from0 to4.03125 units; outer circle remains hidden.
- 50–100%: inner stays complete; outer appears at the inner diameter and expands to6. The outermost visible circumference and actual damage query share the same range calculation.
- Inner rotates counterclockwise and outer clockwise at37.5°/s. During charge both follow the clamped mouse target. Release locks position and both scales while the collision-free meteor follows its existing45° descent.
- Impact starts a.25-second alpha fade without shrinking either circle; rotation continues through the fade. Existing impact sprites/particles retain their2.1-second lifetime. Pause freezes gameplay clocks; owner cancellation/disable/scene cleanup retains ownership of the complete presentation.
- Head gauge appearance/thresholds, .7/1.4-second minimum/full charge, .5-second flight, cooldown, damage multipliers, Burn stacks, basic attack and Q remain unchanged.

## Changed files and purpose

- [Charge data](../../../Assets/_Project/Runtime/Features/Items/Weapons/CrimsonBoundary/CrimsonBoundaryChargeData.cs): `InnerCircleFraction` and `ChargeDiameter` express the approved staged range. [Ability logic](../../../Assets/_Project/Runtime/Features/Items/Weapons/CrimsonBoundary/AbilityLogic_CrimsonBoundaryCharge.cs) uses it both while aiming and at release.
- [Presentation](../../../Assets/_Project/Runtime/Presentation/VFX/Presentation/CrimsonBoundaryChargePresentation.cs): authored sprite references replace generated disc/dash meshes; separate scale/rotation, frozen flight scale and post-impact alpha fade. No new manager, runtime hierarchy or sprite creation.
- [Prefab](../../../Assets/_Project/Prefabs/VFX/CrimsonBoundary/ChargeSixColor/CrimsonChargePresentation.prefab): two SpriteRenderer children replace the two mesh children. Existing prefab GUID and125 unrelated serialized blocks remain unchanged.
- [Approved art](../../../Assets/_Project/Art/Sprites/VFX/CrimsonBoundary/MagicCircle/):128×128 PNG/Aseprite inner/outer pair copied byte-for-byte from revision19, imported Point/uncompressed/no mipmaps/16PPU. Empty10px medallions and solid glyph strokes are preserved.
- [Circle shader](../../../Assets/_Project/Art/Shaders/CrimsonMagicCircle.shader) and [shared material](../../../Assets/_Project/Prefabs/VFX/CrimsonBoundary/ChargeSixColor/MagicCircle.mat): retain thin strokes when128px art is displayed at about96px or rotated. Nine point samples within one screen-pixel footprint choose an existing source color; no palette interpolation, global camera change or per-frame material instance. Screen-space derivatives follow the documented [ddx](https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-ddx) / [ddy](https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-ddy) semantics.
- [Authoring](../../../Assets/_Project/Editor/Tools/Weapons/CrimsonChargeAssetAuthoring.cs): targeted `ApplyMagicCircles` migration and repeat-safe range-text update. The full `Author` command also produces the new warning when invoked, but is not needed for this migration.
- [Native visual validation](../../../Assets/_Project/Editor/Tools/Weapons/CrimsonChargeVisualValidation.cs) and [PlayMode tests](../../../Assets/_Project/Tests/PlayMode/Procedural/CrimsonChargePlayModeTests.cs): source/import/shader checks, staged dimensions, flight/fade captures, real skill execution and cleanup regression coverage.
- [Ability fallback](../../../Assets/_Project/Data/Abilities/Definitions/AD_CrimsonBoundaryIgnite.asset), five `GameText_<locale>.asset` tables and [CSV](../../../DataSheets/Localization/GameText.csv): range text now4.03–6. The CSV's two stale Crimson rows were synchronized with the current runtime tables; all other records are preserved. [Exact localized values](localization-values.json).

## Verification

- Editor and PlayMode-test generated projects built with MSBuild, exit0: [Editor](msbuild-editor-final.log), [test assembly](msbuild-tests.log). Existing unrelated warnings remain; no compile-success claim depends on source inspection alone.
- Unity6000.4.2f1 imported the assets, compiled the shader and captured195 native GPU sequence frames plus named stills/gauge views, exiting0: [final capture log](unity-material-capture.log). Initial ordinary-point samples showed line dropout; the final material was visually reviewed on the same floor/camera.
- Unity PlayMode17/17 passed,0 failed/skipped: [results](playmode-results.xml), [log](unity-playmode.log). Covers two-stage scale/visible range, opposite rotation, fixed flight footprint, .25s fade, enlarged partial actual hit boundary, no airborne damage, duplicate-hurtbox protection, existing partial/full damage/Burn/cooldown, pause, cancellation, owner disable and tail expiry. The existing `-uiBatchValidationTests` flag isolates the previously documented PrewarmTrace writer issue; it does not suppress gameplay assertions or change production behavior.
- [Scope verification](scope-verification.json):131 protected files unchanged;125 unrelated prefab blocks unchanged; only8 mesh-child blocks removed and6 sprite-child blocks added; original prefab GUID retained. All four approved PNG/Aseprite files are byte-identical. Five native locale entries change only their range text; two CSV records synchronized. [Reproducible scope check](verify-scope.py).
- Native views: [50%](captures/charge-orange.png), [100%](captures/charge-full.png), [flight](captures/flight.png), [impact fading](captures/impact-warning-fade.png), [fade complete](captures/impact-warning-gone.png). These are renders of the real prefab with a neutral review floor and player sprite, not a manual gameplay capture.

## Limits and follow-up

- Manual combat in an actual level, Player build and GPU profiling were not executed. Renderer verification is on the current Windows/D3D11 URP project; other graphics APIs are not claimed.
- Conservative sampling can slightly thicken subpixel strokes. It is not pixel-for-pixel identical to the earlier GIF's connected-vector redraw, but avoids the ordinary point-sampling gaps seen in [the initial native capture](point-sampling-charge-full.png).
- Scene/prefab/ScriptableObject schemas outside this warning, scene loading, input actions, package configuration, project settings and existing metadata were not migrated.
- Intermediate authoring caught sparse-asset reserialization and a Windows mapped-file write conflict. The fallback asset was restored to the immediate pre-task structure plus its one intended text change; final targeted authoring no longer writes that mapped file. Details are recorded in ErrorLog.
- Doc Impact Check: SessionLog, existing StructureMemory, DecisionLog and ErrorLog updated. No new RefactorBacklog entry or Architecture/Contracts promotion; no Presentation HTML stale candidate.
