---
status: active
authority: task-scope-guide
category: active-task-guide
last_reviewed: 2026-06-06
---

# Active Tasks

`Docs/ActiveTasks/` stores thread-specific task scope documents for parallel Codex work.

An ActiveTask defines what a single thread is allowed to change. It does not override technical source-of-truth documents such as `Docs/Contracts/` or `Docs/Architecture/`.

## When To Create One

Create an ActiveTask when work is non-trivial, spans multiple turns, may run in parallel with other Codex threads, or needs explicit allowed/forbidden boundaries.

Do not create one for a tiny one-turn investigation unless the user asks for durable task scope.

## File Naming

Use a stable, readable task id:

```txt
<feature-or-system>-<action>-YYYY-MM-DD.md
```

Examples:

```txt
global-uiroot-inventory-2026-06-06.md
input-lock-verification-2026-06-06.md
audio-variant-implementation-2026-06-06.md
```

## Required Use

- Put the task in `Docs/TaskIndex.md` when it should be discoverable by future threads.
- Keep the ActiveTask focused on scope, mode, risk, target, allowed changes, forbidden changes, done criteria, and verification.
- Keep technical rules in `Docs/Contracts/`, `Docs/Architecture/`, or `Docs/Guides/`; link them instead of copying.
- Close or supersede stale ActiveTasks instead of rewriting history.
