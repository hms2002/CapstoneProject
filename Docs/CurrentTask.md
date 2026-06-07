---
status: deprecated
authority: deprecated-compatibility-notice
category: task-routing
last_reviewed: 2026-06-06
---

# CurrentTask.md Is Deprecated

`Docs/CurrentTask.md` is no longer the active task-scope source for this project.

Use the new routing model instead:

- Current prompt Task Brief: first source for the current thread's scope.
- `Docs/ActiveTasks/<task-id>.md`: thread-specific active task scope when a task document exists.
- `Docs/TaskIndex.md`: router/dashboard for active or proposed task documents.
- `Docs/README.md`: technical documentation router.

Do not add new task scope here. This file remains only as a compatibility notice for old links and historical references.

## Replacement Flow

1. Start from the user prompt and identify the work mode.
2. If a matching ActiveTask exists, read it for scope.
3. If not, treat the prompt Task Brief as the scope for this thread.
4. Use `Docs/TaskIndex.md` only to find or register task documents.
5. Use `Docs/README.md` to route into Contracts, Architecture, Guides, StructureMemory, RefactorBacklog, Reviews, Notes, or Handoffs.
