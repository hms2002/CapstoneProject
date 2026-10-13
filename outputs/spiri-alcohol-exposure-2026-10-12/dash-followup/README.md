# Directional dash confusion follow-up

The user explicitly extended confusion to reverse directional dash together
with walking. This supersedes the earlier walking-only choice.

- PlayerIntentInput2D.ApplyConfusionToDirection now supplies the shared direction
  projection. Raw input and other ability inputs retain their existing values.
- AbilityLogic_Dash2D applies it to both cached raw intent and the direct input
  fallback, including a key pressed before the intent component updates.
- No-direction aim fallback, movement locks, forced movement, prefab/effect
  data, bindings and save schema were changed.
- AlcoholExposurePlayModeTests updates five walking/dash cases and adds five
  fresh-input fallback and five no-input aim fallback cases. These also check
  direction restoration after confusion removal.

Verification: the generated ProceduralPlayModeTests.csproj includes the modified
runtime and test files; MSBuild Build exited0 (msbuild.log). The active project's
Editor.log predates this source edit, so its earlier compilation is not used as
follow-up evidence. Unity import/compilation, native PlayMode tests and manual gameplay were not executed for this
follow-up because this project is already open in the user's Editor and no live
Unity test connector is available. Earlier38-case native results cover the
initial behavior, not these15 updated dash expectations.

Doc Impact Check: SessionLog, existing StructureMemory, DecisionLog and task
scope/router updated. ErrorLog unchanged in this follow-up. Presentation HTML
and architecture/contracts were not changed; no stale candidate identified.
