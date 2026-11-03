# Merchant Permanent Affection Rewards

- Mode: Implementation, approved in chat on 2026-11-01.
- Status: Completed; full manual Play Mode and Player build remain unexecuted.
- Scope: Replace Sasha's temporary discounts with four permanent reward tiers and the approved always-visible two-line world price tag. Donation box and other NPC content are excluded.

## Approved Behavior And Initial Values

1. One randomly selected stocked item receives an additional integer 10–20% discount. Selection/rate stay with that stock, including sold state and reentry; a stock refresh can select again.
2. On defeat/time-out, retain up to 100 currently held gold for the next run. Save the pending credit in the profile, consume it once at run start, and never mix it with magic stones. Victory/abort do not generate a new credit.
3. Eligible Rare/Epic relic candidates receive relative selection weights 1.10/1.15 versus normal 1.00. Existing unlock/exclusion/category rules remain intact; these are not absolute probability-point increases.
4. All merchant goods receive 5% global affection discount. Apply this and the per-item special discount multiplicatively from the original price, rounding only the final amount.

Saved affection remains the permanent entitlement source. Existing slots receive unlocked tiers automatically, including slots already at affection 3. Max affection becomes 4. Tier-3 conversations reuse the existing high-affection Ink candidates so the fourth reward remains earnable.

Conversation follow-up (2026-11-02): [cumulative dialogue pool](npc-dialogue-cumulative-pool.md) supersedes the stage-specific candidate configuration above. Earlier paths stay unlocked, the redundant tier-3 copies are removed, and maximum-affection choice conversations continue without gains/indicator. Reward values and ownership remain unchanged.

## Assets And Ownership

- Approved UI scope: existing ShopSlot/ShopSlot_RunGold prefabs, cloned authored original-price and special-offer text, and serialized bindings. No runtime UI hierarchy construction.
- Approved data: Sasha reward assets/NPCData, additive profile/stock save fields, existing five-language tables/CSV.
- Preserve user-adjusted RunMerchantGroup and scenes, all existing GUIDs/enum values/asmdefs/bootstrap and other in-progress edits. Preimages are captured in `outputs/merchant-rewards-2026-11-01/baseline/`.
- Keep modifier aggregation, run start/end commit, stock ownership and UI projection in their existing owners. No new manager or singleton.
- The original merchant setup menu must use the new four-tier configuration rather than restore the former three discounts.

## Success And Verification

- Verify tier entitlements after reload, legacy profile defaults, one-shot gold transfer and run-end reasons, stable single special offer across DTO restore/repricing/reentry, no price compounding, and weighted eligible relic selection.
- Verify original/current prices and special badge on imported prefabs, sold/empty states, real purchase-price agreement, localization, and native rendered world layout.
- Keep the user's open Editor and real save slots untouched. Author/import and validate in an isolated short-path project with matching source/metadata and exact asset dependency closure; copy back only whitelisted assets after baseline/concurrent-edit guards. Never copy isolated Packages, ProjectSettings, scenes, fonts, Addressables or merchant placement. Run generated-project MSBuild with missing new sources explicitly covered by an external import rather than editing generated project files.
- Report full manual gameplay/Player build separately if unexecuted.

Related: [NPC loop](npc-run-affection-dialogue.md), [profile ownership](../Architecture/ProfileSaveOwnershipArchitecture.md), [run gold/shop map](../StructureMemory/RunGoldAndShop.md).

## Completed Evidence

- Unity 6000.4.2f1: 301/301 native checks, exit 0; real imported merchant/slot/native text capture visually inspected. Coverage includes reload-derived tiers, eight Ink candidate bindings, defeat/time-out/victory/abort, capped one-shot credit, stable special offers, non-compounding prices, seeded rarity selection, 25 locale values and sold/empty presentation.
- Whitelisted copyback/preservation audit: 78 checks, including concurrent-edit guards, exact user-adjusted merchant prefab hash, previous translation/key preservation, runtime GUID maps and unchanged existing prefab objects except approved slot authoring plus TMP's canonical font-material assignment.
- MSBuild source build passed with all new sources covered through an external import; generated project files were not edited. Evidence and the final native capture are in [outputs](../../outputs/merchant-rewards-2026-11-01/). See the [session](../SessionLogs/2026-11-01.md) for exact build/manual limits and the documentation impact check.


## Balance Follow-up — 2026-11-02

- User raised tier-2 gold retention from the original 100 to 500 to make the next-run benefit meaningful. Current policy is min(held gold, 500) after Defeat/TimeOver; previously saved credits retain their captured amount. Reward asset/fallback, setup defaults, five localized descriptions/inventory and existing validation expectations are aligned. Other rewards, one-shot transfer, lobby credit display and existing save ownership remain unchanged. The original 301-check receipt above describes the November 1 baseline, not a new native run. See the current [session](../SessionLogs/2026-11-02.md).
