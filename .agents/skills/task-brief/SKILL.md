---
name: task-brief
description: Normalize a rough Codex request into a scoped Task Brief for Unity project work. Use when the user invokes $task-brief with a concrete request to investigate, plan, implement, verify, spike, or micro-fix work and needs scope, intent, risk, mode, or Unity asset safety clarified.
---

# Task Brief

Convert the user's concrete rough request into a concise Task Brief. Do not implement the task, edit files, run tests, or create plans beyond the requested brief.

If the user invokes `$task-brief` without a concrete request, do not print the full template. Tell the user to use `Docs/_templates/` or `Tools/codex-task-brief.ahk` for blank template insertion, then ask them to provide the rough request to normalize.

## Core Rules

- Preserve the user's original intent.
- If mode is missing or ambiguous, choose `Investigation`.
- Separate `Goal` from `Intent`.
- Keep the brief compact and directly usable as the next Codex prompt.
- Add conservative `Forbidden` rules for Unity asset and serialization risks.
- Mark missing but important information as assumptions.
- Report out-of-scope discoveries under `Suggested Later`; do not fix them.

## Filled Brief Shape

```txt
Mode: [Investigation / Planning / Implementation / Verification / Spike / Micro-fix]
Risk: [Low / Medium / High]
Target Type: [Core / Framework / Leaf / Documentation]

Task:
[one-line task name or symptom]

Goal:
[what should be true, fixed, learned, or decided when done]

Intent:
[why this matters, what structure direction to preserve, what shortcut to avoid]

Context:
[symptoms, repro steps, related scene/prefab/asset/script/docs, recent change]

Allowed:
- Read relevant files and routed docs
- [files/folders/systems Codex may inspect or edit]

Forbidden:
- Out-of-scope fixes
- scene/prefab/SO/serialized field changes unless explicitly approved
- Architecture/Contracts changes unless explicitly approved
- unrelated cleanup

Done Criteria:
- [required output or behavior]
- [risk/side-effect that must be checked]
- [what must be reported if not verified]

Verification:
- Static checks: [rg/git diff/static analysis/doc link checks]
- Unity checks: not run by default; run only when required and allowed
- Report unexecuted compile, batchmode, Play Mode, or build checks as not executed

Assumptions:
- [missing information Codex should not silently invent]

Out-of-scope:
- Report out-of-scope findings under Suggested Later instead of fixing them
```

## Field Defaults

- `Mode`: default to `Investigation` when missing or ambiguous.
- `Risk`: default to `Medium`; use `High` for bootstrap, DDOL, save/ID, public API, scene/prefab/SO/serialized contracts, input actions, asmdefs, or cross-system boundaries.
- `Target Type`: use `Documentation` for docs-only work, `Core` for shared runtime/bootstrap/input/UI root policy, `Framework` for reusable feature systems, and `Leaf` for narrow content or local behavior.
- `Allowed`: default to reading relevant files/docs only. Add edit permissions only when the user asks for Implementation or approves a plan.
- `Forbidden`: always include Unity asset/serialization risks unless explicitly approved.
- `Verification`: include checks to run and require clear reporting for checks not run.
- `Assumptions`: list unknowns that affect scope, risk, or verification.

## Mode Presets

Investigation:
- Use for "확인해줘", "봐줘", "문제 있나", "원인 찾아줘", "조사해줘".
- Include `Do not edit files`.
- Output cause candidates, related files/lines, Unity asset/serialization risk, fix candidates, `Suggested Later`, and no implementation plan unless requested.

Planning:
- Use when the user asks for a plan or implementation would be risky.
- Include `Do not edit files`.
- Output change scope, forbidden scope, expected files, risk level, verification method, rollback method, and questions before implementation.

Implementation:
- Use only when the user explicitly approves a plan.
- State that only the approved plan may be implemented.
- Require explicit approval for Unity scene/prefab/SO/serialized changes.
- Require reporting unverified success as not verified.

Verification:
- Use when the user asks to review a diff or check completed work.
- Include `Do not edit files`.
- Check plan match, forbidden files, Unity reference/serialization/prefab risk, verification results, and remaining risks.

Spike:
- Use for disposable experiment/prototype work.
- Keep core APIs and existing pipelines clean.
- Require explicit approval for Unity asset/serialized data changes.
- Ask whether the spike should be adopted, discarded, or formalized.

Micro-fix:
- Use only for local, low-risk edits.
- Keep public APIs unchanged.
- Avoid Unity asset/serialization impact and pipeline bypasses.
