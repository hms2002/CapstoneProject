---
status: active
authority: structure-memory
category: system-map-index
last_reviewed: 2026-05-14
---

# Structure Memory

`StructureMemory` documents are feature-level maps for fast context reconstruction. They are not source-of-truth architecture or contract documents.

Use these documents when a future task needs to quickly understand an existing flow before editing code. For official rules, prefer `Docs/Contracts/` and `Docs/Architecture/`.

## When To Create Or Update

- A reusable structure or multi-file flow is created or materially changed.
- Ownership, lifecycle, cleanup, or runtime state flow changes.
- A MonoBehaviour, ScriptableObject, shared service, interface, asmdef, or prefab-facing contract becomes important to future work.
- A date-based `SessionLogs` entry would be too hard to find for the next related task.

## Required Sections

Each feature document should include:

- Purpose
- Current Structure
- Key Files
- Ownership And Lifecycle
- Extension Entry Points
- Known Pitfalls
- Promotion Candidate

## Boundaries

- Do not record every task diff here.
- Do not use this folder for unverified guesses.
- Do not treat this folder as more authoritative than `Contracts` or `Architecture`.
- If a structure becomes stable enough to guide future implementations, propose promoting it to `Architecture` or `Contracts`.

## Current Documents

- [Script System Map](./ScriptSystemMap.md)
- [Script Systems](./ScriptSystems/README.md)
- [UI Flow Input Blocking](./UIFlowInputBlocking.md)
