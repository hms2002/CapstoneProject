---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-07-04
---

# Shop Structure

## Purpose

Map the merchant shop slot, stock roll, and purchase presentation flow so future shop work starts from the current prefab-slot boundary.

## Current Structure

- `MerchantNPC` is the scene-facing shop owner and public purchase/refresh entry point.
- `ShopDefinitionSO` owns availability, visible slot count, max weapon/consumable caps, stock weights, prices, and refresh policy.
- `MerchantRunStateService` owns per-run stock state and sold/refresh state.
- `ShopInventoryRoll` owns weapon/relic/consumable stock selection and now respects per-slot `ShopSlotItemFilter`.
- `ShopSlot` owns interaction, highlight/detail display, price display, sold presentation, and its stock filter. Its authored `priceText` field is a generic `Component`; text value, preferred-width, and mesh refresh requests go through Core `TextPresentationBinding` so shop gameplay source does not directly reference TextMeshPro.
- `MerchantRefreshInteractable` owns refresh interaction and button animation. Its authored `remainingCountText` field is a generic `Component` and updates through Core `TextPresentationBinding`, so the merchant gameplay source does not directly reference TextMeshPro for this count label.
- `MerchantPurchaseService` owns item grant/currency purchase checks and failure result types.

## Prefab Slot Authoring

- Preferred setup: place empty anchor transforms in the shop layout, assign `MerchantNPC.slotPrefab`, then add ordered `slotAnchors`.
- The reusable source prefab is `Assets/LeeJunMo/Prefab/Dialogue/ShopSlot.prefab`.
- Each anchor carries a `ShopSlotItemFilter`: `Any`, `Weapon`, `Relic`, or `Consumable`.
- At runtime, `MerchantNPC` instantiates/reuses one `ShopSlot` under each anchor, resets its local transform, applies the anchor filter, binds owner/index, and rolls stock for visible slots.
- Existing scene-copied child `ShopSlot` objects remain a compatibility fallback when prefab-slot authoring is not configured.
- `Assets/Scenes/ProtoTypeHub.unity` and `Assets/Scenes/ProtoTypeHub 1.unity` currently use empty `ShopSlotAnchors` children wired to the shared `ShopSlot.prefab`, with filters ordered `Weapon`, `Relic`, `Consumable`, then `Any`.
- Unity Editor migration path: open `Tools/Merchant/ShopSlot Prefab Migration`, select a `MerchantNPC`, review copied slot order, apply the Weapon / Relic / Consumable pattern or per-slot filters, then create empty anchors and replace copied slots through Undo-backed scene edits.
- `Tools/Validation/Scene Setup Validator` reports merchant shops that still use copied scene slots, missing `slotPrefab`/`slotAnchors`, null anchors, non-shared slot prefabs, or prefab anchors without the requested Weapon / Relic / Consumable filter split.

## Key Files

- `Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/MerchantNPC.cs`
- `Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/ShopSlot.cs`
- `Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/MerchantRefreshInteractable.cs`
- `Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/ShopInventoryRoll.cs`
- `Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/ShopDefinitionSO.cs`
- `Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/MerchantPurchaseService.cs`
- `Assets/_Project/Runtime/Core/Presentation/TextPresentationBinding.cs`
- `Assets/LeeJunMo/Script/Editor/SceneSetupValidatorWindow.cs` (`ValidateMerchantShops`, `MerchantShopSlotPrefabMigrationWindow`)
- `Assets/LeeJunMo/Prefab/Dialogue/ShopSlot.prefab`
- `Assets/Scenes/ProtoTypeHub.unity`
- `Assets/Scenes/ProtoTypeHub 1.unity`

## Known Pitfalls

- `ShopDefinitionSO.MaxWeaponSlots` and `MaxConsumableSlots` still cap typed slots. If a scene displays more weapon/consumable anchors than those caps allow, extra filtered slots can be empty.
- `slotAnchors` order is the display and runtime state order. Reordering anchors changes which saved run-state slot index maps to each visual slot.
- `OnValidate` does not instantiate prefab slots. Prefab-slot setup must be tested in play/import, not only by inspecting `shopSlots`.
- Do not solve shop layout by duplicating live `ShopSlot` scene objects again unless intentionally using the legacy fallback.
- The two prototype hub scenes were migrated by controlled YAML rewrite on 2026-05-17 after scene-reference risk review. Unity Editor import/hierarchy review is still required because Codex did not observe Unity reserialization or playmode shop creation.
- Run `Tools/Validation/Scene Setup Validator` after migration so the scene catches stale copied slots and missing typed filters before playtesting.

## Promotion Candidate

This is a structure-memory map only. Promote to an Architecture or Contract document if the shop authoring workflow becomes a project-wide source-of-truth rule.
