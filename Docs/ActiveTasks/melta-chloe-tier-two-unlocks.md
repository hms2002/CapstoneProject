# Melta/Chloe tier-two relic unlocks and Spiri reward icon

Mode: Implementation. User approved 2026-11-05.

Status: implemented. Editor MSBuild (including Gameplay/Core) exit 0; source/reference/locale/image-byte/diff inspection completed. No automated tests, Unity import/Editor compilation, actual popup/acquisition/save-reload or Player build executed.

Goal: replace only Spiri's level-1 reward icon with the supplied PNG. Melta affection 2 unlocks `RD_SlimeBottle`; Chloe affection 2 unlocks `RD_ChloeLantern`, one relic each through existing UnlockItemAffectionEffect and item-tooltip reward slots.

Allowed: supplied icon copy/new meta, existing Spiri reward icon binding and authoring default, two new unlock-effect assets/metas, existing NPC tier-2 registrations, two localization keys/5 locales/map, ItemManager's additive restoration of already-earned unlock effects from committed profile affection, narrow docs.

Preserve: existing rewards (Melta levels 1 and 5, Chloe level 1, Spiri levels 1/2), relic identities/effects/icons, basic unlock lists (these two relics are already registered only in allRelics), current stock, existing saved unlocks, save readiness/commit ownership, UI/scene/prefab/schema/asmdef/bootstrap and existing GUIDs. No immediate inventory grant or forced drop, no retroactive popup, no save migration or actual user-save editing.

Success: correct supplied icon in the Spiri reward asset and authoring menu; one appropriate relic in each tier-2 effect; already qualifying profiles restore missed unlocks without replaying other effects/UI, with normal ready-manager save collection; future eligible chest/shop pools use those IDs. Fresh profiles retain locked status until earned.

Verification: source/reference/diff/localization/image-byte inspection; generated Editor project MSBuild includes changed authoring plus Gameplay ItemManager and Core map. Automated tests and Unity Play Mode checks are not authorized; import/popup/acquisition/save-reload require later manual verification.
