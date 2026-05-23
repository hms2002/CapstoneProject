---
status: active
authority: structure-memory
category: system-map
last_reviewed: 2026-05-20
---

# UI Flow Input Blocking

## Purpose

Capture the current game-flow input blocking structure so future chest, dialogue, upgrade, reward, or other flow work can start without rediscovering the ownership chain.

This is a fast context map, not the final UI architecture source of truth.

## Current Structure

- `UIManager` owns central UI open policy, ESC handling, popup stack state, hover/prompt hiding, and player control lock application.
- `GameFlowInputBlocker` is the lifecycle wrapper used by gameplay flows to acquire/release temporary external UI input blocks.
- Stack UI screens still express their own opened-state gameplay lock through `IStackableUI.GameplayLockProfile`.
- The blocker mainly covers flow gaps and opening presentations where no stack UI should be opened by unrelated input.

## Key Files

- `Assets/LeeJunMo/Script/UIStructure/UIManager.cs`
- `Assets/LeeJunMo/Script/UIStructure/GameFlowInputBlocker.cs`
- `Assets/LeeJunMo/Script/Dialogue/DialogueService.cs`
- `Assets/LeeJunMo/Script/Dialogue/BossEncounterDirector.cs`
- `Assets/LeeJunMo/Script/Dialogue/BossTalkManager.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Inventory/Chest/Runtime/TreasureChest.cs`
- `Assets/HeoMinSeok/_Project/Scripts/UI/Inventory/Chest/ChestFirstOpenRevealPresentation.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/NPCFeature/Upgrade/UpgradeManager.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/NPCFeature/Upgrade/UpgradeUiOpenFlow.cs`
- `Assets/LeeJunMo/Script/UIStructure/RewardDisplayUI.cs`

## Ownership And Lifecycle

- A flow component gets a blocker with `GameFlowInputBlocker.GetOrAdd(this)`.
- The flow calls `Acquire()` when unrelated ESC/new UI/player control input should be blocked.
- The flow calls `Release()` when the protected flow window ends.
- `GameFlowInputBlocker` releases automatically from `OnDisable` and `OnDestroy`.
- A blocked flow that must open its own stack UI uses `GameFlowInputBlocker.TryPushOwnedUI(...)`.
- `UIManager.TryPushUIForExternalBlockOwner(...)` is the UIManager-side owner exception path.
- A popup that is explicitly part of the active flow, but not owned by the same blocker component, can use the flow-owned UI path through a dedicated service. Current example: affection reward UI opens via `RewardDisplayService.ShowFlowOwnedReward(...)`.

## Current Applied Flows

- Chest first open starts blocking from `TreasureChest` interaction and hands off through the inventory/chest reveal presentation.
- Chest reroll hold runs inside the already-open `InventoryScreen` / `ChestScreen` stack UI. It should rely on the stack UI lock and `ChestScreen` first-open reveal guard instead of creating a new external `GameFlowInputBlocker`.
- Dialogue blocking is owned by `DialogueService` while dialogue is playing.
- Boss encounter presentation blocking is owned by `BossEncounterDirector` for the outer camera/transition/dialogue/return sequence. Legacy `BossTalkManager` follows the same rule.
- Upgrade open fade is executed by `UpgradeUiOpenFlow`; it blocks unrelated UI input, then opens `UpgradeTreeUI` through the owned push path.
- Reward open presentation blocks unrelated UI input until the open presentation finishes.
- Affection reward UI can open as a flow-owned popup during dialogue/encounter blocking, then its close callback resumes the waiting dialogue tag.

## Extension Entry Points

- For a new flow gap before stack UI opens, add a `GameFlowInputBlocker` to the flow owner and wrap only the protected window.
- For a flow that opens its own UI while blocked, use `TryPushOwnedUI`.
- For a flow-owned popup opened by a service rather than by the blocker owner itself, use a narrow service/API handoff instead of calling the normal `TryPushUI` path.
- For an already-open stack UI, prefer `IStackableUI.GameplayLockProfile` instead of adding a flow blocker.
- If several flows need a shared handoff rule, update this document and consider an Architecture/Contract promotion.

## Known Pitfalls

- Do not call `UIManager.SetExternalUiInputBlocked(...)` directly from feature code; use `GameFlowInputBlocker`.
- Do not open feature UI while a dialogue-owned blocker is still active. Request dialogue exit first, then open after dialogue and external UI blockers release.
- Do not detach a dialogue continuation from an affection reward close callback to avoid a blocker deadlock. If the reward popup is part of the current dialogue flow, open it through the flow-owned reward path.
- Do not assume dialogue blocking covers an outer encounter/cinematic sequence. The outer flow owner must block its non-dialogue camera, transition, and handoff windows.
- Do not merge chest world-object presentation timing with chest UI reveal timing. Keep timing separate, but block input across the full first-open sequence when needed.
- Interrupted flows must release their blocker from cleanup paths.

## Verification Notes

- Visual Studio MSBuild successfully compiled the implementation after the blocker refactor.
- Manual play verification previously confirmed the blocker behavior and the Upgrade UI dialogue handoff work without observed issues. The later `UpgradeUiOpenFlow` split was source-verified by Codex and user-confirmed through Editor/import/play verification.
- Unity batchmode was not run during the original work because Unity Editor processes were open.

## Promotion Candidate

Candidate for future `Docs/Architecture/` or `Docs/Contracts/` promotion after more flows adopt the same pattern and the team wants this to become official UI flow policy.
