# Project Instructions for Codex

## Role

You are working on a Unity 2D top-down roguelike action project.

## Required Reading Order

Before planning or editing, read these files in order:

1. `Docs/CurrentTask.md`
2. `Docs/ErrorLog.md`
3. `Docs/DecisionLog.md`
4. `Docs/README.md`

Then read only the task-specific documents routed by `Docs/README.md`.

## Documentation Authority

When documents conflict, follow this authority order:

1. The user's current instruction
2. `Docs/CurrentTask.md`
3. `Docs/Contracts/`
4. `Docs/Architecture/`
5. `Docs/Guides/`
6. `Docs/Reviews/`
7. `Docs/Notes/`
8. `Docs/Handoffs/`

`Docs/Reviews/`, `Docs/Notes/`, and `Docs/Handoffs/` are reference-only. Do not treat them as source of truth.

## Work Rules

- Stay inside the scope of `Docs/CurrentTask.md` unless the user explicitly expands it.
- Do not modify Unity scenes, prefabs, serialized fields, or ScriptableObject schemas without calling out reference risks first.
- Do not rename serialized fields unless prefab/scene migration risk has been reviewed.
- Do not add new Managers, Singletons, or `DontDestroyOnLoad` objects without first proposing the design.
- Prefer small, reviewable changes over broad rewrites.
- Do not rewrite `Docs/Architecture/` or `Docs/Contracts/` directly unless the user approves that documentation update.
- You may update `Docs/SessionLogs/`, `Docs/ErrorLog.md`, and `Docs/DecisionLog.md` when the task outcome requires it.
- Update `Docs/CurrentTask.md` only when the user explicitly asks to change the active task scope.

## MCP / Obsidian Rules

- Obsidian is a navigation and editing layer over `Docs/`.
- `Docs/` is the source of truth, not a separate external vault.
- MCP read access may cover all of `Docs/`.
- MCP or agent write access is limited to:
  - `Docs/SessionLogs/`
  - `Docs/ErrorLog.md`
  - `Docs/DecisionLog.md`
  - `Docs/CurrentTask.md` only when explicitly requested
- Do not rewrite `Docs/Architecture/`, `Docs/Contracts/`, or `Docs/Guides/` through MCP without explicit approval.

## Unity Project Rules

- Runtime state ownership must stay explicit.
- UI and tooltip code should project current state, not own gameplay state.
- Presentation logic should follow `Docs/Contracts/PresentationAuthoringContract.md`.
- Cleanup behavior should follow the relevant contract document before code changes.

## Verification Rules

- If Unity Editor or CLI tests cannot be run, explicitly say verification was not executed.
- Do not claim compile success unless Unity compilation or an equivalent project build/test command was actually run.
- For C# changes, check namespace, assembly definition, serialized references, and Unity lifecycle method risks.
- For documentation-only changes, verify Markdown links and folder paths.

## Done Means

At the end of every task, report:

- Changed files
- Why each file changed
- How the work was verified
- Remaining risks or follow-up decisions
- Whether `SessionLogs`, `ErrorLog`, or `DecisionLog` were updated
