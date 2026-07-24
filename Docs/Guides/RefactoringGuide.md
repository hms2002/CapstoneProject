---
status: active
authority: guide
category: refactoring
last_reviewed: 2026-06-09
---

# Refactoring Guide

Use this guide when a task changes structure without intending to change player-visible behavior.

## Definition

Refactoring is a behavior-preserving structure change that lowers the cost or risk of the next change.

In this Unity project, "behavior-preserving" includes player-visible behavior and Unity-facing contracts:

- scene and prefab references
- serialized field names and schemas
- ScriptableObject data meaning
- Animator parameters and Animation Events
- enum persistent values or order
- `Resources` paths, `.meta` files, and GUIDs
- bootstrap, singleton, and `DontDestroyOnLoad` lifecycle

A change that intentionally alters gameplay, content, save semantics, UI behavior, authoring policy, or data migration is not a pure refactor. Scope it as a feature, bug fix, migration, or authoring change.

## SOLID Benefit Rule

Use SOLID for its practical benefits, not its ceremony.

Good refactors should make one or more of these cheaper or safer:

- isolate one reason to change
- add a new weapon, drop, boss, reward, UI panel, or save field without editing core flow
- replace one implementation without breaking callers
- expose smaller contracts to dependents
- keep higher-level policy from depending on low-level Unity objects or fallback creation

Do not add interfaces, factories, managers, or services just to look structured. Add an abstraction only when a verified pain point, repeated change reason, or dependency direction problem makes the extra shape cheaper than the direct code.

## Target Shape

An ideal target shape is not a perfect final architecture. It is the next stable structure that handles the current requirements and is less fragile under the nearest expected change.

Define the target shape from current pain:

- where the next feature currently has no clear home
- where core code must be edited for every content addition
- where one class has too many reasons to change
- where data and execution logic are mixed
- where a lower-level object knows too much about global flow
- where verification requires too many unrelated systems

If the better structure is known but outside the approved slice, record it in `Docs/RefactorBacklog/` instead of expanding the refactor.

## Refactor Boundary

Before implementation, define the behavior-preserving boundary:

- What behavior must stay identical.
- Which files/systems are allowed.
- Which public APIs, serialized fields, assets, or scene/prefab references must not change.
- How equivalence will be verified.

For medium or high-risk refactors, also define the migration loop:

- Freeze current behavior with the smallest useful static check, test, or play flow.
- Create the minimal target-structure skeleton.
- Move one responsibility at a time.
- Re-run the same verification after each move.
- Stop and split scope if verification fails for reasons unrelated to the moved responsibility.

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
