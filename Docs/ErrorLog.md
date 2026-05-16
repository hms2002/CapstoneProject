---
status: active
authority: project-log
category: error-log
last_reviewed: 2026-05-05
---

# Error Log

This file records recurring implementation errors, their causes, and prevention rules.

## Template

```md
## YYYY-MM-DD - Short error name

Context:

Cause:

Fix:

Prevention:
```

## Active Entries

## 2026-05-06 - CurrentTask Drift

Context:
Feature implementation continued while `Docs/CurrentTask.md` still described the old project memory system task.

Cause:
The document was treated as required reading but not as an actively maintained task contract.

Fix:
Update `Docs/CurrentTask.md` at the start of each active task change, and keep detailed progress in `Docs/SessionLogs/`.

Prevention:
Before implementation, confirm `CurrentTask.md` matches the user's current requested work. If it does not, update it first.

## 2026-05-14 - Chest World/UI Presentation Timing Conflation

Context:
While fixing chest first-open input blocking, the world `TreasureChest` open presentation lifetime was used to extend the wait before the chest UI opened.

Cause:
The GameObject open presentation and the chest UI first-open reveal were treated as one timing chain, even though the UI reveal timing is authored separately.

Fix:
Restore `TreasureChest` open-to-UI timing to its existing animator/fallback behavior, and keep input blocking inside the chest UI reveal presentation.

Prevention:
Do not use world object particle/effect lifetime to time chest UI reveal opening. When the issue is "UI reveal blocking", start and end blockers from the UI presentation owner.

## 2026-05-14 - Chest First-Open Blocker Gap

Context:
The chest UI reveal was blocked correctly, but the gap between `TreasureChest` GameObject interaction/open prelude and the later UI reveal still allowed V inventory and ESC pause input.

Cause:
The GameObject open presentation and UI reveal were kept separate for timing, but input blocking was also scoped only to the UI reveal owner.

Fix:
Acquire an external UI input blocker from `TreasureChest` immediately on first-open interaction, allow that owner to open only its intended chest UI, then hand off to the inventory/chest UI reveal blocker.

Prevention:
For first-open chest behavior, keep world and UI presentation timing separate, but treat them as one input-blocking sequence with explicit ownership handoff.

## 2026-05-14 - NPC Feature UI Opened Before Dialogue Blocker Release

Context:
After moving dialogue input blocking into `DialogueService` through `GameFlowInputBlocker`, the Upgrade NPC feature stopped opening its popup.

Cause:
`UpgradeFeature.Execute()` called `UpgradeManager.ToggleUI()` before requesting dialogue exit. The dialogue blocker was still active, so `UpgradeManager.OpenUI()` failed the `UIManager.CanOpenUI(...)` gate before its own open-presentation blocker could take ownership.

Fix:
Request dialogue exit first, then wait until dialogue playback and external UI input blockers are released before opening Upgrade UI.

Prevention:
NPC features that open stack UI after dialogue should not open the UI while dialogue is still the active game-flow blocker. End or hand off the dialogue flow first, then open the feature UI.
