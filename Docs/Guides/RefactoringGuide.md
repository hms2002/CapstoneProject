---
status: active
authority: guide
category: refactoring
last_reviewed: 2026-06-06
---

# Refactoring Guide

Use this guide when a task changes structure without intending to change player-visible behavior.

## Refactor Boundary

Before implementation, define the behavior-preserving boundary:

- What behavior must stay identical.
- Which files/systems are allowed.
- Which public APIs, serialized fields, assets, or scene/prefab references must not change.
- How equivalence will be verified.

## Do Now / Backlog / Do Not

Do Now:

- Small structure changes required to complete the approved task safely.
- Cleanup made necessary by the current change.
- Refactors that reduce immediate duplication without changing contracts.

Backlog:

- Repeated patterns that are not yet stable enough to abstract.
- Larger responsibility splits outside the current scope.
- Structural debt that needs migration, authoring review, or Play Mode validation.

Do Not:

- Unrelated cleanup.
- Public API redesign without approval.
- Scene/prefab/ScriptableObject/serialized migration without explicit approval.
- Architecture or Contracts rewrites without explicit approval.

## Unity Safety

For Unity projects, refactors must preserve serialized references and lifecycle behavior. Treat these as approval gates:

- Scene and prefab YAML.
- ScriptableObject schemas.
- Serialized field names.
- Enum persistent values or order.
- Animator parameters and Animation Events.
- Resources paths, `.meta` files, GUIDs.
- asmdefs, ProjectSettings, Input Actions, Tags/Layers.
- `DontDestroyOnLoad` and bootstrap flow.

## Verification

Use the smallest meaningful check that covers the refactor boundary:

- Static search for removed/added symbols and call sites.
- Project-file inclusion checks for new C# files.
- MSBuild only when generated project files include the touched scripts.
- Unity compile or Play Mode only when appropriate and actually run.

If verification cannot run, report exactly what was not run and why.

## Rollback

Keep refactors reviewable. Avoid bundling multiple ownership changes in one slice unless the user approved that bundle. If the refactor exposes larger debt, record it in `Docs/RefactorBacklog/` instead of silently expanding scope.
