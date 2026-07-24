---
status: active
authority: guide
category: codex-task-brief
last_reviewed: 2026-06-07
---

# Task Brief Guide

Use a Task Brief when asking Codex to do non-trivial work. The brief separates intent, scope, and execution mode so Codex does not infer too much from vague wording.

## Minimal Brief

```txt
Mode:
Goal:
Intent:
Allowed:
Forbidden:
Done Criteria:
Verification:
```

## Using The `$task-brief` Skill

Use `Docs/_templates/` or `Tools/codex-task-brief.ahk` when you want to insert a blank template before sending a Codex prompt. This keeps template insertion out of the Codex thread and avoids spending tokens just to retrieve boilerplate.

```txt
Ctrl+Alt+Numpad1     Investigation template
Ctrl+Alt+Numpad2     Planning template
Ctrl+Alt+Numpad3     Implementation template
Ctrl+Alt+Numpad4     Verification template
Ctrl+Alt+Numpad5     Spike template
Ctrl+Alt+Numpad6     Micro-fix template
```

Deep links are not part of the current workflow.

Use the repo skill only when a rough request should be normalized before work starts:

```txt
$task-brief 업그레이드 패널 입력 누수 조사 브리프 만들어줘
```

```txt
$task-brief 아래 요청을 우리 프로젝트 Task Brief 형식으로 정리해줘:
[rough request]
```

The skill should produce a filled brief only. It should not output blank templates, implement, run tests, or edit files.

## Mode

- Investigation: inspect and report only. No edits.
- Planning: create or refine a plan. No edits.
- Implementation: execute the approved plan only.
- Verification: review diff, behavior, or checks. No extra fixes.
- Spike: disposable experiment with explicit cleanup expectations.
- Micro-fix: local, low-risk edit with obvious scope.

If the mode is missing or ambiguous, Codex should default to Investigation.

## Goal And Intent

Goal is what should change or be learned.

Intent is why the task exists and what direction must be preserved. Include structural constraints, player-facing reason, or production risk when relevant.

## Risk And Target Type

Risk:

- Low: local code or docs, no public API or Unity asset impact.
- Medium: several files, lifecycle behavior, shared flow, or manual validation needed.
- High: core systems, save/ID, public API, scene/prefab/ScriptableObject/serialized contracts, bootstrap, or cross-system boundaries.

Target Type:

- Core: save/load, IDs, factories, input locking, game flow, scene transition/bootstrap, UI root policy, runtime services, shared public APIs.
- Framework: weapon, skill, relic, UI, dialogue, boss pattern, or content-authoring framework.
- Leaf: individual skill, boss pattern, relic effect, local UI animation, sound variant, VFX trigger, or narrow interaction object.
- Documentation: source-level Markdown or derived presentation documents.

## Scope Controls

Use `Allowed` for what Codex may edit. Use `Forbidden` for what must be reported only.

Always call out Unity assets explicitly. Scene, prefab, ScriptableObject schema, serialized field names, enum values/order, Animator parameters, Animation Events, Resources paths, `.meta`/GUIDs, asmdefs, ProjectSettings, Input Actions, Tags/Layers, and bootstrap/DDOL changes require explicit approval.

## Out-Of-Scope Findings

Tell Codex how to handle related discoveries:

```txt
범위 밖 문제는 수정하지 말고 Suggested Later로 보고해.
```

This keeps useful discoveries without expanding the current slice.

## Verification

Say what checks should run and what must be reported if not run. Codex must not claim compile, Unity import, Play Mode, or build success unless that check actually ran.
