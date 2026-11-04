# Approved UI skin — integration evidence

- Unity6000.4.2f1: native authoring/import/compile exit0; final isolated Play Mode validation exit0,433 checks,26 native GPU captures (stats5, potion1, reward4 variants×5 locales).
- [Actual rendered overview](unity-applied-overview.png): Aseprite composition of unscaled Unity captures. This is a disposable validation fixture, not a full game scene screenshot; stat values are test data. Text uses the existing game font/material/localization, so the close button retains its actual localized label.
- [Native assertions](native-validation.txt):13 stats and live attribute updates, potion acquisition/use and36px centering, reward open/close callback exactly once, text bounds, multiple/unlock slot containment,5 locale stability and nonblank native renders.
- [Serialized preservation](serialized-preservation.json): six prefabs, no missing local file references, no original font/material changes, no unrelated GlobalUIRoot block changes. Two accidental Encyclopedia localization-material overrides from native loading were restored from the before-edit snapshot; the authoring tool also preserves unrelated nested instance disk blocks before future saves. No shared font assets were edited.
- Both PNG sheets and both Aseprite masters match the approved source SHA256 hashes. Point filtering, full-rect slices, no mipmaps, uncompressed, PPU100; native render verifies the9-slice corner artwork.
- MSBuild `Editor.csproj /t:Build /p:Configuration=Debug`: exit0,0 errors,164 existing warnings. The generated csproj includes both new Editor sources. Native and MSBuild were run sequentially, with no generated project edits.

## Existing regression results and limits

[Play Mode XML](playmode.xml):75 selected,73 passed,2 failed,0 skipped (Unity test runner exit2).

- `PotionRewardPlayModeTests`, `InputHudCleanupPlayModeTests`, `ChestRevealWidthRegressionPlayModeTests`, `PlayerInventorySlotReusePlayModeTests`: all selected cases passed.
- `PlayerControlAndChestRegressionPlayModeTests.ChestSelection_ReservesDistinctSlots_AndRejectsWholeSelectionWhenOnlyOneFits`: expects Korean `드래그`, receives the correctly localized Chinese warning from the saved language. The test calls the unchanged transfer planner with temporary containers; it does not load any changed UI prefab.
- `PlayerControlAndChestRegressionPlayModeTests.WeaponFeedback_MarkOutlineAndKeyFollowAvailabilityAndRebinding`: exact `Color` equality fails although rounded RGBA values match. It uses the unchanged LightningSpearMark prefab/material, not these UI prefabs. This failure was already recorded in [the earlier placement scope](../../ActiveTasks/lightning-spear-placement-structure.md).

Suggested Later: make these two existing assertions locale-safe/tolerant in their own test-maintenance scope. No unrelated runtime/test fixes were bundled here. Import also emits existing Cinemachine/obsolete API warnings and missing-reference diagnostics for the untouched legacy GlobalUIRoot_Water copy; the modified main root has no missing local references in the preservation report.

Full hub dialogue continuation and reward service queue integration, pointer-driven hover/click interaction across all resolutions, and a standalone Player build were not manually run. The isolated runner invokes the existing authored button callback; it does not grant upgrades/affection rewards or prove the whole hub flow.

## Artifacts

- [Player stats](stats-ko.png), [potion HUD](potion-ko.png), [upgrade popup](upgrade-ko.png), [affection popup](affection-ko.png).
- [Four rewards / English](multiple-en.png), [weapon/relic unlock](unlock-ko.png).
- Full26 captures and native/build logs: `Library/CodexApprovedUiSkin/` (local, regenerable).
- Task: [approved scope](../../ActiveTasks/player-stats-potion-reward-skin.md). Session: [2026-10-31](../2026-10-31.md).

Doc Impact: SessionLog + existing InventoryAndChestUIStructure/UpgradeUiStructure. No new ErrorLog/DecisionLog/RefactorBacklog entry, Architecture/Contracts edit or Presentation HTML stale candidate.
