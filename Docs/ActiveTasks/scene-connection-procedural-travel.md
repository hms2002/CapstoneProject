---
status: complete
mode: implementation
risk: high
target: core-framework
---

# Scene Connection And Procedural Travel

## Goal

Introduce data-driven scene connections for authored and procedural travel endpoints while keeping the legacy `ScenePortal` route path operational during migration.

## Intent

- Let interaction portals, automatic triggers, and arrival-only anchors share one connection contract.
- Resolve destination and direction-specific presentation from connection data instead of room prefab hardcoding.
- Block corridor-to-boss travel after that boss was defeated in the current run and show the common warning popup.
- Regenerate procedural corridors on entry by default while leaving an explicit per-dungeon preservation policy.
- Place players only after procedural endpoint generation is ready.

## Allowed

- Add Core, Gameplay, Infrastructure, UI, and Editor source needed for the new travel path.
- Add ScriptableObject schemas, stable enum values, and room endpoint placement data approved in the conversation.
- Extend run-session data with stable boss identifiers and procedural dungeon state.
- Update the room authoring tool and focused project-memory Markdown.

## Forbidden

- Remove the legacy `ScenePortal` path.
- Rewrite unrelated scenes, prefabs, ProjectSettings, asmdefs, input actions, or presentation HTML.
- Modify unrelated dirty-worktree content.

## Done Criteria

- New connection/endpoint/gate contracts compile.
- Interaction and trigger adapters share the same travel backend.
- Boss-defeated gate returns a stable warning code before departure presentation.
- Procedural generation publishes readiness and supports the agreed reentry policy contract.
- Room data/tool can author endpoint slots separately from complete portal prefabs.
- Existing scene portal source remains compatible.

## Verification

- Unity 6000.4.2f1 batchmode imported the project and compiled all actual asmdefs successfully (`Logs/SceneTravelFinalCompile.log`, return code 0).
- The final log contains no `error CS`, compiler-abort, or new scene-travel warning; the one new deprecated object lookup was replaced with the Unity 6 API.
- Focused reference, serialization-field, Unity `.meta`, legacy `ScenePortal`, and scoped `git diff --check` inspections passed.
- Scene/prefab binding and end-to-end Play Mode travel checks remain intentionally pending for the content-integration slice.
