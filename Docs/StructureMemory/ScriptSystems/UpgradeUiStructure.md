---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-05-18
---

# Upgrade UI Structure

## Purpose

Map the upgrade tree UI navigation and purchase feedback flow after the overflow-arrow and warning-popup additions.

## Current Structure

- `UpgradeTreeUI` builds node buttons and lines from `UpgradeManager.GetAllUpgrades()`.
- `UpgradeTreeUI` owns content sizing, clamp/pan behavior, lake presentation hooks, and optional authored overflow arrow buttons.
- `UpgradeSlotUI` owns node visual state and forwards click attempts to `UpgradeManager`.
- `UpgradeManager.TryBuyUpgrade(...)` is the public purchase entry point. Failed purchases are mapped to `WarningPopupCode`; successful purchases continue through `UpgradePurchaseCompletionService`.
- `UIManager.ShowWarning(WarningPopupCode)` remains the shared warning popup entry point.

## Overflow Navigation

- The horizontal scrollbar reference is cleared by `UpgradeTreeUI.ConfigureScrollRect()`.
- If an older scene instance still has a serialized horizontal scrollbar, `UpgradeTreeUI.ConfigureScrollRect()` disables the scrollbar object before clearing the reference.
- Four optional authored buttons can be assigned: left, right, up, and down.
- `Assets/LeeJunMo/Prefab/UI/Upgrade/UpgradeTreePanel.prefab` currently carries four inactive overflow arrow buttons wired into `UpgradeTreeUI`.
- The previous `Scrollbar Horizontal` subtree has been removed from the main upgrade panel prefab; do not re-add a horizontal scrollbar unless the navigation model changes again.
- Active arrows are shown only when the content can move farther in that direction.
- Arrow clicks move content by `gridCellSize * overflowArrowBlockCells` and then reuse the existing clamp logic.
- Active arrows oscillate along their own direction using unscaled time.
- `Tools/Validation/Scene Setup Validator` checks inactive upgrade panels too, reports missing overflow arrow references, reports stale `ScrollRect.horizontalScrollbar` references, and can detach/disable the stale horizontal scrollbar through Auto Fix.

## Warning Feedback

- `UpgradePurchaseService` still owns purchase validation and failure reasons.
- `UpgradeManager` maps failure reasons to:
  - `UpgradeNotEnoughMagicStone`
  - `UpgradeLocked`
  - `UpgradeUnavailable`
- Locked slots keep their lock/gray visual state but leave the button interactable so clicking a locked node can display the warning.

## Key Files

- `Assets/LeeJunMo/Script/Dialogue/NPC/NPCFeature/Upgrade/UpgradeTreeUI.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/NPCFeature/Upgrade/UpgradeSlotUI.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/NPCFeature/Upgrade/UpgradeManager.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/NPCFeature/Upgrade/UpgradePurchaseService.cs`
- `Assets/LeeJunMo/Script/UIStructure/UIManager.cs`
- `Assets/LeeJunMo/Script/UIStructure/WarningPopupCode.cs`
- `Assets/LeeJunMo/Prefab/UI/Upgrade/UpgradeTreePanel.prefab`

## Known Pitfalls

- The arrow controls are authoring-backed. Other upgrade panel variants or scene overrides may still need their own button wiring if they do not use `UpgradeTreePanel.prefab` as-is.
- `Assets/Scenes/LEeJunmo.unity` and `Assets/Scenes/LEeJunmo 1.unity` still contain legacy upgrade panel data from missing old prefab GUID `f323f5e4b95e66b46aa688b37b914038`. They are not build-enabled as of 2026-05-17, but they should be replaced with the current `UpgradeTreePanel.prefab` before being used as verified content.
- The arrow movement assumes the existing centered content/clamp model. If the content anchor/pivot model changes, re-check direction mapping.
- Locked nodes are now clickable for feedback. Verify cursor, hover, and disabled-looking presentation together so players understand the click shows a reason rather than purchases the node.
- Warning strings live in `UIManager.ResolveWarningMessage(...)`; avoid feature-local popup text unless the shared warning path becomes insufficient.
- The lake preview editor loop uses `Resources.FindObjectsOfTypeAll`, so it must ignore persistent Prefab Asset objects before preview restore or generated layer creation. Otherwise Unity can reject `Transform.SetParent(...)` under a Prefab Asset and leave the Inspector in a broken repaint state.

## Promotion Candidate

Keep this as StructureMemory until the upgrade UI authoring/presentation contract is stable enough for an Architecture or Contract update.
