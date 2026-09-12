---
status: active
authority: structure-memory
category: feature-map
last_reviewed: 2026-09-12
---

# Quest HUD

## Purpose and Authored Layout

QuestHudView projects existing quest-owner state into a vertical list. It does not store quest progress or add a quest manager.

The authored root is Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab > GameplayHUDCanvas > QuestHUD. Adjust its RectTransform (initial top-left offset 36, -240) to position it below Player HUD. Heading displays 퀘스트. QuestRows contains the inactive QuestRowTemplate with title and description TMP fields. Runtime rows clone that authored template; no Canvas or text hierarchy is synthesized at runtime.

## State and Lifecycle

- On enable, the view binds PlayerRuntimeRegistry.CurrentPlayer and subscribes registration changes. It subscribes the player's RelicInventory.OnChanged.
- One or more ParcelRelicDefinition instances project one parcel_delivery row: title 파셀의 소포 배달; description 다음 층으로 소포를 배달하세요! Removing the last parcel removes that row. Parcel receipt, delivery, and failure remain owned by the existing parcel/relic systems.
- ShowQuest(id, title, description) adds another quest below existing rows or updates the same id. Additional quest systems must publish their own current state through this entry point. There is currently one integrated quest source: parcels.
- Entry moves from screen-left to the authored local position using unscaled OutCubic easing. Removal raises the row by 12 units, slides it left, then removes it and eases following rows upward. Re-showing a leaving id cancels removal. Heading hides after the final row exits.
- QuestHudRowView owns its text references and active tween. Disable/unbind releases subscriptions, kills tweens, and destroys generated rows. On enable, parcel state is rebuilt from the current inventory. Future quest sources must likewise republish after view re-enable; rows are presentation, not saved quest state.
- IDefaultHudVisibilityTarget integrates with the existing HUD visibility owner.

## Key Files and Extension Points

- Assets/_Project/Runtime/UI/HUD/QuestHudView.cs
- Assets/_Project/Runtime/UI/HUD/QuestHudRowView.cs
- Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab
- Assets/_Project/Editor/Tools/CombatFeelAndQuestInstaller.cs (explicit Editor authoring command, not runtime bootstrap)
- Assets/_Project/Tests/PlayMode/Procedural/CombatFeelLootQuestPlayModeTests.cs

## Known Limits and Promotion

Rows use the authored fixed height; increase the template height for longer future quest content. Only the parcel quest is wired to gameplay today. This small projection remains StructureMemory, not a new Architecture/Contract.

## Current main/sub presentation (2026-09-12 revision)

This section supersedes the original single 퀘스트 heading/title-plus-body layout above.

- GlobalUIRoot > GameplayHUDCanvas > QuestHUD retains its authored position. MainQuestGroup has orange 메인 퀘스트 and white MainQuestDescription; SubQuestHeading is dark sky blue 서브 퀘스트 and QuestRows displays only white descriptions. Every displayed text uses Galmuri9 SDF BlackOutline.
- QuestHudView projects current SceneManager active scene plus this run's slime/dragon/shadow defeat flags, using the authored mainQuestRoutes catalog to match normal corridor/boss locations. ResolveMainQuestText is a stateless projection, not quest-state storage. Tutorial/hub/Grand Hall/final route text follows the user-authored progression recorded in SessionLogs.
- Main group enters from the left when first shown; changed objective text updates in place. Main tween is killed and pose restored on disable. Run-only parcel state still drives one subquest row; titleLabel is hidden and multiple subquests can use ShowQuest as before.
- Main group, main description, subheading and catalog references are authored in the prefab. CombatFeelAndQuestInstaller.ConfigureQuestSections upgrades or creates the same structure. Subheading hides while no subquests exist; row exits and subsequent reflow retain the existing animation.
- Read-only current-run flags prevent previous runs' durable boss-clear achievements from advancing the new run's portal instructions. No new quest manager/save DTO is introduced.
