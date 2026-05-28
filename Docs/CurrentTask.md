---
status: active
authority: current-task
category: boss-defeat-ending-outro
last_reviewed: 2026-05-27
---

# Current Task

## Goal

Add a boss-selectable terminal ending flow after boss defeat:

`BossDeathPresentation` death speech bubble -> Ink `DialogueService` dialogue -> fullscreen ending outro -> `RunEndReason.Victory` -> `TitleScene`.

## Requested Work

- Add ending outro playback scripts modeled after TitleIntro without modifying the existing title intro path.
- Add a scene-authored boss defeat ending sequence component with explicit boss, dialogue, outro, run-end, and target-scene references.
- Extend `BossDeathPresentation` with an optional terminal ending hook after death speech.
- Skip normal boss reward/portal handling when the terminal ending flow completes.
- Keep Dialogue UI visible during the post-speech Dialogue section even while the boss cinematic letterbox is active.

## Scope Notes

- The flow is opt-in per explicitly assigned boss, not global for every boss.
- Existing normal boss reward/chest/portal behavior remains unchanged.
- The terminal ending path replaces reward/portal activation for the selected boss.
- Outro UI must be authored as scene/prefab UI and driven through serialized references; runtime UI hierarchy creation is not part of this task.
- Do not direct-edit scene or prefab YAML for wiring.

## Done Criteria

- A selected boss can run death speech, Ink dialogue, ending outro, Victory run end, and `TitleScene` transition in order.
- The selected terminal flow does not call boss reward-ready handling or activate the normal reward/portal path.
- Normal bosses without the terminal sequence keep the existing death presentation and reward/portal flow.
- DialogueCanvas is not faded out by the death letterbox while terminal post-speech dialogue is active.
- Static verification checks confirm the new hook ordering, source references, and project-file inclusion state.

## Verification Plan

- Run `rg` checks for the terminal hook before reward notification, no new manager/singleton/`DontDestroyOnLoad`, and ending sequence API references.
- Check generated `.csproj` inclusion for new scripts before choosing build coverage.
- Run `dotnet build Assembly-CSharp.csproj --no-restore` only if generated project files include the new scripts.
- Run `git diff --check` for touched tracked files.
- Run a trailing-whitespace scan for touched source/docs.
- Check for Unity Editor processes and do not run Unity batchmode if the Editor is open.

## Remaining Risks

- New MonoBehaviours and ScriptableObject require Unity import/compile and Inspector wiring.
- Manual Play Mode validation is required for authored outro layout, skip/advance behavior, Dialogue visibility under letterbox, final run-end save behavior, and selected boss reward/portal suppression.
