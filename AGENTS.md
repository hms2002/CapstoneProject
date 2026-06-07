# Project Instructions for Codex

## Role

You are working on a Unity 2D top-down roguelike action project.

## Task Routing

Before non-trivial planning or editing, identify the task scope from the current prompt and project task routing:

1. The user's current instruction and any Task Brief in the prompt.
2. A matching document in `Docs/ActiveTasks/`, when the prompt names or clearly matches one.
3. `Docs/TaskIndex.md` as a router/dashboard only, not as active task scope.
4. `Docs/README.md` for task-specific documentation routes.

`Docs/CurrentTask.md` is deprecated and must not be used as the active task scope for new work.
Search `Docs/ErrorLog.md` and `Docs/DecisionLog.md` only when the task is related to known recurring mistakes, durable decisions, lifecycle/serialization risks, or the user explicitly asks for those logs.

## Scope Authority

When scope documents conflict, follow this order:

1. The user's current instruction.
2. The current prompt's Task Brief.
3. The matching `Docs/ActiveTasks/<task-id>.md`.
4. `Docs/TaskIndex.md` as router/dashboard context only.

Scope authority defines what this thread is allowed to change. It does not override technical contracts.

## Technical Authority

When implementation or architecture documents conflict, follow this order:

1. `Docs/Contracts/`
2. `Docs/Architecture/`
3. `Docs/Guides/`
4. `Docs/StructureMemory/`
5. `Docs/RefactorBacklog/`
6. `Docs/Reviews/`
7. `Docs/Notes/`
8. `Docs/Handoffs/`

`Docs/StructureMemory/` and `Docs/RefactorBacklog/` are context and planning aids, not source-of-truth documents. `Docs/Reviews/`, `Docs/Notes/`, and `Docs/Handoffs/` are reference-only. Do not let any of these override `Contracts` or `Architecture`.

## Work Mode Gate

Start non-trivial work by identifying the active mode. If the mode is unclear, default to Investigation Mode.

- Investigation: inspect and report causes, related files, risks, and candidate fixes. Do not edit files.
- Planning: create or refine an implementation plan. Do not edit files.
- Implementation: execute only the approved plan or explicitly allowed scope.
- Verification: review the diff, behavior, or checks against the success criteria. Do not add new fixes unless the user switches back to Implementation.
- Spike: perform a disposable experiment only when the user explicitly accepts throwaway work and cleanup expectations.
- Micro-fix: perform a local, low-risk edit only when scope and success criteria are obvious.

Do not switch modes automatically. Out-of-scope findings must be reported as `Suggested Later` instead of being fixed in the current slice.

For non-trivial or fuzzy requests, use `Docs/Guides/TaskBriefGuide.md` or the `$task-brief` repo skill to normalize the request before implementation. Keep this file concise; detailed templates and examples belong in the guide or `Docs/_templates/`.

## Work Rules

- Stay inside the current prompt Task Brief or matching `Docs/ActiveTasks/<task-id>.md` unless the user explicitly expands scope.
- Do not modify Unity scenes, prefabs, ScriptableObject schemas, serialized field names, enum persistent values/order, Animator parameters, Animation Events, Resources paths, `.meta`/GUIDs, asmdefs, ProjectSettings, Input Actions, Tags/Layers, or `DontDestroyOnLoad`/bootstrap flow without explicit user approval.
- Do not add new Managers, Singletons, or `DontDestroyOnLoad` objects without first proposing the design and receiving approval.
- Prefer small, reviewable changes over broad rewrites.
- Do not rewrite `Docs/Architecture/` or `Docs/Contracts/` directly unless the user approves that documentation update.
- You may update `Docs/TaskIndex.md`, `Docs/ActiveTasks/`, `Docs/SessionLogs/`, `Docs/StructureMemory/`, `Docs/RefactorBacklog/`, `Docs/ErrorLog.md`, and `Docs/DecisionLog.md` when the task outcome requires it and scope allows it.
- Do not use `Docs/CurrentTask.md` for new active task scope. It exists only as a deprecated compatibility notice.

## Presentation HTML Rules

