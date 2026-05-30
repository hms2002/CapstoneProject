---
status: active
authority: current-task
category: demonking-victory-gameover-flow
last_reviewed: 2026-05-30
---

# Current Task

## Goal

Change the final DemonKing terminal ending so the outro returns to the roguelike run loop through the existing Game Over screen instead of loading `TitleScene`.

## Requested Work

- After DemonKing death speech, Dialogue, and Ending Outro, show a Victory GameOver presentation.
- Reuse the existing authored Game Over UI.
- Show run magic stone gain in the former location text slot.
- Change victory copy to:
  - title: `승리?` in green
  - message: `승리하였지만, 이것으로 충분했을까?`
- Keep the bottom-left inventory HUD icon and key hint available on real defeat and victory GameOver screens.
- Allow inventory opening from real defeat and victory GameOver screens.
- Keep FakeGameOver from showing or accepting inventory HUD/input.
- Keep the player snapshot standing for victory.

## Scope Notes

- Do not directly edit scene or prefab YAML.
- Do not add new managers, singletons, or `DontDestroyOnLoad` objects.
- Existing terminal `TitleScene` load stays as a fallback/optional completion mode.
- `GlobalUIRoot` authored UI remains the presentation source; runtime code may temporarily move the existing inventory HUD button for GameOver presentation, but must restore it.
- Unity Editor is open, so do not run Unity batchmode.

## Done Criteria

- `BossDefeatEndingSequence` defaults terminal completion to Victory GameOver after outro.
- Victory GameOver commits `RunEndReason.Victory` only on return, preserving pending magic stone gain for display.
- Real defeat/victory GameOver can open inventory through a GameOver-owned input-blocker exception.
- FakeGameOver keeps inventory operation and key hint hidden/blocked.
- Outro view does not remain over the Victory GameOver screen.
- Static checks and touched-file diff checks are run.

## Verification Plan

- Run `rg` checks for terminal Victory GameOver, inventory GameOver owner exception, FakeGameOver inventory disable, and no new global managers/singletons.
- Confirm touched C# files are included in `Assembly-CSharp.csproj`.
- Run `dotnet build Assembly-CSharp.csproj --no-restore` when the project file includes touched scripts.
- Do not run Unity batchmode while Unity Editor processes are open.
- Manual Play Mode still needs to verify DemonKing outro -> Victory GameOver, inventory open/close, FakeGameOver blocking, return-to-Hub Victory commit, and no TitleScene transition.

## Remaining Risks

- `BossDefeatEndingSequence` gained a serialized completion-mode enum; existing scene components need Unity import/Inspector review.
- Victory player standing snapshot uses the live player renderer/animator state; manual review should confirm the captured frame is the desired standing pose.
- Inventory HUD is temporarily reparented at runtime and restored; manual review should confirm layout remains bottom-left on the GameOver canvas.
