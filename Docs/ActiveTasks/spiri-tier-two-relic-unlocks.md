# Spiri tier-two relic unlocks

Mode: Implementation. User approved the three-relic reward and preview on 2026-11-05.

Status: complete for the approved data/localization implementation. Core MSBuild exit 0 and source/reference/localization inspection completed. Unity import, actual reward window, acquisition rolls, save/reload and Player build remain unexecuted; no automated tests ran.

Goal: Spiri affection level 2 unlocks Fire Dragon's Fang (`RD_ScorchingSong`), Sun Fragment (`RD_SunFragment`), and A Stiff Drink (`RD_DrunkenRush`) for future eligible chest/shop rolls. Use the existing three-icon unlock reward UI with the approved summary.

Allowed: new `UnlockItemAffectionEffect` asset and its new meta identity; Spiri NPC reward binding; remove only these three relics from ItemDatabase's default-unlocked list while retaining allRelics; one localization key in CSV/inventory/shared/five tables/generated asset-key map; narrow task and memory docs.

Preserve: level-1 reward, all relic identities/effects/icons, existing saved unlocks, fixed or already generated stock/rewards, UI prefab/layout/tooltip behavior, current-run and run-end save ownership. No initial inventory grant, forced drop, migration that relocks saved items, new runtime system, scene/prefab/schema/bootstrap change, Architecture/Contracts/Presentation HTML rewrite.

Success: level 1 remains registered; level 2 binds exactly these three relics through the existing unlock effect; new profiles do not start with them unlocked; saved unlock IDs remain compatible; the existing popup creates three tooltip-enabled icon slots and shows the new localized summary.

Verification: source/reference/diff and localization consistency inspection; MSBuild for the existing generated project covering the asset-key-map change. No automated tests or Unity Play Mode checks authorized. Report Unity import and manual reward/acquisition/save-reload acceptance as not executed.

Compatibility: all three relics were previously default unlocked. Existing profiles that saved those IDs retain access even below affection 2. Their prior unlocks are not revoked, and no retroactive popup is forced for profiles already above the threshold.