- Markdown remains the source of truth.
- Presentation HTML files are human-readable overview documents.
- Do not convert every Markdown file into HTML.
- Do not copy full Markdown content into HTML.
- HTML should summarize, group, explain, visualize, and link back to Markdown.
- Use plain HTML, shared CSS, and small vanilla JS only.
- Put shared styles in `Docs/Presentation/_shared/docs-style.css`.
- Put shared document metadata in `Docs/Presentation/_shared/docs-data.js`.
- Put shared rendering helpers in `Docs/Presentation/_shared/docs-render.js`.
- Prefer editing `docs-data.js` for content updates instead of rewriting HTML structure.
- Use small Mermaid UML-like diagrams only when they improve human understanding.
- Session logs, `TaskIndex`, `ActiveTasks`, `DecisionLog`, and `ErrorLog` remain Markdown-first.
- Do not create one HTML file per Markdown document unless explicitly requested.

## Presentation HTML Approval Policy

Presentation HTML files are human-readable derived overview documents. Markdown files remain the source of truth.

### Authorized HTML Maintainer

Only the following GitHub user is allowed to approve or directly request Presentation HTML updates:

- `nadoman354`

### Rules For Codex

When working on Markdown architecture docs, guides, contracts, refactor notes, or other project structure documents:

1. Codex may edit Markdown source documents normally.
2. Codex must not automatically edit files under `Docs/Presentation/`.
3. Codex must not update Presentation HTML as a side effect of Markdown changes.
4. If Codex detects that Presentation HTML may now be stale, Codex must report it instead of editing it.
5. The report must clearly say which HTML page may need an update and why.
6. The report must ask for approval from the authorized HTML maintainer.
7. Codex may update Presentation HTML only when the current requester is the authorized maintainer or when the authorized maintainer explicitly asks for it.

Required response when HTML may need an update:

```txt
Presentation HTML update may be needed.

Affected page:
- Docs/Presentation/architecture-overview.html

Reason:
- CombatArchitecture.md changed the Combat / Weapon responsibility boundary.

Action required:
- Ask `nadoman354` to approve or perform the Presentation HTML update.
```

## Implementation Discipline

- For non-trivial work, state the relevant assumptions, risk-bearing unknowns, and success criteria before editing. If the answer can be discovered from local files, inspect first; ask only when a reasonable assumption would be risky.
- Prefer the simplest change that satisfies the user's request and the project contracts. Do not add speculative flexibility, configurability, abstraction, or fallback behavior.
- Every changed line should trace directly to the user's current request, a verified bug, or cleanup made necessary by your own change.
- If a smaller or safer approach exists than the requested shape, call out the tradeoff clearly before implementing it.
- For refactors, define the behavior-preserving boundary and verification path before editing, and do not bundle unrelated cleanup into the same slice.

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
- At the end of non-trivial work, run a Doc Impact Check and report one of: no doc update needed, SessionLog, StructureMemory, RefactorBacklog, DecisionLog, ErrorLog, Architecture/Contracts promotion candidate, or Presentation HTML stale candidate.
- Do not create broad new memory documents for every small edit; prefer updating the narrowest existing document that will help the next related task start faster.

## MCP / Obsidian Rules

- Obsidian is a navigation and editing layer over `Docs/`.
- `Docs/` is the source of truth, not a separate external vault.
- MCP read access may cover all of `Docs/`.
- MCP or agent write access is limited to:
  - `Docs/TaskIndex.md`
  - `Docs/ActiveTasks/`
  - `Docs/Guides/` only when explicitly approved
  - `Docs/SessionLogs/`
  - `Docs/StructureMemory/`
  - `Docs/RefactorBacklog/`
  - `Docs/ErrorLog.md`
  - `Docs/DecisionLog.md`
  - `Docs/CurrentTask.md` only for deprecated-notice maintenance when explicitly requested
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
- For Unity script file splits or new `.cs` helper files, do not block source-only refactors solely because Unity-generated `.csproj` files have not refreshed, when the user has accepted Editor compile handoff. In that mode, verify source structure instead: duplicate type definitions, removed original helper blocks, call-site references, namespace/assembly risks, and whitespace. Report Unity compile/import as user-confirmed or not executed.
- Run MSBuild for C# changes when the generated project file includes the relevant source files. If new files are not yet in the `.csproj`, do not manually edit the generated project file and do not claim MSBuild coverage for those files.
- For C# changes, check namespace, assembly definition, serialized references, and Unity lifecycle method risks.
- For documentation-only changes, verify Markdown links and folder paths.

## Done Means

At the end of every task, report:

- Changed files
- Why each file changed
- How the work was verified
- Remaining risks or follow-up decisions
- Whether `SessionLogs`, `ErrorLog`, or `DecisionLog` were updated
- Doc Impact Check category, including whether Presentation HTML may be stale
