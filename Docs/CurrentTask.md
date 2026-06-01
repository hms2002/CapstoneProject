---
status: active
authority: current-task
category: run-route-catalog-fixed-order
last_reviewed: 2026-06-01
---

# Current Task

## Goal

Add a `RunRouteCatalogSO` toggle that lets a run use the authored normal RouteSet order exactly, so the demo route can follow the configured three normal RouteSets before the final boss.

## Requested Work

- Add a fixed normal RouteSet order toggle to `RunRouteCatalogSO`.
- Keep the existing random normal RouteSet behavior when the toggle is disabled.
- When the toggle is enabled, build the normal route plan from `normalRouteSets` in serialized order, up to `normalStageCount`, then append `finalRouteSet`.
- Enable the toggle on the current `RunRouteCatalog.asset` whose normal order is Shadow, Dragon, then Slime.

## Scope Notes

- Do not directly edit Unity scene YAML.
- Do not add new managers, singletons, or `DontDestroyOnLoad` objects.
- This task intentionally changes the `RunRouteCatalogSO` serialized ScriptableObject schema by adding one boolean field.
- Unity Editor is open, so do not run Unity batchmode.
- Do not modify scene or prefab portal authoring for this task.
- Keep `finalRouteSet` as the final boss route appended after the normal route sequence.

## Done Criteria

- `RunRouteCatalogSO` exposes a fixed normal route order toggle.
- `PortalRouteManager` builds fixed-mode plans in `normalRouteSets` order and leaves random-mode behavior unchanged.
- The current `RunRouteCatalog.asset` has the fixed-order toggle enabled.
- Existing route validation reports or applies the fixed-order catalog policy when relevant.
- Static checks and project-file inclusion checks are run.

## Verification Plan

- Run `rg` checks for the fixed-order toggle, plan branch, validator policy, and asset serialized value.
- Confirm touched runtime C# files are included in `Assembly-CSharp.csproj`.
- Confirm touched editor C# files are included in `Assembly-CSharp-Editor.csproj`.
- Run `dotnet build Assembly-CSharp.csproj --no-restore` and `dotnet build Assembly-CSharp-Editor.csproj --no-restore` when the project files include touched scripts.
- Do not run Unity batchmode while Unity Editor processes are open.
- Manual Play Mode still needs to verify fixed ON route order from hub start, fixed OFF random behavior, and insufficient/null RouteSet failure logging.

## Remaining Risks

- `RunRouteCatalogSO` has a new serialized field, so Unity Editor import/Inspector review is required.
- Runtime route behavior must be confirmed in Play Mode because the plan is activated through hub-start portal flow.
