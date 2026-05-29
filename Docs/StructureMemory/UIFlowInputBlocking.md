---
status: active
authority: structure-memory
category: system-map
last_reviewed: 2026-05-29
---

# UI Flow Input Blocking

## Purpose

Capture the current game-flow input blocking structure so future chest, dialogue, upgrade, reward, or other flow work can start without rediscovering the ownership chain.

This is a fast context map, not the final UI architecture source of truth.

## Current Structure

- `UIManager` owns central UI open policy, ESC handling, popup stack state, hover/prompt hiding, and player control lock application.
- `GlobalUIRoot` owns the persistent global UI hierarchy and canvas adoption boundary.
- Lower global UI panels such as `SettingsPanelUI` and `KeyBindingPanelUI` keep static instance handles as lookup caches under the `GlobalUIRoot` / `UIManager` ownership policy; they do not decide to replace an existing persistent representative.
- `GameFlowInputBlocker` is the lifecycle wrapper used by gameplay flows to acquire/release temporary external UI input blocks.
- Stack UI screens still express their own opened-state gameplay lock through `IStackableUI.GameplayLockProfile`.
- The blocker mainly covers flow gaps and opening presentations where no stack UI should be opened by unrelated input.

## Key Files

- `Assets/LeeJunMo/Script/UIStructure/UIManager.cs`
- `Assets/LeeJunMo/Script/UIStructure/GlobalUIRoot.cs`
- `Assets/LeeJunMo/Script/UIStructure/SettingsPanelUI.cs`
- `Assets/LeeJunMo/Script/UIStructure/KeyBindingPanelUI.cs`
- `Assets/LeeJunMo/Script/UIStructure/GameFlowInputBlocker.cs`
- `Assets/LeeJunMo/Script/Dialogue/DialogueService.cs`
- `Assets/LeeJunMo/Script/Dialogue/BossEncounterDirector.cs`
- `Assets/LeeJunMo/Script/Dialogue/BossTalkManager.cs`
- `Assets/Script/Enemy/Boss/FSM/Core/BossDeathPresentation.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/PlayerHealth/GameOverPresentationController.cs`
- `Assets/LeeJunMo/Script/Tutorial/TutorialCombatIntroSequence.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/NPCFeature/Merchant/MerchantActivationCinematic.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Characters/Runtime/PlayerHubSpawnPresentation2D.cs`
- `Assets/LeeJunMo/Script/SceneManagement/BossRewardObjectRevealPresentation.cs`
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
- Persistent global panels are adopted under `GlobalUIRoot`; scene-local duplicate panels must not destroy a valid persistent panel to win their own static instance slot.
- `SettingsPanelUI.EnsureInstance()` and `KeyBindingPanelUI.EnsureInstance()` should return the current valid instance or search existing authored panels. The ownership decision belongs to the root/UI manager layer, not to child-panel replacement rules.

## Current Applied Flows

- Chest first open starts blocking from `TreasureChest` interaction and hands off through the inventory/chest reveal presentation.
- Chest reroll hold runs inside the already-open `InventoryScreen` / `ChestScreen` stack UI. Hold timing/progress should come from `HoldActionButton`, while `ChestScreen` owns refresh eligibility and reroll presentation. It should rely on the stack UI lock plus `ChestScreen` first-open reveal guard instead of creating a new external `GameFlowInputBlocker`.
- Dialogue blocking is owned by `DialogueService` while dialogue is playing.
- Boss encounter presentation blocking is owned by `BossEncounterDirector` for the outer camera/transition/dialogue/return sequence. Legacy `BossTalkManager` follows the same rule.
- Boss death presentation, game-over return presentation, tutorial combat intro, merchant activation cinematic, hub spawn fall/wake presentation, and boss reward portal reveal each own a blocker for their flow window.
- Upgrade open fade is executed by `UpgradeUiOpenFlow`; it blocks unrelated UI input, then opens `UpgradeTreeUI` through the owned push path.
- Reward open presentation blocks unrelated UI input until the open presentation finishes.
- Affection reward UI can open as a flow-owned popup during dialogue/encounter blocking, then its close callback resumes the waiting dialogue tag. While open, `RewardDisplayUI` asks `DialogueService` to make the captured Reward canvas and shared non-raycasting Hover canvas temporarily visible so reward item detail hover can render even though Dialogue suppression still owns the rest of the non-dialogue UI.
- Flowering Bloom cut-in blocks unrelated UI input while it owns a short combat time freeze, so pause/menu freeze owners cannot enter during the cut-in and later restore a stale `Time.timeScale = 0`.
- Stable stack UI close paths are not flow-blocked. Pause, Settings, KeyBinding, Inventory, Chest, Reward, Upgrade, and Encyclopedia should continue to close through their normal stack UI ESC policy outside a protected flow window.

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
- If a stack UI freezes time while `Time.timeScale` is already `0`, its cached restore value may belong to another freeze owner. UI restore must not write that stale zero over a later non-zero restore.
- Do not let a scene-local global UI child destroy `Instance.gameObject` when a persistent child already exists. If the scene-local root is later destroyed as a duplicate, both the old persistent child and the attempted replacement can disappear.
- Do not mix EventSystem selected-object or pad selected-visual policy into global UI ownership fixes; selected-state behavior is a separate input/presentation policy.

## Verification Notes

- Visual Studio MSBuild successfully compiled the implementation after the blocker refactor.
- Manual play verification previously confirmed the blocker behavior and the Upgrade UI dialogue handoff work without observed issues. The later `UpgradeUiOpenFlow` split was source-verified by Codex and user-confirmed through Editor/import/play verification.
- Unity batchmode was not run during the original work because Unity Editor processes were open.

## Promotion Candidate

Candidate for future `Docs/Architecture/` or `Docs/Contracts/` promotion after more flows adopt the same pattern and the team wants this to become official UI flow policy.
