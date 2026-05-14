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
6. `Docs/StructureMemory/`
7. `Docs/RefactorBacklog/`
8. `Docs/Reviews/`
9. `Docs/Notes/`
10. `Docs/Handoffs/`

`Docs/StructureMemory/` and `Docs/RefactorBacklog/` are context and planning aids, not source-of-truth documents. `Docs/Reviews/`, `Docs/Notes/`, and `Docs/Handoffs/` are reference-only. Do not let any of these override `Contracts` or `Architecture`.

## Work Rules

- Stay inside the scope of `Docs/CurrentTask.md` unless the user explicitly expands it.
- Do not modify Unity scenes, prefabs, serialized fields, or ScriptableObject schemas without calling out reference risks first.
- Do not rename serialized fields unless prefab/scene migration risk has been reviewed.
- Do not add new Managers, Singletons, or `DontDestroyOnLoad` objects without first proposing the design.
- Prefer small, reviewable changes over broad rewrites.
- Do not rewrite `Docs/Architecture/` or `Docs/Contracts/` directly unless the user approves that documentation update.
- You may update `Docs/SessionLogs/`, `Docs/StructureMemory/`, `Docs/RefactorBacklog/`, `Docs/ErrorLog.md`, and `Docs/DecisionLog.md` when the task outcome requires it.
- Update `Docs/CurrentTask.md` only when the user explicitly asks to change the active task scope.

## Project Memory Rules

Markdown files are not only task receipts. They are project memory used to reduce future context reconstruction time, speed up work on previously touched systems, and avoid wasting tokens re-discovering structure.

Use a balanced documentation policy: small local fixes usually need only `SessionLogs`, while reusable structure, structural debt, durable decisions, and recurring mistakes need the narrower memory document that matches the change.

### SessionLogs

Use `Docs/SessionLogs/YYYY-MM-DD.md` for what actually changed in the current task.

- Record changed files or systems, why they changed, verification performed, manual playtest confirmation if available, and remaining risks.
- Do not repeat a full system map here if a feature-level `StructureMemory` document exists; link or name it instead.

### StructureMemory

Use `Docs/StructureMemory/<FeatureOrFlow>.md` for feature-level structure maps that help future work start quickly.

Create or update one when a task creates or materially changes reusable structure, ownership boundaries, runtime state flow, cleanup/lifecycle flow, shared services, interfaces, asmdefs, MonoBehaviours, ScriptableObjects, prefab-facing contracts, or multi-file flow behavior.

- StructureMemory is not source of truth. It is the fastest current map for context reconstruction.
- Include purpose, current structure, key files, ownership/lifecycle rules, extension entry points, known pitfalls, and whether the structure is a candidate for future `Architecture` or `Contracts` promotion.
- Do not put per-task diffs, unverified guesses, or mandatory policy language here unless it is also tracked as a durable decision or contract candidate.

### RefactorBacklog

Use `Docs/RefactorBacklog/<FeatureOrDebt>.md` for intentional structural debt and refactor candidates.

Create or update one when a task leaves a legacy adapter, temporary fallback, responsibility overload, duplicate path, prefab/scene migration hold, or a known better structure that is out of current scope.

- RefactorBacklog is not a generic TODO list.
- Each entry must include the current problem, why it exists, target shape, risks, refactor trigger, related documents, and status (`proposed`, `active`, `partially-refactored`, or `resolved`).

### Durable Decisions And Errors

- If the decision should remain durable beyond the current task, add a short entry to `Docs/DecisionLog.md`.
- If the task reveals a recurring implementation mistake or lifecycle/serialization/prefab trap, add or update `Docs/ErrorLog.md`.
- If an `Architecture` or `Contracts` document should become the source of truth, call that out as a follow-up unless the user explicitly approves editing that document.
- Do not create broad new memory documents for every small edit; prefer updating the narrowest existing document that will help the next related task start faster.

## MCP / Obsidian Rules

- Obsidian is a navigation and editing layer over `Docs/`.
- `Docs/` is the source of truth, not a separate external vault.
- MCP read access may cover all of `Docs/`.
- MCP or agent write access is limited to:
  - `Docs/SessionLogs/`
  - `Docs/StructureMemory/`
  - `Docs/RefactorBacklog/`
  - `Docs/ErrorLog.md`
  - `Docs/DecisionLog.md`
  - `Docs/CurrentTask.md` only when explicitly requested
- Do not rewrite `Docs/Architecture/`, `Docs/Contracts/`, or `Docs/Guides/` through MCP without explicit approval.

## Unity Project Rules

- Runtime state ownership must stay explicit.
- UI and tooltip code should project current state, not own gameplay state.
- UI screens, popups, HUD, buttons, text, fade overlays, and authored presentation objects should normally be placed and reviewed in Unity scenes or prefabs, then driven through serialized references.
- Avoid runtime creation of UI hierarchy, `Canvas`, `EventSystem`, buttons, TMP text, sprites, or presentation objects unless the user explicitly asks for a prototype/fallback or approves that design first.
- If runtime UI/object creation is unavoidable, call it out before implementation and report the reason, ownership, cleanup path, and prefab/scene migration follow-up.
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
